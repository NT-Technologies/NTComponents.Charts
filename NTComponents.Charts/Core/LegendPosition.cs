namespace NTComponents.Charts.Core;

/// <summary>
///     Defines the position of the chart legend.
/// </summary>
public enum LegendPosition {
    /// <summary>The legend is not displayed.</summary>
    None,

    /// <summary>The legend is rendered above the chart.</summary>
    Top,

    /// <summary>The legend is rendered below the chart.</summary>
    Bottom,

    /// <summary>The legend is rendered to the left of the chart.</summary>
    Left,

    /// <summary>The legend is rendered to the right of the chart.</summary>
    Right,

    /// <summary>The legend floats over the chart at a configurable position.</summary>
    Floating
}
