namespace Warehouse.StockEngine.Core.Commands;

public sealed record InboundStockCommand(
    Guid TransactionId,
    string DocumentNo,
    IReadOnlyList<InboundStockLine> Lines);

public sealed record InboundStockLine(
    string DocumentLineId,
    int Warehouse,
    int Zone,
    int Bin,
    long Sku,
    decimal Quantity);
