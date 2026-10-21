using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTChart_Validation_Tests : ChartTestBase {
    private static RenderFragment Pie() => builder => {
        builder.OpenComponent<NTPieSeries<Pt>>(0);
        builder.AddAttribute(1, "Data", Sample);
        builder.AddAttribute(2, "XValue", (Func<Pt, object>)(p => p.Name));
        builder.AddAttribute(3, "ValueSelector", (Func<Pt, decimal>)(p => p.Y));
        builder.AddAttribute(4, "AnimationEnabled", false);
        builder.CloseComponent();
    };

    [Fact]
    public void SeriesFromDifferentCoordinateSystems_ThrowOnRegistration() {
        var act = () => RenderChart(Combine(Line(), Pie()));

        act.Should().Throw<InvalidOperationException>().WithMessage("*same coordinate system*");
    }

    [Fact]
    public void SeriesWithDifferentXValueTypes_ThrowOnRegistration() {
        var act = () => RenderChart(Combine(Line(x: p => p.X), Line(x: p => p.Name)));

        act.Should().Throw<InvalidOperationException>().WithMessage("*same X value type*");
    }

    [Fact]
    public void SeriesWithTheSameXValueType_AreAccepted() {
        var chart = RenderChart(Combine(Line(), Bar()));

        chart.Instance.Series.Should().HaveCount(2);
    }

    [Fact]
    public void HorizontalBarsMixedWithOtherSeries_ThrowOnRegistration() {
        var act = () => RenderChart(Combine(Bar(x: p => p.Name, extra: Args(("Orientation", NTChartOrientation.Horizontal))), Line(x: p => p.Name)));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Horizontal bar series can only be combined*");
    }

    [Fact]
    public void HorizontalBarsMixedWithVerticalBars_ThrowOnRegistration() {
        var act = () => RenderChart(Combine(Bar(x: p => p.Name, extra: Args(("Orientation", NTChartOrientation.Horizontal))), Bar(x: p => p.Name)));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Horizontal bar series can only be combined*");
    }

    [Fact]
    public void MultipleHorizontalBarSeries_AreAccepted() {
        var horizontal = Args(("Orientation", NTChartOrientation.Horizontal));

        var chart = RenderChart(Combine(Bar(x: p => p.Name, extra: horizontal), Bar(x: p => p.Name, extra: horizontal)));

        chart.Instance.Series.Should().HaveCount(2);
    }

    [Fact]
    public void CartesianSeriesWithDifferentInteractions_ThrowOnRegistration() {
        var act = () => RenderChart(Combine(Line(extra: Args(("Interactions", ChartInteractions.XPan))), Line(extra: Args(("Interactions", ChartInteractions.None)))));

        act.Should().Throw<InvalidOperationException>().WithMessage("*same interaction configurations*");
    }

    [Fact]
    public void CartesianSeriesWithMatchingInteractions_AreAccepted() {
        var interactions = Args(("Interactions", ChartInteractions.All));

        var chart = RenderChart(Combine(Line(extra: interactions), Bar(extra: interactions)));

        chart.Instance.Series.Should().HaveCount(2);
    }

    [Fact]
    public void DisposedSeries_AreRemovedAndNoLongerAffectRanges() {
        var other = new Pt[] { new("E", 50, 900m), new("F", 60, 950m) };
        var chart = RenderChart(Combine(Line(), Line(other)));
        chart.Instance.GetYRange(null, padded: false).Max.Should().Be(950m);

        chart.Render(parameters => parameters.Add(p => p.ChildContent, Line()));

        chart.Instance.Series.Should().ContainSingle();
        chart.Instance.GetYRange(null, padded: false).Max.Should().Be(40m);
    }
}
