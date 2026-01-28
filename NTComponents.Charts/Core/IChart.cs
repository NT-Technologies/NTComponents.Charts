using SkiaSharp;
using System.Collections.Generic;
using NTComponents.Charts.Core.Axes;
using NTComponents.Core;

namespace NTComponents.Charts.Core;

/// <summary>
///     Common interface for all charts.
/// </summary>
public interface IChart
{
   /// <summary>
   ///     Gets the density of the screen.
   /// </summary>
   float Density { get; }

   /// <summary>
   ///     Gets the default font.
   /// </summary>
   SKFont DefaultFont { get; }

   /// <summary>
   ///     Gets the regular font.
   /// </summary>
   SKFont RegularFont { get; }

   /// <summary>
   ///     Resolves a theme color.
   /// </summary>
   SKColor GetThemeColor(TnTColor color);
}

/// <summary>
///    Interface for charts that support axes.
/// </summary>
public interface IAxisChart : IChart
{
   void SetXAxisOptions(NTAxisOptions options);
   void SetYAxisOptions(NTAxisOptions options);
   void SetRadialAxisOptions(NTAxisOptions options);
   void SetTooltip(NTTooltip tooltip);

   List<NTXAxisOptions> GetUniqueXAxes();
   List<NTYAxisOptions> GetUniqueYAxes();

   (double Min, double Max) GetXRange(NTAxisOptions? axis, bool padded);
   (decimal Min, decimal Max) GetYRange(NTAxisOptions? axis, bool padded);

   float ScaleX(double x, SKRect plotArea, NTAxisOptions? axis = null);
   float ScaleY(decimal y, NTAxisOptions? axis, SKRect plotArea);

   bool IsCategoricalX { get; }
   bool IsCategoricalY { get; }

   List<object> GetAllXValues();
   List<object> GetAllYValues();

   NTXAxisOptions? PrimaryXAxis { get; }
   NTYAxisOptions? PrimaryYAxis { get; }
   NTRadialAxisOptions? PrimaryRadialAxis { get; }

   double GetScaledXValue(object? originalX);
   decimal GetScaledYValue(object? originalY);

   bool IsXAxisDateTime { get; }
}
