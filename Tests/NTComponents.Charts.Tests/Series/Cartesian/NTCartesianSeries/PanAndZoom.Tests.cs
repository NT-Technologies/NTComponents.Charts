using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class PanAndZoom_Tests : CartesianChartTestBase {
    private static readonly SKRect PlotArea = new(0, 0, 300, 200);

    private IRenderedComponent<NTChart<Pt>> RenderInteractive(ChartInteractions interactions, params (string Name, object Value)[] extra) {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(10), [("Interactions", interactions), .. extra]));
        SetPlotArea(cut.Instance, PlotArea);
        return cut;
    }

    private static MouseEventArgs Mouse(double x, double y) => new() { OffsetX = x, OffsetY = y };

    [Fact]
    public void Pan_WithXPan_ShiftsViewRangeOppositeToTheDrag() {
        var cut = RenderInteractive(ChartInteractions.XPan);
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        var (xMin, xMax) = ChartXRange(cut.Instance, true);

        series.HandleMouseDown(Mouse(150, 100));
        series.HandleMouseMove(Mouse(120, 100));

        // Dragging 30px left of a 300px plot reveals 10% of the range on the right.
        var shift = 0.1 * (xMax - xMin);
        var view = series.GetViewXRange()!.Value;
        view.Min.Should().BeApproximately(xMin + shift, 1e-6);
        view.Max.Should().BeApproximately(xMax + shift, 1e-6);
        series.GetViewYRange().Should().BeNull();
    }

    [Fact]
    public void Pan_WithYPan_ShiftsYViewRangeByDragDistance()
    {
        var cut = RenderInteractive(ChartInteractions.YPan);
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        var (yMin, yMax) = ChartYRange(cut.Instance, true);

        series.HandleMouseDown(Mouse(150, 100));
        series.HandleMouseMove(Mouse(150, 120));

        // Dragging 20px down of a 200px plot moves the visible range up by 10% of its height.
        var shift = 0.1m * (yMax - yMin);
        var view = series.GetViewYRange()!.Value;
        view.Min.Should().BeApproximately(yMin + shift, 0.0001m);
        view.Max.Should().BeApproximately(yMax + shift, 0.0001m);
        series.GetViewXRange().Should().BeNull();
    }

    [Fact]
    public void Pan_WithoutPanInteraction_IgnoresDrag() {
        var cut = RenderInteractive(ChartInteractions.None);
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.HandleMouseDown(Mouse(150, 100));
        series.HandleMouseMove(Mouse(100, 50));

        series.IsPanning.Should().BeFalse();
        series.GetViewXRange().Should().BeNull();
        series.GetViewYRange().Should().BeNull();
    }

    [Fact]
    public void Pan_MoveWithoutMouseDown_DoesNotChangeView() {
        var cut = RenderInteractive(ChartInteractions.PanAll);
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.HandleMouseMove(Mouse(100, 50));

        series.GetViewXRange().Should().BeNull();
    }

    [Fact]
    public void Pan_MouseUp_EndsPanningAndKeepsTheView() {
        var cut = RenderInteractive(ChartInteractions.XPan);
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        series.HandleMouseDown(Mouse(150, 100));
        series.HandleMouseMove(Mouse(120, 100));
        series.IsPanning.Should().BeTrue();
        var view = series.GetViewXRange();

        series.HandleMouseUp(Mouse(120, 100));
        series.HandleMouseMove(Mouse(60, 100));

        series.IsPanning.Should().BeFalse();
        series.GetViewXRange().Should().Be(view);
    }

    [Fact]
    public void Pan_WithLargerDrag_ScalesTheShiftProportionally() {
        var cut = RenderInteractive(ChartInteractions.XPan);
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        var (xMin, xMax) = ChartXRange(cut.Instance, true);

        series.HandleMouseDown(Mouse(150, 100));
        series.HandleMouseMove(Mouse(150 - 60, 100));

        series.GetViewXRange()!.Value.Min.Should().BeApproximately(xMin + (0.2 * (xMax - xMin)), 1e-6);
    }

    [Fact]
    public void Pan_ChartRangeFollowsPannedViewWithoutPadding() {
        var cut = RenderInteractive(ChartInteractions.XPan);
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.HandleMouseDown(Mouse(150, 100));
        series.HandleMouseMove(Mouse(120, 100));

        ChartXRange(cut.Instance).Should().Be(series.GetViewXRange()!.Value);
    }

    [Fact]
    public void Zoom_WheelDownZoomsOutByTenPercentAroundThePointer() {
        var cut = RenderInteractive(ChartInteractions.XZoom);
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        var (xMin, xMax) = ChartXRange(cut.Instance, true);
        var pointer = cut.Instance.ScaleXInverse(150f, PlotArea);

        series.HandleMouseWheel(new WheelEventArgs { OffsetX = 150, OffsetY = 100, DeltaY = 100 });

        var view = series.GetViewXRange()!.Value;
        (view.Max - view.Min).Should().BeApproximately((xMax - xMin) * 1.1, 1e-6);
        // The data value under the pointer keeps its relative position inside the window.
        var before = (pointer - xMin) / (xMax - xMin);
        var after = (pointer - view.Min) / (view.Max - view.Min);
        after.Should().BeApproximately(before, 1e-6);
    }

    [Fact]
    public void Zoom_WheelUpZoomsInByTenPercent() {
        var cut = RenderInteractive(ChartInteractions.XZoom);
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        var (xMin, xMax) = ChartXRange(cut.Instance, true);

        series.HandleMouseWheel(new WheelEventArgs { OffsetX = 150, OffsetY = 100, DeltaY = -100 });

        var view = series.GetViewXRange()!.Value;
        (view.Max - view.Min).Should().BeApproximately((xMax - xMin) * 0.9, 1e-6);
    }

    [Fact]
    public void Zoom_PointerOutsidePlotArea_IsIgnored() {
        var cut = RenderInteractive(ChartInteractions.ZoomAll);
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.HandleMouseWheel(new WheelEventArgs { OffsetX = 400, OffsetY = 100, DeltaY = 100 });

        series.GetViewXRange().Should().BeNull();
        series.GetViewYRange().Should().BeNull();
    }

    [Fact]
    public void Zoom_WithoutZoomInteraction_IsIgnored() {
        var cut = RenderInteractive(ChartInteractions.PanAll);
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.HandleMouseWheel(new WheelEventArgs { OffsetX = 150, OffsetY = 100, DeltaY = 100 });

        series.GetViewXRange().Should().BeNull();
    }

    [Fact]
    public void Zoom_WithoutPlotArea_IsIgnored() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(10), ("Interactions", ChartInteractions.XZoom)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.HandleMouseWheel(new WheelEventArgs { OffsetX = 150, OffsetY = 100, DeltaY = 100 });

        series.GetViewXRange().Should().BeNull();
    }

    [Fact]
    public void Zoom_YZoomOnly_ChangesOnlyTheYView() {
        var cut = RenderInteractive(ChartInteractions.YZoom);
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        var (yMin, yMax) = ChartYRange(cut.Instance, true);

        series.HandleMouseWheel(new WheelEventArgs { OffsetX = 150, OffsetY = 100, DeltaY = 100 });

        var view = series.GetViewYRange()!.Value;
        (view.Max - view.Min).Should().BeApproximately((yMax - yMin) * 1.1m, 0.0001m);
        series.GetViewXRange().Should().BeNull();
    }

    [Fact]
    public void Zoom_LineSeriesRefitsYToTheZoomedWindowWithPadding() {
        var cut = RenderInteractive(ChartInteractions.XZoom);
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.HandleMouseWheel(new WheelEventArgs { OffsetX = 150, OffsetY = 100, DeltaY = -100 });

        var x = series.GetViewXRange()!.Value;
        var window = series.GetYRange(x.Min, x.Max)!.Value;
        var padding = (window.Max - window.Min) * 0.05m;
        var y = series.GetViewYRange()!.Value;
        y.Min.Should().BeApproximately(window.Min - padding, 0.0001m);
        y.Max.Should().BeApproximately(window.Max + padding, 0.0001m);
    }

    [Fact]
    public void Zoom_LineSeriesWithFlatWindow_ExpandsTheDegenerateRangeBeforePadding() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(10, _ => 50m), ("Interactions", ChartInteractions.XZoom)));
        SetPlotArea(cut.Instance, PlotArea);
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.HandleMouseWheel(new WheelEventArgs { OffsetX = 150, OffsetY = 100, DeltaY = -100 });

        // Flat 50 widens by 1% each side (49.5..50.5) and then pads by 5% of that span.
        var y = series.GetViewYRange()!.Value;
        y.Min.Should().BeApproximately(49.45m, 0.0001m);
        y.Max.Should().BeApproximately(50.55m, 0.0001m);
    }

    [Fact]
    public void Zoom_LineSeriesWithFlatWindowNearZero_ExpandsByOneUnit() {
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(Linear(10, _ => 0m), ("Interactions", ChartInteractions.XZoom)));
        SetPlotArea(cut.Instance, PlotArea);
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.HandleMouseWheel(new WheelEventArgs { OffsetX = 150, OffsetY = 100, DeltaY = -100 });

        var y = series.GetViewYRange()!.Value;
        y.Min.Should().BeApproximately(-1.1m, 0.0001m);
        y.Max.Should().BeApproximately(1.1m, 0.0001m);
    }

    [Fact]
    public void ResetView_ClearsAllViewRanges() {
        var cut = RenderInteractive(ChartInteractions.All);
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        series.HandleMouseWheel(new WheelEventArgs { OffsetX = 150, OffsetY = 100, DeltaY = 100 });
        series.GetViewXRange().Should().NotBeNull();

        series.ResetView();

        series.GetViewXRange().Should().BeNull();
        series.GetViewYRange().Should().BeNull();
    }

    [Fact]
    public void ChartResetView_ClearsEverySeriesView() {
        var cut = RenderInteractive(ChartInteractions.XZoom);
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        series.HandleMouseWheel(new WheelEventArgs { OffsetX = 150, OffsetY = 100, DeltaY = 100 });

        OnDispatcher(cut, () => cut.Instance.ResetView());

        series.GetViewXRange().Should().BeNull();
        ChartXRange(cut.Instance).Should().Be((1d, 10d));
    }

    [Fact]
    public void Callbacks_PanStartPanAndPanEndReportTheViewAtEachStep() {
        var starts = new List<NTSeriesPanStartEventArgs<Pt>>();
        var pans = new List<NTSeriesPanEventArgs<Pt>>();
        var ends = new List<NTSeriesPanEndEventArgs<Pt>>();
        var cut = RenderInteractive(
            ChartInteractions.XPan,
            ("OnPanStart", Microsoft.AspNetCore.Components.EventCallback.Factory.Create<NTSeriesPanStartEventArgs<Pt>>(this, starts.Add)),
            ("OnPan", Microsoft.AspNetCore.Components.EventCallback.Factory.Create<NTSeriesPanEventArgs<Pt>>(this, pans.Add)),
            ("OnPanEnd", Microsoft.AspNetCore.Components.EventCallback.Factory.Create<NTSeriesPanEndEventArgs<Pt>>(this, ends.Add)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.HandleMouseDown(Mouse(150, 100));
        series.HandleMouseMove(Mouse(120, 100));
        series.HandleMouseUp(Mouse(120, 100));

        cut.WaitForAssertion(() => {
            starts.Should().ContainSingle();
            pans.Should().ContainSingle();
            ends.Should().ContainSingle();
        });
        starts[0].Series.Should().BeSameAs(series);
        starts[0].PointerPosition.Should().Be(new SKPoint(150, 100));
        starts[0].ViewXRange.Should().BeNull();
        pans[0].PointerPosition.Should().Be(new SKPoint(120, 100));
        pans[0].ViewXRange.Should().Be(series.GetViewXRange());
        ends[0].ViewXRange.Should().Be(series.GetViewXRange());
    }

    [Fact]
    public void Callbacks_ZoomAndResetViewReportTheResultingView() {
        var zooms = new List<NTSeriesZoomEventArgs<Pt>>();
        var resets = new List<NTSeriesResetViewEventArgs<Pt>>();
        var cut = RenderInteractive(
            ChartInteractions.XZoom,
            ("OnZoom", Microsoft.AspNetCore.Components.EventCallback.Factory.Create<NTSeriesZoomEventArgs<Pt>>(this, zooms.Add)),
            ("OnResetView", Microsoft.AspNetCore.Components.EventCallback.Factory.Create<NTSeriesResetViewEventArgs<Pt>>(this, resets.Add)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);

        series.HandleMouseWheel(new WheelEventArgs { OffsetX = 150, OffsetY = 100, DeltaY = 100 });
        var zoomedView = series.GetViewXRange();
        series.ResetView();

        cut.WaitForAssertion(() => {
            zooms.Should().ContainSingle();
            resets.Should().ContainSingle();
        });
        zooms[0].Series.Should().BeSameAs(series);
        zooms[0].PointerPosition.Should().Be(new SKPoint(150, 100));
        zooms[0].ViewXRange.Should().Be(zoomedView);
        resets[0].Series.Should().BeSameAs(series);
        resets[0].ViewXRange.Should().BeNull();
        resets[0].ViewYRange.Should().BeNull();
    }

    [Fact]
    public void Callbacks_SuppressedInteractionCallbacks_DoNotFire() {
        var pans = new List<NTSeriesPanEventArgs<Pt>>();
        var cut = RenderInteractive(
            ChartInteractions.XPan,
            ("OnPan", Microsoft.AspNetCore.Components.EventCallback.Factory.Create<NTSeriesPanEventArgs<Pt>>(this, pans.Add)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        typeof(NTBaseSeries<Pt>).GetProperty("SuppressInteractionCallbacks", NonPublic)!.SetValue(series, true);

        series.HandleMouseDown(Mouse(150, 100));
        series.HandleMouseMove(Mouse(120, 100));

        series.GetViewXRange().Should().NotBeNull();
        pans.Should().BeEmpty();
    }
}
