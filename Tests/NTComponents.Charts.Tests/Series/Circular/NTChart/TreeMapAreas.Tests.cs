using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>How the chart splits its plot area between several treemap series, labels them and routes drill input.</summary>
public class TreeMapAreas_Tests : TreeMapTestBase {
    // Bare chart: plot area (8,8)-(392,292).
    private static Dictionary<string, object> SeriesAttrs(NTColor color, string title, params (string Key, object Value)[] extra) {
        var attributes = Plain();
        attributes.Remove("ColorSelector");
        attributes["Color"] = (NTColor?)color;
        attributes["Title"] = title;
        attributes["TextColor"] = (NTColor?)NTColor.Success;
        foreach (var (key, value) in extra) {
            attributes[key] = value;
        }

        return attributes;
    }

    private IRenderedComponent<NTChart<Item>> RenderTwo(Item[] first, Item[] second, Dictionary<string, object>? firstAttributes = null, Dictionary<string, object>? secondAttributes = null) {
        // Delegates inside the attributes must be created once: a new delegate instance on re-render changes the configuration hash and resets drill state.
        var firstFragment = SeriesFragment<NTTreeMapSeries<Item>>(first, firstAttributes ?? SeriesAttrs(NTColor.PrimaryFixed, "First"));
        var secondFragment = SeriesFragment<NTTreeMapSeries<Item>>(second, secondAttributes ?? SeriesAttrs(NTColor.SecondaryFixed, "Second"));
        return RenderChart(builder => {
            firstFragment(builder);
            secondFragment(builder);
        });
    }

    private static bool IsCyan(SKColor c) => IsNear(c, PaletteColors[5], 30);

