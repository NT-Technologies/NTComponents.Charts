namespace NTComponents.Charts.Core.Series;

/// <summary>
///     Defines the rendering style for chart lines.
/// </summary>
public enum LineStyle {
    /// <summary>
    ///     The line is rendered as a solid stroke.
    /// </summary>
    Solid,

    /// <summary>
    ///     The line is rendered as a dashed stroke.
    /// </summary>
    Dashed,

    /// <summary>
    ///     The line is not rendered.
    /// </summary>
    None
}

/// <summary>
///     Defines how lines are interpolated between data points.
/// </summary>
public enum LineInterpolation {
    /// <summary>
    ///     Direct straight lines between points.
    /// </summary>
    Straight,

    /// <summary>
    ///     Lines with smoothed corners.
    /// </summary>
    Smoothed,

    /// <summary>
    ///     Fully rounded/curved lines (Catmull-Rom or Cubic).
    /// </summary>
    Curved,

    /// <summary>
    ///     Step-style lines.
    /// </summary>
    Step
}

/// <summary>
///     Defines how data is aggregated when zoomed out.
/// </summary>
public enum AggregationMode {
    /// <summary>
    ///     No aggregation.
    /// </summary>
    None,

    /// <summary>
    ///     Average value of all points in the bucket.
    /// </summary>
    Average,

    /// <summary>
    ///     Sum of all points in the bucket.
    /// </summary>
    Sum,

    /// <summary>
    ///     Minimum value of all points in the bucket.
    /// </summary>
    Min,

    /// <summary>
    ///     Maximum value of all points in the bucket.
    /// </summary>
    Max
}
