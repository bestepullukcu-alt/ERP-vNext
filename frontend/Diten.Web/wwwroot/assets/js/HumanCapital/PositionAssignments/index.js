'use strict';

const PositionAssignmentsList = (function () {
    let dt;
    let L = window.L10n || {};

    const root = document.getElementById('position-assignments-shell');
    const tableEl = document.querySelector('.datatables-position-assignments');
    const apiUrl = window.API?.hcm || window.ApiBaseUrl;
    const endpoint = `${apiUrl}/api/position-assignments`;
    const assignmentStateNames = ['Deferred', 'Active', 'Archived'];
    const referenceStateNames = ['Deferred', 'Validated', 'Rejected'];
    const sensitiveStateNames = ['Deferred', 'Allowed', 'Denied'];

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

    const normalizeState = (value, names) => {
        if (typeof value === 'number') return names[value] || String(value);
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

    const badgeClass = (text, kind) => {
        if (kind === 'assignment') {
            if (text === 'Active') return 'bg-label-success';
            if (text === 'Archived') return 'bg-label-secondary';
            return 'bg-label-info';
        }
        if (kind === 'reference') {
            if (text === 'Validated') return 'bg-label-success';
            if (text === 'Rejected') return 'bg-label-danger';
            return 'bg-label-info';
        }
        if (text === 'Allowed') return 'bg-label-success';
        if (text === 'Denied') return 'bg-label-danger';
        return 'bg-label-info';
    };

    const renderBadge = (value, kind) => {
        const names = kind === 'assignment'
            ? assignmentStateNames
            : kind === 'reference'
                ? referenceStateNames
                : sensitiveStateNames;
        const text = normalizeState(value, names);
        return `<span class="badge ${badgeClass(text, kind)}">${escapeHtml(text)}</span>`;
    };

    const loadHealth = async () => {
        try {
            const response = await fetch(`${endpoint}/health`, {
                method: 'GET',
                credentials: 'include',
                headers: getAuthHeaders()
            });
            if (!response.ok) throw new Error(`${response.status}`);
            const item = unwrapResponseData(await response.json())[0];
            const owner = document.querySelector('[data-position-field="healthOwnerKey"]');
            const status = document.querySelector('[data-position-field="healthStatus"]');
            if (owner) owner.textContent = normalizeString(item?.ownerKey ?? item?.OwnerKey) || (L.NotAvailable || 'N/A');
            if (status) {
                status.className = 'badge bg-label-success';
                status.textContent = normalizeString(item?.status ?? item?.Status) || (L.NotAvailable || 'N/A');
            }
        } catch (error) {
            console.error('[PositionAssignments] Health load failed.', error);
            const status = document.querySelector('[data-position-field="healthStatus"]');
            if (status) {
                status.className = 'badge bg-label-danger';
                status.textContent = L.ErrorOccurred || 'Error';
            }
        }
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

    const setDetails = (item) => {
        const empty = document.getElementById('positionAssignmentDetailsEmpty');
        const list = document.getElementById('positionAssignmentDetailsList');
        if (!empty || !list) return;

        const values = {
            code: item?.code ?? item?.Code,
            employeeProjectionId: item?.employeeProjectionId ?? item?.EmployeeProjectionId,
            personReferenceId: item?.personReferenceId ?? item?.PersonReferenceId,
            organizationUnitId: item?.organizationUnitId ?? item?.OrganizationUnitId,
            positionId: item?.positionId ?? item?.PositionId,
            managerEmployeeProjectionId: item?.managerEmployeeProjectionId ?? item?.ManagerEmployeeProjectionId,
            effectiveFrom: formatDateTime(item?.effectiveFrom ?? item?.EffectiveFrom),
            effectiveTo: formatDateTime(item?.effectiveTo ?? item?.EffectiveTo),
            assignmentState: normalizeState(item?.assignmentState ?? item?.AssignmentState, assignmentStateNames),
            referenceValidationState: normalizeState(item?.referenceValidationState ?? item?.ReferenceValidationState, referenceStateNames),
            sensitiveAccessDecisionState: normalizeState(item?.sensitiveAccessDecisionState ?? item?.SensitiveAccessDecisionState, sensitiveStateNames),
            sourceContractVersion: item?.sourceContractVersion ?? item?.SourceContractVersion,
            assignmentVersion: item?.assignmentVersion ?? item?.AssignmentVersion,
            lastReferenceValidatedAt: formatDateTime(item?.lastReferenceValidatedAt ?? item?.LastReferenceValidatedAt),
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
                credentials: 'include',
                headers: getAuthHeaders()
            });
            if (!response.ok) throw new Error(`${response.status}`);
            const item = unwrapResponseData(await response.json())[0];
            if (!item) throw new Error('empty');
            setDetails(item);
            const panel = document.getElementById('offcanvasPositionAssignmentDetails');
            if (panel) bootstrap.Offcanvas.getOrCreateInstance(panel).show();
        } catch (error) {
            console.error('[PositionAssignments] Detail load failed.', error);
            window.notyf?.error?.(L.ErrorOccurred || 'An error occurred.');
        }
    };

    const updateActionVisibility = () => {
        const referenceLinkButton = document.querySelector('[data-position-action="reference-link"]');
        const manageButton = document.querySelector('[data-position-action="manage"]');
        const archiveButton = document.querySelector('[data-position-action="archive"]');
        if (referenceLinkButton) referenceLinkButton.classList.toggle('d-none', !hasFlag('canReferenceLink'));
        if (manageButton) manageButton.classList.toggle('d-none', !hasFlag('canManage'));
        if (archiveButton) archiveButton.classList.toggle('d-none', !hasFlag('canArchive'));
    };

    const initDataTable = () => {
        if (!tableEl || !window.DataTable) return;
        syncL10n();

        dt = new DataTable(tableEl, {
            ajax: async (_data, callback) => {
                try {
                    const response = await fetch(endpoint, {
                        method: 'GET',
                        credentials: 'include',
                        headers: getAuthHeaders()
                    });
                    if (!response.ok) throw new Error(`${response.status}`);
                    callback({ data: unwrapResponseData(await response.json()) });
                } catch (error) {
                    console.error('[PositionAssignments] List load failed.', error);
                    callback({ data: [] });
                    window.notyf?.error?.(L.ErrorOccurred || 'An error occurred.');
                } finally {
                    document.getElementById('skeleton-loader')?.classList.add('d-none');
                }
            },
            columns: [
                { data: null, defaultContent: '', orderable: false, searchable: false },
                { data: 'code', render: (data, _type, item) => escapeHtml(data ?? item?.Code) },
                { data: 'employeeProjectionId', render: (data, _type, item) => escapeHtml(data ?? item?.EmployeeProjectionId) },
                { data: 'organizationUnitId', render: (data, _type, item) => escapeHtml(data ?? item?.OrganizationUnitId ?? (L.NotAvailable || 'N/A')) },
                { data: 'positionId', render: (data, _type, item) => escapeHtml(data ?? item?.PositionId ?? (L.NotAvailable || 'N/A')) },
                { data: 'assignmentState', render: (data, _type, item) => renderBadge(data ?? item?.AssignmentState, 'assignment') },
                { data: 'referenceValidationState', render: (data, _type, item) => renderBadge(data ?? item?.ReferenceValidationState, 'reference') },
                { data: 'sensitiveAccessDecisionState', render: (data, _type, item) => renderBadge(data ?? item?.SensitiveAccessDecisionState, 'sensitive') },
                { data: 'effectiveFrom', render: (data, _type, item) => formatDateTime(data ?? item?.EffectiveFrom) },
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
        loadHealth();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', PositionAssignmentsList.init);
