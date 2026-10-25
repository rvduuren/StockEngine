using Testcontainers.MariaDb;
using Warehouse.StockEngine.Core.Interfaces;
using Warehouse.StockEngine.Infrastructure.MariaDb.Database;
using Warehouse.StockEngine.Infrastructure.MariaDb.Execution;
using Warehouse.StockEngine.Infrastructure.MariaDb.Factories;
using Xunit;

namespace Warehouse.StockEngine.Tests.Fixtures;

public sealed class MariaDbTestcontainerFixture : IAsyncLifetime
{
    private MariaDbContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public IDbConnectionFactory ConnectionFactory { get; private set; } = null!;

    public IStockEngine Engine { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new MariaDbBuilder("mariadb:11.4")
            .WithDatabase("stockengine")
            .WithUsername("stock")
            .WithPassword("stock")
            .Build();

        await _container.StartAsync();

        var baseConnStr = _container.GetConnectionString();
        ConnectionString = baseConnStr.EndsWith(';')
            ? $"{baseConnStr}MaximumPoolSize=300;ConnectionTimeout=30;"
            : $"{baseConnStr};MaximumPoolSize=300;ConnectionTimeout=30;";
        ConnectionFactory = new MariaDbConnectionFactory(ConnectionString);

        IDatabaseMigrator migrator = new MariaDbDatabaseMigrator(ConnectionFactory);
        await migrator.MigrateAsync();

        Engine = new MariaDbStockEngine(ConnectionFactory);
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition(nameof(MariaDbCollection))]
public sealed class MariaDbCollection : ICollectionFixture<MariaDbTestcontainerFixture>;
