namespace NTComponents.Charts;

/// <summary>
///     Represents a point (scatter) series in a cartesian chart.
/// </summary>
/// <typeparam name="TData">The type of the data.</typeparam>
public class NTPointSeries<TData> : NTLineSeries<TData> where TData : class {
    /// <summary>
    ///     Initializes a new instance of the <see cref="NTPointSeries{TData}"/> class.
    /// </summary>
    public NTPointSeries() => LineStyle = NTComponents.Charts.Core.Series.LineStyle.None;

    /// <inheritdoc />
    protected override void OnParametersSet() {
        base.OnParametersSet();
        // Re-applied on every parameter pass so an explicit LineStyle can never draw a connecting line.
        LineStyle = NTComponents.Charts.Core.Series.LineStyle.None;
    }
}
