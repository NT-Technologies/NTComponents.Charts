namespace NTComponents.Charts.Core;

/// <summary>
/// Specifies the interaction modes supported by a chart.
/// </summary>
[Flags]
public enum ChartInteractions
{
   /// <summary>
   /// No interactions enabled.
   /// </summary>
   None = 0,

   /// <summary>
   /// Enable panning on the X-axis.
   /// </summary>
   XPan = 1 << 0,

   /// <summary>
   /// Enable panning on the Y-axis.
   /// </summary>
   YPan = 1 << 1,

   /// <summary>
   /// Enable zooming on the X-axis.
   /// </summary>
   XZoom = 1 << 2,

   /// <summary>
   /// Enable zooming on the Y-axis.
   /// </summary>
   YZoom = 1 << 3,

   /// <summary>
   /// Enable panning on both X and Y axes.
   /// </summary>
   PanAll = XPan | YPan,

   /// <summary>
   /// Enable zooming on both X and Y axes.
   /// </summary>
   ZoomAll = XZoom | YZoom,

   /// <summary>
   /// Enable all interactions (Panning and Zooming on both axes).
   /// </summary>
   All = PanAll | ZoomAll
}
