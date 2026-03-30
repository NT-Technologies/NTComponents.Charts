namespace NTComponents.Charts.Core;

/// <summary>
///     Represents the margins around a chart.
/// </summary>
public struct ChartMargin {
    /// <summary>Gets or sets the top margin.</summary>
    public float Top { get; set; }

    /// <summary>Gets or sets the right margin.</summary>
    public float Right { get; set; }

    /// <summary>Gets or sets the bottom margin.</summary>
    public float Bottom { get; set; }

    /// <summary>Gets or sets the left margin.</summary>
    public float Left { get; set; }

    /// <summary>
    ///     Initializes a new <see cref="ChartMargin"/> with individual side values.
    /// </summary>
    /// <param name="top">Top margin.</param>
    /// <param name="right">Right margin.</param>
    /// <param name="bottom">Bottom margin.</param>
    /// <param name="left">Left margin.</param>
    public ChartMargin(float top, float right, float bottom, float left) {
        Top = top;
        Right = right;
        Bottom = bottom;
        Left = left;
    }

    /// <summary>
    ///     Creates a <see cref="ChartMargin"/> where all four sides share the same <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The uniform margin to apply on every side.</param>
    /// <returns>A <see cref="ChartMargin"/> with equal margins on all sides.</returns>
    public static ChartMargin All(float value) => new(value, value, value, value);
}
