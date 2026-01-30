using Microsoft.AspNetCore.Components;

namespace NTComponents.Charts.Core;

/// <summary>
///     Component for configuring chart tooltips.
/// </summary>
public class NTTooltip<TData> : ComponentBase {

    [CascadingParameter]
    protected IAxisChart Chart { get; set; } = default!;

    /// <summary>
    ///     Gets or sets whether tooltips are enabled.
    /// </summary>
    [Parameter]
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets or sets the background color of the tooltip.
    /// </summary>
    [Parameter]
    public TnTColor? BackgroundColor { get; set; }

    /// <summary>
    ///     Gets or sets the text color of the tooltip.
    /// </summary>
    [Parameter]
    public TnTColor? TextColor { get; set; }

    protected override void OnInitialized() {
        base.OnInitialized();
        if (Chart is null) {
            return;
        }
        Chart.SetTooltip(this);
    }
}
