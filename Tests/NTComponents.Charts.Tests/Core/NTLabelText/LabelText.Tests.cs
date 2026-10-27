using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTLabelText_Tests : IDisposable {
    private readonly SKFont _font = new(NTRobotoFonts.Medium, 12);

    public void Dispose() => _font.Dispose();

    [Fact]
    public void Ellipsize_TextThatFits_IsReturnedUnchanged() {
        NTLabelText.Ellipsize(_font, "Chest", 200).Should().Be("Chest");
    }

    [Fact]
    public void Ellipsize_LongText_EndsInAnEllipsisWithinTheWidth() {
        var text = NTLabelText.Ellipsize(_font, "Lumbar and/or Sacral Vertebrae (Vertebra NOC Trunk)", 80);

        text.Should().EndWith(NTLabelText.Ellipsis).And.StartWith("Lumbar");
        _font.MeasureText(text).Should().BeLessThanOrEqualTo(80);
    }

    [Fact]
    public void Ellipsize_WidthBelowTheEllipsis_ReturnsOnlyTheEllipsis() {
        NTLabelText.Ellipsize(_font, "Abdomen", 1).Should().Be(NTLabelText.Ellipsis);
    }

    [Fact]
    public void Ellipsize_NeverSplitsASurrogatePair() {
        var text = NTLabelText.Ellipsize(_font, "ab\U0001F600\U0001F600\U0001F600\U0001F600\U0001F600", _font.MeasureText("ab\U0001F600") + _font.MeasureText(NTLabelText.Ellipsis) + 1);

        text[..^1].Should().NotEndWith("\uD83D");
    }

    [Fact]
    public void Wrap_BreaksAtSpacesAndKeepsEveryWordWhenItFits() {
        var lines = NTLabelText.Wrap(_font, "Body Systems and Multiple Body Systems", _font.MeasureText("Body Systems and Multiple") + 1, 2);

        lines.Should().Equal("Body Systems and Multiple", "Body Systems");
    }

    [Fact]
    public void Wrap_TextLongerThanTheLines_EndsTheLastLineWithAnEllipsis() {
        var lines = NTLabelText.Wrap(_font, "Insufficient Info to Properly Identify-Unclassified", 70, 2);

        lines.Should().HaveCount(2);
        lines[^1].Should().EndWith(NTLabelText.Ellipsis);
        lines.Select(line => _font.MeasureText(line)).Should().OnlyContain(width => width <= 70);
    }

    [Fact]
    public void Wrap_AWordWiderThanTheLine_IsBrokenInsideTheWord() {
        var lines = NTLabelText.Wrap(_font, "Supercalifragilistic", _font.MeasureText("Supercali") + 1, 3);

        lines.Should().HaveCountGreaterThan(1);
        lines[0].Should().Be("Supercali");
        string.Concat(lines).Should().Be("Supercalifragilistic");
    }

    [Fact]
    public void Wrap_TextThatFits_IsASingleLine() {
        NTLabelText.Wrap(_font, "  Wrist  ", 200, 2).Should().Equal("Wrist");
    }
}
