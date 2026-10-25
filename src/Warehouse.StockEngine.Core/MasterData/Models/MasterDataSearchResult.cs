namespace Warehouse.StockEngine.Core.MasterData.Models;

public sealed record MasterDataSearchResult<TEntity>(
    IReadOnlyList<TEntity> Items,
    int TotalCount,
    int Page,
    int PageSize)
    where TEntity : class;