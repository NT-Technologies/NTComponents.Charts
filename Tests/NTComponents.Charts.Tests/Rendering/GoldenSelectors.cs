namespace NTComponents.Charts.Tests.Rendering;

/// <summary>Reusable selectors over <see cref="GoldenRow"/>.</summary>
public static class GoldenSelectors {
    public static readonly Func<GoldenRow, object> Category = r => r.Category;
    public static readonly Func<GoldenRow, object> Date = r => r.Date;
    public static readonly Func<GoldenRow, object> X = r => r.X;
    public static readonly Func<GoldenRow, decimal> A = r => r.A;
    public static readonly Func<GoldenRow, decimal> B = r => r.B;
    public static readonly Func<GoldenRow, decimal> C = r => r.C;
    public static readonly Func<GoldenRow, decimal> Weight = r => r.Weight;
    private static NTColor GroupColor(string key) => key switch { "Fruit" => NTColor.Primary, "Veg" => NTColor.Tertiary, "Grain" => NTColor.Secondary, _ => NTColor.Primary };

    /// <summary>Explicit group colors; the library derives them from a per-process randomized hash by default.</summary>
    public static readonly Func<TreeMapColorContext<GoldenRow>, NTColor> TreeMapColor = c => GroupColor(c.Key);

    /// <summary>Explicit group colors for bubble pack.</summary>
    public static readonly Func<BubbleColorContext<GoldenRow>, NTColor> BubbleColor = c => GroupColor(c.Key);
}
