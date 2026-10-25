namespace Warehouse.StockEngine.Infrastructure.MariaDb.MasterData.Repositories;

public class WarehouseRepository(IDbConnectionFactory connectionFactory)
    : MariaDbRepositoryBase(connectionFactory), IMasterDataRepository<Core.MasterData.Entities.Warehouse>
{
    public Task<Core.MasterData.Entities.Warehouse?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Core.MasterData.Entities.Warehouse?> GetByCodeAsync(string code,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Core.MasterData.Entities.Warehouse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<MasterDataSearchResult<Core.MasterData.Entities.Warehouse>> SearchAsync(MasterDataSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<int> CreateAsync(Core.MasterData.Entities.Warehouse entity,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> UpdateAsync(Core.MasterData.Entities.Warehouse entity,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}