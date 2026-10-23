namespace Warehouse.StockEngine.Core.Models;

public sealed record StockResult(
    Guid TransactionId,
    bool WasAlreadyProcessed,
    int HistoryRowsWritten);
