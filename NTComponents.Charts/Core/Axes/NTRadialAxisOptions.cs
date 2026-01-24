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
public class NTRadialAxisOptions : NTAxisOptions {

   /// <summary>
   ///    Gets or sets the number of concentric circles to draw.
   /// </summary>
   public int Levels { get; set; } = 5;

   /// <summary>
   ///    Gets or sets whether to draw circles or polygons.
   /// </summary>
   public RadialAxisShape Shape { get; set; } = RadialAxisShape.Polygon;

   /// <summary>
   ///    Gets or sets the labels for each spoke. If null, the series LabelSelector will be used.
   /// </summary>
   public List<string>? Labels { get; set; }

   /// <inheritdoc />
   internal override SKRect Measure<TData>(SKRect renderArea, NTChart<TData> chart) {
      // Add padding for labels around the perimeter
      float padding = 60; // More padding for labels
      return new SKRect(
          renderArea.Left + padding,
          renderArea.Top + padding,
          renderArea.Right - padding,
          renderArea.Bottom - padding
      );
   }

   /// <inheritdoc />
   internal override void Render<TData>(SKCanvas canvas, SKRect plotArea, SKRect totalArea, NTChart<TData> chart) {
      var series = chart.Series.OfType<NTRadarSeries<TData>>().FirstOrDefault();
      if (series == null) return;

      var dataList = series.Data?.ToList();
      if (dataList == null || !dataList.Any()) return;

      float centerX = plotArea.MidX;
      float centerY = plotArea.MidY;
      float radius = Math.Min(plotArea.Width, plotArea.Height) / 2f;

      decimal max = series.MaxValue ?? dataList.Max(series.ValueSelector);
      if (max <= 0) max = 1;

      using var linePaint = new SKPaint {
         Color = chart.GetThemeColor(TnTColor.OutlineVariant),
         StrokeWidth = 1,
         Style = SKPaintStyle.Stroke,
         IsAntialias = true
      };

      using var textPaint = new SKPaint {
         Color = chart.GetThemeColor(chart.TextColor),
         IsAntialias = true
      };

      using var textFont = new SKFont {
         Size = 12,
         Embolden = true,
         Typeface = chart.DefaultTypeface
      };

      // Draw concentric rings
      for (int i = 1; i <= Levels; i++) {
         float r = (radius / Levels) * i;
         if (Shape == RadialAxisShape.Circle) {
            canvas.DrawCircle(centerX, centerY, r, linePaint);
         }
         else {
            DrawPolygon(canvas, centerX, centerY, r, dataList.Count, linePaint);
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
            label = series.XValue?.Invoke(dataList[i])?.ToString() ?? $"Item {i + 1}";
         }

         float labelX = centerX + (float)Math.Cos(rad) * (radius + 20);
         float labelY = centerY + (float)Math.Sin(rad) * (radius + 20);

         SKTextAlign textAlign = SKTextAlign.Center;
         if (Math.Abs(Math.Cos(rad)) > 0.1) {
            textAlign = Math.Cos(rad) > 0 ? SKTextAlign.Left : SKTextAlign.Right;
         }

         canvas.DrawText(label, labelX, labelY + 5, textAlign, textFont, textPaint);
      }
   }

   private void DrawPolygon(SKCanvas canvas, float cx, float cy, float r, int sides, SKPaint paint) {
      using var path = new SKPath();
      for (int i = 0; i < sides; i++) {
         float angle = (i * 360f / sides) - 90f;
         float rad = angle * (float)Math.PI / 180f;
         float x = cx + (float)Math.Cos(rad) * r;
         float y = cy + (float)Math.Sin(rad) * r;
         if (i == 0) path.MoveTo(x, y);
         else path.LineTo(x, y);
      }
      path.Close();
      canvas.DrawPath(path, paint);
   }
}
