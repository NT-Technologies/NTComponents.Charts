using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Hit-testing, dragging and zooming of bubbles.</summary>
public class BubbleInteraction_Tests : BubblePackTestBase {
    private static readonly Item[] Data = Named(("A", 4), ("B", 2), ("C", 1));

    private (NTBubblePackSeries<Item> Series, NTChart<Item> Chart, SKSurface Surface) Create(Dictionary<string, object>? attributes = null, Item[]? data = null) {
        var cut = RenderChart<NTBubblePackSeries<Item>>(data ?? Data, attributes ?? Plain("A", "B", "C"));
        var series = cut.FindComponent<NTBubblePackSeries<Item>>().Instance;
        var surface = NewField();
        Frame(series, cut.Instance, surface);
        return (series, cut.Instance, surface);
    }

    [Fact]
    public void HitTest_AtEachBubbleCenter_ReturnsThatBubbleAndItsData() {
        var (series, _, surface) = Create();
        using var _ = surface;

        for (var i = 0; i < 3; i++) {
            series.HitTest(Stats(surface, ColorOf(i)).Centroid, Area)!.Value.Data.Should().BeSameAs(Data[i]);
        }
    }

    [Fact]
    public void HitTest_UsesTheBubbleRadiusAsTheBoundary() {
        var (series, _, surface) = Create(Plain("A", "B", "C"));
        using var _ = surface;
        var a = Stats(surface, ColorOf(0));
        var inside = new SKPoint(a.Centroid.X + a.EquivalentRadius - 3, a.Centroid.Y);
        var farAway = new SKPoint(2, 2);

        series.HitTest(inside, Area)!.Value.Data!.Name.Should().Be("A");
        series.HitTest(farAway, Area).Should().BeNull();
    }

    [Fact]
    public void HitTest_ReportsDistinctStableIndicesPerBubble() {
        var (series, chart, surface) = Create();
        using var _ = surface;
        var first = Enumerable.Range(0, 3).Select(i => series.HitTest(Stats(surface, ColorOf(i)).Centroid, Area)!.Value.Index).ToArray();

        Frame(series, chart, surface);
        var second = Enumerable.Range(0, 3).Select(i => series.HitTest(Stats(surface, ColorOf(i)).Centroid, Area)!.Value.Index).ToArray();

        first.Should().OnlyHaveUniqueItems();
        second.Should().Equal(first);
    }

    [Fact]
    public void Drag_BeyondTheThresholdMovesTheBubbleWithThePointer() {
        var (series, chart, surface) = Create(With(Plain("A", "B", "C"), "GravityStrength", 0f));
        using var _ = surface;
        var start = Stats(surface, ColorOf(2)).Centroid;
        var target = new SKPoint(start.X + 30, start.Y + 10);

        series.HandleMouseDown(Mouse(start.X, start.Y));
        series.HandleMouseMove(Mouse(target.X, target.Y));
        for (var i = 0; i < 3; i++) {
            Frame(series, chart, surface);
        }

        var moved = Stats(surface, ColorOf(2)).Centroid;
        moved.X.Should().BeApproximately(target.X, 2f);
        moved.Y.Should().BeApproximately(target.Y, 2f);
        RequiresAnimationFrame(series).Should().BeTrue("a dragged bubble keeps the render loop running");
    }

    [Fact]
    public void Drag_UnderTheThreshold_DoesNotMoveTheBubble() {
        var (series, chart, surface) = Create(With(Plain("A", "B", "C"), "GravityStrength", 0f));
        using var _ = surface;
        var start = Stats(surface, ColorOf(2)).Centroid;

        series.HandleMouseDown(Mouse(start.X, start.Y));
        series.HandleMouseMove(Mouse(start.X + 4, start.Y));
        for (var i = 0; i < 3; i++) {
            Frame(series, chart, surface);
        }

        Stats(surface, ColorOf(2)).Centroid.X.Should().BeApproximately(start.X, 1.5f);
    }

    [Fact]
    public void PressingOutsideAnyBubble_IgnoresSubsequentMoves() {
        var (series, chart, surface) = Create(With(Plain("A", "B", "C"), "GravityStrength", 0f));
        using var _ = surface;
        var before = Bubbles(surface, 3);

        series.HandleMouseDown(Mouse(Area.Left + 2, Area.Top + 2));
        series.HandleMouseMove(Mouse(Area.Left + 200, Area.Top + 100));
        Frame(series, chart, surface);
        Frame(series, chart, surface);

        var after = Bubbles(surface, 3);
        for (var i = 0; i < 3; i++) {
            after[i].Center.X.Should().BeApproximately(before[i].Center.X, 1.5f);
        }
    }

