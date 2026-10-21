using System.Reflection;
using Bunit.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Axes;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

/// <summary>X value flavour used by the chart's registered X axis.</summary>
public enum XAxisKind {
    /// <summary>No axis registered: the chart's default (categorical) X axis is used.</summary>
    Default,
    Int,
    Double,
    DateTime
}

/// <summary>Plain data row shared by all cartesian series tests.</summary>
public sealed record Pt(object X, decimal Y, decimal Z = 0m);

/// <summary>Describes the axes and chart-level parameters of a test chart.</summary>
public sealed class ChartSetup {
    public XAxisKind XAxis { get; init; } = XAxisKind.Int;
    public bool SecondaryYAxis { get; init; }
    public (string Name, object Value)[] XAxisAttributes { get; init; } = [];
    public (string Name, object Value)[] YAxisAttributes { get; init; } = [];
    public (string Name, object Value)[] ChartAttributes { get; init; } = [];
}

/// <summary>A legend entry read back from the internal legend contract of a series.</summary>
public sealed record LegendEntry(string Label, SKColor Color, string? Key, int? Index, bool IsVisible);

/// <summary>
///     Shared bUnit/Skia harness for cartesian series tests. Charts are rendered with the Skia canvas views stubbed and
///     series are drawn onto an off-screen raster surface so geometry can be asserted through hit tests and pixels.
/// </summary>
public abstract class CartesianChartTestBase : BunitContext {
    protected const int SurfaceWidth = 300;
    protected const int SurfaceHeight = 200;
    protected static readonly SKRect Area = new(0, 0, SurfaceWidth, SurfaceHeight);
    protected static readonly SKColor Red = new(0xFF, 0, 0);
    protected static readonly SKColor Blue = new(0, 0, 0xFF);
    protected static readonly SKColor Green = new(0, 0xFF, 0);
    protected const BindingFlags NonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

    public static readonly Func<Pt, object> XSelector = p => p.X;
    public static readonly Func<Pt, decimal> YSelector = p => p.Y;

    protected CartesianChartTestBase() {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./_content/NTComponents.Charts/ntcomponents-charts.js");
        module.Setup<float>("getDevicePixelRatio").SetResult(1f);
        module.Setup<Dictionary<string, string?>>("getThemeColors", _ => true).SetResult(new Dictionary<string, string?> {
            [nameof(NTColor.Primary)] = "#FF0000",
            [nameof(NTColor.Secondary)] = "#0000FF",
            [nameof(NTColor.Tertiary)] = "#00FF00",
            [nameof(NTColor.Error)] = "#FFFF00",
            [nameof(NTColor.OutlineVariant)] = "#808080",
            [nameof(NTColor.Outline)] = "#404040"
        });
        ComponentFactories.AddStub(type => type.FullName is "SkiaSharp.Views.Blazor.SKGLView" or "SkiaSharp.Views.Blazor.SKCanvasView");
    }

    /// <summary>Creates a render fragment for a series component with the common wiring and optional overrides.</summary>
    protected static RenderFragment SeriesFragment<TSeries>(IEnumerable<Pt> data, params (string Name, object Value)[] overrides) where TSeries : NTCartesianSeries<Pt> => builder => {
        var attributes = new Dictionary<string, object> {
            ["Data"] = data,
            ["XValue"] = XSelector,
            ["YValueSelector"] = YSelector,
            ["AnimationEnabled"] = false,
            ["ShowDataLabels"] = false
        };
        foreach (var (name, value) in overrides) {
            attributes[name] = value;
        }

        builder.OpenComponent(0, typeof(TSeries));
        var sequence = 1;
        foreach (var attribute in attributes) {
            builder.AddAttribute(sequence++, attribute.Key, attribute.Value);
        }
        builder.CloseComponent();
    };

    private static void AddAttributes(RenderTreeBuilder builder, ref int sequence, (string Name, object Value)[] attributes) {
        foreach (var (name, value) in attributes) {
            builder.AddAttribute(sequence++, name, value);
        }
    }

