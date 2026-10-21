using BenchmarkDotNet.Attributes;

namespace NTComponents.Charts.Benchmarks;

/// <summary>Grouped bubble pack after the physics settled: steady repaint, invalidation and hover.</summary>
public class BubblePackBenchmarks {
    private ChartHarness _chart = null!;

    [Params(100, 1_000)]
    public int Items { get; set; }

    [GlobalSetup]
    public void Setup() {
        _chart = new ChartHarness(ChartHarness.Series(typeof(NTBubblePackSeries<BenchDatum>), BenchData.Create(Items, 10),
            ("XValue", (Func<BenchDatum, object>)(d => d.Name)), ("ValueSelector", (Func<BenchDatum, decimal>)(d => d.Y)),
            ("GroupSelector", (Func<BenchDatum, string?>)(d => d.Group)), ("LeafLabelSelector", (Func<BenchDatum, string?>)(d => d.Name))), legend: false);
        if (!_chart.SettleAnimations()) {
            Console.WriteLine($"// WARNING: BubblePack ({Items} items) physics did not settle; steady numbers include physics steps.");
        }
    }

    [GlobalCleanup]
    public void Cleanup() => _chart.Dispose();

    [Benchmark]
    public void SteadyRepaint() => _chart.Paint();

    [Benchmark]
    public void InvalidateAndRepaint() {
        _chart.InvalidateSeries();
        _chart.Paint();
    }

    [Benchmark]
    public void HoverMoveAndRepaint() {
        _chart.MoveMouse();
        _chart.Paint();
    }
}
