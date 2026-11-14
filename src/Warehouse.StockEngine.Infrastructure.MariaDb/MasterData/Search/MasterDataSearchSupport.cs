using Warehouse.StockEngine.Core.MasterData.Models;

namespace Warehouse.StockEngine.Infrastructure.MariaDb.MasterData.Search;

internal static class MasterDataSearchSupport
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 500;

    public readonly record struct NormalizedPaging(int Page, int PageSize, int Offset);

    public static NormalizedPaging NormalizePaging(MasterDataSearchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? DefaultPageSize : Math.Min(request.PageSize, MaxPageSize);
        var offset = (page - 1) * pageSize;

        return new NormalizedPaging(page, pageSize, offset);
    }

    public static string ResolveSortColumn(
        string? sortBy,
        IReadOnlyDictionary<string, string> columnAllowList,
        string defaultColumn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultColumn);

        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return defaultColumn;
        }

        foreach (var (key, column) in columnAllowList)
        {
            if (string.Equals(key, sortBy, StringComparison.OrdinalIgnoreCase))
            {
                return column;
            }
        }

        return defaultColumn;
    }

    public static string BuildOrderByClause(string column, bool sortDescending) =>
        sortDescending ? $"{column} DESC" : $"{column} ASC";
}