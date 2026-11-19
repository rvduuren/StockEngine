using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Warehouse.StockEngine.Core.Interfaces;
using Warehouse.StockEngine.Core.MasterData.Entities;
using Warehouse.StockEngine.Core.MasterData.Interfaces;
using Warehouse.StockEngine.Core.StockWorkflow.Receipt.Enums;
using Warehouse.StockEngine.Core.StockWorkflow.Receipt.Exceptions;
using Warehouse.StockEngine.Core.StockWorkflow.Receipt.Interfaces;
using Warehouse.StockEngine.Core.StockWorkflow.Receipt.Models;
using Warehouse.StockEngine.Tests.Fixtures;

namespace Warehouse.StockEngine.Tests.Integration.StockWorkflow;

[Collection(nameof(MariaDbCollection))]
public sealed class ReceiptWorkflowIntegrationTests
{
    private readonly MariaDbTestcontainerFixture _fixture;

    public ReceiptWorkflowIntegrationTests(MariaDbTestcontainerFixture fixture) => _fixture = fixture;

    [Fact]
    public void Fixture_ResolvesReceiptWorkflow()
    {
        using var scope = _fixture.Services.CreateScope();
        Assert.IsType<Infrastructure.MariaDb.StockWorkflow.Receipt.MariaDbReceiptWorkflow>(
            scope.ServiceProvider.GetRequiredService<IReceiptWorkflow>());
    }

    [Fact]
    public async Task SetReceivingStaging_ValidLocation_PersistsAndReturnsConfig()
    {
        var ctx = await SeedWarehouseWithStagingLocationAsync();
        using var scope = _fixture.Services.CreateScope();
        var workflow = ResolveWorkflow(scope);

        var config = await workflow.SetReceivingStagingAsync(new SetReceivingStagingRequest
        {
            WarehouseId = ctx.WarehouseId,
            ZoneId = ctx.ZoneId,
            BinId = ctx.BinId
        });

        Assert.Equal(ctx.WarehouseId, config.WarehouseId);
        Assert.Equal(ctx.ZoneId, config.ZoneId);
        Assert.Equal(ctx.BinId, config.BinId);

        var loaded = await workflow.GetReceivingStagingAsync(ctx.WarehouseId);
        Assert.NotNull(loaded);
        Assert.Equal(config.BinId, loaded.BinId);
    }

    [Fact]
    public async Task SetReceivingStaging_BinFromDifferentZone_Throws()
    {
        var ctx = await SeedWarehouseWithStagingLocationAsync();
        var otherZoneId = await ResolveZone().CreateAsync(new Zone
        {
            WarehouseId = ctx.WarehouseId,
            Code = UniqueCode("Z-OTHER"),
            Name = "Other"
        });
        using var scope = _fixture.Services.CreateScope();
        var workflow = ResolveWorkflow(scope);

        await Assert.ThrowsAsync<ReceiptWorkflowValidationException>(() =>
            workflow.SetReceivingStagingAsync(new SetReceivingStagingRequest
            {
                WarehouseId = ctx.WarehouseId,
                ZoneId = otherZoneId,
                BinId = ctx.BinId
            }));
    }

    [Fact]
    public async Task CreateReceipt_WithStaging_ShowsZeroReceivedAndFullRemaining()
    {
        var ctx = await SeedWarehouseWithStagingLocationAsync();
        using var scope = _fixture.Services.CreateScope();
        var workflow = ResolveWorkflow(scope);
        await workflow.SetReceivingStagingAsync(new SetReceivingStagingRequest
        {
            WarehouseId = ctx.WarehouseId,
            ZoneId = ctx.ZoneId,
            BinId = ctx.BinId
        });

        var documentNo = UniqueCode("DOC");
        var detail = await workflow.CreateReceiptAsync(new CreateReceiptRequest
        {
            WarehouseId = ctx.WarehouseId,
            DocumentNo = documentNo,
            Lines =
            [
                new CreateReceiptLineRequest
                {
                    LineId = "1",
                    ProductId = ctx.ProductId,
                    ExpectedQuantity = 12.5m
                }
            ]
        });

        Assert.Equal(ReceiptStatus.Open, detail.Status);
        Assert.Single(detail.Lines);
        var line = detail.Lines[0];
        Assert.Equal(12.5m, line.ExpectedQuantity);
        Assert.Equal(0m, line.ReceivedQuantity);
        Assert.Equal(12.5m, line.RemainingQuantity);

        var byId = await workflow.GetReceiptByIdAsync(detail.ReceiptId);
        Assert.NotNull(byId);
        Assert.Equal(documentNo, byId.DocumentNo);

        var byDoc = await workflow.GetReceiptByDocumentNoAsync(ctx.WarehouseId, documentNo);
        Assert.NotNull(byDoc);
        Assert.Equal(detail.ReceiptId, byDoc.ReceiptId);
    }

