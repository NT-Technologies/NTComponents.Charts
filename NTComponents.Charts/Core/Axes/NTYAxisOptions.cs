using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NTComponents.Charts.Core.Axes;

/// <summary>
///     Options for the Y axis of a cartesian chart.
/// </summary>
public class NTYAxisOptions : NTAxisOptions {

   private bool IsSecondary<TData>(NTChart<TData> chart) where TData : class {
      var yAxes = chart.GetUniqueYAxes();
      return yAxes.Count > 1 && yAxes.IndexOf(this) == 1;
   }

   /// <inheritdoc />
   internal override SKRect Measure<TData>(SKRect renderArea, NTChart<TData> chart) {
      float labelWidth = 50;
      float titleWidth = string.IsNullOrEmpty(Title) ? 0 : 20;
      var totalAxisWidth = labelWidth + titleWidth + 5;
      
      if (IsSecondary(chart)) {
         return new SKRect(renderArea.Left, renderArea.Top, renderArea.Right - totalAxisWidth, renderArea.Bottom);
      }
      return new SKRect(renderArea.Left + totalAxisWidth, renderArea.Top, renderArea.Right, renderArea.Bottom);
   }

   /// <inheritdoc />
   internal override void Render<TData>(SKCanvas canvas, SKRect plotArea, SKRect totalArea, NTChart<TData> chart) {
      var (yMinReal, yMaxReal) = chart.GetYRange(this, false);

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

      var isSecondary = IsSecondary(chart);
      var xLine = isSecondary ? plotArea.Right : plotArea.Left;
      canvas.DrawLine(xLine, plotArea.Top, xLine, plotArea.Bottom, linePaint);

      var isCategorical = chart.IsCategoricalY;

       if (isCategorical) {
          var allValues = chart.GetAllYValues().Cast<object>().ToList();
          if (allValues.Any()) {
             for (var i = 0; i < allValues.Count; i++) {
                var val = allValues[i];
                var scaledVal = chart.GetScaledYValue(val);
                var screenCoord = chart.ScaleY(scaledVal, this, plotArea);

                if (screenCoord < plotArea.Top - 1 || screenCoord > plotArea.Bottom + 1) continue;

                float yOffset = 5;
                if (i == 0 && !isCategorical) {
                   yOffset = 0;
                }
                else if (i == allValues.Count - 1 && !isCategorical) {
                   yOffset = 10;
                }

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
                    label = val is IFormattable f ? f.ToString("N2", null) : val?.ToString() ?? "";
                 }

                 if (isSecondary) {

                     canvas.DrawText(label, xLine + 4, screenCoord + yOffset, SKTextAlign.Left, textFont, textPaint);
                 }
                 else {
                     canvas.DrawText(label, xLine - 4, screenCoord + yOffset, SKTextAlign.Right, textFont, textPaint);
                 }
             }
          }
       }
      else if (Scale == NTAxisScale.Logarithmic) {
         var (min, max) = chart.GetYRange(this, true);
         var dMin = Math.Max(0.000001, (double)min);
         var dMax = Math.Max(dMin * 1.1, (double)max);

         var startLog = (int)Math.Floor(Math.Log10(dMin));
         var endLog = (int)Math.Ceiling(Math.Log10(dMax));

         for (int log = startLog; log <= endLog; log++) {
            double val = Math.Pow(10, log);
            if ((decimal)val < min || (decimal)val > max) continue;

            var screenCoord = chart.ScaleY((decimal)val, this, plotArea);
            if (screenCoord < plotArea.Top - 1 || screenCoord > plotArea.Bottom + 1) continue;

            if (isSecondary) {
                canvas.DrawText(val.ToString("G"), xLine + 4, screenCoord + 5, SKTextAlign.Left, textFont, textPaint);
            }
            else {
                canvas.DrawText(val.ToString("G"), xLine - 4, screenCoord + 5, SKTextAlign.Right, textFont, textPaint);
            }
         }
      }
      else {
         var maxTicks = Math.Max(2, (int)(plotArea.Height / 40));
         var (niceMin, niceMax, spacing) = chart.CalculateNiceScaling((double)yMinReal, (double)yMaxReal, maxTicks);
         int totalLabels = (int)Math.Round((niceMax - niceMin) / spacing) + 1;
         for (int i = 0; i < totalLabels; i++) {
            double val = niceMin + i * spacing;
            var screenCoord = chart.ScaleY((decimal)val, this, plotArea);

            if (screenCoord < plotArea.Top - 1 || screenCoord > plotArea.Bottom + 1) continue;

            float yOffset = 5;
            if (i == 0) {
               yOffset = 0;
            }
            else if (i == totalLabels - 1) {
               yOffset = 10;
            }

            string label;
            if (!string.IsNullOrEmpty(LabelFormat)) {
               if (LabelFormat.Contains("{0")) {
                  label = string.Format(LabelFormat, val);
               }
               else {
                  label = val.ToString(LabelFormat);
               }
            }
            else {
               label = val.ToString("N2");
            }

            if (isSecondary) {
               canvas.DrawText(label, xLine + 4, screenCoord + yOffset, SKTextAlign.Left, textFont, textPaint);
            }
            else {
               canvas.DrawText(label, xLine - 4, screenCoord + yOffset, SKTextAlign.Right, textFont, textPaint);
            }
         }
      }


   if (!string.IsNullOrEmpty(Title)) {
      canvas.Save();
      if (isSecondary) {
         canvas.RotateDegrees(90, xLine + 45, plotArea.Top + (plotArea.Height / 2));
         canvas.DrawText(Title, xLine + 45, plotArea.Top + (plotArea.Height / 2), SKTextAlign.Center, titleFont, titlePaint);
      }
      else {
         canvas.RotateDegrees(-90, xLine - 45, plotArea.Top + (plotArea.Height / 2));
         canvas.DrawText(Title, xLine - 45, plotArea.Top + (plotArea.Height / 2), SKTextAlign.Center, titleFont, titlePaint);
      }
      canvas.Restore();
   }
   }
}
