using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTChart_Interactions_Tests : ChartTestBase {
    private static RenderFragment NoAxes() => Combine(XAxis<double>(p => p.X, Args(("Visible", false))), YAxis(Args(("Visible", false))));

    private static (int Index, Pt? Data)? AlwaysHit(SKPoint _, SKRect __) => (1, Sample[1]);

    private IRenderedComponent<NTChart<Pt>> HoverChart(Func<SKPoint, SKRect, (int Index, Pt? Data)?> hit, params (string, object)[] seriesArgs) =>
        RenderChart(Combine(NoAxes(), Recording("a", hit: hit, extra: Args(seriesArgs))));

    private EventCallback<T> Cb<T>(Action<T> handler) => EventCallback.Factory.Create(this, handler);

    // ---------- hover ----------

    [Fact]
    public async Task PointerOverAHit_SetsHoveredSeriesPointAndData() {
        var chart = HoverChart(AlwaysHit);
        using var surface = await Paint(chart);

        await Move(chart, 300, 150);
        await Paint(chart, surface: surface);

        chart.Instance.HoveredSeries.Should().BeSameAs(chart.Instance.Series.Single());
        chart.Instance.HoveredPointIndex.Should().Be(1);
        chart.Instance.HoveredDataPoint.Should().Be(Sample[1]);
        chart.Instance.LastMousePosition.Should().Be(new SKPoint(300, 150));
    }

    [Fact]
    public async Task HitTest_ReceivesPointerPositionAndThePlotArea() {
        var chart = HoverChart(AlwaysHit);
        using var surface = await Paint(chart);

        await Move(chart, 300, 150);
        await Paint(chart, surface: surface);

        var series = chart.Instance.Series.Cast<RecordingSeries>().Single();
        series.HitPoints.Should().Contain(new SKPoint(300, 150));
        series.HitAreas.Last().Should().Be(PlotArea(chart));
    }

    [Fact]
    public async Task PointerOverNoHit_LeavesNothingHovered() {
        var chart = HoverChart((_, _) => null);
        using var surface = await Paint(chart);

        await Move(chart, 300, 150);
        await Paint(chart, surface: surface);

        chart.Instance.HoveredSeries.Should().BeNull();
        chart.Instance.HoveredPointIndex.Should().BeNull();
        chart.Instance.HoveredDataPoint.Should().BeNull();
        chart.Instance.LastMousePosition.Should().Be(new SKPoint(300, 150));
    }

    [Fact]
    public async Task PointerOutsideThePlotArea_NeverHitTestsSeries() {
        var chart = HoverChart(AlwaysHit);
        using var surface = await Paint(chart);

        await Move(chart, 2, 2);
        await Paint(chart, surface: surface);

        chart.Instance.Series.Cast<RecordingSeries>().Single().HitPoints.Should().BeEmpty();
        chart.Instance.HoveredSeries.Should().BeNull();
    }

    [Fact]
    public async Task OverlappingSeries_TopmostRegisteredSeriesWinsAndFallsThroughOnMiss() {
        var topHits = true;
        var chart = RenderChart(Combine(NoAxes(),
            Recording("bottom", hit: (_, _) => (0, Sample[0])),
            Recording("top", hit: (_, _) => topHits ? (3, Sample[3]) : null)));
        using var surface = await Paint(chart);

        await Move(chart, 300, 150);
        await Paint(chart, surface: surface);
        chart.Instance.HoveredSeries.Should().BeSameAs(chart.Instance.Series[1]);
        chart.Instance.HoveredPointIndex.Should().Be(3);

        topHits = false;
        await Move(chart, 301, 150);
        await Paint(chart, surface: surface);
        chart.Instance.HoveredSeries.Should().BeSameAs(chart.Instance.Series[0]);
        chart.Instance.HoveredPointIndex.Should().Be(0);
    }

    [Fact]
    public async Task HiddenSeries_AreNotHitTested() {
        var chart = RenderChart(Combine(NoAxes(),
            Recording("visible", hit: (_, _) => (0, Sample[0])),
            Recording("hidden", hit: (_, _) => (2, Sample[2]), extra: Args(("Visible", false)))));
        using var surface = await Paint(chart);

        await Move(chart, 300, 150);
        await Paint(chart, surface: surface);

        chart.Instance.HoveredSeries.Should().BeSameAs(chart.Instance.Series[0]);
        chart.Instance.Series.Cast<RecordingSeries>().Last().HitPoints.Should().BeEmpty();
    }

    [Fact]
    public async Task HoveredSeries_IsRenderedLastSoItDrawsOnTop() {
        var log = new List<string>();
        var chart = RenderChart(Combine(NoAxes(),
            Recording("first", log, hit: (_, _) => (0, Sample[0])),
            Recording("second", log)));
        using var surface = await Paint(chart);
        log.Should().Equal("first", "second");
        await Move(chart, 300, 150);
        await Paint(chart, surface: surface);
        log.Clear();

        await Paint(chart, surface: surface);

        log.Should().Equal("second", "first");
    }

    [Fact]
    public async Task HoverEnter_IsRaisedOnceWithPointerContextAndNotRepeatedForTheSamePoint() {
        var entered = new List<NTSeriesHoverEnterEventArgs<Pt>>();
        var chart = HoverChart(AlwaysHit, ("OnHoverEnter", Cb<NTSeriesHoverEnterEventArgs<Pt>>(entered.Add)));
        using var surface = await Paint(chart);

        await Move(chart, 300, 150);
        await Paint(chart, surface: surface);
        await Move(chart, 310, 150);
        await Paint(chart, surface: surface);
        await chart.InvokeAsync(() => { });

        entered.Should().ContainSingle();
        entered[0].Series.Should().BeSameAs(chart.Instance.Series.Single());
        entered[0].PointIndex.Should().Be(1);
        entered[0].DataPoint.Should().Be(Sample[1]);
        entered[0].PointerPosition.Should().Be(new SKPoint(300, 150));
    }

    [Fact]
    public async Task MovingToAnotherPoint_RaisesLeaveForThePreviousPointThenEnterForTheNew() {
        var events = new List<string>();
        var index = 0;
        var chart = HoverChart((_, _) => (index, Sample[index]),
            ("OnHoverEnter", Cb<NTSeriesHoverEnterEventArgs<Pt>>(e => events.Add($"enter{e.PointIndex}"))),
            ("OnHoverLeave", Cb<NTSeriesHoverLeaveEventArgs<Pt>>(e => events.Add($"leave{e.PointIndex}"))));
        using var surface = await Paint(chart);
        await Move(chart, 300, 150);
        await Paint(chart, surface: surface);

        index = 2;
        await Move(chart, 320, 150);
        await Paint(chart, surface: surface);
        await chart.InvokeAsync(() => { });

        events.Should().Equal("enter0", "leave0", "enter2");
        chart.Instance.HoveredPointIndex.Should().Be(2);
    }

    [Fact]
    public async Task LosingTheHit_ClearsHoverAndRaisesLeaveAfterTheMissGracePeriod() {
        var leaves = new List<NTSeriesHoverLeaveEventArgs<Pt>>();
        var hit = true;
        var chart = HoverChart((_, _) => hit ? (1, Sample[1]) : null, ("OnHoverLeave", Cb<NTSeriesHoverLeaveEventArgs<Pt>>(leaves.Add)));
        using var surface = await Paint(chart);
        await Move(chart, 300, 150);
        await Paint(chart, surface: surface);
        chart.Instance.HoveredSeries.Should().NotBeNull();

        hit = false;
        await Move(chart, 340, 150);
        for (var i = 0; i < 3; i++) {
            await Paint(chart, surface: surface);
        }
        await chart.InvokeAsync(() => { });

        chart.Instance.HoveredSeries.Should().BeNull();
        chart.Instance.HoveredDataPoint.Should().BeNull();
        leaves.Should().ContainSingle();
        leaves[0].PointIndex.Should().Be(1);
        leaves[0].PointerPosition.Should().Be(new SKPoint(340, 150));
    }

    [Fact]
    public async Task PointerLeavingTheChart_ClearsHoverAndPositionAndRaisesLeave() {
        var leaves = new List<NTSeriesHoverLeaveEventArgs<Pt>>();
        var chart = HoverChart(AlwaysHit, ("OnHoverLeave", Cb<NTSeriesHoverLeaveEventArgs<Pt>>(leaves.Add)));
        using var surface = await Paint(chart);
        await Move(chart, 300, 150);
        await Paint(chart, surface: surface);

        await Fire(chart, h => h.MouseLeave());
        await chart.InvokeAsync(() => { });

        chart.Instance.HoveredSeries.Should().BeNull();
        chart.Instance.HoveredPointIndex.Should().BeNull();
        chart.Instance.LastMousePosition.Should().BeNull();
        leaves.Should().ContainSingle().Which.PointIndex.Should().Be(1);
    }

    [Fact]
    public async Task Hover_OverASeriesWithoutClickBehavior_KeepsTheDefaultCursor() {
        var chart = HoverChart(AlwaysHit);
        using var surface = await Paint(chart);

        await Move(chart, 300, 150);
        await Paint(chart, surface: surface);

        chart.Instance.HoveredSeries.Should().NotBeNull();
        Host(chart).GetAttribute("style").Should().Contain("cursor: default;");
    }

    [Fact]
    public async Task Hover_OverAClickableSeries_SwitchesTheCursorToPointerAndBackToDefault() {
        var hit = true;
        var chart = HoverChart((_, _) => hit ? (1, Sample[1]) : null, ("OnClick", Cb<NTSeriesClickEventArgs<Pt>>(_ => { })));
        using var surface = await Paint(chart);
        Host(chart).GetAttribute("style").Should().Contain("cursor: default;");

        await Move(chart, 300, 150);
        await Paint(chart, surface: surface);
        Host(chart).GetAttribute("style").Should().Contain("cursor: pointer;");

        hit = false;
        await Move(chart, 340, 150);
        for (var i = 0; i < 3; i++) {
            await Paint(chart, surface: surface);
        }
        Host(chart).GetAttribute("style").Should().Contain("cursor: default;");
    }

    [Fact]
    public async Task PointerPosition_IsScaledByDevicePixelDensity() {
        var chart = HoverChart(AlwaysHit);
        await chart.InvokeAsync(() => chart.Instance.OnNativeViewportChanged(true, 2f));
        using var surface = await Paint(chart);

        await Move(chart, 100, 50);

        chart.Instance.LastMousePosition.Should().Be(new SKPoint(200, 100));
    }

    [Fact]
    public async Task InvisibleChart_IgnoresNativeMouseMoves() {
        var chart = HoverChart(AlwaysHit);
        using var surface = await Paint(chart);
        await chart.InvokeAsync(() => chart.Instance.OnNativeViewportChanged(false, 1f));

        await Move(chart, 300, 150);

        chart.Instance.LastMousePosition.Should().BeNull();
    }

    // ---------- click ----------

    [Fact]
    public async Task Click_OnAHit_RaisesSeriesClickWithPointContext() {
        var clicks = new List<NTSeriesClickEventArgs<Pt>>();
        var chart = HoverChart((_, _) => (2, Sample[2]), ("OnClick", Cb<NTSeriesClickEventArgs<Pt>>(clicks.Add)));
        using var surface = await Paint(chart);

        await Fire(chart, h => h.Click(new MouseEventArgs { OffsetX = 300, OffsetY = 150 }));

        chart.WaitForAssertion(() => clicks.Should().ContainSingle());
        clicks[0].Series.Should().BeSameAs(chart.Instance.Series.Single());
        clicks[0].PointIndex.Should().Be(2);
        clicks[0].DataPoint.Should().Be(Sample[2]);
        clicks[0].PointerPosition.Should().Be(new SKPoint(300, 150));
        clicks[0].MouseEvent.OffsetX.Should().Be(300);
    }

    [Fact]
    public async Task Click_ScalesPointerPositionByDensityBeforeHitTesting() {
        var clicks = new List<NTSeriesClickEventArgs<Pt>>();
        var chart = HoverChart(AlwaysHit, ("OnClick", Cb<NTSeriesClickEventArgs<Pt>>(clicks.Add)));
        await chart.InvokeAsync(() => chart.Instance.OnNativeViewportChanged(true, 2f));
        using var surface = await Paint(chart);

        await Fire(chart, h => h.Click(new MouseEventArgs { OffsetX = 150, OffsetY = 75 }));

        chart.WaitForAssertion(() => clicks.Should().ContainSingle());
        clicks[0].PointerPosition.Should().Be(new SKPoint(300, 150));
    }

    [Fact]
    public async Task Click_WithoutAHit_RaisesNothing() {
        var clicks = 0;
        var chart = HoverChart((_, _) => null, ("OnClick", Cb<NTSeriesClickEventArgs<Pt>>(_ => clicks++)));
        using var surface = await Paint(chart);

        await Fire(chart, h => h.Click(new MouseEventArgs { OffsetX = 300, OffsetY = 150 }));
        await chart.InvokeAsync(() => { });

        clicks.Should().Be(0);
    }

    [Fact]
    public async Task Click_OutsideThePlotArea_RaisesNothing() {
        var clicks = 0;
        var chart = HoverChart(AlwaysHit, ("OnClick", Cb<NTSeriesClickEventArgs<Pt>>(_ => clicks++)));
        using var surface = await Paint(chart);

        await Fire(chart, h => h.Click(new MouseEventArgs { OffsetX = 2, OffsetY = 2 }));
        await chart.InvokeAsync(() => { });

        clicks.Should().Be(0);
    }

    // ---------- pan ----------

    [Fact]
    public async Task Drag_WithXAndYPan_ShiftsTheViewByTheDraggedFractionOfTheRange() {
        var chart = RenderChart(Combine(NoAxes(), Recording("a", extra: Args(("Interactions", ChartInteractions.PanAll)))));
        using var surface = await Paint(chart);
        var plot = PlotArea(chart);
        var series = chart.Instance.Series.Single();

        await Fire(chart, h => h.MouseDown(new MouseEventArgs { OffsetX = 300, OffsetY = 150 }));
        await Move(chart, 200, 190);

        var x = series.GetViewXRange()!.Value;
        var y = series.GetViewYRange()!.Value;
        // Dragging left by 100px moves the view right by 100px worth of data; dragging down moves it up.
        x.Min.Should().BeApproximately(-0.15 + (100 / plot.Width * 3.3), 1e-4);
        x.Max.Should().BeApproximately(3.15 + (100 / plot.Width * 3.3), 1e-4);
        y.Min.Should().BeApproximately(8.5m + (decimal)(40 / plot.Height) * 33m, 0.001m);
        (x.Max - x.Min).Should().BeApproximately(3.3, 1e-9, "panning must not change the span");
    }

    [Fact]
    public async Task Drag_WithXPanOnly_LeavesYViewUntouched() {
        var chart = RenderChart(Combine(NoAxes(), Recording("a", extra: Args(("Interactions", ChartInteractions.XPan)))));
        using var surface = await Paint(chart);

        await Fire(chart, h => h.MouseDown(new MouseEventArgs { OffsetX = 300, OffsetY = 150 }));
        await Move(chart, 250, 200);

        var series = chart.Instance.Series.Single();
        series.GetViewXRange().Should().NotBeNull();
        series.GetViewYRange().Should().BeNull();
    }

    [Fact]
    public async Task Drag_WithoutPanInteractions_DoesNotChangeTheView() {
        var chart = RenderChart(Combine(NoAxes(), Recording("a")));
        using var surface = await Paint(chart);

        await Fire(chart, h => h.MouseDown(new MouseEventArgs { OffsetX = 300, OffsetY = 150 }));
        await Move(chart, 200, 190);

        var series = chart.Instance.Series.Single();
        series.GetViewXRange().Should().BeNull();
        series.IsPanning.Should().BeFalse();
    }

    [Fact]
    public async Task Drag_ShowsGrabbingCursorWhilePanning() {
        var chart = RenderChart(Combine(NoAxes(), Recording("a", extra: Args(("Interactions", ChartInteractions.XPan)))));
        using var surface = await Paint(chart);
        Host(chart).GetAttribute("style").Should().Contain("cursor: grab;");

        await Fire(chart, h => h.MouseDown(new MouseEventArgs { OffsetX = 300, OffsetY = 150 }));
        chart.Instance.Series.Single().IsPanning.Should().BeTrue();
        Host(chart).GetAttribute("style").Should().Contain("cursor: grabbing;");

        await Fire(chart, h => h.MouseUp(new MouseEventArgs { OffsetX = 300, OffsetY = 150 }));
        chart.Instance.Series.Single().IsPanning.Should().BeFalse();
    }

    [Fact]
    public async Task Drag_RaisesPanStartPanAndPanEndWithTheCurrentViewRange() {
        var starts = new List<NTSeriesPanStartEventArgs<Pt>>();
        var pans = new List<NTSeriesPanEventArgs<Pt>>();
        var ends = new List<NTSeriesPanEndEventArgs<Pt>>();
        var chart = RenderChart(Combine(NoAxes(), Recording("a", extra: Args(
            ("Interactions", ChartInteractions.XPan),
            ("OnPanStart", Cb<NTSeriesPanStartEventArgs<Pt>>(starts.Add)),
            ("OnPan", Cb<NTSeriesPanEventArgs<Pt>>(pans.Add)),
            ("OnPanEnd", Cb<NTSeriesPanEndEventArgs<Pt>>(ends.Add))))));
        using var surface = await Paint(chart);

        await Fire(chart, h => h.MouseDown(new MouseEventArgs { OffsetX = 300, OffsetY = 150 }));
        await Move(chart, 250, 150);
        await Fire(chart, h => h.MouseUp(new MouseEventArgs { OffsetX = 250, OffsetY = 150 }));

        chart.WaitForAssertion(() => {
            starts.Should().ContainSingle();
            pans.Should().ContainSingle();
            ends.Should().ContainSingle();
        });
        starts[0].ViewXRange.Should().BeNull("no view has been applied before the first movement");
        starts[0].PointerPosition.Should().Be(new SKPoint(300, 150));
        pans[0].PointerPosition.Should().Be(new SKPoint(250, 150));
        pans[0].ViewXRange.Should().NotBeNull();
        ends[0].ViewXRange.Should().Be(chart.Instance.Series.Single().GetViewXRange());
    }

    [Fact]
    public async Task Drag_WithSeveralSeries_MovesAllViewsButRaisesCallbacksOnlyOncePerGesture() {
        var pans = 0;
        var args = Args(("Interactions", ChartInteractions.XPan), ("OnPan", Cb<NTSeriesPanEventArgs<Pt>>(_ => pans++)));
        var chart = RenderChart(Combine(NoAxes(), Recording("a", extra: args), Recording("b", extra: args)));
        using var surface = await Paint(chart);

        await Fire(chart, h => h.MouseDown(new MouseEventArgs { OffsetX = 300, OffsetY = 150 }));
        await Move(chart, 250, 150);
        await chart.InvokeAsync(() => { });

        chart.Instance.Series[0].GetViewXRange().Should().NotBeNull();
        chart.Instance.Series[1].GetViewXRange().Should().Be(chart.Instance.Series[0].GetViewXRange());
        pans.Should().Be(1);
    }

    // ---------- zoom ----------

    private static NTChart<Pt>.NTNativeWheelEventArgs Wheel(double x, double y, double deltaY) => new() { OffsetX = x, OffsetY = y, DeltaY = deltaY };

    [Fact]
    public async Task WheelUp_ZoomsInByTenPercentKeepingTheValueUnderTheCursorFixed() {
        var chart = RenderChart(Combine(NoAxes(), Recording("a", extra: Args(("Interactions", ChartInteractions.ZoomAll)))));
        using var surface = await Paint(chart);
        var plot = PlotArea(chart);
        var pivot = chart.Instance.ScaleXInverse(200, plot);

        await chart.InvokeAsync(() => chart.Instance.OnNativeWheel(Wheel(200, 150, -100)));

        var series = chart.Instance.Series.Single();
        var x = series.GetViewXRange()!.Value;
        var y = series.GetViewYRange()!.Value;
        (x.Max - x.Min).Should().BeApproximately(3.3 * 0.9, 1e-6);
        (y.Max - y.Min).Should().BeApproximately(33m * 0.9m, 0.001m);
        chart.Instance.ScaleX(pivot, plot).Should().BeApproximately(200f, 0.01f);
    }

    [Fact]
    public async Task WheelDown_ZoomsOutByTenPercent() {
        var chart = RenderChart(Combine(NoAxes(), Recording("a", extra: Args(("Interactions", ChartInteractions.XZoom)))));
        using var surface = await Paint(chart);

        await chart.InvokeAsync(() => chart.Instance.OnNativeWheel(Wheel(320, 150, 100)));

        var x = chart.Instance.Series.Single().GetViewXRange()!.Value;
        (x.Max - x.Min).Should().BeApproximately(3.3 * 1.1, 1e-6);
        chart.Instance.Series.Single().GetViewYRange().Should().BeNull("only X zoom is enabled");
    }

    [Fact]
    public async Task Wheel_WithYZoomOnly_ChangesOnlyTheYView() {
        var chart = RenderChart(Combine(NoAxes(), Recording("a", extra: Args(("Interactions", ChartInteractions.YZoom)))));
        using var surface = await Paint(chart);

        await chart.InvokeAsync(() => chart.Instance.OnNativeWheel(Wheel(320, 150, -100)));

        chart.Instance.Series.Single().GetViewXRange().Should().BeNull();
        chart.Instance.Series.Single().GetViewYRange().Should().NotBeNull();
    }

    [Fact]
    public async Task Wheel_WithoutZoomInteractionsOrOutsideThePlot_IsIgnored() {
        var disabled = RenderChart(Combine(NoAxes(), Recording("a")));
        using var surface = await Paint(disabled);
        await disabled.InvokeAsync(() => disabled.Instance.OnNativeWheel(Wheel(320, 150, -100)));
        disabled.Instance.Series.Single().GetViewXRange().Should().BeNull();

        var enabled = RenderChart(Combine(NoAxes(), Recording("a", extra: Args(("Interactions", ChartInteractions.ZoomAll)))));
        await Paint(enabled, surface: surface);
        await enabled.InvokeAsync(() => enabled.Instance.OnNativeWheel(Wheel(1, 1, -100)));
        enabled.Instance.Series.Single().GetViewXRange().Should().BeNull();
    }

    [Fact]
    public async Task Wheel_RaisesZoomOnceWithTheNewViewRange() {
        var zooms = new List<NTSeriesZoomEventArgs<Pt>>();
        var args = Args(("Interactions", ChartInteractions.XZoom), ("OnZoom", Cb<NTSeriesZoomEventArgs<Pt>>(zooms.Add)));
        var chart = RenderChart(Combine(NoAxes(), Recording("a", extra: args), Recording("b", extra: args)));
        using var surface = await Paint(chart);

        await chart.InvokeAsync(() => chart.Instance.OnNativeWheel(Wheel(320, 150, -100)));

        chart.WaitForAssertion(() => zooms.Should().ContainSingle());
        zooms[0].ViewXRange.Should().Be(chart.Instance.Series[0].GetViewXRange());
        zooms[0].PointerPosition.Should().Be(new SKPoint(320, 150));
        zooms[0].WheelEvent.DeltaY.Should().Be(-100);
    }

    [Fact]
    public async Task ZoomedView_RescalesPixelsAndResetViewRestoresThem() {
        var chart = RenderChart(Combine(NoAxes(), Recording("a", extra: Args(("Interactions", ChartInteractions.XZoom)))));
        using var surface = await Paint(chart);
        var plot = PlotArea(chart);
        var original = chart.Instance.ScaleX(1.5, plot);
        await chart.InvokeAsync(() => chart.Instance.OnNativeWheel(Wheel(100, 150, -100)));
        chart.Instance.ScaleX(1.5, plot).Should().NotBeApproximately(original, 0.5f);

        await chart.InvokeAsync(chart.Instance.ResetView);

        chart.Instance.ScaleX(1.5, plot).Should().BeApproximately(original, 0.01f);
        chart.Instance.Series.Single().GetViewXRange().Should().BeNull();
    }

    // ---------- view API ----------

    [Fact]
    public async Task SetViewXRange_AppliesToVisibleSeriesOnlyAndClearsYViewByDefault() {
        var chart = RenderChart(Combine(NoAxes(),
            Recording("a", extra: Args(("Interactions", ChartInteractions.ZoomAll))),
            Recording("b", extra: Args(("Interactions", ChartInteractions.ZoomAll), ("Visible", false)))));
        var visible = (RecordingSeries)chart.Instance.Series[0];
        using var surface = await Paint(chart);
        await chart.InvokeAsync(() => chart.Instance.OnNativeWheel(Wheel(320, 150, -100)));
        visible.GetViewYRange().Should().NotBeNull();

        await chart.InvokeAsync(() => chart.Instance.SetViewXRange(1, 2));

        visible.GetViewXRange().Should().Be((1d, 2d));
        visible.GetViewYRange().Should().BeNull();
        chart.Instance.Series[1].GetViewXRange().Should().BeNull();
    }

    [Fact]
    public async Task SetViewXRange_WithoutResettingY_KeepsTheYView() {
        var chart = RenderChart(Combine(NoAxes(), Recording("a", extra: Args(("Interactions", ChartInteractions.ZoomAll)))));
        using var surface = await Paint(chart);
        await chart.InvokeAsync(() => chart.Instance.OnNativeWheel(Wheel(320, 150, -100)));
        var yBefore = chart.Instance.Series.Single().GetViewYRange();

        await chart.InvokeAsync(() => chart.Instance.SetViewXRange(1, 2, resetYRange: false));

        chart.Instance.Series.Single().GetViewYRange().Should().Be(yBefore);
    }

    [Fact]
    public async Task HasViewRange_ReflectsXAndPerAxisYViewsOfVisibleSeries() {
        var chart = RenderChart(Combine(NoAxes(), YAxis(Args(("Visible", false))),
            Recording("a", extra: Args(("Interactions", ChartInteractions.ZoomAll))),
            Recording("b", extra: Args(("UseSecondaryYAxis", true), ("Interactions", ChartInteractions.ZoomAll)))));
        var xAxis = (NTComponents.Charts.Core.Axes.NTAxisOptions<Pt>)chart.Instance.XAxis;
        var primary = (NTComponents.Charts.Core.Axes.NTAxisOptions<Pt>)chart.Instance.YAxis;
        var secondary = (NTComponents.Charts.Core.Axes.NTAxisOptions<Pt>)chart.Instance.SecondaryYAxis!;
        chart.Instance.HasViewRange(xAxis).Should().BeFalse();

        await chart.InvokeAsync(() => ((RecordingSeries)chart.Instance.Series[1]).SetViewYRangeForTest(1m, 2m));

        chart.Instance.HasViewRange(secondary).Should().BeTrue();
        chart.Instance.HasViewRange(primary).Should().BeFalse();
        chart.Instance.HasViewRange(xAxis).Should().BeFalse();
        await chart.InvokeAsync(() => chart.Instance.SetViewXRange(0, 1, resetYRange: false));
        chart.Instance.HasViewRange(xAxis).Should().BeTrue();
    }

    [Fact]
    public async Task ResetView_ClearsEverySeriesAndRaisesResetCallbacks() {
        var resets = 0;
        var args = Args(("Interactions", ChartInteractions.ZoomAll), ("OnResetView", Cb<NTSeriesResetViewEventArgs<Pt>>(_ => resets++)));
        var chart = RenderChart(Combine(NoAxes(), Recording("a", extra: args)));
        using var surface = await Paint(chart);
        await chart.InvokeAsync(() => chart.Instance.OnNativeWheel(Wheel(320, 150, -100)));

        await chart.InvokeAsync(chart.Instance.ResetView);

        chart.WaitForAssertion(() => resets.Should().Be(1));
        chart.Instance.Series.Single().GetViewXRange().Should().BeNull();
        chart.Instance.Series.Single().GetViewYRange().Should().BeNull();
    }

    // ---------- toolbar / interop ----------

    [Fact]
    public void Toolbar_IsAbsentWithoutExportAndWithoutInteractions() {
        var chart = RenderChart(Combine(NoAxes(), Recording("a")));

        chart.FindAll(".nt-chart-buttons").Should().BeEmpty();
    }

    [Fact]
    public void Toolbar_ShowsResetButtonOnlyWhenPanOrZoomIsEnabled() {
        var withPan = RenderChart(Combine(NoAxes(), Recording("a", extra: Args(("Interactions", ChartInteractions.YPan)))));
        var without = Render<NTChart<Pt>>(p => p.Add(c => c.ChildContent, Combine(NoAxes(), Recording("a"))));
        without.Render(_ => { });

        withPan.FindAll("button[aria-label='Reset chart view']").Should().ContainSingle();
        withPan.FindAll("button[aria-label='Export chart as PNG']").Should().BeEmpty();
        without.FindAll("button[aria-label='Reset chart view']").Should().BeEmpty();
        without.FindAll("button[aria-label='Export chart as PNG']").Should().ContainSingle();
    }

    [Fact]
    public async Task ResetButton_ClearsTheZoomedView() {
        var chart = RenderChart(Combine(NoAxes(), Recording("a", extra: Args(("Interactions", ChartInteractions.XZoom)))));
        using var surface = await Paint(chart);
        await chart.InvokeAsync(() => chart.Instance.OnNativeWheel(Wheel(320, 150, -100)));
        chart.Instance.Series.Single().GetViewXRange().Should().NotBeNull();

        await Fire(chart, h => h.Click(), "button[aria-label='Reset chart view']");

        chart.Instance.Series.Single().GetViewXRange().Should().BeNull();
    }

    [Theory]
    [InlineData(ChartInteractions.None, false)]
    [InlineData(ChartInteractions.XPan, false)]
    [InlineData(ChartInteractions.XZoom, true)]
    [InlineData(ChartInteractions.YZoom, true)]
    [InlineData(ChartInteractions.All, true)]
    public void Initialization_RequestsPreventDefaultOnlyWhenZoomIsEnabled(ChartInteractions interactions, bool expected) {
        var chart = RenderChart(Combine(NoAxes(), Recording("a", extra: Args(("Interactions", interactions)))));

        chart.WaitForAssertion(() => Module.Invocations["initializeChart"].Should().ContainSingle());
        Module.Invocations["initializeChart"].Single().Arguments[2].Should().Be(expected);
    }
}
