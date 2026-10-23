namespace Warehouse.StockEngine.Core.Commands;

public sealed record CountStockCommand(
    Guid TransactionId,
    string DocumentNo,
    IReadOnlyList<CountStockLine> Lines,
    string? Source = null,
    string? UserId = null,
    string? DeviceId = null,
    string? ReasonCode = null);

public sealed record CountStockLine(
    string DocumentLineId,
    int Warehouse,
    int Zone,
    int Bin,
    long Sku,
    decimal CountedQuantity,
    string? ReasonCode = null);
