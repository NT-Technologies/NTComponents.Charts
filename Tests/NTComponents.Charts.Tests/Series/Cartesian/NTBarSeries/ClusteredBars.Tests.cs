using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class NTBarSeriesClusteredBars_Tests : BarTestBase {
    private static Pt[] Categories(params decimal[] values) => values.Select((v, i) => new Pt(((char)('A' + i)).ToString(), v)).ToArray();

    [Fact]
    public void TwoSeries_ShareTheClusterSideBySideWithoutOverlap() {
        var first = Categories(10m, 20m);
        var second = Categories(15m, 25m);
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(first, Primary), SeriesFragment<NTBarSeries<Pt>>(second, Secondary));
        var a = SeriesOf<NTBarSeries<Pt>>(cut, 0);
        var b = SeriesOf<NTBarSeries<Pt>>(cut, 1);
        var rectA = VerticalBar(cut.Instance, 0, 2, 10m, seriesIndex: 0, seriesCount: 2);
        var rectB = VerticalBar(cut.Instance, 0, 2, 15m, seriesIndex: 1, seriesCount: 2);

        using var surface = Draw(cut, a, b);

        rectA.Right.Should().BeLessThan(rectB.Left, "bars of different series do not overlap");
        IsClose(PixelAt(surface, Center(rectA)), Red).Should().BeTrue();
        IsClose(PixelAt(surface, Center(rectB)), Blue).Should().BeTrue();
        a.HitTest(Center(rectA), Area)!.Value.Index.Should().Be(0);
        a.HitTest(Center(rectB), Area).Should().BeNull();
        b.HitTest(Center(rectB), Area)!.Value.Index.Should().Be(0);
    }

    [Fact]
    public void ThreeSeries_SplitTheClusterIntoThreeEqualBars() {
        var cut = RenderChart(
            XAxisKind.Default,
            SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m), Primary),
            SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m), Secondary),
            SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m), ("Color", NTColor.Tertiary)));
        var series = new[] { SeriesOf<NTBarSeries<Pt>>(cut, 0), SeriesOf<NTBarSeries<Pt>>(cut, 1), SeriesOf<NTBarSeries<Pt>>(cut, 2) };
        var colors = new[] { Red, Blue, Green };

        using var surface = Draw(cut, series);

        for (var i = 0; i < 3; i++) {
            var rect = VerticalBar(cut.Instance, 1, 2, 20m, seriesIndex: i, seriesCount: 3);
            IsClose(PixelAt(surface, Center(rect)), colors[i]).Should().BeTrue($"series {i}");
            series[i].HitTest(Center(rect), Area)!.Value.Index.Should().Be(1);
        }
    }

    [Fact]
    public void HidingOneSeries_LetsTheRemainingSeriesTakeTheFullCluster() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m), Primary), SeriesFragment<NTBarSeries<Pt>>(Categories(15m, 25m), Secondary));
        var second = SeriesOf<NTBarSeries<Pt>>(cut, 1);
        using (Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut, 0), second)) {
        }

        ReRender(cut, new ChartSetup { XAxis = XAxisKind.Default }, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m), Primary), SeriesFragment<NTBarSeries<Pt>>(Categories(15m, 25m), Secondary, ("Visible", false)));

        var first = SeriesOf<NTBarSeries<Pt>>(cut, 0);
        using var surface = Draw(cut, first);
        var full = VerticalBar(cut.Instance, 0, 2, 10m, seriesIndex: 0, seriesCount: 1);
        IsClose(PixelAt(surface, new SKPoint(full.Right - 3f, full.MidY)), Red).Should().BeTrue("the full-width bar now reaches where the second series used to be");
        first.HitTest(new SKPoint(full.Right - 3f, full.MidY), Area)!.Value.Index.Should().Be(0);
    }

    [Fact]
    public void MixingHorizontalAndVerticalBars_IsRejected() {
        var act = () => RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Categories(10m, 20m), Primary), SeriesFragment<NTBarSeries<Pt>>(Categories(15m, 25m), Secondary, Horizontal));

        act.Should().Throw<InvalidOperationException>().WithMessage("*horizontal*");
    }

    [Fact]
    public void AddingASecondSeries_NarrowsTheFirstSeriesBars() {
        var data = Categories(10m, 20m);
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, Primary));
        var first = SeriesOf<NTBarSeries<Pt>>(cut);
        using (Draw(cut, first)) {
        }
        var wide = VerticalBar(cut.Instance, 0, 2, 10m);
        var rightEdgeProbe = new SKPoint(wide.Right - 3f, wide.MidY);
        first.HitTest(rightEdgeProbe, Area).Should().NotBeNull();

        ReRender(cut, new ChartSetup { XAxis = XAxisKind.Default }, SeriesFragment<NTBarSeries<Pt>>(data, Primary), SeriesFragment<NTBarSeries<Pt>>(data, Secondary));
        using (Draw(cut, first)) {
        }

        first.HitTest(rightEdgeProbe, Area).Should().BeNull("the first series now only owns the left half of the cluster");
    }
}
