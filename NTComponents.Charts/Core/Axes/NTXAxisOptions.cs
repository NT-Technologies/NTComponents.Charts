using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NTComponents.Charts.Core.Axes;

/// <summary>
///     Options for the X axis of a cartesian chart.
/// </summary>
public class NTXAxisOptions : NTAxisOptions {

   private static readonly NTXAxisOptions _default = new();
   public static NTXAxisOptions Default => _default;

   private SKPaint? _textPaint;
   private SKPaint? _titlePaint;
   private SKPaint? _linePaint;
   private SKFont? _textFont;
   private SKFont? _titleFont;

   public override void Dispose() {
      _textPaint?.Dispose();
      _titlePaint?.Dispose();
      _linePaint?.Dispose();
      _textFont?.Dispose();
      _titleFont?.Dispose();
      base.Dispose();
   }

   protected override void OnInitialized() {
      base.OnInitialized();
      Chart?.SetXAxisOptions(this);
   }

   /// <inheritdoc />
   internal override SKRect Measure(SKRect renderArea, NTRenderContext context, IAxisChart chart) {
      float labelHeight = 16;
      float titleHeight = string.IsNullOrEmpty(Title) ? 0 : 20;
      var totalAxisHeight = (labelHeight + titleHeight + 4) * context.Density;

      // Add a 10px margin on the right to prevent data points from being cut off,
      // but only if there isn't a secondary Y-axis providing its own space.
      var yAxes = chart.GetUniqueYAxes();
      float rightMargin = (yAxes.Count > 1 ? 0 : 10) * context.Density;

      // Determine nice range once during measurement based on available space
      if (!chart.IsCategoricalX && Scale == NTAxisScale.Linear && !chart.HasViewRange(this)) {
         var (min, max) = chart.GetXRange(this, false);
         var maxTicks = Math.Min(MaxTicks, Math.Max(2, (int)(renderArea.Width / (100 * context.Density))));
         var (niceMin, niceMax, _) = CalculateNiceScaling(min, max, maxTicks);
         CachedXRange = (niceMin, niceMax);
      }

      return new SKRect(renderArea.Left, renderArea.Top, renderArea.Right - rightMargin, renderArea.Bottom - totalAxisHeight);
   }

   /// <inheritdoc />
   internal override void Render(NTRenderContext context, IAxisChart chart) {
      var plotArea = context.PlotArea;
      var canvas = context.Canvas;
      var (xMinReal, xMaxReal) = chart.GetXRange(this, false);

      _textPaint ??= new SKPaint {
         IsAntialias = true
      };
      _textPaint.Color = context.TextColor;

      _textFont ??= new SKFont {
         Embolden = true,
         Typeface = context.DefaultFont.Typeface
      };
      _textFont.Size = 12 * context.Density;

      _titlePaint ??= new SKPaint {
         IsAntialias = true
      };
      _titlePaint.Color = context.TextColor;

      _titleFont ??= new SKFont {
         Embolden = true,
         Typeface = context.DefaultFont.Typeface
      };
      _titleFont.Size = 16 * context.Density;

      _linePaint ??= new SKPaint {
         StrokeWidth = context.Density,
         Style = SKPaintStyle.Stroke,
         IsAntialias = true
      };
      _linePaint.Color = chart.GetThemeColor(TnTColor.Outline);

      var yLine = plotArea.Bottom;
      canvas.DrawLine(plotArea.Left, yLine, plotArea.Right, yLine, _linePaint);

      var isCategorical = chart.IsCategoricalX;

      if (isCategorical) {
         var allValues = chart.GetAllXValues();
         if (allValues.Any()) {
            for (var i = 0; i < allValues.Count; i++) {
               var val = allValues[i];
               var scaledVal = chart.GetScaledXValue(val);
               var screenCoord = chart.ScaleX(scaledVal, plotArea);

               if (screenCoord < plotArea.Left - (1 * context.Density) || screenCoord > plotArea.Right + (1 * context.Density)) continue;

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

               canvas.DrawText(label, screenCoord, yLine + (12 * context.Density), textAlign, _textFont, _textPaint);
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
            if (screenCoord < plotArea.Left - (1 * context.Density) || screenCoord > plotArea.Right + (1 * context.Density)) continue;

            canvas.DrawText(val.ToString("G"), screenCoord, yLine + (12 * context.Density), SKTextAlign.Center, _textFont, _textPaint);
         }
      }
      else {
         var maxTicks = Math.Min(MaxTicks, Math.Max(2, (int)(plotArea.Width / (100 * context.Density))));
         var (niceMin, niceMax, spacing) = CalculateNiceScaling(xMinReal, xMaxReal, maxTicks);
         int totalLabels = (int)Math.Round((niceMax - niceMin) / spacing) + 1;
         for (int i = 0; i < totalLabels; i++) {
            double val = niceMin + i * spacing;
            var screenCoord = chart.ScaleX(val, plotArea);

            if (screenCoord < plotArea.Left - (1 * context.Density) || screenCoord > plotArea.Right + (1 * context.Density)) continue;

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

            canvas.DrawText(label, screenCoord, yLine + (12 * context.Density), textAlign, _textFont, _textPaint);

         }
      }

      if (!string.IsNullOrEmpty(Title)) {
         canvas.DrawText(Title, plotArea.Left + (plotArea.Width / 2), plotArea.Bottom + (30 * context.Density), SKTextAlign.Center, _titleFont, _titlePaint);
      }
   }
}
