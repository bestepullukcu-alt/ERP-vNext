/**
 * Tenant Users — DataTables Index Script (FE-C 3/3, MOD-0018-FU9)
 * Golden-reference Slim adaptation.
 *   - Create (email/password/firstName/lastName) / Edit (firstName/lastName/isActive; email immutable,
 *     no password) → offcanvas via /Users MVC controller
 *   - List → gateway /api/users (paginated envelope → custom dataSrc extracts items)
 *   - FE-B (window.Permissions) gates Add/Edit/Delete — UX ONLY; backend authoritative.
 *   - WP-INFRA-AUTH-ACCOUNT-KIND-01: "Account type" column + filter (Unknown/Human/Service, enum NAMES as
 *     strings), quick-view badge, and "Change type" for holders of auth.users.account-kind.manage only
 *     (explicit-grant-only key). The change POSTs to the same-origin /Users/api proxy, never a service port.
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
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6];
    const totalColumnCount = 8;
    const defaultVisibleColumnIndexes = [1, 2, 3, 4, 5, 6];
    const baseOrder = [[1, 'asc']];
    let appliedFilters = { status: [], roles: [], accountKinds: [] };
    let L = window.L10n || {};

    const can = (key) => window.Permissions?.has?.(key) === true;
    const canCreate = () => can('auth.users.create');
    const canUpdate = () => can('auth.users.update');
    const canDelete = () => can('auth.users.delete');
    // Explicit-grant-only key (owner decision 2026-09-11): never in any default role; UX gate only, the backend
    // [HasPermission] on POST /api/users/{id}/account-kind is the authority.
    const canManageKind = () => can('auth.users.account-kind.manage');

    // ─── Account kind (the AuthService enum, by NAME) ───────────────────────
    // The DTO carries the enum NAME as a string ("Unknown" | "Human" | "Service"). Anything else — a number
    // from an older payload, a casing drift, an absent field — collapses to "Unknown": the unconfirmed state
    // is the truthful fallback, never a guessed "Human".
    const ACCOUNT_KINDS = ['Unknown', 'Human', 'Service'];
    const normalizeAccountKind = (value) => {
        if (typeof value === 'number') return ACCOUNT_KINDS[value] || 'Unknown';
        const text = typeof value === 'string' ? value.trim().toLowerCase() : '';
        return ACCOUNT_KINDS.find((k) => k.toLowerCase() === text) || 'Unknown';
    };
    const accountKindLabel = (value) => {
        const kind = normalizeAccountKind(value);
        return L['AccountKind' + kind] || kind;
    };
    const accountKindBadgeClass = (value) => ({
        Unknown: 'bg-label-secondary',
        Human: 'bg-label-info',
        Service: 'bg-label-warning'
    })[normalizeAccountKind(value)];

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
    const emptyFilters = () => ({ status: [], roles: [], accountKinds: [] });
    const normalizeFilters = (filters) => ({
        status: normalizeArray((filters || {}).status),
        roles: normalizeArray((filters || {}).roles),
        accountKinds: normalizeArray((filters || {}).accountKinds)
    });
    const hasFilterValue = (v) => Array.isArray(v) ? normalizeArray(v).length > 0 : normalizeString(v).length > 0;
    const matchesStatusFilter = (selected, isActive) => {
        const norm = normalizeArray(selected);
        if (!norm.length) return true;
        return norm.includes(isActive ? 'Active' : 'Passive');
    };
    const matchesRolesFilter = (selected, roles) => {
        const norm = normalizeArray(selected);
        if (!norm.length) return true;
        const rowRoles = normalizeArray(roles);
        return norm.some((r) => rowRoles.includes(r));
    };
    const matchesAccountKindFilter = (selected, accountKind) => {
        const norm = normalizeArray(selected).map(normalizeAccountKind);
        if (!norm.length) return true;
        return norm.includes(normalizeAccountKind(accountKind));
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
            return matchesStatusFilter(appliedFilters.status, row.isActive)
                && matchesRolesFilter(appliedFilters.roles, row.roles)
                && matchesAccountKindFilter(appliedFilters.accountKinds, row.accountKind);
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

        $('#filterStatus, #filterRoles, #filterAccountKind').each(function () {
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
    /*
     * The two account-kind selects are the module's only offcanvas selects, and the project's convention for a
     * select inside an offcanvas is select2 with `dropdownParent` pointed at that panel — without the parent the
     * dropdown is appended to <body>, which is BELOW the offcanvas in the stacking context, so the list opens
     * behind the panel. Same wrapper the filters above already use, so the three controls on this screen do not
     * look like two different products.
     *
     * Three options and no free text: the search box is suppressed, exactly as the filter selects do.
     */
    const initOffcanvasSelect2 = () => {
        if (!window.jQuery || !$.fn.select2) return;
        [
            { panel: '#offcanvasCreateEdit', select: '#userAccountKind', width: '100%', selectionCssClass: 'form-select' },
            // Sits inline beside the "Change type" button, so it keeps the small, shrink-to-fit shape it had.
            { panel: '#offcanvasDetailsPreview', select: '#oc-accountkind-select', width: 'auto', selectionCssClass: 'form-select form-select-sm' }
        ].forEach(({ panel, select, width, selectionCssClass }) => {
            const $panel = $(panel);
            const $el = $panel.find(select);
            if (!$el.length) return; // Razor draws neither control without auth.users.account-kind.manage.
            if ($el.hasClass('select2-hidden-accessible')) $el.select2('destroy');
            $el.select2({ dropdownParent: $panel, width, selectionCssClass, minimumResultsForSearch: Infinity });
        });
    };

    /*
     * Select2 keeps the native <select> as the value holder but paints its own box, so a value written straight
     * to `.value` leaves the painted box showing the previous choice. Every assignment made by this module goes
     * through here.
     */
    const setSelectValue = (el, value) => {
        if (!el) return;
        el.value = value;
        if (window.jQuery && $.fn.select2 && $(el).hasClass('select2-hidden-accessible')) $(el).trigger('change.select2');
    };

    /*
     * ⚠ And the painted box is a SIBLING of the select, so `d-none` on the select alone hides nothing. This
     * screen uses that toggle to withdraw the change control when the permission snapshot disagrees with the
     * server gate — a hide that silently stopped working would leave a control on screen that the server then
     * refuses.
     */
    const setSelectHidden = (el, hidden) => {
        if (!el) return;
        el.classList.toggle('d-none', hidden);
        if (window.jQuery) $(el).next('.select2-container').toggleClass('d-none', hidden);
    };

    const syncFilterControls = (values) => {
        $('#filterStatus').val(normalizeArray(values.status)).trigger('change');
        $('#filterRoles').val(normalizeArray(values.roles)).trigger('change');
        $('#filterAccountKind').val(normalizeArray(values.accountKinds)).trigger('change');
    };
    const getAppliedFilterCount = () =>
        [appliedFilters.status, appliedFilters.roles, appliedFilters.accountKinds].filter(hasFilterValue).length;

    // Build the role filter options from the distinct roles present in the loaded rows.
    const populateRoleFilterOptions = (api) => {
        const sel = document.getElementById('filterRoles');
        if (!sel || !api) return;
        const set = new Set();
        api.rows().data().each((row) => normalizeArray(row?.roles).forEach((r) => set.add(r)));
        const roles = Array.from(set).sort((a, b) => a.localeCompare(b));
        const current = normalizeArray($(sel).val());
        sel.innerHTML = '';
        roles.forEach((r) => {
            const opt = document.createElement('option');
            opt.value = r;
            opt.textContent = r;
            if (current.includes(r)) opt.selected = true;
            sel.appendChild(opt);
        });
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

    const setupFilters = async (api) => {
        populateRoleFilterOptions(api);
        initSelect2Filters();
        initOffcanvasSelect2();
        applySavedTableState(api, defaultViewState || { filters: appliedFilters });

        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = {
                status: $('#filterStatus').val() || [],
                roles: $('#filterRoles').val() || [],
                accountKinds: $('#filterAccountKind').val() || []
            };
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

    // ─── KPI cards ──────────────────────────────────────────────────────────
    // Live counts from the full loaded dataset (rows().data() returns all rows, unaffected by search/filter).
    const setKpi = (id, value) => { const el = document.getElementById(id); if (el) el.textContent = value; };
    const updateKpis = (api) => {
        const rows = api.rows().data().toArray();
        const total = rows.length;
        const active = rows.filter((r) => r && r.isActive).length;
        const noRole = rows.filter((r) => r && (!Array.isArray(r.roles) || r.roles.length === 0)).length;
        setKpi('kpi-users-total', total);
        setKpi('kpi-users-active', active);
        setKpi('kpi-users-passive', total - active);
        setKpi('kpi-users-norole', noRole);
    };

    // ─── Role chips ───────────────────────────────────────────────────────────
    // One info chip per assigned role (ported from the golden-reference Slim chip pattern).
    //   • table (collapse:true)      → one nowrap line; overflow folds into a "+N" dropdown.
    //   • offcanvas (collapse:false) → wraps and shows every role.
    const escapeChip = (v) => String(v ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    const renderAccountKindBadge = (value) =>
        `<span class="badge ${accountKindBadgeClass(value)}">${escapeChip(accountKindLabel(value))}</span>`;
    const ROLE_CHIP_MAX = 3;
    const roleChip = (r) => `<span class="badge bg-label-info">${escapeChip(r)}</span>`;
    const renderRoleChips = (roles, options) => {
        const collapse = options?.collapse === true;
        const max = options?.max || ROLE_CHIP_MAX;
        const list = Array.isArray(roles) ? roles.map((r) => normalizeString(r)).filter(Boolean) : [];
        if (!list.length) return '<span class="text-muted">-</span>';

        const tooltip = escapeChip(list.join(', '));
        if (!collapse) {
            // Offcanvas: wrap and show all roles.
            return `<span class="d-inline-flex flex-wrap gap-1" title="${tooltip}">${list.map(roleChip).join('')}</span>`;
        }

        // Table cell: one line only; fold the overflow into a click dropdown.
        const shown = list.slice(0, max);
        const rest = list.slice(max);
        let html = `<span class="d-inline-flex flex-nowrap align-items-center gap-1" title="${tooltip}">`;
        html += shown.map(roleChip).join('');
        if (rest.length) {
            const menu = rest.map((r) => `<span class="d-block px-2 py-1">${roleChip(r)}</span>`).join('');
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
        const fullName = [data.firstName, data.lastName].filter(Boolean).join(' ') || data.email || '-';
        document.getElementById('oc-title').innerText = fullName;
        document.getElementById('oc-subtitle').innerText = data.email || '-';
        document.getElementById('oc-email').innerText = data.email || '-';
        document.getElementById('oc-firstname').innerText = data.firstName || '-';
        document.getElementById('oc-lastname').innerText = data.lastName || '-';

        const rolesEl = document.getElementById('oc-roles');
        if (rolesEl) rolesEl.innerHTML = renderRoleChips(data.roles);

        const statusEl = document.getElementById('oc-status');
        const status = getStatusMap()[String(!!data.isActive)] || { title: L.Unknown, class: 'bg-label-primary' };
        statusEl.className = `badge ${status.class}`;
        statusEl.innerText = status.title || '-';

        // ── Account kind: the badge for every reader; the change control only where the server drew it AND
        //    the snapshot agrees (the server gate is the one that matters; this keeps the two from disagreeing).
        const kindEl = document.getElementById('oc-accountkind');
        if (kindEl) {
            kindEl.className = `badge ${accountKindBadgeClass(data.accountKind)}`;
            kindEl.innerText = accountKindLabel(data.accountKind);
        }
        const kindSelect = document.getElementById('oc-accountkind-select');
        const kindBtn = document.getElementById('oc-btn-accountkind');
        if (kindSelect) {
            setSelectValue(kindSelect, normalizeAccountKind(data.accountKind));
            setSelectHidden(kindSelect, !canManageKind());
        }
        if (kindBtn) {
            kindBtn.dataset.userId = data.id || '';
            kindBtn.dataset.userEmail = data.email || '';
            kindBtn.classList.toggle('d-none', !canManageKind());
        }

        // ── Security metrics (from row JSON — no extra fetch) ──
        const lastLoginEl = document.getElementById('oc-lastlogin');
        if (lastLoginEl) lastLoginEl.innerText = data.lastLoginAt ? new Date(data.lastLoginAt).toLocaleString() : (L.Never || 'Never');
        const failedEl = document.getElementById('oc-failedlogins');
        if (failedEl) failedEl.innerText = String(data.failedLoginAttempts ?? 0);
        const mfaEl = document.getElementById('oc-mfastatus');
        if (mfaEl) mfaEl.innerText = L.MFATenantPolicy || 'Tenant policy';

        const editBtn = document.getElementById('oc-btn-edit');
        if (editBtn) {
            editBtn.dataset.editId = data.id;
            editBtn.classList.toggle('d-none', !canUpdate());
        }
    };

    // ─── Create/Edit offcanvas ────────────────────────────────────────────────
    const setCreateMode = (isCreate) => {
        // Invitation hint shows on create; status switch shows on edit. No password field (invite flow).
        document.getElementById('userInviteHint')?.classList.toggle('d-none', !isCreate);
        // Classification is chosen on create and CHANGED on edit through its own route — two rows, one visible.
        document.getElementById('userAccountKindRow')?.classList.toggle('d-none', !isCreate);
        document.getElementById('userAccountKindReadRow')?.classList.toggle('d-none', isCreate);
        document.getElementById('userActiveRow')?.classList.toggle('d-none', isCreate);
        const emailEl = document.getElementById('userEmail');
        const emailHelp = document.getElementById('userEmailHelp');
        /*
         * Immutable on edit, and it must LOOK immutable. readOnly alone stops the typing but Bootstrap 5
         * dropped the [readonly] background rule that Bootstrap 4 had, so the box still reads as editable —
         * the owner clicked into it and found nothing happened. Roles/index.js already solved this the same
         * way for its own immutable Name; this is that pattern, not a second one.
         */
        if (emailEl) {
            emailEl.readOnly = !isCreate;
            emailEl.classList.toggle('bg-label-secondary', !isCreate);
        }
        if (emailHelp) emailHelp.classList.toggle('d-none', isCreate);
    };
    const resetCreateEditForm = () => {
        const form = document.getElementById('formUser');
        if (!form) return;
        form.classList.remove('was-validated');
        form.querySelectorAll('.is-invalid').forEach((el) => el.classList.remove('is-invalid'));
        document.getElementById('userItemId').value = '';
        document.getElementById('userEmail').value = '';
        document.getElementById('userFirstName').value = '';
        document.getElementById('userLastName').value = '';
        document.getElementById('userIsActive').checked = true;
        setSelectValue(document.getElementById('userAccountKind'), '');
        document.getElementById('formUserAlert').classList.add('d-none');
    };
    /*
     * ⚠ ONE CALL, TWO DOORS (2026-09-23). The kind is changed from the quick view AND from the edit offcanvas,
     * and the server takes it through its own route — `UpdateUser` carries no kind, which is why the edit form
     * shows the field READ-ONLY instead of pretending to save it. Both doors post through here, so the request,
     * the refusal handling and the toast exist once; only the way the new value is CHOSEN differs, and that
     * difference is what each door's dialog is for.
     */
    const postAccountKind = async (id, kind, offcanvasToHide) => {
        try {
            const res = await fetch(`/Users/api/${id}/account-kind`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': getAntiForgeryToken(),
                    ...getAuthHeaders()
                },
                body: JSON.stringify({ kind })
            });
            const json = await res.json().catch(() => ({}));
            if (!res.ok) {
                throw new Error((json.errors && json.errors[0]) || json.detail || L.ErrorOccurred);
            }
            suppressResponsiveReturn = true;
            responsiveReturnModalEl = null;
            offcanvasToHide?.hide();
            reloadWithSuccessToast('AccountKindChanged');
        } catch (error) {
            console.error('[Users] Account kind change failed.', error);
            window.showToast?.(error.message || L.ErrorOccurred, 'error');
        }
    };

    /*
     * The edit door. The new value is chosen INSIDE the shared confirm (its `select` input), so this screen
     * opens no dialog of its own — the product has exactly one confirm and this is a one-field question.
     */
    const askAccountKindChange = () => {
        if (!canManageKind()) { return; }
        const button = document.getElementById('btnUserAccountKindChange');
        const id = button?.dataset.userId;
        const email = button?.dataset.userEmail || '';
        const current = normalizeAccountKind(button?.dataset.userKind);
        if (!id) { return; }

        window.showConfirm?.(L.ChangeAccountKind, (chosen) => {
            const kind = normalizeAccountKind(chosen);
            if (kind === current) { return; }   // Nothing to change; the server would accept a no-op write.
            return postAccountKind(id, kind, getOcCreateEditInstance());
        }, {
            entityName: email,
            type: 'primary',
            confirmButtonText: L.ChangeAccountKind,
            showInput: true,
            inputType: 'select',
            inputLabel: L.AccountKindNewLabel,
            inputOptions: {
                Unknown: L.AccountKindUnknown,
                Human: L.AccountKindHuman,
                Service: L.AccountKindService
            },
            inputRequired: true,
            inputValidationMessage: L.AccountKindNewRequired
        });
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
            /*
             * The read-only kind and the button that changes it. The VALUE is shown as a word, never as the enum
             * number, and the button carries what the change needs so the dialog does not go looking for it.
             */
            const kindRead = document.getElementById('userAccountKindRead');
            const kindButton = document.getElementById('btnUserAccountKindChange');
            const currentKind = normalizeAccountKind(d.accountKind);
            if (kindRead) { kindRead.value = accountKindLabel(currentKind); }
            if (kindButton) {
                kindButton.dataset.userId = d.id || '';
                kindButton.dataset.userEmail = d.email || '';
                kindButton.dataset.userKind = currentKind;
                kindButton.classList.toggle('d-none', !canManageKind());
            }
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
                // Dev-only: backend returns a set-password link in setupUrl (null in prod). When present,
                // surface a copyable dialog instead of the plain toast so a dev without SMTP can grab it.
                if (!isEdit && json.setupUrl) {
                    try { dt.ajax.reload(null, false); } catch (e) { }
                    showInviteLink(json.setupUrl);
                } else {
                    reloadWithSuccessToast(isEdit ? 'RecordUpdated' : 'RecordCreated');
                }
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

    /*
     * Dev-only invitation helper: the copyable set-password link (never shown in prod — setupUrl is null there).
     *
     * ⚠ IT WEARS THE PRODUCT'S DIALOG, IT DOES NOT DRAW ONE. This is a raw `Swal.fire` because it carries a
     * field plus a copy button, which the shared confirm deliberately does not take — but "raw" decides the
     * CONTENT, never the LOOK. It used to hand-write its own padding, popup class, icon markup and title
     * spacing, and the result was the thing the owner photographed: a 38px centred title over a 512px popup,
     * an English body under a Turkish button. `_GlobalConfirmation.cshtml` publishes the package exactly so
     * that no file has to guess it — `width` is the one geometry a caller may set, and a URL needs the room.
     */
    const showInviteLink = (link) => {
        const S = window.Swal;
        if (!S) { window.showToast?.(String(link), 'info'); return; }
        const look = window.DitenDialogAppearance;
        if (typeof look !== 'function') { window.showToast?.(String(link), 'info'); return; }
        const safe = String(link).replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
        S.fire({
            ...look({ width: '520px' }),
            /*
             * ⚠ THE PICTURE RIDES THE TITLE, exactly as `showConfirm` composes it — and NOT through `iconHtml`.
             * The package collapses the library's icon slot (`dt-dialog-iconslot`), because Option B moved the
             * circle onto the title's own line; a raw dialog that fills the slot instead therefore draws no
             * icon at all. Measured live before this line existed: no circle on screen.
             */
            title: look.iconHtml(null, 'bx-link-alt') + `<span>${L.InviteLinkTitle}</span>`,
            html: `<p class="${look.description}">${L.InviteLinkHint}</p>`
                + '<div class="input-group">'
                + `<input id="inviteLinkInput" type="text" class="form-control" readonly value="${safe}">`
                + `<button id="inviteLinkCopyBtn" type="button" class="btn btn-primary" title="${L.Copy}"><i class="bx bx-copy"></i></button>`
                + '</div>',
            // "Kapat", not "İptal": nothing is being cancelled — the invitation has already been sent.
            confirmButtonText: L.Close,
            showCancelButton: false,
            didOpen: () => {
                const input = document.getElementById('inviteLinkInput');
                const btn = document.getElementById('inviteLinkCopyBtn');
                input?.addEventListener('focus', () => input.select());
                btn?.addEventListener('click', async () => {
                    try { await navigator.clipboard.writeText(link); }
                    catch (e) { input?.select(); try { document.execCommand('copy'); } catch (e2) { } }
                    window.showToast?.(L.Copied, 'success');
                });
            }
        });
    };

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

        // ── Admin actions (disable/enable/resend/reset) → MVC proxy → AuthService ──
        const adminActions = {
            'js-user-disable': { url: (id) => `/Users/disable/${id}`, toast: 'UserDisabled', confirm: L.Disable, type: 'warning' },
            'js-user-enable': { url: (id) => `/Users/enable/${id}`, toast: 'UserEnabled', confirm: L.Enable, type: 'primary' },
            'js-user-resend': { url: (id) => `/Users/resend-invite/${id}`, toast: 'InvitationResent', confirm: L.ResendInvitation, type: 'primary' },
            'js-user-reset': { url: (id) => `/Users/reset-password/${id}`, toast: 'PasswordReset', confirm: L.ResetPassword, type: 'warning' }
        };
        document.addEventListener('click', (e) => {
            const btn = e.target.closest('.js-user-disable, .js-user-enable, .js-user-resend, .js-user-reset');
            if (!btn) return;
            if (!btn.closest('.datatables-users') && !btn.closest('.modal.dtr-bs-modal')) return;
            e.preventDefault(); e.stopPropagation();
            const key = Object.keys(adminActions).find((k) => btn.classList.contains(k));
            const cfg = key ? adminActions[key] : null;
            if (!cfg) return;
            const data = tryParseRowJson(btn) || {};
            const id = btn.dataset.id || data.id;
            if (!id) return;
            window.showConfirm?.(L.AreYouSure, async () => {
                try {
                    const res = await fetch(cfg.url(id), {
                        method: 'POST',
                        credentials: 'same-origin',
                        headers: { 'RequestVerificationToken': getAntiForgeryToken(), ...getAuthHeaders() }
                    });
                    const json = await res.json().catch(() => ({}));
                    if (!res.ok) {
                        throw new Error((json.errors && json.errors[0]) || L.ErrorOccurred);
                    }
                    // Dev-only: resend/reset return a copyable set-password link → show the modal
                    // (same as create). disable/enable have no setupUrl → plain success toast.
                    if (json.setupUrl) {
                        try { dt.ajax.reload(null, false); } catch (e) { }
                        showInviteLink(json.setupUrl);
                    } else {
                        reloadWithSuccessToast(cfg.toast);
                    }
                } catch (error) {
                    console.error('[Users] Admin action failed.', error);
                    window.showToast?.(error.message || L.ErrorOccurred, 'error');
                }
            }, { entityName: data.email, type: cfg.type, confirmButtonText: cfg.confirm || '' });
        });

        document.getElementById('btnSaveUser')?.addEventListener('click', submitCreateEditForm);

        // ── Change account kind (quick view) → same-origin proxy → AuthService POST /api/users/{id}/account-kind.
        //    One dialog body (window.showConfirm, BL-367); the entity line names the account and the target kind.
        document.getElementById('oc-btn-accountkind')?.addEventListener('click', () => {
            if (!canManageKind()) return;
            const btn = document.getElementById('oc-btn-accountkind');
            const id = btn?.dataset.userId;
            const email = btn?.dataset.userEmail || '';
            const kind = normalizeAccountKind(document.getElementById('oc-accountkind-select')?.value);
            if (!id) return;
            window.showConfirm?.(L.ChangeAccountKind, () => postAccountKind(id, kind, getOcDetailsInstance()),
                { entityName: `${email} → ${accountKindLabel(kind)}`, type: 'primary', confirmButtonText: L.ChangeAccountKind });
        });

        document.getElementById('btnUserAccountKindChange')?.addEventListener('click', askAccountKindChange);

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
                colReorder: { columns: ':gt(0):not(:last-child)' },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'email', name: 'email' },
                    { data: 'firstName', name: 'firstName' },
                    { data: 'lastName', name: 'lastName' },
                    { data: 'roles', name: 'roles' },
                    { data: 'accountKind', name: 'accountKind' },
                    { data: 'isActive', name: 'isActive' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    {
                        targets: 4,
                        orderable: false,
                        className: 'text-start',
                        render: (data, type) => type === 'display'
                            ? renderRoleChips(data, { collapse: true })
                            : (Array.isArray(data) ? data.join(', ') : '')
                    },
                    {
                        targets: 5,
                        className: 'text-start',
                        render: (data, type) => type === 'display' ? renderAccountKindBadge(data) : accountKindLabel(data)
                    },
                    {
                        targets: 6,
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
                            // View: primary (visible) icon — mirrors the GoldenReference/Roles action set.
                            actions.push({
                                className: 'js-quick-view me-1', icon: 'bx bx-show',
                                attrs: { 'data-bs-toggle': 'offcanvas', 'data-bs-target': '#offcanvasDetailsPreview', 'data-json': rowJson, 'title': L.QuickView }
                            });
                            // Edit: requires permission — falls into the kebab (with icon).
                            if (canUpdate()) {
                                actions.push({ className: 'js-edit-item', icon: 'bx bx-edit', text: L.Edit, attrs: { 'data-id': full.id, 'data-json': rowJson } });
                            }
                            // ── Admin actions: state-gated, all in the kebab ──
                            if (canUpdate() && full.isActive) {
                                actions.push({ className: 'js-user-disable text-warning', icon: 'bx bx-minus-circle', text: L.Disable, attrs: { 'data-id': full.id, 'data-json': rowJson } });
                            }
                            if (canUpdate() && !full.isActive) {
                                actions.push({ className: 'js-user-enable text-success', icon: 'bx bx-check-circle', text: L.Enable, attrs: { 'data-id': full.id, 'data-json': rowJson } });
                            }
                            if (canCreate() && full.mustChangePassword) {
                                actions.push({ className: 'js-user-resend', icon: 'bx bx-mail-send', text: L.ResendInvitation, attrs: { 'data-id': full.id, 'data-json': rowJson } });
                            }
                            if (canUpdate() && full.isActive && !full.mustChangePassword) {
                                actions.push({ className: 'js-user-reset', icon: 'bx bx-key', text: L.ResetPassword, attrs: { 'data-id': full.id, 'data-json': rowJson } });
                            }
                            // Delete: requires permission — falls into the kebab (with icon).
                            if (canDelete()) {
                                actions.push({ className: 'delete-record text-danger', icon: 'bx bx-trash', text: L.Delete, attrs: { 'data-json': rowJson } });
                            }
                            return window.DitenDataTable.renderActions(actions);
                        }
                    }
                ],
                buttons: window.DtDefaults.exportButtons(
                    L.AddNew, {}, extraButtons,
                    { exportColumns: [1, 2, 3, 4, 5, 6], colvisColumns: [1, 2, 3, 4, 5, 6] }
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
        },
        /*
         * The account-kind controls are this module's only select2-wrapped selects, and both of their seams are
         * invisible in the markup: select2 paints its own box beside the <select>, so a value write has to
         * repaint it and a hide has to hide it too. Exposed so the tests drive THESE functions with the real
         * vendored library rather than a second copy of them — a copy would keep passing after the real ones
         * broke. Not called by the page: the page calls the private helpers directly.
         */
        offcanvasSelects: {
            init: initOffcanvasSelect2,
            setValue: setSelectValue,
            setHidden: setSelectHidden
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => UsersList.init());
