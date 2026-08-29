/**
 * MOD-0029-FU28A — tenant-global Repository Assessment master data (GMG-QMS-SOP-0001 §11).
 *
 * Profile (same as the MOD-0029 master register screens): same-origin MVC proxy only, no direct Platform 5057 call,
 * no tenant id from the browser, anti-forgery token on every mutation, HTML-escaped server messages.
 *
 * Governance stance: this screen CLASSIFIES a repository against the SOP boundary and records a governance decision.
 * It never asserts computer-system validation, never asserts electronic-signature compliance, and never derives a
 * boundary result client-side — CanSupportReleaseGate / CanSupportRegulatedESignature / BoundaryStatement are read
 * from the backend evaluator verbatim. Evaluating does not approve; approving does not validate.
 */
'use strict';

const RepositoryAssessmentCommon = (function () {
    const endpoint = '/DocumentManagement/RepositoryAssessments/api';

    const L = () => window.L10n || {};
    const t = (key) => L()[key] || key;
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const perms = () => window.RepositoryAssessmentPerms || {};

    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c]));
    const na = () => t('NotAvailable');
    const text = (v) => (v === null || v === undefined || v === '' ? na() : esc(v));
    const fmtDate = (v) => {
        if (!v) return na();
        const d = new Date(v);
        return Number.isNaN(d.getTime()) ? na() : d.toLocaleDateString();
    };
    const fmtDateTime = (v) => {
        if (!v) return na();
        const d = new Date(v);
        return Number.isNaN(d.getTime()) ? na() : d.toLocaleString();
    };
    const unwrap = (p) => p?.data ?? p?.Data ?? null;
    const unwrapList = (p) => {
        const d = unwrap(p) ?? p;
        if (Array.isArray(d)) return d;
        for (const k of ['items', 'Items', 'results', 'Results']) if (Array.isArray(d?.[k])) return d[k];
        return [];
    };

    const badge = (colour, label) => `<span class="badge bg-label-${colour}">${esc(label)}</span>`;
    const boolBadge = (v) => badge(v ? 'success' : 'secondary', v ? t('Yes') : t('No'));

    const typeLabel = (v) => (v ? t(`RepositoryType${v}`) : na());
    const statusBadge = (v) => {
        const key = String(v || '');
        const map = { Draft: 'secondary', UnderReview: 'info', Approved: 'success', Rejected: 'danger', Expired: 'warning', Superseded: 'dark' };
        return key ? badge(map[key] || 'secondary', t(`RepositoryStatus${key}`)) : na();
    };
    const locationTypeLabel = (v) => (v ? t(`LocationType${v}`) : na());
    const findingTypeLabel = (v) => (v ? t(`FindingType${v}`) : na());
    const severityBadge = (v) => {
        const key = String(v || '');
        const map = { Warning: 'warning', Major: 'danger', Critical: 'danger' };
        return key ? badge(map[key] || 'secondary', t(`Severity${key}`)) : na();
    };
    const findingStatusBadge = (v) => {
        const key = String(v || '');
        const map = { Open: 'danger', Resolved: 'success', AcceptedAsInterimRisk: 'warning', Closed: 'secondary' };
        return key ? badge(map[key] || 'secondary', t(`FindingStatus${key}`)) : na();
    };

    /** Boundary notes per repository type — the SOP §11 classification, not a compliance statement. */
    const TYPE_NOTE = {
        ValidatedDms: { cls: 'alert-success', key: 'ValidatedDmsNote' },
        ApprovedInterimRepository: { cls: 'alert-warning', key: 'InterimRepositoryWarning' },
        SeparateApprovalMechanism: { cls: 'alert-info', key: 'SeparateApprovalMechanismNote' },
        UnapprovedRepository: { cls: 'alert-danger', key: 'UnapprovedRepositoryWarning' }
    };
    const renderTypeNote = (elementId, repositoryType) => {
        const box = document.getElementById(elementId);
        if (!box) return;
        const note = TYPE_NOTE[repositoryType];
        if (!note) { box.classList.add('d-none'); return; }
        box.className = `alert ${note.cls}`;
        box.textContent = t(note.key);
        box.classList.remove('d-none');
    };

    const REASON_CODE_KEYS = {
        ASSESSMENT_NOT_FOUND: 'AssessmentNotFound',
        NOT_FOUND_NON_LEAKAGE: 'AssessmentNotFound',
        NAME_AND_TYPE_REQUIRED: 'NameAndTypeRequired',
        REQUIRED_FIELDS_MISSING: 'RequiredFieldsMissing',
        ALREADY_DECIDED: 'AlreadyDecided',
        REASON_REQUIRED: 'ReasonRequiredError',
        APPROVER_ROLE_INVALID: 'ApproverRoleInvalid',
        LINK_STATUS_INVALID: 'LinkStatusInvalid',
        VALIDATION_FAILED: 'ValidationFailed',
        PERMISSION_DENIED: 'Forbidden'
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

    const getJson = async (path) => {
        const res = await fetch(`${endpoint}${path}`, { credentials: 'same-origin', headers: getAuthHeaders() });
        const payload = await res.json().catch(() => ({}));
        return { res, payload, ok: res.ok && payload?.isSuccessful !== false };
    };

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
            console.error('[RepositoryAssessments] Request failed.', error);
            return { res: { status: 0 }, payload: {}, ok: false };
        } finally {
            if (button) button.disabled = false;
        }
    };

    /** Decision (approve/reject) modal is shared by the list and the details page. */
    const openDecisionModal = (mode, assessmentId, label) => {
        const modalEl = document.getElementById('assessmentDecisionModal');
        if (!modalEl) return;
        const form = document.getElementById('assessmentDecisionForm');
        form.reset();
        form.classList.remove('was-validated');

        const isApprove = mode === 'approve';
        document.getElementById('assessmentDecisionId').value = assessmentId;
        document.getElementById('assessmentDecisionMode').value = mode;
        document.getElementById('assessmentDecisionLabel').value = label || '';
        document.getElementById('assessmentDecisionTitle').textContent = t(isApprove ? 'ApproveRepository' : 'RejectRepository');
        document.getElementById('assessmentDecisionSubmit').textContent = t(isApprove ? 'ApproveRepository' : 'RejectRepository');
        document.getElementById('assessmentDecisionNote').textContent = t(isApprove ? 'ApproveDoesNotValidate' : 'RejectDoesNotDelete');
        document.getElementById('assessmentApproveFields').classList.toggle('d-none', !isApprove);
        document.getElementById('assessmentRejectFields').classList.toggle('d-none', isApprove);
        document.getElementById('assessmentRejectionReason').required = !isApprove;

        window.bootstrap?.Modal.getOrCreateInstance(modalEl).show();
    };

    const bindDecisionForm = (onSuccess, alertId) => {
        document.getElementById('assessmentDecisionForm')?.addEventListener('submit', async (event) => {
            event.preventDefault();
            const form = event.currentTarget;
            if (!form.checkValidity()) { form.classList.add('was-validated'); return; }

            const assessmentId = document.getElementById('assessmentDecisionId').value;
            const isApprove = document.getElementById('assessmentDecisionMode').value === 'approve';
            const validUntil = document.getElementById('assessmentValidUntil').value;
            const payload = isApprove
                ? {
                    approvedByRole: document.getElementById('assessmentApproverRole').value,
                    validUntil: validUntil ? new Date(`${validUntil}T00:00:00`).toISOString() : null
                }
                : { reason: document.getElementById('assessmentRejectionReason').value.trim() };

            const result = await postJson(`/${encodeURIComponent(assessmentId)}/${isApprove ? 'approve' : 'reject'}`,
                payload, form.querySelector('[type="submit"]'));
            if (!result.ok) { handleFailure(result.res, result.payload, alertId); return; }

            window.bootstrap?.Modal.getOrCreateInstance(document.getElementById('assessmentDecisionModal')).hide();
            window.showToast?.(t(isApprove ? 'ApprovalSucceeded' : 'RejectionSucceeded'), 'success');
            await onSuccess();
        });
    };

    return {
        endpoint, L, t, token, getAuthHeaders, perms, esc, na, text, fmtDate, fmtDateTime,
        unwrap, unwrapList, badge, boolBadge, typeLabel, statusBadge, locationTypeLabel,
        findingTypeLabel, severityBadge, findingStatusBadge, renderTypeNote,
        describeFailure, showAlert, hideAlert, handleFailure, getJson, postJson,
        openDecisionModal, bindDecisionForm
    };
})();

