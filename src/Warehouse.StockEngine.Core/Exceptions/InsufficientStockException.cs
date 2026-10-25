namespace Warehouse.StockEngine.Core.Exceptions;

public sealed class InsufficientStockException : Exception
{
    public InsufficientStockException(int warehouse, int zone, int bin, int product, decimal requested, decimal available)
        : base($"Insufficient stock for {warehouse}/{zone}/{bin}/{product}: requested {requested}, available {available}.")
    {
        Warehouse = warehouse;
        Zone = zone;
        Bin = bin;
        Product = product;
        Requested = requested;
        Available = available;
    }

    public int Warehouse { get; }
    public int Zone { get; }
    public int Bin { get; }
    public int Product { get; }
    public decimal Requested { get; }
    public decimal Available { get; }
}
