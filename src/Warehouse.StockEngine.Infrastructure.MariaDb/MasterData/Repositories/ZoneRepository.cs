namespace Warehouse.StockEngine.Infrastructure.MariaDb.MasterData.Repositories;

public sealed class ZoneRepository(IDbConnectionFactory connectionFactory)
    : MariaDbRepositoryBase(connectionFactory), IZoneRepository
{
    private const string TableName = "zone";

    private const string SelectColumns =
        "id AS Id, warehouse_id AS WarehouseId, code AS Code, name AS Name";

    private const string ScopePredicate =
        "(@WarehouseId IS NULL OR warehouse_id = @WarehouseId)";

    private static readonly IReadOnlyList<string> SearchColumns = ["code", "name"];

    private static readonly IReadOnlyDictionary<string, string> SortAllowList =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = "code",
            ["name"] = "name",
            ["warehouseId"] = "warehouse_id",
        };

    public async Task<Zone?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await QueryFirstOrDefaultAsync<Zone>(
            connection,
            $"SELECT {SelectColumns} FROM {TableName} WHERE id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>Non-deterministic first match when <paramref name="code"/> repeats across warehouses; use the warehouse-scoped overload.</remarks>
    public async Task<Zone?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await QueryFirstOrDefaultAsync<Zone>(
            connection,
            $"SELECT {SelectColumns} FROM {TableName} WHERE code = @Code",
            new { Code = code },
            cancellationToken: cancellationToken);
    }

    public async Task<Zone?> GetByCodeAsync(
        int warehouseId,
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await QueryFirstOrDefaultAsync<Zone>(
            connection,
            $"SELECT {SelectColumns} FROM {TableName} WHERE warehouse_id = @WarehouseId AND code = @Code",
            new { WarehouseId = warehouseId, Code = code },
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Zone>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await QueryAsync<Zone>(
            connection,
            $"SELECT {SelectColumns} FROM {TableName} ORDER BY code",
            cancellationToken: cancellationToken);
    }

    public Task<MasterDataSearchResult<Zone>> SearchAsync(
        MasterDataSearchRequest request,
        CancellationToken cancellationToken = default) =>
        SearchPagedAsync<Zone>(
            TableName,
            SelectColumns,
            ScopePredicate,
            SearchColumns,
            request,
            SortAllowList,
            defaultSortColumn: "code",
            cancellationToken);

    public Task<int> CreateAsync(Zone entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return InsertAndGetIdAsync(
            $"INSERT INTO {TableName} (warehouse_id, code, name) VALUES (@WarehouseId, @Code, @Name)",
            new { entity.WarehouseId, entity.Code, entity.Name },
            cancellationToken);
    }

    public async Task<bool> UpdateAsync(Zone entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        var affected = await ExecuteAsync(
            connection,
            $"UPDATE {TableName} SET warehouse_id = @WarehouseId, code = @Code, name = @Name WHERE id = @Id",
            new { entity.Id, entity.WarehouseId, entity.Code, entity.Name },
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
