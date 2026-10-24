using Dapper;
using Warehouse.StockEngine.Infrastructure.MariaDb.Database;
using Warehouse.StockEngine.Tests.Fixtures;

namespace Warehouse.StockEngine.Tests.Integration;

[Collection(nameof(MariaDbCollection))]
public sealed class DatabaseMigratorTests
{
    private readonly MariaDbTestcontainerFixture _fixture;

    public DatabaseMigratorTests(MariaDbTestcontainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task MigrateAsync_IsIdempotent_WhenCalledMultipleTimes()
    {
        var migrator = new MariaDbDatabaseMigrator(_fixture.ConnectionFactory);

        // Should complete without error even though fixture already migrated
        await migrator.MigrateAsync();
        await migrator.MigrateAsync();

        await using var connection = _fixture.ConnectionFactory.CreateConnection();
        await connection.OpenAsync();

        var appliedMigrations = (await connection.QueryAsync<string>(
            "SELECT version FROM schema_migrations;")).ToList();

        Assert.Contains("001_InitialSchema", appliedMigrations);
        Assert.Single(appliedMigrations, v => v == "001_InitialSchema");
    }
}
