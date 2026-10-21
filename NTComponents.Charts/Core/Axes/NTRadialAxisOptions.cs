using Microsoft.AspNetCore.Components;
using SkiaSharp;
using System.Collections.Generic;
using NTComponents.Charts.Core.Series;
using System.Linq;

namespace NTComponents.Charts.Core.Axes;

/// <summary>
///     Describes the shape of a radial axis.
/// </summary>
public enum RadialAxisShape {
   /// <summary>Draw as concentric polygons (standard radar).</summary>
   Polygon,
   /// <summary>Draw as concentric circles.</summary>
   Circle
}

/// <summary>
///     Options for a radial axis (used in radar charts).
/// </summary>
public class NTRadialAxisOptions<TData> : NTAxisOptions<TData> where TData : class {

   /// <summary>Gets the default radial axis configuration.</summary>
   public static readonly NTRadialAxisOptions<TData> Default = new();

   /// <summary>
   ///    Gets or sets the number of concentric circles to draw.
   /// </summary>
   [Parameter]
   public int Levels { get; set; } = 5;

   /// <summary>
   ///    Gets or sets whether to draw circles or polygons.
   /// </summary>
   [Parameter]
   public RadialAxisShape Shape { get; set; } = RadialAxisShape.Polygon;

   /// <summary>
   ///    Gets or sets the labels for each spoke. If null, the series LabelSelector will be used.
   /// </summary>
   [Parameter]
   public List<string>? Labels { get; set; }

   private SKPaint? _linePaint;
   private SKPaint? _textPaint;
   private SKFont? _textFont;

   /// <inheritdoc />
   public override void Dispose() {
      _linePaint?.Dispose();
      _textPaint?.Dispose();
      _textFont?.Dispose();
      base.Dispose();
   }

   /// <inheritdoc />
   public override SKRect Measure(NTRenderContext context, SKRect renderArea) {
      if (!Visible) return renderArea;

      // Add padding for labels around the perimeter
      float padding = 60 * context.Density;
      return new SKRect(
          renderArea.Left + padding,
          renderArea.Top + padding,
          renderArea.Right - padding,
          renderArea.Bottom - padding
      );
   }

   /// <inheritdoc />
   public override SKRect Render(NTRenderContext context, SKRect renderArea) {
      if (!Visible) return renderArea;

      var plotArea = Measure(context, renderArea);

      var canvas = context.Canvas;

      // Radar charts usually have one series that defines the axis.
      if (Chart is not NTChart<TData> chart || chart.Series.OfType<NTRadarSeries<TData>>().FirstOrDefault() is not { } radarSeries) return plotArea;

      var dataList = radarSeries.Data?.ToList() ?? [];
      if (dataList.Count == 0) return plotArea;

      float centerX = plotArea.MidX;
      float centerY = plotArea.MidY;
      float radius = Math.Min(plotArea.Width, plotArea.Height) / 2f;

      decimal max = 1;
      if (radarSeries.MaxValue is { } maxValue) {
         max = maxValue;
      }
      else {
         foreach (var item in dataList) {
            max = Math.Max(max, radarSeries.ValueSelector(item));
         }
      }
      if (max <= 0) max = 1;

      var linePaint = _linePaint ??= new SKPaint { Style = SKPaintStyle.Stroke, IsAntialias = true };
      linePaint.Color = Chart.GetThemeColor(NTColor.OutlineVariant);
      linePaint.StrokeWidth = context.Density;

      var textPaint = _textPaint ??= new SKPaint { IsAntialias = true };
      textPaint.Color = NTChartTokens.SecondaryTextColor(Chart);

      var textFont = _textFont ??= new SKFont { Typeface = NTChartTokens.Typeface(NTTypography.BodySmall) };
      textFont.Size = NTChartTokens.Size(NTTypography.BodySmall) * context.Density;

      // Draw concentric rings
      for (int i = 1; i <= Levels; i++) {
         float r = (radius / Levels) * i;
         if (Shape == RadialAxisShape.Circle) {
            canvas.DrawCircle(centerX, centerY, r, linePaint);
         }
         else {
            DrawPolygon(context, centerX, centerY, r, dataList.Count, linePaint);
         }

         // Draw value label on the first axis
         decimal val = (max / Levels) * i;
         float angle = -90f;
         float rad = angle * (float)Math.PI / 180f;
         canvas.DrawText(val.ToString("0.#"), centerX + (float)Math.Cos(rad) * r, centerY + (float)Math.Sin(rad) * r, SKTextAlign.Left, textFont, textPaint);
      }

      // Draw spokes and category labels
      for (int i = 0; i < dataList.Count; i++) {
         float angle = (i * 360f / dataList.Count) - 90f;
         float rad = angle * (float)Math.PI / 180f;

         float x = centerX + (float)Math.Cos(rad) * radius;
         float y = centerY + (float)Math.Sin(rad) * radius;

         canvas.DrawLine(centerX, centerY, x, y, linePaint);

         // Label
         string label;
         if (Labels != null && i < Labels.Count) {
            label = Labels[i];
         }
         else {
            var xVal = radarSeries.XValue?.Invoke(dataList[i]);
            label = xVal?.ToString() ?? $"Item {i + 1}";
         }

         float labelX = centerX + (float)Math.Cos(rad) * (radius + (10 * context.Density));
         float labelY = centerY + (float)Math.Sin(rad) * (radius + (10 * context.Density));

         SKTextAlign textAlign = SKTextAlign.Center;
         if (Math.Abs(Math.Cos(rad)) > 0.1) {
            textAlign = Math.Cos(rad) > 0 ? SKTextAlign.Left : SKTextAlign.Right;
         }

         canvas.DrawText(label, labelX, labelY + (5 * context.Density), textAlign, textFont, textPaint);
      }

      return plotArea;
   }

   private void DrawPolygon(NTRenderContext context, float cx, float cy, float r, int sides, SKPaint paint) {
      var canvas = context.Canvas;
      using var pathBuilder = new SKPathBuilder();
      for (int i = 0; i < sides; i++) {
         float angle = (i * 360f / sides) - 90f;
         float rad = angle * (float)Math.PI / 180f;
         float x = cx + (float)Math.Cos(rad) * r;
         float y = cy + (float)Math.Sin(rad) * r;
         if (i == 0) pathBuilder.MoveTo(x, y);
         else pathBuilder.LineTo(x, y);
      }
      pathBuilder.Close();
      using var path = pathBuilder.Detach();
      canvas.DrawPath(path, paint);
   }
}
