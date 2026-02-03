using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core.Series;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents an area series in a cartesian chart.
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTAreaSeries<TData> : NTLineSeries<TData> where TData : class {
   /// <summary>
   ///    Gets or sets the opacity of the area fill (0.0 to 1.0).
   /// </summary>
   [Parameter]
   public float AreaOpacity { get; set; } = 0.3f;

   /// <summary>
   ///    Gets or sets the Y value for the baseline of the area. Defaults to 0.
   /// </summary>
   [Parameter]
   public decimal BaselineValue { get; set; } = 0;

   private SKPaint? _areaPaint;
   private SKPath? _areaPath;

   public override SKRect Render(NTRenderContext context, SKRect renderArea) {
      var canvas = context.Canvas;
      if (Data == null || !Data.Any()) return renderArea;

      var (xMin, xMax) = Chart.GetXRange(EffectiveXAxis, true);
      var (yMin, yMax) = Chart.GetYRange(EffectiveYAxis, true);

      var points = GetAreaPoints(renderArea, xMin, xMax, yMin, yMax);
      if (points.Count < 2) {
         return base.Render(context, renderArea);
      }

      var isHovered = Chart.HoveredSeries == this;
      var color = Chart.GetSeriesColor(this);
      var visibilityFactor = VisibilityFactor;
      var hoverFactor = HoverFactor;

      var strokeColor = color.WithAlpha((byte)(color.Alpha * hoverFactor * visibilityFactor));

      var fillColor = strokeColor.WithAlpha((byte)(strokeColor.Alpha * AreaOpacity));

      // Draw Area Fill
      _areaPaint ??= new SKPaint {
         Style = SKPaintStyle.Fill,
         IsAntialias = true
      };
      _areaPaint.Color = fillColor;

      _areaPath?.Dispose();
      _areaPath = BuildAreaPath(points, renderArea);
      canvas.DrawPath(_areaPath, _areaPaint);

      // Use base.Render to draw the line and points
      return base.Render(context, renderArea);
   }

   protected override void Dispose(bool disposing) {
      if (disposing) {
         _areaPaint?.Dispose();
         _areaPath?.Dispose();
         _areaPaint = null;
         _areaPath = null;
      }
      base.Dispose(disposing);
   }

   private List<SKPoint> GetAreaPoints(SKRect renderArea, double xMin, double xMax, decimal yMin, decimal yMax) {
      var dataList = Data.ToList();
      var points = new List<SKPoint>();
      var progress = GetAnimationProgress();
      var easedProgress = (decimal)BackEase(progress);

      for (var i = 0; i < dataList.Count; i++) {
         var originalX = XValue.Invoke(dataList[i]);
         var xValue = Chart.GetScaledXValue(originalX);
         var targetYValue = YValueSelector(dataList[i]);

         // Area animations often start from the baseline
         var currentYValue = BaselineValue + ((targetYValue - BaselineValue) * easedProgress);

         var vFactor = (decimal)VisibilityFactor;
         currentYValue *= vFactor * vFactor;

         var screenXCoord = Chart.ScaleX(xValue, renderArea, EffectiveXAxis);
         var screenYCoord = Chart.ScaleY(currentYValue, EffectiveYAxis, renderArea);

         points.Add(new SKPoint(screenXCoord, screenYCoord));
      }
      return points;
   }

   private SKPath BuildAreaPath(List<SKPoint> points, SKRect renderArea) {
      var path = BuildPath(points);
      if (points.Count < 2) return path;

      // Close the path to the baseline
      float baselineCoord = Chart.ScaleY(BaselineValue, EffectiveYAxis, renderArea);

      path.LineTo(points.Last().X, baselineCoord);
      path.LineTo(points.First().X, baselineCoord);
      path.Close();

      return path;
   }
}
