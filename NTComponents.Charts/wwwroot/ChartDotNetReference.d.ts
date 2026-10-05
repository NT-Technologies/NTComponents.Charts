export interface ChartDotNetReference {
    invokeMethodAsync(method: 'OnThemeChanged'): Promise<void>;
    invokeMethodAsync(method: 'OnNativeMouseMove', event: Pick<MouseEvent, 'offsetX' | 'offsetY' | 'clientX' | 'clientY' | 'button' | 'buttons' | 'ctrlKey' | 'shiftKey' | 'altKey' | 'metaKey'>): Promise<void>;
    invokeMethodAsync(method: 'OnNativeViewportChanged', visible: boolean, density: number): Promise<void>;
    invokeMethodAsync(method: 'OnNativeWheel', event: Pick<WheelEvent, 'offsetX' | 'offsetY' | 'deltaX' | 'deltaY' | 'ctrlKey' | 'shiftKey' | 'altKey' | 'metaKey'>): Promise<void>;
}
