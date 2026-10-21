using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Drill-down and drill-up navigation of grouped treemaps.</summary>
public class Drilldown_Tests : TreeMapTestBase {
    // G1 = a(3) + b(1) occupies x 0..266.67; G2 = c(2) occupies x 266.67..400.
    private static readonly Item[] Data = Grouped(("a", 3, "G1"), ("b", 1, "G1"), ("c", 2, "G2"));

    private (ClockedTreeMap Series, NTChart<Item> Chart, SKSurface Surface) Create(Dictionary<string, object>? parent = null, Item[]? data = null) {
        var parentAttributes = With(Plain("G1", "G2"), "GroupSelector", (Func<Item, string?>)(item => item.Group));
        foreach (var attribute in parent ?? []) {
            parentAttributes[attribute.Key] = attribute.Value;
        }
        data ??= Data;
        var cut = RenderChart(SeriesFragment<ClockedTreeMap>(data, parentAttributes, SeriesFragment<NTTreeMapSeries<Item>>(data, Plain("_", "_", "a", "b", "c"))));
        var series = cut.FindComponent<ClockedTreeMap>().Instance;
        var surface = NewSurface();
        series.Render(CreateContext(cut.Instance, surface), Area);
        return (series, cut.Instance, surface);
    }

    private static void Repaint(NTTreeMapSeries<Item> series, NTChart<Item> chart, SKSurface surface) {
        surface.Canvas.Clear(SKColors.White);
        series.Render(CreateContext(chart, surface), Area);
    }

    private static bool InDrilldown(object series) {
        for (var type = series.GetType(); type is not null; type = type.BaseType) {
            var property = type.GetProperty("NTComponents.Charts.Core.Series.ITreeMapDrillableSeries.IsInDrilldown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);
            if (property is not null) {
                return (bool)property.GetValue(series)!;
            }
        }

        throw new InvalidOperationException("IsInDrilldown not found");
    }

    [Fact]
    public void ClickingAGroup_ShowsItsChildrenAcrossTheWholeArea() {
        var (series, chart, surface) = Create(new() { ["ShowChildPreviews"] = false });
        using var _ = surface;

        series.HandleMouseDown(Mouse(100, 150));
        Repaint(series, chart, surface);

        InDrilldown(series).Should().BeTrue();
        AssertRect(Stats(surface, ColorOf(2)).Bounds, new SKRect(0, 0, 300, 300));
        AssertRect(Stats(surface, ColorOf(3)).Bounds, new SKRect(300, 0, 400, 300));
        Stats(surface, ColorOf(4)).Count.Should().Be(0, "the sibling group is no longer shown");
        series.HitTest(new SKPoint(100, 100), Area)!.Value.Data!.Name.Should().Be("a");
        series.HitTest(new SKPoint(350, 100), Area)!.Value.Data!.Name.Should().Be("b");
    }

    [Fact]
    public void ClickingALeaf_DoesNotNavigate() {
        var (series, chart, surface) = Create(new() { ["ShowChildPreviews"] = false });
        using var _ = surface;
        series.HandleMouseDown(Mouse(100, 150));
        Repaint(series, chart, surface);
        var before = Snapshot(surface);

        series.HandleMouseDown(Mouse(100, 150));
        Repaint(series, chart, surface);

        Snapshot(surface).Should().Equal(before);
        InDrilldown(series).Should().BeTrue();
    }

    [Fact]
    public void DrilldownDisabled_IgnoresClicks() {
        var (series, chart, surface) = Create(new() { ["EnableDrilldown"] = false, ["ShowChildPreviews"] = false });
        using var _ = surface;
        var before = Snapshot(surface);

        series.HandleMouseDown(Mouse(100, 150));
        Repaint(series, chart, surface);

        InDrilldown(series).Should().BeFalse();
        Snapshot(surface).Should().Equal(before);
    }

    [Fact]
    public void ClickingInTheGapOrOutsideTheSeriesArea_DoesNothing() {
        var (series, chart, surface) = Create(new() { ["ShowChildPreviews"] = false, ["ItemPadding"] = 10f });
        using var _ = surface;

        series.HandleMouseDown(Mouse(266.67, 150));
        series.HandleMouseDown(Mouse(900, 900));
        Repaint(series, chart, surface);

        InDrilldown(series).Should().BeFalse();
    }

    [Fact]
    public void ClickDuringChildPreview_StillDrillsIntoTheParentGroup() {
        var (series, chart, surface) = Create();
        using var _ = surface;

        series.HandleMouseDown(Mouse(130, 150));
        Repaint(series, chart, surface);

        series.HitTest(new SKPoint(100, 100), Area)!.Value.Data!.Name.Should().Be("a");
        InDrilldown(series).Should().BeTrue();
    }

    [Fact]
    public void BackButton_ReturnsToTheParentLevel() {
        var (series, chart, surface) = Create(new() { ["ShowNavigation"] = true, ["ShowChildPreviews"] = false });
        using var _ = surface;
        series.HandleMouseDown(Mouse(100, 150));
        Repaint(series, chart, surface);
        InDrilldown(series).Should().BeTrue();

        series.HandleMouseDown(Mouse(34, 15));
        Repaint(series, chart, surface);

        InDrilldown(series).Should().BeFalse();
        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(0, 30, 266.67f, 300));
    }

