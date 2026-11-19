namespace Warehouse.StockEngine.Core.StockWorkflow.Receipt.Models;

public sealed class CloseReceiptRequest
{
    public Guid ReceiptId { get; init; }
}