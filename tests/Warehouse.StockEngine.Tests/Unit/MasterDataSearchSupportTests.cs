using Warehouse.StockEngine.Core.MasterData.Models;
using Warehouse.StockEngine.Infrastructure.MariaDb.MasterData.Search;

namespace Warehouse.StockEngine.Tests.Unit;

public sealed class MasterDataSearchSupportTests
{
    private static readonly IReadOnlyDictionary<string, string> AllowList =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = "Code",
            ["name"] = "Name",
        };

    [Theory]
    [InlineData(0, 0, 1, MasterDataSearchSupport.DefaultPageSize)]
    [InlineData(-3, -10, 1, MasterDataSearchSupport.DefaultPageSize)]
    [InlineData(2, 50, 2, 50)]
    [InlineData(1, 10_000, 1, MasterDataSearchSupport.MaxPageSize)]
    public void NormalizePaging_ClampsPageAndPageSize(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var request = new MasterDataSearchRequest { Page = page, PageSize = pageSize };

        var normalized = MasterDataSearchSupport.NormalizePaging(request);

        Assert.Equal(expectedPage, normalized.Page);
        Assert.Equal(expectedPageSize, normalized.PageSize);
        Assert.Equal((expectedPage - 1) * expectedPageSize, normalized.Offset);
    }

    [Theory]
    [InlineData(null, "Code")]
    [InlineData("", "Code")]
    [InlineData("unknown", "Code")]
    [InlineData("CODE", "Code")]
    [InlineData("name", "Name")]
    public void ResolveSortColumn_UsesAllowListOrDefault(string? sortBy, string expectedColumn)
    {
        var column = MasterDataSearchSupport.ResolveSortColumn(sortBy, AllowList, "Code");

        Assert.Equal(expectedColumn, column);
    }

    [Theory]
    [InlineData(false, "Code ASC")]
    [InlineData(true, "Code DESC")]
    public void BuildOrderByClause_ReflectsSortDirection(bool descending, string expected)
    {
        var clause = MasterDataSearchSupport.BuildOrderByClause("Code", descending);

        Assert.Equal(expected, clause);
    }
}