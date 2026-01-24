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

