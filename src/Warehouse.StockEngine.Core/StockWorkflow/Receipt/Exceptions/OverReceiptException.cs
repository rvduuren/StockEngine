namespace Warehouse.StockEngine.Core.StockWorkflow.Receipt.Exceptions;

public sealed class OverReceiptException : ReceiptWorkflowException
{
    public OverReceiptException(string lineId, decimal remainingQuantity, decimal attemptedQuantity)
        : base(
            $"Confirm quantity {attemptedQuantity} exceeds remaining expected quantity {remainingQuantity} for line '{lineId}'.")
    {
        LineId = lineId;
        RemainingQuantity = remainingQuantity;
        AttemptedQuantity = attemptedQuantity;
    }

    public string LineId { get; }
    public decimal RemainingQuantity { get; }
    public decimal AttemptedQuantity { get; }
}