namespace Warehouse.StockEngine.Core.StockWorkflow.Receipt.Exceptions;

public sealed class ReceiptWorkflowValidationException : ReceiptWorkflowException
{
    public ReceiptWorkflowValidationException(string message) : base(message)
    {
    }
}