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

      var paint = ResetPaint(chart.ExtensionPaint, color, style == PointStyle.Filled ? SKPaintStyle.Fill : SKPaintStyle.Stroke, 2 * context.Density);

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
         case PointShape.Diamond:
            // Markers are drawn hundreds of times per frame at a handful of sizes, so the origin-centered path is
            // cached per thread and only rebuilt when the shape or size changes; a translate keeps the geometry exact.
            if (_markerPath is null || _markerShape != shape || _markerHalfSize != halfSize) {
               _markerPath?.Dispose();
               _markerPath = BuildMarkerPath(shape, halfSize);
               _markerShape = shape;
               _markerHalfSize = halfSize;
            }
            canvas.Save();
            canvas.Translate(x, y);
            canvas.DrawPath(_markerPath, paint);
            canvas.Restore();
            break;
      }
   }

   [ThreadStatic] private static SKPath? _markerPath;
   [ThreadStatic] private static PointShape _markerShape;
   [ThreadStatic] private static float _markerHalfSize;

   private static SKPath BuildMarkerPath(PointShape shape, float halfSize) {
      using var builder = new SKPathBuilder();
      builder.MoveTo(0, -halfSize);
      if (shape == PointShape.Triangle) {
         builder.LineTo(halfSize, halfSize);
         builder.LineTo(-halfSize, halfSize);
      }
      else {
         builder.LineTo(halfSize, 0);
         builder.LineTo(0, halfSize);
         builder.LineTo(-halfSize, 0);
      }
      builder.Close();
      return builder.Detach();
   }

   /// <summary>
   /// Draws a formatted data label near the specified canvas coordinates, optionally rendering
   /// a rounded-rectangle background behind the text. The label uses the Material 3 label-medium type role.
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
   /// <param name="textColor">
   /// An optional override for the label text color. By default the label uses the series text color on a series-colored background,
   /// a contrasting color on a custom background, and the chart text color when no background is drawn.
   /// </param>
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
      var sizePx = (fontSize ?? NTChartTokens.Size(NTTypography.LabelMedium)) * context.Density;
      var centerY = y;
      if (textAlign == SKTextAlign.Center) {
         var halfBox = LabelBoxHeight(sizePx, context.Density) / 2f;
         centerY = y - (LabelPointGap * context.Density) - halfBox;
         if (centerY - halfBox < renderArea.Top) {
            centerY = y + (LabelPointGap * context.Density) + halfBox;
         }
      }

      var background = showBackground ? backgroundColor ?? chart.GetSeriesColor(series) : (SKColor?)null;
      var color = textColor ?? (background is null ? chart.GetThemeColor(chart.TextColor) : backgroundColor is null ? chart.GetSeriesTextColor(series) : NTChartTokens.ContrastingTextColor(background.Value));
      DrawLabel(context.Canvas, chart.ExtensionFont, chart.ExtensionPaint, string.Format(format, value), x, centerY, textAlign, context.Density, sizePx, color, background);
   }

   /// <summary>Gap between a data point and the label drawn above or below it, in dp.</summary>
   internal const float LabelPointGap = 8f;

   /// <summary>Height in pixels of a data label box (line height plus block padding) for a label font of <paramref name="sizePx"/>.</summary>
   internal static float LabelBoxHeight(float sizePx, float density) => LabelLineHeight(sizePx) + (2f * NTChartTokens.DataLabelPaddingBlock * density);

   /// <summary>Line height in pixels of label-medium text scaled to a font of <paramref name="sizePx"/>.</summary>
   internal static float LabelLineHeight(float sizePx) => sizePx * NTChartTokens.LineHeight(NTTypography.LabelMedium) / NTChartTokens.Size(NTTypography.LabelMedium);

   /// <summary>
   /// The one data-label renderer shared by every series. Draws label-medium text vertically centered on <paramref name="centerY"/> with an optional
   /// extra-small rounded background (no shadow, no border). The text is anchored at <paramref name="anchorX"/> according to <paramref name="align"/>;
   /// when <paramref name="bounds"/> is set the label is shifted horizontally to stay inside it. <paramref name="paint"/> is reused for fill and text.
   /// </summary>
   internal static void DrawLabel(SKCanvas canvas, SKFont font, SKPaint paint, string text, float anchorX, float centerY, SKTextAlign align, float density, float sizePx, SKColor textColor, SKColor? backgroundColor, SKRect? bounds = null) {
      font.Typeface = NTChartTokens.Typeface(NTTypography.LabelMedium);
      font.Size = sizePx;
      var textWidth = font.MeasureText(text);
      var lineHeight = LabelLineHeight(sizePx);
      var padX = NTChartTokens.DataLabelPaddingInline * density;
      var padY = NTChartTokens.DataLabelPaddingBlock * density;
      var left = align switch {
         SKTextAlign.Left => anchorX - padX,
         SKTextAlign.Right => anchorX - textWidth - padX,
         _ => anchorX - (textWidth / 2f) - padX
      };
      var width = textWidth + (2f * padX);
      if (bounds is { } area) {
         left = Math.Max(area.Left, Math.Min(left, area.Right - width));
      }

      var top = centerY - (((2f * padY) + lineHeight) / 2f);
      var textX = left + padX + align switch { SKTextAlign.Left => 0f, SKTextAlign.Right => textWidth, _ => textWidth / 2f };
      paint.IsAntialias = true;
      paint.Style = SKPaintStyle.Fill;
      paint.StrokeWidth = 0;
      paint.ImageFilter = null;
      if (backgroundColor is { } background) {
         var radius = Math.Min(NTChartTokens.Radius(NTChartTokens.DataLabelCorner) * density, (lineHeight + (2f * padY)) / 2f);
         paint.Color = background;
         canvas.DrawRoundRect(new SKRect(left, top, left + width, top + lineHeight + (2f * padY)), radius, radius, paint);
      }

      paint.Color = textColor;
      canvas.DrawText(text, textX, NTChartTokens.TextBaseline(top + padY, lineHeight, sizePx), align, font, paint);
   }

   // The chart's shared paint is reused for every draw, so each use sets every property these helpers ever change.
   private static SKPaint ResetPaint(SKPaint paint, SKColor color, SKPaintStyle style, float strokeWidth = 0, SKImageFilter? imageFilter = null) {
      paint.Color = color;
      paint.IsAntialias = true;
      paint.Style = style;
      paint.StrokeWidth = strokeWidth;
      paint.ImageFilter = imageFilter;
      return paint;
   }
}
