using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

/// <summary>
///     Hover is painted on the canvas; the component markup only carries the cursor, so it re-renders only when the
///     cursor changes.
/// </summary>
public class NTChart_HoverRendering_Tests : ChartTestBase {
    private static RenderFragment NoAxes() => Combine(XAxis<double>(p => p.X, Args(("Visible", false))), YAxis(Args(("Visible", false))));

    // Only series that react to a click show the pointer cursor.
    private Dictionary<string, object> Clickable() => Args(("OnClick", EventCallback.Factory.Create<NTComponents.Charts.Core.Series.NTSeriesClickEventArgs<Pt>>(this, _ => { })));

    [Fact]
    public async Task MovingBetweenPointsOfAHoveredSeries_DoesNotReRenderTheMarkup() {
        // Arrange: the left half of the plot hits point 0, the right half point 2.
        var chart = RenderChart(Combine(NoAxes(), Recording("a", hit: (point, area) => point.X < area.MidX ? (0, Sample[0]) : (2, Sample[2]), extra: Clickable())));
        using var surface = await Paint(chart);
        await Move(chart, Width * 0.25, Height / 2);
        await Paint(chart, surface: surface);
        var renders = chart.RenderCount;

        // Act
        await Move(chart, Width * 0.75, Height / 2);
        await Paint(chart, surface: surface);

        // Assert
        chart.Instance.HoveredPointIndex.Should().Be(2);
        Host(chart).GetAttribute("style").Should().Contain("cursor: pointer;");
        chart.RenderCount.Should().Be(renders);
    }

    [Fact]
    public async Task EnteringAndLeavingAHit_ReRendersOnceForEachCursorChange() {
        var hit = true;
        var chart = RenderChart(Combine(NoAxes(), Recording("a", hit: (_, _) => hit ? (1, Sample[1]) : null, extra: Clickable())));
        using var surface = await Paint(chart);
        var renders = chart.RenderCount;

        await Move(chart, Width / 2, Height / 2);
        await Paint(chart, surface: surface);
        chart.RenderCount.Should().Be(renders + 1);

        hit = false;
        await Move(chart, (Width / 2) + 10, Height / 2);
        for (var i = 0; i < 3; i++) {
            await Paint(chart, surface: surface);
        }

        chart.RenderCount.Should().Be(renders + 2);
        Host(chart).GetAttribute("style").Should().Contain("cursor: default;");
    }
}
