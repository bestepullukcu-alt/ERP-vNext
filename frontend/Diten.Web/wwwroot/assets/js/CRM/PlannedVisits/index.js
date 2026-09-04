/**
 * MOD-0155-FU01 Planned Visits — DataTables Index (Golden Compact aligned, proxy profile).
 *  - Row actions: Details + Edit + Confirm + Cancel + Archive. Create/Edit/Details are their OWN PAGES (Golden Compact).
 *  - All traffic via the same-origin MVC proxy /CRM/PlannedVisits/api (never a Gateway URL / bearer token).
 *  - There is NO delete and NO bulk delete anywhere (a plan is cancelled/archived), and no reopen (archived is terminal).
 *  - Consent and frequency are shown as BADGES: information, never "the system planned for you" (FU05 lesson).
 */
(function (window, document) {
    'use strict';
    const tableEl = document.getElementById('dt-planned-visits');
    if (!tableEl) return;

    const endpoint = '/CRM/PlannedVisits/api';
    const pageRoot = '/CRM/PlannedVisits';
    const filterCollapseId = 'inlineFilterCollapse';

    let L = window.L10n || {};
    let canManage = false;
    let canConfirm = false;
    try {
        const flags = JSON.parse(document.getElementById('plannedvisit-page-flags')?.textContent || '{}');
        canManage = !!flags.canManage;
        canConfirm = !!flags.canConfirm;
    } catch (e) { canManage = false; canConfirm = false; }

    let dt = null;
    let contract = null;
    let addNewBound = false;
    let allRows = [];
    const emptyFilters = () => ({ planStatus: [], targetType: '', visitPurpose: '', resourceId: '', plannedDateFrom: '', plannedDateTo: '', includeArchived: false });
    let appliedFilters = emptyFilters();

    // Same-origin proxy profile: the browser sends no bearer token (the MVC proxy attaches it server-side). This helper
    // exists so every fetch shares one header set, matching the Golden Compact idiom.
    const getAuthHeaders = () => ({ Accept: 'application/json', 'Content-Type': 'application/json' });
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const badge = (v, cls = 'primary') => `<span class="badge bg-label-${cls}">${esc(v || '—')}</span>`;
    const norm = v => (typeof v === 'string' ? v.trim() : (v == null ? '' : String(v)));
    const normArr = v => Array.isArray(v) ? Array.from(new Set(v.map(x => norm(x)).filter(Boolean))) : (norm(v) ? [norm(v)] : []);

    const statusLabel = v => ({ draft: L.StatusDraft, planned: L.StatusPlanned, confirmed: L.StatusConfirmed, cancelled: L.StatusCancelled, archived: L.StatusArchived }[v] || v);
    const statusTone = v => ({ confirmed: 'success', planned: 'info', cancelled: 'secondary', archived: 'dark' }[v] || 'primary');
    const targetTypeLabel = v => ({ account: L.TargetTypeAccount, contact: L.TargetTypeContact, 'account-contact-link': L.TargetTypeAccountContactLink, pharmacy: L.TargetTypePharmacy }[v] || v);
    const consentLabel = v => ({ allowed: L.ConsentAllowed, blocked: L.ConsentBlocked, unknown: L.ConsentUnknown, not_applicable: L.ConsentNotApplicable }[v] || v || '—');
    const consentTone = v => ({ allowed: 'success', blocked: 'danger', unknown: 'warning' }[v] || 'secondary');
    const freqLabel = v => ({ resolved: L.FreqResolved, unknown: L.FreqUnknown, conflict: L.FreqConflict, not_applicable: L.FreqNotApplicable }[v] || v || '—');
    const freqTone = v => ({ resolved: 'success', conflict: 'warning' }[v] || 'secondary');

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

    const loadFilterOptions = () => {
        fillSelect('filterPlanStatus', (contract?.vocabularies?.statuses || []).map(v => ({ value: v, text: statusLabel(v) })), false);
        fillSelect('filterTargetType', (contract?.vocabularies?.targetTypes || []).map(v => ({ value: v, text: targetTypeLabel(v) })), true);
        fillSelect('filterVisitPurpose', (contract?.vocabularies?.purposes || []).map(v => ({ value: v, text: v })), true);
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
            if (!appliedFilters.includeArchived && r.planStatus === 'archived') return false;
            const rid = norm(appliedFilters.resourceId);
            if (rid && norm(r.resourceId).toLowerCase().indexOf(rid.toLowerCase()) === -1) return false;
            if (appliedFilters.plannedDateFrom && r.plannedDate < appliedFilters.plannedDateFrom) return false;
            if (appliedFilters.plannedDateTo && r.plannedDate > appliedFilters.plannedDateTo) return false;
            return matchesMulti(appliedFilters.planStatus, r.planStatus)
                && matchesSingle(appliedFilters.targetType, r.targetType)
                && matchesSingle(appliedFilters.visitPurpose, r.visitPurpose);
        });
    };
    const getAppliedFilterCount = () => {
        let n = 0;
        if (normArr(appliedFilters.planStatus).length) n++;
        ['targetType', 'visitPurpose', 'resourceId', 'plannedDateFrom', 'plannedDateTo'].forEach(k => { if (norm(appliedFilters[k])) n++; });
        if (appliedFilters.includeArchived) n++;
        return n;
    };

    const readControls = () => ({
        planStatus: window.jQuery('#filterPlanStatus').val() || [],
        targetType: document.getElementById('filterTargetType')?.value || '',
        visitPurpose: document.getElementById('filterVisitPurpose')?.value || '',
        resourceId: document.getElementById('filterResourceId')?.value || '',
        plannedDateFrom: document.getElementById('filterPlannedDateFrom')?.value || '',
        plannedDateTo: document.getElementById('filterPlannedDateTo')?.value || '',
        includeArchived: !!document.getElementById('filterIncludeArchived')?.checked
    });

    const targetCell = row => `${badge(targetTypeLabel(row.targetType), 'primary')}<span class="ms-1 text-muted small">${esc((row.targetId || '').slice(0, 8))}</span>`;

    // A terminal (archived/cancelled) plan offers no mutation. Details + Edit navigate to their own pages (Compact).
    const actions = row => {
        const id = esc(row.plannedVisitId);
        const items = [{ key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-id': id, title: L.ViewDetails } }];
        const terminal = row.planStatus === 'archived' || row.planStatus === 'cancelled';
        if (canManage && !terminal) {
            items.push({ className: 'js-edit-plan', icon: 'bx bx-edit', text: L.Edit, attrs: { 'data-id': id } });
        }
        if (canConfirm && row.planStatus === 'planned') {
            items.push({ className: 'js-confirm-plan text-success', icon: 'bx bx-check-circle', text: L.ConfirmPlannedVisit, attrs: { 'data-id': id, 'data-version': row.version } });
        }
        if (canManage && !terminal) {
            items.push({ className: 'js-cancel-plan text-warning', icon: 'bx bx-x-circle', text: L.CancelPlannedVisit, attrs: { 'data-id': id, 'data-version': row.version } });
        }
        if (canManage && row.planStatus !== 'archived') {
            items.push({ className: 'js-archive-plan text-muted', icon: 'bx bx-archive', text: L.ArchivePlannedVisit, attrs: { 'data-id': id, 'data-version': row.version } });
        }
        return window.DitenDataTable?.renderActions ? window.DitenDataTable.renderActions(items) : '';
    };

    const buildConfig = () => ({
        data: allRows, stateSave: false, processing: true,
        order: [[3, 'desc']],
        columns: [
            { data: null, defaultContent: '' }, { data: 'visitCode' }, { data: 'targetType' },
            { data: 'plannedDate' }, { data: 'resourceId' }, { data: 'visitPurpose' },
            { data: 'planStatus' }, { data: 'consentStatus' }, { data: 'frequencyStatus' }, { data: null }
        ],
        columnDefs: [
            { targets: 0, className: 'control', orderable: false, render: () => '' },
            { targets: 1, render: v => `<span class="fw-medium text-heading">${esc(v)}</span>` },
            { targets: 2, render: (v, t, row) => t === 'display' ? targetCell(row) : (v || '') },
            { targets: 5, render: v => esc(v) },
            { targets: 6, render: v => badge(statusLabel(v), statusTone(v)) },
            { targets: 7, render: v => badge(consentLabel(v), consentTone(v)) },
            { targets: 8, render: v => badge(freqLabel(v), freqTone(v)) },
            { targets: 9, title: L.Actions, orderable: false, searchable: false, className: 'cell-fit text-end pe-3 all', render: (v, t, row) => actions(row) }
        ],
        language: { emptyTable: L.EmptyState, processing: L.Loading },
        buttons: window.DtDefaults.exportButtons(canManage ? (L.CreatePlannedVisit || '') : '', {}, {
            filterBtn: { text: '<i class="icon-base bx bx-filter-alt icon-sm"></i>', className: 'btn btn-icon btn-label-secondary dt-filter-btn position-relative', attr: { title: L.Filter, 'aria-controls': filterCollapseId, 'aria-expanded': 'false', 'data-bs-toggle': 'tooltip' }, action: () => toggleInlineFilter() }
        }, { exportColumns: [1, 2, 3, 4, 5, 6, 7, 8], colvisColumns: [1, 2, 3, 4, 5, 6, 7, 8] }),
        initComplete: function () {
            mountInlineFilter();
            void setupFilters(this.api());
            if (canManage && !addNewBound) { document.querySelector('.add-new')?.addEventListener('click', e => { e.preventDefault(); window.location.assign(`${pageRoot}/Create`); }); addNewBound = true; }
        },
        drawCallback: function () { window.DtDefaults?.updateVisualState?.(this.api(), getAppliedFilterCount()); }
    });

    const setupFilters = async api => {
        loadFilterOptions();
        try { api.rows().invalidate().draw(false); } catch (e) { /* not ready */ }
        document.getElementById('btnFilterApply')?.addEventListener('click', async () => {
            appliedFilters = readControls();
            if (appliedFilters.includeArchived) { allRows = await fetchRows(true); dt.clear(); dt.rows.add(allRows); }
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, getAppliedFilterCount());
            const el = document.getElementById(filterCollapseId);
            if (el) window.bootstrap?.Collapse.getOrCreateInstance(el, { toggle: false }).hide();
        });
        document.getElementById('btnFilterReset')?.addEventListener('click', async e => {
            e.preventDefault();
            appliedFilters = emptyFilters();
            document.getElementById('filterResourceId').value = '';
            document.getElementById('filterPlannedDateFrom').value = '';
            document.getElementById('filterPlannedDateTo').value = '';
            document.getElementById('filterIncludeArchived').checked = false;
            window.jQuery('#filterPlanStatus').val(null).trigger('change');
            window.jQuery('#filterTargetType').val('').trigger('change');
            window.jQuery('#filterVisitPurpose').val('').trigger('change');
            allRows = await fetchRows(false); dt.clear(); dt.rows.add(allRows);
            api.draw();
            window.DtDefaults?.updateVisualState?.(api, 0);
        });
    };

    const loadContract = async () => {
        try {
            contract = await envelope(await fetch(`${endpoint}/contract`, { credentials: 'same-origin', headers: getAuthHeaders() }));
            if (!contract?.isReady || !contract?.features?.supportsPlannedVisit) throw new Error(L.ContractUnavailable);
            return true;
        } catch (error) {
            window.showToast?.(error.message || L.ContractUnavailable, 'error');
            return false;
        }
    };

    const fetchRows = async (includeArchived) => {
        const url = `${endpoint}/plans${includeArchived ? '?includeArchived=true' : ''}`;
        return (await envelope(await fetch(url, { credentials: 'same-origin', headers: getAuthHeaders() })))?.items || [];
    };

    const reloadAndToast = async messageKey => {
        allRows = await fetchRows(appliedFilters.includeArchived);
        if (dt) { dt.clear(); dt.rows.add(allRows).draw(false); }
        window.showToast?.(messageKey, 'success');
    };

    const post = async (url, successKey, body) => {
        try {
            await envelope(await fetch(url, { method: 'POST', credentials: 'same-origin', headers: getAuthHeaders(), body: body ? JSON.stringify(body) : undefined }));
            await reloadAndToast(successKey);
        } catch (error) { window.showToast?.(error.message || L.ErrorOccurred, 'error'); }
    };

    const init = async () => {
        document.getElementById('skeleton-loader')?.classList.remove('d-none');
        registerTableFilter();
        try {
            if (!(await loadContract())) return;
            allRows = await fetchRows(false);
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

        const edit = event.target.closest('.js-edit-plan');
        if (edit) { event.preventDefault(); if (edit.dataset.id) window.location.assign(`${pageRoot}/Edit/${edit.dataset.id}`); return; }

        const confirm = event.target.closest('.js-confirm-plan');
        if (confirm) {
            event.preventDefault();
            window.showConfirm?.(L.ConfirmPlannedVisitConfirm, () => post(`${endpoint}/plans/${confirm.dataset.id}/confirm?expectedVersion=${confirm.dataset.version}`, L.RecordConfirmed),
                { type: 'question', confirmButtonText: L.ConfirmPlannedVisit });
            return;
        }

        const cancel = event.target.closest('.js-cancel-plan');
        if (cancel) {
            event.preventDefault();
            const reason = window.prompt(L.CancellationReasonPrompt || 'Reason');
            if (!reason) return;
            post(`${endpoint}/plans/${cancel.dataset.id}/cancel`, L.RecordCancelled, { cancellationReason: reason, expectedVersion: Number(cancel.dataset.version) });
            return;
        }

        const archive = event.target.closest('.js-archive-plan');
        if (!archive) return;
        event.preventDefault();
        window.showConfirm?.(L.ArchivePlannedVisitConfirm, () => post(`${endpoint}/plans/${archive.dataset.id}/archive?expectedVersion=${archive.dataset.version}`, L.RecordArchived),
            { type: 'warning', confirmButtonText: L.ArchivePlannedVisit });
    });

    init();
})(window, document);
