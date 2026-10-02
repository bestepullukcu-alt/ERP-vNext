'use strict';

// MOD-0173-FU02 — merges the shared location vocabulary (Views/Inventory/Locations/_IndexL10n.cshtml) into
// window.L10n, on L-2 and on L-1. A missing key is reported, never replaced by hardcoded text.
(function () {
    const payload = document.getElementById('locations-l10n');
    const requiredKeys = [
        'Actions', 'Cancel', 'Close', 'Filter', 'Apply', 'Reset', 'ShowAll', 'SaveView', 'ColumnVisibility', 'Searching',
        'NoResults', 'TypeSite', 'TypeWarehouse', 'TypeStorageLocation', 'TypeZone', 'TypeBin', 'StatusActive',
        'StatusInactive', 'StatusArchived', 'ModeInternal', 'ModeExternal', 'StockableYes', 'StockableNo', 'ActionEdit', 'ActionAddChild',
        'ActionActivate', 'ActionDeactivate', 'ActionArchive', 'ActionHistory', 'ActionViewStock', 'ActionBulkBins',
        'FormTitleCreate', 'FormTitleEdit', 'FormTitleChild', 'ValidationRequired', 'ConfirmActivateTitle',
        'ConfirmDeactivateTitle', 'ConfirmArchiveTitle', 'ErrServiceUnavailable'
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

    // ASP.NET Json.Serialize writes camelCase; the pages read PascalCase (L().ActionEdit).
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
        console.error('[Locations] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
