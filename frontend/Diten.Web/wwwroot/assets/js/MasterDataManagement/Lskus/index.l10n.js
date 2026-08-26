'use strict';

(() => {
    const payload = document.getElementById('lskus-l10n');
    if (!payload) return;

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        const normalized = {};
        Object.keys(raw).forEach(key => {
            const pascalKey = key.charAt(0).toUpperCase() + key.slice(1);
            normalized[pascalKey] = raw[key];
        });
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
    } catch (error) {
        console.error('[Lskus] Localization payload could not be parsed.', error);
    }
})();
