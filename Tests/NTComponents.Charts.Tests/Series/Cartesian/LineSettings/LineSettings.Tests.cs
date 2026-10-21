using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Tests.Series.Cartesian;

/// <summary>The settings enums are part of the public parameter surface and are cycled by index, so their members must stay stable.</summary>
public class LineSettings_Tests {
    [Fact]
    public void LineStyle_MembersAreSolidDashedNoneInThatOrder() {
        Enum.GetNames<LineStyle>().Should().Equal("Solid", "Dashed", "None");
        default(LineStyle).Should().Be(LineStyle.Solid);
    }

    [Fact]
    public void LineInterpolation_MembersAreStraightSmoothedCurvedStep() {
        Enum.GetNames<LineInterpolation>().Should().Equal("Straight", "Smoothed", "Curved", "Step");
        default(LineInterpolation).Should().Be(LineInterpolation.Straight);
    }

    [Fact]
    public void AggregationMode_MembersAreNoneAverageSumMinMaxMedian() {
        Enum.GetNames<AggregationMode>().Should().Equal("None", "Average", "Sum", "Min", "Max", "Median");
    }

    [Fact]
    public void PointStyle_MembersAreNoneFilledOutlined() {
        Enum.GetNames<PointStyle>().Should().Equal("None", "Filled", "Outlined");
    }

    [Fact]
    public void PointShape_HasFourContiguousShapesStartingAtCircle() {
        Enum.GetNames<PointShape>().Should().Equal("Circle", "Square", "Triangle", "Diamond");
        Enum.GetValues<PointShape>().Select(v => (int)v).Should().Equal(0, 1, 2, 3);
    }

    [Fact]
    public void SeriesDefaults_MatchTheDocumentedParameterDefaults() {
        var line = new NTLineSeries<Pt>();
        var bar = new NTBarSeries<Pt>();

        line.LineStyle.Should().Be(LineStyle.Solid);
        line.Interpolation.Should().Be(LineInterpolation.Straight);
        line.AggregationMode.Should().Be(AggregationMode.Average);
        line.EnableAggregation.Should().BeFalse();
        line.AggregationThreshold.Should().Be(500);
        line.StrokeWidth.Should().Be(2f);
        line.PointStyle.Should().Be(PointStyle.Filled);
        line.PointShape.Should().BeNull();
        line.PointSize.Should().Be(8f);
        bar.PointStyle.Should().Be(PointStyle.Filled);
    }
}
