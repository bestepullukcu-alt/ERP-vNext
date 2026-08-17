'use strict';

(function () {
    const endpoint = '/Platform/AuditLog/api';
    let L = window.L10n || {};
    let dt = null;
    let appliedFilters = emptyFilters();
    let exportLimits = null;
    let redactionInProgress = false;
    const lookupLabels = {
        category: new Map(),
        operation: new Map(),
        outcome: new Map()
    };

    const tableEl = document.getElementById('dt-auditlog');
    const permissionEl = document.getElementById('audit-log-permission');
    const errorEl = document.getElementById('audit-log-error');
    const emptyEl = document.getElementById('audit-log-empty');
    const redactionResultEl = document.getElementById('audit-redaction-result');
    const detailModalEl = document.getElementById('auditDetailModal');
    const detailModal = detailModalEl ? new bootstrap.Modal(detailModalEl) : null;

    const filterEls = {
        fromUtc: document.getElementById('filterFromUtc'),
        toUtc: document.getElementById('filterToUtc'),
        tenantId: document.getElementById('filterTenant'),
        category: document.getElementById('filterCategory'),
        operation: document.getElementById('filterOperation'),
        outcome: document.getElementById('filterOutcome')
    };

    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Platform', pageKey: 'AuditLog' };
    const totalColumnCount = 11;
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
    const defaultVisibleColumnIndexes = [1, 2, 4, 5, 8, 10];
    const baseOrder = [[1, 'desc']];

    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (raw) => (Array.isArray(raw) ? raw : [raw])
        .map(normalizeString)
        .filter(Boolean);
    const normalizeFilterValue = (v) => typeof v === 'string' ? v.trim() : v;
    const normalizeFilters = (filters) => {
        const source = filters || {};
        return {
            fromUtc: normalizeString(source.fromUtc),
            toUtc: normalizeString(source.toUtc),
            tenantId: normalizeString(source.tenantId),
            category: normalizeString(source.category),
            operation: normalizeString(source.operation),
            outcome: normalizeString(source.outcome)
        };
    };

    const normalizeColVis = (colVis) => {
        if (!colVis) return null;
        const n = {};
        if (Array.isArray(colVis)) {
            saveViewColumnIndexes.forEach((ci, pos) => {
                if (typeof colVis[ci] === 'boolean') n[ci] = colVis[ci];
                else if (typeof colVis[pos] === 'boolean') n[ci] = colVis[pos];
            });
        } else if (typeof colVis === 'object') {
            saveViewColumnIndexes.forEach((ci) => { if (typeof colVis[ci] === 'boolean') n[ci] = colVis[ci]; });
        }
        return Object.keys(n).length ? n : null;
    };

    const captureColVis = (api) => {
        const r = {};
        saveViewColumnIndexes.forEach((ci) => { try { r[ci] = !!api.column(ci).visible(); } catch (e) { } });
        return r;
    };

    const normalizeColOrder = (order) => {
        if (!Array.isArray(order) || order.length !== totalColumnCount) return null;
        const n = order.map(Number).filter((i) => Number.isInteger(i) && i >= 0 && i < totalColumnCount);
        return n.length === totalColumnCount && new Set(n).size === totalColumnCount ? n : null;
    };

    const captureColOrder = (api) => { try { return normalizeColOrder(api?.colReorder?.order?.()); } catch (e) { return null; } };

    const defaultColVis = () => saveViewColumnIndexes.reduce((a, ci) => { a[ci] = defaultVisibleColumnIndexes.includes(ci); return a; }, {});

    const applyColOrder = (api, order) => {
        const n = normalizeColOrder(order);
        if (!n || typeof api?.colReorder?.order !== 'function') return;
        api.colReorder.order(n, true);
    };

    const applyColVis = (api, colVis) => {
        const n = normalizeColVis(colVis);
        if (!n) return;
        saveViewColumnIndexes.forEach((ci) => { if (typeof n[ci] === 'boolean') { try { api.column(ci).visible(n[ci], false); } catch (e) { } } });
    };

    const getSearchVal = (api) => { try { return api.table().container().querySelector('.dt-search input')?.value || ''; } catch (e) { return ''; } };
    const syncSearchInput = (api, v) => { try { const el = api.table().container().querySelector('.dt-search input'); if (el) el.value = v || ''; } catch (e) { } };

    const getCurrentView = (api) => ({
        filters: normalizeFilters(appliedFilters),
        search: normalizeString(getSearchVal(api) || api.search()),
        colVis: captureColVis(api),
        columnOrder: captureColOrder(api),
        order: api.order()
    });

    const serializeView = (v) => JSON.stringify({
        filters: Object.keys(normalizeFilters(v?.filters)).sort().reduce((acc, key) => {
            acc[key] = normalizeFilterValue(normalizeFilters(v?.filters)[key]);
            return acc;
        }, {}),
        search: normalizeString(v?.search),
        colVis: normalizeColVis(v?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(v?.columnOrder) || Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(v?.order) ? v.order : baseOrder
    });

    const getSavedViewId = (sv) => sv?.id || sv?.Id || sv?._id || null;
    const getSavedViewName = (sv) => sv?.viewName || sv?.ViewName || '';
    const isSavedViewDefault = (sv) => sv?.isDefault === true || sv?.IsDefault === true;
    const unwrapViewResponse = (response) => response?.data || response?.Data || response;

    const getSavedViewDef = (sv) => {
        const raw = sv?.viewDefinition ?? sv?.ViewDefinition ?? {};
        if (typeof raw === 'string') { try { return JSON.parse(raw); } catch (e) { return {}; } }
        return raw || {};
    };

    const mapSavedViewToState = (sv) => {
        const d = getSavedViewDef(sv);
        return {
            filters: normalizeFilters(d.filters || d),
            search: normalizeString(d.search),
            colVis: normalizeColVis(d.colVis),
            columnOrder: normalizeColOrder(d.columnOrder),
            order: Array.isArray(d.order) ? d.order : null
        };
    };
    const normalizeViewState = (view) => ({
        filters: normalizeFilters(view?.filters || view || emptyFilters()),
        search: normalizeString(view?.search),
        colVis: normalizeColVis(view?.colVis) || defaultColVis(),
        columnOrder: normalizeColOrder(view?.columnOrder) || Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(view?.order) ? view.order : baseOrder
    });
    const getResetBaselineState = () => normalizeViewState({
        filters: emptyFilters(),
        search: '',
        colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i),
        order: baseOrder
    });

    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };

    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || getResetBaselineState();
        return serializeView(getCurrentView(api)) !== serializeView(baseline);
    };

    const loadDefaultView = async () => {
        defaultViewRecord = null;
        defaultViewState = null;
        if (!personalizationClient?.getViews) return null;
        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = Array.isArray(items) ? (items.find(isSavedViewDefault) || items[0] || null) : null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
            return defaultViewState;
        } catch (error) {
            if (error?.authHandled) return null;
            console.error('[AuditLog SaveView] Failed to load saved views.', error);
            return null;
        }
    };

    const saveDefaultView = async (view) => {
        if (!personalizationClient?.saveView) return null;
        const normalizedView = normalizeViewState(view);
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (getSavedViewName(defaultViewRecord) || L.AuditLogDefaultViewName || L.CommonSaveView || L.SaveView || 'Default').trim(),
            viewDefinition: normalizedView,
            isDefault: true,
            visibility: 'private'
        };
        const existingId = getSavedViewId(defaultViewRecord);
        const savedResponse = existingId
            ? await personalizationClient.updateView(existingId, payload)
            : await personalizationClient.saveView(payload);
        const savedRecord = unwrapViewResponse(savedResponse);
        defaultViewRecord = savedRecord && typeof savedRecord === 'object'
            ? savedRecord
            : Object.assign({}, defaultViewRecord || {}, payload);
        defaultViewState = normalizedView;
        return defaultViewState;
    };

    const syncFilterControls = (values) => {
        filterEls.fromUtc.value = values.fromUtc || '';
        filterEls.toUtc.value = values.toUtc || '';
        if (window.jQuery && $.fn.select2) {
            $(filterEls.tenantId).val(values.tenantId || '').trigger('change');
            $(filterEls.category).val(values.category || '').trigger('change');
            $(filterEls.operation).val(values.operation || '').trigger('change');
            $(filterEls.outcome).val(values.outcome || '').trigger('change');
        } else {
            filterEls.tenantId.value = values.tenantId || '';
            filterEls.category.value = values.category || '';
            filterEls.operation.value = values.operation || '';
            filterEls.outcome.value = values.outcome || '';
        }
    };

    const applySavedTableState = (api, view) => {
        if (!api || !view) return;
        const s = normalizeViewState(view);
        appliedFilters = s.filters;
        syncFilterControls(appliedFilters);
        applyColOrder(api, s.columnOrder);
        applyColVis(api, s.colVis);
        api.search(s.search);
        syncSearchInput(api, s.search);
        api.order(s.order);
        try { api.columns.adjust(); } catch (e) { }
        try { api.responsive?.recalc?.(); } catch (e) { }
        api.draw(false);
        window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
    };

    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });

    function syncL10n() {
        L = window.L10n || {};
    }

    function emptyFilters() {
        return {
            fromUtc: '',
            toUtc: '',
            tenantId: '',
            category: '',
            operation: '',
            outcome: ''
        };
    }

    function escapeHtml(value) {
        if (value === null || value === undefined) return '';
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    function formatDate(value, type) {
        if (!value) return '';
        if (type !== 'display') return value;
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return escapeHtml(value);
        const datePart = new Intl.DateTimeFormat(undefined, {
            day: '2-digit',
            month: 'short',
            year: '2-digit'
        }).format(date);
        const timePart = new Intl.DateTimeFormat(undefined, {
            hour: '2-digit',
            minute: '2-digit'
        }).format(date);
        return `<span class="d-inline-flex flex-column lh-sm"><span>${escapeHtml(datePart)}</span><small class="text-muted">${escapeHtml(timePart)}</small></span>`;
    }

    function isGuid(value) {
        return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
    }

    function toIsoFromLocal(value) {
        if (!value) return '';
        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? '' : date.toISOString();
    }

    function normalizeGatewayPayload(payload) {
        return payload?.data || payload?.Data || payload || {};
    }

    function normalizeLookupRows(payload) {
        const data = normalizeGatewayPayload(payload);
        return Array.isArray(data) ? data : [];
    }

    function normalizeLookupOption(item) {
        const code = item?.code ?? item?.Code ?? item?.value ?? item?.Value ?? '';
        const name = item?.name ?? item?.Name ?? item?.text ?? item?.Text ?? code;
        return {
            value: String(code || ''),
            text: String(name || code || '')
        };
    }

    function ensureSelectedOption(select, value) {
        if (!select || !value || Array.from(select.options).some((option) => option.value === value)) return;
        select.appendChild(new Option(value, value, true, true));
    }

    async function populateLookupSelect(select, labelMap) {
        const lookupUrl = select?.dataset?.lookupUrl;
        if (!select || !lookupUrl) return;

        const selectedValue = select.value || select.dataset.selectedValue || '';
        const placeholder = select.querySelector('option[value=""]')?.textContent || '';
        const response = await fetch(lookupUrl, {
            credentials: 'same-origin',
            headers: getAuthHeaders()
        });

        if (!response.ok) {
            throw new Error(await readError(response));
        }

        const rows = normalizeLookupRows(await response.json())
            .map(normalizeLookupOption)
            .filter(option => option.value && option.text);

        select.innerHTML = '';
        select.appendChild(new Option(placeholder, ''));
        labelMap.clear();
        rows.forEach(option => {
            labelMap.set(option.value, option.text);
            select.appendChild(new Option(option.text, option.value, false, option.value === selectedValue));
        });
        ensureSelectedOption(select, selectedValue);
    }

    async function loadFilterLookups() {
        try {
            await Promise.all([
                populateLookupSelect(filterEls.category, lookupLabels.category),
                populateLookupSelect(filterEls.operation, lookupLabels.operation),
                populateLookupSelect(filterEls.outcome, lookupLabels.outcome)
            ]);
        } catch (error) {
            console.error('[AuditLog] Filter lookup load failed.', error);
            showError(error.message || L.ErrorOccurred || '');
        }
    }

    async function loadExportLimits() {
        try {
            const response = await fetch(`${endpoint}/export-limits`, {
                credentials: 'same-origin',
                headers: getAuthHeaders()
            });

            if (!response.ok) return;
            const payload = normalizeGatewayPayload(await response.json());
            const maxRows = Number(payload.maxRows ?? payload.MaxRows);
            const maxDays = Number(payload.maxDays ?? payload.MaxDays);
            exportLimits = {
                maxRows: Number.isFinite(maxRows) && maxRows > 0 ? maxRows : null,
                maxDays: Number.isFinite(maxDays) && maxDays > 0 ? maxDays : null
            };
        } catch (error) {
            console.error('[AuditLog] Export limits could not be loaded.', error);
            exportLimits = null;
        }
    }

    function normalizeItems(payload) {
        const data = normalizeGatewayPayload(payload);
        return {
            items: data.items || data.Items || [],
            totalCount: data.totalCount || data.TotalCount || 0
        };
    }

    function showError(message) {
        if (!errorEl) return;
        errorEl.textContent = message || L.ErrorOccurred || '';
        errorEl.classList.remove('d-none');
    }

    function clearError() {
        errorEl?.classList.add('d-none');
        if (errorEl) errorEl.textContent = '';
    }

    function setPermissionVisible(visible) {
        permissionEl?.classList.toggle('d-none', !visible);
    }

    function setPermissionMessage(status) {
        if (!permissionEl) return;
        permissionEl.textContent = status === 401
            ? L.AuditLogLoginRequired || ''
            : L.AuditLogForbidden || L.AuditLogUnauthorized || '';
        setPermissionVisible(true);
    }

    function setEmptyVisible(visible) {
        emptyEl?.classList.toggle('d-none', !visible);
    }

    function showRedactionResult(message) {
        if (!redactionResultEl) return;
        redactionResultEl.textContent = message || '';
        redactionResultEl.classList.toggle('d-none', !message);
    }

    function collectFilters() {
        const next = {
            fromUtc: toIsoFromLocal(filterEls.fromUtc?.value || ''),
            toUtc: toIsoFromLocal(filterEls.toUtc?.value || ''),
            tenantId: filterEls.tenantId?.disabled ? '' : (filterEls.tenantId?.value || '').trim(),
            category: filterEls.category?.value || '',
            operation: filterEls.operation?.value || '',
            outcome: filterEls.outcome?.value || ''
        };

        if (next.fromUtc && next.toUtc && new Date(next.fromUtc) > new Date(next.toUtc)) {
            showError(L.AuditLogInvalidDateRange || '');
            return null;
        }

        if (next.tenantId && !isGuid(next.tenantId)) {
            showError(L.AuditLogInvalidGuid || '');
            return null;
        }

        return next;
    }

    function getAppliedFilterCount() {
        return Object.values(appliedFilters).filter(Boolean).length;
    }

    function buildQuery(data, exportFormat) {
        const params = new URLSearchParams();

        if (exportFormat) {
            params.set('format', exportFormat);
            if (exportLimits?.maxRows) {
                params.set('limit', String(exportLimits.maxRows));
            }
        } else {
            const start = Number(data?.start || 0);
            const length = Number(data?.length || 50);
            params.set('page', String(Math.floor(start / Math.max(length, 1)) + 1));
            params.set('pageSize', String(length || 50));
        }

        Object.entries(appliedFilters).forEach(([key, value]) => {
            if (!value) return;
            params.set(key, value);
        });

        const toolbarSearch = normalizeString(data?.search?.value || '');
        if (toolbarSearch) {
            if (isGuid(toolbarSearch)) {
                params.set('correlationId', toolbarSearch);
            } else {
                params.set('search', toolbarSearch);
            }
        }

        return params.toString();
    }

    function statusBadge(value) {
        const text = escapeHtml(lookupLabels.outcome.get(String(value || '')) || value || L.Unknown || '');
        const normalized = String(value || '').toLowerCase();
        const color = normalized === 'succeeded'
            ? 'success'
            : normalized === 'failed'
                ? 'danger'
                : normalized === 'denied'
                    ? 'warning'
                    : 'secondary';
        return `<span class="badge bg-label-${color}">${text}</span>`;
    }

    function categoryBadge(value) {
        return `<span class="badge bg-label-primary">${escapeHtml(lookupLabels.category.get(String(value || '')) || value || L.Unknown || '')}</span>`;
    }

    function redactionStatusBadge(value) {
        const normalized = String(value || '').toLowerCase();
        const color = normalized === 'none' || !normalized
            ? 'secondary'
            : normalized.includes('actor')
                ? 'warning'
                : 'info';
        return `<span class="badge bg-label-${color}">${escapeHtml(value || L.Unknown || '')}</span>`;
    }

    function renderActor(row) {
        const name = row.actorDisplayNameMasked || row.ActorDisplayNameMasked || '';
        const email = row.actorEmailMasked || row.ActorEmailMasked || '';
        const actorId = row.actorId || row.ActorId || '';
        const primary = name || email || actorId || L.Unknown || '';
        const secondary = name && email ? email : actorId;
        return `<div class="d-flex flex-column"><span>${escapeHtml(primary)}</span><small class="text-muted">${escapeHtml(secondary)}</small></div>`;
    }

    function renderActions(row) {
        const id = row.id || row.Id;
        const actions = [
            {
                key: 'quickView',
                className: 'js-quick-view',
                text: L.ViewDetails || '',
                icon: 'bx bx-show',
                attrs: { 'data-id': id }
            }
        ];

        return window.DitenDataTable ? window.DitenDataTable.renderActions(actions) : '';
    }

    function mountInlineFilter() {
        const host = document.getElementById('inlineFilterHost');
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow =
            filterBtn?.closest('.dt-layout-row') ||
            filterBtn?.closest('.row') ||
            filterBtn?.closest('.dt-layout-end')?.parentElement;

        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.add('px-3');
        }
    }

    function toggleInlineFilter() {
        const collapseEl = document.getElementById('inlineFilterCollapse');
        if (!collapseEl) return;
        bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
    }

    function validateExportRange() {
        if (!appliedFilters.fromUtc || !appliedFilters.toUtc) {
            showError(L.AuditLogExportRequiresDates || '');
            return false;
        }

        const rangeMs = new Date(appliedFilters.toUtc).getTime() - new Date(appliedFilters.fromUtc).getTime();
        const maxDays = exportLimits?.maxDays;
        if (maxDays && rangeMs > maxDays * 24 * 60 * 60 * 1000) {
            showError(L.AuditLogExportMaxRangeError || '');
            return false;
        }

        return true;
    }

    async function downloadExport(format) {
        clearError();
        if (!validateExportRange()) return;

        const response = await fetch(`${endpoint}/export?${buildQuery(null, format)}`, {
            credentials: 'same-origin',
            headers: getAuthHeaders()
        });

        if (response.status === 401 || response.status === 403) {
            setPermissionMessage(response.status);
            return;
        }

        if (!response.ok) {
            showError(await readError(response));
            return;
        }

        const blob = await response.blob();
        const contentDisposition = response.headers.get('content-disposition') || '';
        const fileName = parseFileName(contentDisposition) || `audit.${format}`;
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        link.remove();
        URL.revokeObjectURL(url);
        window.showToast?.(L.AuditLogDownloadStarted || '', 'success');
    }

    function buildAffectedRecordsText(count) {
        const template = L.AuditLogRedactActorAffectedRecords || '';
        return template.replace('{0}', Number(count || 0).toLocaleString());
    }

    function buildRedactionConfirmContent() {
        const template = document.getElementById('audit-redaction-template');
        if (!template) return '';
        return template.innerHTML.trim();
    }

    function setRedactionBusy(isBusy) {
        redactionInProgress = isBusy;
        const btn = document.querySelector('.dt-redact-pii-btn');
        if (!btn) return;
        btn.disabled = isBusy;
        btn.classList.toggle('disabled', isBusy);
        btn.innerHTML = isBusy
            ? '<span class="spinner-border spinner-border-sm me-2" aria-hidden="true"></span>' + escapeHtml(L.AuditLogRedactPii || L.AuditLogRedactActorButton || '')
            : '<i class="icon-base bx bx-user-x icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block">' + escapeHtml(L.AuditLogRedactPii || L.AuditLogRedactActorButton || '') + '</span>';
    }

    async function lookupActor(actorId) {
        const params = new URLSearchParams();
        params.set('page', '1');
        params.set('pageSize', '1');
        params.set('actorId', actorId);

        const response = await fetch(`${endpoint}?${params.toString()}`, {
            credentials: 'same-origin',
            headers: getAuthHeaders()
        });

        if (response.status === 401 || response.status === 403) {
            setPermissionMessage(response.status);
            return null;
        }

        if (!response.ok) {
            throw new Error(await readError(response));
        }

        const result = normalizeItems(await response.json());
        const first = result.items[0] || null;
        if (!first || result.totalCount <= 0) return null;

        const displayName = first.actorDisplayNameMasked || first.ActorDisplayNameMasked || '';
        const email = first.actorEmailMasked || first.ActorEmailMasked || '';
        return {
            actorId,
            displayName: displayName || email || L.Unknown || '',
            email,
            affectedCount: result.totalCount
        };
    }

    async function showRedactionModal() {
        if (typeof window.showConfirm !== 'function') return null;

        return new Promise((resolve) => {
            let resolvedActor = null;
            let currentReason = '';
            window.showConfirm(L.AuditLogRedactActorTitle || '', (value) => {
                const reason = String(value || '').trim();
                resolve(resolvedActor && reason ? { actor: resolvedActor, reason } : null);
            }, {
                type: 'danger',
                width: '480px',
                subtext: buildRedactionConfirmContent(),
                confirmButtonText: L.AuditLogRedactActorButton || L.AuditLogRedactActor || '',
                cancelButtonText: L.AuditLogRedactActorCancel || L.Cancel || '',
                showInput: true,
                inputLabel: L.AuditLogRedactActorReason || '',
                inputPlaceholder: L.AuditLogRedactActorReasonPlaceholder || '',
                inputRequired: true,
                inputValidationMessage: L.AuditLogRedactActorReasonRequired || '',
                inputAttributes: {
                    maxlength: '500',
                    autocomplete: 'off',
                    rows: 2,
                    class: 'form-control form-control-sm w-100 rounded-3 shadow-none audit-redaction-reason'
                },
                inputValidator: (value) => {
                    const reason = String(value || '').trim();
                    if (!resolvedActor) return L.AuditLogRedactActorLookupRequired || '';
                    if (!reason) return L.AuditLogRedactActorReasonRequired || '';
                    if (reason.length > 500) return L.AuditLogRedactActorReasonMaxLength || '';
                    return null;
                },
                didOpen: (popup, SwalRef) => {
                    popup.classList.add('audit-redaction-swal');
                    popup.querySelector('.swal2-title')?.classList.add('w-100', 'text-center');
                    popup.querySelector('.swal2-html-container')?.classList.add('w-100', 'p-0', 'mx-0', 'text-start');
                    popup.querySelector('#swal2-html-container > .mb-2')?.classList.remove('mb-2');

                    const reasonLabel = popup.querySelector('.swal2-input-label');
                    reasonLabel?.classList.add('form-label', 'w-100', 'text-start', 'justify-content-start', 'mx-0', 'mt-3', 'mb-1', 'fw-medium', 'fs-6');

                    const mainIcon = popup.querySelector('.swal-icon-circle i');
                    if (mainIcon) {
                        mainIcon.className = 'bx bx-user-x text-danger';
                    }

                    const actorInput = popup.querySelector('#audit-redact-actor-id');
                    const findButton = popup.querySelector('#audit-redact-find-actor');
                    const feedback = popup.querySelector('#audit-redact-actor-feedback');
                    const preview = popup.querySelector('#audit-redact-preview');
                    const previewName = popup.querySelector('#audit-redact-preview-name');
                    const previewEmail = popup.querySelector('#audit-redact-preview-email');
                    const previewCount = popup.querySelector('#audit-redact-preview-count');
                    const reasonInput = popup.querySelector('.swal2-textarea');

                    const setFeedback = (message) => {
                        if (feedback) feedback.textContent = message || '';
                        actorInput?.classList.toggle('is-invalid', !!message);
                    };

                    const clearPreview = () => {
                        resolvedActor = null;
                        preview?.classList.add('d-none');
                        if (previewName) previewName.textContent = '';
                        if (previewEmail) previewEmail.textContent = '';
                        if (previewCount) previewCount.textContent = '';
                        if (reasonInput) {
                            reasonInput.value = '';
                            reasonInput.disabled = true;
                        }
                        SwalRef.disableConfirmButton();
                    };

                    const syncConfirm = () => {
                        currentReason = String(reasonInput?.value || '').trim();
                        if (resolvedActor && currentReason) SwalRef.enableConfirmButton();
                        else SwalRef.disableConfirmButton();
                    };

                    const setLoading = (isLoading) => {
                        if (!findButton) return;
                        findButton.disabled = isLoading;
                        findButton.innerHTML = isLoading
                            ? '<span class="spinner-border spinner-border-sm me-1" aria-hidden="true"></span>' + escapeHtml(L.AuditLogRedactActorFind || '')
                            : escapeHtml(L.AuditLogRedactActorFind || '');
                    };

                    const runLookup = async () => {
                        const actorId = String(actorInput?.value || '').trim();
                        clearPreview();
                        setFeedback('');
                        if (!isGuid(actorId)) {
                            setFeedback(L.AuditLogInvalidGuid || '');
                            return;
                        }

                        setLoading(true);
                        try {
                            const actor = await lookupActor(actorId);
                            if (!actor) {
                                setFeedback(L.AuditLogRedactActorNotFound || '');
                                return;
                            }

                            resolvedActor = actor;
                            actorInput?.classList.remove('is-invalid');
                            previewName.textContent = actor.displayName || L.Unknown || '';
                            previewEmail.textContent = actor.email || '';
                            previewCount.textContent = buildAffectedRecordsText(actor.affectedCount);
                            preview?.classList.remove('d-none');
                            if (reasonInput) reasonInput.disabled = false;
                            reasonInput?.focus();
                            syncConfirm();
                        } catch (error) {
                            console.error('[AuditLog] Actor lookup failed.', error);
                            setFeedback(error.message || L.AuditLogRedactActorLookupError || L.ErrorOccurred || '');
                        } finally {
                            setLoading(false);
                        }
                    };

                    clearPreview();
                    reasonInput?.addEventListener('input', syncConfirm);
                    actorInput?.addEventListener('input', () => {
                        clearPreview();
                        setFeedback('');
                    });
                    actorInput?.addEventListener('keydown', (event) => {
                        if (event.key === 'Enter') {
                            event.preventDefault();
                            runLookup();
                        }
                    });
                    findButton?.addEventListener('click', runLookup);
                },
                onCancel: () => resolve(null)
            });
        });
    }

    async function redactActor() {
        if (redactionInProgress) return;
        clearError();
        showRedactionResult('');
        setRedactionBusy(true);

        try {
            const selection = await showRedactionModal();
            if (!selection) return;

            const response = await fetch(`${endpoint}/redact-actor`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: Object.assign({ 'Content-Type': 'application/json' }, getAuthHeaders()),
                body: JSON.stringify({ actorId: selection.actor.actorId, reason: selection.reason })
            });

            if (response.status === 401 || response.status === 403) {
                setPermissionMessage(response.status);
                return;
            }

            if (!response.ok) {
                showError(await readError(response));
                return;
            }

            const result = normalizeGatewayPayload(await response.json());
            const count = result.redactedEventCount ?? result.RedactedEventCount ?? 0;
            const message = (L.AuditLogRedactActorSuccess || '').replace('{0}', count);
            showRedactionResult(message);
            window.showToast?.(message, 'success');
            dt?.ajax.reload(null, false);
        } finally {
            setRedactionBusy(false);
        }
    }

    function parseFileName(contentDisposition) {
        const match = /filename\*?=(?:UTF-8''|")?([^";]+)/i.exec(contentDisposition);
        return match ? decodeURIComponent(match[1].replace(/"/g, '')) : '';
    }

    async function readError(response) {
        try {
            const payload = await response.json();
            const errors = payload.errors || payload.Errors || [];
            if (errors.length) return errors.join(' ');
            return payload.detail || payload.Detail || L.ErrorOccurred || '';
        } catch {
            return L.ErrorOccurred || '';
        }
    }

    function initSelect2Filters() {
        if (!window.jQuery?.fn?.select2) return;

        $('#inlineFilterHost select.select2').each(function () {
            const $select = $(this);
            if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
            const isMultiple = $select.prop('multiple');

            $select.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                containerCssClass: isMultiple ? 'dt-inline-filter-multi' : undefined,
                minimumResultsForSearch: Infinity,
                selectionCssClass: 'form-select form-select-sm',
                width: 'element',
                placeholder: $select.data('placeholder') || '',
                closeOnSelect: !isMultiple,
                allowClear: !isMultiple
            });

            if (isMultiple) {
                $select.off('change.select2-summary').on('change.select2-summary', function () { syncMultiSelectSummary($select); });
                requestAnimationFrame(() => syncMultiSelectSummary($select));
            }
        });
    }

    function syncMultiSelectSummary($select) {
        const $container = $select.next('.select2-container');
        const $rendered = $container.find('.select2-selection__rendered');
        const $selection = $container.find('.select2-selection--multiple');
        if (!$container.length || !$rendered.length || !$selection.length) return;

        let $summary = $selection.find('.dt-inline-filter-multi__summary');
        let $actions = $selection.find('.dt-inline-filter-multi__actions');
        let $count = $selection.find('.dt-inline-filter-multi__count');
        let $arrow = $selection.find('.select2-selection__arrow');
        if (!$summary.length) $summary = $('<span class="dt-inline-filter-multi__summary"></span>').prependTo($selection);
        if (!$actions.length) $actions = $('<span class="dt-inline-filter-multi__actions"></span>').appendTo($selection);
        if (!$count.length) $count = $('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>').appendTo($actions);
        if (!$arrow.length) $arrow = $('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>').appendTo($selection);

        const placeholder = normalizeString($select.data('placeholder')) || '';
        const selectedValues = normalizeArray($select.val());
        const selectedTexts = ($select.select2('data') || []).map((item) => normalizeString(item.text)).filter(Boolean);
        $summary.text(placeholder);
        $rendered.attr('title', selectedTexts.join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', selectedValues.length > 0);
        $count.toggleClass('d-none', selectedValues.length === 0).text(String(selectedValues.length));
        $actions.find('.dt-multi-clear-btn').remove();

        if (selectedValues.length > 0) {
            const $clearBtn = $('<span class="dt-multi-clear-btn" role="button" aria-label="' + (L.Reset || '') + '" title="' + (L.Reset || '') + '">&times;</span>');
            $clearBtn.on('mousedown', (event) => {
                event.preventDefault();
                event.stopPropagation();
                $select.val(null).trigger('change');
            });
            $actions.append($clearBtn);
        }
    }

    function initDateFilterLabels() {
        document.querySelectorAll('#inlineFilterHost .dt-date-filter').forEach((wrapper) => {
            const input = wrapper.querySelector('input[type="datetime-local"]');
            if (!input || input.dataset.dateLabelBound) return;
            input.dataset.dateLabelBound = '1';

            const sync = () => wrapper.classList.toggle('dt-has-value', !!input.value);
            const openPicker = () => {
                input.focus({ preventScroll: true });
                if (typeof input.showPicker === 'function') {
                    try { input.showPicker(); } catch (e) { }
                }
            };
            input.addEventListener('input', sync);
            input.addEventListener('change', sync);
            input.addEventListener('focus', () => wrapper.classList.add('dt-focused'));
            input.addEventListener('blur', () => {
                wrapper.classList.remove('dt-focused');
                sync();
            });
            input.addEventListener('click', openPicker);
            wrapper.addEventListener('click', (event) => {
                if (event.target === input) return;
                openPicker();
            });
            sync();
        });
    }

    function initFilters() {
        initDateFilterLabels();

        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            const next = collectFilters();
            if (!next) return;
            appliedFilters = next;
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
            clearError();
            dt?.ajax.reload();
            window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount());
        });

        document.getElementById('btnFilterReset')?.addEventListener('click', (event) => {
            event.preventDefault();
            applySavedTableState(dt, getResetBaselineState());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
            clearError();
        });
    }

    async function initDataTable() {
        if (!tableEl || typeof DataTable === 'undefined' || !window.DtDefaults) return;
        const [initialViewState] = await Promise.all([loadDefaultView(), loadExportLimits(), loadFilterLookups()]);

        const filterBtn = {
            text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
            className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
            attr: { title: L.Filter || '', 'aria-controls': 'inlineFilterCollapse', 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
            action: () => toggleInlineFilter()
        };

        const saveFilterBtn = {
            text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + escapeHtml(L.CommonSaveView || L.SaveView || 'Save View') + '</span>',
            className: 'btn btn-label-primary d-none dt-save-filter-btn',
            attr: { title: L.CommonSaveView || L.SaveView || 'Save View', 'data-bs-toggle': 'tooltip' },
            action: async function (e, api) {
                const tableApi = api || dt;
                if (!tableApi) return;
                try {
                    await saveDefaultView(getCurrentView(tableApi));
                    setSaveFilterVisible(false);
                    window.showToast?.(L.AuditLogViewSaved || L.RecordSaved || L.SaveView || '', 'success');
                } catch (error) {
                    if (error?.authHandled) return;
                    console.error('[AuditLog SaveView] Failed to save default view.', error);
                    window.showToast?.(L.ErrorOccurred || '', 'error');
                }
            }
        };

        const redactPiiBtn = {
            text: '<i class="icon-base bx bx-user-x icon-sm me-0 me-sm-2"></i><span class="d-none d-sm-inline-block">' + escapeHtml(L.AuditLogRedactPii || L.AuditLogRedactActorButton || '') + '</span>',
            className: 'btn btn-label-danger dt-redact-pii-btn',
            attr: { title: L.AuditLogRedactActorTooltip || L.AuditLogRedactPii || '', 'data-bs-toggle': 'tooltip' },
            action: () => {
                redactActor().catch((error) => {
                    console.error('[AuditLog] Actor redaction failed.', error);
                    showError(L.ErrorOccurred || '');
                });
            }
        };

        const exportButtons = window.DtDefaults.exportButtons('', null, { filterBtn, saveFilterBtn }, {
            exportColumns: [1, 2, 4, 5, 8],
            colvisColumns: [1, 2, 3, 4, 5, 6, 7, 8, 9]
        });
        exportButtons.push({ buttons: [redactPiiBtn] });

        // Filter the default export list to only show Print, CSV, and JSON
        if (exportButtons.length > 0 && exportButtons[0].buttons && exportButtons[0].buttons[0]) {
            exportButtons[0].buttons[0].buttons = [
                {
                    extend: 'print',
                    text: `<span class="d-flex align-items-center"><i class="icon-base bx bx-printer me-2"></i>${escapeHtml(L.Print || '')}</span>`,
                    className: 'dropdown-item',
                    autoPrint: false
                },
                {
                    text: `<span class="d-flex align-items-center"><i class="icon-base bx bx-file me-2"></i>${escapeHtml(L.AuditLogExportCsv || '')}</span>`,
                    className: 'dropdown-item',
                    action: () => downloadExport('csv')
                },
                {
                    text: `<span class="d-flex align-items-center"><i class="icon-base bx bx-code-alt me-2"></i>${escapeHtml(L.AuditLogExportJson || '')}</span>`,
                    className: 'dropdown-item',
                    action: () => downloadExport('json')
                }
            ];
        }
        const config = window.DtDefaults.create({
            processing: true,
            serverSide: true,
            stateSave: false,
            colReorder: { columns: ':gt(0):not(:last-child)' },
            order: [[1, 'desc']],
            buttons: exportButtons,
            ajax: function (data, callback) {
                setPermissionVisible(false);
                fetch(`${endpoint}?${buildQuery(data)}`, {
                    credentials: 'same-origin',
                    headers: getAuthHeaders()
                })
                    .then((response) => {
                        if (response.status === 401 || response.status === 403) {
                            setPermissionMessage(response.status);
                            callback({ data: [], recordsTotal: 0, recordsFiltered: 0 });
                            return null;
                        }
                        if (!response.ok) throw response;
                        return response.json();
                    })
                    .then((payload) => {
                        if (!payload) return;
                        const result = normalizeItems(payload);
                        setEmptyVisible(result.totalCount === 0);
                        callback({
                            data: result.items,
                            recordsTotal: result.totalCount,
                            recordsFiltered: result.totalCount
                        });
                    })
                    .catch(async (error) => {
                        showError(error instanceof Response ? await readError(error) : L.ErrorOccurred || '');
                        callback({ data: [], recordsTotal: 0, recordsFiltered: 0 });
                    });
            },
            columns: [
                { data: 'id', name: 'control' },
                { data: 'occurredAtUtc', name: 'occurredAtUtc', render: (data, type) => formatDate(data, type) },
                { data: null, name: 'actor', orderable: false, render: (data, type, row) => renderActor(row) },
                { data: 'tenantId', name: 'tenantId', render: (data) => `<code>${escapeHtml(data)}</code>` },
                { data: 'category', name: 'category', render: categoryBadge },
                { data: 'operation', name: 'operation', render: escapeHtml },
                { data: 'entityType', name: 'entityType', render: escapeHtml },
                { data: 'entityId', name: 'entityId', render: (data) => data ? `<code>${escapeHtml(data)}</code>` : '' },
                { data: 'outcome', name: 'outcome', render: statusBadge },
                { data: 'sourceModule', name: 'sourceModule', render: escapeHtml },
                { data: null, name: 'action', orderable: false, searchable: false, className: 'text-end', render: (data, type, row) => renderActions(row) }
            ],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                { targets: 1, responsivePriority: 1 },
                { targets: [3, 6, 7, 9], visible: false },
                { targets: -1, title: L.Actions || '', searchable: false, orderable: false, className: 'cell-fit all text-end pe-3' }
            ],
            initComplete: function () {
                mountInlineFilter();
                initSelect2Filters();
                initFilters();
                applySavedTableState(this.api(), initialViewState || { filters: appliedFilters });
                setTimeout(() => { saveFilterArmed = true; }, 0);
            },
            drawCallback: function () {
                window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
            }
        });

        dt = new DataTable(tableEl, config);

        dt.on('column-visibility.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        dt.on('search.dt order.dt', function () {
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });

        dt.on('column-reorder.dt columns-reordered.dt', function () {
            window.DtDefaults.updateVisualState(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
        });
    }

    async function openDetails(id) {
        if (!id || !detailModal) return;

        document.getElementById('audit-detail-loading')?.classList.remove('d-none');
        document.getElementById('audit-detail-content')?.classList.add('d-none');
        detailModal.show();

        const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, {
            credentials: 'same-origin',
            headers: getAuthHeaders()
        });

        if (response.status === 401 || response.status === 403) {
            setPermissionMessage(response.status);
            detailModal.hide();
            return;
        }

        if (!response.ok) {
            showError(await readError(response));
            detailModal.hide();
            return;
        }

        const payload = normalizeGatewayPayload(await response.json());
        renderDetails(payload);
    }

    let currentDetail = null;

    function renderDetails(item) {
        currentDetail = item;
        const loading = document.getElementById('audit-detail-loading');
        const content = document.getElementById('audit-detail-content');
        const beforeState = item.beforeState || item.BeforeState || null;
        const afterState = item.afterState || item.AfterState || null;
        const metadata = item.metadata || item.Metadata || {};

        renderDetailHeader(item);
        renderSummary(item);
        setJson('audit-before-json', beforeState);
        setJson('audit-after-json', afterState);
        renderMetadata(metadata);
        renderDiff(beforeState, afterState);
        renderForensics(item);
        renderRedactionTrace(item);
        setJson('audit-raw-json', item);
        bindDetailActions(item);

        // Reset to Overview tab on each open
        const overviewBtn = document.getElementById('audit-tab-overview-btn');
        if (overviewBtn && bootstrap?.Tab) {
            bootstrap.Tab.getOrCreateInstance(overviewBtn).show();
        }

        loading?.classList.add('d-none');
        content?.classList.remove('d-none');
    }

    function outcomeMeta(rawOutcome) {
        const normalized = String(rawOutcome || '').toLowerCase();
        if (normalized === 'succeeded') return { color: 'success', icon: 'bx-check-circle' };
        if (normalized === 'failed') return { color: 'danger', icon: 'bx-x-circle' };
        if (normalized === 'denied') return { color: 'warning', icon: 'bx-block' };
        return { color: 'secondary', icon: 'bx-detail' };
    }

    function renderDetailHeader(item) {
        const operation = item.operation || item.Operation || '';
        const entityType = item.entityType || item.EntityType || '';
        const entityId = item.entityId || item.EntityId || '';
        const outcome = item.outcome || item.Outcome || '';
        const occurredAt = item.occurredAtUtc || item.OccurredAtUtc;
        const actorName = item.actorDisplayNameMasked || item.ActorDisplayNameMasked;
        const actorEmail = item.actorEmailMasked || item.ActorEmailMasked;
        const actorId = item.actorId || item.ActorId;

        const titleEl = document.getElementById('auditDetailModalLabel');
        if (titleEl) {
            const opLabel = lookupLabels.operation.get(operation) || operation;
            const entityLabel = entityType + (entityId ? ' · ' + String(entityId).slice(0, 8) : '');
            titleEl.textContent = `${opLabel} → ${entityLabel}`;
        }

        const occurredEl = document.getElementById('audit-detail-occurred');
        if (occurredEl) {
            occurredEl.textContent = formatDateTime(occurredAt) || '--';
            occurredEl.setAttribute('datetime', occurredAt || '');
        }

        const actorEl = document.getElementById('audit-detail-actor-text');
        if (actorEl) {
            actorEl.textContent = actorName || actorEmail || actorId || (L.Unknown || '--');
        }

        const meta = outcomeMeta(outcome);
        const avatar = document.getElementById('audit-detail-outcome-avatar');
        const iconEl = document.getElementById('audit-detail-outcome-icon');
        if (avatar) {
            avatar.className = `avatar-initial rounded border text-${meta.color}`;
        }
        if (iconEl) {
            iconEl.className = `icon-base bx ${meta.icon} fs-4`;
        }

        const badgeEl = document.getElementById('audit-detail-outcome-badge');
        if (badgeEl && outcome) {
            const text = lookupLabels.outcome.get(outcome) || outcome;
            badgeEl.innerHTML = `<span class="badge bg-label-${meta.color}">${escapeHtml(text)}</span>`;
        } else if (badgeEl) {
            badgeEl.innerHTML = '';
        }
    }

    function formatDateTime(value) {
        if (!value) return '';
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return String(value);
        return new Intl.DateTimeFormat(undefined, {
            dateStyle: 'medium',
            timeStyle: 'medium',
            timeZone: 'UTC'
        }).format(date) + ' UTC';
    }

    function renderSummary(item) {
        const host = document.getElementById('audit-detail-summary');
        if (!host) return;

        const tenantId = item.tenantId || item.TenantId;
        const actorId = item.actorId || item.ActorId;
        const category = item.category || item.Category;
        const sourceService = item.sourceService || item.SourceService;
        const sourceModule = item.sourceModule || item.SourceModule;
        const entityType = item.entityType || item.EntityType;
        const entityId = item.entityId || item.EntityId;
        const redactionStatus = item.redactionStatus || item.RedactionStatus;

        const fields = [
            {
                label: L.AuditLogFilterActor,
                icon: 'bx-user',
                primary: item.actorDisplayNameMasked || item.ActorDisplayNameMasked || L.Unknown,
                secondary: item.actorEmailMasked || item.ActorEmailMasked || actorId,
                codeSecondary: true
            },
            {
                label: L.AuditLogFilterTenant,
                icon: 'bx-buildings',
                primary: item.tenantDisplayName || item.TenantDisplayName || (L.AuditLogFilterTenant || 'Tenant'),
                secondary: tenantId,
                codeSecondary: true
            },
            {
                label: L.AuditLogFilterCategory,
                icon: 'bx-tag-alt',
                primary: `<span class="badge bg-label-success">${escapeHtml(lookupLabels.category.get(category) || category || L.Unknown || '')}</span>`,
                isHtml: true
            },
            {
                label: sourceModule ? (L.AuditLogDetailSourceModule || 'Source Module') : (L.AuditLogDetailSourceService || 'Source Service'),
                icon: 'bx-package',
                primary: sourceModule || sourceService || L.Unknown,
                secondary: sourceModule && sourceService && sourceModule !== sourceService ? `${L.AuditLogDetailSourceService || 'Source Service'}: ${sourceService}` : null
            },
            {
                label: L.AuditLogDetailEntityType || 'Entity Type',
                icon: 'bx-cube',
                primary: entityType || L.Unknown,
                secondary: entityId,
                codeSecondary: true
            },
            {
                label: L.AuditLogDetailRedactionStatus,
                icon: 'bx-shield-quarter',
                primary: redactionStatusBadge(redactionStatus),
                isHtml: true
            }
        ];

        host.innerHTML = fields.map((f) => {
            const primaryValue = f.primary || L.Unknown || '--';
            const primaryTitle = f.isHtml ? '' : ` title="${escapeHtml(primaryValue)}"`;
            return `
            <div class="col-12 col-md-6">
                <div class="card backbone-preview-section p-4 h-100 shadow-none">
                    <div class="backbone-preview-field h-100">
                        <i class="bx ${f.icon}"></i>
                        <div class="min-w-0 flex-grow-1">
                            <div class="backbone-preview-label">${escapeHtml(f.label || '')}</div>
                            <div class="backbone-preview-value mt-1 text-truncate"${primaryTitle}>
                                ${f.isHtml ? primaryValue : escapeHtml(primaryValue)}
                            </div>
                            ${f.secondary ? `<div class="backbone-preview-description mt-1 text-truncate ${f.codeSecondary ? 'font-monospace small' : ''}" title="${escapeHtml(f.secondary)}">${escapeHtml(f.secondary)}</div>` : ''}
                        </div>
                    </div>
                </div>
            </div>
        `;
        }).join('');
    }

    function renderForensics(item) {
        const host = document.getElementById('audit-detail-forensics');
        if (!host) return;

        const occurredAt = item.occurredAtUtc || item.OccurredAtUtc;
        const writtenAt = item.writtenAtUtc || item.WrittenAtUtc;
        const delayMs = (occurredAt && writtenAt)
            ? Math.max(0, new Date(writtenAt).getTime() - new Date(occurredAt).getTime())
            : null;

        const fields = [
            {
                label: L.AuditLogDetailEventId || 'Event ID',
                value: item.id || item.Id,
                icon: 'bx-id-card',
                copyable: true
            },
            {
                label: L.AuditLogDetailCorrelationId,
                value: item.correlationId || item.CorrelationId,
                icon: 'bx-link',
                copyable: true
            },
            {
                label: L.AuditLogDetailOccurredAt || 'Occurred At',
                value: formatDateTime(occurredAt),
                icon: 'bx-time-five',
                rawValue: occurredAt
            },
            {
                label: L.AuditLogDetailWrittenAt || 'Written At',
                value: formatDateTime(writtenAt),
                icon: 'bx-save',
                rawValue: writtenAt
            },
            {
                label: L.AuditLogDetailWriteDelay || 'Write Delay',
                icon: 'bx-timer',
                value: delayMs !== null ? `${delayMs} ${L.AuditLogDetailDelayUnit || 'ms'}` : '--'
            },
            {
                label: L.AuditLogDetailIpAddress || 'IP Address',
                value: item.ipAddressMasked || item.IpAddressMasked,
                icon: 'bx-network-chart',
                code: true
            },
            {
                label: L.AuditLogDetailUserAgent || 'User Agent',
                value: item.userAgent || item.UserAgent,
                icon: 'bx-window-alt',
                truncate: true
            },
            {
                label: L.AuditLogDetailSourceService || 'Source Service',
                value: item.sourceService || item.SourceService,
                icon: 'bx-server'
            }
        ];

        host.innerHTML = fields.map((f) => {
            const value = f.value || L.Unknown || '--';
            const display = f.code
                ? `<code class="font-monospace small">${escapeHtml(value)}</code>`
                : f.truncate
                    ? `<span class="text-truncate d-block" title="${escapeHtml(value)}">${escapeHtml(value)}</span>`
                    : `<span>${escapeHtml(value)}</span>`;
            const copyBtn = f.copyable && f.value
                ? `<button type="button" class="btn btn-icon btn-sm btn-label-secondary js-copy-value ms-2" data-copy-value="${escapeHtml(f.value)}" aria-label="${escapeHtml(L.AuditLogDetailCopy || 'Copy')}"><span class="icon-base bx bx-copy" aria-hidden="true"></span></button>`
                : '';
            return `
                <div class="col-12 col-md-6">
                    <div class="card backbone-preview-section p-4 h-100 shadow-none">
                        <div class="backbone-preview-field h-100">
                            <i class="bx ${f.icon || 'bx-info-circle'}"></i>
                            <div class="min-w-0 flex-grow-1">
                                <div class="backbone-preview-label">${escapeHtml(f.label || '')}</div>
                                <div class="d-flex align-items-center justify-content-between gap-2 mt-1">
                                    <div class="backbone-preview-value min-w-0 flex-grow-1">${display}</div>
                                    ${copyBtn}
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            `;
        }).join('');
    }

    function renderRedactionTrace(item) {
        const container = document.getElementById('audit-redaction-trace');
        const fieldsHost = document.getElementById('audit-redaction-trace-fields');
        if (!container || !fieldsHost) return;

        const redactedAt = item.redactedAtUtc || item.RedactedAtUtc;
        const redactedBy = item.redactedByActorId || item.RedactedByActorId;
        const reason = item.redactionReason || item.RedactionReason;
        const status = String(item.redactionStatus || item.RedactionStatus || '').toLowerCase();
        const hasTrace = status !== 'none' && status !== '' && (redactedAt || redactedBy || reason);

        if (!hasTrace) {
            container.classList.add('d-none');
            fieldsHost.innerHTML = '';
            return;
        }

        const traceFields = [
            { label: L.AuditLogDetailRedactedAt || 'Redacted At', value: formatDateTime(redactedAt), icon: 'bx-time-five' },
            { label: L.AuditLogDetailRedactedBy || 'Redacted By', value: redactedBy, icon: 'bx-user-check', code: true },
            { label: L.AuditLogDetailRedactionReason || 'Reason', value: reason, icon: 'bx-message-square-detail' }
        ];

        fieldsHost.innerHTML = traceFields.map((f) => {
            const display = f.value
                ? (f.code ? `<code class="font-monospace small">${escapeHtml(f.value)}</code>` : escapeHtml(f.value))
                : (L.Unknown || '--');
            return `
                <div class="col-12 col-md-${f.label === (L.AuditLogDetailRedactionReason || 'Reason') ? '12' : '6'}">
                    <div class="card backbone-preview-section p-4 h-100 shadow-none">
                        <div class="backbone-preview-field h-100">
                            <i class="bx ${f.icon}"></i>
                            <div class="min-w-0 flex-grow-1">
                                <div class="backbone-preview-label">${escapeHtml(f.label || '')}</div>
                                <div class="backbone-preview-value mt-1">${display}</div>
                            </div>
                        </div>
                    </div>
                </div>
            `;
        }).join('');
        container.classList.remove('d-none');
    }

    function bindDetailActions(item) {
        const copyCidBtn = document.getElementById('audit-action-copy-cid');
        if (copyCidBtn) {
            copyCidBtn.onclick = () => copyToClipboard(item.correlationId || item.CorrelationId, copyCidBtn);
        }

        const relatedBtn = document.getElementById('audit-action-related');
        if (relatedBtn) {
            relatedBtn.onclick = () => {
                const cid = item.correlationId || item.CorrelationId;
                console.log('[AuditLog] Related Events clicked, CID =', cid);
                applyCorrelationFilter(cid);
            };
        } else {
            console.warn('[AuditLog] #audit-action-related button not found at bind time');
        }

        const redactBtn = document.getElementById('audit-action-redact-from-detail');
        if (redactBtn) {
            redactBtn.onclick = () => triggerRedactFromDetail(item);
        }

        // Bind copy buttons inside Forensics tab and JSON panes
        document.querySelectorAll('.js-copy-value').forEach((btn) => {
            btn.onclick = () => copyToClipboard(btn.getAttribute('data-copy-value'), btn);
        });
        document.querySelectorAll('.js-copy-json').forEach((btn) => {
            btn.onclick = () => {
                const source = btn.getAttribute('data-source');
                const payload = source === 'before' ? (currentDetail?.beforeState || currentDetail?.BeforeState) : (currentDetail?.afterState || currentDetail?.AfterState);
                if (payload) copyToClipboard(JSON.stringify(payload, null, 2), btn);
            };
        });

        // Engineering metadata toggle
        const engBtn = document.getElementById('audit-toggle-engineering');
        const engBlock = document.getElementById('audit-engineering-block');
        const engLabel = document.getElementById('audit-toggle-engineering-label');
        if (engBtn && engBlock && engLabel) {
            engBtn.onclick = () => {
                const isHidden = engBlock.classList.toggle('d-none');
                engLabel.textContent = isHidden
                    ? (L.AuditLogDetailShowEngineering || 'Show Engineering')
                    : (L.AuditLogDetailHideEngineering || 'Hide Engineering');
            };
        }
    }

    function copyToClipboard(value, button) {
        if (!value || !navigator.clipboard) return;
        navigator.clipboard.writeText(String(value)).then(() => {
            if (!button) return;
            const original = button.innerHTML;
            button.innerHTML = `<i class="icon-base bx bx-check"></i> ${escapeHtml(L.AuditLogDetailCopied || 'Copied')}`;
            setTimeout(() => { button.innerHTML = original; }, 1500);
        });
    }

    function applyCorrelationFilter(correlationId) {
        if (!correlationId) {
            console.warn('[AuditLog] applyCorrelationFilter: empty correlationId');
            return;
        }
        if (!dt) {
            console.warn('[AuditLog] applyCorrelationFilter: DataTable not initialized');
            return;
        }

        // Close detail modal (animated)
        if (detailModal) detailModal.hide();

        // Defer visual + search so the modal close animation does not race the redraw.
        // The toolbar search is the visible source of truth; buildQuery maps GUID
        // search values to the backend correlationId filter.
        setTimeout(() => {
            try {
                dt.search(correlationId);
                syncSearchInput(dt, correlationId);
            } catch (e) {
                console.warn('[AuditLog] toolbar search sync failed', e);
            }
            clearError();
            try {
                dt.draw();
            } catch (e) {
                console.error('[AuditLog] search draw failed', e);
            }
            window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount());
        }, 50);
    }

    function triggerRedactFromDetail(item) {
        const actorId = item.actorId || item.ActorId;
        if (!actorId) return;
        // Pre-fill via a window-scoped hint; the existing showRedactionModal flow
        // reads this in its actor-id input handler. If not present the user enters it manually.
        window.__auditRedactPrefillActorId = actorId;
        if (detailModal) detailModal.hide();
        if (typeof showRedactionModal === 'function') {
            // Allow modal close animation to settle before re-opening
            setTimeout(() => showRedactionModal(), 200);
        }
    }

    function renderMetadata(metadata) {
        const tableBody = document.querySelector('#audit-metadata-table tbody');
        const jsonPane = document.getElementById('audit-metadata-json');
        if (!tableBody || !jsonPane) return;

        const engineeringKeys = [
            'AuditOutboxIdempotencyKey',
            'AuditOutboxMessageId',
            'paging',
            'Paging',
            'TraceId',
            'SpanId'
        ];

        const businessMetadata = {};
        const engineeringMetadata = {};

        Object.keys(metadata).forEach((key) => {
            if (engineeringKeys.some((ek) => key.includes(ek))) {
                engineeringMetadata[key] = metadata[key];
            } else {
                businessMetadata[key] = metadata[key];
            }
        });

        tableBody.innerHTML = Object.entries(businessMetadata)
            .filter(([, value]) => value !== null && value !== undefined)
            .map(([key, value]) => `
            <tr>
                <td class="text-muted px-3 py-2 border-end audit-log-metadata-key">${escapeHtml(key)}</td>
                <td class="px-3 py-2 text-heading font-monospace audit-log-metadata-value">${escapeHtml(typeof value === 'object' ? JSON.stringify(value) : String(value))}</td>
            </tr>
        `).join('') || `<tr><td colspan="2" class="text-muted p-3 text-center small">${escapeHtml(L.AuditLogNoBusinessMetadata || '')}</td></tr>`;

        jsonPane.textContent = Object.keys(engineeringMetadata).length > 0
            ? JSON.stringify(engineeringMetadata, null, 2)
            : JSON.stringify(metadata, null, 2);
    }

    function setJson(id, value) {
        const el = document.getElementById(id);
        if (!el) return;
        if (!value || (typeof value === 'object' && Object.keys(value).length === 0)) {
            el.innerHTML = `<span class="text-muted italic">${escapeHtml(L.AuditLogDetailNoStateChange || '')}</span>`;
            return;
        }
        el.textContent = JSON.stringify(value, null, 2);
    }

    function flatten(value, prefix, output) {
        if (value === null || value === undefined || typeof value !== 'object' || Array.isArray(value)) {
            output[prefix || '.'] = value;
            return output;
        }

        Object.keys(value).forEach((key) => {
            const next = prefix ? `${prefix}.${key}` : key;
            flatten(value[key], next, output);
        });
        return output;
    }

    function renderDiff(beforeState, afterState) {
        const host = document.getElementById('audit-diff-list');
        if (!host) return;

        const beforeFlat = flatten(beforeState || {}, '', {});
        const afterFlat = flatten(afterState || {}, '', {});
        const keys = Array.from(new Set(Object.keys(beforeFlat).concat(Object.keys(afterFlat)))).sort();
        const changes = keys.filter((key) => JSON.stringify(beforeFlat[key]) !== JSON.stringify(afterFlat[key]));

        if (changes.length === 0) {
            host.innerHTML = `
                <div class="p-3 bg-lighter rounded">
                    <div class="backbone-preview-description d-flex align-items-center justify-content-center gap-2 mb-0">
                        <span class="icon-base bx bx-info-circle flex-shrink-0" aria-hidden="true"></span>
                        <span>${escapeHtml(L.AuditLogNoChanges || '')}</span>
                    </div>
                </div>
            `;
            return;
        }

        host.innerHTML = changes.map((key) => {
            const beforeHas = Object.prototype.hasOwnProperty.call(beforeFlat, key);
            const afterHas = Object.prototype.hasOwnProperty.call(afterFlat, key);
            const label = beforeHas && afterHas
                ? L.AuditLogChanged || ''
                : afterHas
                    ? L.AuditLogAdded || ''
                    : L.AuditLogRemoved || '';
            const badgeColor = beforeHas && afterHas ? 'warning' : (afterHas ? 'success' : 'danger');
            const icon = beforeHas && afterHas ? 'bx-edit-alt' : (afterHas ? 'bx-plus-circle' : 'bx-minus-circle');

            return `
                <div class="list-group-item list-group-item-action d-flex align-items-center justify-content-between gap-4 py-4">
                    <div class="d-flex align-items-center gap-3 min-w-0 flex-grow-1">
                        <div class="avatar avatar-sm flex-shrink-0">
                            <span class="avatar-initial rounded bg-label-${badgeColor}">
                                <i class="icon-base bx ${icon} fs-5"></i>
                            </span>
                        </div>
                        <div class="text-truncate">
                            <h6 class="backbone-preview-value mb-1 text-truncate font-monospace audit-log-diff-key">${escapeHtml(key)}</h6>
                            <span class="badge bg-label-${badgeColor} text-uppercase audit-log-diff-badge">${escapeHtml(label)}</span>
                        </div>
                    </div>
                    <div class="d-flex align-items-center gap-4 flex-shrink-0 text-end">
                        <div class="d-flex flex-column align-items-end gap-2">
                            ${beforeHas ? `
                                <div class="d-flex align-items-center gap-2">
                                    <span class="backbone-preview-label">${escapeHtml(L.AuditLogDetailOld || '')}</span>
                                    <code class="text-muted small text-decoration-line-through bg-label-secondary px-2 rounded">${escapeHtml(JSON.stringify(beforeFlat[key]))}</code>
                                </div>
                            ` : ''}
                            ${afterHas ? `
                                <div class="d-flex align-items-center gap-2">
                                    <span class="backbone-preview-label">${escapeHtml(L.AuditLogDetailNew || '')}</span>
                                    <code class="text-primary small fw-medium bg-label-primary px-2 rounded">${escapeHtml(JSON.stringify(afterFlat[key]))}</code>
                                </div>
                            ` : ''}
                        </div>
                    </div>
                </div>
            `;
        }).join('');
    }

    document.addEventListener('click', (event) => {
        const quickView = event.target.closest('.js-quick-view');
        if (!quickView) return;
        event.preventDefault();
        openDetails(quickView.getAttribute('data-id')).catch((error) => {
            console.error('[AuditLog] Detail open failed.', error);
            showError(L.ErrorOccurred || '');
        });
    });

    document.addEventListener('DOMContentLoaded', () => {
        syncL10n();
        initDataTable().catch((error) => {
            console.error('[AuditLog] DataTable initialization failed.', error);
            showError(L.ErrorOccurred || '');
        });
    });
})();
