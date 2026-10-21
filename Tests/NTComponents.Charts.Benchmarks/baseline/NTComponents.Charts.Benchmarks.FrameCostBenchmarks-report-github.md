```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9739)
Intel Core Ultra 9 285K 3.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 11.0.100-rc.2.26504.105
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Toolchain=InProcessNoEmitToolchain  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=3  

```
| Method          | Mean        | Error       | StdDev      | Gen0   | Allocated |
|---------------- |------------:|------------:|------------:|-------:|----------:|
| RasterClearOnly | 32,337.3 ns | 2,063.31 ns | 1,079.15 ns |      - |         - |
| GetXRange       |    270.9 ns |    14.83 ns |     7.76 ns | 0.0445 |     840 B |
| GetYRange       |    537.0 ns |    18.19 ns |     9.51 ns | 0.0652 |    1272 B |
