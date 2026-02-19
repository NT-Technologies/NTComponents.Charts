using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core.Series;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents a pie/donut series in a circular chart.
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTPieSeries<TData> : NTCircularSeries<TData> where TData : class {
   [Parameter]
   public float ExplosionDistance { get; set; } = 10f;

   /// <summary>
   ///    Gets or sets whether slices should explode on hover.
   /// </summary>
   [Parameter]
   public bool ExplodeOnHover { get; set; } = true;

   /// <summary>
   ///    Gets or sets whether to explode slices on entry.
   /// </summary>
   [Parameter]
   public bool ExplosionOnEntry { get; set; } = true;

   /// <summary>
   ///    Gets or sets the fixed inner radius (in device-independent pixels).
   ///    If greater than zero, this value takes precedence over <see cref="InnerRadiusRatio"/>.
   /// </summary>
   [Parameter]
   public float InnerRadius { get; set; } = 0f;

   private readonly Dictionary<int, float> _explosionFactors = new();
   private DateTime _lastRenderTime = DateTime.Now;

   private SKPaint? _slicePaint;
   private SKPath? _slicePath;
   private SKPaint? _labelPaint;
   private SKFont? _labelFont;

   public override SKRect Render(NTRenderContext context, SKRect renderArea) {
      CalculateSlices(renderArea);
      if (!SliceInfos.Any()) return renderArea;

      var progress = GetAnimationProgress();
      var myVisibilityFactor = VisibilityFactor;
      var totalSweep = progress * 360f;

      var now = DateTime.Now;
      var deltaTime = (float)(now - _lastRenderTime).TotalSeconds;
      _lastRenderTime = now;

      float radius = Math.Min(renderArea.Width, renderArea.Height) / 2f;
      float innerRadius = GetInnerRadius(radius, context.Density);

      // Entry explosion factor (starts at 1.0 and goes to 0.0 as progress goes from 0 to 0.8)
      float entryFactor = ExplosionOnEntry ? Math.Max(0, 1.0f - (progress / 0.8f)) : 0f;

      _slicePaint ??= new SKPaint {
         IsAntialias = true,
         Style = SKPaintStyle.Fill
      };

      _slicePath ??= new SKPath();

      foreach (var slice in SliceInfos) {
         // Update explosion factor for this slice (hover)
         bool isTargetExploded = ExplodeOnHover && Chart.HoveredSeries == this && Chart.HoveredPointIndex == slice.Index;
         float currentFactor = _explosionFactors.GetValueOrDefault(slice.Index, 0f);
         float targetFactor = isTargetExploded ? 1f : 0f;

         if (Math.Abs(currentFactor - targetFactor) > 0.001f) {
            const float durationSeconds = 0.30f;
            float step = deltaTime / durationSeconds;
            if (currentFactor < targetFactor)
               currentFactor = Math.Min(targetFactor, currentFactor + step);
            else
               currentFactor = Math.Max(targetFactor, currentFactor - step);

            _explosionFactors[slice.Index] = currentFactor;
         }

         float totalExplosionFactor = Math.Max(currentFactor, entryFactor);

         // Only draw slices that fall within the current animation progress
         if (slice.StartAngle + 90 > totalSweep) continue;

         float sweep = slice.SweepAngle;
         if (slice.StartAngle + 90 + sweep > totalSweep) {
            sweep = totalSweep - (slice.StartAngle + 90);
         }

         if (sweep <= 0) continue;

         var isAnyHovered = Chart.HoveredSeries != null;
         var isOtherSeriesHovered = isAnyHovered && Chart.HoveredSeries != this;
         var isOtherPointHovered = isAnyHovered && Chart.HoveredSeries == this && Chart.HoveredPointIndex != slice.Index;

         var baseColor = Chart.GetThemeColor(Chart.Palette[slice.Index % Chart.Palette.Count].Background);

         var args = new NTDataPointRenderArgs<TData> {
            Data = slice.Data,
            Index = slice.Index,
            Color = baseColor,
            GetThemeColor = Chart.GetThemeColor
         };
         OnDataPointRender?.Invoke(args);

         var color = args.Color ?? baseColor;

         // Use currentFactor for smooth color transition too
         float dimFactor = 1.0f;
         if (isOtherSeriesHovered || isOtherPointHovered) {
            if (Chart.HoveredSeries == this && Chart.HoveredPointIndex.HasValue) {
               var otherFactor = _explosionFactors.GetValueOrDefault(Chart.HoveredPointIndex.Value, 0f);
               dimFactor = 1.0f - (otherFactor * 0.85f);
            }
            else if (isOtherSeriesHovered) {
               dimFactor = HoverFactor;
            }
         }

         color = color.WithAlpha((byte)(color.Alpha * dimFactor * myVisibilityFactor));

         _slicePaint.Color = color;

         float currentRadius = radius;
         float currentInnerRadius = innerRadius;
         float centerX = renderArea.MidX;
         float centerY = renderArea.MidY;

         if (totalExplosionFactor > 0) {
            // Explode the slice
            float midAngle = slice.StartAngle + slice.SweepAngle / 2f;
            float rad = midAngle * (float)Math.PI / 180f;
            var explosionOffset = ExplosionDistance * context.Density * totalExplosionFactor;
            centerX += (float)Math.Cos(rad) * explosionOffset;
            centerY += (float)Math.Sin(rad) * explosionOffset;

            // Increase opacity for the exploded slice (only if hovered)
            if (currentFactor > 0) {
               var alpha = (byte)((color.Alpha / dimFactor) * (dimFactor + (1f - dimFactor) * currentFactor));
               _slicePaint.Color = color.WithAlpha(alpha);
            }
         }

         _slicePath.Reset();
         var outerRect = new SKRect(centerX - currentRadius, centerY - currentRadius, centerX + currentRadius, centerY + currentRadius);
         var innerRect = new SKRect(centerX - currentInnerRadius, centerY - currentInnerRadius, centerX + currentInnerRadius, centerY + currentInnerRadius);

         _slicePath.ArcTo(outerRect, slice.StartAngle, sweep, true);
         if (currentInnerRadius > 0) {
            _slicePath.ArcTo(innerRect, slice.StartAngle + sweep, -sweep, false);
         }
         else {
            _slicePath.LineTo(centerX, centerY);
         }
         _slicePath.Close();

         context.Canvas.DrawPath(_slicePath, _slicePaint);

         if (ShowDataLabels && progress >= 1.0f) {
            RenderSliceLabel(context, slice, centerX, centerY, currentRadius, currentInnerRadius, color, args);
         }
      }

      return renderArea;
   }

   private void RenderSliceLabel(NTRenderContext context, PieSliceInfo slice, float centerX, float centerY, float radius, float innerRadius, SKColor color, NTDataPointRenderArgs<TData>? args) {
      if (!float.IsFinite(radius) || radius <= 0f || !float.IsFinite(innerRadius) || innerRadius < 0f) {
         return;
      }

      float midAngle = slice.StartAngle + slice.SweepAngle / 2f;
      float rad = midAngle * (float)Math.PI / 180f;

      string text;
      try {
         text = string.Format(DataLabelFormat, slice.Value);
      }
      catch {
         text = slice.Value.ToString("0.##");
      }

      if (string.IsNullOrWhiteSpace(text)) {
         return;
      }

      var labelColor = args?.DataLabelColor ?? (DataLabelColor.HasValue ? Chart.GetThemeColor(DataLabelColor.Value) : Chart.GetPaletteTextColor(slice.Index));
      var labelSize = (args?.DataLabelSize ?? DataLabelSize) * context.Density;
      if (!float.IsFinite(labelSize) || labelSize <= 0f) {
         return;
      }

      _labelPaint ??= new SKPaint {
         IsAntialias = true
      };
      _labelPaint.Color = labelColor;

      _labelFont ??= new SKFont { Typeface = context.DefaultFont.Typeface };
      _labelFont.Size = labelSize;

      // Avoid SKFont.MeasureText/metrics on WASM due intermittent native OOB crashes.
      // Use a conservative width estimate so labels render only when very likely to fit.
      var estimatedTextWidth = EstimateTextWidth(text, labelSize);
      var estimatedTextHeight = labelSize;
      if (!CanRenderLabelInsideSlice(slice.SweepAngle, radius, innerRadius, estimatedTextWidth, estimatedTextHeight)) {
         return;
      }

      float labelRadius = innerRadius + ((radius - innerRadius) / 2f);
      if (innerRadius <= 0) {
         labelRadius = radius * 0.67f;
      }

      float lx = centerX + ((float)Math.Cos(rad) * labelRadius);
      float ly = centerY + ((float)Math.Sin(rad) * labelRadius);
      var baselineY = ly + (labelSize * 0.35f);

      context.Canvas.DrawText(text, lx, baselineY, SKTextAlign.Center, _labelFont, _labelPaint);
   }

   protected override void Dispose(bool disposing) {
      if (disposing) {
         _slicePaint?.Dispose();
         _slicePath?.Dispose();
         _labelPaint?.Dispose();
         _labelFont?.Dispose();
         _slicePaint = null;
         _slicePath = null;
         _labelPaint = null;
         _labelFont = null;
      }
      base.Dispose(disposing);
   }

   public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea) {
      float radius = Math.Min(renderArea.Width, renderArea.Height) / 2f;
      float innerRadius = GetInnerRadius(radius, Chart.Density);
      float centerX = renderArea.MidX;
      float centerY = renderArea.MidY;

      float dx = point.X - centerX;
      float dy = point.Y - centerY;
      float distSq = dx * dx + dy * dy;

      float angle = (float)Math.Atan2(dy, dx) * 180f / (float)Math.PI;
      if (angle < -90) angle += 360; // Normalize to start from -90

      foreach (var slice in SliceInfos) {
         // 1. Angular check (relative to original center)
         if (angle >= slice.StartAngle && angle < slice.StartAngle + slice.SweepAngle) {
            // 2. Distance check
            // We consider the mouse "over" the slice if it's within the original area
            // OR within its current animated position. This prevents the "flicker"
            // that occurrs when a slice moves away from the cursor at the inner boundary.

            // Check original (unexploded) area
            if (distSq >= innerRadius * innerRadius && distSq <= radius * radius) {
               return (slice.Index, slice.Data);
            }

            // Check current visual area (if exploded)
            float factor = _explosionFactors.GetValueOrDefault(slice.Index, 0f);
            if (factor > 0) {
               float midAngle = slice.StartAngle + slice.SweepAngle / 2f;
               float rad = midAngle * (float)Math.PI / 180f;
               float explosionOffset = ExplosionDistance * Chart.Density * factor;
               float exX = centerX + (float)Math.Cos(rad) * explosionOffset;
               float exY = centerY + (float)Math.Sin(rad) * explosionOffset;

               float edx = point.X - exX;
               float edy = point.Y - exY;
               float edistSq = edx * edx + edy * edy;

               if (edistSq >= innerRadius * innerRadius && edistSq <= radius * radius) {
                  return (slice.Index, slice.Data);
               }
            }
         }
      }

      return null;
   }

   private float GetInnerRadius(float outerRadius, float density) {
      if (InnerRadius > 0f) {
         return Math.Clamp(InnerRadius * density, 0f, Math.Max(0f, outerRadius - 1f));
      }

      var ratio = Math.Clamp(InnerRadiusRatio, 0f, 0.95f);
      return outerRadius * ratio;
   }

   private static bool CanRenderLabelInsideSlice(float sweepAngleDegrees, float outerRadius, float innerRadius, float textWidth, float textHeight) {
      if (sweepAngleDegrees <= 0f) {
         return false;
      }

      var ringThickness = outerRadius - innerRadius;
      const float radialPadding = 4f;
      if ((textHeight + (2f * radialPadding)) > ringThickness) {
         return false;
      }

      var labelRadius = innerRadius + (ringThickness / 2f);
      if (innerRadius <= 0f) {
         labelRadius = outerRadius * 0.67f;
      }

      var sweepRadians = sweepAngleDegrees * ((float)Math.PI / 180f);
      var availableArcLength = labelRadius * sweepRadians;
      const float tangentialPadding = 6f;
      return (textWidth + (2f * tangentialPadding)) <= availableArcLength;
   }

   private static float EstimateTextWidth(string text, float labelSize) {
      if (string.IsNullOrEmpty(text)) {
         return 0f;
      }

      // Numeric labels dominate in this chart; this factor intentionally overestimates.
      return text.Length * (labelSize * 0.62f);
   }
}
