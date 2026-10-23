namespace Warehouse.StockEngine.Infrastructure.MariaDb.Sql;

internal static class InboundQueries
{
    // Shared stock SQL lives in StockQueries; this type documents the inbound operation surface.
    public const string Apply = StockQueries.IncreaseQuantity;
}
