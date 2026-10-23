namespace Warehouse.StockEngine.Core.Commands;

public sealed record MoveStockCommand(
    Guid TransactionId,
    string DocumentNo,
    IReadOnlyList<MoveStockLine> Lines);

public sealed record MoveStockLine(
    string DocumentLineId,
    int Warehouse,
    int FromZone,
    int FromBin,
    int ToZone,
    int ToBin,
    long Sku,
    decimal Quantity);
