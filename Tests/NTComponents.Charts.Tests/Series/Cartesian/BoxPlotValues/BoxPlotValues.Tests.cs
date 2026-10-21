using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class BoxPlotValues_Tests {
    [Fact]
    public void Constructor_ExposesEveryStatisticAndDefaultsOutliersToNull() {
        var values = new BoxPlotValues(1m, 2m, 3m, 4m, 5m);

        values.Min.Should().Be(1m);
        values.Q1.Should().Be(2m);
        values.Median.Should().Be(3m);
        values.Q3.Should().Be(4m);
        values.Max.Should().Be(5m);
        values.Outliers.Should().BeNull();
    }

    [Fact]
    public void WithOutliers_KeepsTheOutlierValuesInOrder() {
        var values = new BoxPlotValues(1m, 2m, 3m, 4m, 5m, [9m, -2m]);

        values.Outliers.Should().Equal(9m, -2m);
    }

    [Fact]
    public void Equality_ComparesStatisticsByValue() {
        new BoxPlotValues(1m, 2m, 3m, 4m, 5m).Should().Be(new BoxPlotValues(1m, 2m, 3m, 4m, 5m));
        new BoxPlotValues(1m, 2m, 3m, 4m, 5m).Should().NotBe(new BoxPlotValues(1m, 2m, 3m, 4m, 6m));
    }

    [Fact]
    public void WithExpression_CopiesAllOtherValues() {
        var original = new BoxPlotValues(1m, 2m, 3m, 4m, 5m, [7m]);

        var changed = original with { Median = 3.5m };

        changed.Median.Should().Be(3.5m);
        changed.Min.Should().Be(1m);
        changed.Max.Should().Be(5m);
        changed.Outliers.Should().BeSameAs(original.Outliers);
    }
}
