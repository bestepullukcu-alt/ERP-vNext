'use strict';

// MOD-0178 DL-7 — merges the exception box's words (Views/Warehouse/WarehouseExceptions/_IndexL10n.cshtml) into window.L10n. The Warehouse.* vocabulary
// arrives as L10n.Words (keys kept as written). A missing key is reported, never replaced by hardcoded text.
(function () {
    const payload = document.getElementById('warehouseexceptions-l10n');
    const requiredKeys = [
        'Active', 'Passive', 'Actions', 'ViewDetails', 'QuickView', 'AreYouSure', 'Cancel', 'Search', 'Export', 'Import', 'Filter',
        'Apply', 'Reset', 'ShowAll', 'SaveView', 'ColumnVisibility', 'Status', 'Save', 'Searching', 'NoResults', 'ErrorOccurred', 'Words'
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

    // ASP.NET Json.Serialize writes camelCase members; the page reads PascalCase (L().QuickView). Dictionary keys (Words) are kept.
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
        console.error('[WarehouseExceptions] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
