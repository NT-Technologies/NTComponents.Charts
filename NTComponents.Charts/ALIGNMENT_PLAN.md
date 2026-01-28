# Architecture Alignment Plan

This document outlines the phased strategy for bringing the `NTComponents.Charts` project into full alignment with its defined [ARCHITECTURE.md](ARCHITECTURE.md).

## Phase 1: Interaction & Type Refactoring
*   **Standardize Interaction Flags**:
    *   Create a `ChartInteractions` enum with `[Flags]` (None, XPan, YPan, XZoom, YZoom).
    *   Define convenience values like `PanAll`, `ZoomAll`, and `All`.
    *   Replace individual boolean flags (`EnableXPan`, etc.) in series with this unified property.
*   **Decimal Consistency**:
    *   Audit all Cartesian series to ensure Y-axis values and calculations strictly use `decimal` for consistent precision.

## Phase 2: Decentralizing Interaction Logic
*   **Relocate Viewport State**:
    *   Move the current view ranges (min/max) from `NTChart` into the series components.
*   **Event Delegation**:
    *   Refactor `NTChart` to simply capture and dispatch raw events (mouse, touch, wheel) to child series.
    *   Move the coordinate transformation mathematics (the "logic" of panning and zooming) into the `Series` classes.
*   **Layout Stability**:
    *   Verify that `NTChart` render area stays constant while only the data-to-screen mapping in the series transformations change.

## Phase 3: Validation & Performance Optimization
*   **Eager Validation**:
    *   Move compatibility checks from the render loop (`OnPaintSurface`) to the series registration method (`AddSeries`).
    *   Enforce "Strict Validation" by throwing exceptions immediately if incompatible series (e.g., mixing Radial and Cartesian) are added.
*   **Hot Path Optimization**:
    *   Remove `ValidateState` from the rendering pipeline once registration-time validation is reliable.

## Phase 4: Completing Axis & Layout Responsibilities
*   **Grid Line Support**:
    *   Implement `ShowGridLines` and related style properties in `NTAxisOptions`.
    *   Update axes to render grid lines spanning the entire plot area.
*   **Layout Encapsulation**:
    *   Refactor the measurement flow so `NTChart` provides the "Layout Foundation" and the series explicitly partitions that space for its own axes.
*   **Invisible Axes**:
    *   Ensure derivations for non-linear charts (Pie, TreeMap) can participate in calculations without rendering unnecessary axis lines.

## Phase 5: Verification & Cleanup
*   **Remove Unnecessary Abstractions**:
    *   Delete `IChart.cs` and remove all references to `IChart` and `IAxisChart`.
    *   Refactor components to interact directly with concrete implementations.
*   **Testing**:
    *   Add unit tests to ensure panning/zooming logic works correctly within the series lifecycle.
    *   Update documentation examples in `NTComponents.Site`.
