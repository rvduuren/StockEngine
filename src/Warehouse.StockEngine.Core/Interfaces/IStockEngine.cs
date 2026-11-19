using System.Data.Common;
using Warehouse.StockEngine.Core.Commands;
using Warehouse.StockEngine.Core.Models;

namespace Warehouse.StockEngine.Core.Interfaces;

public interface IStockEngine
{
    /// <summary>
    /// Runs inbound in a connection and transaction owned by the engine (opens, commits or rolls back).
    /// Use this path for standalone stock mutations from services that do not already hold a database transaction.
    /// </summary>
    Task<StockResult> InboundAsync(InboundStockCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs inbound on the caller's open connection and transaction without committing or rolling back.
    /// Use this path when a workflow orchestrator must commit receipt state and inventory in one unit of work.
    /// </summary>
    Task<StockResult> InboundAsync(
        InboundStockCommand command,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<StockResult> OutboundAsync(OutboundStockCommand command, CancellationToken cancellationToken = default);

    Task<StockResult> MoveAsync(MoveStockCommand command, CancellationToken cancellationToken = default);

    Task<StockResult> CountAsync(CountStockCommand command, CancellationToken cancellationToken = default);

    Task<decimal> GetQuantityAsync(
        int warehouse,
        int zone,
        int bin,
        int product,
        CancellationToken cancellationToken = default);
}
