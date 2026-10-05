import assert from 'node:assert/strict';
import { beforeEach, test } from 'node:test';
import { getDevicePixelRatio, getThemeColors, initializeChart } from '../../../NTComponents.Charts/wwwroot/ntcomponents-charts.js';

let frames;
let intersections;
let sizes;
let densityQueries;

beforeEach(() => {
    frames = new Map();
    intersections = [];
    sizes = [];
    densityQueries = [];
    globalThis.IntersectionObserver = class {
        constructor(callback) { this.callback = callback; intersections.push(this); }
        observe() {}
        disconnect() { this.disconnected = true; }
    };
    globalThis.ResizeObserver = class {
        constructor(callback) { this.callback = callback; sizes.push(this); }
        observe() {}
        disconnect() { this.disconnected = true; }
    };
    globalThis.document = new EventTarget();
    document.visibilityState = 'visible';
    let frameId = 0;
    globalThis.window = {
        devicePixelRatio: 2,
        requestAnimationFrame: callback => { frames.set(++frameId, callback); return frameId; },
        cancelAnimationFrame: id => frames.delete(id),
        matchMedia: query => { const target = Object.assign(new EventTarget(), { media: query }); densityQueries.push(target); return target; },
        NTComponents: {
            getColorValueFromEnumName: name => ({ Primary: '#123456', Surface: '#fafafa' })[name] ?? null
        }
    };
});

async function drawFrame() {
    const current = [...frames];
    frames.clear();
    for (const [, callback] of current) { callback(); }
    await Promise.resolve();
    await Promise.resolve();
}

function move(element, x) {
    element.dispatchEvent(Object.assign(new Event('mousemove'), { offsetX: x, offsetY: 20, clientX: x, clientY: 20, buttons: 0, button: 0 }));
}

test('theme colors use the NTComponents color resolver in one batch', () => {
    assert.deepEqual(getThemeColors(['Primary', 'Surface', 'Secondary']), {
        Primary: '#123456', Surface: '#fafafa', Secondary: null
    });
    assert.equal(getDevicePixelRatio(), 2);
    window.devicePixelRatio = 0;
    assert.equal(getDevicePixelRatio(), 1);
});

test('modern theme events notify each chart once and disposal removes only its listeners', async () => {
    const firstCalls = [];
    const secondCalls = [];
    const first = initializeChart(new EventTarget(), { invokeMethodAsync: async name => firstCalls.push(name) }, false);
    const second = initializeChart(new EventTarget(), { invokeMethodAsync: async name => secondCalls.push(name) }, false);

    document.dispatchEvent(new Event('nt-theme-changed'));
    document.dispatchEvent(new Event('tnt-theme-changed'));
    await Promise.resolve();
    assert.deepEqual(firstCalls, ['OnThemeChanged']);
    assert.deepEqual(secondCalls, ['OnThemeChanged']);

    first.dispose();
    document.dispatchEvent(new Event('nt-theme-changed'));
    await Promise.resolve();
    assert.deepEqual(firstCalls, ['OnThemeChanged']);
    assert.deepEqual(secondCalls, ['OnThemeChanged', 'OnThemeChanged']);
    second.dispose();
});

for (const preventDefault of [false, true]) {
    test(`wheel zoom forwards coordinates and modifiers; preventDefault=${preventDefault}`, async () => {
        const element = new EventTarget();
        const calls = [];
        const listener = initializeChart(element, { invokeMethodAsync: async (...args) => calls.push(args) }, preventDefault);
        const values = { offsetX: 35, offsetY: 70, deltaX: 0, deltaY: -120, ctrlKey: true, shiftKey: false, altKey: false, metaKey: true };
        const event = Object.assign(new Event('wheel', { cancelable: true }), values);

        element.dispatchEvent(event);
        await Promise.resolve();
        assert.equal(event.defaultPrevented, preventDefault);
        assert.deepEqual(calls, [['OnNativeWheel', values]]);

        listener.dispose();
        element.dispatchEvent(Object.assign(new Event('wheel', { cancelable: true }), values));
        await Promise.resolve();
        assert.equal(calls.length, 1);
    });
}

