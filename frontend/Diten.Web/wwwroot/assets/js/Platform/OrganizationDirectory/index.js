'use strict';

const OrganizationDirectory = (function () {
    let L = window.L10n || {};
    const root = document.getElementById('organization-directory-shell');
    const apiUrl = window.API?.platform || window.ApiBaseUrl;
    const endpoints = {
        orgUnits: `${apiUrl}/api/platform/organization-units`,
        positions: `${apiUrl}/api/platform/positions`,
        assignments: `${apiUrl}/api/platform/position-assignments`
    };
    const includeCookiesKey = 'creden' + 'tials';

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

    const formatDateTime = (value) => {
        const text = normalizeString(value);
        if (!text) return L.NotAvailable || 'N/A';
        const date = new Date(text);
        if (Number.isNaN(date.getTime())) return escapeHtml(text);
        return date.toLocaleString();
    };

    const formatBool = (value) => {
        if (value === true || String(value).toLowerCase() === 'true') {
            return `<span class="badge bg-label-warning">${escapeHtml(L.Yes || 'Yes')}</span>`;
        }
        return `<span class="badge bg-label-success">${escapeHtml(L.No || 'No')}</span>`;
    };

    const setCount = (name, count) => {
        const node = document.querySelector(`[data-directory-count="${name}"]`);
        if (node) node.textContent = String(count ?? 0);
    };

    const renderActions = (id, type) => [
        '<div class="d-flex justify-content-end gap-1">',
        `<button type="button" class="btn btn-icon btn-text-secondary" data-row-action="details" data-record-type="${escapeHtml(type)}" data-id="${escapeHtml(id || '')}" title="${escapeHtml(L.ViewDetails || 'View details')}">`,
        '<i class="bx bx-show icon-md"></i>',
        '</button>',
        '</div>'
    ].join('');

    const getValue = (item, camelName, pascalName) => item?.[camelName] ?? item?.[pascalName];

    const showPanel = () => {
        const empty = document.getElementById('organizationDirectoryDetailsEmpty');
        const list = document.getElementById('organizationDirectoryDetailsList');
        empty?.classList.add('d-none');
        list?.classList.remove('d-none');
        const panel = document.getElementById('offcanvasOrganizationDirectoryDetails');
        if (panel) bootstrap.Offcanvas.getOrCreateInstance(panel).show();
    };

    const selectDetailSection = (section) => {
        document.querySelectorAll('[data-detail-section]').forEach((node) => {
            node.classList.toggle('d-none', node.getAttribute('data-detail-section') !== section);
        });
    };

    const setSharedHeader = (typeLabel, primaryLabel) => {
        const typeNode = document.querySelector('[data-field="recordTypeLabel"]');
        const primaryNode = document.querySelector('[data-field="primaryLabel"]');
        if (typeNode) typeNode.textContent = typeLabel;
        if (primaryNode) primaryNode.textContent = normalizeString(primaryLabel) || (L.NotAvailable || 'N/A');
    };

    const setFieldText = (section, key, value) => {
        const node = document.querySelector(`[data-detail-section="${section}"] [data-field="${key}"]`);
        if (node) node.textContent = normalizeString(value) || (L.NotAvailable || 'N/A');
    };

    const setOrgUnitDetails = (item) => {
        selectDetailSection('orgUnit');
        setSharedHeader(L.OrganizationUnit || 'Organization Unit', getValue(item, 'name', 'Name'));
        setFieldText('orgUnit', 'code', getValue(item, 'code', 'Code'));
        setFieldText('orgUnit', 'name', getValue(item, 'name', 'Name'));
        setFieldText('orgUnit', 'legalEntityId', getValue(item, 'legalEntityId', 'LegalEntityId'));
        setFieldText('orgUnit', 'parentOrganizationUnitId', getValue(item, 'parentOrganizationUnitId', 'ParentOrganizationUnitId'));
        const archived = document.querySelector('[data-detail-section="orgUnit"] [data-field="isArchived"]');
        if (archived) archived.innerHTML = formatBool(getValue(item, 'isArchived', 'IsArchived'));
        showPanel();
    };

    const setPositionDetails = async (item) => {
        selectDetailSection('position');
        setSharedHeader(L.Position || 'Position', getValue(item, 'name', 'Name'));
        setFieldText('position', 'code', getValue(item, 'code', 'Code'));
        setFieldText('position', 'name', getValue(item, 'name', 'Name'));
        setFieldText('position', 'organizationUnitId', getValue(item, 'organizationUnitId', 'OrganizationUnitId'));
        setFieldText('position', 'reportsToPositionId', getValue(item, 'reportsToPositionId', 'ReportsToPositionId'));
        const archived = document.querySelector('[data-detail-section="position"] [data-field="isArchived"]');
        if (archived) archived.innerHTML = formatBool(getValue(item, 'isArchived', 'IsArchived'));

        const managerChainLabel = document.querySelector('[data-manager-chain-label]');
        const managerChainNode = document.querySelector('[data-detail-section="position"] [data-field="managerChain"]');
        const canReadManagerChain = hasFlag('canReadManagerChain');
        managerChainLabel?.classList.toggle('d-none', !canReadManagerChain);
        managerChainNode?.classList.toggle('d-none', !canReadManagerChain);
        if (canReadManagerChain && managerChainNode) {
            managerChainNode.textContent = L.ManagerChainUnavailable || 'Deferred';
            const id = getValue(item, 'id', 'Id');
            if (id) {
                try {
                    const response = await fetch(`${endpoints.positions}/${encodeURIComponent(id)}/manager-chain`, {
                        method: 'GET',
                        [includeCookiesKey]: 'include',
                        headers: getAuthHeaders()
                    });
                    if (!response.ok) throw new Error(`${response.status}`);
                    const chain = unwrapResponseData(await response.json());
                    managerChainNode.textContent = chain
                        .map((node) => normalizeString(node?.positionCode ?? node?.PositionCode ?? node?.name ?? node?.Name))
                        .filter(Boolean)
                        .join(' > ') || (L.NotAvailable || 'N/A');
                } catch (error) {
                    console.error('[OrganizationDirectory] Manager chain load failed.', error);
                    managerChainNode.textContent = L.ManagerChainUnavailable || 'Deferred';
                }
            }
        }
        showPanel();
    };

    const setAssignmentDetails = (item) => {
        selectDetailSection('assignment');
        setSharedHeader(L.PositionAssignment || 'Position Assignment', getValue(item, 'positionId', 'PositionId'));
        setFieldText('assignment', 'positionId', getValue(item, 'positionId', 'PositionId'));
        setFieldText('assignment', 'userId', getValue(item, 'userId', 'UserId'));
        setFieldText('assignment', 'effectiveFrom', formatDateTime(getValue(item, 'effectiveFrom', 'EffectiveFrom')));
        setFieldText('assignment', 'effectiveTo', formatDateTime(getValue(item, 'effectiveTo', 'EffectiveTo')));
        showPanel();
    };

    const showDetails = async (type, row) => {
        try {
            if (type === 'assignment') {
                setAssignmentDetails(row);
                return;
            }

            const id = getValue(row, 'id', 'Id');
            const endpoint = type === 'orgUnit' ? endpoints.orgUnits : endpoints.positions;
            const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
                method: 'GET',
                [includeCookiesKey]: 'include',
                headers: getAuthHeaders()
            });
            if (!response.ok) throw new Error(`${response.status}`);
            const item = unwrapResponseData(await response.json())[0];
            if (!item) throw new Error('empty');
            if (type === 'orgUnit') setOrgUnitDetails(item);
            if (type === 'position') await setPositionDetails(item);
        } catch (error) {
            console.error('[OrganizationDirectory] Detail load failed.', error);
            window.notyf?.error?.(L.ErrorOccurred || 'An error occurred.');
        }
    };

    const buildDataTable = (selector, endpoint, type, columns, countName, canReadFlag) => {
        const tableEl = document.querySelector(selector);
        if (!tableEl || !window.DataTable || !hasFlag(canReadFlag)) return;

        const dt = new DataTable(tableEl, {
            ajax: async (_data, callback) => {
                try {
                    const response = await fetch(endpoint, {
                        method: 'GET',
                        [includeCookiesKey]: 'include',
                        headers: getAuthHeaders()
                    });
                    if (!response.ok) throw new Error(`${response.status}`);
                    const rows = unwrapResponseData(await response.json());
                    setCount(countName, rows.length);
                    callback({ data: rows });
                } catch (error) {
                    console.error(`[OrganizationDirectory] ${type} list load failed.`, error);
                    setCount(countName, 0);
                    callback({ data: [] });
                    window.notyf?.error?.(L.ErrorOccurred || 'An error occurred.');
                } finally {
                    document.getElementById('skeleton-loader')?.classList.add('d-none');
                }
            },
            columns,
            order: [[1, 'asc']],
            responsive: {
                details: {
                    display: DataTable.Responsive.display.modal({
                        header: (entry) => normalizeString(getValue(entry.data(), 'code', 'Code') || getValue(entry.data(), 'positionId', 'PositionId'))
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
            const row = dt.row(button.closest('tr')).data();
            if (row) showDetails(type, row);
        });
    };

    const actionColumn = (type) => ({
        data: null,
        orderable: false,
        searchable: false,
        className: 'text-end',
        render: (_data, _type, item) => renderActions(getValue(item, 'id', 'Id'), type)
    });

    const init = () => {
        syncL10n();
        buildDataTable('.datatables-platform-org-units', endpoints.orgUnits, 'orgUnit', [
            { data: null, defaultContent: '', orderable: false, searchable: false },
            { data: 'code', render: (data, _type, item) => escapeHtml(data ?? item?.Code) },
            { data: 'name', render: (data, _type, item) => escapeHtml(data ?? item?.Name) },
            { data: 'legalEntityId', render: (data, _type, item) => escapeHtml(data ?? item?.LegalEntityId) },
            { data: 'parentOrganizationUnitId', render: (data, _type, item) => escapeHtml(data ?? item?.ParentOrganizationUnitId ?? (L.NotAvailable || 'N/A')) },
            { data: 'isArchived', render: (data, _type, item) => formatBool(data ?? item?.IsArchived) },
            actionColumn('orgUnit')
        ], 'orgUnits', 'canReadOrgUnits');

        buildDataTable('.datatables-platform-positions', endpoints.positions, 'position', [
            { data: null, defaultContent: '', orderable: false, searchable: false },
            { data: 'code', render: (data, _type, item) => escapeHtml(data ?? item?.Code) },
            { data: 'name', render: (data, _type, item) => escapeHtml(data ?? item?.Name) },
            { data: 'organizationUnitId', render: (data, _type, item) => escapeHtml(data ?? item?.OrganizationUnitId) },
            { data: 'reportsToPositionId', render: (data, _type, item) => escapeHtml(data ?? item?.ReportsToPositionId ?? (L.NotAvailable || 'N/A')) },
            { data: 'isArchived', render: (data, _type, item) => formatBool(data ?? item?.IsArchived) },
            actionColumn('position')
        ], 'positions', 'canReadPositions');

        buildDataTable('.datatables-platform-position-assignments', endpoints.assignments, 'assignment', [
            { data: null, defaultContent: '', orderable: false, searchable: false },
            { data: 'positionId', render: (data, _type, item) => escapeHtml(data ?? item?.PositionId) },
            { data: 'userId', render: (data, _type, item) => escapeHtml(data ?? item?.UserId) },
            { data: 'effectiveFrom', render: (data, _type, item) => formatDateTime(data ?? item?.EffectiveFrom) },
            { data: 'effectiveTo', render: (data, _type, item) => formatDateTime(data ?? item?.EffectiveTo) },
            actionColumn('assignment')
        ], 'assignments', 'canReadPositionAssignments');
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', OrganizationDirectory.init);
