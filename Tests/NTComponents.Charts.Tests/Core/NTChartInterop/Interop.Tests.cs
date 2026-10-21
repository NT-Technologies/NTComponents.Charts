using Bunit.JSInterop;
using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTChartInterop_Tests : ChartTestBase {
    private static RenderFragment Content(ChartInteractions interactions) => Combine(
        XAxis<double>(p => p.X, Args(("Visible", false))), YAxis(Args(("Visible", false))),
        Recording("a", extra: Args(("Interactions", interactions))));

    private BunitJSModuleInterop SetupListeners() {
        var listeners = Module.SetupModule("initializeChart", _ => true);
        listeners.SetupVoid("updateInteractions", _ => true).SetVoidResult();
        listeners.SetupVoid("dispose", _ => true).SetVoidResult();
        return listeners;
    }

    private static int Calls(BunitJSModuleInterop listeners, string identifier) =>
        listeners.Invocations.Where(i => i.Identifier == identifier).Count();

    [Fact]
    public void Initialization_ImportsTheModuleAndAttachesListenersWithThePreventDefaultFlag() {
        SetupListeners();

        var chart = RenderChart(Content(ChartInteractions.XZoom));

        chart.WaitForAssertion(() => Module.Invocations["initializeChart"].Should().ContainSingle());
        var call = Module.Invocations["initializeChart"].Single();
        call.Arguments.Should().HaveCount(3);
        call.Arguments[2].Should().Be(true);
        Module.Invocations["getDevicePixelRatio"].Should().ContainSingle();
        Module.Invocations["getThemeColors"].Should().NotBeEmpty();
    }

    [Fact]
    public void ThemeColorRequest_ListsEveryThemeColorToken() {
        SetupListeners();

        RenderChart(Content(ChartInteractions.None));

        var requested = (string[])Module.Invocations["getThemeColors"].First().Arguments[0]!;
        requested.Should().BeEquivalentTo(Enum.GetNames<NTColor>());
    }

    [Fact]
    public void TogglingZoomInteractions_UpdatesTheListenersOnlyWhenTheStateChanges() {
        var listeners = SetupListeners();
        var chart = RenderChart(Content(ChartInteractions.None));
        chart.WaitForAssertion(() => Module.Invocations["initializeChart"].Should().ContainSingle());

        chart.Render(p => p.Add(c => c.ChildContent, Content(ChartInteractions.XZoom)));
        chart.WaitForAssertion(() => Calls(listeners, "updateInteractions").Should().Be(1));
        listeners.Invocations.Single(i => i.Identifier == "updateInteractions").Arguments.Single().Should().Be(true);

        chart.Render(p => p.Add(c => c.ChildContent, Content(ChartInteractions.ZoomAll)));
        Calls(listeners, "updateInteractions").Should().Be(1, "zoom stays enabled so nothing changes for the listeners");

        chart.Render(p => p.Add(c => c.ChildContent, Content(ChartInteractions.XPan)));
        chart.WaitForAssertion(() => Calls(listeners, "updateInteractions").Should().Be(2));
        listeners.Invocations.Last(i => i.Identifier == "updateInteractions").Arguments.Single().Should().Be(false);
    }

    [Fact]
    public async Task Disposal_DetachesTheListenersExactlyOnce() {
        var listeners = SetupListeners();
        var chart = RenderChart(Content(ChartInteractions.None));
        chart.WaitForAssertion(() => Module.Invocations["initializeChart"].Should().ContainSingle());

        await chart.InvokeAsync(() => chart.Instance.DisposeAsync().AsTask());
        await chart.InvokeAsync(() => chart.Instance.DisposeAsync().AsTask());

        Calls(listeners, "dispose").Should().Be(1);
    }
}
