using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Cell rectangles produced by the treemap partitioning, observed through pixels and hit-testing.</summary>
public class Layout_Tests : TreeMapTestBase {
    private (NTTreeMapSeries<Item> Series, NTChart<Item> Chart, SKSurface Surface) Render(Item[] data, Dictionary<string, object>? attributes = null, SKRect? area = null) {
        var cut = RenderChart<NTTreeMapSeries<Item>>(data, attributes ?? Plain("A", "B", "C", "D", "E", "F"));
        var series = cut.FindComponent<NTTreeMapSeries<Item>>().Instance;
        var surface = NewSurface();
        series.Render(CreateContext(cut.Instance, surface), area ?? Area);
        return (series, cut.Instance, surface);
    }

    private static readonly Item[] Weighted = Named(("A", 6), ("B", 3), ("C", 2), ("D", 1));

    [Fact]
    public void Cells_AreSplitRecursivelyAlongTheLongerSideInDescendingValueOrder() {
        var (_, _, surface) = Render(Weighted);
        using var _ = surface;

        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(0, 0, 200, 300));
        AssertRect(Stats(surface, ColorOf(1)).Bounds, new SKRect(200, 0, 400, 150));
        AssertRect(Stats(surface, ColorOf(2)).Bounds, new SKRect(200, 150, 333.33f, 300));
        AssertRect(Stats(surface, ColorOf(3)).Bounds, new SKRect(333.33f, 150, 400, 300));
    }

    [Fact]
    public void InputOrder_DoesNotChangeTheLayout() {
        var (_, _, ordered) = Render(Weighted);
        using var _ = ordered;
        var (_, _, shuffled) = Render(Named(("C", 2), ("D", 1), ("A", 6), ("B", 3)));
        using var __ = shuffled;

        foreach (var color in Enumerable.Range(0, 4)) {
            AssertRect(Stats(shuffled, ColorOf(color)).Bounds, Stats(ordered, ColorOf(color)).Bounds, 0.5f);
        }
    }

    [Fact]
    public void CellsTileTheWholeAreaAndAreProportionalToValue() {
        var (_, _, surface) = Render(Named(("A", 5), ("B", 3), ("C", 2), ("D", 1), ("E", 1)));
        using var _ = surface;

        var counts = Enumerable.Range(0, 5).Select(i => (double)Stats(surface, ColorOf(i)).Count).ToArray();
        var total = counts.Sum();
        total.Should().BeApproximately(400 * 300, 400 * 300 * 0.05, "cells tile the area (minus 1px borders)");
        new[] { 5d, 3d, 2d, 1d, 1d }.Select((v, i) => counts[i] / total - v / 12d).Should().OnlyContain(d => Math.Abs(d) < 0.03);
    }

    [Fact]
    public void SingleItem_FillsTheWholeArea() {
        var (_, _, surface) = Render(Named(("A", 7)));
        using var _ = surface;

        AssertRect(Stats(surface, ColorOf(0)).Bounds, Area);
    }

    [Fact]
    public void PortraitArea_SplitsVerticallyFirst() {
        var (_, _, surface) = Render(Named(("A", 1), ("B", 1)), area: new SKRect(0, 0, 100, 300));
        using var _ = surface;

        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(0, 0, 100, 150));
        AssertRect(Stats(surface, ColorOf(1)).Bounds, new SKRect(0, 150, 100, 300));
    }

    [Fact]
    public void NonPositiveValues_AreExcludedAndDoNotConsumeSpace() {
        var (series, _, surface) = Render(Named(("A", 1), ("B", 0), ("C", -4), ("D", 1)));
        using var _ = surface;

        Stats(surface, ColorOf(1)).Count.Should().Be(0);
        Stats(surface, ColorOf(2)).Count.Should().Be(0);
        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(0, 0, 200, 300));
        AssertRect(Stats(surface, ColorOf(3)).Bounds, new SKRect(200, 0, 400, 300));
        series.HitTest(new SKPoint(100, 100), Area)!.Value.Data!.Name.Should().Be("A");
    }

    [Theory]
    [InlineData(new double[0])]
    [InlineData(new[] { 0d, 0d })]
    [InlineData(new[] { -3d })]
    public void NoPositiveValues_DrawsNothingAndNothingIsHit(double[] values) {
        var (series, _, surface) = Render(values.Select((v, i) => new Item($"I{i}", (decimal)v)).ToArray());
        using var _ = surface;

        CountNonBackground(surface).Should().Be(0);
        series.HitTest(Mid(Area), Area).Should().BeNull();
    }

    [Fact]
    public void DegenerateArea_DrawsNothingAndDoesNotThrow() {
        var cut = RenderChart<NTTreeMapSeries<Item>>(Weighted, Plain("A", "B", "C", "D"));
        var series = cut.FindComponent<NTTreeMapSeries<Item>>().Instance;
        using var surface = NewSurface();
        var context = CreateContext(cut.Instance, surface);

        var act = () => series.Render(context, new SKRect(10, 10, 10, 200));

        act.Should().NotThrow();
        CountNonBackground(surface).Should().Be(0);
    }

    [Fact]
    public void ItemPadding_InsetsEveryCellOnAllSides() {
        var (_, _, surface) = Render(Named(("A", 1), ("B", 1)), With(Plain("A", "B"), "ItemPadding", 10f));
        using var _ = surface;

        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(10, 10, 190, 290));
        AssertRect(Stats(surface, ColorOf(1)).Bounds, new SKRect(210, 10, 390, 290));
    }

    [Fact]
    public void ItemPadding_IsIgnoredForCellsTooSmallToHoldIt() {
        var (_, _, surface) = Render(Named(("A", 1), ("B", 1)), With(Plain("A", "B"), "ItemPadding", 120f));
        using var _ = surface;

        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(0, 0, 200, 300));
    }

    [Fact]
    public void Navigation_ReservesTheConfiguredHeightAboveTheCells() {
        var attributes = With(With(Plain("A"), "ShowNavigation", true), "NavigationHeight", 40f);
        var (_, _, surface) = Render(Named(("A", 1)), attributes);
        using var _ = surface;

        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(0, 40, 400, 300));
    }

    [Fact]
    public void NavigationHeight_ScalesWithDensity() {
        var cut = RenderChart<NTTreeMapSeries<Item>>(Named(("A", 1)), With(With(Plain("A"), "ShowNavigation", true), "NavigationHeight", 20f));
        var series = cut.FindComponent<NTTreeMapSeries<Item>>().Instance;
        using var surface = NewSurface();

        series.Render(CreateContext(cut.Instance, surface, 2f), Area);

        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(0, 40, 400, 300));
    }

    [Fact]
    public void HitTest_ReturnsTheCellUnderThePointWithItsDataAndNullInGapsOrOutside() {
        var data = Weighted;
        var (series, _, surface) = Render(data, With(Plain("A", "B", "C", "D"), "ItemPadding", 4f));
        using var _ = surface;

        series.HitTest(new SKPoint(100, 150), Area)!.Value.Data.Should().BeSameAs(data[0]);
        series.HitTest(new SKPoint(300, 75), Area)!.Value.Data.Should().BeSameAs(data[1]);
        series.HitTest(new SKPoint(260, 225), Area)!.Value.Data.Should().BeSameAs(data[2]);
        series.HitTest(new SKPoint(370, 225), Area)!.Value.Data.Should().BeSameAs(data[3]);
        series.HitTest(new SKPoint(200, 150), Area).Should().BeNull("the point falls in the padding gap between cells");
        series.HitTest(new SKPoint(500, 150), Area).Should().BeNull();
    }

    [Fact]
    public void HitTest_BeforeFirstRender_ReturnsNull() {
        var cut = RenderChart<NTTreeMapSeries<Item>>(Weighted, Plain("A"));
        var series = cut.FindComponent<NTTreeMapSeries<Item>>().Instance;

        series.HitTest(new SKPoint(100, 100), Area).Should().BeNull();
    }

    [Fact]
    public void HitTestIndex_IsStableAcrossRendersAndDistinctPerCell() {
        var (series, chart, surface) = Render(Weighted);
        using var _ = surface;
        var first = Weighted.Select((_, i) => i).Select(i => series.HitTest(new SKPoint(i == 0 ? 100 : i == 1 ? 300 : i == 2 ? 260 : 370, i < 2 ? 75 : 225), Area)!.Value.Index).ToArray();

        series.Render(CreateContext(chart, surface), Area);
        var second = Weighted.Select((_, i) => i).Select(i => series.HitTest(new SKPoint(i == 0 ? 100 : i == 1 ? 300 : i == 2 ? 260 : 370, i < 2 ? 75 : 225), Area)!.Value.Index).ToArray();

        first.Should().OnlyHaveUniqueItems();
        second.Should().Equal(first);
    }

    [Fact]
    public void Layout_FollowsAChangedRenderAreaWithoutInvalidate() {
        var (series, chart, surface) = Render(Named(("A", 1), ("B", 1)));
        using var _ = surface;

        surface.Canvas.Clear(SKColors.White);
        series.Render(CreateContext(chart, surface), new SKRect(0, 0, 200, 300));

        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(0, 0, 200, 150));
        AssertRect(Stats(surface, ColorOf(1)).Bounds, new SKRect(0, 150, 200, 300));
    }

    [Fact]
    public void HiddenSeries_PaintsNothing() {
        var (_, _, surface) = Render(Weighted, With(Plain("A", "B", "C", "D"), "Visible", false));
        using var _ = surface;

        Stats(surface, ColorOf(0)).Count.Should().Be(0);
    }

    [Fact]
    public void OnDataPointRender_CanRecolorLeafCells() {
        var attributes = With(Plain("A", "B"), "OnDataPointRender", (Action<NTComponents.Charts.Core.Series.NTDataPointRenderArgs<Item>>)(args => {
            if (args.Data.Name == "B") {
                args.Color = args.GetThemeColor(NTColor.Success);
            }
        }));
        var (_, _, surface) = Render(Named(("A", 1), ("B", 1)), attributes);
        using var _ = surface;

        Stats(surface, ColorOf(1)).Count.Should().Be(0);
        Stats(surface, ColorOf(5)).Count.Should().BeGreaterThan(50000);
    }

    [Fact]
    public void DefaultColoring_UsesDistinctPaletteColorsPerLeaf() {
        var attributes = Plain();
        attributes.Remove("ColorSelector");
        var (_, _, surface) = Render(Named(("A", 1), ("B", 1), ("C", 1)), attributes);
        using var _ = surface;

        var distinct = Enumerable.Range(0, 6).Count(i => Stats(surface, ColorOf(i)).Count > 10000);
        distinct.Should().Be(3);
    }

    [Fact]
    public void ExplicitSeriesColor_PaintsEveryLeafWithIt() {
        var attributes = Plain();
        attributes.Remove("ColorSelector");
        attributes["Color"] = (NTColor?)NTColor.Warning;
        var (_, _, surface) = Render(Named(("A", 1), ("B", 2)), attributes);
        using var _ = surface;

        Stats(surface, ColorOf(4)).Count.Should().BeGreaterThan(110000);
    }

    [Fact]
    public void Labels_AreOnlyDrawnWhenCellsAreLargeEnough() {
        var attributes = With(Plain("A", "B"), "ShowLabels", true);
        var (_, _, surface) = Render(Named(("A", 1000), ("B", 1)), attributes);
        using var _ = surface;

        AnyPixel(surface, new SKRect(100, 130, 300, 170), c => !IsNear(c, ColorOf(0), 6)).Should().BeTrue("the large cell shows its label");
        var tiny = Stats(surface, ColorOf(1)).Bounds;
        tiny.Width.Should().BeLessThan(36f);
    }
}
