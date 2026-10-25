using System.Data.Common;
using Dapper;

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
}