// ── Index (list) ─────────────────────────────────────────────────────────────
const RepositoryAssessmentList = (function () {
    const C = RepositoryAssessmentCommon;
    let dt;

    const dtTableEl = document.querySelector('.datatables-repositoryassessments');
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const totalColumnCount = 10;
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8];
    const baseOrder = [[1, 'asc']];

    const emptyFilters = () => ({ repositoryType: [], assessmentStatus: [], locationType: [], ownerRole: '' });
    let appliedFilters = emptyFilters();

    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const hasFilterValue = (v) => (Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0);
    const matchesMulti = (selected, actual) => {
        const norm = normalizeArray(selected).map((s) => s.toUpperCase());
        return !norm.length || norm.includes(normalizeString(actual).toUpperCase());
    };
    const matchesContains = (needle, actual) => {
        const n = normalizeString(needle).toLowerCase();
        return !n || normalizeString(actual).toLowerCase().includes(n);
    };
    const getAppliedFilterCount = () => Object.values(appliedFilters).filter(hasFilterValue).length;

    const mountInlineFilter = () => {
        const host = document.getElementById(filterHostId);
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
        }
    };
    const toggleInlineFilter = () => {
        const collapseEl = document.getElementById(filterCollapseId);
        if (collapseEl) bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
    };
    const bindInlineFilterA11y = () => {
        const btn = document.querySelector('.dt-filter-btn');
        const collapseEl = document.getElementById(filterCollapseId);
        if (!btn || !collapseEl || btn.dataset.bound) return;
        btn.dataset.bound = '1';
        collapseEl.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
        collapseEl.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));
    };

    // The FU16 list endpoint takes no query parameters, so filtering is a client-side predicate over the page.
    const registerTableFilters = () => {
        if (!dtTableEl || !window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl.dataset.compactFilterBound === '1') return;
        dtTableEl.dataset.compactFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _sd, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) return true;
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) return true;
            return matchesMulti(appliedFilters.repositoryType, row.repositoryType)
                && matchesMulti(appliedFilters.assessmentStatus, row.assessmentStatus)
                && matchesMulti(appliedFilters.locationType, row.locationType)
                && matchesContains(appliedFilters.ownerRole, row.repositoryOwnerRole);
        });
    };

    const initSelect2Filters = () => {
        if (!window.jQuery || !$.fn.select2) return;
        const $body = $(document.body);
        $('#filterRepositoryType, #filterAssessmentStatus, #filterLocationType').each(function () {
            const $s = $(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({
                dropdownParent: $body,
                dropdownCssClass: 'dt-inline-filter-dropdown',
                containerCssClass: 'dt-inline-filter-multi',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $s.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element',
                closeOnSelect: false
            });
        });
    };

    const setupFilters = (api) => {
        initSelect2Filters();
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                repositoryType: $('#filterRepositoryType').val() || [],
                assessmentStatus: $('#filterAssessmentStatus').val() || [],
                locationType: $('#filterLocationType').val() || [],
                ownerRole: document.getElementById('filterOwnerRole')?.value || ''
            };
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
            const collapseEl = document.getElementById(filterCollapseId);
            if (collapseEl) bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (e) => {
            e.preventDefault();
            appliedFilters = emptyFilters();
            $('#filterRepositoryType, #filterAssessmentStatus, #filterLocationType').val(null).trigger('change');
            const owner = document.getElementById('filterOwnerRole');
            if (owner) owner.value = '';
            api.search('');
            api.order(baseOrder);
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
        });
    };

    // Only a not-yet-decided assessment can be approved/rejected; an approved/superseded one is read-only.
    const isDecidable = (status) => status === 'Draft' || status === 'UnderReview';

    const rowActionHandlers = {
        quickView: ({ id }) => { if (id) window.location.href = `/DocumentManagementRepositoryAssessments/Details/${id}`; },
        edit: ({ id }) => { if (id) window.location.href = `/DocumentManagementRepositoryAssessments/Edit/${id}`; },
        evaluate: async ({ id }) => {
            // Evaluate CLASSIFIES the repository and returns the boundary result — it does not approve anything.
            const result = await C.postJson(`/${encodeURIComponent(id)}/evaluate`, {});
            if (!result.ok) { C.handleFailure(result.res, result.payload, 'listAlert'); return; }
            window.showToast?.(C.t('EvaluationSucceeded'), 'success');
            dt?.ajax.reload(null, false);
        },
        approve: ({ id, row }) => C.openDecisionModal('approve', id, row?.repositoryName),
        reject: ({ id, row }) => C.openDecisionModal('reject', id, row?.repositoryName)
    };

    const buildRowActions = (full) => {
        const L = C.L();
        const perms = C.perms();
        const actions = [
            { key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': full.id, 'title': L.ViewDetails } }
        ];
        const rowJson = JSON.stringify(full).replace(/'/g, '&#39;');
        if (perms.canManage) {
            actions.push({ key: 'evaluate', icon: 'bx bx-play-circle', text: L.EvaluateRepository, attrs: { 'data-id': full.id } });
            if (isDecidable(full.assessmentStatus)) {
                actions.push({ key: 'edit', icon: 'bx bx-edit', text: L.Edit, attrs: { 'data-id': full.id } });
            }
        }
        if (perms.canApprove && isDecidable(full.assessmentStatus)) {
            actions.push({ key: 'approve', icon: 'bx bx-check-circle', text: L.ApproveRepository, attrs: { 'data-id': full.id, 'data-json': rowJson } });
            actions.push({ key: 'reject', className: 'text-danger', icon: 'bx bx-x-circle', text: L.RejectRepository, attrs: { 'data-id': full.id, 'data-json': rowJson } });
        }
        return window.DitenDataTable.renderActions(actions);
    };

    const init = () => {
        if (!dtTableEl) return;
        const L = C.L();
        registerTableFilters();

        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => toggleInlineFilter()
            }
        };

        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            ajax: {
                url: `${C.endpoint}/list`,
                type: 'GET',
                headers: C.getAuthHeaders(),
                error: (xhr) => {
                    let payload = {};
                    try { payload = JSON.parse(xhr?.responseText || '{}'); } catch (e) { payload = {}; }
                    C.handleFailure({ status: xhr?.status }, payload, 'listAlert');
                }
            },
            actions: { onRowAction: rowActionHandlers },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                language: { emptyTable: L.NoRepositoryAssessmentsFound, zeroRecords: L.NoRepositoryAssessmentsFound },
                order: baseOrder,
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'repositoryName', name: 'repositoryName' },
                    { data: 'repositoryType', name: 'repositoryType' },
                    { data: 'assessmentStatus', name: 'assessmentStatus' },
                    { data: 'repositoryOwnerRole', name: 'repositoryOwnerRole' },
                    { data: 'exactLocation', name: 'exactLocation' },
                    { data: 'locationType', name: 'locationType' },
                    { data: 'approvedAt', name: 'approvedAt' },
                    { data: 'validUntil', name: 'validUntil' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading">${C.text(data)}</span>` },
                    { targets: 2, render: (data) => C.esc(C.typeLabel(data)) },
                    { targets: 3, orderable: false, render: (data) => C.statusBadge(data) },
                    { targets: 4, render: (data) => C.text(data) },
                    { targets: 5, render: (data) => C.text(data) },
                    { targets: 6, render: (data) => C.esc(C.locationTypeLabel(data)) },
                    { targets: 7, searchable: false, render: (data) => C.fmtDate(data) },
                    { targets: 8, searchable: false, render: (data) => C.fmtDate(data) },
                    {
                        targets: -1,
                        title: L.Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => buildRowActions(full)
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    C.perms().canManage ? (L.RepositoryAssessmentCreate || L.AddNew) : null,
                    { href: '/DocumentManagementRepositoryAssessments/Create' },
                    extraButtons,
                    { exportColumns: saveViewColumnIndexes, colvisColumns: saveViewColumnIndexes }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    setupFilters(this.api());
                    document.querySelector('.add-new')?.addEventListener('click', (e) => {
                        e.preventDefault();
                        window.location.href = '/DocumentManagementRepositoryAssessments/Create';
                    });
                    document.getElementById('skeleton-loader')?.classList.add('d-none');
                },
                drawCallback: function () {
                    window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
                }
            }
        });

        dt.on('column-visibility.dt column-reorder.dt columns-reordered.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
        });

        C.bindDecisionForm(async () => { dt?.ajax.reload(null, false); }, 'listAlert');

        if (totalColumnCount !== dtTableEl.querySelectorAll('thead th').length) {
            console.warn('[RepositoryAssessments] Column count drift between markup and script.');
        }
    };

    return { init };
})();

