using System.Globalization;
using Warehouse.StockEngine.Core.Commands;
using Warehouse.StockEngine.Infrastructure.MariaDb.Execution;

namespace Warehouse.StockEngine.Tests.Unit;

public sealed class RequestHasherTests
{
    private static readonly Guid TxId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private const string DocNo = "ASN-999";

    [Fact]
    public void Hash_Inbound_IsDeterministic()
    {
        var cmd1 = new InboundStockCommand(
            TxId,
            DocNo,
            [new InboundStockLine("L1", 1, 10, 100, 50001, 15.75m)]);

        var cmd2 = new InboundStockCommand(
            TxId,
            DocNo,
            [new InboundStockLine("L1", 1, 10, 100, 50001, 15.75m)]);

        var hash1 = RequestHasher.Hash(cmd1);
        var hash2 = RequestHasher.Hash(cmd2);

        Assert.Equal(hash1, hash2);
        Assert.Matches("^[0-9a-f]{64}$", hash1);
    }

    [Fact]
    public void Hash_Inbound_IsLineOrderInsensitive()
    {
        var line1 = new InboundStockLine("L1", 1, 10, 100, 50001, 10m);
        var line2 = new InboundStockLine("L2", 1, 10, 101, 50002, 20m);

        var cmdOrderA = new InboundStockCommand(TxId, DocNo, [line1, line2]);
        var cmdOrderB = new InboundStockCommand(TxId, DocNo, [line2, line1]);

        var hashA = RequestHasher.Hash(cmdOrderA);
        var hashB = RequestHasher.Hash(cmdOrderB);

        Assert.Equal(hashA, hashB);
    }

    [Fact]
    public void Hash_Outbound_IsDeterministic()
    {
        var cmd = new OutboundStockCommand(
            TxId,
            DocNo,
            [new OutboundStockLine("L1", 1, 10, 100, 50001, 5m)]);

        var hash1 = RequestHasher.Hash(cmd);
        var hash2 = RequestHasher.Hash(cmd);

        Assert.Equal(hash1, hash2);
        Assert.Matches("^[0-9a-f]{64}$", hash1);
    }

    [Fact]
    public void Hash_Move_IsDeterministic()
    {
        var cmd = new MoveStockCommand(
            TxId,
            DocNo,
            [new MoveStockLine("L1", 1, FromZone: 10, FromBin: 100, ToZone: 10, ToBin: 200, Sku: 50001, Quantity: 4m)]);

        var hash1 = RequestHasher.Hash(cmd);
        var hash2 = RequestHasher.Hash(cmd);

        Assert.Equal(hash1, hash2);
        Assert.Matches("^[0-9a-f]{64}$", hash1);
    }

    [Fact]
    public void Hash_Count_IsDeterministic()
    {
        var cmd = new CountStockCommand(
            TxId,
            DocNo,
            [new CountStockLine("L1", 1, 10, 100, 50001, CountedQuantity: 8m, ReasonCode: "AUDIT")]);

        var hash1 = RequestHasher.Hash(cmd);
        var hash2 = RequestHasher.Hash(cmd);

        Assert.Equal(hash1, hash2);
        Assert.Matches("^[0-9a-f]{64}$", hash1);
    }

    [Fact]
    public void Hash_Inbound_ChangesWhenAnyFieldChanges()
    {
        var baseCommand = new InboundStockCommand(
            TxId,
            DocNo,
            [new InboundStockLine("L1", 1, 10, 100, 50001, 10m)]);

        var baseHash = RequestHasher.Hash(baseCommand);

        // Different TransactionId
        var diffTx = baseCommand with { TransactionId = Guid.NewGuid() };
        Assert.NotEqual(baseHash, RequestHasher.Hash(diffTx));

        // Different DocumentNo
        var diffDoc = baseCommand with { DocumentNo = "DIFF-DOC" };
        Assert.NotEqual(baseHash, RequestHasher.Hash(diffDoc));

        // Different Quantity
        var diffQty = baseCommand with { Lines = [new InboundStockLine("L1", 1, 10, 100, 50001, 11m)] };
        Assert.NotEqual(baseHash, RequestHasher.Hash(diffQty));

        // Different SKU
        var diffSku = baseCommand with { Lines = [new InboundStockLine("L1", 1, 10, 100, 99999, 10m)] };
        Assert.NotEqual(baseHash, RequestHasher.Hash(diffSku));

        // Different Location
        var diffLoc = baseCommand with { Lines = [new InboundStockLine("L1", 1, 10, 101, 50001, 10m)] };
        Assert.NotEqual(baseHash, RequestHasher.Hash(diffLoc));
    }

    [Fact]
    public void Hash_Count_ChangesWhenReasonCodeChanges()
    {
        var cmdWithReason = new CountStockCommand(
            TxId,
            DocNo,
            [new CountStockLine("L1", 1, 10, 100, 50001, 5m, ReasonCode: "DAMAGED")]);

        var cmdWithoutReason = new CountStockCommand(
            TxId,
            DocNo,
            [new CountStockLine("L1", 1, 10, 100, 50001, 5m, ReasonCode: null)]);

        Assert.NotEqual(RequestHasher.Hash(cmdWithReason), RequestHasher.Hash(cmdWithoutReason));
    }

    [Fact]
    public void Hash_DiffersBetweenInboundAndOutbound_EvenWithIdenticalProperties()
    {
        var inbound = new InboundStockCommand(
            TxId,
            DocNo,
            [new InboundStockLine("L1", 1, 10, 100, 50001, 10m)]);

        var outbound = new OutboundStockCommand(
            TxId,
            DocNo,
            [new OutboundStockLine("L1", 1, 10, 100, 50001, 10m)]);

        Assert.NotEqual(RequestHasher.Hash(inbound), RequestHasher.Hash(outbound));
    }

    [Fact]
    public void Hash_FormatsDecimalsInvariantly_RegardlessOfCurrentCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            // Culture with comma decimal separator (Dutch)
            CultureInfo.CurrentCulture = new CultureInfo("nl-NL");

            var cmd = new InboundStockCommand(
                TxId,
                DocNo,
                [new InboundStockLine("L1", 1, 10, 100, 50001, 123.456m)]);

            var hashDutch = RequestHasher.Hash(cmd);

            // Culture with dot decimal separator (US)
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var hashUS = RequestHasher.Hash(cmd);

            Assert.Equal(hashDutch, hashUS);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }
}
