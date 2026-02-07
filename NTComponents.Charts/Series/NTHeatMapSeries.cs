using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core.Series;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents a heatmap series in a cartesian chart.
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTHeatMapSeries<TData> : NTCartesianSeries<TData> where TData : class {
   [Parameter, EditorRequired]
   public Func<TData, decimal> WeightSelector { get; set; } = default!;

   [Parameter]
   public TnTColor MinColor { get; set; } = TnTColor.SurfaceContainerLowest;

   [Parameter]
   public TnTColor MaxColor { get; set; } = TnTColor.Primary;

   /// <summary>
   ///    Gets or sets the padding between cells (0.0 to 1.0).
   /// </summary>
   [Parameter]
   public float CellPadding { get; set; } = 0.05f;

   private SKPaint? _cellPaint;

   public override SKRect Render(NTRenderContext context, SKRect renderArea) {
    

      return renderArea;
   }

   protected override void Dispose(bool disposing) {
      if (disposing) {
         _cellPaint?.Dispose();
         _cellPaint = null;
      }
      base.Dispose(disposing);
   }

   private SKColor InterpolateColor(SKColor c1, SKColor c2, float t) {
      byte r = (byte)(c1.Red + (c2.Red - c1.Red) * t);
      byte g = (byte)(c1.Green + (c2.Green - c1.Green) * t);
      byte b = (byte)(c1.Blue + (c2.Blue - c1.Blue) * t);
      byte a = (byte)(c1.Alpha + (c2.Alpha - c1.Alpha) * t);
      return new SKColor(r, g, b, a);
   }

   public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea) {
  

      return null;
   }
}
