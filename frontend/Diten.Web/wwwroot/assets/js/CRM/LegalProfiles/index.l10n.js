'use strict';

// WP-KP-5a-UI — Legal Profiles L10n bridge. Json.Serialize camelCases the payload keys, so they are turned back into the
// PascalCase names the scripts read (memory l10n-bridge-pascalcase-loader: a skipped conversion = undefined keys).
(function () {
    const payload = document.getElementById('legal-profiles-l10n');
    const requiredKeys = [
        'Active', 'Passive', 'Unknown', 'Actions', 'Edit', 'ViewDetails', 'QuickView', 'AreYouSure', 'Cancel',
        'Search', 'Export', 'Filter', 'Apply', 'Reset', 'ShowAll', 'SaveView', 'ColumnVisibility', 'Status',
        'AddNew', 'OnlyActive', 'Code', 'Country', 'Language', 'Version', 'UpdatedAt'
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
        for (const key of Object.keys(raw)) normalized[toPascalCase(key)] = raw[key];
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
        logMissingKeys(window.L10n);
    } catch (error) {
        console.error('[LegalProfiles] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
