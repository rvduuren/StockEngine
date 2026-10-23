using System.Reflection;
using Dapper;
using Warehouse.StockEngine.Core.Interfaces;

namespace Warehouse.StockEngine.Infrastructure.MariaDb.Database;

public sealed class MariaDbDatabaseMigrator : IDatabaseMigrator
{
    private const string MigrationVersion = "001_InitialSchema";
    private readonly IDbConnectionFactory _connectionFactory;

    public MariaDbDatabaseMigrator(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version     VARCHAR(50)  NOT NULL,
                applied_at  DATETIME(6)  NOT NULL,
                PRIMARY KEY (version)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """,
            cancellationToken: cancellationToken));

        var applied = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(*) FROM schema_migrations WHERE version = @Version;",
            new { Version = MigrationVersion },
            cancellationToken: cancellationToken));

        if (applied > 0)
        {
            return;
        }

        var sql = LoadEmbeddedSql($"{MigrationVersion}.sql");
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var batch in SplitBatches(sql))
            {
                await connection.ExecuteAsync(new CommandDefinition(batch, transaction: transaction, cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO schema_migrations (version, applied_at)
                VALUES (@Version, UTC_TIMESTAMP(6));
                """,
                new { Version = MigrationVersion },
                transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string LoadEmbeddedSql(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded migration '{fileName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IEnumerable<string> SplitBatches(string sql)
    {
        return sql
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(batch => !string.IsNullOrWhiteSpace(batch));
    }
}
