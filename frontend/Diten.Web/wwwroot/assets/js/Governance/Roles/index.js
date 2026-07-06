/**
 * Tenant Roles — DataTables Index Script (FE-C, MOD-0018-FU9)
 * Adapted from the golden-reference Slim pattern.
 *   - Create / Edit → offcanvas (AJAX via /Roles MVC controller; name immutable on edit)
 *   - List → gateway /api/roles (DitenDataTable unwraps the Response envelope)
 *   - FE-B (window.Permissions) gates Add/Edit/Delete affordances — UX ONLY; backend
 *     [HasPermission] on AuthService is authoritative. System roles are not editable/deletable.
 */
'use strict';

const RolesList = (function () {
    let dt;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let saveFilterArmed = false;

    const dtTableEl = document.querySelector('.datatables-roles');
    const apiUrl = window.API?.auth;
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'Governance', pageKey: 'Roles' };
    const filterHostId = 'inlineFilterHost';
    const filterCollapseId = 'inlineFilterCollapse';
    const saveViewColumnIndexes = [1, 2, 3, 4, 5];
    const totalColumnCount = 7;
    const defaultVisibleColumnIndexes = [1, 2, 3, 4, 5];
    const baseOrder = [[1, 'asc']];
    let appliedFilters = { type: [] };
    let L = window.L10n || {};

    // FE-B UX permission gates (NOT enforcement).
    const can = (key) => window.Permissions?.has?.(key) === true;
    const canCreate = () => can('auth.roles.create');
    const canUpdate = () => can('auth.roles.update');
    const canDelete = () => can('auth.roles.delete');

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

    const getAuthHeaders = (includeJson = false) =>
        window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const getAntiForgeryToken = () =>
        document.querySelector('#formRole input[name="__RequestVerificationToken"]')?.value || '';

    // ─── Normalize helpers ───────────────────────────────────────────────────
    const normalizeString = (v) => (typeof v === 'string' ? v.trim() : '');
    const normalizeArray = (v) => {
        if (Array.isArray(v)) return Array.from(new Set(v.map((i) => normalizeString(String(i))).filter(Boolean)));
        const s = normalizeString(v);
        return s ? [s] : [];
    };
    const normalizeFilterValue = (value) => Array.isArray(value) ? normalizeArray(value) : normalizeString(value);
    const emptyFilters = () => ({ type: [] });
    const normalizeFilters = (filters) => ({ type: normalizeArray((filters || {}).type) });
    const hasFilterValue = (v) => Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0;

    const matchesTypeFilter = (selected, isSystem) => {
        const norm = normalizeArray(selected);
        if (!norm.length) return true;
        return norm.includes(isSystem ? 'System' : 'Custom');
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
            console.error('[Roles SaveView] Failed to load saved views.', error);
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
        if (!window.jQuery?.fn?.dataTable?.ext?.search || dtTableEl?.dataset.rolesFilterBound === '1') return;
        dtTableEl.dataset.rolesFilterBound = '1';
        $.fn.dataTable.ext.search.push((settings, _sd, dataIndex, rowData) => {
            if (settings.nTable !== dtTableEl) return true;
            const row = rowData || dt?.row(dataIndex)?.data?.() || null;
            if (!row) return true;
            return matchesTypeFilter(appliedFilters.type, row.isSystem);
        });
    };

    // ─── Select2 multi-summary (ported from golden-reference Slim) ────────────
    const syncMultiSelectSummary = ($select) => {
        const $container = $select.next('.select2-container');
        const $rendered = $container.find('.select2-selection__rendered');
        const $selection = $container.find('.select2-selection--multiple');
        if (!$container.length || !$rendered.length || !$selection.length) return;

        let $summary = $selection.find('.dt-inline-filter-multi__summary');
        let $actions = $selection.find('.dt-inline-filter-multi__actions');
        let $count = $selection.find('.dt-inline-filter-multi__count');
        let $arrow = $selection.find('.select2-selection__arrow');

        if (!$summary.length) { $summary = $('<span class="dt-inline-filter-multi__summary"></span>'); $selection.prepend($summary); }
        if (!$actions.length) { $actions = $('<span class="dt-inline-filter-multi__actions"></span>'); $selection.append($actions); }
        if (!$count.length) { $count = $('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>'); $actions.append($count); }
        if (!$arrow.length) { $arrow = $('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>'); $selection.append($arrow); }

        const placeholder = normalizeString($select.data('placeholder')) || '';
        const selectedValues = normalizeArray($select.val());
        const selectedTexts = ($select.select2('data') || []).map((i) => normalizeString(i.text)).filter(Boolean);

        $summary.text(placeholder);
        $rendered.attr('title', selectedTexts.join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', selectedValues.length > 0);
        $count.toggleClass('d-none', selectedValues.length === 0).text(String(selectedValues.length));

        $actions.find('.dt-multi-clear-btn').remove();
        if (selectedValues.length > 0) {
            const $clearBtn = $('<span class="dt-multi-clear-btn" role="button" aria-label="' + (L.Reset || '') + '" title="' + (L.Reset || '') + '">&times;</span>');
            $clearBtn.on('mousedown', (e) => { e.preventDefault(); e.stopPropagation(); $select.val(null).trigger('change'); });
            $actions.append($clearBtn);
        }
    };

    const initSelect2Filters = () => {
        if (!window.jQuery || !$.fn.select2) return;
        const $body = $(document.body);

        const clampDropdown = () => {
            requestAnimationFrame(() => {
                const dd = document.querySelector('.select2-dropdown.dt-inline-filter-dropdown');
                if (!dd) return;
                const rect = dd.getBoundingClientRect();
                const pad = 8;
                let dx = 0, dy = 0;
                if (rect.right > window.innerWidth - pad) dx -= rect.right - (window.innerWidth - pad);
                if (rect.left < pad) dx += pad - rect.left;
                if (rect.bottom > window.innerHeight - pad) dy -= rect.bottom - (window.innerHeight - pad);
                if (rect.top < pad) dy += pad - rect.top;
                if (!dx && !dy) return;
                const cs = window.getComputedStyle(dd);
                const baseLeft = parseFloat(cs.left) || rect.left + window.scrollX;
                const baseTop = parseFloat(cs.top) || rect.top + window.scrollY;
                if (dx) dd.style.left = `${baseLeft + dx}px`;
                if (dy) dd.style.top = `${baseTop + dy}px`;
                dd.style.transform = 'none';
            });
        };

        $('#filterType').each(function () {
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
            $s.on('select2:open', clampDropdown);
            $s.on('change.select2-summary', function () { syncMultiSelectSummary($s); });
            requestAnimationFrame(() => syncMultiSelectSummary($s));
        });
    };
    const syncFilterControls = (values) => {
        $('#filterType').val(normalizeArray(values.type)).trigger('change');
    };
    const getAppliedFilterCount = () => [appliedFilters.type].filter(hasFilterValue).length;

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
            appliedFilters = { type: $('#filterType').val() || [] };
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

    // ─── KPI cards ──────────────────────────────────────────────────────────
    // Live counts from the full loaded dataset (unaffected by search/filter — rows().data() returns all rows).
    const setKpi = (id, value) => { const el = document.getElementById(id); if (el) el.textContent = value; };
    const updateKpis = (api) => {
        const rows = api.rows().data().toArray();
        const total = rows.length;
        const system = rows.filter((r) => r && r.isSystem).length;
        const users = rows.reduce((sum, r) => sum + (Number(r && r.userCount) || 0), 0);
        setKpi('kpi-roles-total', total);
        setKpi('kpi-roles-system', system);
        setKpi('kpi-roles-custom', total - system);
        setKpi('kpi-roles-users', users);
    };

    // ─── Type badge ─────────────────────────────────────────────────────────
    const getTypeBadge = (isSystem) => isSystem
        ? `<span class="badge bg-label-primary">${L.RoleTypeSystem || 'System'}</span>`
        : `<span class="badge bg-label-secondary">${L.RoleTypeCustom || 'Custom'}</span>`;

    // ─── Permission module chips ──────────────────────────────────────────────
    // One info chip per module (Module N). Uses the enriched list response
    // (data.modulePermissions) — never a per-row fetch.
    //   • table  (collapse:true)  → single nowrap line so the row height never
    //                               grows; overflow folds into a "+N" badge that
    //                               reveals the rest in a click dropdown.
    //   • offcanvas (collapse:false) → wraps and shows every module (room to spare).
    const escapeChip = (v) => String(v ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    const MODULE_CHIP_MAX = 3;
    const moduleChip = ([m, c]) => `<span class="badge bg-label-info">${escapeChip(m)} ${escapeChip(c)}</span>`;
    const renderModuleChips = (modulePermissions, total, options) => {
        const collapse = options?.collapse === true;
        const max = options?.max || MODULE_CHIP_MAX;
        const entries = modulePermissions && typeof modulePermissions === 'object'
            ? Object.entries(modulePermissions).filter(([, c]) => Number(c) > 0)
            : [];
        const totalText = total != null ? String(total) : '0';
        if (!entries.length) return `<span class="badge bg-label-info" title="${totalText}">${totalText}</span>`;

        const tooltip = escapeChip(entries.map(([m, c]) => `${m}: ${c}`).join(', '));

        if (!collapse) {
            // Offcanvas: wrap and show all modules.
            return `<span class="d-inline-flex flex-wrap gap-1" title="${tooltip}">${entries.map(moduleChip).join('')}</span>`;
        }

        // Table cell: one line only; fold the overflow into a click dropdown.
        const shown = entries.slice(0, max);
        const rest = entries.slice(max);
        let html = `<span class="d-inline-flex flex-nowrap align-items-center gap-1" title="${tooltip}">`;
        html += shown.map(moduleChip).join('');
        if (rest.length) {
            const menu = rest.map((e) => `<span class="d-block px-2 py-1">${moduleChip(e)}</span>`).join('');
            html += '<span class="dropdown d-inline-block">'
                + `<a href="javascript:;" class="badge bg-label-secondary text-decoration-none" data-bs-toggle="dropdown" aria-expanded="false" title="${tooltip}">+${rest.length}</a>`
                + `<span class="dropdown-menu p-1">${menu}</span>`
                + '</span>';
        }
        html += '</span>';
        return html;
    };

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
        const setText = (id, value) => { const el = document.getElementById(id); if (el) { el.innerText = value; el.setAttribute('title', value); } };
        document.getElementById('oc-title').innerText = data.displayName || data.name || '-';
        document.getElementById('oc-subtitle').innerText = data.name || '-';
        setText('oc-name', data.name || '-');
        setText('oc-displayname', data.displayName || '-');
        const permsEl = document.getElementById('oc-permissions');
        if (permsEl) permsEl.innerHTML = renderModuleChips(data.modulePermissions, data.permissionCount);
        document.getElementById('oc-usercount').innerText = data.userCount != null ? String(data.userCount) : '-';
        document.getElementById('oc-desc').innerText = data.description || '-';
        const typeEl = document.getElementById('oc-type');
        if (typeEl) { typeEl.className = `badge ${data.isSystem ? 'bg-label-primary' : 'bg-label-secondary'}`; typeEl.innerText = data.isSystem ? (L.RoleTypeSystem || 'System') : (L.RoleTypeCustom || 'Custom'); }
        const editBtn = document.getElementById('oc-btn-edit');
        if (editBtn) {
            editBtn.dataset.editId = data.id;
            // UX: editing a system role or lacking permission hides the edit affordance.
            editBtn.classList.toggle('d-none', !!data.isSystem || !canUpdate());
        }
    };

    // ─── Create/Edit offcanvas ────────────────────────────────────────────────
    const setNameImmutable = (immutable) => {
        const nameEl = document.getElementById('roleName');
        const help = document.getElementById('roleNameHelp');
        if (nameEl) { nameEl.readOnly = immutable; nameEl.classList.toggle('bg-label-secondary', immutable); }
        if (help) help.classList.toggle('d-none', !immutable);
    };
    const resetCreateEditForm = () => {
        const form = document.getElementById('formRole');
        if (!form) return;
        form.classList.remove('was-validated');
        form.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        document.getElementById('roleItemId').value = '';
        document.getElementById('roleName').value = '';
        document.getElementById('roleDisplayName').value = '';
        document.getElementById('roleDescription').value = '';
        document.getElementById('formRoleAlert').classList.add('d-none');
    };
    const openCreateOffcanvas = () => {
        editingId = null;
        resetCreateEditForm();
        setNameImmutable(false);
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.FormTitleCreate || L.AddNew || '';
        const saveBtn = document.getElementById('btnSaveRole');
        if (saveBtn) saveBtn.textContent = L.Save || '';
        getOcCreateEditInstance()?.show();
    };
    const openEditOffcanvas = async (id) => {
        if (!id) return;
        editingId = id;
        resetCreateEditForm();
        setNameImmutable(true); // name is immutable once created
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = L.FormTitleEdit || L.EditItem || L.Edit || '';
        const saveBtn = document.getElementById('btnSaveRole');
        if (saveBtn) saveBtn.textContent = L.Update || L.Save || '';
        try {
            const res = await fetch(`/Roles/get/${id}`, { credentials: 'same-origin', headers: getAuthHeaders() });
            const json = await res.json();
            if (!json.success || !json.data) throw new Error('Failed to load role.');
            const d = json.data;
            document.getElementById('roleItemId').value = d.id || '';
            document.getElementById('roleName').value = d.name || '';
            document.getElementById('roleDisplayName').value = d.displayName || '';
            document.getElementById('roleDescription').value = d.description || '';
        } catch (error) {
            console.error('[Roles] Failed to load role for edit.', error);
            window.showToast?.(L.ErrorOccurred, 'error');
            return;
        }
        getOcCreateEditInstance()?.show();
    };
    const showFormErrors = (errors) => {
        const alertEl = document.getElementById('formRoleAlert');
        if (!alertEl) return;
        alertEl.innerHTML = Array.isArray(errors) ? errors.map((e) => `<div>${e}</div>`).join('') : (errors || L.FormValidationError || '');
        alertEl.classList.remove('d-none');
    };
    const submitCreateEditForm = async () => {
        const form = document.getElementById('formRole');
        if (!form) return;
        form.classList.add('was-validated');
        if (!form.checkValidity()) { showFormErrors([L.FormValidationError || '']); return; }

        const formData = new FormData(form);
        const isEdit = !!editingId;
        const url = isEdit ? `/Roles/edit/${editingId}` : '/Roles/create';
        const saveBtn = document.getElementById('btnSaveRole');
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
            console.error('[Roles] Form submit failed.', error);
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

    // ─── Events ───────────────────────────────────────────────────────────────
    const bindEvents = () => {
        document.addEventListener('click', (e) => {
            const quickViewBtn = e.target.closest('.js-quick-view');
            const editBtn = e.target.closest('.js-edit-item');
            const deleteBtn = e.target.closest('.delete-record');
            const actionEl = quickViewBtn || editBtn || deleteBtn;
            if (!actionEl) return;
            const inTable = !!actionEl.closest('.datatables-roles');
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
                    const res = await fetch(`${apiUrl}/api/roles/${data.id}`, { method: 'DELETE', credentials: 'include', headers: getAuthHeaders() });
                    if (!res.ok) throw new Error('Delete failed.');
                    reloadWithSuccessToast('RecordDeleted');
                } catch (error) {
                    console.error(error);
                    window.showToast?.(L.ErrorOccurred, 'error');
                }
            }, { entityName: data.displayName || data.name, type: 'danger', confirmButtonText: L.Delete });
        });

        document.getElementById('btnSaveRole')?.addEventListener('click', submitCreateEditForm);
        document.getElementById('oc-btn-edit')?.addEventListener('click', () => {
            const id = document.getElementById('oc-btn-edit')?.dataset.editId;
            if (id) openEditOffcanvas(id);
        });
        document.getElementById('offcanvasCreateEdit')?.addEventListener('hidden.bs.offcanvas', restoreResponsiveModalAfterCancel);
        document.getElementById('offcanvasDetailsPreview')?.addEventListener('hidden.bs.offcanvas', restoreResponsiveModalAfterCancel);
    };

    // ─── DataTable init ───────────────────────────────────────────────────────
    const initDataTable = async () => {
        if (!dtTableEl) return;
        if (!apiUrl) { console.error('[Roles] window.API.auth is required.'); return; }

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
                        console.error('[Roles SaveView] Failed to save default view.', error);
                        window.showToast?.(L.ErrorOccurred || '', 'error');
                    }
                }
            }
        };

        dt = window.DitenDataTable.createCrudTable({
            tableEl: dtTableEl,
            bulk: bulkOptions,
            ajax: { url: apiUrl + '/api/roles', type: 'GET', xhrFields: { withCredentials: true } },
            config: {
                stateSave: false,
                colReorder: { columns: ':gt(0):not(:last-child)' },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'name', name: 'name' },
                    { data: 'displayName', name: 'displayName' },
                    { data: 'isSystem', name: 'isSystem' },
                    { data: 'permissionCount', name: 'permissionCount' },
                    { data: 'userCount', name: 'userCount' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    { targets: 3, render: (data, type) => type === 'display' ? getTypeBadge(!!data) : (data ? 'System' : 'Custom') },
                    { targets: 4, className: 'text-start', render: (data, type, full) => type === 'display' ? renderModuleChips(full.modulePermissions, data, { collapse: true }) : (data ?? 0) },
                    { targets: 5, className: 'text-center', render: (data, type) => type === 'display' ? `<span class="badge bg-label-secondary">${data ?? 0}</span>` : (data ?? 0) },
                    {
                        targets: -1,
                        title: L.Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => {
                            const rowJson = JSON.stringify(full).replace(/'/g, "&#39;");
                            const actions = [];
                            // View: primary (visible) icon — mirrors the GoldenReference action set.
                            actions.push({
                                className: 'js-quick-view me-1', icon: 'bx bx-show',
                                attrs: { 'data-bs-toggle': 'offcanvas', 'data-bs-target': '#offcanvasDetailsPreview', 'data-json': rowJson, 'title': L.QuickView }
                            });
                            // Edit: requires permission and not a system role — falls into the kebab (with icon).
                            if (canUpdate() && !full.isSystem) {
                                actions.push({ className: 'js-edit-item', icon: 'bx bx-edit', text: L.Edit, attrs: { 'data-id': full.id, 'data-json': rowJson } });
                            }
                            // Delete: requires permission and not a system role — falls into the kebab (with icon).
                            if (canDelete() && !full.isSystem) {
                                actions.push({ className: 'delete-record text-danger', icon: 'bx bx-trash', text: L.Delete, attrs: { 'data-json': rowJson } });
                            }
                            let html = window.DitenDataTable.renderActions(actions);
                            // Manage Permissions — opens the Role-Permission screen pre-filtered to this role.
                            const manageTitle = L.ManagePermissions || '';
                            const manageBtn = `<a href="/RoleAssignments?roleId=${full.id}" class="btn btn-icon me-1 js-manage-perms" title="${manageTitle}" aria-label="${manageTitle}"><i class="bx bx-key icon-md"></i></a>`;
                            html = html.replace('<div class="d-flex align-items-center">', '<div class="d-flex align-items-center">' + manageBtn);
                            // System roles are locked (no edit/delete) — surface a lock indicator beside View.
                            if (full.isSystem) {
                                const lockTitle = L.SystemRoleLocked || '';
                                const lock = `<span class="btn btn-icon text-muted pe-none" tabindex="-1" title="${lockTitle}" aria-label="${lockTitle}"><i class="bx bx-lock-alt icon-md"></i></span>`;
                                html = html.replace('</div>', lock + '</div>');
                            }
                            return html;
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    L.AddNew, {}, extraButtons,
                    { exportColumns: [1, 2, 3, 4, 5], colvisColumns: [1, 2, 3, 4, 5] }
                ),
                initComplete: function () {
                    mountInlineFilter();
                    bindInlineFilterA11y();
                    void setupFilters(this.api());
                    const addBtn = document.querySelector('.add-new');
                    if (addBtn) {
                        // FE-B UX gate: hide "Add New" when the user cannot create roles.
                        if (!canCreate()) addBtn.classList.add('d-none');
                        addBtn.addEventListener('click', (e) => { e.preventDefault(); openCreateOffcanvas(); });
                    }
                    setTimeout(() => { saveFilterArmed = true; }, 0);
                },
                drawCallback: function () {
                    window.DtDefaults.updateVisualState(this.api(), getAppliedFilterCount());
                    updateKpis(this.api());
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

document.addEventListener('DOMContentLoaded', () => RolesList.init());
