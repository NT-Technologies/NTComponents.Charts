using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts.Tests.ChartCore;

public class NTChartTokens_Tests {
    [Theory]
    [InlineData(NTTypography.LabelSmall, 11f, 16f, 500)]
    [InlineData(NTTypography.LabelMedium, 12f, 16f, 500)]
    [InlineData(NTTypography.BodySmall, 12f, 16f, 400)]
    [InlineData(NTTypography.BodyMedium, 14f, 20f, 400)]
    [InlineData(NTTypography.TitleSmall, 14f, 20f, 500)]
    [InlineData(NTTypography.TitleMedium, 16f, 24f, 500)]
    public void TypeScale_MatchesMaterial3SizeLineHeightAndWeight(NTTypography role, float size, float lineHeight, int weight) {
        NTChartTokens.Size(role).Should().Be(size);
        NTChartTokens.LineHeight(role).Should().Be(lineHeight);
        NTChartTokens.Typeface(role).FontWeight.Should().Be(weight);
    }

    [Fact]
    public void EmphasizedBody_UsesTheMediumWeight() {
        NTChartTokens.Typeface(NTTypography.BodyMedium, emphasized: true).FontWeight.Should().Be(500);
    }

    [Theory]
    [InlineData(NTCornerRadius.None, 0f)]
    [InlineData(NTCornerRadius.ExtraSmall, 4f)]
    [InlineData(NTCornerRadius.Small, 8f)]
    [InlineData(NTCornerRadius.Medium, 12f)]
    public void Radius_MatchesTheCornerScale(NTCornerRadius radius, float expected) {
        NTChartTokens.Radius(radius).Should().Be(expected);
    }

    [Fact]
    public void StateLayerOpacities_FollowMaterial3() {
        (NTChartTokens.HoverOpacity, NTChartTokens.PressedOpacity, NTChartTokens.FocusOpacity, NTChartTokens.DeEmphasisOpacity).Should().Be((0.08f, 0.10f, 0.10f, 0.38f));
    }

    [Fact]
    public void ContrastRatio_IsTheWcagFormula() {
        NTChartTokens.ContrastRatio(SKColors.Black, SKColors.White).Should().BeApproximately(21f, 0.01f);
        NTChartTokens.ContrastRatio(SKColors.White, SKColors.White).Should().BeApproximately(1f, 0.001f);
    }
}
