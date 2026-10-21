using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Grouping, nested series levels, color contexts and child previews of the treemap.</summary>
public class Hierarchy_Tests : TreeMapTestBase {
    // G1 = a(3) + b(1) = 4, G2 = c(2)
    private static readonly Item[] Data = Grouped(("a", 3, "G1"), ("b", 1, "G1"), ("c", 2, "G2"));

    internal RenderFragment Nested(Item[] data, Dictionary<string, object>? parent = null, Dictionary<string, object>? child = null) {
        var parentAttributes = With(With(Plain("G1", "G2"), "GroupSelector", (Func<Item, string?>)(item => item.Group)), "ShowChildPreviews", true);
        foreach (var attribute in parent ?? []) {
            parentAttributes[attribute.Key] = attribute.Value;
        }
        var childAttributes = Plain("_", "_", "a", "b", "c");
        foreach (var attribute in child ?? []) {
            childAttributes[attribute.Key] = attribute.Value;
        }

        return SeriesFragment<NTTreeMapSeries<Item>>(data, parentAttributes, SeriesFragment<NTTreeMapSeries<Item>>(data, childAttributes));
    }

    private (NTTreeMapSeries<Item> Series, NTChart<Item> Chart, SKSurface Surface) RenderNested(Item[] data, Dictionary<string, object>? parent = null, Dictionary<string, object>? child = null) {
        var cut = RenderChart(Nested(data, parent, child));
        var series = cut.FindComponents<NTTreeMapSeries<Item>>().First().Instance;
        var surface = NewSurface();
        series.Render(CreateContext(cut.Instance, surface), Area);
        return (series, cut.Instance, surface);
    }

