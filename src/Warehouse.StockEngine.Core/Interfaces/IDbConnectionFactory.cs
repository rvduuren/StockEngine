using System.Data.Common;

namespace Warehouse.StockEngine.Core.Interfaces;

public interface IDbConnectionFactory
{
    DbConnection CreateConnection();
}
