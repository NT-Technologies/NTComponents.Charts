using Microsoft.AspNetCore.Components;
using System.Runtime.InteropServices;
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

   /// <summary>
   ///    Gets or sets the stroke width of the radar outline.
   /// </summary>
   [Parameter]
   public float StrokeWidth { get; set; } = 2f;

   /// <summary>
   ///    Gets or sets the opacity of the filled radar area (0.0 to 1.0).
   /// </summary>
   [Parameter]
   public float AreaOpacity { get; set; } = 0.2f;

   /// <summary>
   ///    Gets or sets the maximum value for the radar scale. If null, it will be calculated from the data.
   /// </summary>
   [Parameter]
   public decimal? MaxValue { get; set; }


   /// <inheritdoc />
   protected override void OnInitialized() {
      base.OnInitialized();
   }

   /// <inheritdoc />
   protected override void OnParametersSet() {
      base.OnParametersSet();
   }

   private SKPaint? _fillPaint;
   private SKPaint? _strokePaint;
   private SKPaint? _pointPaint;
   private SKPaint? _labelPaint;
   private SKFont? _labelFont;
   private List<SKPoint>? _cachedRadarPoints;
   private readonly List<SKPoint> _animatedPoints = [];
   private (SKRect Area, decimal MaxValue)? _cachedRadarPointsKey;
   private decimal? _cachedRadarDataMax;

   /// <inheritdoc />
   protected override void OnCircularGeometryInvalidated() {
      _cachedRadarPoints = null;
      _cachedRadarPointsKey = null;
      _cachedRadarDataMax = null;
   }

   /// <inheritdoc />
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
   public override SKRect Render(NTRenderContext context, SKRect renderArea) {
      var canvas = context.Canvas;
      var dataList = GetCachedData();
      if (dataList.Count == 0) return renderArea;

      int count = dataList.Count;
      if (count < 3) return renderArea;

      float centerX = renderArea.MidX;
      float centerY = renderArea.MidY;
      float radius = Math.Min(renderArea.Width, renderArea.Height) / 2f;

      decimal max = GetRadarMax(dataList);
      if (max <= 0) max = 1;

      var progress = EaseAnimation(GetAnimationProgress());
      var visibilityFactor = VisibilityFactor;
      var hoverFactor = HoverFactor;

      var color = Chart.GetSeriesColor(this);
      color = color.WithAlpha((byte)(color.Alpha * visibilityFactor * hoverFactor));

      var basePoints = GetRadarPoints(dataList, renderArea, max);
      var points = basePoints;
      if (progress < 1f) {
         _animatedPoints.Clear();
         for (var i = 0; i < basePoints.Count; i++) {
            var point = basePoints[i];
            _animatedPoints.Add(new SKPoint(centerX + ((point.X - centerX) * progress), centerY + ((point.Y - centerY) * progress)));
         }
         points = _animatedPoints;
      }

      using var pathBuilder = new SKPathBuilder();
      pathBuilder.AddPoly(CollectionsMarshal.AsSpan(points), true);
      using var path = pathBuilder.Detach();

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
         NTDataPointRenderArgs<TData>? args = null;
         var pointColor = color;
         if (OnDataPointRender is { } onDataPointRender) {
            args = new NTDataPointRenderArgs<TData> {
               Data = item,
               Index = i,
               Color = color,
               GetThemeColor = GetThemeColorSelector()
            };
            onDataPointRender(args);
            pointColor = args.Color ?? color;
         }
         var p = points[i];
         RenderPoint(context, p.X, p.Y, pointColor);

         if (ShowDataLabels) {
            var labelColor = args?.DataLabelColor ?? pointColor;
            var labelSize = args?.DataLabelSize ?? 12f;
            RenderDataLabel(context, p.X, p.Y - (10 * context.Density), ValueSelector(item), labelColor, labelSize);
         }
      }

      return renderArea;
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

   /// <inheritdoc />
   public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea) {
      // Radar hit testing is usually proximity based
      var dataList = GetCachedData();
      if (dataList.Count == 0) return null;
      float centerX = renderArea.MidX;
      float centerY = renderArea.MidY;
      decimal max = GetRadarMax(dataList);
      if (max <= 0) max = 1;

      var points = GetRadarPoints(dataList, renderArea, max);
      for (int i = 0; i < points.Count; i++) {
         var px = points[i].X;
         var py = points[i].Y;

         float dx = point.X - px;
         float dy = point.Y - py;
         if (dx * dx + dy * dy < (10 * Chart.Density) * (10 * Chart.Density)) return (i, dataList[i]);
      }
      return null;
   }

   private List<SKPoint> GetRadarPoints(IReadOnlyList<TData> data, SKRect renderArea, decimal max) {
      if (_cachedRadarPoints is not null && _cachedRadarPointsKey is { } key && key.Area == renderArea && key.MaxValue == max) {
         return _cachedRadarPoints;
      }

      var centerX = renderArea.MidX;
      var centerY = renderArea.MidY;
      var radius = Math.Min(renderArea.Width, renderArea.Height) / 2f;
      var points = new List<SKPoint>(data.Count);
      for (var i = 0; i < data.Count; i++) {
         var angle = ((i * 360f) / data.Count) - 90f;
         var radians = angle * (float)Math.PI / 180f;
         var valueRadius = (float)(ValueSelector(data[i]) / max) * radius;
         points.Add(new SKPoint(centerX + (float)Math.Cos(radians) * valueRadius, centerY + (float)Math.Sin(radians) * valueRadius));
      }

      _cachedRadarPoints = points;
      _cachedRadarPointsKey = (renderArea, max);
      return points;
   }

   private decimal GetRadarMax(IReadOnlyList<TData> data) {
      if (MaxValue.HasValue) {
         return MaxValue.Value;
      }

      _cachedRadarDataMax ??= data.Max(ValueSelector);
      return _cachedRadarDataMax.Value;
   }
}
