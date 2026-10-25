using Warehouse.StockEngine.Core.Commands;
using Warehouse.StockEngine.Core.Exceptions;
using Warehouse.StockEngine.Tests.Fixtures;

namespace Warehouse.StockEngine.Tests.Integration;

[Collection(nameof(MariaDbCollection))]
public sealed class OutboundTests
{
    private readonly MariaDbTestcontainerFixture _fixture;

    public OutboundTests(MariaDbTestcontainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Outbound_DecreasesQuantity()
    {
        var product = UniqueProduct();
        await _fixture.Engine.InboundAsync(new InboundStockCommand(
            Guid.NewGuid(),
            "IN-OUT-SETUP",
            [new InboundStockLine("1", 1, 20, 200, product, 20m)]));

        var result = await _fixture.Engine.OutboundAsync(new OutboundStockCommand(
            Guid.NewGuid(),
            "OUT-001",
            [new OutboundStockLine("1", 1, 20, 200, product, 7m)]));

        Assert.False(result.WasAlreadyProcessed);
        Assert.Equal(1, result.HistoryRowsWritten);
        Assert.Equal(13m, await _fixture.Engine.GetQuantityAsync(1, 20, 200, product));
    }

    [Fact]
    public async Task Outbound_RejectsNegativeStock()
    {
        var product = UniqueProduct();
        await _fixture.Engine.InboundAsync(new InboundStockCommand(
            Guid.NewGuid(),
            "IN-OUT-NEG",
            [new InboundStockLine("1", 1, 20, 201, product, 3m)]));

        await Assert.ThrowsAsync<InsufficientStockException>(() =>
            _fixture.Engine.OutboundAsync(new OutboundStockCommand(
                Guid.NewGuid(),
                "OUT-NEG",
                [new OutboundStockLine("1", 1, 20, 201, product, 5m)])));

        Assert.Equal(3m, await _fixture.Engine.GetQuantityAsync(1, 20, 201, product));
    }

    private static int UniqueProduct() => Random.Shared.Next(20, 30);
}
