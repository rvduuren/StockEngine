namespace Warehouse.StockEngine.Core.StockWorkflow.Receipt.Models;

public sealed class CreateReceiptRequest
{
    public int WarehouseId { get; init; }
    public required string DocumentNo { get; init; }
    public required IReadOnlyList<CreateReceiptLineRequest> Lines { get; init; }
}