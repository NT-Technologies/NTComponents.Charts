using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class NTBarSeriesSegmentedBars_Tests : BarTestBase {
    private static readonly Pt[] Rows = [new("A", 0m), new("B", 0m)];

    private static (string, object) Selector(Func<Pt, IEnumerable<NTBarSegment>> selector) => ("SegmentSelector", selector);

    private static IEnumerable<NTBarSegment> RedBlue(Pt row) => row.X.ToString() == "A"
        ? [new NTBarSegment { Value = 10m, Label = "Low", CustomColor = SKColors.Red }, new NTBarSegment { Value = 30m, Label = "High", CustomColor = SKColors.Blue }]
        : [new NTBarSegment { Value = 20m, Label = "Low", CustomColor = SKColors.Red }];

    [Fact]
    public void Render_StacksSegmentsFromTheBottomWithHeightsProportionalToTheirValues() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Rows, Primary, Selector(RedBlue)));
        var rect = VerticalBar(cut.Instance, 0, 2, 40m);

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        var x = (int)rect.MidX;
        var red = ColumnCount(surface, x, Red);
        var blue = ColumnCount(surface, x, Blue);
        (red + blue).Should().BeInRange((int)rect.Height - 2, (int)rect.Height + 1);
        ((double)blue / red).Should().BeApproximately(3.0, 0.15);
        IsClose(PixelAt(surface, new SKPoint(rect.MidX, rect.Bottom - 2f)), Red).Should().BeTrue("the first segment sits at the bottom");
        IsClose(PixelAt(surface, new SKPoint(rect.MidX, rect.Top + 2f)), Blue).Should().BeTrue("the last segment is on top");
    }

    [Fact]
    public void Render_BarHeightIsTheSumOfItsSegmentsOnTheValueAxis() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Rows, Primary, Selector(RedBlue)));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);

        series.GetYRange().Should().Be((20m, 40m));
        ChartYRange(cut.Instance).Should().Be((0m, 40m));
    }

    [Fact]
    public void Render_ZeroValueSegmentsTakeNoSpace() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Rows, Primary, Selector(row => [
            new NTBarSegment { Value = 10m, CustomColor = SKColors.Red },
            new NTBarSegment { Value = 0m, CustomColor = SKColors.Lime },
            new NTBarSegment { Value = 10m, CustomColor = SKColors.Blue }])));
        var rect = VerticalBar(cut.Instance, 0, 2, 20m);

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        ColumnCount(surface, (int)rect.MidX, Green).Should().Be(0);
        ColumnCount(surface, (int)rect.MidX, Red).Should().BeCloseTo(ColumnCount(surface, (int)rect.MidX, Blue), 2);
    }

    [Fact]
    public void Render_SegmentColorsPreferCustomColorThenThemeColorThenSelector() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Rows, Primary,
            ("SegmentColorSelector", (Func<NTBarSegmentColorContext<Pt>, NTColor>)(_ => NTColor.Tertiary)),
            Selector(row => [
                new NTBarSegment { Value = 10m, CustomColor = SKColors.Blue, Color = NTColor.Primary },
                new NTBarSegment { Value = 10m, Color = NTColor.Primary },
                new NTBarSegment { Value = 10m }])));
        var rect = VerticalBar(cut.Instance, 0, 2, 30m);

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        var third = rect.Height / 3f;
        IsClose(PixelAt(surface, new SKPoint(rect.MidX, rect.Bottom - (third * 0.5f))), Blue).Should().BeTrue("custom color wins over the theme color");
        IsClose(PixelAt(surface, new SKPoint(rect.MidX, rect.Bottom - (third * 1.5f))), Red).Should().BeTrue("theme color wins over the selector");
        IsClose(PixelAt(surface, new SKPoint(rect.MidX, rect.Bottom - (third * 2.5f))), Green).Should().BeTrue("the selector colors otherwise uncolored segments");
    }

    [Fact]
    public void SegmentColorSelector_ReceivesTheDataSegmentAndIndices() {
        var contexts = new List<(int DataIndex, int SegmentIndex, string? Label, decimal Value)>();
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Rows, Primary,
            ("SegmentColorSelector", (Func<NTBarSegmentColorContext<Pt>, NTColor>)(c => {
                contexts.Add((c.DataIndex, c.SegmentIndex, c.SegmentLabel, c.SegmentValue));
                return NTColor.Primary;
            })),
            Selector(row => [new NTBarSegment { Value = 5m, Label = "first" }, new NTBarSegment { Value = 7m, Label = "second" }])));

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        contexts.Should().Contain((1, 1, "second", 7m));
        contexts.Should().Contain((0, 0, "first", 5m));
    }

    [Fact]
    public void Render_HorizontalSegmentsStackLeftToRightProportionally() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Rows, Primary, Horizontal, Selector(RedBlue)));
        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        var y = (int)cut.Instance.ScaleY(0, Area);

        var red = RowCount(surface, y, Red);
        var blue = RowCount(surface, y, Blue);

        ((double)blue / red).Should().BeApproximately(3.0, 0.15);
        series.HitTest(new SKPoint(cut.Instance.ScaleX(40, Area) - 3f, y), Area)!.Value.Index.Should().Be(0);
    }

    [Fact]
    public void HitTest_ReturnsTheBarForAnySegmentOfIt() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Rows, Primary, Selector(RedBlue)));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        var rect = VerticalBar(cut.Instance, 0, 2, 40m);
        using var surface = Draw(cut, series);

        series.HitTest(new SKPoint(rect.MidX, rect.Bottom - 2f), Area)!.Value.Index.Should().Be(0);
        series.HitTest(new SKPoint(rect.MidX, rect.Top + 2f), Area)!.Value.Index.Should().Be(0);
        series.HitTest(new SKPoint(rect.MidX, rect.Top - 3f), Area).Should().BeNull();
    }

    [Fact]
    public void Tooltip_OverAHoveredSegment_ListsThatSegmentFirstAndTheBarTotalSecond() {
        var data = Rows;
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary, ("Title", "Sales"), Selector(RedBlue)));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        var rect = VerticalBar(cut.Instance, 0, 2, 40m);
        using var surface = Draw(cut, series);
        series.HitTest(new SKPoint(rect.MidX, rect.Top + 2f), Area).Should().NotBeNull();
        SetHover(cut.Instance, series, 0);

        var info = Tooltip(series, data[0]);

        info.Header.Should().Be("A");
        info.Lines.Should().HaveCount(2);
        info.Lines[0].Label.Should().Be("High");
        info.Lines[0].Value.Should().Be("30");
        info.Lines[0].Color.Should().Be(Blue);
        info.Lines[1].Label.Should().Be("Sales");
        info.Lines[1].Value.Should().Be("40");
    }

    [Fact]
    public void Tooltip_WhenNoSegmentWasHit_ShowsOnlyTheTotal() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Rows, Primary, Selector(RedBlue)));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        SetHover(cut.Instance, series, 0);

        var info = Tooltip(series, Rows[0]);

        info.Lines.Should().ContainSingle();
        info.Lines[0].Label.Should().Be("Total");
        info.Lines[0].Value.Should().Be("40");
    }

    [Fact]
    public void Tooltip_WhenNotHovered_FallsBackToTheDefaultSeriesPayload() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Rows, Primary, Selector(RedBlue)));

        var info = Tooltip(SeriesOf<NTBarSeries<Pt>>(cut), Rows[0]);

        info.Header.Should().Be("A");
        info.Lines.Should().ContainSingle();
        info.Lines[0].Label.Should().Be("Series");
    }

    [Fact]
    public void Tooltip_SelectorReceivesTheHoveredSegmentDetails() {
        NTSeriesTooltipContext<Pt>? received = null;
        Func<NTSeriesTooltipContext<Pt>, TooltipInfo> selector = context => {
            received = context;
            return context.DefaultInfo;
        };
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Rows, Primary, Selector(RedBlue), ("TooltipInfoSelector", selector)));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        var rect = VerticalBar(cut.Instance, 0, 2, 40m);
        using var surface = Draw(cut, series);
        series.HitTest(new SKPoint(rect.MidX, rect.Bottom - 2f), Area);
        SetHover(cut.Instance, series, 0);

        Tooltip(series, Rows[0]);

        received.Should().NotBeNull();
        received!.SegmentLabel.Should().Be("Low");
        received.SegmentValue.Should().Be(10m);
        received.SegmentColor.Should().Be(SKColors.Red);
        received.PointIndex.Should().Be(0);
    }

    [Fact]
    public void Legend_ListsDistinctLabelledSegmentsSortedByLabelWithTheirColors() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Rows, Primary, Selector(RedBlue)));

        var entries = Legend(SeriesOf<NTBarSeries<Pt>>(cut));

        entries.Select(e => e.Label).Should().Equal("High", "Low");
        entries.Select(e => e.Color).Should().Equal(SKColors.Blue, SKColors.Red);
        entries.Should().OnlyContain(e => e.IsVisible && e.Key == e.Label);
    }

    [Fact]
    public void Legend_IgnoresUnlabelledAndZeroValueSegmentsAndMergesLabelsCaseInsensitively() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Rows, Primary, Selector(row => row.X.ToString() == "A"
            ? [new NTBarSegment { Value = 1m, Label = " Alpha " }, new NTBarSegment { Value = 2m }, new NTBarSegment { Value = 0m, Label = "Zero" }]
            : [new NTBarSegment { Value = 3m, Label = "alpha" }, new NTBarSegment { Value = 4m, Label = "Beta" }])));

        Legend(SeriesOf<NTBarSeries<Pt>>(cut)).Select(e => e.Label).Should().Equal("Alpha", "Beta");
    }

    [Fact]
    public void Legend_WithoutAnyLabelledSegment_FallsBackToTheSeriesEntry() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Rows, Primary, ("Title", "Revenue"), Selector(_ => [new NTBarSegment { Value = 5m }])));

        var entries = Legend(SeriesOf<NTBarSeries<Pt>>(cut));

        entries.Should().ContainSingle();
        entries[0].Label.Should().Be("Revenue");
    }

    [Fact]
    public void ToggleLegend_HidesThatSegmentRecomputesTotalsAndRestoresOnSecondToggle() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Rows, Primary, Selector(RedBlue)));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        series.GetYRange().Should().Be((20m, 40m));

        ToggleLegend(series, "High");

        series.GetYRange().Should().Be((10m, 20m), "the hidden segment no longer counts towards bar totals");
        Legend(series).Single(e => e.Label == "High").IsVisible.Should().BeFalse();
        Legend(series).Single(e => e.Label == "Low").IsVisible.Should().BeTrue();
        using (var surface = Draw(cut, series)) {
            ColumnCount(surface, (int)VerticalBar(cut.Instance, 0, 2, 10m).MidX, Blue).Should().Be(0);
        }

        ToggleLegend(series, "High");

        series.GetYRange().Should().Be((20m, 40m));
        Legend(series).Single(e => e.Label == "High").IsVisible.Should().BeTrue();
    }

    [Fact]
    public void ToggleLegend_DoesNotHideTheWholeSeries() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Rows, Primary, Selector(RedBlue)));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);

        ToggleLegend(series, "Low");

        series.Visible.Should().BeTrue();
    }
}
