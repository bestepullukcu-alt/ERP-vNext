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

    // SCMM-09-UI-refine (Not 2): Priority is edited as Low/Medium/High but stored as the backend int. The three
    // buckets (High=10, Medium=20, Low=30) are the select's option values, so save needs no mapping; on edit an
    // arbitrary stored number snaps to the nearest bucket, and a new connection defaults to Medium.
    const PRIORITY_BUCKETS = [10, 20, 30];
    const nearestPriorityBucket = n => {
        const x = Number(n);
        if (!Number.isFinite(x)) return 20;
        return PRIORITY_BUCKETS.reduce((a, b) => (Math.abs(b - x) < Math.abs(a - x) ? b : a));
    };

    // SCMM-09-UI-refine (Not 3): the connection name auto-fills to "{from} → {to}" while the user has not typed one.
    // A manual edit (input event) sets the dirty flag and auto-fill backs off; the field stays editable.
    let relNameDirty = false;
    const autoFillRelationshipName = () => {
        if (relNameDirty) return;
        const from = norm(document.getElementById('relFromNodeId')?.value);
        const to = norm(document.getElementById('relToNodeId')?.value);
        if (!from || !to) return;
        const el = document.getElementById('relRelationshipName');
        if (el) el.value = `${labelNode(from)} → ${labelNode(to)}`;
    };

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
            // Idempotent: a combined-write (②) reloads nodes so the new node resolves in labels + pickers.
            nodeRows.length = 0;
            Object.keys(nodeMap).forEach(k => delete nodeMap[k]);
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
    // SCMM-09 (①): ConceptType name cell carries a colour swatch + group/list badges (kompakt, no extra columns).
    const colorSwatch = c => c
        ? `<span class="d-inline-block rounded-circle me-2 align-middle flex-shrink-0" style="width:10px;height:10px;background:${esc(c)};border:1px solid rgba(0,0,0,.15)" title="${esc(c)}"></span>`
        : '';
    const typeNameCell = (v, row) => {
        const badges = (row.isGroup ? ` <span class="badge bg-label-info">${esc(L.IsGroup || 'Group')}</span>` : '')
            + (row.isList ? ` <span class="badge bg-label-secondary">${esc(L.IsList || 'List')}</span>` : '');
        return `<span class="d-inline-flex align-items-center">${colorSwatch(row.color)}<span class="fw-medium text-heading">${esc(v)}</span></span>${badges}`;
    };
    const refCell = (v, label) => v ? `<span class="text-muted" title="${esc(v)}">${esc(label(v))}</span>` : '<span class="text-muted">—</span>';
    const conformanceBadge = v => v
        ? `<span class="badge bg-label-success">${esc(L.Conforming || 'Conforming')}</span>`
        : `<span class="badge bg-label-warning" title="${esc(L.NonConformingNote || '')}">${esc(L.NonConforming || 'Non-conforming')}</span>`;
    const sequenceCell = (ids, row) => {
        const list = Array.isArray(ids) ? ids : [];
        if (!list.length) return '<span class="text-muted">—</span>';
        // SCMM-10 (③): a multi-branch template shows a branch-count badge before the spine (no extra column).
        const branchCount = Array.isArray(row?.branches) ? row.branches.length : 0;
        const branchBadge = branchCount > 1
            ? `<span class="badge bg-label-info me-2"><i class="bx bx-git-branch me-1"></i>${branchCount} ${esc(L.BranchCountLabel || '')}</span>`
            : '';
        return `<span>${branchBadge}${list.map(id => esc(labelType(id))).join(' <i class="bx bx-chevron-right"></i> ')}</span>`;
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
                { targets:2, render:(v, t, row) => typeNameCell(v, row) },
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
                { targets:4, orderable:false, render:(v, t, row) => sequenceCell(v, row) },
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
    // SCMM-09-UI-refine (Not 4): a per-select description lookup so the RelationshipType / Direction pickers explain
    // each option. Descriptions are 7-language resx (RelTypeDesc_* / DirectionDesc_*); an option with no description
    // (e.g. a custom relationship type) renders as its plain label.
    const OPTION_DESC = {
        relRelationshipType: v => L['RelTypeDesc_' + v],
        relDirection: v => L['DirectionDesc_' + v]
    };
    const initFormSelect2 = canvasId => {
        const jq = window.jQuery;
        if (!jq?.fn?.select2) return;
        jq(`#${canvasId} select.concept-form-select2`).each(function () {
            const $s = jq(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            // select2 only clears into an empty option, so allowClear is offered exactly when the list carries one.
            const clearable = !this.required && this.options.length > 0 && this.options[0].value === '';
            const descFn = OPTION_DESC[this.id];
            const options = { dropdownParent: jq(`#${canvasId}`), placeholder: $s.data('placeholder') || '', width: '100%', allowClear: clearable };
            if (descFn) {
                options.templateResult = opt => {
                    if (!opt.id) return opt.text;
                    const d = descFn(opt.id);
                    if (!d) return opt.text;
                    const $wrap = jq('<span>');
                    $wrap.append(jq('<span class="fw-medium">').text(opt.text));
                    $wrap.append(jq('<small class="d-block text-muted">').text(d));
                    return $wrap;
                };
            }
            $s.select2(options);
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

    // SCMM-09 (①): cycle-safe parent-type options. The backend rejects a self/cycle parent (RM1, 400); the UI mirrors
    // that by excluding the type itself and all of its descendants from the picker. Same subject, non-archived only.
    const typeDescendants = rootId => {
        const childrenOf = {};
        state['concept-types'].rows.forEach(t => {
            const p = t.parentConceptTypeId;
            if (p) (childrenOf[p] = childrenOf[p] || []).push(t.conceptTypeId);
        });
        const out = new Set();
        const stack = [rootId];
        while (stack.length) {
            const cur = stack.pop();
            (childrenOf[cur] || []).forEach(c => { if (!out.has(c)) { out.add(c); stack.push(c); } });
        }
        return out;
    };
    const parentTypeOptions = (subjectId, selfId) => {
        const excluded = selfId ? typeDescendants(selfId) : new Set();
        return state['concept-types'].rows
            .filter(t => String(t.subjectId) === String(subjectId)
                && !t.isArchived
                && String(t.conceptTypeId) !== String(selfId)
                && !excluded.has(t.conceptTypeId))
            .map(t => ({ value: t.conceptTypeId, text: `${t.conceptTypeCode} — ${t.conceptTypeName}` }));
    };
    const refreshTypeParentPicker = () => {
        const subjectId = norm(document.getElementById('typeSubjectId')?.value);
        const selfId = norm(document.getElementById('typeFormId')?.value);
        fillFormSelect('typeParentConceptTypeId', parentTypeOptions(subjectId, selfId), true, null, null);
        initFormSelect2('offcanvasTypeCreateEdit');
    };

    // ─── Tab 1 · ConceptType form ────────────────────────────────────────────
    const openTypeForm = row => {
        const form = document.getElementById('conceptTypeForm');
        form.reset();
        showAlert('conceptTypeFormAlert', '');
        fillFormSelect('typeSubjectId', liveSubjects(row?.subjectId), true, row?.subjectId, labelSubject(row?.subjectId));
        fillFormSelect('typeStatus', liveStatuses('conceptStatuses'), false, row?.status, row?.status);
        // SCMM-09 (①): cycle-safe parent picker (same subject, excludes self + descendants).
        fillFormSelect('typeParentConceptTypeId', parentTypeOptions(row?.subjectId, row?.conceptTypeId), true,
            row?.parentConceptTypeId, labelType(row?.parentConceptTypeId));
        initFormSelect2('offcanvasTypeCreateEdit');

        setValue('typeFormId', row?.conceptTypeId || '');
        setValue('typeSubjectId', row?.subjectId || '');
        setValue('typeConceptTypeCode', row ? row.conceptTypeCode : nextCode('concept-types', 'conceptTypeCode'));
        setValue('typeConceptTypeName', row?.conceptTypeName || '');
        setValue('typeStatus', row?.status || 'active');
        setValue('typeSortOrder', row?.sortOrder ?? 0);
        setValue('typeDescription', row?.description || '');
        // SCMM-09 (①): color / group / list / parent.
        setValue('typeParentConceptTypeId', row?.parentConceptTypeId || '');
        const typeColor = row?.color || '';
        const colorText = document.getElementById('typeColor');
        const colorPicker = document.getElementById('typeColorPicker');
        if (colorText) colorText.value = typeColor;
        if (colorPicker) colorPicker.value = typeColor || '#6366f1';
        document.getElementById('typeIsGroup').checked = !!row?.isGroup;
        document.getElementById('typeIsList').checked = !!row?.isList;
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
            status: norm(document.getElementById('typeStatus').value) || null,
            // SCMM-09 (①) — additive fields carried on both create and update.
            color: norm(document.getElementById('typeColor').value) || null,
            isGroup: document.getElementById('typeIsGroup').checked,
            isList: document.getElementById('typeIsList').checked,
            parentConceptTypeId: norm(document.getElementById('typeParentConceptTypeId').value) || null
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
    // SCMM-09 (②): the new-node pickers (type + counterpart) are scoped to the chosen subject, exactly like From/To.
    const refreshRelationshipNewNodePickers = () => {
        const subjectId = norm(document.getElementById('relSubjectId').value);
        fillFormSelect('relNewNodeTypeId', typeOptionsFor(subjectId), true, null, null);
        fillFormSelect('relCounterpartNodeId', nodeOptionsFor(subjectId, null), true, null, null);
        initFormSelect2('offcanvasRelationshipCreateEdit');
    };
    const REL_EXISTING_NODE_IDS = ['relFromNodeId', 'relToNodeId'];
    const REL_NEW_NODE_IDS = ['relNewNodeTypeId', 'relNewNodeCode', 'relNewNodeName', 'relNewNodeEffectiveFrom', 'relCounterpartNodeId'];
    // Toggle blocks + disable the inactive one's inputs so native `required` validation only fires on the active mode.
    const setRelationshipMode = mode => {
        const isNew = mode === 'new-node';
        document.getElementById('relExistingNodesBlock')?.classList.toggle('d-none', isNew);
        document.getElementById('relNewNodeBlock')?.classList.toggle('d-none', !isNew);
        REL_EXISTING_NODE_IDS.forEach(id => setDisabled(id, isNew));
        REL_NEW_NODE_IDS.forEach(id => setDisabled(id, !isNew));
    };

    const openRelationshipForm = row => {
        const form = document.getElementById('conceptRelationshipForm');
        form.reset();
        // A stored name (edit) is treated as user-owned → never auto-overwritten; a new connection starts clean.
        relNameDirty = !!(row && row.relationshipName);
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
        setValue('relPriority', nearestPriorityBucket(row && row.priority != null ? row.priority : 20));
        setValue('relStatus', row?.status || 'active');
        setValue('relEffectiveFrom', row ? toDateInput(row.effectiveFrom) : todayInput());
        setValue('relEffectiveTo', toDateInput(row?.effectiveTo));

        // SCMM-09 (②): combined write is a create-only convenience — the mode selector is hidden on edit.
        refreshRelationshipNewNodePickers();
        setValue('relNewNodeTypeId', '');
        setValue('relCounterpartNodeId', '');
        document.getElementById('relNewNodeCode').value = '';
        document.getElementById('relNewNodeName').value = '';
        setValue('relNewNodeEffectiveFrom', todayInput());
        document.getElementById('relNewNodeSource').checked = true;
        document.getElementById('relModeExisting').checked = true;
        document.getElementById('relModeNewNode').checked = false;
        document.getElementById('relModeBlock')?.classList.toggle('d-none', !!row);
        setRelationshipMode('existing');

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
        const mode = document.querySelector('input[name="relMode"]:checked')?.value || 'existing';

        // SCMM-09 (②): combined write — new node + edge in ONE atomic call (create-only).
        if (!id && mode === 'new-node') {
            const newNodeIsSource = (document.querySelector('input[name="relNewNodeIsSource"]:checked')?.value || 'true') === 'true';
            const combined = {
                subjectId: norm(document.getElementById('relSubjectId').value),
                conceptTypeId: norm(document.getElementById('relNewNodeTypeId').value),
                conceptNodeCode: norm(document.getElementById('relNewNodeCode').value),
                conceptNodeName: norm(document.getElementById('relNewNodeName').value),
                nodeEffectiveFrom: fromDateInput(document.getElementById('relNewNodeEffectiveFrom').value),
                counterpartConceptNodeId: norm(document.getElementById('relCounterpartNodeId').value),
                relationshipType: norm(document.getElementById('relRelationshipType').value),
                relationshipCode: norm(document.getElementById('relRelationshipCode').value),
                relationshipName: norm(document.getElementById('relRelationshipName').value),
                relationshipEffectiveFrom: fromDateInput(document.getElementById('relEffectiveFrom').value),
                newNodeIsSource,
                direction: norm(document.getElementById('relDirection').value) || null,
                priority: Number(document.getElementById('relPriority').value || 0),
                relationshipStatus: norm(document.getElementById('relStatus').value) || null,
                relationshipEffectiveTo: fromDateInput(document.getElementById('relEffectiveTo').value)
            };
            await envelope(await fetch(`${base}/concept-nodes/with-relationship`, {
                method: 'POST', credentials: 'same-origin', headers: jsonHeaders, body: JSON.stringify(combined)
            }));
            await loadNodes(); // the new node must resolve in edge labels + node pickers
            return false;      // created
        }

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

    // ─── Tab 4 · ConceptChainTemplate branched builder (SCMM-10 ③) ───────────
    const typeOptionsFor = subjectId => state['concept-types'].rows
        .filter(t => String(t.subjectId) === String(subjectId) && !t.isArchived)
        .map(t => ({ value: t.conceptTypeId, text: `${t.conceptTypeCode} — ${t.conceptTypeName}` }));

    // Builder model: [{ name, steps:[{ conceptTypeId, min, max, roles:[], audiences:[] }] }].
    let branches = [];
    let templateReadOnly = false;
    // Moderator / for-whom refs are opaque config strings (D8 — no engine); the editor takes them comma-separated.
    const splitRefs = v => norm(v).split(',').map(x => x.trim()).filter(Boolean);
    // The spine (OrderedConceptTypes) is the DISTINCT type ids across every branch step, first-occurrence order — it is
    // sent alongside Branches so conformance + backward-compat keep working (SCMM-10 contract).
    const spineFromBranches = () => {
        const seen = new Set();
        const out = [];
        branches.forEach(b => b.steps.forEach(s => {
            const id = String(s.conceptTypeId || '');
            if (id && !seen.has(id)) { seen.add(id); out.push(id); }
        }));
        return out;
    };
    const bumpVersion = v => {
        const m = /^v?(\d+)(?:\.(\d+))?$/i.exec(norm(v));
        if (!m) return norm(v) ? `${norm(v)}-2` : 'v2';
        const major = parseInt(m[1], 10);
        return m[2] != null ? `v${major}.${parseInt(m[2], 10) + 1}` : `v${major + 1}`;
    };
    const renderBranches = () => {
        const host = document.getElementById('tplBranches');
        const empty = document.getElementById('tplBranchesEmpty');
        if (!host) return;
        const subjectId = norm(document.getElementById('tplSubjectId').value);
        const ro = templateReadOnly;
        host.innerHTML = branches.map((b, bi) => {
            const steps = b.steps.map((s, si) => `
                <li class="list-group-item">
                    <div class="d-flex justify-content-between align-items-center gap-2">
                        <span class="fw-medium text-truncate">${esc(labelType(s.conceptTypeId))}</span>
                        <span class="d-flex gap-1 flex-shrink-0">
                            <button type="button" class="btn btn-icon btn-sm btn-label-secondary js-step-move" data-b="${bi}" data-s="${si}" data-delta="-1" title="${esc(L.MoveUp || '')}" ${ro || si === 0 ? 'disabled' : ''}><i class="bx bx-up-arrow-alt"></i></button>
                            <button type="button" class="btn btn-icon btn-sm btn-label-secondary js-step-move" data-b="${bi}" data-s="${si}" data-delta="1" title="${esc(L.MoveDown || '')}" ${ro || si === b.steps.length - 1 ? 'disabled' : ''}><i class="bx bx-down-arrow-alt"></i></button>
                            <button type="button" class="btn btn-icon btn-sm btn-label-danger js-step-remove" data-b="${bi}" data-s="${si}" title="${esc(L.RemoveStep || '')}" ${ro ? 'disabled' : ''}><i class="bx bx-x"></i></button>
                        </span>
                    </div>
                    <div class="row g-2 mt-1">
                        <div class="col-6 col-md-3"><label class="form-label small mb-0">${esc(L.MinSelection || 'Min')}</label><input type="number" min="0" step="1" class="form-control form-control-sm js-step-min" data-b="${bi}" data-s="${si}" value="${esc(String(s.min ?? 1))}" ${ro ? 'disabled' : ''}></div>
                        <div class="col-6 col-md-3"><label class="form-label small mb-0">${esc(L.MaxSelection || 'Max')}</label><input type="number" min="1" step="1" class="form-control form-control-sm js-step-max" data-b="${bi}" data-s="${si}" value="${s.max == null ? '' : esc(String(s.max))}" ${ro ? 'disabled' : ''}></div>
                        <div class="col-12 col-md-3"><label class="form-label small mb-0">${esc(L.Moderator || '')}</label><input type="text" class="form-control form-control-sm js-step-roles" data-b="${bi}" data-s="${si}" value="${esc((s.roles || []).join(', '))}" placeholder="${esc(L.RefsCommaHint || '')}" ${ro ? 'disabled' : ''}></div>
                        <div class="col-12 col-md-3"><label class="form-label small mb-0">${esc(L.ForWhom || '')}</label><input type="text" class="form-control form-control-sm js-step-aud" data-b="${bi}" data-s="${si}" value="${esc((s.audiences || []).join(', '))}" placeholder="${esc(L.RefsCommaHint || '')}" ${ro ? 'disabled' : ''}></div>
                    </div>
                </li>`).join('');
            const opts = typeOptionsFor(subjectId).filter(o => !b.steps.some(s => String(s.conceptTypeId) === String(o.value)));
            return `
                <div class="card border shadow-none">
                    <div class="card-body p-3">
                        <div class="d-flex justify-content-between align-items-center gap-2 mb-2">
                            <input type="text" class="form-control form-control-sm js-branch-name" data-b="${bi}" value="${esc(b.name || '')}" placeholder="${esc(L.BranchNamePlaceholder || '')}" ${ro ? 'disabled' : ''} style="max-width:18rem">
                            <button type="button" class="btn btn-icon btn-sm btn-label-danger js-branch-remove" data-b="${bi}" title="${esc(L.RemoveBranch || '')}" ${ro ? 'disabled' : ''}><i class="bx bx-trash"></i></button>
                        </div>
                        <ol class="list-group list-group-numbered mb-2">${steps || `<li class="list-group-item text-muted">${esc(L.BranchStepsEmpty || '')}</li>`}</ol>
                        <div class="d-flex gap-2">
                            <select class="form-select form-select-sm js-branch-type-picker" data-b="${bi}" ${ro ? 'disabled' : ''}>
                                <option value=""></option>
                                ${opts.map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('')}
                            </select>
                            <button type="button" class="btn btn-sm btn-label-primary js-branch-add-step" data-b="${bi}" ${ro ? 'disabled' : ''}><i class="bx bx-plus"></i></button>
                        </div>
                    </div>
                </div>`;
        }).join('');
        empty?.classList.toggle('d-none', branches.length > 0);
        setValue('tplOrderedConceptTypes', spineFromBranches().join(','));
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

        // The backend always returns branches (a legacy flat template read-migrates to a single branch), so the builder
        // loads them directly. A brand-new create starts with one empty branch for convenience.
        branches = (row?.branches || []).map(b => ({
            name: b.branchName || '',
            steps: (b.steps || []).map(s => ({
                conceptTypeId: s.conceptTypeId,
                min: s.minSelection ?? 1,
                max: s.maxSelection ?? null,
                roles: (s.allowedRoleRefs || []).slice(),
                audiences: (s.audienceDimensionRefs || []).slice()
            }))
        }));
        if (!row && branches.length === 0) branches = [{ name: '', steps: [] }];

        // A published chain freezes its structure; the builder is read-only and "New version" clones it into a draft.
        const frozen = norm(row?.status) === 'published';
        templateReadOnly = frozen;
        document.getElementById('conceptTemplateFrozenNote')?.classList.toggle('d-none', !frozen);
        document.getElementById('btnTplNewVersion')?.classList.toggle('d-none', !frozen);
        document.getElementById('btnSaveConceptTemplate')?.classList.toggle('d-none', frozen);
        renderBranches();
        setDisabled('btnTplAddBranch', frozen);
        // SubjectId and the chain code are stable across versions and are not in the update contract.
        setDisabled('tplSubjectId', !!row);
        setReadOnly('tplChainCode', !!row);
        document.getElementById('tplChainCodeHint')?.classList.toggle('d-none', !!row);
        document.getElementById('offcanvasTemplateCreateEditLabel').textContent = row ? (L.EditTemplate || L.Edit) : (L.CreateTemplate || '');
        canvasOf('concept-chain-templates')?.show();
    };
    // SCMM-10 (③): "New version" clones the published template's structure into a fresh DRAFT create form (same code +
    // subject, bumped version, new effective window). The user edits + publishes it as a NON-overlapping version (V13).
    const startNewTemplateVersion = () => {
        templateReadOnly = false;
        setValue('templateFormId', '');
        setValue('tplStatus', 'draft');
        setValue('tplEffectiveFrom', todayInput());
        setValue('tplEffectiveTo', '');
        setValue('tplChainVersion', bumpVersion(document.getElementById('tplChainVersion').value));
        setDisabled('tplSubjectId', false);   // same subject, but must be sent on create
        setReadOnly('tplChainCode', false);    // same code, new version
        document.getElementById('conceptTemplateFrozenNote')?.classList.add('d-none');
        document.getElementById('btnTplNewVersion')?.classList.add('d-none');
        document.getElementById('btnSaveConceptTemplate')?.classList.remove('d-none');
        renderBranches();
        setDisabled('btnTplAddBranch', false);
        document.getElementById('offcanvasTemplateCreateEditLabel').textContent = L.CreateTemplate || '';
    };
    const submitTemplateForm = async () => {
        const id = norm(document.getElementById('templateFormId').value);
        const error = document.getElementById('tplSequenceError');
        const spine = spineFromBranches();
        // The backend requires the spine (min 2 DISTINCT types across all branches); keep the builder honest first.
        if (spine.length < 2) {
            if (error) { error.textContent = L.SequenceMinTwo || ''; error.classList.remove('d-none'); }
            throw Object.assign(new Error(L.SequenceMinTwo || ''), { handled: true });
        }
        error?.classList.add('d-none');

        // Send BOTH the spine and the rich branch structure (SCMM-10 contract; update = full replace).
        const branchPayload = branches
            .filter(b => b.steps.length > 0)
            .map((b, i) => ({
                branchCode: `BR${i + 1}`,
                branchName: norm(b.name) || null,
                sortOrder: i,
                steps: b.steps.map(s => ({
                    conceptTypeId: String(s.conceptTypeId),
                    minSelection: Number.isFinite(Number(s.min)) ? Number(s.min) : 1,
                    maxSelection: (s.max === '' || s.max == null) ? null : Number(s.max),
                    allowedRoleRefs: s.roles || [],
                    audienceDimensionRefs: s.audiences || []
                }))
            }));

        const payload = {
            chainName: norm(document.getElementById('tplChainName').value),
            orderedConceptTypes: spine,
            branches: branchPayload,
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
            // SCMM-09 (①) — colour swatch + parent + group/list flags.
            const pvColor = document.getElementById('pv-type-color');
            if (pvColor) pvColor.innerHTML = row.color
                ? `${colorSwatch(row.color)}<span class="align-middle">${esc(row.color)}</span>`
                : '<span class="text-muted">—</span>';
            setText('pv-type-parent', row.parentConceptTypeId ? labelType(row.parentConceptTypeId) : '');
            setBadge('pv-type-group', row.isGroup ? L.Yes : L.No, row.isGroup ? 'info' : 'secondary');
            setBadge('pv-type-list', row.isList ? L.Yes : L.No, row.isList ? 'info' : 'secondary');
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
            // SCMM-10 (③) — branch structure (read-only): each branch's steps with cardinality + moderator/for-whom.
            const brHost = document.getElementById('pv-tpl-branches');
            if (brHost) {
                const list = Array.isArray(row.branches) ? row.branches : [];
                brHost.innerHTML = list.length ? list.map(b => {
                    const steps = (b.steps || []).map(s => {
                        const card = `${s.minSelection ?? 1}–${s.maxSelection == null ? '∞' : s.maxSelection}`;
                        const roles = (s.allowedRoleRefs || []).length ? ` · ${esc(L.Moderator || '')}: ${esc((s.allowedRoleRefs || []).join(', '))}` : '';
                        const aud = (s.audienceDimensionRefs || []).length ? ` · ${esc(L.ForWhom || '')}: ${esc((s.audienceDimensionRefs || []).join(', '))}` : '';
                        return `<li class="list-group-item"><span class="fw-medium">${esc(labelType(s.conceptTypeId))}</span> <span class="text-muted small">(${esc(card)})${roles}${aud}</span></li>`;
                    }).join('');
                    return `<div class="card border shadow-none"><div class="card-body p-3">
                        <div class="fw-medium mb-2">${esc(b.branchName || b.branchCode || '')}</div>
                        <ol class="list-group list-group-numbered mb-0">${steps}</ol></div></div>`;
                }).join('') : `<span class="text-muted">—</span>`;
            }
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

        // SCMM-10 (③) branched builder (tab 4).
        const addBranch = event.target.closest('#btnTplAddBranch');
        if (addBranch) {
            event.preventDefault();
            if (templateReadOnly) return;
            branches.push({ name: '', steps: [] });
            renderBranches();
            return;
        }
        const newVersion = event.target.closest('#btnTplNewVersion');
        if (newVersion) {
            event.preventDefault();
            startNewTemplateVersion();
            return;
        }
        const branchRemove = event.target.closest('.js-branch-remove');
        if (branchRemove) {
            event.preventDefault();
            if (templateReadOnly) return;
            branches.splice(Number(branchRemove.dataset.b), 1);
            renderBranches();
            return;
        }
        const addStep = event.target.closest('.js-branch-add-step');
        if (addStep) {
            event.preventDefault();
            if (templateReadOnly) return;
            const bi = Number(addStep.dataset.b);
            const picker = document.querySelector(`.js-branch-type-picker[data-b="${bi}"]`);
            const value = norm(picker?.value);
            if (!value || branches[bi].steps.some(s => String(s.conceptTypeId) === value)) return;
            branches[bi].steps.push({ conceptTypeId: value, min: 1, max: null, roles: [], audiences: [] });
            renderBranches();
            return;
        }
        const stepMove = event.target.closest('.js-step-move');
        if (stepMove) {
            event.preventDefault();
            if (templateReadOnly) return;
            const bi = Number(stepMove.dataset.b);
            const si = Number(stepMove.dataset.s);
            const target = si + Number(stepMove.dataset.delta);
            const steps = branches[bi].steps;
            if (target < 0 || target >= steps.length) return;
            const [item] = steps.splice(si, 1);
            steps.splice(target, 0, item);
            renderBranches();
            return;
        }
        const stepRemove = event.target.closest('.js-step-remove');
        if (stepRemove) {
            event.preventDefault();
            if (templateReadOnly) return;
            branches[Number(stepRemove.dataset.b)].steps.splice(Number(stepRemove.dataset.s), 1);
            renderBranches();
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
        bind('relSubjectId', () => { refreshRelationshipNodePickers(null); refreshRelationshipNewNodePickers(); });
        // SCMM-09-UI-refine (Not 3): From/To drive the auto-filled connection name; a manual edit stops the auto-fill.
        bind('relFromNodeId', autoFillRelationshipName);
        bind('relToNodeId', autoFillRelationshipName);
        document.getElementById('relRelationshipName')?.addEventListener('input', () => { relNameDirty = true; });
        // SCMM-10 (③): changing the subject resets the branch builder (types are subject-scoped).
        bind('tplSubjectId', () => { branches = [{ name: '', steps: [] }]; renderBranches(); });
        // SCMM-09 (①): subject drives the cycle-safe parent-type picker on the ConceptType form.
        bind('typeSubjectId', () => refreshTypeParentPicker());
        // SCMM-09 (②): connection-mode radios toggle the existing/new-node blocks.
        document.querySelectorAll('input[name="relMode"]').forEach(radio =>
            radio.addEventListener('change', () => setRelationshipMode(radio.value)));
    };

    // SCMM-10 (③): keep the branch-step model in sync as the user types (no re-render, so focus is never lost).
    const bindTemplateBuilderInputs = () => {
        const host = document.getElementById('tplBranches');
        if (!host) return;
        host.addEventListener('input', event => {
            const el = event.target;
            if (!el?.dataset || el.dataset.b == null) return;
            const bi = Number(el.dataset.b);
            if (!branches[bi]) return;
            if (el.classList.contains('js-branch-name')) { branches[bi].name = el.value; return; }
            if (el.dataset.s == null) return;
            const step = branches[bi].steps[Number(el.dataset.s)];
            if (!step) return;
            if (el.classList.contains('js-step-min')) step.min = el.value === '' ? 0 : Number(el.value);
            else if (el.classList.contains('js-step-max')) step.max = el.value === '' ? null : Number(el.value);
            else if (el.classList.contains('js-step-roles')) step.roles = splitRefs(el.value);
            else if (el.classList.contains('js-step-aud')) step.audiences = splitRefs(el.value);
        });
    };

    // SCMM-09 (①): keep the native colour picker and the hex text input in sync (either can drive the value).
    const bindTypeColorSync = () => {
        const picker = document.getElementById('typeColorPicker');
        const text = document.getElementById('typeColor');
        if (!picker || !text) return;
        picker.addEventListener('input', () => { text.value = picker.value; });
        text.addEventListener('input', () => {
            const v = text.value.trim();
            if (/^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/.test(v)) picker.value = v;
        });
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
    bindTypeColorSync();
    bindTemplateBuilderInputs();
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
