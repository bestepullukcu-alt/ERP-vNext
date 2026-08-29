/**
 * MOD-0029-FU25 — Master Register Detail Governance Pack: Identifiers (FU07) + Lifecycle (FU08/FU08A) tabs.
 *
 * Profile (unchanged from FU24): same-origin MVC proxy only, no direct Platform 5057 call, no tenant id from the
 * browser, anti-forgery token on every mutation, HTML-escaped server messages.
 *
 * Governance stance: this file NEVER pre-satisfies, skips or infers past a backend guard. The allowed-transition
 * flags come from the FU08 state endpoint (Can* booleans computed by the domain policy), the reason/effective-date/
 * identifier/approval/release-gate rules are all evaluated server-side, and a refused transition is surfaced as a
 * localized message — never retried, never worked around. Cancelling an identifier ALLOCATION is not a delete: the
 * value is retained forever and never reused (SOP §6.3).
 */
'use strict';

const MasterRegisterGovernance = (function () {
    const host = document.querySelector('.master-register-details');
    const entryId = host?.dataset.masterRegisterId || '';
    const currentUserId = host?.dataset.currentUserId || '';
    const currentUserDisplayName = host?.dataset.currentUserDisplayName || currentUserId;
    const endpoint = `/DocumentManagement/MasterRegister/api/${entryId}`;
    const perms = window.MasterRegisterDetailPerms || {};

    const L = () => window.L10n || {};
    const t = (key) => L()[key] || key;
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const getAuthHeaders = () => window.DitenDataTable?.getAuthHeaders?.() || {};

    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c]));
    const na = () => t('NotAvailable');
    const text = (v) => (v === null || v === undefined || v === '' ? na() : esc(v));
