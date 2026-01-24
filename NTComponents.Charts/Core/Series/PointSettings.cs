namespace NTComponents.Charts.Core.Series;

/// <summary>
///     Defines the rendering style for data points.
/// </summary>
public enum PointStyle
{
   /// <summary>
   ///     Points are not rendered.
   /// </summary>
   None,

   /// <summary>
   ///    Points are rendered with a solid fill.
   /// </summary>
   Filled,

   /// <summary>
   ///     Points are rendered with an outline.
   /// </summary>
   Outlined
}

/// <summary>
///     Defines the shape of data points.
/// </summary>
public enum PointShape
{
   /// <summary>
   ///     A circular point.
   /// </summary>
   Circle,

   /// <summary>
   ///     A square point.
   /// </summary>
   Square,

   /// <summary>
   ///     A triangular point.
   /// </summary>
   Triangle,

   /// <summary>
   ///     A diamond-shaped point.
   /// </summary>
   Diamond
}
