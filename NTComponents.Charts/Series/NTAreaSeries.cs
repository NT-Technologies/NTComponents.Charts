using Microsoft.AspNetCore.Components;
using NTComponents.Charts.Core.Series;
using NTComponents.Charts.Core;
using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents an area series in a cartesian chart.
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTAreaSeries<TData> : NTLineSeries<TData> where TData : class {
   /// <summary>
   ///    Gets or sets the opacity of the area fill (0.0 to 1.0).
   /// </summary>
   [Parameter]
   public float AreaOpacity { get; set; } = 0.3f;

   /// <summary>
   ///    Gets or sets the Y value for the baseline of the area. Defaults to 0.
   /// </summary>
   [Parameter]
   public decimal BaselineValue { get; set; } = 0;

   private SKPaint? _areaPaint;
   private SKPath? _areaPath;
   private (int Version, decimal Baseline)? _areaPathKey;

   /// <inheritdoc />
   protected override void OnDataChanged() {
      _areaPathKey = null;
      base.OnDataChanged();
   }

   /// <inheritdoc />
   protected override void RenderFill(NTRenderContext context, SKRect renderArea, SKColor color) {
      var points = RenderScreenPoints;
      if (points.Count < 2) {
         return;
      }

      var key = (RenderVersion, BaselineValue);
      if (_areaPath is null || _areaPathKey != key) {
         _areaPath?.Dispose();
         _areaPath = BuildAreaPath(points, ScaleSeriesY(BaselineValue, renderArea));
         _areaPathKey = key;
      }

      _areaPaint ??= new SKPaint { Style = SKPaintStyle.Fill, IsAntialias = true };
      _areaPaint.Color = color.WithAlpha((byte)(color.Alpha * Math.Clamp(AreaOpacity, 0f, 1f)));
      context.Canvas.DrawPath(_areaPath, _areaPaint);
   }

   /// <inheritdoc />
   protected override void Dispose(bool disposing) {
      if (disposing) {
         _areaPaint?.Dispose();
         _areaPath?.Dispose();
         _areaPaint = null;
         _areaPath = null;
      }
      base.Dispose(disposing);
   }

   private SKPath BuildAreaPath(List<SKPoint> points, float baselineCoord) {
      var path = BuildPath(points);
      using var pathBuilder = new SKPathBuilder(path);
      path.Dispose();
      pathBuilder.LineTo(points[^1].X, baselineCoord);
      pathBuilder.LineTo(points[0].X, baselineCoord);
      pathBuilder.Close();

      return pathBuilder.Detach();
   }
}
