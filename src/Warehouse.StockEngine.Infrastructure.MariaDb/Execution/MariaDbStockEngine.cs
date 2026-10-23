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
                    await EnsureRowAsync(conn, tx, line.WarehouseCode, line.LocationCode, line.ItemCode, cancellationToken);
                    var before = await LockQuantityAsync(conn, tx, line.WarehouseCode, line.LocationCode, line.ItemCode, cancellationToken);
                    await conn.ExecuteAsync(new CommandDefinition(
                        InboundQueries.Apply,
                        new
                        {
                            Quantity = line.Quantity,
                            line.WarehouseCode,
                            line.LocationCode,
                            line.ItemCode
                        },
                        tx,
                        cancellationToken: cancellationToken));

                    var after = before + line.Quantity;
                    await InsertHistoryAsync(
                        conn, tx,
                        command.TransactionId, command.DocumentNo, line.DocumentLineId,
                        MovementType.Inbound,
                        line.WarehouseCode, line.LocationCode, line.ItemCode,
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
                    await EnsureRowAsync(conn, tx, line.WarehouseCode, line.LocationCode, line.ItemCode, cancellationToken);
                    var before = await LockQuantityAsync(conn, tx, line.WarehouseCode, line.LocationCode, line.ItemCode, cancellationToken);

                    var affected = await conn.ExecuteAsync(new CommandDefinition(
                        OutboundQueries.Apply,
                        new
                        {
                            Quantity = line.Quantity,
                            line.WarehouseCode,
                            line.LocationCode,
                            line.ItemCode
                        },
                        tx,
                        cancellationToken: cancellationToken));

                    if (affected == 0)
                    {
                        throw new InsufficientStockException(
                            line.WarehouseCode, line.LocationCode, line.ItemCode, line.Quantity, before);
                    }

                    var after = before - line.Quantity;
                    await InsertHistoryAsync(
                        conn, tx,
                        command.TransactionId, command.DocumentNo, line.DocumentLineId,
                        MovementType.Outbound,
                        line.WarehouseCode, line.LocationCode, line.ItemCode,
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
                    // Deterministic lock order to reduce deadlocks.
                    var firstIsFrom = string.CompareOrdinal(line.FromLocationCode, line.ToLocationCode) <= 0;
                    if (firstIsFrom)
                    {
                        await EnsureRowAsync(conn, tx, line.WarehouseCode, line.FromLocationCode, line.ItemCode, cancellationToken);
                        await EnsureRowAsync(conn, tx, line.WarehouseCode, line.ToLocationCode, line.ItemCode, cancellationToken);
                        await LockQuantityAsync(conn, tx, line.WarehouseCode, line.FromLocationCode, line.ItemCode, cancellationToken);
                        await LockQuantityAsync(conn, tx, line.WarehouseCode, line.ToLocationCode, line.ItemCode, cancellationToken);
                    }
                    else
                    {
                        await EnsureRowAsync(conn, tx, line.WarehouseCode, line.ToLocationCode, line.ItemCode, cancellationToken);
                        await EnsureRowAsync(conn, tx, line.WarehouseCode, line.FromLocationCode, line.ItemCode, cancellationToken);
                        await LockQuantityAsync(conn, tx, line.WarehouseCode, line.ToLocationCode, line.ItemCode, cancellationToken);
                        await LockQuantityAsync(conn, tx, line.WarehouseCode, line.FromLocationCode, line.ItemCode, cancellationToken);
                    }

                    var fromBefore = await GetLockedQuantityAsync(conn, tx, line.WarehouseCode, line.FromLocationCode, line.ItemCode, cancellationToken);
                    var toBefore = await GetLockedQuantityAsync(conn, tx, line.WarehouseCode, line.ToLocationCode, line.ItemCode, cancellationToken);

                    var affected = await conn.ExecuteAsync(new CommandDefinition(
                        MoveQueries.DecreaseSource,
                        new
                        {
                            Quantity = line.Quantity,
                            line.WarehouseCode,
                            LocationCode = line.FromLocationCode,
                            line.ItemCode
                        },
                        tx,
                        cancellationToken: cancellationToken));

                    if (affected == 0)
                    {
                        throw new InsufficientStockException(
                            line.WarehouseCode, line.FromLocationCode, line.ItemCode, line.Quantity, fromBefore);
                    }

                    await conn.ExecuteAsync(new CommandDefinition(
                        MoveQueries.IncreaseDestination,
                        new
                        {
                            Quantity = line.Quantity,
                            line.WarehouseCode,
                            LocationCode = line.ToLocationCode,
                            line.ItemCode
                        },
                        tx,
                        cancellationToken: cancellationToken));

                    var fromAfter = fromBefore - line.Quantity;
                    var toAfter = toBefore + line.Quantity;

                    await InsertHistoryAsync(
                        conn, tx,
                        command.TransactionId, command.DocumentNo, line.DocumentLineId,
                        MovementType.Move,
                        line.WarehouseCode, line.FromLocationCode, line.ItemCode,
                        -line.Quantity, fromBefore, fromAfter,
                        cancellationToken);
                    historyRows++;

                    await InsertHistoryAsync(
                        conn, tx,
                        command.TransactionId, command.DocumentNo, line.DocumentLineId,
                        MovementType.Move,
                        line.WarehouseCode, line.ToLocationCode, line.ItemCode,
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
                    await EnsureRowAsync(conn, tx, line.WarehouseCode, line.LocationCode, line.ItemCode, cancellationToken);
                    var before = await LockQuantityAsync(conn, tx, line.WarehouseCode, line.LocationCode, line.ItemCode, cancellationToken);

                    await conn.ExecuteAsync(new CommandDefinition(
                        CountQueries.Apply,
                        new
                        {
                            Quantity = line.CountedQuantity,
                            line.WarehouseCode,
                            line.LocationCode,
                            line.ItemCode
                        },
                        tx,
                        cancellationToken: cancellationToken));

                    var after = line.CountedQuantity;
                    var delta = after - before;
                    await InsertHistoryAsync(
                        conn, tx,
                        command.TransactionId, command.DocumentNo, line.DocumentLineId,
                        MovementType.Count,
                        line.WarehouseCode, line.LocationCode, line.ItemCode,
                        delta, before, after,
                        cancellationToken);
                    historyRows++;
                }

                return historyRows;
            },
            cancellationToken);
    }

    public async Task<decimal> GetQuantityAsync(
        string warehouseCode,
        string locationCode,
        string itemCode,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var quantity = await connection.QuerySingleOrDefaultAsync<decimal?>(new CommandDefinition(
            StockQueries.GetQuantity,
            new { WarehouseCode = warehouseCode, LocationCode = locationCode, ItemCode = itemCode },
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
        string warehouseCode,
        string locationCode,
        string itemCode,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            StockQueries.EnsureInventoryRow,
            new { WarehouseCode = warehouseCode, LocationCode = locationCode, ItemCode = itemCode },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<decimal> LockQuantityAsync(
        DbConnection connection,
        DbTransaction transaction,
        string warehouseCode,
        string locationCode,
        string itemCode,
        CancellationToken cancellationToken)
    {
        var quantity = await connection.QuerySingleAsync<decimal>(new CommandDefinition(
            StockQueries.LockQuantity,
            new { WarehouseCode = warehouseCode, LocationCode = locationCode, ItemCode = itemCode },
            transaction,
            cancellationToken: cancellationToken));

        return quantity;
    }

    private static Task<decimal> GetLockedQuantityAsync(
        DbConnection connection,
        DbTransaction transaction,
        string warehouseCode,
        string locationCode,
        string itemCode,
        CancellationToken cancellationToken)
        => LockQuantityAsync(connection, transaction, warehouseCode, locationCode, itemCode, cancellationToken);

    private static Task InsertHistoryAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid transactionId,
        string documentNo,
        string documentLineId,
        MovementType movementType,
        string warehouseCode,
        string locationCode,
        string itemCode,
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
                WarehouseCode = warehouseCode,
                LocationCode = locationCode,
                ItemCode = itemCode,
                QuantityDelta = quantityDelta,
                QuantityBefore = quantityBefore,
                QuantityAfter = quantityAfter
            },
            transaction,
            cancellationToken: cancellationToken));
    }
}
