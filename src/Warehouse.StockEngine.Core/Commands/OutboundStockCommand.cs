namespace Warehouse.StockEngine.Core.Commands;

public sealed record OutboundStockCommand(
    Guid TransactionId,
    string DocumentNo,
    IReadOnlyList<OutboundStockLine> Lines);

public sealed record OutboundStockLine(
    string DocumentLineId,
    string WarehouseCode,
    string LocationCode,
    string ItemCode,
    decimal Quantity);
