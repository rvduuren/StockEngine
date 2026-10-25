using Warehouse.StockEngine.Core.Commands;
using Warehouse.StockEngine.Core.Exceptions;
using Warehouse.StockEngine.Tests.Fixtures;

namespace Warehouse.StockEngine.Tests.Integration;

[Collection(nameof(MariaDbCollection))]
public sealed class InboundTests
{
    private readonly MariaDbTestcontainerFixture _fixture;

    public InboundTests(MariaDbTestcontainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Inbound_IncreasesQuantity_AndWritesHistory()
    {
        var transactionId = Guid.NewGuid();
        var product = UniqueProduct();
        var result = await _fixture.Engine.InboundAsync(new InboundStockCommand(
            transactionId,
            "IN-001",
            [
                new InboundStockLine("1", 1, 10, 100, product, 10m),
                new InboundStockLine("2", 1, 10, 100, product, 5m)
            ]));

        Assert.False(result.WasAlreadyProcessed);
        Assert.Equal(2, result.HistoryRowsWritten);
        Assert.Equal(15m, await _fixture.Engine.GetQuantityAsync(1, 10, 100, product));
    }

    [Fact]
    public async Task Inbound_RejectsNonPositiveQuantity()
    {
        await Assert.ThrowsAsync<InvalidStockCommandException>(() =>
            _fixture.Engine.InboundAsync(new InboundStockCommand(
                Guid.NewGuid(),
                "IN-BAD",
                [new InboundStockLine("1", 1, 10, 100, UniqueProduct(), 0m)])));
    }

    private static int UniqueProduct() => Random.Shared.Next(20, 30);
}
