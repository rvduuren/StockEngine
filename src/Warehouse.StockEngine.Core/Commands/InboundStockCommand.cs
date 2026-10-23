namespace Warehouse.StockEngine.Core.Commands;

public sealed record InboundStockCommand(
    Guid TransactionId,
    string DocumentNo,
    IReadOnlyList<InboundStockLine> Lines);

public sealed record InboundStockLine(
    string DocumentLineId,
    string WarehouseCode,
    string LocationCode,
    string ItemCode,
    decimal Quantity);
