using System.Data.Common;
using System.Reflection;
using Dapper;
using Warehouse.StockEngine.Core.Interfaces;

namespace Warehouse.StockEngine.Infrastructure.MariaDb.Database;

public sealed class MariaDbDatabaseMigrator : IDatabaseMigrator
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly Assembly _migrationAssembly;

    public MariaDbDatabaseMigrator(IDbConnectionFactory connectionFactory)
        : this(connectionFactory, typeof(MariaDbDatabaseMigrator).Assembly)
    {
    }

    public MariaDbDatabaseMigrator(IDbConnectionFactory connectionFactory, Assembly migrationAssembly)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _migrationAssembly = migrationAssembly ?? throw new ArgumentNullException(nameof(migrationAssembly));
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var migrations = DiscoverMigrations();
        if (migrations.Count == 0)
        {
            return;
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        await EnsureSchemaMigrationsTableAsync(connection, cancellationToken);

        var appliedVersions = await GetAppliedMigrationVersionsAsync(connection, cancellationToken);

        var pendingMigrations = migrations
            .Where(m => !appliedVersions.Contains(m.Version))
            .ToList();

        if (pendingMigrations.Count == 0)
        {
            return;
        }

        foreach (var migration in pendingMigrations)
        {
            await ApplyMigrationAsync(connection, migration, cancellationToken);
        }
    }

    internal IReadOnlyList<MigrationDescriptor> DiscoverMigrations()
    {
        var resourceNames = _migrationAssembly.GetManifestResourceNames();
        var migrations = new List<MigrationDescriptor>();

        foreach (var resourceName in resourceNames)
        {
            if (!resourceName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var version = ExtractVersionFromResourceName(resourceName);
            migrations.Add(new MigrationDescriptor(version, resourceName));
        }

        return migrations
            .OrderBy(m => m, MigrationComparer.Instance)
            .ToList();
    }

    internal static string ExtractVersionFromResourceName(string resourceName)
    {
        var nameWithoutExtension = resourceName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)
            ? resourceName[..^4]
            : resourceName;

        const string migrationsMarker = ".Database.Migrations.";
        var markerIndex = nameWithoutExtension.LastIndexOf(migrationsMarker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex >= 0)
        {
            return nameWithoutExtension[(markerIndex + migrationsMarker.Length)..];
        }

        const string altMarker = ".Migrations.";
        var altIndex = nameWithoutExtension.LastIndexOf(altMarker, StringComparison.OrdinalIgnoreCase);
        if (altIndex >= 0)
        {
            return nameWithoutExtension[(altIndex + altMarker.Length)..];
        }

        var lastDot = nameWithoutExtension.LastIndexOf('.');
        return lastDot >= 0 ? nameWithoutExtension[(lastDot + 1)..] : nameWithoutExtension;
    }

    private static async Task EnsureSchemaMigrationsTableAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version     VARCHAR(50)  NOT NULL,
                applied_at  DATETIME(6)  NOT NULL,
                PRIMARY KEY (version)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            """,
            cancellationToken: cancellationToken));
    }

    private static async Task<HashSet<string>> GetAppliedMigrationVersionsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var applied = await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT version FROM schema_migrations;",
            cancellationToken: cancellationToken));

        return new HashSet<string>(applied, StringComparer.OrdinalIgnoreCase);
    }

    private async Task ApplyMigrationAsync(DbConnection connection, MigrationDescriptor migration, CancellationToken cancellationToken)
    {
        var sql = LoadEmbeddedSql(migration.ResourceName);
        var batches = SplitBatches(sql);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var batch in batches)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    batch,
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO schema_migrations (version, applied_at)
                VALUES (@Version, UTC_TIMESTAMP(6));
                """,
                new { Version = migration.Version },
                transaction: transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private string LoadEmbeddedSql(string resourceName)
    {
        using var stream = _migrationAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded migration '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IEnumerable<string> SplitBatches(string sql)
    {
        return sql
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(batch => !string.IsNullOrWhiteSpace(batch) && IsExecutableSql(batch));
    }

    private static bool IsExecutableSql(string batch)
    {
        var lines = batch.Split('\n');
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length > 0 && !line.StartsWith("--") && !line.StartsWith("/*"))
            {
                return true;
            }
        }
        return false;
    }
}

internal sealed record MigrationDescriptor(string Version, string ResourceName);

internal sealed class MigrationComparer : IComparer<MigrationDescriptor>
{
    public static readonly MigrationComparer Instance = new();

    public int Compare(MigrationDescriptor? x, MigrationDescriptor? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        var numX = ExtractLeadingNumber(x.Version);
        var numY = ExtractLeadingNumber(y.Version);

        var numCompare = numX.CompareTo(numY);
        if (numCompare != 0)
        {
            return numCompare;
        }

        return string.Compare(x.Version, y.Version, StringComparison.OrdinalIgnoreCase);
    }

    private static long ExtractLeadingNumber(string version)
    {
        var separatorIndex = version.IndexOfAny(['_', '-', '.']);
        var prefix = separatorIndex > 0 ? version[..separatorIndex] : version;
        return long.TryParse(prefix, out var number) ? number : long.MaxValue;
    }
}
