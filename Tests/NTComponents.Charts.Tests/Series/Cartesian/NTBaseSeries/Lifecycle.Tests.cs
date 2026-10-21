using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class Lifecycle_Tests : CartesianChartTestBase {
    [Fact]
    public void OutsideAChart_ThrowsDescriptiveError() {
        var act = () => Render<NTLineSeries<Pt>>(parameters => parameters
            .Add(p => p.Data, Linear(3))
            .Add(p => p.XValue, XSelector)
            .Add(p => p.YValueSelector, YSelector));

        act.Should().Throw<ArgumentNullException>().WithMessage("*NTChart*");
    }

    [Fact]
    public void InsideAChart_RegistersWithTheChartInDeclarationOrder() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3)), SeriesFragment<NTBarSeries<Pt>>(Linear(3)));

        cut.Instance.Series.Should().HaveCount(2);
        cut.Instance.Series[0].Should().BeOfType<NTLineSeries<Pt>>();
        cut.Instance.Series[1].Should().BeOfType<NTBarSeries<Pt>>();
    }

    [Fact]
    public void WhenRemovedFromTheChart_UnregistersAndStopsContributingToRanges() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3)), SeriesFragment<NTLineSeries<Pt>>(Linear(3, i => 1000m * (i + 1)), ("UseSecondaryYAxis", false)));
        cut.Instance.Series.Should().HaveCount(2);
        ChartYRange(cut.Instance).Max.Should().Be(3000m);

        ReRender(cut, new ChartSetup(), SeriesFragment<NTLineSeries<Pt>>(Linear(3)));

        cut.Instance.Series.Should().HaveCount(1);
        ChartYRange(cut.Instance).Max.Should().Be(30m);
    }

    [Fact]
    public void WhenRemovedFromTheChart_CategoricalValuesNoLongerIncludeItsCategories() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTLineSeries<Pt>>([new Pt("A", 1m)]), SeriesFragment<NTLineSeries<Pt>>([new Pt("Z", 1m)]));
        cut.Instance.GetAllXValues().Should().Equal("A", "Z");

        ReRender(cut, new ChartSetup { XAxis = XAxisKind.Default }, SeriesFragment<NTLineSeries<Pt>>([new Pt("A", 1m)]));

        cut.Instance.GetAllXValues().Should().Equal("A");
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrowAndUnregistersOnce() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3)), SeriesFragment<NTLineSeries<Pt>>(Linear(3)));
        var first = SeriesOf<NTLineSeries<Pt>>(cut, 0);

        var act = () => {
            first.Dispose();
            first.Dispose();
        };

        act.Should().NotThrow();
        cut.Instance.Series.Should().NotContain(first);
        cut.Instance.Series.Should().HaveCount(1);
    }

    [Fact]
    public void Dispose_AfterRendering_ReleasesPaintsWithoutBreakingTheChart() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("ShowDataLabels", true), ("LineStyle", LineStyle.Dashed)), SeriesFragment<NTBarSeries<Pt>>(Linear(3), ("ShowDataLabels", true)));
        using (var surface = Draw(cut, cut.Instance.Series.ToArray())) {
            CountDrawnPixels(surface).Should().BeGreaterThan(0);
        }

        var act = () => cut.Instance.Series.ToList().ForEach(s => s.Dispose());

        act.Should().NotThrow();
        cut.Instance.Series.Should().BeEmpty();
        ChartYRange(cut.Instance).Should().Be((0m, 1m));
    }

    [Fact]
    public void ChangingData_ResetsTheAnimationAndRequestsFrames() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("AnimationEnabled", true), ("AnimationDuration", NTMotionDuration.Ms1000)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        RequiresAnimationFrame(series).Should().BeTrue();
    }

    [Fact]
    public void WithAnimationDisabled_NeverRequestsFrames() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3)));

        RequiresAnimationFrame(SeriesOf<NTLineSeries<Pt>>(cut)).Should().BeFalse();
    }

    [Fact]
    public void WithAnimationEnabledButNoData_DoesNotRequestFrames() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>([], ("AnimationEnabled", true), ("AnimationDuration", NTMotionDuration.Ms1000)));

        RequiresAnimationFrame(SeriesOf<NTLineSeries<Pt>>(cut)).Should().BeFalse();
    }

    [Fact]
    public void ResetAnimation_WithAnimationEnabled_RestartsDataAnimation() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("AnimationEnabled", true), ("AnimationDuration", NTMotionDuration.Ms1000)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.ResetAnimation();

        RequiresAnimationFrame(series).Should().BeTrue();
    }

    [Fact]
    public void TurningAnimationOff_StopsRequestingFrames() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("AnimationEnabled", true), ("AnimationDuration", NTMotionDuration.Ms1000)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        RequiresAnimationFrame(series).Should().BeTrue();
        var data = series.Data;

        ReRender(cut, new ChartSetup(), SeriesFragment<NTLineSeries<Pt>>(data, ("AnimationEnabled", false)));

        RequiresAnimationFrame(series).Should().BeFalse();
    }

    private static bool RequiresAnimationFrame(NTBaseSeries<Pt> series) => (bool)typeof(NTBaseSeries<Pt>)
        .GetProperty("RequiresAnimationFrame", NonPublic)!
        .GetValue(series)!;
}