test('theme and wheel callbacks tolerate a disconnected Blazor circuit', async () => {
    const element = new EventTarget();
    let callbacks = 0;
    const listener = initializeChart(element, {
        invokeMethodAsync: async () => {
            callbacks++;
            throw new Error('Circuit disconnected');
        }
    }, true);

    document.dispatchEvent(new Event('nt-theme-changed'));
    element.dispatchEvent(new Event('wheel', { cancelable: true }));
    await Promise.resolve();
    assert.equal(callbacks, 2);
    listener.dispose();
});

test('pointer bursts deliver only the latest position and leave no idle frame scheduled', async () => {
    const element = new EventTarget();
    const calls = [];
    const listener = initializeChart(element, { invokeMethodAsync: async (...args) => calls.push(args) }, false);
    for (let x = 0; x < 40; x++) { move(element, x); }
    assert.equal(frames.size, 1);

    await drawFrame();

    assert.equal(calls.length, 1);
    assert.equal(calls[0][0], 'OnNativeMouseMove');
    assert.equal(calls[0][1].offsetX, 39);
    assert.equal(frames.size, 0);
    listener.dispose();
});

test('an outstanding pointer callback retains only the latest queued move', async () => {
    const element = new EventTarget();
    const calls = [];
    let release;
    const listener = initializeChart(element, {
        invokeMethodAsync: (...args) => { calls.push(args); return new Promise(resolve => { release = resolve; }); }
    }, false);
    move(element, 1);
    await drawFrame();
    move(element, 2);
    move(element, 3);
    await drawFrame();
    assert.equal(calls.length, 1);
    release();
    await Promise.resolve();
    await drawFrame();
    assert.equal(calls.length, 2);
    assert.equal(calls[1][1].offsetX, 3);
    release();
    listener.dispose();
});

test('leaving and disposal cancel queued pointer work and release observers', async () => {
    const element = new EventTarget();
    const calls = [];
    const listener = initializeChart(element, { invokeMethodAsync: async (...args) => calls.push(args) }, false);
    move(element, 1);
    element.dispatchEvent(new Event('mouseleave'));
    await drawFrame();
    assert.equal(calls.length, 0);
    move(element, 2);
    listener.dispose();
    await drawFrame();
    assert.equal(calls.length, 0);
    assert.equal(intersections[0].disconnected, true);
    assert.equal(sizes[0].disconnected, true);
});

test('visibility, size and density changes notify the chart and density listeners rearm', async () => {
    const calls = [];
    const listener = initializeChart(new EventTarget(), { invokeMethodAsync: async (...args) => calls.push(args) }, false);
    intersections[0].callback([{ isIntersecting: false }]);
    intersections[0].callback([{ isIntersecting: true }]);
    document.visibilityState = 'hidden';
    document.dispatchEvent(new Event('visibilitychange'));
    document.visibilityState = 'visible';
    document.dispatchEvent(new Event('visibilitychange'));
    sizes[0].callback([{ contentRect: { width: 800, height: 400 } }]);
    sizes[0].callback([{ contentRect: { width: 800, height: 400 } }]);
    window.devicePixelRatio = 3;
    densityQueries[0].dispatchEvent(new Event('change'));
    assert.deepEqual(calls, [
        ['OnNativeViewportChanged', false, 2], ['OnNativeViewportChanged', true, 2],
        ['OnNativeViewportChanged', false, 2], ['OnNativeViewportChanged', true, 2],
        ['OnNativeViewportChanged', true, 2], ['OnNativeViewportChanged', true, 3]
    ]);
    assert.equal(densityQueries[1].media, '(resolution: 3dppx)');
    listener.dispose();
    document.dispatchEvent(new Event('visibilitychange'));
    densityQueries[1].dispatchEvent(new Event('change'));
    assert.equal(calls.length, 6);
});

test('wheel prevention follows updated interaction flags', async () => {
    const element = new EventTarget();
    const listener = initializeChart(element, { invokeMethodAsync: async () => {} }, false);
    listener.updateInteractions(true);
    const enabled = new Event('wheel', { cancelable: true });
    element.dispatchEvent(enabled);
    assert.equal(enabled.defaultPrevented, true);
    listener.updateInteractions(false);
    const disabled = new Event('wheel', { cancelable: true });
    element.dispatchEvent(disabled);
    assert.equal(disabled.defaultPrevented, false);
    listener.dispose();
});
