/**
 * Tenant Users — DataTables Index Script (FE-C 3/3, MOD-0018-FU9)
 * Golden-reference Slim adaptation.
 *   - Create (email/password/firstName/lastName) / Edit (firstName/lastName/isActive; email immutable,
 *     no password) → offcanvas via /Users MVC controller
 *   - List → gateway /api/users (paginated envelope → custom dataSrc extracts items)
 *   - FE-B (window.Permissions) gates Add/Edit/Delete — UX ONLY; backend authoritative.
 */
'use strict';

const UsersList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-users');
    const apiUrl = window.API?.auth;
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Governance', pageKey: 'Users' };
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [2, 3, 4, 5];
    const totalColumnCount = 7;
    const defaultVisibleColumnIndexes = [2, 3, 4, 5];
    const baseOrder = [[2, 'asc']];
    let appliedFilters = { status: [] };
    let L = window.L10n || {};

    const can = (key) => window.Permissions?.has?.(key) === true;
    const canCreate = () => can('auth.users.create');
    const canUpdate = () => can('auth.users.update');
    const canDelete = () => can('auth.users.delete');

    let editingId = null;
    let responsiveReturnModalEl = null;
    let suppressResponsiveReturn = false;

    const getOcCreateEditInstance = () => {
        const el = document.getElementById('offcanvasCreateEdit');
        return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    };
    const getOcDetailsInstance = () => {
        const el = document.getElementById('offcanvasDetailsPreview');
        return el ? bootstrap.Offcanvas.getOrCreateInstance(el) : null;
    };

    const syncL10n = () => {
        const current = window.L10n;
        if (current && typeof current === 'object' && Object.keys(current).length) L = current;
    };
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const getAntiForgeryToken = () =>
        document.querySelector('#formUser input[name="__RequestVerificationToken"]')?.value || '';

    // ─── Normalize helpers ───────────────────────────────────────────────────
    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const normalizeFilterValue = (value) => Array.isArray(value) ? normalizeArray(value) : normalizeString(value);
    const emptyFilters = () => ({ status: [] });
    const normalizeFilters = (filters) => ({ status: normalizeArray((filters || {}).status) });
    const hasFilterValue = (v) => Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0;
    const matchesStatusFilter = (selected, isActive) => {
        const norm = normalizeArray(selected);
        if (!norm.length) return true;
        return norm.includes(isActive ? 'Active' : 'Passive');
    };

    // ─── Column visibility / order helpers ──────────────────────────────────
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
        filters: Object.assign({}, appliedFilters),
        search: normalizeString(getSearchVal(api) || api.search()),
        colVis: captureColVis(api),
        columnOrder: captureColOrder(api),
        order: api.order()
    });
    const serializeView = (v) => JSON.stringify({
        filters: Object.keys(v?.filters || {}).sort().reduce((acc, key) => { acc[key] = normalizeFilterValue(v.filters[key]); return acc; }, {}),
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
        filters: emptyFilters(), search: '', colVis: defaultColVis(),
        columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i), order: baseOrder
    });

    const setSaveFilterVisible = (visible) => {
        const btn = document.querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !visible);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const isDirtyComparedToDefault = (api) => {
        const baseline = defaultViewState || {
            filters: emptyFilters(), search: '', colVis: defaultColVis(),
            columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i), order: baseOrder
        };
        return serializeView(getCurrentView(api)) !== serializeView(baseline);
    };

    const loadDefaultView = async () => {
        defaultViewRecord = null; defaultViewState = null;
        if (!personalizationClient?.getViews) return null;
        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = Array.isArray(items) ? (items.find(isSavedViewDefault) || items[0] || null) : null;
            defaultViewState = defaultViewRecord ? mapSavedViewToState(defaultViewRecord) : null;
            return defaultViewState;
        } catch (error) {
            if (error?.authHandled) return null;
            console.error('[Users SaveView] Failed to load saved views.', error);
            return null;
        }
    };
    const saveDefaultView = async (view) => {
        if (!personalizationClient?.saveView) return null;
        const normalizedView = normalizeViewState(view);
        const payload = {
            moduleKey: personalizationContext.moduleKey,
            pageKey: personalizationContext.pageKey,
            viewName: (getSavedViewName(defaultViewRecord) || L.SaveView || 'Default').trim(),
            viewDefinition: normalizedView,
            isDefault: true,
            visibility: 'private'
        };
        const existingId = getSavedViewId(defaultViewRecord);
        const savedResponse = existingId
            ? await personalizationClient.updateView(existingId, payload)
            : await personalizationClient.saveView(payload);
        const savedRecord = unwrapViewResponse(savedResponse);
        defaultViewRecord = savedRecord && typeof savedRecord === 'object' ? savedRecord : Object.assign({}, defaultViewRecord || {}, payload);
        defaultViewState = normalizedView;
        return defaultViewState;
    };

    // ─── Inline filter UI ────────────────────────────────────────────────────
    const mountInlineFilter = () => {
        const host = document.getElementById(filterHostId);
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) {
            toolbarRow.insertAdjacentElement('afterend', host);
            host.classList.remove('px-6');
            host.classList.add('px-3');
        }
    };
    const toggleInlineFilter = () => {
        const collapseEl = document.getElementById(filterCollapseId);
        if (!collapseEl) return;
        bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).toggle();
    };
    const bindInlineFilterA11y = () => {
        const btn = document.querySelector('.dt-filter-btn');
        const collapseEl = document.getElementById(filterCollapseId);
        if (!btn || !collapseEl || btn.dataset.bound) return;
        btn.dataset.bound = '1';
        collapseEl.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
        collapseEl.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));
    };
    const registerTableFilters = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl?.dataset.usersFilterBound === '1') return;
        dtTableEl.dataset.usersFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _sd, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) return true;
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) return true;
            return matchesStatusFilter(appliedFilters.status, row.isActive);
        });
    };

    const initSelect2Filters = () => {
        if (!window.jQuery || !$.fn.select2) return;
        const $body = $(document.body);
        $('#filterStatus').each(function () {
            const $s = $(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({
                dropdownParent: $body,
                dropdownCssClass: 'dt-inline-filter-dropdown',
                containerCssClass: 'dt-inline-filter-multi',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $s.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element',
                closeOnSelect: false
            });
        });
    };
    const syncFilterControls = (values) => { $('#filterStatus').val(normalizeArray(values.status)).trigger('change'); };
    const getAppliedFilterCount = () => [appliedFilters.status].filter(hasFilterValue).length;

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

    const setupFilters = async (api) => {
        initSelect2Filters();
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });

        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = { status: $('#filterStatus').val() || [] };
            api.draw();
            window.DtDefaults.updateVisualState(api, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
            const collapseEl = document.getElementById(filterCollapseId);
            if (collapseEl) bootstrap.Collapse.getOrCreateInstance(collapseEl, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', (e) => {
            e.preventDefault();
            applySavedTableState(api, getResetBaselineState());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
        });
    };

    const getStatusMap = () => ({
        true: { title: L.Active, class: 'bg-label-success' },
        false: { title: L.Passive, class: 'bg-label-secondary' }
    });

    // ─── Quick View ──────────────────────────────────────────────────────────
    const tryParseRowJson = (el) => {
        if (!el) return null;
        const raw = el.getAttribute('data-json');
        if (!raw) return null;
        try { return JSON.parse(raw.replace(/&#39;/g, "'")); } catch (e) { return null; }
    };
    const closeResponsiveModal = (returnOnOffcanvasClose = false) => {
        const modalEl = document.querySelector('.modal.dtr-bs-modal.show');
        if (!modalEl) return false;
        if (returnOnOffcanvasClose) { responsiveReturnModalEl = modalEl; suppressResponsiveReturn = false; }
        const modal = bootstrap.Modal.getInstance(modalEl);
        if (modal) modal.hide(); else modalEl.querySelector('[data-bs-dismiss="modal"], .btn-close')?.click();
        return true;
    };
    const restoreResponsiveModalAfterCancel = () => {
        if (!responsiveReturnModalEl || suppressResponsiveReturn) { responsiveReturnModalEl = null; suppressResponsiveReturn = false; return; }
        const modalEl = responsiveReturnModalEl;
        responsiveReturnModalEl = null;
        window.setTimeout(() => bootstrap.Modal.getOrCreateInstance(modalEl).show(), 120);
    };
    const populateDetailsOffcanvas = (data) => {
        if (!data) return;
        const fullName = [data.firstName, data.lastName].filter(Boolean).join(' ') || data.email || '-';
        document.getElementById('oc-title').innerText = fullName;
        document.getElementById('oc-subtitle').innerText = data.email || '-';
        document.getElementById('oc-email').innerText = data.email || '-';
        document.getElementById('oc-firstname').innerText = data.firstName || '-';
        document.getElementById('oc-lastname').innerText = data.lastName || '-';

        const rolesEl = document.getElementById('oc-roles');
        const roles = Array.isArray(data.roles) ? data.roles : [];
        rolesEl.innerHTML = roles.length
            ? roles.map((r) => `<span class="badge bg-label-primary">${r}</span>`).join(' ')
            : '-';

        const statusEl = document.getElementById('oc-status');
        const status = getStatusMap()[String(!!data.isActive)] || { title: L.Unknown, class: 'bg-label-primary' };
        statusEl.className = `badge ${status.class}`;
        statusEl.innerText = status.title || '-';

        const editBtn = document.getElementById('oc-btn-edit');
        if (editBtn) {
            editBtn.dataset.editId = data.id;
            editBtn.classList.toggle('d-none', !canUpdate());
        }
    };

    // ─── Create/Edit offcanvas ────────────────────────────────────────────────
    const setCreateMode = (isCreate) => {
        document.getElementById('userPasswordRow')?.classList.toggle('d-none', !isCreate);
        document.getElementById('userActiveRow')?.classList.toggle('d-none', isCreate);
        const pwd = document.getElementById('userPassword');
        if (pwd) pwd.required = isCreate;
        const emailEl = document.getElementById('userEmail');
        const emailHelp = document.getElementById('userEmailHelp');
        if (emailEl) emailEl.readOnly = !isCreate; // email immutable on edit
        if (emailHelp) emailHelp.classList.toggle('d-none', isCreate);
    };
    const resetCreateEditForm = () => {
        const form = document.getElementById('formUser');
        if (!form) return;
        form.classList.remove('was-validated');
        form.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        document.getElementById('userItemId').value = '';
        document.getElementById('userEmail').value = '';
        document.getElementById('userPassword').value = '';
        document.getElementById('userFirstName').value = '';
        document.getElementById('userLastName').value = '';
        document.getElementById('userIsActive').checked = true;
        document.getElementById('formUserAlert').classList.add('d-none');
    };
    const openCreateOffcanvas = () => {
        editingId = null;
        resetCreateEditForm();
        setCreateMode(true);
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.FormTitleCreate || L.AddNew || '';
        const saveBtn = document.getElementById('btnSaveUser');
        if (saveBtn) saveBtn.textContent = L.Save || '';
        getOcCreateEditInstance()?.show();
    };
    const openEditOffcanvas = async (id) => {
        if (!id) return;
        editingId = id;
        resetCreateEditForm();
        setCreateMode(false);
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.FormTitleEdit || L.EditItem || L.Edit || '';
        const saveBtn = document.getElementById('btnSaveUser');
        if (saveBtn) saveBtn.textContent = L.Update || L.Save || '';
        try {
            const res = await fetch(`/Users/get/${id}`, { credentials: 'same-origin', headers: getAuthHeaders() });
            const json = await res.json();
            if (!json.success || !json.data) throw new Error('Failed to load user.');
            const d = json.data;
            document.getElementById('userItemId').value = d.id || '';
            document.getElementById('userEmail').value = d.email || '';
            document.getElementById('userFirstName').value = d.firstName || '';
            document.getElementById('userLastName').value = d.lastName || '';
            document.getElementById('userIsActive').checked = !!d.isActive;
        } catch (error) {
            console.error('[Users] Failed to load user for edit.', error);
            window.showToast?.(L.ErrorOccurred, 'error');
            return;
        }
        getOcCreateEditInstance()?.show();
    };
    const showFormErrors = (errors) => {
        const alertEl = document.getElementById('formUserAlert');
        if (!alertEl) return;
        alertEl.innerHTML = Array.isArray(errors) ? errors.map((e) => `<div>${e}</div>`).join('') : (errors || L.FormValidationError || '');
        alertEl.classList.remove('d-none');
    };
    const submitCreateEditForm = async () => {
        const form = document.getElementById('formUser');
        if (!form) return;
        form.classList.add('was-validated');
        if (!form.checkValidity()) { showFormErrors([L.FormValidationError || '']); return; }

        const formData = new FormData(form);
        const isEdit = !!editingId;
        const url = isEdit ? `/Users/edit/${editingId}` : '/Users/create';
        const saveBtn = document.getElementById('btnSaveUser');
        if (saveBtn) saveBtn.disabled = true;
        try {
            const res = await fetch(url, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'RequestVerificationToken': getAntiForgeryToken(), ...getAuthHeaders() },
                body: formData
            });
            const json = await res.json();
            if (json.success) {
                suppressResponsiveReturn = true;
                responsiveReturnModalEl = null;
                getOcCreateEditInstance()?.hide();
                reloadWithSuccessToast(isEdit ? 'RecordUpdated' : 'RecordCreated');
            } else {
                showFormErrors(json.errors);
            }
        } catch (error) {
            console.error('[Users] Form submit failed.', error);
            showFormErrors([L.ErrorOccurred]);
        } finally {
            if (saveBtn) saveBtn.disabled = false;
        }
    };

    const bulkOptions = {
        bulkBarSelector: '#bulkActionBar',
        bulkCountSelector: '#bulkSelectedCount',
        checkboxSelector: '.dt-checkboxes',
        clearSelectionSelector: '#btnClearSelection',
        selectAllSelector: '.dt-checkboxes-select-all'
    };
    const reloadWithSuccessToast = (messageKey, interpolationValue) =>
        window.DitenDataTable.reloadWithToast(dt, dtTableEl, messageKey, interpolationValue, bulkOptions);

    const bindEvents = () => {
        document.addEventListener('click', (e) => {
            const quickViewBtn = e.target.closest('.js-quick-view');
            const editBtn = e.target.closest('.js-edit-item');
            const deleteBtn = e.target.closest('.delete-record');
            const actionEl = quickViewBtn || editBtn || deleteBtn;
            if (!actionEl) return;
            const inTable = !!actionEl.closest('.datatables-users');
            const inResponsiveModal = !!actionEl.closest('.modal.dtr-bs-modal');
            if (!inTable && !inResponsiveModal) return;

            if (quickViewBtn) {
                e.preventDefault(); e.stopPropagation();
                const data = tryParseRowJson(quickViewBtn);
                if (!data) return;
                populateDetailsOffcanvas(data);
                const wasModalOpen = closeResponsiveModal(inResponsiveModal);
                window.setTimeout(() => getOcDetailsInstance()?.show(), wasModalOpen ? 160 : 0);
                return;
            }
            if (editBtn) {
                e.preventDefault(); e.stopPropagation();
                const id = editBtn.dataset.id;
                const wasModalOpen = closeResponsiveModal(inResponsiveModal);
                if (id) window.setTimeout(() => openEditOffcanvas(String(id)), wasModalOpen ? 160 : 0);
                return;
            }
            if (!deleteBtn) return;
            e.preventDefault(); e.stopPropagation();
            let data = tryParseRowJson(deleteBtn);
            if (!data && inTable) {
                let rowEl = deleteBtn.closest('tr');
                if (rowEl?.classList.contains('child')) rowEl = rowEl.previousElementSibling;
                data = rowEl ? dt.row(rowEl).data() : null;
            }
            if (!data?.id) return;
            window.showConfirm?.(L.AreYouSure, async () => {
                try {
                    const res = await fetch(`${apiUrl}/api/users/${data.id}`, { method: 'DELETE', credentials: 'include', headers: getAuthHeaders() });
                    if (!res.ok) throw new Error('Delete failed.');
                    reloadWithSuccessToast('RecordDeleted');
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred, 'error');
                }
            }, { entityName: data.email, type: 'danger', confirmButtonText: L.Delete });
        });

        document.getElementById('btnSaveUser')?.addEventListener('click', submitCreateEditForm);
        document.getElementById('oc-btn-edit')?.addEventListener('click', () => {
            const id = document.getElementById('oc-btn-edit')?.dataset.editId;
            if (id) openEditOffcanvas(id);
        });
        document.getElementById('offcanvasCreateEdit')?.addEventListener('hidden.bs.offcanvas', restoreResponsiveModalAfterCancel);
        document.getElementById('offcanvasDetailsPreview')?.addEventListener('hidden.bs.offcanvas', restoreResponsiveModalAfterCancel);
    };

    const initDataTable = async () => {
        if (!dtTableEl) return;
        if (!apiUrl) { console.error('[Users] window.API.auth is required.'); return; }

        syncL10n();
        await loadDefaultView();

        const extraButtons = {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => toggleInlineFilter()
            },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn',
                attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (e, api) {
                    const tableApi = api || dt;
                    if (!tableApi) return;
                    try {
                        await saveDefaultView(getCurrentView(tableApi));
                        setSaveFilterVisible(false);
                        window.showToast?.(L.RecordSaved || L.SaveView || '', 'success');
                    } catch (error) {
                        if (error?.authHandled) return;
                        console.error('[Users SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            bulk: bulkOptions,
            ajax: {
                url: apiUrl + '/api/users?page=1&pageSize=1000',
                type: 'GET',
                xhrFields: { withCredentials: true },
                // /api/users returns a paginated envelope { items, totalCount, ... } (no Response wrapper).
                dataSrc: (json) => (json && Array.isArray(json.items)) ? json.items : (json?.data?.items || [])
            },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(1):not(:last-child)' },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'id', name: 'checkbox' },
                    { data: 'email', name: 'email' },
                    { data: 'firstName', name: 'firstName' },
                    { data: 'lastName', name: 'lastName' },
                    { data: 'isActive', name: 'isActive' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">` },
                    { targets: 2, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    {
                        targets: 5,
                        render: (data, type) => type === 'display'
                            ? window.DitenDataTable.renderStatusBadge(data, getStatusMap())
                            : (getStatusMap()[String(!!data)] || { title: L.Unknown }).title
                    },
                    {
                        targets: -1,
                        title: L.Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => {
                            const rowJson = JSON.stringify(full).replace(/'/g, "&#39;");
                            const actions = [];
                            if (canDelete()) {
                                actions.push({ className: 'delete-record text-danger me-1', icon: '<i class="bx bx-trash icon-md"></i>', attrs: { 'data-json': rowJson } });
                            }
                            actions.push({
                                className: 'js-quick-view', text: L.QuickView,
                                attrs: { 'data-bs-toggle': 'offcanvas', 'data-bs-target': '#offcanvasDetailsPreview', 'data-json': rowJson }
                            });
                            if (canUpdate()) {
                                actions.push({ className: 'js-edit-item', text: L.Edit, attrs: { 'data-id': full.id, 'data-json': rowJson } });
                            }
                            return window.DitenDataTable.renderActions(actions);
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    L.AddNew, {}, extraButtons,
                    { exportColumns: [2, 3, 4, 5], colvisColumns: [2, 3, 4, 5] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    void setupFilters(this.api());
                    const addBtn = document.querySelector('.add-new');
                    if (addBtn) {
                        if (!canCreate()) addBtn.classList.add('d-none');
                        addBtn.addEventListener('click', (e) => { e.preventDefault(); openCreateOffcanvas(); });
                    }
                    setTimeout(() => { saveFilterArmed = true; }, 0);
                },
                drawCallback: function () {
                    window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
                }
            }
        });

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
    };

    return {
        init: function () {
            registerTableFilters();
            initDataTable();
            bindEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => UsersList.init());
