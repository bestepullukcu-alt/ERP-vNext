/**
 * MOD-0162-FU03 Concept Node (Compact primary surface) — DataTables Index (Golden aligned)
 *  - Native toolbar search (searching enabled)
 *  - Select2 filter chips (status multi; type/subject/externalRefType single) mounted under the toolbar
 *  - SaveView (filter + search + colvis + colorder) via personalizationClient
 *  - Row actions via window.DitenDataTable.renderActions (primary View + "…" dropdown)
 *  - Toolbar "Create" (.add-new) → Compact create page (route-based; Compact rule)
 */
(function (window, document) {
    'use strict';
    const tableEl = document.getElementById('dt-concept-nodes');
    if (!tableEl) return;

    const endpoint = '/CRM/KnowledgeConcepts/api';
    const filterCollapseId = 'inlineFilterCollapse';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'CRM', pageKey: 'KnowledgeConcepts' };
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
    const totalColumnCount = 12;
    const baseOrder = [[10, 'desc']];

    let L = window.ConceptL10n || window.L10n || {};
    let dt = null;
    let contract = null;
    let addNewBound = false;
    let saveFilterArmed = false;
    let defaultViewRecord = null;
    let defaultViewState = null;
    const emptyFilters = () => ({ status: [], conceptTypeId: '', subjectId: '', externalRefType: '', includeArchived: 'true' });
    let appliedFilters = emptyFilters();
    let allRows = [];
    const subjectMap = {}, typeMap = {};

    const getAuthHeaders = () => ({ Accept: 'application/json' });
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' }[ch]));
    const badge = (v, cls = 'primary') => `<span class="badge bg-label-${cls}">${esc(v || '—')}</span>`;
    const date = v => v ? new Date(v).toLocaleString() : '—';
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const normArr = v => Array.isArray(v) ? Array.from(new Set(v.map(x => norm(x)).filter(Boolean))) : (norm(v) ? [norm(v)] : []);
    const hasVal = v => Array.isArray(v) ? normArr(v).length > 0 : norm(v).length > 0;

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status });
        return body.data;
    };

    // ─── Select2 helpers ───────────────────────────────────────────────────────
    const fillSelect = (id, options, keepShowAll) => {
        const el = document.getElementById(id);
        if (!el) return;
        const head = keepShowAll ? `<option value="">${esc(L.ShowAll || 'All')}</option>` : '';
        el.innerHTML = head + (options || []).map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('');
    };
    const syncMultiSelectSummary = $select => {
        const $container = $select.next('.select2-container');
        const $rendered = $container.find('.select2-selection__rendered');
        const $selection = $container.find('.select2-selection--multiple');
        if (!$container.length || !$rendered.length || !$selection.length) return;
        let $summary = $selection.find('.dt-inline-filter-multi__summary');
        let $actions = $selection.find('.dt-inline-filter-multi__actions');
        let $count = $selection.find('.dt-inline-filter-multi__count');
        let $arrow = $selection.find('.select2-selection__arrow');
        if (!$summary.length) { $summary = window.jQuery('<span class="dt-inline-filter-multi__summary"></span>'); $selection.prepend($summary); }
        if (!$actions.length) { $actions = window.jQuery('<span class="dt-inline-filter-multi__actions"></span>'); $selection.append($actions); }
        if (!$count.length) { $count = window.jQuery('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>'); $actions.append($count); }
        if (!$arrow.length) { $arrow = window.jQuery('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>'); $selection.append($arrow); }
        const placeholder = norm($select.data('placeholder')) || '';
        const selectedValues = normArr($select.val());
        const selectedTexts = ($select.select2('data') || []).map(i => norm(i.text)).filter(Boolean);
        $summary.text(placeholder);
        $rendered.attr('title', selectedTexts.join(', ') || placeholder);
        $container.toggleClass('dt-inline-filter-multi--has-value', selectedValues.length > 0);
        $count.toggleClass('d-none', selectedValues.length === 0).text(String(selectedValues.length));
        $actions.find('.dt-multi-clear-btn').remove();
        if (selectedValues.length > 0) {
            const $clear = window.jQuery('<span class="dt-multi-clear-btn" role="button" aria-label="' + (L.Reset || '') + '" title="' + (L.Reset || '') + '">&times;</span>');
            $clear.on('mousedown', e => { e.preventDefault(); e.stopPropagation(); $select.val(null).trigger('change'); });
            $actions.append($clear);
        }
    };

    const initSelect2 = () => {
        if (!window.jQuery || !window.jQuery.fn.select2) return;
        const $body = window.jQuery(document.body);
        window.jQuery('#inlineFilterHost .select2').each(function () {
            const $s = window.jQuery(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            if ($s.prop('multiple')) {
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
                $s.off('change.select2-summary').on('change.select2-summary', () => syncMultiSelectSummary($s));
                window.requestAnimationFrame(() => syncMultiSelectSummary($s));
            } else {
                $s.select2({
                    dropdownParent: $body,
                    dropdownCssClass: 'dt-inline-filter-dropdown',
                    selectionCssClass: 'form-select form-select-sm',
                    placeholder: $s.data('placeholder') || '',
                    minimumResultsForSearch: Infinity,
                    width: 'element',
                    allowClear: true
                });
            }
        });
    };

    const loadFilterOptions = async () => {
        fillSelect('filterStatus', (contract?.vocabularies?.conceptStatuses || []).map(v => ({ value: v, text: v })), false);
        fillSelect('filterExternalRefType', (contract?.vocabularies?.externalRefTypes || []).map(v => ({ value: v, text: v })), true);
        const fetchList = async (path, idKey, codeKey, nameKey, map) => {
            try {
                const data = await envelope(await fetch(`${endpoint}/${path}?includeArchived=true`, { credentials: 'same-origin', headers: getAuthHeaders() }));
                const items = data?.items || [];
                items.forEach(x => { if (x[idKey]) map[x[idKey]] = x[nameKey] || x[codeKey] || x[idKey]; });
                return items.map(x => ({ value: x[idKey], text: `${x[codeKey]} — ${x[nameKey]}` }));
            } catch (e) { return []; }
        };
        fillSelect('filterSubjectId', await fetchList('subjects', 'subjectId', 'subjectCode', 'subjectName', subjectMap), true);
        fillSelect('filterConceptTypeId', await fetchList('concept-types', 'conceptTypeId', 'conceptTypeCode', 'conceptTypeName', typeMap), true);
        initSelect2();
    };

    // ─── Inline filter mount / toggle (Golden) ─────────────────────────────────
    // The Concepts console carries four DataTables (this Compact node table plus the three Slim tabs), so every
    // toolbar lookup is scoped to THIS table's container — a global selector would grab the first tab's buttons.
    const nodeContainer = api => { try { return api.table().container(); } catch (e) { return document; } };
    const mountInlineFilter = api => {
        const host = document.getElementById('inlineFilterHost');
        const filterBtn = nodeContainer(api).querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) { toolbarRow.insertAdjacentElement('afterend', host); host.classList.remove('px-6'); host.classList.add('px-3'); }
    };
    const toggleInlineFilter = () => {
        const el = document.getElementById(filterCollapseId);
        if (el) window.bootstrap?.Collapse.getOrCreateInstance(el, { toggle: false }).toggle();
    };
    const bindInlineFilterA11y = api => {
        const btn = nodeContainer(api).querySelector('.dt-filter-btn');
        const el = document.getElementById(filterCollapseId);
        if (!btn || !el || btn.dataset.bound) return;
        btn.dataset.bound = '1';
        el.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
        el.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));
    };

    // ─── Custom client-side filter ─────────────────────────────────────────────
    const matchesMulti = (sel, val) => { const n = normArr(sel); return !n.length || n.includes(norm(val)); };
    const matchesSingle = (sel, val) => { const n = norm(sel); return !n || norm(val) === n; };
    const registerTableFilter = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || tableEl.dataset.filterBound === '1') return;
        tableEl.dataset.filterBound = '1';
        window.jQuery.fn.dataTable.ext.search.push((settings, _d, dataIndex, row) => {
            if (settings.nTable !== tableEl) return true;
            const r = row || dt?.row(dataIndex)?.data?.();
            if (!r) return true;
            if (appliedFilters.includeArchived === 'false' && r.isArchived) return false;
            return matchesMulti(appliedFilters.status, r.status)
                && matchesSingle(appliedFilters.conceptTypeId, r.conceptTypeId)
                && matchesSingle(appliedFilters.subjectId, r.subjectId)
                && matchesSingle(appliedFilters.externalRefType, r.externalRefType);
        });
    };
    const getAppliedFilterCount = () => [appliedFilters.status, appliedFilters.conceptTypeId, appliedFilters.subjectId, appliedFilters.externalRefType].filter(hasVal).length + (appliedFilters.includeArchived === 'false' ? 1 : 0);

    const readControls = () => ({
        status: window.jQuery('#filterStatus').val() || [],
        conceptTypeId: document.getElementById('filterConceptTypeId')?.value || '',
        subjectId: document.getElementById('filterSubjectId')?.value || '',
        externalRefType: document.getElementById('filterExternalRefType')?.value || '',
        includeArchived: document.getElementById('filterIncludeArchived')?.value || 'true'
    });
    const writeControls = f => {
        window.jQuery('#filterStatus').val(normArr(f.status)).trigger('change');
        window.jQuery('#filterConceptTypeId').val(f.conceptTypeId || '').trigger('change');
        window.jQuery('#filterSubjectId').val(f.subjectId || '').trigger('change');
        window.jQuery('#filterExternalRefType').val(f.externalRefType || '').trigger('change');
        window.jQuery('#filterIncludeArchived').val(f.includeArchived || 'true').trigger('change');
    };

    // ─── SaveView (personalization) ────────────────────────────────────────────
    const captureColVis = api => { const r = {}; saveViewColumnIndexes.forEach(ci => { try { r[ci] = !!api.column(ci).visible(); } catch (e) {} }); return r; };
    const captureColOrder = api => { try { const o = api?.colReorder?.order?.(); return Array.isArray(o) && o.length === totalColumnCount ? o.map(Number) : null; } catch (e) { return null; } };
    const applyColVis = (api, cv) => { if (!cv) return; saveViewColumnIndexes.forEach(ci => { if (typeof cv[ci] === 'boolean') { try { api.column(ci).visible(cv[ci], false); } catch (e) {} } }); };
    const applyColOrder = (api, co) => { if (!Array.isArray(co) || co.length !== totalColumnCount || typeof api?.colReorder?.order !== 'function') return; try { api.colReorder.order(co, true); } catch (e) {} };
    const defaultColVis = () => saveViewColumnIndexes.reduce((a, ci) => { a[ci] = true; return a; }, {});
    const currentView = api => ({ filters: Object.assign({}, appliedFilters), search: norm(api.search()), colVis: captureColVis(api), columnOrder: captureColOrder(api), order: api.order() });
    const serializeView = v => JSON.stringify({
        filters: Object.keys(v?.filters || {}).sort().reduce((a, k) => { a[k] = Array.isArray(v.filters[k]) ? normArr(v.filters[k]).slice().sort() : norm(v.filters[k]); return a; }, {}),
        search: norm(v?.search), colVis: v?.colVis || defaultColVis(),
        columnOrder: Array.isArray(v?.columnOrder) ? v.columnOrder : Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(v?.order) ? v.order : baseOrder
    });
    const getResetBaselineState = () => ({ filters: emptyFilters(), search: '', colVis: defaultColVis(), columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i), order: baseOrder });
    const setSaveFilterVisible = show => { const b = dt ? nodeContainer(dt).querySelector('.dt-save-filter-btn') : null; if (!b) return; b.classList.toggle('d-none', !show); window.DtDefaults?.refreshButtonGroupRadii?.(); };
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
        } catch (e) { if (!e?.authHandled) console.error('[Concept SaveView] load failed', e); }
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

    // ─── Row actions (Golden: plain primary View + "…" dropdown) ────────────────
    const actions = row => {
        const id = esc(row.conceptNodeId);
        const items = [{ className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': id, title: L.View } }];
        if (!row.isArchived) {
            items.push({ className: 'js-edit-node', icon: 'bx bx-edit', text: L.EditNode, attrs: { 'data-id': id } });
            items.push({ className: 'js-archive-node text-warning', icon: 'bx bx-archive-in', text: L.ArchiveNode, attrs: { 'data-id': id, 'data-name': esc(row.conceptNodeName) } });
        }
        return window.DitenDataTable?.renderActions ? window.DitenDataTable.renderActions(items) : '';
    };

    const buildConfig = () => ({
        data: allRows, stateSave: false, processing: true,
        colReorder: { columns: ':gt(0):not(:last-child)' },
        order: baseOrder,
        columns: [
            { data: null, defaultContent: '' }, { data: 'conceptNodeCode' }, { data: 'conceptNodeName' },
            { data: 'conceptTypeId' }, { data: 'status' }, { data: 'subjectId' }, { data: 'externalRefType' },
            { data: 'effectiveFrom' }, { data: 'effectiveTo' }, { data: 'isArchived' }, { data: 'updatedAt' }, { data: null }
        ],
        columnDefs: [
            { targets: 0, className: 'control', orderable: false, render: () => '' },
            { targets: 2, render: v => `<span class="fw-medium text-heading">${esc(v)}</span>` },
            { targets: 3, render: v => esc(typeMap[v] || v || '—') },
            { targets: 4, render: v => badge(v, v === 'archived' ? 'secondary' : (v === 'active' ? 'success' : 'primary')) },
            { targets: 5, render: v => esc(subjectMap[v] || v || '—') },
            { targets: 6, render: v => esc(v || '—') },
            { targets: [7, 8, 10], render: v => date(v) },
            { targets: 9, render: v => badge(v ? L.Yes : L.No, v ? 'warning' : 'success') },
            { targets: 11, title: L.Actions, orderable: false, searchable: false, className: 'cell-fit text-end pe-3 all', render: (v, t, row) => actions(row) }
        ],
        language: { emptyTable: L.EmptyState, processing: L.Loading },
        buttons: window.DtDefaults.exportButtons(L.CreateNode, { href: '/CRM/KnowledgeConcepts/Create' }, {
            filterBtn: { text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>', className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative', attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' }, action: () => toggleInlineFilter() },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn', attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (e, api) {
                    try { await saveDefaultView(currentView(api || dt)); setSaveFilterVisible(false); window.showToast?.(L.RecordSaved || L.SaveView || '', 'success'); }
                    catch (err) { if (!err?.authHandled) { console.error(err); window.showToast?.(L.ErrorState, 'error'); } }
                }
            }
        }, { exportColumns: saveViewColumnIndexes, colvisColumns: saveViewColumnIndexes }),
        initComplete: function () {
            const api = this.api();
            mountInlineFilter(api);
            bindInlineFilterA11y(api);
            void setupFilters(api);
            if (!addNewBound) { nodeContainer(api).querySelector('.add-new')?.addEventListener('click', e => { e.preventDefault(); window.location.href = '/CRM/KnowledgeConcepts/Create'; }); addNewBound = true; }
            setTimeout(() => { saveFilterArmed = true; }, 0);
        },
        drawCallback: function () { window.DtDefaults?.updateVisualState?.(this.api(), getAppliedFilterCount()); }
    });

    const setupFilters = async api => {
        await loadFilterOptions();
        try { api.rows().invalidate().draw(false); } catch (e) { /* table not ready */ }
        applySavedTableState(api, defaultViewState);
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = readControls();
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
            const el = document.getElementById(filterCollapseId);
            if (el) window.bootstrap?.Collapse.getOrCreateInstance(el, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', e => {
            e.preventDefault();
            applySavedTableState(api, getResetBaselineState());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(api));
        });
    };

    const loadContract = async () => {
        try {
            contract = await envelope(await fetch(`${endpoint}/contract`, { credentials: 'same-origin', headers: getAuthHeaders() }));
            if (!contract?.isReady || !contract?.features?.supportsConceptNode) throw new Error(L.ConceptContractUnavailable);
            return true;
        } catch (error) {
            const host = document.getElementById('conceptContractError');
            if (host) { host.textContent = error.message || L.ConceptContractUnavailable; host.classList.remove('d-none'); }
            return false;
        }
    };

    const init = async () => {
        document.getElementById('skeleton-loader')?.classList.remove('d-none');
        registerTableFilter();
        try {
            if (!(await loadContract())) return;
            await loadDefaultView();
            allRows = (await envelope(await fetch(`${endpoint}/concept-nodes?includeArchived=true`, { credentials: 'same-origin', headers: getAuthHeaders() })))?.items || [];
            dt = new DataTable(tableEl, window.DtDefaults?.create ? window.DtDefaults.create(buildConfig()) : buildConfig());
            dt.on('column-visibility.dt search.dt order.dt column-reorder.dt columns-reordered.dt', () => {
                window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount());
                if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
            });
        } catch (error) {
            window.showToast?.(error.message || L.ErrorState, 'error');
        } finally {
            document.getElementById('skeleton-loader')?.classList.add('d-none');
        }
    };

    // Row-action delegation (Compact: navigate; archive via proxy).
    document.addEventListener('click', event => {
        const view = event.target.closest('.js-quick-view');
        if (view) { event.preventDefault(); window.location.href = `/CRM/KnowledgeConcepts/Details/${view.dataset.id}`; return; }
        const edit = event.target.closest('.js-edit-node');
        if (edit) { event.preventDefault(); window.location.href = `/CRM/KnowledgeConcepts/Edit/${edit.dataset.id}`; return; }
        const archive = event.target.closest('.js-archive-node');
        if (!archive) return;
        event.preventDefault();
        window.showConfirm?.(L.ArchiveNodeConfirm, async () => {
            try {
                await envelope(await fetch(`${endpoint}/concept-nodes/${archive.dataset.id}/archive`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }));
                window.showToast?.(L.RecordArchived, 'success');
                allRows = (await envelope(await fetch(`${endpoint}/concept-nodes?includeArchived=true`, { credentials: 'same-origin', headers: getAuthHeaders() })))?.items || [];
                if (dt) { dt.clear(); dt.rows.add(allRows).draw(false); }
            } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
        }, { entityName: archive.dataset.name, type: 'warning', confirmButtonText: L.ArchiveNode });
    });

    init();
})(window, document);
