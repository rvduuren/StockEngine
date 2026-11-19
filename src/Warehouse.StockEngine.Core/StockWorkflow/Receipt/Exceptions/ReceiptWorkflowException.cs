namespace Warehouse.StockEngine.Core.StockWorkflow.Receipt.Exceptions;

public class ReceiptWorkflowException : Exception
{
    protected ReceiptWorkflowException(string message) : base(message)
    {
    }
}