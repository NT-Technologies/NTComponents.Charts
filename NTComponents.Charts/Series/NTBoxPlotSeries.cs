using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core.Series;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents a box plot series in a cartesian chart.
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTBoxPlotSeries<TData> : NTCartesianSeries<TData> where TData : class {
   [Parameter, EditorRequired]
   public Func<TData, BoxPlotValues> BoXValue { get; set; } = default!;

   /// <summary>
   ///    Gets or sets the width of the box as a fraction of the available space (0.0 to 1.0).
   /// </summary>
   [Parameter]
   public float BoxWidthRatio { get; set; } = 0.6f;

   /// <summary>
   ///    Gets or sets the width of the whiskers as a fraction of the box width (0.0 to 1.0).
   /// </summary>
   [Parameter]
   public float WhiskerWidthRatio { get; set; } = 0.5f;

   private SKPaint? _strokePaint;
   private SKPaint? _fillPaint;

   public override SKRect Render(NTRenderContext context, SKRect renderArea) {
     

      return renderArea;
   }

   protected override void Dispose(bool disposing) {
      if (disposing) {
         _strokePaint?.Dispose();
         _fillPaint?.Dispose();
         _strokePaint = null;
         _fillPaint = null;
      }
      base.Dispose(disposing);
   }

   public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea) {
    

      return null;
   }
}
