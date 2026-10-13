'use strict';

// MOD-0173-FU02 L-1 — merges the tree's own words (Views/Inventory/LocationTree/_IndexL10n.cshtml) into window.L10n,
// after the shared location vocabulary. A missing key is reported, never replaced by hardcoded text.
(function () {
    const payload = document.getElementById('locationtree-l10n');
    const requiredKeys = [
        'TooManyChildren', 'OpenInList', 'ShowInList', 'ChildrenCount', 'NoChildrenForBin', 'StockYes', 'StockNo',
        'InactiveNote', 'ArchivedNote', 'ExternalNote', 'HistAction_Created', 'HistField_code'
    ];

    const logMissingKeys = (dictionary) => {
        requiredKeys.forEach((key) => {
            if (!dictionary[key]) console.warn(`[L10N WARNING] Missing localization key: ${key}`);
        });
    };

    if (!payload) {
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
        return;
    }

    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        const normalized = {};
        for (const key of Object.keys(raw)) {
            normalized[toPascalCase(key)] = raw[key];
        }
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
        logMissingKeys(window.L10n);
    } catch (error) {
        console.error('[LocationTree] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
