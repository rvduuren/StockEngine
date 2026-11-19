namespace Warehouse.StockEngine.Core.StockWorkflow.Receipt.Models;

public sealed class ConfirmReceiveResult
{
    public Guid ConfirmId { get; init; }
    public Guid EngineTransactionId { get; init; }
    public decimal ConfirmedQuantity { get; init; }
    public required string LineId { get; init; }
    public required ReceiptDetail Receipt { get; init; }
}