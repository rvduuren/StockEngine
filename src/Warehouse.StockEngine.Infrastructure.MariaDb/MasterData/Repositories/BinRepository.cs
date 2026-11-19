namespace Warehouse.StockEngine.Infrastructure.MariaDb.MasterData.Repositories;

public sealed class BinRepository(IDbConnectionFactory connectionFactory)
    : MariaDbRepositoryBase(connectionFactory), IBinRepository
{
    private const string TableName = "bin";

    private const string SelectColumns = "id AS Id, zone_id AS ZoneId, code AS Code, name AS Name";

    private const string ScopePredicate = "(@ZoneId IS NULL OR zone_id = @ZoneId)";

    private static readonly IReadOnlyList<string> SearchColumns = ["code", "name"];

    private static readonly IReadOnlyDictionary<string, string> SortAllowList =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = "code",
            ["name"] = "name",
            ["zoneId"] = "zone_id",
        };

    public async Task<Bin?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await QueryFirstOrDefaultAsync<Bin>(
            connection,
            $"SELECT {SelectColumns} FROM {TableName} WHERE id = @Id",
            new { Id = id },
            cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>Non-deterministic first match when <paramref name="code"/> repeats across zones; use the zone-scoped overload.</remarks>
    public async Task<Bin?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await QueryFirstOrDefaultAsync<Bin>(
            connection,
            $"SELECT {SelectColumns} FROM {TableName} WHERE code = @Code",
            new { Code = code },
            cancellationToken: cancellationToken);
    }

    public async Task<Bin?> GetByCodeAsync(
        int zoneId,
        string code,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await QueryFirstOrDefaultAsync<Bin>(
            connection,
            $"SELECT {SelectColumns} FROM {TableName} WHERE zone_id = @ZoneId AND code = @Code",
            new { ZoneId = zoneId, Code = code },
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<Bin>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);

        return await QueryAsync<Bin>(
            connection,
            $"SELECT {SelectColumns} FROM {TableName} ORDER BY code",
            cancellationToken: cancellationToken);
    }

    public Task<MasterDataSearchResult<Bin>> SearchAsync(
        MasterDataSearchRequest request,
        CancellationToken cancellationToken = default) =>
        SearchPagedAsync<Bin>(
            TableName,
            SelectColumns,
            ScopePredicate,
            SearchColumns,
            request,
            SortAllowList,
            defaultSortColumn: "code",
            cancellationToken);

    public Task<int> CreateAsync(Bin entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return InsertAndGetIdAsync(
            $"INSERT INTO {TableName} (zone_id, code, name) VALUES (@ZoneId, @Code, @Name)",
            new { entity.ZoneId, entity.Code, entity.Name },
            cancellationToken);
    }

    public async Task<bool> UpdateAsync(Bin entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);

        await using var connection = await OpenConnectionAsync(cancellationToken);

        var affected = await ExecuteAsync(
            connection,
            $"UPDATE {TableName} SET zone_id = @ZoneId, code = @Code, name = @Name WHERE id = @Id",
            new { entity.Id, entity.ZoneId, entity.Code, entity.Name },
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
