using NBomber.CSharp;
using Warehouse.StockEngine.Core.Commands;
using Warehouse.StockEngine.Tests.Fixtures;

namespace Warehouse.StockEngine.Tests.Performance;

[Collection(nameof(MariaDbCollection))]
public sealed class StockEngineLoadTests
{
    private readonly MariaDbTestcontainerFixture _fixture;

    public StockEngineLoadTests(MariaDbTestcontainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Inbound_Load_CompletesWithoutErrors()
    {
        var sku = Random.Shared.NextInt64(1_000_000, 9_000_000_000);
        await _fixture.Engine.InboundAsync(new InboundStockCommand(
            Guid.NewGuid(),
            "LOAD-SEED",
            [new InboundStockLine("1", 1, 90, 900, sku, 1m)]));

        var scenario = Scenario.Create("inbound_load", async context =>
            {
                var command = new InboundStockCommand(
                    Guid.NewGuid(),
                    $"LOAD-{context.InvocationNumber}",
                    [new InboundStockLine("1", 1, 90, 900, sku, 1m)]);

                await _fixture.Engine.InboundAsync(command);
                return Response.Ok();
            })
            .WithLoadSimulations(
                Simulation.KeepConstant(copies: 5, during: TimeSpan.FromSeconds(5)));

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder("nbomber-reports")
            .Run();

        var scn = stats.ScenarioStats.Single(s => s.ScenarioName == "inbound_load");
        Assert.True(scn.Fail.Request.Count == 0, $"Expected zero failures, got {scn.Fail.Request.Count}");
        Assert.True(scn.Ok.Request.Count > 0);
    }
}
