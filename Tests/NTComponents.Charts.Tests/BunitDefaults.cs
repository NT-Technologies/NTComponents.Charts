using System.Runtime.CompilerServices;

namespace NTComponents.Charts.Tests;

/// <summary>
///     bUnit waits default to one second, which async JS-interop work (such as theme colors resolving) can exceed when
///     the three target frameworks run in parallel on a loaded machine. A wait still returns as soon as its condition
///     holds, so this only gives slow-but-correct runs room instead of reporting them as canceled.
/// </summary>
internal static class BunitDefaults {
    [ModuleInitializer]
    internal static void Initialize() => BunitContext.DefaultWaitTimeout = TimeSpan.FromSeconds(10);
}