    /// <summary>Builds the chart content: optional axes first, then every series fragment.</summary>
    protected static RenderFragment Content(ChartSetup setup, params RenderFragment[] series) => builder => {
        var sequence = 0;
        switch (setup.XAxis) {
            case XAxisKind.Int:
                builder.OpenComponent<NTXAxisOptions<Pt, int>>(sequence++);
                builder.AddAttribute(sequence++, nameof(NTXAxisOptions<Pt, int>.ValueSelector), (Func<Pt, int>)(p => Convert.ToInt32(p.X)));
                AddAttributes(builder, ref sequence, setup.XAxisAttributes);
                builder.CloseComponent();
                break;
            case XAxisKind.Double:
                builder.OpenComponent<NTXAxisOptions<Pt, double>>(sequence++);
                builder.AddAttribute(sequence++, nameof(NTXAxisOptions<Pt, double>.ValueSelector), (Func<Pt, double>)(p => Convert.ToDouble(p.X)));
                AddAttributes(builder, ref sequence, setup.XAxisAttributes);
                builder.CloseComponent();
                break;
            case XAxisKind.DateTime:
                builder.OpenComponent<NTXAxisOptions<Pt, DateTime>>(sequence++);
                builder.AddAttribute(sequence++, nameof(NTXAxisOptions<Pt, DateTime>.ValueSelector), (Func<Pt, DateTime>)(p => (DateTime)p.X));
                AddAttributes(builder, ref sequence, setup.XAxisAttributes);
                builder.CloseComponent();
                break;
        }

        builder.OpenComponent<NTYAxisOptions<Pt, decimal>>(sequence++);
        builder.AddAttribute(sequence++, nameof(NTYAxisOptions<Pt, decimal>.ValueSelector), (Func<Pt, decimal>)(p => p.Y));
        AddAttributes(builder, ref sequence, setup.YAxisAttributes);
        builder.CloseComponent();
        if (setup.SecondaryYAxis) {
            builder.OpenComponent<NTYAxisOptions<Pt, decimal>>(sequence++);
            builder.AddAttribute(sequence++, nameof(NTYAxisOptions<Pt, decimal>.ValueSelector), (Func<Pt, decimal>)(p => p.Z));
            builder.CloseComponent();
        }

        foreach (var fragment in series) {
            builder.AddContent(sequence++, fragment);
        }
    };

    protected IRenderedComponent<NTChart<Pt>> RenderChart(XAxisKind xAxis, params RenderFragment[] series) => RenderChart(new ChartSetup { XAxis = xAxis }, series);

    protected IRenderedComponent<NTChart<Pt>> RenderChart(ChartSetup setup, params RenderFragment[] series) {
        var cut = Render<NTChart<Pt>>(parameters => {
            parameters.Add(p => p.ChildContent, Content(setup, series));
            foreach (var (name, value) in setup.ChartAttributes) {
                parameters.TryAdd(name, value);
            }
        });
        // Theme colors are resolved asynchronously after the first render; wait (bounded) so pixel assertions are deterministic.
        cut.WaitForState(() => cut.Instance.GetThemeColor(NTColor.Primary) == Red, TimeSpan.FromSeconds(5));
        return cut;
    }

    protected static void ReRender(IRenderedComponent<NTChart<Pt>> cut, ChartSetup setup, params RenderFragment[] series) =>
        cut.Render(parameters => parameters.Add(p => p.ChildContent, Content(setup, series)));

    protected static T SeriesOf<T>(IRenderedComponent<NTChart<Pt>> cut, int index = 0) where T : NTCartesianSeries<Pt> =>
        cut.Instance.Series.Select(s => s as T).Where(s => s is not null).Select(s => s!).ElementAt(index);

    protected static NTRenderContext CreateContext(NTChart<Pt> chart, SKSurface surface) {
        var info = new SKImageInfo(SurfaceWidth, SurfaceHeight);
        return new NTRenderContext {
            Canvas = surface.Canvas,
            DefaultFont = chart.DefaultFont,
            RegularFont = chart.RegularFont,
            Density = 1f,
            Info = info,
            PlotArea = new SKRect(0, 0, info.Width, info.Height),
            TextColor = SKColors.Black,
            TotalArea = new SKRect(0, 0, info.Width, info.Height)
        };
    }

