using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NTComponents.Charts.Core.Axes;

/// <summary>
///     Options for the X axis of a cartesian chart.
/// </summary>
public class NTXAxisOptions : NTAxisOptions {

/// <inheritdoc />
internal override SKRect Measure<TData>(SKRect renderArea, NTChart<TData> chart) {
      float labelHeight = 16;
      float titleHeight = string.IsNullOrEmpty(Title) ? 0 : 20;
      var totalAxisHeight = labelHeight + titleHeight + 4;

      // Add a 10px margin on the right to prevent data points from being cut off,
      // but only if there isn't a secondary Y-axis providing its own space.
      var yAxes = chart.GetUniqueYAxes();
      float rightMargin = yAxes.Count > 1 ? 0 : 10;

      return new SKRect(renderArea.Left, renderArea.Top, renderArea.Right - rightMargin, renderArea.Bottom - totalAxisHeight);
   }

   /// <inheritdoc />
   internal override void Render<TData>(SKCanvas canvas, SKRect plotArea, SKRect totalArea, NTChart<TData> chart) {
      var (xMinReal, xMaxReal) = chart.GetXRange(this, false);

      using var textPaint = new SKPaint {
         Color = chart.GetThemeColor(chart.TextColor),
         IsAntialias = true
      };

      using var textFont = new SKFont {
         Size = 12,
         Embolden = true,
         Typeface = chart.DefaultTypeface
      };

      using var titlePaint = new SKPaint {
         Color = chart.GetThemeColor(chart.TextColor),
         IsAntialias = true
      };

      using var titleFont = new SKFont {
         Size = 16,
         Embolden = true,
         Typeface = chart.DefaultTypeface
      };

      using var linePaint = new SKPaint {
         Color = chart.GetThemeColor(TnTColor.Outline),
         StrokeWidth = 1,
         Style = SKPaintStyle.Stroke,
         IsAntialias = true
      };

      var yLine = plotArea.Bottom;
      canvas.DrawLine(plotArea.Left, yLine, plotArea.Right, yLine, linePaint);

      var isCategorical = chart.IsCategoricalX;

      if (isCategorical) {
         var allValues = chart.GetAllXValues();
         if (allValues.Any()) {
            for (var i = 0; i < allValues.Count; i++) {
               var val = allValues[i];
               var scaledVal = chart.GetScaledXValue(val);
               var screenCoord = chart.ScaleX(scaledVal, plotArea);

               if (screenCoord < plotArea.Left - 1 || screenCoord > plotArea.Right + 1) continue;

               string label;
               if (!string.IsNullOrEmpty(LabelFormat)) {
                  if (LabelFormat.Contains("{0")) {
                     label = string.Format(LabelFormat, val);
                  }
                  else if (val is IFormattable formattable) {
                     label = formattable.ToString(LabelFormat, null);
                  }
                  else {
                     label = val?.ToString() ?? "";
                  }
               }
               else {
                  label = val is IFormattable f ? f.ToString("0.#", null) : val?.ToString() ?? "";
               }

               SKTextAlign textAlign = SKTextAlign.Center;

               if (!isCategorical) {
                  if (i == 0 && allValues.Count > 1) {
                     textAlign = SKTextAlign.Left;
                  }
                  else if (i == allValues.Count - 1 && allValues.Count > 1) {
                     textAlign = SKTextAlign.Right;
                  }
               }

               canvas.DrawText(label, screenCoord, yLine + 12, textAlign, textFont, textPaint);
            }
         }
         }
      else if (Scale == NTAxisScale.Logarithmic) {
         var (min, max) = chart.GetXRange(this, true);
         min = Math.Max(0.000001, min);
         max = Math.Max(min * 1.1, max);

         var startLog = (int)Math.Floor(Math.Log10(min));
         var endLog = (int)Math.Ceiling(Math.Log10(max));

         for (int log = startLog; log <= endLog; log++) {
            double val = Math.Pow(10, log);
            if (val < min || val > max) continue;

            var screenCoord = chart.ScaleX(val, plotArea);
            if (screenCoord < plotArea.Left - 1 || screenCoord > plotArea.Right + 1) continue;

            canvas.DrawText(val.ToString("G"), screenCoord, yLine + 12, SKTextAlign.Center, textFont, textPaint);
         }
      }
      else {
         var maxTicks = Math.Max(2, (int)(plotArea.Width / 100));
         var (niceMin, niceMax, spacing) = chart.CalculateNiceScaling(xMinReal, xMaxReal, maxTicks);
         int totalLabels = (int)Math.Round((niceMax - niceMin) / spacing) + 1;
         for (int i = 0; i < totalLabels; i++) {
            double val = niceMin + i * spacing;
            var screenCoord = chart.ScaleX(val, plotArea);

            if (screenCoord < plotArea.Left - 1 || screenCoord > plotArea.Right + 1) continue;

            SKTextAlign textAlign = SKTextAlign.Center;
            if (i == 0) {
               textAlign = SKTextAlign.Left;
            }
            else if (i == totalLabels - 1) {
               textAlign = SKTextAlign.Right;
            }

            string label;
            if (!string.IsNullOrEmpty(LabelFormat)) {
               if (LabelFormat.Contains("{0")) {
                  label = string.Format(LabelFormat, chart.IsXAxisDateTime ? new DateTime((long)val) : val);
               }
               else if (chart.IsXAxisDateTime) {
                  label = new DateTime((long)val).ToString(LabelFormat);
               }
               else {
                  label = val.ToString(LabelFormat);
               }
            }
            else if (chart.IsXAxisDateTime) {
               label = new DateTime((long)val).ToString("d");
            }
            else {
               label = val.ToString("0.##");
            }

            canvas.DrawText(label, screenCoord, yLine + 12, textAlign, textFont, textPaint);

         }
      }

      if (!string.IsNullOrEmpty(Title)) {
         canvas.DrawText(Title, plotArea.Left + (plotArea.Width / 2), plotArea.Bottom + 30, SKTextAlign.Center, titleFont, titlePaint);
      }
   }
}
