using SkiaSharp;
using NTComponents.Charts.Core.Series;
using NTComponents.Core;

namespace NTComponents.Charts.Core;

/// <summary>
/// Provides extension methods for <see cref="NTRenderContext"/> to draw chart primitives
/// such as data points and data labels onto a SkiaSharp canvas.
/// </summary>
public static class NTRenderContextExtensions {
   /// <summary>
   /// Draws a single data point on the chart canvas using the specified style, shape, and color.
   /// </summary>
   /// <typeparam name="TData">The type of data items bound to the chart.</typeparam>
   /// <param name="context">The render context that provides canvas and density information.</param>
   /// <param name="chart">The chart instance used to resolve theme colors.</param>
   /// <param name="series">The series that owns the data point being drawn.</param>
   /// <param name="x">The horizontal canvas coordinate of the point center.</param>
   /// <param name="y">The vertical canvas coordinate of the point center.</param>
   /// <param name="color">The fill or stroke color of the point.</param>
   /// <param name="style">Determines whether the point is filled, outlined, or hidden.</param>
   /// <param name="size">The logical (pre-density-scaled) diameter of the point.</param>
   /// <param name="shape">The geometric shape used to render the point.</param>
   /// <param name="strokeColor">
   /// An optional stroke color applied when <paramref name="style"/> is <see cref="PointStyle.Outlined"/>.
   /// </param>
   public static void DrawPoint<TData>(
       this NTRenderContext context,
       NTChart<TData> chart,
       NTBaseSeries<TData> series,
       float x,
       float y,
       SKColor color,
       PointStyle style,
       float size,
       PointShape shape,
       SKColor? strokeColor = null) where TData : class {
      if (style == PointStyle.None) return;

      var scaledSize = size * context.Density;

      using var paint = new SKPaint {
         Color = color,
         IsAntialias = true,
         Style = style == PointStyle.Filled ? SKPaintStyle.Fill : SKPaintStyle.Stroke,
         StrokeWidth = 2 * context.Density
      };

      DrawPointInternal(context.Canvas, x, y, scaledSize, shape, paint);

      if (style == PointStyle.Outlined && strokeColor.HasValue) {
         paint.Color = strokeColor.Value;
         paint.Style = SKPaintStyle.Stroke;
         context.Canvas.DrawCircle(x, y, scaledSize / 2, paint);
      }
   }

