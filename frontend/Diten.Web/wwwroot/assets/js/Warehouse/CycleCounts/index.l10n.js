'use strict';

// MOD-0181 DL-4 — merges the screen's words (Views/Warehouse/CycleCounts/_IndexL10n.cshtml) into window.L10n. The page's own
// vocabulary arrives as L10n.Words (keys kept as written). A missing key is reported, never replaced by hardcoded text.
(function () {
    const payload = document.getElementById('cyclecounts-l10n');
    const requiredKeys = [
        'Active', 'Passive', 'Actions', 'Edit', 'ViewDetails', 'QuickView', 'AreYouSure', 'Cancel', 'Search', 'Export', 'Import', 'Filter',
        'Apply', 'Reset', 'ShowAll', 'SaveView', 'ColumnVisibility', 'Status', 'Save', 'Searching', 'NoResults', 'ErrorOccurred', 'BulkDelete', 'BulkDeleteConfirm', 'AddNew', 'Words'
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
        console.error('[CycleCounts] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
