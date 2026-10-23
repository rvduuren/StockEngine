namespace Warehouse.StockEngine.Core.Exceptions;

public sealed class InsufficientStockException : Exception
{
    public InsufficientStockException(string warehouseCode, string locationCode, string itemCode, decimal requested, decimal available)
        : base($"Insufficient stock for {warehouseCode}/{locationCode}/{itemCode}: requested {requested}, available {available}.")
    {
        WarehouseCode = warehouseCode;
        LocationCode = locationCode;
        ItemCode = itemCode;
        Requested = requested;
        Available = available;
    }

    public string WarehouseCode { get; }
    public string LocationCode { get; }
    public string ItemCode { get; }
    public decimal Requested { get; }
    public decimal Available { get; }
}
