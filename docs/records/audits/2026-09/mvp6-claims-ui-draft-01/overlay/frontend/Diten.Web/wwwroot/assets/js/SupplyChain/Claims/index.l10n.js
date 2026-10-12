'use strict';

// MOD-0187 Claims — L10n bridge normalizer (golden-reference slim pattern). DRAFT overlay — not built.
// Reads #claims-l10n, normalizes names to PascalCase into window.L10n and warns (never substitutes text) when a key is
// missing, so a missing resource is visible in the console instead of rendering an English fallback.
(function () {
    const payload = document.getElementById('claims-l10n');
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
        'ClaimsTitle',
        'PageDescription',
        'AddNewClaims',
        'ActionsHeader',
        'ClaimNumber',
        'ClaimId',
        'ShipmentId',
        'ShipmentIdHelp',
        'ClaimedAmount',
        'CurrencyLabel',
        'ClaimSummary',
        'QuickView',
        'EmptyState',
        'ListErrorState',
        'Retry',
        'NotProvided',
        'SupportReference',
        'CopyDone',
        'TotalLabel',
        'ApprovedAmountNotListed',
        'StatusOpen',
        'StatusInvestigating',
        'StatusApproved',
        'StatusRejected',
        'StatusSettled',
        'StatusClosed',
        'StatusWithdrawn',
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
        'LinkShipmentCarrier',
        'NoShipmentCarrier',
        'ReasonCode',
        'AmountHelp',
        'CurrencyHelp',
        'EvidenceReferenceIds',
        'EvidenceReference',
        'AddEvidenceReference',
        'RemoveEvidenceReference',
        'EvidenceHelp',
        'CreateClaim',
        'ClaimCreated',
        'ClaimCreateReplayed',
        'TransitionTitle',
        'TransitionTarget',
        'ActionInvestigate',
        'ActionWithdraw',
        'ActionApprove',
        'ActionReject',
        'ActionSettle',
        'ActionClose',
        'TargetSettledLabel',
        'SettledNoPaymentNote',
        'OccurredAt',
        'OccurredAtHelp',
        'ApprovedAmount',
        'ApprovedAmountHelp',
        'ResolutionCode',
        'NoteLabel',
        'SubmitTransition',
        'ConfirmTransition',
        'TransitionCompleted',
        'TransitionReplayed',
        'ApprovedAmountResult',
        'RequestPending',
        'ErrInvalidRequest',
        'ErrUnauthenticated',
        'ErrForbidden',
        'ErrClaimNotFound',
        'ErrUnsupportedMediaType',
        'ErrClaimShipmentIneligible',
        'ErrClaimCarrierMismatch',
        'ErrClaimAmountInvalid',
        'ErrClaimApprovalAmountInvalid',
        'ErrClaimApprovedAmountNotAllowed',
        'ErrInvalidClaimTransition',
        'ErrClaimCorrelationMismatch',
        'ErrIdempotencyKeyReused',
        'ErrClaimReferenceInvalid',
        'ErrClaimReferenceIncomplete',
        'ErrClaimReferenceUnavailable',
        'ErrClaimStorageUnavailable',
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
        console.error('[Claims] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
