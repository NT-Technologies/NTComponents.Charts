using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core.Series;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents an area series in a cartesian chart.
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTAreaSeries<TData> : NTLineSeries<TData> where TData : class
{
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

   public override void Render(SKCanvas canvas, SKRect renderArea)
   {
      if (Data == null || !Data.Any()) return;

      var (xMin, xMax) = Chart.GetXRange(XAxis, true);
      var (yMin, yMax) = Chart.GetYRange(YAxis, true);

      var points = GetAreaPoints(renderArea, xMin, xMax, yMin, yMax);
      if (points.Count < 2)
      {
         base.Render(canvas, renderArea);
         return;
      }

      var isHovered = Chart.HoveredSeries == this;
      var color = Chart.GetSeriesColor(this);
      var visibilityFactor = VisibilityFactor;
      var hoverFactor = HoverFactor;

      var strokeColor = color.WithAlpha((byte)(color.Alpha * hoverFactor * visibilityFactor));

      var fillColor = strokeColor.WithAlpha((byte)(strokeColor.Alpha * AreaOpacity));

      // Draw Area Fill
      using (var fillPaint = new SKPaint
      {
         Style = SKPaintStyle.Fill,
         Color = fillColor,
         IsAntialias = true
      })
      {
         using var areaPath = BuildAreaPath(points, renderArea);
         canvas.DrawPath(areaPath, fillPaint);
      }

      // Use base.Render to draw the line and points
      base.Render(canvas, renderArea);
   }

   private List<SKPoint> GetAreaPoints(SKRect renderArea, double xMin, double xMax, decimal yMin, decimal yMax)
   {
      var dataList = Data.ToList();
      var points = new List<SKPoint>();
      var progress = GetAnimationProgress();
      var easedProgress = (decimal)BackEase(progress);

      for (var i = 0; i < dataList.Count; i++)
      {
         var originalX = XValue.Invoke(dataList[i]);
         var xValue = Chart.GetScaledXValue(originalX);
         var targetYValue = YValueSelector(dataList[i]);

         // Area animations often start from the baseline
         var currentYValue = BaselineValue + ((targetYValue - BaselineValue) * easedProgress);

         var vFactor = (decimal)VisibilityFactor;
         currentYValue *= vFactor * vFactor;

         var screenXCoord = Chart.ScaleX(xValue, renderArea);
         var screenYCoord = Chart.ScaleY(currentYValue, YAxis, renderArea);

         points.Add(new SKPoint(screenXCoord, screenYCoord));
      }
      return points;
   }

   private SKPath BuildAreaPath(List<SKPoint> points, SKRect renderArea)
   {
      var path = BuildPath(points);
      if (points.Count < 2) return path;

      // Close the path to the baseline
      float baselineCoord = Chart.ScaleY(BaselineValue, YAxis, renderArea);

      path.LineTo(points.Last().X, baselineCoord);
      path.LineTo(points.First().X, baselineCoord);
      path.Close();

      return path;
   }
}
