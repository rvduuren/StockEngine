namespace Warehouse.StockEngine.Infrastructure.MariaDb.Sql;

internal static class MoveQueries
{
    public const string DecreaseSource = StockQueries.DecreaseQuantityIfSufficient;
    public const string IncreaseDestination = StockQueries.IncreaseQuantity;
}
