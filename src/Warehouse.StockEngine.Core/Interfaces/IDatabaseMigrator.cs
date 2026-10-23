namespace Warehouse.StockEngine.Core.Interfaces;

public interface IDatabaseMigrator
{
    Task MigrateAsync(CancellationToken cancellationToken = default);
}
