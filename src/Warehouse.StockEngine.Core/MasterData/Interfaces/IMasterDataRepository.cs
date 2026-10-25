using Warehouse.StockEngine.Core.MasterData.Entities;
using Warehouse.StockEngine.Core.MasterData.Models;

namespace Warehouse.StockEngine.Core.MasterData.Interfaces;

public interface IMasterDataRepository<TEntity>
    where TEntity : class
{
    Task<TEntity?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    Task<TEntity?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TEntity>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<MasterDataSearchResult<TEntity>> SearchAsync(
        MasterDataSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<int> CreateAsync(
        TEntity entity,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(
        TEntity entity,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        int id,
        CancellationToken cancellationToken = default);
}