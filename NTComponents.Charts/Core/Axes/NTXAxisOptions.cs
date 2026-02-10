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
    public bool IsCategorical
    {
        get
        {
            var type = typeof(TAxisType);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            {
                type = Nullable.GetUnderlyingType(type)!;
            }

            return !(type == typeof(DateTime) ||
                     type == typeof(DateTimeOffset) ||
                     type == typeof(DateOnly) ||
                     type == typeof(TimeOnly) ||
                     type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(INumberBase<>)));
        }
    }

    private List<string> _cachedLabels = [];
    private SKPaint _linePaint = default!;
    private float _rotation = 0;
    private SKFont _textFont = default!;
    private SKPaint _textPaint = default!;
    private SKFont _titleFont = default!;
    private SKPaint _titlePaint = default!;

    public override void Dispose() {
        _textPaint?.Dispose();
        _titlePaint?.Dispose();
        _linePaint?.Dispose();
        _textFont?.Dispose();
        _titleFont?.Dispose();
        Chart.UnregisterAxis(this);
        base.Dispose();
    }
    private float? _titleHeight;
    private float GetTitleHeight(NTRenderContext context, SKFont font) {
        if (string.IsNullOrEmpty(Title)) {
            return 0f;
        }
        else {
            font.MeasureText(Title, out var bounds);
            return bounds.Height + (5 * context.Density);
        }
    }

    /// <inheritdoc />
    public override SKRect Measure(NTRenderContext context, SKRect renderArea) {
        if (!Visible) {
            return renderArea;
        }

        InitializePaints(context);
        _titleHeight ??= GetTitleHeight(context, _titleFont);
        _rotation = 0;
        float finalLabelHeight = 0;

        if (IsCategorical) {
            _cachedLabels = Chart.GetAllXValues().Select(FormatLabel).ToList();
            if (_cachedLabels.Count == 0) {
                return renderArea;
            }

            float maxLWidth = 0;
            float maxLHeight = 0;
            foreach (var label in _cachedLabels) {
                _textFont.MeasureText(label, out var bounds);
                maxLWidth = Math.Max(maxLWidth, bounds.Width);
                maxLHeight = Math.Max(maxLHeight, bounds.Height);
            }

            var labelSlotWidth = renderArea.Width / _cachedLabels.Count;
            if (maxLWidth + (5 * context.Density) > labelSlotWidth) {
                _rotation = -45f;
                var rad = Math.Abs(_rotation) * Math.PI / 180.0;
                finalLabelHeight = (float)((Math.Sin(rad) * maxLWidth) + (Math.Cos(rad) * maxLHeight));
            }
            else {
                finalLabelHeight = maxLHeight;
            }
        }
        else {
            // Numeric or Date
            var (min, max) = Chart.GetXRange(this, true);
            if (double.IsNaN(min) || double.IsNaN(max) || min == max) {
                _cachedLabels = [];
                return renderArea;
            }

            // Max ticks based on available width
            var s1 = FormatLabel(min);
            var s2 = FormatLabel(max);
            _textFont.MeasureText(s1, out var b1);
            _textFont.MeasureText(s2, out var b2);
            var estWidth = Math.Max(b1.Width, b2.Width) + (12 * context.Density);
            var tickCount = Math.Max(2, (int)(renderArea.Width / estWidth));

            _cachedLabels = [];
            var step = (max - min) / (tickCount - 1);
            for (var i = 0; i < tickCount; i++) {
                _cachedLabels.Add(FormatLabel(min + (i * step)));
            }

            finalLabelHeight = Math.Max(b1.Height, b2.Height);
        }

        var tickHeight = 5 * context.Density;
        var totalHeight = _titleHeight.Value + finalLabelHeight + tickHeight + (10 * context.Density);

        return new SKRect(renderArea.Left, renderArea.Top, renderArea.Right, renderArea.Bottom - totalHeight);
    }

    /// <inheritdoc />
    public override SKRect Render(NTRenderContext context, SKRect renderArea) => !Visible ? renderArea : renderArea;

    protected override void OnInitialized() {
        base.OnInitialized();
        Chart.RegisterAxis(this);
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

        if (Chart.IsXAxisDateTime) {
            if (value is double d) {
                try {
                    return DateTime.FromOADate(d).ToString("d");
                }
                catch {
                    return d.ToString();
                }
            }
            if (value is DateTime dt) {
                return dt.ToString("d");
            }
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

        _titlePaint ??= new SKPaint {
            IsAntialias = true,
            Color = context.TextColor,
        };
        _titleFont ??= new SKFont(context.DefaultFont.Typeface, TitleFontSize * context.Density);

        _linePaint ??= new SKPaint {
            IsAntialias = true,
            Color = context.TextColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1 * context.Density
        };
    }
}