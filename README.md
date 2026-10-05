# NTComponents.Charts

Interactive Blazor charts powered by SkiaSharp for .NET 9, 10, and 11. The library uses NTComponents 2.0.0-preview.71 and its current Material 3 icon buttons and theme runtime.

Install `NTComponents.Charts`, register NTComponents services with `builder.Services.AddNTServices()`, and include `NTHeadDependencies` in your application's head. For a WebAssembly host, include the NTComponents and Charts scoped stylesheets in `wwwroot/index.html`.

```razor
@using NTComponents.Charts
@using NTComponents.Charts.Core
@using NTComponents.Charts.Core.Axes

<div style="height: 320px">
    <NTChart TData="SalesPoint">
        <NTXAxisOptions ValueSelector="@(point => point.Month)" />
        <NTYAxisOptions ValueSelector="@(point => point.Total)" />
        <NTLineSeries Data="_sales" XValue="@(point => point.Month)" YValueSelector="@(point => point.Total)" />
    </NTChart>
</div>

@code {
    private readonly SalesPoint[] _sales = [new("January", 120m), new("February", 180m)];
    private sealed record SalesPoint(string Month, decimal Total);
}
```

Chart colors continue to use `TnTColor`, the color-role enum in the current NTComponents API. Charts refresh their palette after the theme runtime's `nt-theme-changed` event, including light/dark and contrast changes. Export and reset use named `NTIconButton` actions; `AllowExport` and series interaction flags control their visibility. Unmatched attributes such as `aria-label` and `data-*` are forwarded to the chart root.

Canvas drawings and PNG exports use embedded Roboto Bold (700), Medium (500), and Regular (400) font files, without synthetic bolding. Titles, axes, legends, and data labels use Bold; tooltip labels, annotations, and hierarchy navigation use Medium; debug text uses Regular. These drawings do not depend on installed system fonts or browser font downloads. The redistributed Roboto fonts include their SIL Open Font License in the package.

Series use `NTMotionDuration` and `NTMotionEasing` from NTComponents for drawing and visibility transitions. The defaults are `AnimationDuration="NTMotionDuration.Ms500"` and `AnimationEasing="NTMotionEasing.Emphasized"`. Chart hover effects, including pie explosions and treemap highlighting, use `HoverAnimationDuration="NTMotionDuration.Ms250"` and `HoverAnimationEasing="NTMotionEasing.Standard"`. Canvas easing matches the NTComponents web curves; `AnimationEnabled="false"` completes transitions immediately. Bubble radius growth uses the series timing; its physical movement still follows the configured physics settings.

Charts paint on demand and run continuous frames only while a transition or bubble simulation is active. Settled bubble physics sleeps until data, layout, or interactions change. Charts suspend animation frames while hidden or outside the viewport, and redraw after becoming visible, resizing, or changing display density. Axes, titles, legend measurements, and series geometry reuse cached resources between changes. After changing items inside an existing `Data` collection, call `chart.Invalidate()` to refresh cached values and redraw. Theme changes redraw the current state without restarting data animations.

The duration parameters now accept enum tokens instead of `TimeSpan`: replace `AnimationDuration="TimeSpan.FromMilliseconds(400)"` with `AnimationDuration="NTMotionDuration.Ms400"`, and migrate `HoverAnimationDuration` in the same way. Custom series should use the protected `EaseAnimation(progress)` helper in place of the former `BackEase(progress)` overshoot helper.

When checked out under the NTComponents repository, Charts automatically references the sibling NTComponents project. Use `-p:UseLocalNTComponentsProject=false` to validate the published NuGet dependency instead.

From this repository's root, run:

```powershell
npm ci
npm run build:ts
dotnet build NTComponents.Charts.slnx -c Release -m:1 -p:UseLocalNTComponentsProject=false
dotnet test --project "$PWD/Tests/NTComponents.Charts.Tests/NTComponents.Charts.Tests.csproj" -c Release --no-build -p:UseLocalNTComponentsProject=false
npm test
dotnet pack NTComponents.Charts/NTComponents.Charts.csproj -c Release -o artifacts/nuget -p:UseLocalNTComponentsProject=false
```

The chart module is authored in `NTComponents.Charts/wwwroot/ntcomponents-charts.ts` with strict TypeScript checking. Run `npm ci` once, then `npm run build:ts` before .NET build, pack, or publish so static asset discovery sees the generated module. `npm test` regenerates the module before running Node's built-in tests. Node 22 or later is required. Generated JavaScript is ignored by Git but included in the NuGet package and consuming application publishes; TypeScript source and declarations are excluded from both.

SkiaSharp's native WebAssembly assets support native linking with the .NET 10 toolchain. The .NET 11 compatibility host validates compilation and trimming only: browser rendering requires native linking, which SkiaSharp 4.153.1 cannot perform with .NET 11 RC's Emscripten 6 toolchain (`saveSetjmp`/`testSetjmp` are unresolved). .NET 11 browser support requires compatible upstream native assets.
