'use strict';

// MOD-0190 S&OP — L10n bridge normalizer for the plan workspace (golden-reference slim pattern). DRAFT overlay — not built.
// Reads #sandop-details-l10n, normalizes names to PascalCase into window.L10n and warns (never substitutes text) when a key is
// missing, so a missing resource is visible in the console instead of rendering an English fallback (pack §23.9).
(function () {
    const payload = document.getElementById('sandop-details-l10n');
    const requiredKeys = [
        'Cancel',
        'Close',
        'Copy',
        'ErrorOccurred',
        'SandopPlansTitle',
        'PlanId',
        'PlanName',
        'HorizonStart',
        'HorizonEnd',
        'DemandPlanId',
        'DemandPlanVersion',
        'Status',
        'StatusDraft',
        'StatusInReview',
        'StatusApproved',
        'StatusRejected',
        'StatusArchived',
        'NotProvided',
        'SupportReference',
        'CopyDone',
        'RequestPending',
        'RequiredMissing',
        'ErrInvalidRequest',
        'ErrInvalidCorrelationId',
        'ErrUnauthenticated',
        'ErrForbidden',
        'ErrUnknownSandopPlan',
        'ErrSandopPlanAlreadyExists',
        'ErrSandopPlanStateConflict',
        'ErrSandopSignOffStateConflict',
        'ErrSignOffAlreadyRecorded',
        'ErrIdempotencyKeyReused',
        'ErrInvalidDemandReference',
        'ErrInvalidSnapshotReference',
        'ErrDependencyUnavailable',
        'ErrCommitResultUnresolved',
        'PlanDetailsTitle',
        'BackToPlans',
        'PlanSummary',
        'Horizon',
        'DemandReference',
        'CurrentSnapshot',
        'CreatedAt',
        'SummaryErrorState',
        'Snapshots',
        'SnapshotId',
        'CaptureSnapshot',
        'CaptureSnapshotTitle',
        'SourceCapturedAt',
        'SourceCapturedAtHelp',
        'SourceChecksum',
        'SupplyInputRefs',
        'SupplyInputRefsHelp',
        'SupplyInputRefCount',
        'Source',
        'ResourceId',
        'ResourceVersion',
        'AddRow',
        'RemoveRow',
        'RowIncomplete',
        'CapturedAt',
        'Provenance',
        'CurrentMarker',
        'EmptySnapshots',
        'SnapshotsErrorState',
        'SnapshotCaptured',
        'SignOffs',
        'RecordSignOff',
        'RecordSignOffTitle',
        'Snapshot',
        'Role',
        'RoleDemandPlanning',
        'RoleSupplyPlanning',
        'RoleFinance',
        'RoleOperations',
        'RoleExecutive',
        'Decision',
        'DecisionApproved',
        'DecisionRejected',
        'Comment',
        'CommentHelp',
        'CommentTooLong',
        'DecidedBy',
        'DecidedAt',
        'ConfirmSignOff',
        'SignOffRecorded',
        'EmptySignOffs',
        'SignOffsErrorState',
        'SignOffStatusNote',
        'Retry',
        'SelectOption'
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
        console.error('[S&OP] Localization payload could not be parsed.', error);
        window.L10n = window.L10n || {};
        logMissingKeys(window.L10n);
    }
})();
