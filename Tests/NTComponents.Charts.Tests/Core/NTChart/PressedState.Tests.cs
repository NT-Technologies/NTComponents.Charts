using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTChart_PressedState_Tests : ChartTestBase {
    private static RenderFragment NoAxes() => Combine(XAxis<double>(p => p.X, Args(("Visible", false))), YAxis(Args(("Visible", false))));

    private static (int Index, Pt? Data)? Hit(SKPoint _, SKRect __) => (1, Sample[1]);

    private async Task<IRenderedComponent<NTChart<Pt>>> HoveredChart() {
        var chart = RenderChart(Combine(NoAxes(), Recording("a", hit: Hit)));
        using var surface = await Paint(chart);
        await Move(chart, 300, 150);
        await Paint(chart, surface: surface);
        return chart;
    }

    [Fact]
    public async Task MouseDownOverAHoveredSeries_MarksItPressedAndMouseUpClearsIt() {
        var chart = await HoveredChart();
        var series = chart.Instance.Series.Single();

        await Fire(chart, h => h.MouseDown(new MouseEventArgs { OffsetX = 300, OffsetY = 150 }));
        series.IsPressed.Should().BeTrue();

        await Fire(chart, h => h.MouseUp(new MouseEventArgs { OffsetX = 300, OffsetY = 150 }));
        series.IsPressed.Should().BeFalse();
    }

    [Fact]
    public async Task LeavingTheChartWhilePressed_ClearsThePressedState() {
        var chart = await HoveredChart();
        var series = chart.Instance.Series.Single();
        await Fire(chart, h => h.MouseDown(new MouseEventArgs { OffsetX = 300, OffsetY = 150 }));

        await Fire(chart, h => h.MouseLeave());

        series.IsPressed.Should().BeFalse();
    }

    [Fact]
    public async Task MouseDownWithNothingHovered_PressesNothing() {
        var chart = RenderChart(Combine(NoAxes(), Recording("a", hit: (_, _) => null)));
        using var surface = await Paint(chart);

        await Fire(chart, h => h.MouseDown(new MouseEventArgs { OffsetX = 300, OffsetY = 150 }));

        chart.Instance.Series.Single().IsPressed.Should().BeFalse();
    }
}
