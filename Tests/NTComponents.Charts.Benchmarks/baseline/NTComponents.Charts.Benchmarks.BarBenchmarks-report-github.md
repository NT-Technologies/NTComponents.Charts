```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26220.9739)
Intel Core Ultra 9 285K 3.70GHz, 1 CPU, 24 logical and 24 physical cores
.NET SDK 11.0.100-rc.2.26504.105
  [Host] : .NET 10.0.13 (10.0.13, 10.0.1326.50504), X64 RyuJIT x86-64-v3

Toolchain=InProcessNoEmitToolchain  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=3  

```
| Method                      | Categories | Mean       | Error    | StdDev   | Gen0    | Gen1   | Allocated |
|---------------------------- |----------- |-----------:|---------:|---------:|--------:|-------:|----------:|
| **GroupedSteadyRepaint**        | **20**         |   **193.3 μs** |  **1.55 μs** |  **0.81 μs** |       **-** |      **-** |  **11.95 KB** |
| StackedSteadyRepaint        | 20         |   182.6 μs |  1.78 μs |  0.93 μs |  1.8116 |      - |  42.08 KB |
| GroupedInvalidateAndRepaint | 20         |   261.7 μs |  2.73 μs |  1.43 μs |  2.9070 |      - |  54.08 KB |
| GroupedHoverMoveAndRepaint  | 20         |   215.0 μs |  3.60 μs |  1.88 μs |       - |      - |  13.08 KB |
| StackedHoverMoveAndRepaint  | 20         |   193.5 μs |  2.64 μs |  1.38 μs |  1.9841 |      - |  43.07 KB |
| GroupedResizeRepaint        | 20         |   250.4 μs |  2.88 μs |  1.50 μs |  1.2500 |      - |  39.39 KB |
| **GroupedSteadyRepaint**        | **500**        | **1,062.0 μs** | **14.24 μs** |  **6.32 μs** |       **-** |      **-** |  **11.98 KB** |
| StackedSteadyRepaint        | 500        | 1,158.5 μs | 15.56 μs |  8.14 μs | 45.4545 |      - | 893.39 KB |
| GroupedInvalidateAndRepaint | 500        | 1,599.9 μs |  9.28 μs |  4.85 μs | 23.4375 | 7.8125 | 513.93 KB |
| GroupedHoverMoveAndRepaint  | 500        | 1,079.8 μs | 19.63 μs | 10.27 μs |       - |      - |  12.99 KB |
| StackedHoverMoveAndRepaint  | 500        | 1,183.5 μs | 16.13 μs |  8.44 μs | 45.4545 |      - | 894.38 KB |
| GroupedResizeRepaint        | 500        | 1,362.7 μs | 17.70 μs |  9.26 μs |  6.9444 |      - | 208.34 KB |