    [Fact]
    public async Task ListOpenReceipts_ReturnsOnlyOpenForWarehouse()
    {
        var ctx = await SeedWarehouseWithStagingLocationAsync();
        using var scope = _fixture.Services.CreateScope();
        var workflow = ResolveWorkflow(scope);
        await workflow.SetReceivingStagingAsync(new SetReceivingStagingRequest
        {
            WarehouseId = ctx.WarehouseId,
            ZoneId = ctx.ZoneId,
            BinId = ctx.BinId
        });

        await workflow.CreateReceiptAsync(new CreateReceiptRequest
        {
            WarehouseId = ctx.WarehouseId,
            DocumentNo = UniqueCode("DOC-A"),
            Lines = [Line(ctx.ProductId, "A", 1m)]
        });
        await workflow.CreateReceiptAsync(new CreateReceiptRequest
        {
            WarehouseId = ctx.WarehouseId,
            DocumentNo = UniqueCode("DOC-B"),
            Lines = [Line(ctx.ProductId, "B", 1m)]
        });

        var open = await workflow.ListOpenReceiptsAsync(ctx.WarehouseId);
        Assert.Equal(2, open.Count);
        Assert.All(open, r => Assert.Equal(ReceiptStatus.Open, r.Status));
    }

    [Fact]
    public async Task CreateReceipt_WithoutStaging_Throws()
    {
        var ctx = await SeedWarehouseWithStagingLocationAsync();
        using var scope = _fixture.Services.CreateScope();
        var workflow = ResolveWorkflow(scope);

        await Assert.ThrowsAsync<ReceivingStagingNotConfiguredException>(() =>
            workflow.CreateReceiptAsync(new CreateReceiptRequest
            {
                WarehouseId = ctx.WarehouseId,
                DocumentNo = UniqueCode("DOC"),
                Lines = [Line(ctx.ProductId, "1", 1m)]
            }));
    }

    [Fact]
    public async Task CreateReceipt_UnknownProduct_Throws()
    {
        var ctx = await SeedWarehouseWithStagingLocationAsync();
        using var scope = _fixture.Services.CreateScope();
        var workflow = ResolveWorkflow(scope);
        await workflow.SetReceivingStagingAsync(new SetReceivingStagingRequest
        {
            WarehouseId = ctx.WarehouseId,
            ZoneId = ctx.ZoneId,
            BinId = ctx.BinId
        });

        await Assert.ThrowsAsync<ReceiptWorkflowValidationException>(() =>
            workflow.CreateReceiptAsync(new CreateReceiptRequest
            {
                WarehouseId = ctx.WarehouseId,
                DocumentNo = UniqueCode("DOC"),
                Lines =
                [
                    new CreateReceiptLineRequest
                    {
                        LineId = "1",
                        ProductId = 9_999_999,
                        ExpectedQuantity = 1m
                    }
                ]
            }));
    }

