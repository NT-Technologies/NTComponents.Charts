using SkiaSharp;

namespace NTComponents.Charts;

/// <summary>
///     Represents a single segment inside a bar data point.
/// </summary>
public sealed class NTBarSegment {
    /// <summary>Gets the numeric value of this segment, used to determine its proportional size in the bar.</summary>
    public required decimal Value { get; init; }
    /// <summary>Gets an optional label displayed within or alongside this segment.</summary>
    public string? Label { get; init; }
    /// <summary>Gets an optional theme color applied to this segment.</summary>
    public NTColor? Color { get; init; }
    /// <summary>Gets an optional custom SkiaSharp color that overrides the theme color for this segment.</summary>
    public SKColor? CustomColor { get; init; }
}

/// <summary>
///     Context used by <see cref="NTBarSeries{TData}.SegmentColorSelector"/> to choose a segment color.
/// </summary>
/// <typeparam name="TData">The bar data type.</typeparam>
public sealed class NTBarSegmentColorContext<TData> where TData : class {
    /// <summary>Gets the data item associated with the bar containing this segment.</summary>
    public required TData Data { get; init; }
    /// <summary>Gets the zero-based index of the bar data item in the series data collection.</summary>
    public required int DataIndex { get; init; }
    /// <summary>Gets the zero-based index of this segment within the bar's segment list.</summary>
    public required int SegmentIndex { get; init; }
    /// <summary>Gets the label of this segment, or <see langword="null"/> if none was provided.</summary>
    public required string? SegmentLabel { get; init; }
    /// <summary>Gets the numeric value of this segment.</summary>
    public required decimal SegmentValue { get; init; }
}
