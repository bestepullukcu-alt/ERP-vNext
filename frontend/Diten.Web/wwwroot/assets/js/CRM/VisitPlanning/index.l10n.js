'use strict';

/**
 * MOD-0155 FU05 Visit Planning — L10n bridge. The JSON payload is serialized camelCase; the pages read PascalCase, so
 * every key MUST be PascalCased here (skipping this is why a toast renders as "(undefined: <correlationId>)").
 */
(function () {
    const payload = document.getElementById('visitplanning-l10n');
    const requiredKeys = [
        'Actions', 'Apply', 'AreYouSure', 'Cancel', 'ColumnVisibility', 'Details', 'Edit', 'EmptyState',
        'ErrorOccurred', 'Export', 'Filter', 'Import', 'Loading', 'NotAvailable', 'RecordCreated', 'RecordUpdated',
        'Reset', 'Save', 'SaveView', 'Search', 'SelectPlaceholder', 'ShowAll', 'Status', 'ViewDetails', 'QuickView',
        'SessionsTitle', 'Plan', 'CyclePeriod', 'Rep', 'Targets', 'Updated', 'NewSession', 'RouteAction',
        'DeleteSession', 'DeleteSessionConfirm', 'RecordDeleted', 'PreviewFailed', 'StatusDraft', 'StatusCommitted'
    ];

    const logMissingKeys = (dictionary) => {
        requiredKeys.forEach((key) => {
            if (!dictionary[key]) {
                console.warn(`[L10N WARNING] Missing localization key: ${key}`);
            }
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
        console.error('[VisitPlanning] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
