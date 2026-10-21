using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Circular;

/// <summary>Cache invalidation, tooltips and disposal of the treemap.</summary>
public class TreeMapCaching_Tests : TreeMapTestBase {
    private (NTTreeMapSeries<Item> Series, IRenderedComponent<NTChart<Item>> Cut, SKSurface Surface) Render(IEnumerable<Item> data, Dictionary<string, object>? attributes = null) {
        var cut = RenderChart<NTTreeMapSeries<Item>>(data, attributes ?? Plain("A", "B", "C"));
        var series = cut.FindComponent<NTTreeMapSeries<Item>>().Instance;
        var surface = NewSurface();
        series.Render(CreateContext(cut.Instance, surface), Area);
        return (series, cut, surface);
    }

    private static void Repaint(NTTreeMapSeries<Item> series, NTChart<Item> chart, SKSurface surface) {
        surface.Canvas.Clear(SKColors.White);
        series.Render(CreateContext(chart, surface), Area);
    }

    [Fact]
    public void InPlaceMutation_ReshapesCellsOnlyAfterInvalidate() {
        var data = new List<Item> { new("A", 1), new("B", 1) };
        var (series, cut, surface) = Render(data);
        using var _ = surface;
        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(0, 0, 200, 300));

        data[0] = data[0] with { Value = 3 };
        Repaint(series, cut.Instance, surface);
        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(0, 0, 200, 300));

        series.Invalidate();
        Repaint(series, cut.Instance, surface);
        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(0, 0, 300, 300));
        AssertRect(Stats(surface, ColorOf(1)).Bounds, new SKRect(300, 0, 400, 300));
    }

    [Fact]
    public void ReplacedDataReference_IsReflectedOnTheNextRender() {
        var (series, cut, surface) = Render(Named(("A", 1), ("B", 1)));
        using var _ = surface;

        cut.Render(p => p.Add(c => c.ChildContent, SeriesFragment<NTTreeMapSeries<Item>>(Named(("A", 1), ("B", 1), ("C", 2)), Plain("A", "B", "C"))));
        Repaint(series, cut.Instance, surface);

        AssertRect(Stats(surface, ColorOf(2)).Bounds, new SKRect(0, 0, 200, 300));
    }

    [Fact]
    public void ChangingItemPadding_RelaysOutWithoutInvalidate() {
        var (series, cut, surface) = Render(Named(("A", 1), ("B", 1)));
        using var _ = surface;

        cut.Render(p => p.Add(c => c.ChildContent, SeriesFragment<NTTreeMapSeries<Item>>(Named(("A", 1), ("B", 1)), With(Plain("A", "B", "C"), "ItemPadding", 20f))));
        Repaint(series, cut.Instance, surface);

        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(20, 20, 180, 280));
    }

    [Fact]
    public void ChangingValueSelector_RebuildsTheHierarchy() {
        var (series, cut, surface) = Render(Named(("A", 1), ("B", 3)));
        using var _ = surface;
        AssertRect(Stats(surface, ColorOf(1)).Bounds, new SKRect(0, 0, 300, 300));

        cut.Render(p => p.Add(c => c.ChildContent, SeriesFragment<NTTreeMapSeries<Item>>(Named(("A", 1), ("B", 3)), With(Plain("A", "B", "C"), "ValueSelector", (Func<Item, decimal>)(item => item.Name == "A" ? 3 : 1)))));
        Repaint(series, cut.Instance, surface);

        AssertRect(Stats(surface, ColorOf(0)).Bounds, new SKRect(0, 0, 300, 300));
    }

    [Fact]
    public void RepeatedRendersOfUnchangedState_ProduceIdenticalPixels() {
        var (series, cut, surface) = Render(Named(("A", 6), ("B", 3), ("C", 2)));
        using var _ = surface;
        var first = Snapshot(surface);

        Repaint(series, cut.Instance, surface);

        Snapshot(surface).Should().Equal(first);
    }

    [Fact]
    public async Task HoveredLeaf_ProducesATooltipWithItsLabelFormattedValueAndCellColor() {
        var data = Named(("A", 1500), ("B", 500));
        var cut = RenderChart<NTTreeMapSeries<Item>>(data, With(Plain("A", "B"), "Title", "Revenue"));
        var series = cut.FindComponent<NTTreeMapSeries<Item>>().Instance;
        using var surface = NewSurface();
        await PaintAsync(cut, surface);

        await HoverAsync(cut, surface, 100, 150);
        var tooltip = Tooltip(series, data[0]);

        cut.Instance.HoveredPointIndex.Should().NotBeNull();
        tooltip.Header.Should().Be("A");
        tooltip.Lines.Single().Label.Should().Be("Revenue");
        tooltip.Lines.Single().Value.Should().Be(1500.ToString("N0"));
        tooltip.Lines.Single().Color.Should().Be(ColorOf(0));
    }

    [Fact]
    public async Task HoveredGroup_ReportsTheGroupNameAndSum() {
        var data = Grouped(("a", 3, "G1"), ("b", 1, "G1"), ("c", 2, "G2"));
        var attributes = With(With(Plain("G1", "G2"), "GroupSelector", (Func<Item, string?>)(item => item.Group)), "DataLabelFormat", "{0:0.0}");
        var cut = RenderChart(SeriesFragment<NTTreeMapSeries<Item>>(data, attributes, SeriesFragment<NTTreeMapSeries<Item>>(data, Plain())));
        var series = cut.FindComponent<NTTreeMapSeries<Item>>().Instance;
        using var surface = NewSurface();
        await PaintAsync(cut, surface);

        await HoverAsync(cut, surface, 60, 15);
        var tooltip = Tooltip(series, data[0]);

        tooltip.Header.Should().Be("G1");
        tooltip.Lines.Single().Value.Should().Be(string.Format("{0:0.0}", 4m));
    }

    [Fact]
    public void WithoutHover_TooltipDescribesTheBoundItem() {
        var data = Named(("A", 7), ("B", 1));
        var (series, _, surface) = Render(data, With(Plain("A", "B"), "DataLabelFormat", "${0}"));
        using var _ = surface;

        var tooltip = Tooltip(series, data[0]);

        tooltip.Header.Should().Be("A");
        tooltip.Lines.Single().Value.Should().Be("$7");
        tooltip.Lines.Single().Label.Should().Be("Series");
    }

    [Fact]
    public void InvalidDataLabelFormat_FallsBackToThousandsFormat() {
        var data = Named(("A", 1234), ("B", 1));
        var (series, _, surface) = Render(data, With(Plain("A", "B"), "DataLabelFormat", "{0:Q9}"));
        using var _ = surface;

        Tooltip(series, data[0]).Lines.Single().Value.Should().Be(1234.ToString("N0"));
    }

    [Fact]
    public void CustomTooltipSelector_ReplacesTheDefaultPayload() {
        var data = Named(("A", 7), ("B", 1));
        var (series, _, surface) = Render(data, With(Plain("A", "B"), "TooltipInfoSelector", (Func<NTSeriesTooltipContext<Item>, TooltipInfo>)(_ => new TooltipInfo { Header = "custom" })));
        using var _ = surface;

        Tooltip(series, data[0]).Header.Should().Be("custom");
    }

    [Fact]
    public void Dispose_ReleasesNativeResourcesAndUnregistersFromTheChart() {
        var (series, cut, surface) = Render(Named(("A", 1), ("B", 1)), With(Plain("A", "B"), "ShowLabels", true));
        using var _ = surface;
        var resources = NativeResources(series);
        resources.Should().NotBeEmpty();

        series.Dispose();

        resources.Should().OnlyContain(r => r.Handle == IntPtr.Zero);
        cut.Instance.Series.Should().BeEmpty();
    }
}
