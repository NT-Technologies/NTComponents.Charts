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
   /// <summary>
   ///    Gets or sets the selector that extracts box plot statistical values from a data item.
   /// </summary>
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

   /// <summary>
   ///    Renders the box plot series onto the provided canvas within the given render area.
   /// </summary>
   /// <param name="context">The rendering context containing the canvas and theme information.</param>
   /// <param name="renderArea">The bounding rectangle available for rendering.</param>
   /// <returns>The render area after rendering.</returns>
   public override SKRect Render(NTRenderContext context, SKRect renderArea) {
     

      return renderArea;
   }

   /// <inheritdoc />
   protected override void Dispose(bool disposing) {
      if (disposing) {
         _strokePaint?.Dispose();
         _fillPaint?.Dispose();
         _strokePaint = null;
         _fillPaint = null;
      }
      base.Dispose(disposing);
   }

   /// <summary>
   ///    Performs a hit test against the rendered boxes to identify which data item (if any) is at the given point.
   /// </summary>
   /// <param name="point">The point in screen coordinates to test.</param>
   /// <param name="renderArea">The bounding rectangle used during the last render pass.</param>
   /// <returns>The index and data item at the point, or <see langword="null"/> if no hit was found.</returns>
   public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea) {
    

      return null;
   }
}
