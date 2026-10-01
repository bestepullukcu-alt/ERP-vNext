/**
 * MOD-0162-FU04 KnowledgePath — DataTables Index (Golden aligned, proxy profile).
 * WP-KP-UI-1 — Knowledge Path Studio list (mockup v2): rows from api/studio/paths (names, never raw country / language
 * codes), columns Path · Product · Country/language · Chain · Version · Status (+ "unapproved legacy path" badge),
 * filters product / country / language (native names) / status / legacy-only, summary cards that apply a filter, and the
 * bind-chain modal on a legacy draft row (studio-common.js).
 *  - Native toolbar search, Select2 filter chips mounted under the toolbar
 *  - SaveView (filter + search + colvis + colorder) via personalizationClient
 *  - Row actions: Open (workspace) + Bind to a chain (legacy draft) + Archive; Compact "Create" (.add-new) → /Create
 *  - All traffic via same-origin MVC proxy /CRM/KnowledgePaths/api (never a Gateway URL / bearer token)
 */
(function (window, document) {
    'use strict';
    const tableEl = document.getElementById('dt-knowledge-paths');
    if (!tableEl) return;

    const endpoint = '/CRM/KnowledgePaths/api';
    const filterCollapseId = 'inlineFilterCollapse';
    const personalizationClient = window.personalizationClient;
    const personalizationContext = { moduleKey: 'CRM', pageKey: 'KnowledgePaths' };
    const saveViewColumnIndexes = [1, 2, 3, 4, 5, 6, 7];
    const totalColumnCount = 9;
    const baseOrder = [[7, 'desc']];

    let L = window.KnowledgePathsL10n || window.L10n || {};
    const S = window.KpStudio;
    const t = S ? S.t : () => '';
    const canManage = document.getElementById('kpSummary')?.dataset.canManage === 'true';
    let dt = null;
    let contract = null;
    let addNewBound = false;
    let saveFilterArmed = false;
    let defaultViewRecord = null;
    let defaultViewState = null;
    const emptyFilters = () => ({ product: [], countryCode: '', languageCode: '', status: [], legacy: '' });
    let appliedFilters = emptyFilters();
    let allRows = [];

    const getAuthHeaders = () => ({ Accept: 'application/json' });
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
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
            const $clear = window.jQuery('<span class="dt-multi-clear-btn" role="button" title="' + (L.Reset || '') + '">&times;</span>');
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
                $s.select2({ dropdownParent: $body, dropdownCssClass: 'dt-inline-filter-dropdown', containerCssClass: 'dt-inline-filter-multi', selectionCssClass: 'form-select form-select-sm', placeholder: $s.data('placeholder') || '', minimumResultsForSearch: Infinity, width: 'element', closeOnSelect: false });
                $s.off('change.select2-summary').on('change.select2-summary', () => syncMultiSelectSummary($s));
                window.requestAnimationFrame(() => syncMultiSelectSummary($s));
            } else {
                $s.select2({ dropdownParent: $body, dropdownCssClass: 'dt-inline-filter-dropdown', selectionCssClass: 'form-select form-select-sm', placeholder: $s.data('placeholder') || '', minimumResultsForSearch: Infinity, width: 'element', allowClear: true });
            }
        });
    };

    const distinctBy = (valueKey, textKey) => {
        const map = new Map();
        allRows.forEach(r => { if (r[valueKey]) map.set(r[valueKey], r[textKey] || r[valueKey]); });
        return Array.from(map, ([value, text]) => ({ value, text })).sort((a, b) => a.text.localeCompare(b.text));
    };

    const loadFilterOptions = async () => {
        fillSelect('filterProduct', distinctBy('productName', 'productName'), false);
        fillSelect('filterStatus', distinctBy('status', 'statusLabel'), false);
        try {
            const countries = S ? await S.countries() : [];
            fillSelect('filterCountry', countries.map(c => ({ value: c.code, text: c.name })), true);
            const languages = new Map();
            countries.forEach(c => (c.languageDetails || []).forEach(l => { if (!languages.has(l.code)) languages.set(l.code, l.nativeName); }));
            fillSelect('filterLanguage', Array.from(languages, ([value, text]) => ({ value, text })), true);
        } catch (e) {
            // The country axis is unavailable: fall back to the names already on the rows (never raw codes).
            fillSelect('filterCountry', distinctBy('countryCode', 'countryName'), true);
            fillSelect('filterLanguage', distinctBy('languageCode', 'languageName'), true);
        }
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

    const matchesMulti = (sel, val) => { const n = normArr(sel); return !n.length || n.includes(norm(val)); };
    const matchesSingle = (sel, val) => { const n = norm(sel); return !n || norm(val) === n; };
    const registerTableFilter = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || tableEl.dataset.filterBound === '1') return;
        tableEl.dataset.filterBound = '1';
        window.jQuery.fn.dataTable.ext.search.push((settings, _d, dataIndex, row) => {
            if (settings.nTable !== tableEl) return true;
            const r = row || dt?.row(dataIndex)?.data?.();
            if (!r) return true;
            if (appliedFilters.legacy === 'legacy' && !r.isLegacyUnapproved) return false;
            return matchesMulti(appliedFilters.product, r.productName)
                && matchesSingle(appliedFilters.countryCode, r.countryCode)
                && matchesSingle(appliedFilters.languageCode, r.languageCode)
                && matchesMulti(appliedFilters.status, r.status);
        });
    };
    const getAppliedFilterCount = () => [appliedFilters.product, appliedFilters.countryCode, appliedFilters.languageCode, appliedFilters.status, appliedFilters.legacy].filter(hasVal).length;

    const readControls = () => ({
        product: window.jQuery('#filterProduct').val() || [],
        countryCode: document.getElementById('filterCountry')?.value || '',
        languageCode: document.getElementById('filterLanguage')?.value || '',
        status: window.jQuery('#filterStatus').val() || [],
        legacy: document.getElementById('filterLegacy')?.value || ''
    });
    const writeControls = f => {
        window.jQuery('#filterProduct').val(normArr(f.product)).trigger('change');
        window.jQuery('#filterCountry').val(f.countryCode || '').trigger('change');
        window.jQuery('#filterLanguage').val(f.languageCode || '').trigger('change');
        window.jQuery('#filterStatus').val(normArr(f.status)).trigger('change');
        window.jQuery('#filterLegacy').val(f.legacy || '').trigger('change');
    };

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
        } catch (e) { if (!e?.authHandled) console.error('[KnowledgePaths SaveView] load failed', e); }
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

    const statusTone = v => ({ draft: 'secondary', review: 'info', approved: 'success', published: 'success', inactive: 'warning', archived: 'secondary' }[v] || 'secondary');

    const pathCell = row => `
        <div class="d-flex flex-column">
            <a class="fw-medium text-heading" href="/CRM/KnowledgePaths/${esc(row.pathId)}">${esc(row.pathName)}</a>
            <small class="text-muted">${esc(row.pathCode)}</small>
            ${row.isLegacyUnapproved ? `<span class="badge bg-label-warning align-self-start mt-1" title="${esc(t('LegacyBadgeTip'))}">${esc(t('LegacyBadge'))}</span>` : ''}
        </div>`;

    const countryLanguageCell = row => row.countryName || row.languageName
        ? `<div class="d-flex flex-column"><span>${esc(row.countryName || '—')}</span><small class="text-muted">${esc(row.languageName || '—')}</small></div>`
        : '—';

    const canBind = row => canManage && row.isLegacyUnapproved && !row.isArchived && row.status === 'draft';

    const actions = row => {
        const id = esc(row.pathId);
        const items = [{ className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': id, title: t('OpenPath') } }];
        if (canBind(row)) {
            items.push({ className: 'js-bind-path', icon: 'bx bx-link', text: t('BindChain'), attrs: { 'data-id': id } });
        }
        if (canManage && !row.isArchived) {
            items.push({ className: 'js-archive-path text-warning', icon: 'bx bx-archive-in', text: L.ArchivePath, attrs: { 'data-id': id, 'data-name': esc(row.pathName) } });
        }
        return window.DitenDataTable?.renderActions ? window.DitenDataTable.renderActions(items) : '';
    };

    const renderSummary = () => {
        const set = (id, n) => { const el = document.getElementById(id); if (el) el.textContent = String(n); };
        const live = allRows.filter(r => !r.isArchived);
        set('kpSumTotal', live.length);
        set('kpSumDraft', live.filter(r => r.status === 'draft').length);
        set('kpSumPublished', live.filter(r => r.status === 'published').length);
        set('kpSumLegacy', live.filter(r => r.isLegacyUnapproved).length);
    };

    const buildConfig = () => ({
        data: allRows, stateSave: false, processing: true,
        colReorder: { columns: ':gt(0):not(:last-child)' },
        order: baseOrder,
        columns: [
            { data: null, defaultContent: '' }, { data: 'pathName' }, { data: 'productName' }, { data: 'countryName' },
            { data: 'chainName' }, { data: 'pathVersion' }, { data: 'statusLabel' }, { data: 'updatedAt' }, { data: null }
        ],
        columnDefs: [
            { targets: 0, className: 'control', orderable: false, render: () => '' },
            { targets: 1, render: (v, type, row) => type === 'display' ? pathCell(row) : `${row.pathName || ''} ${row.pathCode || ''}` },
            { targets: 2, render: v => esc(v || '—') },
            { targets: 3, render: (v, type, row) => type === 'display' ? countryLanguageCell(row) : `${row.countryName || ''} ${row.languageName || ''}` },
            { targets: 4, render: (v, type, row) => v ? `${esc(v)}${row.chainVersion ? ` <small class="text-muted">${esc(t('VersionShort', row.chainVersion))}</small>` : ''}` : '—' },
            { targets: 5, render: v => esc(v || '—') },
            { targets: 6, render: (v, type, row) => type === 'display' ? badge(v, statusTone(row.status)) : (v || '') },
            { targets: 7, render: v => date(v) },
            { targets: 8, title: L.Actions, orderable: false, searchable: false, className: 'cell-fit text-end pe-3 all', render: (v, type, row) => actions(row) }
        ],
        language: { emptyTable: L.EmptyState, zeroRecords: t('EmptyFiltered') || L.EmptyState, processing: L.Loading },
        buttons: window.DtDefaults.exportButtons(t('NewPath') || L.CreatePath, { href: '/CRM/KnowledgePaths/Create' }, {
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
            void setupFilters(this.api());
            if (!addNewBound) { document.querySelector('.add-new')?.addEventListener('click', e => { e.preventDefault(); window.location.href = '/CRM/KnowledgePaths/Create'; }); addNewBound = true; }
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
            if (!contract?.isReady || !contract?.features?.supportsKnowledgePath) throw new Error(L.KnowledgePathContractUnavailable);
            return true;
        } catch (error) {
            const host = document.getElementById('knowledgePathContractError');
            if (host) { host.textContent = error.message || L.KnowledgePathContractUnavailable; host.classList.remove('d-none'); }
            return false;
        }
    };

    const fetchRows = async () => {
        const rows = (await envelope(await fetch(`${endpoint}/studio/paths`, { credentials: 'same-origin', headers: getAuthHeaders() }))) || [];
        allRows = rows;
        renderSummary();
        return rows;
    };
    const reloadRows = async () => {
        allRows = await fetchRows();
        if (dt) { dt.clear(); dt.rows.add(allRows).draw(false); }
    };

    const init = async () => {
        document.getElementById('skeleton-loader')?.classList.remove('d-none');
        registerTableFilter();
        try {
            if (!(await loadContract())) return;
            await loadDefaultView();
            allRows = await fetchRows();
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

    document.addEventListener('click', event => {
        const view = event.target.closest('.js-quick-view');
        if (view) { event.preventDefault(); window.location.href = `/CRM/KnowledgePaths/${view.dataset.id}`; return; }
        const bind = event.target.closest('.js-bind-path');
        if (bind) {
            event.preventDefault();
            const row = allRows.find(r => r.pathId === bind.dataset.id);
            if (row && window.KpLegacyWizard) window.KpLegacyWizard.open({ pathId: row.pathId, subjectId: row.subjectId, name: row.pathName, code: row.pathCode }, reloadRows);
            return;
        }
        const summary = event.target.closest('.js-summary');
        if (summary && dt) {
            event.preventDefault();
            const kind = summary.dataset.filter;
            appliedFilters = Object.assign(emptyFilters(), kind === 'legacy' ? { legacy: 'legacy' } : kind === 'all' ? {} : { status: [kind] });
            writeControls(appliedFilters);
            dt.draw();
            window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount());
            if (saveFilterArmed) setSaveFilterVisible(isDirtyComparedToDefault(dt));
            return;
        }
        const archive = event.target.closest('.js-archive-path');
        if (!archive) return;
        event.preventDefault();
        window.showConfirm?.(L.ArchivePathConfirm, async () => {
            try {
                await envelope(await fetch(`${endpoint}/paths/${archive.dataset.id}/archive`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders() }));
                window.showToast?.(L.RecordArchived, 'success');
                await reloadRows();
            } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
        }, { entityName: archive.dataset.name, type: 'warning', confirmButtonText: L.ArchivePath });
    });

    init();
})(window, document);