const fmtDateTime = (v) => {
    if (!v) return na();
    const d = new Date(v);
    return Number.isNaN(d.getTime()) ? na() : d.toLocaleString();
};
const fmtIdentifierDateTime = (v) => {
    if (!v) return na();
    const d = new Date(v);
    if (Number.isNaN(d.getTime())) return na();

    const parts = new Intl.DateTimeFormat('en-US', {
        month: 'short',
        day: '2-digit',
        year: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        hour12: true
    }).formatToParts(d).reduce((result, part) => {
        result[part.type] = part.value;
        return result;
    }, {});

    return `${parts.month} ${parts.day}, ${parts.year} ${parts.hour}:${parts.minute} ${parts.dayPeriod}`;
};
    const unwrap = (p) => p?.data ?? p?.Data ?? null;
    const unwrapList = (p) => {
        const d = unwrap(p) ?? p;
        if (Array.isArray(d)) return d;
        for (const k of ['items', 'Items', 'results', 'Results']) if (Array.isArray(d?.[k])) return d[k];
        return [];
    };

    const badge = (color, label) => `<span class="badge bg-label-${color}">${esc(label)}</span>`;
    const boolBadge = (v) => badge(v ? 'success' : 'secondary', v ? t('Yes') : t('No'));
    const row = (label, valueHtml) =>
        `<dt class="col-sm-5 fw-normal text-muted">${esc(label)}</dt><dd class="col-sm-7">${valueHtml}</dd>`;

    const lifecycleBadge = (value) => {
        const key = String(value || '');
        const map = {
            Draft: 'secondary', InReview: 'info', ApprovedPendingEffective: 'primary', Effective: 'success',
            UnderRevision: 'warning', Suspended: 'danger', Superseded: 'secondary', Retired: 'dark', ObsoleteCopy: 'dark'
        };
        return key ? badge(map[key] || 'secondary', t(`Lifecycle${key}`)) : na();
    };
    const allocationStatusBadge = (value) => {
        const key = String(value || '');
        const map = { Reserved: 'info', Assigned: 'success', Cancelled: 'secondary', Abandoned: 'dark', SupersededByCorrection: 'warning' };
        return key ? badge(map[key] || 'secondary', t(`AllocationStatus${key}`)) : na();
    };
    const identifierTypeLabel = (v) => (v ? t(`IdentifierType${v}`) : na());
    const allocationReasonLabel = (v) => (v ? t(`AllocationReason${v}`) : na());

    /**
     * Maps the backend reason_code to a localized, actionable message. Falls back to the server's own error text so
     * a reason code we have not localized yet is still shown truthfully rather than swallowed.
     */
    const REASON_CODE_KEYS = {
        INVALID_TRANSITION: 'InvalidTransition',
        REASON_REQUIRED: 'ReasonRequiredError',
        MISSING_IDENTIFIER: 'MissingUidOrCode',
        RETROACTIVE_EFFECTIVE_DATE: 'RetroactiveEffectiveDate',
        RELEASE_GATE_BLOCKED: 'ReleaseGateBlocked',
        RELEASE_GATE_INCOMPLETE: 'ReleaseGateBlocked',
        APPROVAL_EVIDENCE_MISSING: 'ApprovalEvidenceBlocked',
        APPROVAL_EVIDENCE_INCOMPLETE: 'ApprovalEvidenceBlocked',
        DUPLICATE_EFFECTIVE: 'DuplicateEffective',
        STALE_VERSION: 'StaleVersion',
        DUPLICATE_IDENTIFIER: 'DuplicateIdentifier',
        MANUAL_IDENTIFIER_EXISTS: 'ManualIdentifierExists',
        TYPE_MAPPING_MISSING: 'TypeMappingMissing',
        RECORD_NOT_ELIGIBLE: 'RecordNotEligible',
        EXTERNAL_NOT_ELIGIBLE: 'ExternalNotEligible',
        VARIANT_INHERITS_PARENT: 'VariantInheritsParent',
        ENTRY_NOT_ALLOCATABLE: 'EntryNotAllocatable',
        NOT_FOUND_NON_LEAKAGE: 'RegisterEntryNotFound',
        VALIDATION_FAILED: 'ValidationFailed',
        PERMISSION_DENIED: 'Forbidden',

        // MOD-0029-FU26 — approval (FU09) + release gate (FU10). The first block is what those services actually
        // emit today; the second is the wider governance vocabulary the lifecycle/gate engines may surface as they
        // grow. Anything not listed still shows the server's own message rather than being swallowed.
        REQUIREMENT_NOT_FOUND: 'RequirementNotFound',
        WRONG_APPROVER_ROLE: 'WrongApproverRole',
        SEGREGATION_FAILED: 'SegregationFailedError',
        INVALID_GATE_KEY: 'InvalidGateKey',
        EVIDENCE_INCOMPLETE: 'GateEvidenceRequired',

        AUTHOR_SOLE_APPROVER_BLOCKED: 'AuthorSoleApproverBlocked',
        MANDATORY_REQUIREMENT_MISSING: 'MissingMandatoryEvidence',
        RELEASE_GATE_BLOCKED_DETAIL: 'ReleaseGateBlocked',
        TRAINING_NOT_READY: 'TrainingNotReady',
        REPOSITORY_NOT_APPROVED: 'RepositoryNotApproved',
        REQUIRED_EXECUTION_MATERIALS_MISSING: 'RequiredExecutionMaterialsMissing',
        SUPERSEDED_COPY_WITHDRAWAL_MISSING: 'SupersededCopyWithdrawalMissing',
        MASTER_REGISTER_INACTIVE: 'MasterRegisterInactive',
        UID_CODE_MISSING: 'UidCodeMissing',
        EVIDENCE_REFERENCE_REQUIRED: 'EvidenceReferenceRequired',
        GATE_EVIDENCE_REQUIRED: 'GateEvidenceRequired',

        // MOD-0029-FU27 — training (FU11). ASSIGNMENT_NOT_FOUND / EVIDENCE_REQUIRED / REASON_REQUIRED are what the
        // service emits today; the TRAINING_* family is the wider vocabulary the readiness engine may surface later.
        ASSIGNMENT_NOT_FOUND: 'TrainingAssignmentMissing',
        EVIDENCE_REQUIRED: 'EvidenceReferenceRequired',
        REASON_REQUIRED: 'ReasonRequiredError',
        TRAINING_MATRIX_MISSING: 'TrainingMatrixMissing',
        TRAINING_ASSIGNMENT_MISSING: 'TrainingAssignmentMissing',
        TRAINING_COMPLETION_REQUIRED: 'TrainingCompletionRequired',
        TRAINING_EFFECTIVENESS_REQUIRED: 'TrainingEffectivenessRequired',
        TRAINING_EFFECTIVENESS_FAILED: 'TrainingEffectivenessFailed',
        TRAINING_RESTRICTION_REQUIRED: 'TrainingRestrictionRequired',
        INVALID_TRAINING_STATUS: 'InvalidTrainingStatus',

        // MOD-0029-FU28 — repository assessment (FU16) + controlled copy (FU17). First block is what those services
        // emit today; the rest is the wider gate-2/gate-6 vocabulary the release-gate engine may surface.
        ASSESSMENT_NOT_FOUND: 'RepositoryAssessmentNotFound',
        NAME_AND_TYPE_REQUIRED: 'RepositoryNameAndTypeRequired',
        REQUIRED_FIELDS_MISSING: 'RepositoryRequiredFieldsMissing',
        ALREADY_DECIDED: 'RepositoryAlreadyDecided',
        APPROVER_ROLE_INVALID: 'RepositoryApproverRoleInvalid',
        LINK_STATUS_INVALID: 'RepositoryLinkStatusInvalid',
        COPY_NOT_FOUND: 'ControlledCopyNotFound',
        PLAN_NOT_FOUND: 'WithdrawalPlanNotFound',
        FINDING_NOT_FOUND: 'FindingNotFound',
        NOT_ELIGIBLE_FOR_ACTIVE_COPY: 'CopyNotEligible',
        DUPLICATE_COPY_NUMBER: 'DuplicateCopyNumber',
        HOLDER_OR_LOCATION_REQUIRED: 'HolderOrLocationRequired',
        PLAN_INCOMPLETE: 'PlanIncomplete',
        DEVIATION_REQUIRED: 'DeviationRequired',

        REPOSITORY_ASSESSMENT_NOT_FOUND: 'RepositoryAssessmentNotFound',
        REPOSITORY_BOUNDARY_BLOCKED: 'RepositoryBoundaryBlocked',
        UNAPPROVED_REPOSITORY: 'UnapprovedRepositoryError',
        VALIDATED_DMS_EVIDENCE_REQUIRED: 'ValidatedDmsEvidenceRequired',
        INTERIM_REPOSITORY_LIMITATION: 'InterimRepositoryLimitation',
        CONTROLLED_COPY_NOT_FOUND: 'ControlledCopyNotFound',
        CONTROLLED_COPY_WITHDRAWAL_REQUIRED: 'ControlledCopyWithdrawalRequired',
        OBSOLETE_COPY_FINDING_OPEN: 'ObsoleteCopyFindingOpen',
        COPY_ALREADY_WITHDRAWN: 'CopyAlreadyWithdrawn',
        COPY_RECONCILIATION_REQUIRED: 'CopyReconciliationRequired',

        // MOD-0029-FU29 — retention (FU15) + signatures (FU23) + quality events (FU22). First value is the constant
        // the service emits today; anything not listed still shows the server's own message rather than being swallowed.
        RETENTION_POLICY_NOT_FOUND: 'NoRetentionScheduleFound',
        RETENTION_SUBJECT_NOT_FOUND: 'NoRetentionScheduleFound',
        LEGAL_HOLD_ACTIVE: 'LegalHoldActive',
        LEGAL_HOLD_NOT_FOUND: 'LegalHoldNotFound',
        LEGAL_HOLD_NOT_ACTIVE: 'LegalHoldNotActive',
        DISPOSITION_BLOCKED: 'DispositionBlocked',
        DISPOSITION_NOT_ELIGIBLE: 'DispositionNotEligible',
        HOLD_RELEASE_LEGAL_APPROVAL_REQUIRED: 'HoldReleaseApprovalRequired',
        HOLD_RELEASE_GQD_CONCURRENCE_REQUIRED: 'HoldReleaseApprovalRequired',

        SIGNATURE_POLICY_NOT_FOUND: 'SignaturePolicyNotFound',
        SIGNATURE_REQUEST_NOT_FOUND: 'SignatureRequestNotFound',
        SIGNATURE_NOT_FOUND: 'SignatureNotFound',
        SIGNATURE_NOT_ALLOWED: 'SignatureNotAllowed',
        SUBJECT_NOT_SIGNABLE: 'SubjectNotSignable',
        FINGERPRINT_MISMATCH: 'FingerprintMismatch',
        AUTHENTICATION_CONTEXT_REQUIRED: 'AuthenticationContextRequired',
        TWO_FACTOR_NOT_IMPLEMENTED: 'TwoFactorNotImplemented',
        DUPLICATE_SIGNATURE: 'DuplicateSignature',
        SIGNATURE_REQUEST_ALREADY_SIGNED: 'DuplicateSignature',
        UNAPPROVED_REPOSITORY: 'UnapprovedRepositoryError',

        QUALITY_EVENT_NOT_FOUND: 'QualityEventNotFound',
        QUALITY_EVENT_LINK_REQUIRED: 'QualityEventLinkRequired',
        QUALITY_EVENT_BLOCKING: 'QualityEventBlocking',
        DEVIATION_NOT_FOUND: 'DeviationNotFoundError',
        CAPA_ACTION_NOT_FOUND: 'CapaNotFoundError',
        QUALITY_EVENT_SOURCE_NOT_FOUND: 'QualityEventSourceNotFound',
        REQUIRED_DEVIATION_NOT_CLOSED: 'DeviationOpen',
        REQUIRED_CAPA_ACTIONS_NOT_SETTLED: 'CapaOpen',
        CAPA_OPEN: 'CapaOpen',
        CAPA_EFFECTIVENESS_PENDING: 'CapaEffectivenessPending',
        DEVIATION_OPEN: 'DeviationOpen',
        LINK_ALREADY_EXISTS: 'LinkAlreadyExists'
    };

    const describeFailure = (res, payload) => {
        const errors = payload?.errors || payload?.Errors;
        const serverMessage = Array.isArray(errors) ? errors.filter(Boolean).join(' • ') : (typeof errors === 'string' ? errors : '');
        const code = payload?.reason_code || payload?.reasonCode || payload?.ReasonCode;
        const localized = code && REASON_CODE_KEYS[code] ? t(REASON_CODE_KEYS[code]) : '';

        if (res?.status === 401) return t('Unauthorized');
        if (res?.status === 403) return localized || serverMessage || t('Forbidden');
        if (localized && serverMessage) return `${localized} — ${serverMessage}`;
        if (localized) return localized;
        if (serverMessage) return serverMessage;
        if (res?.status === 400 || res?.status === 422) return t('ValidationFailed');
        if (res?.status === 409) return t('ConflictOccurred');
        return t('ErrorOccurred');
    };

    const showAlert = (id, message) => {
        const box = document.getElementById(id);
        if (!box) return;
        box.textContent = message;
        box.classList.remove('d-none');
    };
    const hideAlert = (id) => document.getElementById(id)?.classList.add('d-none');

    const handleFailure = (res, payload, alertId) => {
        if (window.DitenUnauthorized?.handle(res, payload)) return;
        const message = describeFailure(res, payload);
        window.showToast?.(message, 'error');
        if (alertId) showAlert(alertId, message);
    };

    const emptyRow = (colspan, messageKey) =>
        `<tr><td colspan="${colspan}" class="text-center text-muted py-4">${esc(t(messageKey))}</td></tr>`;
    const loadingRow = (colspan) =>
        `<tr><td colspan="${colspan}" class="text-center py-4"><span class="spinner-border spinner-border-sm text-primary" role="status" aria-hidden="true"></span></td></tr>`;

    const getJson = async (path) => {
        const res = await fetch(`${endpoint}${path}`, { credentials: 'same-origin', headers: getAuthHeaders() });
        const payload = await res.json().catch(() => ({}));
        return { res, payload, ok: res.ok && payload?.isSuccessful !== false };
    };

    // Every mutation goes through here: anti-forgery token, button lock, envelope-aware failure handling.
    const postJson = async (path, body, button) => {
        const form = new FormData();
        form.append('__RequestVerificationToken', token());
        form.append('payloadJson', JSON.stringify(body || {}));

        if (button) button.disabled = true;
        try {
            const res = await fetch(`${endpoint}${path}`, { method: 'POST', credentials: 'same-origin', body: form });
            const payload = await res.json().catch(() => ({}));
            return { res, payload, ok: res.ok && payload?.isSuccessful !== false };
        } catch (error) {
            console.error('[MasterRegister FU25] Request failed.', error);
            return { res: { status: 0 }, payload: {}, ok: false };
        } finally {
            if (button) button.disabled = false;
        }
    };

    // ── Identifiers tab ──────────────────────────────────────────────────────
    const Identifiers = (function () {
        let loaded = false;
        let currentDetail = null;
        let allocations = [];
        let ledgerDt = null;

        const setActionAvailability = () => {
            const hasUid = !!currentDetail?.permanentUid;
            const hasCode = !!currentDetail?.documentCode;
            const apply = (id, buttonName, disabled, tooltipKey) => {
                const title = disabled ? t(tooltipKey) : '';
                const btn = document.getElementById(id);
                if (btn) {
                    btn.disabled = disabled;
                    btn.title = title;
                }

                const dtButton = ledgerDt?.button(`${buttonName}:name`);
                if (!dtButton?.any?.()) return;
                dtButton.enable(!disabled);
                const node = dtButton.node();
                if (node?.attr) node.attr('title', title);
            };
            apply('btnAllocateUid', 'allocateUid', hasUid, 'UidAlreadyAllocated');
            apply('btnAllocateCode', 'allocateCode', hasCode, 'CodeAlreadyAllocated');
            apply('btnAllocateBoth', 'allocateBoth', hasUid || hasCode, 'IdentifiersAlreadyAllocated');
        };

        const renderLedger = () => {
            const table = document.getElementById('identifierLedgerTable');
            document.getElementById('identifierLedgerSkeleton')?.classList.add('d-none');
            if (!table) return;

            if (ledgerDt) {
                ledgerDt.clear();
                ledgerDt.rows.add(allocations);
                ledgerDt.draw();
                return;
            }

            if (!window.DataTable || !window.DtDefaults?.create) return;
            const identifierActionButtons = [{
                text: `<i class="icon-base bx bx-refresh icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${esc(t('ReloadTab'))}</span>`,
                className: 'dropdown-item',
                attr: { id: 'btnReloadIdentifiers', title: t('ReloadTab') },
                action: () => void load()
            }];
            if (perms.canAllocate) {
                identifierActionButtons.push(
                    {
                        name: 'allocateUid',
                        text: `<i class="icon-base bx bx-hash icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${esc(t('AllocateUid'))}</span>`,
                        className: 'dropdown-item',
                        attr: { id: 'btnAllocateUid', 'data-identifier-action': 'allocate-uid', title: t('AllocateUid') },
                        action: (_event, _dt, node) => void allocate('allocate-uid', node?.get?.(0) || node?.[0])
                    },
                    {
                        name: 'allocateCode',
                        text: `<i class="icon-base bx bx-barcode icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${esc(t('AllocateCode'))}</span>`,
                        className: 'dropdown-item',
                        attr: { id: 'btnAllocateCode', 'data-identifier-action': 'allocate-code', title: t('AllocateCode') },
                        action: (_event, _dt, node) => void allocate('allocate-code', node?.get?.(0) || node?.[0])
                    },
                    {
                        name: 'allocateBoth',
                        text: `<i class="icon-base bx bx-collection icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${esc(t('AllocateBoth'))}</span>`,
                        className: 'dropdown-item',
                        attr: { id: 'btnAllocateBoth', 'data-identifier-action': 'allocate-both', title: t('AllocateBoth') },
                        action: (_event, _dt, node) => void allocate('allocate-both', node?.get?.(0) || node?.[0])
                    }
                );
            }
            if (perms.canReserve) {
                identifierActionButtons.push({
                    text: `<i class="icon-base bx bx-bookmark icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${esc(t('ReserveIdentifier'))}</span>`,
                    className: 'dropdown-item',
                    attr: { id: 'btnReserveIdentifier', title: t('ReserveIdentifier') },
                    action: () => {
                        const form = document.getElementById('reserveIdentifierForm');
                        form?.reset();
                        form?.classList.remove('was-validated');
                        hideAlert('reserveIdentifierAlert');
                        window.bootstrap?.Offcanvas
                            .getOrCreateInstance(document.getElementById('reserveIdentifierOffcanvas'))
                            .show();
                    }
                });
            }
            const toolbarButtons = [{
                extend: 'collection',
                text: `<i class="icon-base bx bx-cog icon-sm"></i><span class="ms-2">${esc(t('IdentifierOperations'))}</span>`,
                className: 'btn btn-primary dropdown-toggle',
                attr: {
                    id: 'btnIdentifierActions',
                    title: t('IdentifierOperations'),
                    'aria-haspopup': 'true'
                },
                buttons: identifierActionButtons
            }];

            ledgerDt = new DataTable(table, window.DtDefaults.create({
                data: allocations,
                stateSave: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                paging: true,
                lengthChange: true,
                searching: true,
                info: true,
                order: [[6, 'desc']],
                language: {
                    emptyTable: t('NoIdentifierLedgerFound'),
                    zeroRecords: t('NoIdentifierLedgerFound')
                },
                columns: [
                    { data: null, name: 'control', defaultContent: '' },
                    { data: 'identifierType', name: 'identifierType' },
                    { data: 'identifierValue', name: 'identifierValue' },
                    { data: 'allocationStatus', name: 'allocationStatus' },
                    { data: 'isSystemAllocated', name: 'allocationSource' },
                    { data: 'allocationReason', name: 'allocationReason' },
                    { data: 'allocatedAt', name: 'allocatedAt' },
                    { data: 'allocatedBy', name: 'allocatedBy' },
                    { data: null, name: 'actions', defaultContent: '' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => esc(identifierTypeLabel(data)) },
                    { targets: 2, responsivePriority: 1, render: (data) => `<span class="fw-medium text-heading">${text(data)}</span>` },
                    { targets: 3, render: (data) => allocationStatusBadge(data) },
                    { targets: 4, render: (data) => data ? badge('primary', t('SystemAllocated')) : badge('secondary', t('ManualReserve')) },
                    { targets: 5, render: (data) => esc(allocationReasonLabel(data)) },
            {
                targets: 6,
                render: (data, type) => type === 'display' ? esc(fmtIdentifierDateTime(data)) : (data || '')
            },
                    { targets: 7, render: (data) => text(data) },
                    {
                        targets: -1,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all text-end pe-3',
                        render: (_data, _type, rowData) => {
                            const cancellable = rowData.allocationStatus === 'Reserved' || rowData.allocationStatus === 'Assigned';
                            return perms.canCancelAllocation && cancellable
                                ? `<button type="button" class="btn btn-sm btn-label-warning js-cancel-allocation"
                                           data-allocation-id="${esc(rowData.id)}" data-allocation-value="${esc(rowData.identifierValue)}">
                                       <i class="icon-base bx bx-x-circle me-1"></i>${esc(t('CancelAllocation'))}
                                   </button>`
                                : `<span class="text-muted small">${esc(t('ActionNotAvailable'))}</span>`;
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    null,
                    {},
                    toolbarButtons,
                    { exportColumns: [1, 2, 3, 4, 5, 6, 7], colvisColumns: [1, 2, 3, 4, 5, 6, 7] }
                ),
                drawCallback: function () {
                    window.DtDefaults?.updateVisualState?.(this.api(), 0);
                },
                initComplete: function () {
                    window.DtDefaults?.updateVisualState?.(this.api(), 0);
                }
            }));
        };

        const load = async () => {
            hideAlert('identifiersAlert');
            document.getElementById('identifierLedgerSkeleton')?.classList.remove('d-none');

            const detailUrl = `/DocumentManagement/MasterRegister/api/detail/${encodeURIComponent(entryId)}`;
            const [detail, ledger] = await Promise.all([
                fetch(detailUrl, { credentials: 'same-origin', headers: getAuthHeaders() })
                    .then(async (res) => {
                        const payload = await res.json().catch(() => ({}));
                        return { res, payload, ok: res.ok && payload?.isSuccessful !== false };
                    }),
                getJson('/identifiers/ledger')
            ]);

            // The detail call is the authority for the entry's CURRENT identity; the ledger is the audit trail.
            if (detail.ok) currentDetail = unwrap(detail.payload) || currentDetail;
            else handleFailure(detail.res, detail.payload, 'identifiersAlert');

            if (ledger.ok) {
                allocations = unwrapList(ledger.payload);
            } else {
                allocations = [];
                handleFailure(ledger.res, ledger.payload, 'identifiersAlert');
            }

            renderLedger();
            setActionAvailability();
            loaded = true;
        };

        const allocate = async (action, button) => {
            const result = await postJson(`/identifiers/${action}`, { allocationReason: 'NewDocument' }, button);
            if (!result.ok) {
                handleFailure(result.res, result.payload, 'identifiersAlert');
                return;
            }
            window.showToast?.(t('AllocationSucceeded'), 'success');
            await load();
            Lifecycle.invalidate();
        };

        const bind = () => {
            document.getElementById('reserveIdentifierForm')?.addEventListener('submit', async (event) => {
                event.preventDefault();
                const form = event.currentTarget;
                if (!form.checkValidity()) { form.classList.add('was-validated'); return; }

                const payload = {
                    identifierType: document.getElementById('reserveIdentifierType').value,
                    identifierValue: document.getElementById('reserveIdentifierValue').value.trim(),
                    allocationReason: document.getElementById('reserveAllocationReason').value,
                    legacyCode: document.getElementById('reserveLegacyCode').value.trim() || null,
                    sourceSystem: document.getElementById('reserveSourceSystem').value.trim() || null,
                    sourceLegacyId: document.getElementById('reserveSourceLegacyId').value.trim() || null
                };
                // registerEntryId is intentionally NOT sent — the MVC proxy pins it from the route.
                const result = await postJson('/identifiers/reserve', payload, event.submitter);
                if (!result.ok) {
                    handleFailure(result.res, result.payload, 'reserveIdentifierAlert');
                    return;
                }
                window.bootstrap?.Offcanvas
                    .getOrCreateInstance(document.getElementById('reserveIdentifierOffcanvas'))
                    .hide();
                window.showToast?.(t('ReservationSucceeded'), 'success');
                await load();
            });

            document.getElementById('identifierLedgerBody')?.addEventListener('click', (event) => {
                const btn = event.target.closest('.js-cancel-allocation');
                if (!btn) return;
                const allocationId = btn.dataset.allocationId;
                const allocationValue = btn.dataset.allocationValue;

                window.showConfirm?.(t('CancelAllocation'), async (inputValue) => {
                    const reason = String(inputValue || '').trim();
                    const result = await postJson(
                        `/identifiers/cancel/${allocationId}`,
                        { cancellationReason: reason }
                    );
                    if (!result.ok) {
                        handleFailure(result.res, result.payload, 'identifiersAlert');
                        return;
                    }
                    window.showToast?.(t('CancellationSucceeded'), 'success');
                    await load();
                }, {
                    entityName: allocationValue,
                    type: 'danger',
                    subtext: t('ConfirmCancelAllocation'),
                    confirmButtonText: t('CancelAllocation'),
                    showInput: true,
                    inputLabel: t('CancelAllocationReason'),
                    inputPlaceholder: t('CancelAllocationReason'),
                    inputRequired: true,
                    inputValidationMessage: t('ReasonRequiredError'),
                    inputValidator: (value) => String(value || '').trim() ? '' : t('ReasonRequiredError'),
                    inputAttributes: { maxlength: 1000 }
                });
            });
        };

        return {
            bind,
            ensureLoaded: () => { if (!loaded) void load(); },
            reload: () => void load(),
            invalidate: () => { loaded = false; }
        };
    })();

    // ── Lifecycle tab ────────────────────────────────────────────────────────
    const Lifecycle = (function () {
        let loaded = false;
        let state = null;
        let historyDt = null;

        // Backend Can* flag → target status. Order mirrors the SOP §6.2 progression.
        const TARGET_FLAGS = [
            { flag: 'canStartReview', status: 'InReview' },
            { flag: 'canReturnToDraft', status: 'Draft' },
            { flag: 'canMarkApprovedPendingEffective', status: 'ApprovedPendingEffective' },
            { flag: 'canMarkEffective', status: 'Effective' },
            { flag: 'canStartRevision', status: 'UnderRevision' },
            { flag: 'canSuspend', status: 'Suspended' },
            { flag: 'canMarkSuperseded', status: 'Superseded' },
            { flag: 'canRetire', status: 'Retired' }
        ];
        // Backend RequiresReason(): reason is mandatory for the states that stop or end use.
        const REASON_MANDATORY = ['Suspended', 'Retired', 'Superseded'];
        const STRUCTURAL_TARGETS = {
            Draft: ['InReview'],
            InReview: ['Draft', 'ApprovedPendingEffective'],
            ApprovedPendingEffective: ['Effective', 'Draft'],
            Effective: ['UnderRevision', 'Suspended', 'Retired'],
            UnderRevision: ['Effective', 'Superseded', 'Retired'],
            Suspended: ['Effective', 'Retired']
        };

        const allowedTargets = () => TARGET_FLAGS.filter((x) => state?.[x.flag] === true).map((x) => x.status);

        const setLifecycleActionAvailability = () => {
            const availability = {
                transitionLifecycle: allowedTargets().length > 0,
                markEffective: state?.canMarkEffective === true,
                supersedeLifecycle: state?.canMarkSuperseded === true,
                retireLifecycle: state?.canRetire === true
            };

            Object.entries(availability).forEach(([buttonName, enabled]) => {
                const button = historyDt?.button(`${buttonName}:name`);
                if (!button?.any?.()) return;
                button.enable(enabled);
                const node = button.node();
                if (node?.attr) node.attr('title', enabled ? '' : t('NoTransitionAvailable'));
            });
        };

        const renderState = () => {
            const s = state || {};
            const setHtml = (id, value) => {
                const element = document.getElementById(id);
                if (element) element.innerHTML = value;
            };
            setHtml('lifecycleCurrentStatus', lifecycleBadge(s.currentStatus));
            setHtml('lifecycleOperationalUse', boolBadge(s.operationalUseAllowed === true));
            setHtml('lifecycleLastTransition', esc(fmtIdentifierDateTime(s.lastTransitionAt)));
            setHtml('lifecycleTransitionedBy', text(s.lastTransitionBy));
            setHtml('lifecycleLastTransitionReason', text(s.statusReasonSummary));

            const targets = document.getElementById('lifecycleAllowedTargets');
            if (targets) {
                const list2 = allowedTargets();
                targets.innerHTML = list2.length
                    ? list2.map((x) => lifecycleBadge(x)).join(' ')
                    : `<span class="text-muted">${esc(t('NoTransitionAvailable'))}</span>`;
            }

            const warnBox = document.getElementById('lifecycleWarnings');
            if (warnBox) {
                const warnings = Array.isArray(s.warnings) ? s.warnings.filter(Boolean) : [];
                if (warnings.length) {
                    warnBox.innerHTML = `<strong>${esc(t('BackendWarnings'))}</strong><ul class="mb-0 mt-2">${warnings.map((w) => `<li>${esc(w)}</li>`).join('')}</ul>`;
                    warnBox.classList.remove('d-none');
                } else {
                    warnBox.classList.add('d-none');
                }
            }
            setLifecycleActionAvailability();
        };

        const renderHistory = (records) => {
            const table = document.getElementById('lifecycleHistoryTable');
            document.getElementById('lifecycleHistorySkeleton')?.classList.add('d-none');
            if (!table) return;

            if (historyDt) {
                historyDt.clear();
                historyDt.rows.add(records);
                historyDt.draw();
                return;
            }

            if (!window.DataTable || !window.DtDefaults?.create) return;

            const toolbarButtons = [{
                text: `<i class="icon-base bx bx-refresh icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${esc(t('ReloadTab'))}</span>`,
                className: 'btn btn-label-secondary',
                attr: { id: 'btnReloadLifecycle', title: t('ReloadTab') },
                action: () => void load()
            }];

            if (perms.canManageLifecycle) {
                toolbarButtons.push({
                    extend: 'collection',
                    text: `<i class="icon-base bx bx-transfer icon-sm"></i><span class="ms-2">${esc(t('TransitionLifecycle'))}</span>`,
                    className: 'btn btn-primary dropdown-toggle',
                    attr: {
                        id: 'btnLifecycleOperations',
                        title: t('TransitionLifecycle'),
                        'aria-haspopup': 'true'
                    },
                    buttons: [
                        {
                            name: 'transitionLifecycle',
                            text: `<i class="icon-base bx bx-transfer icon-sm"></i><span class="ms-2">${esc(t('TransitionLifecycle'))}</span>`,
                            className: 'dropdown-item',
                            action: () => openModal('transition')
                        },
                        {
                            name: 'markEffective',
                            text: `<i class="icon-base bx bx-check-circle icon-sm"></i><span class="ms-2">${esc(t('MarkEffective'))}</span>`,
                            className: 'dropdown-item',
                            action: () => openModal('effective')
                        },
                        {
                            name: 'supersedeLifecycle',
                            text: `<i class="icon-base bx bx-git-branch icon-sm"></i><span class="ms-2">${esc(t('Supersede'))}</span>`,
                            className: 'dropdown-item',
                            action: () => openModal('supersede')
                        },
                        {
                            name: 'retireLifecycle',
                            text: `<i class="icon-base bx bx-archive-out icon-sm"></i><span class="ms-2">${esc(t('Retire'))}</span>`,
                            className: 'dropdown-item',
                            action: () => openModal('retire')
                        }
                    ]
                });
            }

            historyDt = new DataTable(table, window.DtDefaults.create({
                data: records,
                stateSave: false,
                colReorder: { columns: ':gt(0)' },
                paging: true,
                lengthChange: true,
                searching: true,
                info: true,
                order: [[3, 'desc']],
                language: {
                    emptyTable: t('NoLifecycleHistoryFound'),
                    zeroRecords: t('NoLifecycleHistoryFound')
                },
                columns: [
                    { data: null, name: 'control', defaultContent: '' },
                    { data: 'fromStatus', name: 'fromStatus' },
                    { data: 'toStatus', name: 'toStatus' },
                    { data: 'performedAt', name: 'performedAt' },
                    { data: 'performedBy', name: 'performedBy' },
                    { data: 'transitionReason', name: 'transitionReason' },
                    { data: 'comment', name: 'comment' },
                    { data: 'evidenceReference', name: 'evidenceReference' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => lifecycleBadge(data) },
                    { targets: 2, responsivePriority: 1, render: (data) => lifecycleBadge(data) },
                    {
                        targets: 3,
                        render: (data, type) => type === 'display' ? esc(fmtIdentifierDateTime(data)) : (data || '')
                    },
                    { targets: 4, render: (data) => text(data) },
                    { targets: 5, render: (data) => text(data) },
                    { targets: 6, render: (data) => text(data) },
                    { targets: 7, render: (data) => text(data) }
                ],
                buttons: window.DtDefaults.exportButtons(
                    null,
                    {},
                    toolbarButtons,
                    { exportColumns: [1, 2, 3, 4, 5, 6, 7], colvisColumns: [1, 2, 3, 4, 5, 6, 7] }
                ),
                drawCallback: function () {
                    window.DtDefaults?.updateVisualState?.(this.api(), 0);
                },
                initComplete: function () {
                    window.DtDefaults?.updateVisualState?.(this.api(), 0);
                }
            }));
        };

        const loadReplacementRegisterEntries = async () => {
            const select = document.getElementById('lifecycleReplacementEntryId');
            const offcanvasEl = document.getElementById('lifecycleTransitionOffcanvas');
            if (!select || !offcanvasEl) return;

            select.innerHTML = '<option value=""></option>';
            const loadStatus = async (status) => {
                const res = await fetch(
                    `/DocumentManagement/MasterRegister/api/list?lifecycleStatus=${encodeURIComponent(status)}`,
                    { credentials: 'same-origin', headers: getAuthHeaders() }
                );
                const payload = await res.json().catch(() => ({}));
                return { res, payload, ok: res.ok && payload?.isSuccessful !== false };
            };

            const results = await Promise.all([loadStatus('Effective'), loadStatus('UnderRevision')]);
            const failed = results.find((result) => !result.ok);
            if (failed) {
                handleFailure(failed.res, failed.payload, 'lifecycleAlert');
                return;
            }

            const candidates = new Map();
            results.flatMap((result) => unwrapList(result.payload)).forEach((entry) => {
                const id = entry.id || entry.Id;
                if (!id || String(id).toLowerCase() === String(entryId).toLowerCase()) return;
                candidates.set(String(id).toLowerCase(), entry);
            });

            [...candidates.values()]
                .sort((a, b) => String(a.documentTitle || a.DocumentTitle || '')
                    .localeCompare(String(b.documentTitle || b.DocumentTitle || '')))
                .forEach((entry) => {
                    const option = document.createElement('option');
                    option.value = entry.id || entry.Id;
                    const title = entry.documentTitle || entry.DocumentTitle || option.value;
                    const code = entry.documentCode || entry.DocumentCode || na();
                    const uid = entry.permanentUid || entry.PermanentUid || na();
                    option.textContent = `${title} — ${code} — ${uid}`;
                    select.appendChild(option);
                });

            if (window.jQuery?.fn?.select2) {
                const $select = window.jQuery(select);
                if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
                $select.select2({
                    dropdownParent: window.jQuery(offcanvasEl),
                    width: '100%',
                    allowClear: true,
                    placeholder: t('ReplacementRegisterEntry')
                });
            }
        };

        const load = async () => {
            hideAlert('lifecycleAlert');
            if (!historyDt) document.getElementById('lifecycleHistorySkeleton')?.classList.remove('d-none');

            const [stateRes, historyRes] = await Promise.all([getJson('/lifecycle/state'), getJson('/lifecycle/history')]);

            if (stateRes.ok) state = unwrap(stateRes.payload);
            else handleFailure(stateRes.res, stateRes.payload, 'lifecycleAlert');

            if (historyRes.ok) renderHistory(unwrapList(historyRes.payload));
            else { renderHistory([]); handleFailure(historyRes.res, historyRes.payload, 'lifecycleAlert'); }

            renderState();
            loaded = true;
        };

        const openModal = async (mode) => {
            const offcanvasEl = document.getElementById('lifecycleTransitionOffcanvas');
            if (!offcanvasEl) return;

            const titleKey = { effective: 'MarkEffective', supersede: 'Supersede', retire: 'Retire', transition: 'TransitionLifecycle' }[mode];
            document.getElementById('lifecycleModalMode').value = mode;
            document.getElementById('lifecycleModalTitle').textContent = t(titleKey);
            document.getElementById('lifecycleModalSubmit').textContent = t(titleKey);
            document.getElementById('lifecycleTransitionForm').reset();
            document.getElementById('lifecycleTransitionForm').classList.remove('was-validated');

            const warning = document.getElementById('lifecycleModalWarning');
            const warningKey = { effective: 'ConfirmMarkEffective', supersede: 'ConfirmSupersede', retire: 'ConfirmRetire' }[mode];
            if (warningKey) {
                warning.innerHTML = `${esc(t(warningKey))}<br><small>${esc(t('MarkEffectiveWarning'))}</small>`;
                warning.classList.remove('d-none');
            } else {
                warning.classList.add('d-none');
            }

            // Generic transition: offer only the targets the backend currently allows.
            const targetWrapper = document.getElementById('lifecycleTargetWrapper');
            const targetSelect = document.getElementById('lifecycleTargetStatus');
            if (mode === 'transition') {
                const allowed = new Set(allowedTargets());
                const candidates = STRUCTURAL_TARGETS[state?.currentStatus] || allowedTargets();
                targetSelect.innerHTML = candidates.map((s) =>
                    `<option value="${esc(s)}"${allowed.has(s) ? '' : ' disabled'}>${esc(t(`Lifecycle${s}`))}${allowed.has(s) ? '' : ` — ${esc(t('NoTransitionAvailable'))}`}</option>`
                ).join('');
                targetSelect.required = true;
                targetWrapper.classList.remove('d-none');
            } else {
                targetSelect.innerHTML = '';
                targetSelect.required = false;
                targetWrapper.classList.add('d-none');
            }

            const effectiveMode = mode === 'effective';
            document.getElementById('lifecycleEffectiveDateWrapper').classList.toggle('d-none', !effectiveMode);
            const replacementMode = mode === 'supersede' || effectiveMode;
            document.getElementById('lifecycleReplacementWrapper').classList.toggle('d-none', !replacementMode);
            if (replacementMode) await loadReplacementRegisterEntries();

            syncReasonRequirement();
            targetSelect.onchange = syncReasonRequirement;
            targetSelect.dispatchEvent(new Event('change', { bubbles: true }));
            window.bootstrap?.Offcanvas.getOrCreateInstance(offcanvasEl).show();
        };

        // Mirrors the backend rule rather than forcing a reason everywhere: required for stop/end states, optional
        // otherwise. The backend still has the final say (REASON_REQUIRED).
        const syncReasonRequirement = () => {
            const mode = document.getElementById('lifecycleModalMode').value;
            const target = mode === 'transition'
                ? document.getElementById('lifecycleTargetStatus').value
                : { effective: 'Effective', supersede: 'Superseded', retire: 'Retired' }[mode];
            const required = REASON_MANDATORY.includes(target);
            document.getElementById('lifecycleReason').required = required;
            document.getElementById('lifecycleReasonRequiredMark').classList.toggle('d-none', !required);
        };

        const submit = async (event) => {
            event.preventDefault();
            const form = event.currentTarget;
            if (!form.checkValidity()) { form.classList.add('was-validated'); return; }

            const mode = document.getElementById('lifecycleModalMode').value;
            const payload = {
                reason: document.getElementById('lifecycleReason').value.trim() || null,
                comment: document.getElementById('lifecycleComment').value.trim() || null,
                evidenceReference: document.getElementById('lifecycleEvidenceReference').value.trim() || null
            };

            const effectiveDate = document.getElementById('lifecycleEffectiveDate').value;
            if (mode === 'effective' && effectiveDate) payload.effectiveDate = new Date(`${effectiveDate}T00:00:00`).toISOString();

            const replacement = document.getElementById('lifecycleReplacementEntryId').value.trim();
            if (replacement) payload.relatedReplacementRegisterEntryId = replacement;

            // For the pinned routes the target status is set SERVER-SIDE by the proxy; only the generic route sends it.
            const path = {
                effective: '/lifecycle/mark-effective',
                supersede: '/lifecycle/supersede',
                retire: '/lifecycle/retire',
                transition: '/lifecycle/transition'
            }[mode];
            if (mode === 'transition') payload.targetStatus = document.getElementById('lifecycleTargetStatus').value;

            const result = await postJson(path, payload, event.submitter);
            if (!result.ok) {
                handleFailure(result.res, result.payload, 'lifecycleAlert');
                return;
            }

            window.bootstrap?.Offcanvas
                .getOrCreateInstance(document.getElementById('lifecycleTransitionOffcanvas'))
                .hide();
            window.showToast?.(t('LifecycleTransitionSucceeded'), 'success');
            await load();
            Identifiers.invalidate();
        };

        const bind = () => {
            document.getElementById('lifecycleTransitionForm')?.addEventListener('submit', submit);
        };

        return {
            bind,
            ensureLoaded: () => { if (!loaded) void load(); },
            invalidate: () => { loaded = false; }
        };
    })();

    // ── Shared: user picker options (used by both FU26 evidence modals) ──────
    let userOptionsHtml = null;
    const loadUserOptions = async () => {
        if (userOptionsHtml !== null) return userOptionsHtml;
        try {
            const res = await fetch(`${endpoint}/users`, { credentials: 'same-origin', headers: getAuthHeaders() });
            const payload = await res.json().catch(() => ({}));
            const users = unwrapList(payload);
            userOptionsHtml = ['<option value=""></option>'].concat(users.map((u) => {
                const id = u?.id ?? u?.Id;
                const full = `${u?.firstName ?? u?.FirstName ?? ''} ${u?.lastName ?? u?.LastName ?? ''}`.trim();
                const email = u?.email ?? u?.Email ?? '';
                const label = full && email ? `${full} (${email})` : (full || email || id);
                return id ? `<option value="${esc(id)}">${esc(label)}</option>` : '';
            })).join('');
        } catch (error) {
            console.error('[MasterRegister FU26] User list could not be loaded.', error);
            userOptionsHtml = '<option value=""></option>';
        }
        return userOptionsHtml;
    };

    const summaryCard = (colour, icon, value, label) => `
        <div class="col-6 col-lg-3">
            <div class="card h-100">
                <div class="card-body d-flex align-items-center gap-3">
                    <div class="avatar"><span class="avatar-initial rounded bg-label-${colour}"><i class="bx ${icon}"></i></span></div>
                    <div>
                        <h5 class="mb-0">${esc(String(value ?? 0))}</h5>
                        <small class="text-muted">${esc(label)}</small>
                    </div>
                </div>
            </div>
        </div>`;

    // ── Approval tab (FU09) ──────────────────────────────────────────────────
    const Approval = (function () {
        let loaded = false;
        let readiness = null;
        let requirements = [];
        let requirementsDt = null;
        let authRoleCatalog = null;
        let approvalDefaultViewRecord = null;
        let approvalDefaultViewState = null;
        let approvalSaveViewArmed = false;
        const approvalPersonalization = window.personalizationClient;
        const approvalPersonalizationContext = { moduleKey: 'DocumentManagement', pageKey: 'MasterRegisterApproval' };
        const approvalViewColumns = [1, 2, 3, 4, 5, 6, 7];
        const approvalBaseOrder = [[1, 'asc']];

        const requirementStatusBadge = (value) => {
            const key = String(value || '');
            const map = { Pending: 'warning', Completed: 'success', Rejected: 'danger', Waived: 'secondary', Blocked: 'danger' };
            return key ? badge(map[key] || 'secondary', t(`RequirementStatus${key}`)) : na();
        };
        const roleLabel = (v) => (v ? t(`ApprovalRole${v}`) : na());
        const typeLabel = (v) => (v ? t(`RequirementType${v}`) : na());
        const sourceRuleLabel = (v) => (v ? t(`SourceRule${v}`) : na());

        const renderSummary = () => {
            const r = readiness || {};
            const host2 = document.getElementById('approvalSummaryCards');
            if (!host2) return;
            const missing = Array.isArray(r.missingMandatoryRoles) ? r.missingMandatoryRoles.length : 0;
            const segFailures = Array.isArray(r.segregationFailures) ? r.segregationFailures.length : 0;
            const statusColour = { Complete: 'success', Pending: 'warning', Rejected: 'danger', Blocked: 'danger', NotRequired: 'secondary' }[r.approvalEvidenceStatus] || 'secondary';
            host2.innerHTML = [
                summaryCard(statusColour, 'bx-badge-check', r.completedCount, `${t('ApprovalEvidenceStatus')}: ${t(`ApprovalState${r.approvalEvidenceStatus || 'NotRequired'}`)}`),
                summaryCard('primary', 'bx-list-ul', r.requiredCount, t('RequiredApprovers')),
                summaryCard(missing > 0 ? 'danger' : 'success', 'bx-user-x', missing, t('MissingMandatoryEvidence')),
                summaryCard((r.blockedCount || segFailures) ? 'danger' : 'success', 'bx-block', (r.blockedCount || 0) + segFailures, t('BlockingIssues'))
            ].join('');
        };

        const renderSegregation = () => {
            const r = readiness || {};
            const failures = Array.isArray(r.segregationFailures) ? r.segregationFailures.filter(Boolean) : [];
            const missing = Array.isArray(r.missingMandatoryRoles) ? r.missingMandatoryRoles.filter(Boolean) : [];

            // The author-is-sole-approver rule is a backend segregation failure string; surface it prominently.
            const alertBox = document.getElementById('approvalSegregationAlert');
            if (alertBox) {
                if (failures.length) {
                    const soleApproverFailure = failures.find((failure) =>
                        String(failure).toLowerCase().includes('author is the sole approver'));
                    const remainingFailures = failures.filter((failure) => failure !== soleApproverFailure);
                    alertBox.className = 'alert alert-danger';
                    alertBox.innerHTML = `<strong>${esc(t('SegregationFailed'))}</strong>`
                        + (soleApproverFailure ? `<div class="small mt-1">${esc(t('AuthorSoleApproverBlocked'))}</div>` : '')
                        + (remainingFailures.length
                            ? `<ul class="mb-0 mt-2">${remainingFailures.map((f) => `<li>${esc(f)}</li>`).join('')}</ul>`
                            : '');
                    alertBox.classList.remove('d-none');
                } else {
                    alertBox.classList.add('d-none');
                    alertBox.innerHTML = '';
                }
            }

            const blocking = document.getElementById('approvalBlockingList');
            const blockingAlert = document.getElementById('approvalBlockingAlert');
            if (blocking) {
                const items = failures.map((f) => ({ colour: 'danger', text: f }))
                    .concat(missing.map((m) => ({ colour: 'warning', text: `${t('MissingMandatoryEvidence')}: ${roleLabel(m)}` })));
                blocking.innerHTML = items.length
                    ? `<ul class="list-unstyled mb-0">${items.map((i) =>
                        `<li class="mb-2"><i class="bx bx-x-circle text-${i.colour} me-2"></i>${esc(i.text)}</li>`).join('')}</ul>`
                    : `<div class="text-success"><i class="bx bx-check-circle me-2"></i>${esc(t('NoBlockingIssues'))}</div>`;
                blockingAlert?.classList.remove('d-none');
                blockingAlert?.classList.toggle('alert-warning', items.length > 0);
                blockingAlert?.classList.toggle('alert-success', items.length === 0);
            }
        };

        const filterRegex = (values) => values.length
            ? `^(${values.map((value) => String(value).replace(/[.*+?^${}()|[\]\\]/g, '\\$&')).join('|')})$`
            : '';

        const selectedValues = (id) => Array.from(document.getElementById(id)?.selectedOptions || [], (o) => o.value).filter(Boolean);
        const approvalFilterState = () => ({
            type: selectedValues('approvalFilterType'),
            mandatory: document.getElementById('approvalFilterMandatory')?.value || '',
            status: selectedValues('approvalFilterStatus')
        });
        const emptyApprovalView = () => ({
            filters: { type: [], mandatory: '', status: [] },
            search: '',
            colVis: approvalViewColumns.reduce((state, index) => { state[index] = true; return state; }, {}),
            columnOrder: Array.from({ length: 9 }, (_, index) => index),
            order: approvalBaseOrder
        });
        const normalizeApprovalView = (view) => {
            const baseline = emptyApprovalView();
            const filters = view?.filters || {};
            return {
                filters: {
                    type: Array.isArray(filters.type) ? [...new Set(filters.type.filter(Boolean))].sort() : [],
                    mandatory: String(filters.mandatory || ''),
                    status: Array.isArray(filters.status) ? [...new Set(filters.status.filter(Boolean))].sort() : []
                },
                search: String(view?.search || '').trim(),
                colVis: Object.assign({}, baseline.colVis, view?.colVis || {}),
                columnOrder: Array.isArray(view?.columnOrder) && view.columnOrder.length === 9
                    ? view.columnOrder.map(Number)
                    : baseline.columnOrder,
                order: Array.isArray(view?.order) ? view.order : baseline.order
            };
        };
        const approvalCurrentView = (api) => {
            const colVis = {};
            approvalViewColumns.forEach((index) => { colVis[index] = !!api.column(index).visible(); });
            let columnOrder = Array.from({ length: 9 }, (_, index) => index);
            try { columnOrder = api.colReorder?.order?.() || columnOrder; } catch (error) { }
            const searchInput = api.table().container().querySelector('.dt-search input');
            return normalizeApprovalView({
                filters: approvalFilterState(),
                search: searchInput?.value || api.search(),
                colVis,
                columnOrder,
                order: api.order()
            });
        };
        const serializeApprovalView = (view) => JSON.stringify(normalizeApprovalView(view));
        const approvalSavedViewId = (view) => view?.id || view?.Id || view?._id || null;
        const approvalSavedViewName = (view) => view?.viewName || view?.ViewName || '';
        const approvalSavedViewDefinition = (view) => {
            const raw = view?.viewDefinition ?? view?.ViewDefinition ?? {};
            if (typeof raw !== 'string') return raw || {};
            try { return JSON.parse(raw); } catch (error) { return {}; }
        };
        const setApprovalSaveViewVisible = (visible) => {
            const button = document.querySelector('#approvalRequirementsTable_wrapper .dt-save-filter-btn');
            button?.classList.toggle('d-none', !visible);
            window.DtDefaults?.refreshButtonGroupRadii?.();
        };
        const approvalViewIsDirty = (api) => serializeApprovalView(approvalCurrentView(api))
            !== serializeApprovalView(approvalDefaultViewState || emptyApprovalView());
        const loadApprovalDefaultView = async () => {
            approvalDefaultViewRecord = null;
            approvalDefaultViewState = null;
            if (!approvalPersonalization?.getViews) return;
            try {
                const response = await approvalPersonalization.getViews(
                    approvalPersonalizationContext.moduleKey,
                    approvalPersonalizationContext.pageKey);
                const views = Array.isArray(response) ? response : (response?.data || response?.Data || []);
                approvalDefaultViewRecord = views.find((view) => view?.isDefault === true || view?.IsDefault === true)
                    || views[0]
                    || null;
                approvalDefaultViewState = approvalDefaultViewRecord
                    ? normalizeApprovalView(approvalSavedViewDefinition(approvalDefaultViewRecord))
                    : null;
            } catch (error) {
                if (!error?.authHandled) console.error('[MasterRegister Approval SaveView] Saved view could not be loaded.', error);
            }
        };
        const saveApprovalDefaultView = async (api) => {
            if (!approvalPersonalization?.saveView) return;
            const viewDefinition = approvalCurrentView(api);
            const payload = {
                moduleKey: approvalPersonalizationContext.moduleKey,
                pageKey: approvalPersonalizationContext.pageKey,
                viewName: (approvalSavedViewName(approvalDefaultViewRecord) || t('SaveView') || 'Default').trim(),
                viewDefinition,
                isDefault: true,
                visibility: 'private'
            };
            const existingId = approvalSavedViewId(approvalDefaultViewRecord);
            const response = existingId
                ? await approvalPersonalization.updateView(existingId, payload)
                : await approvalPersonalization.saveView(payload);
            approvalDefaultViewRecord = response?.data || response?.Data || response || payload;
            approvalDefaultViewState = viewDefinition;
        };

        const syncMultiSelectSummary = ($select) => {
            const $container = $select.next('.select2-container');
            const $rendered = $container.find('.select2-selection__rendered');
            const $selection = $container.find('.select2-selection--multiple');
            if (!$container.length || !$rendered.length || !$selection.length) return;

            let $summary = $selection.find('.dt-inline-filter-multi__summary');
            let $actions = $selection.find('.dt-inline-filter-multi__actions');
            let $count = $selection.find('.dt-inline-filter-multi__count');
            let $arrow = $selection.find('.select2-selection__arrow');
            if (!$summary.length) $summary = window.jQuery('<span class="dt-inline-filter-multi__summary"></span>').prependTo($selection);
            if (!$actions.length) $actions = window.jQuery('<span class="dt-inline-filter-multi__actions"></span>').appendTo($selection);
            if (!$count.length) $count = window.jQuery('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>').appendTo($actions);
            if (!$arrow.length) $arrow = window.jQuery('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>').appendTo($selection);

            const placeholder = String($select.data('placeholder') || '');
            const values = $select.val() || [];
            const texts = ($select.select2('data') || []).map((item) => String(item.text || '').trim()).filter(Boolean);
            $summary.text(placeholder);
            $rendered.attr('title', texts.join(', ') || placeholder);
            $container.toggleClass('dt-inline-filter-multi--has-value', values.length > 0);
            $count.toggleClass('d-none', values.length === 0).text(String(values.length));
            $actions.find('.dt-multi-clear-btn').remove();
            if (values.length) {
                const $clear = window.jQuery(`<span class="dt-multi-clear-btn" role="button" aria-label="${esc(t('Reset'))}" title="${esc(t('Reset'))}">&times;</span>`);
                $clear.on('mousedown', (event) => {
                    event.preventDefault();
                    event.stopPropagation();
                    $select.val(null).trigger('change');
                });
                $actions.append($clear);
            }
        };

        const initApprovalFilters = () => {
            if (!window.jQuery?.fn?.select2) return;
            const $body = window.jQuery(document.body);
            ['approvalFilterType', 'approvalFilterStatus'].forEach((id) => {
                const $select = window.jQuery(`#${id}`);
                if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
                $select.select2({
                    dropdownParent: $body,
                    dropdownCssClass: 'dt-inline-filter-dropdown',
                    containerCssClass: 'dt-inline-filter-multi',
                    selectionCssClass: 'form-select form-select-sm',
                    placeholder: $select.data('placeholder') || '',
                    minimumResultsForSearch: Infinity,
                    width: 'element',
                    closeOnSelect: false
                });
                $select.on('change.approval-summary', () => syncMultiSelectSummary($select));
                requestAnimationFrame(() => syncMultiSelectSummary($select));
            });
            const $mandatory = window.jQuery('#approvalFilterMandatory');
            if ($mandatory.hasClass('select2-hidden-accessible')) $mandatory.select2('destroy');
            $mandatory.select2({
                dropdownParent: $body,
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $mandatory.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element',
                allowClear: true
            });
        };

        const mountApprovalFilter = () => {
            const host = document.getElementById('approvalRequirementsFilterHost');
            const button = document.querySelector('#approvalRequirementsTable_wrapper .dt-filter-btn');
            const toolbarRow = button?.closest('.dt-layout-row')
                || button?.closest('.row')
                || button?.closest('.dt-layout-end')?.parentElement;
            if (host && toolbarRow) {
                toolbarRow.insertAdjacentElement('afterend', host);
                host.classList.remove('px-6');
                host.classList.add('px-3');
            }

            const collapse = document.getElementById('approvalRequirementsFilterCollapse');
            if (button && collapse && !button.dataset.bound) {
                button.dataset.bound = '1';
                collapse.addEventListener('shown.bs.collapse', () => button.setAttribute('aria-expanded', 'true'));
                collapse.addEventListener('hidden.bs.collapse', () => button.setAttribute('aria-expanded', 'false'));
            }
        };

        const applyFilters = () => {
            if (!requirementsDt) return;
            requirementsDt.column(2).search(filterRegex(selectedValues('approvalFilterType')), true, false);
            const mandatory = document.getElementById('approvalFilterMandatory')?.value || '';
            requirementsDt.column(3).search(mandatory ? `^${mandatory}$` : '', true, false);
            requirementsDt.column(5).search(filterRegex(selectedValues('approvalFilterStatus')), true, false);
            requirementsDt.draw();
            const collapse = document.getElementById('approvalRequirementsFilterCollapse');
            if (collapse) window.bootstrap?.Collapse.getOrCreateInstance(collapse, { toggle: false }).hide();
        };

        const populateFilter = (id, values, label) => {
            const select = document.getElementById(id);
            if (!select) return;
            const selected = new Set(Array.from(select.selectedOptions, (o) => o.value));
            select.innerHTML = [...new Set(values.filter(Boolean))].sort().map((value) =>
                `<option value="${esc(value)}"${selected.has(value) ? ' selected' : ''}>${esc(label(value))}</option>`).join('');
        };

        const loadAuthRoleCatalog = async () => {
            if (authRoleCatalog !== null) return authRoleCatalog;
            const result = await getJson('/approval/roles');
            authRoleCatalog = result.ok ? unwrapList(result.payload) : [];
            return authRoleCatalog;
        };

        const renderRequirements = () => {
            const table = document.getElementById('approvalRequirementsTable');
            if (!table || !window.DataTable || !window.DtDefaults?.create) return;

            populateFilter('approvalFilterType', requirements.map((q) => q.requirementType), typeLabel);
            populateFilter('approvalFilterStatus', requirements.map((q) => q.status), (v) => t(`RequirementStatus${v}`));

            if (requirementsDt) {
                requirementsDt.clear();
                requirementsDt.rows.add(requirements);
                requirementsDt.draw();
                return;
            }

            const filterButton = {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: {
                    title: t('Filter'),
                    'aria-label': t('Filter'),
                    'aria-controls': 'approvalRequirementsFilterCollapse',
                    'aria-expanded': 'false',
                    'data-bs-toggle': 'tooltip'
                },
                action: () => {
                    const el = document.getElementById('approvalRequirementsFilterCollapse');
                    const instance = window.bootstrap?.Collapse.getOrCreateInstance(el, { toggle: false });
                    instance?.toggle();
                }
            };
            const saveViewButton = {
                text: `<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${esc(t('SaveView'))}</span>`,
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: t('SaveView'), 'data-bs-toggle': 'tooltip' },
                action: async (event, api) => {
                    try {
                        await saveApprovalDefaultView(api || requirementsDt);
                        setApprovalSaveViewVisible(false);
                        window.showToast?.(t('RecordSaved'), 'success');
                    } catch (error) {
                        if (error?.authHandled) return;
                        console.error('[MasterRegister Approval SaveView] Saved view could not be stored.', error);
                        window.showToast?.(t('ErrorOccurred'), 'error');
                    }
                }
            };

            requirementsDt = new DataTable(table, window.DtDefaults.create({
                data: requirements,
                stateSave: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                paging: true,
                lengthChange: true,
                searching: true,
                info: true,
                order: [[1, 'asc']],
                language: {
                    emptyTable: t('NoApprovalRequirementsFound'),
                    zeroRecords: t('NoApprovalRequirementsFound')
                },
                columns: [
                    { data: null, name: 'control', defaultContent: '' },
                    { data: 'requiredRole', name: 'requiredRole' },
                    { data: 'requirementType', name: 'requirementType' },
                    { data: 'isMandatory', name: 'isMandatory' },
                    { data: 'sourceRule', name: 'sourceRule' },
                    { data: 'status', name: 'status' },
                    { data: 'completedByRole', name: 'completedByRole' },
                    { data: 'evidenceReference', name: 'evidenceReference' },
                    { data: null, name: 'actions', defaultContent: '' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    {
                        targets: 1,
                        responsivePriority: 1,
                        render: (data, type, q) => type === 'display'
                            ? `<span class="fw-medium text-heading">${esc(q.requiredRoleDisplayName || roleLabel(data))}</span>${q.isNonDelegable ? `<br><span class="badge bg-label-dark mt-1">${esc(t('NonDelegable'))}</span>` : ''}`
                            : (q.requiredRoleDisplayName || roleLabel(data))
                    },
                    { targets: 2, render: (data, type) => type === 'display' ? esc(typeLabel(data)) : (data || '') },
                    { targets: 3, render: (data, type) => type === 'display' ? (data ? badge('danger', t('Mandatory')) : badge('secondary', t('Optional'))) : String(!!data) },
                    { targets: 4, render: (data, type) => type === 'display' ? esc(sourceRuleLabel(data)) : (data || '') },
                    { targets: 5, render: (data, type) => type === 'display' ? requirementStatusBadge(data) : (data || '') },
                    {
                        targets: 6,
                        render: (data, type, q) => type === 'display'
                            ? `${text(data ? roleLabel(data) : null)}<br><small class="text-muted">${esc(fmtDateTime(q.completedAt))}</small>`
                            : (data ? roleLabel(data) : '')
                    },
                    { targets: 7, render: (data, type) => type === 'display' ? text(data) : (data || '') },
                    {
                        targets: 8,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (data, type, q) => {
                            if (type !== 'display') return '';
                            if (!perms.canRecordApprovalEvidence || q.status !== 'Pending') {
                                return `<span class="text-muted small">${esc(t('ActionNotAvailable'))}</span>`;
                            }
                            return window.DitenDataTable.renderActions([
                                {
                                    className: 'js-record-approval me-1',
                                    icon: 'bx bx-check-shield',
                                    attrs: {
                                        title: t('RecordEvidence'),
                                        'aria-label': t('RecordEvidence'),
                                        'data-requirement-id': q.id,
                                        'data-role': q.requiredRole,
                                        'data-key': q.requirementKey
                                    }
                                },
                                {
                                    className: 'js-reject-approval text-danger',
                                    icon: 'bx bx-x-circle',
                                    text: t('RejectEvidence'),
                                    attrs: {
                                        'data-requirement-id': q.id,
                                        'data-role': q.requiredRole,
                                        'data-key': q.requirementKey
                                    }
                                }
                            ]);
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    null,
                    {},
                    { filterBtn: filterButton, saveFilterBtn: saveViewButton },
                    { exportColumns: [1, 2, 3, 4, 5, 6, 7], colvisColumns: [1, 2, 3, 4, 5, 6, 7] }
                ),
                drawCallback: function () {
                    window.DtDefaults.updateVisualState(this.api(), selectedValues('approvalFilterType').length
                        + selectedValues('approvalFilterMandatory').length
                        + selectedValues('approvalFilterStatus').length);
                },
                initComplete: function () {
                    document.getElementById('approvalRequirementsSkeleton')?.classList.add('d-none');
                    mountApprovalFilter();
                    initApprovalFilters();
                    const api = this.api();
                    const saved = normalizeApprovalView(approvalDefaultViewState || emptyApprovalView());
                    window.jQuery('#approvalFilterType').val(saved.filters.type).trigger('change');
                    window.jQuery('#approvalFilterMandatory').val(saved.filters.mandatory).trigger('change');
                    window.jQuery('#approvalFilterStatus').val(saved.filters.status).trigger('change');
                    api.column(2).search(filterRegex(saved.filters.type), true, false);
                    api.column(3).search(saved.filters.mandatory ? `^${saved.filters.mandatory}$` : '', true, false);
                    api.column(5).search(filterRegex(saved.filters.status), true, false);
                    api.search(saved.search);
                    approvalViewColumns.forEach((index) => api.column(index).visible(saved.colVis[index] !== false, false));
                    try { api.colReorder?.order?.(saved.columnOrder, true); } catch (error) { }
                    api.order(saved.order).draw(false);
                    setTimeout(() => { approvalSaveViewArmed = true; }, 0);
                }
            }));
            requirementsDt.on('column-visibility.dt search.dt order.dt column-reorder.dt columns-reordered.dt', () => {
                if (approvalSaveViewArmed) setApprovalSaveViewVisible(approvalViewIsDirty(requirementsDt));
            });
        };

        const load = async () => {
            hideAlert('approvalAlert');
            if (!requirementsDt) document.getElementById('approvalRequirementsSkeleton')?.classList.remove('d-none');

            const [reqRes, readyRes] = await Promise.all([
                getJson('/approval/requirements'),
                getJson('/approval/readiness')
            ]);

            if (reqRes.ok) requirements = unwrapList(reqRes.payload);
            else { requirements = []; handleFailure(reqRes.res, reqRes.payload, 'approvalAlert'); }

            if (readyRes.ok) readiness = unwrap(readyRes.payload);
            else handleFailure(readyRes.res, readyRes.payload, 'approvalAlert');

            if (!requirementsDt) await loadApprovalDefaultView();
            renderSummary();
            renderSegregation();
            renderRequirements();
            loaded = true;
        };

        const openEvidenceModal = async (mode, btn) => {
            const offcanvasEl = document.getElementById('approvalEvidenceOffcanvas');
            if (!offcanvasEl) return;
            const form = document.getElementById('approvalEvidenceForm');
            form.reset();
            form.classList.remove('was-validated');

            const requirementRole = btn.dataset.role || '';
            document.getElementById('approvalRequirementId').value = btn.dataset.requirementId;
            document.getElementById('approvalEvidenceMode').value = mode;
            document.getElementById('approvalRequirementLabel').value = `${roleLabel(requirementRole)} — ${btn.dataset.key}`;

            const isReject = mode === 'reject';
            document.getElementById('approvalEvidenceModalTitle').textContent = isReject ? t('RejectEvidence') : t('RecordEvidence');
            document.getElementById('approvalEvidenceSubmit').textContent = isReject ? t('RejectEvidence') : t('RecordEvidence');
            document.getElementById('approvalEvidenceModalNote').textContent = isReject ? t('RejectEvidenceNote') : t('RecordEvidenceNote');
            document.getElementById('approvalActionWrapper').classList.toggle('d-none', isReject);
            document.getElementById('approvalRejectionReasonWrapper').classList.toggle('d-none', !isReject);
            document.getElementById('approvalRejectionReason').required = isReject;
            document.getElementById('approvalAction').required = !isReject;

            const select = document.getElementById('approvalPerformedByUserId');
            select.innerHTML = currentUserId
                ? `<option value="${esc(currentUserId)}" selected>${esc(currentUserDisplayName)}</option>`
                : '<option value=""></option>';
            select.disabled = true;

            const normalizeRole = (value) => String(value || '').replace(/[^a-z0-9]/gi, '').toLowerCase();
            const isDocumentOwner = normalizeRole(requirementRole) === 'documentowner';
            const roleWrapper = document.getElementById('approvalPerformedByRoleWrapper');
            const roleSelect = document.getElementById('approvalPerformedByRole');
            const roleValue = document.getElementById('approvalPerformedByRoleValue');
            roleWrapper?.classList.toggle('d-none', isDocumentOwner);

            if (isDocumentOwner) {
                roleSelect.innerHTML = '';
                roleValue.value = requirementRole;
            } else {
                const roles = await loadAuthRoleCatalog();
                const requiredLabel = roleLabel(requirementRole);
                const matchingRole = roles.find((role) =>
                    normalizeRole(role?.name ?? role?.Name) === normalizeRole(requirementRole)
                    || normalizeRole(role?.displayName ?? role?.DisplayName) === normalizeRole(requiredLabel));
                const roleName = matchingRole?.name ?? matchingRole?.Name ?? requirementRole;
                const roleDisplayName = matchingRole?.displayName ?? matchingRole?.DisplayName ?? requiredLabel;
                roleSelect.innerHTML = `<option value="${esc(roleName)}" selected>${esc(roleDisplayName)}</option>`;
                roleSelect.value = roleName;
                roleValue.value = roleName;
            }

            window.bootstrap?.Offcanvas.getOrCreateInstance(offcanvasEl).show();
        };

        const submitEvidence = async (event) => {
            event.preventDefault();
            const form = event.currentTarget;
            if (!form.checkValidity()) { form.classList.add('was-validated'); return; }

            const mode = document.getElementById('approvalEvidenceMode').value;
            const isReject = mode === 'reject';
            const payload = {
                requirementId: document.getElementById('approvalRequirementId').value,
                performedByUserId: currentUserId,
                performedByRole: document.getElementById('approvalPerformedByRoleValue').value,
                evidenceReference: document.getElementById('approvalEvidenceReference').value.trim() || null,
                comment: document.getElementById('approvalEvidenceComment').value.trim() || null
            };
            if (isReject) payload.reason = document.getElementById('approvalRejectionReason').value.trim();
            else payload.action = document.getElementById('approvalAction').value;

            const path = isReject ? '/approval/evidence/reject' : '/approval/evidence/record';
            const result = await postJson(path, payload, form.querySelector('[type="submit"]'));
            if (!result.ok) {
                handleFailure(result.res, result.payload, 'approvalAlert');
                return;
            }

            window.bootstrap?.Offcanvas.getOrCreateInstance(document.getElementById('approvalEvidenceOffcanvas')).hide();
            window.showToast?.(t(isReject ? 'EvidenceRejectSucceeded' : 'EvidenceRecordSucceeded'), 'success');
            await load();
            Lifecycle.invalidate();
            // Approval feeds release gate 3 — invalidate so the gates tab refetches when next opened.
            ReleaseGates.invalidate();
        };

        const bind = () => {
            document.getElementById('approvalRequirementsTable')?.addEventListener('click', (event) => {
                const record = event.target.closest('.js-record-approval');
                if (record) { void openEvidenceModal('record', record); return; }
                const reject = event.target.closest('.js-reject-approval');
                if (reject) void openEvidenceModal('reject', reject);
            });

            document.getElementById('btnApprovalFilterApply')?.addEventListener('click', applyFilters);
            document.getElementById('btnApprovalFilterReset')?.addEventListener('click', () => {
                ['approvalFilterType', 'approvalFilterMandatory', 'approvalFilterStatus'].forEach((id) => {
                    const el = document.getElementById(id);
                    if (el) {
                        Array.from(el.options).forEach((option) => { option.selected = false; });
                        window.jQuery?.(el).trigger('change');
                    }
                });
                if (requirementsDt) {
                    const baseline = emptyApprovalView();
                    requirementsDt.columns([2, 3, 5]).search('');
                    requirementsDt.search('');
                    approvalViewColumns.forEach((index) => requirementsDt.column(index).visible(true, false));
                    try { requirementsDt.colReorder?.order?.(baseline.columnOrder, true); } catch (error) { }
                    requirementsDt.order(baseline.order).draw();
                    setApprovalSaveViewVisible(approvalViewIsDirty(requirementsDt));
                }
            });

            document.getElementById('approvalEvidenceForm')?.addEventListener('submit', submitEvidence);
        };

        return {
            bind,
            ensureLoaded: () => { if (!loaded) void load(); },
            invalidate: () => { loaded = false; }
        };
    })();

    // ── Release Gates tab (FU10) ─────────────────────────────────────────────
    const ReleaseGates = (function () {
        let loaded = false;
        let evaluation = null;

        // The six gates are a fixed, service-controlled catalog — never waivable, never overridable from here.
        const GATE_LABEL_KEYS = {
            MasterRegisterActive: 'MasterRegisterGate',
            ApprovedRepositoryAvailable: 'ApprovedRepositoryGate',
            MandatoryApprovalEvidence: 'MandatoryApprovalEvidenceGate',
            RequiredExecutionMaterialsEffective: 'RequiredExecutionMaterialsGate',
            TrainingReadiness: 'TrainingReadinessGate',
            SupersededCopyWithdrawalMethod: 'SupersededCopyWithdrawalGate'
        };
        // Gates whose detail screen is not built yet — the blocking reason is shown, the deep dive is signposted.
        const DEFERRED_DETAIL_KEYS = {
            TrainingReadiness: 'TrainingDetailsDeferred',
            ApprovedRepositoryAvailable: 'RepositoryDetailsDeferred',
            SupersededCopyWithdrawalMethod: 'ControlledCopyDetailsDeferred'
        };

        const gateResultBadge = (value) => {
            const key = String(value || '');
            const map = { Yes: 'success', No: 'danger', NotApplicable: 'secondary' };
            const labelKey = { Yes: 'GatePassed', No: 'GateBlocked', NotApplicable: 'GateNotApplicable' }[key];
            return key ? badge(map[key] || 'secondary', t(labelKey || key)) : na();
        };

        const renderSummary = () => {
            const e = evaluation || {};
            const host2 = document.getElementById('gatesSummaryCards');
            if (!host2) return;
            const gates = Array.isArray(e.gates) ? e.gates : [];
            const evidenceMissing = gates.filter((g) => g.gateResult !== 'Yes' && !g.evidenceReference).length;
            host2.innerHTML = [
                summaryCard(e.ready ? 'success' : 'warning', 'bx-shield-quarter', e.completedGateCount ?? 0, `${t('GatesPassed')} / ${e.gateCount ?? 0}`),
                summaryCard((e.blockingCount ?? 0) > 0 ? 'danger' : 'success', 'bx-block', e.blockingCount ?? 0, t('GatesBlocked')),
                summaryCard((e.warningCount ?? 0) > 0 ? 'warning' : 'secondary', 'bx-error', e.warningCount ?? 0, t('GatesWarning')),
                summaryCard(evidenceMissing > 0 ? 'warning' : 'success', 'bx-file-blank', evidenceMissing, t('EvidenceMissingCount'))
            ].join('');
        };

        const renderGates = () => {
            const listHost = document.getElementById('gatesCardList');
            if (!listHost) return;
            const gates = Array.isArray(evaluation?.gates) ? evaluation.gates : [];
            if (!gates.length) {
                listHost.innerHTML = `<div class="col-12"><div class="alert alert-secondary mb-0">${esc(t('NoGateEvaluationFound'))}</div></div>`;
                return;
            }

            listHost.innerHTML = gates.slice().sort((a, b) => (a.gateNumber || 0) - (b.gateNumber || 0)).map((g) => {
                const nameKey = GATE_LABEL_KEYS[g.gateKey];
                const name = nameKey ? t(nameKey) : (g.gateName || g.gateKey);
                const deferredKey = DEFERRED_DETAIL_KEYS[g.gateKey];
                const blocked = g.gateResult === 'No';
                const canRecord = perms.canRecordGateEvidence;

                const deferredNote = deferredKey && blocked
                    ? `<div class="alert alert-secondary py-2 px-3 small mb-3">${esc(t(deferredKey))}</div>` : '';
                const blocking = g.blockingReason
                    ? `<div class="text-danger small mb-2"><i class="bx bx-x-circle me-1"></i>${esc(g.blockingReason)}</div>` : '';
                const warning = g.warningReason
                    ? `<div class="text-warning small mb-2"><i class="bx bx-error me-1"></i>${esc(g.warningReason)}</div>` : '';

                return `<div class="col-12 col-lg-6">
                    <section class="card h-100">
                        <div class="card-body">
                            <div class="d-flex justify-content-between align-items-start gap-2 mb-3">
                                <div>
                                    <h6 class="mb-1">${esc(String(g.gateNumber ?? ''))}. ${esc(name)}</h6>
                                    ${g.isNonWaivable ? `<span class="badge bg-label-dark"><i class="bx bx-lock me-1"></i>${esc(t('NoWaiverAllowed'))}</span>` : ''}
                                </div>
                                <div>${gateResultBadge(g.gateResult)}</div>
                            </div>
                            ${blocking}${warning}${deferredNote}
                            <dl class="row mb-0 small">
                                <dt class="col-5 fw-normal text-muted">${esc(t('EvidenceReference'))}</dt>
                                <dd class="col-7">${text(g.evidenceReference)}</dd>
                                <dt class="col-5 fw-normal text-muted">${esc(t('VerifiedByRole'))}</dt>
                                <dd class="col-7">${text(g.verifiedByRole)}</dd>
                                <dt class="col-5 fw-normal text-muted">${esc(t('VerificationDate'))}</dt>
                                <dd class="col-7">${esc(fmtDateTime(g.verificationDate))}</dd>
                                <dt class="col-5 fw-normal text-muted">${esc(t('GateSource'))}</dt>
                                <dd class="col-7">${text(g.source)}</dd>
                            </dl>
                            ${canRecord ? `<button type="button" class="btn btn-sm btn-label-primary mt-3 js-record-gate-evidence"
                                    data-gate-key="${esc(g.gateKey)}" data-gate-name="${esc(name)}">
                                    <i class="bx bx-file me-1"></i>${esc(t('RecordGateEvidence'))}
                                </button>` : ''}
                        </div>
                    </section>
                </div>`;
            }).join('');
        };

        const renderHistory = (records) => {
            const body = document.getElementById('gateHistoryBody');
            if (!body) return;
            if (!records.length) { body.innerHTML = emptyRow(6, 'NoGateEvaluationFound'); return; }
            body.innerHTML = records.slice()
                .sort((a, b) => new Date(b.evaluatedAt) - new Date(a.evaluatedAt))
                .map((e) => `<tr>
                    <td>${esc(fmtDateTime(e.evaluatedAt))}</td>
                    <td>${text(e.evaluatedBy)}</td>
                    <td>${e.ready ? badge('success', t('GateEvaluationComplete')) : badge('warning', t('GateEvaluationIncomplete'))}</td>
                    <td>${esc(String(e.completedGateCount ?? 0))}</td>
                    <td>${esc(String(e.blockingCount ?? 0))}</td>
                    <td>${esc(String(e.warningCount ?? 0))}</td>
                </tr>`).join('');
        };

        // Tab open uses READINESS: it recomputes the six gates without persisting an evaluation record.
        const load = async () => {
            hideAlert('gatesAlert');
            const body = document.getElementById('gateHistoryBody');
            if (body) body.innerHTML = loadingRow(6);

            const [readyRes, historyRes] = await Promise.all([
                getJson('/release-gates/readiness'),
                getJson('/release-gates/history')
            ]);

            if (readyRes.ok) evaluation = unwrap(readyRes.payload);
            else handleFailure(readyRes.res, readyRes.payload, 'gatesAlert');

            renderHistory(historyRes.ok ? unwrapList(historyRes.payload) : []);
            renderSummary();
            renderGates();
            loaded = true;
        };

        const bind = () => {
            document.getElementById('btnRefreshGates')?.addEventListener('click', () => void load());

            document.getElementById('btnEvaluateGates')?.addEventListener('click', async (e) => {
                // Evaluate persists an evaluation record. It does NOT mark the document effective and cannot
                // override a blocked gate — going effective stays a lifecycle transition with its own guards.
                const result = await postJson('/release-gates/evaluate', {}, e.currentTarget);
                if (!result.ok) { handleFailure(result.res, result.payload, 'gatesAlert'); return; }
                window.showToast?.(t('GateEvaluationSucceeded'), 'success');
                await load();
                Lifecycle.invalidate();
            });

            document.getElementById('gatesCardList')?.addEventListener('click', async (event) => {
                const btn = event.target.closest('.js-record-gate-evidence');
                if (!btn) return;
                const form = document.getElementById('gateEvidenceForm');
                if (!form) return;
                form.reset();
                form.classList.remove('was-validated');
                document.getElementById('gateEvidenceKey').value = btn.dataset.gateKey;
                document.getElementById('gateEvidenceGateName').value = btn.dataset.gateName;
                document.getElementById('gateVerifiedByUserId').innerHTML = await loadUserOptions();
                window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('gateEvidenceModal')).show();
            });

            document.getElementById('gateEvidenceForm')?.addEventListener('submit', async (event) => {
                event.preventDefault();
                const form = event.currentTarget;
                if (!form.checkValidity()) { form.classList.add('was-validated'); return; }

                const gateKey = document.getElementById('gateEvidenceKey').value;
                const verificationDate = document.getElementById('gateVerificationDate').value;
                const payload = {
                    gateKey: gateKey,
                    evidenceReference: document.getElementById('gateEvidenceReference').value.trim(),
                    verifiedByUserId: document.getElementById('gateVerifiedByUserId').value || null,
                    verifiedByRole: document.getElementById('gateVerifiedByRole').value.trim() || null,
                    verificationDate: verificationDate ? new Date(`${verificationDate}T00:00:00`).toISOString() : null,
                    comment: document.getElementById('gateEvidenceComment').value.trim() || null
                };

                const result = await postJson(`/release-gates/${encodeURIComponent(gateKey)}/evidence`, payload, form.querySelector('[type="submit"]'));
                if (!result.ok) { handleFailure(result.res, result.payload, 'gatesAlert'); return; }

                window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('gateEvidenceModal')).hide();
                window.showToast?.(t('EvidenceRecordSucceeded'), 'success');
                await load();
            });
        };

        return {
            bind,
            ensureLoaded: () => { if (!loaded) void load(); },
            invalidate: () => { loaded = false; }
        };
    })();

    // ── Training tab (FU11) ──────────────────────────────────────────────────
    const Training = (function () {
        let loaded = false;
        let readiness = null;
        let requirements = [];
        // FU11 has no GET for assignments. Rather than fake a register, we keep only what THIS session created or
        // changed — every row here came back from a real backend response — and the view says so explicitly.
        const sessionAssignments = new Map();

        const audienceLabel = (v) => (v ? t(`TrainingAudience${v}`) : na());
        const trainingTypeLabel = (v) => (v ? t(`TrainingType${v}`) : na());
        const sourceRuleLabel = (v) => (v ? t(`TrainingSourceRule${v}`) : na());
        const requirementStatusBadge = (v) => {
            const key = String(v || '');
            const map = {
                Pending: 'warning', Assigned: 'info', Completed: 'success',
                Restricted: 'secondary', WaivedNotAllowed: 'danger', Overdue: 'danger', Blocked: 'danger'
            };
            return key ? badge(map[key] || 'secondary', t(`TrainingRequirementStatus${key}`)) : na();
        };
        const assignmentStatusBadge = (v) => {
            const key = String(v || '');
            const map = { Assigned: 'info', Completed: 'success', Failed: 'danger', Restricted: 'secondary', Cancelled: 'dark' };
            return key ? badge(map[key] || 'secondary', t(`TrainingAssignmentStatus${key}`)) : na();
        };
        const effectivenessBadge = (v) => {
            const key = String(v || '');
            const map = { NotRequired: 'secondary', Pending: 'warning', Passed: 'success', Failed: 'danger' };
            return key ? badge(map[key] || 'secondary', t(`Effectiveness${key}`)) : na();
        };

        const requirementLabel = (r) => {
            const target = r.requiredRole || r.requiredDepartment || r.requiredUserId || audienceLabel(r.audienceType);
            return `${audienceLabel(r.audienceType)} — ${target} — ${trainingTypeLabel(r.trainingType)}`;
        };

        const renderSummary = () => {
            const r = readiness || {};
            const hostEl = document.getElementById('trainingSummaryCards');
            if (!hostEl) return;
            hostEl.innerHTML = [
                summaryCard(r.ready ? 'success' : 'warning', 'bx-check-double', `${r.completedCount ?? 0}/${r.requiredCount ?? 0}`,
                    `${t('TrainingReadiness')}: ${r.ready ? t('TrainingReady') : t('TrainingNotReady')}`),
                summaryCard((r.missingAssignmentCount ?? 0) > 0 ? 'danger' : 'success', 'bx-user-plus', r.missingAssignmentCount ?? 0, t('MissingTrainingAssignments')),
                summaryCard((r.effectivenessPendingCount ?? 0) > 0 ? 'warning' : 'success', 'bx-task', r.effectivenessPendingCount ?? 0, t('TrainingEffectivenessRequired')),
                summaryCard((r.failedCount ?? 0) > 0 ? 'danger' : 'secondary', 'bx-x-circle', r.failedCount ?? 0, t('TrainingEffectivenessFailed'))
            ].join('');
        };

        const renderReadiness = () => {
            const r = readiness || {};
            const list = document.getElementById('trainingReadinessList');
            if (list) {
                list.innerHTML = [
                    row(t('TrainingReadiness'), r.ready === true ? badge('success', t('TrainingReady')) : badge('warning', t('TrainingNotReady'))),
                    row(t('RequiredCount'), esc(String(r.requiredCount ?? 0))),
                    row(t('AssignedCount'), esc(String(r.assignedCount ?? 0))),
                    row(t('CompletedCount'), esc(String(r.completedCount ?? 0))),
                    row(t('PendingCount'), esc(String(r.pendingCount ?? 0))),
                    row(t('RestrictedCount'), esc(String(r.restrictedCount ?? 0))),
                    row(t('EffectivenessPendingCount'), esc(String(r.effectivenessPendingCount ?? 0))),
                    row(t('TrainingEffectivenessFailed'), esc(String(r.failedCount ?? 0)))
                ].join('');
            }

            const blocking = document.getElementById('trainingBlockingList');
            if (blocking) {
                const blocks = Array.isArray(r.blockingReasons) ? r.blockingReasons.filter(Boolean) : [];
                const warns = Array.isArray(r.warningReasons) ? r.warningReasons.filter(Boolean) : [];
                const html = blocks.map((b) => `<li class="mb-2"><i class="bx bx-x-circle text-danger me-2"></i>${esc(b)}</li>`)
                    .concat(warns.map((w) => `<li class="mb-2"><i class="bx bx-error text-warning me-2"></i>${esc(w)}</li>`))
                    .join('');
                blocking.innerHTML = html
                    ? `<ul class="list-unstyled mb-0">${html}</ul>`
                    : `<div class="text-success"><i class="bx bx-check-circle me-2"></i>${esc(t('NoBlockingIssues'))}</div>`;
            }
        };

        const renderRequirements = () => {
            const body = document.getElementById('trainingRequirementsBody');
            if (!body) return;
            if (!requirements.length) { body.innerHTML = emptyRow(8, 'NoTrainingRequirementsFound'); return; }
            body.innerHTML = requirements.map((r) => {
                const action = perms.canManageTraining
                    ? `<button type="button" class="btn btn-sm btn-label-primary js-assign-training"
                               data-requirement-id="${esc(r.id)}" data-label="${esc(requirementLabel(r))}">
                           ${esc(t('AssignTraining'))}
                       </button>`
                    : `<span class="text-muted small">${esc(t('ActionNotAvailable'))}</span>`;
                return `<tr>
                    <td><span class="fw-medium text-heading">${esc(audienceLabel(r.audienceType))}</span><br>
                        <small class="text-muted">${text(r.requiredRole || r.requiredDepartment || r.requiredUserId)}</small></td>
                    <td>${esc(trainingTypeLabel(r.trainingType))}</td>
                    <td>${r.mandatoryBeforeEffective ? badge('danger', t('Mandatory')) : badge('secondary', t('Optional'))}</td>
                    <td>${boolBadge(r.isCriticalProcessUserRequirement === true)}</td>
                    <td>${boolBadge(r.effectivenessCheckRequired === true)}</td>
                    <td>${esc(sourceRuleLabel(r.sourceRule))}</td>
                    <td>${requirementStatusBadge(r.status)}</td>
                    <td class="text-end pe-3">${action}</td>
                </tr>`;
            }).join('');
        };

        const assignmentLabel = (a) =>
            `${a.assignedToRole || a.assignedToDepartment || a.assignedToUserId || na()} — ${trainingTypeLabel(a.trainingType)}`;

        const renderAssignments = () => {
            const body = document.getElementById('trainingAssignmentsBody');
            if (!body) return;
            const rows = Array.from(sessionAssignments.values());
            if (!rows.length) { body.innerHTML = emptyRow(8, 'NoTrainingAssignmentsFound'); return; }
            body.innerHTML = rows.map((a) => {
                const restricted = a.status === 'Restricted' || !!a.restrictionReason;
                const canComplete = perms.canVerifyTraining && a.status === 'Assigned';
                const canEffect = perms.canVerifyTraining && a.effectivenessCheckStatus === 'Pending';
                const canRestrict = perms.canManageTraining && !restricted;
                const actions = [
                    canComplete ? `<button type="button" class="btn btn-sm btn-label-primary js-complete-training me-1" data-assignment-id="${esc(a.id)}" data-label="${esc(assignmentLabel(a))}">${esc(t('CompleteTraining'))}</button>` : '',
                    canEffect ? `<button type="button" class="btn btn-sm btn-label-info js-effectiveness-training me-1" data-assignment-id="${esc(a.id)}" data-label="${esc(assignmentLabel(a))}">${esc(t('RecordEffectiveness'))}</button>` : '',
                    canRestrict ? `<button type="button" class="btn btn-sm btn-label-warning js-restrict-training" data-assignment-id="${esc(a.id)}" data-label="${esc(assignmentLabel(a))}">${esc(t('RestrictTraining'))}</button>` : ''
                ].filter(Boolean).join('');
                return `<tr>
                    <td><span class="fw-medium text-heading">${text(a.assignedToRole || a.assignedToDepartment || a.assignedToUserId)}</span></td>
                    <td>${esc(trainingTypeLabel(a.trainingType))}</td>
                    <td>${assignmentStatusBadge(a.status)}</td>
                    <td>${esc(fmtDateTime(a.completedAt))}</td>
                    <td>${text(a.completionEvidenceReference)}</td>
                    <td>${effectivenessBadge(a.effectivenessCheckStatus)}<br>
                        <small class="text-muted">${text(a.effectivenessEvidenceReference)}</small></td>
                    <td>${restricted ? badge('warning', t('Restricted')) : badge('secondary', t('NotRestricted'))}<br>
                        <small class="text-muted">${text(a.restrictionReason)}</small></td>
                    <td class="text-end pe-3">${actions || `<span class="text-muted small">${esc(t('ActionNotAvailable'))}</span>`}</td>
                </tr>`;
            }).join('');
        };

        const load = async () => {
            hideAlert('trainingAlert');
            const body = document.getElementById('trainingRequirementsBody');
            if (body) body.innerHTML = loadingRow(8);

            const [reqRes, readyRes] = await Promise.all([
                getJson('/training/requirements'),
                getJson('/training/readiness')
            ]);

            if (reqRes.ok) requirements = unwrapList(reqRes.payload);
            else { requirements = []; handleFailure(reqRes.res, reqRes.payload, 'trainingAlert'); }

            if (readyRes.ok) readiness = unwrap(readyRes.payload);
            else handleFailure(readyRes.res, readyRes.payload, 'trainingAlert');

            renderSummary();
            renderReadiness();
            renderRequirements();
            renderAssignments();
            loaded = true;
        };

        // Every mutation returns the updated TrainingAssignmentModel — that response is the only source for the
        // session table, so nothing on screen is invented client-side.
        const captureAssignment = (payload) => {
            const a = unwrap(payload);
            if (a?.id) sessionAssignments.set(a.id, a);
        };

        const afterMutation = async (payload, toastKey) => {
            captureAssignment(payload);
            window.showToast?.(t(toastKey), 'success');
            await load();
            // Gate 5 consumes training readiness, so the gates tab is stale now. Invalidate it so it refetches when
            // next opened — deliberately NOT auto-evaluating: persisting an evaluation stays a user decision.
            ReleaseGates.invalidate();
        };

        const openModal = (modalId, idFieldId, labelFieldId, btn) => {
            const modalEl = document.getElementById(modalId);
            if (!modalEl) return;
            const form = modalEl.querySelector('form');
            form?.reset();
            form?.classList.remove('was-validated');
            document.getElementById(idFieldId).value = btn.dataset.assignmentId || btn.dataset.requirementId;
            document.getElementById(labelFieldId).value = btn.dataset.label || '';
            window.bootstrap?.Modal.getOrCreateInstance(modalEl).show();
        };

        const bind = () => {
            document.getElementById('btnRefreshTraining')?.addEventListener('click', () => void load());

            document.getElementById('btnResolveTrainingMatrix')?.addEventListener('click', async (e) => {
                // Resolve computes REQUIREMENTS from criticality/class. It assigns nobody and completes nothing.
                const result = await postJson('/training/resolve', {}, e.currentTarget);
                if (!result.ok) { handleFailure(result.res, result.payload, 'trainingAlert'); return; }
                window.showToast?.(t('TrainingResolveSucceeded'), 'success');
                await load();
            });

            document.getElementById('trainingRequirementsBody')?.addEventListener('click', async (event) => {
                const btn = event.target.closest('.js-assign-training');
                if (!btn) return;
                document.getElementById('trainingAssignedToUserId').innerHTML = await loadUserOptions();
                openModal('trainingAssignModal', 'trainingRequirementId', 'trainingRequirementLabel', btn);
            });

            document.getElementById('trainingAssignForm')?.addEventListener('submit', async (event) => {
                event.preventDefault();
                const form = event.currentTarget;
                if (!form.checkValidity()) { form.classList.add('was-validated'); return; }
                const due = document.getElementById('trainingDueDate').value;
                const payload = {
                    requirementId: document.getElementById('trainingRequirementId').value,
                    assignedToUserId: document.getElementById('trainingAssignedToUserId').value || null,
                    assignedToRole: document.getElementById('trainingAssignedToRole').value.trim() || null,
                    assignedToDepartment: document.getElementById('trainingAssignedToDepartment').value.trim() || null,
                    dueDate: due ? new Date(`${due}T00:00:00`).toISOString() : null
                };
                const result = await postJson('/training/assignments', payload, form.querySelector('[type="submit"]'));
                if (!result.ok) { handleFailure(result.res, result.payload, 'trainingAlert'); return; }
                window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('trainingAssignModal')).hide();
                await afterMutation(result.payload, 'TrainingAssignSucceeded');
            });

            document.getElementById('trainingAssignmentsBody')?.addEventListener('click', (event) => {
                const complete = event.target.closest('.js-complete-training');
                if (complete) { openModal('trainingCompleteModal', 'completeAssignmentId', 'completeAssignmentLabel', complete); return; }
                const effect = event.target.closest('.js-effectiveness-training');
                if (effect) { openModal('trainingEffectivenessModal', 'effectivenessAssignmentId', 'effectivenessAssignmentLabel', effect); return; }
                const restrict = event.target.closest('.js-restrict-training');
                if (restrict) openModal('trainingRestrictModal', 'restrictAssignmentId', 'restrictAssignmentLabel', restrict);
            });

            document.getElementById('trainingCompleteForm')?.addEventListener('submit', async (event) => {
                event.preventDefault();
                const form = event.currentTarget;
                if (!form.checkValidity()) { form.classList.add('was-validated'); return; }
                const assignmentId = document.getElementById('completeAssignmentId').value;
                const payload = { completionEvidenceReference: document.getElementById('trainingCompletionEvidence').value.trim() };
                const result = await postJson(`/training/assignments/${encodeURIComponent(assignmentId)}/complete`, payload, form.querySelector('[type="submit"]'));
                if (!result.ok) { handleFailure(result.res, result.payload, 'trainingAlert'); return; }
                window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('trainingCompleteModal')).hide();
                await afterMutation(result.payload, 'TrainingCompleteSucceeded');
            });

            document.getElementById('trainingEffectivenessForm')?.addEventListener('submit', async (event) => {
                event.preventDefault();
                const form = event.currentTarget;
                if (!form.checkValidity()) { form.classList.add('was-validated'); return; }
                const assignmentId = document.getElementById('effectivenessAssignmentId').value;
                const payload = {
                    passed: document.getElementById('effectivenessResult').value === 'true',
                    evidenceReference: document.getElementById('effectivenessEvidence').value.trim()
                };
                const result = await postJson(`/training/assignments/${encodeURIComponent(assignmentId)}/effectiveness`, payload, form.querySelector('[type="submit"]'));
                if (!result.ok) { handleFailure(result.res, result.payload, 'trainingAlert'); return; }
                window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('trainingEffectivenessModal')).hide();
                await afterMutation(result.payload, 'TrainingEffectivenessSucceeded');
            });

            document.getElementById('trainingRestrictForm')?.addEventListener('submit', async (event) => {
                event.preventDefault();
                const form = event.currentTarget;
                if (!form.checkValidity()) { form.classList.add('was-validated'); return; }
                const assignmentId = document.getElementById('restrictAssignmentId').value;
                const payload = { reason: document.getElementById('trainingRestrictionReason').value.trim() };
                const result = await postJson(`/training/assignments/${encodeURIComponent(assignmentId)}/restrict`, payload, form.querySelector('[type="submit"]'));
                if (!result.ok) { handleFailure(result.res, result.payload, 'trainingAlert'); return; }
                window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('trainingRestrictModal')).hide();
                await afterMutation(result.payload, 'TrainingRestrictSucceeded');
            });
        };

        return {
            bind,
            ensureLoaded: () => { if (!loaded) void load(); },
            invalidate: () => { loaded = false; }
        };
    })();

    // ── Repository assessment (FU16) + Controlled copy (FU17) ────────────────
    const Repository = (function () {
        let loaded = false;
        let linked = null;
        let boundary = null;
        let assessments = [];
        let repoFindings = [];
        let copyReadiness = null;
        let copies = [];
        let plans = [];
        let obsoleteFindings = [];

        const repoTypeLabel = (v) => (v ? t(`RepositoryType${v}`) : na());
        const repoStatusBadge = (v) => {
            const key = String(v || '');
            const map = { Draft: 'secondary', UnderReview: 'info', Approved: 'success', Rejected: 'danger', Expired: 'warning', Superseded: 'dark' };
            return key ? badge(map[key] || 'secondary', t(`RepositoryStatus${key}`)) : na();
        };
        const copyTypeLabel = (v) => (v ? t(`CopyType${v}`) : na());
        const copyStatusBadge = (v) => {
            const key = String(v || '');
            const map = { Active: 'success', PendingWithdrawal: 'warning', Withdrawn: 'info', Reconciled: 'primary', Missing: 'danger', Obsolete: 'danger', Destroyed: 'dark' };
            return key ? badge(map[key] || 'secondary', t(`CopyStatus${key}`)) : na();
        };
        const planStatusBadge = (v) => {
            const key = String(v || '');
            const map = { Draft: 'secondary', Active: 'info', Completed: 'success', Blocked: 'danger', Cancelled: 'dark' };
            return key ? badge(map[key] || 'secondary', t(`PlanStatus${key}`)) : na();
        };
        const findingStatusBadge = (v) => {
            const key = String(v || '');
            const map = { Open: 'danger', Acknowledged: 'warning', Resolved: 'success', Closed: 'secondary' };
            return key ? badge(map[key] || 'secondary', t(`FindingStatus${key}`)) : na();
        };
        const severityBadge = (v) => {
            const key = String(v || '');
            const map = { Critical: 'danger', Major: 'warning', Minor: 'info' };
            return key ? badge(map[key] || 'secondary', t(`Severity${key}`) || key) : na();
        };

        const renderRepositorySummary = () => {
            const hostEl = document.getElementById('repositorySummaryCards');
            if (!hostEl) return;
            const type = linked?.repositoryType;
            const status = linked?.assessmentStatus;
            const approved = status === 'Approved';
            const criticalFindings = repoFindings.filter((f) => f.severity === 'Critical' && f.status !== 'Resolved' && f.status !== 'Closed').length;
            hostEl.innerHTML = [
                summaryCard(approved ? 'success' : 'warning', 'bx-server', linked ? 1 : 0,
                    `${t('ApprovedRepositoryAvailable')}: ${linked ? t(`RepositoryStatus${status}`) : t('NotLinked')}`),
                summaryCard(type === 'ValidatedDms' ? 'success' : (type === 'UnapprovedRepository' ? 'danger' : 'warning'),
                    'bx-shield', assessments.length, `${t('RepositoryType')}: ${type ? repoTypeLabel(type) : na()}`),
                summaryCard(boundary?.canSupportReleaseGate ? 'success' : 'danger', 'bx-log-in-circle',
                    boundary ? (boundary.canSupportReleaseGate ? 1 : 0) : 0, t('CanSupportReleaseGate')),
                summaryCard(criticalFindings > 0 ? 'danger' : 'success', 'bx-error-circle', criticalFindings, t('CriticalFindings'))
            ].join('');
        };

        const renderLinked = () => {
            const list = document.getElementById('linkedRepositoryList');
            if (!list) return;
            const linkButton = perms.canManageRepository
                ? `<dd class="col-12 mt-3 mb-0"><button type="button" class="btn btn-sm btn-label-primary js-open-link-modal">
                       <i class="bx bx-link me-1"></i>${esc(t('LinkRepository'))}</button></dd>`
                : '';
            if (!linked) {
                list.innerHTML = `<dd class="col-12 mb-0"><span class="text-warning"><i class="bx bx-unlink me-2"></i>${esc(t('NoRepositoryLinked'))}</span></dd>${linkButton}`;
                return;
            }
            list.innerHTML = [
                row(t('RepositoryName'), text(linked.repositoryName)),
                row(t('RepositoryType'), esc(repoTypeLabel(linked.repositoryType))),
                row(t('AssessmentStatus'), repoStatusBadge(linked.assessmentStatus)),
                row(t('RepositoryLocation'), text(linked.exactLocation)),
                row(t('RepositoryOwner'), text(linked.repositoryOwnerRole)),
                row(t('AccessControlModel'), text(linked.accessModelDescription)),
                row(t('BackupRestorePlan'), text(linked.backupMethodDescription)),
                row(t('ApprovalMechanism'), text(linked.approvalMechanismDescription)),
                row(t('EffectiveCopyControl'), text(linked.effectiveCopyControlDescription)),
                row(t('AuditTrail'), text(linked.auditTrailDescription)),
                row(t('ChangeControl'), text(linked.changeControlDescription)),
                row(t('ValidationEvidence'), text(linked.validationEvidenceReference)),
                row(t('ApprovedAt'), `${esc(fmtDateTime(linked.approvedAt))} — ${text(linked.approvedByRole)}`),
                row(t('ValidUntil'), esc(fmtDateTime(linked.validUntil)))
            ].join('') + linkButton;
        };

        // The boundary statement is the backend's own words about what this repository may and may not be used for.
        // It is rendered verbatim (escaped) — the UI never softens it and never adds a compliance claim of its own.
        const renderBoundary = () => {
            const panel = document.getElementById('repositoryBoundaryPanel');
            if (!panel) return;
            if (!boundary) {
                panel.innerHTML = `<span class="text-muted">${esc(t('EvaluateToSeeBoundary'))}</span>`;
                return;
            }
            const interimWarning = boundary.repositoryType === 'ApprovedInterimRepository'
                ? `<div class="alert alert-warning py-2 px-3 small mb-3">${esc(t('InterimRepositoryWarning'))}</div>` : '';
            panel.innerHTML = `
                ${interimWarning}
                <p class="mb-3">${esc(boundary.boundaryStatement || na())}</p>
                <dl class="row mb-0 small">
                    <dt class="col-7 fw-normal text-muted">${esc(t('CanSupportReleaseGate'))}</dt>
                    <dd class="col-5">${boolBadge(boundary.canSupportReleaseGate === true)}</dd>
                    <dt class="col-7 fw-normal text-muted">${esc(t('CanSupportRegulatedESignature'))}</dt>
                    <dd class="col-5">${boolBadge(boundary.canSupportRegulatedESignature === true)}</dd>
                </dl>`;
        };

        const renderAssessments = () => {
            const body = document.getElementById('repositoryAssessmentsBody');
            if (!body) return;
            if (!assessments.length) { body.innerHTML = emptyRow(8, 'NoRepositoryAssessmentsFound'); return; }
            body.innerHTML = assessments.map((a) => {
                const isLinked = linked && linked.id === a.id;
                const decidable = a.assessmentStatus === 'Draft' || a.assessmentStatus === 'UnderReview';
                const actions = [
                    perms.canManageRepository ? `<button type="button" class="btn btn-sm btn-label-info js-evaluate-repository me-1" data-assessment-id="${esc(a.id)}">${esc(t('EvaluateRepository'))}</button>` : '',
                    perms.canApproveRepository && decidable ? `<button type="button" class="btn btn-sm btn-label-success js-approve-repository me-1" data-assessment-id="${esc(a.id)}" data-label="${esc(a.repositoryName)}">${esc(t('ApproveRepository'))}</button>` : '',
                    perms.canApproveRepository && decidable ? `<button type="button" class="btn btn-sm btn-label-danger js-reject-repository" data-assessment-id="${esc(a.id)}" data-label="${esc(a.repositoryName)}">${esc(t('RejectRepository'))}</button>` : ''
                ].filter(Boolean).join('');
                return `<tr${isLinked ? ' class="table-active"' : ''}>
                    <td><span class="fw-medium text-heading">${text(a.repositoryName)}</span>
                        ${isLinked ? `<br><span class="badge bg-label-primary mt-1">${esc(t('LinkedRepository'))}</span>` : ''}</td>
                    <td>${esc(repoTypeLabel(a.repositoryType))}</td>
                    <td>${repoStatusBadge(a.assessmentStatus)}</td>
                    <td>${text(a.exactLocation)}</td>
                    <td>${text(a.repositoryOwnerRole)}</td>
                    <td>${esc(fmtDateTime(a.approvedAt))}</td>
                    <td>${esc(fmtDateTime(a.validUntil))}</td>
                    <td class="text-end pe-3">${actions || `<span class="text-muted small">${esc(t('ActionNotAvailable'))}</span>`}</td>
                </tr>`;
            }).join('');
        };

        const renderRepositoryFindings = () => {
            const body = document.getElementById('repositoryFindingsBody');
            if (!body) return;
            if (!repoFindings.length) { body.innerHTML = emptyRow(5, 'NoRepositoryFindingsFound'); return; }
            body.innerHTML = repoFindings.map((f) => `<tr>
                <td>${text(f.findingType)}</td>
                <td>${severityBadge(f.severity)}</td>
                <td>${findingStatusBadge(f.status)}</td>
                <td>${text(f.description)}</td>
                <td>${text(f.evidenceReference)}</td>
            </tr>`).join('');
        };

        const renderCopySummary = () => {
            const r = copyReadiness || {};
            const hostEl = document.getElementById('copySummaryCards');
            if (hostEl) {
                hostEl.innerHTML = [
                    summaryCard('info', 'bx-copy', r.activeCopyCount ?? 0, t('ActiveCopies')),
                    summaryCard((r.pendingWithdrawalCount ?? 0) > 0 ? 'warning' : 'success', 'bx-time', r.pendingWithdrawalCount ?? 0, t('PendingWithdrawal')),
                    summaryCard('primary', 'bx-check', r.withdrawnCount ?? 0, t('WithdrawnCopies')),
                    summaryCard((r.openCriticalFindingCount ?? 0) > 0 ? 'danger' : 'success', 'bx-error-circle', r.openCriticalFindingCount ?? 0, t('CriticalFinding'))
                ].join('');
            }

            const panel = document.getElementById('copyBlockingPanel');
            if (panel) {
                const blocks = Array.isArray(r.blockingReasons) ? r.blockingReasons.filter(Boolean) : [];
                if (blocks.length) {
                    panel.className = 'alert alert-danger';
                    panel.innerHTML = `<strong>${esc(t('ControlledCopyWithdrawalRequired'))}</strong>`
                        + `<ul class="mb-0 mt-2">${blocks.map((b) => `<li>${esc(b)}</li>`).join('')}</ul>`;
                } else if (r.hasControlledCopyData === false) {
                    panel.className = 'alert alert-secondary';
                    panel.innerHTML = esc(t('NoControlledCopyDataYet'));
                } else {
                    panel.className = 'alert alert-success';
                    panel.innerHTML = esc(t('CopyWithdrawalReady'));
                }
                panel.classList.remove('d-none');
            }
        };

        const renderCopies = () => {
            const body = document.getElementById('controlledCopiesBody');
            if (!body) return;
            if (!copies.length) { body.innerHTML = emptyRow(9, 'NoControlledCopiesFound'); return; }
            body.innerHTML = copies.map((c) => {
                const label = `#${c.copyNumber} — ${copyTypeLabel(c.copyType)}`;
                const live = c.copyStatus === 'Active' || c.copyStatus === 'PendingWithdrawal';
                const actions = [
                    perms.canManageCopies && live ? `<button type="button" class="btn btn-sm btn-label-primary js-copy-action me-1" data-mode="withdraw" data-target-id="${esc(c.id)}" data-label="${esc(label)}">${esc(t('WithdrawCopy'))}</button>` : '',
                    perms.canReconcileCopies && c.copyStatus === 'Withdrawn' ? `<button type="button" class="btn btn-sm btn-label-info js-copy-action me-1" data-mode="reconcile" data-target-id="${esc(c.id)}" data-label="${esc(label)}">${esc(t('ReconcileCopy'))}</button>` : '',
                    perms.canManageCopies && live ? `<button type="button" class="btn btn-sm btn-label-danger js-copy-action me-1" data-mode="mark-missing" data-target-id="${esc(c.id)}" data-label="${esc(label)}">${esc(t('MarkCopyMissing'))}</button>` : '',
                    perms.canManageCopies && live ? `<button type="button" class="btn btn-sm btn-label-warning js-copy-action" data-mode="mark-obsolete" data-target-id="${esc(c.id)}" data-label="${esc(label)}">${esc(t('MarkCopyObsolete'))}</button>` : ''
                ].filter(Boolean).join('');
                return `<tr>
                    <td><span class="fw-medium text-heading">#${esc(String(c.copyNumber ?? ''))}</span></td>
                    <td>${esc(copyTypeLabel(c.copyType))}</td>
                    <td>${copyStatusBadge(c.copyStatus)}</td>
                    <td>${text(c.holderRole || c.holderDepartment || c.holderUserId)}</td>
                    <td>${text(c.locationDescription)}</td>
                    <td>${esc(fmtDateTime(c.issuedAt))}</td>
                    <td>${esc(fmtDateTime(c.withdrawnAt))}<br><small class="text-muted">${text(c.withdrawalEvidenceReference)}</small></td>
                    <td>${esc(fmtDateTime(c.reconciledAt))}<br><small class="text-muted">${text(c.reconciliationEvidenceReference)}</small></td>
                    <td class="text-end pe-3">${actions || `<span class="text-muted small">${esc(t('ActionNotAvailable'))}</span>`}</td>
                </tr>`;
            }).join('');
        };

        const renderPlans = () => {
            const body = document.getElementById('withdrawalPlansBody');
            if (!body) return;
            if (!plans.length) { body.innerHTML = emptyRow(8, 'NoWithdrawalPlansFound'); return; }
            body.innerHTML = plans.map((p) => {
                const open = p.planStatus === 'Draft' || p.planStatus === 'Active' || p.planStatus === 'Blocked';
                const action = perms.canManageCopies && open
                    ? `<button type="button" class="btn btn-sm btn-label-primary js-copy-action" data-mode="complete-plan" data-target-id="${esc(p.id)}" data-label="${esc(t('WithdrawalPlan'))}">${esc(t('CompleteWithdrawalPlan'))}</button>`
                    : `<span class="text-muted small">${esc(t('ActionNotAvailable'))}</span>`;
                return `<tr>
                    <td>${text(p.triggerType)}</td>
                    <td>${planStatusBadge(p.planStatus)}</td>
                    <td>${esc(String(p.requiredCopyCount ?? 0))}</td>
                    <td>${esc(String(p.withdrawnCopyCount ?? 0))}</td>
                    <td>${esc(String(p.missingCopyCount ?? 0))}</td>
                    <td>${esc(fmtDateTime(p.dueDate))}</td>
                    <td>${esc(fmtDateTime(p.completedAt))}<br><small class="text-muted">${text(p.planEvidenceReference)}</small></td>
                    <td class="text-end pe-3">${action}</td>
                </tr>`;
            }).join('');
        };

        const renderObsoleteFindings = () => {
            const body = document.getElementById('obsoleteFindingsBody');
            if (!body) return;
            if (!obsoleteFindings.length) { body.innerHTML = emptyRow(7, 'NoObsoleteFindingsFound'); return; }
            body.innerHTML = obsoleteFindings.map((f) => {
                const open = f.status === 'Open' || f.status === 'Acknowledged';
                const action = perms.canManageCopies && open
                    ? `<button type="button" class="btn btn-sm btn-label-primary js-copy-action" data-mode="resolve-finding" data-target-id="${esc(f.id)}" data-label="${esc(f.findingKey || f.findingType)}">${esc(t('ResolveFinding'))}</button>`
                    : `<span class="text-muted small">${esc(t('ActionNotAvailable'))}</span>`;
                return `<tr${f.severity === 'Critical' && open ? ' class="table-danger"' : ''}>
                    <td>${text(f.findingType)}</td>
                    <td>${severityBadge(f.severity)}</td>
                    <td>${findingStatusBadge(f.status)}</td>
                    <td>${text(f.description)}</td>
                    <td>${text(f.locationDescription)}</td>
                    <td>${text(f.deviationReference)}</td>
                    <td class="text-end pe-3">${action}</td>
                </tr>`;
            }).join('');
        };

        const load = async () => {
            hideAlert('repositoryAlert');

            const calls = [];
            if (perms.canViewRepository) calls.push(getJson('/repository/linked'), getJson('/repository/assessments'));
            if (perms.canViewCopies) {
                calls.push(getJson('/controlled-copies'), getJson('/controlled-copies/readiness'),
                    getJson('/controlled-copies/plans'), getJson('/controlled-copies/findings'));
            }
            const results = await Promise.all(calls);
            let i = 0;

            if (perms.canViewRepository) {
                const linkedRes = results[i++];
                const assessRes = results[i++];
                // A register entry with no linked repository is a normal state, not an error — 404 is expected.
                linked = linkedRes.ok ? unwrap(linkedRes.payload) : null;
                if (assessRes.ok) assessments = unwrapList(assessRes.payload);
                else { assessments = []; handleFailure(assessRes.res, assessRes.payload, 'repositoryAlert'); }

                repoFindings = [];
                if (linked?.id) {
                    const f = await getJson(`/repository/assessments/${linked.id}/findings`);
                    if (f.ok) repoFindings = unwrapList(f.payload);
                }
                renderLinked();
                renderBoundary();
                renderAssessments();
                renderRepositoryFindings();
                renderRepositorySummary();
                populateLinkOptions();
            }

            if (perms.canViewCopies) {
                const copiesRes = results[i++];
                const readyRes = results[i++];
                const plansRes = results[i++];
                const findingsRes = results[i++];
                copies = copiesRes.ok ? unwrapList(copiesRes.payload) : [];
                copyReadiness = readyRes.ok ? unwrap(readyRes.payload) : null;
                plans = plansRes.ok ? unwrapList(plansRes.payload) : [];
                obsoleteFindings = findingsRes.ok ? unwrapList(findingsRes.payload) : [];
                if (!copiesRes.ok) handleFailure(copiesRes.res, copiesRes.payload, 'repositoryAlert');
                renderCopySummary();
                renderCopies();
                renderPlans();
                renderObsoleteFindings();
            }

            loaded = true;
        };

        const populateLinkOptions = () => {
            const select = document.getElementById('repositoryLinkAssessmentId');
            if (!select) return;
            select.innerHTML = ['<option value=""></option>'].concat(assessments.map((a) =>
                `<option value="${esc(a.id)}">${esc(a.repositoryName)} — ${esc(repoTypeLabel(a.repositoryType))} (${esc(t(`RepositoryStatus${a.assessmentStatus}`))})</option>`)).join('');
        };

        // Gate 2 and gate 6 both consume this data, so the gates tab is stale after any mutation. Invalidate it so it
        // refetches when next opened — deliberately NOT auto-evaluating.
        const afterMutation = async (toastKey) => {
            window.showToast?.(t(toastKey), 'success');
            await load();
            ReleaseGates.invalidate();
        };

        // ── copy/plan/finding action modal: one form, per-mode field visibility ──
        const COPY_ACTION_MODES = {
            'withdraw': { titleKey: 'WithdrawCopy', noteKey: 'WithdrawalDoesNotDelete', evidence: true, evidenceRequired: true, payloadKey: 'withdrawalEvidenceReference', toastKey: 'CopyWithdrawSucceeded' },
            'reconcile': { titleKey: 'ReconcileCopy', noteKey: 'ReconcileDoesNotDelete', evidence: true, evidenceRequired: true, payloadKey: 'reconciliationEvidenceReference', toastKey: 'CopyReconcileSucceeded' },
            'mark-missing': { titleKey: 'MarkCopyMissing', noteKey: 'MarkMissingDoesNotDelete', evidence: false, comment: true, toastKey: 'CopyMarkMissingSucceeded' },
            'mark-obsolete': { titleKey: 'MarkCopyObsolete', noteKey: 'MarkObsoleteDoesNotDelete', evidence: false, reason: true, location: true, toastKey: 'CopyMarkObsoleteSucceeded' },
            'complete-plan': { titleKey: 'CompleteWithdrawalPlan', noteKey: 'PlanCompleteNote', evidence: true, evidenceRequired: false, payloadKey: 'planEvidenceReference', deviation: true, toastKey: 'PlanCompleteSucceeded' },
            'resolve-finding': { titleKey: 'ResolveFinding', noteKey: 'ResolveFindingDoesNotDelete', evidence: true, evidenceRequired: true, payloadKey: 'resolutionEvidenceReference', deviation: true, toastKey: 'FindingResolveSucceeded' }
        };

        const openCopyAction = (btn) => {
            const mode = btn.dataset.mode;
            const cfg = COPY_ACTION_MODES[mode];
            const modalEl = document.getElementById('copyActionModal');
            if (!cfg || !modalEl) return;
            const form = document.getElementById('copyActionForm');
            form.reset();
            form.classList.remove('was-validated');

            document.getElementById('copyActionMode').value = mode;
            document.getElementById('copyActionTargetId').value = btn.dataset.targetId;
            document.getElementById('copyActionLabel').value = btn.dataset.label || '';
            document.getElementById('copyActionTitle').textContent = t(cfg.titleKey);
            document.getElementById('copyActionSubmit').textContent = t(cfg.titleKey);
            document.getElementById('copyActionNote').textContent = t(cfg.noteKey);

            const toggle = (wrapperId, fieldId, visible, required) => {
                document.getElementById(wrapperId).classList.toggle('d-none', !visible);
                const field = document.getElementById(fieldId);
                if (field) field.required = !!(visible && required);
            };
            toggle('copyActionEvidenceWrapper', 'copyActionEvidence', !!cfg.evidence, cfg.evidenceRequired);
            document.getElementById('copyActionEvidenceRequiredMark').classList.toggle('d-none', !cfg.evidenceRequired);
            toggle('copyActionReasonWrapper', 'copyActionReason', !!cfg.reason, true);
            toggle('copyActionLocationWrapper', 'copyActionLocation', !!cfg.location, false);
            toggle('copyActionCommentWrapper', 'copyActionComment', !!cfg.comment, false);
            toggle('copyActionDeviationWrapper', 'copyActionDeviation', !!cfg.deviation, false);

            window.bootstrap?.Modal.getOrCreateInstance(modalEl).show();
        };

        const submitCopyAction = async (event) => {
            event.preventDefault();
            const form = event.currentTarget;
            if (!form.checkValidity()) { form.classList.add('was-validated'); return; }

            const mode = document.getElementById('copyActionMode').value;
            const cfg = COPY_ACTION_MODES[mode];
            const targetId = document.getElementById('copyActionTargetId').value;
            const evidence = document.getElementById('copyActionEvidence').value.trim();
            const payload = {};
            if (cfg.payloadKey && evidence) payload[cfg.payloadKey] = evidence;
            if (cfg.reason) payload.obsoleteReason = document.getElementById('copyActionReason').value.trim();
            if (cfg.location) payload.locationDescription = document.getElementById('copyActionLocation').value.trim() || null;
            if (cfg.comment) payload.comment = document.getElementById('copyActionComment').value.trim() || null;
            if (cfg.deviation) {
                const dev = document.getElementById('copyActionDeviation').value.trim();
                if (dev) payload.deviationReference = dev;
                if (mode === 'complete-plan' && dev) { payload.missingDeviationReference = dev; delete payload.deviationReference; }
            }

            const path = {
                'withdraw': `/controlled-copies/${encodeURIComponent(targetId)}/withdraw`,
                'reconcile': `/controlled-copies/${encodeURIComponent(targetId)}/reconcile`,
                'mark-missing': `/controlled-copies/${encodeURIComponent(targetId)}/mark-missing`,
                'mark-obsolete': `/controlled-copies/${encodeURIComponent(targetId)}/mark-obsolete`,
                'complete-plan': `/controlled-copies/plans/${encodeURIComponent(targetId)}/complete`,
                'resolve-finding': `/controlled-copies/findings/${encodeURIComponent(targetId)}/resolve`
            }[mode];

            const result = await postJson(path, payload, form.querySelector('[type="submit"]'));
            if (!result.ok) { handleFailure(result.res, result.payload, 'repositoryAlert'); return; }
            window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('copyActionModal')).hide();
            await afterMutation(cfg.toastKey);
        };

        const bind = () => {
            document.getElementById('btnRefreshRepository')?.addEventListener('click', () => void load());

            // ── repository assessment actions ──
            document.getElementById('repositoryAssessmentsBody')?.addEventListener('click', async (event) => {
                const evaluate = event.target.closest('.js-evaluate-repository');
                if (evaluate) {
                    // Evaluate CLASSIFIES the repository and returns the boundary — it does not approve it.
                    const result = await postJson(`/repository/assessments/${encodeURIComponent(evaluate.dataset.assessmentId)}/evaluate`, {}, evaluate);
                    if (!result.ok) { handleFailure(result.res, result.payload, 'repositoryAlert'); return; }
                    boundary = unwrap(result.payload);
                    renderBoundary();
                    renderRepositorySummary();
                    window.showToast?.(t('RepositoryEvaluateSucceeded'), 'success');
                    ReleaseGates.invalidate();
                    return;
                }

                const approve = event.target.closest('.js-approve-repository');
                const reject = event.target.closest('.js-reject-repository');
                const btn = approve || reject;
                if (!btn) return;
                const modalEl = document.getElementById('repositoryDecisionModal');
                if (!modalEl) return;
                const isApprove = !!approve;
                const form = document.getElementById('repositoryDecisionForm');
                form.reset();
                form.classList.remove('was-validated');
                document.getElementById('repositoryDecisionAssessmentId').value = btn.dataset.assessmentId;
                document.getElementById('repositoryDecisionMode').value = isApprove ? 'approve' : 'reject';
                document.getElementById('repositoryDecisionLabel').value = btn.dataset.label || '';
                document.getElementById('repositoryDecisionTitle').textContent = t(isApprove ? 'ApproveRepository' : 'RejectRepository');
                document.getElementById('repositoryDecisionSubmit').textContent = t(isApprove ? 'ApproveRepository' : 'RejectRepository');
                document.getElementById('repositoryDecisionNote').textContent = t(isApprove ? 'ApproveDoesNotClaimValidation' : 'RejectRepositoryNote');
                document.getElementById('repositoryApproveFields').classList.toggle('d-none', !isApprove);
                document.getElementById('repositoryRejectFields').classList.toggle('d-none', isApprove);
                document.getElementById('repositoryRejectionReason').required = !isApprove;
                window.bootstrap?.Modal.getOrCreateInstance(modalEl).show();
            });

            document.getElementById('repositoryDecisionForm')?.addEventListener('submit', async (event) => {
                event.preventDefault();
                const form = event.currentTarget;
                if (!form.checkValidity()) { form.classList.add('was-validated'); return; }
                const assessmentId = document.getElementById('repositoryDecisionAssessmentId').value;
                const isApprove = document.getElementById('repositoryDecisionMode').value === 'approve';
                const validUntil = document.getElementById('repositoryValidUntil').value;
                const payload = isApprove
                    ? {
                        approvedByRole: document.getElementById('repositoryApprovedByRole').value,
                        validUntil: validUntil ? new Date(`${validUntil}T00:00:00`).toISOString() : null
                    }
                    : { reason: document.getElementById('repositoryRejectionReason').value.trim() };
                const path = `/repository/assessments/${encodeURIComponent(assessmentId)}/${isApprove ? 'approve' : 'reject'}`;
                const result = await postJson(path, payload, form.querySelector('[type="submit"]'));
                if (!result.ok) { handleFailure(result.res, result.payload, 'repositoryAlert'); return; }
                window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('repositoryDecisionModal')).hide();
                await afterMutation(isApprove ? 'RepositoryApproveSucceeded' : 'RepositoryRejectSucceeded');
            });

            document.getElementById('repositoryLinkForm')?.addEventListener('submit', async (event) => {
                event.preventDefault();
                const form = event.currentTarget;
                if (!form.checkValidity()) { form.classList.add('was-validated'); return; }
                const payload = { repositoryAssessmentId: document.getElementById('repositoryLinkAssessmentId').value };
                const result = await postJson('/repository/link', payload, form.querySelector('[type="submit"]'));
                if (!result.ok) { handleFailure(result.res, result.payload, 'repositoryAlert'); return; }
                window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('repositoryLinkModal')).hide();
                await afterMutation('RepositoryLinkSucceeded');
            });

            document.getElementById('linkedRepositoryList')?.addEventListener('click', (event) => {
                if (!event.target.closest('.js-open-link-modal')) return;
                populateLinkOptions();
                window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('repositoryLinkModal')).show();
            });

            // ── controlled copy actions ──
            document.getElementById('btnRegisterCopy')?.addEventListener('click', () => {
                const form = document.getElementById('registerCopyForm');
                form?.reset();
                form?.classList.remove('was-validated');
                window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('registerCopyModal')).show();
            });

            document.getElementById('registerCopyForm')?.addEventListener('submit', async (event) => {
                event.preventDefault();
                const form = event.currentTarget;
                if (!form.checkValidity()) { form.classList.add('was-validated'); return; }
                const payload = {
                    copyType: document.getElementById('copyType').value,
                    locationDescription: document.getElementById('copyLocationDescription').value.trim() || null,
                    holderRole: document.getElementById('copyHolderRole').value.trim() || null,
                    holderDepartment: document.getElementById('copyHolderDepartment').value.trim() || null
                };
                const result = await postJson('/controlled-copies/register', payload, form.querySelector('[type="submit"]'));
                if (!result.ok) { handleFailure(result.res, result.payload, 'repositoryAlert'); return; }
                window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('registerCopyModal')).hide();
                await afterMutation('CopyRegisterSucceeded');
            });

            document.getElementById('btnGeneratePlan')?.addEventListener('click', async (e) => {
                const result = await postJson('/controlled-copies/plans/generate', {}, e.currentTarget);
                if (!result.ok) { handleFailure(result.res, result.payload, 'repositoryAlert'); return; }
                await afterMutation('PlanGenerateSucceeded');
            });

            document.getElementById('btnEvaluateReconciliation')?.addEventListener('click', async (e) => {
                // Detects obsolete copies still at point of use and raises findings. It withdraws nothing by itself.
                const result = await postJson('/controlled-copies/reconciliation/evaluate', {}, e.currentTarget);
                if (!result.ok) { handleFailure(result.res, result.payload, 'repositoryAlert'); return; }
                await afterMutation('ReconciliationEvaluateSucceeded');
            });

            ['controlledCopiesBody', 'withdrawalPlansBody', 'obsoleteFindingsBody'].forEach((id) => {
                document.getElementById(id)?.addEventListener('click', (event) => {
                    const btn = event.target.closest('.js-copy-action');
                    if (btn) openCopyAction(btn);
                });
            });

            document.getElementById('copyActionForm')?.addEventListener('submit', submitCopyAction);
        };

        return {
            bind,
            ensureLoaded: () => { if (!loaded) void load(); },
            invalidate: () => { loaded = false; }
        };
    })();

    // ── shared FU29 helpers ──────────────────────────────────────────────────
    const sameEntry = (v) => !!v && String(v).toLowerCase() === entryId.toLowerCase();
    const anySameEntry = (list) => Array.isArray(list) && list.some(sameEntry);
    const isOpenState = (v) => {
        const s = String(v || '').toLowerCase();
        return s !== '' && !['closed', 'cancelled', 'canceled', 'released', 'executed', 'reconciled', 'retired'].includes(s);
    };

    // ── Retention tab (FU15) ─────────────────────────────────────────────────
    const Retention = (function () {
        let loaded = false;
        let subject = null;
        let holds = [];
        let dispositions = [];

        const renderSummary = () => {
            const s = subject || {};
            const hostEl = document.getElementById('retentionSummaryCards');
            if (!hostEl) return;
            const activeHolds = holds.filter((h) => isOpenState(h.holdStatus)).length;
            hostEl.innerHTML = [
                summaryCard('info', 'bx-purchase-tag', text(s.retentionClass), t('RetentionClass')),
                summaryCard('primary', 'bx-calendar-event', s.dispositionEligibleAt ? fmtDateTime(s.dispositionEligibleAt) : na(), t('RetentionEligibilityDate')),
                summaryCard(activeHolds > 0 ? 'danger' : 'success', 'bx-shield-quarter', activeHolds, t('LegalHoldActive')),
                summaryCard(subject && s.isDispositionEligible ? 'success' : 'secondary', 'bx-task', subject ? (s.isDispositionEligible ? t('DispositionEligible') : t('DispositionNotEligible')) : na(), t('DispositionStatus'))
            ].join('');
        };

        const renderSchedule = () => {
            const list = document.getElementById('retentionScheduleList');
            if (!list) return;
            if (!subject) { list.innerHTML = `<dd class="col-12 text-muted mb-0">${esc(t('NoRetentionScheduleFound'))}</dd>`; return; }
            const s = subject;
            list.innerHTML = [
                row(t('RetentionClass'), text(s.retentionClass)),
                row(t('RetentionPolicyKey'), text(s.policyKey)),
                row(t('RetentionTriggerDate'), esc(fmtDateTime(s.retentionTriggerDate))),
                row(t('RetentionDueDate'), esc(fmtDateTime(s.retentionDueDate))),
                row(t('RetentionEligibilityDate'), esc(fmtDateTime(s.dispositionEligibleAt))),
                row(t('RetentionPermanent'), boolBadge(s.isPermanentRetention === true)),
                row(t('LastEvaluatedAt'), esc(fmtDateTime(s.lastEvaluatedAt))),
                row(t('EvaluationStatus'), text(s.evaluationStatus)),
                row(t('EvaluationNote'), text(s.evaluationNote))
            ].join('');
        };

        const renderDisposition = () => {
            const list = document.getElementById('retentionDispositionList');
            if (!list) return;
            const s = subject || {};
            const activeHolds = holds.filter((h) => isOpenState(h.holdStatus)).length;
            list.innerHTML = [
                row(t('DispositionEligible'), subject ? boolBadge(s.isDispositionEligible === true) : na()),
                row(t('LegalHoldActive'), subject ? boolBadge(s.isBlockedByLegalHold === true) : boolBadge(activeHolds > 0)),
                row(t('RetentionActiveHolds'), esc(String(activeHolds))),
                row(t('DispositionRequestsTitle'), esc(String(dispositions.length))),
                row(t('LastEvaluatedAt'), esc(fmtDateTime(s.lastEvaluatedAt)))
            ].join('');
        };

        const holdReasonRef = (h) => h.legalApprovalEvidenceReference || h.releaseLegalApprovalReference || na();
        const renderHolds = () => {
            const body = document.getElementById('retentionLegalHoldsBody');
            if (!body) return;
            if (!holds.length) { body.innerHTML = emptyRow(6, 'NoLegalHoldsFound'); return; }
            body.innerHTML = holds.map((h) => `<tr>
                <td><span class="fw-medium text-heading">${text(h.holdTitle)}</span></td>
                <td>${badge(isOpenState(h.holdStatus) ? 'danger' : 'secondary', String(h.holdStatus || na()))}</td>
                <td>${text(h.holdReason)}</td>
                <td>${esc(fmtDateTime(h.issuedAt))}</td>
                <td>${text(holdReasonRef(h))}</td>
                <td>${esc(fmtDateTime(h.releasedAt))}</td>
            </tr>`).join('');
        };

        const renderDispositions = () => {
            const body = document.getElementById('retentionDispositionsBody');
            if (!body) return;
            if (!dispositions.length) { body.innerHTML = emptyRow(6, 'NoDispositionRequestsForEntry'); return; }
            body.innerHTML = dispositions.map((d) => `<tr>
                <td><span class="fw-medium text-heading">${text(d.requestNumber)}</span></td>
                <td>${badge(isOpenState(d.requestStatus) ? 'info' : 'secondary', String(d.requestStatus || na()))}</td>
                <td>${text(d.eligibilityResult)}</td>
                <td>${esc(fmtDateTime(d.requestedAt))}</td>
                <td>${text(d.executionEvidenceReference || d.approvalEvidenceReference)}</td>
                <td>${esc(fmtDateTime(d.executedAt))}</td>
            </tr>`).join('');
        };

        const renderAll = () => { renderSummary(); renderSchedule(); renderDisposition(); renderHolds(); renderDispositions(); };

        const load = async () => {
            hideAlert('retentionAlert');
            const [subjectRes, holdsRes, dispRes] = await Promise.all([
                getJson('/retention/subject'),
                perms.canViewLegalHold ? getJson('/retention/legal-holds') : Promise.resolve({ ok: true, payload: {} }),
                getJson('/retention/dispositions')
            ]);
            // A missing retention subject is not an error — the entry simply has no evaluated schedule yet.
            subject = subjectRes.ok ? unwrap(subjectRes.payload) : null;
            const allHolds = holdsRes.ok ? unwrapList(holdsRes.payload) : [];
            // Narrow global holds/dispositions to the ones scoping THIS register entry (backend enforces tenant isolation).
            holds = allHolds.filter((h) => anySameEntry(h.registerEntryIds));
            const allDisp = dispRes.ok ? unwrapList(dispRes.payload) : [];
            dispositions = allDisp.filter((d) => sameEntry(d.registerEntryId));
            renderAll();
            loaded = true;
        };

        const bind = () => {
            document.getElementById('btnReloadRetention')?.addEventListener('click', () => void load());
            document.getElementById('btnEvaluateRetention')?.addEventListener('click', async (e) => {
                // Opt-in, idempotent: recomputes eligibility + legal-hold block. It disposes of nothing.
                const result = await postJson('/retention/evaluate', {}, e.currentTarget);
                if (!result.ok) { handleFailure(result.res, result.payload, 'retentionAlert'); return; }
                window.showToast?.(t('RetentionEvaluateSucceeded'), 'success');
                await load();
            });
        };

        return { bind, ensureLoaded: () => { if (!loaded) void load(); }, invalidate: () => { loaded = false; } };
    })();

    // ── Signatures tab (FU23) ────────────────────────────────────────────────
    const Signatures = (function () {
        let loaded = false;
        let policies = [];
        let requests = [];
        let records = [];

        const isInvalid = (v) => ['invalid', 'requiresresign', 'requires_resign'].includes(String(v || '').toLowerCase());

        const renderSummary = () => {
            const hostEl = document.getElementById('signaturesSummaryCards');
            if (!hostEl) return;
            const openReq = requests.filter((r) => isOpenState(r.requestStatus)).length;
            const invalid = records.filter((r) => isInvalid(r.signatureStatus)).length;
            hostEl.innerHTML = [
                summaryCard('info', 'bx-file', policies.length, t('SignaturePolicyCount')),
                summaryCard(openReq > 0 ? 'warning' : 'secondary', 'bx-envelope', openReq, t('OpenSignatureRequests')),
                summaryCard('primary', 'bx-pen', records.length, t('CompletedSignatures')),
                summaryCard(invalid > 0 ? 'danger' : 'success', 'bx-shield-x', invalid, t('InvalidVerifications'))
            ].join('');
        };

        const sigStatusBadge = (v) => badge(isInvalid(v) ? 'danger' : (isOpenState(v) ? 'info' : 'success'), String(v || na()));

        const renderPolicies = () => {
            const body = document.getElementById('signaturePoliciesBody');
            if (!body) return;
            if (!policies.length) { body.innerHTML = emptyRow(6, 'NoSignaturePoliciesFound'); return; }
            body.innerHTML = policies.map((p) => `<tr>
                <td><span class="fw-medium text-heading">${text(p.policyName)}</span></td>
                <td>${text(p.signableSubjectType)}</td>
                <td>${text(p.signatureMeaning)}</td>
                <td>${boolBadge(p.requiresReAuthentication === true)}</td>
                <td>${p.requiresSecondFactor ? `${boolBadge(true)} <span class="badge bg-label-warning">${esc(t('TwoFactorNotImplemented'))}</span>` : boolBadge(false)}</td>
                <td>${badge(String(p.policyStatus || '').toLowerCase() === 'active' ? 'success' : 'secondary', String(p.policyStatus || na()))}</td>
            </tr>`).join('');
        };

        const renderRequests = () => {
            const body = document.getElementById('signatureRequestsBody');
            if (!body) return;
            if (!requests.length) { body.innerHTML = emptyRow(6, 'NoSignatureRequestsFound'); return; }
            body.innerHTML = requests.map((r) => `<tr>
                <td><span class="fw-medium text-heading">${text(r.signatureRequestNumber)}</span></td>
                <td>${text(r.signatureMeaning)}</td>
                <td>${text(r.requestedSignerRole)}</td>
                <td>${sigStatusBadge(r.requestStatus)}${r.isOverdue ? ` <span class="badge bg-label-danger">${esc(t('TrainingRequirementStatusOverdue'))}</span>` : ''}</td>
                <td>${esc(fmtDateTime(r.requestedAt))}</td>
                <td>${esc(fmtDateTime(r.dueDate))}</td>
            </tr>`).join('');
        };

        const renderRecords = () => {
            const body = document.getElementById('signatureRecordsBody');
            if (!body) return;
            if (!records.length) { body.innerHTML = emptyRow(6, 'NoSignatureRecordsFound'); return; }
            body.innerHTML = records.map((r) => {
                const fp = (r.objectFingerprint || '').slice(0, 12);
                const verify = perms.canVerifySignatures
                    ? `<button type="button" class="btn btn-sm btn-label-info js-verify-signature" data-signature-id="${esc(r.id)}"><i class="icon-base bx bx-check-shield me-1"></i>${esc(t('VerifySignature'))}</button>`
                    : `<span class="text-muted small">${esc(t('ActionNotAvailable'))}</span>`;
                return `<tr>
                    <td>${text(r.signatureMeaning)}</td>
                    <td>${text(r.signerDisplayName || r.signerRole || r.signerUserId)}</td>
                    <td>${esc(fmtDateTime(r.signedAt))}</td>
                    <td>${sigStatusBadge(r.signatureStatus)}</td>
                    <td>${fp ? `<code>${esc(fp)}…</code>` : na()}</td>
                    <td class="text-end pe-3">${verify}</td>
                </tr>`;
            }).join('');
        };

        const renderAll = () => { renderSummary(); renderPolicies(); renderRequests(); renderRecords(); };

        const load = async () => {
            hideAlert('signaturesAlert');
            const [polRes, reqRes, recRes] = await Promise.all([
                getJson('/signatures/policies'), getJson('/signatures/requests'), getJson('/signatures/records')
            ]);
            policies = polRes.ok ? unwrapList(polRes.payload) : [];
            requests = (reqRes.ok ? unwrapList(reqRes.payload) : []).filter((r) => sameEntry(r.registerEntryId));
            records = (recRes.ok ? unwrapList(recRes.payload) : []).filter((r) => sameEntry(r.registerEntryId));
            renderAll();
            loaded = true;
        };

        const bind = () => {
            document.getElementById('btnReloadSignatures')?.addEventListener('click', () => void load());
            document.getElementById('signatureRecordsBody')?.addEventListener('click', async (event) => {
                const btn = event.target.closest('.js-verify-signature');
                if (!btn) return;
                const sigId = btn.dataset.signatureId;
                const result = await postJson(`/signatures/${encodeURIComponent(sigId)}/verify`, {}, btn);
                if (!result.ok) { handleFailure(result.res, result.payload, 'signaturesAlert'); return; }
                const v = unwrap(result.payload) || {};
                const ok = v.fingerprintMatches === true;
                window.showToast?.(ok ? t('FingerprintValid') : t('FingerprintInvalid'), ok ? 'success' : 'warning');
                await load();
            });
        };

        return { bind, ensureLoaded: () => { if (!loaded) void load(); }, invalidate: () => { loaded = false; } };
    })();

    // ── Quality Events tab (FU22) ────────────────────────────────────────────
    const QualityEvents = (function () {
        let loaded = false;
        let events = [];
        let deviations = [];
        let capa = [];

        const renderSummary = () => {
            const hostEl = document.getElementById('qualitySummaryCards');
            if (!hostEl) return;
            const openDev = deviations.filter((d) => isOpenState(d.deviationStatus)).length;
            const openCapa = capa.filter((c) => isOpenState(c.actionStatus)).length;
            const effPending = capa.filter((c) => c.effectivenessCheckRequired && String(c.effectivenessResult || '').toLowerCase() === 'pending').length;
            hostEl.innerHTML = [
                summaryCard('info', 'bx-error-circle', events.length, t('LinkedQualityEventsCount')),
                summaryCard(openDev > 0 ? 'warning' : 'success', 'bx-git-repo-forked', openDev, t('OpenDeviations')),
                summaryCard(openCapa > 0 ? 'warning' : 'success', 'bx-wrench', openCapa, t('OpenCapas')),
                summaryCard(effPending > 0 ? 'warning' : 'success', 'bx-time', effPending, t('EffectivenessPending'))
            ].join('');
        };

        const blockingBadge = (open) => open
            ? `<span class="badge bg-label-danger">${esc(t('QualityBlockingYes'))}</span>`
            : `<span class="badge bg-label-success">${esc(t('QualityBlockingNo'))}</span>`;
        const sevBadge = (v) => {
            const s = String(v || '').toLowerCase();
            const c = s === 'critical' ? 'danger' : (s === 'major' ? 'warning' : 'secondary');
            return badge(c, String(v || na()));
        };

        const renderEvents = () => {
            const body = document.getElementById('qualityEventsBody');
            if (!body) return;
            if (!events.length) { body.innerHTML = emptyRow(6, 'NoQualityEventsFound'); return; }
            body.innerHTML = events.map((e) => `<tr>
                <td><span class="fw-medium text-heading">${text(e.qualityEventNumber)}</span></td>
                <td>${text(e.eventType)}</td>
                <td>${sevBadge(e.eventSeverity)}</td>
                <td>${badge(isOpenState(e.eventStatus) ? 'info' : 'secondary', String(e.eventStatus || na()))}</td>
                <td>${blockingBadge(isOpenState(e.eventStatus))}</td>
                <td>${esc(fmtDateTime(e.detectedAt))}</td>
            </tr>`).join('');
        };

        const renderDeviations = () => {
            const body = document.getElementById('qualityDeviationsBody');
            if (!body) return;
            if (!deviations.length) { body.innerHTML = emptyRow(6, 'NoDeviationsFound'); return; }
            body.innerHTML = deviations.map((d) => `<tr>
                <td><span class="fw-medium text-heading">${text(d.deviationNumber)}</span></td>
                <td>${sevBadge(d.deviationSeverity)}</td>
                <td>${badge(isOpenState(d.deviationStatus) ? 'warning' : 'secondary', String(d.deviationStatus || na()))}</td>
                <td>${text(d.reportedBy)}</td>
                <td>${blockingBadge(isOpenState(d.deviationStatus))}</td>
                <td>${esc(fmtDateTime(d.closedAt))}</td>
            </tr>`).join('');
        };

        const renderCapa = () => {
            const body = document.getElementById('qualityCapaBody');
            if (!body) return;
            if (!capa.length) { body.innerHTML = emptyRow(6, 'NoCapasFound'); return; }
            body.innerHTML = capa.map((c) => {
                const effPending = c.effectivenessCheckRequired && String(c.effectivenessResult || '').toLowerCase() === 'pending';
                const open = isOpenState(c.actionStatus);
                return `<tr>
                    <td><span class="fw-medium text-heading">${text(c.capaNumber)}</span></td>
                    <td>${badge(open ? 'warning' : 'secondary', String(c.actionStatus || na()))}${c.isOverdue ? ` <span class="badge bg-label-danger">${esc(t('TrainingRequirementStatusOverdue'))}</span>` : ''}</td>
                    <td>${text(c.actionOwnerRole || c.actionOwnerUserId)}</td>
                    <td>${esc(fmtDateTime(c.dueDate))}</td>
                    <td>${effPending ? `<span class="badge bg-label-warning">${esc(t('CapaEffectivenessPending'))}</span>` : text(c.effectivenessResult)}</td>
                    <td>${blockingBadge(open || effPending)}</td>
                </tr>`;
            }).join('');
        };

        const renderAll = () => { renderSummary(); renderEvents(); renderDeviations(); renderCapa(); };

        const load = async () => {
            hideAlert('qualityAlert');
            const [evRes, devRes, capaRes] = await Promise.all([
                perms.canViewQualityEvents ? getJson('/quality-events') : Promise.resolve({ ok: true, payload: {} }),
                perms.canViewDeviations ? getJson('/quality-events/deviations') : Promise.resolve({ ok: true, payload: {} }),
                perms.canViewCapa ? getJson('/quality-events/capa') : Promise.resolve({ ok: true, payload: {} })
            ]);
            events = (evRes.ok ? unwrapList(evRes.payload) : []).filter((e) => sameEntry(e.registerEntryId));
            const eventIds = new Set(events.map((e) => String(e.id || '').toLowerCase()));
            deviations = (devRes.ok ? unwrapList(devRes.payload) : []).filter((d) => eventIds.has(String(d.qualityEventId || '').toLowerCase()));
            const deviationIds = new Set(deviations.map((d) => String(d.id || '').toLowerCase()));
            capa = (capaRes.ok ? unwrapList(capaRes.payload) : []).filter((c) =>
                anySameEntry(c.relatedRegisterEntryIds)
                || eventIds.has(String(c.qualityEventId || '').toLowerCase())
                || deviationIds.has(String(c.deviationId || '').toLowerCase()));
            renderAll();
            loaded = true;
        };

        const bind = () => {
            document.getElementById('btnReloadQuality')?.addEventListener('click', () => void load());
        };

        return { bind, ensureLoaded: () => { if (!loaded) void load(); }, invalidate: () => { loaded = false; } };
    })();

    // A record is not a controlled document: the governance tabs below do not apply to it (no identifier allocation,
    // lifecycle, approval, release gates, training, controlled copies or e-signatures). Only General + Retention remain.
    const RECORD_HIDDEN_TABS = ['identifiers', 'lifecycle', 'approval', 'gates', 'training', 'repository', 'signatures', 'quality'];
    const applyRecordTabVisibility = async () => {
        try {
            const res = await fetch(`/DocumentManagement/MasterRegister/api/detail/${encodeURIComponent(entryId)}`,
                { credentials: 'same-origin', headers: getAuthHeaders() });
            const payload = await res.json().catch(() => ({}));
            if (!res.ok || payload?.isSuccessful === false) return;
            const d = unwrap(payload);
            if (!d || d.isRecord !== true) return;
            RECORD_HIDDEN_TABS.forEach((key) => {
                (document.getElementById(`tabBtn-${key}`)?.closest('li') || document.getElementById(`tabBtn-${key}`))?.classList.add('d-none');
                document.getElementById(`tab-${key}`)?.classList.add('d-none');
            });
        } catch {
            // Non-fatal: on failure every tab simply stays visible.
        }
    };

    return {
        init: function () {
            if (!host || !entryId) return;

            void applyRecordTabVisibility();
            Identifiers.bind();
            Lifecycle.bind();
            Approval.bind();
            ReleaseGates.bind();
            Training.bind();
            Repository.bind();
            Retention.bind();
            Signatures.bind();
            QualityEvents.bind();

            // Lazy load: nothing is fetched until the tab is actually shown for the first time.
            document.getElementById('tabBtn-identifiers')?.addEventListener('shown.bs.tab', () => Identifiers.ensureLoaded());
            document.getElementById('tabBtn-lifecycle')?.addEventListener('shown.bs.tab', () => Lifecycle.ensureLoaded());
            document.getElementById('tabBtn-approval')?.addEventListener('shown.bs.tab', () => Approval.ensureLoaded());
            document.getElementById('tabBtn-gates')?.addEventListener('shown.bs.tab', () => ReleaseGates.ensureLoaded());
            document.getElementById('tabBtn-training')?.addEventListener('shown.bs.tab', () => Training.ensureLoaded());
            document.getElementById('tabBtn-repository')?.addEventListener('shown.bs.tab', () => Repository.ensureLoaded());
            document.getElementById('tabBtn-retention')?.addEventListener('shown.bs.tab', () => Retention.ensureLoaded());
            document.getElementById('tabBtn-signatures')?.addEventListener('shown.bs.tab', () => Signatures.ensureLoaded());
            document.getElementById('tabBtn-quality')?.addEventListener('shown.bs.tab', () => QualityEvents.ensureLoaded());
        }
    };
})();

document.addEventListener('DOMContentLoaded', function () {
    MasterRegisterGovernance.init();
});
