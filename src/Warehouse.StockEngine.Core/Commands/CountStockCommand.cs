namespace Warehouse.StockEngine.Core.Commands;

public sealed record CountStockCommand(
    Guid TransactionId,
    string DocumentNo,
    IReadOnlyList<CountStockLine> Lines);

public sealed record CountStockLine(
    string DocumentLineId,
    int Warehouse,
    int Zone,
    int Bin,
    long Sku,
    decimal CountedQuantity);
