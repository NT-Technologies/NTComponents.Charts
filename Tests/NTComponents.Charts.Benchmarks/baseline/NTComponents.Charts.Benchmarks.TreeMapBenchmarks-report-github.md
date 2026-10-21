```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9739)
Intel Core Ultra 9 285K 3.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 11.0.100-rc.2.26504.105
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Toolchain=InProcessNoEmitToolchain  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=3  

```
| Method               | Items | Mean     | Error    | StdDev  | Gen0   | Allocated |
|--------------------- |------ |---------:|---------:|--------:|-------:|----------:|
| **SteadyRepaint**        | **100**   | **153.7 μs** |  **1.95 μs** | **1.02 μs** |      **-** |   **6.41 KB** |
| InvalidateAndRepaint | 100   | 179.1 μs |  1.53 μs | 0.80 μs | 0.8803 |  21.63 KB |
| HoverMoveAndRepaint  | 100   | 163.5 μs |  8.82 μs | 3.92 μs |      - |   7.24 KB |
| **SteadyRepaint**        | **2000**  | **165.4 μs** |  **6.81 μs** | **3.56 μs** |      **-** |   **6.49 KB** |
| InvalidateAndRepaint | 2000  | 290.3 μs | 18.04 μs | 9.43 μs | 5.6818 | 104.68 KB |
| HoverMoveAndRepaint  | 2000  | 161.4 μs |  2.58 μs | 1.14 μs |      - |   7.33 KB |
