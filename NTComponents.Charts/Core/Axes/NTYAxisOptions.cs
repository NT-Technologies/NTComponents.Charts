using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NTComponents.Charts.Core.Axes;

/// <summary>
///     Options for the Y axis of a cartesian chart.
/// </summary>
public class NTYAxisOptions : NTAxisOptions {

   private static readonly NTYAxisOptions _default = new();
   public static NTYAxisOptions Default => _default;

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
      Chart?.SetYAxisOptions(this);
   }

   private bool IsSecondary(IAxisChart chart) {
      var yAxes = chart.GetUniqueYAxes();
      return yAxes.Count > 1 && yAxes.IndexOf(this) == 1;
   }

   /// <inheritdoc />
   internal override SKRect Measure(SKRect renderArea, NTRenderContext context, IAxisChart chart) {
      float labelWidth = 50 * context.Density;
      float titleWidth = (string.IsNullOrEmpty(Title) ? 0 : 20) * context.Density;
      var totalAxisWidth = labelWidth + titleWidth + (5 * context.Density);

      // Determine nice range once during measurement based on available space
      if (!chart.IsCategoricalY && Scale == NTAxisScale.Linear && !chart.HasViewRange(this)) {
         var (min, max) = chart.GetYRange(this, false);
         var maxTicks = Math.Min(MaxTicks, Math.Max(2, (int)(renderArea.Height / (40 * context.Density))));
         var (niceMin, niceMax, _) = CalculateNiceScaling(min, max, maxTicks);
         CachedYRange = (niceMin, niceMax);
      }

      if (IsSecondary(chart)) {
         return new SKRect(renderArea.Left, renderArea.Top, renderArea.Right - totalAxisWidth, renderArea.Bottom);
      }
      return new SKRect(renderArea.Left + totalAxisWidth, renderArea.Top, renderArea.Right, renderArea.Bottom);
   }

   /// <inheritdoc />
   internal override void Render(NTRenderContext context, IAxisChart chart) {
      var plotArea = context.PlotArea;
      var canvas = context.Canvas;
      var (yMinReal, yMaxReal) = chart.GetYRange(this, false);

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

      var isSecondary = IsSecondary(chart);
      var xLine = isSecondary ? plotArea.Right : plotArea.Left;
      canvas.DrawLine(xLine, plotArea.Top, xLine, plotArea.Bottom, _linePaint);

      var isCategorical = chart.IsCategoricalY;

      if (isCategorical) {
         var allValues = chart.GetAllYValues().Cast<object>().ToList();
         if (allValues.Any()) {
            for (var i = 0; i < allValues.Count; i++) {
               var val = allValues[i];
               var scaledVal = chart.GetScaledYValue(val);
               var screenCoord = chart.ScaleY(scaledVal, this, plotArea);

               if (screenCoord < plotArea.Top - (1 * context.Density) || screenCoord > plotArea.Bottom + (1 * context.Density)) continue;

               float yOffset = 5 * context.Density;
               if (i == 0 && !isCategorical) {
                  yOffset = 0;
               }
               else if (i == allValues.Count - 1 && !isCategorical) {
                  yOffset = 10 * context.Density;
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

                  canvas.DrawText(label, xLine + (4 * context.Density), screenCoord + yOffset, SKTextAlign.Left, _textFont, _textPaint);
               }
               else {
                  canvas.DrawText(label, xLine - (4 * context.Density), screenCoord + yOffset, SKTextAlign.Right, _textFont, _textPaint);
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
            if (screenCoord < plotArea.Top - (1 * context.Density) || screenCoord > plotArea.Bottom + (1 * context.Density)) continue;

            if (isSecondary) {
               canvas.DrawText(val.ToString("G"), xLine + (4 * context.Density), screenCoord + (5 * context.Density), SKTextAlign.Left, _textFont, _textPaint);
            }
            else {
               canvas.DrawText(val.ToString("G"), xLine - (4 * context.Density), screenCoord + (5 * context.Density), SKTextAlign.Right, _textFont, _textPaint);
            }
         }
      }
      else {
         var maxTicks = Math.Min(MaxTicks, Math.Max(2, (int)(plotArea.Height / (40 * context.Density))));
         var (niceMin, niceMax, spacing) = CalculateNiceScaling(yMinReal, yMaxReal, maxTicks);
         int totalLabels = (int)Math.Round((double)((niceMax - niceMin) / spacing)) + 1;
         for (int i = 0; i < totalLabels; i++) {
            decimal val = niceMin + i * spacing;
            var screenCoord = chart.ScaleY(val, this, plotArea);

            if (screenCoord < plotArea.Top - (1 * context.Density) || screenCoord > plotArea.Bottom + (1 * context.Density)) continue;

            float yOffset = 5 * context.Density;
            if (i == 0) {
               yOffset = 0;
            }
            else if (i == totalLabels - 1) {
               yOffset = 10 * context.Density;
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
               canvas.DrawText(label, xLine + (4 * context.Density), screenCoord + yOffset, SKTextAlign.Left, _textFont, _textPaint);
            }
            else {
               canvas.DrawText(label, xLine - (4 * context.Density), screenCoord + yOffset, SKTextAlign.Right, _textFont, _textPaint);
            }
         }
      }


      if (!string.IsNullOrEmpty(Title)) {
         canvas.Save();
         if (isSecondary) {
            canvas.RotateDegrees(90, xLine + (45 * context.Density), plotArea.Top + (plotArea.Height / 2));
            canvas.DrawText(Title, xLine + (45 * context.Density), plotArea.Top + (plotArea.Height / 2), SKTextAlign.Center, _titleFont, _titlePaint);
         }
         else {
            canvas.RotateDegrees(-90, xLine - (45 * context.Density), plotArea.Top + (plotArea.Height / 2));
            canvas.DrawText(Title, xLine - (45 * context.Density), plotArea.Top + (plotArea.Height / 2), SKTextAlign.Center, _titleFont, _titlePaint);
         }
         canvas.Restore();
      }
   }
}
