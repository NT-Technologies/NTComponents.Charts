using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core;
using NTComponents.Charts.Core.Series;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents a bar series in a cartesian chart.
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTBarSeries<TData> : NTCartesianSeries<TData> where TData : class {
    /// <summary>
    ///     Gets or sets the corner radius for the bars.
    /// </summary>
    [Parameter]
    public float CornerRadius { get; set; } = 0.0f;

    /// <summary>
    ///    Gets or sets the orientation of the chart (Vertical or Horizontal).
    /// </summary>
    [Parameter]
    public NTChartOrientation Orientation { get; set; } = NTChartOrientation.Vertical;

    /// <inheritdoc />
    public override void Render(SKCanvas canvas, SKRect renderArea) {
        if (Data == null || !Data.Any()) {
            return;
        }

        var (xMin, xMax) = Chart.GetXRange(XAxis, true);
        var (yMin, yMax) = Chart.GetYRange(YAxis, true);
        // If vertical, bars start from 0 on Y axis. If horizontal, bars start from 0 on X axis.
        var yBase = Orientation == NTChartOrientation.Vertical ? Math.Max(yMin, 0m) : Math.Max((decimal)xMin, 0m);

        var barRects = GetBarRects(renderArea, xMin, xMax, yMin, yMax, yBase);

        var isHovered = Chart.HoveredSeries == this;
        var color = Chart.GetSeriesColor(this);
        var myVisibilityFactor = VisibilityFactor;
        var hoverFactor = HoverFactor;

        color = color.WithAlpha((byte)(color.Alpha * hoverFactor * myVisibilityFactor));

        using var paint = new SKPaint {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        var dataList = Data.ToList();
        for (var i = 0; i < barRects.Count; i++) {
            var item = dataList[i];
            var args = new NTDataPointRenderArgs<TData> {
                Data = item,
                Index = i,
                Color = color,
                GetThemeColor = Chart.GetThemeColor
            };
            OnDataPointRender?.Invoke(args);

            var rect = barRects[i];
            var isPointHovered = isHovered && Chart.HoveredPointIndex == i;
            var pointColor = args.Color ?? color;

            if (isPointHovered) {
                // Highlight hovered bar
                using var highlightPaint = new SKPaint {
                    Color = pointColor.WithAlpha(255),
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                DrawBar(canvas, rect, highlightPaint);
            }
            else {
                using var barPaint = new SKPaint {
                    Color = pointColor,
                    IsAntialias = true,
                    Style = SKPaintStyle.Fill
                };
                DrawBar(canvas, rect, barPaint);
            }

            if (ShowDataLabels || isPointHovered) {
                var labelColor = args.DataLabelColor;
                var labelSize = args.DataLabelSize ?? DataLabelSize;
                
                if (Orientation == NTChartOrientation.Vertical) {
                    RenderDataLabel(canvas, rect.MidX, rect.Top - 5, YValueSelector(dataList[i]), renderArea, labelColor, labelSize);
                }
                else {
                    // For horizontal bars, we want the label to the end of the bar
                    var value = YValueSelector(dataList[i]);
                    var labelX = value >= 0 ? rect.Right + 5 : rect.Left - 5;
                    var textAlign = value >= 0 ? SKTextAlign.Left : SKTextAlign.Right;
                    
                    RenderDataLabel(canvas, labelX, rect.MidY + (labelSize / 2), value, renderArea, labelColor, labelSize, textAlign);
                }
            }

        }
    }

    /// <inheritdoc />
    internal override (double Min, double Max)? GetXRange() {
        if (Data == null || !Data.Any()) return null;

        if (Orientation == NTChartOrientation.Vertical) {
            return base.GetXRange();
        }

        // Horizontal: X axis shows values (YValueSelector)
        var values = Data.Select(item => (double)YValueSelector(item)).ToList();
        var min = Math.Min(0, values.Min());
        var max = values.Max();
        return (min, max);
    }

    /// <inheritdoc />
    internal override (decimal Min, decimal Max)? GetYRange(double? xMin = null, double? xMax = null) {
        if (Data == null || !Data.Any()) return null;

        if (Orientation == NTChartOrientation.Vertical) {
            return base.GetYRange(xMin, xMax);
        }


        // Horizontal: Y axis shows categories (XValue)
        var dataList = Data.ToList();
        var values = new List<decimal>();
        for (int i = 0; i < dataList.Count; i++) {
            var val = XValue.Invoke(dataList[i]);
            var scaled = (decimal)Chart.GetScaledYValue(val);

            if (!Chart.IsCategoricalY && scaled == 0 && val != null && !(val is IConvertible)) {
                // If not categorical and we got 0 for a non-numeric value, use index
                values.Add(i);
            }
            else {
                values.Add(scaled);
            }
        }
        
        var min = values.Any() ? values.Min() : 0;
        var max = values.Any() ? values.Max() : 1;
        return (min, max);
    }


    /// <inheritdoc />
    internal override void RegisterXValues(HashSet<object> values) {
        if (Data == null || Orientation == NTChartOrientation.Horizontal) return;
        base.RegisterXValues(values);
    }

    /// <inheritdoc />
    internal override void RegisterYValues(HashSet<object> values) {
        if (Data == null) return;
        if (Orientation == NTChartOrientation.Vertical) {
            base.RegisterYValues(values);
        }
        else {
            // Horizontal: register categories for the Y axis
            foreach (var item in Data) {
                var val = XValue.Invoke(item);
                if (val != null) values.Add(val);
            }
        }

    }

    private void DrawBar(SKCanvas canvas, SKRect rect, SKPaint paint) {
        if (CornerRadius > 0) {
            canvas.DrawRoundRect(rect, CornerRadius, CornerRadius, paint);
        }
        else {
            canvas.DrawRect(rect, paint);
        }
    }

    private List<SKRect> GetBarRects(SKRect renderArea, double xMin, double xMax, decimal yMin, decimal yMax, decimal yBase) {
        var dataList = Data.ToList();
        var rects = new List<SKRect>();
        var xRange = (float)Math.Max(0.0001, xMax - xMin);

        var progress = GetAnimationProgress();
        var easedProgress = (decimal)BackEase(progress);

        // Multi-series info for side-by-side layout with animation weights
        var barSeriesWeightTotal = Math.Max(0.001f, Chart.GetBarSeriesTotalWeight(YAxis));
        var barSeriesOffsetWeight = Chart.GetBarSeriesOffsetWeight(this);
        var myVisibilityFactor = VisibilityFactor;

        // Calculate available width for each categorical slot
        float slotWidth;
        var plotWidth = Orientation == NTChartOrientation.Vertical ? renderArea.Width : renderArea.Height;
        var categoricalRange = (float)(Orientation == NTChartOrientation.Vertical ? (xMax - xMin) : (double)(yMax - yMin));

        if (Orientation == NTChartOrientation.Vertical ? Chart.IsCategoricalX : Chart.IsCategoricalY) {
            var x0 = Orientation == NTChartOrientation.Vertical ? Chart.ScaleX(0, renderArea) : Chart.ScaleY(0, YAxis, renderArea);
            var x1 = Orientation == NTChartOrientation.Vertical ? Chart.ScaleX(1, renderArea) : Chart.ScaleY(1, YAxis, renderArea);
            slotWidth = Math.Abs(x1 - x0);
        }
        else {
            slotWidth = dataList.Count > 1
                ? (plotWidth / Math.Max(0.0001f, categoricalRange))
                : plotWidth * 0.1f;
        }


        // Group width is 80% of the slot to allow spacing between categories
        var groupWidth = slotWidth * 0.8f;

        // Each bar's width is proportional to its weight
        var barWidth = groupWidth / barSeriesWeightTotal * myVisibilityFactor;

        if (AnimationCurrentValues == null || AnimationCurrentValues.Length != dataList.Count) {
            AnimationCurrentValues = new decimal[dataList.Count];
        }

        for (var i = 0; i < dataList.Count; i++) {
            var originalX = XValue?.Invoke(dataList[i]);
            var categoryValue = Orientation == NTChartOrientation.Vertical 
                ? Chart.GetScaledXValue(originalX) 
                : (Chart.IsCategoricalY ? (double)Chart.GetScaledYValue(originalX) : i);

            // We use VisibilityFactor^2 for the height to ensure it shrinks to zero 

            // even as the axis range (which uses linear VisibilityFactor) also shrinks.
            var targetYValue = YValueSelector(dataList[i]) * (decimal)(myVisibilityFactor * myVisibilityFactor);

            var startYValue = (AnimationStartValues != null && i < AnimationStartValues.Length)
                ? AnimationStartValues[i]
                : yBase;

            var currentYValue = startYValue + ((targetYValue - startYValue) * easedProgress);
            AnimationCurrentValues[i] = currentYValue;

            var centerPos = Orientation == NTChartOrientation.Vertical ? Chart.ScaleX(categoryValue, renderArea) : Chart.ScaleY((decimal)categoryValue, YAxis, renderArea);

            // Calculate the start position for this series' bar within the group
            // The group is centered at centerPos
            var groupStart = centerPos - (groupWidth / 2);
            var barPos = groupStart + (Chart.GetBarSeriesOffsetWeight(this) * (groupWidth / Chart.GetBarSeriesTotalWeight(YAxis))) + (barWidth / 2);

            if (Orientation == NTChartOrientation.Vertical) {
                var topPos = Chart.ScaleY(currentYValue, YAxis, renderArea);
                var bottomPos = Chart.ScaleY(yBase, YAxis, renderArea);
                rects.Add(new SKRect(barPos - (barWidth / 2), Math.Min(topPos, bottomPos), barPos + (barWidth / 2), Math.Max(topPos, bottomPos)));
            }
            else {
                // Horizontal: barPos is Y coordinate, topPos/bottomPos are X coordinates
                var topPos = Chart.ScaleX((double)currentYValue, renderArea);
                var bottomPos = Chart.ScaleX((double)yBase, renderArea);
                rects.Add(new SKRect(Math.Min(topPos, bottomPos), barPos - (barWidth / 2), Math.Max(topPos, bottomPos), barPos + (barWidth / 2)));
            }
        }

        return rects;
    }

    /// <inheritdoc />
    public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea) {
        if (Data == null || !Data.Any()) {
            return null;
        }

        var (xMin, xMax) = Chart.GetXRange(XAxis, true);
        var (yMin, yMax) = Chart.GetYRange(YAxis, true);
        var yBase = Orientation == NTChartOrientation.Vertical ? Math.Max(yMin, 0m) : Math.Max((decimal)xMin, 0m);

        var rects = GetBarRects(renderArea, xMin, xMax, yMin, yMax, yBase);
        var dataList = Data.ToList();


        for (var i = 0; i < rects.Count; i++) {
            if (rects[i].Contains(point)) {
                return (i, dataList[i]);
            }
        }

        return null;
    }
}
