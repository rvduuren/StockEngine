namespace Warehouse.StockEngine.Infrastructure.MariaDb.MasterData.Repositories;

public class ZoneRepository (IDbConnectionFactory connectionFactory)
    : MariaDbRepositoryBase(connectionFactory), IMasterDataRepository<Zone>
{
    public Task<Zone?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Zone?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<Zone>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<MasterDataSearchResult<Zone>> SearchAsync(MasterDataSearchRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<int> CreateAsync(Zone entity, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> UpdateAsync(Zone entity, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}