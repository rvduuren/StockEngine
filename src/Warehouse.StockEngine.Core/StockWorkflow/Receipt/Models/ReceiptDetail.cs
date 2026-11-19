using Warehouse.StockEngine.Core.StockWorkflow.Receipt.Enums;

namespace Warehouse.StockEngine.Core.StockWorkflow.Receipt.Models;

public sealed class ReceiptDetail
{
    public Guid ReceiptId { get; init; }
    public int WarehouseId { get; init; }
    public required string DocumentNo { get; init; }
    public ReceiptStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public required IReadOnlyList<ReceiptLineDetail> Lines { get; init; }
}