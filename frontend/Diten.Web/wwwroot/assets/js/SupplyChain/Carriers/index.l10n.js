'use strict';

(function () {
    const payload = document.getElementById('carriers-l10n');
    const requiredKeys = [
        'Active', 'Actions', 'AddNewCarrier', 'Apply', 'AreYouSure', 'Cancel',
        'CarrierCode', 'CarrierCodeConflict', 'CarrierNotFound', 'ChangeStatus',
        'ColumnVisibility', 'ConfirmStatusChange', 'CreateReplaySuccess',
        'CreateSuccess', 'DisplayName', 'EmptyList', 'ErrorOccurred', 'Filter',
        'IdempotencyKeyReused', 'InternalError', 'InvalidCarrierTransition',
        'ModeAir', 'ModeParcel', 'ModeRail', 'ModeRoad', 'ModeSea', 'Passive',
        'PersistenceUnavailable',
        'ListUnavailable', 'QuickView', 'RecordSaved', 'Reset', 'RetrySameRequest',
        'Save', 'SaveView', 'Search', 'ShowAll', 'Status', 'StatusActive',
        'StatusChangeConfirm', 'StatusReplaySuccess', 'StatusRetired',
        'StatusSuccess', 'StatusSuspended', 'SupportReference', 'SupportedModes',
        'Unknown', 'ValidationError', 'ViewDetails'
    ];

    const toPascalCase = (key) => key.charAt(0).toUpperCase() + key.slice(1);
    const hasLocalizedValue = (dictionary, key) =>
        Object.prototype.hasOwnProperty.call(dictionary, key)
        && typeof dictionary[key] === 'string'
        && dictionary[key].trim().length > 0;
    const warnMissing = (dictionary) => requiredKeys.forEach((key) => {
        if (!hasLocalizedValue(dictionary, key))
            console.warn(`[L10N WARNING] Missing localization key: ${key}`);
    });

    if (!payload) {
        window.L10n = window.L10n || {};
        warnMissing(window.L10n);
        return;
    }

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        const normalized = {};
        Object.keys(raw).forEach((key) => { normalized[toPascalCase(key)] = raw[key]; });
        window.L10n = Object.assign({}, window.L10n || {}, normalized);
        warnMissing(window.L10n);
    } catch (error) {
        console.error('[Carriers] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        warnMissing(window.L10n);
    }
})();