    protected static SKSurface NewSurface() {
        var surface = SKSurface.Create(new SKImageInfo(SurfaceWidth, SurfaceHeight))!;
        surface.Canvas.Clear(SKColors.White);
        return surface;
    }

    /// <summary>Renders every given series (in order) onto a fresh white surface and returns it.</summary>
    protected static SKSurface Draw(IRenderedComponent<NTChart<Pt>> cut, params NTBaseSeries<Pt>[] series) {
        var surface = NewSurface();
        var context = CreateContext(cut.Instance, surface);
        foreach (var item in series) {
            item.Render(context, Area);
        }

        return surface;
    }

    protected static SKColor PixelAt(SKSurface surface, int x, int y) {
        using var pixmap = surface.PeekPixels();
        return pixmap.GetPixelColor(x, y);
    }

    protected static SKColor PixelAt(SKSurface surface, SKPoint point) => PixelAt(surface, (int)MathF.Floor(point.X), (int)MathF.Floor(point.Y));

    protected static bool IsClose(SKColor actual, SKColor expected, int tolerance = 12) =>
        Math.Abs(actual.Red - expected.Red) <= tolerance && Math.Abs(actual.Green - expected.Green) <= tolerance && Math.Abs(actual.Blue - expected.Blue) <= tolerance;

    protected static bool IsWhite(SKColor color) => IsClose(color, SKColors.White, 2);

