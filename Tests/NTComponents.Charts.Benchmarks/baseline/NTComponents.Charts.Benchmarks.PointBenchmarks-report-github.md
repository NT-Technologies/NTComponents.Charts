```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9739)
Intel Core Ultra 9 285K 3.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 11.0.100-rc.2.26504.105
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Toolchain=InProcessNoEmitToolchain  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=3  

```
| Method              | Points | Mean     | Error   | StdDev  | Allocated |
|-------------------- |------- |---------:|--------:|--------:|----------:|
| **SteadyRepaint**       | **100**    | **219.6 μs** | **3.24 μs** | **1.70 μs** |   **8.32 KB** |
| HoverMoveAndRepaint | 100    | 227.4 μs | 9.15 μs | 4.06 μs |  11.02 KB |
| **SteadyRepaint**       | **5000**   | **107.9 μs** | **1.31 μs** | **0.69 μs** |    **4.4 KB** |
| HoverMoveAndRepaint | 5000   | 146.7 μs | 2.70 μs | 1.41 μs |   7.42 KB |
