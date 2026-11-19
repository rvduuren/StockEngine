using Warehouse.StockEngine.Infrastructure.MariaDb.Execution;

namespace Warehouse.StockEngine.Infrastructure.MariaDb.Factories;

public sealed class MariaDbStockEngineFactory : IStockEngineFactory
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MariaDbStockEngineFactory(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public IStockEngine Create() => new MariaDbStockEngine(_connectionFactory);
}
