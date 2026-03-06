window.NTCharts = {
    getCssVariable: function (variableName) {
        return getComputedStyle(document.documentElement).getPropertyValue(variableName).trim();
    },
    getDevicePixelRatio: function () {
        return window.devicePixelRatio || 1;
    },
    onThemeChanged: function (dotNetHelper) {
        const observer = new MutationObserver((mutations) => {
            let themeChanged = false;
            for (const mutation of mutations) {
                if (mutation.type === 'childList') {
                    for (const node of mutation.addedNodes) {
                        if (node.nodeName === 'LINK' && node.getAttribute('data-tnt-theme') === 'true') {
                            themeChanged = true;
                            break;
                        }
                    }
                } else if (mutation.type === 'attributes' && mutation.attributeName === 'href') {
                    if (mutation.target.nodeName === 'LINK' && mutation.target.getAttribute('data-tnt-theme') === 'true') {
                        themeChanged = true;
                    }
                }
                if (themeChanged) break;
            }

            if (themeChanged) {
                dotNetHelper.invokeMethodAsync('OnThemeChanged');
            }
        });

        const head = document.head;
        const themeLink = document.querySelector('link[data-tnt-theme]');

        observer.observe(head, { childList: true, subtree: true });
        if (themeLink) {
            observer.observe(themeLink, { attributes: true });
        }

        return {
            dispose: function () {
                observer.disconnect();
            }
        };
    }
};

export function registerWheelHandler(element, dotNetHelper, preventDefault) {
    if (!element) {
        return {
            dispose: function () { }
        };
    }

    const handler = (event) => {
        if (preventDefault) {
            event.preventDefault();
        }

        dotNetHelper.invokeMethodAsync('OnNativeWheel', {
            offsetX: event.offsetX,
            offsetY: event.offsetY,
            deltaX: event.deltaX,
            deltaY: event.deltaY,
            ctrlKey: event.ctrlKey,
            shiftKey: event.shiftKey,
            altKey: event.altKey,
            metaKey: event.metaKey
        });
    };

    element.addEventListener('wheel', handler, { passive: false });

    return {
        dispose: function () {
            element.removeEventListener('wheel', handler);
        }
    };
}

