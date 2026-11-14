using Warehouse.StockEngine.Core.MasterData.Entities;

namespace Warehouse.StockEngine.Core.MasterData.Interfaces;

public interface IZoneRepository : IMasterDataRepository<Zone>
{
    Task<Zone?> GetByCodeAsync(
        int warehouseId,
        string code,
        CancellationToken cancellationToken = default);
}