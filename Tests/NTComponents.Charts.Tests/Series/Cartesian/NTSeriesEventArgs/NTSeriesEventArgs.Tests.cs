using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using NTComponents.Charts.Core.Axes;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class NTSeriesEventArgs_Tests : CartesianChartTestBase {
    private static EventCallback<T> Callback<T>(object receiver, Action<T> handler) => EventCallback.Factory.Create(receiver, handler);

    private static void Click(IRenderedComponent<NTChart<Pt>> cut, SKPoint point) =>
        cut.Find(".nt-chart-canvas-host").Click(new MouseEventArgs { OffsetX = point.X, OffsetY = point.Y });

    [Fact]
    public void Click_OnADataPoint_RaisesOnClickWithPointIndexDataAndPosition() {
        var clicks = new List<NTSeriesClickEventArgs<Pt>>();
        var data = Linear(3);
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("OnClick", Callback<NTSeriesClickEventArgs<Pt>>(this, clicks.Add))));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        SetPlotArea(cut.Instance, Area);
        var target = ExpectedPoint(cut.Instance, 2, 20m);

        Click(cut, target);

        cut.WaitForAssertion(() => clicks.Should().ContainSingle());
        clicks[0].Series.Should().BeSameAs(series);
        clicks[0].PointIndex.Should().Be(1);
        clicks[0].DataPoint.Should().BeSameAs(data[1]);
        clicks[0].PointerPosition.Should().Be(target);
        clicks[0].MouseEvent.OffsetX.Should().Be(target.X);
    }

    [Fact]
    public void Click_OnBarSeries_ReportsTheClickedBar() {
        var clicks = new List<NTSeriesClickEventArgs<Pt>>();
        var data = new[] { new Pt("A", 10m), new Pt("B", 20m) };
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(data, ("OnClick", Callback<NTSeriesClickEventArgs<Pt>>(this, clicks.Add))));
        var series = SeriesOf<NTBarSeries<Pt>>(cut);
        SetPlotArea(cut.Instance, Area);
        using var surface = Draw(cut, series);

        Click(cut, new SKPoint(225, 190));

        cut.WaitForAssertion(() => clicks.Should().ContainSingle());
        clicks[0].PointIndex.Should().Be(1);
        clicks[0].DataPoint.Should().BeSameAs(data[1]);
    }

    [Fact]
    public void Click_OnEmptySpace_DoesNotRaiseOnClick() {
        var clicks = new List<NTSeriesClickEventArgs<Pt>>();
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(3), ("OnClick", Callback<NTSeriesClickEventArgs<Pt>>(this, clicks.Add))));
        SetPlotArea(cut.Instance, Area);

        Click(cut, new SKPoint(150, 4));

        clicks.Should().BeEmpty();
    }

    [Fact]
    public void Click_OnAYearGroupedPoint_ZoomsToThatYearAndReportsTheGroupInsteadOfAPointClick() {
        var pointClicks = new List<NTSeriesClickEventArgs<Pt>>();
        var groupClicks = new List<NTSeriesDateGroupClickEventArgs<Pt>>();
        var data = Enumerable.Range(0, 120).Select(i => new Pt(new DateTime(2010 + (i / 12), 1 + (i % 12), 15), 50m)).ToArray();
        var cut = RenderChart(XAxisKind.DateTime, SeriesFragment<NTLineSeries<Pt>>(
            data,
            ("OnClick", Callback<NTSeriesClickEventArgs<Pt>>(this, pointClicks.Add)),
            ("OnDateGroupClick", Callback<NTSeriesDateGroupClickEventArgs<Pt>>(this, groupClicks.Add))));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        SetPlotArea(cut.Instance, Area);
        using (Draw(cut, series)) {
        }

        // Every year aggregates to the same flat value, so the whole line is one horizontal corridor.
        var lineY = ExpectedPoint(cut.Instance, new DateTime(2015, 7, 1).Ticks, 50m).Y;
        Click(cut, new SKPoint(150, lineY));

        cut.WaitForAssertion(() => groupClicks.Should().ContainSingle());
        var group = groupClicks[0];
        group.GroupingLevel.Should().Be(NTDateGroupingLevel.Year);
        group.RangeStart.Should().Be(new DateTime(group.ClickedDate.Year, 1, 1));
        group.RangeEnd.Should().Be(new DateTime(group.ClickedDate.Year + 1, 1, 1).AddTicks(-1));
        group.GroupDate.Should().Be(group.RangeStart);
        group.ZoomApplied.Should().BeTrue();
        group.Series.Should().BeSameAs(series);
        series.GetViewXRange().Should().Be(((double)group.RangeStart.Ticks, (double)group.RangeEnd.Ticks));
        pointClicks.Should().BeEmpty("the group click replaces the regular point click");
    }

    [Fact]
    public void Click_OnAYearGroupedPointWithZoomDisabled_LeavesTheViewUntouchedAndStillReportsTheGroup() {
        var groupClicks = new List<NTSeriesDateGroupClickEventArgs<Pt>>();
        var data = Enumerable.Range(0, 120).Select(i => new Pt(new DateTime(2010 + (i / 12), 1 + (i % 12), 15), 50m)).ToArray();
        var cut = RenderChart(XAxisKind.DateTime, SeriesFragment<NTLineSeries<Pt>>(
            data,
            ("ZoomToDateGroupOnClick", false),
            ("OnDateGroupClick", Callback<NTSeriesDateGroupClickEventArgs<Pt>>(this, groupClicks.Add))));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        SetPlotArea(cut.Instance, Area);
        using (Draw(cut, series)) {
        }
        var lineY = ExpectedPoint(cut.Instance, new DateTime(2015, 7, 1).Ticks, 50m).Y;

        Click(cut, new SKPoint(150, lineY));

        cut.WaitForAssertion(() => groupClicks.Should().ContainSingle());
        groupClicks[0].ZoomApplied.Should().BeFalse();
        series.GetViewXRange().Should().BeNull();
    }

    [Fact]
    public void HoverCallbacks_PassSeriesPointIndexDataAndPositionThrough() {
        var enters = new List<NTSeriesHoverEnterEventArgs<Pt>>();
        var leaves = new List<NTSeriesHoverLeaveEventArgs<Pt>>();
        var data = Linear(2);
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data,
            ("OnHoverEnter", Callback<NTSeriesHoverEnterEventArgs<Pt>>(this, enters.Add)),
            ("OnHoverLeave", Callback<NTSeriesHoverLeaveEventArgs<Pt>>(this, leaves.Add))));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        typeof(NTBaseSeries<Pt>).GetMethod("NotifyHoverEnter", NonPublic)!.Invoke(series, [new NTSeriesHoverEnterEventArgs<Pt> { Series = series, PointIndex = 1, DataPoint = data[1], PointerPosition = new SKPoint(3, 4) }]);
        typeof(NTBaseSeries<Pt>).GetMethod("NotifyHoverLeave", NonPublic)!.Invoke(series, [new NTSeriesHoverLeaveEventArgs<Pt> { Series = series, PointIndex = 1, PointerPosition = new SKPoint(5, 6) }]);

        cut.WaitForAssertion(() => {
            enters.Should().ContainSingle();
            leaves.Should().ContainSingle();
        });
        enters[0].PointIndex.Should().Be(1);
        enters[0].DataPoint.Should().BeSameAs(data[1]);
        enters[0].PointerPosition.Should().Be(new SKPoint(3, 4));
        leaves[0].PointIndex.Should().Be(1);
        leaves[0].PointerPosition.Should().Be(new SKPoint(5, 6));
    }

    [Fact]
    public void ArgsWithoutOptionalValues_DefaultToNull() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(1)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        var enter = new NTSeriesHoverEnterEventArgs<Pt> { Series = series };
        var reset = new NTSeriesResetViewEventArgs<Pt> { Series = series };

        enter.PointIndex.Should().BeNull();
        enter.DataPoint.Should().BeNull();
        enter.PointerPosition.Should().BeNull();
        reset.ViewXRange.Should().BeNull();
        reset.ViewYRange.Should().BeNull();
    }
}
