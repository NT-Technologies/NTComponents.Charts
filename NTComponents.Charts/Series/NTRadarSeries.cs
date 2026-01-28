using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core.Series;
using NTComponents.Charts.Core.Axes;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents a radar series in a circular chart.
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTRadarSeries<TData> : NTCircularSeries<TData> where TData : class {
   /// <inheritdoc />
   public override ChartCoordinateSystem CoordinateSystem => ChartCoordinateSystem.Polar;

   [Parameter]
   public float StrokeWidth { get; set; } = 2f;

   [Parameter]
   public float AreaOpacity { get; set; } = 0.2f;

   /// <summary>
   ///    Gets or sets the maximum value for the radar scale. If null, it will be calculated from the data.
   /// </summary>
   [Parameter]
   public decimal? MaxValue { get; set; }

   /// <summary>
   ///    Gets or sets the radial axis options for this series.
   /// </summary>
   [Parameter]
   public NTRadialAxisOptions? RadialAxis { get; set; }

   public NTRadialAxisOptions EffectiveRadialAxis => RadialAxis ?? Chart?.PrimaryRadialAxis ?? _defaultRadialAxis;

   private static readonly NTRadialAxisOptions _defaultRadialAxis = new();

   private SKPaint? _fillPaint;
   private SKPaint? _strokePaint;
   private SKPaint? _pointPaint;
   private SKPaint? _labelPaint;
   private SKFont? _labelFont;

   protected override void Dispose(bool disposing) {
      if (disposing) {
         _fillPaint?.Dispose();
         _strokePaint?.Dispose();
         _pointPaint?.Dispose();
         _labelPaint?.Dispose();
         _labelFont?.Dispose();
      }
      base.Dispose(disposing);
   }

   /// <inheritdoc />
   internal override SKRect Measure(SKRect renderArea, NTRenderContext context, HashSet<object> measured) {
      if (EffectiveRadialAxis != null && EffectiveRadialAxis.Visible && measured.Add(EffectiveRadialAxis)) {
         return EffectiveRadialAxis.Measure(renderArea, context, Chart);
      }
      return renderArea;
   }

   /// <inheritdoc />
   internal override void RenderAxes(NTRenderContext context, HashSet<object> rendered) {
      if (EffectiveRadialAxis != null && EffectiveRadialAxis.Visible && rendered.Add(EffectiveRadialAxis)) {
         EffectiveRadialAxis.Render(context, Chart);
      }
   }

   public override void Render(NTRenderContext context, SKRect renderArea) {
      var canvas = context.Canvas;
      if (Data == null || !Data.Any()) return;

      var dataList = Data.ToList();
      int count = dataList.Count;
      if (count < 3) return;

      float centerX = renderArea.MidX;
      float centerY = renderArea.MidY;
      float radius = Math.Min(renderArea.Width, renderArea.Height) / 2f;

      decimal max = MaxValue ?? dataList.Max(ValueSelector);
      if (max <= 0) max = 1;

      var progress = GetAnimationProgress();
      var visibilityFactor = VisibilityFactor;
      var hoverFactor = HoverFactor;

      var color = Chart.GetSeriesColor(this);
      color = color.WithAlpha((byte)(color.Alpha * visibilityFactor * hoverFactor));

      var points = new SKPoint[count];
      for (int i = 0; i < count; i++) {
         float angle = (i * 360f / count) - 90f;
         float rad = angle * (float)Math.PI / 180f;

         decimal val = ValueSelector(dataList[i]) * (decimal)progress;
         float r = (float)(val / max) * radius;

         points[i] = new SKPoint(
             centerX + (float)Math.Cos(rad) * r,
             centerY + (float)Math.Sin(rad) * r
         );
      }

      using var path = new SKPath();
      path.AddPoly(points, true);

      // Fill
      _fillPaint ??= new SKPaint {
         Style = SKPaintStyle.Fill,
         IsAntialias = true
      };
      _fillPaint.Color = color.WithAlpha((byte)(color.Alpha * AreaOpacity));
      canvas.DrawPath(path, _fillPaint);

      // Stroke
      _strokePaint ??= new SKPaint {
         Style = SKPaintStyle.Stroke,
         IsAntialias = true
      };
      _strokePaint.Color = color;
      _strokePaint.StrokeWidth = StrokeWidth * context.Density;
      canvas.DrawPath(path, _strokePaint);

      // Points and Labels
      for (int i = 0; i < count; i++) {
         var item = dataList[i];
         var args = new NTDataPointRenderArgs<TData> {
            Data = item,
            Index = i,
            Color = color,
            GetThemeColor = Chart.GetThemeColor
         };
         OnDataPointRender?.Invoke(args);

         var pointColor = args.Color ?? color;
         var p = points[i];
         RenderPoint(context, p.X, p.Y, pointColor);

         if (ShowDataLabels) {
            var labelColor = args.DataLabelColor ?? pointColor;
            var labelSize = args.DataLabelSize ?? 12f;
            RenderDataLabel(context, p.X, p.Y - (10 * context.Density), ValueSelector(item), labelColor, labelSize);
         }
      }
   }

   private void RenderDataLabel(NTRenderContext context, float x, float y, decimal value, SKColor color, float fontSize) {
      var canvas = context.Canvas;
      var text = string.Format("{0:N0}", value);

      _labelPaint ??= new SKPaint { IsAntialias = true };
      _labelPaint.Color = color;

      _labelFont ??= new SKFont { Typeface = Chart.DefaultFont.Typeface };
      _labelFont.Size = fontSize * context.Density;

      canvas.DrawText(text, x, y, SKTextAlign.Center, _labelFont, _labelPaint);
   }

   private void RenderPoint(NTRenderContext context, float x, float y, SKColor color) {
      var canvas = context.Canvas;
      _pointPaint ??= new SKPaint { IsAntialias = true };
      _pointPaint.Color = color;
      canvas.DrawCircle(x, y, 4 * context.Density, _pointPaint);
   }

   public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea) {
      // Radar hit testing is usually proximity based
      var dataList = Data.ToList();
      float centerX = renderArea.MidX;
      float centerY = renderArea.MidY;
      float radius = Math.Min(renderArea.Width, renderArea.Height) / 2f;
      decimal max = MaxValue ?? dataList.Max(ValueSelector);
      if (max <= 0) max = 1;

      for (int i = 0; i < dataList.Count; i++) {
         float angle = (i * 360f / dataList.Count) - 90f;
         float rad = angle * (float)Math.PI / 180f;
         decimal val = ValueSelector(dataList[i]);
         float r = (float)(val / max) * radius;

         var px = centerX + (float)Math.Cos(rad) * r;
         var py = centerY + (float)Math.Sin(rad) * r;

         float dx = point.X - px;
         float dy = point.Y - py;
         if (dx * dx + dy * dy < (10 * Chart.Density) * (10 * Chart.Density)) return (i, dataList[i]);
      }
      return null;
   }
}
