using System.Data.Common;
using Dapper;
using Warehouse.StockEngine.Infrastructure.MariaDb.MasterData.Search;

namespace Warehouse.StockEngine.Infrastructure.MariaDb.MasterData.Repositories.Base;

public abstract class MariaDbRepositoryBase(IDbConnectionFactory connectionFactory)
{
    private IDbConnectionFactory ConnectionFactory =>
        connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <summary>
    /// Maakt een verbinding en opent deze asynchroon.
    /// De aanroeper is verantwoordelijk voor het disposen van de verbinding.
    /// </summary>
    protected async Task<DbConnection> OpenConnectionAsync(
        CancellationToken cancellationToken = default)
    {
        var connection = ConnectionFactory.CreateConnection();

        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Voert een INSERT, UPDATE of DELETE uit.
    /// Retourneert het aantal geraakte rijen.
    /// </summary>
    protected async Task<int> ExecuteAsync(
        DbConnection connection,
        string sql,
        object? parameters = null,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var command = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            transaction: transaction,
            cancellationToken: cancellationToken);

        return await connection.ExecuteAsync(command);
    }

    protected async Task<IReadOnlyList<T>> QueryAsync<T>(
        DbConnection connection,
        string sql,
        object? parameters = null,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var command = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            transaction: transaction,
            cancellationToken: cancellationToken);

        var result = await connection.QueryAsync<T>(command);

        return result.AsList();
    }

    protected async Task<T?> QuerySingleOrDefaultAsync<T>(
        DbConnection connection,
        string sql,
        object? parameters = null,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var command = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            transaction: transaction,
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<T>(command);
    }

    protected async Task<T> QuerySingleAsync<T>(
        DbConnection connection,
        string sql,
        object? parameters = null,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var command = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            transaction: transaction,
            cancellationToken: cancellationToken);

        return await connection.QuerySingleAsync<T>(command);
    }

    protected async Task<T?> QueryFirstOrDefaultAsync<T>(
        DbConnection connection,
        string sql,
        object? parameters = null,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var command = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            transaction: transaction,
            cancellationToken: cancellationToken);

        return await connection.QueryFirstOrDefaultAsync<T>(command);
    }

    /// <summary>
    /// Gets scalar value: example COUNT(*) or ID.
    /// </summary>
    protected async Task<T?> ExecuteScalarAsync<T>(
        DbConnection connection,
        string sql,
        object? parameters = null,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var command = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            transaction: transaction,
            cancellationToken: cancellationToken);

        return await connection.ExecuteScalarAsync<T>(command);
    }

    protected async Task<int> InsertAndGetIdAsync(
        string insertSql,
        object parameters,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await ExecuteAsync(connection, insertSql, parameters, cancellationToken: cancellationToken);
        return await QuerySingleAsync<int>(
            connection,
            "SELECT LAST_INSERT_ID();",
            cancellationToken: cancellationToken);
    }

    protected async Task<MasterDataSearchResult<T>> SearchPagedAsync<T>(
        string tableName,
        string selectColumns,
        string scopePredicate,
        IReadOnlyList<string> searchColumns,
        MasterDataSearchRequest request,
        IReadOnlyDictionary<string, string> sortAllowList,
        string defaultSortColumn,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
        ArgumentException.ThrowIfNullOrWhiteSpace(selectColumns);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopePredicate);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sortAllowList);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultSortColumn);

        var paging = MasterDataSearchSupport.NormalizePaging(request);
        var sortColumn = MasterDataSearchSupport.ResolveSortColumn(
            request.SortBy,
            sortAllowList,
            defaultSortColumn);
        var orderBy = MasterDataSearchSupport.BuildOrderByClause(sortColumn, request.SortDescending);
        var searchPredicate = MasterDataRepositorySearch.BuildSearchTermClause(request.SearchTerm, searchColumns);
        var whereClause = $"({scopePredicate}) AND ({searchPredicate})";

        await using var connection = await OpenConnectionAsync(cancellationToken);

        var parameters = new
        {
            SearchPattern = string.IsNullOrWhiteSpace(request.SearchTerm)
                ? null
                : $"%{request.SearchTerm.Trim()}%",
            WarehouseId = request.WarehouseId,
            ZoneId = request.ZoneId,
            PageSize = paging.PageSize,
            Offset = paging.Offset,
        };

        var countSql = $"SELECT COUNT(*) FROM {tableName} WHERE {whereClause}";
        var totalCount = await ExecuteScalarAsync<int>(
            connection,
            countSql,
            parameters,
            cancellationToken: cancellationToken);

        var selectSql =
            $"SELECT {selectColumns} FROM {tableName} WHERE {whereClause} ORDER BY {orderBy} LIMIT @PageSize OFFSET @Offset";

        var items = await QueryAsync<T>(
            connection,
            selectSql,
            parameters,
            cancellationToken: cancellationToken);

        return new MasterDataSearchResult<T>(items, totalCount, paging.Page, paging.PageSize);
    }
}
