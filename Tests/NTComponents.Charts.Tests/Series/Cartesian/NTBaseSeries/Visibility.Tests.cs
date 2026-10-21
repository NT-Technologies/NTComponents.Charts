using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class Visibility_Tests : CartesianChartTestBase {
    [Fact]
    public void ByDefault_SeriesIsFullyVisible() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.Visible.Should().BeTrue();
        series.VisibilityFactor.Should().Be(1f);
        series.IsEffectivelyVisible.Should().BeTrue();
    }

    [Fact]
    public void WhenHiddenWithoutAnimation_FactorIsZeroAndSeriesIsNotEffectivelyVisible() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("Visible", false)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.VisibilityFactor.Should().Be(0f);
        series.IsEffectivelyVisible.Should().BeFalse();
    }

    [Fact]
    public void WhenHiddenWithAnimation_StaysEffectivelyVisibleWhileFadingOut() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("AnimationEnabled", true), ("AnimationDuration", NTMotionDuration.Ms1000)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        ReRender(cut, new ChartSetup(), SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("AnimationEnabled", true), ("AnimationDuration", NTMotionDuration.Ms1000), ("Visible", false)));

        series.Visible.Should().BeFalse();
        series.IsEffectivelyVisible.Should().BeTrue();
        series.VisibilityFactor.Should().BeInRange(0.01f, 1f);
    }

    [Fact]
    public void WhenToggledThroughParameters_RaisesVisibilityChangedWithNewState() {
        var changes = new List<NTSeriesVisibilityChangedEventArgs<Pt>>();
        var callback = Microsoft.AspNetCore.Components.EventCallback.Factory.Create<NTSeriesVisibilityChangedEventArgs<Pt>>(this, changes.Add);
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("OnVisibilityChanged", callback)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        changes.Should().BeEmpty();

        ReRender(cut, new ChartSetup(), SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("OnVisibilityChanged", callback), ("Visible", false)));
        cut.WaitForAssertion(() => changes.Should().ContainSingle());
        ReRender(cut, new ChartSetup(), SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("OnVisibilityChanged", callback), ("Visible", true)));

        cut.WaitForAssertion(() => changes.Should().HaveCount(2));
        changes[0].Series.Should().BeSameAs(series);
        changes[0].Visible.Should().BeFalse();
        changes[1].Visible.Should().BeTrue();
    }

    [Fact]
    public void WhenReRenderedWithSameVisibility_DoesNotRaiseVisibilityChanged() {
        var changes = new List<NTSeriesVisibilityChangedEventArgs<Pt>>();
        var callback = Microsoft.AspNetCore.Components.EventCallback.Factory.Create<NTSeriesVisibilityChangedEventArgs<Pt>>(this, changes.Add);
        var data = Linear(3);
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("OnVisibilityChanged", callback)));

        ReRender(cut, new ChartSetup(), SeriesFragment<NTLineSeries<Pt>>(data, ("OnVisibilityChanged", callback)));

        changes.Should().BeEmpty();
    }

    [Fact]
    public void WhenHidden_DrawsNothing() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(5), ("Color", NTColor.Primary), ("Visible", false)));
        using var surface = Draw(cut, SeriesOf<NTLineSeries<Pt>>(cut));

        CountDrawnPixels(surface).Should().Be(0);
    }

    [Fact]
    public void WhenHiddenBarSeries_DrawsNothing() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTBarSeries<Pt>>(Linear(5), ("Color", NTColor.Primary), ("Visible", false)));
        using var surface = Draw(cut, SeriesOf<NTBarSeries<Pt>>(cut));

        CountDrawnPixels(surface).Should().Be(0);
    }

    [Fact]
    public void WhenShownAgain_DrawsTheSameImageAsAnAlwaysVisibleSeries() {
        var data = Linear(5);
        var always = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary)));
        using var expected = Draw(always, SeriesOf<NTLineSeries<Pt>>(always));
        var toggled = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), ("Visible", false)));
        ReRender(toggled, new ChartSetup(), SeriesFragment<NTLineSeries<Pt>>(data, ("Color", NTColor.Primary), ("Visible", true)));

        using var actual = Draw(toggled, SeriesOf<NTLineSeries<Pt>>(toggled));

        Snapshot(actual).Should().Equal(Snapshot(expected));
    }

    [Fact]
    public void LegendToggleOfUnsegmentedSeries_FlipsVisibility() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        ToggleSeriesLegend(series);
        series.Visible.Should().BeFalse();
        series.IsEffectivelyVisible.Should().BeFalse();

        ToggleSeriesLegend(series);
        series.Visible.Should().BeTrue();
    }

    [Fact]
    public void HoverFactor_WithNothingHovered_IsOne() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3)));

        SeriesOf<NTLineSeries<Pt>>(cut).HoverFactor.Should().Be(1f);
    }

    [Fact]
    public void HoverFactor_WhenAnotherSeriesIsHovered_DimsToTheDeEmphasisOpacity() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3)), SeriesFragment<NTLineSeries<Pt>>(Linear(3)));
        var first = SeriesOf<NTLineSeries<Pt>>(cut, 0);
        var second = SeriesOf<NTLineSeries<Pt>>(cut, 1);

        SetHover(cut.Instance, second, 0);

        first.HoverFactor.Should().Be(NTChartTokens.DeEmphasisOpacity);
        second.HoverFactor.Should().Be(1f);
    }

    [Fact]
    public void HoverFactor_AfterHoverEnds_RestoresFullOpacity() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3)), SeriesFragment<NTLineSeries<Pt>>(Linear(3)));
        var first = SeriesOf<NTLineSeries<Pt>>(cut, 0);
        SetHover(cut.Instance, SeriesOf<NTLineSeries<Pt>>(cut, 1), 0);
        first.HoverFactor.Should().Be(NTChartTokens.DeEmphasisOpacity);

        SetHover(cut.Instance, null, null);

        first.HoverFactor.Should().Be(1f);
    }

    [Fact]
    public void DimmedSeries_DrawsALighterColorThanWhenNothingIsHovered() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(5, _ => 20m), ("Color", NTColor.Primary), ("StrokeWidth", 6f), ("PointStyle", PointStyle.None)), SeriesFragment<NTLineSeries<Pt>>(Linear(5, _ => 40m), ("Color", NTColor.Secondary)));
        var first = SeriesOf<NTLineSeries<Pt>>(cut, 0);
        var probe = ExpectedPoint(cut.Instance, 3, 20m);
        using var normal = Draw(cut, first);
        SetHover(cut.Instance, SeriesOf<NTLineSeries<Pt>>(cut, 1), 0);

        using var dimmed = Draw(cut, first);

        IsClose(PixelAt(normal, probe), Red).Should().BeTrue();
        var dimmedPixel = PixelAt(dimmed, probe);
        dimmedPixel.Green.Should().BeCloseTo((byte)(255 * (1f - NTChartTokens.DeEmphasisOpacity)), 4, "a 38% opaque red stroke over white is a washed-out pink");
    }
}
