namespace Warehouse.StockEngine.Core.StockWorkflow.Receipt.Models;

public sealed class ConfirmReceiveRequest
{
    public Guid ReceiptId { get; init; }
    public required string LineId { get; init; }
    public decimal Quantity { get; init; }
    public Guid ConfirmId { get; init; }
    public string? Source { get; init; }
    public string? UserId { get; init; }
    public string? DeviceId { get; init; }
    public string? ReasonCode { get; init; }
}