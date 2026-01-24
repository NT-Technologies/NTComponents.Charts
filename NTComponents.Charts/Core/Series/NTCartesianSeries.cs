using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core.Axes;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NTComponents.Charts.Core.Series;

public abstract class NTCartesianSeries<TData> : NTBaseSeries<TData> where TData : class {

    /// <inheritdoc />
    public override ChartCoordinateSystem CoordinateSystem => ChartCoordinateSystem.Cartesian;
    private readonly static NTXAxisOptions _defaultXAxis = new();
    private readonly static NTYAxisOptions _defaultYAxis = new();
    [Parameter, EditorRequired]
    public Func<TData, decimal> YValueSelector { get; set; } = default!;

    /// <summary>
    ///     Gets or sets the X axis options.
    /// </summary>
    [Parameter]
    public NTXAxisOptions XAxis { get; set; } = _defaultXAxis;

    /// <summary>
    ///    Gets or sets the Y axis options.
    /// </summary>
    [Parameter]
    public NTYAxisOptions YAxis { get; set; } = _defaultYAxis;

    /// <inheritdoc />
    internal override TooltipInfo GetTooltipInfo(TData data)
    {
        var xValue = XValue.Invoke(data);
        var yValue = YValueSelector(data);
        return new TooltipInfo

        {
            Header = xValue?.ToString(),
            Lines =
            [
                new TooltipLine
                {
                    Label = Title ?? "Series",
                    Value = string.Format(DataLabelFormat, yValue),
                    Color = Chart.GetSeriesColor(this)
                }
            ]
        };
    }

    /// <inheritdoc />
    internal override SKRect Measure(SKRect renderArea, HashSet<object> measured) {
        if (!IsEffectivelyVisible) return renderArea;
        var rect = renderArea;
        if (XAxis != null && XAxis.Visible && measured.Add(XAxis)) {
            rect = XAxis.Measure(rect, Chart);
        }
        if (YAxis != null && YAxis.Visible && measured.Add(YAxis)) {
            rect = YAxis.Measure(rect, Chart);
        }
        return rect;
    }

    /// <inheritdoc />
    internal override void RenderAxes(SKCanvas canvas, SKRect plotArea, SKRect totalArea, HashSet<object> rendered) {
        if (!IsEffectivelyVisible) return;
        if (XAxis != null && XAxis.Visible && rendered.Add(XAxis)) {
            XAxis.Render(canvas, plotArea, totalArea, Chart);
        }
        if (YAxis != null && YAxis.Visible && rendered.Add(YAxis)) {
            YAxis.Render(canvas, plotArea, totalArea, Chart);
        }
    }

    /// <inheritdoc />
    internal override (double Min, double Max)? GetXRange() {
        if (Data == null || !Data.Any()) return null;
        var values = Data.Select(item => Chart.GetScaledXValue(XValue.Invoke(item))).ToList();
        var min = values.Min();
        var max = values.Max();

        return (min, max);
    }

    /// <inheritdoc />
    internal override (decimal Min, decimal Max)? GetYRange(double? xMin = null, double? xMax = null) {
      if (Data == null || !Data.Any()) return null;
      var min = decimal.MaxValue;
      var max = decimal.MinValue;

      var dataToConsider = Data;
      if (xMin.HasValue && xMax.HasValue) {
         dataToConsider = dataToConsider.Where(d => {
            var x = Chart.GetScaledXValue(XValue.Invoke(d));
            return x >= xMin.Value && x <= xMax.Value;
         });
      }


      if (!dataToConsider.Any()) return null;

      if (this is NTBoxPlotSeries<TData> boxPlot) {
         foreach (var item in dataToConsider) {
            var values = boxPlot.BoXValue(item);

                min = Math.Min(min, values.Min);
                max = Math.Max(max, values.Max);
                if (values.Outliers != null && values.Outliers.Any()) {
                    min = Math.Min(min, values.Outliers.Min());
                    max = Math.Max(max, values.Outliers.Max());
                }
            }
        }
        else {
            var visibilityFactor = (decimal)VisibilityFactor;
            var values = dataToConsider.Select(item => YValueSelector(item) * visibilityFactor).ToList();
            min = values.Min();
            max = values.Max();
        }


        return (min, max);
    }

    /// <inheritdoc />
    internal override void RegisterXValues(HashSet<object> values) {

        if (Data == null) return;
        foreach (var item in Data) {
            var val = XValue.Invoke(item);
            if (val != null) values.Add(val);
        }
    }


    /// <inheritdoc />
    internal override void RegisterYValues(HashSet<object> values) {
        if (Data == null) return;
        foreach (var item in Data) {
            values.Add(YValueSelector(item));
        }
    }

    /// <summary>
    ///    Gets or sets the style of the data points.
    /// </summary>
    [Parameter]
    public PointStyle PointStyle { get; set; } = PointStyle.Filled;

    /// <summary>
    ///     Gets or sets the shape of the data points. If null, a shape will be assigned based on the series index.
    /// </summary>
    [Parameter]
    public PointShape? PointShape { get; set; }

    /// <summary>
    ///    Gets or sets the size of the data points.
    /// </summary>
    [Parameter]
    public float PointSize { get; set; } = 8.0f;

    /// <summary>
    ///     Gets or sets whether to show data labels for each point.
    /// </summary>
    [Parameter]
    public bool ShowDataLabels { get; set; }

    /// <summary>
    ///     Gets or sets the format for the data labels.
    /// </summary>
    [Parameter]
    public string DataLabelFormat { get; set; } = "{0:0.#}";

    /// <summary>
    ///     Gets or sets the size of the data labels.
    /// </summary>
    [Parameter]
    public float DataLabelSize { get; set; } = 12.0f;

