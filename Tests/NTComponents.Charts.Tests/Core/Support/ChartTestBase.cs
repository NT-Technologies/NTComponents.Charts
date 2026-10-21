using System.Reflection;
using Bunit.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Axes;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

/// <summary>Data item used by the core chart tests.</summary>
public sealed record Pt(string Name, double X, decimal Y, DateTime Date = default);

/// <summary>
///     Shared bUnit set-up for the core chart tests: stubs JS interop and the Skia views, renders charts from
///     fragments and paints full frames onto raster surfaces so tests can assert on observable output.
/// </summary>
public abstract class ChartTestBase : BunitContext {
    protected const int Width = 640;
    protected const int Height = 320;

    /// <summary>Theme colors returned by the JS stub. Tests may override entries before rendering.</summary>
    protected readonly Dictionary<string, string?> ThemeColors = new() {
        ["Surface"] = "#ffffff",
        ["OnSurface"] = "#000000",
        ["Primary"] = "#ff0000",
        ["Tertiary"] = "#00aa00",
        ["Secondary"] = "#0000ff",
        ["Assert"] = "#aa00aa",
        ["Warning"] = "#aaaa00",
        ["Success"] = "#00aaaa",
        ["PrimaryFixed"] = "#ff0000",
        ["SecondaryFixed"] = "#00aa00",
        ["TertiaryFixed"] = "#0000ff",
        ["SurfaceVariant"] = "#ff00ff",
        ["SurfaceContainer"] = "#ff00ff",
        ["OnSurfaceVariant"] = "#111111",
        ["OutlineVariant"] = "#dddddd",
        ["Outline"] = "#000000"
    };

    protected BunitJSModuleInterop Module { get; }

    /// <summary>Names of the Skia view types the chart instantiated (they are stubbed in tests).</summary>
    protected List<string> CreatedViews { get; } = [];

    protected ChartTestBase() {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Module = JSInterop.SetupModule("./_content/NTComponents.Charts/ntcomponents-charts.js");
        Module.Setup<float>("getDevicePixelRatio").SetResult(1f);
        Module.Setup<Dictionary<string, string?>>("getThemeColors", _ => true).SetResult(ThemeColors);
        ComponentFactories.AddStub(type => {
            var isView = type.FullName is "SkiaSharp.Views.Blazor.SKGLView" or "SkiaSharp.Views.Blazor.SKCanvasView";
            if (isView) {
                CreatedViews.Add(type.Name);
            }
            return isView;
        });
    }

    protected static readonly Pt[] Sample = [new("A", 0, 10m), new("B", 1, 40m), new("C", 2, 20m), new("D", 3, 30m)];

    // ---------- fragment helpers ----------

    protected static RenderFragment Combine(params RenderFragment[] fragments) => builder => {
        foreach (var fragment in fragments) {
            fragment(builder);
        }
    };

    protected static RenderFragment Series<TSeries>(IEnumerable<Pt> data, Func<Pt, object>? x = null, Func<Pt, decimal>? y = null, IDictionary<string, object>? extra = null) where TSeries : NTBaseSeries<Pt> => builder => {
        builder.OpenComponent<TSeries>(0);
        builder.AddAttribute(1, "Data", data);
        builder.AddAttribute(2, "XValue", x ?? (p => p.X));
        builder.AddAttribute(3, "YValueSelector", y ?? (p => p.Y));
        builder.AddAttribute(4, "AnimationEnabled", false);
        AddExtra(builder, extra);
        builder.CloseComponent();
    };

    protected static RenderFragment Line(IEnumerable<Pt>? data = null, Func<Pt, object>? x = null, Func<Pt, decimal>? y = null, IDictionary<string, object>? extra = null) =>
        Series<NTLineSeries<Pt>>(data ?? Sample, x, y, extra);

    protected static RenderFragment Bar(IEnumerable<Pt>? data = null, Func<Pt, object>? x = null, Func<Pt, decimal>? y = null, IDictionary<string, object>? extra = null) =>
        Series<NTBarSeries<Pt>>(data ?? Sample, x, y, extra);

