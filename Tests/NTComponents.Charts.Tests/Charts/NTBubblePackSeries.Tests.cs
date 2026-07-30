using NTComponents.Charts;
using NTComponents.Charts.Core;
using SkiaSharp;
using System.Reflection;

namespace NTComponents.Charts.Tests.Charts;

public class NTBubblePackSeries_Tests {

    [Fact]
    public void OnParametersSet_enables_xy_zoom_when_interactions_are_none() {
        // Arrange
        var series = new TestBubblePackSeries {
            Interactions = ChartInteractions.None
        };

        // Act
        series.InvokeParametersSet();

        // Assert
        series.Interactions.Should().Be(ChartInteractions.XZoom | ChartInteractions.YZoom);
    }

    [Fact]
    public void OnParametersSet_removes_pan_flags_and_keeps_zoom_flags() {
        // Arrange
        var series = new TestBubblePackSeries {
            Interactions = ChartInteractions.All
        };

        // Act
        series.InvokeParametersSet();

        // Assert
        series.Interactions.Should().Be(ChartInteractions.XZoom | ChartInteractions.YZoom);
        series.Interactions.HasFlag(ChartInteractions.XPan).Should().BeFalse();
        series.Interactions.HasFlag(ChartInteractions.YPan).Should().BeFalse();
    }

    [Fact]
    public void OnParametersSet_preserves_existing_zoom_axis_selection() {
        // Arrange
        var series = new TestBubblePackSeries {
            Interactions = ChartInteractions.XZoom | ChartInteractions.YPan
        };

        // Act
        series.InvokeParametersSet();

        // Assert
        series.Interactions.Should().Be(ChartInteractions.XZoom);
    }

    [Fact]
    public void Defaults_match_expected_behavior() {
        // Arrange
        var series = new TestBubblePackSeries();

        // Assert
        series.TargetFillRatio.Should().Be(0.32f);
        series.CanvasPadding.Should().Be(10f);
        series.LabelPadding.Should().Be(12f);
        series.ConstrainToCanvas.Should().BeFalse();
        series.EnableDrilldown.Should().BeTrue();
    }

    [Fact]
    public void WorldToView_and_ViewToWorld_are_inverse_for_nonzero_zoom() {
        // Arrange
        var series = new TestBubblePackSeries();
        SetPrivateField(series, "_zoomScaleX", 1.8f);
        SetPrivateField(series, "_zoomScaleY", 0.75f);

        var origin = new SKPoint(100f, 80f);
        var world = new SKPoint(140f, 20f);

        // Act
        var view = (SKPoint)InvokeNonPublic(series, "WorldToView", [world, origin])!;
        var backToWorld = (SKPoint)InvokeNonPublic(series, "ViewToWorld", [view, origin])!;

        // Assert
        backToWorld.X.Should().BeApproximately(world.X, 0.001f);
        backToWorld.Y.Should().BeApproximately(world.Y, 0.001f);
    }

    [Fact]
    public void GetRadiusZoomScale_uses_clamped_zoom_values() {
        // Arrange
        var series = new TestBubblePackSeries();
        SetPrivateField(series, "_zoomScaleX", 0f);
        SetPrivateField(series, "_zoomScaleY", 4f);

        // Act
        var scale = (float)InvokeNonPublic(series, "GetRadiusZoomScale", [])!;

        // Assert
        scale.Should().BeApproximately(0.02f, 0.0001f); // sqrt(0.0001 * 4)
    }

    [Fact]
    public void ConfigurationHash_changes_when_interactions_change() {
        // Arrange
        var series = new TestBubblePackSeries {
            Interactions = ChartInteractions.None
        };

        // Act
        var hashBefore = (int)InvokeNonPublic(series, "GetConfigurationHash", [])!;
        series.Interactions = ChartInteractions.XZoom;
        var hashAfter = (int)InvokeNonPublic(series, "GetConfigurationHash", [])!;

        // Assert
        hashAfter.Should().NotBe(hashBefore);
    }

    private sealed class TestBubblePackSeries : NTBubblePackSeries<BubbleDatum> {
        public TestBubblePackSeries() {
            Data = [new BubbleDatum("A", 10m)];
            XValue = p => p.Name;
            ValueSelector = p => p.Value;
        }

        public void InvokeParametersSet() => OnParametersSet();
    }

    private sealed record BubbleDatum(string Name, decimal Value);

    private static object? InvokeNonPublic(object target, string methodName, object?[] args) {
        var method = FindNonPublicMethod(target.GetType(), methodName);
        method.Should().NotBeNull($"private method '{methodName}' should exist on '{target.GetType().Name}'");
        return method!.Invoke(target, args);
    }

    private static void SetPrivateField(object target, string fieldName, object value) {
        var field = FindNonPublicField(target.GetType(), fieldName);
        field.Should().NotBeNull($"private field '{fieldName}' should exist on '{target.GetType().Name}'");
        field!.SetValue(target, value);
    }

    private static MethodInfo? FindNonPublicMethod(Type type, string methodName) {
        for (var current = type; current is not null; current = current.BaseType) {
            var method = current.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method is not null) {
                return method;
            }
        }

        return null;
    }

    private static FieldInfo? FindNonPublicField(Type type, string fieldName) {
        for (var current = type; current is not null; current = current.BaseType) {
            var field = current.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is not null) {
                return field;
            }
        }

        return null;
    }
}
