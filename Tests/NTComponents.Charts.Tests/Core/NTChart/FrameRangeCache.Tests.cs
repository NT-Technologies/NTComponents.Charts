using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

/// <summary>
///     Ranges are memoized while a frame paints. These tests pin down when that memo must be discarded.
/// </summary>
public class NTChart_FrameRangeCache_Tests : ChartTestBase {
    [Fact]
    public async Task ViewChangedDuringAFrame_IsSeenByLaterRangeQueriesInTheSameFrame() {
        // Arrange: hit testing runs mid-frame, after the axes have already queried (and memoized) the ranges.
        IRenderedComponent<NTChart<Pt>> chart = null!;
        (double Min, double Max)? before = null;
        (double Min, double Max)? after = null;
        chart = RenderChart(Combine(XAxis<double>(p => p.X), Recording("a", hit: (_, _) => {
            if (before is null) {
                before = chart.Instance.GetXRange(null, padded: true);
                chart.Instance.SetViewXRange(1, 2);
                after = chart.Instance.GetXRange(null, padded: true);
            }
            return null;
        })));
        using var surface = await Paint(chart);
        await Move(chart, Width / 2, Height / 2);

        // Act
        await Paint(chart, surface: surface);

        // Assert
        before.Should().NotBe((1d, 2d));
        after.Should().Be((1d, 2d));
    }

    [Fact]
    public async Task ViewChangedBetweenFrames_IsUsedByTheNextFrame() {
        // Arrange: record the range seen mid-frame (during hit testing) in each frame.
        IRenderedComponent<NTChart<Pt>> chart = null!;
        var seen = new List<(double Min, double Max)>();
        chart = RenderChart(Combine(XAxis<double>(p => p.X), Recording("a", hit: (_, _) => {
            seen.Add(chart.Instance.GetXRange(null, padded: true));
            return null;
        })));
        using var surface = await Paint(chart);
        await Move(chart, Width / 2, Height / 2);
        await Paint(chart, surface: surface);

        // Act
        await chart.InvokeAsync(() => chart.Instance.SetViewXRange(1, 2));
        await Paint(chart, surface: surface);

        // Assert
        seen.Should().HaveCount(2);
        seen[0].Should().NotBe((1d, 2d));
        seen[1].Should().Be((1d, 2d));
    }
}
