using NTComponents.Charts.Core;

namespace NTComponents.Charts.Tests.Charts;

public class NTChartAnnotation_Tests {

    [Fact]
    public void Defaults_are_initialized_as_expected() {
        // Arrange
        var annotation = new NTChartAnnotation();

        // Assert
        annotation.Type.Should().Be(NTChartAnnotationType.YLine);
        annotation.X.Should().BeNull();
        annotation.X2.Should().BeNull();
        annotation.Y.Should().BeNull();
        annotation.Y2.Should().BeNull();
        annotation.Label.Should().BeNull();
        annotation.UseSecondaryYAxis.Should().BeFalse();
        annotation.StrokeColor.Should().Be(NTColor.Primary);
        annotation.FillColor.Should().BeNull();
        annotation.TextColor.Should().BeNull();
        annotation.StrokeWidth.Should().Be(1.5f);
        annotation.DashLength.Should().Be(0f);
        annotation.MarkerSize.Should().Be(5f);
        annotation.FontSize.Should().Be(11f);
        annotation.LabelOffsetX.Should().Be(0f);
        annotation.LabelOffsetY.Should().Be(0f);
        annotation.Opacity.Should().Be(1f);
        annotation.ClipToPlotArea.Should().BeTrue();
        annotation.CustomRenderer.Should().BeNull();
    }

    [Fact]
    public void Supports_assigning_custom_renderer() {
        // Arrange
        var called = false;
        Action<NTChartAnnotationRenderContext> renderer = _ => called = true;

        // Act
        var annotation = new NTChartAnnotation {
            Type = NTChartAnnotationType.Custom,
            CustomRenderer = renderer
        };

        // Assert
        annotation.Type.Should().Be(NTChartAnnotationType.Custom);
        annotation.CustomRenderer.Should().NotBeNull();

        // We do not execute renderer here since render context is produced by NTChart at draw time.
        called.Should().BeFalse();
    }
}
