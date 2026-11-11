/**
 * MOD-0178 DL-3 — Depo Lokasyon Rolleri (WAREHOUSELOCATIONROLES) — DataTables index script.
 *
 * THE LIST IS A COMPONENT (BL-440): Save View, column state, filters, paging, export and the bulk bar come from DitenDataTable.createList.
 * This file writes only the screen's own business: columns, the role / storage-condition badges (inherited set: "üst düğümden" + its
 * source node, §11.3a), the QuickView, the form and the deletes.
 *
 * SERVER MODE, PROXY PROFILE (pack §11.2): '/Warehouse/WarehouseLocationRoles/api' translates the factory's wire for listLocationRoles; the
 * browser never calls the inventory service (5067), /api/warehouse or the Gateway. Writes only for warehouse.location-roles.manage (absent
 * otherwise); the service's [HasPermission] stays the authority.
 */
'use strict';

const WarehouseLocationRolesList = (function () {
    let list = null;

    const tableEl = document.querySelector('.datatables-warehouselocationroles');
    const endpoint = '/Warehouse/WarehouseLocationRoles/api';
    const bulkEndpoint = '/Warehouse/WarehouseLocationRoles/bulk';
    const L = () => window.L10n || {};
    const W = (key) => (L().Words || {})[key] || '';
    const byId = (id) => document.getElementById(id);
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const can = (key) => window.Permissions?.has?.(key) === true;
    const canManage = () => can('warehouse.location-roles.manage') && !!byId('formLocationRole');
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const newKey = () => (window.crypto?.randomUUID ? window.crypto.randomUUID() : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
        const r = Math.random() * 16 | 0;
        return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
    }));
    const token = () => document.querySelector('#formLocationRole input[name="__RequestVerificationToken"]')?.value || '';
    const offcanvas = (id) => (byId(id) ? window.bootstrap?.Offcanvas.getOrCreateInstance(byId(id)) : null);
    const format = (template, ...values) => values.reduce((text, value, i) => text.replace(`{${i}}`, value ?? ''), template || '');

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

    const failure = (answer) => answer?.json?.error?.message || W('Warehouse.Error.Generic');
    const showAlert = (id, text) => { const box = byId(id); if (box) { box.textContent = text || ''; box.classList.toggle('d-none', !text); } };
    const reload = () => list?.dt?.ajax?.reload(null, false);

    // ─── Badges ──────────────────────────────────────────────────────────────
    const roleBadges = (roles) => ((roles || []).length
        ? roles.map((r) => `<span class="badge bg-label-primary me-1">${esc(W(`Warehouse.Role.${r}`))}</span>`).join('')
        : `<span class="text-muted">${esc(W('NoRoles'))}</span>`);

    /** The set in force; an inherited one carries the "üst düğümden" badge and its source node (§11.3a). */
    const conditionBadges = (row) => {
        const codes = row.effective || [];
        if (!codes.length) return `<span class="text-muted">${esc(W('NoConditions'))}</span>`;
        const inherited = !(row.accepted || []).length && row.inheritedFrom;
        const badges = codes.map((c) => `<span class="badge bg-label-info me-1">${esc(W(`Warehouse.StorageCondition.${c}`))}</span>`).join('');
        return inherited
            ? `${badges}<span class="badge bg-label-secondary" data-inherited title="${esc(format(W('InheritedFrom'), row.inheritedFrom))}">${esc(W('InheritedBadge'))} · ${esc(row.inheritedFrom)}</span>`
            : badges;
    };

    // ─── Filters ─────────────────────────────────────────────────────────────
    const filterFields = [
        { id: 'filterWarehouse', key: 'warehouseId', kind: 'text' },
        { id: 'filterRole', key: 'role', kind: 'single' }
    ];

    const ajaxSelect = (el, url, extra = {}) => {
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
                data: (params) => ({ q: params.term || '' }),
                processResults: (data) => {
                    const notice = byId('roles-picker-unavailable');
                    if (notice) { notice.textContent = data?.available === false ? W('Warehouse.Error.ServiceUnavailable') : ''; notice.classList.toggle('d-none', data?.available !== false); }
                    return { results: (data?.items || []).map((item) => ({ id: item.value, text: item.text })) };
                }
            }
        }, extra));
    };

    const loadLookupOptions = async () => {
        ajaxSelect(byId('filterWarehouse'), '/Warehouse/WarehouseLocationRoles/warehouses',
            { dropdownParent: window.jQuery?.(document.body), dropdownCssClass: 'dt-inline-filter-dropdown', selectionCssClass: 'form-select form-select-sm', width: 'element' });
    };

    const onListError = (xhr) => {
        let message = '';
        try { message = JSON.parse(xhr?.responseText || '{}')?.error?.message || ''; } catch (error) { message = ''; }
        showAlert('roles-list-error', message || W('Warehouse.Error.ServiceUnavailable'));
    };

    // ─── QuickView ───────────────────────────────────────────────────────────
    const populateDetailsOffcanvas = (row) => {
        byId('offcanvasDetailsPreviewLabel').textContent = row.location || '-';
        byId('oc-subtitle').textContent = row.locationType ? W(`Warehouse.LocationType.${row.locationType}`) : '-';
        byId('oc-warehouse').textContent = row.warehouse || '-';
        byId('oc-roles').innerHTML = roleBadges(row.roles);
        byId('oc-accepted').innerHTML = (row.accepted || []).length
            ? row.accepted.map((c) => `<span class="badge bg-label-info me-1">${esc(W(`Warehouse.StorageCondition.${c}`))}</span>`).join('')
            : `<span class="text-muted">${esc(W('NoConditions'))}</span>`;
        byId('oc-effective').innerHTML = conditionBadges(row);
        byId('oc-note').textContent = row.note || '-';
        const edit = byId('oc-btn-edit');
        if (edit) { edit.classList.toggle('d-none', !canManage()); edit.dataset.editId = row.id; }
    };

    // ─── Form ────────────────────────────────────────────────────────────────
    let formState = null; // { mode, row, etag, key }

    const checked = (selector) => Array.from(document.querySelectorAll(selector)).filter((el) => el.checked).map((el) => el.value);
    const setChecked = (selector, values) => document.querySelectorAll(selector).forEach((el) => { el.checked = (values || []).includes(el.value); });

    const initLocationPicker = () => {
        const el = byId('roleLocation');
        if (el && window.jQuery && !window.jQuery(el).hasClass('select2-hidden-accessible')) ajaxSelect(el, '/Warehouse/WarehouseLocationRoles/nodes', { dropdownParent: window.jQuery('#offcanvasCreateEdit') });
    };

    const openCreate = () => {
        if (!canManage()) return;
        formState = { mode: 'create', row: null, etag: null, key: newKey() };
        showAlert('formLocationRoleAlert', '');
        byId('offcanvasCreateEditLabel').textContent = W('FormTitleCreate');
        byId('roleLocationBlock').classList.remove('d-none');
        byId('roleLocationReadonly').classList.add('d-none');
        if (window.jQuery) window.jQuery('#roleLocation').val(null).trigger('change');
        byId('roleLocationError').textContent = '';
        setChecked('[data-role-choice]', []);
        setChecked('[data-condition-choice]', []);
        byId('roleNote').value = '';
        offcanvas('offcanvasCreateEdit')?.show();
    };

    const openEdit = async (id) => {
        if (!canManage()) return;
        const answer = await getJson(`/Warehouse/WarehouseLocationRoles/role/${encodeURIComponent(id)}`);
        if (!answer.ok) { window.showToast?.(failure(answer), 'error'); return; }
        const row = answer.json.role;
        formState = { mode: 'edit', row, etag: answer.json.etag, key: newKey() };
        showAlert('formLocationRoleAlert', '');
        byId('offcanvasCreateEditLabel').textContent = W('FormTitleEdit');
        byId('roleLocationBlock').classList.add('d-none');
        byId('roleLocationReadonly').classList.remove('d-none');
        byId('roleLocationText').textContent = row.location || '';
        setChecked('[data-role-choice]', row.roles);
        setChecked('[data-condition-choice]', row.accepted);
        byId('roleNote').value = row.note || '';
        offcanvas('offcanvasCreateEdit')?.show();
    };

    const submitForm = async () => {
        if (!formState) return;
        showAlert('formLocationRoleAlert', '');
        const body = { idempotencyKey: formState.key, roles: checked('[data-role-choice]'), acceptedStorageConditions: checked('[data-condition-choice]'), note: byId('roleNote').value.trim() || null };
        let answer;
        if (formState.mode === 'create') {
            const location = byId('roleLocation')?.value;
            if (!location) { byId('roleLocationError').textContent = W('ValidationLocation'); return; }
            answer = await post('/Warehouse/WarehouseLocationRoles/create', Object.assign(body, { locationId: location }));
        } else {
            answer = await post(`/Warehouse/WarehouseLocationRoles/edit/${encodeURIComponent(formState.row.id)}`, Object.assign(body, { ifMatch: formState.etag }));
        }
        if (!answer.ok) { showAlert('formLocationRoleAlert', failure(answer)); return; }
        offcanvas('offcanvasCreateEdit')?.hide();
        window.showToast?.(answer.json?.message || '', 'success');
        reload();
    };

    // ─── Deletes ─────────────────────────────────────────────────────────────
    const deleteRow = ({ row }) => {
        if (!row?.id || !canManage()) return;
        window.showConfirm?.(W('DeleteConfirm'), async () => {
            const answer = await post(`/Warehouse/WarehouseLocationRoles/delete/${encodeURIComponent(row.id)}`, { idempotencyKey: newKey(), ifMatch: row.etag });
            if (!answer.ok) { window.showToast?.(failure(answer), 'error'); return; }
            window.showToast?.(answer.json?.message || '', 'success');
            reload();
        }, { entityName: row.location, type: 'danger', confirmButtonText: L().Delete });
    };

    const bulkDelete = ({ ids }) => {
        if (!ids?.length || !canManage()) return;
        const rows = (list?.dt?.rows?.().data?.().toArray?.() || []).filter((r) => ids.includes(r.id));
        window.showConfirm?.(format(W('BulkDeleteConfirm'), rows.length), async () => {
            const answer = await post(bulkEndpoint, { idempotencyKey: newKey(), items: rows.map((r) => ({ id: r.id, version: r.version })) });
            if (!answer.ok) { window.showToast?.(failure(answer), 'error'); return; }
            window.showToast?.(format(W('BulkDeleteResult'), answer.json.deleted, rows.length), answer.json.deleted === rows.length ? 'success' : 'warning');
            reload();
        }, { entityName: String(rows.length), type: 'danger', confirmButtonText: L().Delete });
    };

    const renderAction = (full) => {
        const rowJson = JSON.stringify(full).replace(/'/g, '&#39;');
        const manage = canManage();
        return window.DitenDataTable.renderActions([
            { key: 'quickView', className: 'js-quick-view', icon: 'bx bx-show', attrs: { 'data-json': rowJson, title: L().QuickView, 'aria-label': L().QuickView } },
            { key: 'edit', visible: manage, icon: 'bx bx-edit', text: W('Warehouse.Action.EditRole'), attrs: { 'data-id': full.id, 'data-json': rowJson } },
            { key: 'delete', visible: manage, className: 'text-danger', icon: 'bx bx-trash', text: W('Warehouse.Action.DeleteRole'), attrs: { 'data-json': rowJson } }
        ]);
    };

    // ─── The list ────────────────────────────────────────────────────────────
    const initDataTable = async () => {
        if (!tableEl) return;
        const manage = canManage() && !!byId('bulkActionBar');
        list = await window.DitenDataTable.createList({
            tableEl,
            dataMode: 'server',
            ajax: { url: endpoint, type: 'GET', error: onListError },
            onResponse: () => byId('roles-list-error')?.classList.add('d-none'),
            export: { mode: 'server', url: '/Warehouse/WarehouseLocationRoles/export', fileName: 'warehouse-location-roles' },
            bulk: manage ? {
                bulkBarSelector: '#bulkActionBar', bulkCountSelector: '#bulkSelectedCount', bulkActionSelector: '[data-bulk-action]', checkboxSelector: '.dt-checkboxes',
                clearSelectionSelector: '#btnClearSelection', selectAllSelector: '.dt-checkboxes-select-all', onBulkAction: { delete: bulkDelete }
            } : undefined,
            actions: { onRowAction: { edit: ({ row }) => row?.id && openEdit(row.id), delete: deleteRow } },
            toolbar: { addNewText: canManage() ? (L().AddNew || '') : '', onAddNew: openCreate, colvisColumns: [2, 3, 4, 5, 6, 7] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'Warehouse', pageKey: 'WarehouseLocationRoles', saveViewColumnIndexes: [2, 3, 4, 5, 6, 7], defaultVisibleColumnIndexes: [2, 3, 4, 5, 6, 7], baseOrder: [] },
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateDetailsOffcanvas },
            config: {
                ordering: false,
                searching: false,
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'id', name: 'checkbox' },
                    { data: 'location', name: 'location' },
                    { data: 'locationType', name: 'locationType' },
                    { data: 'warehouse', name: 'warehouse' },
                    { data: 'roles', name: 'roles' },
                    { data: 'effective', name: 'conditions' },
                    { data: 'note', name: 'note' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${esc(data)}">` },
                    { targets: 2, render: (data) => `<span class="fw-medium text-heading font-monospace">${esc(data)}</span>` },
                    { targets: 3, render: (data) => esc(data ? W(`Warehouse.LocationType.${data}`) : '') },
                    { targets: 4, render: (data) => esc(data) },
                    { targets: 5, render: (data) => roleBadges(data) },
                    { targets: 6, render: (data, type, full) => conditionBadges(full) },
                    { targets: 7, render: (data) => `<small class="text-muted">${esc(data)}</small>` },
                    { targets: -1, title: L().Actions, searchable: false, orderable: false, className: 'cell-fit all', render: (data, type, full) => renderAction(full) }
                ]
            }
        });
    };

    const bindEvents = () => {
        byId('btnSaveLocationRole')?.addEventListener('click', submitForm);
        byId('offcanvasCreateEdit')?.addEventListener('show.bs.offcanvas', () => window.setTimeout(initLocationPicker, 50));
        byId('oc-btn-edit')?.addEventListener('click', () => { const id = byId('oc-btn-edit')?.dataset.editId; if (id) openEdit(id); });
    };

    return {
        init: function () {
            initDataTable();
            bindEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => WarehouseLocationRolesList.init());
