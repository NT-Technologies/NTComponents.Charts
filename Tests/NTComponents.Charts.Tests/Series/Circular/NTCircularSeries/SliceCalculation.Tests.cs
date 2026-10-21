using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.Series.Circular;

public class SliceCalculation_Tests : CircularChartTestBase {
    private readonly NTChart<Item> _chart = new();

    [Fact]
    public void PositiveValues_SweepIsProportionalAndStartsAtTwelveOClock() {
        using var series = new ProbeCircularSeries(_chart, Items(1, 2, 1));

        var slices = series.Slices;

        slices.Select(s => s.Index).Should().Equal(0, 1, 2);
        slices.Select(s => s.SweepAngle).Should().BeEquivalentTo([90f, 180f, 90f], o => o.WithStrictOrdering());
        slices.Select(s => s.StartAngle).Should().BeEquivalentTo([-90f, 0f, 180f], o => o.WithStrictOrdering());
        slices.Sum(s => s.SweepAngle).Should().BeApproximately(360f, 0.001f);
        slices.Select(s => s.Value).Should().Equal(1f, 2f, 1f);
    }

    [Fact]
    public void SingleItem_FillsTheWholeCircle() {
        using var series = new ProbeCircularSeries(_chart, Items(42));

        var slice = series.Slices.Should().ContainSingle().Subject;

        slice.StartAngle.Should().Be(-90f);
        slice.SweepAngle.Should().BeApproximately(360f, 0.001f);
    }

    [Fact]
    public void NegativeValue_IsTreatedAsZeroAndOthersShareTheCircle() {
        using var series = new ProbeCircularSeries(_chart, Items(3, -5, 1));

        var slices = series.Slices;

        slices.Select(s => s.SweepAngle).Should().BeEquivalentTo([270f, 0f, 90f], o => o.WithStrictOrdering());
        slices[1].Value.Should().Be(0f);
        slices[2].StartAngle.Should().BeApproximately(180f, 0.001f);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AllValuesNonPositive_ProduceNoSlices(int value) {
        using var series = new ProbeCircularSeries(_chart, Items(value, value, value));

        series.Slices.Should().BeEmpty();
    }

    [Fact]
    public void EmptyData_ProducesNoSlices() {
        using var series = new ProbeCircularSeries(_chart, []);

        series.Slices.Should().BeEmpty();
    }

    [Fact]
    public void NullData_ProducesNoSlicesAndNoLegendItems() {
        using var series = new ProbeCircularSeries(_chart, Items(1, 2)) { Data = null! };
        series.ApplyParameters();

        series.Slices.Should().BeEmpty();
        LegendItems(series).Should().BeEmpty();
    }

    [Fact]
    public void HiddenIndex_IsExcludedAndRemainingSlicesRenormalizeKeepingOriginalIndices() {
        using var series = new ProbeCircularSeries(_chart, Items(1, 1, 2));

        ToggleLegend(series, 2);
        var slices = series.Slices;

        slices.Select(s => s.Index).Should().Equal(0, 1);
        slices.Select(s => s.SweepAngle).Should().BeEquivalentTo([180f, 180f], o => o.WithStrictOrdering());
    }

    [Fact]
    public void TogglingHiddenIndexAgain_RestoresTheSlice() {
        using var series = new ProbeCircularSeries(_chart, Items(1, 1, 2));

        ToggleLegend(series, 2);
        _ = series.Slices;
        ToggleLegend(series, 2);

        series.Slices.Select(s => s.SweepAngle).Should().BeEquivalentTo([90f, 90f, 180f], o => o.WithStrictOrdering());
    }

    [Fact]
    public void AllIndicesHidden_ProducesNoSlices() {
        using var series = new ProbeCircularSeries(_chart, Items(1, 2));

        ToggleLegend(series, 0);
        ToggleLegend(series, 1);

        series.Slices.Should().BeEmpty();
    }

    [Fact]
    public void UnchangedData_DoesNotReevaluateValueSelector() {
        var calls = 0;
        using var series = new ProbeCircularSeries(_chart, Items(1, 2, 3)) { ValueSelector = item => { calls++; return item.Value; } };
        series.ApplyParameters();
        _ = series.Slices;
        var firstPass = calls;

        _ = series.Slices;

        firstPass.Should().BeGreaterThan(0);
        calls.Should().Be(firstPass);
    }

    [Fact]
    public void InPlaceMutation_IsNotSeenUntilInvalidate() {
        var data = new List<Item> { new("A", 1), new("B", 1) };
        using var series = new ProbeCircularSeries(_chart, data);
        series.Slices.Select(s => s.SweepAngle).Should().Equal(180f, 180f);

        data[0] = data[0] with { Value = 3 };
        series.Slices.Select(s => s.SweepAngle).Should().Equal(180f, 180f);

        series.Invalidate();

        series.Slices.Select(s => s.SweepAngle).Should().BeEquivalentTo([270f, 90f], o => o.WithStrictOrdering());
    }

    [Fact]
    public void ReplacedValueSelector_RecomputesSlices() {
        using var series = new ProbeCircularSeries(_chart, new[] { new Item("A", 1), new Item("B", 3) });
        series.Slices.Select(s => s.SweepAngle).Should().Equal(90f, 270f);

        series.ValueSelector = item => item.Name == "A" ? 3 : 1;
        series.ApplyParameters();

        series.Slices.Select(s => s.SweepAngle).Should().Equal(270f, 90f);
    }

    [Fact]
    public void Invalidate_KeepsHiddenIndices() {
        var data = new List<Item> { new("A", 1), new("B", 1), new("C", 2) };
        using var series = new ProbeCircularSeries(_chart, data);
        ToggleLegend(series, 2);

        series.Invalidate();

        series.Slices.Select(s => s.Index).Should().Equal(0, 1);
    }

    [Fact]
    public void ReplacingDataReference_ResetsHiddenIndices() {
        using var series = new ProbeCircularSeries(_chart, Items(1, 1, 2));
        ToggleLegend(series, 2);
        series.Slices.Should().HaveCount(2);

        series.Data = Items(1, 1, 2);
        series.ApplyParameters();

        series.Slices.Select(s => s.Index).Should().Equal(0, 1, 2);
    }

    [Fact]
    public void Slices_ExposeTheBoundDataItem() {
        var items = Items(1, 2);
        using var series = new ProbeCircularSeries(_chart, items);

        series.Slices.Select(s => s.Data).Should().Equal(items);
    }
}
