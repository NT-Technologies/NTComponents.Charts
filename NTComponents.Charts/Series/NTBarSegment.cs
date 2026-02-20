namespace NTComponents.Charts;

/// <summary>
///     Represents a single segment inside a bar data point.
/// </summary>
public sealed class NTBarSegment {
    public required decimal Value { get; init; }
    public string? Label { get; init; }
    public TnTColor? Color { get; init; }
}

/// <summary>
///     Context used by <see cref="NTBarSeries{TData}.SegmentColorSelector"/> to choose a segment color.
/// </summary>
/// <typeparam name="TData">The bar data type.</typeparam>
public sealed class NTBarSegmentColorContext<TData> where TData : class {
    public required TData Data { get; init; }
    public required int DataIndex { get; init; }
    public required int SegmentIndex { get; init; }
    public required string? SegmentLabel { get; init; }
    public required decimal SegmentValue { get; init; }
}
