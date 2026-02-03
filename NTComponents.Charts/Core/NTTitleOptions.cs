using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTComponents.Charts.Core;

public class NTTitleOptions {
    public string Title { get; set; }
    public TnTColor? TextColor { get; set; }
    public float FontSize { get; set; } = 20f;

    public NTTitleOptions(string title) {
        ArgumentException.ThrowIfNullOrWhiteSpace(title, nameof(title));
        Title = title;
    }
}
