namespace Warehouse.StockEngine.Core.MasterData.Entities;

public class Bin
{
    public int Id { get; set; }
    public int ZoneId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}