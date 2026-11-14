using Microsoft.Extensions.DependencyInjection;
using Warehouse.StockEngine.Core.Interfaces;
using Warehouse.StockEngine.Core.MasterData.Entities;
using Warehouse.StockEngine.Core.MasterData.Interfaces;
using Warehouse.StockEngine.Infrastructure.MariaDb.Factories;
using Warehouse.StockEngine.Infrastructure.MariaDb.MasterData.Repositories;
using Warehouse.StockEngine.Tests.Fixtures;

namespace Warehouse.StockEngine.Tests.Integration.MasterData;

[Collection(nameof(MariaDbCollection))]
public sealed class MasterDataHarnessTests
{
    private readonly MariaDbTestcontainerFixture _fixture;

    public MasterDataHarnessTests(MariaDbTestcontainerFixture fixture) => _fixture = fixture;

    [Fact]
    public void Fixture_ExposesConnectionFactoryAndMigratedDatabase()
    {
        Assert.NotNull(_fixture.ConnectionFactory);
        Assert.IsType<MariaDbConnectionFactory>(_fixture.ConnectionFactory);
        Assert.False(string.IsNullOrWhiteSpace(_fixture.ConnectionString));
    }

    [Fact]
    public void Fixture_ServiceProvider_ResolvesStockEngineAndConnectionFactory()
    {
        var connectionFactory = _fixture.Services.GetRequiredService<IDbConnectionFactory>();
        Assert.Same(_fixture.ConnectionFactory, connectionFactory);

        using var scope = _fixture.Services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IStockEngine>());
    }

    [Fact]
    public void Fixture_ServiceProvider_ResolvesMasterDataRepositoryPorts()
    {
        using var scope = _fixture.Services.CreateScope();
        var provider = scope.ServiceProvider;

        Assert.IsType<WarehouseRepository>(
            provider.GetRequiredService<IMasterDataRepository<Core.MasterData.Entities.Warehouse>>());
        Assert.IsType<ProductRepository>(provider.GetRequiredService<IMasterDataRepository<Product>>());
        Assert.IsType<ZoneRepository>(provider.GetRequiredService<IZoneRepository>());
        Assert.IsType<BinRepository>(provider.GetRequiredService<IBinRepository>());
    }
}