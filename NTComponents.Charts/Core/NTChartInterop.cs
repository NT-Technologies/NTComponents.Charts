using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace NTComponents.Charts.Core;

internal sealed class NTChartInterop(IJSRuntime _jsRuntime) : IAsyncDisposable {
    internal const string ModulePath = "./_content/NTComponents.Charts/ntcomponents-charts.js";
    private IJSObjectReference? _module;
    private IJSObjectReference? _listeners;
    private bool _preventDefault;
    private Task<float>? _initialization;
    private bool _disposed;

    internal ValueTask<float> InitializeAsync<TData>(ElementReference element, DotNetObjectReference<NTChart<TData>> chart, bool preventDefault) where TData : class =>
        _disposed ? ValueTask.FromResult(1f) : new(_initialization ??= InitializeCoreAsync(element, chart, preventDefault));

    private async Task<float> InitializeCoreAsync<TData>(ElementReference element, DotNetObjectReference<NTChart<TData>> chart, bool preventDefault) where TData : class {
        try {
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
            _listeners = await _module.InvokeAsync<IJSObjectReference>("initializeChart", element, chart, preventDefault);
            _preventDefault = preventDefault;
            return await _module.InvokeAsync<float>("getDevicePixelRatio");
        }
        catch (JSDisconnectedException) {
            return 1f;
        }
    }

    internal async ValueTask UpdateInteractionsAsync(bool preventDefault) {
        try {
            if (_listeners is not null && _preventDefault != preventDefault) {
                await _listeners.InvokeVoidAsync("updateInteractions", preventDefault);
                _preventDefault = preventDefault;
            }
        }
        catch (JSDisconnectedException) {
            // The circuit has already released its listeners.
        }
    }

    internal async ValueTask<Dictionary<string, string?>> GetThemeColorsAsync(string[] colorNames) {
        try {
            return _module is null ? [] : await _module.InvokeAsync<Dictionary<string, string?>>("getThemeColors", (object)colorNames);
        }
        catch (JSDisconnectedException) {
            return [];
        }
    }

    public async ValueTask DisposeAsync() {
        if (_disposed) {
            return;
        }
        _disposed = true;
        try {
            try {
                if (_initialization is not null) {
                    await _initialization;
                }
            }
            finally {
                try {
                    if (_listeners is not null) {
                        await _listeners.InvokeVoidAsync("dispose");
                        await _listeners.DisposeAsync();
                    }
                }
                finally {
                    if (_module is not null) {
                        await _module.DisposeAsync();
                    }
                    _listeners = null;
                    _module = null;
                }
            }
        }
        catch (JSDisconnectedException) {
            // The circuit has already released its JavaScript references.
        }
    }
}
