'use strict';

// MOD-0187 Claims — L10n bridge reader (golden-reference slim pattern). Built by R-4c from the September draft v4.
// Reads #claims-l10n into window.L10n and warns (never substitutes text) when a key is missing, so a missing resource is
// visible in the console instead of rendering an English fallback. The bridge is a Dictionary (MODULE-RECIPE 5.3), so the
// keys arrive exactly as the resx names them and are copied as they are; the draft re-capitalized camel-cased names.
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

    try {
        const raw = JSON.parse(payload.textContent || '{}');
        window.L10n = Object.assign({}, window.L10n || {}, raw);
        logMissingKeys(window.L10n);
    } catch (error) {
        console.error('[Claims] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
