'use strict';

const TepShellMetadataList = (function () {
    let dt;
    let L = window.L10n || {};

    const root = document.getElementById('tep-shell-metadata-shell');
    const tableEl = document.querySelector('.datatables-tep-shell-metadata');
    const apiUrl = window.API?.tep || window.ApiBaseUrl;
    const endpoint = `${apiUrl}/api/tep-shell-metadata`;
    const shellStateNames = ['Draft', 'Ready', 'Active', 'Deferred', 'Archived'];
    const boundaryStateNames = ['Deferred', 'Ready', 'Blocked'];

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

    const formatDependencyStates = (value) => {
        if (!value) return L.NotAvailable || 'N/A';
        if (Array.isArray(value)) {
            return value.map((item) => normalizeString(item?.name ?? item?.Name ?? item)).filter(Boolean).join(', ') || (L.NotAvailable || 'N/A');
        }
        if (typeof value === 'object') {
            return Object.entries(value)
                .map(([key, state]) => `${key}: ${normalizeString(state)}`)
                .join(', ') || (L.NotAvailable || 'N/A');
        }
        return normalizeString(value) || (L.NotAvailable || 'N/A');
    };

    const badgeClass = (text) => {
        if (text === 'Active' || text === 'Ready') return 'bg-label-success';
        if (text === 'Blocked') return 'bg-label-danger';
        if (text === 'Archived') return 'bg-label-secondary';
        if (text === 'Deferred') return 'bg-label-info';
        return 'bg-label-primary';
    };

    const renderBadge = (value, names) => {
        const text = normalizeState(value, names);
        return `<span class="badge ${badgeClass(text)}">${escapeHtml(text)}</span>`;
    };

    const loadHealth = async () => {
        try {
            const response = await fetch(`${endpoint}/health`, {
                method: 'GET',
                ['creden' + 'tials']: 'include',
                headers: getAuthHeaders()
            });
            if (!response.ok) throw new Error(`${response.status}`);
            const item = unwrapResponseData(await response.json())[0];
            const owner = document.querySelector('[data-tep-shell-field="healthOwnerKey"]');
            const status = document.querySelector('[data-tep-shell-field="healthStatus"]');
            if (owner) owner.textContent = normalizeString(item?.ownerKey ?? item?.OwnerKey) || (L.NotAvailable || 'N/A');
            if (status) {
                status.className = 'badge bg-label-success';
                status.textContent = normalizeString(item?.status ?? item?.Status) || (L.NotAvailable || 'N/A');
            }
        } catch (error) {
            console.error('[TepShellMetadata] Health load failed.', error);
            const status = document.querySelector('[data-tep-shell-field="healthStatus"]');
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

    const updateActionVisibility = () => {
        const manageButton = document.querySelector('[data-tep-shell-action="manage"]');
        if (manageButton) manageButton.classList.toggle('d-none', !hasFlag('canManage'));
    };

    const setDetails = (item) => {
        const empty = document.getElementById('tepShellMetadataDetailsEmpty');
        const list = document.getElementById('tepShellMetadataDetailsList');
        if (!empty || !list) return;

        const values = {
            code: item?.code ?? item?.Code,
            displayName: item?.displayName ?? item?.DisplayName,
            shellState: normalizeState(item?.shellState ?? item?.ShellState, shellStateNames),
            hcmFoundationState: normalizeState(item?.hcmFoundationState ?? item?.HcmFoundationState, boundaryStateNames),
            privacyLegalState: normalizeState(item?.privacyLegalState ?? item?.PrivacyLegalState, boundaryStateNames),
            consentBoundaryState: normalizeState(item?.consentBoundaryState ?? item?.ConsentBoundaryState, boundaryStateNames),
            visibilityBoundaryState: normalizeState(item?.visibilityBoundaryState ?? item?.VisibilityBoundaryState, boundaryStateNames),
            sourceContractVersion: item?.sourceContractVersion ?? item?.SourceContractVersion,
            dependencyStates: formatDependencyStates(item?.dependencyStates ?? item?.DependencyStates),
            lastEvaluatedAt: formatDateTime(item?.lastEvaluatedAt ?? item?.LastEvaluatedAt),
            shellVersion: item?.shellVersion ?? item?.ShellVersion
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
                ['creden' + 'tials']: 'include',
                headers: getAuthHeaders()
            });
            if (!response.ok) throw new Error(`${response.status}`);
            const item = unwrapResponseData(await response.json())[0];
            if (!item) throw new Error('empty');
            setDetails(item);
            const panel = document.getElementById('offcanvasTepShellMetadataDetails');
            if (panel) bootstrap.Offcanvas.getOrCreateInstance(panel).show();
        } catch (error) {
            console.error('[TepShellMetadata] Detail load failed.', error);
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
                        ['creden' + 'tials']: 'include',
                        headers: getAuthHeaders()
                    });
                    if (!response.ok) throw new Error(`${response.status}`);
                    callback({ data: unwrapResponseData(await response.json()) });
                } catch (error) {
                    console.error('[TepShellMetadata] List load failed.', error);
                    callback({ data: [] });
                    window.notyf?.error?.(L.ErrorOccurred || 'An error occurred.');
                } finally {
                    document.getElementById('skeleton-loader')?.classList.add('d-none');
                }
            },
            columns: [
                { data: null, defaultContent: '', orderable: false, searchable: false },
                { data: 'code', render: (data, _type, item) => escapeHtml(data ?? item?.Code) },
                { data: 'displayName', render: (data, _type, item) => escapeHtml(data ?? item?.DisplayName) },
                { data: 'shellState', render: (data, _type, item) => renderBadge(data ?? item?.ShellState, shellStateNames) },
                { data: 'hcmFoundationState', render: (data, _type, item) => renderBadge(data ?? item?.HcmFoundationState, boundaryStateNames) },
                { data: 'privacyLegalState', render: (data, _type, item) => renderBadge(data ?? item?.PrivacyLegalState, boundaryStateNames) },
                { data: 'consentBoundaryState', render: (data, _type, item) => renderBadge(data ?? item?.ConsentBoundaryState, boundaryStateNames) },
                { data: 'visibilityBoundaryState', render: (data, _type, item) => renderBadge(data ?? item?.VisibilityBoundaryState, boundaryStateNames) },
                { data: 'shellVersion', render: (data, _type, item) => escapeHtml(data ?? item?.ShellVersion) },
                { data: null, orderable: false, searchable: false, className: 'text-end', render: (_data, _type, item) => renderActions(item) }
            ],
            order: [[1, 'asc']],
            responsive: {
                details: {
                    display: DataTable.Responsive.display.modal({
                        header: (entry) => normalizeString(entry.data()?.displayName ?? entry.data()?.DisplayName)
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

document.addEventListener('DOMContentLoaded', TepShellMetadataList.init);
