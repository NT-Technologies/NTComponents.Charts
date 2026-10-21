```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9739)
Intel Core Ultra 9 285K 3.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 11.0.100-rc.2.26504.105
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Toolchain=InProcessNoEmitToolchain  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=3  

```
| Method              | Slices | Mean     | Error    | StdDev  | Allocated |
|-------------------- |------- |---------:|---------:|--------:|----------:|
| **SteadyRepaint**       | **8**      | **130.2 μs** |  **2.28 μs** | **1.19 μs** |   **4.14 KB** |
| HoverMoveAndRepaint | 8      | 159.2 μs |  2.28 μs | 1.19 μs |  12.99 KB |
| **SteadyRepaint**       | **200**    | **886.5 μs** | **11.74 μs** | **6.14 μs** |  **60.44 KB** |
| HoverMoveAndRepaint | 200    | 940.7 μs | 11.14 μs | 5.83 μs |   73.9 KB |
