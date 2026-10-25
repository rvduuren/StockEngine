using Microsoft.Extensions.DependencyInjection;
using Warehouse.StockEngine.Core.Interfaces;
using Warehouse.StockEngine.Infrastructure.MariaDb.Database;
using Warehouse.StockEngine.Infrastructure.MariaDb.DependencyInjection;
using Warehouse.StockEngine.Infrastructure.MariaDb.Execution;
using Warehouse.StockEngine.Infrastructure.MariaDb.Factories;

namespace Warehouse.StockEngine.Tests.Unit;

public sealed class DependencyInjectionTests
{
    private const string DummyConnectionString = "Server=localhost;Database=test;User=root;Password=secret;";

    [Fact]
    public void AddMariaDbStockEngine_WithConnectionString_RegistersAllRequiredServices()
    {
        var services = new ServiceCollection();

        services.AddMariaDbStockEngine(DummyConnectionString);

        using var provider = services.BuildServiceProvider();

        var connectionFactory = provider.GetService<IDbConnectionFactory>();
        Assert.NotNull(connectionFactory);
        Assert.IsType<MariaDbConnectionFactory>(connectionFactory);

        var migrator = provider.GetService<IDatabaseMigrator>();
        Assert.NotNull(migrator);
        Assert.IsType<MariaDbDatabaseMigrator>(migrator);

        var engineFactory = provider.GetService<IStockEngineFactory>();
        Assert.NotNull(engineFactory);
        Assert.IsType<MariaDbStockEngineFactory>(engineFactory);

        using var scope = provider.CreateScope();
        var stockEngine = scope.ServiceProvider.GetService<IStockEngine>();
        Assert.NotNull(stockEngine);
        Assert.IsType<MariaDbStockEngine>(stockEngine);

        var concreteEngine = scope.ServiceProvider.GetService<MariaDbStockEngine>();
        Assert.NotNull(concreteEngine);
        Assert.Same(stockEngine, concreteEngine);
    }

    [Fact]
    public void AddMariaDbStockEngine_WithFactory_ResolvesConnectionStringFromProvider()
    {
        var services = new ServiceCollection();
        var factoryInvoked = false;

        services.AddMariaDbStockEngine(sp =>
        {
            factoryInvoked = true;
            return DummyConnectionString;
        });

        using var provider = services.BuildServiceProvider();

        Assert.False(factoryInvoked);

        var connectionFactory = provider.GetRequiredService<IDbConnectionFactory>();
        Assert.True(factoryInvoked);
        Assert.NotNull(connectionFactory);
    }

    [Fact]
    public void AddMariaDbStockEngine_RespectsConfiguredLifetime()
    {
        var services = new ServiceCollection();

        services.AddMariaDbStockEngine(DummyConnectionString, ServiceLifetime.Singleton);

        using var provider = services.BuildServiceProvider();

        var engine1 = provider.GetRequiredService<IStockEngine>();
        var engine2 = provider.GetRequiredService<IStockEngine>();

        Assert.Same(engine1, engine2);
    }

    [Fact]
    public void AddMariaDbStockEngine_DefaultsToScopedLifetime()
    {
        var services = new ServiceCollection();

        services.AddMariaDbStockEngine(DummyConnectionString);

        using var provider = services.BuildServiceProvider();

        IStockEngine engineScope1;
        using (var scope1 = provider.CreateScope())
        {
            engineScope1 = scope1.ServiceProvider.GetRequiredService<IStockEngine>();
            var sameScopeEngine = scope1.ServiceProvider.GetRequiredService<IStockEngine>();
            Assert.Same(engineScope1, sameScopeEngine);
        }

        using (var scope2 = provider.CreateScope())
        {
            var engineScope2 = scope2.ServiceProvider.GetRequiredService<IStockEngine>();
            Assert.NotSame(engineScope1, engineScope2);
        }
    }

    [Fact]
    public void AddMariaDbStockEngine_ThrowsOnInvalidArguments()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(() =>
            StockEngineServiceCollectionExtensions.AddMariaDbStockEngine(null!, DummyConnectionString));

        Assert.Throws<ArgumentException>(() =>
            services.AddMariaDbStockEngine(string.Empty));

        Assert.Throws<ArgumentException>(() =>
            services.AddMariaDbStockEngine("   "));

        Assert.Throws<ArgumentNullException>(() =>
            services.AddMariaDbStockEngine((Func<IServiceProvider, string>)null!));
    }

    [Fact]
    public void AddStockEngine_Alias_WorksIdentically()
    {
        var services = new ServiceCollection();

        services.AddStockEngine(DummyConnectionString);

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IStockEngineFactory>());
        Assert.NotNull(provider.GetService<IDbConnectionFactory>());
    }
}
