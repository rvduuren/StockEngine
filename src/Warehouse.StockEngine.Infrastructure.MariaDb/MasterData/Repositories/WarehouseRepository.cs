namespace Warehouse.StockEngine.Infrastructure.MariaDb.MasterData.Repositories;

public sealed class WarehouseRepository(IDbConnectionFactory connectionFactory)
    : MariaDbRepositoryBase(connectionFactory), IMasterDataRepository<Core.MasterData.Entities.Warehouse>
{
    private const string TableName = "warehouse";

    private const string SelectColumns = "id AS Id, code AS Code, name AS Name";

    private static readonly IReadOnlyList<string> SearchColumns = ["code", "name"];

    private static readonly IReadOnlyDictionary<string, string> SortAllowList =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = "code",
            ["name"] = "name",
        };

    public async Task<Core.MasterData.Entities.Warehouse?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await QueryFirstOrDefaultAsync<Core.MasterData.Entities.Warehouse>(
            connection,
            $"SELECT {SelectColumns} FROM {TableName} WHERE id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken);
    }

    public async Task<Core.MasterData.Entities.Warehouse?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await QueryFirstOrDefaultAsync<Core.MasterData.Entities.Warehouse>(
            connection,
            $"SELECT {SelectColumns} FROM {TableName} WHERE code = @Code",
            new { Code = code },
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Core.MasterData.Entities.Warehouse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await QueryAsync<Core.MasterData.Entities.Warehouse>(
            connection,
            $"SELECT {SelectColumns} FROM {TableName} ORDER BY code",
            cancellationToken: cancellationToken);
    }

    public Task<MasterDataSearchResult<Core.MasterData.Entities.Warehouse>> SearchAsync(
        MasterDataSearchRequest request,
        CancellationToken cancellationToken = default) =>
        SearchPagedAsync<Core.MasterData.Entities.Warehouse>(
            TableName,
            SelectColumns,
            scopePredicate: "1=1",
            SearchColumns,
            request,
            SortAllowList,
            defaultSortColumn: "code",
            cancellationToken);

    public Task<int> CreateAsync(
        Core.MasterData.Entities.Warehouse entity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return InsertAndGetIdAsync(
            $"INSERT INTO {TableName} (code, name) VALUES (@Code, @Name)",
            new { entity.Code, entity.Name },
            cancellationToken);
    }

    public async Task<bool> UpdateAsync(
        Core.MasterData.Entities.Warehouse entity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        var affected = await ExecuteAsync(
            connection,
            $"UPDATE {TableName} SET code = @Code, name = @Name WHERE id = @Id",
            new { entity.Id, entity.Code, entity.Name },
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