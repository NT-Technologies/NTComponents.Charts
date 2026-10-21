using BenchmarkDotNet.Attributes;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Benchmarks;

/// <summary>
///     The public drawing helpers custom series use: 100 points and 20 data labels per operation, drawn straight onto a
///     raster surface (no chart frame), so only the helpers' own cost is measured.
/// </summary>
public class RenderContextExtensionsBenchmarks {
    private ChartHarness _chart = null!;
    private SKSurface _surface = null!;
    private NTRenderContext _context = null!;

    [GlobalSetup]
    public void Setup() {
        var data = BenchData.Create(20);
        _chart = new ChartHarness(ChartHarness.Series(typeof(NTLineSeries<BenchDatum>), data, ("XValue", (Func<BenchDatum, object>)(d => d.X)), ("YValueSelector", (Func<BenchDatum, decimal>)(d => d.Y))));
        _surface = SKSurface.Create(new SKImageInfo(1200, 600))!;
        _context = new NTRenderContext {
            Canvas = _surface.Canvas,
            Info = new SKImageInfo(1200, 600),
            TotalArea = new SKRect(0, 0, 1200, 600),
            PlotArea = new SKRect(0, 0, 1200, 600),
            Density = 1f,
            DefaultFont = _chart.Chart.DefaultFont,
            RegularFont = _chart.Chart.RegularFont,
            TextColor = SKColors.Black
        };
    }

    [GlobalCleanup]
    public void Cleanup() {
        _surface.Dispose();
        _chart.Dispose();
    }

    [Benchmark]
    public void DrawHundredPoints() {
        var series = _chart.Chart.Series[0];
        for (var i = 0; i < 100; i++) {
            _context.DrawPoint(_chart.Chart, series, 10 + (i * 11), 300, SKColors.Red, PointStyle.Outlined, 8, PointShape.Circle, SKColors.Blue);
        }
    }

    [Benchmark]
    public void DrawTwentyDataLabels() {
        var series = _chart.Chart.Series[0];
        for (var i = 0; i < 20; i++) {
            _context.DrawDataLabel(_chart.Chart, series, 30 + (i * 55), 300, 1234.5m + i, _context.PlotArea, "{0:N1}");
        }
    }
}
