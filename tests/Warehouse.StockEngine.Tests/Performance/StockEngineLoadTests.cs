using System.Collections.Concurrent;
using System.Diagnostics;
using NBomber.CSharp;
using Warehouse.StockEngine.Core.Commands;
using Warehouse.StockEngine.Core.Interfaces;
using Warehouse.StockEngine.Tests.Fixtures;
using Xunit.Abstractions;

namespace Warehouse.StockEngine.Tests.Performance;

[Collection(nameof(MariaDbCollection))]
public sealed class StockEngineLoadTests
{
    private const int Warehouse = 1;
    private const int Seed = 42;

    private readonly MariaDbTestcontainerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public StockEngineLoadTests(MariaDbTestcontainerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public void MultiPhase_Warehouse_Load_CompletesWithoutErrors()
    {
        var rng = new Random(Seed);
        var skuSeq = Random.Shared.NextInt64(1_000_000_000, 8_000_000_000);
        long NextSku() => Interlocked.Increment(ref skuSeq);

        var locations = BuildLocationPool();
        var stock = new StockLedger();

        // --- Phase 1: plan + run inbounds ---
        var inbounds = PlanInbounds(rng, locations, NextSku, count: 10);
        foreach (var cmd in inbounds)
        {
            foreach (var line in cmd.Lines)
            {
                stock.Add(line.Warehouse, line.Zone, line.Bin, line.Sku, line.Quantity);
            }
        }

        RunPhase(
            "phase1_inbound",
            inbounds,
            concurrency: 10,
            async cmd => await _fixture.Engine.InboundAsync(cmd));

        // --- Phase 2: plan moves from reserved stock (disjoint) ---
        var moves = PlanMoves(rng, stock, count: 10);
        RunPhase(
            "phase2_move",
            moves,
            concurrency: 10,
            async cmd => await _fixture.Engine.MoveAsync(cmd));

        // --- Phase 3: plan outbounds from remaining stock ---
        var outbounds = PlanOutbounds(rng, stock, count: 30);
        RunPhase(
            "phase3_outbound",
            outbounds,
            concurrency: 15,
            async cmd => await _fixture.Engine.OutboundAsync(cmd));

        // --- Phase 4: counts + bijstuur inbounds ---
        var counts = PlanCounts(rng, stock, count: 12);
        var bijstuur = PlanBijstuurInbounds(rng, locations, NextSku, count: 5);
        foreach (var cmd in bijstuur)
        {
            foreach (var line in cmd.Lines)
            {
                stock.Add(line.Warehouse, line.Zone, line.Bin, line.Sku, line.Quantity);
            }
        }

        RunPhase(
            "phase4_count",
            counts,
            concurrency: 6,
            async cmd => await _fixture.Engine.CountAsync(cmd));

        RunPhase(
            "phase4_bijstuur_inbound",
            bijstuur,
            concurrency: 5,
            async cmd => await _fixture.Engine.InboundAsync(cmd));

        // Spot-check a sample of ledger vs DB
        var mismatches = VerifySampleStock(_fixture.Engine, stock, sampleSize: 25, rng);
        Assert.True(mismatches == 0, $"Expected ledger and DB to match; mismatches={mismatches}");

        _output.WriteLine(
            """
            Load test stats (seed={0}):
              inbounds={1} (lines={2})
              moves={3} (lines={4})
              outbounds={5} (lines={6})
              counts={7} (lines={8})
              bijstuur_inbounds={9} (lines={10})
              tracked_lots={11}
              stock_mismatches={12}
            """,
            Seed,
            inbounds.Count, inbounds.Sum(c => c.Lines.Count),
            moves.Count, moves.Sum(c => c.Lines.Count),
            outbounds.Count, outbounds.Sum(c => c.Lines.Count),
            counts.Count, counts.Sum(c => c.Lines.Count),
            bijstuur.Count, bijstuur.Sum(c => c.Lines.Count),
            stock.LotCount,
            mismatches);
    }

    private void RunPhase<TCommand>(
        string name,
        IReadOnlyList<TCommand> commands,
        int concurrency,
        Func<TCommand, Task> execute)
    {
        if (commands.Count == 0)
        {
            _output.WriteLine($"[{name}] skipped (0 commands)");
            return;
        }

        var queue = new ConcurrentQueue<TCommand>(commands);
        var sw = Stopwatch.StartNew();

        var scenario = Scenario.Create(name, async _ =>
            {
                if (!queue.TryDequeue(out var command))
                {
                    return Response.Ok();
                }

                try
                {
                    await execute(command);
                    return Response.Ok();
                }
                catch (Exception ex)
                {
                    return Response.Fail(statusCode: "engine_error", message: ex.Message);
                }
            })
            .WithoutWarmUp()
            .WithLoadSimulations(
                Simulation.IterationsForConstant(
                    copies: Math.Min(concurrency, commands.Count),
                    iterations: commands.Count));

        var stats = NBomberRunner
            .RegisterScenarios(scenario)
            .WithReportFolder("nbomber-reports")
            .Run();

        sw.Stop();

        var scn = stats.ScenarioStats.Single(s => s.ScenarioName == name);
        _output.WriteLine(
            "[{0}] ops={1} ok={2} fail={3} elapsed={4:g}",
            name,
            commands.Count,
            scn.Ok.Request.Count,
            scn.Fail.Request.Count,
            sw.Elapsed);

        Assert.True(
            scn.Fail.Request.Count == 0,
            $"[{name}] expected zero failures, got {scn.Fail.Request.Count}");
        Assert.True(queue.IsEmpty, $"[{name}] not all commands were dequeued");
    }

    private static List<(int Zone, int Bin)> BuildLocationPool()
    {
        var locations = new List<(int Zone, int Bin)>();
        for (var zone = 1; zone <= 5; zone++)
        {
            for (var bin = 1; bin <= 20; bin++)
            {
                locations.Add((zone, bin));
            }
        }

        return locations;
    }

    private static List<InboundStockCommand> PlanInbounds(
        Random rng,
        IReadOnlyList<(int Zone, int Bin)> locations,
        Func<long> nextSku,
        int count)
    {
        var commands = new List<InboundStockCommand>(count);
        for (var i = 0; i < count; i++)
        {
            var lineCount = rng.Next(10, 21);
            var lines = new List<InboundStockLine>(lineCount);
            for (var line = 0; line < lineCount; line++)
            {
                var (zone, bin) = locations[rng.Next(locations.Count)];
                lines.Add(new InboundStockLine(
                    DocumentLineId: (line + 1).ToString(),
                    Warehouse: Warehouse,
                    Zone: zone,
                    Bin: bin,
                    Sku: nextSku(),
                    Quantity: rng.Next(3, 21)));
            }

            commands.Add(new InboundStockCommand(
                Guid.NewGuid(),
                $"IN-LOAD-{i + 1:D3}",
                lines,
                Source: "LOADTEST"));
        }

        return commands;
    }

    private static List<MoveStockCommand> PlanMoves(Random rng, StockLedger stock, int count)
    {
        var commands = new List<MoveStockCommand>(count);
        for (var i = 0; i < count; i++)
        {
            var lineCount = rng.Next(3, 11);
            var lines = new List<MoveStockLine>(lineCount);

            for (var line = 0; line < lineCount; line++)
            {
                if (!stock.TryPickLot(rng, minQty: 1, out var lot))
                {
                    break;
                }

                var qty = Math.Min(rng.Next(1, 6), (int)lot.Quantity);
                if (qty <= 0 || !stock.TryReserve(lot.Warehouse, lot.Zone, lot.Bin, lot.Sku, qty))
                {
                    break;
                }

                var (toZone, toBin) = PickDifferentBin(rng, lot.Zone, lot.Bin);
                stock.Add(lot.Warehouse, toZone, toBin, lot.Sku, qty);

                lines.Add(new MoveStockLine(
                    DocumentLineId: (line + 1).ToString(),
                    Warehouse: lot.Warehouse,
                    FromZone: lot.Zone,
                    FromBin: lot.Bin,
                    ToZone: toZone,
                    ToBin: toBin,
                    Sku: lot.Sku,
                    Quantity: qty));
            }

            if (lines.Count > 0)
            {
                commands.Add(new MoveStockCommand(
                    Guid.NewGuid(),
                    $"MV-LOAD-{i + 1:D3}",
                    lines,
                    Source: "LOADTEST"));
            }
        }

        return commands;
    }

    private static List<OutboundStockCommand> PlanOutbounds(Random rng, StockLedger stock, int count)
    {
        var commands = new List<OutboundStockCommand>(count);
        for (var i = 0; i < count; i++)
        {
            var lineCount = rng.Next(3, 11);
            var lines = new List<OutboundStockLine>(lineCount);

            for (var line = 0; line < lineCount; line++)
            {
                if (!stock.TryPickLot(rng, minQty: 1, out var lot))
                {
                    break;
                }

                var qty = Math.Min(rng.Next(1, 6), (int)lot.Quantity);
                if (qty <= 0 || !stock.TryReserve(lot.Warehouse, lot.Zone, lot.Bin, lot.Sku, qty))
                {
                    break;
                }

                lines.Add(new OutboundStockLine(
                    DocumentLineId: (line + 1).ToString(),
                    Warehouse: lot.Warehouse,
                    Zone: lot.Zone,
                    Bin: lot.Bin,
                    Sku: lot.Sku,
                    Quantity: qty));
            }

            if (lines.Count > 0)
            {
                commands.Add(new OutboundStockCommand(
                    Guid.NewGuid(),
                    $"OUT-LOAD-{i + 1:D3}",
                    lines,
                    Source: "LOADTEST"));
            }
        }

        return commands;
    }

    private static List<CountStockCommand> PlanCounts(Random rng, StockLedger stock, int count)
    {
        var pool = stock.SnapshotPositiveLots();
        if (pool.Count == 0)
        {
            return [];
        }

        // Shuffle so each lot is assigned to at most one count line (safe under concurrency).
        for (var i = pool.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        var commands = new List<CountStockCommand>(count);
        var offset = 0;
        for (var i = 0; i < count && offset < pool.Count; i++)
        {
            var lineCount = Math.Min(rng.Next(2, 6), pool.Count - offset);
            var lines = new List<CountStockLine>(lineCount);

            for (var line = 0; line < lineCount; line++)
            {
                var lot = pool[offset++];
                decimal counted;
                string? reason;
                if (rng.Next(2) == 0)
                {
                    counted = lot.Quantity;
                    reason = "MATCH";
                }
                else
                {
                    var delta = rng.Next(1, 6) * (rng.Next(2) == 0 ? 1 : -1);
                    counted = Math.Max(0, lot.Quantity + delta);
                    reason = "ADJUST";
                    stock.Set(lot.Warehouse, lot.Zone, lot.Bin, lot.Sku, counted);
                }

                lines.Add(new CountStockLine(
                    DocumentLineId: (line + 1).ToString(),
                    Warehouse: lot.Warehouse,
                    Zone: lot.Zone,
                    Bin: lot.Bin,
                    Sku: lot.Sku,
                    CountedQuantity: counted,
                    ReasonCode: reason));
            }

            commands.Add(new CountStockCommand(
                Guid.NewGuid(),
                $"CNT-LOAD-{i + 1:D3}",
                lines,
                Source: "LOADTEST",
                ReasonCode: "CYCLE"));
        }

        return commands;
    }

    private static List<InboundStockCommand> PlanBijstuurInbounds(
        Random rng,
        IReadOnlyList<(int Zone, int Bin)> locations,
        Func<long> nextSku,
        int count)
    {
        var commands = new List<InboundStockCommand>(count);
        for (var i = 0; i < count; i++)
        {
            var lineCount = rng.Next(1, 4);
            var lines = new List<InboundStockLine>(lineCount);
            for (var line = 0; line < lineCount; line++)
            {
                var (zone, bin) = locations[rng.Next(locations.Count)];
                lines.Add(new InboundStockLine(
                    DocumentLineId: (line + 1).ToString(),
                    Warehouse: Warehouse,
                    Zone: zone,
                    Bin: bin,
                    Sku: nextSku(),
                    Quantity: rng.Next(1, 6)));
            }

            commands.Add(new InboundStockCommand(
                Guid.NewGuid(),
                $"IN-BIJ-{i + 1:D3}",
                lines,
                Source: "BIJSTUUR",
                ReasonCode: "CORRECTION"));
        }

        return commands;
    }

    private static (int Zone, int Bin) PickDifferentBin(Random rng, int fromZone, int fromBin)
    {
        int zone;
        int bin;
        do
        {
            zone = rng.Next(1, 6);
            bin = rng.Next(1, 21);
        } while (zone == fromZone && bin == fromBin);

        return (zone, bin);
    }

    private static int VerifySampleStock(IStockEngine engine, StockLedger stock, int sampleSize, Random rng)
    {
        var lots = stock.SnapshotPositiveLots();
        if (lots.Count == 0)
        {
            return 0;
        }

        var mismatches = 0;
        var take = Math.Min(sampleSize, lots.Count);
        for (var i = 0; i < take; i++)
        {
            var lot = lots[rng.Next(lots.Count)];
            var dbQty = engine.GetQuantityAsync(lot.Warehouse, lot.Zone, lot.Bin, lot.Sku)
                .GetAwaiter().GetResult();
            if (dbQty != lot.Quantity)
            {
                mismatches++;
            }
        }

        return mismatches;
    }

    private readonly record struct StockKey(int Warehouse, int Zone, int Bin, long Sku);

    private sealed record StockLot(int Warehouse, int Zone, int Bin, long Sku, decimal Quantity)
    {
        public StockKey Key => new(Warehouse, Zone, Bin, Sku);
    }

    /// <summary>
    /// Shadow stock used only to plan non-overlapping reservations so concurrent phases cannot
    /// race into insufficient-stock failures.
    /// </summary>
    private sealed class StockLedger
    {
        private readonly Dictionary<StockKey, decimal> _qty = new();

        public int LotCount => _qty.Count;

        public void Add(int warehouse, int zone, int bin, long sku, decimal quantity)
        {
            var key = new StockKey(warehouse, zone, bin, sku);
            _qty[key] = _qty.TryGetValue(key, out var current) ? current + quantity : quantity;
        }

        public void Set(int warehouse, int zone, int bin, long sku, decimal quantity)
        {
            var key = new StockKey(warehouse, zone, bin, sku);
            if (quantity <= 0)
            {
                _qty.Remove(key);
            }
            else
            {
                _qty[key] = quantity;
            }
        }

        public bool TryReserve(int warehouse, int zone, int bin, long sku, decimal quantity)
        {
            var key = new StockKey(warehouse, zone, bin, sku);
            if (!_qty.TryGetValue(key, out var available) || available < quantity)
            {
                return false;
            }

            var remaining = available - quantity;
            if (remaining == 0)
            {
                _qty.Remove(key);
            }
            else
            {
                _qty[key] = remaining;
            }

            return true;
        }

        public bool TryPickLot(Random rng, int minQty, out StockLot lot)
        {
            var candidates = _qty
                .Where(kv => kv.Value >= minQty)
                .Select(kv => new StockLot(kv.Key.Warehouse, kv.Key.Zone, kv.Key.Bin, kv.Key.Sku, kv.Value))
                .ToList();

            if (candidates.Count == 0)
            {
                lot = null!;
                return false;
            }

            lot = candidates[rng.Next(candidates.Count)];
            return true;
        }

        public List<StockLot> SnapshotPositiveLots()
            => _qty
                .Where(kv => kv.Value > 0)
                .Select(kv => new StockLot(kv.Key.Warehouse, kv.Key.Zone, kv.Key.Bin, kv.Key.Sku, kv.Value))
                .ToList();
    }
}
