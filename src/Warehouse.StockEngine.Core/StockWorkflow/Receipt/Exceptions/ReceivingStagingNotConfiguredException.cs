namespace Warehouse.StockEngine.Core.StockWorkflow.Receipt.Exceptions;

public sealed class ReceivingStagingNotConfiguredException : ReceiptWorkflowException
{
    public ReceivingStagingNotConfiguredException(int warehouseId)
        : base($"Receiving staging is not configured for warehouse {warehouseId}.")
    {
        WarehouseId = warehouseId;
    }

    public int WarehouseId { get; }
}