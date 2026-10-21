using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Bubble sizing, initial placement and the settled packing produced by the physics simulation.</summary>
public class BubbleLayout_Tests : BubblePackTestBase {
    private (NTBubblePackSeries<Item> Series, NTChart<Item> Chart, SKSurface Surface) Create(Item[] data, Dictionary<string, object>? attributes = null) {
        var cut = RenderChart<NTBubblePackSeries<Item>>(data, attributes ?? Plain("A", "B", "C", "D", "E", "F"));
        return (cut.FindComponent<NTBubblePackSeries<Item>>().Instance, cut.Instance, NewField());
    }

    private static (NTBubblePackSeries<Item> Series, NTChart<Item> Chart, SKSurface Surface) FirstFrame((NTBubblePackSeries<Item> Series, NTChart<Item> Chart, SKSurface Surface) setup) {
        Frame(setup.Series, setup.Chart, setup.Surface);
        return setup;
    }

    [Fact]
    public void SingleBubble_IsCenteredInTheArea() {
        var (series, chart, surface) = FirstFrame(Create(Named(("A", 5))));
        using var _ = surface;

        var bubble = Stats(surface, ColorOf(0));

        bubble.Centroid.X.Should().BeApproximately(400f, 1f);
        bubble.Centroid.Y.Should().BeApproximately(300f, 1f);
        series.HitTest(Center, Area)!.Value.Data!.Name.Should().Be("A");
    }

    [Fact]
    public void BubbleAreas_AreProportionalToValues() {
        var (_, _, surface) = FirstFrame(Create(Named(("A", 4), ("B", 2), ("C", 1))));
        using var _ = surface;

        var areas = Enumerable.Range(0, 3).Select(i => (double)Stats(surface, ColorOf(i)).Count).ToArray();

        (areas[0] / areas[2]).Should().BeApproximately(4d, 0.4);
        (areas[1] / areas[2]).Should().BeApproximately(2d, 0.2);
    }

    [Fact]
    public void TotalBubbleArea_MatchesTheTargetFillRatioOfThePaddedCanvas() {
        var (_, _, surface) = FirstFrame(Create(Named(("A", 4), ("B", 2), ("C", 1))));
        using var _ = surface;

        var total = Enumerable.Range(0, 3).Sum(i => Stats(surface, ColorOf(i)).Count);

        // (400 - 2*10) * (300 - 2*10) * 0.32 = 34048
        total.Should().BeInRange((long)(34048 * 0.92), (long)(34048 * 1.02));
    }

    [Fact]
    public void TargetFillRatio_ScalesTheTotalBubbleAreaAndIsClamped() {
        var data = Named(("A", 4), ("B", 2), ("C", 1));
        var (_, _, small) = FirstFrame(Create(data, With(Plain("A", "B", "C"), "TargetFillRatio", 0.1f)));
        using var a = small;
        var (_, _, bigger) = FirstFrame(Create(data, With(Plain("A", "B", "C"), "TargetFillRatio", 0.2f)));
        using var b = bigger;
        var (_, _, clampedLow) = FirstFrame(Create(data, With(Plain("A", "B", "C"), "TargetFillRatio", 0.0f)));
        using var c = clampedLow;

        long Total(SKSurface s) => Enumerable.Range(0, 3).Sum(i => Stats(s, ColorOf(i)).Count);

        ((double)Total(bigger) / Total(small)).Should().BeApproximately(2d, 0.2);
        ((double)Total(clampedLow) / Total(small)).Should().BeApproximately(0.5d, 0.1, "0 is clamped up to the 0.05 minimum");
    }

    [Fact]
    public void MaxBubbleRadius_CapsTheLargestBubbleAndKeepsProportions() {
        var (_, _, surface) = FirstFrame(Create(Named(("A", 4), ("B", 2), ("C", 1)), With(Plain("A", "B", "C"), "MaxBubbleRadius", 50f)));
        using var _ = surface;

        var radii = Enumerable.Range(0, 3).Select(i => Stats(surface, ColorOf(i)).EquivalentRadius).ToArray();

        radii[0].Should().BeApproximately(50f, 1.5f);
        (radii[0] / radii[1]).Should().BeApproximately(MathF.Sqrt(2f), 0.06f);
        (radii[0] / radii[2]).Should().BeApproximately(2f, 0.1f);
    }

    [Fact]
    public void MinBubbleRadius_LiftsAllBubblesWhenTheLargestStillFits() {
        var attributes = With(With(With(Plain("A", "B"), "MinBubbleRadius", 40f), "MaxBubbleRadius", 0f), "ConstrainToCanvas", true);
        var (_, _, surface) = FirstFrame(Create(Named(("A", 10), ("B", 1)), attributes));
        using var _ = surface;

        Stats(surface, ColorOf(1)).EquivalentRadius.Should().BeApproximately(40f, 2f);
        Stats(surface, ColorOf(0)).EquivalentRadius.Should().BeApproximately(126.5f, 3f);
    }

