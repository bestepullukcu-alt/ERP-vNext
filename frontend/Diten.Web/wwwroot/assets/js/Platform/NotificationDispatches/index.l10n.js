'use strict';

(function () {
    const payload = document.getElementById('notificationdispatches-l10n');
    const requiredKeys = [
        'Active', 'Passive', 'Unknown', 'Actions', 'Edit', 'ViewDetails', 'QuickView',
        'BulkDelete', 'BulkDeleteConfirm', 'AreYouSure', 'Cancel',
        'Search', 'Export', 'Import', 'Filter', 'Apply', 'Reset', 'ShowAll',
        'SaveView', 'ColumnVisibility', 'Status', 'ErrorOccurred', 'NotAvailable',
        'TargetTenant', 'SelectTenant', 'SelectTenantPrompt', 'TemplateKey', 'Channel',
        'Provider', 'Recipients', 'QueuedAt', 'SentAt', 'FailedAt',
        'StatusQueued', 'StatusSent', 'StatusFailed', 'StatusCancelled',
        'CancelDispatch', 'CancelConfirm', 'CancelNotAllowed', 'DispatchCancelled'
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
        console.error('[NotificationDispatches] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
