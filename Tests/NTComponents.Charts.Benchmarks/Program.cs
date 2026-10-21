// Chart hot-path benchmarks (CPU + allocations). Run from this folder:
//   dotnet run -c Release -- --filter "*"
// Subsets:  --filter "*LineBenchmarks*"   --filter "*Hover*"   --list flat
// Results land in BenchmarkDotNet.Artifacts/results (*-report-github.md and *-report-full.json).
// Compare two runs:  pwsh ./compare.ps1 <before-dir> <after-dir>   (see README.md).
using BenchmarkDotNet.Running;
using NTComponents.Charts.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, new ChartBenchConfig());