    [Fact]
    public void MinBubbleRadius_IsIgnoredWhenLiftingWouldExceedTheMaximum() {
        var (_, _, surface) = FirstFrame(Create(Named(("A", 100), ("B", 1)), With(Plain("A", "B"), "MinBubbleRadius", 40f)));
        using var _ = surface;

        Stats(surface, ColorOf(1)).EquivalentRadius.Should().BeLessThan(20f);
    }

    [Fact]
    public void InitialPlacement_NeverOverlapsAndKeepsTheConfiguredSpacing() {
        var (_, _, surface) = FirstFrame(Create(Named(("A", 8), ("B", 7), ("C", 6), ("D", 5), ("E", 4), ("F", 3)), With(Plain("A", "B", "C", "D", "E", "F"), "BubbleSpacing", 6f)));
        using var _ = surface;
        var bubbles = Bubbles(surface, 6);

        for (var i = 0; i < bubbles.Length; i++) {
            for (var j = i + 1; j < bubbles.Length; j++) {
                Gap(bubbles[i], bubbles[j]).Should().BeGreaterThan(4f, $"bubbles {i} and {j}");
            }
        }
    }

    [Fact]
    public void NonPositiveValues_AreExcluded() {
        var (series, _, surface) = FirstFrame(Create(Named(("A", 3), ("B", 0), ("C", -2), ("D", 1))));
        using var _ = surface;

        Stats(surface, ColorOf(1)).Count.Should().Be(0);
        Stats(surface, ColorOf(2)).Count.Should().Be(0);
        Stats(surface, ColorOf(0)).Count.Should().BeGreaterThan(0);
        Stats(surface, ColorOf(3)).Count.Should().BeGreaterThan(0);
        series.HitTest(Stats(surface, ColorOf(0)).Centroid, Area)!.Value.Data!.Name.Should().Be("A");
    }

    [Theory]
    [InlineData(new double[0])]
    [InlineData(new[] { 0d, 0d })]
    [InlineData(new[] { -1d })]
    public void NoPositiveValues_DrawsNothingAndNothingIsHit(double[] values) {
        var (series, _, surface) = FirstFrame(Create(values.Select((v, i) => new Item($"I{i}", (decimal)v)).ToArray()));
        using var _ = surface;

        CountNonBackground(surface).Should().Be(0);
        series.HitTest(Center, Area).Should().BeNull();
        RequiresAnimationFrame(series).Should().BeFalse();
    }

    [Fact]
    public void DegenerateArea_DoesNotThrowAndDrawsNothing() {
        var (series, chart, surface) = Create(Named(("A", 1), ("B", 2)));
        using var _ = surface;
        var context = CreateContext(chart, surface);

        var act = () => series.Render(context, new SKRect(0, 0, 0, 100));

        act.Should().NotThrow();
        CountNonBackground(surface).Should().Be(0);
    }

    [Fact]
    public void NavigationBar_ReservesSpaceAndShiftsTheBubbleCenterDown() {
        var (_, _, surface) = FirstFrame(Create(Named(("A", 5)), With(With(Plain("A"), "ShowNavigation", true), "NavigationHeight", 40f)));
        using var _ = surface;

        Stats(surface, ColorOf(0)).Centroid.Y.Should().BeApproximately(320f, 1.5f, "content area is (200,190)-(600,450)");
    }

    [Fact]
    public void Settling_ProducesANonOverlappingPackingAndStopsRequestingFrames() {
        var (series, chart, surface) = Create(Named(("A", 8), ("B", 7), ("C", 6), ("D", 5), ("E", 4), ("F", 3)));
        using var _ = surface;
        Frame(series, chart, surface);

        var frames = Settle(series, chart, surface);

        frames.Should().BeLessThan(1500, "physics must settle");
        RequiresAnimationFrame(series).Should().BeFalse();
        var bubbles = Bubbles(surface, 6);
        for (var i = 0; i < bubbles.Length; i++) {
            for (var j = i + 1; j < bubbles.Length; j++) {
                Gap(bubbles[i], bubbles[j]).Should().BeGreaterThan(-1.5f, $"bubbles {i} and {j} must not overlap after settling");
            }
        }
    }

    [Fact]
    public void Gravity_PullsTheClusterTowardTheAreaCenter() {
        var (series, chart, surface) = Create(Named(("A", 8), ("B", 7), ("C", 6), ("D", 5), ("E", 4), ("F", 3)));
        using var _ = surface;
        Frame(series, chart, surface);
        Settle(series, chart, surface);
        var bubbles = Bubbles(surface, 6);

        var cx = bubbles.Sum(b => b.Center.X * b.Radius * b.Radius) / bubbles.Sum(b => b.Radius * b.Radius);
        var cy = bubbles.Sum(b => b.Center.Y * b.Radius * b.Radius) / bubbles.Sum(b => b.Radius * b.Radius);

        cx.Should().BeApproximately(400f, 25f);
        cy.Should().BeApproximately(300f, 25f);
    }

