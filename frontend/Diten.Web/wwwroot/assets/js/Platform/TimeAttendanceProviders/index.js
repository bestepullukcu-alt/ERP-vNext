'use strict';

const TimeAttendanceProviders = (function () {
    let L = window.L10n || {};
    const root = document.getElementById('time-attendance-providers-shell');
    const apiUrl = window.API?.platform || window.ApiBaseUrl;
    const endpoints = {
        providers: '/Platform/TimeAttendanceProviders/api'
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

    const getValue = (item, camelName, pascalName) => item?.[camelName] ?? item?.[pascalName];

    const formatDateTime = (value) => {
        const text = normalizeString(value);
        if (!text) return L.NotAvailable || 'N/A';
        const date = new Date(text);
        if (Number.isNaN(date.getTime())) return escapeHtml(text);
        return date.toLocaleString();
    };

    const maskReference = (value) => {
        const text = normalizeString(value);
        if (!text) return L.NotAvailable || 'N/A';
        if (text.length <= 8) return L.Masked || 'Masked';
        return `${escapeHtml(text.slice(0, 4))}...${escapeHtml(text.slice(-4))}`;
    };

    const renderState = (value, tone) => {
        const text = normalizeString(value) || (L.Deferred || 'Deferred');
        return `<span class="badge bg-label-${tone || 'secondary'}">${escapeHtml(text)}</span>`;
    };

    const setCount = (name, count) => {
        const node = document.querySelector(`[data-ta-count="${name}"]`);
        if (node) node.textContent = String(count ?? 0);
    };

    const setAccessState = () => {
        const node = document.querySelector('[data-ta-health-state]');
        if (node) node.textContent = hasFlag('canReadHealth') ? (L.ReadOnly || 'Read-only') : (L.Deferred || 'Deferred');
    };

    const setFieldText = (key, value) => {
        const node = document.querySelector(`[data-field="${key}"]`);
        if (node) node.textContent = normalizeString(value) || (L.NotAvailable || 'N/A');
    };

    const setFieldHtml = (key, value) => {
        const node = document.querySelector(`[data-field="${key}"]`);
        if (node) node.innerHTML = value || escapeHtml(L.NotAvailable || 'N/A');
    };

    const showPanel = () => {
        document.getElementById('timeAttendanceProviderDetailsEmpty')?.classList.add('d-none');
        document.getElementById('timeAttendanceProviderDetailsList')?.classList.remove('d-none');
        const panel = document.getElementById('offcanvasTimeAttendanceProviderDetails');
        if (panel) bootstrap.Offcanvas.getOrCreateInstance(panel).show();
    };

    const readList = async (url) => {
        const response = await fetch(url, {
            method: 'GET',
            [includeCookiesKey]: 'include',
            headers: getAuthHeaders()
        });
        if (!response.ok) return [];
        return unwrapResponseData(await response.json());
    };

    const loadRelatedMetadata = async (id) => {
        document.querySelectorAll('[data-checkpoint-label]').forEach((node) => node.classList.toggle('d-none', !hasFlag('canReadCheckpoint')));
        document.querySelectorAll('[data-health-label]').forEach((node) => node.classList.toggle('d-none', !hasFlag('canReadHealth')));
        document.querySelector('[data-field="syncMode"]')?.classList.toggle('d-none', !hasFlag('canReadCheckpoint'));
        document.querySelector('[data-field="syncCheckpoint"]')?.classList.toggle('d-none', !hasFlag('canReadCheckpoint'));
        document.querySelector('[data-field="healthState"]')?.classList.toggle('d-none', !hasFlag('canReadHealth'));

        setFieldText('contractVersion', L.Deferred || 'Deferred');
        setFieldText('referenceMapCount', L.Deferred || 'Deferred');
        setFieldText('eventReferenceCount', L.Deferred || 'Deferred');
        setFieldText('summaryReferenceCount', L.Deferred || 'Deferred');
        setFieldText('syncMode', L.Deferred || 'Deferred');
        setFieldText('syncCheckpoint', L.Deferred || 'Deferred');
        setFieldText('healthState', L.Deferred || 'Deferred');

        try {
            const contractProfile = (await readList(`${endpoints.providers}/${encodeURIComponent(id)}/contract-profile`))[0];
            setFieldText('contractVersion', getValue(contractProfile, 'contractVersion', 'ContractVersion'));
        } catch (error) {
            console.error('[TimeAttendanceProviders] Contract metadata load failed.', error);
        }

        try {
            const referenceMaps = await readList(`${endpoints.providers}/${encodeURIComponent(id)}/employee-reference-maps`);
            setFieldText('referenceMapCount', referenceMaps.length);
        } catch (error) {
            console.error('[TimeAttendanceProviders] Reference map metadata load failed.', error);
        }

        try {
            const eventReferences = await readList(`${endpoints.providers}/${encodeURIComponent(id)}/event-references`);
            setFieldText('eventReferenceCount', eventReferences.length);
        } catch (error) {
            console.error('[TimeAttendanceProviders] Event reference metadata load failed.', error);
        }

        try {
            const summaryReferences = await readList(`${endpoints.providers}/${encodeURIComponent(id)}/attendance-summary-references`);
            setFieldText('summaryReferenceCount', summaryReferences.length);
        } catch (error) {
            console.error('[TimeAttendanceProviders] Summary reference metadata load failed.', error);
        }

        if (hasFlag('canReadCheckpoint')) {
            try {
                const checkpoint = (await readList(`${endpoints.providers}/${encodeURIComponent(id)}/sync-checkpoint`))[0];
                const checkpointState = getValue(checkpoint, 'status', 'Status')
                    || getValue(checkpoint, 'syncMode', 'SyncMode')
                    || L.Deferred;
                setFieldText('syncMode', getValue(checkpoint, 'syncMode', 'SyncMode'));
                setFieldText('syncCheckpoint', checkpointState);
            } catch (error) {
                console.error('[TimeAttendanceProviders] Checkpoint metadata load failed.', error);
            }
        }

        if (hasFlag('canReadHealth')) {
            try {
                const health = (await readList(`${endpoints.providers}/${encodeURIComponent(id)}/health`))[0];
                const healthState = getValue(health, 'healthState', 'HealthState')
                    || getValue(health, 'checkedAt', 'CheckedAt')
                    || L.Deferred;
                setFieldText('healthState', healthState);
                setFieldText('lastValidation', formatDateTime(getValue(health, 'checkedAt', 'CheckedAt')));
            } catch (error) {
                console.error('[TimeAttendanceProviders] Health metadata load failed.', error);
            }
        }
    };

    const setDetails = async (item) => {
        const id = getValue(item, 'id', 'Id');
        setFieldText('primaryLabel', getValue(item, 'displayName', 'DisplayName'));
        setFieldText('code', getValue(item, 'code', 'Code'));
        setFieldText('displayName', getValue(item, 'displayName', 'DisplayName'));
        setFieldText('providerFamily', getValue(item, 'providerFamily', 'ProviderFamily'));
        setFieldHtml('externalProviderAccountId', maskReference(getValue(item, 'externalProviderAccountId', 'ExternalProviderAccountId')));
        setFieldHtml('connectionProfileReference', maskReference(getValue(item, 'connectionProfileReference', 'ConnectionProfileReference')));
        setFieldHtml('lifecycleState', renderState(getValue(item, 'lifecycleState', 'LifecycleState'), 'primary'));
        setFieldText('supportOwner', getValue(item, 'supportOwner', 'SupportOwner'));
        setFieldText('contractProfileId', getValue(item, 'contractProfileId', 'ContractProfileId'));
        setFieldText('lastValidation', formatDateTime(getValue(item, 'updatedAt', 'UpdatedAt') || getValue(item, 'createdAt', 'CreatedAt')));
        showPanel();
        if (id) await loadRelatedMetadata(id);
    };

    const showDetails = async (row) => {
        try {
            const id = getValue(row, 'id', 'Id');
            const response = await fetch(`${endpoints.providers}/${encodeURIComponent(id)}`, {
                method: 'GET',
                [includeCookiesKey]: 'include',
                headers: getAuthHeaders()
            });
            if (!response.ok) throw new Error(`${response.status}`);
            const item = unwrapResponseData(await response.json())[0];
            if (!item) throw new Error('empty');
            await setDetails(item);
        } catch (error) {
            console.error('[TimeAttendanceProviders] Detail load failed.', error);
            window.notyf?.error?.(L.ErrorOccurred || 'An error occurred.');
        }
    };

    const renderActions = (id) => [
        '<div class="d-flex justify-content-end gap-1">',
        `<button type="button" class="btn btn-icon btn-text-secondary" data-row-action="details" data-id="${escapeHtml(id || '')}" title="${escapeHtml(L.ViewDetails || 'View details')}">`,
        '<i class="bx bx-show icon-md"></i>',
        '</button>',
        '</div>'
    ].join('');

    const initTable = () => {
        const tableEl = document.querySelector('.datatables-platform-time-attendance-providers');
        if (!tableEl || !window.DataTable || !hasFlag('canRead')) return;

        const dt = new DataTable(tableEl, {
            ajax: async (_data, callback) => {
                try {
                    const response = await fetch(endpoints.providers, {
                        method: 'GET',
                        [includeCookiesKey]: 'include',
                        headers: getAuthHeaders()
                    });
                    if (!response.ok) throw new Error(`${response.status}`);
                    const rows = unwrapResponseData(await response.json());
                    setCount('providers', rows.length);
                    setCount('active', rows.filter((row) => /active|enabled/i.test(normalizeString(getValue(row, 'lifecycleState', 'LifecycleState')))).length);
                    callback({ data: rows });
                } catch (error) {
                    console.error('[TimeAttendanceProviders] List load failed.', error);
                    setCount('providers', 0);
                    setCount('active', 0);
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
                { data: 'providerFamily', render: (data, _type, item) => escapeHtml(data ?? item?.ProviderFamily) },
                { data: 'lifecycleState', render: (data, _type, item) => renderState(data ?? item?.LifecycleState, 'primary') },
                { data: 'externalProviderAccountId', render: (data, _type, item) => maskReference(data ?? item?.ExternalProviderAccountId) },
                { data: 'contractProfileId', render: (data, _type, item) => escapeHtml(data ?? item?.ContractProfileId ?? (L.NotAvailable || 'N/A')) },
                {
                    data: null,
                    orderable: false,
                    searchable: false,
                    className: 'text-end',
                    render: (_data, _type, item) => renderActions(getValue(item, 'id', 'Id'))
                }
            ],
            order: [[1, 'asc']],
            responsive: {
                details: {
                    display: DataTable.Responsive.display.modal({
                        header: (entry) => normalizeString(getValue(entry.data(), 'code', 'Code'))
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
            if (row) showDetails(row);
        });
    };

    const init = () => {
        syncL10n();
        setAccessState();
        initTable();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', TimeAttendanceProviders.init);
