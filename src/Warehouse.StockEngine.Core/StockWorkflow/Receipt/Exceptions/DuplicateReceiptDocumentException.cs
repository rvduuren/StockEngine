namespace Warehouse.StockEngine.Core.StockWorkflow.Receipt.Exceptions;

public sealed class DuplicateReceiptDocumentException : ReceiptWorkflowException
{
    public DuplicateReceiptDocumentException(int warehouseId, string documentNo)
        : base($"Receipt document '{documentNo}' already exists for warehouse {warehouseId}.")
    {
        WarehouseId = warehouseId;
        DocumentNo = documentNo;
    }

    public int WarehouseId { get; }
    public string DocumentNo { get; }
}