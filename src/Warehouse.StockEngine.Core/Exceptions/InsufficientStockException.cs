namespace Warehouse.StockEngine.Core.Exceptions;

public sealed class InsufficientStockException : Exception
{
    public InsufficientStockException(int warehouse, int zone, int bin, long sku, decimal requested, decimal available)
        : base($"Insufficient stock for {warehouse}/{zone}/{bin}/{sku}: requested {requested}, available {available}.")
    {
        Warehouse = warehouse;
        Zone = zone;
        Bin = bin;
        Sku = sku;
        Requested = requested;
        Available = available;
    }

    public int Warehouse { get; }
    public int Zone { get; }
    public int Bin { get; }
    public long Sku { get; }
    public decimal Requested { get; }
    public decimal Available { get; }
}
