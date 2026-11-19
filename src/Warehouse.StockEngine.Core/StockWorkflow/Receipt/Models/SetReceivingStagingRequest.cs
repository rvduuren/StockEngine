namespace Warehouse.StockEngine.Core.StockWorkflow.Receipt.Models;

public sealed class SetReceivingStagingRequest
{
    public int WarehouseId { get; init; }
    public int ZoneId { get; init; }
    public int BinId { get; init; }
}