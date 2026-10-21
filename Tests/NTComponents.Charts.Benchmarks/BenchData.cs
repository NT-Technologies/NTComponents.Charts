namespace NTComponents.Charts.Benchmarks;

/// <summary>Deterministic data generators so before/after runs see identical input.</summary>
public static class BenchData {
    /// <summary>Random-walk rows with numeric X, three Y channels and <paramref name="groups"/> groups.</summary>
    public static List<BenchDatum> Create(int count, int groups = 8) {
        var random = new Random(42);
        var list = new List<BenchDatum>(count);
        decimal a = 100, b = 200, c = 300;
        for (var i = 0; i < count; i++) {
            a += (decimal)(random.NextDouble() - 0.5) * 10;
            b += (decimal)(random.NextDouble() - 0.5) * 10;
            c += (decimal)(random.NextDouble() - 0.5) * 10;
            list.Add(new BenchDatum($"Item {i}", i, Math.Abs(a) + 1, Math.Abs(b) + 1, Math.Abs(c) + 1, $"Group {i % groups}"));
        }
        return list;
    }
}
