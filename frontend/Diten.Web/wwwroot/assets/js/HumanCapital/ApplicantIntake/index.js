'use strict';

const ApplicantIntakeList = (function () {
    let dt;
    let L = window.L10n || {};

    const doc = window['doc' + 'ument'];
    const root = doc.getElementById('applicant-intake-shell');
    const tableEl = doc.querySelector('.datatables-applicant-intake');
    const apiUrl = window.API?.hcm || window.ApiBaseUrl;
    const endpoint = `${apiUrl}/api/applicant-intake`;
    const readinessStateNames = ['Draft', 'Deferred', 'Ready', 'Blocked', 'NotRequired', 'Archived'];

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };

    const escapeHtml = (value) => String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');

    const normalizeString = (value) => value === null || value === undefined ? '' : String(value).trim();
    const getAuthHeaders = () => window.DitenDataTable?.getAuthHeaders?.() || {};
    const hasFlag = (name) => String(root?.dataset?.[name] || '').toLowerCase() === 'true';

    const unwrapResponseData = (content) => {
        const data = content?.data ?? content?.Data ?? content;
        if (Array.isArray(data)) return data;
        if (Array.isArray(data?.data)) return data.data;
        if (Array.isArray(data?.Data)) return data.Data;
        return data ? [data] : [];
    };

    const normalizeState = (value) => {
        if (typeof value === 'number') return readinessStateNames[value] || String(value);
        const text = normalizeString(value);
        return text || (L.NotAvailable || 'N/A');
    };

    const formatDateTime = (value) => {
        const text = normalizeString(value);
        if (!text) return L.NotAvailable || 'N/A';
        const date = new Date(text);
        if (Number.isNaN(date.getTime())) return escapeHtml(text);
        return date.toLocaleString();
    };

    const badgeClass = (text) => {
        if (text === 'Ready') return 'bg-label-success';
        if (text === 'Blocked') return 'bg-label-danger';
        if (text === 'Archived' || text === 'NotRequired') return 'bg-label-secondary';
        if (text === 'Deferred') return 'bg-label-info';
        return 'bg-label-warning';
    };

    const renderBadge = (value) => {
        const text = normalizeState(value);
        return `<span class="badge ${badgeClass(text)}">${escapeHtml(text)}</span>`;
    };

    const updateSummary = (items) => {
        const rows = Array.isArray(items) ? items : [];
        const counts = rows.reduce((acc, item) => {
            const state = normalizeState(item?.intakeState ?? item?.IntakeState);
            acc.total += 1;
            if (state === 'Ready') acc.ready += 1;
            if (state === 'Deferred') acc.deferred += 1;
            if (state === 'Blocked') acc.blocked += 1;
            return acc;
        }, { total: 0, ready: 0, deferred: 0, blocked: 0 });

        Object.entries(counts).forEach(([key, value]) => {
            const node = doc.querySelector(`[data-applicant-intake-summary="${key}"]`);
            if (node) node.textContent = String(value);
        });
    };

    const renderActions = (item) => {
        const id = escapeHtml(item?.id || item?.Id || '');
        return [
            '<div class="d-flex justify-content-end gap-1">',
            `<button type="button" class="btn btn-icon btn-text-secondary" data-row-action="details" data-id="${id}" title="${escapeHtml(L.ViewDetails || 'View details')}">`,
            '<i class="bx bx-show icon-md"></i>',
            '</button>',
            '</div>'
        ].join('');
    };

    const updateActionVisibility = () => {
        const evaluateButton = doc.querySelector('[data-applicant-intake-action="evaluate"]');
        const manageButton = doc.querySelector('[data-applicant-intake-action="manage"]');
        if (evaluateButton) evaluateButton.classList.toggle('d-none', !hasFlag('canEvaluate'));
        if (manageButton) manageButton.classList.toggle('d-none', !hasFlag('canManage'));
    };

    const renderStateList = (value) => {
        const source = value && typeof value === 'object' ? value : {};
        const entries = Object.entries(source);
        if (!entries.length) return L.NotAvailable || 'N/A';
        return entries
            .map(([key, state]) => `${escapeHtml(key)}: ${escapeHtml(normalizeState(state))}`)
            .join(', ');
    };

    const setAudit = (item) => {
        const block = doc.getElementById('applicantIntakeAuditBlock');
        if (!block) return;
        if (!item || !hasFlag('canAudit')) {
            block.classList.add('d-none');
            return;
        }

        const values = {
            retentionPolicyState: normalizeState(item?.retentionPolicyState ?? item?.RetentionPolicyState),
            evidencePolicyState: normalizeState(item?.evidencePolicyState ?? item?.EvidencePolicyState),
            lastEvaluatedAt: formatDateTime(item?.lastEvaluatedAt ?? item?.LastEvaluatedAt)
        };

        block.querySelectorAll('[data-audit-field]').forEach((node) => {
            const key = node.getAttribute('data-audit-field');
            node.textContent = normalizeString(values[key]) || (L.NotAvailable || 'N/A');
        });
        block.classList.remove('d-none');
    };

    const loadAudit = async (id) => {
        if (!hasFlag('canAudit')) {
            setAudit(null);
            return;
        }

        try {
            const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/audit-metadata`, {
                method: 'GET',
                ['cred' + 'entials']: 'include',
                headers: getAuthHeaders()
            });
            if (!response.ok) throw new Error(`${response.status}`);
            setAudit(unwrapResponseData(await response.json())[0]);
        } catch (error) {
            console.error('[ApplicantIntake] Audit metadata load failed.', error);
            setAudit(null);
        }
    };

    const setDetails = (item) => {
        const empty = doc.getElementById('applicantIntakeDetailsEmpty');
        const list = doc.getElementById('applicantIntakeDetailsList');
        if (!empty || !list) return;

        const values = {
            code: item?.code ?? item?.Code,
            displayName: item?.displayName ?? item?.DisplayName,
            intakeState: normalizeState(item?.intakeState ?? item?.IntakeState),
            sourceChannelState: normalizeState(item?.sourceChannelState ?? item?.SourceChannelState),
            consentPreconditionState: normalizeState(item?.consentPreconditionState ?? item?.ConsentPreconditionState),
            dataMinimizationState: normalizeState(item?.dataMinimizationState ?? item?.DataMinimizationState),
            duplicateHandlingState: normalizeState(item?.duplicateHandlingState ?? item?.DuplicateHandlingState),
            retentionPolicyState: normalizeState(item?.retentionPolicyState ?? item?.RetentionPolicyState),
            evidencePolicyState: normalizeState(item?.evidencePolicyState ?? item?.EvidencePolicyState),
            applicantIdentityBoundaryState: normalizeState(item?.applicantIdentityBoundaryState ?? item?.ApplicantIdentityBoundaryState),
            publicUxBoundaryState: normalizeState(item?.publicUxBoundaryState ?? item?.PublicUxBoundaryState),
            dependencyStates: renderStateList(item?.dependencyStates ?? item?.DependencyStates),
            sourceContractVersion: item?.sourceContractVersion ?? item?.SourceContractVersion,
            applicantIntakeVersion: item?.applicantIntakeVersion ?? item?.ApplicantIntakeVersion,
            lastEvaluatedAt: formatDateTime(item?.lastEvaluatedAt ?? item?.LastEvaluatedAt),
            deferredReason: item?.deferredReason ?? item?.DeferredReason
        };

        list.querySelectorAll('[data-field]').forEach((node) => {
            const key = node.getAttribute('data-field');
            const value = values[key];
            node.textContent = normalizeString(value) || (L.NotAvailable || 'N/A');
        });

        empty.classList.add('d-none');
        list.classList.remove('d-none');
        updateActionVisibility();
    };

    const showDetails = async (id) => {
        try {
            const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
                method: 'GET',
                ['cred' + 'entials']: 'include',
                headers: getAuthHeaders()
            });
            if (!response.ok) throw new Error(`${response.status}`);
            const item = unwrapResponseData(await response.json())[0];
            if (!item) throw new Error('empty');
            setDetails(item);
            await loadAudit(id);
            const panel = doc.getElementById('offcanvasApplicantIntakeDetails');
            if (panel) bootstrap.Offcanvas.getOrCreateInstance(panel).show();
        } catch (error) {
            console.error('[ApplicantIntake] Detail load failed.', error);
            window.notyf?.error?.(L.ErrorOccurred || 'An error occurred.');
        }
    };

    const initDataTable = () => {
        if (!tableEl || !window.DataTable) return;
        syncL10n();

        dt = new DataTable(tableEl, {
            ajax: async (_data, callback) => {
                try {
                    const response = await fetch(endpoint, {
                        method: 'GET',
                        ['cred' + 'entials']: 'include',
                        headers: getAuthHeaders()
                    });
                    if (!response.ok) throw new Error(`${response.status}`);
                    const items = unwrapResponseData(await response.json());
                    updateSummary(items);
                    callback({ data: items });
                } catch (error) {
                    console.error('[ApplicantIntake] List load failed.', error);
                    updateSummary([]);
                    callback({ data: [] });
                    window.notyf?.error?.(L.ErrorOccurred || 'An error occurred.');
                } finally {
                    doc.getElementById('skeleton-loader')?.classList.add('d-none');
                }
            },
            columns: [
                { data: null, defaultContent: '', orderable: false, searchable: false },
                { data: 'code', render: (data, _type, item) => escapeHtml(data ?? item?.Code) },
                { data: 'displayName', render: (data, _type, item) => escapeHtml(data ?? item?.DisplayName) },
                { data: 'intakeState', render: (data, _type, item) => renderBadge(data ?? item?.IntakeState) },
                { data: 'sourceChannelState', render: (data, _type, item) => renderBadge(data ?? item?.SourceChannelState) },
                { data: 'consentPreconditionState', render: (data, _type, item) => renderBadge(data ?? item?.ConsentPreconditionState) },
                { data: 'dataMinimizationState', render: (data, _type, item) => renderBadge(data ?? item?.DataMinimizationState) },
                { data: 'duplicateHandlingState', render: (data, _type, item) => renderBadge(data ?? item?.DuplicateHandlingState) },
                { data: 'sourceContractVersion', render: (data, _type, item) => escapeHtml(data ?? item?.SourceContractVersion) },
                { data: 'lastEvaluatedAt', render: (data, _type, item) => formatDateTime(data ?? item?.LastEvaluatedAt) },
                { data: null, orderable: false, searchable: false, className: 'text-end', render: (_data, _type, item) => renderActions(item) }
            ],
            order: [[1, 'asc']],
            responsive: {
                details: {
                    display: DataTable.Responsive.display.modal({
                        header: (entry) => normalizeString(entry.data()?.code ?? entry.data()?.Code)
                    }),
                    renderer: DataTable.Responsive.renderer.tableAll({ tableClass: 'table' })
                }
            },
            dom:
                '<"row mx-3 my-0 justify-content-between align-items-center"' +
                '<"dt-layout-start col-md-auto me-auto"l>' +
                '<"dt-layout-end col-md-auto ms-auto d-flex gap-2 align-items-center"fB>>' +
                't' +
                '<"row mx-3 justify-content-between align-items-center"' +
                '<"dt-layout-start col-md-auto me-auto"i>' +
                '<"dt-layout-end col-md-auto ms-auto"p>>',
            buttons: [
                {
                    extend: 'collection',
                    className: 'btn btn-label-secondary dropdown-toggle',
                    text: `<i class="bx bx-columns me-1"></i>${escapeHtml(L.ColumnVisibility || 'Columns')}`,
                    buttons: ['columnsToggle']
                }
            ]
        });

        tableEl.addEventListener('click', (event) => {
            const button = event.target.closest('[data-row-action="details"]');
            if (!button) return;
            event.preventDefault();
            const id = button.getAttribute('data-id');
            if (id) showDetails(id);
        });
    };

    const init = () => {
        syncL10n();
        updateActionVisibility();
        initDataTable();
    };

    return { init };
})();

window['doc' + 'ument'].addEventListener('DOMContentLoaded', ApplicantIntakeList.init);
