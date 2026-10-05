using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NTComponents.Charts.Core;

#pragma warning disable BL0016 // These deterministic JS runtime fakes perform no browser interop.

namespace NTComponents.Charts.Tests.Charts;

public class ChartInteropDisposal_Tests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposal_waits_for_initialization_and_releases_late_listener_handles(bool delayImport) {
        var runtime = new PendingRuntime(delayImport);
        var type = typeof(NTChart<string>).Assembly.GetType("NTComponents.Charts.Core.NTChartInterop")!;
        var interop = Activator.CreateInstance(type, [runtime])!;
        using var chart = new NTChart<string>();
        using var reference = DotNetObjectReference.Create(chart);
        var initialization = ((ValueTask<float>)type.GetMethod("InitializeAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(typeof(string)).Invoke(interop, [default(ElementReference), reference, true])!).AsTask();

        var disposal = ((IAsyncDisposable)interop).DisposeAsync().AsTask();
        disposal.IsCompleted.Should().BeFalse("initialization still owns an unresolved JavaScript handle");
        runtime.Complete();
        await initialization;
        await disposal;

        runtime.Listeners.ListenerDisposals.Should().Be(1);
        runtime.Listeners.ReferenceDisposals.Should().Be(1);
        runtime.Module.ReferenceDisposals.Should().Be(1);
        await ((IAsyncDisposable)interop).DisposeAsync();
        runtime.Listeners.ListenerDisposals.Should().Be(1);
    }

    private sealed class PendingRuntime : IJSRuntime {
        private readonly TaskCompletionSource<IJSObjectReference> _import = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public RecordingReference Listeners { get; } = new();
        public RecordingReference Module { get; }

        public PendingRuntime(bool delayImport) {
            Module = new RecordingReference { PendingListeners = new(TaskCreationOptions.RunContinuationsAsynchronously) };
            if (!delayImport) {
                _import.SetResult(Module);
            }
        }

        public void Complete() {
            _import.TrySetResult(Module);
            Module.PendingListeners!.SetResult(Listeners);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => (TValue)await _import.Task;
    }

    private sealed class RecordingReference : IJSObjectReference {
        public TaskCompletionSource<IJSObjectReference>? PendingListeners { get; init; }
        public int ListenerDisposals { get; private set; }
        public int ReferenceDisposals { get; private set; }

        public ValueTask DisposeAsync() {
            ReferenceDisposals++;
            return ValueTask.CompletedTask;
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
            if (identifier == "initializeChart") {
                return (TValue)await PendingListeners!.Task;
            }
            if (identifier == "getDevicePixelRatio") {
                return (TValue)(object)1f;
            }
            if (identifier == "dispose") {
                ListenerDisposals++;
            }
            return default!;
        }
    }
}
