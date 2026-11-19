using Warehouse.StockEngine.Core.MasterData.Entities;

namespace Warehouse.StockEngine.Core.MasterData.Interfaces;

/// <inheritdoc cref="IMasterDataRepository{Zone}.GetByCodeAsync(string, CancellationToken)"/>
public interface IZoneRepository : IMasterDataRepository<Zone>
{
    /// <summary>
    /// Preferred lookup: <paramref name="code"/> scoped to <paramref name="warehouseId"/>.
    /// </summary>
    Task<Zone?> GetByCodeAsync(
        int warehouseId,
        string code,
        CancellationToken cancellationToken = default);
}
