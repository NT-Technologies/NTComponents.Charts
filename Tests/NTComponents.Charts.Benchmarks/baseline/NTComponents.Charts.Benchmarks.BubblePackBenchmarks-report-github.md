```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9739)
Intel Core Ultra 9 285K 3.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 11.0.100-rc.2.26504.105
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Toolchain=InProcessNoEmitToolchain  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=3  

```
| Method               | Items | Mean     | Error   | StdDev  | Gen0   | Allocated |
|--------------------- |------ |---------:|--------:|--------:|-------:|----------:|
| **SteadyRepaint**        | **100**   | **177.5 μs** | **3.37 μs** | **1.20 μs** |      **-** |   **4.97 KB** |
| InvalidateAndRepaint | 100   | 201.7 μs | 2.30 μs | 1.20 μs | 1.0081 |  21.11 KB |
| HoverMoveAndRepaint  | 100   | 226.0 μs | 3.91 μs | 2.04 μs |      - |  18.78 KB |
| **SteadyRepaint**        | **1000**  | **178.6 μs** | **3.75 μs** | **1.96 μs** |      **-** |   **4.97 KB** |
| InvalidateAndRepaint | 1000  | 237.7 μs | 4.84 μs | 2.53 μs | 2.3585 |  60.42 KB |
| HoverMoveAndRepaint  | 1000  | 230.8 μs | 7.34 μs | 3.84 μs |      - |  18.68 KB |
