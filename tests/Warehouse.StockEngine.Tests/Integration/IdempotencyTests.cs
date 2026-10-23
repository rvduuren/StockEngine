using Warehouse.StockEngine.Core.Commands;
using Warehouse.StockEngine.Core.Exceptions;
using Warehouse.StockEngine.Tests.Fixtures;

namespace Warehouse.StockEngine.Tests.Integration;

[Collection(nameof(MariaDbCollection))]
public sealed class IdempotencyTests
{
    private readonly MariaDbTestcontainerFixture _fixture;

    public IdempotencyTests(MariaDbTestcontainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SameTransactionId_SamePayload_IsIdempotent()
    {
        var transactionId = Guid.NewGuid();
        var sku = UniqueSku();
        var command = new InboundStockCommand(
            transactionId,
            "IDEM-001",
            [new InboundStockLine("1", 1, 50, 500, sku, 9m)]);

        var first = await _fixture.Engine.InboundAsync(command);
        var second = await _fixture.Engine.InboundAsync(command);

        Assert.False(first.WasAlreadyProcessed);
        Assert.True(second.WasAlreadyProcessed);
        Assert.Equal(first.HistoryRowsWritten, second.HistoryRowsWritten);
        Assert.Equal(9m, await _fixture.Engine.GetQuantityAsync(1, 50, 500, sku));
    }

    [Fact]
    public async Task SameTransactionId_DifferentPayload_Throws()
    {
        var transactionId = Guid.NewGuid();
        var sku = UniqueSku();

        await _fixture.Engine.InboundAsync(new InboundStockCommand(
            transactionId,
            "CONF-001",
            [new InboundStockLine("1", 1, 50, 501, sku, 4m)]));

        await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            _fixture.Engine.InboundAsync(new InboundStockCommand(
                transactionId,
                "CONF-001",
                [new InboundStockLine("1", 1, 50, 501, sku, 8m)])));

        Assert.Equal(4m, await _fixture.Engine.GetQuantityAsync(1, 50, 501, sku));
    }

    private static long UniqueSku() => Random.Shared.NextInt64(1_000_000, 9_000_000_000);
}
