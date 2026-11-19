using Warehouse.StockEngine.Core.StockWorkflow.Receipt.Models;

namespace Warehouse.StockEngine.Core.StockWorkflow.Receipt.Interfaces;

/// <summary>
/// Stock-workflow façade for receipt document lifecycle (v1: staging, create/query, confirm receive).
/// </summary>
public interface IReceiptWorkflow
{
    Task<ReceivingStagingConfig> SetReceivingStagingAsync(
        SetReceivingStagingRequest request,
        CancellationToken cancellationToken = default);

    Task<ReceivingStagingConfig?> GetReceivingStagingAsync(
        int warehouseId,
        CancellationToken cancellationToken = default);

    Task<ReceiptDetail> CreateReceiptAsync(
        CreateReceiptRequest request,
        CancellationToken cancellationToken = default);

    Task<ReceiptDetail?> GetReceiptByIdAsync(
        Guid receiptId,
        CancellationToken cancellationToken = default);

    Task<ReceiptDetail?> GetReceiptByDocumentNoAsync(
        int warehouseId,
        string documentNo,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReceiptSummary>> ListOpenReceiptsAsync(
        int warehouseId,
        CancellationToken cancellationToken = default);

    Task<ConfirmReceiveResult> ConfirmReceiveAsync(
        ConfirmReceiveRequest request,
        CancellationToken cancellationToken = default);
}