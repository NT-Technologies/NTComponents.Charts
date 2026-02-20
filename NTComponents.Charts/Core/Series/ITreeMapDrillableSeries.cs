namespace NTComponents.Charts.Core.Series;

/// <summary>
///     Exposes drill-down state for treemap series so the chart can allocate full canvas to an active drill path.
/// </summary>
internal interface ITreeMapDrillableSeries {
    bool IsInDrilldown { get; }
}

