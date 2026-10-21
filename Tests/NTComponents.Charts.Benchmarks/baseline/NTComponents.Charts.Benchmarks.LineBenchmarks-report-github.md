```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9739)
Intel Core Ultra 9 285K 3.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 11.0.100-rc.2.26504.105
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Toolchain=InProcessNoEmitToolchain  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=3  

```
| Method               | Points | Mean        | Error       | StdDev      | Gen0      | Gen1      | Gen2      | Allocated   |
|--------------------- |------- |------------:|------------:|------------:|----------:|----------:|----------:|------------:|
| **SteadyRepaint**        | **100**    |    **728.3 μs** |    **14.67 μs** |     **7.67 μs** |         **-** |         **-** |         **-** |    **37.83 KB** |
| InvalidateAndRepaint | 100    |    931.3 μs |    13.98 μs |     7.31 μs |    4.8077 |         - |         - |   174.08 KB |
| HoverMoveAndRepaint  | 100    |    824.7 μs |    11.29 μs |     5.91 μs |         - |         - |         - |    74.69 KB |
| ResizeRepaint        | 100    |    890.8 μs |    18.23 μs |     8.09 μs |    4.4643 |         - |         - |    95.19 KB |
| **SteadyRepaint**        | **10000**  | **23,001.5 μs** | **1,732.56 μs** |   **906.16 μs** |         **-** |         **-** |         **-** |    **10.79 KB** |
| InvalidateAndRepaint | 10000  | 34,973.2 μs | 1,576.26 μs |   699.87 μs | 1166.6667 | 1166.6667 | 1166.6667 | 10609.83 KB |
| HoverMoveAndRepaint  | 10000  | 24,952.2 μs | 2,062.95 μs | 1,078.96 μs |         - |         - |         - |    29.86 KB |
| ResizeRepaint        | 10000  | 26,658.3 μs |   850.62 μs |   444.89 μs |  250.0000 |  250.0000 |  250.0000 |  2147.81 KB |
