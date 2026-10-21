using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Hover/tooltip output, visibility, entry animation, cache invalidation and disposal of the bubble pack.</summary>
public class BubbleLifecycle_Tests : BubblePackTestBase {
    private static readonly Item[] Data = Named(("A", 4), ("B", 2), ("C", 1));

    private (T Series, IRenderedComponent<NTChart<Item>> Cut, SKSurface Surface) Create<T>(Dictionary<string, object>? attributes = null, Item[]? data = null) where T : NTBubblePackSeries<Item> {
        var cut = RenderChart<T>(data ?? Data, attributes ?? Plain("A", "B", "C"));
        var series = cut.FindComponent<T>().Instance;
        var surface = NewField();
        Frame(series, cut.Instance, surface);
        return (series, cut, surface);
    }

    [Fact]
    public async Task HoveringABubble_ReportsItsDataAndDimsTheOthers() {
        var cut = RenderChart<NTBubblePackSeries<Item>>(Data, Plain("A", "B", "C"));
        using var surface = NewSurface();
        await PaintAsync(cut, surface);
        var a = Stats(surface, ColorOf(0)).Centroid;
        var bBefore = Stats(surface, ColorOf(1));

        await HoverAsync(cut, surface, a.X, a.Y);

        cut.Instance.HoveredDataPoint.Should().BeSameAs(Data[0]);
        cut.Instance.HoveredPointIndex.Should().NotBeNull();
        var dimmedCenter = PixelAt(surface, bBefore.Centroid.X, bBefore.Centroid.Y);
        // 38% (the Material 3 de-emphasis opacity) green over white.
        var expected = (byte)(255 * (1f - NTChartTokens.DeEmphasisOpacity));
        dimmedCenter.Red.Should().BeInRange((byte)(expected - 4), (byte)(expected + 4));
        dimmedCenter.Green.Should().BeGreaterThan(250);
        dimmedCenter.Blue.Should().BeInRange((byte)(expected - 4), (byte)(expected + 4));
        IsStateLayered(PixelAt(surface, a.X, a.Y), ColorOf(0)).Should().BeTrue("the hovered bubble keeps full opacity under its state layer");
    }

    [Fact]
    public async Task HoveredBubbleTooltip_UsesTheNodeLabelFormattedValueAndBubbleColor() {
        var cut = RenderChart<NTBubblePackSeries<Item>>(Data, With(Plain("A", "B", "C"), "Title", "Pack"));
        var series = cut.FindComponent<NTBubblePackSeries<Item>>().Instance;
        using var surface = NewSurface();
        await PaintAsync(cut, surface);
        var b = Stats(surface, ColorOf(1)).Centroid;

        await HoverAsync(cut, surface, b.X, b.Y);
        var tooltip = Tooltip(series, Data[1]);

        tooltip.Header.Should().Be("B");
        tooltip.Lines.Single().Label.Should().Be("Pack");
        tooltip.Lines.Single().Value.Should().Be(2.ToString("N0"));
        tooltip.Lines.Single().Color.Should().Be(ColorOf(1));
    }

    [Fact]
    public void WithoutHover_TooltipDescribesTheBoundItemUsingTheDataLabelFormat() {
        var (series, _, surface) = Create<NTBubblePackSeries<Item>>(With(Plain("A", "B", "C"), "DataLabelFormat", "{0:0.0}u"));
        using var _ = surface;

        var tooltip = Tooltip(series, Data[0]);

        tooltip.Header.Should().Be("A");
        tooltip.Lines.Single().Value.Should().Be(string.Format("{0:0.0}u", 4m));
        tooltip.Lines.Single().Label.Should().Be("Series");
    }

    [Fact]
    public void CustomTooltipSelector_ReplacesTheDefaultPayload() {
        var (series, _, surface) = Create<NTBubblePackSeries<Item>>(With(Plain("A", "B", "C"), "TooltipInfoSelector", (Func<NTSeriesTooltipContext<Item>, TooltipInfo>)(_ => new TooltipInfo { Header = "custom" })));
        using var _ = surface;

        Tooltip(series, Data[0]).Header.Should().Be("custom");
    }

    [Fact]
    public void Legend_HasASingleEntryAndTogglingItHidesTheSeries() {
        var (series, cut, surface) = Create<NTBubblePackSeries<Item>>(With(Plain("A", "B", "C"), "Title", "Pack"));
        using var _ = surface;

        var entries = LegendItems(series);
        ToggleLegend(series, null);
        Frame(series, cut.Instance, surface);

        entries.Should().ContainSingle();
        entries[0].Label.Should().Be("Pack");
        entries[0].Index.Should().BeNull();
        series.Visible.Should().BeFalse();
        CountNonBackground(surface).Should().Be(0);
        RequiresAnimationFrame(series).Should().BeFalse();
    }

    [Fact]
    public void LegendWithoutTitle_UsesBubblePackLabel() {
        var (series, _, surface) = Create<NTBubblePackSeries<Item>>();
        using var _ = surface;

        LegendItems(series)[0].Label.Should().Be("Bubble Pack");
    }

