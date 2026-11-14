using Warehouse.StockEngine.Core.MasterData.Entities;

namespace Warehouse.StockEngine.Core.MasterData.Interfaces;

public interface IBinRepository : IMasterDataRepository<Bin>
{
    Task<Bin?> GetByCodeAsync(
        int zoneId,
        string code,
        CancellationToken cancellationToken = default);
}