    [Fact]
    public void DraggedBubble_PushesOverlappingNeighborsAwayWithoutBeingMoved() {
        var (series, chart, surface) = Create(With(Plain("A", "B", "C"), "GravityStrength", 0f));
        using var _ = surface;
        var a = Stats(surface, ColorOf(0)).Centroid;
        var c = Stats(surface, ColorOf(2)).Centroid;

        series.HandleMouseDown(Mouse(c.X, c.Y));
        series.HandleMouseMove(Mouse(a.X, a.Y));
        for (var i = 0; i < 60; i++) {
            Frame(series, chart, surface);
        }

        var bubbles = Bubbles(surface, 3);
        bubbles[2].Center.X.Should().BeApproximately(a.X, 2f, "the dragged bubble stays under the pointer");
        bubbles[2].Center.Y.Should().BeApproximately(a.Y, 2f);
        Gap(bubbles[0], bubbles[2]).Should().BeGreaterThan(-3f, "the neighbor is pushed out of the dragged bubble");
    }

    [Fact]
    public void ReleasingADrag_WakesPhysicsAndTheClusterEventuallySettlesWithoutOverlap() {
        var (series, chart, surface) = Create();
        using var _ = surface;
        var c = Stats(surface, ColorOf(2)).Centroid;
        series.HandleMouseDown(Mouse(c.X, c.Y));
        series.HandleMouseMove(Mouse(c.X + 40, c.Y + 40));
        for (var i = 0; i < 5; i++) {
            Frame(series, chart, surface);
        }

        series.HandleMouseUp(Mouse(c.X + 40, c.Y + 40));
        Settle(series, chart, surface);

        RequiresAnimationFrame(series).Should().BeFalse();
        var bubbles = Bubbles(surface, 3);
        for (var i = 0; i < 3; i++) {
            for (var j = i + 1; j < 3; j++) {
                Gap(bubbles[i], bubbles[j]).Should().BeGreaterThan(-1.5f);
            }
        }
    }

    [Fact]
    public void ConstrainedDrag_KeepsTheBubbleInsideTheContentArea() {
        var (series, chart, surface) = Create(With(With(Plain("A", "B", "C"), "ConstrainToCanvas", true), "GravityStrength", 0f));
        using var _ = surface;
        var c = Stats(surface, ColorOf(2)).Centroid;

        series.HandleMouseDown(Mouse(c.X, c.Y));
        series.HandleMouseMove(Mouse(Area.Right + 300, Area.Bottom + 300));
        for (var i = 0; i < 4; i++) {
            Frame(series, chart, surface);
        }

        var bounds = Stats(surface, ColorOf(2)).Bounds;
        bounds.Right.Should().BeLessThanOrEqualTo(Area.Right + 1.5f);
        bounds.Bottom.Should().BeLessThanOrEqualTo(Area.Bottom + 1.5f);
    }

    [Fact]
    public void WheelUp_ZoomsBubbleCentersAndRadiiAboutTheContentCenter() {
        var (series, chart, surface) = Create();
        using var _ = surface;
        var before = Stats(surface, ColorOf(1));
        var originOffset = new SKPoint(before.Centroid.X - Center.X, before.Centroid.Y - Center.Y);

        series.HandleMouseWheel(new WheelEventArgs { OffsetX = Center.X, OffsetY = Center.Y, DeltaY = -1 });
        Frame(series, chart, surface);

        var after = Stats(surface, ColorOf(1));
        after.EquivalentRadius.Should().BeApproximately(before.EquivalentRadius * 1.1f, 1.5f);
        (after.Centroid.X - Center.X).Should().BeApproximately(originOffset.X * 1.1f, 2f);
        (after.Centroid.Y - Center.Y).Should().BeApproximately(originOffset.Y * 1.1f, 2f);
    }

    [Fact]
    public void HitTest_FollowsTheZoomedBubblePositions() {
        var (series, chart, surface) = Create();
        using var _ = surface;
        series.HandleMouseWheel(new WheelEventArgs { OffsetX = Center.X, OffsetY = Center.Y, DeltaY = -1 });
        series.HandleMouseWheel(new WheelEventArgs { OffsetX = Center.X, OffsetY = Center.Y, DeltaY = -1 });
        Frame(series, chart, surface);

        for (var i = 0; i < 3; i++) {
            series.HitTest(Stats(surface, ColorOf(i)).Centroid, Area)!.Value.Data.Should().BeSameAs(Data[i]);
        }
    }

    [Fact]
    public void WheelDown_ZoomsOutAndIsClampedAtTheMinimumScale() {
        var (series, chart, surface) = Create();
        using var _ = surface;
        var baseline = Stats(surface, ColorOf(0)).EquivalentRadius;

        for (var i = 0; i < 40; i++) {
            series.HandleMouseWheel(new WheelEventArgs { OffsetX = Center.X, OffsetY = Center.Y, DeltaY = 1 });
        }
        Frame(series, chart, surface);

        (Stats(surface, ColorOf(0)).EquivalentRadius / baseline).Should().BeApproximately(0.45f, 0.03f);
    }

