using SkiaSharp;
using System.Collections.Generic;

namespace NTComponents.Charts.Core;

public class TooltipInfo
{
    public string? Header { get; set; }
    public List<TooltipLine> Lines { get; set; } = [];
}

public struct TooltipLine
{
    public string Label { get; set; }
    public string Value { get; set; }
    public SKColor Color { get; set; }
}
