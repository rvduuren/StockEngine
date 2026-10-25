using static Warehouse.StockEngine.Infrastructure.MariaDb.Execution.MariaDbStockEngine;

namespace Warehouse.StockEngine.Tests.Unit;

public sealed class InventoryKeyTests
{
    [Fact]
    public void CompareTo_SortsByWarehouseFirst()
    {
        var key1 = new InventoryKey(Warehouse: 1, Zone: 9, Bin: 99, Product: 9999);
        var key2 = new InventoryKey(Warehouse: 2, Zone: 1, Bin: 1, Product: 1);

        Assert.True(key1.CompareTo(key2) < 0);
        Assert.True(key2.CompareTo(key1) > 0);
    }

    [Fact]
    public void CompareTo_SortsByZoneSecond()
    {
        var key1 = new InventoryKey(Warehouse: 1, Zone: 1, Bin: 99, Product: 9999);
        var key2 = new InventoryKey(Warehouse: 1, Zone: 2, Bin: 1, Product: 1);

        Assert.True(key1.CompareTo(key2) < 0);
        Assert.True(key2.CompareTo(key1) > 0);
    }

    [Fact]
    public void CompareTo_SortsByBinThird()
    {
        var key1 = new InventoryKey(Warehouse: 1, Zone: 1, Bin: 10, Product: 9999);
        var key2 = new InventoryKey(Warehouse: 1, Zone: 1, Bin: 20, Product: 1);

        Assert.True(key1.CompareTo(key2) < 0);
        Assert.True(key2.CompareTo(key1) > 0);
    }

    [Fact]
    public void CompareTo_SortsByProductFourth()
    {
        var key1 = new InventoryKey(Warehouse: 1, Zone: 1, Bin: 10, Product: 100);
        var key2 = new InventoryKey(Warehouse: 1, Zone: 1, Bin: 10, Product: 200);

        Assert.True(key1.CompareTo(key2) < 0);
        Assert.True(key2.CompareTo(key1) > 0);
    }

    [Fact]
    public void CompareTo_EqualKeys_ReturnsZero()
    {
        var key1 = new InventoryKey(Warehouse: 1, Zone: 1, Bin: 10, Product: 100);
        var key2 = new InventoryKey(Warehouse: 1, Zone: 1, Bin: 10, Product: 100);

        Assert.Equal(0, key1.CompareTo(key2));
    }
}
