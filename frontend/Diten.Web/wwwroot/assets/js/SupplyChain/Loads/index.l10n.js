'use strict';

// MOD-0185 Loads — L10n bridge normalizer (R-4b; the Carrier pattern, R-4a). Reads #loads-l10n into window.L10n and warns —
// never substitutes text — when a key is missing or empty, so a missing resource is visible in the console.
(function () {
    const payload = document.getElementById('loads-l10n');
    const requiredKeys = [
        'AccessDenied',
        'ActionDelivery',
        'ActionPickup',
        'ActionReturn',
        'AddNewLoad',
        'AddShipment',
        'AddStop',
        'Apply',
        'Cancel',
        'CarrierId',
        'CarrierIdHelp',
        'ColumnVisibility',
        'CreateReplaySuccess',
        'CreateSuccess',
        'EmptyList',
        'ErrCarrierModeUnsupported',
        'ErrCarrierNotFound',
        'ErrCorrelationRootMismatch',
        'ErrDependencyResponseInvalid',
        'ErrDependencyUnavailable',
        'ErrDuplicateShipment',
        'ErrIdempotencyKeyReused',
        'ErrInternalError',
        'ErrInvalidLoadStops',
        'ErrPersistenceUnavailable',
        'ErrReferenceStateUnavailable',
        'ErrShipmentAlreadyAssigned',
        'ErrShipmentCarrierMismatch',
        'ErrShipmentNotEligible',
        'ErrShipmentNotFound',
        'ErrorOccurred',
        'Filter',
        'FilterCarrierId',
        'ListUnavailable',
        'LoadNumber',
        'LoadsTitle',
        'Mode',
        'ModeAir',
        'ModeParcel',
        'ModeRail',
        'ModeRoad',
        'ModeSea',
        'NotProvided',
        'PageDescription',
        'PlannedDepartAt',
        'PlannedDepartAtHelp',
        'PlannedDepartAtInvalid',
        'RecordSaved',
        'RemoveShipment',
        'RemoveStop',
        'Reset',
        'RetrySameRequest',
        'Save',
        'SaveView',
        'Search',
        'ShipmentId',
        'ShipmentIds',
        'ShipmentIdsHelp',
        'ShowAll',
        'Status',
        'StatusAccepted',
        'StatusCancelled',
        'StatusCompleted',
        'StatusDispatched',
        'StatusDraft',
        'StatusPlanned',
        'StatusTendered',
        'StopAction',
        'StopLocationReferenceId',
        'StopSequence',
        'Stops',
        'StopsHelp',
        'SupportReference',
        'ValidationError'
    ];
    const hasLocalizedValue = (dictionary, key) =>
        Object.prototype.hasOwnProperty.call(dictionary, key) && typeof dictionary[key] === 'string' && dictionary[key].trim().length > 0;
    const warnMissing = (dictionary) => requiredKeys.forEach((key) => {
        if (!hasLocalizedValue(dictionary, key)) console.warn(`[L10N WARNING] Missing localization key: ${key}`);
    });
    try {
        const raw = payload ? JSON.parse(payload.textContent || '{}') : {};
        window.L10n = Object.assign({}, window.L10n || {}, raw);
    } catch (error) {
        console.error('[Loads] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
    }
    warnMissing(window.L10n);
})();
