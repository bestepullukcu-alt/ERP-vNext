'use strict';

/*
 * WorkCenterNext — localization bridge.
 * Reads the server-rendered JSON payload (#workcenternext-l10n) into
 * window.WCN.L10n. All text in app.js resolves through t()/tf() so every
 * label comes from the 7-language resx (never hard-coded English).
 */
(function (global) {
    const store = {};
    const payload = document.getElementById('workcenternext-l10n');

    if (payload) {
        try {
            const raw = JSON.parse(payload.textContent || '{}');
            Object.keys(raw).forEach((key) => { store[key] = raw[key]; });
        } catch (error) {
            console.error('WorkCenterNext localization payload could not be parsed.', error);
        }
    }

    // t(key): plain lookup, falls back to the key so a missing string is visible.
    const t = (key) => (Object.prototype.hasOwnProperty.call(store, key) ? store[key] : key);

    // tf(key, ...args): {0}, {1} … token replacement.
    const tf = (key, ...args) => {
        let text = t(key);
        args.forEach((value, index) => {
            text = text.split('{' + index + '}').join(String(value));
        });
        return text;
    };

    global.WCN = global.WCN || {};
    global.WCN.L10n = store;
    global.WCN.t = t;
    global.WCN.tf = tf;
})(window);