// ── Create / Edit form ───────────────────────────────────────────────────────
const RepositoryAssessmentForm = (function () {
    const C = RepositoryAssessmentCommon;

    const val = (id) => document.getElementById(id)?.value?.trim() || '';
    const checked = (id) => document.getElementById(id)?.checked === true;
    const setVal = (id, v) => { const el = document.getElementById(id); if (el) el.value = v ?? ''; };
    const setChecked = (id, v) => { const el = document.getElementById(id); if (el) el.checked = v === true; };
    const nullable = (v) => (v === '' ? null : v);
    const nullableInt = (v) => {
        if (v === '') return null;
        const n = Number(v);
        return Number.isFinite(n) ? Math.trunc(n) : null;
    };
    const toIsoDate = (v) => (v ? new Date(`${v}T00:00:00`).toISOString() : null);
    const toDateInput = (v) => {
        if (!v) return '';
        const d = new Date(v);
        return Number.isNaN(d.getTime()) ? '' : d.toISOString().slice(0, 10);
    };

    // Mirrors RepositoryAssessmentFieldsInput one-for-one — no invented field names.
    const buildPayload = () => ({
        repositoryName: val('repositoryName'),
        repositoryType: val('repositoryType'),
        locationType: nullable(val('locationType')),
        repositoryOwnerUserId: null,
        repositoryOwnerRole: nullable(val('repositoryOwnerRole')),
        exactLocation: nullable(val('exactLocation')),
        accessModelDescription: nullable(val('accessModelDescription')),
        accessReviewFrequency: nullable(val('accessReviewFrequency')),
        backupMethodDescription: nullable(val('backupMethodDescription')),
        restoreTestFrequency: nullable(val('restoreTestFrequency')),
        approvalMechanismDescription: nullable(val('approvalMechanismDescription')),
        effectiveCopyControlDescription: nullable(val('effectiveCopyControlDescription')),
        auditTrailDescription: nullable(val('auditTrailDescription')),
        changeControlDescription: nullable(val('changeControlDescription')),
        validationEvidenceReference: nullable(val('validationEvidenceReference')),
        maxInterimPeriodDays: nullableInt(val('maxInterimPeriodDays')),
        interimCheckpointDueDate: toIsoDate(val('interimCheckpointDueDate')),
        migrationReconciliationRequired: checked('migrationReconciliationRequired'),
        migrationReconciliationReference: nullable(val('migrationReconciliationReference')),
        assessmentEvidenceReference: nullable(val('assessmentEvidenceReference'))
    });

    const hydrate = async (assessmentId) => {
        const result = await C.getJson(`/${encodeURIComponent(assessmentId)}`);
        if (!result.ok) {
            C.handleFailure(result.res, result.payload, 'formAlert');
            return;
        }
        const a = C.unwrap(result.payload);
        if (!a) return;

        setVal('repositoryName', a.repositoryName);
        setVal('repositoryType', a.repositoryType);
        setVal('locationType', a.locationType);
        setVal('repositoryOwnerRole', a.repositoryOwnerRole);
        setVal('exactLocation', a.exactLocation);
        setVal('accessModelDescription', a.accessModelDescription);
        setVal('accessReviewFrequency', a.accessReviewFrequency);
        setVal('approvalMechanismDescription', a.approvalMechanismDescription);
        setVal('effectiveCopyControlDescription', a.effectiveCopyControlDescription);
        setVal('backupMethodDescription', a.backupMethodDescription);
        setVal('restoreTestFrequency', a.restoreTestFrequency);
        setVal('auditTrailDescription', a.auditTrailDescription);
        setVal('changeControlDescription', a.changeControlDescription);
        setVal('validationEvidenceReference', a.validationEvidenceReference);
        setVal('maxInterimPeriodDays', a.maxInterimPeriodDays);
        setVal('interimCheckpointDueDate', toDateInput(a.interimCheckpointDueDate));
        setChecked('migrationReconciliationRequired', a.migrationReconciliationRequired);
        setVal('migrationReconciliationReference', a.migrationReconciliationReference);
        setVal('assessmentEvidenceReference', a.assessmentEvidenceReference);

        C.renderTypeNote('repositoryTypeNote', a.repositoryType);

        // An approved/superseded assessment cannot be edited (backend: ALREADY_DECIDED) — say so before saving fails.
        if (a.assessmentStatus === 'Approved' || a.assessmentStatus === 'Superseded') {
            C.showAlert('formAlert', C.t('ApprovedAssessmentReadOnly'));
        }
    };

    const submit = async (form) => {
        C.hideAlert('formAlert');
        if (!form.checkValidity()) {
            form.classList.add('was-validated');
            window.showToast?.(C.t('ValidationFailed'), 'error');
            return;
        }

        const isEdit = form.dataset.formMode === 'edit';
        const assessmentId = form.dataset.assessmentId || '';
        const path = isEdit ? `/${encodeURIComponent(assessmentId)}/update` : '/create';

        const result = await C.postJson(path, buildPayload(), form.querySelector('[type="submit"]'));
        if (!result.ok) {
            C.handleFailure(result.res, result.payload, 'formAlert');
            return;
        }

        window.showToast?.(C.t('SaveSucceeded'), 'success');
        const saved = C.unwrap(result.payload);
        const savedId = saved?.id || saved?.Id || assessmentId;
        window.location.href = savedId
            ? `/DocumentManagementRepositoryAssessments/Details/${savedId}`
            : '/DocumentManagementRepositoryAssessments';
    };

    return {
        init: function () {
            const form = document.getElementById('repositoryAssessmentForm');
            if (!form) return;

            const typeSelect = document.getElementById('repositoryType');
            typeSelect?.addEventListener('change', () => C.renderTypeNote('repositoryTypeNote', typeSelect.value));
            if (typeSelect) C.renderTypeNote('repositoryTypeNote', typeSelect.value);

            if (form.dataset.formMode === 'edit' && form.dataset.assessmentId) {
                void hydrate(form.dataset.assessmentId);
            }

            form.addEventListener('submit', (event) => {
                event.preventDefault();
                void submit(form);
            });
        }
    };
})();

