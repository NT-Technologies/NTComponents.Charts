namespace NTComponents.Charts.Benchmarks;

/// <summary>Plain data row shared by every benchmark chart.</summary>
public sealed record BenchDatum(string Name, double X, decimal Y, decimal Y2, decimal Y3, string Group);
