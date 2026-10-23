namespace Warehouse.StockEngine.Core.Exceptions;

public sealed class InvalidStockCommandException : Exception
{
    public InvalidStockCommandException(string message) : base(message)
    {
    }
}
