window.NTCharts = {
    getCssVariable: function (variableName) {
        return getComputedStyle(document.documentElement).getPropertyValue(variableName).trim();
    }
};
