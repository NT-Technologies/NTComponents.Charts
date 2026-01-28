using Microsoft.AspNetCore.Components.Web;
using SkiaSharp;

namespace NTComponents.Charts.Core.Series;

/// <summary>
///     Common interface for cartesian series, used by axes to retrieve data ranges and scaling information.
/// </summary>
public interface ICartesianSeries
{
   /// <summary>
   ///     Returns the series' data range on the X axis.
   /// </summary>
   (double Min, double Max)? GetXRange();

   /// <summary>
   ///     Returns the series' data range on the Y axis.
   /// </summary>
   (decimal Min, decimal Max)? GetYRange(double? xMin = null, double? xMax = null);

   /// <summary>
   ///    Returns the current view range on the X axis (if zoomed/panned).
   /// </summary>
   (double Min, double Max)? GetViewXRange();

   /// <summary>
   ///   Returns the current view range on the Y axis (if zoomed/panned).
   /// </summary>
   (decimal Min, decimal Max)? GetViewYRange();

   /// <summary>
   ///    Gets whether the series is effectively visible.
   /// </summary>
   bool IsEffectivelyVisible { get; }

   /// <summary>
   ///    Gets the X axis options assigned to this series.
   /// </summary>
   Axes.NTXAxisOptions EffectiveXAxis { get; }

   /// <summary>
   ///    Gets the Y axis options assigned to this series.
   /// </summary>
   Axes.NTYAxisOptions EffectiveYAxis { get; }
}
