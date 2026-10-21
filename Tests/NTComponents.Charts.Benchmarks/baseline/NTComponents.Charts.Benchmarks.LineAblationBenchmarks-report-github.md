```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9739)
Intel Core Ultra 9 285K 3.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 11.0.100-rc.2.26504.105
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Toolchain=InProcessNoEmitToolchain  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=3  

```
| Method                           | Mean     | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------------------- |---------:|---------:|---------:|------:|--------:|----------:|------------:|
| Full                             | 724.9 μs | 41.96 μs | 21.95 μs |  1.00 |    0.04 |  37.79 KB |        1.00 |
| NoLegendNoTitle                  | 722.6 μs | 26.84 μs | 11.92 μs |  1.00 |    0.03 |   37.2 KB |        0.98 |
| NoMarkers                        | 603.8 μs | 77.95 μs | 34.61 μs |  0.83 |    0.05 |   9.68 KB |        0.26 |
| NoLegendNoTitleNoMarkers         | 549.7 μs | 19.16 μs |  6.83 μs |  0.76 |    0.02 |   9.06 KB |        0.24 |
| NoLegendNoTitleNoMarkersNoStroke | 146.7 μs |  3.51 μs |  1.25 μs |  0.20 |    0.01 |   9.05 KB |        0.24 |
