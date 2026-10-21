using BenchmarkDotNet.Attributes;
using NTComponents.Charts.Core.Axes;
using SkiaSharp;

namespace NTComponents.Charts.Benchmarks;

/// <summary>
///     Isolates per-frame fixed costs on the three-series, 100-point line chart: the raw raster clear (floor that no managed
///     change can remove) and the range calculations that run several times per frame.
/// </summary>
public class FrameCostBenchmarks {
    private ChartHarness _chart = null!;
    private SKSurface _surface = null!;
    private NTAxisOptions<BenchDatum> _xAxis = null!;
    private NTAxisOptions<BenchDatum> _yAxis = null!;

    [GlobalSetup]
    public void Setup() {
        var data = BenchData.Create(100);
        var x = (Func<BenchDatum, object>)(d => d.X);
        _chart = new ChartHarness(ChartHarness.Combine(
            ChartHarness.Series(typeof(NTLineSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y))),
            ChartHarness.Series(typeof(NTLineSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y2))),
            ChartHarness.Series(typeof(NTLineSeries<BenchDatum>), data, ("XValue", x), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y3)))));
        _chart.Paint();
        _surface = SKSurface.Create(new SKImageInfo(1200, 600))!;
        _xAxis = (NTAxisOptions<BenchDatum>)_chart.Chart.XAxis;
        _yAxis = (NTAxisOptions<BenchDatum>)_chart.Chart.YAxis;
    }

    [GlobalCleanup]
    public void Cleanup() {
        _surface.Dispose();
        _chart.Dispose();
    }

    [Benchmark]
    public void RasterClearOnly() => _surface.Canvas.Clear(SKColors.White);

    [Benchmark]
    public (double, double) GetXRange() => _chart.Chart.GetXRange(_xAxis, true);

    [Benchmark]
    public (decimal, decimal) GetYRange() => _chart.Chart.GetYRange(_yAxis, true);
}
