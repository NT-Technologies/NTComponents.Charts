using BenchmarkDotNet.Attributes;
using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Benchmarks;

/// <summary>A box plot with a categorical X axis; every fifth box has outliers.</summary>
public class BoxPlotBenchmarks {
    private ChartHarness _chart = null!;

    [Params(20, 200)]
    public int Boxes { get; set; }

    [GlobalSetup]
    public void Setup() {
        var data = BenchData.Create(Boxes);
        _chart = new ChartHarness(ChartHarness.Series(typeof(NTBoxPlotSeries<BenchDatum>), data,
            ("XValue", (Func<BenchDatum, object>)(d => d.Name)),
            ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y)),
            ("BoXValue", (Func<BenchDatum, BoxPlotValues>)(d => new BoxPlotValues(d.Y - 20, d.Y - 10, d.Y, d.Y + 10, d.Y + 20, d.X % 5 == 0 ? [d.Y + 35, d.Y - 35] : null)))));
        _chart.Paint();
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