// ── Details ──────────────────────────────────────────────────────────────────
const RepositoryAssessmentDetails = (function () {
    const C = RepositoryAssessmentCommon;
    let assessment = null;
    let boundary = null;
    let findings = [];

    const row = (label, valueHtml) =>
        `<dt class="col-sm-5 fw-normal text-muted">${C.esc(label)}</dt><dd class="col-sm-7">${valueHtml}</dd>`;
    const fill = (id, rows) => { const el = document.getElementById(id); if (el) el.innerHTML = rows.join(''); };

    const summaryCard = (colour, icon, value, label) => `
        <div class="col-6 col-lg-3">
            <div class="card h-100">
                <div class="card-body d-flex align-items-center gap-3">
                    <div class="avatar"><span class="avatar-initial rounded bg-label-${colour}"><i class="bx ${icon}"></i></span></div>
                    <div>
                        <h5 class="mb-0">${C.esc(String(value ?? ''))}</h5>
                        <small class="text-muted">${C.esc(label)}</small>
                    </div>
                </div>
            </div>
        </div>`;

    const criticalOpenCount = () =>
        findings.filter((f) => f.severity === 'Critical' && f.status !== 'Resolved' && f.status !== 'Closed').length;

    const render = () => {
        const a = assessment || {};
        const t = C.t;

        document.getElementById('detailTitle').textContent = a.repositoryName || t('RepositoryAssessmentDetails');
        document.getElementById('detailSubtitle').textContent =
            [C.typeLabel(a.repositoryType), a.repositoryKey].filter(Boolean).join(' • ');

        C.renderTypeNote('repositoryTypeWarning', a.repositoryType);

        const critical = criticalOpenCount();
        const cards = document.getElementById('detailSummaryCards');
        if (cards) {
            cards.innerHTML = [
                summaryCard(a.assessmentStatus === 'Approved' ? 'success' : 'secondary', 'bx-badge-check',
                    t(`RepositoryStatus${a.assessmentStatus || 'Draft'}`), t('AssessmentStatus')),
                summaryCard(a.repositoryType === 'ValidatedDms' ? 'success' : (a.repositoryType === 'UnapprovedRepository' ? 'danger' : 'warning'),
                    'bx-server', C.typeLabel(a.repositoryType), t('RepositoryType')),
                summaryCard(boundary?.canSupportReleaseGate ? 'success' : 'warning', 'bx-log-in-circle',
                    boundary ? (boundary.canSupportReleaseGate ? t('Yes') : t('No')) : C.na(), t('CanSupportReleaseGate')),
                summaryCard(critical > 0 ? 'danger' : 'success', 'bx-error-circle', critical, t('CriticalFindings'))
            ].join('');
        }

        fill('detailBasicList', [
            row(t('RepositoryName'), C.text(a.repositoryName)),
            row(t('RepositoryKey'), C.text(a.repositoryKey)),
            row(t('RepositoryType'), C.esc(C.typeLabel(a.repositoryType))),
            row(t('AssessmentStatus'), C.statusBadge(a.assessmentStatus)),
            row(t('RepositoryOwner'), C.text(a.repositoryOwnerRole)),
            row(t('LocationType'), C.esc(C.locationTypeLabel(a.locationType))),
            row(t('RepositoryLocation'), C.text(a.exactLocation))
        ]);

        fill('detailAccessList', [
            row(t('AccessControlModel'), C.text(a.accessModelDescription)),
            row(t('AccessReviewFrequency'), C.text(a.accessReviewFrequency)),
            row(t('ApprovalMechanism'), C.text(a.approvalMechanismDescription)),
            row(t('EffectiveCopyControl'), C.text(a.effectiveCopyControlDescription))
        ]);

        fill('detailTechnicalList', [
            row(t('BackupPlan'), C.text(a.backupMethodDescription)),
            row(t('RestoreTestEvidence'), C.text(a.restoreTestFrequency)),
            row(t('AuditTrail'), C.text(a.auditTrailDescription)),
            row(t('ChangeControl'), C.text(a.changeControlDescription)),
            row(t('ValidationEvidence'), C.text(a.validationEvidenceReference))
        ]);

        fill('detailInterimList', [
            row(t('MaxInterimPeriodDays'), C.text(a.maxInterimPeriodDays)),
            row(t('InterimCheckpointDueDate'), C.esc(C.fmtDate(a.interimCheckpointDueDate))),
            row(t('MigrationReconciliationRequired'), C.boolBadge(a.migrationReconciliationRequired === true)),
            row(t('MigrationReconciliationEvidence'), C.text(a.migrationReconciliationReference))
        ]);

        fill('detailEvidenceList', [
            row(t('EvidenceReference'), C.text(a.assessmentEvidenceReference)),
            row(t('ApprovedBy'), C.text(a.approvedByRole)),
            row(t('ApprovedAt'), C.esc(C.fmtDateTime(a.approvedAt))),
            row(t('ValidFrom'), C.esc(C.fmtDate(a.validFrom))),
            row(t('ValidUntil'), C.esc(C.fmtDate(a.validUntil))),
            row(t('RejectionReason'), C.text(a.rejectionReason))
        ]);

        renderBoundary();
        renderFindings();

        // Approve/reject only make sense while the assessment is still open.
        const decidable = a.assessmentStatus === 'Draft' || a.assessmentStatus === 'UnderReview';
        document.getElementById('btnApproveDetail')?.classList.toggle('d-none', !decidable);
        document.getElementById('btnRejectDetail')?.classList.toggle('d-none', !decidable);
        document.getElementById('btnEditDetail')?.classList.toggle('d-none', !decidable);
    };

    // The boundary statement is the backend evaluator's own wording, rendered verbatim (escaped). The UI never
    // softens it, never rewrites it, and never adds a compliance claim of its own.
    const renderBoundary = () => {
        const panel = document.getElementById('boundaryPanel');
        if (!panel) return;
        if (!boundary) {
            panel.innerHTML = `<span class="text-muted">${C.esc(C.t('EvaluateToSeeBoundary'))}</span>`;
            return;
        }
        const blocking = Array.isArray(boundary.blockingFindings) ? boundary.blockingFindings : [];
        const warnings = Array.isArray(boundary.warningFindings) ? boundary.warningFindings : [];
        const list = (items, colour, titleKey) => items.length
            ? `<div class="mt-3"><strong class="small text-${colour}">${C.esc(C.t(titleKey))}</strong>
                 <ul class="mb-0 mt-1 small">${items.map((f) => `<li>${C.esc(f.description || C.findingTypeLabel(f.findingType))}</li>`).join('')}</ul></div>`
            : '';

        panel.innerHTML = `
            <p class="mb-3">${C.esc(boundary.boundaryStatement || C.na())}</p>
            <dl class="row mb-0 small">
                <dt class="col-7 fw-normal text-muted">${C.esc(C.t('CanSupportReleaseGate'))}</dt>
                <dd class="col-5">${C.boolBadge(boundary.canSupportReleaseGate === true)}</dd>
                <dt class="col-7 fw-normal text-muted">${C.esc(C.t('CanSupportRegulatedESignature'))}</dt>
                <dd class="col-5">${C.boolBadge(boundary.canSupportRegulatedESignature === true)}</dd>
            </dl>
            ${list(blocking, 'danger', 'BlockingFindings')}
            ${list(warnings, 'warning', 'WarningFindings')}`;
    };

    const renderFindings = () => {
        const body = document.getElementById('assessmentFindingsBody');
        if (!body) return;
        if (!findings.length) {
            body.innerHTML = `<tr><td colspan="5" class="text-center text-muted py-4">${C.esc(C.t('NoFindingsFound'))}</td></tr>`;
            return;
        }
        body.innerHTML = findings.map((f) => {
            const open = f.status === 'Open';
            return `<tr${f.severity === 'Critical' && open ? ' class="table-danger"' : ''}>
                <td>${C.esc(C.findingTypeLabel(f.findingType))}</td>
                <td>${C.severityBadge(f.severity)}</td>
                <td>${C.findingStatusBadge(f.status)}</td>
                <td>${C.text(f.description)}</td>
                <td>${C.text(f.evidenceReference)}</td>
            </tr>`;
        }).join('');
    };

    const load = async (assessmentId) => {
        C.hideAlert('detailAlert');
        const [detailRes, findingsRes] = await Promise.all([
            C.getJson(`/${encodeURIComponent(assessmentId)}`),
            C.getJson(`/${encodeURIComponent(assessmentId)}/findings`)
        ]);

        if (detailRes.ok) assessment = C.unwrap(detailRes.payload);
        else C.handleFailure(detailRes.res, detailRes.payload, 'detailAlert');

        findings = findingsRes.ok ? C.unwrapList(findingsRes.payload) : [];
        render();
    };

    return {
        init: function () {
            const host = document.querySelector('.repository-assessment-details');
            if (!host) return;
            const assessmentId = host.dataset.assessmentId;
            if (!assessmentId) return;

            void load(assessmentId);

            document.getElementById('btnEvaluateDetail')?.addEventListener('click', async (e) => {
                const result = await C.postJson(`/${encodeURIComponent(assessmentId)}/evaluate`, {}, e.currentTarget);
                if (!result.ok) { C.handleFailure(result.res, result.payload, 'detailAlert'); return; }
                boundary = C.unwrap(result.payload);
                window.showToast?.(C.t('EvaluationSucceeded'), 'success');
                await load(assessmentId);
            });

            document.getElementById('btnApproveDetail')?.addEventListener('click', () =>
                C.openDecisionModal('approve', assessmentId, assessment?.repositoryName));
            document.getElementById('btnRejectDetail')?.addEventListener('click', () =>
                C.openDecisionModal('reject', assessmentId, assessment?.repositoryName));

            C.bindDecisionForm(async () => { await load(assessmentId); }, 'detailAlert');
        }
    };
})();

document.addEventListener('DOMContentLoaded', function () {
    RepositoryAssessmentList.init();
    RepositoryAssessmentForm.init();
    RepositoryAssessmentDetails.init();
});