    protected static RenderFragment XAxis<TX>(Func<Pt, TX> selector, IDictionary<string, object>? extra = null) => builder => {
        builder.OpenComponent<NTXAxisOptions<Pt, TX>>(0);
        builder.AddAttribute(1, "ValueSelector", selector);
        AddExtra(builder, extra);
        builder.CloseComponent();
    };

    protected static RenderFragment YAxis(IDictionary<string, object>? extra = null) => builder => {
        builder.OpenComponent<NTYAxisOptions<Pt, decimal>>(0);
        builder.AddAttribute(1, "ValueSelector", (Func<Pt, decimal>)(p => p.Y));
        AddExtra(builder, extra);
        builder.CloseComponent();
    };

    protected static RenderFragment Legend(IDictionary<string, object>? extra = null) => builder => {
        builder.OpenComponent<NTLegend<Pt>>(0);
        AddExtra(builder, extra);
        builder.CloseComponent();
    };

    protected static RenderFragment Tooltip(IDictionary<string, object>? extra = null) => builder => {
        builder.OpenComponent<NTTooltip<Pt>>(0);
        AddExtra(builder, extra);
        builder.CloseComponent();
    };

    protected static RenderFragment Recording(string label, List<string>? log = null, Func<SKPoint, SKRect, (int Index, Pt? Data)?>? hit = null, IEnumerable<Pt>? data = null, IDictionary<string, object>? extra = null) => builder => {
        builder.OpenComponent<RecordingSeries>(0);
        builder.AddAttribute(1, "Data", data ?? Sample);
        builder.AddAttribute(2, "XValue", (Func<Pt, object>)(p => p.X));
        builder.AddAttribute(3, "YValueSelector", (Func<Pt, decimal>)(p => p.Y));
        builder.AddAttribute(4, "AnimationEnabled", false);
        builder.AddAttribute(5, "Label", label);
        builder.AddAttribute(6, "Log", log);
        builder.AddAttribute(7, "Hit", hit);
        AddExtra(builder, extra);
        builder.CloseComponent();
    };

    protected static Dictionary<string, object> Args(params (string Name, object Value)[] values) => values.ToDictionary(v => v.Name, v => v.Value);

    private static void AddExtra(RenderTreeBuilder builder, IDictionary<string, object>? extra) {
        if (extra is null) {
            return;
        }
        var sequence = 10;
        foreach (var pair in extra) {
            builder.AddAttribute(sequence++, pair.Key, pair.Value);
        }
    }

    protected IRenderedComponent<NTChart<Pt>> RenderChart(RenderFragment children, Action<ComponentParameterCollectionBuilder<NTChart<Pt>>>? configure = null) {
        var chart = Render<NTChart<Pt>>(parameters => {
            parameters.Add(p => p.AllowExport, false);
            parameters.Add(p => p.ChildContent, children);
            configure?.Invoke(parameters);
        });
        chart.WaitForState(() => chart.Instance.GetThemeColor(NTColor.Surface) != SKColors.Black);
        // The chart only learns which interactions are enabled after its series register, which re-renders it asynchronously;
        // render once more here so the toolbar and cursor are in their final state before any test inspects them.
        chart.Render(_ => { });
        return chart;
    }

    /// <summary>
    ///     Finds the element and dispatches the event in one dispatcher turn, so a re-render (e.g. the throttled UI refresh) can never
    ///     invalidate the event handler between the lookup and the dispatch.
    /// </summary>
    protected static Task Fire(IRenderedComponent<NTChart<Pt>> chart, Action<AngleSharp.Dom.IElement> act, string selector = ".nt-chart-canvas-host") => chart.InvokeAsync(() => act(chart.Find(selector)));

    protected static AngleSharp.Dom.IElement Host(IRenderedComponent<NTChart<Pt>> chart) => chart.Find(".nt-chart-canvas-host");

    // ---------- painting / geometry ----------