    [Fact]
    public async Task CreateReceipt_NonPositiveExpectedQuantity_Throws()
    {
        var ctx = await SeedWarehouseWithStagingLocationAsync();
        using var scope = _fixture.Services.CreateScope();
        var workflow = ResolveWorkflow(scope);
        await workflow.SetReceivingStagingAsync(new SetReceivingStagingRequest
        {
            WarehouseId = ctx.WarehouseId,
            ZoneId = ctx.ZoneId,
            BinId = ctx.BinId
        });

        await Assert.ThrowsAsync<ReceiptWorkflowValidationException>(() =>
            workflow.CreateReceiptAsync(new CreateReceiptRequest
            {
                WarehouseId = ctx.WarehouseId,
                DocumentNo = UniqueCode("DOC"),
                Lines = [Line(ctx.ProductId, "1", 0m)]
            }));
    }

    [Fact]
    public async Task ConfirmReceive_Partial_IncreasesStagingQuantityAndUpdatesLine()
    {
        var ctx = await SeedWarehouseWithStagingLocationAsync();
        using var scope = _fixture.Services.CreateScope();
        var workflow = ResolveWorkflow(scope);
        var engine = scope.ServiceProvider.GetRequiredService<IStockEngine>();
        await workflow.SetReceivingStagingAsync(new SetReceivingStagingRequest
        {
            WarehouseId = ctx.WarehouseId,
            ZoneId = ctx.ZoneId,
            BinId = ctx.BinId
        });

        var receipt = await workflow.CreateReceiptAsync(new CreateReceiptRequest
        {
            WarehouseId = ctx.WarehouseId,
            DocumentNo = UniqueCode("DOC-CONF"),
            Lines = [Line(ctx.ProductId, "1", 10m)]
        });

        var confirmId = Guid.NewGuid();
        var result = await workflow.ConfirmReceiveAsync(new ConfirmReceiveRequest
        {
            ReceiptId = receipt.ReceiptId,
            LineId = "1",
            Quantity = 4m,
            ConfirmId = confirmId
        });

        Assert.Equal(confirmId, result.ConfirmId);
        Assert.NotEqual(Guid.Empty, result.EngineTransactionId);
        Assert.Equal(4m, result.ConfirmedQuantity);
        var updatedLine = result.Receipt.Lines.Single(l => l.LineId == "1");
        Assert.Equal(4m, updatedLine.ReceivedQuantity);
        Assert.Equal(6m, updatedLine.RemainingQuantity);
        Assert.Equal(4m, await engine.GetQuantityAsync(ctx.WarehouseId, ctx.ZoneId, ctx.BinId, ctx.ProductId));
    }

    [Fact]
    public async Task ConfirmReceive_MultiplePartials_AccumulateOnLineAndStaging()
    {
        var ctx = await SeedWarehouseWithStagingLocationAsync();
        using var scope = _fixture.Services.CreateScope();
        var workflow = ResolveWorkflow(scope);
        var engine = scope.ServiceProvider.GetRequiredService<IStockEngine>();
        await workflow.SetReceivingStagingAsync(new SetReceivingStagingRequest
        {
            WarehouseId = ctx.WarehouseId,
            ZoneId = ctx.ZoneId,
            BinId = ctx.BinId
        });

        var receipt = await workflow.CreateReceiptAsync(new CreateReceiptRequest
        {
            WarehouseId = ctx.WarehouseId,
            DocumentNo = UniqueCode("DOC-PART"),
            Lines = [Line(ctx.ProductId, "1", 10m)]
        });

        await workflow.ConfirmReceiveAsync(new ConfirmReceiveRequest
        {
            ReceiptId = receipt.ReceiptId,
            LineId = "1",
            Quantity = 3m,
            ConfirmId = Guid.NewGuid()
        });
        await workflow.ConfirmReceiveAsync(new ConfirmReceiveRequest
        {
            ReceiptId = receipt.ReceiptId,
            LineId = "1",
            Quantity = 5m,
            ConfirmId = Guid.NewGuid()
        });

        var detail = await workflow.GetReceiptByIdAsync(receipt.ReceiptId);
        Assert.NotNull(detail);
        var line = detail.Lines.Single(l => l.LineId == "1");
        Assert.Equal(8m, line.ReceivedQuantity);
        Assert.Equal(2m, line.RemainingQuantity);
        Assert.Equal(8m, await engine.GetQuantityAsync(ctx.WarehouseId, ctx.ZoneId, ctx.BinId, ctx.ProductId));
    }

