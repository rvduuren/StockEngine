using System.Data.Common;
using MySqlConnector;
using Warehouse.StockEngine.Core.Interfaces;

namespace Warehouse.StockEngine.Infrastructure.MariaDb.Factories;

public sealed class MariaDbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public MariaDbConnectionFactory(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connectionString = connectionString;
    }

    public DbConnection CreateConnection() => new MySqlConnection(_connectionString);
}