    [Fact]
    public void EntryAnimation_StartsWithSmallBubblesAndGrowsToFullSize() {
        var (series, cut, surface) = Create<ClockedBubblePack>(With(Plain("A", "B", "C"), "AnimationEnabled", true));
        using var _ = surface;

        float RadiusAt(TimeSpan age) {
            series.SetAnimationAge(age);
            Frame(series, cut.Instance, surface);
            return Stats(surface, ColorOf(0)).EquivalentRadius;
        }

        var start = RadiusAt(TimeSpan.Zero);
        var mid = RadiusAt(TimeSpan.FromMilliseconds(150));
        var full = RadiusAt(TimeSpan.FromSeconds(10));

        (start / full).Should().BeApproximately(0.65f, 0.05f, "bubbles start at 65% of their target radius");
        mid.Should().BeGreaterThan(start).And.BeLessThan(full);
        RequiresAnimationFrame(series).Should().BeTrue("radii were still changing, so physics is awake");
    }

    [Fact]
    public void DisabledAnimation_UsesFullRadiiOnTheFirstFrame() {
        var (_, _, surface) = Create<ClockedBubblePack>(With(Plain("A", "B", "C"), "AnimationEnabled", false));
        using var _ = surface;

        Stats(surface, ColorOf(0)).EquivalentRadius.Should().BeGreaterThan(70f);
    }

    [Fact]
    public void InPlaceMutation_ResizesBubblesOnlyAfterInvalidate() {
        var data = new List<Item> { new("A", 1), new("B", 1) };
        var cut = RenderChart<NTBubblePackSeries<Item>>(data, Plain("A", "B"));
        var series = cut.FindComponent<NTBubblePackSeries<Item>>().Instance;
        using var surface = NewField();
        Frame(series, cut.Instance, surface);
        var before = Stats(surface, ColorOf(0)).Count;

        data[0] = data[0] with { Value = 3 };
        Frame(series, cut.Instance, surface);
        Stats(surface, ColorOf(0)).Count.Should().BeInRange((long)(before * 0.95), (long)(before * 1.05));

        series.Invalidate();
        Frame(series, cut.Instance, surface);
        Frame(series, cut.Instance, surface);

        ((double)Stats(surface, ColorOf(0)).Count / Stats(surface, ColorOf(1)).Count).Should().BeApproximately(3d, 0.5);
    }

    [Fact]
    public void ReplacedData_ShowsOnlyTheNewBubbles() {
        var (series, cut, surface) = Create<NTBubblePackSeries<Item>>();
        using var _ = surface;

        cut.Render(p => p.Add(c => c.ChildContent, SeriesFragment<NTBubblePackSeries<Item>>(Named(("A", 1)), Plain("A", "B", "C"))));
        Frame(series, cut.Instance, surface);

        Stats(surface, ColorOf(0)).Count.Should().BeGreaterThan(0);
        Stats(surface, ColorOf(1)).Count.Should().Be(0);
        Stats(surface, ColorOf(2)).Count.Should().Be(0);
    }

    [Fact]
    public void ConfigurationChange_RebuildsAndRestartsPhysics() {
        var (series, cut, surface) = Create<NTBubblePackSeries<Item>>();
        using var _ = surface;
        Settle(series, cut.Instance, surface);
        var settledRadius = Stats(surface, ColorOf(0)).EquivalentRadius;

        cut.Render(p => p.Add(c => c.ChildContent, SeriesFragment<NTBubblePackSeries<Item>>(Data, With(Plain("A", "B", "C"), "TargetFillRatio", 0.16f))));
        Frame(series, cut.Instance, surface);

        Stats(surface, ColorOf(0)).EquivalentRadius.Should().BeLessThan(settledRadius * 0.85f);
        RequiresAnimationFrame(series).Should().BeTrue();
    }

    [Fact]
    public void Dispose_ReleasesNativeResourcesAndUnregistersFromTheChart() {
        var (series, cut, surface) = Create<NTBubblePackSeries<Item>>(With(Plain("A", "B", "C"), "ShowLabels", true));
        using var _ = surface;
        var resources = NativeResources(series);
        resources.Should().NotBeEmpty();

        series.Dispose();

        resources.Should().OnlyContain(r => r.Handle == IntPtr.Zero);
        cut.Instance.Series.Should().BeEmpty();
    }

    [Fact]
    public void Labels_AreDrawnInsideTheLargestBubble() {
        var (_, _, surface) = Create<NTBubblePackSeries<Item>>(With(With(Plain("A", "B", "C"), "ShowLabels", true), "ShowValuesInLabels", true));
        using var _ = surface;
        var a = Stats(surface, ColorOf(0));

        AnyPixel(surface, new SKRect(a.Centroid.X - 20, a.Centroid.Y - 20, a.Centroid.X + 20, a.Centroid.Y + 20), c => !IsNear(c, ColorOf(0), 6)).Should().BeTrue("the largest bubble shows its label");
    }
}
