using Microsoft.AspNetCore.Components;
using SkiaSharp;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace NTComponents.Charts.Core.Axes;

/// <summary>
///     Options for the X axis of a cartesian chart.
/// </summary>
public class NTXAxisOptions<TData, TAxisType> : NTAxisOptions<TData>, INTXAxis<TData> where TData : class {

    public static INTXAxis<TData> Default { get; } = new NTXAxisOptions<TData, TAxisType>() {
        ValueSelector = data => default!
    };

    [Parameter, EditorRequired]
    public Func<TData, TAxisType> ValueSelector { get; set; }

    /// <inheritdoc />
    public bool IsCategorical {
        get {
            var type = typeof(TAxisType);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                type = Nullable.GetUnderlyingType(type)!;
            }

            return !(type == typeof(DateTime) ||
                     type == typeof(DateTimeOffset) ||
                     type == typeof(DateOnly) ||
                     type == typeof(TimeOnly) ||
                     type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INumberBase<>)));
        }
    }

    private readonly List<AxisTick> _cachedTicks = [];
    private AxisCacheKey? _cacheKey;
    private SKPaint _linePaint = default!;
    private float _rotation;
    private float _tickLabelHeight;
    private SKFont _textFont = default!;
    private SKPaint _textPaint = default!;
    private SKFont _titleFont = default!;
    private SKPaint _titlePaint = default!;
    private float? _titleHeight;

    public override void Dispose() {
        _textPaint?.Dispose();
        _titlePaint?.Dispose();
        _linePaint?.Dispose();
        _textFont?.Dispose();
        _titleFont?.Dispose();
        Chart.UnregisterAxis(this);
        base.Dispose();
    }

    /// <inheritdoc />
    public override SKRect Measure(NTRenderContext context, SKRect renderArea) {
        if (!Visible) {
            return renderArea;
        }

        InitializePaints(context);
        _titleHeight = GetTitleHeight(context, _titleFont);

        BuildTicks(context, renderArea);

        var tickHeight = 5 * context.Density;
        var totalHeight = _titleHeight.Value + _tickLabelHeight + tickHeight + (10 * context.Density);
        return new SKRect(renderArea.Left, renderArea.Top, renderArea.Right, renderArea.Bottom - totalHeight);
    }

    /// <inheritdoc />
    public override SKRect Render(NTRenderContext context, SKRect renderArea) {
        if (!Visible) {
            return renderArea;
        }

        InitializePaints(context);
        BuildTicks(context, renderArea);

        var plotArea = context.PlotArea;
        var axisY = plotArea.Bottom;
        var tickHeight = 5 * context.Density;

        context.Canvas.DrawLine(plotArea.Left, axisY, plotArea.Right, axisY, _linePaint);

        foreach (var tick in _cachedTicks) {
            var x = Chart.ScaleX(tick.Value, plotArea);
            if (x < plotArea.Left - context.Density || x > plotArea.Right + context.Density) {
                continue;
            }

            context.Canvas.DrawLine(x, axisY, x, axisY + tickHeight, _linePaint);

            var textBaseline = axisY + tickHeight + _tickLabelHeight + (2 * context.Density);
            if (_rotation == 0f) {
                context.Canvas.DrawText(tick.Label, x, textBaseline, SKTextAlign.Center, _textFont, _textPaint);
            }
            else {
                context.Canvas.Save();
                context.Canvas.Translate(x, textBaseline);
                context.Canvas.RotateDegrees(_rotation);
                context.Canvas.DrawText(tick.Label, 0, 0, SKTextAlign.Center, _textFont, _textPaint);
                context.Canvas.Restore();
            }
        }

        if (!string.IsNullOrEmpty(Title)) {
            var titleY = renderArea.Bottom - (2 * context.Density);
            context.Canvas.DrawText(Title, plotArea.MidX, titleY, SKTextAlign.Center, _titleFont, _titlePaint);
        }

        return renderArea;
    }

    protected override void OnInitialized() {
        base.OnInitialized();
        Chart.RegisterAxis(this);
    }

    private void BuildTicks(NTRenderContext context, SKRect renderArea) {
        var plotArea = context.PlotArea;
        var (min, max) = Chart.GetXRange(this, true);
        var allXCount = IsCategorical ? Chart.GetAllXValues().Count : 0;
        var key = new AxisCacheKey(
            IsCategorical,
            allXCount,
            Math.Round(min, 6),
            Math.Round(max, 6),
            Math.Round(plotArea.Width, 2),
            Math.Round(context.Density, 3),
            AxisFontSize,
            LabelFormat,
            Title,
            TitleFontSize);

        if (_cacheKey.HasValue && _cacheKey.Value.Equals(key)) {
            return;
        }

        _cacheKey = key;
        _cachedTicks.Clear();
        _rotation = 0f;
        _tickLabelHeight = 0f;

        if (IsCategorical) {
            BuildCategoricalTicks(context, plotArea);
        }
        else {
            BuildContinuousTicks(context, min, max, plotArea.Width);
        }
    }

    private void BuildCategoricalTicks(NTRenderContext context, SKRect plotArea) {
        var allX = Chart.GetAllXValues();
        if (allX.Count == 0) {
            return;
        }

        var (rangeMin, rangeMax) = Chart.GetXRange(this, false);
        var start = Math.Max(0, (int)Math.Floor(Math.Min(rangeMin, rangeMax)));
        var end = Math.Min(allX.Count - 1, (int)Math.Ceiling(Math.Max(rangeMin, rangeMax)));
        if (end < start) {
            return;
        }

        var visibleCount = end - start + 1;
        var (maxLabelWidth, maxLabelHeight) = MeasureSampleLabels(context, start, end, idx => FormatLabel(allX[idx]));

        var targetTicks = EstimateTargetTicks(plotArea.Width, maxLabelWidth, context.Density);
        var step = Math.Max(1, (int)Math.Ceiling(visibleCount / (double)targetTicks));

        for (var i = start; i <= end; i += step) {
            _cachedTicks.Add(new AxisTick(i, FormatLabel(allX[i])));
        }

        if (_cachedTicks.Count == 0 || _cachedTicks[^1].Value != end) {
            _cachedTicks.Add(new AxisTick(end, FormatLabel(allX[end])));
        }

        var labelSlotWidth = plotArea.Width / Math.Max(1, _cachedTicks.Count - 1);
        if (maxLabelWidth + (5 * context.Density) > labelSlotWidth) {
            _rotation = -45f;
            var rad = Math.Abs(_rotation) * Math.PI / 180.0;
            _tickLabelHeight = (float)((Math.Sin(rad) * maxLabelWidth) + (Math.Cos(rad) * maxLabelHeight));
        }
        else {
            _tickLabelHeight = maxLabelHeight;
        }
    }

    private void BuildContinuousTicks(NTRenderContext context, double min, double max, float plotWidth) {
        if (double.IsNaN(min) || double.IsNaN(max) || double.IsInfinity(min) || double.IsInfinity(max)) {
            return;
        }

        if (Math.Abs(max - min) < double.Epsilon) {
            _cachedTicks.Add(new AxisTick(min, FormatLabel(ToLabelValue(min))));
            _textFont.MeasureText(_cachedTicks[0].Label, out var bounds);
            _tickLabelHeight = bounds.Height;
            return;
        }

        var minLabel = FormatLabel(ToLabelValue(min));
        var maxLabel = FormatLabel(ToLabelValue(max));

        _textFont.MeasureText(minLabel, out var minBounds);
        _textFont.MeasureText(maxLabel, out var maxBounds);
        var estWidth = Math.Max(minBounds.Width, maxBounds.Width);

        var targetTicks = EstimateTargetTicks(plotWidth, estWidth, context.Density);
        var spacing = CalculateNiceSpacing(min, max, targetTicks);

        if (spacing <= 0 || double.IsInfinity(spacing) || double.IsNaN(spacing)) {
            _cachedTicks.Add(new AxisTick(min, minLabel));
            _cachedTicks.Add(new AxisTick(max, maxLabel));
            _tickLabelHeight = Math.Max(minBounds.Height, maxBounds.Height);
            return;
        }

        var firstTick = Math.Ceiling(min / spacing) * spacing;
        var epsilon = spacing / 10000.0;

        for (var tick = firstTick; tick <= max + epsilon; tick += spacing) {
            if (tick < min - epsilon) {
                continue;
            }

            _cachedTicks.Add(new AxisTick(tick, FormatLabel(ToLabelValue(tick))));
        }

        if (_cachedTicks.Count == 0) {
            _cachedTicks.Add(new AxisTick(min, minLabel));
            _cachedTicks.Add(new AxisTick(max, maxLabel));
        }

        _tickLabelHeight = Math.Max(minBounds.Height, maxBounds.Height);
    }

    private (float MaxWidth, float MaxHeight) MeasureSampleLabels(NTRenderContext context, int start, int end, Func<int, string> labelFactory) {
        const int maxSamples = 24;
        var count = end - start + 1;
        if (count <= 0) {
            return (0, 0);
        }

        var step = Math.Max(1, count / maxSamples);
        float maxWidth = 0;
        float maxHeight = 0;

        for (var i = start; i <= end; i += step) {
            var label = labelFactory(i);
            _textFont.MeasureText(label, out var bounds);
            maxWidth = Math.Max(maxWidth, bounds.Width);
            maxHeight = Math.Max(maxHeight, bounds.Height);
        }

        if ((end - start) % step != 0) {
            var label = labelFactory(end);
            _textFont.MeasureText(label, out var bounds);
            maxWidth = Math.Max(maxWidth, bounds.Width);
            maxHeight = Math.Max(maxHeight, bounds.Height);
        }

        return (maxWidth, maxHeight);
    }

    private static int EstimateTargetTicks(float plotWidth, float estimatedLabelWidth, float density) {
        var width = Math.Max(1f, estimatedLabelWidth + (12 * density));
        return Math.Max(2, (int)(plotWidth / width));
    }

    private static double CalculateNiceSpacing(double min, double max, int targetTicks) {
        var range = Math.Abs(max - min);
        if (range <= 0 || targetTicks <= 1) {
            return 0;
        }

        var rough = range / (targetTicks - 1);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var residual = rough / magnitude;

        var nice = residual switch {
            <= 1 => 1,
            <= 2 => 2,
            <= 5 => 5,
            _ => 10
        };

        return nice * magnitude;
    }

    private object ToLabelValue(double scaledValue) {
        if (!Chart.IsXAxisDateTime) {
            return scaledValue;
        }

        var ticks = (long)Math.Round(scaledValue);
        if (ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks) {
            return new DateTime(ticks);
        }

        return scaledValue;
    }

    private float GetTitleHeight(NTRenderContext context, SKFont font) {
        if (string.IsNullOrEmpty(Title)) {
            return 0f;
        }

        font.MeasureText(Title, out var bounds);
        return bounds.Height + (5 * context.Density);
    }

    private string FormatLabel(object? value) {
        if (value is null) {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(LabelFormat)) {
            try {
                return string.Format(LabelFormat, value);
            }
            catch {
                return value.ToString() ?? string.Empty;
            }
        }

        if (value is DateTime dt) {
            return dt.ToString("d");
        }

        return value.ToString() ?? string.Empty;
    }

    [MemberNotNull(nameof(_textPaint), nameof(_textFont), nameof(_titlePaint), nameof(_titleFont), nameof(_linePaint))]
    private void InitializePaints(NTRenderContext context) {
        _textPaint ??= new SKPaint {
            IsAntialias = true,
            Color = context.TextColor
        };
        _textFont ??= new SKFont(context.DefaultFont.Typeface, AxisFontSize * context.Density);
        _textFont.Size = AxisFontSize * context.Density;

        _titlePaint ??= new SKPaint {
            IsAntialias = true,
            Color = context.TextColor
        };
        _titleFont ??= new SKFont(context.DefaultFont.Typeface, TitleFontSize * context.Density);
        _titleFont.Size = TitleFontSize * context.Density;

        _linePaint ??= new SKPaint {
            IsAntialias = true,
            Color = context.TextColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1 * context.Density
        };
        _linePaint.Color = context.TextColor;
        _linePaint.StrokeWidth = context.Density;
    }

    private readonly record struct AxisTick(double Value, string Label);

    private readonly record struct AxisCacheKey(
        bool IsCategorical,
        int CategoryCount,
        double Min,
        double Max,
        double PlotWidth,
        double Density,
        float AxisFontSize,
        string? LabelFormat,
        string? Title,
        float TitleFontSize);
}
