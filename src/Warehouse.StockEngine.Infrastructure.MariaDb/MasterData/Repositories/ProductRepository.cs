namespace Warehouse.StockEngine.Infrastructure.MariaDb.MasterData.Repositories;

public sealed class ProductRepository(IDbConnectionFactory connectionFactory)
    : MariaDbRepositoryBase(connectionFactory), IMasterDataRepository<Product>
{
    private const string TableName = "product";

    private const string SelectColumns = "id AS Id, code AS Code, description AS Description";

    private static readonly IReadOnlyList<string> SearchColumns = ["code", "description"];

    private static readonly IReadOnlyDictionary<string, string> SortAllowList =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = "code",
            ["description"] = "description",
        };

    public async Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await QueryFirstOrDefaultAsync<Product>(
            connection,
            $"SELECT {SelectColumns} FROM {TableName} WHERE id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken);
    }

    public async Task<Product?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await QueryFirstOrDefaultAsync<Product>(
            connection,
            $"SELECT {SelectColumns} FROM {TableName} WHERE code = @Code",
            new { Code = code },
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await QueryAsync<Product>(
            connection,
            $"SELECT {SelectColumns} FROM {TableName} ORDER BY code",
            cancellationToken: cancellationToken);
    }

    public Task<MasterDataSearchResult<Product>> SearchAsync(
        MasterDataSearchRequest request,
        CancellationToken cancellationToken = default) =>
        SearchPagedAsync<Product>(
            TableName,
            SelectColumns,
            scopePredicate: "1=1",
            SearchColumns,
            request,
            SortAllowList,
            defaultSortColumn: "code",
            cancellationToken);

    public Task<int> CreateAsync(Product entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return InsertAndGetIdAsync(
            $"INSERT INTO {TableName} (code, description) VALUES (@Code, @Description)",
            new { entity.Code, entity.Description },
            cancellationToken);
    }

    public async Task<bool> UpdateAsync(Product entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        var affected = await ExecuteAsync(
            connection,
            $"UPDATE {TableName} SET code = @Code, description = @Description WHERE id = @Id",
            new { entity.Id, entity.Code, entity.Description },
            cancellationToken: cancellationToken);

        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        var affected = await ExecuteAsync(
            connection,
            $"DELETE FROM {TableName} WHERE id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken);

        return affected > 0;
    }
}
