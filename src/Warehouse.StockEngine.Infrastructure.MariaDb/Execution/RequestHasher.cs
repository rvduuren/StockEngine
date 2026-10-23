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
        sb.Append("Inbound|").Append(command.TransactionId).Append('|').Append(NullSafe(command.DocumentNo));
        foreach (var line in StableLines(command.Lines, l => l.DocumentLineId))
        {
            sb.Append('|')
              .Append(NullSafe(line.DocumentLineId)).Append(';')
              .Append(line.Warehouse).Append(';')
              .Append(line.Zone).Append(';')
              .Append(line.Bin).Append(';')
              .Append(line.Sku).Append(';')
              .Append(FormatDecimal(line.Quantity));
        }

        return Sha256Hex(sb.ToString());
    }

    public static string Hash(OutboundStockCommand command)
    {
        var sb = new StringBuilder();
        sb.Append("Outbound|").Append(command.TransactionId).Append('|').Append(NullSafe(command.DocumentNo));
        foreach (var line in StableLines(command.Lines, l => l.DocumentLineId))
        {
            sb.Append('|')
              .Append(NullSafe(line.DocumentLineId)).Append(';')
              .Append(line.Warehouse).Append(';')
              .Append(line.Zone).Append(';')
              .Append(line.Bin).Append(';')
              .Append(line.Sku).Append(';')
              .Append(FormatDecimal(line.Quantity));
        }

        return Sha256Hex(sb.ToString());
    }

    public static string Hash(MoveStockCommand command)
    {
        var sb = new StringBuilder();
        sb.Append("Move|").Append(command.TransactionId).Append('|').Append(NullSafe(command.DocumentNo));
        foreach (var line in StableLines(command.Lines, l => l.DocumentLineId))
        {
            sb.Append('|')
              .Append(NullSafe(line.DocumentLineId)).Append(';')
              .Append(line.Warehouse).Append(';')
              .Append(line.FromZone).Append(';')
              .Append(line.FromBin).Append(';')
              .Append(line.ToZone).Append(';')
              .Append(line.ToBin).Append(';')
              .Append(line.Sku).Append(';')
              .Append(FormatDecimal(line.Quantity));
        }

        return Sha256Hex(sb.ToString());
    }

    public static string Hash(CountStockCommand command)
    {
        var sb = new StringBuilder();
        sb.Append("Count|").Append(command.TransactionId).Append('|').Append(NullSafe(command.DocumentNo));
        foreach (var line in StableLines(command.Lines, l => l.DocumentLineId))
        {
            sb.Append('|')
              .Append(NullSafe(line.DocumentLineId)).Append(';')
              .Append(line.Warehouse).Append(';')
              .Append(line.Zone).Append(';')
              .Append(line.Bin).Append(';')
              .Append(line.Sku).Append(';')
              .Append(FormatDecimal(line.CountedQuantity)).Append(';')
              .Append(NullSafe(line.ReasonCode));
        }

        return Sha256Hex(sb.ToString());
    }

    private static IEnumerable<T> StableLines<T>(IReadOnlyList<T> lines, Func<T, string?> keySelector)
        => lines.OrderBy(l => NullSafe(keySelector(l)), StringComparer.Ordinal);

    private static string FormatDecimal(decimal value)
        => value.ToString(CultureInfo.InvariantCulture);

    private static string NullSafe(string? value)
        => value ?? string.Empty;

    private static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
