namespace Warehouse.StockEngine.Core.Commands;

public sealed record MoveStockCommand(
    Guid TransactionId,
    string DocumentNo,
    IReadOnlyList<MoveStockLine> Lines,
    string? Source = null,
    string? UserId = null,
    string? DeviceId = null,
    string? ReasonCode = null);

public sealed record MoveStockLine(
    string DocumentLineId,
    int Warehouse,
    int FromZone,
    int FromBin,
    int ToZone,
    int ToBin,
    long Sku,
    decimal Quantity);
