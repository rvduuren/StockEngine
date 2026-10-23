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
        var sku = UniqueSku();
        await _fixture.Engine.InboundAsync(new InboundStockCommand(
            Guid.NewGuid(),
            "IN-CNT-SETUP",
            [new InboundStockLine("1", 1, 40, 400, sku, 10m)]));

        var result = await _fixture.Engine.CountAsync(new CountStockCommand(
            Guid.NewGuid(),
            "CNT-001",
            [new CountStockLine("1", 1, 40, 400, sku, 6m)]));

        Assert.False(result.WasAlreadyProcessed);
        Assert.Equal(1, result.HistoryRowsWritten);
        Assert.Equal(6m, await _fixture.Engine.GetQuantityAsync(1, 40, 400, sku));
    }

    [Fact]
    public async Task Count_CanIncreaseAboveCurrent()
    {
        var sku = UniqueSku();
        await _fixture.Engine.InboundAsync(new InboundStockCommand(
            Guid.NewGuid(),
            "IN-CNT2-SETUP",
            [new InboundStockLine("1", 1, 40, 401, sku, 1m)]));

        await _fixture.Engine.CountAsync(new CountStockCommand(
            Guid.NewGuid(),
            "CNT-002",
            [new CountStockLine("1", 1, 40, 401, sku, 25m)]));

        Assert.Equal(25m, await _fixture.Engine.GetQuantityAsync(1, 40, 401, sku));
    }

    private static long UniqueSku() => Random.Shared.NextInt64(1_000_000, 9_000_000_000);
}
