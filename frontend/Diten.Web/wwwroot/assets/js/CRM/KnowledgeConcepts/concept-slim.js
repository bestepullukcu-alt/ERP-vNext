/**
 * MOD-0162-FU03 Concept Graph — the three Slim aggregate tabs (Golden Slim aligned).
 *
 *   Tab 1  ConceptType            subject-scoped concept vocabulary
 *   Tab 3  ConceptRelationship    directed edges; template conformance stays VISIBLE, never hidden
 *   Tab 4  ConceptChainTemplate   the expected TYPE sequence (chain order lives here, not on a type's SortOrder)
 *
 * The ConceptNode Compact surface (tab 2) is index.js and is untouched by this module.
 *
 *  - One shared builder drives all three tables so they behave identically.
 *  - Select2 filter chips in a per-tab collapse host relocated into that table's own toolbar.
 *  - SaveView (filters + search + colVis + column order + order) via the shared personalizationClient, one pageKey
 *    per tab; Reset restores the FACTORY table state, not the saved view.
 *  - Create/edit is an offcanvas per aggregate (Slim rule); there is no delete surface — closing a row is Archive,
 *    and the runtime exposes no unarchive for these three, so an archived row is view-only.
 *  - Every call goes through the same-origin proxy /CRM/KnowledgeConcepts/api; the browser never sees a service URL
 *    or a bearer token.
 */
