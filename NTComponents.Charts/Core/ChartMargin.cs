namespace NTComponents.Charts.Core;

/// <summary>
///     Represents the margins around a chart.
/// </summary>
public struct ChartMargin {
    public float Top { get; set; }
    public float Right { get; set; }
    public float Bottom { get; set; }
    public float Left { get; set; }

    public ChartMargin(float top, float right, float bottom, float left) {
        Top = top;
        Right = right;
        Bottom = bottom;
        Left = left;
    }

    public static ChartMargin All(float value) => new(value, value, value, value);
}
