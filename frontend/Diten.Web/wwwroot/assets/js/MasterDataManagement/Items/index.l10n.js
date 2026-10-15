'use strict';

// MOD-0290-FU04 S5 — the Items screen's words: read once from #items-l10n (_IndexL10n.cshtml) into window.L10n.
(function () {
    const payload = document.getElementById('items-l10n');
    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);
    const requiredKeys = [
        'Actions', 'AddNew', 'Apply', 'Cancel', 'ColumnVisibility', 'Edit', 'ErrorConflict', 'ErrorForbidden',
        'ErrorGateway', 'ErrorNotFound', 'ErrorProviderUnavailable', 'ErrorUnauthorized', 'ErrorValidation', 'Export',
        'Filter', 'LifecycleActive', 'LifecycleDeactivated', 'LifecycleDraft', 'LifecyclePendingActivation',
        'LifecycleRetired', 'LifecycleStateChanged', 'ReasonLabel', 'ReasonRequired', 'Reset', 'SaveView', 'Search',
        'Unknown', 'ViewDetails'
    ];
    const warnMissing = (dictionary) => requiredKeys.forEach((key) => {
        if (!dictionary[key]) console.warn(`[L10N WARNING] Missing localization key: ${key}`);
    });

    if (!payload) {
        window.L10n = window.L10n || {};
        warnMissing(window.L10n);
        return;
    }

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        const normalized = {};
        for (const key of Object.keys(raw)) normalized[toPascalCase(key)] = raw[key];
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
        warnMissing(window.L10n);
    } catch (error) {
        console.error('[Items] Localization payload could not be parsed.', error);
    }
})();