(function (window, document) {
    'use strict';
    if (!document.getElementById('dt-concept-types')) return;

    const base = '/CRM/KnowledgeConcepts/api';
    const PERSONALIZATION_MODULE = 'CRM';
    let L = window.ConceptL10n || window.L10n || {};

    const headers = { Accept: 'application/json' };
    const jsonHeaders = { 'Content-Type': 'application/json', Accept: 'application/json' };
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', "'":'&#39;', '"':'&quot;' }[ch]));
    const badge = (v, cls = 'primary') => `<span class="badge bg-label-${cls}">${esc(v || '—')}</span>`;
    const muted = v => v ? esc(v) : '<span class="text-muted">—</span>';
    const stamp = v => v ? new Date(v).toLocaleString() : '—';
    // Effective-window cell: "Aug 03, 26" over "05:04 PM" (Golden Slim two-line stamp).
    const dtStamp = v => {
        if (!v) return '<span class="text-muted">—</span>';
        const d = new Date(v);
        if (isNaN(d.getTime())) return esc(v);
        const dp = d.toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: '2-digit' });
        const tp = d.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: true });
        return `<div class="text-nowrap">${esc(dp)}</div><div class="text-muted small text-nowrap">${esc(tp)}</div>`;
    };
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const normArr = v => Array.isArray(v) ? Array.from(new Set(v.map(x => norm(x)).filter(Boolean))) : (norm(v) ? [norm(v)] : []);
    const hasVal = v => Array.isArray(v) ? normArr(v).length > 0 : norm(v).length > 0;

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status });
        return body.data;
    };
    // The shared contract banner lives in the Nodes tab card, which may not be the visible pane when a Slim tab
    // fails — so the message is always toasted as well and never disappears into a hidden pane.
    const showError = message => {
        const text = message || L.ErrorState;
        const host = document.getElementById('conceptContractError');
        if (host) { host.textContent = text; host.classList.remove('d-none'); }
        window.showToast?.(text, 'error');
    };

    // ─── Capability contract ─────────────────────────────────────────────────
    // Every vocabulary (concept status, chain status, relationship type, direction) comes from the FU03 contract.
    // Nothing here invents a value: an unknown one is a backend 400, so a hardcoded list would only mislead.
    let contract = null;
    const vocab = name => (contract?.vocabularies?.[name] || []).map(v => ({ value: v, text: v }));
    const loadContract = async () => {
        try {
            contract = await envelope(await fetch(`${base}/contract`, { credentials: 'same-origin', headers }));
            if (!contract?.isReady) throw new Error(L.ConceptContractUnavailable);
        } catch (error) {
            contract = null;
            showError(error.message || L.ConceptContractUnavailable);
        }
    };

    // ─── Reference labels (subjects / types / nodes) ─────────────────────────
    // Subjects and nodes are read-only references here: FU02 owns Subject, and ConceptNode is the Compact surface.
    const subjectMap = {}, typeMap = {}, nodeMap = {};
    const subjectOptions = [], nodeRows = [];
    const labelSubject = id => subjectMap[id] || id || '';
    const labelType = id => typeMap[id] || id || '';
    const labelNode = id => nodeMap[id]?.label || id || '';

    const loadSubjects = async () => {
        try {
            const data = await envelope(await fetch(`${base}/subjects?includeArchived=true`, { credentials: 'same-origin', headers }));
            (data?.items || []).forEach(s => {
                subjectMap[s.subjectId] = `${s.subjectCode} — ${s.subjectName}`;
                subjectOptions.push({ value: s.subjectId, text: subjectMap[s.subjectId], isArchived: !!s.isArchived });
            });
        } catch (e) { /* the tabs still render; the reference columns fall back to raw ids */ }
    };
    const loadNodes = async () => {
        try {
            const data = await envelope(await fetch(`${base}/concept-nodes?includeArchived=true`, { credentials: 'same-origin', headers }));
            (data?.items || []).forEach(n => {
                nodeMap[n.conceptNodeId] = {
                    label: `${n.conceptNodeCode} — ${n.conceptNodeName}`,
                    subjectId: n.subjectId, isArchived: !!n.isArchived
                };
                nodeRows.push(n);
            });
        } catch (e) { /* the From/To pickers stay empty; the backend still rejects an unresolved node */ }
    };

    // ─── Per-tab specification ───────────────────────────────────────────────
    // The kind key is also the API path segment and the value carried by data-concept-apply / -reset / -create.
    const SPECS = {
        'concept-types': {
            tableId: 'dt-concept-types', hostId: 'typesFilterHost', collapseId: 'typesFilterCollapse',
            skeletonId: 'types-skeleton-loader', pageKey: 'KnowledgeConceptTypes',
            idField: 'conceptTypeId', nameField: 'conceptTypeName',
            createText: () => L.CreateType, editText: () => L.EditType,
            archiveText: () => L.ArchiveType, archiveConfirm: () => L.ArchiveTypeConfirm,
            emptyText: () => L.TypesEmptyState,
            canvasId: 'offcanvasTypeCreateEdit',
            totalColumns: 10, managedColumns: [1, 2, 3, 4, 5, 6, 7, 8], order: [[8, 'desc']],
            archivedId: 'filterTypesArchived',
            filterFields: {
                subjectId: { id: 'filterTypesSubjectId', multi: true, field: 'subjectId', options: () => subjectOptions },
                status: { id: 'filterTypesStatus', multi: true, field: 'status', options: () => vocab('conceptStatuses') }
            }
        },
        'concept-relationships': {
            tableId: 'dt-concept-relationships', hostId: 'relationshipsFilterHost', collapseId: 'relationshipsFilterCollapse',
            skeletonId: 'relationships-skeleton-loader', pageKey: 'KnowledgeConceptRelationships',
            idField: 'conceptRelationshipId', nameField: 'relationshipName',
            createText: () => L.CreateConnection, editText: () => L.EditConnection,
            archiveText: () => L.ArchiveConnection, archiveConfirm: () => L.ArchiveConnectionConfirm,
            emptyText: () => L.ConnectionsEmptyState,
            canvasId: 'offcanvasRelationshipCreateEdit',
            totalColumns: 16, managedColumns: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14], order: [[14, 'desc']],
            archivedId: 'filterRelationshipsArchived',
            filterFields: {
                subjectId: { id: 'filterRelationshipsSubjectId', multi: true, field: 'subjectId', options: () => subjectOptions },
                relationshipType: { id: 'filterRelationshipsType', multi: true, field: 'relationshipType', options: () => vocab('relationshipTypes') },
                direction: { id: 'filterRelationshipsDirection', multi: true, field: 'direction', options: () => vocab('directions') },
                status: { id: 'filterRelationshipsStatus', multi: true, field: 'status', options: () => vocab('conceptStatuses') }
            },
            // Conformance is a single-select diagnostic filter over the derived IsTemplateConforming flag.
            extraFilters: { conformance: { id: 'filterRelationshipsConformance', field: 'isTemplateConforming' } }
        },
        'concept-chain-templates': {
            tableId: 'dt-concept-chain-templates', hostId: 'templatesFilterHost', collapseId: 'templatesFilterCollapse',
            skeletonId: 'templates-skeleton-loader', pageKey: 'KnowledgeConceptChainTemplates',
            idField: 'conceptChainTemplateId', nameField: 'chainName',
            createText: () => L.CreateTemplate, editText: () => L.EditTemplate,
            archiveText: () => L.ArchiveTemplate, archiveConfirm: () => L.ArchiveTemplateConfirm,
            emptyText: () => L.TemplatesEmptyState,
            canvasId: 'offcanvasTemplateCreateEdit',
            totalColumns: 13, managedColumns: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11], order: [[11, 'desc']],
            archivedId: 'filterTemplatesArchived',
            filterFields: {
                subjectId: { id: 'filterTemplatesSubjectId', multi: true, field: 'subjectId', options: () => subjectOptions },
                // A chain has its own, wider lifecycle (draft → review → approved → published → inactive → archived).
                status: { id: 'filterTemplatesStatus', multi: true, field: 'status', options: () => vocab('chainStatuses') }
            }
        }
    };
    const KINDS = Object.keys(SPECS);

    const emptyFilters = kind => {
        const spec = SPECS[kind];
        const f = { includeArchived: 'true' };
        Object.keys(spec.filterFields).forEach(key => { f[key] = spec.filterFields[key].multi ? [] : ''; });
        Object.keys(spec.extraFilters || {}).forEach(key => { f[key] = ''; });
        return f;
    };

    const state = {};
    KINDS.forEach(kind => {
        state[kind] = { rows: [], table: null, applied: emptyFilters(kind), armed: false, viewRecord: null, viewState: null };
    });

    // ─── Select2 filter chips ────────────────────────────────────────────────
    const clampFilterDropdown = () => {
        window.requestAnimationFrame(() => {
            const dd = document.querySelector('.select2-dropdown.dt-inline-filter-dropdown');
            if (!dd) return;
            const rect = dd.getBoundingClientRect(); const pad = 8; let dx = 0;
            if (rect.right > window.innerWidth - pad) dx -= rect.right - (window.innerWidth - pad);
            if (rect.left < pad) dx += pad - rect.left;
            if (!dx) return;
            const cs = window.getComputedStyle(dd);
            const baseLeft = parseFloat(cs.left) || rect.left + window.scrollX;
            dd.style.left = `${baseLeft + dx}px`; dd.style.transform = 'none';
        });
    };
    // A multi-select shows placeholder + count badge (not clipped tags) — the Golden inline-filter summary.
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
    const initSelect2 = hostId => {
        const jq = window.jQuery;
        if (!jq?.fn?.select2) return;
        jq(`#${hostId} select.select2`).each(function () {
            const $s = jq(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            const shared = {
                dropdownParent: jq(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select form-select-sm',
                placeholder: $s.data('placeholder') || '',
                minimumResultsForSearch: Infinity,
                width: 'element'
            };
            if ($s.prop('multiple')) {
                $s.select2(Object.assign({ containerCssClass: 'dt-inline-filter-multi', closeOnSelect: false }, shared));
                $s.off('change.select2-summary').on('change.select2-summary', () => syncMultiSelectSummary($s));
                window.requestAnimationFrame(() => syncMultiSelectSummary($s));
            } else {
                $s.select2(Object.assign({ allowClear: true }, shared));
            }
            $s.on('select2:open', clampFilterDropdown);
        });
    };
    const fillSelect = (id, options, keepShowAll) => {
        const el = document.getElementById(id);
        if (!el) return;
        const selected = normArr(window.jQuery ? window.jQuery(el).val() : el.value);
        const head = keepShowAll ? `<option value="">${esc(L.ShowAll || 'All')}</option>` : '';
        el.innerHTML = head + (options || []).map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('');
        if (selected.length && window.jQuery) window.jQuery(el).val(el.multiple ? selected : selected[0]);
    };
    const loadFilterOptions = kind => {
        const spec = SPECS[kind];
        Object.values(spec.filterFields).forEach(def => fillSelect(def.id, def.options(), false));
        initSelect2(spec.hostId);
    };

    // ─── Inline filter host relocation ───────────────────────────────────────
    const mountInlineFilter = (hostId, api) => {
        const host = document.getElementById(hostId);
        const container = api.table().container();
        const filterBtn = container.querySelector('.dt-filter-btn');
        const row = filterBtn && (filterBtn.closest('.dt-layout-row') || filterBtn.closest('.row') || (filterBtn.closest('.dt-layout-end') || {}).parentElement);
        if (host && row) { row.insertAdjacentElement('afterend', host); host.classList.remove('px-6'); host.classList.add('px-3'); }
    };
    const bindInlineFilterA11y = (collapseId, api) => {
        const btn = api.table().container().querySelector('.dt-filter-btn');
        const el = document.getElementById(collapseId);
        if (!btn || !el || btn.dataset.bound) return;
        btn.dataset.bound = '1';
        btn.setAttribute('aria-controls', collapseId);
        el.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
        el.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));
    };

    // ─── Filter read/write + client-side matching ────────────────────────────
    const readControls = kind => {
        const spec = SPECS[kind];
        const jq = window.jQuery;
        const f = { includeArchived: document.getElementById(spec.archivedId)?.value || 'true' };
        Object.entries(spec.filterFields).forEach(([key, def]) => {
            const el = document.getElementById(def.id);
            f[key] = def.multi ? normArr(jq ? jq(el).val() : []) : (el?.value || '');
        });
        Object.entries(spec.extraFilters || {}).forEach(([key, def]) => {
            f[key] = document.getElementById(def.id)?.value || '';
        });
        return f;
    };
    const writeControls = (kind, f) => {
        const spec = SPECS[kind];
        const jq = window.jQuery;
        Object.entries(spec.filterFields).forEach(([key, def]) => {
            const el = document.getElementById(def.id);
            if (!el) return;
            const value = def.multi ? normArr(f[key]) : (f[key] || '');
            if (jq) jq(el).val(def.multi ? value : (value || null)).trigger('change'); else el.value = def.multi ? '' : value;
        });
        Object.entries(spec.extraFilters || {}).forEach(([key, def]) => {
            const el = document.getElementById(def.id);
            if (!el) return;
            el.value = f[key] || '';
            if (jq) jq(el).trigger('change');
        });
        const arch = document.getElementById(spec.archivedId);
        if (arch) { arch.value = f.includeArchived || 'true'; if (jq) jq(arch).trigger('change'); }
    };
    const matchesMulti = (sel, value) => { const n = normArr(sel); return !n.length || n.includes(norm(value)); };
    const filterCount = kind => {
        const spec = SPECS[kind];
        const f = state[kind].applied;
        let n = Object.keys(spec.filterFields).filter(key => hasVal(f[key])).length;
        n += Object.keys(spec.extraFilters || {}).filter(key => hasVal(f[key])).length;
        if (f.includeArchived === 'false') n++;
        return n;
    };
    const registerTableFilter = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search) return;
        const owners = KINDS.map(k => ({ kind: k, el: document.getElementById(SPECS[k].tableId) }));
        window.jQuery.fn.dataTable.ext.search.push((settings, _d, dataIndex, row) => {
            const kind = (owners.find(o => o.el === settings.nTable) || {}).kind;
            if (!kind) return true;
            const r = row || state[kind].table?.row(dataIndex)?.data?.();
            if (!r) return true;
            const spec = SPECS[kind];
            const f = state[kind].applied;
            if (f.includeArchived === 'false' && r.isArchived) return false;
            const multiOk = Object.entries(spec.filterFields).every(([key, def]) => matchesMulti(f[key], r[def.field]));
            if (!multiOk) return false;
            return Object.entries(spec.extraFilters || {}).every(([key, def]) => {
                const want = norm(f[key]);
                return !want || String(!!r[def.field]) === want;
            });
        });
    };

    // ─── Save View (personalization) ─────────────────────────────────────────
    const captureColVis = (kind, api) => {
        const r = {};
        SPECS[kind].managedColumns.forEach(ci => { try { r[ci] = !!api.column(ci).visible(); } catch (e) { /* stale index */ } });
        return r;
    };
    const defaultColVis = kind => SPECS[kind].managedColumns.reduce((a, ci) => { a[ci] = true; return a; }, {});
    const captureColOrder = (kind, api) => {
        try {
            const o = api?.colReorder?.order?.();
            return Array.isArray(o) && o.length === SPECS[kind].totalColumns ? o.map(Number) : null;
        } catch (e) { return null; }
    };
    const applyColVis = (api, cv) => {
        if (!cv) return;
        Object.keys(cv).forEach(ci => { if (typeof cv[ci] === 'boolean') { try { api.column(Number(ci)).visible(cv[ci], false); } catch (e) { /* stale index */ } } });
    };
    const applyColOrder = (kind, api, co) => {
        if (!Array.isArray(co) || co.length !== SPECS[kind].totalColumns || typeof api?.colReorder?.order !== 'function') return;
        try { api.colReorder.order(co, true); } catch (e) { /* colReorder not ready */ }
    };
    const naturalOrder = kind => Array.from({ length: SPECS[kind].totalColumns }, (_, i) => i);
    const currentView = (kind, api) => ({
        filters: Object.assign({}, state[kind].applied), search: norm(api.search()),
        colVis: captureColVis(kind, api), columnOrder: captureColOrder(kind, api), order: api.order()
    });
    const serializeView = (kind, v) => JSON.stringify({
        filters: Object.keys(v?.filters || {}).sort().reduce((a, k) => {
            a[k] = Array.isArray(v.filters[k]) ? normArr(v.filters[k]).slice().sort() : norm(v.filters[k]);
            return a;
        }, {}),
        search: norm(v?.search),
        colVis: v?.colVis || defaultColVis(kind),
        columnOrder: Array.isArray(v?.columnOrder) ? v.columnOrder : naturalOrder(kind),
        order: Array.isArray(v?.order) ? v.order : SPECS[kind].order
    });
    // Reset is the FACTORY state (empty filters, no search, all managed columns visible, natural column order,
    // the default sort) — deliberately not "back to the saved view".
    const resetBaseline = kind => ({
        filters: emptyFilters(kind), search: '', colVis: defaultColVis(kind),
        columnOrder: naturalOrder(kind), order: SPECS[kind].order
    });
    const setSaveFilterVisible = (kind, api, show) => {
        const btn = api.table().container().querySelector('.dt-save-filter-btn');
        if (!btn) return;
        btn.classList.toggle('d-none', !show);
        window.DtDefaults?.refreshButtonGroupRadii?.();
    };
    const isDirty = (kind, api) =>
        serializeView(kind, currentView(kind, api)) !== serializeView(kind, state[kind].viewState || resetBaseline(kind));
    const refreshDirty = (kind, api) => { if (state[kind].armed) setSaveFilterVisible(kind, api, isDirty(kind, api)); };

    const viewId = r => r?.id ?? r?.Id ?? r?._id ?? null;
    const viewName = r => r?.viewName ?? r?.ViewName ?? '';
    const viewDef = r => {
        const raw = r?.viewDefinition ?? r?.ViewDefinition ?? {};
        if (typeof raw === 'string') { try { return JSON.parse(raw); } catch (e) { return {}; } }
        return raw || {};
    };
    const mapViewToState = (kind, record) => {
        const d = viewDef(record);
        return {
            filters: Object.assign(emptyFilters(kind), d.filters || {}), search: norm(d.search),
            colVis: d.colVis || null, columnOrder: Array.isArray(d.columnOrder) ? d.columnOrder : null,
            order: Array.isArray(d.order) ? d.order : null
        };
    };
    const loadDefaultView = async kind => {
        const pc = window.personalizationClient;
        state[kind].viewRecord = null; state[kind].viewState = null;
        if (!pc?.getViews) return;
        try {
            const views = await pc.getViews(PERSONALIZATION_MODULE, SPECS[kind].pageKey);
            const items = Array.isArray(views) ? views : (views?.data || views?.Data || []);
            const record = Array.isArray(items) ? (items.find(v => (v?.isDefault ?? v?.IsDefault) === true) || items[0] || null) : null;
            state[kind].viewRecord = record;
            state[kind].viewState = record ? mapViewToState(kind, record) : null;
        } catch (e) { if (!e?.authHandled) console.error('[Concept Slim SaveView] load failed', e); }
    };
    const saveDefaultView = async (kind, view) => {
        const pc = window.personalizationClient;
        if (!pc?.saveView) return;
        const payload = {
            moduleKey: PERSONALIZATION_MODULE, pageKey: SPECS[kind].pageKey,
            viewName: (viewName(state[kind].viewRecord) || L.SaveView || 'Default').trim(),
            viewDefinition: view, isDefault: true, visibility: 'private'
        };
        const id = viewId(state[kind].viewRecord);
        const saved = id ? await pc.updateView(id, payload) : await pc.saveView(payload);
        const rec = saved?.data || saved?.Data || saved;
        state[kind].viewRecord = rec && typeof rec === 'object' ? rec : Object.assign({}, state[kind].viewRecord || {}, payload);
        state[kind].viewState = view;
    };
    const applyTableState = (kind, api, view) => {
        const v = view || resetBaseline(kind);
        state[kind].applied = Object.assign(emptyFilters(kind), v.filters || {});
        writeControls(kind, state[kind].applied);
        applyColOrder(kind, api, v.columnOrder);
        applyColVis(api, v.colVis);
        api.search(v.search || '');
        api.order(v.order || SPECS[kind].order);
        api.draw(false);
        window.DtDefaults?.updateVisualState?.(api, filterCount(kind));
    };

    // ─── Row actions ─────────────────────────────────────────────────────────
    // An archived row accepts no update (the backend answers 409) and these three aggregates expose no unarchive,
    // so it is view-only. There is no delete anywhere: closing a row is Archive.
    const rowActions = (kind, row) => {
        const spec = SPECS[kind];
        const ref = { 'data-kind': kind, 'data-id': esc(row[spec.idField]) };
        const items = [{ className: 'js-concept-view', icon: 'bx bx-show', text: L.ViewDetails || L.View, attrs: Object.assign({ title: L.View }, ref) }];
        if (!row.isArchived) {
            items.push({ className: 'js-concept-edit', icon: 'bx bx-edit', text: spec.editText() || L.Edit, attrs: Object.assign({}, ref) });
            items.push({
                className: 'js-concept-archive text-warning', icon: 'bx bx-archive-in', text: spec.archiveText(),
                attrs: Object.assign({ 'data-name': esc(row[spec.nameField]) }, ref)
            });
        }
        return window.DitenDataTable?.renderActions ? window.DitenDataTable.renderActions(items) : '';
    };

    // ─── Columns ─────────────────────────────────────────────────────────────
    const statusBadge = v => badge(v, v === 'archived' ? 'secondary' : (v === 'active' || v === 'published' ? 'success' : 'primary'));
    const archivedBadge = v => badge(v ? L.Yes : L.No, v ? 'warning' : 'success');
    const nameCell = v => `<span class="fw-medium text-heading">${esc(v)}</span>`;
    const refCell = (v, label) => v ? `<span class="text-muted" title="${esc(v)}">${esc(label(v))}</span>` : '<span class="text-muted">—</span>';
    const conformanceBadge = v => v
        ? `<span class="badge bg-label-success">${esc(L.Conforming || 'Conforming')}</span>`
        : `<span class="badge bg-label-warning" title="${esc(L.NonConformingNote || '')}">${esc(L.NonConforming || 'Non-conforming')}</span>`;
    const sequenceCell = ids => {
        const list = Array.isArray(ids) ? ids : [];
        if (!list.length) return '<span class="text-muted">—</span>';
        return `<span>${list.map(id => esc(labelType(id))).join(' <i class="bx bx-chevron-right"></i> ')}</span>`;
    };

    const columnsFor = kind => {
        const ctrl = { data: null, defaultContent: '' };
        const act = { data: null };
        const actionDef = index => ({
            targets: index, title: L.Actions, orderable: false, searchable: false,
            className: 'cell-fit all text-end pe-3', render: (v, t, row) => rowActions(kind, row)
        });

        if (kind === 'concept-types') return {
            columns: [ctrl, { data:'conceptTypeCode' }, { data:'conceptTypeName' }, { data:'subjectId' }, { data:'status' },
                { data:'sortOrder' }, { data:'description' }, { data:'isArchived' }, { data:'updatedAt' }, act],
            columnDefs: [
                { targets:0, className:'control', orderable:false, render:() => '' },
                { targets:2, render:v => nameCell(v) },
                { targets:3, render:v => refCell(v, labelSubject) },
                { targets:4, render:v => statusBadge(v) },
                { targets:6, render:v => muted(v) },
                { targets:7, render:v => archivedBadge(v) },
                { targets:8, render:v => stamp(v) },
                actionDef(9)
            ]
        };

        if (kind === 'concept-relationships') return {
            columns: [ctrl, { data:'relationshipCode' }, { data:'relationshipName' }, { data:'fromConceptNodeId' },
                { data:'toConceptNodeId' }, { data:'relationshipType' }, { data:'direction' }, { data:'priority' },
                { data:'isTemplateConforming' }, { data:'status' }, { data:'subjectId' }, { data:'effectiveFrom' },
                { data:'effectiveTo' }, { data:'isArchived' }, { data:'updatedAt' }, act],
            columnDefs: [
                { targets:0, className:'control', orderable:false, render:() => '' },
                { targets:2, render:v => nameCell(v) },
                { targets:[3, 4], render:v => refCell(v, labelNode) },
                { targets:5, render:v => badge(v, 'info') },
                { targets:6, render:v => muted(v) },
                { targets:8, render:v => conformanceBadge(v) },
                { targets:9, render:v => statusBadge(v) },
                { targets:10, render:v => refCell(v, labelSubject) },
                { targets:[11, 12], render:v => dtStamp(v) },
                { targets:13, render:v => archivedBadge(v) },
                { targets:14, render:v => stamp(v) },
                actionDef(15)
            ]
        };

        return {
            columns: [ctrl, { data:'chainCode' }, { data:'chainName' }, { data:'subjectId' },
                { data:'orderedConceptTypes' }, { data:'orderedConceptTypes' }, { data:'chainVersion' },
                { data:'status' }, { data:'effectiveFrom' }, { data:'effectiveTo' }, { data:'isArchived' },
                { data:'updatedAt' }, act],
            columnDefs: [
                { targets:0, className:'control', orderable:false, render:() => '' },
                { targets:2, render:v => nameCell(v) },
                { targets:3, render:v => refCell(v, labelSubject) },
                { targets:4, orderable:false, render:v => sequenceCell(v) },
                { targets:5, render:v => esc(String((v || []).length)) },
                { targets:6, render:v => muted(v) },
                { targets:7, render:v => statusBadge(v) },
                { targets:[8, 9], render:v => dtStamp(v) },
                { targets:10, render:v => archivedBadge(v) },
                { targets:11, render:v => stamp(v) },
                actionDef(12)
            ]
        };
    };

    const buildConfig = kind => {
        const spec = SPECS[kind];
        return Object.assign({
            data: state[kind].rows, stateSave: false, searching: true, processing: true,
            colReorder: { columns: ':gt(0):not(:last-child)' },
            order: spec.order,
            language: { emptyTable: spec.emptyText(), processing: L.Loading },
            buttons: window.DtDefaults ? window.DtDefaults.exportButtons(
                spec.createText(), { 'data-concept-create': kind },
                {
                    filterBtn: {
                        text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                        className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                        attr: { title: L.Filter, 'aria-controls': spec.collapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                        action: () => window.bootstrap?.Collapse.getOrCreateInstance(document.getElementById(spec.collapseId), { toggle: false }).toggle()
                    },
                    saveFilterBtn: {
                        text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + (L.SaveView || '') + '</span>',
                        className: 'btn btn-label-primary d-none dt-save-filter-btn',
                        attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                        action: async function (e, api) {
                            const target = api || state[kind].table;
                            try {
                                await saveDefaultView(kind, currentView(kind, target));
                                setSaveFilterVisible(kind, target, false);
                                window.showToast?.(L.RecordSaved || L.SaveView || '', 'success');
                            } catch (err) {
                                if (!err?.authHandled) { console.error(err); window.showToast?.(L.ErrorState, 'error'); }
                            }
                        }
                    }
                },
                { exportColumns: spec.managedColumns, colvisColumns: spec.managedColumns }) : [],
            initComplete: function () {
                const api = this.api();
                mountInlineFilter(spec.hostId, api);
                bindInlineFilterA11y(spec.collapseId, api);
                loadFilterOptions(kind);
                applyTableState(kind, api, state[kind].viewState);
                api.on('column-visibility.dt search.dt order.dt column-reorder.dt columns-reordered.dt', () => {
                    window.DtDefaults?.updateVisualState?.(api, filterCount(kind));
                    refreshDirty(kind, api);
                });
                setTimeout(() => { state[kind].armed = true; }, 0);
            },
            drawCallback: function () { window.DtDefaults?.updateVisualState?.(this.api(), filterCount(kind)); }
        }, columnsFor(kind));
    };

    // ─── Load ────────────────────────────────────────────────────────────────
    const load = async kind => {
        const spec = SPECS[kind];
        document.getElementById(spec.skeletonId)?.classList.remove('d-none');
        try {
            const data = await envelope(await fetch(`${base}/${kind}?includeArchived=true`, { credentials: 'same-origin', headers }));
            state[kind].rows = data?.items || [];
            if (kind === 'concept-types') {
                state[kind].rows.forEach(t => { typeMap[t.conceptTypeId] = `${t.conceptTypeCode} — ${t.conceptTypeName}`; });
            }
            if (state[kind].table) {
                state[kind].table.clear();
                state[kind].table.rows.add(state[kind].rows).draw(false);
                loadFilterOptions(kind);
                return;
            }
            await loadDefaultView(kind);
            const el = document.getElementById(spec.tableId);
            const config = buildConfig(kind);
            state[kind].table = new DataTable(el, window.DtDefaults?.create ? window.DtDefaults.create(config) : config);
        } catch (error) {
            showError(error.message);
        } finally {
            document.getElementById(spec.skeletonId)?.classList.add('d-none');
        }
    };
    const findRow = (kind, id) => state[kind].rows.find(r => String(r[SPECS[kind].idField]) === String(id));

    // ─── Form helpers ────────────────────────────────────────────────────────
    const canvasOf = kind => {
        const el = document.getElementById(SPECS[kind].canvasId);
        return el ? window.bootstrap?.Offcanvas.getOrCreateInstance(el) : null;
    };
    // A programmatic set has to reach BOTH select2 (so the control repaints) and the required-fields tracker (which
    // listens with addEventListener, and jQuery .trigger() would not call it).
    const setValue = (id, value) => {
        const el = document.getElementById(id);
        if (!el) return;
        el.value = value == null ? '' : String(value);
        if (window.jQuery && window.jQuery(el).hasClass('select2-hidden-accessible')) window.jQuery(el).trigger('change.select2');
        el.dispatchEvent(new Event('change', { bubbles: true }));
    };
    const fillFormSelect = (id, options, withEmpty, current, currentLabel) => {
        const el = document.getElementById(id);
        if (!el) return;
        const list = (options || []).slice();
        // A stored value that is no longer offered (an archived reference, a retired vocabulary entry) is kept so the
        // form never silently drops it.
        if (current && !list.some(o => String(o.value) === String(current))) list.unshift({ value: current, text: currentLabel || current });
        el.innerHTML = (withEmpty ? '<option value=""></option>' : '') + list.map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('');
    };
    const initFormSelect2 = canvasId => {
        const jq = window.jQuery;
        if (!jq?.fn?.select2) return;
        jq(`#${canvasId} select.concept-form-select2`).each(function () {
            const $s = jq(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            // select2 only clears into an empty option, so allowClear is offered exactly when the list carries one.
            const clearable = !this.required && this.options.length > 0 && this.options[0].value === '';
            $s.select2({ dropdownParent: jq(`#${canvasId}`), placeholder: $s.data('placeholder') || '', width: '100%', allowClear: clearable });
        });
    };
    const setDisabled = (id, disabled) => {
        const el = document.getElementById(id);
        if (!el) return;
        el.disabled = !!disabled;
        if (window.jQuery && window.jQuery(el).hasClass('select2-hidden-accessible')) window.jQuery(el).trigger('change.select2');
    };
    const setReadOnly = (id, readOnly) => { const el = document.getElementById(id); if (el) el.readOnly = !!readOnly; };
    const showAlert = (id, message) => {
        const el = document.getElementById(id);
        if (!el) return;
        el.textContent = message || '';
        el.classList.toggle('d-none', !message);
    };
    const toDateInput = v => {
        if (!v) return '';
        const d = new Date(v);
        return isNaN(d.getTime()) ? '' : d.toISOString().slice(0, 10);
    };
    const fromDateInput = v => (norm(v) ? new Date(`${norm(v)}T00:00:00Z`).toISOString() : null);
    const todayInput = () => new Date().toISOString().slice(0, 10);
    // A code default, never a lock: PREFIX-001, PREFIX-002 … The field stays editable on create.
    const CODE_PREFIX = { 'concept-types': 'CT', 'concept-relationships': 'CR', 'concept-chain-templates': 'CHN' };
    const nextCode = (kind, field) => {
        const prefix = CODE_PREFIX[kind];
        const pattern = new RegExp(`^${prefix}-(\\d+)$`, 'i');
        const used = new Set(state[kind].rows.map(r => norm(r[field]).toUpperCase()));
        const taken = state[kind].rows.map(r => pattern.exec(norm(r[field]))).filter(Boolean).map(m => parseInt(m[1], 10));
        let n = (taken.length ? Math.max.apply(null, taken) : 0) + 1;
        let code = `${prefix}-${String(n).padStart(3, '0')}`;
        while (used.has(code)) { n += 1; code = `${prefix}-${String(n).padStart(3, '0')}`; }
        return code;
    };
    const liveSubjects = current => subjectOptions
        .filter(o => !o.isArchived || String(o.value) === String(current))
        .map(o => ({ value: o.value, text: o.text }));
    // "archived" is deliberately not offered: archiving is its own action and an update carrying it is a 400.
    const liveStatuses = name => vocab(name).filter(o => o.value !== 'archived');

    // ─── Tab 1 · ConceptType form ────────────────────────────────────────────
    const openTypeForm = row => {
        const form = document.getElementById('conceptTypeForm');
        form.reset();
        showAlert('conceptTypeFormAlert', '');
        fillFormSelect('typeSubjectId', liveSubjects(row?.subjectId), true, row?.subjectId, labelSubject(row?.subjectId));
        fillFormSelect('typeStatus', liveStatuses('conceptStatuses'), false, row?.status, row?.status);
        initFormSelect2('offcanvasTypeCreateEdit');

        setValue('typeFormId', row?.conceptTypeId || '');
        setValue('typeSubjectId', row?.subjectId || '');
        setValue('typeConceptTypeCode', row ? row.conceptTypeCode : nextCode('concept-types', 'conceptTypeCode'));
        setValue('typeConceptTypeName', row?.conceptTypeName || '');
        setValue('typeStatus', row?.status || 'active');
        setValue('typeSortOrder', row?.sortOrder ?? 0);
        setValue('typeDescription', row?.description || '');
        // SubjectId and the code are not in the update contract — they are fixed at creation.
        setDisabled('typeSubjectId', !!row);
        setReadOnly('typeConceptTypeCode', !!row);
        document.getElementById('typeConceptTypeCodeHint')?.classList.toggle('d-none', !!row);
        document.getElementById('offcanvasTypeCreateEditLabel').textContent = row ? (L.EditType || L.Edit) : (L.CreateType || '');
        canvasOf('concept-types')?.show();
    };
    const submitTypeForm = async () => {
        const id = norm(document.getElementById('typeFormId').value);
        const payload = {
            conceptTypeName: norm(document.getElementById('typeConceptTypeName').value),
            description: norm(document.getElementById('typeDescription').value) || null,
            sortOrder: Number(document.getElementById('typeSortOrder').value || 0),
            status: norm(document.getElementById('typeStatus').value) || null
        };
        if (!id) {
            payload.subjectId = norm(document.getElementById('typeSubjectId').value);
            payload.conceptTypeCode = norm(document.getElementById('typeConceptTypeCode').value);
        }
        await envelope(await fetch(id ? `${base}/concept-types/${id}` : `${base}/concept-types`, {
            method: id ? 'PUT' : 'POST', credentials: 'same-origin', headers: jsonHeaders, body: JSON.stringify(payload)
        }));
        return !!id;
    };

    // ─── Tab 3 · ConceptRelationship form ────────────────────────────────────
    // The From/To pickers only ever offer nodes of the chosen subject: a cross-subject edge is a backend 400 (V08)
    // and there is no reason to let the operator build one by hand.
    const nodeOptionsFor = (subjectId, current) => nodeRows
        .filter(n => String(n.subjectId) === String(subjectId) && (!n.isArchived || String(n.conceptNodeId) === String(current)))
        .map(n => ({ value: n.conceptNodeId, text: `${n.conceptNodeCode} — ${n.conceptNodeName}` }));
    const refreshRelationshipNodePickers = (row) => {
        const subjectId = norm(document.getElementById('relSubjectId').value);
        const from = row?.fromConceptNodeId || norm(document.getElementById('relFromNodeId').value);
        const to = row?.toConceptNodeId || norm(document.getElementById('relToNodeId').value);
        fillFormSelect('relFromNodeId', nodeOptionsFor(subjectId, from), true, from, labelNode(from));
        fillFormSelect('relToNodeId', nodeOptionsFor(subjectId, to), true, to, labelNode(to));
        initFormSelect2('offcanvasRelationshipCreateEdit');
        setValue('relFromNodeId', from || '');
        setValue('relToNodeId', to || '');
    };
    const openRelationshipForm = row => {
        const form = document.getElementById('conceptRelationshipForm');
        form.reset();
        showAlert('conceptRelationshipFormAlert', '');
        fillFormSelect('relSubjectId', liveSubjects(row?.subjectId), true, row?.subjectId, labelSubject(row?.subjectId));
        fillFormSelect('relRelationshipType', vocab('relationshipTypes'), true, row?.relationshipType, row?.relationshipType);
        fillFormSelect('relDirection', vocab('directions'), false, row?.direction, row?.direction);
        fillFormSelect('relStatus', liveStatuses('conceptStatuses'), false, row?.status, row?.status);
        initFormSelect2('offcanvasRelationshipCreateEdit');

        setValue('relationshipFormId', row?.conceptRelationshipId || '');
        setValue('relSubjectId', row?.subjectId || '');
        refreshRelationshipNodePickers(row);
        setValue('relRelationshipType', row?.relationshipType || '');
        setValue('relRelationshipCode', row ? row.relationshipCode : nextCode('concept-relationships', 'relationshipCode'));
        setValue('relRelationshipName', row?.relationshipName || '');
        setValue('relDirection', row?.direction || 'outbound');
        setValue('relPriority', row?.priority ?? 0);
        setValue('relStatus', row?.status || 'active');
        setValue('relEffectiveFrom', row ? toDateInput(row.effectiveFrom) : todayInput());
        setValue('relEffectiveTo', toDateInput(row?.effectiveTo));

        // Subject, both endpoints, the type and the code are fixed at creation: the update contract carries none.
        ['relSubjectId', 'relFromNodeId', 'relToNodeId', 'relRelationshipType'].forEach(id => setDisabled(id, !!row));
        setReadOnly('relRelationshipCode', !!row);
        // V16 is a diagnostic, not a rejection — surface it, never suppress the row.
        document.getElementById('conceptRelationshipConformanceNote')
            ?.classList.toggle('d-none', !(row && row.isTemplateConforming === false));
        document.getElementById('offcanvasRelationshipCreateEditLabel').textContent = row ? (L.EditConnection || L.Edit) : (L.CreateConnection || '');
        canvasOf('concept-relationships')?.show();
    };
    const submitRelationshipForm = async () => {
        const id = norm(document.getElementById('relationshipFormId').value);
        const payload = {
            relationshipName: norm(document.getElementById('relRelationshipName').value),
            effectiveFrom: fromDateInput(document.getElementById('relEffectiveFrom').value),
            direction: norm(document.getElementById('relDirection').value) || null,
            priority: Number(document.getElementById('relPriority').value || 0),
            status: norm(document.getElementById('relStatus').value) || null,
            effectiveTo: fromDateInput(document.getElementById('relEffectiveTo').value)
        };
        if (!id) {
            payload.subjectId = norm(document.getElementById('relSubjectId').value);
            payload.fromConceptNodeId = norm(document.getElementById('relFromNodeId').value);
            payload.toConceptNodeId = norm(document.getElementById('relToNodeId').value);
            payload.relationshipType = norm(document.getElementById('relRelationshipType').value);
            payload.relationshipCode = norm(document.getElementById('relRelationshipCode').value);
        }
        await envelope(await fetch(id ? `${base}/concept-relationships/${id}` : `${base}/concept-relationships`, {
            method: id ? 'PUT' : 'POST', credentials: 'same-origin', headers: jsonHeaders, body: JSON.stringify(payload)
        }));
        return !!id;
    };

    // ─── Tab 4 · ConceptChainTemplate form + sequence editor ─────────────────
    let sequence = [];
    const typeOptionsFor = subjectId => state['concept-types'].rows
        .filter(t => String(t.subjectId) === String(subjectId) && !t.isArchived)
        .map(t => ({ value: t.conceptTypeId, text: `${t.conceptTypeCode} — ${t.conceptTypeName}` }));
    const renderSequence = readOnly => {
        const host = document.getElementById('tplSequence');
        const empty = document.getElementById('tplSequenceEmpty');
        if (!host) return;
        host.innerHTML = sequence.map((id, index) => `
            <li class="list-group-item d-flex justify-content-between align-items-center gap-2">
                <span class="text-truncate">${esc(labelType(id))}</span>
                <span class="d-flex gap-1 flex-shrink-0">
                    <button type="button" class="btn btn-icon btn-sm btn-label-secondary js-tpl-move" data-index="${index}" data-delta="-1" title="${esc(L.MoveUp || '')}" ${readOnly || index === 0 ? 'disabled' : ''}><i class="bx bx-up-arrow-alt"></i></button>
                    <button type="button" class="btn btn-icon btn-sm btn-label-secondary js-tpl-move" data-index="${index}" data-delta="1" title="${esc(L.MoveDown || '')}" ${readOnly || index === sequence.length - 1 ? 'disabled' : ''}><i class="bx bx-down-arrow-alt"></i></button>
                    <button type="button" class="btn btn-icon btn-sm btn-label-danger js-tpl-remove" data-index="${index}" title="${esc(L.RemoveStep || '')}" ${readOnly ? 'disabled' : ''}><i class="bx bx-x"></i></button>
                </span>
            </li>`).join('');
        empty?.classList.toggle('d-none', sequence.length > 0);
        setValue('tplOrderedConceptTypes', sequence.join(','));
    };
    const refreshTemplateTypePicker = () => {
        const subjectId = norm(document.getElementById('tplSubjectId').value);
        // A type already in the sequence is not offered again: V12 forbids the same type twice (v1; recursion is F7).
        fillFormSelect('tplTypePicker', typeOptionsFor(subjectId).filter(o => !sequence.includes(String(o.value))), true, null, null);
        initFormSelect2('offcanvasTemplateCreateEdit');
    };
    const openTemplateForm = row => {
        const form = document.getElementById('conceptTemplateForm');
        form.reset();
        showAlert('conceptTemplateFormAlert', '');
        document.getElementById('tplSequenceError')?.classList.add('d-none');
        fillFormSelect('tplSubjectId', liveSubjects(row?.subjectId), true, row?.subjectId, labelSubject(row?.subjectId));
        fillFormSelect('tplStatus', liveStatuses('chainStatuses'), false, row?.status, row?.status);
        initFormSelect2('offcanvasTemplateCreateEdit');

        setValue('templateFormId', row?.conceptChainTemplateId || '');
        setValue('tplSubjectId', row?.subjectId || '');
        setValue('tplChainCode', row ? row.chainCode : nextCode('concept-chain-templates', 'chainCode'));
        setValue('tplChainName', row?.chainName || '');
        setValue('tplDescription', row?.description || '');
        setValue('tplChainVersion', row?.chainVersion || '');
        setValue('tplStatus', row?.status || 'draft');
        setValue('tplEffectiveFrom', row ? toDateInput(row.effectiveFrom) : todayInput());
        setValue('tplEffectiveTo', toDateInput(row?.effectiveTo));

        sequence = (row?.orderedConceptTypes || []).map(String);
        // A published chain freezes its sequence; changing it needs a new version.
        const frozen = norm(row?.status) === 'published';
        document.getElementById('conceptTemplateFrozenNote')?.classList.toggle('d-none', !frozen);
        renderSequence(frozen);
        refreshTemplateTypePicker();
        setDisabled('tplTypePicker', frozen);
        setDisabled('btnTplAddType', frozen);
        // SubjectId and the chain code are stable across versions and are not in the update contract.
        setDisabled('tplSubjectId', !!row);
        setReadOnly('tplChainCode', !!row);
        document.getElementById('tplChainCodeHint')?.classList.toggle('d-none', !!row);
        document.getElementById('offcanvasTemplateCreateEditLabel').textContent = row ? (L.EditTemplate || L.Edit) : (L.CreateTemplate || '');
        canvasOf('concept-chain-templates')?.show();
    };
    const submitTemplateForm = async () => {
        const id = norm(document.getElementById('templateFormId').value);
        const error = document.getElementById('tplSequenceError');
        // Keep the sequence honest while it is being built; V12/V13 remain the backend's call.
        if (sequence.length < 2) {
            if (error) { error.textContent = L.SequenceMinTwo || ''; error.classList.remove('d-none'); }
            throw Object.assign(new Error(L.SequenceMinTwo || ''), { handled: true });
        }
        if (new Set(sequence).size !== sequence.length) {
            if (error) { error.textContent = L.SequenceDuplicateType || ''; error.classList.remove('d-none'); }
            throw Object.assign(new Error(L.SequenceDuplicateType || ''), { handled: true });
        }
        error?.classList.add('d-none');

        const payload = {
            chainName: norm(document.getElementById('tplChainName').value),
            orderedConceptTypes: sequence.slice(),
            effectiveFrom: fromDateInput(document.getElementById('tplEffectiveFrom').value),
            description: norm(document.getElementById('tplDescription').value) || null,
            status: norm(document.getElementById('tplStatus').value) || null,
            chainVersion: norm(document.getElementById('tplChainVersion').value) || null,
            effectiveTo: fromDateInput(document.getElementById('tplEffectiveTo').value)
        };
        if (!id) {
            payload.subjectId = norm(document.getElementById('tplSubjectId').value);
            payload.chainCode = norm(document.getElementById('tplChainCode').value);
        }
        await envelope(await fetch(id ? `${base}/concept-chain-templates/${id}` : `${base}/concept-chain-templates`, {
            method: id ? 'PUT' : 'POST', credentials: 'same-origin', headers: jsonHeaders, body: JSON.stringify(payload)
        }));
        return !!id;
    };

    const OPEN_FORM = {
        'concept-types': openTypeForm,
        'concept-relationships': openRelationshipForm,
        'concept-chain-templates': openTemplateForm
    };
    const SUBMIT_FORM = {
        'concept-types': submitTypeForm,
        'concept-relationships': submitRelationshipForm,
        'concept-chain-templates': submitTemplateForm
    };
    const ALERT_ID = {
        'concept-types': 'conceptTypeFormAlert',
        'concept-relationships': 'conceptRelationshipFormAlert',
        'concept-chain-templates': 'conceptTemplateFormAlert'
    };

    // ─── Read-only quick view (shared preview canvas) ────────────────────────
    let previewRef = null;
    const setText = (id, value) => { const el = document.getElementById(id); if (el) el.textContent = value == null || value === '' ? '—' : String(value); };
    const setBadge = (id, value, cls) => {
        const el = document.getElementById(id);
        if (!el) return;
        el.textContent = value || '—';
        el.className = `badge bg-label-${cls}`;
    };
    const fillPreview = (kind, row) => {
        document.querySelectorAll('#offcanvasDetailsPreview [data-preview-kind]')
            .forEach(el => el.classList.toggle('d-none', el.getAttribute('data-preview-kind') !== kind));

        if (kind === 'concept-types') {
            setText('conceptPreviewTitle', row.conceptTypeName);
            setText('conceptPreviewSubtitle', `${L.TypeDetails || ''} · ${row.conceptTypeCode}`);
            setText('pv-type-code', row.conceptTypeCode);
            setText('pv-type-name', row.conceptTypeName);
            setText('pv-type-subject', labelSubject(row.subjectId));
            setBadge('pv-type-status', row.status, row.status === 'active' ? 'success' : 'secondary');
            setText('pv-type-sortorder', row.sortOrder);
            setText('pv-type-description', row.description);
            setBadge('pv-type-archived', row.isArchived ? L.Yes : L.No, row.isArchived ? 'warning' : 'success');
            setText('pv-type-updated', stamp(row.updatedAt || row.createdAt));
        } else if (kind === 'concept-relationships') {
            setText('conceptPreviewTitle', row.relationshipName);
            setText('conceptPreviewSubtitle', `${L.ConnectionDetails || ''} · ${row.relationshipCode}`);
            setText('pv-rel-code', row.relationshipCode);
            setText('pv-rel-name', row.relationshipName);
            setText('pv-rel-subject', labelSubject(row.subjectId));
            setText('pv-rel-from', labelNode(row.fromConceptNodeId));
            setText('pv-rel-to', labelNode(row.toConceptNodeId));
            setBadge('pv-rel-type', row.relationshipType, 'info');
            setText('pv-rel-direction', row.direction);
            setText('pv-rel-priority', row.priority);
            setBadge('pv-rel-status', row.status, row.status === 'active' ? 'success' : 'secondary');
            setBadge('pv-rel-conformance', row.isTemplateConforming ? (L.Conforming || '') : (L.NonConforming || ''), row.isTemplateConforming ? 'success' : 'warning');
            document.getElementById('pv-rel-conformance-note')?.classList.toggle('d-none', row.isTemplateConforming !== false);
            setText('pv-rel-from-date', stamp(row.effectiveFrom));
            setText('pv-rel-to-date', row.effectiveTo ? stamp(row.effectiveTo) : '');
            setBadge('pv-rel-archived', row.isArchived ? L.Yes : L.No, row.isArchived ? 'warning' : 'success');
            setText('pv-rel-updated', stamp(row.updatedAt || row.createdAt));
        } else {
            setText('conceptPreviewTitle', row.chainName);
            setText('conceptPreviewSubtitle', `${L.TemplateDetails || ''} · ${row.chainCode}`);
            setText('pv-tpl-code', row.chainCode);
            setText('pv-tpl-name', row.chainName);
            setText('pv-tpl-subject', labelSubject(row.subjectId));
            setText('pv-tpl-version', row.chainVersion);
            setBadge('pv-tpl-status', row.status, row.status === 'published' ? 'success' : 'secondary');
            const seq = document.getElementById('pv-tpl-sequence');
            if (seq) {
                seq.innerHTML = (row.orderedConceptTypes || [])
                    .map(id => `<li class="list-group-item">${esc(labelType(id))}</li>`).join('')
                    || `<li class="list-group-item text-muted">${esc(L.SequenceEmpty || '')}</li>`;
            }
            document.getElementById('pv-tpl-frozen')?.classList.toggle('d-none', norm(row.status) !== 'published');
            setText('pv-tpl-description', row.description);
            setText('pv-tpl-from', stamp(row.effectiveFrom));
            setText('pv-tpl-to', row.effectiveTo ? stamp(row.effectiveTo) : '');
            setBadge('pv-tpl-archived', row.isArchived ? L.Yes : L.No, row.isArchived ? 'warning' : 'success');
            setText('pv-tpl-updated', stamp(row.updatedAt || row.createdAt));
        }

        previewRef = { kind, id: row[SPECS[kind].idField] };
        // An archived row is view-only: there is no update path and no unarchive endpoint for these three.
        document.getElementById('conceptPreviewEdit')?.classList.toggle('d-none', !!row.isArchived);
        const el = document.getElementById('offcanvasDetailsPreview');
        if (el) window.bootstrap?.Offcanvas.getOrCreateInstance(el).show();
    };

    // ─── Delegated interactions ──────────────────────────────────────────────
    document.addEventListener('click', async event => {
        // Create lives in each table's toolbar (.add-new slot) tagged with data-concept-create.
        const create = event.target.closest('[data-concept-create]');
        if (create) { event.preventDefault(); OPEN_FORM[create.getAttribute('data-concept-create')](null); return; }

        const view = event.target.closest('.js-concept-view');
        if (view) {
            event.preventDefault();
            const row = findRow(view.dataset.kind, view.dataset.id);
            if (row) fillPreview(view.dataset.kind, row);
            return;
        }
        const edit = event.target.closest('.js-concept-edit');
        if (edit) {
            event.preventDefault();
            const row = findRow(edit.dataset.kind, edit.dataset.id);
            if (row) OPEN_FORM[edit.dataset.kind](row);
            return;
        }
        const previewEdit = event.target.closest('#conceptPreviewEdit');
        if (previewEdit && previewRef) {
            event.preventDefault();
            const { kind, id } = previewRef;
            const row = findRow(kind, id);
            if (!row) return;
            // Wait for the preview to finish closing: opening the form while the first canvas is still animating
            // leaves a stranded backdrop.
            const previewEl = document.getElementById('offcanvasDetailsPreview');
            if (previewEl) {
                previewEl.addEventListener('hidden.bs.offcanvas', () => OPEN_FORM[kind](row), { once: true });
                window.bootstrap?.Offcanvas.getOrCreateInstance(previewEl).hide();
            } else {
                OPEN_FORM[kind](row);
            }
            return;
        }

        const archive = event.target.closest('.js-concept-archive');
        if (archive) {
            event.preventDefault();
            const kind = archive.dataset.kind;
            const spec = SPECS[kind];
            window.showConfirm?.(spec.archiveConfirm(), async () => {
                try {
                    await envelope(await fetch(`${base}/${kind}/${archive.dataset.id}/archive`, { method: 'POST', credentials: 'same-origin', headers }));
                    window.showToast?.(L.RecordArchived, 'success');
                    await load(kind);
                    // Archiving a type can flip a relationship's conformance diagnostic, so refresh the edges too.
                    if (kind === 'concept-types' || kind === 'concept-chain-templates') await load('concept-relationships');
                } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
            }, { entityName: archive.dataset.name, type: 'warning', confirmButtonText: spec.archiveText() });
            return;
        }

        // Sequence editor (tab 4).
        const move = event.target.closest('.js-tpl-move');
        if (move) {
            event.preventDefault();
            const index = Number(move.dataset.index);
            const target = index + Number(move.dataset.delta);
            if (target < 0 || target >= sequence.length) return;
            const [item] = sequence.splice(index, 1);
            sequence.splice(target, 0, item);
            renderSequence(false);
            return;
        }
        const remove = event.target.closest('.js-tpl-remove');
        if (remove) {
            event.preventDefault();
            sequence.splice(Number(remove.dataset.index), 1);
            renderSequence(false);
            refreshTemplateTypePicker();
            return;
        }
        const addType = event.target.closest('#btnTplAddType');
        if (addType) {
            event.preventDefault();
            const value = norm(document.getElementById('tplTypePicker').value);
            if (!value || sequence.includes(value)) return;
            sequence.push(value);
            renderSequence(false);
            refreshTemplateTypePicker();
            return;
        }

        // Filter apply / reset.
        const apply = event.target.closest('[data-concept-apply]');
        if (apply) {
            event.preventDefault();
            const kind = apply.getAttribute('data-concept-apply');
            const api = state[kind].table;
            if (!api) return;
            state[kind].applied = readControls(kind);
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, filterCount(kind));
            refreshDirty(kind, api);
            window.bootstrap?.Collapse.getOrCreateInstance(document.getElementById(SPECS[kind].collapseId), { toggle: false }).hide();
            return;
        }
        const reset = event.target.closest('[data-concept-reset]');
        if (reset) {
            event.preventDefault();
            const kind = reset.getAttribute('data-concept-reset');
            const api = state[kind].table;
            if (!api) return;
            applyTableState(kind, api, resetBaseline(kind));
            refreshDirty(kind, api);
        }
    });

    // Subject drives the node pickers (tab 3) and the type picker (tab 4).
    const bindSubjectCascade = () => {
        const bind = (id, handler) => {
            const el = document.getElementById(id);
            if (!el) return;
            el.addEventListener('change', handler);
            if (window.jQuery) window.jQuery(el).on('change', handler);
        };
        bind('relSubjectId', () => refreshRelationshipNodePickers(null));
        bind('tplSubjectId', () => { sequence = []; renderSequence(false); refreshTemplateTypePicker(); });
    };

    KINDS.forEach(kind => {
        const form = document.querySelector(`#${SPECS[kind].canvasId} form`);
        form?.addEventListener('submit', async event => {
            event.preventDefault();
            if (!form.reportValidity()) return;
            try {
                const wasUpdate = await SUBMIT_FORM[kind]();
                window.showToast?.(wasUpdate ? L.RecordUpdated : L.RecordCreated, 'success');
                canvasOf(kind)?.hide();
                await load(kind);
                // A new/renamed type changes the sequence and conformance labels the other two tabs render.
                if (kind === 'concept-types') await Promise.all([load('concept-relationships'), load('concept-chain-templates')]);
                if (kind === 'concept-chain-templates') await load('concept-relationships');
            } catch (error) {
                if (error?.handled) return;                       // already rendered inline by the sequence editor
                showAlert(ALERT_ID[kind], error.message || L.ErrorState);
                window.showToast?.(error.message || L.ErrorState, 'error');
            }
        });
    });

    // A DataTable built inside a hidden tab-pane measures its columns wrong; recalc when the tab is shown.
    const paneKind = {
        '#tab-concept-types': 'concept-types',
        '#tab-concept-connections': 'concept-relationships',
        '#tab-concept-templates': 'concept-chain-templates'
    };
    document.querySelectorAll('button[data-bs-toggle="tab"]').forEach(btn => {
        btn.addEventListener('shown.bs.tab', event => {
            const kind = paneKind[event.target.getAttribute('data-bs-target')];
            if (!kind) return;
            try { state[kind].table?.columns.adjust().responsive.recalc(); } catch (e) { /* responsive not ready yet */ }
        });
    });

    registerTableFilter();
    bindSubjectCascade();
    (async () => {
        L = window.ConceptL10n || window.L10n || {};
        // The contract first (it supplies every vocabulary the filters and forms pick from), then the read-only
        // references, then the types — the other two tabs label their columns with type and node names.
        await loadContract();
        await Promise.all([loadSubjects(), loadNodes()]);
        await load('concept-types');
        await Promise.all([load('concept-relationships'), load('concept-chain-templates')]);
    })();
})(window, document);
