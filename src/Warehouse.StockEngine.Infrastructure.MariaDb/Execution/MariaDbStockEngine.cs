using System.Data.Common;
using Dapper;
using MySqlConnector;
using Warehouse.StockEngine.Core.Commands;
using Warehouse.StockEngine.Core.Enums;
using Warehouse.StockEngine.Core.Exceptions;
using Warehouse.StockEngine.Core.Interfaces;
using Warehouse.StockEngine.Core.Models;
using Warehouse.StockEngine.Infrastructure.MariaDb.Sql;

namespace Warehouse.StockEngine.Infrastructure.MariaDb.Execution;

public sealed class MariaDbStockEngine : IStockEngine
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MariaDbStockEngine(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<StockResult> InboundAsync(InboundStockCommand command, CancellationToken cancellationToken = default)
    {
        CommandValidator.Validate(command);
        return ExecuteAsync(
            command.TransactionId,
            RequestHasher.Hash(command),
            MovementType.Inbound,
            async (conn, tx) =>
            {
                var historyRows = 0;
                foreach (var line in command.Lines)
                {
                    await EnsureRowAsync(conn, tx, line.Warehouse, line.Zone, line.Bin, line.Sku, cancellationToken);
                    var before = await LockQuantityAsync(conn, tx, line.Warehouse, line.Zone, line.Bin, line.Sku, cancellationToken);
                    await conn.ExecuteAsync(new CommandDefinition(
                        InboundQueries.Apply,
                        new
                        {
                            Quantity = line.Quantity,
                            line.Warehouse,
                            line.Zone,
                            line.Bin,
                            line.Sku
                        },
                        tx,
                        cancellationToken: cancellationToken));

                    var after = before + line.Quantity;
                    await InsertHistoryAsync(
                        conn, tx,
                        command.TransactionId, command.DocumentNo, line.DocumentLineId,
                        MovementType.Inbound,
                        line.Warehouse, line.Zone, line.Bin, line.Sku,
                        line.Quantity, before, after,
                        cancellationToken);
                    historyRows++;
                }

                return historyRows;
            },
            cancellationToken);
    }

    public Task<StockResult> OutboundAsync(OutboundStockCommand command, CancellationToken cancellationToken = default)
    {
        CommandValidator.Validate(command);
        return ExecuteAsync(
            command.TransactionId,
            RequestHasher.Hash(command),
            MovementType.Outbound,
            async (conn, tx) =>
            {
                var historyRows = 0;
                foreach (var line in command.Lines)
                {
                    await EnsureRowAsync(conn, tx, line.Warehouse, line.Zone, line.Bin, line.Sku, cancellationToken);
                    var before = await LockQuantityAsync(conn, tx, line.Warehouse, line.Zone, line.Bin, line.Sku, cancellationToken);

                    var affected = await conn.ExecuteAsync(new CommandDefinition(
                        OutboundQueries.Apply,
                        new
                        {
                            Quantity = line.Quantity,
                            line.Warehouse,
                            line.Zone,
                            line.Bin,
                            line.Sku
                        },
                        tx,
                        cancellationToken: cancellationToken));

                    if (affected == 0)
                    {
                        throw new InsufficientStockException(
                            line.Warehouse, line.Zone, line.Bin, line.Sku, line.Quantity, before);
                    }

                    var after = before - line.Quantity;
                    await InsertHistoryAsync(
                        conn, tx,
                        command.TransactionId, command.DocumentNo, line.DocumentLineId,
                        MovementType.Outbound,
                        line.Warehouse, line.Zone, line.Bin, line.Sku,
                        -line.Quantity, before, after,
                        cancellationToken);
                    historyRows++;
                }

                return historyRows;
            },
            cancellationToken);
    }

    public Task<StockResult> MoveAsync(MoveStockCommand command, CancellationToken cancellationToken = default)
    {
        CommandValidator.Validate(command);
        return ExecuteAsync(
            command.TransactionId,
            RequestHasher.Hash(command),
            MovementType.Move,
            async (conn, tx) =>
            {
                var historyRows = 0;
                foreach (var line in command.Lines)
                {
                    var fromFirst = CompareLocation(line.Warehouse, line.FromZone, line.FromBin, line.Warehouse, line.ToZone, line.ToBin) <= 0;
                    if (fromFirst)
                    {
                        await EnsureRowAsync(conn, tx, line.Warehouse, line.FromZone, line.FromBin, line.Sku, cancellationToken);
                        await EnsureRowAsync(conn, tx, line.Warehouse, line.ToZone, line.ToBin, line.Sku, cancellationToken);
                        await LockQuantityAsync(conn, tx, line.Warehouse, line.FromZone, line.FromBin, line.Sku, cancellationToken);
                        await LockQuantityAsync(conn, tx, line.Warehouse, line.ToZone, line.ToBin, line.Sku, cancellationToken);
                    }
                    else
                    {
                        await EnsureRowAsync(conn, tx, line.Warehouse, line.ToZone, line.ToBin, line.Sku, cancellationToken);
                        await EnsureRowAsync(conn, tx, line.Warehouse, line.FromZone, line.FromBin, line.Sku, cancellationToken);
                        await LockQuantityAsync(conn, tx, line.Warehouse, line.ToZone, line.ToBin, line.Sku, cancellationToken);
                        await LockQuantityAsync(conn, tx, line.Warehouse, line.FromZone, line.FromBin, line.Sku, cancellationToken);
                    }

                    var fromBefore = await GetLockedQuantityAsync(conn, tx, line.Warehouse, line.FromZone, line.FromBin, line.Sku, cancellationToken);
                    var toBefore = await GetLockedQuantityAsync(conn, tx, line.Warehouse, line.ToZone, line.ToBin, line.Sku, cancellationToken);

                    var affected = await conn.ExecuteAsync(new CommandDefinition(
                        MoveQueries.DecreaseSource,
                        new
                        {
                            Quantity = line.Quantity,
                            line.Warehouse,
                            Zone = line.FromZone,
                            Bin = line.FromBin,
                            line.Sku
                        },
                        tx,
                        cancellationToken: cancellationToken));

                    if (affected == 0)
                    {
                        throw new InsufficientStockException(
                            line.Warehouse, line.FromZone, line.FromBin, line.Sku, line.Quantity, fromBefore);
                    }

                    await conn.ExecuteAsync(new CommandDefinition(
                        MoveQueries.IncreaseDestination,
                        new
                        {
                            Quantity = line.Quantity,
                            line.Warehouse,
                            Zone = line.ToZone,
                            Bin = line.ToBin,
                            line.Sku
                        },
                        tx,
                        cancellationToken: cancellationToken));

                    var fromAfter = fromBefore - line.Quantity;
                    var toAfter = toBefore + line.Quantity;

                    await InsertHistoryAsync(
                        conn, tx,
                        command.TransactionId, command.DocumentNo, line.DocumentLineId,
                        MovementType.Move,
                        line.Warehouse, line.FromZone, line.FromBin, line.Sku,
                        -line.Quantity, fromBefore, fromAfter,
                        cancellationToken);
                    historyRows++;

                    await InsertHistoryAsync(
                        conn, tx,
                        command.TransactionId, command.DocumentNo, line.DocumentLineId,
                        MovementType.Move,
                        line.Warehouse, line.ToZone, line.ToBin, line.Sku,
                        line.Quantity, toBefore, toAfter,
                        cancellationToken);
                    historyRows++;
                }

                return historyRows;
            },
            cancellationToken);
    }

    public Task<StockResult> CountAsync(CountStockCommand command, CancellationToken cancellationToken = default)
    {
        CommandValidator.Validate(command);
        return ExecuteAsync(
            command.TransactionId,
            RequestHasher.Hash(command),
            MovementType.Count,
            async (conn, tx) =>
            {
                var historyRows = 0;
                foreach (var line in command.Lines)
                {
                    await EnsureRowAsync(conn, tx, line.Warehouse, line.Zone, line.Bin, line.Sku, cancellationToken);
                    var before = await LockQuantityAsync(conn, tx, line.Warehouse, line.Zone, line.Bin, line.Sku, cancellationToken);

                    await conn.ExecuteAsync(new CommandDefinition(
                        CountQueries.Apply,
                        new
                        {
                            Quantity = line.CountedQuantity,
                            line.Warehouse,
                            line.Zone,
                            line.Bin,
                            line.Sku
                        },
                        tx,
                        cancellationToken: cancellationToken));

                    var after = line.CountedQuantity;
                    var delta = after - before;
                    await InsertHistoryAsync(
                        conn, tx,
                        command.TransactionId, command.DocumentNo, line.DocumentLineId,
                        MovementType.Count,
                        line.Warehouse, line.Zone, line.Bin, line.Sku,
                        delta, before, after,
                        cancellationToken);
                    historyRows++;
                }

                return historyRows;
            },
            cancellationToken);
    }

    public async Task<decimal> GetQuantityAsync(
        int warehouse,
        int zone,
        int bin,
        long sku,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var quantity = await connection.QuerySingleOrDefaultAsync<decimal?>(new CommandDefinition(
            StockQueries.GetQuantity,
            new { Warehouse = warehouse, Zone = zone, Bin = bin, Sku = sku },
            cancellationToken: cancellationToken));

        return quantity ?? 0m;
    }

    private async Task<StockResult> ExecuteAsync(
        Guid transactionId,
        string requestHash,
        MovementType movementType,
        Func<DbConnection, DbTransaction, Task<int>> work,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var claim = await TryClaimAsync(connection, transaction, transactionId, requestHash, movementType, cancellationToken);
            if (!claim.IsNew)
            {
                await transaction.CommitAsync(cancellationToken);
                return new StockResult(transactionId, WasAlreadyProcessed: true, claim.ExistingHistoryRows);
            }

            var historyRows = await work(connection, transaction);
            await transaction.CommitAsync(cancellationToken);
            return new StockResult(transactionId, WasAlreadyProcessed: false, historyRows);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<(bool IsNew, int ExistingHistoryRows)> TryClaimAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid transactionId,
        string requestHash,
        MovementType movementType,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                StockQueries.ClaimTransaction,
                new
                {
                    TransactionId = transactionId.ToString(),
                    RequestHash = requestHash,
                    MovementType = movementType.ToString()
                },
                transaction,
                cancellationToken: cancellationToken));

            return (true, 0);
        }
        catch (MySqlException ex) when (ex.ErrorCode is MySqlErrorCode.DuplicateKeyEntry)
        {
            var existingHash = await connection.QuerySingleAsync<string>(new CommandDefinition(
                StockQueries.GetTransactionHash,
                new { TransactionId = transactionId.ToString() },
                transaction,
                cancellationToken: cancellationToken));

            if (!string.Equals(existingHash, requestHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new IdempotencyConflictException(transactionId);
            }

            var historyRows = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                StockQueries.CountHistoryRows,
                new { TransactionId = transactionId.ToString() },
                transaction,
                cancellationToken: cancellationToken));

            return (false, historyRows);
        }
    }

    private static async Task EnsureRowAsync(
        DbConnection connection,
        DbTransaction transaction,
        int warehouse,
        int zone,
        int bin,
        long sku,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            StockQueries.EnsureInventoryRow,
            new { Warehouse = warehouse, Zone = zone, Bin = bin, Sku = sku },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<decimal> LockQuantityAsync(
        DbConnection connection,
        DbTransaction transaction,
        int warehouse,
        int zone,
        int bin,
        long sku,
        CancellationToken cancellationToken)
    {
        return await connection.QuerySingleAsync<decimal>(new CommandDefinition(
            StockQueries.LockQuantity,
            new { Warehouse = warehouse, Zone = zone, Bin = bin, Sku = sku },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static Task<decimal> GetLockedQuantityAsync(
        DbConnection connection,
        DbTransaction transaction,
        int warehouse,
        int zone,
        int bin,
        long sku,
        CancellationToken cancellationToken)
        => LockQuantityAsync(connection, transaction, warehouse, zone, bin, sku, cancellationToken);

    private static int CompareLocation(int warehouseA, int zoneA, int binA, int warehouseB, int zoneB, int binB)
    {
        var warehouseCmp = warehouseA.CompareTo(warehouseB);
        if (warehouseCmp != 0)
        {
            return warehouseCmp;
        }

        var zoneCmp = zoneA.CompareTo(zoneB);
        return zoneCmp != 0 ? zoneCmp : binA.CompareTo(binB);
    }

    private static Task InsertHistoryAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid transactionId,
        string documentNo,
        string documentLineId,
        MovementType movementType,
        int warehouse,
        int zone,
        int bin,
        long sku,
        decimal quantityDelta,
        decimal quantityBefore,
        decimal quantityAfter,
        CancellationToken cancellationToken)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            StockQueries.InsertHistory,
            new
            {
                TransactionId = transactionId.ToString(),
                DocumentNo = documentNo,
                DocumentLineId = documentLineId,
                MovementType = movementType.ToString(),
                Warehouse = warehouse,
                Zone = zone,
                Bin = bin,
                Sku = sku,
                QuantityDelta = quantityDelta,
                QuantityBefore = quantityBefore,
                QuantityAfter = quantityAfter
            },
            transaction,
            cancellationToken: cancellationToken));
    }
}
