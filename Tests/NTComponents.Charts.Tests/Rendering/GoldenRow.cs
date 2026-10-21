using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Tests.Rendering;

/// <summary>Fixed, fully explicit data row shared by every golden-image scenario.</summary>
public sealed record GoldenRow(string Category, DateTime Date, double X, decimal A, decimal B, decimal C, string Group, string Leaf, decimal Weight, BoxPlotValues Box);
