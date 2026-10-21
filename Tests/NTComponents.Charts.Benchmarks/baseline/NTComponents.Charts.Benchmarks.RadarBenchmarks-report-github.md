```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9739)
Intel Core Ultra 9 285K 3.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 11.0.100-rc.2.26504.105
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Toolchain=InProcessNoEmitToolchain  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=3  

```
| Method        | Axes | Mean     | Error   | StdDev  | Allocated |
|-------------- |----- |---------:|--------:|--------:|----------:|
| **SteadyRepaint** | **8**    | **134.8 μs** | **1.49 μs** | **0.66 μs** |   **3.68 KB** |
| **SteadyRepaint** | **64**   | **336.0 μs** | **5.84 μs** | **3.05 μs** |  **24.84 KB** |
