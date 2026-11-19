using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Warehouse.StockEngine.Core.MasterData.Entities;
using Warehouse.StockEngine.Core.MasterData.Interfaces;
using Warehouse.StockEngine.Core.MasterData.Models;
using Warehouse.StockEngine.Tests.Fixtures;

namespace Warehouse.StockEngine.Tests.Integration.MasterData;

[Collection(nameof(MariaDbCollection))]
public sealed class MasterDataRepositoryIntegrationTests
{
    private readonly MariaDbTestcontainerFixture _fixture;

    public MasterDataRepositoryIntegrationTests(MariaDbTestcontainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Warehouse_CreateGetUpdateSearchDelete_Lifecycle()
    {
        var repo = ResolveWarehouse();
        var code = UniqueCode("WH");
        var name = "North DC";

        var id = await repo.CreateAsync(new Core.MasterData.Entities.Warehouse { Code = code, Name = name });

        Assert.True(id > 0);

        var byId = await repo.GetByIdAsync(id);
        Assert.NotNull(byId);
        Assert.Equal(code, byId.Code);

        var byCode = await repo.GetByCodeAsync(code);
        Assert.NotNull(byCode);
        Assert.Equal(id, byCode.Id);

        byId!.Name = "North Distribution";
        Assert.True(await repo.UpdateAsync(byId));
        Assert.Equal("North Distribution", (await repo.GetByIdAsync(id))!.Name);

        var search = await repo.SearchAsync(new MasterDataSearchRequest
        {
            SearchTerm = code,
            Page = 0,
            PageSize = 0,
        });
        Assert.Equal(1, search.TotalCount);
        Assert.Single(search.Items);
        Assert.Equal(1, search.Page);
        Assert.Equal(50, search.PageSize);

        Assert.True(await repo.DeleteAsync(id));
        Assert.False(await repo.DeleteAsync(id));
    }

    [Fact]
    public async Task Warehouse_DuplicateCodeOnCreate_Throws()
    {
        var repo = ResolveWarehouse();
        var code = UniqueCode("WH-DUP");
        await repo.CreateAsync(new Core.MasterData.Entities.Warehouse { Code = code, Name = "One" });

        await Assert.ThrowsAsync<MySqlException>(() =>
            repo.CreateAsync(new Core.MasterData.Entities.Warehouse { Code = code, Name = "Two" }));
    }

    [Fact]
    public async Task Warehouse_DeleteWithZones_Throws()
    {
        var warehouses = ResolveWarehouse();
        var zones = ResolveZone();

        var warehouseId = await warehouses.CreateAsync(new Core.MasterData.Entities.Warehouse
        {
            Code = UniqueCode("WH-Z"),
            Name = "With Zone",
        });

        await zones.CreateAsync(new Zone
        {
            WarehouseId = warehouseId,
            Code = UniqueCode("Z"),
            Name = "Aisle",
        });

        await Assert.ThrowsAsync<MySqlException>(() => warehouses.DeleteAsync(warehouseId));
    }

    [Fact]
    public async Task Warehouse_UpdateMissingId_ReturnsFalse()
    {
        var repo = ResolveWarehouse();

        Assert.False(await repo.UpdateAsync(new Core.MasterData.Entities.Warehouse
        {
            Id = 9_999_999,
            Code = UniqueCode("GHOST"),
            Name = "Missing",
        }));
    }

    [Fact]
    public async Task Product_CreateSearchUpdateDelete_Behaviour()
    {
        var repo = ResolveProduct();
        var code = UniqueCode("PRD");
        var description = "Widget XL";

        var id = await repo.CreateAsync(new Product { Code = code, Description = description });
        Assert.True(id > 0);

        var byCode = await repo.GetByCodeAsync(code);
        Assert.NotNull(byCode);

        var search = await repo.SearchAsync(new MasterDataSearchRequest { SearchTerm = "Widget" });
        Assert.Contains(search.Items, p => p.Id == id);

        byCode!.Description = "Widget XL revised";
        Assert.True(await repo.UpdateAsync(byCode));

        Assert.True(await repo.DeleteAsync(id));
        Assert.False(await repo.DeleteAsync(id));
    }

    [Fact]
    public async Task Zone_ScopedCodeAndWarehouseSearch()
    {
        var warehouses = ResolveWarehouse();
        var zones = ResolveZone();

        var warehouseA = await warehouses.CreateAsync(new Core.MasterData.Entities.Warehouse
        {
            Code = UniqueCode("WH-A"),
            Name = "A",
        });
        var warehouseB = await warehouses.CreateAsync(new Core.MasterData.Entities.Warehouse
        {
            Code = UniqueCode("WH-B"),
            Name = "B",
        });

        var sharedZoneCode = UniqueCode("ZONE");
        await zones.CreateAsync(new Zone { WarehouseId = warehouseA, Code = sharedZoneCode, Name = "A1" });
        await zones.CreateAsync(new Zone { WarehouseId = warehouseB, Code = sharedZoneCode, Name = "B1" });

        var fromA = await zones.GetByCodeAsync(warehouseA, sharedZoneCode);
        var fromB = await zones.GetByCodeAsync(warehouseB, sharedZoneCode);
        Assert.NotNull(fromA);
        Assert.NotNull(fromB);
        Assert.NotEqual(fromA!.Id, fromB!.Id);

        await Assert.ThrowsAsync<MySqlException>(() =>
            zones.CreateAsync(new Zone { WarehouseId = warehouseA, Code = sharedZoneCode, Name = "Dup" }));

        var paged = await zones.SearchAsync(new MasterDataSearchRequest
        {
            WarehouseId = warehouseA,
            Page = 1,
            PageSize = 10,
        });
        Assert.True(paged.TotalCount >= 1);
        Assert.All(paged.Items, z => Assert.Equal(warehouseA, z.WarehouseId));
    }

    [Fact]
    public async Task Bin_ScopedCodeSearchAndDeleteRestriction()
    {
        var warehouses = ResolveWarehouse();
        var zones = ResolveZone();
        var bins = ResolveBin();

        var warehouseId = await warehouses.CreateAsync(new Core.MasterData.Entities.Warehouse
        {
            Code = UniqueCode("WH-BIN"),
            Name = "Bin WH",
        });
        var zoneA = await zones.CreateAsync(new Zone
        {
            WarehouseId = warehouseId,
            Code = UniqueCode("Z-A"),
            Name = "Zone A",
        });
        var zoneB = await zones.CreateAsync(new Zone
        {
            WarehouseId = warehouseId,
            Code = UniqueCode("Z-B"),
            Name = "Zone B",
        });

        var sharedBinCode = UniqueCode("BIN");
        var binA = await bins.CreateAsync(new Bin { ZoneId = zoneA, Code = sharedBinCode, Name = "Bin A" });
        var binB = await bins.CreateAsync(new Bin { ZoneId = zoneB, Code = sharedBinCode, Name = "Bin B" });

        Assert.NotEqual(binA, binB);
        Assert.NotNull(await bins.GetByCodeAsync(zoneA, sharedBinCode));
        Assert.NotNull(await bins.GetByCodeAsync(zoneB, sharedBinCode));

        await Assert.ThrowsAsync<MySqlException>(() =>
            zones.DeleteAsync(zoneA));

        await Assert.ThrowsAsync<MySqlException>(() =>
            bins.CreateAsync(new Bin { ZoneId = zoneA, Code = sharedBinCode, Name = "Dup bin" }));

        var paged = await bins.SearchAsync(new MasterDataSearchRequest
        {
            ZoneId = zoneA,
            Page = 1,
            PageSize = 10,
        });
        Assert.True(paged.TotalCount >= 1);
        Assert.All(paged.Items, b => Assert.Equal(zoneA, b.ZoneId));

        Assert.True(await bins.DeleteAsync(binA));
        Assert.True(await zones.DeleteAsync(zoneA));
    }

    [Fact]
    public async Task Product_DuplicateCodeOnCreate_Throws()
    {
        var repo = ResolveProduct();
        var code = UniqueCode("PRD-DUP");
        await repo.CreateAsync(new Product { Code = code, Description = "One" });

        await Assert.ThrowsAsync<MySqlException>(() =>
            repo.CreateAsync(new Product { Code = code, Description = "Two" }));
    }

    [Fact]
    public async Task Warehouse_GetAllAsync_ReturnsCreatedEntity()
    {
        var repo = ResolveWarehouse();
        var code = UniqueCode("ALL");
        var id = await repo.CreateAsync(new Core.MasterData.Entities.Warehouse { Code = code, Name = "All Test" });

        var all = await repo.GetAllAsync();
        Assert.Contains(all, w => w.Id == id);
    }

    [Fact]
    public async Task Product_GetAllAsync_ReturnsCreatedEntity()
    {
        var repo = ResolveProduct();
        var id = await repo.CreateAsync(new Product { Code = UniqueCode("ALL-P"), Description = "Listed" });

        var all = await repo.GetAllAsync();
        Assert.Contains(all, p => p.Id == id);
    }

    [Fact]
    public async Task Zone_GetAllAsync_ReturnsCreatedEntity()
    {
        var warehouses = ResolveWarehouse();
        var zones = ResolveZone();
        var warehouseId = await warehouses.CreateAsync(new Core.MasterData.Entities.Warehouse
        {
            Code = UniqueCode("WH-ALL-Z"),
            Name = "Zone list WH",
        });
        var id = await zones.CreateAsync(new Zone
        {
            WarehouseId = warehouseId,
            Code = UniqueCode("Z-ALL"),
            Name = "Listed zone",
        });

        var all = await zones.GetAllAsync();
        Assert.Contains(all, z => z.Id == id);
    }

    [Fact]
    public async Task Bin_GetAllAsync_ReturnsCreatedEntity()
    {
        var warehouses = ResolveWarehouse();
        var zones = ResolveZone();
        var bins = ResolveBin();
        var warehouseId = await warehouses.CreateAsync(new Core.MasterData.Entities.Warehouse
        {
            Code = UniqueCode("WH-ALL-B"),
            Name = "Bin list WH",
        });
        var zoneId = await zones.CreateAsync(new Zone
        {
            WarehouseId = warehouseId,
            Code = UniqueCode("Z-ALL-B"),
            Name = "Bin list zone",
        });
        var id = await bins.CreateAsync(new Bin { ZoneId = zoneId, Code = UniqueCode("B-ALL"), Name = "Listed bin" });

        var all = await bins.GetAllAsync();
        Assert.Contains(all, b => b.Id == id);
    }

    [Fact]
    public async Task Warehouse_Search_MatchesName()
    {
        var repo = ResolveWarehouse();
        var name = $"Searchable-{Guid.NewGuid():N}"[..20];
        var id = await repo.CreateAsync(new Core.MasterData.Entities.Warehouse
        {
            Code = UniqueCode("WH-NAME"),
            Name = name,
        });

        var search = await repo.SearchAsync(new MasterDataSearchRequest { SearchTerm = name });
        Assert.Contains(search.Items, w => w.Id == id);
    }

    [Fact]
    public async Task Product_GetByIdAsync_ReturnsCreatedEntity()
    {
        var repo = ResolveProduct();
        var code = UniqueCode("BY-ID");
        var id = await repo.CreateAsync(new Product { Code = code, Description = "Lookup by id" });

        var byId = await repo.GetByIdAsync(id);
        Assert.NotNull(byId);
        Assert.Equal(code, byId.Code);
    }

    [Fact]
    public async Task Product_UpdateMissingId_ReturnsFalse()
    {
        var repo = ResolveProduct();

        Assert.False(await repo.UpdateAsync(new Product
        {
            Id = 9_999_999,
            Code = UniqueCode("GHOST-P"),
            Description = "Missing",
        }));
    }

    [Fact]
    public async Task Zone_UpdateMissingId_ReturnsFalse()
    {
        var zones = ResolveZone();

        Assert.False(await zones.UpdateAsync(new Zone
        {
            Id = 9_999_999,
            WarehouseId = 1,
            Code = UniqueCode("GHOST-Z"),
            Name = "Missing",
        }));
    }

    [Fact]
    public async Task Bin_UpdateMissingId_ReturnsFalse()
    {
        var bins = ResolveBin();

        Assert.False(await bins.UpdateAsync(new Bin
        {
            Id = 9_999_999,
            ZoneId = 1,
            Code = UniqueCode("GHOST-B"),
            Name = "Missing",
        }));
    }

    [Fact]
    public async Task Search_UnknownSortField_FallsBackToDefaultCodeOrder()
    {
        var repo = ResolveWarehouse();
        var codeA = UniqueCode("SORT-A");
        var codeB = UniqueCode("SORT-B");
        await repo.CreateAsync(new Core.MasterData.Entities.Warehouse { Code = codeA, Name = "A" });
        await repo.CreateAsync(new Core.MasterData.Entities.Warehouse { Code = codeB, Name = "B" });

        var result = await repo.SearchAsync(new MasterDataSearchRequest
        {
            SearchTerm = "SORT-",
            SortBy = "invalid",
            SortDescending = false,
            PageSize = 50,
        });

        var ordered = result.Items.Select(w => w.Code).ToList();
        Assert.Equal(ordered, ordered.OrderBy(c => c, StringComparer.Ordinal).ToList());
    }

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
}
