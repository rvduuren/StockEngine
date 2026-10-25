using Warehouse.StockEngine.Core.Commands;
using Warehouse.StockEngine.Tests.Fixtures;

namespace Warehouse.StockEngine.Tests.Integration;

[Collection(nameof(MariaDbCollection))]
public sealed class ConcurrencyTests
{
    private readonly MariaDbTestcontainerFixture _fixture;

    public ConcurrencyTests(MariaDbTestcontainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Concurrent_MultiLine_Moves_InOppositeOrder_DoNotDeadlock()
    {
        var product1 = UniqueProduct();
        var product2 = UniqueProduct();

        const int warehouse = 1;
        const int zone = 10;
        const int binA = 100;
        const int binB = 200;
        const int binC = 300;
        const int binD = 400;

        // Seed stock on all 4 bins
        await _fixture.Engine.InboundAsync(new InboundStockCommand(
            Guid.NewGuid(),
            "SEED-CONC",
            [
                new InboundStockLine("S1", warehouse, zone, binA, product1, 500m),
                new InboundStockLine("S2", warehouse, zone, binB, product1, 500m),
                new InboundStockLine("S3", warehouse, zone, binC, product2, 500m),
                new InboundStockLine("S4", warehouse, zone, binD, product2, 500m)
            ]));

        const int taskCount = 20;
        var barrier = new Barrier(taskCount);

        var tasks = Enumerable.Range(0, taskCount).Select(i => Task.Run(async () =>
        {
            barrier.SignalAndWait(); // Synchronize all threads so they hit MariaDB simultaneously

            if (i % 2 == 0)
            {
                // Forward order: A -> B, then C -> D
                await _fixture.Engine.MoveAsync(new MoveStockCommand(
                    Guid.NewGuid(),
                    $"MV-FWD-{i}",
                    [
                        new MoveStockLine("1", warehouse, FromZone: zone, FromBin: binA, ToZone: zone, ToBin: binB, Product: product1, Quantity: 2m),
                        new MoveStockLine("2", warehouse, FromZone: zone, FromBin: binC, ToZone: zone, ToBin: binD, Product: product2, Quantity: 2m)
                    ]));
            }
            else
            {
                // Inverted order: D -> C, then B -> A
                await _fixture.Engine.MoveAsync(new MoveStockCommand(
                    Guid.NewGuid(),
                    $"MV-REV-{i}",
                    [
                        new MoveStockLine("1", warehouse, FromZone: zone, FromBin: binD, ToZone: zone, ToBin: binC, Product: product2, Quantity: 2m),
                        new MoveStockLine("2", warehouse, FromZone: zone, FromBin: binB, ToZone: zone, ToBin: binA, Product: product1, Quantity: 2m)
                    ]));
            }
        }));

        // All tasks should complete successfully without MySqlException 1213 (Deadlock)
        await Task.WhenAll(tasks);

        // Verify total inventory balance is conserved (500*2 = 1000 for each PRODUCT)
        var totalProduct1 = await _fixture.Engine.GetQuantityAsync(warehouse, zone, binA, product1)
                      + await _fixture.Engine.GetQuantityAsync(warehouse, zone, binB, product1);
        var totalProduct2 = await _fixture.Engine.GetQuantityAsync(warehouse, zone, binC, product2)
                      + await _fixture.Engine.GetQuantityAsync(warehouse, zone, binD, product2);

        Assert.Equal(1000m, totalProduct1);
        Assert.Equal(1000m, totalProduct2);
    }

    [Fact]
    public async Task Concurrent_MultiLine_InboundAndOutbound_InReverseOrder_DoNotDeadlock()
    {
        var product = UniqueProduct();
        const int warehouse = 1;
        const int zone = 20;
        const int binX = 500;
        const int binY = 600;

        // Seed initial stock
        await _fixture.Engine.InboundAsync(new InboundStockCommand(
            Guid.NewGuid(),
            "SEED-INOUT",
            [
                new InboundStockLine("1", warehouse, zone, binX, product, 1000m),
                new InboundStockLine("2", warehouse, zone, binY, product, 1000m)
            ]));

        const int iterations = 10;
        var barrier = new Barrier(iterations * 2);

        var inbounds = Enumerable.Range(0, iterations).Select(i => Task.Run(async () =>
        {
            barrier.SignalAndWait();
            // Inbound touches X then Y
            await _fixture.Engine.InboundAsync(new InboundStockCommand(
                Guid.NewGuid(),
                $"IN-CONC-{i}",
                [
                    new InboundStockLine("1", warehouse, zone, binX, product, 5m),
                    new InboundStockLine("2", warehouse, zone, binY, product, 5m)
                ]));
        }));

        var outbounds = Enumerable.Range(0, iterations).Select(i => Task.Run(async () =>
        {
            barrier.SignalAndWait();
            // Outbound touches Y then X (opposite order!)
            await _fixture.Engine.OutboundAsync(new OutboundStockCommand(
                Guid.NewGuid(),
                $"OUT-CONC-{i}",
                [
                    new OutboundStockLine("1", warehouse, zone, binY, product, 5m),
                    new OutboundStockLine("2", warehouse, zone, binX, product, 5m)
                ]));
        }));

        await Task.WhenAll(inbounds.Concat(outbounds));

        // After equal inbounds and outbounds, stock should remain exactly 1000m each
        var qtyX = await _fixture.Engine.GetQuantityAsync(warehouse, zone, binX, product);
        var qtyY = await _fixture.Engine.GetQuantityAsync(warehouse, zone, binY, product);

        Assert.Equal(1000m, qtyX);
        Assert.Equal(1000m, qtyY);
    }

    private static int UniqueProduct() => Random.Shared.Next(20, 30);
}
