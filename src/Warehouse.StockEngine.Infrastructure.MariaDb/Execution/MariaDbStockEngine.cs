using System.Data.Common;
using Dapper;
using MySqlConnector;
using Warehouse.StockEngine.Core.Commands;
using Warehouse.StockEngine.Core.Enums;
using Warehouse.StockEngine.Core.Exceptions;
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
            AuditFrom(command.Source, command.UserId, command.DeviceId, command.ReasonCode),
            async (conn, tx) =>
            {
                var keys = command.Lines.Select(l => new InventoryKey(l.Warehouse, l.Zone, l.Bin, l.Product));
                var balances = await LockLocationsAsync(conn, tx, keys, cancellationToken);

                var historyRows = 0;
                foreach (var line in command.Lines)
                {
                    var key = new InventoryKey(line.Warehouse, line.Zone, line.Bin, line.Product);
                    var before = balances[key];

                    await conn.ExecuteAsync(new CommandDefinition(
                        InboundQueries.Apply,
                        new
                        {
                            Quantity = line.Quantity,
                            line.Warehouse,
                            line.Zone,
                            line.Bin,
                            Product = line.Product
                        },
                        tx,
                        cancellationToken: cancellationToken));

                    var after = before + line.Quantity;
                    balances[key] = after;

                    await InsertHistoryAsync(
                        conn, tx,
                        command.TransactionId, command.DocumentNo, line.DocumentLineId,
                        MovementType.Inbound,
                        line.Warehouse, line.Zone, line.Bin, line.Product,
                        line.Quantity, before, after,
                        countedQuantity: null,
                        reasonCode: null,
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
            AuditFrom(command.Source, command.UserId, command.DeviceId, command.ReasonCode),
            async (conn, tx) =>
            {
                var keys = command.Lines.Select(l => new InventoryKey(l.Warehouse, l.Zone, l.Bin, l.Product));
                var balances = await LockLocationsAsync(conn, tx, keys, cancellationToken);

                var historyRows = 0;
                foreach (var line in command.Lines)
                {
                    var key = new InventoryKey(line.Warehouse, line.Zone, line.Bin, line.Product);
                    var before = balances[key];

                    var affected = await conn.ExecuteAsync(new CommandDefinition(
                        OutboundQueries.Apply,
                        new
                        {
                            Quantity = line.Quantity,
                            line.Warehouse,
                            line.Zone,
                            line.Bin,
                            Product = line.Product
                        },
                        tx,
                        cancellationToken: cancellationToken));

                    if (affected == 0)
                    {
                        throw new InsufficientStockException(
                            line.Warehouse, line.Zone, line.Bin, line.Product, line.Quantity, before);
                    }

                    var after = before - line.Quantity;
                    balances[key] = after;

                    await InsertHistoryAsync(
                        conn, tx,
                        command.TransactionId, command.DocumentNo, line.DocumentLineId,
                        MovementType.Outbound,
                        line.Warehouse, line.Zone, line.Bin, line.Product,
                        -line.Quantity, before, after,
                        countedQuantity: null,
                        reasonCode: null,
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
            AuditFrom(command.Source, command.UserId, command.DeviceId, command.ReasonCode),
            async (conn, tx) =>
            {
                var keys = command.Lines.SelectMany(l => new[]
                {
                    new InventoryKey(l.Warehouse, l.FromZone, l.FromBin, l.Product),
                    new InventoryKey(l.Warehouse, l.ToZone, l.ToBin, l.Product)
                });
                var balances = await LockLocationsAsync(conn, tx, keys, cancellationToken);

                var historyRows = 0;
                foreach (var line in command.Lines)
                {
                    var fromKey = new InventoryKey(line.Warehouse, line.FromZone, line.FromBin, line.Product);
                    var toKey = new InventoryKey(line.Warehouse, line.ToZone, line.ToBin, line.Product);

                    var fromBefore = balances[fromKey];
                    var toBefore = balances[toKey];

                    var affected = await conn.ExecuteAsync(new CommandDefinition(
                        MoveQueries.DecreaseSource,
                        new
                        {
                            Quantity = line.Quantity,
                            line.Warehouse,
                            Zone = line.FromZone,
                            Bin = line.FromBin,
                            Product = line.Product
                        },
                        tx,
                        cancellationToken: cancellationToken));

                    if (affected == 0)
                    {
                        throw new InsufficientStockException(
                            line.Warehouse, line.FromZone, line.FromBin, line.Product, line.Quantity, fromBefore);
                    }

                    await conn.ExecuteAsync(new CommandDefinition(
                        MoveQueries.IncreaseDestination,
                        new
                        {
                            Quantity = line.Quantity,
                            line.Warehouse,
                            Zone = line.ToZone,
                            Bin = line.ToBin,
                            Product = line.Product
                        },
                        tx,
                        cancellationToken: cancellationToken));

                    var fromAfter = fromBefore - line.Quantity;
                    var toAfter = toBefore + line.Quantity;

                    balances[fromKey] = fromAfter;
                    balances[toKey] = toAfter;

                    await InsertHistoryAsync(
                        conn, tx,
                        command.TransactionId, command.DocumentNo, line.DocumentLineId,
                        MovementType.Move,
                        line.Warehouse, line.FromZone, line.FromBin, line.Product,
                        -line.Quantity, fromBefore, fromAfter,
                        countedQuantity: null,
                        reasonCode: null,
                        cancellationToken);
                    historyRows++;

                    await InsertHistoryAsync(
                        conn, tx,
                        command.TransactionId, command.DocumentNo, line.DocumentLineId,
                        MovementType.Move,
                        line.Warehouse, line.ToZone, line.ToBin, line.Product,
                        line.Quantity, toBefore, toAfter,
                        countedQuantity: null,
                        reasonCode: null,
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
            AuditFrom(command.Source, command.UserId, command.DeviceId, command.ReasonCode),
            async (conn, tx) =>
            {
                var keys = command.Lines.Select(l => new InventoryKey(l.Warehouse, l.Zone, l.Bin, l.Product));
                var balances = await LockLocationsAsync(conn, tx, keys, cancellationToken);

                var historyRows = 0;
                foreach (var line in command.Lines)
                {
                    var key = new InventoryKey(line.Warehouse, line.Zone, line.Bin, line.Product);
                    var before = balances[key];

                    await conn.ExecuteAsync(new CommandDefinition(
                        CountQueries.Apply,
                        new
                        {
                            Quantity = line.CountedQuantity,
                            line.Warehouse,
                            line.Zone,
                            line.Bin,
                            Product = line.Product
                        },
                        tx,
                        cancellationToken: cancellationToken));

                    var after = line.CountedQuantity;
                    var delta = after - before;
                    balances[key] = after;

                    await InsertHistoryAsync(
                        conn, tx,
                        command.TransactionId, command.DocumentNo, line.DocumentLineId,
                        MovementType.Count,
                        line.Warehouse, line.Zone, line.Bin, line.Product,
                        delta, before, after,
                        countedQuantity: line.CountedQuantity,
                        reasonCode: line.ReasonCode,
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
        int product,
        CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var quantity = await connection.QuerySingleOrDefaultAsync<decimal?>(new CommandDefinition(
            StockQueries.GetQuantity,
            new { Warehouse = warehouse, Zone = zone, Bin = bin, Product = product },
            cancellationToken: cancellationToken));

        return quantity ?? 0m;
    }

    private async Task<StockResult> ExecuteAsync(
        Guid transactionId,
        string requestHash,
        MovementType movementType,
        TransactionAudit audit,
        Func<DbConnection, DbTransaction, Task<int>> work,
        CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var claim = await TryClaimAsync(connection, transaction, transactionId, requestHash, movementType, audit, cancellationToken);
            if (!claim.IsNew)
            {
                await transaction.CommitAsync(cancellationToken);
                return new StockResult(transactionId, WasAlreadyProcessed: true, claim.ExistingHistoryRows);
            }

            var historyRows = await work(connection, transaction);
            await CompleteTransactionAsync(connection, transaction, transactionId, cancellationToken);
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
        TransactionAudit audit,
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
                    MovementType = movementType.ToString(),
                    audit.Source,
                    audit.UserId,
                    audit.DeviceId,
                    audit.ReasonCode
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

    private static Task CompleteTransactionAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        return connection.ExecuteAsync(new CommandDefinition(
            StockQueries.CompleteTransaction,
            new { TransactionId = transactionId.ToString() },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<Dictionary<InventoryKey, decimal>> LockLocationsAsync(
        DbConnection connection,
        DbTransaction transaction,
        IEnumerable<InventoryKey> keys,
        CancellationToken cancellationToken)
    {
        var sortedUniqueKeys = keys.Distinct().ToList();
        sortedUniqueKeys.Sort();

        var balances = new Dictionary<InventoryKey, decimal>(sortedUniqueKeys.Count);

        foreach (var key in sortedUniqueKeys)
        {
            await EnsureRowAsync(connection, transaction, key.Warehouse, key.Zone, key.Bin, key.Product, cancellationToken);
            var qty = await LockQuantityAsync(connection, transaction, key.Warehouse, key.Zone, key.Bin, key.Product, cancellationToken);
            balances[key] = qty;
        }

        return balances;
    }

    private static async Task EnsureRowAsync(
        DbConnection connection,
        DbTransaction transaction,
        int warehouse,
        int zone,
        int bin,
        long product,
        CancellationToken cancellationToken)
    {
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                StockQueries.EnsureInventoryRow,
                new { Warehouse = warehouse, Zone = zone, Bin = bin, Product = product },
                transaction,
                cancellationToken: cancellationToken));
        }
        catch (MySqlException ex) when (ex.ErrorCode is MySqlErrorCode.DuplicateKeyEntry)
        {
            // Concurrent EnsureRow raced; row exists — continue to FOR UPDATE lock.
        }
    }

    private static async Task<decimal> LockQuantityAsync(
        DbConnection connection,
        DbTransaction transaction,
        int warehouse,
        int zone,
        int bin,
        long product,
        CancellationToken cancellationToken)
    {
        return await connection.QuerySingleAsync<decimal>(new CommandDefinition(
            StockQueries.LockQuantity,
            new { Warehouse = warehouse, Zone = zone, Bin = bin, Product = product },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static Task InsertHistoryAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid transactionId,
        string? documentNo,
        string? documentLineId,
        MovementType movementType,
        int warehouse,
        int zone,
        int bin,
        long product,
        decimal quantityDelta,
        decimal quantityBefore,
        decimal quantityAfter,
        decimal? countedQuantity,
        string? reasonCode,
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
                Product = product,
                QuantityDelta = quantityDelta,
                QuantityBefore = quantityBefore,
                QuantityAfter = quantityAfter,
                CountedQuantity = countedQuantity,
                ReasonCode = reasonCode
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static TransactionAudit AuditFrom(string? source, string? userId, string? deviceId, string? reasonCode)
        => new(source, userId, deviceId, reasonCode);

    private sealed record TransactionAudit(
        string? Source,
        string? UserId,
        string? DeviceId,
        string? ReasonCode);

    internal readonly record struct InventoryKey(int Warehouse, int Zone, int Bin, long Product) : IComparable<InventoryKey>
    {
        public int CompareTo(InventoryKey other)
        {
            var cmp = Warehouse.CompareTo(other.Warehouse);
            if (cmp != 0) return cmp;

            cmp = Zone.CompareTo(other.Zone);
            if (cmp != 0) return cmp;

            cmp = Bin.CompareTo(other.Bin);
            if (cmp != 0) return cmp;

            return Product.CompareTo(other.Product);
        }
    }
}
