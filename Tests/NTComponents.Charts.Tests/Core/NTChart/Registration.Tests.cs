using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Axes;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTChart_Registration_Tests : ChartTestBase {
    private static NTXAxisOptions<Pt, double> NewXAxis() => new() { ValueSelector = p => p.X };

    private static NTYAxisOptions<Pt, decimal> NewYAxis() => new() { ValueSelector = p => p.Y };

    [Fact]
    public void NewChart_UsesDefaultAxesWithoutSecondaryAxis() {
        using var chart = new NTChart<Pt>();

        chart.XAxis.Should().NotBeNull();
        chart.YAxis.Should().NotBeNull();
        chart.SecondaryYAxis.Should().BeNull();
        chart.XAxis.IsCategorical.Should().BeTrue("the default X axis treats values as categories");
    }

    [Fact]
    public void RegisterAxis_X_ReplacesTheDefaultAxis() {
        using var chart = new NTChart<Pt>();
        var axis = NewXAxis();

        chart.RegisterAxis(axis);

        chart.XAxis.Should().BeSameAs(axis);
    }

    [Fact]
    public void RegisterAxis_SecondXAxis_Throws() {
        using var chart = new NTChart<Pt>();
        chart.RegisterAxis(NewXAxis());

        var act = () => chart.RegisterAxis(NewXAxis());

        act.Should().Throw<InvalidOperationException>().WithMessage("*Only one X axis*");
    }

    [Fact]
    public void UnregisterAxis_X_RestoresTheDefaultAxis() {
        using var chart = new NTChart<Pt>();
        var defaultAxis = chart.XAxis;
        var axis = NewXAxis();
        chart.RegisterAxis(axis);

        chart.UnregisterAxis(axis);

        chart.XAxis.Should().BeSameAs(defaultAxis);
    }

    [Fact]
    public void UnregisterAxis_UnknownXAxis_LeavesRegisteredAxisInPlace() {
        using var chart = new NTChart<Pt>();
        var axis = NewXAxis();
        chart.RegisterAxis(axis);

        chart.UnregisterAxis(NewXAxis());

        chart.XAxis.Should().BeSameAs(axis);
    }

    [Fact]
    public void RegisterAxis_NullAxis_Throws() {
        using var chart = new NTChart<Pt>();

        var act = () => chart.RegisterAxis((INTXAxis<Pt>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RegisterAxis_FirstYAxisBecomesPrimaryAndSecondBecomesSecondary() {
        using var chart = new NTChart<Pt>();
        var first = NewYAxis();
        var second = NewYAxis();

        chart.RegisterAxis(first);
        chart.RegisterAxis(second);

        chart.YAxis.Should().BeSameAs(first);
        chart.SecondaryYAxis.Should().BeSameAs(second);
    }

    [Fact]
    public void RegisterAxis_ThirdYAxis_Throws() {
        using var chart = new NTChart<Pt>();
        chart.RegisterAxis(NewYAxis());
        chart.RegisterAxis(NewYAxis());

        var act = () => chart.RegisterAxis(NewYAxis());

        act.Should().Throw<InvalidOperationException>().WithMessage("*Only two Y axes*");
    }

    [Fact]
    public void UnregisterAxis_PrimaryY_PromotesTheSecondaryAxis() {
        using var chart = new NTChart<Pt>();
        var first = NewYAxis();
        var second = NewYAxis();
        chart.RegisterAxis(first);
        chart.RegisterAxis(second);

        chart.UnregisterAxis(first);

        chart.YAxis.Should().BeSameAs(second);
        chart.SecondaryYAxis.Should().BeNull();
    }

    [Fact]
    public void UnregisterAxis_SecondaryY_ClearsOnlyTheSecondaryAxis() {
        using var chart = new NTChart<Pt>();
        var first = NewYAxis();
        var second = NewYAxis();
        chart.RegisterAxis(first);
        chart.RegisterAxis(second);

        chart.UnregisterAxis(second);

        chart.YAxis.Should().BeSameAs(first);
        chart.SecondaryYAxis.Should().BeNull();
    }

    [Fact]
    public void UnregisterAxis_OnlyPrimaryY_RestoresTheDefaultAxis() {
        using var chart = new NTChart<Pt>();
        var defaultAxis = chart.YAxis;
        var axis = NewYAxis();
        chart.RegisterAxis(axis);

        chart.UnregisterAxis(axis);

        chart.YAxis.Should().BeSameAs(defaultAxis);
    }

    [Fact]
    public void AxisComponents_RegisterOnInitializeAndRestoreDefaultsWhenRemoved() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), YAxis(), YAxis(), Line()));
        chart.Instance.XAxis.Should().BeOfType<NTXAxisOptions<Pt, double>>();
        chart.Instance.YAxis.Should().BeOfType<NTYAxisOptions<Pt, decimal>>();
        chart.Instance.SecondaryYAxis.Should().NotBeNull();

        chart.Render(parameters => parameters.Add(p => p.ChildContent, Line()));

        chart.Instance.XAxis.Should().NotBeOfType<NTXAxisOptions<Pt, double>>();
        chart.Instance.XAxis.IsCategorical.Should().BeTrue();
        chart.Instance.SecondaryYAxis.Should().BeNull();
    }

    [Fact]
    public void RenderingTwoXAxes_Throws() {
        var act = () => RenderChart(Combine(XAxis<double>(p => p.X), XAxis<double>(p => p.X), Line()));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Only one X axis*");
    }

    [Fact]
    public void RenderingThreeYAxes_Throws() {
        var act = () => RenderChart(Combine(YAxis(), YAxis(), YAxis(), Line()));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Only two Y axes*");
    }

    [Fact]
    public void RenderingTwoLegends_Throws() {
        var act = () => RenderChart(Combine(Legend(), Legend(), Line()));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Only one legend*");
    }

    [Fact]
    public void Legend_IsExposedWhileMountedAndClearedWhenRemoved() {
        var chart = RenderChart(Combine(Legend(), Line()));
        chart.Instance.Legend.Should().NotBeNull();

        chart.Render(parameters => parameters.Add(p => p.ChildContent, Line()));

        chart.Instance.Legend.Should().BeNull();
    }

    [Fact]
    public async Task RegisterRenderable_SameInstanceTwice_RendersOncePerFrame() {
        var chart = RenderChart(Line());
        var stub = new StubRenderable(RenderOrdered.Tooltip, "stub");
        chart.Instance.RegisterRenderable(stub);
        chart.Instance.RegisterRenderable(stub);

        using var surface = await Paint(chart);

        stub.RenderCount.Should().Be(1);
    }

    [Fact]
    public async Task UnregisterRenderable_StopsRenderingAndInvalidating() {
        var chart = RenderChart(Line());
        var stub = new StubRenderable(RenderOrdered.Tooltip, "stub");
        chart.Instance.RegisterRenderable(stub);
        chart.Instance.UnregisterRenderable(stub);

        using var surface = await Paint(chart);
        chart.Instance.Invalidate();

        stub.RenderCount.Should().Be(0);
        stub.InvalidateCount.Should().Be(0);
    }

    [Fact]
    public void RegisterRenderable_Null_Throws() {
        using var chart = new NTChart<Pt>();

        var act = () => chart.RegisterRenderable(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Invalidate_PropagatesToEveryRegisteredRenderable() {
        var chart = RenderChart(Line());
        var tooltip = new StubRenderable(RenderOrdered.Tooltip, "t");
        var legend = new StubRenderable(RenderOrdered.Legend, "l");
        chart.Instance.RegisterRenderable(tooltip);
        chart.Instance.RegisterRenderable(legend);

        chart.Instance.Invalidate();

        tooltip.InvalidateCount.Should().Be(1);
        legend.InvalidateCount.Should().Be(1);
    }
}
