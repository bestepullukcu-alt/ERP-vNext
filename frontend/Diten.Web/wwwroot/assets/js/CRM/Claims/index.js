/**
 * WP-CL-FE-1 — Claims v2 list (mockup scenario 1 / 12 / 13), DataTables v2 (Golden aligned):
 *  - one list read (/CRM/Claims/api/v2/claims?includeCounts=true) + lookups (countries, audience profiles, closure
 *    reasons); counters that CRM could not compute arrive as null and render "—" (never guessed)
 *  - country status chips in COUNTRY_CODES order (mockup colour language mapped onto theme variables, claims-index.css)
 *  - inline filter (product / country / language / status / audience / core-local / archived) + three work flags
 *  - SaveView (filter + search + colvis + colorder) via personalizationClient
 *  - quick view offcanvas: the row + its country versions (one read on open)
 *  - row actions: view · edit (draft only) · new version (approved / needs review) · archive. There is NO approve:
 *    a claim is approved only through its MOD-0023 round (submit-review).
 */
(function (window, document) {
    'use strict';
    const tableEl = document.getElementById('dt-claims');
    if (!tableEl) return;

    const api = '/CRM/Claims/api/v2';
    const filterCollapseId = 'inlineFilterCollapse';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'CRM', pageKey: 'Claims' };
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7, 8];
    const totalColumnCount = 10;
    const baseOrder = [[1, 'asc']];
    const CLAIM_STATES = ['approved', 'in-review', 'draft', 'review-required', 'expiring', 'closed', 'not-opened'];
    const STATE_ICONS = {
        'approved': 'bx-check-circle', 'in-review': 'bx-time-five', 'draft': 'bx-edit-alt', 'review-required': 'bx-revision',
        'expiring': 'bx-alarm-exclamation', 'closed': 'bx-block', 'not-opened': 'bx-plus-circle', 'not-applicable': 'bx-minus',
        'inactive': 'bx-archive', 'archived': 'bx-archive'
    };

    const flags = (() => { try { return JSON.parse(document.getElementById('claim-page-flags')?.textContent || '{}'); } catch (e) { return {}; } })();
    const canManage = flags.canManage === true;

    let L = window.ClaimL10n || window.L10n || {};
    let dt = null;
    let saveFilterArmed = false;
    let defaultViewRecord = null;
    let defaultViewState = null;
    const emptyFilters = () => ({ product: [], country: '', language: [], status: [], audience: [], kind: '', includeArchived: 'false', flag: '' });
    let appliedFilters = emptyFilters();
    let allRows = [];
    let countries = [];          // [{ code, name, languages[] }] — COUNTRY_CODES order
    let audienceNames = {};      // audienceProfileId → name
    let closureReasons = {};     // value_code → display name (tenant BRD)
    let orgUnitNames = null;     // lazily loaded for the quick view

    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const normArr = v => Array.isArray(v) ? Array.from(new Set(v.map(x => norm(x)).filter(Boolean))) : (norm(v) ? [norm(v)] : []);
    const hasVal = v => Array.isArray(v) ? normArr(v).length > 0 : norm(v).length > 0;
    const fmt = (template, ...args) => String(template ?? '').replace(/\{(\d+)\}/g, (_, i) => args[Number(i)] ?? '');
    const stateLabel = s => L['State_' + s] || s || '—';
    const reasonLabel = code => closureReasons[code] || code || '';
    const isLive = r => !r.isArchived && r.status !== 'inactive';

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) {
            const errors = Array.isArray(body.errors) ? body.errors : [];
            // CRM refusals are [code, message]: show the message, keep the code for callers.
            const message = errors.length > 1 ? errors.slice(1).join(' · ') : (errors[0] || body.message || L.ErrorState);
            throw Object.assign(new Error(message), { status: response.status, code: errors[0] });
        }
        return body.data;
    };
    const getJson = async url => envelope(await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } }));
    const tryJson = async url => { try { return await getJson(url); } catch (e) { return null; } };

    // ─── Row model (derived once per load) ──────────────────────────────────────
    const chipsFor = row => {
        const summary = {};
        (row.countrySummary || []).forEach(s => { summary[norm(s.countryCode).toUpperCase()] = s.state; });
        const closures = {};
        (row.countryClosures || []).filter(c => c.isActive).forEach(c => { closures[norm(c.countryCode).toUpperCase()] = c.reasonCode; });
        const expiring = new Set((row.expiringCountryCodes || []).map(c => norm(c).toUpperCase()));
        const local = row.kind === 'local' ? norm(row.localCountryCode).toUpperCase() : null;
        return countries.map(c => {
            let state;
            let title;
            if (local && c.code !== local) { state = 'not-applicable'; title = stateLabel(state); }
            else if (closures[c.code] !== undefined) { state = 'closed'; title = fmt(L.ClosedTooltip, reasonLabel(closures[c.code])); }
            else if (summary[c.code]) {
                state = summary[c.code] === 'approved' && expiring.has(c.code) ? 'expiring' : summary[c.code];
                title = stateLabel(state);
            } else { state = 'not-opened'; title = L.NotOpenedTooltip; }
            return { code: c.code, name: c.name, state, title: `${c.name}: ${title}` };
        });
    };

    const decorate = row => {
        row.kind = row.kind === 'local' ? 'local' : 'core';
        row.chips = chipsFor(row);
        row.audienceNames = (row.audienceProfileIds || []).map(id => audienceNames[id] || '').filter(Boolean);
        row.languages = Array.from(new Set([norm(row.textLanguageCode).toLowerCase()].concat(
            row.chips.filter(ch => !['not-applicable', 'not-opened', 'closed'].includes(ch.state))
                .flatMap(ch => (countries.find(c => c.code === ch.code)?.languages || []))).filter(Boolean)));
        row.flagEvidence = row.evidenceCount === 0;
        row.flagReview = row.status === 'review-required' || row.chips.some(ch => ch.state === 'review-required');
        row.flagExpiring = row.evidenceExpiring === true || (row.expiringCountryCodes || []).length > 0;
        row.searchText = [row.claimCode, row.claimName, row.claimText, row.productDisplay].filter(Boolean).join(' ');
        return row;
    };

    // ─── Renderers ──────────────────────────────────────────────────────────────
    const chipHtml = ch => {
        const dim = appliedFilters.country && appliedFilters.country !== ch.code ? ' is-dimmed' : '';
        const text = ch.state === 'not-applicable' ? '—' : esc(ch.code);
        return `<span class="claim-chip state-${esc(ch.state)}${dim}" title="${esc(ch.title)}" data-bs-toggle="tooltip">`
            + `<i class="bx ${STATE_ICONS[ch.state] || 'bx-circle'}"></i>${text}</span>`;
    };
    const claimCell = row => {
        const sub = row.kind === 'core'
            ? fmt(L.CoreVersionLine, esc(row.claimVersion))
            : fmt(L.LocalLine, esc(countries.find(c => c.code === norm(row.localCountryCode).toUpperCase())?.name || row.localCountryCode || ''));
        const status = row.status && row.status !== 'approved'
            ? ` <span class="claim-chip state-${esc(row.status)} ms-1">${esc(stateLabel(row.status))}</span>` : '';
        return `<div class="d-flex flex-column"><span class="text-muted small">${esc(row.claimCode)}</span>`
            + `<span class="fw-medium text-heading">${esc(row.claimName)}${status}</span>`
            + `<span class="claim-sub" title="${esc(row.claimText)}">${sub}</span></div>`;
    };
    const kindBadge = row => row.kind === 'local'
        ? `<span class="badge rounded-pill claim-kind-local"><i class="bx bx-map-pin me-1"></i>${esc(L.KindLocal)}</span>`
        : `<span class="badge rounded-pill claim-kind-core"><i class="bx bx-globe me-1"></i>${esc(L.KindCore)}</span>`;
    const unavailable = () => `<span class="text-muted" title="${esc(L.CounterUnavailable)}">—</span>`;
    const evidenceCell = row => row.evidenceCount == null ? unavailable()
        : row.evidenceCount === 0 ? `<span class="claim-evidence-missing">0 · ${esc(L.EvidenceMissing)}</span>`
            : `<span>${esc(row.evidenceCount)}</span>`;
    const approvedCell = row => row.approvedCountryCount == null ? unavailable()
        : esc(fmt(L.ApprovedOfCountries, row.approvedCountryCount,
            row.kind === 'local' ? 1 : countries.length));
    const usageCell = row => row.usageCount == null ? unavailable() : esc(row.usageCount);

    // ─── Select2 filter helpers (Golden) ────────────────────────────────────────
    const fillSelect = (id, options, withAll) => {
        const el = document.getElementById(id);
        if (!el) return;
        const head = withAll ? `<option value="">${esc(L.ShowAll || '')}</option>` : '';
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
            const $clear = window.jQuery('<span class="dt-multi-clear-btn" role="button" aria-label="' + esc(L.Reset) + '" title="' + esc(L.Reset) + '">&times;</span>');
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
                    placeholder: $s.data('placeholder') || '', minimumResultsForSearch: 8, width: 'element', closeOnSelect: false
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
    const uniqueOptions = pairs => Array.from(new Map(pairs.filter(p => p.value).map(p => [p.value, p])).values())
        .sort((a, b) => a.text.localeCompare(b.text));
    const loadFilterOptions = () => {
        fillSelect('filterProduct', uniqueOptions(allRows.map(r => ({ value: norm(r.productId || r.productDisplay), text: norm(r.productDisplay) || norm(r.productId) }))), false);
        fillSelect('filterCountry', countries.map(c => ({ value: c.code, text: `${c.name} (${c.code})` })), true);
        fillSelect('filterLanguage', uniqueOptions(allRows.flatMap(r => r.languages).map(l => ({ value: l, text: l }))), false);
        fillSelect('filterStatus', CLAIM_STATES.map(s => ({ value: s, text: stateLabel(s) })), false);
        fillSelect('filterAudience', uniqueOptions(allRows.flatMap(r => (r.audienceProfileIds || []).map(id => ({ value: id, text: audienceNames[id] || id })))), false);
        fillSelect('filterKind', [{ value: 'core', text: L.KindCore }, { value: 'local', text: L.KindLocal }], true);
        initSelect2();
    };

    // ─── Inline filter mount / toggle (Golden) ─────────────────────────────────
    const nodeContainer = apiRef => { try { return apiRef.table().container(); } catch (e) { return document; } };
    const mountInlineFilter = apiRef => {
        const host = document.getElementById('inlineFilterHost');
        const filterBtn = nodeContainer(apiRef).querySelector('.dt-filter-btn');
        const toolbarRow = filterBtn?.closest('.dt-layout-row') || filterBtn?.closest('.row') || filterBtn?.closest('.dt-layout-end')?.parentElement;
        if (host && toolbarRow) { toolbarRow.insertAdjacentElement('afterend', host); host.classList.remove('px-6'); host.classList.add('px-3'); }
    };
    const toggleInlineFilter = () => {
        const el = document.getElementById(filterCollapseId);
        if (el) window.bootstrap?.Collapse.getOrCreateInstance(el, { toggle: false }).toggle();
    };
    const bindInlineFilterA11y = apiRef => {
        const btn = nodeContainer(apiRef).querySelector('.dt-filter-btn');
        const el = document.getElementById(filterCollapseId);
        if (!btn || !el || btn.dataset.bound) return;
        btn.dataset.bound = '1';
        el.addEventListener('shown.bs.collapse', () => btn.setAttribute('aria-expanded', 'true'));
        el.addEventListener('hidden.bs.collapse', () => btn.setAttribute('aria-expanded', 'false'));
    };

    // ─── Client-side filter (scoped to THIS table — memory updatevisualstate-global-selectors) ─────────────────
    const anyOf = (selected, values) => { const s = normArr(selected); return !s.length || values.some(v => s.includes(norm(v))); };
    const matches = r => {
        const f = appliedFilters;
        if (f.includeArchived !== 'true' && !isLive(r)) return false;
        if (!anyOf(f.product, [r.productId || r.productDisplay])) return false;
        if (f.country && r.chips.find(ch => ch.code === f.country)?.state === 'not-applicable') return false;
        if (!anyOf(f.language, r.languages)) return false;
        if (normArr(f.status).length) {
            const scope = f.country ? r.chips.filter(ch => ch.code === f.country) : r.chips;
            if (!anyOf(f.status, scope.map(ch => ch.state).concat(f.country ? [] : [r.status]))) return false;
        }
        if (!anyOf(f.audience, r.audienceProfileIds || [])) return false;
        if (f.kind && r.kind !== f.kind) return false;
        if (f.flag === 'evidence' && !r.flagEvidence) return false;
        if (f.flag === 'review' && !r.flagReview) return false;
        if (f.flag === 'expiring' && !r.flagExpiring) return false;
        return true;
    };
    const registerTableFilter = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || tableEl.dataset.filterBound === '1') return;
        tableEl.dataset.filterBound = '1';
        window.jQuery.fn.dataTable.ext.search.push((settings, _d, dataIndex, row) => {
            if (settings.nTable !== tableEl) return true;
            const r = row || dt?.row(dataIndex)?.data?.();
            return !r || matches(r);
        });
    };
    const getAppliedFilterCount = () => ['product', 'country', 'language', 'status', 'audience', 'kind']
        .filter(k => hasVal(appliedFilters[k])).length + (appliedFilters.includeArchived === 'true' ? 1 : 0) + (appliedFilters.flag ? 1 : 0);

    const readControls = () => ({
        product: window.jQuery('#filterProduct').val() || [],
        country: document.getElementById('filterCountry')?.value || '',
        language: window.jQuery('#filterLanguage').val() || [],
        status: window.jQuery('#filterStatus').val() || [],
        audience: window.jQuery('#filterAudience').val() || [],
        kind: document.getElementById('filterKind')?.value || '',
        includeArchived: document.getElementById('filterIncludeArchived')?.value || 'false',
        flag: appliedFilters.flag || ''
    });
    const writeControls = f => {
        ['Product', 'Language', 'Status', 'Audience'].forEach(k => window.jQuery('#filter' + k).val(normArr(f[k.toLowerCase()])).trigger('change'));
        window.jQuery('#filterCountry').val(f.country || '').trigger('change');
        window.jQuery('#filterKind').val(f.kind || '').trigger('change');
        window.jQuery('#filterIncludeArchived').val(f.includeArchived || 'false').trigger('change');
        syncFlagBar();
    };

    // ─── Work flags (evidence missing / needs review / expiring) ────────────────
    const syncFlagBar = () => {
        const live = allRows.filter(r => appliedFilters.includeArchived === 'true' || isLive(r));
        const counts = { evidence: live.filter(r => r.flagEvidence).length, review: live.filter(r => r.flagReview).length, expiring: live.filter(r => r.flagExpiring).length };
        document.querySelectorAll('#claimFlagBar .claim-flag').forEach(btn => {
            const key = btn.dataset.flag;
            const active = appliedFilters.flag === key;
            btn.classList.toggle('active', active);
            btn.setAttribute('aria-pressed', active ? 'true' : 'false');
            const badge = btn.querySelector('[data-flag-count]');
            if (badge) badge.textContent = String(counts[key] ?? 0);
        });
    };

    // ─── SaveView (personalization) ────────────────────────────────────────────
    const captureColVis = apiRef => { const r = {}; saveViewColumnIndexes.forEach(ci => { try { r[ci] = !!apiRef.column(ci).visible(); } catch (e) { } }); return r; };
    const captureColOrder = apiRef => { try { const o = apiRef?.colReorder?.order?.(); return Array.isArray(o) && o.length === totalColumnCount ? o.map(Number) : null; } catch (e) { return null; } };
    const applyColVis = (apiRef, cv) => { if (!cv) return; saveViewColumnIndexes.forEach(ci => { if (typeof cv[ci] === 'boolean') { try { apiRef.column(ci).visible(cv[ci], false); } catch (e) { } } }); };
    const applyColOrder = (apiRef, co) => { if (!Array.isArray(co) || co.length !== totalColumnCount || typeof apiRef?.colReorder?.order !== 'function') return; try { apiRef.colReorder.order(co, true); } catch (e) { } };
    const defaultColVis = () => saveViewColumnIndexes.reduce((a, ci) => { a[ci] = true; return a; }, {});
    const currentView = apiRef => ({ filters: Object.assign({}, appliedFilters), search: norm(apiRef.search()), colVis: captureColVis(apiRef), columnOrder: captureColOrder(apiRef), order: apiRef.order() });
    const serializeView = v => JSON.stringify({
        filters: Object.keys(v?.filters || {}).sort().reduce((a, k) => { a[k] = Array.isArray(v.filters[k]) ? normArr(v.filters[k]).slice().sort() : norm(v.filters[k]); return a; }, {}),
        search: norm(v?.search), colVis: v?.colVis || defaultColVis(),
        columnOrder: Array.isArray(v?.columnOrder) ? v.columnOrder : Array.from({ length: totalColumnCount }, (_, i) => i),
        order: Array.isArray(v?.order) ? v.order : baseOrder
    });
    const getResetBaselineState = () => ({ filters: emptyFilters(), search: '', colVis: defaultColVis(), columnOrder: Array.from({ length: totalColumnCount }, (_, i) => i), order: baseOrder });
    const setSaveFilterVisible = show => { const b = dt ? nodeContainer(dt).querySelector('.dt-save-filter-btn') : null; if (!b) return; b.classList.toggle('d-none', !show); window.DtDefaults?.refreshButtonGroupRadii?.(); };
    const isDirtyComparedToDefault = apiRef => serializeView(currentView(apiRef)) !== serializeView(defaultViewState || getResetBaselineState());
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
        } catch (e) { if (!e?.authHandled) console.error('[Claim SaveView] load failed', e); }
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
    const applySavedTableState = (apiRef, view) => {
        const v = view || getResetBaselineState();
        appliedFilters = Object.assign(emptyFilters(), v.filters || {});
        writeControls(appliedFilters);
        applyColOrder(apiRef, v.columnOrder);
        applyColVis(apiRef, v.colVis);
        apiRef.search(v.search || '');
        apiRef.order(v.order || baseOrder);
        apiRef.draw(false);
        window.DtDefaults?.updateVisualState?.(apiRef, getAppliedFilterCount());
    };
    const refreshAfterFilterChange = () => {
        if (!dt) return;
        dt.draw();
        syncFlagBar();
        window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount());
        if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
    };
    const clearAllFilters = () => { if (dt) { applySavedTableState(dt, getResetBaselineState()); syncFlagBar(); if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt)); } };

    // ─── Row actions (Golden: primary quick view + "…" dropdown) ────────────────
    const rowById = {};
    const actions = row => {
        const id = esc(row.claimId);
        rowById[row.claimId] = row;
        const items = [{ className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': id, title: L.QuickView } }];
        if (canManage && row.status === 'draft' && !row.isArchived) {
            items.push({ className: 'js-edit-claim', icon: 'bx bx-edit', text: L.EditClaim, attrs: { 'data-id': id } });
        }
        if (canManage && !row.isArchived && (row.status === 'approved' || row.status === 'review-required')) {
            items.push({ className: 'js-new-version', icon: 'bx bx-git-branch', text: L.NewVersion, attrs: { 'data-id': id, 'data-name': esc(row.claimName) } });
        }
        if (canManage && !row.isArchived) {
            items.push({ className: 'js-archive-claim text-danger', icon: 'bx bx-archive-in', text: L.ArchiveClaim, attrs: { 'data-id': id, 'data-name': esc(row.claimName) } });
        }
        return window.DitenDataTable?.renderActions ? window.DitenDataTable.renderActions(items) : '';
    };

    // ─── Quick view ─────────────────────────────────────────────────────────────
    const loadOrgUnitNames = async () => {
        if (orgUnitNames) return orgUnitNames;
        const data = await tryJson(`${api}/lookups/org-units`);
        const items = Array.isArray(data) ? data : (data?.items || []);
        orgUnitNames = {};
        items.forEach(u => { if (u?.id) orgUnitNames[u.id] = u.name || u.code || u.id; });
        return orgUnitNames;
    };
    const versionRow = v => {
        const country = countries.find(c => c.code === norm(v.countryCode).toUpperCase());
        const state = v.status === 'approved' && v.isExpiring ? 'expiring' : v.status;
        const text = (v.texts || [])[0]?.text || '—';
        const langs = (v.texts || []).map(t => t.languageCode).join(' · ');
        const note = v.adaptationReason ? `<small class="text-muted d-block mt-1">${esc(v.adaptationReason)}</small>` : '';
        return `<div class="claim-version-row"><div class="d-flex justify-content-between align-items-center gap-2 mb-1">`
            // WP-CL-FIX-1 — the country version opens its page (read-only for a reader or a locked version).
            + `<a class="fw-medium" href="/CRM/Claims/CountryVersions/${encodeURIComponent(v.countryVersionId)}/Edit">${esc(country?.name || v.countryCode)} <span class="text-muted">v${esc(v.version)}</span></a>`
            + `<span class="claim-chip state-${esc(state)}"><i class="bx ${STATE_ICONS[state] || 'bx-circle'}"></i>${esc(stateLabel(state))}</span></div>`
            + `<small class="text-muted d-block mb-1">${esc(L.QvLanguages)}: ${esc(langs || '—')}</small>`
            + `<div class="claim-version-text">${esc(text)}</div>${note}</div>`;
    };
    const closedRow = ch => `<div class="claim-version-row"><div class="d-flex justify-content-between align-items-center gap-2">`
        + `<span class="fw-medium">${esc(ch.name)}</span>${chipHtml(ch)}</div>`
        + `<small class="text-muted d-block mt-1">${esc(ch.title)}</small></div>`;

    // WP-KP-4 — the usage list of the claim code: contents · knowledge paths (the retired content set is no longer a
    // source) · journeys, grouped by country. A knowledge path links to its studio workspace, a journey to its detail.
    const USAGE_LINKS = {
        'knowledge-path': id => `/CRM/KnowledgePaths/${encodeURIComponent(id)}`,
        journey: id => `/CRM/ContentEngagementJourneys/Details/${encodeURIComponent(id)}`
    };
    const usageType = type => L['UsageType_' + type] || L.Unknown;
    const usageItem = item => {
        const link = USAGE_LINKS[item.type];
        const title = esc(item.name || item.code);
        return `<li class="d-flex align-items-start gap-2 py-1">`
            + `<span class="badge bg-label-secondary flex-shrink-0">${esc(usageType(item.type))}</span>`
            + `<div class="min-w-0"><div class="fw-medium text-break">${link ? `<a href="${esc(link(item.id))}">${title}</a>` : title}</div>`
            + `<small class="text-muted">${esc([item.code, item.version ? 'v' + item.version : '', item.via ? fmt(L.UsageVia, item.via) : ''].filter(Boolean).join(' · '))}</small>`
            + (item.claimNeedsReview ? `<small class="d-block text-warning">${esc(L.UsageNeedsReview)}</small>` : '')
            + '</div></li>';
    };
    const loadUsage = async claimCode => {
        const host = document.getElementById('pvUsageList');
        if (!host) return;
        host.innerHTML = `<div class="text-muted small">${esc(L.Loading)}</div>`;
        try {
            const usage = await getJson(`${api}/claims/usage?claimCode=${encodeURIComponent(claimCode)}`);
            const groups = Array.isArray(usage?.groups) ? usage.groups : [];
            host.innerHTML = groups.length === 0
                ? `<div class="text-muted small">${esc(L.QvUsageNone)}</div>`
                : groups.map(g => {
                    const country = g.countryCode === 'GLOBAL' ? L.UsageGlobal : (countries.find(c => c.code === g.countryCode)?.name || g.countryCode);
                    return `<div class="claim-version-row"><div class="fw-medium mb-1">${esc(country)}</div>`
                        + `<ul class="list-unstyled mb-0">${(g.items || []).map(usageItem).join('')}</ul></div>`;
                }).join('');
        } catch (error) {
            host.innerHTML = `<div class="text-danger small">${esc(L.QvUsageFailed)}</div>`;
        }
    };

    const openClaimPreview = async id => {
        const row = rowById[id];
        if (!row) return;
        const set = (elId, val) => { const el = document.getElementById(elId); if (el) el.textContent = val ?? '—'; };
        const kindEl = document.getElementById('claimPreviewKind');
        if (kindEl) { kindEl.className = 'badge rounded-pill ' + (row.kind === 'local' ? 'claim-kind-local' : 'claim-kind-core'); kindEl.textContent = row.kind === 'local' ? L.KindLocal : L.KindCore; }
        set('claimPreviewCode', row.claimCode);
        set('claimPreviewTitle', row.claimName || row.claimCode);
        set('claimPreviewSubtitle', row.kind === 'core' ? fmt(L.CoreVersionLine, row.claimVersion) : fmt(L.LocalLine, row.localCountryCode || ''));
        set('pvProduct', row.productDisplay || '—');
        set('pvStatus', stateLabel(row.status));
        set('pvTextLabel', row.kind === 'local' ? L.QvLocalText : L.QvCoreText);
        set('pvText', row.claimText || '—');
        set('pvQualifiers', (row.qualifiers || []).join(' · ') || '—');
        set('pvEvidence', row.evidenceCount == null ? '—' : String(row.evidenceCount));
        set('pvUsage', row.usageCount == null ? '—' : String(row.usageCount));
        set('pvAudience', String((row.audienceProfileIds || []).length));
        set('pvTeam', '—');
        const open = document.getElementById('claimPreviewOpen');
        if (open) {
            // The detail page arrives with WP-CL-FE-6; until then "open details" is the edit page for a draft, else hidden.
            const editable = canManage && row.status === 'draft' && !row.isArchived;
            open.classList.toggle('d-none', !editable);
            open.setAttribute('href', editable ? `/CRM/Claims/Edit/${encodeURIComponent(id)}` : '#');
        }
        const host = document.getElementById('pvVersions');
        if (host) host.innerHTML = `<div class="text-muted small">${esc(L.Loading)}</div>`;
        const el = document.getElementById('offcanvasDetailsPreview');
        if (el && window.bootstrap) window.bootstrap.Offcanvas.getOrCreateInstance(el).show();

        if (row.responsibleOrgUnitId) {
            loadOrgUnitNames().then(names => set('pvTeam', names[row.responsibleOrgUnitId] || '—'));
        }
        try {
            const versions = await getJson(`${api}/claims/${encodeURIComponent(id)}/country-versions?includeArchived=false`) || [];
            const live = versions.filter(v => v.status !== 'inactive');
            const closed = row.chips.filter(ch => ch.state === 'closed');
            const html = live.map(versionRow).concat(closed.map(closedRow));
            if (host) host.innerHTML = html.length ? html.join('') : `<div class="text-muted small">${esc(L.QvNoCountryVersions)}</div>`;
        } catch (error) {
            if (host) host.innerHTML = `<div class="text-danger small">${esc(L.QvLoadFailed)}</div>`;
        }
        await loadUsage(row.claimCode);
    };

    // ─── Table ──────────────────────────────────────────────────────────────────
    const noMatchHtml = () => `<div class="text-center py-4"><h6 class="mb-1">${esc(L.NoMatchTitle)}</h6>`
        + `<p class="text-muted mb-3">${esc(L.NoMatchText)}</p>`
        + `<button type="button" class="btn btn-sm btn-label-primary js-clear-filters">${esc(L.ClearFilters)}</button></div>`;

    const buildConfig = () => ({
        data: allRows, stateSave: false, processing: true,
        colReorder: { columns: ':gt(0):not(:last-child)' },
        order: baseOrder,
        createdRow: tr => tr.classList.add('claim-row'),
        columns: [
            { data: null, defaultContent: '' },
            { data: 'claimCode' }, { data: 'productDisplay' }, { data: 'kind' }, { data: 'audienceNames' },
            { data: 'chips' }, { data: 'evidenceCount' }, { data: 'approvedCountryCount' }, { data: 'usageCount' }, { data: null }
        ],
        columnDefs: [
            { targets: 0, className: 'control', orderable: false, render: () => '' },
            { targets: 1, render: (v, t, row) => t === 'display' ? claimCell(row) : `${row.claimCode} ${row.claimName} ${row.claimText || ''}` },
            { targets: 2, render: v => esc(v || '—') },
            { targets: 3, render: (v, t, row) => t === 'display' ? kindBadge(row) : (row.kind === 'local' ? L.KindLocal : L.KindCore) },
            { targets: 4, render: (v, t) => t === 'display' ? esc((v || []).join(', ') || '—') : (v || []).join(', ') },
            { targets: 5, orderable: false, render: (v, t) => t === 'display' ? `<div class="claim-chips">${(v || []).map(chipHtml).join('')}</div>` : (v || []).map(ch => `${ch.code}: ${stateLabel(ch.state)}`).join(', ') },
            { targets: 6, render: (v, t, row) => t === 'display' ? evidenceCell(row) : (v ?? '') },
            { targets: 7, render: (v, t, row) => t === 'display' ? approvedCell(row) : (v ?? '') },
            { targets: 8, render: (v, t, row) => t === 'display' ? usageCell(row) : (v ?? '') },
            { targets: 9, title: L.Actions, orderable: false, searchable: false, className: 'cell-fit text-end pe-3 all', render: (v, t, row) => actions(row) }
        ],
        language: { emptyTable: noMatchHtml(), zeroRecords: noMatchHtml(), processing: L.Loading },
        buttons: window.DtDefaults.exportButtons(canManage ? L.NewCoreClaim : null, canManage ? { href: '/CRM/Claims/Create?kind=core' } : null, {
            filterBtn: { text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>', className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative', attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' }, action: () => toggleInlineFilter() },
            saveFilterBtn: {
                text: '<i class="icon-base bx bx-save icon-sm"></i><span class="ms-2 d-none d-lg-inline-block">' + esc(L.SaveView || '') + '</span>',
                className: 'btn btn-label-primary d-none dt-save-filter-btn', attr: { title: L.SaveView, 'data-bs-toggle': 'tooltip' },
                action: async function (e, apiRef) {
                    try { await saveDefaultView(currentView(apiRef || dt)); setSaveFilterVisible(false); window.showToast?.(L.SaveView || '', 'success'); }
                    catch (err) { if (!err?.authHandled) { console.error(err); window.showToast?.(L.ErrorState, 'error'); } }
                }
            }
        }, { exportColumns: saveViewColumnIndexes, colvisColumns: saveViewColumnIndexes }),
        initComplete: function () {
            const apiRef = this.api();
            mountInlineFilter(apiRef);
            bindInlineFilterA11y(apiRef);
            void setupFilters(apiRef);
            // The toolbar "new" button is the core claim; the local one lives in the page header (manage only).
            nodeContainer(apiRef).querySelector('.add-new')?.addEventListener('click', e => { e.preventDefault(); window.location.href = '/CRM/Claims/Create?kind=core'; });
            if (!canManage) nodeContainer(apiRef).querySelector('.add-new')?.remove();
            setTimeout(() => { saveFilterArmed = true; }, 0);
        },
        drawCallback: function () {
            window.DtDefaults?.updateVisualState?.(this.api(), getAppliedFilterCount());
            if (window.bootstrap?.Tooltip) {
                tableEl.querySelectorAll('.claim-chip[data-bs-toggle="tooltip"]').forEach(n => window.bootstrap.Tooltip.getOrCreateInstance(n));
            }
        }
    });

    const setupFilters = async apiRef => {
        loadFilterOptions();
        try { apiRef.rows().invalidate().draw(false); } catch (e) { /* table not ready */ }
        applySavedTableState(apiRef, defaultViewState);
        syncFlagBar();
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = readControls();
            refreshAfterFilterChange();
            const el = document.getElementById(filterCollapseId);
            if (el) window.bootstrap?.Collapse.getOrCreateInstance(el, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', e => { e.preventDefault(); clearAllFilters(); });
    };

    const loadLookups = async () => {
        const [countryData, profiles, reasons] = await Promise.all([
            tryJson(`${api}/lookups/countries`),
            tryJson(`${api}/lookups/audience-profiles?includeArchived=true`),
            tryJson(`${api}/lookups/closure-reasons`)
        ]);
        countries = Array.isArray(countryData) ? countryData.map(c => ({ code: norm(c.code).toUpperCase(), name: c.name || c.code, languages: c.languages || [] })) : [];
        const profileItems = Array.isArray(profiles) ? profiles : (profiles?.items || []);
        audienceNames = {};
        profileItems.forEach(p => { if (p?.audienceProfileId) audienceNames[p.audienceProfileId] = p.profileName || p.profileCode; });
        closureReasons = {};
        (Array.isArray(reasons) ? reasons : []).forEach(r => { if (r?.code) closureReasons[r.code] = r.name || r.code; });
    };

    const loadRows = async () => ((await getJson(`${api}/claims?includeArchived=true&includeCounts=true`))?.items || []).map(decorate);

    const showEmptyLibrary = empty => {
        document.getElementById('claimEmptyLibrary')?.classList.toggle('d-none', !empty);
        document.getElementById('claimTableCard')?.classList.toggle('d-none', empty);
        document.getElementById('claimFlagBar')?.classList.toggle('d-none', empty);
    };

    const init = async () => {
        document.getElementById('skeleton-loader')?.classList.remove('d-none');
        registerTableFilter();
        try {
            await Promise.all([loadDefaultView(), loadLookups()]);
            allRows = await loadRows();
            showEmptyLibrary(allRows.length === 0);
            dt = new DataTable(tableEl, window.DtDefaults?.create ? window.DtDefaults.create(buildConfig()) : buildConfig());
            dt.on('column-visibility.dt search.dt order.dt column-reorder.dt columns-reordered.dt', () => {
                window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount());
                if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
            });
        } catch (error) {
            const host = document.getElementById('claimError');
            if (host) { host.textContent = error.message || L.ErrorState; host.classList.remove('d-none'); }
            window.showToast?.(error.message || L.ErrorState, 'error');
        } finally {
            document.getElementById('skeleton-loader')?.classList.add('d-none');
        }
    };

    const reload = async () => {
        allRows = await loadRows();
        showEmptyLibrary(allRows.length === 0);
        if (dt) { dt.clear(); dt.rows.add(allRows).draw(false); }
        syncFlagBar();
    };

    const postAndReload = async (path, okMsg) => {
        try {
            const data = await envelope(await fetch(`${api}${path}`, { method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' } }));
            window.showToast?.(okMsg, 'success');
            await reload();
            return data;
        } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); return null; }
    };

    // Delegated clicks: row → quick view; action menu items; flag bar; "clear filters" in the empty result.
    document.addEventListener('click', event => {
        const clear = event.target.closest('.js-clear-filters');
        if (clear) { event.preventDefault(); clearAllFilters(); return; }

        const flag = event.target.closest('#claimFlagBar .claim-flag');
        if (flag) {
            event.preventDefault();
            appliedFilters.flag = appliedFilters.flag === flag.dataset.flag ? '' : flag.dataset.flag;
            refreshAfterFilterChange();
            return;
        }

        const quick = event.target.closest('.js-quick-view');
        if (quick) { event.preventDefault(); void openClaimPreview(quick.dataset.id); return; }

        const edit = event.target.closest('.js-edit-claim');
        if (edit) { event.preventDefault(); window.location.href = `/CRM/Claims/Edit/${encodeURIComponent(edit.dataset.id)}`; return; }

        const next = event.target.closest('.js-new-version');
        if (next) {
            event.preventDefault();
            window.showConfirm?.(L.NewVersionConfirm, async () => {
                const newId = await postAndReload(`/claims/${encodeURIComponent(next.dataset.id)}/new-version`, L.RecordNewVersion);
                if (newId) window.location.href = `/CRM/Claims/Edit/${encodeURIComponent(newId)}`;
            }, { entityName: next.dataset.name, type: 'info', confirmButtonText: L.NewVersion });
            return;
        }

        const archive = event.target.closest('.js-archive-claim');
        if (archive) {
            event.preventDefault();
            window.showConfirm?.(L.ArchiveClaimConfirm, () => postAndReload(`/claims/${encodeURIComponent(archive.dataset.id)}/archive`, L.RecordArchived),
                { entityName: archive.dataset.name, type: 'warning', confirmButtonText: L.ArchiveClaim });
            return;
        }

        // Row click (not on a control) opens the quick view (mockup: "click a row for the quick view").
        const tr = event.target.closest('#dt-claims tbody tr.claim-row');
        if (tr && !event.target.closest('a, button, .dropdown, .dropdown-menu, input, td.control') && dt) {
            const data = dt.row(tr).data();
            if (data?.claimId) void openClaimPreview(data.claimId);
        }
    });

    init();
})(window, document);
