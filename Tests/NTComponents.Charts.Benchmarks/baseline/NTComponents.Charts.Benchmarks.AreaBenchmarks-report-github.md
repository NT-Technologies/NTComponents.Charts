```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9739)
Intel Core Ultra 9 285K 3.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 11.0.100-rc.2.26504.105
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Toolchain=InProcessNoEmitToolchain  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=3  

```
| Method        | Points | Mean       | Error     | StdDev   | Allocated |
|-------------- |------- |-----------:|----------:|---------:|----------:|
| **SteadyRepaint** | **100**    |   **552.4 μs** |   **5.32 μs** |  **2.36 μs** |  **14.62 KB** |
| **SteadyRepaint** | **5000**   | **9,038.4 μs** | **112.11 μs** | **49.78 μs** |   **6.98 KB** |
