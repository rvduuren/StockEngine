namespace Warehouse.StockEngine.Infrastructure.MariaDb.Sql;

internal static class OutboundQueries
{
    public const string Apply = StockQueries.DecreaseQuantityIfSufficient;
}