   /// <summary>
   /// Draws the geometric shape for a data point directly onto the provided <see cref="SKCanvas"/>.
   /// This method handles the low-level shape rendering and is intended to be called after all
   /// paint configuration has been applied.
   /// </summary>
   /// <param name="canvas">The SkiaSharp canvas to draw on.</param>
   /// <param name="x">The horizontal canvas coordinate of the shape center.</param>
   /// <param name="y">The vertical canvas coordinate of the shape center.</param>
   /// <param name="scaledSize">The density-adjusted diameter of the shape.</param>
   /// <param name="shape">The geometric shape to render.</param>
   /// <param name="paint">The paint object that controls color, style, and anti-aliasing.</param>
   public static void DrawPointInternal(SKCanvas canvas, float x, float y, float scaledSize, PointShape shape, SKPaint paint) {
      var halfSize = scaledSize / 2;

      switch (shape) {
         case PointShape.Circle:
            canvas.DrawCircle(x, y, halfSize, paint);
            break;
         case PointShape.Square:
            canvas.DrawRect(x - halfSize, y - halfSize, scaledSize, scaledSize, paint);
            break;
         case PointShape.Triangle:
            using (var path = new SKPath()) {
               path.MoveTo(x, y - halfSize);
               path.LineTo(x + halfSize, y + halfSize);
               path.LineTo(x - halfSize, y + halfSize);
               path.Close();
               canvas.DrawPath(path, paint);
            }
            break;
         case PointShape.Diamond:
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
   }

   /// <summary>
   /// Draws a formatted data label near the specified canvas coordinates, optionally rendering
   /// a rounded-rectangle background with a drop shadow and theme-colored border behind the text.
   /// </summary>
   /// <typeparam name="TData">The type of data items bound to the chart.</typeparam>
   /// <param name="context">The render context that provides canvas, density, and font information.</param>
   /// <param name="chart">The chart instance used to resolve theme and series colors.</param>
   /// <param name="series">The series associated with the data point being labeled.</param>
   /// <param name="x">The horizontal canvas coordinate at which the label is anchored.</param>
   /// <param name="y">The vertical canvas coordinate at which the label is anchored.</param>
   /// <param name="value">The numeric value to format and display as the label text.</param>
   /// <param name="renderArea">The bounding rectangle of the chart's render area, used to clamp label position.</param>
   /// <param name="format">A composite format string (e.g. <c>"{0:N2}"</c>) applied to <paramref name="value"/>.</param>
   /// <param name="textColor">An optional override for the label text color; defaults to the series text color.</param>
   /// <param name="fontSize">An optional override for the logical font size; defaults to 12.</param>
   /// <param name="textAlign">Horizontal alignment of the label text relative to <paramref name="x"/>.</param>
   /// <param name="showBackground">
   /// When <see langword="true"/>, a filled rounded rectangle is drawn behind the label text.
   /// </param>
   /// <param name="backgroundColor">
   /// An optional override for the background rectangle color; defaults to the series color.
   /// </param>
   public static void DrawDataLabel<TData>(
       this NTRenderContext context,
       NTChart<TData> chart,
       NTBaseSeries<TData> series,
       float x,
       float y,
       decimal value,
       SKRect renderArea,
       string format,
       SKColor? textColor = null,
       float? fontSize = null,
       SKTextAlign textAlign = SKTextAlign.Center,
       bool showBackground = true,
       SKColor? backgroundColor = null) where TData : class {
      var color = textColor ?? chart.GetSeriesTextColor(series);
      var size = (fontSize ?? 12f) * context.Density;

      using var font = new SKFont {
         Size = size,
         Embolden = true,
         Typeface = context.DefaultFont.Typeface
      };

      using var paint = new SKPaint {
         Color = color,
         IsAntialias = true
      };

      var text = string.Format(format, value);
      var textWidth = font.MeasureText(text);
      var textHeight = font.Size;

      var paddingX = 6f * context.Density;
      var paddingY = 2f * context.Density;

      var drawY = y;
      if (textAlign == SKTextAlign.Center) {
         drawY = y - ((size / 2) + (5 * context.Density));
         if (drawY - textHeight < renderArea.Top) {
            drawY = y + ((size / 2) + textHeight + (5 * context.Density));
         }
      }

      if (showBackground) {
         var bgColor = backgroundColor ?? chart.GetSeriesColor(series);

         using var bgPaint = new SKPaint {
            Color = bgColor,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
            ImageFilter = SKImageFilter.CreateDropShadow(2 * context.Density, 2 * context.Density, 4 * context.Density, 4 * context.Density, SKColors.Black.WithAlpha(80))
         };

         var bgRect = textAlign switch {
            SKTextAlign.Left => new SKRect(x - paddingX, drawY - (textHeight / 2) - paddingY, x + textWidth + paddingX, drawY + (textHeight / 2) + paddingY),
            SKTextAlign.Right => new SKRect(x - textWidth - paddingX, drawY - (textHeight / 2) - paddingY, x + paddingX, drawY + (textHeight / 2) + paddingY),
            _ => new SKRect(x - (textWidth / 2) - paddingX, drawY - textHeight - paddingY, x + (textWidth / 2) + paddingX + (2 * context.Density), drawY + paddingY)
         };

         context.Canvas.DrawRoundRect(bgRect, 6 * context.Density, 6 * context.Density, bgPaint);

         using var borderPaint = new SKPaint {
            Color = chart.GetThemeColor(TnTColor.Outline),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
         };
         context.Canvas.DrawRoundRect(bgRect, 6 * context.Density, 6 * context.Density, borderPaint);
      }

      context.Canvas.DrawText(text, x, drawY, textAlign, font, paint);
   }
}
