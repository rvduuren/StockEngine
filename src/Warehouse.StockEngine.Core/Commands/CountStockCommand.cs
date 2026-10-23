namespace Warehouse.StockEngine.Core.Commands;

public sealed record CountStockCommand(
    Guid TransactionId,
    string DocumentNo,
    IReadOnlyList<CountStockLine> Lines);

public sealed record CountStockLine(
    string DocumentLineId,
    string WarehouseCode,
    string LocationCode,
    string ItemCode,
    decimal CountedQuantity);
