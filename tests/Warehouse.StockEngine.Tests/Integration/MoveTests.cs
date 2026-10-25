using Dapper;
using Warehouse.StockEngine.Core.Commands;
using Warehouse.StockEngine.Core.Exceptions;
using Warehouse.StockEngine.Tests.Fixtures;

namespace Warehouse.StockEngine.Tests.Integration;

[Collection(nameof(MariaDbCollection))]
public sealed class MoveTests
{
    private readonly MariaDbTestcontainerFixture _fixture;

    public MoveTests(MariaDbTestcontainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Move_TransfersQuantity_AndWritesTwoHistoryRowsPerLine()
    {
        var product = UniqueProduct();
        await _fixture.Engine.InboundAsync(new InboundStockCommand(
            Guid.NewGuid(),
            "IN-MV-SETUP",
            [new InboundStockLine("1", 1, 30, 300, product, 12m)]));

        var transactionId = Guid.NewGuid();
        var result = await _fixture.Engine.MoveAsync(new MoveStockCommand(
            transactionId,
            "MV-001",
            [new MoveStockLine("1", 1, 30, 300, 30, 301, product, 4m)]));

        Assert.False(result.WasAlreadyProcessed);
        Assert.Equal(2, result.HistoryRowsWritten);
        Assert.Equal(8m, await _fixture.Engine.GetQuantityAsync(1, 30, 300, product));
        Assert.Equal(4m, await _fixture.Engine.GetQuantityAsync(1, 30, 301, product));

        await using var connection = _fixture.ConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        var historyCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM inventory_history WHERE transaction_id = @Id",
            new { Id = transactionId.ToString() });
        Assert.Equal(2, historyCount);
    }

    [Fact]
    public async Task Move_RejectsInsufficientSourceStock()
    {
        var product = UniqueProduct();
        await _fixture.Engine.InboundAsync(new InboundStockCommand(
            Guid.NewGuid(),
            "IN-MV-NEG",
            [new InboundStockLine("1", 1, 30, 302, product, 2m)]));

        await Assert.ThrowsAsync<InsufficientStockException>(() =>
            _fixture.Engine.MoveAsync(new MoveStockCommand(
                Guid.NewGuid(),
                "MV-NEG",
                [new MoveStockLine("1", 1, 30, 302, 30, 303, product, 10m)])));

        Assert.Equal(2m, await _fixture.Engine.GetQuantityAsync(1, 30, 302, product));
        Assert.Equal(0m, await _fixture.Engine.GetQuantityAsync(1, 30, 303, product));
    }

    private static int UniqueProduct() => Random.Shared.Next(20, 30);
}
