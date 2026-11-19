namespace Warehouse.StockEngine.Core.StockWorkflow.Receipt.Exceptions;

public sealed class ReceiptNotFoundException : ReceiptWorkflowException
{
    public ReceiptNotFoundException(string message) : base(message)
    {
    }
}