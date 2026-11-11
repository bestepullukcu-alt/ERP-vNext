/**
 * MOD-0181 DL-4 — Sayımlar Panosu (CYCLECOUNTS) — DataTables index script.
 *
 * THE LIST IS A COMPONENT (BL-440): Save View, column state, filters, paging and the bulk bar come from DitenDataTable.createList. This file writes
 * only the screen's own business: the KPI cards (AC-40 — the service counts them at read time), columns and badges, the QuickView (timeline, "Başlat",
 * "İptal", "Sayım formuna git"), the 8-field Draft form and "Toplu sil" (Draft only, a reason).
 *
 * SERVER MODE, PROXY PROFILE (pack §11): '/Warehouse/CycleCounts/api' translates the factory's wire for listCycleCounts; the browser never calls the
 * inventory service (5067), /api/warehouse or the Gateway. A control whose key the reader lacks is absent; the service's [HasPermission] and state
 * rules stay the authority. The Draft row action is "İptal" (cycle-count.counts.cancel) — there is no "Sil" (G-0181-01).
 */
'use strict';

const CycleCountsList = (function () {
    let list = null;

    const tableEl = document.querySelector('.datatables-cyclecounts');
    const root = '/Warehouse/CycleCounts';
    const endpoint = '/Warehouse/CycleCounts/api';
    const bulkEndpoint = '/Warehouse/CycleCounts/bulk'; // "Toplu sil" (bulkDeleteCycleCounts, Draft only)
    const L = () => window.L10n || {};
    const W = (key) => (L().Words || {})[key] || '';
    const byId = (id) => document.getElementById(id);
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const can = (key) => window.Permissions?.has?.(key) === true;
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const newKey = () => (window.crypto?.randomUUID ? window.crypto.randomUUID() : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
        const r = Math.random() * 16 | 0;
        return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
    }));
    const token = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const offcanvas = (id) => (byId(id) ? window.bootstrap?.Offcanvas.getOrCreateInstance(byId(id)) : null);
    const modal = (id) => (byId(id) ? window.bootstrap?.Modal.getOrCreateInstance(byId(id)) : null);
    const format = (template, ...values) => values.reduce((text, value, i) => text.replace(`{${i}}`, value ?? ''), template || '');
    const dateText = (value) => (value ? new Date(value).toLocaleString(document.documentElement.lang || undefined) : '-');

    const getJson = async (url) => {
        const res = await fetch(url, { credentials: 'same-origin', headers: getAuthHeaders() });
        let json = null;
        try { json = await res.json(); } catch (error) { json = null; }
        return { ok: res.ok, status: res.status, json };
    };

    const post = async (url, body) => {
        const headers = Object.assign({}, getAuthHeaders(true), { RequestVerificationToken: token() });
        const res = await fetch(url, { method: 'POST', credentials: 'same-origin', headers, body: JSON.stringify(body) });
        let json = null;
        try { json = await res.json(); } catch (error) { json = null; }
        return { ok: res.ok, status: res.status, json };
    };

    const failure = (answer) => answer?.json?.error?.message || W('CycleCount.Error.Generic');
    const showAlert = (id, text) => { const box = byId(id); if (box) { box.textContent = text || ''; box.classList.toggle('d-none', !text); } };
    const reload = () => { list?.dt?.ajax?.reload(null, false); loadKpis(); };

    // ─── Badges ──────────────────────────────────────────────────────────────
    const statusTone = { Draft: 'secondary', Released: 'info', Counting: 'primary', Submitted: 'warning', Closed: 'success', Cancelled: 'dark' };
    const statusBadge = (status) => `<span class="badge bg-label-${statusTone[status] || 'secondary'}">${esc(W(`CycleCount.Status.${status}`))}</span>`;
    const typeText = (row) => `${esc(W(`CycleCount.Type.${row.countType}`))}${row.blind ? ` <span class="badge bg-label-dark ms-1">${esc(W('BlindBadge'))}</span>` : ''}`;

    // ─── KPI cards (AC-40) ───────────────────────────────────────────────────
    const loadKpis = async () => {
        const warehouse = byId('filterWarehouse')?.value || '';
        const answer = await getJson(`${root}/dashboard${warehouse ? `?warehouseId=${encodeURIComponent(warehouse)}` : ''}`);
        document.querySelectorAll('[data-cc-kpi]').forEach((card) => {
            const name = card.getAttribute('data-cc-kpi');
            const value = card.querySelector('[data-cc-value]');
            if (!value) return;
            if (!answer.ok) { value.textContent = '–'; return; }
            const raw = answer.json?.[name];
            if (name === 'accuracyPercent') value.textContent = raw ? format(W('Kpi.PercentValue'), raw) : '–';
            else if (name === 'manualCountMovements') value.textContent = raw === undefined || raw === null ? W('Kpi.SelectWarehouse') : String(raw);
            else value.textContent = String(raw ?? 0);
        });
    };

    // ─── Filters ─────────────────────────────────────────────────────────────
    const filterFields = [
        { id: 'filterStatus', key: 'status', kind: 'multi' },
        { id: 'filterWarehouse', key: 'warehouseId', kind: 'text' }
    ];

    const ajaxSelect = (el, url, extra = {}, data = null) => {
        if (!el || !window.jQuery?.fn?.select2) return;
        const $el = window.jQuery(el);
        $el.select2(Object.assign({
            placeholder: $el.data('placeholder') || '',
            allowClear: true,
            width: '100%',
            language: { searching: () => L().Searching || '', noResults: () => L().NoResults || '' },
            ajax: {
                url,
                dataType: 'json',
                delay: 250,
                data: (params) => Object.assign({ q: params.term || '' }, data ? data() : {}),
                processResults: (answer) => {
                    const notice = byId('cc-picker-unavailable');
                    if (notice) { notice.textContent = answer?.available === false ? W('CycleCount.Error.ServiceUnavailable') : ''; notice.classList.toggle('d-none', answer?.available !== false); }
                    return { results: (answer?.items || []).map((item) => ({ id: item.value, text: item.text })) };
                }
            }
        }, extra));
    };

    const loadLookupOptions = async () => {
        ajaxSelect(byId('filterWarehouse'), `${root}/warehouses`,
            { dropdownParent: window.jQuery?.(document.body), dropdownCssClass: 'dt-inline-filter-dropdown', selectionCssClass: 'form-select form-select-sm', width: 'element' });
        window.jQuery?.(byId('filterWarehouse'))?.on('change', loadKpis);
    };

    const onListError = (xhr) => {
        let message = '';
        try { message = JSON.parse(xhr?.responseText || '{}')?.error?.message || ''; } catch (error) { message = ''; }
        showAlert('cc-list-error', message || W('CycleCount.Error.ServiceUnavailable'));
    };

    // ─── QuickView ───────────────────────────────────────────────────────────
    let current = null; // { count, etag }

    const populateDetailsOffcanvas = async (row) => {
        byId('offcanvasDetailsPreviewLabel').textContent = row.number || '-';
        byId('oc-subtitle').innerHTML = statusBadge(row.status);
        byId('oc-timeline').innerHTML = `<li class="text-muted">${esc(W('Loading'))}</li>`;
        const answer = await getJson(`${root}/count/${encodeURIComponent(row.countId)}`);
        if (!answer.ok) { byId('oc-timeline').innerHTML = `<li class="text-danger">${esc(failure(answer))}</li>`; return; }
        const c = answer.json.count;
        current = { count: c, etag: answer.json.etag };
        byId('oc-warehouse').textContent = c.warehouse || '-';
        byId('oc-type').innerHTML = typeText(c);
        byId('oc-scope').textContent = c.countType === 'Full' ? W('ScopeFull') : (c.scope || []).join(', ') || '-';
        byId('oc-planned').textContent = c.plannedDate || '-';
        byId('oc-assignee').textContent = c.assignedToName || '-';
        byId('oc-snapshot').textContent = c.snapshotAt ? dateText(c.snapshotAt) : '-';
        byId('oc-lines').textContent = String(c.lineCount ?? 0);
        byId('oc-recountof').textContent = c.recountOfId ? W('RecountOfYes') : '-';
        byId('oc-note').textContent = c.note || '-';
        byId('oc-cancel').textContent = c.cancelReason || '-';
        const readonly = c.status === 'Closed' ? W('ReadonlyClosed') : c.status === 'Cancelled' ? W('ReadonlyCancelled') : '';
        showAlert('oc-readonly', readonly);
        const timeline = answer.json.timeline;
        byId('oc-timeline').innerHTML = timeline === null || timeline === undefined
            ? `<li class="text-muted">${esc(W('TimelineUnavailable'))}</li>`
            : (timeline || []).slice().reverse().map((e) => `<li><span class="fw-medium">${esc(W(`CycleCount.Event.${e.kind}`))}</span>`
                + ` <small class="text-muted">${esc(dateText(e.atUtc))}${e.actorName ? ` · ${esc(e.actorName)}` : ''}</small></li>`).join('');
        const sheet = byId('oc-btn-sheet');
        if (sheet) { sheet.classList.toggle('d-none', !['Released', 'Counting', 'Submitted', 'Closed'].includes(c.status)); sheet.href = `${root}/Sheet/${encodeURIComponent(c.countId)}`; }
        byId('oc-btn-edit')?.classList.toggle('d-none', c.status !== 'Draft');
        byId('oc-btn-release')?.classList.toggle('d-none', c.status !== 'Draft');
        byId('oc-btn-cancel')?.classList.toggle('d-none', !['Draft', 'Released', 'Counting'].includes(c.status));
    };

    // ─── Release / cancel / bulk delete ──────────────────────────────────────
    const release = () => {
        if (!current || !can('cycle-count.counts.update')) return;
        window.showConfirm?.(W('ReleaseConfirm'), async () => {
            const answer = await post(`${root}/release/${encodeURIComponent(current.count.countId)}`, { idempotencyKey: newKey(), ifMatch: current.etag });
            if (!answer.ok) { window.showToast?.(failure(answer), 'error'); return; }
            offcanvas('offcanvasDetailsPreview')?.hide();
            window.showToast?.(answer.json?.message || '', 'success');
            reload();
        }, { entityName: current.count.number, type: 'warning', confirmButtonText: W('CycleCount.Action.Release') });
    };

    let reasonTarget = null; // { kind: 'cancel' | 'bulk', ... }

    const askReason = (target, title) => {
        reasonTarget = target;
        byId('ccReasonModalLabel').textContent = title;
        byId('ccReason').value = '';
        showAlert('ccReasonAlert', '');
        modal('ccReasonModal')?.show();
    };

    const confirmReason = async () => {
        if (!reasonTarget) return;
        const reason = byId('ccReason').value.trim();
        if (!reason) { showAlert('ccReasonAlert', W('CycleCount.Error.COUNT_REASON_REQUIRED')); return; }
        let answer;
        if (reasonTarget.kind === 'cancel') {
            answer = await post(`${root}/cancel/${encodeURIComponent(reasonTarget.id)}`, { idempotencyKey: newKey(), ifMatch: reasonTarget.etag, reason });
            if (!answer.ok) { showAlert('ccReasonAlert', failure(answer)); return; }
            window.showToast?.(answer.json?.message || '', 'success');
        } else {
            answer = await post(bulkEndpoint, { idempotencyKey: newKey(), reason, items: reasonTarget.rows.map((r) => ({ id: r.countId, version: r.version })) });
            if (!answer.ok) { showAlert('ccReasonAlert', failure(answer)); return; }
            window.showToast?.(format(W('BulkDeleteResult'), answer.json.deleted, reasonTarget.rows.length), answer.json.deleted === reasonTarget.rows.length ? 'success' : 'warning');
        }
        modal('ccReasonModal')?.hide();
        offcanvas('offcanvasDetailsPreview')?.hide();
        reasonTarget = null;
        reload();
    };

    const cancelRow = (row) => {
        if (!row?.countId || !can('cycle-count.counts.cancel')) return;
        askReason({ kind: 'cancel', id: row.countId, etag: row.etag }, format(W('CancelTitle'), row.number));
    };

    const bulkDelete = ({ ids }) => {
        if (!ids?.length || !can('cycle-count.counts.bulk-delete')) return;
        const rows = (list?.dt?.rows?.().data?.().toArray?.() || []).filter((r) => ids.includes(r.countId));
        askReason({ kind: 'bulk', rows }, format(W('BulkDeleteTitle'), rows.length));
    };

    // ─── Form (8 fields, pack §4.1) ──────────────────────────────────────────
    let formState = null; // { mode, row, etag, key }

    const initFormPickers = () => {
        const $ = window.jQuery;
        if (!$) return;
        const parent = $('#offcanvasCreateEdit');
        if (!$('#ccWarehouse').hasClass('select2-hidden-accessible')) ajaxSelect(byId('ccWarehouse'), `${root}/warehouses`, { dropdownParent: parent });
        if (!$('#ccScope').hasClass('select2-hidden-accessible')) ajaxSelect(byId('ccScope'), `${root}/nodes`, { dropdownParent: parent }, () => ({ warehouseId: byId('ccWarehouse').value }));
        if (!$('#ccSkus').hasClass('select2-hidden-accessible')) ajaxSelect(byId('ccSkus'), `${root}/skus`, { dropdownParent: parent });
        if (!$('#ccAssignee').hasClass('select2-hidden-accessible')) {
            ajaxSelect(byId('ccAssignee'), `${root}/people`, { dropdownParent: parent });
        }
    };

    const setSelect = (id, options) => {
        const $el = window.jQuery?.(byId(id));
        if (!$el) return;
        $el.empty();
        (options || []).forEach((o) => $el.append(new Option(o.text, o.value, true, true)));
        $el.trigger('change');
    };

    const toggleScope = () => {
        const full = document.querySelector('input[name="ccType"]:checked')?.value === 'Full';
        byId('ccScopeBlock')?.classList.toggle('d-none', full);
    };

    const openCreate = () => {
        if (!can('cycle-count.counts.create')) return;
        formState = { mode: 'create', row: null, etag: null, key: newKey() };
        showAlert('formCycleCountAlert', '');
        byId('offcanvasCreateEditLabel').textContent = W('FormTitleCreate');
        offcanvas('offcanvasCreateEdit')?.show();
        window.setTimeout(() => {
            initFormPickers();
            setSelect('ccWarehouse', []);
            setSelect('ccScope', []);
            setSelect('ccSkus', []);
            setSelect('ccAssignee', []);
            byId('ccTypeCycle').checked = true;
            byId('ccBlind').checked = false;
            byId('ccPlannedDate').value = new Date().toISOString().slice(0, 10);
            byId('ccNote').value = '';
            ['ccWarehouseError', 'ccScopeError'].forEach((id) => { byId(id).textContent = ''; });
            toggleScope();
        }, 50);
    };

    const openEdit = async (id) => {
        if (!can('cycle-count.counts.update')) return;
        const answer = await getJson(`${root}/count/${encodeURIComponent(id)}`);
        if (!answer.ok) { window.showToast?.(failure(answer), 'error'); return; }
        const c = answer.json.count;
        formState = { mode: 'edit', row: c, etag: answer.json.etag, key: newKey() };
        showAlert('formCycleCountAlert', '');
        byId('offcanvasCreateEditLabel').textContent = W('FormTitleEdit');
        offcanvas('offcanvasCreateEdit')?.show();
        window.setTimeout(() => {
            initFormPickers();
            setSelect('ccWarehouse', [{ value: c.warehouseId, text: c.warehouse }]);
            setSelect('ccScope', (c.scopeLocationIds || []).map((value, i) => ({ value, text: (c.scope || [])[i] || value })));
            setSelect('ccSkus', (c.scopeSkuIds || []).map((value) => ({ value, text: value })));
            setSelect('ccAssignee', c.assignedTo ? [{ value: c.assignedTo, text: c.assignedToName || c.assignedTo }] : []);
            byId(c.countType === 'Full' ? 'ccTypeFull' : 'ccTypeCycle').checked = true;
            byId('ccBlind').checked = !!c.blind;
            byId('ccPlannedDate').value = c.plannedDate || '';
            byId('ccNote').value = c.note || '';
            toggleScope();
        }, 50);
    };

    const values = (id) => (window.jQuery?.(byId(id))?.val() || []).filter(Boolean);

    const submitForm = async () => {
        if (!formState) return;
        showAlert('formCycleCountAlert', '');
        ['ccWarehouseError', 'ccScopeError'].forEach((id) => { byId(id).textContent = ''; });
        const countType = document.querySelector('input[name="ccType"]:checked')?.value || 'Cycle';
        const body = {
            idempotencyKey: formState.key,
            warehouseId: byId('ccWarehouse').value,
            countType,
            blind: byId('ccBlind').checked,
            scopeLocationIds: countType === 'Full' ? [] : values('ccScope'),
            scopeSkuIds: values('ccSkus'),
            plannedDate: byId('ccPlannedDate').value,
            assignedTo: byId('ccAssignee').value || null,
            note: byId('ccNote').value.trim() || null
        };
        if (!body.warehouseId) { byId('ccWarehouseError').textContent = W('ValidationWarehouse'); return; }
        if (countType === 'Cycle' && !body.scopeLocationIds.length) { byId('ccScopeError').textContent = W('ValidationScope'); return; }
        if (!body.plannedDate) { showAlert('formCycleCountAlert', W('ValidationPlannedDate')); return; }
        const answer = formState.mode === 'create'
            ? await post(`${root}/create`, body)
            : await post(`${root}/edit/${encodeURIComponent(formState.row.countId)}`, Object.assign(body, { ifMatch: formState.etag }));
        if (!answer.ok) { showAlert('formCycleCountAlert', failure(answer)); return; }
        offcanvas('offcanvasCreateEdit')?.hide();
        window.showToast?.(answer.json?.message || '', 'success');
        reload();
    };

    const renderAction = (full) => {
        const rowJson = JSON.stringify(full).replace(/'/g, '&#39;');
        return window.DitenDataTable.renderActions([
            { key: 'quickView', className: 'js-quick-view', icon: 'bx bx-show', attrs: { 'data-json': rowJson, title: L().QuickView, 'aria-label': L().QuickView } },
            { key: 'sheet', visible: ['Released', 'Counting', 'Submitted', 'Closed'].includes(full.status), icon: 'bx bx-list-check', text: W('CycleCount.Action.OpenSheet'),
              attrs: { 'data-json': rowJson } },
            { key: 'edit', visible: can('cycle-count.counts.update') && full.status === 'Draft', icon: 'bx bx-edit', text: W('CycleCount.Action.Edit'), attrs: { 'data-json': rowJson } },
            { key: 'cancel', visible: can('cycle-count.counts.cancel') && ['Draft', 'Released', 'Counting'].includes(full.status), className: 'text-danger', icon: 'bx bx-block',
              text: W('CycleCount.Action.Cancel'), attrs: { 'data-json': rowJson } }
        ]);
    };

    // ─── The list ────────────────────────────────────────────────────────────
    const initDataTable = async () => {
        if (!tableEl) return;
        const bulk = can('cycle-count.counts.bulk-delete') && !!byId('bulkActionBar');
        list = await window.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            export: { mode: 'server', url: '/Warehouse/CycleCounts/export', fileName: 'cycle-counts' },
            ajax: { url: endpoint, type: 'GET', error: onListError },
            onResponse: () => byId('cc-list-error')?.classList.add('d-none'),
            bulk: bulk ? {
                bulkBarSelector: '#bulkActionBar', bulkCountSelector: '#bulkSelectedCount', bulkActionSelector: '[data-bulk-action]', checkboxSelector: '.dt-checkboxes',
                clearSelectionSelector: '#btnClearSelection', selectAllSelector: '.dt-checkboxes-select-all', onBulkAction: { delete: bulkDelete }
            } : undefined,
            actions: {
                onRowAction: {
                    sheet: ({ row }) => { if (row?.countId) window.location.href = `${root}/Sheet/${encodeURIComponent(row.countId)}`; },
                    edit: ({ row }) => row?.countId && openEdit(row.countId),
                    cancel: ({ row }) => cancelRow(row)
                }
            },
            toolbar: { addNewText: can('cycle-count.counts.create') ? (L().AddNew || '') : '', onAddNew: openCreate, colvisColumns: [3, 4, 5, 6, 7, 8, 9] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'Warehouse', pageKey: 'CycleCounts', saveViewColumnIndexes: [3, 4, 5, 6, 7, 8, 9], defaultVisibleColumnIndexes: [3, 4, 5, 6, 7, 8, 9], baseOrder: [] },
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateDetailsOffcanvas },
            config: {
                ordering: false,
                searching: true,
                columns: [
                    { data: 'countId', name: 'control' },
                    { data: 'countId', name: 'checkbox' },
                    { data: 'number', name: 'number' },
                    { data: 'status', name: 'status' },
                    { data: 'warehouse', name: 'warehouse' },
                    { data: 'countType', name: 'type' },
                    { data: 'plannedDate', name: 'plannedDate' },
                    { data: 'assignedToName', name: 'assignee' },
                    { data: 'lineCount', name: 'lines' },
                    { data: 'createdAt', name: 'created' },
                    { data: 'countId', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit',
                      render: (data, type, full) => (full.status === 'Draft' ? `<input type="checkbox" class="dt-checkboxes form-check-input" value="${esc(data)}">` : '') },
                    { targets: 2, render: (data) => `<span class="fw-medium text-heading font-monospace">${esc(data)}</span>` },
                    { targets: 3, render: (data) => statusBadge(data) },
                    { targets: 4, render: (data) => esc(data) },
                    { targets: 5, render: (data, type, full) => typeText(full) },
                    { targets: 6, render: (data) => esc(data) },
                    { targets: 7, render: (data) => esc(data || '-') },
                    { targets: 8, render: (data) => esc(data ?? 0) },
                    { targets: 9, render: (data, type, full) => `<small class="text-muted">${esc(dateText(data))}${full.createdByName ? ` · ${esc(full.createdByName)}` : ''}</small>` },
                    { targets: -1, title: L().Actions, searchable: false, orderable: false, className: 'cell-fit all', render: (data, type, full) => renderAction(full) }
                ]
            }
        });
    };

    const bindEvents = () => {
        byId('btnSaveCycleCount')?.addEventListener('click', submitForm);
        document.querySelectorAll('input[name="ccType"]').forEach((el) => el.addEventListener('change', toggleScope));
        byId('oc-btn-edit')?.addEventListener('click', () => current && openEdit(current.count.countId));
        byId('oc-btn-release')?.addEventListener('click', release);
        byId('oc-btn-cancel')?.addEventListener('click', () => current && cancelRow({ countId: current.count.countId, etag: current.etag, number: current.count.number }));
        byId('ccReasonConfirm')?.addEventListener('click', confirmReason);
    };

    return {
        init: function () {
            initDataTable();
            bindEvents();
            loadKpis();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => CycleCountsList.init());
