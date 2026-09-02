'use strict';

const EmployeeProjectionsList = (function () {
    let dt;
    let L = window.L10n || {};

    const tableEl = document.querySelector('.datatables-employee-projections');
    const apiUrl = window.API?.hcm || window.ApiBaseUrl;
    const endpoint = `${apiUrl}/api/employee-projections`;
    const stateNames = ['Deferred', 'Validated', 'Archived'];
    const visibilityNames = ['StandardHr', 'SensitiveHr', 'RestrictedHr'];

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

    const renderBadge = (value, type) => {
        const text = type === 'visibility'
            ? normalizeState(value, visibilityNames)
            : normalizeState(value, stateNames);
        const css = type === 'visibility'
            ? (text === 'RestrictedHr' ? 'bg-label-danger' : text === 'SensitiveHr' ? 'bg-label-warning' : 'bg-label-primary')
            : (text === 'Validated' ? 'bg-label-success' : text === 'Archived' ? 'bg-label-secondary' : 'bg-label-info');
        return `<span class="badge ${css}">${escapeHtml(text)}</span>`;
    };

    const renderActions = (row) => {
        const id = escapeHtml(row?.id || row?.Id || '');
        return [
            '<div class="d-flex justify-content-end">',
            `<button type="button" class="btn btn-icon btn-text-secondary" data-row-action="details" data-id="${id}" title="${escapeHtml(L.ViewDetails || 'View details')}">`,
            '<i class="bx bx-show icon-md"></i>',
            '</button>',
            '</div>'
        ].join('');
    };

    const setDetails = (item) => {
        const empty = document.getElementById('employeeProjectionDetailsEmpty');
        const list = document.getElementById('employeeProjectionDetailsList');
        if (!empty || !list) return;

        const values = {
            code: item?.code ?? item?.Code,
            displayName: item?.displayName ?? item?.DisplayName,
            hrisSourceProfileId: item?.hrisSourceProfileId ?? item?.HrisSourceProfileId,
            personReferenceId: item?.personReferenceId ?? item?.PersonReferenceId,
            externalEmployeeReference: item?.externalEmployeeReference ?? item?.ExternalEmployeeReference,
            employmentRecordReferenceKey: item?.employmentRecordReferenceKey ?? item?.EmploymentRecordReferenceKey,
            employmentStatusCode: item?.employmentStatusCode ?? item?.EmploymentStatusCode,
            workerTypeCode: item?.workerTypeCode ?? item?.WorkerTypeCode,
            sourceContractVersion: item?.sourceContractVersion ?? item?.SourceContractVersion,
            projectionState: normalizeState(item?.projectionState ?? item?.ProjectionState, stateNames),
            visibilityClassification: normalizeState(item?.visibilityClassification ?? item?.VisibilityClassification, visibilityNames),
            sourceLastSyncedAt: formatDateTime(item?.sourceLastSyncedAt ?? item?.SourceLastSyncedAt),
            projectionVersion: item?.projectionVersion ?? item?.ProjectionVersion,
            lastValidatedAt: formatDateTime(item?.lastValidatedAt ?? item?.LastValidatedAt),
            deferredReason: item?.deferredReason ?? item?.DeferredReason,
            createdAt: formatDateTime(item?.createdAt ?? item?.CreatedAt),
            updatedAt: formatDateTime(item?.updatedAt ?? item?.UpdatedAt)
        };

        list.querySelectorAll('[data-field]').forEach((node) => {
            const key = node.getAttribute('data-field');
            const value = values[key];
            node.textContent = normalizeString(value) || (L.NotAvailable || 'N/A');
        });

        empty.classList.add('d-none');
        list.classList.remove('d-none');
    };

    const showDetails = async (id) => {
        try {
            const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
                method: 'GET',
                credentials: 'include',
                headers: getAuthHeaders()
            });
            if (!response.ok) throw new Error(`${response.status}`);
            const content = await response.json();
            const item = unwrapResponseData(content)[0];
            if (!item) throw new Error('empty');
            setDetails(item);
            const panel = document.getElementById('offcanvasEmployeeProjectionDetails');
            if (panel) bootstrap.Offcanvas.getOrCreateInstance(panel).show();
        } catch (error) {
            console.error('[EmployeeProjections] Detail load failed.', error);
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
                        credentials: 'include',
                        headers: getAuthHeaders()
                    });
                    if (!response.ok) throw new Error(`${response.status}`);
                    const content = await response.json();
                    callback({ data: unwrapResponseData(content) });
                } catch (error) {
                    console.error('[EmployeeProjections] List load failed.', error);
                    callback({ data: [] });
                    window.notyf?.error?.(L.ErrorOccurred || 'An error occurred.');
                } finally {
                    document.getElementById('skeleton-loader')?.classList.add('d-none');
                }
            },
            columns: [
                { data: null, defaultContent: '', orderable: false, searchable: false },
                { data: 'code', render: (data, _type, row) => escapeHtml(data ?? row?.Code) },
                { data: 'displayName', render: (data, _type, row) => escapeHtml(data ?? row?.DisplayName) },
                { data: 'projectionState', render: (data, _type, row) => renderBadge(data ?? row?.ProjectionState, 'state') },
                { data: 'visibilityClassification', render: (data, _type, row) => renderBadge(data ?? row?.VisibilityClassification, 'visibility') },
                { data: 'createdAt', render: (data, _type, row) => formatDateTime(data ?? row?.CreatedAt) },
                { data: 'updatedAt', render: (data, _type, row) => formatDateTime(data ?? row?.UpdatedAt) },
                { data: null, orderable: false, searchable: false, className: 'text-end', render: (_data, _type, row) => renderActions(row) }
            ],
            order: [[1, 'asc']],
            responsive: {
                details: {
                    display: DataTable.Responsive.display.modal({
                        header: (row) => normalizeString(row.data()?.displayName ?? row.data()?.DisplayName)
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

    const init = () => initDataTable();

    return { init };
})();

document.addEventListener('DOMContentLoaded', EmployeeProjectionsList.init);