    [Fact]
    public async Task ConfirmReceive_SameConfirmId_DoesNotDoubleReceive()
    {
        var ctx = await SeedWarehouseWithStagingLocationAsync();
        using var scope = _fixture.Services.CreateScope();
        var workflow = ResolveWorkflow(scope);
        var engine = scope.ServiceProvider.GetRequiredService<IStockEngine>();
        await workflow.SetReceivingStagingAsync(new SetReceivingStagingRequest
        {
            WarehouseId = ctx.WarehouseId,
            ZoneId = ctx.ZoneId,
            BinId = ctx.BinId
        });

        var receipt = await workflow.CreateReceiptAsync(new CreateReceiptRequest
        {
            WarehouseId = ctx.WarehouseId,
            DocumentNo = UniqueCode("DOC-IDEM"),
            Lines = [Line(ctx.ProductId, "1", 6m)]
        });

        var confirmId = Guid.NewGuid();
        var request = new ConfirmReceiveRequest
        {
            ReceiptId = receipt.ReceiptId,
            LineId = "1",
            Quantity = 2m,
            ConfirmId = confirmId
        };

        var first = await workflow.ConfirmReceiveAsync(request);
        var second = await workflow.ConfirmReceiveAsync(request);

        Assert.Equal(first.EngineTransactionId, second.EngineTransactionId);
        Assert.Equal(2m, await engine.GetQuantityAsync(ctx.WarehouseId, ctx.ZoneId, ctx.BinId, ctx.ProductId));
    }

    [Fact]
    public async Task ConfirmReceive_ExceedsRemaining_Throws()
    {
        var ctx = await SeedWarehouseWithStagingLocationAsync();
        using var scope = _fixture.Services.CreateScope();
        var workflow = ResolveWorkflow(scope);
        var receipt = await ConfigureStagingAndCreateReceipt(workflow, ctx);

        await Assert.ThrowsAsync<OverReceiptException>(() =>
            workflow.ConfirmReceiveAsync(new ConfirmReceiveRequest
            {
                ReceiptId = receipt.ReceiptId,
                LineId = "1",
                Quantity = 11m,
                ConfirmId = Guid.NewGuid()
            }));
    }

    [Fact]
    public async Task ConfirmReceive_OnClosedReceipt_Throws()
    {
        var ctx = await SeedWarehouseWithStagingLocationAsync();
        using var scope = _fixture.Services.CreateScope();
        var workflow = ResolveWorkflow(scope);
        var receipt = await ConfigureStagingAndCreateReceipt(workflow, ctx);

        await CloseReceiptDirectAsync(receipt.ReceiptId);

        await Assert.ThrowsAsync<ReceiptClosedException>(() =>
            workflow.ConfirmReceiveAsync(new ConfirmReceiveRequest
            {
                ReceiptId = receipt.ReceiptId,
                LineId = "1",
                Quantity = 1m,
                ConfirmId = Guid.NewGuid()
            }));
    }

    [Fact]
    public async Task ConfirmReceive_WithoutStaging_Throws()
    {
        var ctx = await SeedWarehouseWithStagingLocationAsync();
        using var scope = _fixture.Services.CreateScope();
        var workflow = ResolveWorkflow(scope);
        var receipt = await ConfigureStagingAndCreateReceipt(workflow, ctx);

        await using var connection = _fixture.ConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            "DELETE FROM receiving_staging_config WHERE warehouse_id = @WarehouseId",
            new { ctx.WarehouseId });

