'use strict';

const PayrollSources = (function () {
    let L = window.L10n || {};
    const root = document.getElementById('payroll-sources-shell');
    const apiUrl = window.API?.platform || window.ApiBaseUrl;
    const endpoints = {
        sources: `${apiUrl}/api/payroll-sources`
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
    const getValue = (item, camelName, pascalName) => item?.[camelName] ?? item?.[pascalName];

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
        const node = document.querySelector(`[data-payroll-source-count="${name}"]`);
        if (node) node.textContent = String(count ?? 0);
    };

    const setHealthAccess = () => {
        const node = document.querySelector('[data-payroll-source-health-state]');
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
        document.getElementById('payrollSourceDetailsEmpty')?.classList.add('d-none');
        document.getElementById('payrollSourceDetailsList')?.classList.remove('d-none');
        const panel = document.getElementById('offcanvasPayrollSourceDetails');
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

    const summarizeCount = (rows) => `${rows.length} ${L.Count || 'count'}`;

    const summarizeStates = (rows, camelName, pascalName) => {
        if (!rows.length) return L.Deferred || 'Deferred';
        const counts = rows.reduce((acc, row) => {
            const value = normalizeString(getValue(row, camelName, pascalName)) || (L.Deferred || 'Deferred');
            acc[value] = (acc[value] || 0) + 1;
            return acc;
        }, {});

        return Object.entries(counts)
            .map(([state, count]) => `${escapeHtml(state)}: ${count}`)
            .join(', ');
    };

    const loadSupplementaryMetadata = async (id) => {
        const labels = document.querySelectorAll('[data-health-label]');
        labels.forEach((node) => node.classList.toggle('d-none', !hasFlag('canReadHealth')));
        document.querySelector('[data-field="healthState"]')?.classList.toggle('d-none', !hasFlag('canReadHealth'));
        document.querySelector('[data-field="checkedAt"]')?.classList.toggle('d-none', !hasFlag('canReadHealth'));

        setFieldText('contractVersion', L.Deferred || 'Deferred');
        setFieldText('effectiveWindow', L.Deferred || 'Deferred');
        setFieldText('employeeReferenceMaps', L.Deferred || 'Deferred');
        setFieldText('cycleReferences', L.Deferred || 'Deferred');
        setFieldText('resultReferences', L.Deferred || 'Deferred');
        setFieldText('healthState', L.Deferred || 'Deferred');
        setFieldText('checkedAt', L.Deferred || 'Deferred');

        try {
            const contract = (await readList(`${endpoints.sources}/${encodeURIComponent(id)}/contract-profile`))[0];
            if (contract) {
                setFieldText('contractVersion', getValue(contract, 'contractVersion', 'ContractVersion'));
                const from = formatDateTime(getValue(contract, 'effectiveFrom', 'EffectiveFrom'));
                const to = formatDateTime(getValue(contract, 'effectiveTo', 'EffectiveTo'));
                setFieldText('effectiveWindow', `${from} - ${to}`);
            }
        } catch (error) {
            console.error('[PayrollSources] Contract metadata load failed.', error);
        }

        try {
            const maps = await readList(`${endpoints.sources}/${encodeURIComponent(id)}/employee-reference-maps`);
            setFieldText('employeeReferenceMaps', summarizeStates(maps, 'mappingState', 'MappingState'));
        } catch (error) {
            console.error('[PayrollSources] Reference map metadata load failed.', error);
        }

        try {
            const cycles = await readList(`${endpoints.sources}/${encodeURIComponent(id)}/cycle-references`);
            setFieldText('cycleReferences', summarizeStates(cycles, 'processingState', 'ProcessingState'));
        } catch (error) {
            console.error('[PayrollSources] Cycle metadata load failed.', error);
        }

        try {
            const results = await readList(`${endpoints.sources}/${encodeURIComponent(id)}/result-references`);
            setFieldText('resultReferences', summarizeStates(results, 'resultState', 'ResultState'));
        } catch (error) {
            console.error('[PayrollSources] Result metadata load failed.', error);
        }

        if (!hasFlag('canReadHealth')) return;

        try {
            const health = (await readList(`${endpoints.sources}/${encodeURIComponent(id)}/health`))[0];
            if (health) {
                setFieldText('healthState', getValue(health, 'healthState', 'HealthState'));
                setFieldText('checkedAt', formatDateTime(getValue(health, 'checkedAt', 'CheckedAt')));
            }
        } catch (error) {
            console.error('[PayrollSources] Health metadata load failed.', error);
        }
    };

    const setDetails = async (item) => {
        const id = getValue(item, 'id', 'Id');
        setFieldText('primaryLabel', getValue(item, 'displayName', 'DisplayName'));
        setFieldText('code', getValue(item, 'code', 'Code'));
        setFieldText('displayName', getValue(item, 'displayName', 'DisplayName'));
        setFieldText('providerFamily', getValue(item, 'providerFamily', 'ProviderFamily'));
        setFieldHtml('externalPayrollSystemId', maskReference(getValue(item, 'externalPayrollSystemId', 'ExternalPayrollSystemId')));
        setFieldHtml('connectionProfileReference', maskReference(getValue(item, 'connectionProfileReference', 'ConnectionProfileReference')));
        setFieldHtml('lifecycleState', renderState(getValue(item, 'lifecycleState', 'LifecycleState'), 'primary'));
        setFieldText('supportOwner', getValue(item, 'supportOwner', 'SupportOwner'));
        showPanel();
        if (id) await loadSupplementaryMetadata(id);
    };

    const showDetails = async (row) => {
        try {
            const id = getValue(row, 'id', 'Id');
            const response = await fetch(`${endpoints.sources}/${encodeURIComponent(id)}`, {
                method: 'GET',
                [includeCookiesKey]: 'include',
                headers: getAuthHeaders()
            });
            if (!response.ok) throw new Error(`${response.status}`);
            const item = unwrapResponseData(await response.json())[0];
            if (!item) throw new Error('empty');
            await setDetails(item);
        } catch (error) {
            console.error('[PayrollSources] Detail load failed.', error);
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
        const tableEl = document.querySelector('.datatables-platform-payroll-sources');
        if (!tableEl || !window.DataTable || !hasFlag('canRead')) return;

        const dt = new DataTable(tableEl, {
            ajax: async (_data, callback) => {
                try {
                    const response = await fetch(endpoints.sources, {
                        method: 'GET',
                        [includeCookiesKey]: 'include',
                        headers: getAuthHeaders()
                    });
                    if (!response.ok) throw new Error(`${response.status}`);
                    const rows = unwrapResponseData(await response.json());
                    setCount('sources', rows.length);
                    setCount('active', rows.filter((row) => /active|enabled/i.test(normalizeString(getValue(row, 'lifecycleState', 'LifecycleState')))).length);
                    callback({ data: rows });
                } catch (error) {
                    console.error('[PayrollSources] List load failed.', error);
                    setCount('sources', 0);
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
                { data: 'supportOwner', render: (data, _type, item) => escapeHtml(data ?? item?.SupportOwner) },
                {
                    data: 'contractProfileId',
                    render: (data, _type, item) => maskReference(data ?? item?.ContractProfileId)
                },
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
        setHealthAccess();
        initTable();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', PayrollSources.init);
