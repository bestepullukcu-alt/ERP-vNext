'use strict';

const SensitiveAccessView = (function () {
    let dt;
    let L = window.L10n || {};
    const decisionRows = [];

    const root = document.getElementById('sensitive-access-shell');
    const tableEl = document.querySelector('.datatables-sensitive-access');
    const apiUrl = window.API?.hcm || window.ApiBaseUrl;
    const endpoint = `${apiUrl}/api/sensitive-access`;
    const actionNames = ['Read', 'Manage', 'Review'];
    const decisionNames = ['Denied', 'Allowed', 'Deferred'];
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
    const getAuthHeaders = () => Object.assign({ 'Content-Type': 'application/json' }, window.DitenDataTable?.getAuthHeaders?.() || {});
    const unwrapResponseData = (content) => content?.data ?? content?.Data ?? content;
    const hasFlag = (name) => String(root?.dataset?.[name] || '').toLowerCase() === 'true';

    const normalizeEnum = (value, names) => {
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

    const renderDecisionBadge = (value) => {
        const text = normalizeEnum(value, decisionNames);
        const css = text === 'Allowed' ? 'bg-label-success' : text === 'Deferred' ? 'bg-label-info' : 'bg-label-danger';
        return `<span class="badge ${css}">${escapeHtml(text)}</span>`;
    };

    const renderVisibilityBadge = (value) => {
        const text = normalizeEnum(value, visibilityNames);
        const css = text === 'RestrictedHr' ? 'bg-label-danger' : text === 'SensitiveHr' ? 'bg-label-warning' : 'bg-label-primary';
        return `<span class="badge ${css}">${escapeHtml(text)}</span>`;
    };

    const setField = (selector, value) => {
        const node = document.querySelector(selector);
        if (node) node.textContent = normalizeString(value) || (L.NotAvailable || 'N/A');
    };

    const setBadge = (selector, value, cssClass) => {
        const node = document.querySelector(selector);
        if (!node) return;
        node.className = `badge ${cssClass}`;
        node.textContent = normalizeString(value) || (L.NotAvailable || 'N/A');
    };

    const loadHealth = async () => {
        try {
            const response = await fetch(`${endpoint}/health`, {
                method: 'GET',
                credentials: 'include',
                headers: getAuthHeaders()
            });
            if (!response.ok) throw new Error(`${response.status}`);
            const data = unwrapResponseData(await response.json());
            setField('[data-sensitive-field="healthOwnerKey"]', data?.ownerKey ?? data?.OwnerKey);
            setBadge('[data-sensitive-field="healthStatus"]', data?.status ?? data?.Status, 'bg-label-success');
        } catch (error) {
            console.error('[SensitiveAccess] Health load failed.', error);
            setBadge('[data-sensitive-field="healthStatus"]', L.HealthLoadFailed || 'Unavailable', 'bg-label-danger');
        }
    };

    const loadAudit = async () => {
        if (!hasFlag('canAudit')) {
            setField('[data-sensitive-field="auditFollowUp"]', L.AuditUnavailable || 'N/A');
            setBadge('[data-sensitive-field="auditStatus"]', L.AuditUnavailable || 'N/A', 'bg-label-secondary');
            return;
        }

        try {
            const response = await fetch(`${endpoint}/audit/deferred`, {
                method: 'GET',
                credentials: 'include',
                headers: getAuthHeaders()
            });
            if (!response.ok) throw new Error(`${response.status}`);
            const data = unwrapResponseData(await response.json());
            setField('[data-sensitive-field="auditFollowUp"]', data?.followUp ?? data?.FollowUp);
            setBadge('[data-sensitive-field="auditStatus"]', data?.status ?? data?.Status, 'bg-label-info');
        } catch (error) {
            console.error('[SensitiveAccess] Audit load failed.', error);
            setField('[data-sensitive-field="auditFollowUp"]', L.AuditUnavailable || 'N/A');
            setBadge('[data-sensitive-field="auditStatus"]', L.AuditUnavailable || 'N/A', 'bg-label-secondary');
        }
    };

    const updateActionVisibility = () => {
        const reviewButton = document.querySelector('[data-sensitive-action="review"]');
        const manageButton = document.querySelector('[data-sensitive-action="manage"]');
        if (reviewButton) reviewButton.classList.toggle('d-none', !hasFlag('canReview'));
        if (manageButton) manageButton.classList.toggle('d-none', !hasFlag('canManage'));
    };

    const initDataTable = () => {
        if (!tableEl || !window.DataTable) return;
        dt = new DataTable(tableEl, {
            data: decisionRows,
            columns: [
                { data: 'employeeProjectionId', render: (data) => escapeHtml(data) },
                { data: 'visibilityClassification', render: (data) => renderVisibilityBadge(data) },
                { data: 'requestedAction', render: (data) => escapeHtml(normalizeEnum(data, actionNames)) },
                { data: 'decision', render: (data) => renderDecisionBadge(data) },
                { data: 'reasonCode', render: (data) => escapeHtml(data || (L.NotAvailable || 'N/A')) },
                { data: 'dataScope', render: (data) => escapeHtml(data || (L.NotAvailable || 'N/A')) },
                { data: 'evaluatedAt', render: (data) => formatDateTime(data) }
            ],
            order: [[6, 'desc']],
            responsive: true,
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
        document.getElementById('skeleton-loader')?.classList.add('d-none');
    };

    const init = () => {
        syncL10n();
        updateActionVisibility();
        initDataTable();
        loadHealth();
        loadAudit();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', SensitiveAccessView.init);
