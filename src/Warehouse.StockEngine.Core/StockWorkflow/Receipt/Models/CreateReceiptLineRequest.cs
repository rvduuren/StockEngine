namespace Warehouse.StockEngine.Core.StockWorkflow.Receipt.Models;

public sealed class CreateReceiptLineRequest
{
    public required string LineId { get; init; }
    public int ProductId { get; init; }
    public decimal ExpectedQuantity { get; init; }
}