using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Grouping, drill-down navigation, tooltips, visibility and animation of the bubble pack.</summary>
public class BubbleHierarchy_Tests : BubblePackTestBase {
    // G1 = a(3)+b(1) = 4, G2 = c(2)
    private static readonly Item[] Data = Grouped(("a", 3, "G1"), ("b", 1, "G1"), ("c", 2, "G2"));

    private (ClockedBubblePack Series, IRenderedComponent<NTChart<Item>> Cut, SKSurface Surface) Create(Dictionary<string, object>? parent = null, Item[]? data = null) {
        var attributes = With(Plain("G1", "G2"), "GroupSelector", (Func<Item, string?>)(item => item.Group));
        foreach (var attribute in parent ?? []) {
            attributes[attribute.Key] = attribute.Value;
        }
        data ??= Data;
        var cut = RenderChart(SeriesFragment<ClockedBubblePack>(data, attributes, SeriesFragment<NTBubblePackSeries<Item>>(data, Plain("_", "_", "a", "b", "c"))));
        var series = cut.FindComponent<ClockedBubblePack>().Instance;
        var surface = NewField();
        Frame(series, cut.Instance, surface);
        return (series, cut, surface);
    }

    private static void Click(NTBubblePackSeries<Item> series, SKPoint point) {
        series.HandleMouseDown(Mouse(point.X, point.Y));
        series.HandleMouseUp(Mouse(point.X, point.Y));
    }

    [Fact]
    public void GroupBubbles_AreSizedByTheirSums() {
        var (_, _, surface) = Create();
        using var _ = surface;

        var g1 = Stats(surface, ColorOf(0));
        var g2 = Stats(surface, ColorOf(1));

        ((double)g1.Count / g2.Count).Should().BeApproximately(2d, 0.25);
    }

    [Fact]
    public void ClickingAGroupBubble_ShowsItsMembersInstead() {
        var (series, cut, surface) = Create();
        using var _ = surface;
        Click(series, Stats(surface, ColorOf(0)).Centroid);

        Frame(series, cut.Instance, surface);

        Stats(surface, ColorOf(0)).Count.Should().Be(0);
        Stats(surface, ColorOf(1)).Count.Should().Be(0);
        var a = Stats(surface, ColorOf(2));
        var b = Stats(surface, ColorOf(3));
        a.Count.Should().BeGreaterThan(b.Count * 2);
        Stats(surface, ColorOf(4)).Count.Should().Be(0, "members of the other group are not shown");
        series.HitTest(a.Centroid, Area)!.Value.Data!.Name.Should().Be("a");
    }

    [Fact]
    public void ClickingALeafBubble_DoesNotNavigate() {
        var (series, cut, surface) = Create();
        using var _ = surface;
        Click(series, Stats(surface, ColorOf(0)).Centroid);
        Frame(series, cut.Instance, surface);
        var leaf = Stats(surface, ColorOf(2)).Centroid;

        Click(series, leaf);
        Frame(series, cut.Instance, surface);

        Stats(surface, ColorOf(2)).Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void DrilldownDisabled_KeepsGroupBubbles() {
        var (series, cut, surface) = Create(new() { ["EnableDrilldown"] = false });
        using var _ = surface;

        Click(series, Stats(surface, ColorOf(0)).Centroid);
        Frame(series, cut.Instance, surface);

        Stats(surface, ColorOf(0)).Count.Should().BeGreaterThan(0);
        Stats(surface, ColorOf(2)).Count.Should().Be(0);
    }

    [Fact]
    public void BackButton_ReturnsToTheGroupLevel() {
        var (series, cut, surface) = Create(new() { ["ShowNavigation"] = true });
        using var _ = surface;
        Click(series, Stats(surface, ColorOf(0)).Centroid);
        Frame(series, cut.Instance, surface);
        Stats(surface, ColorOf(0)).Count.Should().Be(0);

        series.HandleMouseDown(Mouse(Area.Left + 34, Area.Top + 15));
        Frame(series, cut.Instance, surface);
        Frame(series, cut.Instance, surface);

        Stats(surface, ColorOf(0)).Count.Should().BeGreaterThan(0);
        Stats(surface, ColorOf(2)).Count.Should().Be(0);
    }

    [Fact]
    public void BackButton_DoesNothingAtTheTopLevel() {
        var (series, cut, surface) = Create(new() { ["ShowNavigation"] = true });
        using var _ = surface;
        var before = Stats(surface, ColorOf(0)).Count;

        series.HandleMouseDown(Mouse(Area.Left + 34, Area.Top + 15));
        Frame(series, cut.Instance, surface);

        Stats(surface, ColorOf(0)).Count.Should().BeInRange((long)(before * 0.95), (long)(before * 1.05));
    }

    [Fact]
    public void DrillingDown_RestartsPackingForTheNewLevel() {
        var (series, cut, surface) = Create();
        using var _ = surface;
        Frame(series, cut.Instance, surface);
        Settle(series, cut.Instance, surface);
        RequiresAnimationFrame(series).Should().BeFalse();

        Click(series, Stats(surface, ColorOf(0)).Centroid);
        Frame(series, cut.Instance, surface);

        RequiresAnimationFrame(series).Should().BeTrue();
    }

    [Fact]
    public void ReplacingData_ResetsTheDrillPath() {
        var (series, cut, surface) = Create();
        using var _ = surface;
        Click(series, Stats(surface, ColorOf(0)).Centroid);
        Frame(series, cut.Instance, surface);

        series.Data = Grouped(("x", 1, "G1"), ("y", 1, "G2"));
        series.Invalidate();
        Frame(series, cut.Instance, surface);

        Stats(surface, ColorOf(0)).Count.Should().BeGreaterThan(0);
        Stats(surface, ColorOf(1)).Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ColorSelector_ReceivesTheGroupContext() {
        var seen = new List<BubbleColorContext<Item>>();
        Func<BubbleColorContext<Item>, NTColor> record = context => {
            seen.Add(context);
            return NTColor.Assert;
        };
        var (_, _, surface) = Create(new() { ["ColorSelector"] = record });
        using var _ = surface;
        var group = seen.First(c => c.Key == "G1");
        group.IsGroup.Should().BeTrue();
        group.Depth.Should().Be(0);
        group.Value.Should().Be(4m);
        group.Path.Should().Equal("G1");
        group.Data!.Name.Should().Be("a");
    }
}