    [Fact]
    public void ZeroGravity_LeavesNonOverlappingBubblesWhereTheyWereFirstPlaced() {
        var (series, chart, surface) = Create(Named(("A", 8), ("B", 7), ("C", 6)), With(Plain("A", "B", "C"), "GravityStrength", 0f));
        using var _ = surface;
        Frame(series, chart, surface);
        var before = Bubbles(surface, 3);

        Settle(series, chart, surface);
        var after = Bubbles(surface, 3);

        for (var i = 0; i < 3; i++) {
            after[i].Center.X.Should().BeApproximately(before[i].Center.X, 1.5f);
            after[i].Center.Y.Should().BeApproximately(before[i].Center.Y, 1.5f);
        }
    }

    [Fact]
    public void ConstrainToCanvas_KeepsEveryBubbleInsideTheContentArea() {
        var data = Enumerable.Range(0, 6).Select(i => new Item(((char)('A' + i)).ToString(), 6 - i)).ToArray();
        var (series, chart, surface) = Create(data, With(Plain("A", "B", "C", "D", "E", "F"), "ConstrainToCanvas", true));
        using var _ = surface;
        Frame(series, chart, surface);

        Settle(series, chart, surface);

        for (var i = 0; i < 6; i++) {
            var bounds = Stats(surface, ColorOf(i)).Bounds;
            bounds.Left.Should().BeGreaterThanOrEqualTo(198.5f);
            bounds.Top.Should().BeGreaterThanOrEqualTo(148.5f);
            bounds.Right.Should().BeLessThanOrEqualTo(601.5f);
            bounds.Bottom.Should().BeLessThanOrEqualTo(451.5f);
        }
    }

    [Fact]
    public void ManyBubbles_UsingTheBroadPhaseStillEndNonOverlapping() {
        var data = Enumerable.Range(0, 40).Select(i => new Item($"N{i}", 1 + (i % 5))).ToArray();
        var (series, chart, surface) = Create(data, With(Plain(), "ColorSelector", (Func<BubbleColorContext<Item>, NTColor>)(context => PaletteNtColors[int.Parse(context.Key[1..]) % 6])));
        using var _ = surface;
        Frame(series, chart, surface);

        Settle(series, chart, surface);

        RequiresAnimationFrame(series).Should().BeFalse();
        var hits = new HashSet<int>();
        for (var y = 150; y < 450; y += 4) {
            for (var x = 200; x < 600; x += 4) {
                if (series.HitTest(new SKPoint(x, y), Area) is { } hit) {
                    hits.Add(hit.Index);
                }
            }
        }
        hits.Count.Should().BeGreaterThan(20);
    }

    [Fact]
    public void ChangedRenderArea_ScalesBubblesAndRepacks() {
        var (series, chart, surface) = Create(Named(("A", 4), ("B", 2), ("C", 1)));
        using var _ = surface;
        Frame(series, chart, surface);
        var small = Stats(surface, ColorOf(0)).EquivalentRadius;

        var bigSurface = NewField();
        using var __ = bigSurface;
        Frame(series, chart, bigSurface, new SKRect(0, 0, 800, 600));

        Stats(bigSurface, ColorOf(0)).EquivalentRadius.Should().BeGreaterThan(small * 1.3f);
        RequiresAnimationFrame(series).Should().BeTrue("a changed area wakes the physics");
    }

    [Fact]
    public void ExplicitInvalidate_RestartsPackingAfterItHadSettled() {
        var data = new List<Item> { new("A", 4), new("B", 2), new("C", 1) };
        var (series, chart, surface) = Create(data.ToArray());
        using var _ = surface;
        Frame(series, chart, surface);
        Settle(series, chart, surface);
        RequiresAnimationFrame(series).Should().BeFalse();

        series.Invalidate();
        Frame(series, chart, surface);

        RequiresAnimationFrame(series).Should().BeTrue();
    }

    [Fact]
    public void SameData_InTwoCharts_GivesTheSameFirstFramePositions() {
        var data = Named(("A", 8), ("B", 7), ("C", 6), ("D", 5));
        var first = FirstFrame(Create(data));
        var second = FirstFrame(Create(data));
        using var _ = first.Surface;
        using var __ = second.Surface;

        var firstBubbles = Bubbles(first.Surface, 4);
        var secondBubbles = Bubbles(second.Surface, 4);

        for (var i = 0; i < 4; i++) {
            secondBubbles[i].Center.Should().Be(firstBubbles[i].Center);
        }
    }

    [Fact]
    public void ManySmallBubbles_WithLabels_PaintWithoutThrowing() {
        var data = Enumerable.Range(0, 300).Select(i => new Item($"Item{i}", 1 + (i % 7))).ToArray();
        var cut = RenderChart<NTBubblePackSeries<Item>>(data, new() { ["ShowLabels"] = true });
        using var surface = NewSurface(1200, 600);

        var act = () => Frame(cut.FindComponent<NTBubblePackSeries<Item>>().Instance, cut.Instance, surface, new SKRect(0, 0, 1200, 600));

        act.Should().NotThrow();
    }
}
