/**
 * MOD-0028-FU05 - Documentation Structures instantiation L10n bridge.
 */
'use strict';

(function () {
    const script = document.getElementById('instantiations-l10n');
    if (!script) return;

    // The bridge payload (@Json.Serialize) emits camelCase keys; index.js reads PascalCase
    // (e.g. L.CorrelationId). Normalize camelCase -> PascalCase before merging, matching the
    // QmsBaselines loader standard, so every JS-side L10n key resolves (not just the ones with
    // an inline English fallback).
    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);

    try {
        const parsed = JSON.parse(script.textContent || '{}');
        const normalized = {};
        for (const key of Object.keys(parsed || {})) {
            normalized[toPascalCase(key)] = parsed[key];
        }
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
    } catch (_) {
        window.L10n = window.L10n || {};
    }
})();
