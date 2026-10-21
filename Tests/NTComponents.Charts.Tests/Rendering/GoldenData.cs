using NTComponents.Charts.Core.Series;

namespace NTComponents.Charts.Tests.Rendering;

/// <summary>Literal data sets for golden scenarios. Nothing here is random or clock-dependent.</summary>
public static class GoldenData {
    private static readonly string[] _months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug"];
    private static readonly decimal[] _a = [12, 18, 9, 22, 17, 25, 14, 20];
    private static readonly decimal[] _b = [8, 11, 15, 10, 19, 13, 21, 16];
    private static readonly decimal[] _c = [5, 7, 6, 9, 8, 12, 10, 11];
    private static readonly decimal[] _negatives = [10, -6, 14, -9, 7, -12, 5, -3];
    private static readonly decimal[] _logs = [1, 10, 30, 120, 800, 5000, 20000, 90000];
    private static readonly decimal[] _secondary = [1200, 900, 1500, 1100, 1800, 1600, 2100, 1900];

    /// <summary>Eight categorical months with three positive series (A, B, C).</summary>
    // Cached so every chart re-render passes the same Data reference; a new array resets series caches (and bubble physics).
    private static GoldenRow[]? _cacheMonthly;
    public static GoldenRow[] Monthly() => _cacheMonthly ??= Build(i => _a[i], i => _b[i], i => _c[i]);

    /// <summary>Eight categorical months where A has mixed positive and negative values.</summary>
    // Cached so every chart re-render passes the same Data reference; a new array resets series caches (and bubble physics).
    private static GoldenRow[]? _cacheNegatives;
    public static GoldenRow[] Negatives() => _cacheNegatives ??= Build(i => _negatives[i], i => _b[i], i => _c[i]);

    /// <summary>Eight categorical months where A spans five orders of magnitude.</summary>
    // Cached so every chart re-render passes the same Data reference; a new array resets series caches (and bubble physics).
    private static GoldenRow[]? _cacheLogarithmic;
    public static GoldenRow[] Logarithmic() => _cacheLogarithmic ??= Build(i => _logs[i], i => _logs[i] / 2, i => _c[i]);

    /// <summary>Eight categorical months where B is on a different magnitude (for a secondary axis).</summary>
    // Cached so every chart re-render passes the same Data reference; a new array resets series caches (and bubble physics).
    private static GoldenRow[]? _cacheWithSecondary;
    public static GoldenRow[] WithSecondary() => _cacheWithSecondary ??= Build(i => _a[i], i => _secondary[i], i => _c[i]);

    /// <summary>Twelve month-start dates in 2024 with A, B and C values.</summary>
    // Cached so every chart re-render passes the same Data reference; a new array resets series caches (and bubble physics).
    private static GoldenRow[]? _cacheDated;
    public static GoldenRow[] Dated() => _cacheDated ??= Enumerable.Range(0, 12).Select(i => Row(
        _months[i % 8], new DateTime(2024, 1 + i, 1), i, _a[i % 8] + i, _b[(i + 3) % 8], _c[(i + 5) % 8])).ToArray();

    /// <summary>Twelve numeric X positions with scattered Y values.</summary>
    // Cached so every chart re-render passes the same Data reference; a new array resets series caches (and bubble physics).
    private static GoldenRow[]? _cacheScatter;
    public static GoldenRow[] Scatter() => _cacheScatter ??= Enumerable.Range(0, 12).Select(i => Row(
        _months[i % 8], new DateTime(2024, 1, 1), 1.5 + (i * 0.75), _a[(i * 3) % 8], _b[(i * 5) % 8], _c[i % 8])).ToArray();

    /// <summary>Five categories with literal five-number summaries and an outlier.</summary>
    // Cached so every chart re-render passes the same Data reference; a new array resets series caches (and bubble physics).
    private static GoldenRow[]? _cacheBoxPlots;
    public static GoldenRow[] BoxPlots() => _cacheBoxPlots ??= Enumerable.Range(0, 5).Select(i => Row(
        _months[i], new DateTime(2024, 1, 1), i, 0, 0, 0, box: new BoxPlotValues(2 + i, 6 + i, 10 + i, 14 + i, 20 + i, i % 2 == 0 ? [26 + i] : null))).ToArray();

    /// <summary>A 5x4 grid of weights: X is the column label, A is the row value.</summary>
    // Cached so every chart re-render passes the same Data reference; a new array resets series caches (and bubble physics).
    private static GoldenRow[]? _cacheHeat;
    public static GoldenRow[] HeatCells() => _cacheHeat ??= BuildHeatCells();

    private static GoldenRow[] BuildHeatCells() {
        var rows = new List<GoldenRow>();
        for (var x = 0; x < 5; x++) {
            for (var y = 0; y < 4; y++) {
                rows.Add(Row(_months[x], new DateTime(2024, 1, 1), x, y + 1, 0, 0, weight: ((x + 1) * (y + 2)) % 11 + 1));
            }
        }
        return [.. rows];
    }

    /// <summary>Four pie slices.</summary>
    // Cached so every chart re-render passes the same Data reference; a new array resets series caches (and bubble physics).
    private static GoldenRow[]? _cacheSlices;
    public static GoldenRow[] Slices() => _cacheSlices ??= Build(i => i switch { 0 => 40m, 1 => 25m, 2 => 20m, _ => 15m }, _ => 0, _ => 0)[..4];

    /// <summary>Six radar spokes with three series.</summary>
    // Cached so every chart re-render passes the same Data reference; a new array resets series caches (and bubble physics).
    private static GoldenRow[]? _cacheSpokes;
    public static GoldenRow[] Spokes() => _cacheSpokes ??= Build(i => _a[i], i => _b[i], i => _c[i])[..6];

    /// <summary>Hierarchical leaves used by tree map and bubble pack scenarios (two groups).</summary>
    // Cached so every chart re-render passes the same Data reference; a new array resets series caches (and bubble physics).
    private static GoldenRow[]? _cacheHierarchy;
    public static GoldenRow[] Hierarchy() => _cacheHierarchy ??= [
        Leaf("Fruit", "Apple", 40), Leaf("Fruit", "Pear", 22), Leaf("Fruit", "Plum", 14), Leaf("Fruit", "Fig", 6),
        Leaf("Veg", "Kale", 30), Leaf("Veg", "Leek", 18), Leaf("Veg", "Beet", 11), Leaf("Veg", "Okra", 5),
        Leaf("Grain", "Rye", 26), Leaf("Grain", "Oat", 9)
    ];

    private static GoldenRow[] Build(Func<int, decimal> a, Func<int, decimal> b, Func<int, decimal> c) => Enumerable.Range(0, 8).Select(i => Row(
        _months[i], new DateTime(2024, 1 + i, 1), i, a(i), b(i), c(i), leaf: _months[i])).ToArray();

    private static GoldenRow Leaf(string group, string leaf, decimal weight) => Row(leaf, new DateTime(2024, 1, 1), 0, weight, 0, 0, group, leaf, weight);

    private static GoldenRow Row(string category, DateTime date, double x, decimal a, decimal b, decimal c, string group = "", string leaf = "", decimal weight = 0, BoxPlotValues? box = null)
        => new(category, date, x, a, b, c, group, leaf, weight, box ?? new BoxPlotValues(0, 0, 0, 0, 0));
}
