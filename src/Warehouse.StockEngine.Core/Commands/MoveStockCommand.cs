namespace Warehouse.StockEngine.Core.Commands;

public sealed record MoveStockCommand(
    Guid TransactionId,
    string DocumentNo,
    IReadOnlyList<MoveStockLine> Lines);

public sealed record MoveStockLine(
    string DocumentLineId,
    string WarehouseCode,
    string FromLocationCode,
    string ToLocationCode,
    string ItemCode,
    decimal Quantity);
