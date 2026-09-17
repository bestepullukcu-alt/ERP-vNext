/**
 * MOD-0165-FU03 (WP-FREQ-A) Visit Frequency / Call-Cycle Policy console — DataTables Index (Golden Compact v2,
 * mirrors the SCMM-11 EligibilityPolicies console). Client-side table over the same-origin proxy.
 *
 * Lifecycle (both soft): Archive keeps a policy as readable history (status=archived, still listed after a filter reset);
 * Delete (WP-FREQ-A soft-delete) removes it from the list + resolve working set. The create/edit editor (FREQ-B) and the
 * details quick-view / resolve panel (FREQ-C) are placeholders here — "New Policy", row "Details" and row "Edit" only
 * open the (empty) offcanvas shells.
 *
 * Vocabulary is NOT hardcoded: the status / target / source filter options come from the FU03 /contract endpoint, and
 * each code is humanized for display (statuses use the localized statusLabels bridge; the rest title-case the code).
 */
(function (window, document) {
    'use strict';
    const tableEl = document.getElementById('dt-visitfrequencypolicies');
    if (!tableEl) return;

    const endpoint = '/CRM/VisitFrequencyPolicies/api';
    const filterCollapseId = 'inlineFilterCollapse';
    // Golden Compact v2 column map: 0 control · 1 Policy(name+code) · 2 Target · 3 Frequency · 4 Validity ·
    // 5 Weight(band) · 6 Source · 7 Status · 8 Actions.
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7];
    const totalColumnCount = 9;
    const baseOrder = [[1, 'asc']];

    // Save View personalization (shared backend; no new endpoint — mirrors EligibilityPolicies).
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'crm', pageKey: 'visit-frequency-policies' };

    const L = window.VfpL10n || window.L10n || {};
    let dt = null;
    let addNewBound = false;
    let saveFilterArmed = false;
    let defaultViewRecord = null;
    let defaultViewState = null;
    const emptyFilters = () => ({ status: [], targetType: [], source: '' });
    let appliedFilters = emptyFilters();
    let allRows = [];
    const rowById = {};
    // AĞIRLIK: contract priority weight (int) → band code (populated from /contract; empty ⇒ raw-number fallback).
    let bandByWeight = new Map();
    const bandLabels = L.bandLabels || {};
    const sourceLabels = L.sourceLabels || {};
    const periodLabels = L.periodLabels || {};
    // AĞIRLIK: contract band CODE → Bootstrap label tone (5-tier weight model, WP-FREQ-F1). Unmapped codes fall back
    // to a neutral secondary badge — the codes themselves still arrive from /contract, never hardcoded as vocabulary.
    const bandTones = { 'override-all': 'danger', 'campaign-level': 'warning', standard: 'primary', baseline: 'info', 'last-resort': 'secondary' };

    // ── helpers ──────────────────────────────────────────────────────────────
    const getAuthHeaders = () => ({ Accept: 'application/json' });
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const badge = (v, cls = 'primary') => `<span class="badge bg-label-${cls}">${esc(v || '—')}</span>`;
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const normArr = v => Array.isArray(v) ? Array.from(new Set(v.map(x => norm(x)).filter(Boolean))) : (norm(v) ? [norm(v)] : []);
    const hasVal = v => Array.isArray(v) ? normArr(v).length > 0 : norm(v).length > 0;
    // Title-case a contract vocabulary code for display ("account-contact-link" → "Account Contact Link"). The valid
    // set still comes from /contract — this is only a display transform, never a hardcoded vocabulary.
    const humanize = code => norm(code).split(/[-_\s]+/).filter(Boolean).map(w => w.charAt(0).toUpperCase() + w.slice(1)).join(' ');
    const statusLabels = L.statusLabels || {};
    const statusLabel = s => statusLabels[norm(s)] || humanize(s) || '—';
    const statusTone = s => ({ draft: 'secondary', active: 'success', inactive: 'warning', archived: 'secondary' }[norm(s)] || 'primary');
    const shortId = id => { const s = norm(id); return s ? s.slice(0, 8) + '…' : ''; };
    // dd.MM.yyyy (mockup). Falls back to the raw yyyy-MM-dd slice if the value is not a parseable date.
    const fmtDate = v => {
        const s = norm(v);
        if (!s) return '';
        const d = new Date(s);
        if (Number.isNaN(d.getTime())) return s.slice(0, 10);
        const p = n => String(n).padStart(2, '0');
        return `${p(d.getDate())}.${p(d.getMonth() + 1)}.${d.getFullYear()}`;
    };
    // KAYNAK: contract source code → localized label (humanized fallback; never a hardcoded vocabulary).
    const sourceLabel = s => sourceLabels[norm(s)] || humanize(s) || '—';
    // FREKANS: contract period code → localized label (humanized fallback; never a hardcoded vocabulary).
    const periodLabel = p => periodLabels[norm(p)] || humanize(p) || '';
    // AĞIRLIK: priority weight → contract band code (from bandByWeight). Empty ⇒ no matching tier.
    const weightCode = priority => {
        const p = priority == null ? null : Number(priority);
        if (p == null || Number.isNaN(p)) return '';
        return bandByWeight.get(p) || '';
    };
    // AĞIRLIK: priority weight → localized band label. Falls back to the bare weight when no tier matches.
    const weightLabel = priority => {
        const p = priority == null ? null : Number(priority);
        if (p == null || Number.isNaN(p)) return '';
        const code = bandByWeight.get(p);
        const label = code ? (bandLabels[code] || humanize(code)) : '';
        return label || String(p);
    };

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status });
        return body.data;
    };
    const getJson = path => fetch(`${endpoint}${path}`, { credentials: 'same-origin', headers: getAuthHeaders() }).then(envelope);

    // ── inline filter (Golden Compact: dt-inline-filter-host) ────────────────
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
                    dropdownParent: $body, dropdownCssClass: 'dt-inline-filter-dropdown',
                    containerCssClass: 'dt-inline-filter-multi', selectionCssClass: 'form-select form-select-sm',
                    placeholder: $s.data('placeholder') || '', minimumResultsForSearch: Infinity, width: 'element', closeOnSelect: false
                });
                $s.off('change.select2-summary').on('change.select2-summary', () => syncMultiSelectSummary($s));
                window.requestAnimationFrame(() => syncMultiSelectSummary($s));
            } else {
                $s.select2({
                    dropdownParent: $body, dropdownCssClass: 'dt-inline-filter-dropdown',
                    selectionCssClass: 'form-select form-select-sm', placeholder: $s.data('placeholder') || '',
                    minimumResultsForSearch: Infinity, width: 'element', allowClear: true
                });
            }
        });
    };

    // Vocabulary comes from the FU03 contract — never hardcoded here (statuses, target types, sources AND the
    // priority bands that map an authored weight to its named AĞIRLIK band).
    const loadFilterOptions = async () => {
        let vocab = { statuses: [], targetTypes: [], sources: [], priorityBands: [] };
        try {
            const contract = await envelope(await fetch(`${endpoint}/visit-frequency-policies/contract`, { credentials: 'same-origin', headers: getAuthHeaders() }));
            vocab = contract?.vocabulary || vocab;
        } catch (e) { /* filters degrade to empty; the list still renders */ }
        bandByWeight = new Map((vocab.priorityBands || []).map(b => [Number(b.value), norm(b.code)]));
        fillSelect('filterStatus', (vocab.statuses || []).map(v => ({ value: v, text: statusLabel(v) })), false);
        fillSelect('filterTargetType', (vocab.targetTypes || []).map(v => ({ value: v, text: humanize(v) })), false);
        fillSelect('filterSource', (vocab.sources || []).map(v => ({ value: v, text: sourceLabel(v) })), true);
        initSelect2();
    };

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

    const matchesMulti = (sel, val) => { const n = normArr(sel); return !n.length || n.includes(norm(val)); };
    const matchesSingle = (sel, val) => { const n = norm(sel); return !n || norm(val) === n; };
    const registerTableFilter = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || tableEl.dataset.filterBound === '1') return;
        tableEl.dataset.filterBound = '1';
        window.jQuery.fn.dataTable.ext.search.push((settings, _d, dataIndex, row) => {
            if (settings.nTable !== tableEl) return true;
            const r = row || dt?.row(dataIndex)?.data?.();
            if (!r) return true;
            return matchesMulti(appliedFilters.status, r.status)
                && matchesMulti(appliedFilters.targetType, r.targetType)
                && matchesSingle(appliedFilters.source, r.source);
        });
    };
    const getAppliedFilterCount = () => [appliedFilters.status, appliedFilters.targetType, appliedFilters.source].filter(hasVal).length;

    const readControls = () => ({
        status: window.jQuery('#filterStatus').val() || [],
        targetType: window.jQuery('#filterTargetType').val() || [],
        source: document.getElementById('filterSource')?.value || ''
    });
    const writeControls = f => {
        window.jQuery('#filterStatus').val(normArr(f.status)).trigger('change');
        window.jQuery('#filterTargetType').val(normArr(f.targetType)).trigger('change');
        window.jQuery('#filterSource').val(f.source || '').trigger('change');
    };

    // ── target name resolution (HEDEF) ───────────────────────────────────────
    // Reuses the FREQ-C resolve.js pattern (READERS / mapOption / TARGET_KIND) — a NAME is shown, never a GUID; an
    // unresolved ref degrades to a short id (no fabricated name). resolve.js is NOT touched: the pattern is copied here.
    const READERS = {
        segment: () => getJson('/segments?pageSize=200'),
        account: () => getJson('/accounts?pageSize=200'),
        contact: () => getJson('/contacts?pageSize=200'),
        campaign: () => getJson('/campaigns?pageSize=200'),
        'concept-node': () => getJson('/concept-nodes?pageSize=200'),
        'audience-profile': () => getJson('/audience-profiles?pageSize=200')
    };
    const mapOption = x => ({
        value: x.id ?? x.value ?? x.valueCode ?? x.segmentId ?? x.accountId ?? x.contactId ?? x.campaignId
            ?? x.conceptNodeId ?? x.audienceProfileId ?? '',
        text: x.name || x.Name || x.text || x.displayName || x.DisplayName || x.label || x.Label
            || x.segmentName || x.SegmentName || x.accountName || x.AccountName || x.contactName || x.ContactName
            || x.fullName || x.FullName || x.campaignName || x.CampaignName || x.conceptName || x.ConceptName
            || x.audienceProfileName || x.profileName || x.code || x.Code
            || String(x.id ?? x.value ?? x.valueCode ?? '')
    });
    // targetType → entity-picker kind (anything unmapped falls back to a short id).
    const TARGET_KIND = {
        segment: 'segment', account: 'account', contact: 'contact',
        'campaign-target': 'campaign', 'concept-node': 'concept-node', 'audience-profile': 'audience-profile'
    };
    const optionCache = new Map();
    const loadOptions = async kind => {
        if (optionCache.has(kind)) return optionCache.get(kind);
        let options = [];
        try {
            const data = await (READERS[kind] ? READERS[kind]() : Promise.resolve([]));
            const items = Array.isArray(data) ? data : (data?.items || data?.nodes || data?.values || []);
            options = items.map(mapOption).filter(o => o.value !== '' && o.value != null);
        } catch (e) { options = []; }
        optionCache.set(kind, options);
        return options;
    };
    const targetNameByKey = new Map(); // `${kind}:${id}` → name
    const resolveAllTargetNames = async rows => {
        const kinds = new Set();
        (rows || []).forEach(r => { const k = TARGET_KIND[norm(r.targetType)]; if (k) kinds.add(k); });
        await Promise.all([...kinds].map(async kind => {
            const options = await loadOptions(kind);
            const byId = new Map(options.map(o => [String(o.value), o.text]));
            (rows || []).forEach(r => {
                if (TARGET_KIND[norm(r.targetType)] !== kind) return;
                const id = norm(r.targetId);
                const name = id ? byId.get(id) : '';
                if (name) targetNameByKey.set(`${kind}:${id}`, name);
            });
        }));
    };
    const targetName = row => {
        const kind = TARGET_KIND[norm(row.targetType)];
        return kind ? (targetNameByKey.get(`${kind}:${norm(row.targetId)}`) || '') : '';
    };

    // ── Save View (shared personalization; ported from EligibilityPolicies) ───
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
        } catch (e) { if (!e?.authHandled) console.error('[Vfp SaveView] load failed', e); }
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

    // ── create/edit navigation (WP-FREQ-F2: the editor is a SEPARATE page; the offcanvas is retired) ──────────
    const goCreate = () => { window.location.href = '/CRM/VisitFrequencyPolicies/Create'; };
    const goEdit = id => { if (id) window.location.href = `/CRM/VisitFrequencyPolicies/Edit/${id}`; };
    const openDetails = id => {
        const row = rowById[id];
        if (!row) return;
        const set = (elId, val) => { const el = document.getElementById(elId); if (el) el.textContent = val ?? '—'; };
        set('oc-title', row.policyName || row.policyCode || '—');
        set('oc-subtitle', row.policyCode || '—');
        const el = document.getElementById('offcanvasDetailsPreview');
        if (el && window.bootstrap) window.bootstrap.Offcanvas.getOrCreateInstance(el).show();
    };

    // ── row actions ──────────────────────────────────────────────────────────
    const actions = row => {
        const id = esc(row.policyId);
        rowById[row.policyId] = row;
        const items = [{ className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': id, title: L.QuickView || L.Details } }];
        items.push({ className: 'js-vfp-edit', icon: 'bx bx-edit', text: L.Edit, attrs: { 'data-id': id } });
        if (norm(row.status) !== 'archived') {
            items.push({ className: 'js-vfp-archive text-warning', icon: 'bx bx-archive-in', text: L.Archive, attrs: { 'data-id': id, 'data-name': esc(row.policyName) } });
        }
        items.push({ className: 'js-vfp-delete text-danger', icon: 'bx bx-trash', text: L.Delete, attrs: { 'data-id': id, 'data-name': esc(row.policyName) } });
        return window.DitenDataTable?.renderActions ? window.DitenDataTable.renderActions(items) : '';
    };

    // ── column renderers ─────────────────────────────────────────────────────
    // POLİTİKA — policy name (prominent) + policy code (muted).
    const policyCell = row => `<span class="fw-medium text-heading d-block">${esc(row.policyName || '—')}</span>`
        + (norm(row.policyCode) ? `<span class="text-muted small">${esc(row.policyCode)}</span>` : '');
    // HEDEF — target type chip + resolved NAME (degrades to a short id, never a fabricated name).
    const targetCell = row => {
        const label = humanize(row.targetType);
        const name = targetName(row);
        const nameHtml = name
            ? `<span class="fw-medium">${esc(name)}</span>`
            : `<span class="text-muted small">${esc(shortId(row.targetId))}</span>`;
        return badge(label, 'secondary') + `<div class="mt-1">${nameHtml}</div>`;
    };
    // FREKANS — "N / <localized period>" prominent + subtitle (dönem sınırlı [EffectiveTo set] / süresiz geçerli
    // [empty]). No frequencyType badge (WP-FREQ-F1 mockup: "2 / ay" + "süresiz geçerli").
    const frequencyCell = row => {
        const count = row.requiredVisitCount != null ? String(row.requiredVisitCount) : '';
        const period = periodLabel(row.periodType);
        const per = norm(L.PerPeriod) || '/';
        const line = [count, per, period].filter(s => norm(s)).join(' ');
        const sub = norm(row.effectiveTo) ? norm(L.FreqBounded) : norm(L.FreqOpenEnded);
        return (line ? `<span class="fw-medium text-heading d-block">${esc(line)}</span>` : '<span class="text-muted">—</span>')
            + (sub ? `<span class="text-muted small">${esc(sub)}</span>` : '');
    };
    // GEÇERLİLİK — EffectiveFrom → EffectiveTo | "süresiz", dd.MM.yyyy.
    const validityCell = row => {
        const from = fmtDate(row.effectiveFrom);
        const to = norm(row.effectiveTo) ? fmtDate(row.effectiveTo) : (norm(L.ValidityOpenEnded) || 'süresiz');
        return `<span>${esc(from || '—')}</span> <span class="text-muted">→</span> <span>${esc(to)}</span>`;
    };
    // AĞIRLIK — colored priority tier badge (5-tier tone map); raw weight ⇒ neutral badge fallback.
    const weightCell = row => {
        const label = weightLabel(row.priority);
        if (!label) return '—';
        const code = weightCode(row.priority);
        const tone = code ? (bandTones[code] || 'secondary') : 'secondary';
        return `<span class="badge bg-label-${tone}">${esc(label)}</span>`;
    };
    // KAYNAK — source label.
    const sourceCell = row => `<span>${esc(sourceLabel(row.source))}</span>`;

    const buildConfig = () => ({
        data: allRows, stateSave: false, processing: true,
        colReorder: { columns: ':gt(0):not(:last-child)' },
        order: baseOrder,
        columns: [
            { data: null, defaultContent: '' },
            { data: 'policyName' },
            { data: 'targetType' },
            { data: 'frequencyType' },
            { data: 'effectiveFrom' },
            { data: 'priority' },
            { data: 'source' },
            { data: 'status' },
            { data: null }
        ],
        columnDefs: [
            { targets: 0, className: 'control', orderable: false, render: () => '' },
            { targets: 1, render: (v, t, row) => t === 'display' ? policyCell(row) : `${norm(row.policyName)} ${norm(row.policyCode)}` },
            { targets: 2, orderable: false, render: (v, t, row) => t === 'display' ? targetCell(row) : `${humanize(row.targetType)} ${targetName(row)}` },
            { targets: 3, orderable: false, render: (v, t, row) => frequencyCell(row) },
            { targets: 4, render: (v, t, row) => t === 'display' ? validityCell(row) : norm(row.effectiveFrom) },
            { targets: 5, render: (v, t, row) => t === 'display' ? weightCell(row) : (t === 'sort' || t === 'type' ? (row.priority == null ? '' : Number(row.priority)) : weightLabel(row.priority)) },
            { targets: 6, render: (v, t, row) => t === 'display' ? sourceCell(row) : sourceLabel(row.source) },
            { targets: 7, render: v => badge(statusLabel(v), statusTone(v)) },
            { targets: 8, title: L.Actions, orderable: false, searchable: false, className: 'cell-fit text-end pe-3 all', render: (v, t, row) => actions(row) }
        ],
        language: { emptyTable: L.EmptyState, processing: L.Loading },
        buttons: window.DtDefaults.exportButtons(L.NewPolicy, {}, {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => toggleInlineFilter()
            },
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
            const api = this.api();
            mountInlineFilter(api);
            bindInlineFilterA11y(api);
            void setupFilters(api);
            if (!addNewBound) {
                nodeContainer(api).querySelector('.add-new')?.addEventListener('click', e => { e.preventDefault(); goCreate(); });
                addNewBound = true;
            }
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

    const loadRows = async () =>
        (await envelope(await fetch(`${endpoint}/visit-frequency-policies`, { credentials: 'same-origin', headers: getAuthHeaders() })))?.items || [];

    const init = async () => {
        document.getElementById('skeleton-loader')?.classList.remove('d-none');
        registerTableFilter();
        try {
            await loadDefaultView();
            allRows = await loadRows();
            await resolveAllTargetNames(allRows);
            dt = new DataTable(tableEl, window.DtDefaults?.create ? window.DtDefaults.create(buildConfig()) : buildConfig());
            dt.on('column-visibility.dt search.dt order.dt column-reorder.dt columns-reordered.dt', () => {
                window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount());
                if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
            });
        } catch (error) {
            const host = document.getElementById('policyError');
            if (host) { host.textContent = error.message || L.ErrorState; host.classList.remove('d-none'); }
            window.showToast?.(error.message || L.ErrorState, 'error');
        } finally {
            document.getElementById('skeleton-loader')?.classList.add('d-none');
        }
    };

    const postAndReload = async (path, okMsg) => {
        try {
            await envelope(await fetch(`${endpoint}${path}`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }));
            window.showToast?.(okMsg, 'success');
            allRows = await loadRows();
            await resolveAllTargetNames(allRows);
            if (dt) { dt.clear(); dt.rows.add(allRows).draw(false); }
        } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
    };

    document.addEventListener('click', event => {
        const details = event.target.closest('.js-quick-view');
        if (details) { event.preventDefault(); openDetails(details.dataset.id); return; }

        const edit = event.target.closest('.js-vfp-edit');
        if (edit) { event.preventDefault(); goEdit(edit.dataset.id); return; }

        const archive = event.target.closest('.js-vfp-archive');
        if (archive) {
            event.preventDefault();
            window.showConfirm?.(L.ArchiveConfirm, () => postAndReload(`/visit-frequency-policies/${archive.dataset.id}/archive`, L.RecordArchived),
                { entityName: archive.dataset.name, type: 'warning', confirmButtonText: L.Archive });
            return;
        }

        const del = event.target.closest('.js-vfp-delete');
        if (!del) return;
        event.preventDefault();
        window.showConfirm?.(L.DeleteConfirm, () => postAndReload(`/visit-frequency-policies/${del.dataset.id}/delete`, L.RecordDeleted),
            { entityName: del.dataset.name, type: 'danger', confirmButtonText: L.Delete });
    });

    init();
})(window, document);