        await Assert.ThrowsAsync<ReceivingStagingNotConfiguredException>(() =>
            workflow.ConfirmReceiveAsync(new ConfirmReceiveRequest
            {
                ReceiptId = receipt.ReceiptId,
                LineId = "1",
                Quantity = 1m,
                ConfirmId = Guid.NewGuid()
            }));
    }

    [Fact]
    public async Task CreateReceipt_DuplicateDocumentNoInWarehouse_Throws()
    {
        var ctx = await SeedWarehouseWithStagingLocationAsync();
        using var scope = _fixture.Services.CreateScope();
        var workflow = ResolveWorkflow(scope);
        await workflow.SetReceivingStagingAsync(new SetReceivingStagingRequest
        {
            WarehouseId = ctx.WarehouseId,
            ZoneId = ctx.ZoneId,
            BinId = ctx.BinId
        });

        var documentNo = UniqueCode("DOC-DUP");
        await workflow.CreateReceiptAsync(new CreateReceiptRequest
        {
            WarehouseId = ctx.WarehouseId,
            DocumentNo = documentNo,
            Lines = [Line(ctx.ProductId, "1", 1m)]
        });

        await Assert.ThrowsAsync<DuplicateReceiptDocumentException>(() =>
            workflow.CreateReceiptAsync(new CreateReceiptRequest
            {
                WarehouseId = ctx.WarehouseId,
                DocumentNo = documentNo,
                Lines = [Line(ctx.ProductId, "2", 1m)]
            }));
    }

    private static CreateReceiptLineRequest Line(int productId, string lineId, decimal expected) =>
        new() { LineId = lineId, ProductId = productId, ExpectedQuantity = expected };

    private async Task<ReceiptDetail> ConfigureStagingAndCreateReceipt(IReceiptWorkflow workflow, SeedContext ctx)
    {
        await workflow.SetReceivingStagingAsync(new SetReceivingStagingRequest
        {
            WarehouseId = ctx.WarehouseId,
            ZoneId = ctx.ZoneId,
            BinId = ctx.BinId
        });

        return await workflow.CreateReceiptAsync(new CreateReceiptRequest
        {
            WarehouseId = ctx.WarehouseId,
            DocumentNo = UniqueCode("DOC"),
            Lines = [Line(ctx.ProductId, "1", 10m)]
        });
    }

    private async Task CloseReceiptDirectAsync(Guid receiptId)
    {
        await using var connection = _fixture.ConnectionFactory.CreateConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync(
            "UPDATE receipt SET status = 'CLOSED' WHERE receipt_id = @ReceiptId",
            new { ReceiptId = receiptId.ToString() });
    }

    private async Task<SeedContext> SeedWarehouseWithStagingLocationAsync()
    {
        var warehouses = ResolveWarehouse();
        var zones = ResolveZone();
        var bins = ResolveBin();
        var products = ResolveProduct();

        var warehouseId = await warehouses.CreateAsync(new Core.MasterData.Entities.Warehouse
        {
            Code = UniqueCode("WH"),
            Name = "Receipt WH"
        });

        var zoneId = await zones.CreateAsync(new Zone
        {
            WarehouseId = warehouseId,
            Code = UniqueCode("Z"),
            Name = "Recv"
        });

        var binId = await bins.CreateAsync(new Bin
        {
            ZoneId = zoneId,
            Code = UniqueCode("B"),
            Name = "Staging"
        });

        var productId = await products.CreateAsync(new Product
        {
            Code = UniqueCode("P"),
            Description = "Widget"
        });

        return new SeedContext(warehouseId, zoneId, binId, productId);
    }

    private IReceiptWorkflow ResolveWorkflow(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IReceiptWorkflow>();

    private IMasterDataRepository<Core.MasterData.Entities.Warehouse> ResolveWarehouse()
    {
        using var scope = _fixture.Services.CreateScope();
        return scope.ServiceProvider
            .GetRequiredService<IMasterDataRepository<Core.MasterData.Entities.Warehouse>>();
    }

    private IMasterDataRepository<Product> ResolveProduct()
    {
        using var scope = _fixture.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IMasterDataRepository<Product>>();
    }

    private IZoneRepository ResolveZone()
    {
        using var scope = _fixture.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IZoneRepository>();
    }

    private IBinRepository ResolveBin()
    {
        using var scope = _fixture.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IBinRepository>();
    }

    private static string UniqueCode(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..24];

    private sealed record SeedContext(int WarehouseId, int ZoneId, int BinId, int ProductId);
}