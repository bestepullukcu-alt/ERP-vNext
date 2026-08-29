/**
 * MOD-0165-FU04 Campaigns — DataTables Index (Golden Compact aligned, proxy profile).
 *  - Native toolbar search, Select2 filter chips mounted under the toolbar, Save View via personalizationClient
 *  - Row actions: View (Details) + Edit + Archive. There is NO delete action anywhere: closing a campaign is
 *    Archive, so an already-snapshotted target selection stays explainable. Hence no bulk bar either.
 *  - DATA PATH UNCHANGED: every request still goes through the same-origin MVC proxy /CRM/Campaigns/api with the
 *    exact same query parameters as before (server-side filtering). The restyle did not move filtering client-side.
 */
(function (window, document) {
    'use strict';
    const tableEl = document.getElementById('dt-campaigns');
    if (!tableEl) return;

    const endpoint = '/CRM/Campaigns/api';
    const filterCollapseId = 'inlineFilterCollapse';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'CRM', pageKey: 'Campaigns' };
    // FU08 inserted the cycle-period column at index 11, shifting archived/updatedAt/actions one to the right.
    // FU10 dropped the brand / product columns and added the targeting-mode column: 15 -> 14.
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
    const totalColumnCount = 14;
    const baseOrder = [[12, 'desc']];

    let L = window.CampaignL10n || window.L10n || {};
    let dt = null;
    let contract = null;
    let addNewBound = false;
    let saveFilterArmed = false;
    let defaultViewRecord = null;
    let defaultViewState = null;
    let canManage = false;
    try { canManage = !!JSON.parse(document.getElementById('campaign-page-flags')?.textContent || '{}').canManage; }
    catch (e) { canManage = false; }

    const emptyFilters = () => ({ campaignStatus: '', campaignType: '', targetingMode: '', includeArchived: 'true' });
    let appliedFilters = emptyFilters();

    const getAuthHeaders = () => ({ Accept: 'application/json' });
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const badge = (v, cls = 'primary') => `<span class="badge bg-label-${cls}">${esc(v || '—')}</span>`;
    const date = v => v ? new Date(v).toLocaleString() : '—';
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const hasVal = v => norm(v).length > 0;

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status, body });
        return body.data;
    };

    // ---------------------------------------------------------------- filter chips

    const fillSelect = (id, values, showAllText) => {
        const el = document.getElementById(id);
        if (!el) return;
        el.innerHTML = `<option value="">${esc(showAllText)}</option>`
            + (values || []).map(v => `<option value="${esc(v)}">${esc(v)}</option>`).join('');
    };
    const initSelect2 = () => {
        if (!window.jQuery || !window.jQuery.fn.select2) return;
        const $body = window.jQuery(document.body);
        window.jQuery('#inlineFilterHost .select2').each(function () {
            const $s = window.jQuery(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({
                dropdownParent: $body,
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $s.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element',
                allowClear: false
            });
        });
    };
    // Every option list comes from the CONTRACT vocabulary. No hardcoded list.
    const loadFilterOptions = () => {
        fillSelect('filterCampaignStatus', contract?.vocabulary?.campaignStatuses, L.ShowAll || 'All');
        fillSelect('filterCampaignType', contract?.vocabulary?.campaignTypes, L.ShowAll || 'All');
        initSelect2();
    };

    const mountInlineFilter = () => {
        const host = document.getElementById('inlineFilterHost');
        const filterBtn = document.querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) { toolbarRow.insertAdjacentElement('afterend', host); host.classList.remove('px-6'); host.classList.add('px-3'); }
    };
    const toggleInlineFilter = () => {
        const el = document.getElementById(filterCollapseId);
        if (el) window.bootstrap?.Collapse.getOrCreateInstance(el, { toggle: false }).toggle();
    };
    const bindInlineFilterA11y = () => {
        const btn = document.querySelector('.dt-filter-btn');
        const el = document.getElementById(filterCollapseId);
        if (!btn || !el || btn.dataset.bound) return;
        btn.dataset.bound = '1';
        el.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
        el.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));
    };

    const readControls = () => ({
        campaignStatus: document.getElementById('filterCampaignStatus')?.value || '',
        campaignType: document.getElementById('filterCampaignType')?.value || '',
        targetingMode: document.getElementById('filterTargetingMode')?.value || '',
        includeArchived: document.getElementById('filterIncludeArchived')?.value || 'true'
    });
    const writeControls = f => {
        const setSel = (id, value) => {
            const el = document.getElementById(id);
            if (!el) return;
            el.value = value || '';
            if (window.jQuery?.fn?.select2) window.jQuery(el).trigger('change.select2');
        };
        setSel('filterCampaignStatus', f.campaignStatus);
        setSel('filterCampaignType', f.campaignType);
        setSel('filterTargetingMode', f.targetingMode);
        setSel('filterIncludeArchived', f.includeArchived || 'true');
    };
    const getAppliedFilterCount = () =>
        [appliedFilters.campaignStatus, appliedFilters.campaignType, appliedFilters.targetingMode]
            .filter(hasVal).length + (appliedFilters.includeArchived === 'false' ? 1 : 0);

    // ---------------------------------------------------------------- save view

    const captureColVis = api => { const r = {}; saveViewColumnIndexes.forEach(ci => { try { r[ci] = !!api.column(ci).visible(); } catch (e) {} }); return r; };
    const captureColOrder = api => { try { const o = api?.colReorder?.order?.(); return Array.isArray(o) && o.length === totalColumnCount ? o.map(Number) : null; } catch (e) { return null; } };
    const applyColVis = (api, cv) => { if (!cv) return; saveViewColumnIndexes.forEach(ci => { if (typeof cv[ci] === 'boolean') { try { api.column(ci).visible(cv[ci], false); } catch (e) {} } }); };
    const applyColOrder = (api, co) => { if (!Array.isArray(co) || co.length !== totalColumnCount || typeof api?.colReorder?.order !== 'function') return; try { api.colReorder.order(co, true); } catch (e) {} };
    const defaultColVis = () => saveViewColumnIndexes.reduce((a, ci) => { a[ci] = true; return a; }, {});
    const currentView = api => ({ filters: Object.assign({}, appliedFilters), search: norm(api.search()), colVis: captureColVis(api), columnOrder: captureColOrder(api), order: api.order() });
    const serializeView = v => JSON.stringify({
        filters: Object.keys(v?.filters || {}).sort().reduce((a, k) => { a[k] = norm(v.filters[k]); return a; }, {}),
        search: norm(v?.search), colVis: v?.colVis || defaultColVis(),
        columnOrder: Array.isArray(v?.columnOrder) ? v.columnOrder : Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(v?.order) ? v.order : baseOrder
    });
    const getResetBaselineState = () => ({ filters: emptyFilters(), search: '', colVis: defaultColVis(), columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i), order: baseOrder });
    const setSaveFilterVisible = show => { const b = document.querySelector('.dt-save-filter-btn'); if (!b) return; b.classList.toggle('d-none', !show); window.DtDefaults?.refreshButtonGroupRadii?.(); };
    const isDirtyComparedToDefault = api => serializeView(currentView(api)) !== serializeView(defaultViewState || getResetBaselineState());

    const getViewId = sv => sv?.id || sv?.Id || sv?._id || null;
    const getSavedViewName = sv => sv?.viewName || sv?.ViewName || '';
    const getViewDef = sv => { const raw = sv?.viewDefinition ?? sv?.ViewDefinition ?? {}; if (typeof raw === 'string') { try { return JSON.parse(raw); } catch (e) { return {}; } } return raw || {}; };
    const mapViewToState = sv => { const d = getViewDef(sv); return { filters: Object.assign(emptyFilters(), d.filters || {}), search: norm(d.search), colVis: d.colVis || null, columnOrder: Array.isArray(d.columnOrder) ? d.columnOrder : null, order: Array.isArray(d.order) ? d.order : null }; };
    const loadDefaultView = async () => {
        defaultViewRecord = null; defaultViewState = null;
        if (!personalizationClient?.getViews) return;
        try {
            const views = await personalizationClient.getViews(personalizationContext.moduleKey, personalizationContext.pageKey);
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            defaultViewRecord = Array.isArray(items) ? (items.find(v => v?.isDefault === true || v?.IsDefault === true) || items[0] || null) : null;
            defaultViewState = defaultViewRecord ? mapViewToState(defaultViewRecord) : null;
        } catch (e) { if (!e?.authHandled) console.error('[Campaigns SaveView] load failed', e); }
    };
    const saveDefaultView = async view => {
        if (!personalizationClient?.saveView) return;
        const payload = { moduleKey: personalizationContext.moduleKey, pageKey: personalizationContext.pageKey, viewName: (getSavedViewName(defaultViewRecord) || L.SaveView || 'Default').trim(), viewDefinition: view, isDefault: true, visibility: 'private' };
        const id = getViewId(defaultViewRecord);
        const saved = id ? await personalizationClient.updateView(id, payload) : await personalizationClient.saveView(payload);
        const rec = saved?.data || saved?.Data || saved;
        defaultViewRecord = rec && typeof rec === 'object' ? rec : Object.assign({}, defaultViewRecord || {}, payload);
        defaultViewState = view;
    };
    /** Restores table chrome + filter controls. The row set itself is refreshed by the caller via loadRows(). */
    const applySavedTableState = (api, view) => {
        const v = view || getResetBaselineState();
        appliedFilters = Object.assign(emptyFilters(), v.filters || {});
        writeControls(appliedFilters);
        applyColOrder(api, v.columnOrder);
        applyColVis(api, v.colVis);
        api.search(v.search || '');
        api.order(v.order || baseOrder);
        api.draw(false);
        window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
    };

    // ---------------------------------------------------------------- data (unchanged proxy contract)

    const query = () => {
        const params = new URLSearchParams();
        ['campaignStatus', 'campaignType', 'targetingMode'].forEach(key => {
            const value = norm(appliedFilters[key]);
            if (value) params.set(key, value);
        });
        params.set('includeArchived', appliedFilters.includeArchived === 'false' ? 'false' : 'true');
        return params.toString();
    };

    const scopeCell = row => {
        const level = row.scopeType || 'tenant';
        const label = L['ScopeType_' + level] || level;
        return row.scopeRef
            ? `<span class="fw-medium">${esc(label)}</span><span class="text-muted ms-1">${esc(row.scopeRef)}</span>`
            : `<span class="fw-medium">${esc(label)}</span>`;
    };

    const cyclePeriodCell = row => {
        const cycle = row.cyclePeriod;
        if (cycle) {
            const window = `${date(cycle.startDate)} — ${date(cycle.endDate)}`;
            return `<span class="fw-medium" title="${esc(window)}">${esc(cycle.cycleCode)}</span>`
                + `<span class="badge bg-label-secondary ms-1">${esc(cycle.cycleStatus)}</span>`;
        }
        return row.cyclePeriodId ? `<span class="text-muted">${esc(row.cyclePeriodId)}</span>` : '—';
    };

    const actions = row => {
        const id = esc(row.campaignId);
        const items = [{ className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': id, title: L.ViewDetails || L.View } }];
        if (!row.isArchived) {
            items.push({ className: 'js-edit-campaign', icon: 'bx bx-edit', text: L.EditCampaign, attrs: { 'data-id': id } });
            items.push({ className: 'js-archive-campaign text-warning', icon: 'bx bx-archive-in', text: L.ArchiveCampaign, attrs: { 'data-id': id, 'data-name': esc(row.campaignName) } });
        }
        // FU10 - the read-only targeting page. Available on every row, archived included: reading what a campaign
        // targeted is exactly the kind of thing an archived campaign is kept for.
        items.push({ className: 'js-campaign-targeting', icon: 'bx bx-been-here', text: L.TargetingTitle, attrs: { 'data-id': id } });
        return window.DitenDataTable?.renderActions ? window.DitenDataTable.renderActions(items) : '';
    };

    const buildConfig = rows => ({
        data: rows, stateSave: false, processing: true,
        colReorder: { columns: ':gt(0):not(:last-child)' },
        order: baseOrder,
        columns: [
            { data: null, defaultContent: '' }, { data: 'campaignCode' }, { data: 'campaignName' }, { data: 'campaignType' },
            { data: 'campaignStatus' }, { data: 'objectiveType' }, { data: null }, { data: 'targetingMode' },
            { data: 'startDate' }, { data: 'endDate' }, { data: 'cyclePeriod' },
            { data: 'isArchived' }, { data: 'updatedAt' }, { data: null }
        ],
        columnDefs: [
            { targets: 0, className: 'control', orderable: false, render: () => '' },
            { targets: 2, render: v => `<span class="fw-medium text-heading">${esc(v)}</span>` },
            { targets: [3, 4], render: v => badge(v, v === 'archived' ? 'secondary' : 'primary') },
            { targets: 5, render: v => esc(v || '—') },
            { targets: 7, render: v => badge(L['TargetingMode_' + (v || 'manual')] || v || 'manual', 'info') },
            // FU09 - the campaign's address. A tenant-scoped row shows the level rather than a dash: "no reference"
            // is an address here, not a missing value.
            { targets: 6, orderable: false, render: (v, t, row) => scopeCell(row) },
            { targets: [8, 9, 12], render: v => date(v) },
            // FU08 - the bound period, as the API projected it at read time. Nothing here is stored on the campaign,
            // so an unbound row and an unreadable period are shown differently: the first is a dash, the second is
            // the bare id rather than an invented label.
            { targets: 10, orderable: false, render: (v, t, row) => cyclePeriodCell(row) },
            { targets: 11, render: v => badge(v ? L.Yes : L.No, v ? 'warning' : 'success') },
            { targets: 13, title: L.Actions, orderable: false, searchable: false, className: 'cell-fit text-end pe-3 all', render: (v, t, row) => actions(row) }
        ],
        language: { emptyTable: L.EmptyState, processing: L.Loading },
        buttons: window.DtDefaults.exportButtons(canManage ? (L.CreateCampaign || '') : '', { href: '/CRM/Campaigns/Create' }, {
            filterBtn: { text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>', className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative', attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' }, action: () => toggleInlineFilter() },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn', attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (e, api) {
                    try { await saveDefaultView(currentView(api || dt)); setSaveFilterVisible(false); window.showToast?.(L.SaveView || '', 'success'); }
                    catch (err) { if (!err?.authHandled) { console.error(err); window.showToast?.(L.ErrorState, 'error'); } }
                }
            }
        }, { exportColumns: saveViewColumnIndexes, colvisColumns: saveViewColumnIndexes }),
        initComplete: function () {
            mountInlineFilter();
            bindInlineFilterA11y();
            setupFilters(this.api());
            if (canManage && !addNewBound) { document.querySelector('.add-new')?.addEventListener('click', e => { e.preventDefault(); window.location.href = '/CRM/Campaigns/Create'; }); addNewBound = true; }
            setTimeout(() => { saveFilterArmed = true; }, 0);
        },
        drawCallback: function () { window.DtDefaults?.updateVisualState?.(this.api(), getAppliedFilterCount()); }
    });

    const setupFilters = api => {
        loadFilterOptions();
        applySavedTableState(api, defaultViewState);
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = readControls();
            void loadRows();
            window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
            const el = document.getElementById(filterCollapseId);
            if (el) window.bootstrap?.Collapse.getOrCreateInstance(el, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', e => {
            e.preventDefault();
            applySavedTableState(api, getResetBaselineState());
            void loadRows();
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
        });
    };

    const loadContract = async () => {
        try {
            contract = await envelope(await fetch(`${endpoint}/contract`, { credentials: 'same-origin', headers: getAuthHeaders() }));
            if (!contract?.isReady || !contract?.features?.supportsCampaignManagement) throw new Error(L.CampaignContractUnavailable);
            canManage = canManage && !!contract.features.supportsCampaignManagement;
            return true;
        } catch (error) {
            const host = document.getElementById('campaignContractError');
            if (host) { host.textContent = error.message || L.CampaignContractUnavailable; host.classList.remove('d-none'); }
            canManage = false;
            return false;
        }
    };

    const fetchRows = async () => (await envelope(await fetch(`${endpoint}?${query()}`, { credentials: 'same-origin', headers: getAuthHeaders() })))?.items || [];

    const loadRows = async () => {
        try {
            const rows = await fetchRows();
            if (dt) { dt.clear(); dt.rows.add(rows).draw(false); }
        } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
    };

    const init = async () => {
        document.getElementById('skeleton-loader')?.classList.remove('d-none');
        try {
            if (!(await loadContract())) return;
            await loadDefaultView();
            const rows = await fetchRows();
            dt = new DataTable(tableEl, window.DtDefaults?.create ? window.DtDefaults.create(buildConfig(rows)) : buildConfig(rows));
            dt.on('column-visibility.dt search.dt order.dt column-reorder.dt columns-reordered.dt', () => {
                window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount());
                if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
            });
        } catch (error) {
            window.showToast?.(error.message || L.ErrorState, 'error');
            document.getElementById('campaignContractError')?.classList.remove('d-none');
        } finally {
            document.getElementById('skeleton-loader')?.classList.add('d-none');
        }
    };

    document.addEventListener('click', event => {
        const view = event.target.closest('.js-quick-view');
        if (view) { event.preventDefault(); window.location.href = `/CRM/Campaigns/Details/${view.dataset.id}`; return; }

        const targeting = event.target.closest('.js-campaign-targeting');
        if (targeting) { event.preventDefault(); window.location.href = `/CRM/Campaigns/${targeting.dataset.id}/Targeting`; return; }

        const edit = event.target.closest('.js-edit-campaign');
        if (edit) { event.preventDefault(); window.location.href = `/CRM/Campaigns/Edit/${edit.dataset.id}`; return; }

        const archive = event.target.closest('.js-archive-campaign');
        if (!archive) return;
        event.preventDefault();
        window.showConfirm?.(L.ArchiveCampaignConfirm, async () => {
            try {
                await envelope(await fetch(`${endpoint}/${archive.dataset.id}/archive`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }));
                window.showToast?.(L.RecordArchived, 'success');
                await loadRows();
            } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
        }, { entityName: archive.dataset.name, type: 'warning', confirmButtonText: L.ArchiveCampaign });
    });

    init();
})(window, document);