    /// <summary>Counts the pixels on the surface that are not white.</summary>
    protected static int CountDrawnPixels(SKSurface surface) {
        using var pixmap = surface.PeekPixels();
        var count = 0;
        for (var y = 0; y < pixmap.Height; y++) {
            for (var x = 0; x < pixmap.Width; x++) {
                if (!IsWhite(pixmap.GetPixelColor(x, y))) {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>Counts the pixels in a vertical column that match <paramref name="color" />.</summary>
    protected static int ColumnCount(SKSurface surface, int x, SKColor color) {
        using var pixmap = surface.PeekPixels();
        var count = 0;
        for (var y = 0; y < pixmap.Height; y++) {
            if (IsClose(pixmap.GetPixelColor(x, y), color)) {
                count++;
            }
        }

        return count;
    }

    /// <summary>Counts the pixels in a horizontal row that match <paramref name="color" />.</summary>
    protected static int RowCount(SKSurface surface, int y, SKColor color) {
        using var pixmap = surface.PeekPixels();
        var count = 0;
        for (var x = 0; x < pixmap.Width; x++) {
            if (IsClose(pixmap.GetPixelColor(x, y), color)) {
                count++;
            }
        }

        return count;
    }

    protected static byte[] Snapshot(SKSurface surface) {
        using var pixmap = surface.PeekPixels();
        return pixmap.GetPixelSpan().ToArray();
    }

    /// <summary>Maps a data point to the pixel the line/point series draws it at (3px inner padding on both axes).</summary>
    protected static SKPoint ExpectedPoint(NTChart<Pt> chart, double x, decimal y, bool secondary = false) {
        var (xMin, xMax) = chart.GetXRange(chart.XAxis as NTAxisOptions<Pt>, true);
        var (yMin, yMax) = chart.GetYRange((secondary ? chart.SecondaryYAxis : chart.YAxis) as NTAxisOptions<Pt>, true);
        const float p = 3f;
        var px = Area.Left + p + (float)((x - xMin) / (xMax - xMin)) * (Area.Width - (2 * p));
        var py = Area.Bottom - p - (float)((double)((y - yMin) / (yMax - yMin))) * (Area.Height - (2 * p));
        return new SKPoint(px, py);
    }

    /// <summary>Padded ranges are rounded: .NET 11 converts the double RangePadding to decimal exactly, leaving ~1e-16 noise.</summary>
    protected static (double Min, double Max) ChartXRange(NTChart<Pt> chart, bool padded = false) {
        var (min, max) = chart.GetXRange((NTAxisOptions<Pt>)chart.XAxis, padded);
        return padded ? (Math.Round(min, 9), Math.Round(max, 9)) : (min, max);
    }

    protected static (decimal Min, decimal Max) ChartYRange(NTChart<Pt> chart, bool padded = false, bool secondary = false) {
        var (min, max) = chart.GetYRange((NTAxisOptions<Pt>)(secondary ? chart.SecondaryYAxis! : chart.YAxis), padded);
        return padded ? (Math.Round(min, 10), Math.Round(max, 10)) : (min, max);
    }

    /// <summary>Runs an action on the renderer dispatcher, as required for APIs that call StateHasChanged.</summary>
    protected static void OnDispatcher(IRenderedComponent<NTChart<Pt>> cut, Action action) => cut.InvokeAsync(action).GetAwaiter().GetResult();

    /// <summary>Forces the chart's hover state (normally driven by the paint callback) through the public property setters.</summary>
    protected static void SetHover(NTChart<Pt> chart, NTBaseSeries<Pt>? series, int? pointIndex) {
        typeof(NTChart<Pt>).GetProperty(nameof(NTChart<Pt>.HoveredSeries))!.SetValue(chart, series);
        typeof(NTChart<Pt>).GetProperty(nameof(NTChart<Pt>.HoveredPointIndex))!.SetValue(chart, pointIndex);
    }

    /// <summary>Gives the chart a plot area, which pan/zoom/click handling needs and the paint callback normally supplies.</summary>
    protected static void SetPlotArea(NTChart<Pt> chart, SKRect area) =>
        typeof(NTChart<Pt>).GetProperty("LastPlotArea", NonPublic)!.SetValue(chart, area);

    protected static TooltipInfo Tooltip(NTBaseSeries<Pt> series, Pt data) => ((ISeries)series).GetTooltipInfo(data);

    protected static List<LegendEntry> Legend(NTBaseSeries<Pt> series) {
        var method = series.GetType().GetMethod("GetLegendItems", NonPublic)!;
        var entries = new List<LegendEntry>();
        foreach (var item in (System.Collections.IEnumerable)method.Invoke(series, null)!) {
            var type = item.GetType();
            entries.Add(new LegendEntry(
                (string)type.GetProperty("Label")!.GetValue(item)!,
                (SKColor)type.GetProperty("Color")!.GetValue(item)!,
                (string?)type.GetProperty("Key")!.GetValue(item),
                (int?)type.GetProperty("Index")!.GetValue(item),
                (bool)type.GetProperty("IsVisible")!.GetValue(item)!));
        }

        return entries;
    }

    /// <summary>Toggles the legend entry with the given label exactly as a legend click would.</summary>
    protected static void ToggleLegend(NTBaseSeries<Pt> series, string label) {
        var items = (System.Collections.IEnumerable)series.GetType().GetMethod("GetLegendItems", NonPublic)!.Invoke(series, null)!;
        var item = items.Cast<object>().First(i => (string)i.GetType().GetProperty("Label")!.GetValue(i)! == label);
        var toggle = series.GetType().GetMethods(NonPublic).First(m => m.Name == "ToggleLegendItem" && m.GetParameters()[0].ParameterType == item.GetType());
        toggle.Invoke(series, [item]);
    }

    /// <summary>Toggles the series itself (the legend entry of an unsegmented series).</summary>
    protected static void ToggleSeriesLegend(NTBaseSeries<Pt> series) {
        var toggle = series.GetType().GetMethods(NonPublic).First(m => m.Name == "ToggleLegendItem" && m.GetParameters()[0].ParameterType == typeof(int?));
        toggle.Invoke(series, [null]);
    }

    protected static Pt[] Linear(int count, Func<int, decimal>? y = null, int firstX = 1) =>
        Enumerable.Range(0, count).Select(i => new Pt(firstX + i, y?.Invoke(i) ?? (i + 1) * 10m)).ToArray();
}
