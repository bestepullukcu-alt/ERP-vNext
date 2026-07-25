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

    // tn(key, args): NAMED token replacement — {objectType}, {objectId} … (WC-1b DEC-3).
    // A backend-supplied resource label carries its arguments as a NAMED map, not a positional list, so the
    // positional tf() above cannot render it. Additive and independent: tf() is untouched.
    const tn = (key, args) => {
        const text = t(key);
        if (!args || typeof args !== 'object') { return text; }
        return Object.keys(args).reduce(
            (acc, name) => acc.split('{' + name + '}').join(String(args[name])),
            text);
    };

    global.WCN = global.WCN || {};
    global.WCN.L10n = store;
    global.WCN.t = t;
    global.WCN.tf = tf;
    global.WCN.tn = tn;
})(window);
