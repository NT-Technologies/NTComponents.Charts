using System.Diagnostics;
using System.Reflection;
using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Shared fixtures for bubble pack tests: unlabeled bubbles with one palette color per node name and deterministic physics stepping.</summary>
public abstract class BubblePackTestBase : CircularChartTestBase {
    // A 400x300 field in the middle of an 800x600 surface so bubbles that drift outside the field are never clipped by the raster edge.
    protected static readonly SKRect Area = new(200, 150, 600, 450);
    protected static readonly SKPoint Center = new(400, 300);

    protected static SKSurface NewField() => NewSurface(800, 600);

    protected static Func<BubbleColorContext<Item>, NTColor> ByName(params string[] names) => context => {
        var index = Array.IndexOf(names, context.Key);
        return index < 0 ? NTColor.Success : PaletteNtColors[index];
    };

    protected static Dictionary<string, object> Plain(params string[] colorNames) => new() {
        ["ShowLabels"] = false,
        ["ShowNavigation"] = false,
        ["ShowDrillIndicator"] = false,
        ["ColorSelector"] = ByName(colorNames)
    };

    protected static Dictionary<string, object> With(Dictionary<string, object> source, string key, object value) => new(source) { [key] = value };

    protected static Item[] Named(params (string Name, decimal Value)[] items) => items.Select(i => new Item(i.Name, i.Value)).ToArray();

    protected static Item[] Grouped(params (string Name, decimal Value, string Group)[] items) => items.Select(i => new Item(i.Name, i.Value, i.Group)).ToArray();

    protected static MouseEventArgs Mouse(double x, double y) => new() { OffsetX = x, OffsetY = y };

    protected static SKColor ColorOf(int index) => PaletteColors[index];

    /// <summary>Renders one frame at a fixed 60Hz time step so physics is independent of wall-clock timing.</summary>
    protected static void Frame(NTBubblePackSeries<Item> series, NTChart<Item> chart, SKSurface surface, SKRect? area = null, float density = 1f) {
        var field = FindField(series.GetType(), "_lastPhysicsStepTimestamp");
        if (field is not null && (long)field.GetValue(series)! != 0) {
            field.SetValue(series, Stopwatch.GetTimestamp() - (Stopwatch.Frequency / 60));
        }

        surface.Canvas.Clear(SKColors.White);
        series.Render(CreateContext(chart, surface, density), area ?? Area);
    }

    /// <summary>Steps frames until the series stops requesting animation frames; returns the frame count.</summary>
    protected static int Settle(NTBubblePackSeries<Item> series, NTChart<Item> chart, SKSurface surface, SKRect? area = null, int maxFrames = 1500) {
        var frames = 0;
        do {
            Frame(series, chart, surface, area);
            frames++;
        } while (RequiresAnimationFrame(series) && frames < maxFrames);

        return frames;
    }

    protected static (SKPoint Center, float Radius)[] Bubbles(SKSurface surface, int count) => Enumerable.Range(0, count)
        .Select(i => Stats(surface, ColorOf(i)))
        .Select(s => (s.Centroid, s.EquivalentRadius))
        .ToArray();

    protected static float Gap((SKPoint Center, float Radius) a, (SKPoint Center, float Radius) b) =>
        MathF.Sqrt(MathF.Pow(a.Center.X - b.Center.X, 2) + MathF.Pow(a.Center.Y - b.Center.Y, 2)) - a.Radius - b.Radius;

    private static FieldInfo? FindField(Type type, string name) {
        for (var current = type; current is not null; current = current.BaseType) {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is not null) {
                return field;
            }
        }

        return null;
    }
}
