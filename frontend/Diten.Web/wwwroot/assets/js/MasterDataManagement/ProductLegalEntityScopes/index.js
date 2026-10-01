/* MOD-0290-FU03 Product Legal Entity Scope Assignment — Golden Slim, same-origin only. */
const ProductLegalEntityScopesList = (() => {
    'use strict';

    const endpoint = '/MasterDataManagement/ProductLegalEntityScopes/api';
    const tableEl = document.querySelector('.datatables-product-legal-entity-scopes');
    const host = document.querySelector('[data-can-configure]');
    const canConfigure = host?.dataset.canConfigure === 'true';
    const canReplace = host?.dataset.canReplace === 'true';
    const canEnd = host?.dataset.canEnd === 'true';
    const L = window.L10n || {};
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'MasterDataManagement', pageKey: 'ProductLegalEntityScopes' };
    const baseOrder = [[1, 'asc']];
    const totalColumnCount = 5;
    const visibleColumns = [1, 2, 3];
    let dt = null;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveViewArmed = false;
    let writeMode = 'create';

    const value = (item, camel, pascal) => item?.[camel] ?? item?.[pascal];
    const unwrap = payload => payload?.data ?? payload?.Data ?? payload;
    const escapeHtml = input => String(input ?? '').replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[character]));
    const emptyFilters = () => ({ search: '' });
    const defaultColVis = () => visibleColumns.reduce((result, index) => ({ ...result, [index]: true }), {});
    const normalizeView = input => ({
        filters: { ...emptyFilters(), ...(input?.filters || {}) },
        search: String(input?.search || ''),
        colVis: input?.colVis || defaultColVis(),
        columnOrder: Array.isArray(input?.columnOrder) && input.columnOrder.length === totalColumnCount ? input.columnOrder : Array.from({ length: totalColumnCount }, (_, index) => index),
        order: Array.isArray(input?.order) && input.order.length ? input.order : baseOrder
    });
    const createInitialTableState = saved => {
        const state = normalizeView(saved || getResetBaselineState());
        const input = document.getElementById('productScopeSearch');
        if (input) input.value = state.filters.search || state.search;
        return {
            state,
            search: { search: state.search },
            order: state.order,
            colReorder: { columns: ':gt(0):not(:last-child)', order: state.columnOrder }
        };
    };
    const getCurrentView = api => normalizeView({
        filters: { search: document.getElementById('productScopeSearch')?.value || '' },
        search: api.search(),
        colVis: visibleColumns.reduce((result, index) => ({ ...result, [index]: api.column(index).visible() }), {}),
        columnOrder: api.colReorder?.order?.() || Array.from({ length: totalColumnCount }, (_, index) => index),
        order: api.order()
    });
    const getResetBaselineState = () => normalizeView({
        filters: emptyFilters(), search: '', colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, index) => index), order: baseOrder
    });
    const setSaveFilterVisible = visible => {
        document.querySelector('.dt-save-filter-btn')?.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const isDirtyComparedToDefault = api => JSON.stringify(getCurrentView(api)) !== JSON.stringify(defaultViewState || getResetBaselineState());
    const applySavedTableState = (api, state) => {
        const normalized = normalizeView(state);
        const input = document.getElementById('productScopeSearch');
        if (input) input.value = normalized.filters.search || normalized.search;
        api.search(normalized.search);
        visibleColumns.forEach(index => api.column(index).visible(normalized.colVis[index] !== false, false));
        api.colReorder?.order?.(normalized.columnOrder, true);
        api.order(normalized.order);
    };
    const loadDefaultView = async () => {
        if (!personalizationClient?.getViews) return null;
        try {
            const response = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const records = Array.isArray(response) ? response : (response?.data || response?.Data || []);
            defaultViewRecord = records.find(item => item?.isDefault || item?.IsDefault) || records[0] || null;
            const raw = defaultViewRecord?.viewDefinition ?? defaultViewRecord?.ViewDefinition;
            defaultViewState = raw ? normalizeView(typeof raw === 'string' ? JSON.parse(raw) : raw) : null;
            return defaultViewState;
        } catch { return null; }
    };
    const saveDefaultView = async api => {
        if (!personalizationClient?.saveView) return;
        const state = getCurrentView(api);
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (defaultViewRecord?.viewName || defaultViewRecord?.ViewName || L.SaveView || 'Default'),
            isDefault: true,
            visibility: 'private',
            viewDefinition: state
        };
        const id = defaultViewRecord?.id || defaultViewRecord?.Id || defaultViewRecord?._id;
        const response = id && personalizationClient.updateView
            ? await personalizationClient.updateView(id, payload)
            : await personalizationClient.saveView(payload);
        defaultViewRecord = response?.data || response?.Data || response || payload;
        defaultViewState = state;
        setSaveFilterVisible(false);
        window.showToast?.(L.Saved, 'success');
    };

    const errorMessage = async response => {
        await response.text().catch(() => '');
        return ({ 400: L.ErrorValidation, 403: L.ErrorForbidden, 404: L.ErrorNotFound, 409: L.ErrorConflict, 503: L.ErrorProviderUnavailable, 504: L.ErrorProviderTimeout })[response.status] || L.ErrorGateway;
    };
    const formatDate = input => input ? new Intl.DateTimeFormat(document.documentElement.lang || undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(input)) : (L.Unknown || '-');
    const productLabel = row => `${value(row, 'canonicalCode', 'CanonicalCode')} — ${value(row, 'globalProductName', 'GlobalProductName')}`;
    const productId = row => value(row, 'id', 'Id');
    const policyPeriod = policy => {
        const periods = value(policy, 'periods', 'Periods') || [];
        return periods.find(period => !value(period, 'effectiveToUtc', 'EffectiveToUtc')) || null;
    };
    const resolveManageMode = policy => {
        if (!policy) return canConfigure ? 'create' : null;
        return policyPeriod(policy) && canReplace ? 'replace' : null;
    };
    const canEndPolicy = policy => Boolean(policyPeriod(policy));
    const setText = (id, content) => {
        const element = document.getElementById(id);
        if (element) element.textContent = content === null || content === undefined || content === '' ? (L.Unknown || '-') : String(content);
    };

    const fetchPolicy = async id => {
        const response = await fetch(`${endpoint}/${encodeURIComponent(id)}`, { credentials: 'same-origin', headers: getAuthHeaders() });
        if (response.status === 404) return null;
        if (!response.ok) throw new Error(await errorMessage(response));
        return unwrap(await response.json());
    };
    const showDetail = async row => {
        try {
            const policy = await fetchPolicy(productId(row));
            const period = policyPeriod(policy);
            setText('oc-title', productLabel(row));
            setText('oc-policy-status', policy ? L.Configured : L.LegacyUnclassified);
            setText('oc-mode', period
                ? (Number(value(period, 'mode', 'Mode')) === 1 ? L.GroupWide : L.Scoped)
                : (policy ? L.Unknown : L.LegacyUnclassified));
            setText('oc-legal-entities', (value(period, 'legalEntityIds', 'LegalEntityIds') || []).join(', ') || '-');
            setText('oc-effective-from', formatDate(value(period, 'effectiveFromUtc', 'EffectiveFromUtc')));
            setText('oc-effective-to', formatDate(value(period, 'effectiveToUtc', 'EffectiveToUtc')));
            setText('oc-version', value(policy, 'version', 'Version'));
            bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasDetailsPreview')).show();
        } catch (error) { window.showToast?.(error.message || L.ErrorGateway, 'error'); }
    };

    const fillProduct = row => {
        const select = document.getElementById('globalProductId');
        select.innerHTML = '';
        select.add(new Option(productLabel(row), productId(row), true, true));
    };
    const loadOptions = async id => {
        const response = await fetch(`${endpoint}/${encodeURIComponent(id)}/create-options`, { credentials: 'same-origin', headers: getAuthHeaders() });
        if (!response.ok) throw new Error(await errorMessage(response));
        const options = unwrap(await response.json());
        const select = document.getElementById('legalEntityIds');
        select.innerHTML = '';
        const legalEntities = value(options, 'legalEntities', 'LegalEntities') || [];
        if (legalEntities.length > 200) throw new Error(L.ErrorGateway);
        legalEntities.forEach(item => select.add(new Option(`${value(item, 'code', 'Code')} — ${value(item, 'name', 'Name')}`, value(item, 'id', 'Id'))));
    };
    const applyLegalEntitySelection = selectedIds => {
        const selected = new Set((selectedIds || []).map(String));
        Array.from(document.getElementById('legalEntityIds')?.options || [])
            .forEach(option => { option.selected = selected.has(option.value); });
        $('#legalEntityIds').trigger('change');
    };
    const syncScopeMode = () => {
        const scoped = document.getElementById('scopeMode')?.value === '2';
        document.getElementById('legalEntityField')?.classList.toggle('d-none', !scoped);
        document.getElementById('legalEntityIds')?.toggleAttribute('required', scoped);
        if (!scoped) $('#legalEntityIds').val([]).trigger('change');
        updateRequiredProgress();
    };
    const updateRequiredProgress = () => {
        const scoped = document.getElementById('scopeMode')?.value === '2';
        const total = scoped ? 3 : 2;
        const completed = Number(Boolean(document.getElementById('globalProductId')?.value))
            + Number(Boolean(document.getElementById('scopeMode')?.value))
            + (scoped ? Number((document.getElementById('legalEntityIds')?.selectedOptions?.length || 0) > 0) : 0);
        setText('requiredProgress', `${completed}/${total}`);
    };
    const openForm = async (row, mode) => {
        writeMode = mode;
        const form = document.getElementById('formProductScope');
        form?.reset();
        fillProduct(row);
        document.getElementById('productScopeFormTitle').textContent = mode === 'replace' ? L.ReplaceTitle : L.ConfigureTitle;
        const policy = mode === 'replace' ? await fetchPolicy(productId(row)) : null;
        document.getElementById('expectedVersion').value = policy ? String(value(policy, 'version', 'Version')) : '';
        const period = policyPeriod(policy);
        document.getElementById('scopeMode').value = period ? String(value(period, 'mode', 'Mode')) : '';
        await loadOptions(productId(row));
        applyLegalEntitySelection(value(period, 'legalEntityIds', 'LegalEntityIds') || []);
        syncScopeMode();
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).show();
    };
    const managePolicy = async row => {
        const policy = await fetchPolicy(productId(row));
        const mode = resolveManageMode(policy);
        if (mode) return openForm(row, mode);
        window.showToast?.(policy ? L.ErrorConflict : L.ErrorForbidden, 'error');
    };
    const submitForm = async () => {
        const form = document.getElementById('formProductScope');
        const product = document.getElementById('globalProductId')?.value;
        const mode = document.getElementById('scopeMode')?.value;
        const ids = Array.from(document.getElementById('legalEntityIds')?.selectedOptions || []).map(option => option.value);
        if (!form || !product || !mode || mode === '2' && ids.length === 0 || ids.length > 200) {
            form?.classList.add('was-validated');
            window.showToast?.(L.ErrorValidation, 'error');
            return;
        }
        const body = new FormData();
        body.set('GlobalProductId', product);
        body.set('Mode', mode);
        if (writeMode === 'replace') body.set('ExpectedVersion', document.getElementById('expectedVersion').value);
        ids.forEach(id => body.append('LegalEntityIds', id));
        body.set('FormAttemptToken', document.getElementById('formAttemptToken').value);
        const antiForgeryToken = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        body.set('__RequestVerificationToken', antiForgeryToken);
        const response = await fetch(writeMode === 'replace' ? `${endpoint}/replace` : endpoint, {
            method: 'POST', credentials: 'same-origin', headers: { ...getAuthHeaders(), RequestVerificationToken: antiForgeryToken }, body
        });
        const payload = await response.json().catch(() => null);
        if (response.status === 202) { window.showToast?.(payload?.errors?.[0] || L.ReconciliationPending, 'warning'); return; }
        if (!response.ok) { window.showToast?.(payload?.errors?.[0] || L.ErrorGateway, 'error'); return; }
        const nextToken = payload?.formAttemptToken;
        if (nextToken) document.getElementById('formAttemptToken').value = nextToken;
        bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasCreateEdit')).hide();
        window.showToast?.(L.Saved, 'success');
    };
    const endPolicy = async row => {
        const policy = await fetchPolicy(productId(row));
        if (!policy) return window.showToast?.(L.ErrorNotFound, 'error');
        if (!canEndPolicy(policy)) return window.showToast?.(L.ErrorConflict, 'error');
        window.showConfirm?.(L.EndConfirmation, async () => {
            const form = document.getElementById('formProductScope');
            const body = new FormData();
            body.set('GlobalProductId', productId(row));
            body.set('ExpectedVersion', value(policy, 'version', 'Version'));
            body.set('FormAttemptToken', document.getElementById('formAttemptToken')?.value || '');
            const antiForgeryToken = form?.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
            body.set('__RequestVerificationToken', antiForgeryToken);
            const response = await fetch(`${endpoint}/end`, { method: 'POST', credentials: 'same-origin', headers: { ...getAuthHeaders(), RequestVerificationToken: antiForgeryToken }, body });
            const payload = await response.json().catch(() => null);
            if (response.status === 202) {
                window.showToast?.(payload?.errors?.[0] || L.ReconciliationPending, 'warning');
                return;
            }
            if (!response.ok) return window.showToast?.(payload?.errors?.[0] || payload?.Errors?.[0] || L.ErrorGateway, 'error');
            if (payload?.formAttemptToken) document.getElementById('formAttemptToken').value = payload.formAttemptToken;
            window.showToast?.(L.Saved, 'success');
        }, { type: 'warning', confirmButtonText: L.End });
    };

    const query = data => {
        const pageSize = Math.max(10, Math.min(Number(data.length) || 20, 100));
        const result = new URLSearchParams({ pageNumber: String(Math.floor((Number(data.start) || 0) / pageSize) + 1), pageSize: String(pageSize) });
        const search = data.search?.value || document.getElementById('productScopeSearch')?.value || '';
        if (search.trim()) result.set('search', search.trim());
        return result.toString();
    };
    const initTable = async () => {
        if (!tableEl || !window.DtDefaults) return;
        const saved = await loadDefaultView();
        const initial = createInitialTableState(saved);
        const buttons = {
            filterBtn: { text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>', className: 'btn btn-icon btn-label-secondary dt-filter-btn', attr: { title: L.Filter, 'aria-label': L.Filter }, action: () => bootstrap.Collapse.getOrCreateInstance(document.getElementById('inlineFilterCollapse'), { toggle: false }).toggle() },
            saveFilterBtn: { text: `<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">${escapeHtml(L.SaveView)}</span>`, className: 'btn btn-label-primary dt-save-filter-btn d-none', action: (event, api) => saveDefaultView(api || dt) }
        };
        const config = window.DtDefaults.create({
            processing: true, serverSide: true, stateSave: false, search: initial.search, order: initial.order,
            colReorder: initial.colReorder,
            ajax: (data, callback) => fetch(`${endpoint}?${query(data)}`, { credentials: 'same-origin', headers: getAuthHeaders() })
                .then(response => response.ok ? response.json() : Promise.reject(new Error(L.ErrorGateway)))
                .then(payload => { const page = unwrap(payload); callback({ data: value(page, 'items', 'Items') || [], recordsTotal: value(page, 'totalCount', 'TotalCount') || 0, recordsFiltered: value(page, 'totalCount', 'TotalCount') || 0 }); })
                .catch(error => { window.showToast?.(error.message, 'error'); callback({ data: [], recordsTotal: 0, recordsFiltered: 0 }); }),
            columns: [{ data: 'id' }, { data: 'canonicalCode' }, { data: 'globalProductName' }, { data: 'lifecycleStatus' }, { data: null }],
            columnDefs: [
                { targets: 0, className: 'control', searchable: false, orderable: false, render: () => '' },
                { targets: 1, render: data => `<span class="fw-medium text-heading">${escapeHtml(data)}</span>` },
                { targets: 2, render: escapeHtml }, { targets: 3, render: escapeHtml },
                { targets: -1, searchable: false, orderable: false, className: 'cell-fit all text-end pe-3', render: (data, type, row) => window.DitenDataTable.renderActions([
                    { key: 'details', className: 'js-quick-view', text: L.ViewDetails, icon: 'bx bx-show', attrs: { 'data-id': productId(row) } },
                    ...(canConfigure || canReplace ? [{ key: 'manage', className: 'js-scope-manage', text: L.Configure, icon: 'bx bx-link', attrs: { 'data-id': productId(row) } }] : []),
                    ...(canEnd ? [{ key: 'end', className: 'js-scope-end', text: L.End, icon: 'bx bx-stop-circle', attrs: { 'data-id': productId(row) } }] : [])
                ]) }
            ],
            buttons: window.DtDefaults.exportButtons(null, {}, buttons, { exportColumns: visibleColumns, colvisColumns: visibleColumns }),
            initComplete: function () { const api = this.api(); applySavedTableState(api, initial.state); setTimeout(() => { saveViewArmed = true; }, 0); },
            drawCallback: function () { window.DtDefaults.updateVisualState(this.api(), 0); }
        });
        dt = new DataTable(tableEl, config);
        $(tableEl).on('column-reorder.dt columns-reordered.dt search.dt order.dt column-visibility.dt', () => { if (saveViewArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt)); });
    };
    const rowFor = target => dt?.row(target.closest('tr')).data();
    const bind = () => {
        document.getElementById('scopeMode')?.addEventListener('change', syncScopeMode);
        document.getElementById('legalEntityIds')?.addEventListener('change', updateRequiredProgress);
        document.getElementById('btnSaveProductScope')?.addEventListener('click', submitForm);
        document.getElementById('btnFilterApply')?.addEventListener('click', () => { dt?.search(document.getElementById('productScopeSearch')?.value || '').draw(); if (dt) setSaveFilterVisible(isDirtyComparedToDefault(dt)); });
        document.getElementById('btnFilterReset')?.addEventListener('click', event => { event.preventDefault(); if (dt) { applySavedTableState(dt, getResetBaselineState()); dt.draw(); setSaveFilterVisible(isDirtyComparedToDefault(dt)); } });
        document.addEventListener('click', event => {
            const quickView = event.target.closest('.js-quick-view');
            const action = quickView || event.target.closest('.js-scope-manage,.js-scope-end');
            if (!action || !action.closest('.datatables-product-legal-entity-scopes')) return;
            const row = rowFor(action); if (!row) return;
            if (action.classList.contains('js-quick-view')) showDetail(row);
            else if (action.classList.contains('js-scope-manage')) void managePolicy(row).catch(error => window.showToast?.(error.message || L.ErrorGateway, 'error'));
            else void endPolicy(row).catch(error => window.showToast?.(error.message || L.ErrorGateway, 'error'));
        });
    };
    const initSelectors = () => {
        const $legalEntities = $('#legalEntityIds');
        if ($legalEntities.length && !$legalEntities.hasClass('select2-hidden-accessible')) {
            $legalEntities.select2({ dropdownParent: $(document.body), dropdownCssClass: 'dt-inline-filter-dropdown', width: 'element' });
        }
    };
    return {
        init: async () => { initSelectors(); bind(); await initTable(); },
        testHooks: { createInitialTableState, resolveManageMode, canEndPolicy, applyLegalEntitySelection }
    };
})();
document.addEventListener('DOMContentLoaded', () => ProductLegalEntityScopesList.init());
