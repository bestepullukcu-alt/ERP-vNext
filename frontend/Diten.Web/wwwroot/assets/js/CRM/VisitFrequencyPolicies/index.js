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
    const baseOrder = [[1, 'asc']];

    const L = window.VfpL10n || window.L10n || {};
    let dt = null;
    let addNewBound = false;
    const emptyFilters = () => ({ status: [], targetType: [], source: '' });
    let appliedFilters = emptyFilters();
    let allRows = [];
    const rowById = {};

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

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status });
        return body.data;
    };

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

    // Vocabulary comes from the FU03 contract — never hardcoded here.
    const loadFilterOptions = async () => {
        let vocab = { statuses: [], targetTypes: [], sources: [] };
        try {
            const contract = await envelope(await fetch(`${endpoint}/visit-frequency-policies/contract`, { credentials: 'same-origin', headers: getAuthHeaders() }));
            vocab = contract?.vocabulary || vocab;
        } catch (e) { /* filters degrade to empty; the list still renders */ }
        fillSelect('filterStatus', (vocab.statuses || []).map(v => ({ value: v, text: statusLabel(v) })), false);
        fillSelect('filterTargetType', (vocab.targetTypes || []).map(v => ({ value: v, text: humanize(v) })), false);
        fillSelect('filterSource', (vocab.sources || []).map(v => ({ value: v, text: humanize(v) })), true);
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

    // ── offcanvas placeholders (FREQ-B/C fill them) ──────────────────────────
    const openCreateEdit = (row) => {
        const label = document.getElementById('offcanvasCreateEditLabel');
        if (label) label.textContent = row ? (row.policyName || L.Edit || '') : (L.NewPolicy || '');
        const el = document.getElementById('offcanvasCreateEdit');
        if (el && window.bootstrap) window.bootstrap.Offcanvas.getOrCreateInstance(el).show();
    };
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
    const targetCell = row => {
        const label = humanize(row.targetType);
        const ref = shortId(row.targetId);
        return `<span class="fw-medium">${esc(label)}</span>` + (ref ? `<br><span class="text-muted small">${esc(ref)}</span>` : '');
    };
    const frequencyCell = row => {
        const type = humanize(row.frequencyType);
        const count = row.requiredVisitCount != null ? String(row.requiredVisitCount) : '';
        const period = humanize(row.periodType);
        const per = norm(L.PerPeriod) || '/';
        const line = [count, per, period].filter(s => norm(s)).join(' ');
        return badge(type, 'info') + (line ? ` <span class="text-muted small">${esc(line)}</span>` : '');
    };

    const buildConfig = () => ({
        data: allRows, stateSave: false, processing: true,
        colReorder: { columns: ':gt(0):not(:last-child)' },
        order: baseOrder,
        columns: [
            { data: null, defaultContent: '' },
            { data: 'policyCode' },
            { data: 'policyName' },
            { data: 'targetType' },
            { data: 'frequencyType' },
            { data: 'priority' },
            { data: 'status' },
            { data: null }
        ],
        columnDefs: [
            { targets: 0, className: 'control', orderable: false, render: () => '' },
            { targets: 1, render: v => `<span class="fw-medium text-heading">${esc(v)}</span>` },
            { targets: 3, orderable: false, render: (v, t, row) => targetCell(row) },
            { targets: 4, orderable: false, render: (v, t, row) => frequencyCell(row) },
            { targets: 6, render: v => badge(statusLabel(v), statusTone(v)) },
            { targets: 7, title: L.Actions, orderable: false, searchable: false, className: 'cell-fit text-end pe-3 all', render: (v, t, row) => actions(row) }
        ],
        language: { emptyTable: L.EmptyState, processing: L.Loading },
        buttons: window.DtDefaults.exportButtons(L.NewPolicy, {}, {
            filterBtn: {
                text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>',
                className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative',
                attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' },
                action: () => toggleInlineFilter()
            }
        }, { exportColumns: [1, 2, 3, 4, 5, 6], colvisColumns: [1, 2, 3, 4, 5, 6] }),
        initComplete: function () {
            const api = this.api();
            mountInlineFilter(api);
            bindInlineFilterA11y(api);
            void setupFilters(api);
            if (!addNewBound) {
                nodeContainer(api).querySelector('.add-new')?.addEventListener('click', e => { e.preventDefault(); openCreateEdit(null); });
                addNewBound = true;
            }
        },
        drawCallback: function () { window.DtDefaults?.updateVisualState?.(this.api(), getAppliedFilterCount()); }
    });

    const setupFilters = async api => {
        await loadFilterOptions();
        writeControls(appliedFilters);
        try { api.rows().invalidate().draw(false); } catch (e) { /* table not ready */ }
        window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
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
            writeControls(appliedFilters);
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
        });
    };

    const loadRows = async () =>
        (await envelope(await fetch(`${endpoint}/visit-frequency-policies`, { credentials: 'same-origin', headers: getAuthHeaders() })))?.items || [];

    const init = async () => {
        document.getElementById('skeleton-loader')?.classList.remove('d-none');
        registerTableFilter();
        try {
            allRows = await loadRows();
            dt = new DataTable(tableEl, window.DtDefaults?.create ? window.DtDefaults.create(buildConfig()) : buildConfig());
            dt.on('column-visibility.dt search.dt order.dt column-reorder.dt columns-reordered.dt', () => {
                window.DtDefaults?.updateVisualState?.(dt, getAppliedFilterCount());
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
            if (dt) { dt.clear(); dt.rows.add(allRows).draw(false); }
        } catch (error) { window.showToast?.(error.message || L.ErrorState, 'error'); }
    };

    document.addEventListener('click', event => {
        const details = event.target.closest('.js-quick-view');
        if (details) { event.preventDefault(); openDetails(details.dataset.id); return; }

        const edit = event.target.closest('.js-vfp-edit');
        if (edit) { event.preventDefault(); openCreateEdit(rowById[edit.dataset.id]); return; }

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
