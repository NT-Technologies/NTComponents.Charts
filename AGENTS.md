# Repository Guidelines

## Project Structure & Module Organization
This repository contains a single .NET Razor class library solution: `NTComponents.Charts.slnx`.

- `NTComponents.Charts/`: main library project (`NTComponents.Charts.csproj`), multi-targeting `net9.0` and `net10.0`.
- `NTComponents.Charts/Core/`: chart engine, rendering context, axes wiring, and shared chart primitives.
- `NTComponents.Charts/Core/Axes/`: axis interfaces and axis option types.
- `NTComponents.Charts/Core/Series/`: base series abstractions and shared series models.
- `NTComponents.Charts/Series/`: concrete chart series implementations (line, bar, pie, treemap, etc.).
- `NTComponents.Charts/wwwroot/`: static JS module assets.
- `NTComponents.Charts/Core/*.razor.scss`: component styles compiled during build via `sasscompiler.json`.

## Build, Test, and Development Commands
- `dotnet restore NTComponents.Charts.slnx`: restore NuGet packages.
- `dotnet build NTComponents.Charts.slnx -c Release`: build both target frameworks and run SCSS compilation.
- `dotnet build NTComponents.Charts/NTComponents.Charts.csproj -c Debug`: fast local iteration on the library.

Current state: there is no test project in this repo yet. `dotnet test` expects a test project/solution and is not a validation path for the library alone.

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
When adding tests, create a dedicated test project (for example, `NTComponents.Charts.Tests`) and include it in `NTComponents.Charts.slnx`. Prefer scenario-based tests for:
- axis scaling and range calculations,
- hit testing and interaction behavior,
- rendering/data caching invalidation.

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
