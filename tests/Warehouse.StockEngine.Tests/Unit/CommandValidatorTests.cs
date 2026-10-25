using Warehouse.StockEngine.Core.Commands;
using Warehouse.StockEngine.Core.Exceptions;
using Warehouse.StockEngine.Infrastructure.MariaDb.Execution;

namespace Warehouse.StockEngine.Tests.Unit;

public sealed class CommandValidatorTests
{
    private static readonly Guid ValidTxId = Guid.NewGuid();
    private const string ValidDocNo = "DOC-001";
    private const string ValidLineId = "L1";

    #region Inbound Tests

    [Fact]
    public void Validate_Inbound_ValidCommand_Succeeds()
    {
        var command = new InboundStockCommand(
            ValidTxId,
            ValidDocNo,
            [new InboundStockLine(ValidLineId, Warehouse: 1, Zone: 10, Bin: 100, Product: 12345, Quantity: 10.5m)]);

        CommandValidator.Validate(command);
    }

    [Fact]
    public void Validate_Inbound_EmptyTransactionId_Throws()
    {
        var command = new InboundStockCommand(
            Guid.Empty,
            ValidDocNo,
            [new InboundStockLine(ValidLineId, 1, 10, 100, 12345, 10m)]);

        var ex = Assert.Throws<InvalidStockCommandException>(() => CommandValidator.Validate(command));
        Assert.Equal("TransactionId is required.", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Inbound_InvalidDocumentNo_Throws(string? docNo)
    {
        var command = new InboundStockCommand(
            ValidTxId,
            docNo!,
            [new InboundStockLine(ValidLineId, 1, 10, 100, 12345, 10m)]);

        var ex = Assert.Throws<InvalidStockCommandException>(() => CommandValidator.Validate(command));
        Assert.Equal("documentNo is required.", ex.Message);
    }

    [Fact]
    public void Validate_Inbound_EmptyLines_Throws()
    {
        var command = new InboundStockCommand(ValidTxId, ValidDocNo, []);

        var ex = Assert.Throws<InvalidStockCommandException>(() => CommandValidator.Validate(command));
        Assert.Equal("At least one line is required.", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Inbound_InvalidDocumentLineId_Throws(string? lineId)
    {
        var command = new InboundStockCommand(
            ValidTxId,
            ValidDocNo,
            [new InboundStockLine(lineId!, 1, 10, 100, 12345, 10m)]);

        var ex = Assert.Throws<InvalidStockCommandException>(() => CommandValidator.Validate(command));
        Assert.Equal("DocumentLineId is required.", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.0001)]
    [InlineData(-100)]
    public void Validate_Inbound_NonPositiveQuantity_Throws(decimal qty)
    {
        var command = new InboundStockCommand(
            ValidTxId,
            ValidDocNo,
            [new InboundStockLine(ValidLineId, 1, 10, 100, 12345, qty)]);

        var ex = Assert.Throws<InvalidStockCommandException>(() => CommandValidator.Validate(command));
        Assert.Equal("Quantity must be greater than zero.", ex.Message);
    }

    #endregion

    #region Outbound Tests

    [Fact]
    public void Validate_Outbound_ValidCommand_Succeeds()
    {
        var command = new OutboundStockCommand(
            ValidTxId,
            ValidDocNo,
            [new OutboundStockLine(ValidLineId, 1, 10, 100, 12345, 5m)]);

        CommandValidator.Validate(command);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_Outbound_NonPositiveQuantity_Throws(decimal qty)
    {
        var command = new OutboundStockCommand(
            ValidTxId,
            ValidDocNo,
            [new OutboundStockLine(ValidLineId, 1, 10, 100, 12345, qty)]);

        var ex = Assert.Throws<InvalidStockCommandException>(() => CommandValidator.Validate(command));
        Assert.Equal("Quantity must be greater than zero.", ex.Message);
    }

    [Fact]
    public void Validate_Outbound_EmptyLines_Throws()
    {
        var command = new OutboundStockCommand(ValidTxId, ValidDocNo, []);

        var ex = Assert.Throws<InvalidStockCommandException>(() => CommandValidator.Validate(command));
        Assert.Equal("At least one line is required.", ex.Message);
    }

    #endregion

    #region Move Tests

    [Fact]
    public void Validate_Move_ValidCommand_Succeeds()
    {
        var command = new MoveStockCommand(
            ValidTxId,
            ValidDocNo,
            [new MoveStockLine(ValidLineId, Warehouse: 1, FromZone: 10, FromBin: 100, ToZone: 10, ToBin: 200, Product: 12345, Quantity: 3m)]);

        CommandValidator.Validate(command);
    }

    [Fact]
    public void Validate_Move_IdenticalFromAndToBin_Throws()
    {
        var command = new MoveStockCommand(
            ValidTxId,
            ValidDocNo,
            [new MoveStockLine(ValidLineId, 1, FromZone: 10, FromBin: 100, ToZone: 10, ToBin: 100, Product: 12345, Quantity: 3m)]);

        var ex = Assert.Throws<InvalidStockCommandException>(() => CommandValidator.Validate(command));
        Assert.Equal("From and to bin must differ.", ex.Message);
    }

    [Fact]
    public void Validate_Move_DifferentZoneSameBinNumber_Succeeds()
    {
        var command = new MoveStockCommand(
            ValidTxId,
            ValidDocNo,
            [new MoveStockLine(ValidLineId, 1, FromZone: 10, FromBin: 100, ToZone: 20, ToBin: 100, Product: 12345, Quantity: 3m)]);

        CommandValidator.Validate(command);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_Move_NonPositiveQuantity_Throws(decimal qty)
    {
        var command = new MoveStockCommand(
            ValidTxId,
            ValidDocNo,
            [new MoveStockLine(ValidLineId, 1, 10, 100, 10, 200, 12345, qty)]);

        var ex = Assert.Throws<InvalidStockCommandException>(() => CommandValidator.Validate(command));
        Assert.Equal("Quantity must be greater than zero.", ex.Message);
    }

    #endregion

    #region Count Tests

    [Fact]
    public void Validate_Count_ValidCommand_WithPositiveQuantity_Succeeds()
    {
        var command = new CountStockCommand(
            ValidTxId,
            ValidDocNo,
            [new CountStockLine(ValidLineId, 1, 10, 100, 12345, CountedQuantity: 42m)]);

        CommandValidator.Validate(command);
    }

    [Fact]
    public void Validate_Count_ZeroQuantity_IsAllowed()
    {
        // Cycle counts discovering zero remaining items are valid in warehouse operations
        var command = new CountStockCommand(
            ValidTxId,
            ValidDocNo,
            [new CountStockLine(ValidLineId, 1, 10, 100, 12345, CountedQuantity: 0m)]);

        CommandValidator.Validate(command);
    }

    [Fact]
    public void Validate_Count_NegativeQuantity_Throws()
    {
        var command = new CountStockCommand(
            ValidTxId,
            ValidDocNo,
            [new CountStockLine(ValidLineId, 1, 10, 100, 12345, CountedQuantity: -1m)]);

        var ex = Assert.Throws<InvalidStockCommandException>(() => CommandValidator.Validate(command));
        Assert.Equal("Counted quantity cannot be negative.", ex.Message);
    }

    #endregion
}
