namespace Warehouse.StockEngine.Core.MasterData.Models;

public sealed record MasterDataSearchRequest
{
    public string? SearchTerm { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;

    public string? SortBy { get; init; }

    public bool SortDescending { get; init; }

    /// <summary>
    /// When searching zones, limits results to this warehouse. Ignored for other entities.
    /// </summary>
    public int? WarehouseId { get; init; }

    /// <summary>
    /// When searching bins, limits results to this zone. Ignored for other entities.
    /// </summary>
    public int? ZoneId { get; init; }
}
