/**
 * MOD-0155-FU05 Visit Planning — planning-session ("draft plan") DataTables Index (Golden Compact, proxy profile).
 *  - Entity = a planning session. Rows come from GET /CRM/VisitPlanning/api/sessions (client-side; few rows).
 * *  - Row actions: Route (POST /preview → Details), Details, Apply (CanApply). Create is its own page.
 *  - All traffic via the same-origin MVC proxy /CRM/VisitPlanning/api (never a Gateway URL / bearer token).
 */
(function (window, document) {
    'use strict';
    const tableEl = document.getElementById('dt-visit-planning');
    if (!tableEl) return;

    const endpoint = '/CRM/VisitPlanning/api';
    const pageRoot = '/CRM/VisitPlanning';
    const filterCollapseId = 'inlineFilterCollapse';

    let L = window.L10n || {};
    let canGenerate = false;
    let canApply = false;
    try {
        const flags = JSON.parse(document.getElementById('visitplanning-page-flags')?.textContent || '{}');
        canGenerate = !!flags.canGenerate;
        canApply = !!flags.canApply;
    } catch (e) { canGenerate = false; canApply = false; }

    let dt = null;
    let addNewBound = false;
    let allRows = [];
    const periodMap = {};
    const emptyFilters = () => ({ sessionStatus: [], cyclePeriodId: '', rep: '' });
    let appliedFilters = emptyFilters();

    // Same-origin proxy profile: the browser sends no bearer token (the MVC proxy attaches it server-side).
    const getAuthHeaders = () => ({ Accept: 'application/json', 'Content-Type': 'application/json' });
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const badge = (v, cls = 'primary') => `<span class="badge bg-label-${cls}">${esc(v || '—')}</span>`;
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const normArr = v => Array.isArray(v) ? Array.from(new Set(v.map(x => norm(x)).filter(Boolean))) : (norm(v) ? [norm(v)] : []);
    const date = v => { if (!v) return '—'; const d = new Date(v); return isNaN(d) ? '—' : d.toLocaleDateString('en-US', { month: 'short', day: '2-digit', year: '2-digit' }); };
    // ISO-8601 week number (Thursday-based, Monday start) — labels the plan's saved target week.
    const isoWeek = dt => { const d = new Date(dt); d.setHours(0, 0, 0, 0); d.setDate(d.getDate() + 3 - ((d.getDay() + 6) % 7)); const w1 = new Date(d.getFullYear(), 0, 4); return 1 + Math.round(((d - w1) / 86400000 - 3 + ((w1.getDay() + 6) % 7)) / 7); };
    const sWeek = s => { const w = s.targetWeekStart || s.TargetWeekStart; return /^\d{4}-\d{2}-\d{2}$/.test(w || '') ? (L.WeekNumberLabel || '{0}. ' + (L.WeekLabel || 'Week')).replace('{0}', isoWeek(new Date(w))) : '—'; };

    // Defensive field readers — the session DTO fields are read with fallbacks (the backend is unchanged / not typed here).
    const sid = s => s.planningSessionId || s.id || s.sessionId || '';
    const sName = s => s.sessionName || s.name || s.planName || ('#' + String(sid(s)).slice(0, 8));
    const sPeriodId = s => s.cyclePeriodId || s.CyclePeriodId || '';
    const sRep = s => s.resourceDisplayName || s.resourceName || s.resourceId || '';
    const sStatus = s => norm(s.status || s.sessionStatus || 'draft');
    const sTargets = s => {
        if (typeof s.targetCount === 'number') return s.targetCount;
        const c = Array.isArray(s.selectedContacts) ? s.selectedContacts.length : 0;
        const a = Array.isArray(s.selectedAccountIds) ? s.selectedAccountIds.length : 0;
        return c + a;
    };
    const sUpdated = s => s.updatedAt || s.updatedOn || s.modifiedAt || s.lastModifiedAt || s.createdAt || null;

    const statusLabel = v => ({ draft: L.StatusDraft, committed: L.StatusCommitted }[v] || v);
    const statusTone = v => ({ committed: 'success', draft: 'primary' }[v] || 'secondary');

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorOccurred]).join(' · ')), { status: response.status });
        return body.data;
    };

    const fillSelect = (id, options, keepShowAll) => {
        const el = document.getElementById(id);
        if (!el) return;
        const head = keepShowAll ? `<option value="">${esc(L.ShowAll || 'All')}</option>` : '';
        el.innerHTML = head + (options || []).map(o => `<option value="${esc(o.value)}">${esc(o.text)}</option>`).join('');
    };

    const initSelect2 = () => {
        if (!window.jQuery || !window.jQuery.fn.select2) return;
        const $body = window.jQuery(document.body);
        window.jQuery('#inlineFilterHost .select2').each(function () {
            const $s = window.jQuery(this);
            if ($s.hasClass('select2-hidden-accessible')) $s.select2('destroy');
            $s.select2({ dropdownParent: $body, dropdownCssClass: 'dt-inline-filter-dropdown', selectionCssClass: 'form-select form-select-sm', placeholder: $s.data('placeholder') || '', minimumResultsForSearch: Infinity, width: 'element', allowClear: !$s.prop('multiple'), closeOnSelect: !$s.prop('multiple') });
        });
    };

    const distinct = key => Array.from(new Set(allRows.map(key).filter(Boolean)));
    const loadFilterOptions = () => {
        fillSelect('filterSessionStatus', distinct(sStatus).map(v => ({ value: v, text: statusLabel(v) })), false);
        fillSelect('filterCyclePeriod', distinct(sPeriodId).map(v => ({ value: v, text: periodMap[v] || v })), true);
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

    const matchesMulti = (sel, val) => { const n = normArr(sel); return !n.length || n.includes(norm(val)); };
    const matchesSingle = (sel, val) => { const n = norm(sel); return !n || norm(val) === n; };
    const registerTableFilter = () => {
        if (!window.jQuery?.fn?.dataTable?.ext?.search || tableEl.dataset.filterBound === '1') return;
        tableEl.dataset.filterBound = '1';
        window.jQuery.fn.dataTable.ext.search.push((settings, _d, dataIndex, row) => {
            if (settings.nTable !== tableEl) return true;
            const r = row || dt?.row(dataIndex)?.data?.();
            if (!r) return true;
            const rep = norm(appliedFilters.rep);
            if (rep && norm(sRep(r)).toLowerCase().indexOf(rep.toLowerCase()) === -1) return false;
            return matchesMulti(appliedFilters.sessionStatus, sStatus(r))
                && matchesSingle(appliedFilters.cyclePeriodId, sPeriodId(r));
        });
    };
    const getAppliedFilterCount = () => {
        let n = 0;
        if (normArr(appliedFilters.sessionStatus).length) n++;
        ['cyclePeriodId', 'rep'].forEach(k => { if (norm(appliedFilters[k])) n++; });
        return n;
    };

    const readControls = () => ({
        sessionStatus: window.jQuery('#filterSessionStatus').val() || [],
        cyclePeriodId: document.getElementById('filterCyclePeriod')?.value || '',
        rep: document.getElementById('filterRep')?.value || ''
    });

    // Row action menu: Route (preview → Details), Details, Apply (CanApply + draft).
    const actions = row => {
        const id = esc(sid(row));
        const status = sStatus(row);
        const items = [{ key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': id, title: L.ViewDetails } }];
        if (canGenerate) {
            items.push({ className: 'js-route text-primary', icon: 'bx bx-map-alt', text: L.RouteAction, attrs: { 'data-id': id } });
        }
        items.push({ className: 'js-details', icon: 'bx bx-detail', text: L.Details, attrs: { 'data-id': id } });
        if (canApply && status !== 'committed') {
            items.push({ className: 'js-apply text-success', icon: 'bx bx-check-circle', text: L.Apply, attrs: { 'data-id': id } });
        }
        return window.DitenDataTable?.renderActions ? window.DitenDataTable.renderActions(items) : '';
    };

    const buildConfig = () => ({
        data: allRows, stateSave: false, processing: true,
        order: [[7, 'desc']],
        columns: [
            { data: null, defaultContent: '' },
            { data: null }, { data: null }, { data: null }, { data: null }, { data: null }, { data: null }, { data: null }, { data: null }
        ],
        columnDefs: [
            { targets: 0, className: 'control', orderable: false, render: () => '' },
            { targets: 1, render: (v, t, row) => t === 'display' ? `<span class="fw-medium text-heading">${esc(sName(row))}</span>` : sName(row) },
            { targets: 2, render: (v, t, row) => esc(periodMap[sPeriodId(row)] || sPeriodId(row) || '—') },
            { targets: 3, render: (v, t, row) => esc(sWeek(row)) },
            { targets: 4, render: (v, t, row) => esc(sRep(row) || '—') },
            { targets: 5, render: (v, t, row) => badge(statusLabel(sStatus(row)), statusTone(sStatus(row))) },
            { targets: 6, render: (v, t, row) => esc(sTargets(row)) },
            { targets: 7, render: (v, t, row) => t === 'display' ? date(sUpdated(row)) : (sUpdated(row) || '') },
            { targets: 8, title: L.Actions, orderable: false, searchable: false, className: 'cell-fit text-end pe-3 all', render: (v, t, row) => actions(row) }
        ],
        language: { emptyTable: L.EmptyState, processing: L.Loading },
        buttons: window.DtDefaults.exportButtons(canGenerate ? (L.NewSession || '') : '', {}, {
            filterBtn: { text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>', className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative', attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' }, action: () => toggleInlineFilter() }
        }, { exportColumns: [1, 2, 3, 4, 5, 6], colvisColumns: [1, 2, 3, 4, 5, 6] }),
        initComplete: function () {
            mountInlineFilter();
            void setupFilters(this.api());
            if (canGenerate && !addNewBound) { document.querySelector('.add-new')?.addEventListener('click', e => { e.preventDefault(); window.location.assign(`${pageRoot}/Create`); }); addNewBound = true; }
        },
        drawCallback: function () { window.DtDefaults?.updateVisualState?.(this.api(), getAppliedFilterCount()); }
    });

    const setupFilters = async api => {
        loadFilterOptions();
        try { api.rows().invalidate().draw(false); } catch (e) { /* not ready */ }
        document.getElementById('btnFilterApply')?.addEventListener('click', () => {
            appliedFilters = readControls();
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
            const el = document.getElementById(filterCollapseId);
            if (el) window.bootstrap?.Collapse.getOrCreateInstance(el, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', e => {
            e.preventDefault();
            appliedFilters = emptyFilters();
            document.getElementById('filterRep').value = '';
            window.jQuery('#filterSessionStatus').val(null).trigger('change');
            window.jQuery('#filterCyclePeriod').val('').trigger('change');
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, 0);
        });
    };

    const loadPeriods = async () => {
        try {
            const data = await envelope(await fetch(`${endpoint}/cycle-periods`, { credentials: 'same-origin', headers: getAuthHeaders() }));
            const items = data?.items || (Array.isArray(data) ? data : []);
            items.forEach(p => { const id = p.cyclePeriodId || p.id; if (id) periodMap[id] = p.cycleName || p.cycleCode || p.name || id; });
        } catch (e) { /* period names degrade to ids */ }
    };

    const fetchRows = async () => {
        const data = await envelope(await fetch(`${endpoint}/sessions`, { credentials: 'same-origin', headers: getAuthHeaders() }));
        return data?.items || (Array.isArray(data) ? data : []);
    };

    const reload = async () => { allRows = await fetchRows(); if (dt) { dt.clear(); dt.rows.add(allRows).draw(false); } };

    const init = async () => {
        document.getElementById('skeleton-loader')?.classList.remove('d-none');
        registerTableFilter();
        try {
            await loadPeriods();
            allRows = await fetchRows();
            dt = new DataTable(tableEl, window.DtDefaults?.create ? window.DtDefaults.create(buildConfig()) : buildConfig());
            dt.on('column-visibility.dt search.dt order.dt', () => window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount()));
        } catch (error) {
            window.showToast?.(error.message || L.ErrorOccurred, 'error');
        } finally {
            document.getElementById('skeleton-loader')?.classList.add('d-none');
        }
    };

    document.addEventListener('click', event => {
        const quickView = event.target.closest('.js-quick-view');
        if (quickView) { event.preventDefault(); if (quickView.dataset.id) window.location.assign(`${pageRoot}/Details/${quickView.dataset.id}`); return; }

        const details = event.target.closest('.js-details');
        if (details) { event.preventDefault(); if (details.dataset.id) window.location.assign(`${pageRoot}/Details/${details.dataset.id}`); return; }

        const route = event.target.closest('.js-route');
        if (route) {
            event.preventDefault();
            const id = route.dataset.id;
            // Generate the route for this session (dry-run preview), then open Details where the week grid renders.
            fetch(`${endpoint}/preview`, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders(), body: JSON.stringify({ planningSessionId: id }) })
                .then(r => r.json().catch(() => ({})).then(b => ({ ok: r.ok, b })))
                .then(({ ok, b }) => { if (!ok) window.showToast?.((b.errors || [L.PreviewFailed]).join(' · '), 'error'); })
                .catch(() => window.showToast?.(L.PreviewFailed, 'error'))
                .finally(() => window.location.assign(`${pageRoot}/Details/${id}`));
            return;
        }

        const apply = event.target.closest('.js-apply');
        if (apply) {
            event.preventDefault();
            const id = apply.dataset.id;
            window.location.assign(`${pageRoot}/Details/${id}`);
            return;
        }
    });

    init();
})(window, document);
