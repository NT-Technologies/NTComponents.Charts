using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace NTComponents.Charts.Benchmarks;

/// <summary>
///     Shared config: in-process toolchain (no per-benchmark child build, so it never touches other agents' obj folders),
///     memory diagnoser, GitHub markdown plus full JSON output for before/after comparison.
/// </summary>
public sealed class ChartBenchConfig : ManualConfig {
    public ChartBenchConfig() {
        Add(DefaultConfig.Instance);
        AddJob(Job.Default
            .WithToolchain(InProcessNoEmitToolchain.Instance)
            .WithLaunchCount(1)
            .WithWarmupCount(3)
            .WithIterationCount(8)
            .WithIterationTime(Perfolizer.Horology.TimeInterval.FromMilliseconds(200)));
        AddDiagnoser(MemoryDiagnoser.Default);
        AddExporter(JsonExporter.Full);
    }
}
