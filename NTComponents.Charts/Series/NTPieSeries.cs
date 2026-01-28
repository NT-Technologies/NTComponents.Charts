using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core.Series;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents a pie/donut series in a circular chart.
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTPieSeries<TData> : NTCircularSeries<TData> where TData : class
{
   [Parameter]
   public float ExplosionDistance { get; set; } = 10f;

   /// <summary>
   ///    Gets or sets whether to explode slices on entry.
   /// </summary>
   [Parameter]
   public bool ExplosionOnEntry { get; set; } = true;

   private readonly Dictionary<int, float> _explosionFactors = new();
   private DateTime _lastRenderTime = DateTime.Now;

   public override void Render(NTRenderContext context, SKRect renderArea)
   {
      CalculateSlices(renderArea);
      if (!SliceInfos.Any()) return;

      var progress = GetAnimationProgress();
      var myVisibilityFactor = VisibilityFactor;
      var totalSweep = progress * 360f;

      var now = DateTime.Now;
      var deltaTime = (float)(now - _lastRenderTime).TotalSeconds;
      _lastRenderTime = now;

      float radius = Math.Min(renderArea.Width, renderArea.Height) / 2f;
      float innerRadius = radius * InnerRadiusRatio;

      // Entry explosion factor (starts at 1.0 and goes to 0.0 as progress goes from 0 to 0.8)
      float entryFactor = ExplosionOnEntry ? Math.Max(0, 1.0f - (progress / 0.8f)) : 0f;

      foreach (var slice in SliceInfos)
      {
         // Update explosion factor for this slice (hover)
         bool isTargetExploded = Chart.HoveredSeries == this && Chart.HoveredPointIndex == slice.Index;
         float currentFactor = _explosionFactors.GetValueOrDefault(slice.Index, 0f);
         float targetFactor = isTargetExploded ? 1f : 0f;

         if (Math.Abs(currentFactor - targetFactor) > 0.001f)
         {
            // Animate over ~150ms
            float step = deltaTime / 0.15f;
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
         if (slice.StartAngle + 90 + sweep > totalSweep)
         {
            sweep = totalSweep - (slice.StartAngle + 90);
         }

         if (sweep <= 0) continue;

         var isAnyHovered = Chart.HoveredSeries != null;
          var isOtherSeriesHovered = isAnyHovered && Chart.HoveredSeries != this;
          var isOtherPointHovered = isAnyHovered && Chart.HoveredSeries == this && Chart.HoveredPointIndex != slice.Index;

          var baseColor = Chart.GetThemeColor(Chart.Palette[slice.Index % Chart.Palette.Count].Background);

          var args = new NTDataPointRenderArgs<TData>
         {
            Data = slice.Data,
            Index = slice.Index,
            Color = baseColor,
            GetThemeColor = Chart.GetThemeColor
         };
         OnDataPointRender?.Invoke(args);

         var color = args.Color ?? baseColor;

         // Use currentFactor for smooth color transition too
         float dimFactor = 1.0f;
         if (isOtherSeriesHovered || isOtherPointHovered)
         {
            if (Chart.HoveredSeries == this && Chart.HoveredPointIndex.HasValue)
            {
               var otherFactor = _explosionFactors.GetValueOrDefault(Chart.HoveredPointIndex.Value, 0f);
               dimFactor = 1.0f - (otherFactor * 0.85f);
            }
            else if (isOtherSeriesHovered)
            {
               dimFactor = HoverFactor;
            }
         }

         color = color.WithAlpha((byte)(color.Alpha * dimFactor * myVisibilityFactor));

         using var paint = new SKPaint
         {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
         };

         float currentRadius = radius;
         float currentInnerRadius = innerRadius;
         float centerX = renderArea.MidX;
         float centerY = renderArea.MidY;

         if (totalExplosionFactor > 0)
         {
            // Explode the slice
            float midAngle = slice.StartAngle + slice.SweepAngle / 2f;
            float rad = midAngle * (float)Math.PI / 180f;
            var explosionOffset = ExplosionDistance * context.Density * totalExplosionFactor;
            centerX += (float)Math.Cos(rad) * explosionOffset;
            centerY += (float)Math.Sin(rad) * explosionOffset;

            // Increase opacity for the exploded slice (only if hovered)
            if (currentFactor > 0)
            {
               var alpha = (byte)((color.Alpha / dimFactor) * (dimFactor + (1f - dimFactor) * currentFactor));
               paint.Color = color.WithAlpha(alpha);
            }
         }

         using var path = new SKPath();
         var outerRect = new SKRect(centerX - currentRadius, centerY - currentRadius, centerX + currentRadius, centerY + currentRadius);
         var innerRect = new SKRect(centerX - currentInnerRadius, centerY - currentInnerRadius, centerX + currentInnerRadius, centerY + currentInnerRadius);

         path.ArcTo(outerRect, slice.StartAngle, sweep, true);
         if (currentInnerRadius > 0)
         {
            path.ArcTo(innerRect, slice.StartAngle + sweep, -sweep, false);
         }
         else
         {
            path.LineTo(centerX, centerY);
         }
         path.Close();

         context.Canvas.DrawPath(path, paint);

         if (ShowDataLabels && progress >= 1.0f)
         {
            RenderSliceLabel(context, slice, centerX, centerY, currentRadius, currentInnerRadius, color, args);
         }
      }
   }

   private void RenderSliceLabel(NTRenderContext context, PieSliceInfo slice, float centerX, float centerY, float radius, float innerRadius, SKColor color, NTDataPointRenderArgs<TData>? args)
   {
      float midAngle = slice.StartAngle + slice.SweepAngle / 2f;
      float rad = midAngle * (float)Math.PI / 180f;

      float labelRadius = innerRadius + (radius - innerRadius) / 2f;
      if (innerRadius == 0) labelRadius = radius * 0.7f;

      float lx = centerX + (float)Math.Cos(rad) * labelRadius;
      float ly = centerY + (float)Math.Sin(rad) * labelRadius;

      var text = string.Format(DataLabelFormat, slice.Value);

      var labelColor = args?.DataLabelColor ?? (DataLabelColor.HasValue ? Chart.GetThemeColor(DataLabelColor.Value) : Chart.GetPaletteTextColor(slice.Index));
      var labelSize = (args?.DataLabelSize ?? DataLabelSize) * context.Density;

      using var paint = new SKPaint
      {
         Color = labelColor,
         IsAntialias = true
      };
      using var font = new SKFont { Size = labelSize, Typeface = context.DefaultFont.Typeface };

      context.Canvas.DrawText(text, lx, ly, SKTextAlign.Center, font, paint);
   }

   public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea)
   {
      float radius = Math.Min(renderArea.Width, renderArea.Height) / 2f;
      float innerRadius = radius * InnerRadiusRatio;
      float centerX = renderArea.MidX;
      float centerY = renderArea.MidY;

      float dx = point.X - centerX;
      float dy = point.Y - centerY;
      float distSq = dx * dx + dy * dy;

      float angle = (float)Math.Atan2(dy, dx) * 180f / (float)Math.PI;
      if (angle < -90) angle += 360; // Normalize to start from -90

      foreach (var slice in SliceInfos)
      {
         // 1. Angular check (relative to original center)
         if (angle >= slice.StartAngle && angle < slice.StartAngle + slice.SweepAngle)
         {
            // 2. Distance check
            // We consider the mouse "over" the slice if it's within the original area
            // OR within its current animated position. This prevents the "flicker"
            // that occurrs when a slice moves away from the cursor at the inner boundary.

            // Check original (unexploded) area
            if (distSq >= innerRadius * innerRadius && distSq <= radius * radius)
            {
               return (slice.Index, slice.Data);
            }

            // Check current visual area (if exploded)
            float factor = _explosionFactors.GetValueOrDefault(slice.Index, 0f);
            if (factor > 0)
            {
               float midAngle = slice.StartAngle + slice.SweepAngle / 2f;
               float rad = midAngle * (float)Math.PI / 180f;
               float explosionOffset = ExplosionDistance * Chart.Density * factor;
               float exX = centerX + (float)Math.Cos(rad) * explosionOffset;
               float exY = centerY + (float)Math.Sin(rad) * explosionOffset;

               float edx = point.X - exX;
               float edy = point.Y - exY;
               float edistSq = edx * edx + edy * edy;

               if (edistSq >= innerRadius * innerRadius && edistSq <= radius * radius)
               {
                  return (slice.Index, slice.Data);
               }
            }
         }
      }

      return null;
   }
}