    [Fact]
    public void WheelUp_IsClampedAtTheMaximumScale() {
        var (series, chart, surface) = Create(With(Plain("A", "B", "C"), "TargetFillRatio", 0.05f), [new Item("A", 1)]);
        using var _ = surface;
        var baseline = Stats(surface, ColorOf(0)).EquivalentRadius;

        for (var i = 0; i < 60; i++) {
            series.HandleMouseWheel(new WheelEventArgs { OffsetX = Center.X, OffsetY = Center.Y, DeltaY = -1 });
        }
        var surface2 = NewField();
        using var __ = surface2;
        Frame(series, chart, surface2);

        (Stats(surface2, ColorOf(0)).EquivalentRadius / baseline).Should().BeApproximately(3.2f, 0.2f);
    }

    [Fact]
    public void WheelOutsideTheContentArea_IsIgnored() {
        var (series, chart, surface) = Create();
        using var _ = surface;
        var baseline = Stats(surface, ColorOf(0)).EquivalentRadius;

        series.HandleMouseWheel(new WheelEventArgs { OffsetX = 5, OffsetY = 5, DeltaY = -1 });
        Frame(series, chart, surface);

        Stats(surface, ColorOf(0)).EquivalentRadius.Should().BeApproximately(baseline, 0.5f);
    }

    [Fact]
    public void XOnlyZoom_ScalesHorizontalSpreadButRadiusByTheGeometricMean() {
        var (series, chart, surface) = Create(With(Plain("A", "B", "C"), "Interactions", ChartInteractions.XZoom));
        using var _ = surface;
        var before = Stats(surface, ColorOf(1));

        series.HandleMouseWheel(new WheelEventArgs { OffsetX = Center.X, OffsetY = Center.Y, DeltaY = -1 });
        Frame(series, chart, surface);

        var after = Stats(surface, ColorOf(1));
        (after.Centroid.X - Center.X).Should().BeApproximately((before.Centroid.X - Center.X) * 1.1f, 2f);
        (after.Centroid.Y - Center.Y).Should().BeApproximately(before.Centroid.Y - Center.Y, 1.5f);
        after.EquivalentRadius.Should().BeApproximately(before.EquivalentRadius * MathF.Sqrt(1.1f), 1.5f);
    }

    [Fact]
    public void ResetView_RestoresTheUnzoomedLayout() {
        var (series, chart, surface) = Create();
        using var _ = surface;
        var baseline = Stats(surface, ColorOf(0));
        series.HandleMouseWheel(new WheelEventArgs { OffsetX = Center.X, OffsetY = Center.Y, DeltaY = -1 });
        series.HandleMouseWheel(new WheelEventArgs { OffsetX = Center.X, OffsetY = Center.Y, DeltaY = -1 });

        series.ResetView();
        Frame(series, chart, surface);

        var after = Stats(surface, ColorOf(0));
        after.EquivalentRadius.Should().BeApproximately(baseline.EquivalentRadius, 1f);
        after.Centroid.X.Should().BeApproximately(baseline.Centroid.X, 2f);
    }

    [Fact]
    public async Task ZoomRaisesTheSeriesZoomCallback() {
        var calls = 0;
        var attributes = With(Plain("A", "B", "C"), "OnZoom", Microsoft.AspNetCore.Components.EventCallback.Factory.Create<NTComponents.Charts.Core.Series.NTSeriesZoomEventArgs<Item>>(this, _ => calls++));
        var cut = RenderChart<NTBubblePackSeries<Item>>(Data, attributes);
        var series = cut.FindComponent<NTBubblePackSeries<Item>>().Instance;
        using var surface = NewField();
        Frame(series, cut.Instance, surface);

        await cut.InvokeAsync(() => series.HandleMouseWheel(new WheelEventArgs { OffsetX = Center.X, OffsetY = Center.Y, DeltaY = -1 }));
        // Series callbacks are queued on the renderer dispatcher (InvokeAsync); a later turn on the same dispatcher runs after them.
        await cut.InvokeAsync(() => { });

        calls.Should().Be(1);
    }

    [Fact]
    public void Interactions_NeverIncludePanning() {
        var (series, _, surface) = Create(With(Plain("A", "B", "C"), "Interactions", ChartInteractions.All));
        using var _ = surface;

        series.Interactions.HasFlag(ChartInteractions.XPan).Should().BeFalse();
        series.Interactions.HasFlag(ChartInteractions.YPan).Should().BeFalse();
        series.Interactions.HasFlag(ChartInteractions.XZoom).Should().BeTrue();
    }
}
