using Warehouse.StockEngine.Core.Interfaces;
using Warehouse.StockEngine.Infrastructure.MariaDb.Database;

namespace Warehouse.StockEngine.Tests.Unit;

public sealed class MariaDbDatabaseMigratorTests
{
    [Fact]
    public void DiscoverMigrations_FindsInitialSchemaMigration()
    {
        var dummyConnectionFactory = new DummyConnectionFactory();
        var migrator = new MariaDbDatabaseMigrator(dummyConnectionFactory);

        var migrations = migrator.DiscoverMigrations();

        Assert.NotEmpty(migrations);
        Assert.Contains(migrations, m => m.Version == "001_InitialSchema");
    }

    [Theory]
    [InlineData("Warehouse.StockEngine.Infrastructure.MariaDb.Database.Migrations.001_InitialSchema.sql", "001_InitialSchema")]
    [InlineData("MyAssembly.Database.Migrations.002_AddIndex.sql", "002_AddIndex")]
    [InlineData("MyAssembly.Migrations.003_MoreTables.sql", "003_MoreTables")]
    [InlineData("Some.Namespace.999_Custom.sql", "999_Custom")]
    public void ExtractVersionFromResourceName_ExtractsExpectedVersion(string resourceName, string expectedVersion)
    {
        var actual = MariaDbDatabaseMigrator.ExtractVersionFromResourceName(resourceName);
        Assert.Equal(expectedVersion, actual);
    }

    [Fact]
    public void MigrationComparer_OrdersNumericallyAndOrdinally()
    {
        var list = new List<MigrationDescriptor>
        {
            new("010_Tenth", "res10"),
            new("002_Second", "res2"),
            new("001_First", "res1"),
            new("999_Last", "res999"),
            new("003_Third", "res3")
        };

        list.Sort(MigrationComparer.Instance);

        var versions = list.Select(m => m.Version).ToList();
        Assert.Equal(["001_First", "002_Second", "003_Third", "010_Tenth", "999_Last"], versions);
    }

    private sealed class DummyConnectionFactory : IDbConnectionFactory
    {
        public System.Data.Common.DbConnection CreateConnection() => throw new NotImplementedException();
    }
}
