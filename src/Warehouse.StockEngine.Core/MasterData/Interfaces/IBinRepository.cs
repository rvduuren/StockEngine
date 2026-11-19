using Warehouse.StockEngine.Core.MasterData.Entities;

namespace Warehouse.StockEngine.Core.MasterData.Interfaces;

/// <inheritdoc cref="IMasterDataRepository{Bin}.GetByCodeAsync(string, CancellationToken)"/>
public interface IBinRepository : IMasterDataRepository<Bin>
{
    /// <summary>
    /// Preferred lookup: <paramref name="code"/> scoped to <paramref name="zoneId"/>.
    /// </summary>
    Task<Bin?> GetByCodeAsync(
        int zoneId,
        string code,
        CancellationToken cancellationToken = default);
}
