using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTChart_AccessibleName_Tests : ChartTestBase {
    private static RenderFragment NoAxes() => Combine(XAxis<double>(p => p.X, Args(("Visible", false))), YAxis(Args(("Visible", false))));

    [Fact]
    public void CanvasHost_IsAnImageNamedAfterTheChartTitle() {
        var chart = RenderChart(Combine(NoAxes(), Line()), p => p.Add(c => c.TitleOptions, new NTTitleOptions("Quarterly revenue")));

        var host = Host(chart);

        host.GetAttribute("role").Should().Be("img");
        host.GetAttribute("aria-label").Should().Be("Quarterly revenue");
    }

    [Fact]
    public void WithoutATitle_TheCanvasHostHasNoAccessibleNameAttribute() {
        var chart = RenderChart(Combine(NoAxes(), Line()));

        Host(chart).GetAttribute("aria-label").Should().BeNull();
    }

    [Fact]
    public void ToolbarButtons_StayOutsideTheImageRole() {
        var chart = Render<NTChart<Pt>>(p => p.Add(c => c.AllowExport, true).Add(c => c.ChildContent, Combine(NoAxes(), Line())));

        chart.FindAll("button").Should().NotBeEmpty();
        Host(chart).QuerySelectorAll("button").Should().BeEmpty("a role=img element hides its descendants from assistive technology");
    }
}
