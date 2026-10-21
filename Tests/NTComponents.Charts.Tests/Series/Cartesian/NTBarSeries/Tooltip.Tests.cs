using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Tests.Series.Cartesian;

public class NTBarSeriesTooltip_Tests : BarTestBase {
    private static readonly Pt[] Data = [new("North", 10m), new("South", 30m)];

    [Fact]
    public void Plain_ShowsCategoryHeaderAndFormattedValue() {
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Data, Primary, ("Title", "Sales")));

        var info = Tooltip(SeriesOf<NTBarSeries<Pt>>(cut), Data[1]);

        info.Header.Should().Be("South");
        info.Lines.Should().ContainSingle();
        info.Lines[0].Label.Should().Be("Sales");
        info.Lines[0].Value.Should().Be("30");
        info.Lines[0].Color.Should().Be(Red);
    }

    [Fact]
    public void Plain_UsesNumericXAxisFormatForTheHeader() {
        var data = new[] { new Pt(4, 1m), new Pt(5, 2m) };
        var cut = RenderChart(XAxisKind.Int, SeriesFragment<NTBarSeries<Pt>>(data, Primary));

        Tooltip(SeriesOf<NTBarSeries<Pt>>(cut), data[1]).Header.Should().Be("5");
    }

    [Fact]
    public void TooltipInfoSelector_IsAppliedExactlyOnceToThePlainPayload() {
        var calls = 0;
        Func<NTSeriesTooltipContext<Pt>, TooltipInfo> selector = context => {
            calls++;
            var lines = context.DefaultInfo.Lines.ToList();
            lines.Add(new TooltipLine { Label = "extra", Value = "1", Color = Blue });
            return new TooltipInfo { Header = context.DefaultInfo.Header, Lines = lines };
        };
        var cut = RenderChart(XAxisKind.Default, SeriesFragment<NTBarSeries<Pt>>(Data, Primary, ("TooltipInfoSelector", selector)));

        var info = Tooltip(SeriesOf<NTBarSeries<Pt>>(cut), Data[0]);

        calls.Should().Be(1);
        info.Lines.Select(l => l.Label).Should().Equal("Series", "extra");
    }
}
