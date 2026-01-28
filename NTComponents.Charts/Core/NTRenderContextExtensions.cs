using SkiaSharp;
using NTComponents.Charts.Core.Series;
using NTComponents.Core;

namespace NTComponents.Charts.Core;

public static class NTRenderContextExtensions {
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
