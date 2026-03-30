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
   /// <summary>
   ///    Gets or sets the selector that extracts the numeric weight value from a data item, used to determine cell intensity.
   /// </summary>
   [Parameter, EditorRequired]
   public Func<TData, decimal> WeightSelector { get; set; } = default!;

   /// <summary>
   ///    Gets or sets the theme color applied to cells with the lowest weight value.
   /// </summary>
   [Parameter]
   public TnTColor MinColor { get; set; } = TnTColor.SurfaceContainerLowest;

   /// <summary>
   ///    Gets or sets the theme color applied to cells with the highest weight value.
   /// </summary>
   [Parameter]
   public TnTColor MaxColor { get; set; } = TnTColor.Primary;

   /// <summary>
   ///    Gets or sets the padding between cells (0.0 to 1.0).
   /// </summary>
   [Parameter]
   public float CellPadding { get; set; } = 0.05f;

   private SKPaint? _cellPaint;

   /// <summary>
   ///    Renders the heatmap series onto the provided canvas within the given render area.
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

   /// <summary>
   ///    Performs a hit test against the rendered heatmap cells to identify which data item (if any) is at the given point.
   /// </summary>
   /// <param name="point">The point in screen coordinates to test.</param>
   /// <param name="renderArea">The bounding rectangle used during the last render pass.</param>
   /// <returns>The index and data item at the point, or <see langword="null"/> if no hit was found.</returns>
   public override (int Index, TData? Data)? HitTest(SKPoint point, SKRect renderArea) {
  

      return null;
   }
}
