using BenchmarkDotNet.Attributes;

namespace NTComponents.Charts.Benchmarks;

/// <summary>
///     Ungrouped bubble pack (every item is a bubble, so collision buckets are used) while physics is awake: invalidation restarts
///     the simulation from the initial positions, then 30 frames each run a physics step. dt follows the wall clock, so positions vary
///     slightly between runs; allocations are the stable signal.
/// </summary>
public class BubblePackPhysicsBenchmarks {
    private ChartHarness _chart = null!;

    [Params(300)]
    public int Items { get; set; }

    [GlobalSetup]
    public void Setup() {
        _chart = new ChartHarness(ChartHarness.Series(typeof(NTBubblePackSeries<BenchDatum>), BenchData.Create(Items, 10),
            ("XValue", (Func<BenchDatum, object>)(d => d.Name)), ("ValueSelector", (Func<BenchDatum, decimal>)(d => d.Y)),
            // Labels off: tiny bubbles hit a Math.Clamp(min > max) ArgumentException in RenderBubbleLabel (pre-existing bug).
            ("LeafLabelSelector", (Func<BenchDatum, string?>)(d => d.Name)), ("ShowLabels", false)), legend: false);
        _chart.Paint();
    }

    [GlobalCleanup]
    public void Cleanup() => _chart.Dispose();

    [Benchmark]
    public void InvalidateAndRunPhysics30Frames() {
        _chart.InvalidateSeries();
        for (var i = 0; i < 30; i++) {
            _chart.Paint();
        }
    }
}
