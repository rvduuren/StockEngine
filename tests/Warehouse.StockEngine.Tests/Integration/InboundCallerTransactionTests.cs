using System.Data.Common;
using Dapper;
using Warehouse.StockEngine.Core.Commands;
using Warehouse.StockEngine.Infrastructure.MariaDb.Execution;
using Warehouse.StockEngine.Tests.Fixtures;

namespace Warehouse.StockEngine.Tests.Integration;

[Collection(nameof(MariaDbCollection))]
public sealed class InboundCallerTransactionTests
{
    private readonly MariaDbTestcontainerFixture _fixture;

    public InboundCallerTransactionTests(MariaDbTestcontainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Inbound_OnCallerTransaction_CommitsWithSiblingSql()
    {
        await EnsureWorkflowReceiptStubTableAsync();

        var product = UniqueProduct();
        var receiptId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var engine = new MariaDbStockEngine(_fixture.ConnectionFactory);

        await using DbConnection connection = _fixture.ConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await connection.ExecuteAsync(
            """
            INSERT INTO workflow_receipt_stub (receipt_id, status)
            VALUES (@ReceiptId, @Status)
            """,
            new { ReceiptId = receiptId.ToString(), Status = "RECEIVED" },
            transaction);

        var result = await engine.InboundAsync(
            new InboundStockCommand(
                transactionId,
                "IN-SHARED-TX",
                [new InboundStockLine("1", 1, 10, 100, product, 7m)]),
            connection,
            transaction);

        Assert.False(result.WasAlreadyProcessed);
        Assert.Equal(1, result.HistoryRowsWritten);

        await transaction.CommitAsync();

        var stubStatus = await connection.QuerySingleAsync<string>(
            "SELECT status FROM workflow_receipt_stub WHERE receipt_id = @ReceiptId",
            new { ReceiptId = receiptId.ToString() });
        Assert.Equal("RECEIVED", stubStatus);
        Assert.Equal(7m, await engine.GetQuantityAsync(1, 10, 100, product));
    }

    [Fact]
    public async Task Inbound_OnCallerTransaction_RollsBackWithSiblingSql()
    {
        await EnsureWorkflowReceiptStubTableAsync();

        var product = UniqueProduct();
        var receiptId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();
        var engine = new MariaDbStockEngine(_fixture.ConnectionFactory);

        await using DbConnection connection = _fixture.ConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await connection.ExecuteAsync(
            """
            INSERT INTO workflow_receipt_stub (receipt_id, status)
            VALUES (@ReceiptId, @Status)
            """,
            new { ReceiptId = receiptId.ToString(), Status = "RECEIVED" },
            transaction);

        await engine.InboundAsync(
            new InboundStockCommand(
                transactionId,
                "IN-ROLLBACK",
                [new InboundStockLine("1", 1, 10, 100, product, 4m)]),
            connection,
            transaction);

        await transaction.RollbackAsync();

        var stubCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM workflow_receipt_stub WHERE receipt_id = @ReceiptId",
            new { ReceiptId = receiptId.ToString() });
        Assert.Equal(0, stubCount);
        Assert.Equal(0m, await engine.GetQuantityAsync(1, 10, 100, product));
    }

    private async Task EnsureWorkflowReceiptStubTableAsync()
    {
        await using var connection = _fixture.ConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS workflow_receipt_stub (
                receipt_id VARCHAR(36) NOT NULL PRIMARY KEY,
                status VARCHAR(32) NOT NULL
            )
            """);
    }

    private static int UniqueProduct() => Random.Shared.Next(40, 50);
}