using Warehouse.StockEngine.Core.MasterData.Entities;
using Warehouse.StockEngine.Core.MasterData.Models;

namespace Warehouse.StockEngine.Core.MasterData.Interfaces;

public interface IMasterDataRepository<TEntity>
    where TEntity : class
{
    Task<TEntity?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up an entity by globally unique <paramref name="code"/> (warehouse, product).
    /// For hierarchical entities (<see cref="Zone"/>, <see cref="Bin"/>), prefer the scoped
    /// overloads on <see cref="IZoneRepository"/> / <see cref="IBinRepository"/>; the inherited
    /// unscoped method is non-deterministic when the same code exists under different parents.
    /// </summary>
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
