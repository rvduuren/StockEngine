namespace Warehouse.StockEngine.Core.Commands;

public sealed record OutboundStockCommand(
    Guid TransactionId,
    string DocumentNo,
    IReadOnlyList<OutboundStockLine> Lines);

public sealed record OutboundStockLine(
    string DocumentLineId,
    int Warehouse,
    int Zone,
    int Bin,
    long Sku,
    decimal Quantity);
