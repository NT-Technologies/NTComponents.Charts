using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTChart_ValueConversion_Tests : ChartTestBase {
    private static readonly DateTime Jan = new(2024, 1, 15);
    private static readonly DateTime Feb = new(2024, 2, 20);
    private static readonly DateTime Mar = new(2024, 3, 25);

    private static Pt[] Dated() => [new("a", 0, 5m, Feb), new("b", 1, 7m, Jan), new("c", 2, 3m, Mar)];

    [Fact]
    public void GetScaledXValue_NullValue_ReturnsZero() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line()));

        chart.Instance.GetScaledXValue(null).Should().Be(0);
    }

    [Theory]
    [InlineData(2.5, 2.5)]
    [InlineData(7, 7)]
    [InlineData(-3, -3)]
    public void GetScaledXValue_NumericAxis_ReturnsTheNumericValue(object value, double expected) {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line()));

        chart.Instance.GetScaledXValue(value).Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void GetScaledXValue_DateTimeOnDateAxis_ReturnsTicks() {
        var chart = RenderChart(Combine(XAxis<DateTime>(p => p.Date), Line(Dated(), x: p => p.Date)));

        chart.Instance.GetScaledXValue(Jan).Should().Be(Jan.Ticks);
    }

    [Fact]
    public void GetScaledXValue_NonConvertibleObjectOnNumericAxis_ReturnsZero() {
        var chart = RenderChart(Combine(XAxis<double>(p => p.X), Line()));

        chart.Instance.GetScaledXValue(new object()).Should().Be(0);
    }

    [Fact]
    public void GetScaledXValue_CategoricalAxis_ReturnsIndexInFirstSeenOrder() {
        var data = new Pt[] { new("Cherry", 0, 1m), new("Apple", 1, 2m), new("Banana", 2, 3m) };
        var chart = RenderChart(Line(data, x: p => p.Name));

        chart.Instance.GetScaledXValue("Cherry").Should().Be(0);
        chart.Instance.GetScaledXValue("Apple").Should().Be(1);
        chart.Instance.GetScaledXValue("Banana").Should().Be(2);
    }

    [Fact]
    public void GetScaledXValue_UnknownCategory_ReturnsZero() {
        var chart = RenderChart(Line(x: p => p.Name));

        chart.Instance.GetScaledXValue("Nope").Should().Be(0);
    }

    [Fact]
    public void GetScaledYValue_HandlesNullDatesAndNumbers() {
        var chart = RenderChart(Line());

        chart.Instance.GetScaledYValue(null).Should().Be(0m);
        chart.Instance.GetScaledYValue(12.5m).Should().Be(12.5m);
        chart.Instance.GetScaledYValue(4).Should().Be(4m);
        chart.Instance.GetScaledYValue(Jan).Should().Be(Jan.Ticks);
        chart.Instance.GetScaledYValue(new object()).Should().Be(0m);
    }

    [Fact]
    public void GetAllXValues_ReturnsDistinctValuesInFirstSeenOrder() {
        var data = new Pt[] { new("C", 0, 1m), new("A", 1, 2m), new("B", 2, 3m), new("A", 3, 4m) };
        var chart = RenderChart(Line(data, x: p => p.Name));

        chart.Instance.GetAllXValues().Should().Equal("C", "A", "B");
    }

    [Fact]
    public void GetAllXValues_MergesCategoriesAcrossSeries() {
        var second = new Pt[] { new("D", 0, 1m), new("A", 1, 2m) };
        var chart = RenderChart(Combine(Line(x: p => p.Name), Line(second, x: p => p.Name)));

        chart.Instance.GetAllXValues().Should().Equal("A", "B", "C", "D");
    }

    [Fact]
    public void GetAllXValues_ExcludesHiddenSeries() {
        var hidden = new Pt[] { new("Z", 0, 1m) };
        var chart = RenderChart(Combine(Line(x: p => p.Name), Line(hidden, x: p => p.Name, extra: Args(("Visible", false)))));

        chart.Instance.GetAllXValues().Should().Equal("A", "B", "C", "D");
    }

    [Fact]
    public void GetAllXValues_IsStableBetweenCallsAndRefreshedWhenSeriesDataChanges() {
        var chart = RenderChart(Line(x: p => p.Name));
        var first = chart.Instance.GetAllXValues();
        chart.Instance.GetAllXValues().Should().BeSameAs(first, "unchanged data must not be recomputed");

        chart.Render(parameters => parameters.Add(p => p.ChildContent, Line([new("Q", 0, 1m), new("R", 1, 2m)], x: p => p.Name)));

        chart.Instance.GetAllXValues().Should().Equal("Q", "R");
    }

    [Fact]
    public void GetScaledXValue_CategoryIndexMapRefreshesWhenCategoriesChange() {
        var chart = RenderChart(Line(x: p => p.Name));
        chart.Instance.GetScaledXValue("D").Should().Be(3);

        chart.Render(parameters => parameters.Add(p => p.ChildContent, Line([new("D", 0, 1m), new("A", 1, 2m)], x: p => p.Name)));

        chart.Instance.GetScaledXValue("D").Should().Be(0);
        chart.Instance.GetScaledXValue("A").Should().Be(1);
    }

    [Fact]
    public void GetAllXValues_ReincludesSeriesWhenItBecomesVisible() {
        var hidden = new Pt[] { new("Z", 0, 1m) };
        var chart = RenderChart(Combine(Line(x: p => p.Name), Line(hidden, x: p => p.Name, extra: Args(("Visible", false)))));
        chart.Instance.GetAllXValues().Should().NotContain("Z");

        chart.Render(parameters => parameters.Add(p => p.ChildContent, Combine(Line(x: p => p.Name), Line(hidden, x: p => p.Name, extra: Args(("Visible", true))))));

        chart.Instance.GetAllXValues().Should().Contain("Z");
    }

    [Fact]
    public void GetAllYValues_ReturnsSortedDistinctValues() {
        var data = new Pt[] { new("A", 0, 30m), new("B", 1, 10m), new("C", 2, 30m), new("D", 3, 20m) };
        var chart = RenderChart(Line(data));

        chart.Instance.GetAllYValues().Should().Equal(10m, 20m, 30m);
    }

    [Fact]
    public void IsXAxisDateTime_DateXValues_IsTrue() {
        var chart = RenderChart(Combine(XAxis<DateTime>(p => p.Date), Line(Dated(), x: p => p.Date)));

        chart.Instance.IsXAxisDateTime.Should().BeTrue();
    }

    [Fact]
    public void IsXAxisDateTime_NumericAndCategoryXValues_IsFalse() {
        RenderChart(Combine(XAxis<double>(p => p.X), Line())).Instance.IsXAxisDateTime.Should().BeFalse();
        RenderChart(Line(x: p => p.Name)).Instance.IsXAxisDateTime.Should().BeFalse();
    }

    [Fact]
    public void IsXAxisDateTime_WithoutSeriesOrData_IsFalse() {
        RenderChart(_ => { }).Instance.IsXAxisDateTime.Should().BeFalse();
        RenderChart(Line([], x: p => p.Date)).Instance.IsXAxisDateTime.Should().BeFalse();
    }

    [Fact]
    public void DateAxis_GetXRangeUsesTicksAndPadsByRangePadding() {
        var chart = RenderChart(Combine(XAxis<DateTime>(p => p.Date), Line(Dated(), x: p => p.Date)));

        var (min, max) = chart.Instance.GetXRange(null, padded: false);
        var padded = chart.Instance.GetXRange(null, padded: true);

        min.Should().Be(Jan.Ticks);
        max.Should().Be(Mar.Ticks);
        padded.Min.Should().BeApproximately(Jan.Ticks - ((Mar.Ticks - Jan.Ticks) * 0.05), 1024);
        padded.Max.Should().BeApproximately(Mar.Ticks + ((Mar.Ticks - Jan.Ticks) * 0.05), 1024);
    }
}
