using NTComponents.Charts;
using NTComponents.Charts.Core;
using Microsoft.AspNetCore.Components.Web;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using SkiaSharp;

namespace NTComponents.Charts.Tests.Charts;

public class BubblePackPhysics_Tests {
    [Theory]
    [InlineData(1.9f, 0.07f, true)]
    [InlineData(2.01f, 0.01f, false)]
    [InlineData(0f, 0.081f, false)]
    public void PhysicsSettled_requires_low_actual_motion_and_displacement(float velocity, float displacement, bool expected) {
        var isSettled = (bool)typeof(NTBubblePackSeries<BubbleDatum>)
            .GetMethod("IsPhysicsSettled", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [velocity, displacement])!;

        isSettled.Should().Be(expected);
    }

    [Fact]
    public void WakePhysics_restarts_timestamp_and_marks_physics_awake() {
        var series = new NTBubblePackSeries<BubbleDatum>();
        SetPrivateField(series, "_physicsAwake", false);
        SetPrivateField(series, "_lastPhysicsStepTimestamp", 123L);

        typeof(NTBubblePackSeries<BubbleDatum>)
            .GetMethod("WakePhysics", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(series, null);

        GetPrivateField<bool>(series, "_physicsAwake").Should().BeTrue();
        GetPrivateField<long>(series, "_lastPhysicsStepTimestamp").Should().Be(0L);
    }

    [Fact]
    public void CollisionBuckets_include_every_overlapping_pair_in_a_large_deterministic_cloud() {
        var random = new Random(417);
        var positions = Enumerable.Range(0, 180)
            .Select(_ => new SKPoint(random.Next(-1500, 1501), random.Next(-1500, 1501)))
            .ToArray();
        var radii = Enumerable.Range(0, positions.Length)
            .Select(_ => (float)random.Next(5, 65))
            .ToArray();
        const float spacing = 3f;
        var cellSize = (2f * radii.Max()) + spacing;
        var isCandidate = typeof(NTBubblePackSeries<BubbleDatum>)
            .GetMethod("IsCollisionBucketCandidate", BindingFlags.Static | BindingFlags.NonPublic)!;
        var overlappingPairCount = 0;

        for (var i = 0; i < positions.Length; i++) {
            for (var j = i + 1; j < positions.Length; j++) {
                var dx = positions[i].X - positions[j].X;
                var dy = positions[i].Y - positions[j].Y;
                var minimumDistance = radii[i] + radii[j] + spacing;
                if ((dx * dx) + (dy * dy) >= (minimumDistance * minimumDistance)) {
                    continue;
                }

                overlappingPairCount++;
                var candidate = (bool)isCandidate.Invoke(null, [positions[i], positions[j], cellSize])!;
                candidate.Should().BeTrue($"overlapping bubbles {i} and {j} must share a broad-phase neighborhood");
            }
        }

        overlappingPairCount.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(0f, 2)]
    [InlineData(3.2f, 2)]
    [InlineData(3.2f, 40)]
    public void Simulation_sleeps_after_settling_and_wakes_for_layout_data_and_zoom(float gravity, int count) {
        using var chart = new NTChart<BubbleDatum>();
        using var surface = SKSurface.Create(new SKImageInfo(400, 300))!;
        using var series = new TestBubbleSeries(chart) {
            AnimationEnabled = false,
            ShowLabels = false,
            ShowNavigation = false,
            GravityStrength = gravity,
            ValueSelector = item => item.Value,
            XValue = item => item.Name,
            Data = Enumerable.Range(0, count).Select(i => new BubbleDatum($"Bubble {i}", 10m)).ToArray()
        };
        var context = CreateContext(chart, surface, 400, 300);
        var initialArea = new SKRect(0, 0, 320, 220);

        series.Render(context, initialArea);
        SetOverlappingBubblePositions(series);
        AdvanceUntilAsleep(series, context, initialArea);

        var changedArea = new SKRect(0, 0, 360, 240);
        series.Render(context, changedArea);
        GetPrivateField<bool>(series, "_physicsAwake").Should().BeTrue("a changed content area must wake packing physics");
        AdvanceUntilAsleep(series, context, changedArea);

        series.Data = [new BubbleDatum("A", 10m), new BubbleDatum("B", 10m), new BubbleDatum("C", 6m)];
        series.InvokeDataChanged();
        series.Render(context, changedArea);
        GetPrivateField<bool>(series, "_physicsAwake").Should().BeTrue("new data must restart packing physics");
        AdvanceUntilAsleep(series, context, changedArea);

        series.Interactions = ChartInteractions.XZoom;
        series.HandleMouseWheel(new WheelEventArgs { OffsetX = changedArea.MidX, OffsetY = changedArea.MidY, DeltaY = 1 });

        GetPrivateField<bool>(series, "_physicsAwake").Should().BeTrue("zoom input must wake packing physics");
        GetRequiresAnimationFrame(series).Should().BeTrue();
    }

    private static void AdvanceUntilAsleep(TestBubbleSeries series, NTRenderContext context, SKRect area) {
        var lastDisplacement = 0f;
        for (var frame = 0; frame < 600 && GetPrivateField<bool>(series, "_physicsAwake"); frame++) {
            var bubbles = ((IList)GetPrivateField<object>(series, "_visibleBubbles")).Cast<object>().Select(b => b.GetType().GetProperty("State")!.GetValue(b)!).ToArray();
            var before = bubbles.Select(b => GetPrivateField<SKPoint>(b, "Position")).ToArray();
            SetPrivateField(series, "_lastPhysicsStepTimestamp", Stopwatch.GetTimestamp() - (Stopwatch.Frequency / 60));
            series.Render(context, area);
            lastDisplacement = bubbles.Select((b, i) => {
                var position = GetPrivateField<SKPoint>(b, "Position");
                return MathF.Sqrt(MathF.Pow(position.X - before[i].X, 2) + MathF.Pow(position.Y - before[i].Y, 2));
            }).DefaultIfEmpty().Max();
        }

        var states = ((IList)GetPrivateField<object>(series, "_visibleBubbles")).Cast<object>().Select(b => b.GetType().GetProperty("State")!.GetValue(b)!).ToArray();
        var velocity = states.Select(b => GetPrivateField<SKPoint>(b, "Velocity")).Select(v => MathF.Sqrt((v.X * v.X) + (v.Y * v.Y))).DefaultIfEmpty().Max();
        var overlap = 0f;
        for (var i = 0; i < states.Length; i++) {
            for (var j = i + 1; j < states.Length; j++) {
                var a = GetPrivateField<SKPoint>(states[i], "Position");
                var b = GetPrivateField<SKPoint>(states[j], "Position");
                var distance = MathF.Sqrt(MathF.Pow(a.X - b.X, 2) + MathF.Pow(a.Y - b.Y, 2));
                overlap = Math.Max(overlap, GetPrivateField<float>(states[i], "Radius") + GetPrivateField<float>(states[j], "Radius") + series.BubbleSpacing - distance);
            }
        }
        GetPrivateField<bool>(series, "_physicsAwake").Should().BeFalse($"the deterministic physics sequence should settle within the frame limit (velocity {velocity}, displacement {lastDisplacement}, overlap {overlap})");
        GetRequiresAnimationFrame(series).Should().BeFalse("settled bubble physics should stop requesting frames");
    }

    private static void SetOverlappingBubblePositions(TestBubbleSeries series) {
        var visibleBubbles = (IList)GetPrivateField<object>(series, "_visibleBubbles");
        visibleBubbles.Count.Should().BeGreaterThanOrEqualTo(2);
        var first = visibleBubbles[0]!.GetType().GetProperty("State")!.GetValue(visibleBubbles[0])!;
        var second = visibleBubbles[1]!.GetType().GetProperty("State")!.GetValue(visibleBubbles[1])!;
        var firstRadius = GetPrivateField<float>(first, "Radius");
        var secondRadius = GetPrivateField<float>(second, "Radius");
        var centerX = 160f;
        var centerY = 110f;
        var offset = (firstRadius + secondRadius) * 0.45f;
        SetPrivateField(first, "Position", new SKPoint(centerX - offset, centerY));
        SetPrivateField(second, "Position", new SKPoint(centerX + offset, centerY));
        SetPrivateField(first, "Velocity", SKPoint.Empty);
        SetPrivateField(second, "Velocity", SKPoint.Empty);
    }

    private static NTRenderContext CreateContext(NTChart<BubbleDatum> chart, SKSurface surface, int width, int height) {
        var info = new SKImageInfo(width, height);
        return new NTRenderContext {
            Canvas = surface.Canvas,
            DefaultFont = chart.DefaultFont,
            RegularFont = chart.RegularFont,
            Density = 1f,
            Info = info,
            PlotArea = new SKRect(0, 0, width, height),
            TextColor = SKColors.Black,
            TotalArea = new SKRect(0, 0, width, height)
        };
    }

    private static bool GetRequiresAnimationFrame(TestBubbleSeries series) =>
        (bool)typeof(NTBubblePackSeries<BubbleDatum>)
            .GetProperty("RequiresAnimationFrame", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(series)!;

    private sealed class TestBubbleSeries : NTBubblePackSeries<BubbleDatum> {
        public TestBubbleSeries(NTChart<BubbleDatum> chart) => Chart = chart;

        public void InvokeDataChanged() => OnDataChanged();
    }

    private sealed record BubbleDatum(string Name, decimal Value);

    private static T GetPrivateField<T>(object target, string fieldName) {
        var field = FindPrivateField(target.GetType(), fieldName);
        if (field is not null) {
            return (T)field.GetValue(target)!;
        }

        return (T)target.GetType().GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(target)!;
    }

    private static void SetPrivateField(object target, string fieldName, object value) {
        var field = FindPrivateField(target.GetType(), fieldName);
        if (field is not null) {
            field.SetValue(target, value);
            return;
        }

        target.GetType().GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(target, value);
    }

    private static FieldInfo? FindPrivateField(Type type, string name) {
        for (var current = type; current is not null; current = current.BaseType) {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is not null) {
                return field;
            }
        }

        return null;
    }
}
