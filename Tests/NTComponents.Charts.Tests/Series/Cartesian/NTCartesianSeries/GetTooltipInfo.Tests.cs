using System.Globalization;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class GetTooltipInfo_Tests : CartesianChartTestBase {
    [Fact]
    public void WithNumericX_UsesXAsHeaderAndFormattedYAsValue() {
        var data = new[] { new Pt(1, 10m), new Pt(2, 30m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("Title", "Revenue"), ("Color", NTColor.Primary)));

        var info = Tooltip(SeriesOf<NTLineSeries<Pt>>(cut), data[1]);

        info.Header.Should().Be("2");
        info.Lines.Should().ContainSingle();
        info.Lines[0].Label.Should().Be("Revenue");
        info.Lines[0].Value.Should().Be("30");
        info.Lines[0].Color.Should().Be(Red);
    }

    [Fact]
    public void WithoutTitle_LabelsTheLineSeries() {
        var data = new[] { new Pt(1, 10m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data));

        Tooltip(SeriesOf<NTLineSeries<Pt>>(cut), data[0]).Lines[0].Label.Should().Be("Series");
    }

    [Fact]
    public void WithCategoricalX_UsesCategoryNameAsHeader() {
        var data = new[] { new Pt("North", 10m), new Pt("South", 30m) };
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTLineSeries<Pt>>(data));

        Tooltip(SeriesOf<NTLineSeries<Pt>>(cut), data[1]).Header.Should().Be("South");
    }

    [Fact]
    public void WithDateTimeX_FormatsHeaderAsDate() {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try {
            var data = new[] { new Pt(new DateTime(2024, 3, 5), 10m), new Pt(new DateTime(2024, 3, 6), 20m) };
            var cut = RenderChart(XAxisKind.DateTime, SeriesFragment<NTLineSeries<Pt>>(data));

            Tooltip(SeriesOf<NTLineSeries<Pt>>(cut), data[0]).Header.Should().Be("03/05/2024");
        }
        finally {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void WithAxisLabelFormat_FormatsHeaderWithThatFormat() {
        var data = new[] { new Pt(5, 10m) };
        var setup = new ChartSetup { XAxisAttributes = [("LabelFormat", "Week {0}")] };
        var cut = RenderChart(setup, SeriesFragment<NTLineSeries<Pt>>(data));

        Tooltip(SeriesOf<NTLineSeries<Pt>>(cut), data[0]).Header.Should().Be("Week 5");
    }

    [Theory]
    [InlineData("{0:0.#}", "12.3")]
    [InlineData("{0:F2}", "12.35")]
    [InlineData("${0:0}", "$12")]
    public void WithDataLabelFormat_FormatsTheValue(string format, string expected) {
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try {
            var data = new[] { new Pt(1, 12.345m) };
            var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("DataLabelFormat", format)));

            Tooltip(SeriesOf<NTLineSeries<Pt>>(cut), data[0]).Lines[0].Value.Should().Be(expected);
        }
        finally {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void WithNegativeValue_FormatsSign() {
        var data = new[] { new Pt(1, -7m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data));

        Tooltip(SeriesOf<NTLineSeries<Pt>>(cut), data[0]).Lines[0].Value.Should().Be("-7");
    }

    [Fact]
    public void WithoutExplicitColor_UsesPaletteColorOfTheSeriesIndex() {
        var data = new[] { new Pt(1, 10m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data), SeriesFragment<NTLineSeries<Pt>>(data));

        var first = Tooltip(SeriesOf<NTLineSeries<Pt>>(cut, 0), data[0]).Lines[0].Color;
        var second = Tooltip(SeriesOf<NTLineSeries<Pt>>(cut, 1), data[0]).Lines[0].Color;

        first.Should().Be(cut.Instance.GetThemeColor(cut.Instance.Palette[0].Background));
        second.Should().Be(cut.Instance.GetThemeColor(cut.Instance.Palette[1].Background));
    }

    [Fact]
    public void WithTooltipInfoSelector_ReplacesDefaultPayloadAndReceivesContext() {
        var data = new[] { new Pt(1, 10m), new Pt(2, 30m) };
        NTSeriesTooltipContext<Pt>? received = null;
        Func<NTSeriesTooltipContext<Pt>, TooltipInfo> selector = context => {
            received = context;
            return new TooltipInfo { Header = "custom", Lines = [new TooltipLine { Label = "L", Value = "V", Color = Blue }] };
        };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("TooltipInfoSelector", selector)));
        var series = SeriesOf<NTLineSeries<Pt>>(cut);
        SetHover(cut.Instance, series, 1);

        var info = Tooltip(series, data[1]);

        info.Header.Should().Be("custom");
        info.Lines[0].Label.Should().Be("L");
        received.Should().NotBeNull();
        received!.Series.Should().BeSameAs(series);
        received.Data.Should().Be(data[1]);
        received.PointIndex.Should().Be(1);
        received.DefaultInfo.Header.Should().Be("2");
        received.DefaultInfo.Lines[0].Value.Should().Be("30");
    }

    [Fact]
    public void WithTooltipInfoSelectorReturningNull_FallsBackToDefaultPayload() {
        var data = new[] { new Pt(1, 10m) };
        Func<NTSeriesTooltipContext<Pt>, TooltipInfo> selector = _ => null!;
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("TooltipInfoSelector", selector)));

        var info = Tooltip(SeriesOf<NTLineSeries<Pt>>(cut), data[0]);

        info.Header.Should().Be("1");
        info.Lines[0].Value.Should().Be("10");
    }

    [Fact]
    public void WithoutHoveredPoint_ProvidesNullPointIndexToSelector() {
        var data = new[] { new Pt(1, 10m) };
        int? index = -1;
        Func<NTSeriesTooltipContext<Pt>, TooltipInfo> selector = context => {
            index = context.PointIndex;
            return context.DefaultInfo;
        };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTLineSeries<Pt>>(data, ("TooltipInfoSelector", selector)));

        Tooltip(SeriesOf<NTLineSeries<Pt>>(cut), data[0]);

        index.Should().BeNull();
    }
}
