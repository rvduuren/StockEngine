using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Warehouse.StockEngine.Core.Commands;

namespace Warehouse.StockEngine.Infrastructure.MariaDb.Execution;

internal static class RequestHasher
{
    public static string Hash(InboundStockCommand command)
    {
        var sb = new StringBuilder();
        sb.Append("Inbound|").Append(command.TransactionId).Append('|').Append(command.DocumentNo);
        foreach (var line in command.Lines.OrderBy(l => l.DocumentLineId, StringComparer.Ordinal))
        {
            sb.Append('|')
              .Append(line.DocumentLineId).Append(';')
              .Append(line.WarehouseCode).Append(';')
              .Append(line.LocationCode).Append(';')
              .Append(line.ItemCode).Append(';')
              .Append(line.Quantity.ToString(CultureInfo.InvariantCulture));
        }

        return Sha256Hex(sb.ToString());
    }

    public static string Hash(OutboundStockCommand command)
    {
        var sb = new StringBuilder();
        sb.Append("Outbound|").Append(command.TransactionId).Append('|').Append(command.DocumentNo);
        foreach (var line in command.Lines.OrderBy(l => l.DocumentLineId, StringComparer.Ordinal))
        {
            sb.Append('|')
              .Append(line.DocumentLineId).Append(';')
              .Append(line.WarehouseCode).Append(';')
              .Append(line.LocationCode).Append(';')
              .Append(line.ItemCode).Append(';')
              .Append(line.Quantity.ToString(CultureInfo.InvariantCulture));
        }

        return Sha256Hex(sb.ToString());
    }

    public static string Hash(MoveStockCommand command)
    {
        var sb = new StringBuilder();
        sb.Append("Move|").Append(command.TransactionId).Append('|').Append(command.DocumentNo);
        foreach (var line in command.Lines.OrderBy(l => l.DocumentLineId, StringComparer.Ordinal))
        {
            sb.Append('|')
              .Append(line.DocumentLineId).Append(';')
              .Append(line.WarehouseCode).Append(';')
              .Append(line.FromLocationCode).Append(';')
              .Append(line.ToLocationCode).Append(';')
              .Append(line.ItemCode).Append(';')
              .Append(line.Quantity.ToString(CultureInfo.InvariantCulture));
        }

        return Sha256Hex(sb.ToString());
    }

    public static string Hash(CountStockCommand command)
    {
        var sb = new StringBuilder();
        sb.Append("Count|").Append(command.TransactionId).Append('|').Append(command.DocumentNo);
        foreach (var line in command.Lines.OrderBy(l => l.DocumentLineId, StringComparer.Ordinal))
        {
            sb.Append('|')
              .Append(line.DocumentLineId).Append(';')
              .Append(line.WarehouseCode).Append(';')
              .Append(line.LocationCode).Append(';')
              .Append(line.ItemCode).Append(';')
              .Append(line.CountedQuantity.ToString(CultureInfo.InvariantCulture));
        }

        return Sha256Hex(sb.ToString());
    }

    private static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