    [Fact]
    public void BackButton_IsNotHitAtTheTopLevel() {
        var (series, chart, surface) = Create(new() { ["ShowNavigation"] = true, ["ShowChildPreviews"] = false });
        using var _ = surface;

        series.HandleMouseDown(Mouse(34, 15));
        Repaint(series, chart, surface);

        InDrilldown(series).Should().BeFalse();
        series.HitTest(new SKPoint(100, 150), Area).Should().NotBeNull();
    }

    [Fact]
    public void WithoutNavigationBar_ThereIsNoBackButtonToLeaveTheDrilledGroup() {
        var (series, chart, surface) = Create(new() { ["ShowNavigation"] = false, ["ShowChildPreviews"] = false });
        using var _ = surface;
        series.HandleMouseDown(Mouse(100, 150));
        Repaint(series, chart, surface);

        series.HandleMouseDown(Mouse(34, 15));
        Repaint(series, chart, surface);

        InDrilldown(series).Should().BeTrue();
    }

    [Fact]
    public void BackButtonWidth_FollowsBackText() {
        var (series, chart, surface) = Create(new() { ["ShowNavigation"] = true, ["ShowChildPreviews"] = false, ["BackText"] = "Return to the previous level" });
        using var _ = surface;
        series.HandleMouseDown(Mouse(100, 150));
        Repaint(series, chart, surface);

        // (len + 3) * 7 = 217px wide button starting at x=6, so x=200 is on it.
        series.HandleMouseDown(Mouse(200, 15));
        Repaint(series, chart, surface);

        InDrilldown(series).Should().BeFalse();
    }

    [Fact]
    public void ReplacingData_ResetsTheDrillPath() {
        var (series, chart, surface) = Create(new() { ["ShowChildPreviews"] = false });
        using var _ = surface;
        series.HandleMouseDown(Mouse(100, 150));
        Repaint(series, chart, surface);

        series.Data = Grouped(("x", 1, "G1"), ("y", 1, "G2"));
        series.Invalidate();
        Repaint(series, chart, surface);

        InDrilldown(series).Should().BeFalse();
        Stats(surface, ColorOf(0)).Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ChangingConfiguration_ResetsTheDrillPath() {
        var (series, chart, surface) = Create(new() { ["ShowChildPreviews"] = false });
        using var _ = surface;
        series.HandleMouseDown(Mouse(100, 150));
        Repaint(series, chart, surface);

        series.ItemPadding = 3f;
        typeof(NTTreeMapSeries<Item>).GetMethod("OnParametersSet", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(series, null);

        InDrilldown(series).Should().BeFalse();
    }

    [Fact]
    public void DrillTransition_BlocksFurtherClicksUntilTheAnimationFinishes() {
        var (series, chart, surface) = Create(new() { ["AnimationEnabled"] = true, ["ShowNavigation"] = true, ["ShowChildPreviews"] = false });
        using var _ = surface;
        series.SetAnimationAge(TimeSpan.FromSeconds(10));
        Repaint(series, chart, surface);
        series.HandleMouseDown(Mouse(100, 150));
        Repaint(series, chart, surface);
        RequiresAnimationFrame(series).Should().BeTrue("the drill transition is animating");

        series.HandleMouseDown(Mouse(34, 15));
        InDrilldown(series).Should().BeTrue("clicks are ignored while a transition runs");

        series.SetAnimationAge(TimeSpan.FromSeconds(10));
        Repaint(series, chart, surface);
        RequiresAnimationFrame(series).Should().BeFalse();
        series.HandleMouseDown(Mouse(34, 15));
        Repaint(series, chart, surface);
        InDrilldown(series).Should().BeFalse();
    }

    [Fact]
    public void DrillIndicator_IsOnlyDrawnOnGroupCells() {
        var withIndicator = Create(new() { ["ShowDrillIndicator"] = true, ["ShowChildPreviews"] = false });
        using var a = withIndicator.Surface;
        var without = Create(new() { ["ShowDrillIndicator"] = false, ["ShowChildPreviews"] = false });
        using var b = without.Surface;

        Snapshot(a).Should().NotEqual(Snapshot(b));
        var topRight = new SKRect(266.67f - 22, 0, 266.67f, 22);
        AnyPixel(a, topRight, c => !IsNear(c, ColorOf(0), 6)).Should().BeTrue();
        AnyPixel(b, new SKRect(266.67f - 22, 3, 266.67f - 3, 22), c => !IsNear(c, ColorOf(0), 6)).Should().BeFalse();
    }
}
