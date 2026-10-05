import type { ChartDotNetReference } from './ChartDotNetReference.js';

const ON_THEME_CHANGED = 'OnThemeChanged';
const ON_NATIVE_WHEEL = 'OnNativeWheel';

export function getDevicePixelRatio(): number {
    return window.devicePixelRatio || 1;
}

export function getThemeColors(colorNames: readonly string[]): Record<string, string | null | undefined> {
    return Object.fromEntries(colorNames.map(name => [
        name,
        window.NTComponents.getColorValueFromEnumName(name)
    ]));
}

export function initializeChart(element: HTMLElement | null, dotNetHelper: ChartDotNetReference, preventDefault: boolean): { dispose(): void; updateInteractions(preventDefault: boolean): void } {
    const controller = new AbortController();
    let disposed = false;
    let moveFrame = 0;
    let moveInFlight = false;
    let pendingMove: MouseEvent | null = null;
    let intersecting = true;
    let size = '';
    let lastViewport = '';

    const notifyViewport = async (): Promise<void> => {
        const visible = intersecting && document.visibilityState !== 'hidden';
        const density = getDevicePixelRatio();
        const viewport = `${visible}:${density}:${size}`;
        if (disposed || viewport === lastViewport) {
            return;
        }
        lastViewport = viewport;
        try {
            await dotNetHelper.invokeMethodAsync('OnNativeViewportChanged', visible, density);
        } catch {
            // The chart has been disposed.
        }
    };

    const sendMove = async (): Promise<void> => {
        if (disposed || moveInFlight || pendingMove === null) {
            return;
        }
        const event = pendingMove;
        pendingMove = null;
        moveInFlight = true;
        try {
            await dotNetHelper.invokeMethodAsync('OnNativeMouseMove', {
                offsetX: event.offsetX, offsetY: event.offsetY,
                clientX: event.clientX, clientY: event.clientY,
                button: event.button, buttons: event.buttons,
                ctrlKey: event.ctrlKey, shiftKey: event.shiftKey,
                altKey: event.altKey, metaKey: event.metaKey
            });
        } catch {
            // The chart has been disposed.
        } finally {
            moveInFlight = false;
            if (pendingMove !== null && !disposed) {
                scheduleMove();
            }
        }
    };

    const scheduleMove = (): void => {
        if (moveFrame === 0) {
            moveFrame = window.requestAnimationFrame(() => {
                moveFrame = 0;
                void sendMove();
            });
        }
    };

    const flushMove = (): void => {
        if (moveFrame !== 0) {
            window.cancelAnimationFrame(moveFrame);
            moveFrame = 0;
        }
        void sendMove();
    };

    element?.addEventListener('mousemove', event => {
        pendingMove = event;
        scheduleMove();
    }, { signal: controller.signal });
    element?.addEventListener('mousedown', flushMove, { capture: true, signal: controller.signal });
    element?.addEventListener('mouseup', flushMove, { capture: true, signal: controller.signal });
    element?.addEventListener('click', flushMove, { capture: true, signal: controller.signal });
    element?.addEventListener('mouseleave', () => {
        pendingMove = null;
        if (moveFrame !== 0) {
            window.cancelAnimationFrame(moveFrame);
            moveFrame = 0;
        }
    }, { signal: controller.signal });

    const intersection = typeof IntersectionObserver === 'undefined' ? null : new IntersectionObserver(entries => {
        intersecting = entries[0]?.isIntersecting ?? false;
        void notifyViewport();
    });
    const resize = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(entries => {
        const rect = entries[0]?.contentRect;
        size = `${rect?.width ?? 0}:${rect?.height ?? 0}`;
        void notifyViewport();
    });
    if (element) {
        intersection?.observe(element);
        resize?.observe(element);
    }
    document.addEventListener('visibilitychange', () => { void notifyViewport(); }, { signal: controller.signal });

    let densityQuery: MediaQueryList | null = null;
    const watchDensity = (): void => {
        densityQuery?.removeEventListener('change', onDensityChange);
        densityQuery = window.matchMedia?.(`(resolution: ${getDevicePixelRatio()}dppx)`) ?? null;
        densityQuery?.addEventListener('change', onDensityChange);
    };
    const onDensityChange = (): void => {
        watchDensity();
        void notifyViewport();
    };
    watchDensity();
    document.addEventListener('nt-theme-changed', async () => {
        try {
            await dotNetHelper.invokeMethodAsync(ON_THEME_CHANGED);
        } catch {
            // The chart or its Blazor circuit has been disposed.
        }
    }, { signal: controller.signal });

    element?.addEventListener('wheel', async event => {
        flushMove();
        if (preventDefault) {
            event.preventDefault();
        }
        try {
            await dotNetHelper.invokeMethodAsync(ON_NATIVE_WHEEL, {
                offsetX: event.offsetX,
                offsetY: event.offsetY,
                deltaX: event.deltaX,
                deltaY: event.deltaY,
                ctrlKey: event.ctrlKey,
                shiftKey: event.shiftKey,
                altKey: event.altKey,
                metaKey: event.metaKey
            });
        } catch {
            // The chart or its Blazor circuit has been disposed.
        }
    }, { passive: false, signal: controller.signal });

    return {
        updateInteractions: value => { preventDefault = value; },
        dispose: () => {
            disposed = true;
            controller.abort();
            intersection?.disconnect();
            resize?.disconnect();
            densityQuery?.removeEventListener('change', onDensityChange);
            if (moveFrame !== 0) {
                window.cancelAnimationFrame(moveFrame);
            }
            pendingMove = null;
        }
    };
}
