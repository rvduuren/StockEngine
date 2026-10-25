using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Warehouse.StockEngine.Infrastructure.MariaDb.Database;
using Warehouse.StockEngine.Infrastructure.MariaDb.Execution;
using Warehouse.StockEngine.Infrastructure.MariaDb.Factories;
using Warehouse.StockEngine.Infrastructure.MariaDb.MasterData.Repositories;

namespace Warehouse.StockEngine.Infrastructure.MariaDb.DependencyInjection;

/// <summary>
/// Extension methods for configuring Warehouse Stock Engine services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class StockEngineServiceCollectionExtensions
{
    /// <summary>
    /// Adds the MariaDB-backed Warehouse Stock Engine and related services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="connectionString">The MariaDB database connection string.</param>
    /// <param name="engineLifetime">The service lifetime for <see cref="IStockEngine"/>. Defaults to <see cref="ServiceLifetime.Scoped"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddMariaDbStockEngine(
        this IServiceCollection services,
        string connectionString,
        ServiceLifetime engineLifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddScoped<IMasterDataRepository<Product>, ProductRepository>();
        services.TryAddScoped<IMasterDataRepository<Core.MasterData.Entities.Warehouse>, WarehouseRepository>();
        //
        // services.TryAddScoped<
        //     IMasterDataRepository<Zone>,
        //     ZoneRepository>();
        //
        // services.TryAddScoped<
        //     IMasterDataRepository<Bin>,
        //     BinRepository>();

        return services.AddMariaDbStockEngine(_ => connectionString, engineLifetime);
    }

    /// <summary>
    /// Adds the MariaDB-backed Warehouse Stock Engine and related services to the specified <see cref="IServiceCollection"/>
    /// using a factory delegate to resolve the connection string.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="connectionStringFactory">A factory delegate that produces the connection string from the <see cref="IServiceProvider"/>.</param>
    /// <param name="engineLifetime">The service lifetime for <see cref="IStockEngine"/>. Defaults to <see cref="ServiceLifetime.Scoped"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddMariaDbStockEngine(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringFactory,
        ServiceLifetime engineLifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionStringFactory);

        services.TryAddSingleton<IDbConnectionFactory>(sp =>
        {
            var connectionString = connectionStringFactory(sp);
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
            return new MariaDbConnectionFactory(connectionString);
        });

        services.TryAddTransient<IDatabaseMigrator, MariaDbDatabaseMigrator>();
        services.TryAddSingleton<IStockEngineFactory, MariaDbStockEngineFactory>();

        services.TryAdd(ServiceDescriptor.Describe(typeof(MariaDbStockEngine), typeof(MariaDbStockEngine),
            engineLifetime));
        services.TryAdd(ServiceDescriptor.Describe(typeof(IStockEngine),
            sp => sp.GetRequiredService<MariaDbStockEngine>(), engineLifetime));

        return services;
    }

    /// <summary>
    /// Alias for <see cref="AddMariaDbStockEngine(IServiceCollection, string, ServiceLifetime)"/>.
    /// </summary>
    public static IServiceCollection AddStockEngine(
        this IServiceCollection services,
        string connectionString,
        ServiceLifetime engineLifetime = ServiceLifetime.Scoped)
        => services.AddMariaDbStockEngine(connectionString, engineLifetime);

    /// <summary>
    /// Alias for <see cref="AddMariaDbStockEngine(IServiceCollection, Func{IServiceProvider, string}, ServiceLifetime)"/>.
    /// </summary>
    public static IServiceCollection AddStockEngine(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringFactory,
        ServiceLifetime engineLifetime = ServiceLifetime.Scoped)
        => services.AddMariaDbStockEngine(connectionStringFactory, engineLifetime);
}