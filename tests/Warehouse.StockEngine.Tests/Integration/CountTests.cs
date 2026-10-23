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
        var item = $"SKU-CNT-{Guid.NewGuid():N}"[..20];
        await _fixture.Engine.InboundAsync(new InboundStockCommand(
            Guid.NewGuid(),
            "IN-CNT-SETUP",
            [new InboundStockLine("1", "WH1", "D-01", item, 10m)]));

        var result = await _fixture.Engine.CountAsync(new CountStockCommand(
            Guid.NewGuid(),
            "CNT-001",
            [new CountStockLine("1", "WH1", "D-01", item, 6m)]));

        Assert.False(result.WasAlreadyProcessed);
        Assert.Equal(1, result.HistoryRowsWritten);
        Assert.Equal(6m, await _fixture.Engine.GetQuantityAsync("WH1", "D-01", item));
    }

    [Fact]
    public async Task Count_CanIncreaseAboveCurrent()
    {
        var item = $"SKU-CNT2-{Guid.NewGuid():N}"[..20];
        await _fixture.Engine.InboundAsync(new InboundStockCommand(
            Guid.NewGuid(),
            "IN-CNT2-SETUP",
            [new InboundStockLine("1", "WH1", "D-02", item, 1m)]));

        await _fixture.Engine.CountAsync(new CountStockCommand(
            Guid.NewGuid(),
            "CNT-002",
            [new CountStockLine("1", "WH1", "D-02", item, 25m)]));

        Assert.Equal(25m, await _fixture.Engine.GetQuantityAsync("WH1", "D-02", item));
    }
}
