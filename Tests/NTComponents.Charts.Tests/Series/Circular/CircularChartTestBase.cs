using System.Reflection;
using Bunit.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Shared bUnit setup, raster helpers and reflection helpers for circular and hierarchical series tests.</summary>
public abstract class CircularChartTestBase : BunitContext {
    protected const int CanvasWidth = 400;
    protected const int CanvasHeight = 300;

    /// <summary>Palette colors in chart palette order (Primary, Tertiary, Secondary, Assert, Warning, Success).</summary>
    protected static readonly SKColor[] PaletteColors = [
        new(255, 0, 0), new(0, 255, 0), new(0, 0, 255), new(255, 255, 0), new(255, 0, 255), new(0, 255, 255)
    ];

    protected static readonly NTColor[] PaletteNtColors = [
        NTColor.Primary, NTColor.Tertiary, NTColor.Secondary, NTColor.Assert, NTColor.Warning, NTColor.Success
    ];

    protected CircularChartTestBase() {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var module = JSInterop.SetupModule("./_content/NTComponents.Charts/ntcomponents-charts.js");
        module.Setup<float>("getDevicePixelRatio").SetResult(1f);
        module.Setup<Dictionary<string, string?>>("getThemeColors", _ => true).SetResult(new Dictionary<string, string?> {
            ["PrimaryFixed"] = "#ff0000",
            ["SecondaryFixed"] = "#00ff00",
            ["TertiaryFixed"] = "#0000ff",
            ["Primary"] = "#ff0000",
            ["Tertiary"] = "#00ff00",
            ["Secondary"] = "#0000ff",
            ["Assert"] = "#ffff00",
            ["Warning"] = "#ff00ff",
            ["Success"] = "#00ffff",
            ["Surface"] = "#ffffff",
            ["OutlineVariant"] = "#808080",
            ["OnSurface"] = "#000000"
        });
        ComponentFactories.AddStub(type => type.FullName is "SkiaSharp.Views.Blazor.SKGLView" or "SkiaSharp.Views.Blazor.SKCanvasView");
    }

    public sealed record Item(string Name, decimal Value, string? Group = null);

    protected static Item[] Items(params decimal[] values) => values.Select((v, i) => new Item($"Item{i}", v)).ToArray();

    /// <summary>Builds a series fragment with deterministic defaults (no animation, no labels).</summary>
    protected static RenderFragment SeriesFragment<TSeries>(IEnumerable<Item> data, Dictionary<string, object>? attributes = null, RenderFragment? children = null) where TSeries : NTBaseSeries<Item> => builder => {
        var merged = new Dictionary<string, object> {
            ["Data"] = data,
            ["XValue"] = (Func<Item, object>)(item => item.Name),
            ["ValueSelector"] = (Func<Item, decimal>)(item => item.Value),
            ["AnimationEnabled"] = false
        };
        foreach (var attribute in attributes ?? []) {
            merged[attribute.Key] = attribute.Value;
        }
        if (children is not null) {
            merged["ChildContent"] = children;
        }

        var sequence = 0;
        builder.OpenComponent<TSeries>(sequence++);
        foreach (var attribute in merged) {
            builder.AddAttribute(sequence++, attribute.Key, attribute.Value);
        }
        builder.CloseComponent();
    };

    protected IRenderedComponent<NTChart<Item>> RenderChart(RenderFragment content) => Render<NTChart<Item>>(parameters => parameters.Add(p => p.ChildContent, content));

    protected IRenderedComponent<NTChart<Item>> RenderChart<TSeries>(IEnumerable<Item> data, Dictionary<string, object>? attributes = null) where TSeries : NTBaseSeries<Item> => RenderChart(SeriesFragment<TSeries>(data, attributes));

    protected static SKSurface NewSurface(int width = CanvasWidth, int height = CanvasHeight) {
        var surface = SKSurface.Create(new SKImageInfo(width, height))!;
        surface.Canvas.Clear(SKColors.White);
        return surface;
    }

    protected static NTRenderContext CreateContext(NTChart<Item> chart, SKSurface surface, float density = 1f) {
        var info = new SKImageInfo(surface.Canvas.DeviceClipBounds.Width, surface.Canvas.DeviceClipBounds.Height);
        return new NTRenderContext {
            Canvas = surface.Canvas,
            DefaultFont = chart.DefaultFont,
            RegularFont = chart.RegularFont,
            Density = density,
            Info = info,
            PlotArea = new SKRect(0, 0, info.Width, info.Height),
            TextColor = SKColors.Black,
            TotalArea = new SKRect(0, 0, info.Width, info.Height)
        };
    }