    /// <summary>
    ///     Gets or sets the color of the data labels. If null, the chart's text color will be used.
    /// </summary>
    [Parameter]
    public TnTColor? DataLabelColor { get; set; }

    /// <summary>
    ///     Gets or sets whether to show a background for data labels.
    /// </summary>
    [Parameter]
    public bool ShowDataLabelBackground { get; set; } = true;

    /// <summary>
    ///     Gets or sets the background color for data labels. If null, the series' color will be used.
    /// </summary>
    [Parameter]
    public TnTColor? DataLabelBackgroundColor { get; set; }

    protected decimal[]? AnimationStartValues { get; set; }
    protected decimal[]? AnimationCurrentValues { get; set; }

    /// <inheritdoc />
    protected override void OnDataChanged() {
        if (AnimationCurrentValues != null) {
            AnimationStartValues = AnimationCurrentValues;
        }
        AnimationCurrentValues = null;
        base.OnDataChanged();
    }

    protected void RenderDataLabel(SKCanvas canvas, float x, float y, decimal value, SKRect renderArea, SKColor? overrideColor = null, float? overrideFontSize = null, SKTextAlign textAlign = SKTextAlign.Center) {
        if (overrideColor == null && !ShowDataLabels) {
            return;
        }

        var color = overrideColor ?? (DataLabelColor.HasValue ? Chart.GetThemeColor(DataLabelColor.Value) : Chart.GetSeriesTextColor(this));
        var size = overrideFontSize ?? DataLabelSize;

        using var font = new SKFont {
            Size = size,
            Embolden = true,
            Typeface = Chart.DefaultTypeface
        };

        using var paint = new SKPaint {
            Color = color,
            IsAntialias = true
        };

        var text = string.Format(DataLabelFormat, value);
        var textWidth = font.MeasureText(text);
        var textHeight = font.Size;

        var paddingX = 6f;
        var paddingY = 2f;
        
        // For non-centered text, we might want different positioning logic
        // But let's keep it simple for now and just handle the Y offset if it's centered
        var drawY = y;
        if (textAlign == SKTextAlign.Center)
        {
            drawY = y - ((size / 2) + 5);

            // Check if label would be cut off at the top
            if (drawY - textHeight < renderArea.Top) {
                // Shift label below the point if it would be cut off
                drawY = y + ((size / 2) + textHeight + 5);
            }
        }

        if (ShowDataLabelBackground) {
            var bgColor = DataLabelBackgroundColor.HasValue
                ? Chart.GetThemeColor(DataLabelBackgroundColor.Value)
                : Chart.GetSeriesColor(this);

            using var bgPaint = new SKPaint {
                Color = bgColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                ImageFilter = SKImageFilter.CreateDropShadow(2, 2, 4, 4, SKColors.Black.WithAlpha(80))
            };

            var bgRect = textAlign switch
            {
                SKTextAlign.Left => new SKRect(x - paddingX, drawY - (textHeight / 2) - paddingY, x + textWidth + paddingX, drawY + (textHeight / 2) + paddingY),
                SKTextAlign.Right => new SKRect(x - textWidth - paddingX, drawY - (textHeight / 2) - paddingY, x + paddingX, drawY + (textHeight / 2) + paddingY),
                _ => new SKRect(x - (textWidth / 2) - paddingX, drawY - textHeight - paddingY, x + (textWidth / 2) + paddingX + 2, drawY + paddingY)
            };

            canvas.DrawRoundRect(bgRect, 6, 6, bgPaint);

            using var borderPaint = new SKPaint {
                Color = Chart.GetThemeColor(TnTColor.Outline),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = true
            };
            canvas.DrawRoundRect(bgRect, 6, 6, borderPaint);
        }

        canvas.DrawText(text, x, drawY, textAlign, font, paint);
    }

    protected void RenderPoint(SKCanvas canvas, float x, float y, SKColor color, float? pointSize = null, PointShape? pointShape = null, SKColor? strokeColor = null) {
        if (PointStyle == PointStyle.None) {
            return;
        }

        var size = pointSize ?? PointSize;
        var shape = pointShape ?? PointShape ?? (PointShape)(Chart.GetSeriesIndex(this) % Enum.GetValues<PointShape>().Length);

        using var paint = new SKPaint {
            Color = color,
            IsAntialias = true,
            Style = PointStyle == PointStyle.Filled ? SKPaintStyle.Fill : SKPaintStyle.Stroke,
            StrokeWidth = 2
        };

        var halfSize = size / 2;

        switch (shape) {
            case Series.PointShape.Circle:
                canvas.DrawCircle(x, y, halfSize, paint);
                break;
            case Series.PointShape.Square:
                canvas.DrawRect(x - halfSize, y - halfSize, size, size, paint);
                break;
            case Series.PointShape.Triangle:
                using (var path = new SKPath()) {
                    path.MoveTo(x, y - halfSize);
                    path.LineTo(x + halfSize, y + halfSize);
                    path.LineTo(x - halfSize, y + halfSize);
                    path.Close();
                    canvas.DrawPath(path, paint);
                }
                break;
            case Series.PointShape.Diamond:
                using (var path = new SKPath()) {
                    path.MoveTo(x, y - halfSize);
                    path.LineTo(x + halfSize, y);
                    path.LineTo(x, y + halfSize);
                    path.LineTo(x - halfSize, y);
                    path.Close();
                    canvas.DrawPath(path, paint);
                }
                break;
        }

        if (PointStyle == PointStyle.Outlined && strokeColor.HasValue) {
            paint.Color = strokeColor.Value;
            paint.Style = SKPaintStyle.Stroke;
            // Redraw with stroke color if explicitly requested? 
            // Actually, the base paint already did it if Style was Stroke.
        }
    }
}
