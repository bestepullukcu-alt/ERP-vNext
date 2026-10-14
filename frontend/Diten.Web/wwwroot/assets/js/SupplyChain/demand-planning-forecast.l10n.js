(function () {
    'use strict';
    const payload = document.getElementById('demandForecastL10n');
    if (!payload) return;
    const strings = JSON.parse(payload.textContent);
    const normalized = {};
    Object.keys(strings).forEach(key => {
        normalized[key.charAt(0).toUpperCase() + key.slice(1)] = strings[key];
    });
    window.L10n = Object.assign({}, window.L10n || {}, normalized);
})();