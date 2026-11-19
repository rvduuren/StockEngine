namespace Warehouse.StockEngine.Core.StockWorkflow.Receipt.Models;

public sealed class ReceiptLineDetail
{
    public required string LineId { get; init; }
    public int ProductId { get; init; }
    public decimal ExpectedQuantity { get; init; }
    public decimal ReceivedQuantity { get; init; }
    public decimal RemainingQuantity { get; init; }
}