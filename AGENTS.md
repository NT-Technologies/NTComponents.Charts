# Repository Guidelines

## Project Structure & Module Organization
This repository contains a single .NET Razor class library solution: `NTComponents.Charts.slnx`.

- `NTComponents.Charts/`: main library project (`NTComponents.Charts.csproj`), multi-targeting `net9.0`, `net10.0`, and `net11.0`.
- `NTComponents.Charts/Core/`: chart engine, rendering context, axes wiring, and shared chart primitives.
- `NTComponents.Charts/Core/Axes/`: axis interfaces and axis option types.
- `NTComponents.Charts/Core/Series/`: base series abstractions and shared series models.
- `NTComponents.Charts/Series/`: concrete chart series implementations (line, bar, pie, treemap, etc.).
- `NTComponents.Charts/wwwroot/`: static JS module assets.
- `NTComponents.Charts/Core/*.razor.scss`: component styles compiled during build via `sasscompiler.json`.
- `Tests/NTComponents.Charts.Tests/`: xUnit v3 and bUnit tests for chart behavior.

## Build, Test, and Development Commands
- `dotnet restore NTComponents.Charts.slnx`: restore NuGet packages.
- `dotnet build NTComponents.Charts.slnx -c Release`: build all target frameworks and run SCSS compilation.
- `dotnet build NTComponents.Charts/NTComponents.Charts.csproj -c Debug`: fast local iteration on the library.
- `dotnet test --project Tests/NTComponents.Charts.Tests/NTComponents.Charts.Tests.csproj -c Release`: run chart tests for net9/net10/net11.
- `dotnet pack NTComponents.Charts/NTComponents.Charts.csproj -c Release -o artifacts/nuget -p:UseLocalNTComponentsProject=false`: create the standalone NuGet package.
- `./test-package.ps1 -PackagePath artifacts/nuget/<package>.nupkg`: validate package structure and compile net9/net10/net11 consumers.
- `./test-aot-compatibility.ps1`: publish and browser-smoke the net10 native-AOT and net11 trimmed WebAssembly consumers.

CI runs the dedicated test project on net9, net10, and net11 in addition to package-consumer and WebAssembly compatibility smoke projects.

## Coding Style & Naming Conventions
- Use 4-space indentation and braces on the same line as declarations.
- Keep nullable annotations enabled and avoid introducing nullable warnings.
- Follow existing C# naming:
  - `PascalCase` for types, methods, and public members.
  - `_camelCase` for private fields.
  - Generic names like `TData` for chart data types.
- Preserve XML documentation on public APIs and component parameters.
- Keep file names aligned to primary type names (for example, `NTLineSeries.cs` for `NTLineSeries<TData>`).

## Testing Guidelines
Add chart tests to `Tests/NTComponents.Charts.Tests` and keep them organized by chart feature. Prefer scenario-based tests for:
- axis scaling and range calculations,
- hit testing and interaction behavior,
- rendering/data caching invalidation.

The reusable PR/release gate is `.github/workflows/build-template.yml`. Keep its terminal `build-test-pack` job stable because branch protection and Dependabot auto-merge use it as the required-check sentinel. Stable releases are manual; preview releases are created from successful pushes to `main`.

## Commit & Pull Request Guidelines
Use Conventional Commits consistent with existing history:
- `feat(scope): ...`
- `fix(scope): ...`
- `chore(scope): ...`
- mark breaking changes with `!` (for example, `feat(axis)!: ...`).

For PRs, include:
- a concise summary of behavior changes,
- linked issue(s) when applicable,
- screenshots/GIFs for UI or rendering changes,
- notes on performance impact for rendering-path updates.
