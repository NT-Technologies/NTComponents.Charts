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
      if (chart == null) return false;
      var yAxes = chart.GetUniqueYAxes();
      return yAxes.IndexOf(this) > 0;
   }

   /// <inheritdoc />
   public override SKRect Render(NTRenderContext context, SKRect renderArea) {
      if (!Visible) return renderArea;

      float labelWidth = 50 * context.Density;
      float titleWidth = (string.IsNullOrEmpty(Title) ? 0 : 20) * context.Density;
      var totalAxisWidth = labelWidth + titleWidth + (5 * context.Density);

      // Determine nice range once during measurement based on available space
      if (!Chart.IsCategoricalY && Scale == NTAxisScale.Linear && !Chart.HasViewRange(this)) {
         var (min, max) = Chart.GetYRange(this, false);
         var maxTicks = Math.Min(MaxTicks, Math.Max(2, (int)(renderArea.Height / (40 * context.Density))));
         var (niceMin, niceMax, _) = CalculateNiceScaling(min, max, maxTicks);
         CachedYRange = (niceMin, niceMax);
      }

      var isSecondary = IsSecondary(Chart);
      SKRect newArea;
      if (isSecondary) {
         newArea = new SKRect(renderArea.Left, renderArea.Top, renderArea.Right - totalAxisWidth, renderArea.Bottom);
      }
      else {
         newArea = new SKRect(renderArea.Left + totalAxisWidth, renderArea.Top, renderArea.Right, renderArea.Bottom);
      }

      // Now draw the axis
      var canvas = context.Canvas;
      var (yMinReal, yMaxReal) = Chart.GetYRange(this, false);

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
      _linePaint.Color = Chart.GetThemeColor(TnTColor.Outline);

      var xLine = isSecondary ? renderArea.Right - totalAxisWidth : renderArea.Left + totalAxisWidth;
      // We align the line with what will BECOME the plot area
      canvas.DrawLine(xLine, renderArea.Top, xLine, renderArea.Bottom, _linePaint);

      // Temporary plot area for scaling
      var tempPlotArea = new SKRect(
         isSecondary ? renderArea.Left : xLine,
         renderArea.Top,
         isSecondary ? xLine : renderArea.Right,
         renderArea.Bottom);

      var isCategorical = Chart.IsCategoricalY;

      if (isCategorical) {
         var allValues = Chart.GetAllYValues().Cast<object>().ToList();
         if (allValues.Any()) {
            for (var i = 0; i < allValues.Count; i++) {
               var val = allValues[i];
               var scaledVal = Chart.GetScaledYValue(val);
               var screenCoord = Chart.ScaleY(scaledVal, this, tempPlotArea);

               if (screenCoord < tempPlotArea.Top - (1 * context.Density) || screenCoord > tempPlotArea.Bottom + (1 * context.Density)) continue;

               float yOffset = 5 * context.Density;
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
         var (min, max) = Chart.GetYRange(this, true);
         var dMin = Math.Max(0.000001, (double)min);
         var dMax = Math.Max(dMin * 1.1, (double)max);

         var startLog = (int)Math.Floor(Math.Log10(dMin));
         var endLog = (int)Math.Ceiling(Math.Log10(dMax));

         for (int log = startLog; log <= endLog; log++) {
            double val = Math.Pow(10, log);
            if ((decimal)val < min || (decimal)val > max) continue;

            var screenCoord = Chart.ScaleY((decimal)val, this, tempPlotArea);
            if (screenCoord < tempPlotArea.Top - (1 * context.Density) || screenCoord > tempPlotArea.Bottom + (1 * context.Density)) continue;

            if (isSecondary) {
               canvas.DrawText(val.ToString("G"), xLine + (4 * context.Density), screenCoord + (5 * context.Density), SKTextAlign.Left, _textFont, _textPaint);
            }
            else {
               canvas.DrawText(val.ToString("G"), xLine - (4 * context.Density), screenCoord + (5 * context.Density), SKTextAlign.Right, _textFont, _textPaint);
            }
         }
      }
      else {
         var maxTicks = Math.Min(MaxTicks, Math.Max(2, (int)(tempPlotArea.Height / (40 * context.Density))));
         var (niceMin, niceMax, spacing) = CalculateNiceScaling(yMinReal, yMaxReal, maxTicks);
         int totalLabels = (int)Math.Round((double)((niceMax - niceMin) / spacing)) + 1;
         for (int i = 0; i < totalLabels; i++) {
            decimal val = niceMin + i * spacing;
            var screenCoord = Chart.ScaleY(val, this, tempPlotArea);

            if (screenCoord < tempPlotArea.Top - (1 * context.Density) || screenCoord > tempPlotArea.Bottom + (1 * context.Density)) continue;

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
            canvas.RotateDegrees(90, xLine + (45 * context.Density), tempPlotArea.Top + (tempPlotArea.Height / 2));
            canvas.DrawText(Title, xLine + (45 * context.Density), tempPlotArea.Top + (tempPlotArea.Height / 2), SKTextAlign.Center, _titleFont, _titlePaint);
         }
         else {
            canvas.RotateDegrees(-90, xLine - (45 * context.Density), tempPlotArea.Top + (tempPlotArea.Height / 2));
            canvas.DrawText(Title, xLine - (45 * context.Density), tempPlotArea.Top + (tempPlotArea.Height / 2), SKTextAlign.Center, _titleFont, _titlePaint);
         }
         canvas.Restore();
      }

      return newArea;
   }
}
