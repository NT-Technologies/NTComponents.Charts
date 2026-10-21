using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class NTBarSeriesHorizontalBars_Tests : BarTestBase {
    private static Pt[] Categories(params decimal[] values) => values.Select((v, i) => new Pt(((char)('A' + i)).ToString(), v)).ToArray();

    [Fact]
    public void Render_FillsBarsFromTheBaselineToTheValue() {
        var data = Categories(10m, 20m, 40m);
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary, Horizontal));

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        for (var i = 0; i < data.Length; i++) {
            var rect = HorizontalBar(cut.Instance, i, data.Length, data[i].Y);
            IsClose(PixelAt(surface, Center(rect)), Red).Should().BeTrue($"bar {i} is filled");
            IsClose(PixelAt(surface, new SKPoint(rect.Right - 2f, rect.MidY)), Red).Should().BeTrue($"bar {i} reaches its value");
            IsWhite(PixelAt(surface, new SKPoint(rect.Right + 2.5f, rect.MidY))).Should().BeTrue($"nothing right of bar {i}");
        }
    }

    [Fact]
    public void Render_BarLengthsAreProportionalToValues() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m, 40m), Primary, Horizontal));

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        var lengths = Enumerable.Range(0, 3).Select(i => (double)RowCount(surface, (int)HorizontalBar(cut.Instance, i, 3, 10m).MidY, Red)).ToArray();
        (lengths[1] / lengths[0]).Should().BeApproximately(2.0, 0.08);
        (lengths[2] / lengths[0]).Should().BeApproximately(4.0, 0.15);
    }

    [Fact]
    public void Render_NegativeValuesExtendLeftOfTheZeroBaseline() {
        var data = Categories(30m, -20m);
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary, Horizontal));
        var positive = HorizontalBar(cut.Instance, 0, 2, 30m);
        var negative = HorizontalBar(cut.Instance, 1, 2, -20m);

        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        negative.Right.Should().BeApproximately(positive.Left, 0.01f);
        IsClose(PixelAt(surface, Center(negative)), Red).Should().BeTrue();
        IsClose(PixelAt(surface, Center(positive)), Red).Should().BeTrue();
        IsWhite(PixelAt(surface, new SKPoint(negative.Right + 3f, negative.MidY))).Should().BeTrue();
    }

    [Fact]
    public void XRange_IsTheValueRangeAndYRangeIsTheCategoryIndexRange() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m, 40m), Primary, Horizontal));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);

        series.GetXRange().Should().Be((10d, 40d));
        series.GetYRange().Should().Be((0m, 2m));
    }

    [Fact]
    public void ChartXRange_StartsAtZeroForPositiveValuesAndOnlyPadsTheRight() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m, 40m), Primary, Horizontal));

        var (min, max) = ChartXRange(cut.Instance, padded: true);

        min.Should().Be(0d);
        max.Should().BeApproximately(42d, 1e-9);
    }

    [Fact]
    public void ChartXRange_ForNegativeValuesEndsAtZeroAndOnlyPadsTheLeft() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(-10m, -40m), Primary, Horizontal));

        var (min, max) = ChartXRange(cut.Instance, padded: true);

        max.Should().Be(-10d, "the data max is the plotted edge when every value is negative");
        min.Should().BeLessThan(-40d);
    }

    [Fact]
    public void ChartXRange_WithValueAxisMinimum_StartsAtThatMinimum() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m), Primary, Horizontal, ("ValueAxisMinimum", 4m)));

        ChartXRange(cut.Instance).Min.Should().Be(4d);
    }

    [Fact]
    public void ChartYRange_PadsAtLeastHalfACategoryOnEachSide() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m, 40m), Primary, Horizontal));

        ChartYRange(cut.Instance, padded: true).Should().Be((-0.5m, 2.5m));
    }

    [Fact]
    public void RegisteredValues_AreCategoriesOnYAndNothingOnX() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m), Primary, Horizontal));

        cut.Instance.GetAllYValues().Should().Equal("A", "B");
        cut.Instance.GetAllXValues().Should().BeEmpty();
    }

    [Fact]
    public void RegisteredValues_ForVerticalBarsAreCategoriesOnXAndValuesOnY() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m), Primary));

        cut.Instance.GetAllXValues().Should().Equal("A", "B");
        cut.Instance.GetAllYValues().Should().Equal(10m, 20m);
    }

    [Fact]
    public void HitTest_InsideBars_ReturnsTheirIndexAndMissesOutside() {
        var data = Categories(10m, 20m, 40m);
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary, Horizontal));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        using var surface = Draw(cut, series);

        for (var i = 0; i < data.Length; i++) {
            var rect = HorizontalBar(cut.Instance, i, data.Length, data[i].Y);
            series.HitTest(Center(rect), Area)!.Value.Index.Should().Be(i);
            series.HitTest(new SKPoint(rect.Right + 4f, rect.MidY), Area).Should().BeNull();
        }
    }

    [Fact]
    public void Render_ChangingOrientationAfterFirstRender_RedrawsInTheNewOrientation() {
        var data = Categories(10m, 20m);
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        using (Draw(cut, series)) {
        }

        series.Orientation = NTChartOrientation.Horizontal;

        using var surface = Draw(cut, series);
        var rect = HorizontalBar(cut.Instance, 1, 2, 20m);
        IsClose(PixelAt(surface, Center(rect)), Red).Should().BeTrue();
        series.HitTest(Center(rect), Area)!.Value.Index.Should().Be(1);
    }
}
