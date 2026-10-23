using Warehouse.StockEngine.Core.Commands;
using Warehouse.StockEngine.Core.Exceptions;

namespace Warehouse.StockEngine.Infrastructure.MariaDb.Execution;

internal static class CommandValidator
{
    public static void Validate(InboundStockCommand command)
    {
        ValidateHeader(command.TransactionId, command.DocumentNo, command.Lines.Count);
        foreach (var line in command.Lines)
        {
            Require(line.DocumentLineId, nameof(line.DocumentLineId));
            Require(line.WarehouseCode, nameof(line.WarehouseCode));
            Require(line.LocationCode, nameof(line.LocationCode));
            Require(line.ItemCode, nameof(line.ItemCode));
            RequirePositive(line.Quantity, nameof(line.Quantity));
        }
    }

    public static void Validate(OutboundStockCommand command)
    {
        ValidateHeader(command.TransactionId, command.DocumentNo, command.Lines.Count);
        foreach (var line in command.Lines)
        {
            Require(line.DocumentLineId, nameof(line.DocumentLineId));
            Require(line.WarehouseCode, nameof(line.WarehouseCode));
            Require(line.LocationCode, nameof(line.LocationCode));
            Require(line.ItemCode, nameof(line.ItemCode));
            RequirePositive(line.Quantity, nameof(line.Quantity));
        }
    }

    public static void Validate(MoveStockCommand command)
    {
        ValidateHeader(command.TransactionId, command.DocumentNo, command.Lines.Count);
        foreach (var line in command.Lines)
        {
            Require(line.DocumentLineId, nameof(line.DocumentLineId));
            Require(line.WarehouseCode, nameof(line.WarehouseCode));
            Require(line.FromLocationCode, nameof(line.FromLocationCode));
            Require(line.ToLocationCode, nameof(line.ToLocationCode));
            Require(line.ItemCode, nameof(line.ItemCode));
            RequirePositive(line.Quantity, nameof(line.Quantity));

            if (string.Equals(line.FromLocationCode, line.ToLocationCode, StringComparison.Ordinal))
            {
                throw new InvalidStockCommandException("From and to location must differ.");
            }
        }
    }

    public static void Validate(CountStockCommand command)
    {
        ValidateHeader(command.TransactionId, command.DocumentNo, command.Lines.Count);
        foreach (var line in command.Lines)
        {
            Require(line.DocumentLineId, nameof(line.DocumentLineId));
            Require(line.WarehouseCode, nameof(line.WarehouseCode));
            Require(line.LocationCode, nameof(line.LocationCode));
            Require(line.ItemCode, nameof(line.ItemCode));

            if (line.CountedQuantity < 0)
            {
                throw new InvalidStockCommandException("Counted quantity cannot be negative.");
            }
        }
    }

    private static void ValidateHeader(Guid transactionId, string documentNo, int lineCount)
    {
        if (transactionId == Guid.Empty)
        {
            throw new InvalidStockCommandException("TransactionId is required.");
        }

        Require(documentNo, nameof(documentNo));

        if (lineCount == 0)
        {
            throw new InvalidStockCommandException("At least one line is required.");
        }
    }

    private static void Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidStockCommandException($"{name} is required.");
        }
    }

    private static void RequirePositive(decimal value, string name)
    {
        if (value <= 0)
        {
            throw new InvalidStockCommandException($"{name} must be greater than zero.");
        }
    }
}
