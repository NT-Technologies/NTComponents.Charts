using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Shared fixtures for treemap tests: plain unlabeled cells, one distinct color per node name.</summary>
public abstract class TreeMapTestBase : CircularChartTestBase {
    protected static readonly SKRect Area = new(0, 0, 400, 300);

    /// <summary>Maps node display keys to palette colors by position in <paramref name="names" />.</summary>
    protected static Func<TreeMapColorContext<Item>, NTColor> ByName(params string[] names) => context => {
        var index = Array.IndexOf(names, context.Key);
        return index < 0 ? NTColor.Success : PaletteNtColors[index];
    };

    protected static Dictionary<string, object> Plain(params string[] colorNames) => new() {
        ["ShowLabels"] = false,
        ["ShowNavigation"] = false,
        ["ShowDrillIndicator"] = false,
        ["ItemPadding"] = 0f,
        ["ColorSelector"] = ByName(colorNames)
    };

    protected static Dictionary<string, object> With(Dictionary<string, object> source, string key, object value) {
        var copy = new Dictionary<string, object>(source) { [key] = value };
        return copy;
    }

    protected static Item[] Named(params (string Name, decimal Value)[] items) => items.Select(i => new Item(i.Name, i.Value)).ToArray();

    protected static Item[] Grouped(params (string Name, decimal Value, string Group)[] items) => items.Select(i => new Item(i.Name, i.Value, i.Group)).ToArray();

    protected static SKColor ColorOf(int index) => PaletteColors[index];

    protected static void AssertRect(SKRect actual, SKRect expected, float tolerance = 2f) {
        actual.Left.Should().BeApproximately(expected.Left, tolerance, "left");
        actual.Top.Should().BeApproximately(expected.Top, tolerance, "top");
        actual.Right.Should().BeApproximately(expected.Right, tolerance, "right");
        actual.Bottom.Should().BeApproximately(expected.Bottom, tolerance, "bottom");
    }

    protected static MouseEventArgs Mouse(double x, double y) => new() { OffsetX = x, OffsetY = y };

    protected static SKPoint Mid(SKRect rect) => new(rect.MidX, rect.MidY);
}
