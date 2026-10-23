using Warehouse.StockEngine.Core.Commands;
using Warehouse.StockEngine.Core.Models;

namespace Warehouse.StockEngine.Core.Interfaces;

public interface IStockEngine
{
    Task<StockResult> InboundAsync(InboundStockCommand command, CancellationToken cancellationToken = default);

    Task<StockResult> OutboundAsync(OutboundStockCommand command, CancellationToken cancellationToken = default);

    Task<StockResult> MoveAsync(MoveStockCommand command, CancellationToken cancellationToken = default);

    Task<StockResult> CountAsync(CountStockCommand command, CancellationToken cancellationToken = default);

    Task<decimal> GetQuantityAsync(
        int warehouse,
        int zone,
        int bin,
        long sku,
        CancellationToken cancellationToken = default);
}
