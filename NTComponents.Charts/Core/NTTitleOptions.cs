using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTComponents.Charts.Core;

/// <summary>
///     Configures the title displayed above a chart.
/// </summary>
public class NTTitleOptions {
    /// <summary>Gets or sets the title text.</summary>
    public string Title { get; set; }

    /// <summary>Gets or sets an optional override for the title text color. When <see langword="null"/> the chart theme color is used.</summary>
    public NTColor? TextColor { get; set; }

    /// <summary>Gets or sets the font size of the title in points. Defaults to <c>20</c>.</summary>
    public float FontSize { get; set; } = 20f;

    /// <summary>
    ///     Initializes a new <see cref="NTTitleOptions"/> with the specified title text.
    /// </summary>
    /// <param name="title">The title text to display. Must not be null or whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="title"/> is null or whitespace.</exception>
    public NTTitleOptions(string title) {
        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));
        Title = title;
    }
}