    /// <summary>Paints the whole chart through its real paint pipeline (area allocation, series, group labels).</summary>
    protected static async Task PaintAsync(IRenderedComponent<NTChart<Item>> chart, SKSurface surface) {
        var info = new SKImageInfo(surface.Canvas.DeviceClipBounds.Width, surface.Canvas.DeviceClipBounds.Height);
        await chart.InvokeAsync(() => typeof(NTChart<Item>)
            .GetMethod("OnPaintSurface", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(SKCanvas), typeof(SKImageInfo)])!
            .Invoke(chart.Instance, [surface.Canvas, info]));
    }

    protected static async Task HoverAsync(IRenderedComponent<NTChart<Item>> chart, SKSurface surface, double x, double y) {
        await chart.Instance.OnNativeMouseMove(new MouseEventArgs { OffsetX = x, OffsetY = y });
        await PaintAsync(chart, surface);
    }

    protected static Task ChartMouseDownAsync(IRenderedComponent<NTChart<Item>> chart, double x, double y) => chart.InvokeAsync(() => typeof(NTChart<Item>)
        .GetMethod("OnMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(chart.Instance, [new MouseEventArgs { OffsetX = x, OffsetY = y }]));

    protected static Task ChartClickAsync(IRenderedComponent<NTChart<Item>> chart, double x, double y) => chart.InvokeAsync(() => typeof(NTChart<Item>)
        .GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(chart.Instance, [new MouseEventArgs { OffsetX = x, OffsetY = y }]));

    protected static SKColor PixelAt(SKSurface surface, float x, float y) {
        using var pixmap = surface.PeekPixels();
        return pixmap.GetPixelColor((int)MathF.Round(x), (int)MathF.Round(y));
    }

    protected static bool IsNear(SKColor actual, SKColor expected, int tolerance = 4) =>
        Math.Abs(actual.Red - expected.Red) <= tolerance &&
        Math.Abs(actual.Green - expected.Green) <= tolerance &&
        Math.Abs(actual.Blue - expected.Blue) <= tolerance;

    /// <summary>True when <paramref name="actual" /> is <paramref name="fill" /> under the Material 3 hover state layer (an on-color overlay of at most 8%, plus a little anti-aliasing slack).</summary>
    protected static bool IsStateLayered(SKColor actual, SKColor fill) => IsNear(actual, fill, (int)MathF.Ceiling(255 * NTChartTokens.HoverOpacity) + 2);

    protected static bool IsBackground(SKColor color) => IsNear(color, SKColors.White);

    /// <summary>Pixel count, bounding box and centroid of pixels that exactly match <paramref name="color" />.</summary>
    protected static ColorStats Stats(SKSurface surface, SKColor color, int tolerance = 2) {
        using var pixmap = surface.PeekPixels();
        long count = 0;
        double sumX = 0, sumY = 0;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (var y = 0; y < pixmap.Height; y++) {
            for (var x = 0; x < pixmap.Width; x++) {
                if (!IsNear(pixmap.GetPixelColor(x, y), color, tolerance)) {
                    continue;
                }
                count++;
                sumX += x + 0.5;
                sumY += y + 0.5;
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return count == 0
            ? new ColorStats(0, SKRect.Empty, SKPoint.Empty)
            : new ColorStats(count, new SKRect(minX, minY, maxX + 1, maxY + 1), new SKPoint((float)(sumX / count), (float)(sumY / count)));
    }

    protected static bool AnyPixel(SKSurface surface, SKRect region, Func<SKColor, bool> predicate) {
        using var pixmap = surface.PeekPixels();
        for (var y = Math.Max(0, (int)region.Top); y < Math.Min(pixmap.Height, (int)MathF.Ceiling(region.Bottom)); y++) {
            for (var x = Math.Max(0, (int)region.Left); x < Math.Min(pixmap.Width, (int)MathF.Ceiling(region.Right)); x++) {
                if (predicate(pixmap.GetPixelColor(x, y))) {
                    return true;
                }
            }
        }

        return false;
    }

    protected static long CountNonBackground(SKSurface surface) {
        using var pixmap = surface.PeekPixels();
        long count = 0;
        for (var y = 0; y < pixmap.Height; y++) {
            for (var x = 0; x < pixmap.Width; x++) {
                if (!IsBackground(pixmap.GetPixelColor(x, y))) {
                    count++;
                }
            }
        }

        return count;
    }

    protected static byte[] Snapshot(SKSurface surface) {
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        return bitmap.Bytes;
    }

    protected static TooltipInfo Tooltip(NTBaseSeries<Item> series, Item item) => ((ISeries)series).GetTooltipInfo(item);

    protected static IReadOnlyList<LegendEntry> LegendItems(object series) {
        var method = series.GetType().GetMethod("GetLegendItems", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var entries = new List<LegendEntry>();
        foreach (var legendItem in (System.Collections.IEnumerable)method.Invoke(series, null)!) {
            var type = legendItem.GetType();
            entries.Add(new LegendEntry(
                (string)type.GetProperty("Label")!.GetValue(legendItem)!,
                (SKColor)type.GetProperty("Color")!.GetValue(legendItem)!,
                (int?)type.GetProperty("Index")!.GetValue(legendItem),
                (string?)type.GetProperty("Key")!.GetValue(legendItem),
                (bool)type.GetProperty("IsVisible")!.GetValue(legendItem)!));
        }

        return entries;
    }

    protected static void ToggleLegend(object series, int? index) => series.GetType()
        .GetMethod("ToggleLegendItem", BindingFlags.Instance | BindingFlags.NonPublic, [typeof(int?)])!
        .Invoke(series, [index]);

    protected static bool RequiresAnimationFrame(object series) => (bool)series.GetType()
        .GetProperty("RequiresAnimationFrame", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(series)!;

    protected static T Field<T>(object instance, string name) {
        for (var type = instance.GetType(); type is not null; type = type.BaseType) {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is not null) {
                return (T)field.GetValue(instance)!;
            }
        }

        throw new InvalidOperationException($"Field {name} not found on {instance.GetType().Name}.");
    }

    protected static SKPoint Polar(SKPoint center, float radius, float degrees) {
        var radians = degrees * MathF.PI / 180f;
        return new SKPoint(center.X + (MathF.Cos(radians) * radius), center.Y + (MathF.Sin(radians) * radius));
    }

    protected static Task ChartMouseOutAsync(IRenderedComponent<NTChart<Item>> chart) => chart.InvokeAsync(() => typeof(NTChart<Item>)
        .GetMethod("OnMouseOut", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(chart.Instance, [new MouseEventArgs()]));

    /// <summary>Finds every native Skia resource held in instance fields (name-agnostic, so refactors that rename fields keep working).</summary>
    protected static IReadOnlyList<SKObject> NativeResources(object instance) {
        var found = new List<SKObject>();
        for (var type = instance.GetType(); type is not null; type = type.BaseType) {
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) {
                if (typeof(SKObject).IsAssignableFrom(field.FieldType) && field.GetValue(instance) is SKObject resource) {
                    found.Add(resource);
                }
            }
        }

        return found;
    }

    protected readonly record struct ColorStats(long Count, SKRect Bounds, SKPoint Centroid) {
        public float EquivalentRadius => MathF.Sqrt(Count / MathF.PI);
    }

    protected readonly record struct LegendEntry(string Label, SKColor Color, int? Index, string? Key, bool IsVisible);

    /// <summary>Pie series whose animation clock can be positioned without wall-clock waits.</summary>
    public sealed class ClockedPie : NTPieSeries<Item> {
        public void SetAnimationAge(TimeSpan age) => AnimationStartTime = DateTime.Now - age;
    }

    /// <summary>Radar series whose animation clock can be positioned without wall-clock waits.</summary>
    public sealed class ClockedRadar : NTRadarSeries<Item> {
        public void SetAnimationAge(TimeSpan age) => AnimationStartTime = DateTime.Now - age;
    }

    /// <summary>Tree map series whose animation clock can be positioned without wall-clock waits.</summary>
    public sealed class ClockedTreeMap : NTTreeMapSeries<Item> {
        public void SetAnimationAge(TimeSpan age) => AnimationStartTime = DateTime.Now - age;
    }

    /// <summary>Bubble pack series whose animation clock can be positioned without wall-clock waits.</summary>
    public sealed class ClockedBubblePack : NTBubblePackSeries<Item> {
        public void SetAnimationAge(TimeSpan age) => AnimationStartTime = DateTime.Now - age;
    }
}
