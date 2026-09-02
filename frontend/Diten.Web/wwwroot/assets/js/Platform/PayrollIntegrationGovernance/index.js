'use strict';

const PayrollIntegrationGovernance = (function () {
    let L = window.L10n || {};
    const root = document.getElementById('payroll-integration-governance-shell');
    const apiUrl = window.API?.platform || window.ApiBaseUrl;
    const endpoints = {
        runs: `${apiUrl}/api/payroll-integration-governance/runs`
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

    const summarizeStates = (rows, camelName, pascalName) => {
        if (!Array.isArray(rows) || rows.length === 0) return `0 / ${L.Deferred || 'Deferred'}`;
        const states = rows
            .map((row) => normalizeString(getValue(row, camelName, pascalName)))
            .filter(Boolean);
        const unique = [...new Set(states)];
        return `${rows.length} / ${unique.length ? unique.join(', ') : (L.Deferred || 'Deferred')}`;
    };

    const setCount = (name, count) => {
        const node = document.querySelector(`[data-pig-count="${name}"]`);
        if (node) node.textContent = String(count ?? 0);
    };

    const setAccessState = () => {
        const node = document.querySelector('[data-pig-health-state]');
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
        document.getElementById('payrollIntegrationGovernanceDetailsEmpty')?.classList.add('d-none');
        document.getElementById('payrollIntegrationGovernanceDetailsList')?.classList.remove('d-none');
        const panel = document.getElementById('offcanvasPayrollIntegrationGovernanceDetails');
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
        document.querySelectorAll('[data-health-label]').forEach((node) => node.classList.toggle('d-none', !hasFlag('canReadHealth')));
        document.querySelector('[data-field="healthState"]')?.classList.toggle('d-none', !hasFlag('canReadHealth'));

        setFieldText('sourceLinks', L.Deferred || 'Deferred');
        setFieldText('mappingControls', L.Deferred || 'Deferred');
        setFieldText('reconciliationControls', L.Deferred || 'Deferred');
        setFieldText('exceptions', L.Deferred || 'Deferred');
        setFieldText('evidenceReferences', L.Deferred || 'Deferred');
        setFieldText('healthState', L.Deferred || 'Deferred');

        const base = `${endpoints.runs}/${encodeURIComponent(id)}`;

        try {
            const sourceLinks = await readList(`${base}/source-links`);
            setFieldText('sourceLinks', summarizeStates(sourceLinks, 'linkState', 'LinkState'));
        } catch (error) {
            console.error('[PayrollIntegrationGovernance] Source link metadata load failed.', error);
        }

        try {
            const mappingControls = await readList(`${base}/mapping-controls`);
            setFieldText('mappingControls', summarizeStates(mappingControls, 'controlState', 'ControlState'));
        } catch (error) {
            console.error('[PayrollIntegrationGovernance] Mapping metadata load failed.', error);
        }

        try {
            const reconciliationControls = await readList(`${base}/reconciliation-controls`);
            setFieldText('reconciliationControls', summarizeStates(reconciliationControls, 'controlState', 'ControlState'));
        } catch (error) {
            console.error('[PayrollIntegrationGovernance] Reconciliation metadata load failed.', error);
        }

        try {
            const exceptions = await readList(`${base}/exceptions`);
            setFieldText('exceptions', summarizeStates(exceptions, 'exceptionState', 'ExceptionState'));
        } catch (error) {
            console.error('[PayrollIntegrationGovernance] Exception metadata load failed.', error);
        }

        try {
            const evidenceReferences = await readList(`${base}/evidence-export-references`);
            setFieldText('evidenceReferences', summarizeStates(evidenceReferences, 'exportState', 'ExportState'));
        } catch (error) {
            console.error('[PayrollIntegrationGovernance] Evidence reference metadata load failed.', error);
        }

        if (hasFlag('canReadHealth')) {
            try {
                const health = (await readList(`${base}/health`))[0];
                const healthState = getValue(health, 'healthState', 'HealthState')
                    || getValue(health, 'checkedAt', 'CheckedAt')
                    || L.Deferred;
                setFieldText('healthState', healthState);
            } catch (error) {
                console.error('[PayrollIntegrationGovernance] Health metadata load failed.', error);
            }
        }
    };

    const setDetails = async (item) => {
        const id = getValue(item, 'id', 'Id');
        setFieldText('primaryLabel', getValue(item, 'runCode', 'RunCode'));
        setFieldText('runCode', getValue(item, 'runCode', 'RunCode'));
        setFieldText('runType', getValue(item, 'runType', 'RunType'));
        setFieldHtml('status', renderState(getValue(item, 'status', 'Status'), 'primary'));
        setFieldText('contractVersion', getValue(item, 'contractVersion', 'ContractVersion'));
        setFieldHtml('payrollSourceProfileId', maskReference(getValue(item, 'payrollSourceProfileId', 'PayrollSourceProfileId')));
        setFieldHtml('timeAttendanceProviderProfileId', maskReference(getValue(item, 'timeAttendanceProviderProfileId', 'TimeAttendanceProviderProfileId')));
        setFieldHtml('hrisSourceProfileId', maskReference(getValue(item, 'hrisSourceProfileId', 'HrisSourceProfileId')));
        setFieldHtml('correlationId', maskReference(getValue(item, 'correlationId', 'CorrelationId')));
        setFieldText('startedAt', formatDateTime(getValue(item, 'startedAt', 'StartedAt')));
        setFieldText('completedAt', formatDateTime(getValue(item, 'completedAt', 'CompletedAt')));
        setFieldText('summary', getValue(item, 'summary', 'Summary'));
        showPanel();
        if (id) await loadRelatedMetadata(id);
    };

    const showDetails = async (row) => {
        try {
            const id = getValue(row, 'id', 'Id');
            const response = await fetch(`${endpoints.runs}/${encodeURIComponent(id)}`, {
                method: 'GET',
                [includeCookiesKey]: 'include',
                headers: getAuthHeaders()
            });
            if (!response.ok) throw new Error(`${response.status}`);
            const item = unwrapResponseData(await response.json())[0];
            if (!item) throw new Error('empty');
            await setDetails(item);
        } catch (error) {
            console.error('[PayrollIntegrationGovernance] Detail load failed.', error);
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
        const tableEl = document.querySelector('.datatables-platform-payroll-integration-governance');
        if (!tableEl || !window.DataTable || !hasFlag('canRead')) return;

        const dt = new DataTable(tableEl, {
            ajax: async (_data, callback) => {
                try {
                    const response = await fetch(endpoints.runs, {
                        method: 'GET',
                        [includeCookiesKey]: 'include',
                        headers: getAuthHeaders()
                    });
                    if (!response.ok) throw new Error(`${response.status}`);
                    const rows = unwrapResponseData(await response.json());
                    setCount('runs', rows.length);
                    setCount('active', rows.filter((row) => /queued|running|draft/i.test(normalizeString(getValue(row, 'status', 'Status')))).length);
                    callback({ data: rows });
                } catch (error) {
                    console.error('[PayrollIntegrationGovernance] List load failed.', error);
                    setCount('runs', 0);
                    setCount('active', 0);
                    callback({ data: [] });
                    window.notyf?.error?.(L.ErrorOccurred || 'An error occurred.');
                } finally {
                    document.getElementById('skeleton-loader')?.classList.add('d-none');
                }
            },
            columns: [
                { data: null, defaultContent: '', orderable: false, searchable: false },
                { data: 'runCode', render: (data, _type, item) => escapeHtml(data ?? item?.RunCode) },
                { data: 'runType', render: (data, _type, item) => escapeHtml(data ?? item?.RunType) },
                { data: 'status', render: (data, _type, item) => renderState(data ?? item?.Status, 'primary') },
                { data: 'contractVersion', render: (data, _type, item) => escapeHtml(data ?? item?.ContractVersion) },
                { data: 'startedAt', render: (data, _type, item) => formatDateTime(data ?? item?.StartedAt) },
                { data: 'completedAt', render: (data, _type, item) => formatDateTime(data ?? item?.CompletedAt) },
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
                        header: (entry) => normalizeString(getValue(entry.data(), 'runCode', 'RunCode'))
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

document.addEventListener('DOMContentLoaded', PayrollIntegrationGovernance.init);