    private static readonly MethodInfo PaintMethod = typeof(NTChart<Pt>).GetMethod("OnPaintSurface", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(SKCanvas), typeof(SKImageInfo)])!;
    private static readonly PropertyInfo LastPlotAreaProperty = typeof(NTChart<Pt>).GetProperty("LastPlotArea", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>Paints one full frame onto a raster surface (a fresh one unless supplied) and returns it. The caller disposes it.</summary>
    protected static async Task<SKSurface> Paint(IRenderedComponent<NTChart<Pt>> chart, int width = Width, int height = Height, SKSurface? surface = null) {
        surface ??= SKSurface.Create(new SKImageInfo(width, height))!;
        var info = new SKImageInfo(width, height);
        await chart.InvokeAsync(() => PaintMethod.Invoke(chart.Instance, [surface.Canvas, info]));
        return surface;
    }

    protected static SKRect PlotArea(IRenderedComponent<NTChart<Pt>> chart) => (SKRect)LastPlotAreaProperty.GetValue(chart.Instance)!;

    protected static SKBitmap Bitmap(SKSurface surface) {
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    protected static bool IsColor(SKColor actual, SKColor expected, int tolerance = 8) =>
        Math.Abs(actual.Red - expected.Red) <= tolerance && Math.Abs(actual.Green - expected.Green) <= tolerance && Math.Abs(actual.Blue - expected.Blue) <= tolerance;

    /// <summary>Bounding box of all pixels matching <paramref name="color"/>, or null when none match.</summary>
    protected static SKRectI? BoundsOf(SKBitmap bitmap, SKColor color, int tolerance = 8, SKRectI? region = null) {
        var area = region ?? new SKRectI(0, 0, bitmap.Width, bitmap.Height);
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
        for (var y = Math.Max(0, area.Top); y < Math.Min(bitmap.Height, area.Bottom); y++) {
            for (var x = Math.Max(0, area.Left); x < Math.Min(bitmap.Width, area.Right); x++) {
                if (IsColor(bitmap.GetPixel(x, y), color, tolerance)) {
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }
        }
        return maxX < 0 ? null : new SKRectI(minX, minY, maxX, maxY);
    }

    /// <summary>
    ///     Highest coverage (0..1) of <paramref name="ink"/> painted over a white background within <paramref name="radius"/> pixels of the point.
    ///     Needed because 1px strokes centered on a pixel boundary are anti-aliased across two rows.
    /// </summary>
    protected static float InkAt(SKBitmap bitmap, int x, int y, SKColor ink, int radius = 1) {
        var best = 0f;
        for (var py = Math.Max(0, y - radius); py <= Math.Min(bitmap.Height - 1, y + radius); py++) {
            for (var px = Math.Max(0, x - radius); px <= Math.Min(bitmap.Width - 1, x + radius); px++) {
                var pixel = bitmap.GetPixel(px, py);
                int[] actual = [pixel.Red, pixel.Green, pixel.Blue];
                int[] target = [ink.Red, ink.Green, ink.Blue];
                var channel = 0;
                for (var i = 1; i < 3; i++) {
                    if (255 - target[i] > 255 - target[channel]) {
                        channel = i;
                    }
                }
                var alpha = (255f - actual[channel]) / Math.Max(1, 255 - target[channel]);
                var consistent = true;
                for (var i = 0; i < 3; i++) {
                    var expected = 255f - (alpha * (255 - target[i]));
                    consistent &= Math.Abs(actual[i] - expected) <= 14;
                }
                if (consistent) {
                    best = Math.Max(best, Math.Clamp(alpha, 0f, 1f));
                }
            }
        }
        return best;
    }

    protected static int CountColor(SKBitmap bitmap, SKColor color, int tolerance = 8) {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++) {
            for (var x = 0; x < bitmap.Width; x++) {
                if (IsColor(bitmap.GetPixel(x, y), color, tolerance)) {
                    count++;
                }
            }
        }
        return count;
    }

    // ---------- interaction ----------

    protected static Task Move(IRenderedComponent<NTChart<Pt>> chart, double x, double y) =>
        chart.InvokeAsync(() => chart.Instance.OnNativeMouseMove(new MouseEventArgs { OffsetX = x, OffsetY = y }));

    protected static T Get<T>(object instance, string name, Type? declaring = null) =>
        (T)(declaring ?? instance.GetType()).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(instance)!;
}
