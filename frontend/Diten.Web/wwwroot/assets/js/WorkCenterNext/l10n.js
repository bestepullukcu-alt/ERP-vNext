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

    /*
     * moduleLabel(code): a provider CODE → the module's name in the reader's language.
     *
     * MOVED, NOT COPIED — and this is the only implementation. The rule was first written inside mock-data.js,
     * which uses it for the source chip on REAL items as well as fixtures. When the partial-board banner needed
     * the same answer, app.js could not read it from there — the shell must not render real data through the
     * fixture facade — so the rule was lifted here, to the module both host views already load first and where a
     * code-resolved-through-the-resx belongs. mock-data.js now calls this function; its private copy is gone.
     * Leaving it would have meant the banner's name and the chip's name resolving through two different
     * functions, free to drift apart — which is exactly what happened for the one turn both existed.
     *
     * Derived, not mapped: master-data → ModuleMasterData. Adding a provider means adding one resx entry.
     *
     * An unmapped code renders as the RAW CODE and warns once. That is deliberate: the raw code is still
     * something a reader can quote to whoever fixes it, and a new provider becomes a visible, explained gap
     * instead of silently borrowing another module's name.
     */
    const reportedMissingModuleCodes = new Set();
    const moduleResourceKey = (code) => 'Module' + String(code).split(/[-_]/)
        .filter(Boolean)
        .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
        .join('');
    const moduleLabel = (code) => {
        if (!code) { return ''; }
        const key = moduleResourceKey(code);
        // Read through global.WCN.t, not the local t: a host (or a test harness) may install its own translator
        // on WCN, and this must resolve with the same one every other label on the page uses.
        const translate = (global.WCN && global.WCN.t) || t;
        const resolved = translate(key);
        if (!resolved || resolved === key) {
            if (!reportedMissingModuleCodes.has(code)) {
                reportedMissingModuleCodes.add(code);
                console.warn(
                    `[WorkCenterNext] No module name for provider code "${code}" — rendering the raw code. `
                    + `Add "${key}" to the WorkCenterNext resx (7 languages).`);
            }
            return code;
        }
        return resolved;
    };

    global.WCN = global.WCN || {};
    global.WCN.L10n = store;
    global.WCN.t = t;
    global.WCN.tf = tf;
    global.WCN.tn = tn;
    global.WCN.moduleLabel = moduleLabel;
})(window);
