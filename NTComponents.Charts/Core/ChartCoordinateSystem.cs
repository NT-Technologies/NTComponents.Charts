namespace NTComponents.Charts.Core;

/// <summary>
///     Defines the coordinate system used by a chart series.
/// </summary>
public enum ChartCoordinateSystem
{
   /// <summary>
   ///     No coordinate system.
   /// </summary>
   None,

   /// <summary>
   ///     A cartesian coordinate system (X and Y axes).
   /// </summary>
   Cartesian,

   /// <summary>
   ///     A polar coordinate system (Angle and Radius).
   /// </summary>
   Polar,

   /// <summary>
   ///     A part-to-whole visualization (e.g., Pie Chart).
   /// </summary>
   Circular,

   /// <summary>
   ///     A hierarchical visualization using nested rectangles.
   /// </summary>
   TreeMap
}
