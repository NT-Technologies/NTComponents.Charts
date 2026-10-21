# NTComponents.Charts.Benchmarks

BenchmarkDotNet harness for chart hot paths (CPU + allocations). Each benchmark drives `NTChart<T>.OnPaintSurface`
(and `OnMouseMove`) headless on a 1200x600 Skia raster surface via bUnit with the Skia views stubbed.
Delegates to protected members are created once in setup. The in-process toolchain is used, so a run never builds child projects.

## Run (from this folder)

    dotnet run -c Release -- --filter "*"

Isolated build output (recommended when other builds run concurrently):

    dotnet run -c Release --artifacts-path <dir> -- --filter "*"

Subsets: `--filter "*LineBenchmarks*"`, `--filter "*Hover*"`, `--list flat`.
Results: `BenchmarkDotNet.Artifacts/results/*-report-github.md` and `*-report-full.json`.
Baseline (pre-optimization) results live in `baseline/`.

## Compare before/after

    pwsh ./compare.ps1 <baseline-results-dir> <new-results-dir>

Prints per-benchmark Mean and Allocated deltas from the `*-report-full.json` files.

## Notes

- Heat map and box plot are covered by `HeatMapBenchmarks` and `BoxPlotBenchmarks`.
- Animations are disabled on series; Pie/TreeMap/BubblePack are settled before measuring.
- Benchmarked cartesian charts render a legend and a title, as most real charts do.
