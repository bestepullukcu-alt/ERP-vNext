'use strict';

// MOD-0186 Returns — L10n bridge normalizer (golden-reference slim pattern). Built by R-2 (2026-10-04).
// Reads #returns-l10n, normalizes names to PascalCase into window.L10n and warns (never substitutes text) when a key is
// missing, so a missing resource is visible in the console instead of rendering an English fallback (pack §32.9).
(function () {
    const payload = document.getElementById('returns-l10n');
    const requiredKeys = [
        'Status',
        'ShowAll',
        'Apply',
        'Reset',
        'Cancel',
        'Close',
        'Copy',
        'Filter',
        'SaveView',
        'ColumnVisibility',
        'Search',
        'ErrorOccurred',
        'RecordSaved',
        'AreYouSure',
        'ReturnsTitle',
        'PageDescription',
        'AddNewReturns',
        'ActionsHeader',
        'RmaNumber',
        'ReturnId',
        'ShipmentId',
        'ShipmentIdHelp',
        'ReturnSummary',
        'QuickView',
        'QuickViewNote',
        'EmptyState',
        'ListErrorState',
        'Retry',
        'NotProvided',
        'SupportReference',
        'CopyDone',
        'TotalLabel',
        'StatusRequested',
        'StatusAuthorized',
        'StatusRejected',
        'StatusInTransit',
        'StatusReceived',
        'StatusDispositioned',
        'StatusClosed',
        'StatusCancelled',
        'ShipmentStatusDraft',
        'ShipmentStatusPlanned',
        'ShipmentStatusDispatched',
        'ShipmentStatusInTransit',
        'ShipmentStatusDelivered',
        'ShipmentStatusException',
        'ShipmentStatusClosed',
        'ShipmentStatusCancelled',
        'FormTitleCreate',
        'Resolve',
        'ResolveFirst',
        'ShipmentNumber',
        'ShipmentStatus',
        'EligibilityNote',
        'ShipmentIneligible',
        'ReturnLines',
        'ReturnLinesHelp',
        'SelectLine',
        'LineNumber',
        'ShippedQuantity',
        'ReturnQuantity',
        'ReturnQuantityHelp',
        'UomId',
        'NoShipmentLines',
        'ReasonCode',
        'EvidenceReferenceIds',
        'EvidenceReference',
        'AddEvidenceReference',
        'RemoveEvidenceReference',
        'EvidenceHelp',
        'CreateReturn',
        'ReturnCreated',
        'ReturnCreateReplayed',
        'TransitionTitle',
        'TransitionTarget',
        'ActionAuthorize',
        'ActionReject',
        'ActionMarkInTransit',
        'ActionCancelReturn',
        'ActionReceive',
        'ActionDisposition',
        'ActionClose',
        'TargetReceivedLabel',
        'ReceivedManualNote',
        'OccurredAt',
        'OccurredAtHelp',
        'OccurredAtInvalid',
        'DispositionCode',
        'DispositionCodeHelp',
        'InventoryTransactionReferenceId',
        'InventoryReferenceHelp',
        'SubmitTransition',
        'ConfirmTransition',
        'TransitionCompleted',
        'TransitionReplayed',
        'RequestPending',
        'NoticeSessionEnded',
        'NoticeForbidden',
        'NoticeUnsupportedMedia',
        'ErrInvalidRequest',
        'ErrReturnNotFound',
        'ErrShipmentNotFound',
        'ErrShipmentLineNotFound',
        'ErrInvalidReturnTransition',
        'ErrDispositionRequired',
        'ErrInvalidReturnQuantity',
        'ErrReturnQuantityExceeded',
        'ErrReturnUomMismatch',
        'ErrDuplicateReturnLine',
        'ErrShipmentNotReturnable',
        'ErrCorrelationRootMismatch',
        'ErrIdempotencyKeyReused',
        'ErrReturnSourceChanged',
        'ErrReturnShipmentRootInvalid',
        'ErrDependencyResponseInvalid',
        'ErrReturnShipmentRootUnavailable',
        'ErrReferenceStateUnavailable',
        'ErrDependencyUnavailable',
        'ErrPersistenceUnavailable',
        'ErrInternalError'
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
        console.error('[Returns] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
