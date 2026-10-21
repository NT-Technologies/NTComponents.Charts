using BenchmarkDotNet.Attributes;

namespace NTComponents.Charts.Benchmarks;

/// <summary>Grouped (three series) and stacked (segment selector) bar charts.</summary>
public class BarBenchmarks {
    private ChartHarness _grouped = null!;
    private ChartHarness _stacked = null!;

    [Params(20, 500)]
    public int Categories { get; set; }

    [GlobalSetup]
    public void Setup() {
        var data = BenchData.Create(Categories);
        var x = (Func<BenchDatum, object>)(d => d.Name);
        _grouped = new ChartHarness(ChartHarness.Combine(
            ChartHarness.Series(typeof(NTBarSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y)), ("ShowDataLabels", false)),
            ChartHarness.Series(typeof(NTBarSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y2)), ("ShowDataLabels", false)),
            ChartHarness.Series(typeof(NTBarSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y3)), ("ShowDataLabels", false))));
        _stacked = new ChartHarness(ChartHarness.Series(typeof(NTBarSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y + d.Y2 + d.Y3)),
            ("SegmentSelector", (Func<BenchDatum, IEnumerable<NTBarSegment>>)(d => [new NTBarSegment { Value = d.Y, Label = "A" }, new NTBarSegment { Value = d.Y2, Label = "B" }, new NTBarSegment { Value = d.Y3, Label = "C" }])),
            ("ShowDataLabels", false)));
        _grouped.Paint();
        _stacked.Paint();
    }

    [GlobalCleanup]
    public void Cleanup() {
        _grouped.Dispose();
        _stacked.Dispose();
    }

    [Benchmark]
    public void GroupedSteadyRepaint() => _grouped.Paint();

    [Benchmark]
    public void StackedSteadyRepaint() => _stacked.Paint();

    [Benchmark]
    public void GroupedInvalidateAndRepaint() {
        _grouped.InvalidateSeries();
        _grouped.Paint();
    }

    [Benchmark]
    public void GroupedHoverMoveAndRepaint() {
        _grouped.MoveMouse();
        _grouped.Paint();
    }

    [Benchmark]
    public void StackedHoverMoveAndRepaint() {
        _stacked.MoveMouse();
        _stacked.Paint();
    }

    [Benchmark]
    public void GroupedResizeRepaint() => _grouped.PaintAlternatingSize();
}
