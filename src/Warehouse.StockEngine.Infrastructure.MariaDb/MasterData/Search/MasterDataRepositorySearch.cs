namespace Warehouse.StockEngine.Infrastructure.MariaDb.MasterData.Search;

internal static class MasterDataRepositorySearch
{
    public static string BuildSearchTermClause(string? searchTerm, IReadOnlyList<string> columns)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return "1=1";
        }

        ArgumentNullException.ThrowIfNull(columns);

        if (columns.Count == 0)
        {
            throw new ArgumentException("At least one search column is required.", nameof(columns));
        }

        var predicates = columns.Select(column => $"{column} LIKE @SearchPattern");
        return $"({string.Join(" OR ", predicates)})";
    }
}
