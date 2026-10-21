using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class Legend_Tests : CartesianChartTestBase {
    [Fact]
    public void UnsegmentedSeries_ProducesOneEntryNamedAfterTheTitle() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("Title", "Revenue"), ("Color", NTColor.Primary)));

        var entries = Legend(SeriesOf<NTLineSeries<Pt>>(cut));

        entries.Should().ContainSingle();
        entries[0].Label.Should().Be("Revenue");
        entries[0].Key.Should().Be("Revenue");
        entries[0].Color.Should().Be(Red);
        entries[0].IsVisible.Should().BeTrue();
        entries[0].Index.Should().BeNull();
    }

    [Fact]
    public void UntitledSeries_AreNumberedByTheirPositionInTheChart() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3)), SeriesFragment<NTBarSeries<Pt>>(Linear(3)), SeriesFragment<NTLineSeries<Pt>>(Linear(3)));

        Legend(cut.Instance.Series[0]).Single().Label.Should().Be("Series 1");
        Legend(cut.Instance.Series[1]).Single().Label.Should().Be("Series 2");
        Legend(cut.Instance.Series[2]).Single().Label.Should().Be("Series 3");
    }

    [Fact]
    public void HiddenSeries_ReportsNotVisible() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("Visible", false)));

        Legend(SeriesOf<NTLineSeries<Pt>>(cut)).Single().IsVisible.Should().BeFalse();
    }

    [Fact]
    public void SeriesWithoutExplicitColor_UsesTheChartPaletteInOrder() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3)), SeriesFragment<NTLineSeries<Pt>>(Linear(3)));
        var palette = cut.Instance.Palette;

        Legend(cut.Instance.Series[0]).Single().Color.Should().Be(cut.Instance.GetThemeColor(palette[0].Background));
        Legend(cut.Instance.Series[1]).Single().Color.Should().Be(cut.Instance.GetThemeColor(palette[1].Background));
    }

    [Fact]
    public void TogglingTheEntry_HidesThenShowsTheSeriesInChartRanges() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3)), SeriesFragment<NTLineSeries<Pt>>(Linear(3, i => 1000m * (i + 1))));
        var second = SeriesOf<NTLineSeries<Pt>>(cut, 1);
        ChartYRange(cut.Instance).Max.Should().Be(3000m);

        ToggleSeriesLegend(second);
        ChartYRange(cut.Instance).Max.Should().Be(30m);
        Legend(second).Single().IsVisible.Should().BeFalse();

        ToggleSeriesLegend(second);
        ChartYRange(cut.Instance).Max.Should().Be(3000m);
        Legend(second).Single().IsVisible.Should().BeTrue();
    }
}
