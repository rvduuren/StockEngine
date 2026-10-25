using Dapper;
using Warehouse.StockEngine.Core.Commands;
using Warehouse.StockEngine.Tests.Fixtures;

namespace Warehouse.StockEngine.Tests.Integration;

[Collection(nameof(MariaDbCollection))]
public sealed class CountTests
{
    private readonly MariaDbTestcontainerFixture _fixture;

    public CountTests(MariaDbTestcontainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Count_SetsAbsoluteQuantity()
    {
        var product = UniqueProduct();
        await _fixture.Engine.InboundAsync(new InboundStockCommand(
            Guid.NewGuid(),
            "IN-CNT-SETUP",
            [new InboundStockLine("1", 1, 40, 400, product, 10m)]));

        var result = await _fixture.Engine.CountAsync(new CountStockCommand(
            Guid.NewGuid(),
            "CNT-001",
            [new CountStockLine("1", 1, 40, 400, product, 6m)]));

        Assert.False(result.WasAlreadyProcessed);
        Assert.Equal(1, result.HistoryRowsWritten);
        Assert.Equal(6m, await _fixture.Engine.GetQuantityAsync(1, 40, 400, product));
    }

    [Fact]
    public async Task Count_CanIncreaseAboveCurrent()
    {
        var product = UniqueProduct();
        await _fixture.Engine.InboundAsync(new InboundStockCommand(
            Guid.NewGuid(),
            "IN-CNT2-SETUP",
            [new InboundStockLine("1", 1, 40, 401, product, 1m)]));

        await _fixture.Engine.CountAsync(new CountStockCommand(
            Guid.NewGuid(),
            "CNT-002",
            [new CountStockLine("1", 1, 40, 401, product, 25m)]));

        Assert.Equal(25m, await _fixture.Engine.GetQuantityAsync(1, 40, 401, product));
    }

    [Fact]
    public async Task Count_PersistsCountedQuantity_AndReasonCode()
    {
        var product = UniqueProduct();
        var transactionId = Guid.NewGuid();

        await _fixture.Engine.InboundAsync(new InboundStockCommand(
            Guid.NewGuid(),
            "IN-CNT-REASON",
            [new InboundStockLine("1", 1, 40, 402, product, 10m)],
            Source: "RF",
            UserId: "u-1"));

        await _fixture.Engine.CountAsync(new CountStockCommand(
            transactionId,
            "CNT-REASON",
            [new CountStockLine("1", 1, 40, 402, product, 7m, ReasonCode: "CYCLE")],
            Source: "RF",
            UserId: "u-1",
            DeviceId: "gun-9",
            ReasonCode: "CYCLE-AUDIT"));

        await using var connection = _fixture.ConnectionFactory.CreateConnection();
        await connection.OpenAsync();

        var history = await connection.QuerySingleAsync<(decimal? CountedQuantity, string? ReasonCode, decimal Delta)>(
            """
            SELECT counted_quantity AS CountedQuantity, reason_code AS ReasonCode, quantity_delta AS Delta
            FROM inventory_history
            WHERE transaction_id = @Id
            """,
            new { Id = transactionId.ToString() });

        Assert.Equal(7m, history.CountedQuantity);
        Assert.Equal("CYCLE", history.ReasonCode);
        Assert.Equal(-3m, history.Delta);

        var txn = await connection.QuerySingleAsync<(string Status, string? Source, string? UserId, string? DeviceId, string? ReasonCode, DateTime? CompletedAt)>(
            """
            SELECT status AS Status, source AS Source, user_id AS UserId, device_id AS DeviceId,
                   reason_code AS ReasonCode, completed_at AS CompletedAt
            FROM inventory_transaction
            WHERE transaction_id = @Id
            """,
            new { Id = transactionId.ToString() });

        Assert.Equal("COMPLETED", txn.Status);
        Assert.Equal("RF", txn.Source);
        Assert.Equal("u-1", txn.UserId);
        Assert.Equal("gun-9", txn.DeviceId);
        Assert.Equal("CYCLE-AUDIT", txn.ReasonCode);
        Assert.NotNull(txn.CompletedAt);
    }

    private static int UniqueProduct() => Random.Shared.Next(20, 30);
}
