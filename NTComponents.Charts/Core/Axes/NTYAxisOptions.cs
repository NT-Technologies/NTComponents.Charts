using Microsoft.AspNetCore.Components;
using SkiaSharp;
using System.Numerics;

namespace NTComponents.Charts.Core.Axes;

public interface INTYAxis<TData> : INTAxis<TData> where TData : class {
    static abstract INTYAxis<TData> Default { get; }
}

/// <summary>
///     Options for the Y axis of a cartesian chart.
/// </summary>
public class NTYAxisOptions<TData, TAxisValue> : NTAxisOptions<TData>, INTYAxis<TData>
    where TData : class where TAxisValue : INumberBase<TAxisValue> {

    private SKPaint? _textPaint;
    private SKPaint? _titlePaint;
    private SKPaint? _linePaint;
    private SKFont? _textFont;
    private SKFont? _titleFont;
    private readonly List<AxisTick> _cachedTicks = [];
    private AxisCacheKey? _cacheKey;
    private float _measuredLabelWidth;
    private float _titleWidth;

    public static INTYAxis<TData> Default { get; } = new NTYAxisOptions<TData, TAxisValue>() {
        ValueSelector = _ => TAxisValue.Zero
    };

    [Parameter, EditorRequired]
    public Func<TData, TAxisValue> ValueSelector { get; set; }

    public override void Dispose() {
        _textPaint?.Dispose();
        _titlePaint?.Dispose();
        _linePaint?.Dispose();
        _textFont?.Dispose();
        _titleFont?.Dispose();
        Chart.UnregisterAxis(this);
        base.Dispose();
    }

    protected override void OnInitialized() {
        base.OnInitialized();
        Chart.RegisterAxis(this);
    }

    private bool IsSecondary(IChart<TData> chart) => ReferenceEquals(chart.SecondaryYAxis, this);

    /// <inheritdoc />
    public override SKRect Measure(NTRenderContext context, SKRect renderArea) {
        if (!Visible) {
            return renderArea;
        }

        InitializePaints(context);
        BuildTicks(context, renderArea.Height);

        var axisPadding = 10 * context.Density;
        _titleWidth = string.IsNullOrWhiteSpace(Title) ? 0 : (_titleFont!.Size + (8 * context.Density));
        var totalAxisWidth = _measuredLabelWidth + _titleWidth + axisPadding;

        return IsSecondary(Chart)
            ? new SKRect(renderArea.Left, renderArea.Top, renderArea.Right - totalAxisWidth, renderArea.Bottom)
            : new SKRect(renderArea.Left + totalAxisWidth, renderArea.Top, renderArea.Right, renderArea.Bottom);
    }

    /// <inheritdoc />
    public override SKRect Render(NTRenderContext context, SKRect renderArea) {
        if (!Visible) {
            return renderArea;
        }

        InitializePaints(context);
        BuildTicks(context, context.PlotArea.Height);

        var canvas = context.Canvas;
        var plotArea = context.PlotArea;
        var isSecondary = IsSecondary(Chart);
        var xLine = isSecondary ? plotArea.Right : plotArea.Left;

        canvas.DrawLine(xLine, plotArea.Top, xLine, plotArea.Bottom, _linePaint);

        foreach (var tick in _cachedTicks) {
            var y = ScaleYForAxis(tick.Value, plotArea);
            if (y < plotArea.Top - context.Density || y > plotArea.Bottom + context.Density) {
                continue;
            }

            if (isSecondary) {
                canvas.DrawLine(xLine, y, xLine + (5 * context.Density), y, _linePaint);
                canvas.DrawText(tick.Label, xLine + (8 * context.Density), y + (4 * context.Density), SKTextAlign.Left, _textFont, _textPaint);
            }
            else {
                canvas.DrawLine(xLine - (5 * context.Density), y, xLine, y, _linePaint);
                canvas.DrawText(tick.Label, xLine - (8 * context.Density), y + (4 * context.Density), SKTextAlign.Right, _textFont, _textPaint);
            }
        }

        if (!string.IsNullOrWhiteSpace(Title)) {
            var centerY = plotArea.Top + (plotArea.Height / 2);
            canvas.Save();
            if (isSecondary) {
                var titleX = plotArea.Right + _titleWidth - (2 * context.Density);
                canvas.RotateDegrees(90, titleX, centerY);
                canvas.DrawText(Title, titleX, centerY, SKTextAlign.Center, _titleFont, _titlePaint);
            }
            else {
                var titleX = plotArea.Left - _measuredLabelWidth - (8 * context.Density);
                canvas.RotateDegrees(-90, titleX, centerY);
                canvas.DrawText(Title, titleX, centerY, SKTextAlign.Center, _titleFont, _titlePaint);
            }
            canvas.Restore();
        }

        return renderArea;
    }

    private void BuildTicks(NTRenderContext context, float plotHeight) {
        var (min, max) = Chart.GetYRange(this, true);
        var key = new AxisCacheKey(
            Math.Round((double)min, 6),
            Math.Round((double)max, 6),
            Scale,
            Math.Round(plotHeight, 2),
            Math.Round(context.Density, 3),
            AxisFontSize,
            LabelFormat,
            Title,
            TitleFontSize,
            IsSecondary(Chart));

        if (_cacheKey.HasValue && _cacheKey.Value.Equals(key)) {
            return;
        }

        _cacheKey = key;
        _cachedTicks.Clear();
        _measuredLabelWidth = 0;

        if (Scale == NTAxisScale.Logarithmic) {
            BuildLogTicks(min, max);
        }
        else {
            BuildLinearTicks(min, max, plotHeight, context.Density);
        }

        foreach (var tick in _cachedTicks) {
            _textFont!.MeasureText(tick.Label, out var bounds);
            _measuredLabelWidth = Math.Max(_measuredLabelWidth, bounds.Width);
        }
    }

    private void BuildLinearTicks(decimal min, decimal max, float plotHeight, float density) {
        if (max < min) {
            (min, max) = (max, min);
        }

        if (max == min) {
            var delta = Math.Abs(min) > 1 ? Math.Abs(min * 0.01m) : 1m;
            min -= delta;
            max += delta;
        }

        var targetTicks = Math.Max(2, (int)(plotHeight / Math.Max(24f, _textFont!.Size + (8 * density))));
        var spacing = (decimal)CalculateNiceSpacing((double)min, (double)max, targetTicks);
        if (spacing <= 0) {
            _cachedTicks.Add(new AxisTick(min, FormatLabel(min)));
            _cachedTicks.Add(new AxisTick(max, FormatLabel(max)));
            return;
        }

        var first = Math.Ceiling(min / spacing) * spacing;
        var epsilon = spacing / 10000m;
        for (var tick = first; tick <= max + epsilon; tick += spacing) {
            if (tick < min - epsilon) {
                continue;
            }
            _cachedTicks.Add(new AxisTick(tick, FormatLabel(tick)));
        }

        if (_cachedTicks.Count == 0) {
            _cachedTicks.Add(new AxisTick(min, FormatLabel(min)));
            _cachedTicks.Add(new AxisTick(max, FormatLabel(max)));
        }
    }

    private void BuildLogTicks(decimal min, decimal max) {
        var dMin = Math.Max(0.000001, (double)min);
        var dMax = Math.Max(dMin * 1.1, (double)max);

        var start = (int)Math.Floor(Math.Log10(dMin));
        var end = (int)Math.Ceiling(Math.Log10(dMax));

        for (var p = start; p <= end; p++) {
            var val = (decimal)Math.Pow(10, p);
            if (val < min || val > max) {
                continue;
            }
            _cachedTicks.Add(new AxisTick(val, FormatLabel(val)));
        }

        if (_cachedTicks.Count == 0) {
            _cachedTicks.Add(new AxisTick((decimal)dMin, FormatLabel((decimal)dMin)));
            _cachedTicks.Add(new AxisTick((decimal)dMax, FormatLabel((decimal)dMax)));
        }
    }

    private float ScaleYForAxis(decimal y, SKRect plotArea) {
        var (min, max) = Chart.GetYRange(this, true);
        double t;

        if (Scale == NTAxisScale.Logarithmic) {
            var dMin = Math.Max(0.000001, (double)min);
            var dMax = Math.Max(dMin * 1.1, (double)max);
            var dy = Math.Max(dMin, (double)y);
            t = (Math.Log10(dy) - Math.Log10(dMin)) / (Math.Log10(dMax) - Math.Log10(dMin));
        }
        else {
            var range = max - min;
            if (range <= 0) {
                return plotArea.Bottom;
            }
            t = (double)((y - min) / range);
        }

        const float p = 3f;
        var bottom = plotArea.Bottom - p;
        var height = plotArea.Height - (p * 2);
        return (float)(bottom - (t * height));
    }

    private string FormatLabel(decimal value) {
        if (!string.IsNullOrEmpty(LabelFormat)) {
            try {
                if (LabelFormat.Contains("{0")) {
                    return string.Format(LabelFormat, value);
                }
                return value.ToString(LabelFormat);
            }
            catch {
                return value.ToString("G");
            }
        }

        return value.ToString("N2");
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

    private void InitializePaints(NTRenderContext context) {
        _textPaint ??= new SKPaint {
            IsAntialias = true,
            Color = context.TextColor
        };
        _textPaint.Color = context.TextColor;

        _titlePaint ??= new SKPaint {
            IsAntialias = true,
            Color = context.TextColor
        };
        _titlePaint.Color = context.TextColor;

        _linePaint ??= new SKPaint {
            StrokeWidth = context.Density,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };
        _linePaint.Color = context.TextColor;
        _linePaint.StrokeWidth = context.Density;

        _textFont ??= new SKFont(context.DefaultFont.Typeface, AxisFontSize * context.Density);
        _textFont.Size = AxisFontSize * context.Density;

        _titleFont ??= new SKFont(context.DefaultFont.Typeface, TitleFontSize * context.Density);
        _titleFont.Size = TitleFontSize * context.Density;
    }

    private readonly record struct AxisTick(decimal Value, string Label);

    private readonly record struct AxisCacheKey(
        double Min,
        double Max,
        NTAxisScale Scale,
        double PlotHeight,
        double Density,
        float AxisFontSize,
        string? LabelFormat,
        string? Title,
        float TitleFontSize,
        bool IsSecondary);
}
