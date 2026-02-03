using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core.Series;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents a box plot series in a cartesian chart.
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTBoxPlotSeries<TData> : NTCartesianSeries<TData> where TData : class {
   [Parameter, EditorRequired]
   public Func<TData, BoxPlotValues> BoXValue { get; set; } = default!;

   /// <summary>
   ///    Gets or sets the width of the box as a fraction of the available space (0.0 to 1.0).
   /// </summary>
   [Parameter]
   public float BoxWidthRatio { get; set; } = 0.6f;

   /// <summary>
   ///    Gets or sets the width of the whiskers as a fraction of the box width (0.0 to 1.0).
   /// </summary>
   [Parameter]
   public float WhiskerWidthRatio { get; set; } = 0.5f;

   private SKPaint? _strokePaint;
   private SKPaint? _fillPaint;

   public override SKRect Render(NTRenderContext context, SKRect renderArea) {
      var canvas = context.Canvas;
      if (Data == null || !Data.Any()) return renderArea;

      var dataList = Data.ToList();
      var (xMin, xMax) = Chart.GetXRange(EffectiveXAxis, true);
      var (yMin, yMax) = Chart.GetYRange(EffectiveYAxis, true);

      var isHovered = Chart.HoveredSeries == this;
      var hasHover = Chart.HoveredSeries != null;
      var baseColor = Chart.GetSeriesColor(this);
      var visibilityFactor = VisibilityFactor;

      var progress = (decimal)GetAnimationProgress();
      var easedProgress = progress; // Linear for now

      // Determine available width for each categorical item
      var allX = Chart.GetAllXValues();
      float plotWidth = renderArea.Width;
      float itemWidth = plotWidth / Math.Max(1, allX.Count);
      float boxWidth = itemWidth * BoxWidthRatio;

      _strokePaint ??= new SKPaint {
         Style = SKPaintStyle.Stroke,
         IsAntialias = true
      };
      _strokePaint.StrokeWidth = 2 * context.Density;

      _fillPaint ??= new SKPaint {
         Style = SKPaintStyle.Fill,
         IsAntialias = true
      };

      for (int i = 0; i < dataList.Count; i++) {
         var item = dataList[i];

         var args = new NTDataPointRenderArgs<TData> {
            Data = item,
            Index = i,
            Color = baseColor,
            GetThemeColor = Chart.GetThemeColor
         };
         OnDataPointRender?.Invoke(args);

         var xVal = Chart.GetScaledXValue(XValue.Invoke(item));
         var boxValues = BoXValue(item);


         float centerPos = Chart.ScaleX(xVal, renderArea, EffectiveXAxis);

         var vFactor = (decimal)visibilityFactor;

         // Animation: scale values from median or 0
         decimal animMin = boxValues.Median + (boxValues.Min - boxValues.Median) * easedProgress;
         decimal animQ1 = boxValues.Median + (boxValues.Q1 - boxValues.Median) * easedProgress;
         decimal animQ3 = boxValues.Median + (boxValues.Q3 - boxValues.Median) * easedProgress;
         decimal animMax = boxValues.Median + (boxValues.Max - boxValues.Median) * easedProgress;
         decimal animMedian = boxValues.Median;

         float minPos = Chart.ScaleY(animMin * vFactor, EffectiveYAxis, renderArea);
         float q1Pos = Chart.ScaleY(animQ1 * vFactor, EffectiveYAxis, renderArea);
         float medianPos = Chart.ScaleY(animMedian * vFactor, EffectiveYAxis, renderArea);
         float q3Pos = Chart.ScaleY(animQ3 * vFactor, EffectiveYAxis, renderArea);
         float maxPos = Chart.ScaleY(animMax * vFactor, EffectiveYAxis, renderArea);

         var isPointHovered = Chart.HoveredSeries == this && Chart.HoveredPointIndex == i;
         var hoverFactor = HoverFactor;
         var currentColor = args.Color ?? baseColor;
         var color = (isPointHovered) ? currentColor : currentColor.WithAlpha((byte)(currentColor.Alpha * hoverFactor));

         _strokePaint.Color = color;
         _fillPaint.Color = color.WithAlpha((byte)(color.Alpha * 0.3f));

         // Draw Box
         var boxRect = new SKRect(centerPos - boxWidth / 2, q3Pos, centerPos + boxWidth / 2, q1Pos);
         canvas.DrawRect(boxRect, _fillPaint);
         canvas.DrawRect(boxRect, _strokePaint);

         // Draw Median
         canvas.DrawLine(centerPos - boxWidth / 2, medianPos, centerPos + boxWidth / 2, medianPos, _strokePaint);

         // Draw Whiskers
         canvas.DrawLine(centerPos, q3Pos, centerPos, maxPos, _strokePaint);
         canvas.DrawLine(centerPos, q1Pos, centerPos, minPos, _strokePaint);

         float whiskerWidth = boxWidth * WhiskerWidthRatio;
         canvas.DrawLine(centerPos - whiskerWidth / 2, maxPos, centerPos + whiskerWidth / 2, maxPos, _strokePaint);
         canvas.DrawLine(centerPos - whiskerWidth / 2, minPos, centerPos + whiskerWidth / 2, minPos, _strokePaint);

         // Outliers
         if (boxValues.Outliers != null) {
            foreach (var outlier in boxValues.Outliers) {
               float oy = Chart.ScaleY(outlier * vFactor, EffectiveYAxis, renderArea);
               RenderPoint(context, centerPos, oy, color);
            }
         }
      }

      return renderArea;
   }

   protected override void Dispose(bool disposing) {
      if (disposing) {
         _strokePaint?.Dispose();
         _fillPaint?.Dispose();
         _strokePaint = null;
         _fillPaint = null;
      }
      base.Dispose(disposing);
   }

   public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea) {
      if (Data == null || !Data.Any()) return null;
      var dataList = Data.ToList();
      var allX = Chart.GetAllXValues();
      float plotWidth = renderArea.Width;
      float itemWidth = plotWidth / Math.Max(1, allX.Count);
      float boxWidth = itemWidth * BoxWidthRatio;

      for (int i = 0; i < dataList.Count; i++) {
         var item = dataList[i];
         var xVal = Chart.GetScaledXValue(XValue.Invoke(item));
         var boxValues = BoXValue(item);

         float centerPos = Chart.ScaleX(xVal, renderArea, EffectiveXAxis);

         float minPos = Chart.ScaleY(boxValues.Min, EffectiveYAxis, renderArea);
         float maxPos = Chart.ScaleY(boxValues.Max, EffectiveYAxis, renderArea);

         var hitRect = new SKRect(centerPos - boxWidth / 2, Math.Min(minPos, maxPos), centerPos + boxWidth / 2, Math.Max(minPos, maxPos));

         if (hitRect.Contains(point)) return (i, item);
      }

      return null;
   }
}
