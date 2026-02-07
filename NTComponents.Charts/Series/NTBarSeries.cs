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

    private SKPaint? _barPaint;
    private SKPaint? _highlightPaint;

    protected override void Dispose(bool disposing) {
        if (disposing) {
            _barPaint?.Dispose();
            _highlightPaint?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override SKRect Render(NTRenderContext context, SKRect renderArea) {
        //var canvas = context.Canvas;
        //if (Data == null || !Data.Any()) {
        //    return renderArea;
        //}

        //var (xMin, xMax) = Chart.GetXRange(Chart.XAxis, true);
        //var (yMin, yMax) = Chart.GetYRange(Chart.YAxis, true);
        //// If vertical, bars start from 0 on Y axis. If horizontal, bars start from 0 on X axis.
        //var yBase = Orientation == NTChartOrientation.Vertical ? Math.Max(yMin, 0m) : Math.Max((decimal)xMin, 0m);

        //var barRects = GetBarRects(renderArea, xMin, xMax, yMin, yMax, yBase);

        //var isHovered = Chart.HoveredSeries == this;
        //var color = Chart.GetSeriesColor(this);
        //var myVisibilityFactor = VisibilityFactor;
        //var hoverFactor = HoverFactor;

        //color = color.WithAlpha((byte)(color.Alpha * hoverFactor * myVisibilityFactor));

        //_barPaint ??= new SKPaint {
        //    IsAntialias = true,
        //    Style = SKPaintStyle.Fill
        //};
        //_barPaint.Color = color;

        //var dataList = Data.ToList();
        //for (var i = 0; i < barRects.Count; i++) {
        //    var item = dataList[i];
        //    var args = new NTDataPointRenderArgs<TData> {
        //        Data = item,
        //        Index = i,
        //        Color = color,
        //        GetThemeColor = Chart.GetThemeColor
        //    };
        //    OnDataPointRender?.Invoke(args);

        //    var rect = barRects[i];
        //    var isPointHovered = isHovered && Chart.HoveredPointIndex == i;
        //    var pointColor = args.Color ?? color;

        //    if (isPointHovered) {
        //        // Highlight hovered bar
        //        _highlightPaint ??= new SKPaint {
        //            IsAntialias = true,
        //            Style = SKPaintStyle.Fill
        //        };
        //        _highlightPaint.Color = pointColor.WithAlpha(255);
        //        DrawBar(canvas, rect, _highlightPaint, context.Density);
        //    }
        //    else {
        //        // If the color changed per point, we update _barPaint
        //        _barPaint.Color = pointColor;
        //        DrawBar(canvas, rect, _barPaint, context.Density);
        //    }

        //    if (ShowDataLabels || isPointHovered) {
        //        var labelColor = args.DataLabelColor;
        //        var labelSize = args.DataLabelSize ?? DataLabelSize;

        //        if (Orientation == NTChartOrientation.Vertical) {
        //            RenderDataLabel(context, rect.MidX, rect.Top - (5 * context.Density), YValueSelector(dataList[i]), renderArea, labelColor, labelSize);
        //        }
        //        else {
        //            // For horizontal bars, we want the label to the end of the bar
        //            var value = YValueSelector(dataList[i]);
        //            var labelX = value >= 0 ? rect.Right + (5 * context.Density) : rect.Left - (5 * context.Density);
        //            var textAlign = value >= 0 ? SKTextAlign.Left : SKTextAlign.Right;

        //            RenderDataLabel(context, labelX, rect.MidY + (labelSize / 2), value, renderArea, labelColor, labelSize, textAlign);
        //        }
        //    }

        //}

        return renderArea;
    }

    /// <inheritdoc />
    public override (double Min, double Max)? GetXRange() {
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
    public override (decimal Min, decimal Max)? GetYRange(double? xMin = null, double? xMax = null) {
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

    private void DrawBar(SKCanvas canvas, SKRect rect, SKPaint paint, float density) {
        if (CornerRadius > 0) {
            var scaledRadius = CornerRadius * density;
            canvas.DrawRoundRect(rect, scaledRadius, scaledRadius, paint);
        }
        else {
            canvas.DrawRect(rect, paint);
        }
    }

    private List<SKRect> GetBarRects(SKRect renderArea, double xMin, double xMax, decimal yMin, decimal yMax, decimal yBase) {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea) {
        //if (Data == null || !Data.Any()) {
        //    return null;
        //}

        //var (xMin, xMax) = Chart.GetXRange(Chart.XAxis, true);
        //var (yMin, yMax) = Chart.GetYRange(Chart.YAxis, true);
        //var yBase = Orientation == NTChartOrientation.Vertical ? Math.Max(yMin, 0m) : Math.Max((decimal)xMin, 0m);

        //var rects = GetBarRects(renderArea, xMin, xMax, yMin, yMax, yBase);
        //var dataList = Data.ToList();


        //for (var i = 0; i < rects.Count; i++) {
        //    if (rects[i].Contains(point)) {
        //        return (i, dataList[i]);
        //    }
        //}

        return null;
    }
}