    [Fact]
    public void GroupsAtTheTopLevel_ShareTheAreaProportionallyToTheirSums() {
        var (_, _, surface) = RenderNested(Data, new() { ["ShowChildPreviews"] = false });
        using var _ = surface;

        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(0, 0, 266.67f, 300));
        AssertRect(Stats(surface, ColorOf(1)).Bounds, new SKRect(266.67f, 0, 400, 300));
    }

    [Fact]
    public void ChildPreviews_AreDrawnInsideTheParentBelowItsHeader() {
        var (_, _, surface) = RenderNested(Data);
        using var _ = surface;

        var a = Stats(surface, ColorOf(2)).Bounds;
        var b = Stats(surface, ColorOf(3)).Bounds;
        a.Top.Should().BeApproximately(28f, 2f, "header is clamp(16% of height, 12, 28) = 28px");
        a.Left.Should().BeApproximately(1.5f, 2f);
        a.Right.Should().BeLessThanOrEqualTo(266.67f);
        b.Top.Should().BeGreaterThan(a.Top, "smaller sibling is below the larger one");
        (a.Bottom).Should().BeLessThan(299f);
        Stats(surface, ColorOf(2)).Count.Should().BeGreaterThan(Stats(surface, ColorOf(3)).Count * 2);
        Stats(surface, ColorOf(4)).Bounds.Top.Should().BeApproximately(28f, 2f, "G2's only child fills its preview area");
    }

    [Fact]
    public void DisablingChildPreviews_ShowsOnlyGroupCells() {
        var (_, _, surface) = RenderNested(Data, new() { ["ShowChildPreviews"] = false });
        using var _ = surface;

        Stats(surface, ColorOf(2)).Count.Should().Be(0);
        Stats(surface, ColorOf(0)).Count.Should().BeGreaterThan(70000);
    }

    [Fact]
    public void HitTest_ReturnsTheGroupEvenWhenThePointIsOverAChildPreview() {
        var (series, _, surface) = RenderNested(Data);
        using var _ = surface;
        var overChild = Mid(Stats(surface, ColorOf(2)).Bounds);
        var overHeader = new SKPoint(100, 10);

        var child = series.HitTest(overChild, Area);
        var header = series.HitTest(overHeader, Area);

        child.Should().NotBeNull();
        child!.Value.Data!.Name.Should().Be("a", "group node samples its first item");
        header!.Value.Index.Should().Be(child.Value.Index);
        series.HitTest(new SKPoint(350, 150), Area)!.Value.Data!.Name.Should().Be("c");
        series.HitTest(new SKPoint(350, 150), Area)!.Value.Index.Should().NotBe(child.Value.Index);
    }

    [Fact]
    public void ColorSelector_ReceivesDepthGroupFlagPathValueAndSampleData() {
        var seen = new List<TreeMapColorContext<Item>>();
        Func<TreeMapColorContext<Item>, NTColor> record = context => {
            seen.Add(context);
            return NTColor.Assert;
        };
        var (_, _, surface) = RenderNested(Data, new() { ["ColorSelector"] = record }, new() { ["ColorSelector"] = record });
        using var _ = surface;

        var g1 = seen.First(c => c.Key == "G1");
        g1.IsGroup.Should().BeTrue();
        g1.Depth.Should().Be(0);
        g1.Value.Should().Be(4m);
        g1.Path.Should().Equal("G1");
        g1.Data!.Name.Should().Be("a");
        var leaf = seen.First(c => c.Key == "a");
        leaf.IsGroup.Should().BeFalse();
        leaf.Depth.Should().Be(1);
        leaf.Value.Should().Be(3m);
        leaf.Path.Should().Equal("G1", "a");
        leaf.Data.Should().BeSameAs(Data[0]);
    }

    [Fact]
    public void BlankOrNullGroupKeys_CollapseIntoUngroupedAndKeysAreTrimmed() {
        var data = Grouped(("a", 1, " G1 "), ("b", 1, "G1"), ("c", 1, ""), ("d", 1, null!));
        var seen = new List<string>();
        Func<TreeMapColorContext<Item>, NTColor> record = context => {
            if (context.IsGroup) {
                seen.Add(context.Key);
            }
            return NTColor.Assert;
        };
        var (series, _, surface) = RenderNested(data, new() { ["ColorSelector"] = record });
        using var _ = surface;

        seen.Distinct().Should().BeEquivalentTo(["G1", "Ungrouped"]);
        series.HitTest(new SKPoint(100, 150), Area).Should().NotBeNull();
    }

    [Fact]
    public void SingleLevelWithGroupSelectorOnly_StillFormsGroupCells() {
        var attributes = With(Plain("G1", "G2"), "GroupSelector", (Func<Item, string?>)(item => item.Group));
        var cut = RenderChart<NTTreeMapSeries<Item>>(Data, attributes);
        var series = cut.FindComponent<NTTreeMapSeries<Item>>().Instance;
        using var surface = NewSurface();

        series.Render(CreateContext(cut.Instance, surface), Area);

        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(0, 0, 266.67f, 300));
        series.HitTest(new SKPoint(350, 150), Area)!.Value.Data!.Name.Should().Be("c");
    }

    [Fact]
    public void TotalValue_IsTheSumOfPositiveValues() {
        var cut = RenderChart<NTTreeMapSeries<Item>>(Named(("A", 3), ("B", -2), ("C", 4)), Plain("A"));
        var series = cut.FindComponent<NTTreeMapSeries<Item>>().Instance;

        ((decimal)typeof(NTTreeMapSeries<Item>).BaseType!.GetMethod("GetTotalValue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(series, null)!).Should().Be(7m);
    }

    [Fact]
    public void ChildSeriesRemoved_RebuildsTheHierarchyWithoutTheLowerLevel() {
        var cut = RenderChart(Nested(Data));
        var series = cut.FindComponents<NTTreeMapSeries<Item>>().First().Instance;
        using var surface = NewSurface();
        series.Render(CreateContext(cut.Instance, surface), Area);
        Stats(surface, ColorOf(2)).Count.Should().BeGreaterThan(0);

        cut.Render(p => p.Add(c => c.ChildContent, SeriesFragment<NTTreeMapSeries<Item>>(Data, With(Plain("G1", "G2"), "GroupSelector", (Func<Item, string?>)(item => item.Group)), _ => { })));
        surface.Canvas.Clear(SKColors.White);
        series.Render(CreateContext(cut.Instance, surface), Area);

        Stats(surface, ColorOf(2)).Count.Should().Be(0);
        Stats(surface, ColorOf(0)).Count.Should().BeGreaterThan(70000);
    }

    [Fact]
    public void LegendAndHierarchy_ReportASingleEntryForTheWholeTree() {
        var cut = RenderChart(Nested(Data, new() { ["Title"] = "Sales" }));
        var series = cut.FindComponents<NTTreeMapSeries<Item>>().First().Instance;

        var entries = LegendItems(series);

        entries.Should().ContainSingle();
        entries[0].Label.Should().Be("Sales");
        entries[0].Index.Should().BeNull();
        entries[0].IsVisible.Should().BeTrue();
        ToggleLegend(series, null);
        series.Visible.Should().BeFalse();
        LegendItems(series)[0].IsVisible.Should().BeFalse();
    }

    [Fact]
    public void LegendWithoutTitle_UsesTreeMapLabel() {
        var cut = RenderChart<NTTreeMapSeries<Item>>(Data, Plain());

        LegendItems(cut.FindComponent<NTTreeMapSeries<Item>>().Instance)[0].Label.Should().Be("TreeMap");
    }
}
