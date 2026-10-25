namespace Warehouse.StockEngine.Core.Commands;

public sealed record InboundStockCommand(
    Guid TransactionId,
    string DocumentNo,
    IReadOnlyList<InboundStockLine> Lines,
    string? Source = null,
    string? UserId = null,
    string? DeviceId = null,
    string? ReasonCode = null);

public sealed record InboundStockLine(
    string DocumentLineId,
    int Warehouse,
    int Zone,
    int Bin,
    int Product,
    decimal Quantity);
