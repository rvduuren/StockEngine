namespace Warehouse.StockEngine.Core.MasterData.Models;

public sealed record MasterDataSearchRequest
{
    public string? SearchTerm { get; init; }

    public bool IncludeInactive { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;

    public string? SortBy { get; init; }

    public bool SortDescending { get; init; }
}