namespace Warehouse.StockEngine.Core.StockWorkflow.Receipt.Exceptions;

public sealed class ReceiptClosedException : ReceiptWorkflowException
{
    public ReceiptClosedException(Guid receiptId)
        : base($"Receipt {receiptId} is closed and cannot receive stock.")
    {
        ReceiptId = receiptId;
    }

    public Guid ReceiptId { get; }
}