    [Fact]
    public async Task SingleSeries_FillsTheWholePlotAreaWithNoGroupLabel() {
        var cut = RenderChart<NTTreeMapSeries<Item>>(Named(("A", 1)), SeriesAttrs(NTColor.PrimaryFixed, "Only"));
        using var surface = NewSurface();

        await PaintAsync(cut, surface);

        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(8, 8, 392, 292));
        AnyPixel(surface, new SKRect(0, 0, 400, 300), IsCyan).Should().BeFalse();
    }

    [Fact]
    public async Task TwoSeries_ShareThePlotAreaProportionallyToTheirTotalsLeavingATitleStrip() {
        var cut = RenderTwo(Named(("a", 3)), Named(("b", 1)));
        using var surface = NewSurface();

        await PaintAsync(cut, surface);

        // Horizontal split 3:1 => first 8..296, second 296..392; each cell starts 20px below the area top for the title.
        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(8, 28, 296f, 292));
        AssertRect(Stats(surface, ColorOf(1)).Bounds, new SKRect(296f, 28, 392, 292));
    }

    [Fact]
    public async Task TwoSeries_AreEachTitledInTheirStripWhenTheTitleFits() {
        var cut = RenderTwo(Named(("a", 3)), Named(("b", 1)));
        using var surface = NewSurface();

        await PaintAsync(cut, surface);

        AnyPixel(surface, new SKRect(8, 8, 296f, 28), IsCyan).Should().BeTrue("first series title");
        AnyPixel(surface, new SKRect(296f, 8, 392, 28), IsCyan).Should().BeTrue("second series title");
    }

    [Fact]
    public async Task SeriesTooNarrowForItsTitle_OmitsTheTitle() {
        var cut = RenderTwo(Named(("a", 1000)), Named(("b", 1)));
        using var surface = NewSurface();

        await PaintAsync(cut, surface);

        AnyPixel(surface, new SKRect(8, 8, 380, 28), IsCyan).Should().BeTrue();
        AnyPixel(surface, new SKRect(385, 8, 392, 28), IsCyan).Should().BeFalse("the second area is ~0.4px wide");
    }

    [Fact]
    public async Task SeriesWithZeroTotals_SplitTheAreaEvenly() {
        var cut = RenderTwo(Named(("a", 0)), Named(("b", 0)));
        using var surface = NewSurface();

        await PaintAsync(cut, surface);

        AnyPixel(surface, new SKRect(8, 8, 200, 28), IsCyan).Should().BeTrue();
        AnyPixel(surface, new SKRect(200, 8, 392, 28), IsCyan).Should().BeTrue();
    }

    [Fact]
    public async Task HiddenSeries_GivesItsSpaceToTheRemainingSeries() {
        var cut = RenderTwo(Named(("a", 3)), Named(("b", 1)), secondAttributes: SeriesAttrs(NTColor.SecondaryFixed, "Second", ("Visible", false)));
        using var surface = NewSurface();

        await PaintAsync(cut, surface);

        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(8, 8, 392, 292));
        Stats(surface, ColorOf(1)).Count.Should().Be(0);
    }

    [Fact]
    public async Task HoverOverEachSeriesArea_HitsThatSeriesOnly() {
        var firstData = Named(("a", 3));
        var secondData = Named(("b", 1));
        var cut = RenderTwo(firstData, secondData);
        using var surface = NewSurface();
        await PaintAsync(cut, surface);

        await HoverAsync(cut, surface, 100, 150);
        var overFirst = cut.Instance.HoveredSeries;
        var firstPoint = cut.Instance.HoveredDataPoint;
        await HoverAsync(cut, surface, 350, 150);

        overFirst.Should().BeSameAs(cut.Instance.Series[0]);
        firstPoint.Should().BeSameAs(firstData[0]);
        cut.Instance.HoveredSeries.Should().BeSameAs(cut.Instance.Series[1]);
        cut.Instance.HoveredDataPoint.Should().BeSameAs(secondData[0]);
    }

    [Fact]
    public async Task HoverOverTheTitleStrip_HitsNothing() {
        var cut = RenderTwo(Named(("a", 3)), Named(("b", 1)));
        using var surface = NewSurface();
        await PaintAsync(cut, surface);

        await HoverAsync(cut, surface, 100, 12);

        cut.Instance.HoveredPointIndex.Should().BeNull();
    }

    private IRenderedComponent<NTChart<Item>> RenderGroupedPair() {
        var groupSelector = (Func<Item, string?>)(item => item.Group);
        var first = Grouped(("a", 3, "G1"), ("b", 1, "G1"), ("c", 2, "G2"));
        var second = Grouped(("x", 1, "H1"));
        var firstFragment = SeriesFragment<NTTreeMapSeries<Item>>(first, SeriesAttrs(NTColor.PrimaryFixed, "First", ("GroupSelector", groupSelector), ("ShowChildPreviews", false)), SeriesFragment<NTTreeMapSeries<Item>>(first, Plain()));
        var secondFragment = SeriesFragment<NTTreeMapSeries<Item>>(second, SeriesAttrs(NTColor.SecondaryFixed, "Second", ("GroupSelector", groupSelector), ("ShowChildPreviews", false)), SeriesFragment<NTTreeMapSeries<Item>>(second, Plain()));
        return RenderChart(builder => {
            firstFragment(builder);
            secondFragment(builder);
        });
    }

    [Fact]
    public async Task DrillingOneSeries_GivesItTheWholePlotAreaAndHidesTheOthers() {
        var cut = RenderGroupedPair();
        using var surface = NewSurface();
        await PaintAsync(cut, surface);

        await ChartMouseDownAsync(cut, 100, 150);
        surface.Canvas.Clear(SKColors.White);
        await PaintAsync(cut, surface);

        Stats(surface, ColorOf(1)).Count.Should().Be(0, "the other series is not painted while one is drilled");
        Stats(surface, ColorOf(0)).Count.Should().Be(0, "group cells are replaced by the leaves of the drilled group");
        Stats(surface, ColorOf(5)).Bounds.Width.Should().BeGreaterThan(377f);
        Stats(surface, ColorOf(5)).Bounds.Height.Should().BeGreaterThan(280f);
    }

    [Fact]
    public async Task MouseDown_IsRoutedOnlyToTheSeriesUnderThePointer() {
        var cut = RenderGroupedPair();
        using var surface = NewSurface();
        await PaintAsync(cut, surface);

        await ChartMouseDownAsync(cut, 380, 150);
        surface.Canvas.Clear(SKColors.White);
        await PaintAsync(cut, surface);

        Stats(surface, ColorOf(0)).Count.Should().Be(0, "the first series is hidden while the second is drilled");
        Stats(surface, ColorOf(5)).Bounds.Width.Should().BeGreaterThan(377f);
    }

    [Fact]
    public async Task MouseDownOnTheTitleStrip_DoesNotDrill() {
        var cut = RenderGroupedPair();
        using var surface = NewSurface();
        await PaintAsync(cut, surface);
        var before = Snapshot(surface);

        await ChartMouseDownAsync(cut, 100, 12);
        surface.Canvas.Clear(SKColors.White);
        await PaintAsync(cut, surface);

        Snapshot(surface).Should().Equal(before);
    }

    [Fact]
    public async Task MixingTreeMapAndPieSeries_IsRejected() {
        var act = () => Task.FromResult(RenderChart(builder => {
            SeriesFragment<NTTreeMapSeries<Item>>(Named(("a", 1)))(builder);
            SeriesFragment<NTPieSeries<Item>>(Named(("a", 1)))(builder);
        }));

        await act.Should().ThrowAsync<Exception>().WithMessage("*same coordinate system*");
    }
}
