/**
 * Golden Reference Slim — DataTables Index Script
 * Diten ERP vNext | DevEnablement/GoldenReferenceSlim
 *
 * SLIM PATTERN (≤8 form fields): create/edit in an offcanvas, quick view in an offcanvas.
 *
 * THE LIST IS A COMPONENT (BL-440 package 2). Everything a list does the same way on every screen — the Save
 * View state machine, column visibility/order, filter normalisation, the inline filter bar, the responsive
 * modal return, form submission, offcanvas plumbing — lives in DitenDataTable.createList. This file writes
 * ONLY what is Golden-Slim's own: its columns and renderers, its badge maps, how it fills the quick view and
 * the form, and its endpoints. Before the factory this file was 991 lines; 79 of its names were plumbing.
 */
'use strict';

const GoldenReferenceSlimList = (function () {
    let list = null;

    const dtTableEl = document.querySelector('.datatables-goldenreferenceslim');
    const apiUrl = window.API?.deven;
    const L = () => window.L10n || {};
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const byId = (id) => document.getElementById(id);

    // ─── The page's own vocabulary ───────────────────────────────────────────
    const getStatusMap = () => ({
        true: { title: L().Active, class: 'bg-label-success' },
        false: { title: L().Passive, class: 'bg-label-secondary' }
    });
    const getReferenceTypeMap = () => ({
        'Standard': L().ReferenceTypeStandard || 'Standard',
        'Custom': L().ReferenceTypeCustom || 'Custom',
        'Pro': L().ReferenceTypePro || 'Pro'
    });

    // ─── Filters: the fields and the ONE rule the factory cannot guess (status is a boolean on the row) ──
    const filterFields = [
        { id: 'filterStatus', key: 'status', kind: 'multi', matches: (row, selected) => !selected.length || selected.includes(row.isActive ? 'Active' : 'Passive') },
        { id: 'filterReferenceType', key: 'referenceType', kind: 'multi' },
        { id: 'filterPriority', key: 'priority', kind: 'single' }
    ];

    const loadLookupOptions = async ({ fillSelect }) => {
        const refSelect = byId('filterReferenceType');
        const prioritySelect = byId('filterPriority');
        if (!refSelect || !prioritySelect) return;
        const res = await fetch('/GoldenReferenceSlim/lookups', { method: 'GET', credentials: 'same-origin', headers: getAuthHeaders() });
        if (!res.ok) return;
        const data = await res.json();
        fillSelect(refSelect, data?.referenceTypes);
        fillSelect(prioritySelect, (data?.priorities || []).map((item) => ({
            value: item.value,
            text: item.text || `${L().LevelPrefix || ''} ${item.value}`.trim()
        })), { showAllText: L().ShowAll || '' });
    };

    // ─── Quick view: only how the fields are filled ──────────────────────────
    const populateDetailsOffcanvas = (data) => {
        byId('oc-title').innerText = data.name || '-';
        byId('oc-subtitle').innerText = data.code || '-';
        byId('oc-code').innerText = data.code || '-';
        byId('oc-name').innerText = data.name || '-';
        byId('oc-type').innerText = getReferenceTypeMap()[data.referenceType] || data.referenceType || '-';
        byId('oc-priority').innerText = data.priority != null ? String(data.priority) : '-';
        byId('oc-desc').innerText = data.description || '-';

        const status = getStatusMap()[String(!!data.isActive)] || { title: L().Unknown, class: 'bg-label-primary' };
        const statusEl = byId('oc-status');
        statusEl.className = `badge ${status.class}`;
        statusEl.innerText = status.title || '-';

        const priorityDot = byId('oc-priority-dot');
        if (priorityDot) {
            const priority = Number(data.priority || 0);
            priorityDot.className = 'backbone-priority-dot';
            if (priority >= 70) priorityDot.classList.add('is-high');
            else if (priority > 0) priorityDot.classList.add('is-medium');
        }
        const editBtn = byId('oc-btn-edit');
        if (editBtn) editBtn.dataset.editId = data.id;
    };

    // ─── Form: only the fields ───────────────────────────────────────────────
    const setReferenceType = (value) => {
        if (window.jQuery && $('#slimReferenceType').hasClass('select2-hidden-accessible')) $('#slimReferenceType').val(value || '').trigger('change');
        else if (byId('slimReferenceType')) byId('slimReferenceType').value = value || '';
    };

    const resetFormFields = () => {
        byId('slimItemId').value = '';
        byId('slimCode').value = '';
        byId('slimName').value = '';
        byId('slimDescription').value = '';
        byId('slimPriority').value = '0';
        byId('slimIsActive').checked = true;
        setReferenceType('');
    };

    const loadFormFields = async (id) => {
        const res = await fetch(`/GoldenReferenceSlim/get/${id}`, { credentials: 'same-origin', headers: getAuthHeaders() });
        const json = await res.json();
        if (!json.success || !json.data) throw new Error('Failed to load item.');
        const d = json.data;
        byId('slimItemId').value = d.id || '';
        byId('slimCode').value = d.code || '';
        byId('slimName').value = d.name || '';
        byId('slimDescription').value = d.description || '';
        byId('slimPriority').value = d.priority ?? 0;
        byId('slimIsActive').checked = !!d.isActive;
        setReferenceType(d.referenceType);
    };

    const submitForm = async (formData, isEdit, { editingId, headers }) => {
        const url = isEdit ? `/GoldenReferenceSlim/edit/${editingId}` : '/GoldenReferenceSlim/create';
        const res = await fetch(url, { method: 'POST', credentials: 'same-origin', headers, body: formData });
        return res.json();
    };

    const initOffcanvasSelect2 = () => {
        if (!window.jQuery || !$.fn.select2) return;
        const $el = $('#slimReferenceType');
        if ($el.hasClass('select2-hidden-accessible')) $el.select2('destroy');
        // THE PLACEHOLDER COMES FROM THE EMPTY OPTION — the only place it is localized (measured: the markup carries
        // no data-placeholder, and an empty placeholder hides the option's own text). One localized source.
        $el.select2({
            dropdownParent: $('#offcanvasCreateEdit'),
            placeholder: $el.data('placeholder') || $el.find('option[value=""]').text() || '',
            allowClear: true,
            width: '100%'
        });
    };

    // ─── Delete: the endpoints are the page's ────────────────────────────────
    const deleteRow = ({ row }) => {
        if (!row?.id) return;
        window.showConfirm?.(L().AreYouSure, async () => {
            try {
                const res = await fetch(`${apiUrl}/api/golden-reference-slim/${row.id}`, { method: 'DELETE', credentials: 'include', headers: getAuthHeaders() });
                if (!res.ok) throw new Error('Delete failed.');
                list.reload('RecordDeleted');
            } catch (error) {
                console.error(error);
                window.showToast?.(L().ErrorOccurred, 'error');
            }
        }, { entityName: row.name, type: 'danger', confirmButtonText: L().Delete });
    };

    const bulkOptions = {
        bulkBarSelector: '#bulkActionBar',
        bulkCountSelector: '#bulkSelectedCount',
        bulkActionSelector: '[data-bulk-action]',
        checkboxSelector: '.dt-checkboxes',
        clearSelectionSelector: '#btnClearSelection',
        selectAllSelector: '.dt-checkboxes-select-all',
        onBulkAction: {
            delete: ({ ids }) => {
                if (!ids.length) return;
                const confirmText = (L().BulkDeleteConfirm || '').replace('{0}', ids.length);
                window.showConfirm?.(confirmText, async () => {
                    try {
                        const res = await fetch(`${apiUrl}/api/golden-reference-slim/bulk`, { method: 'DELETE', credentials: 'include', headers: getAuthHeaders(true), body: JSON.stringify(ids) });
                        if (!res.ok) throw new Error('Bulk delete failed.');
                        list.reload('BulkDeleteSuccess', String(ids.length));
                    } catch (error) {
                        console.error(error);
                        window.showToast?.(L().ErrorOccurred, 'error');
                    }
                }, { entityName: String(ids.length), type: 'danger', confirmButtonText: L().Delete });
            }
        }
    };

    // ─── The list ────────────────────────────────────────────────────────────
    const initDataTable = async () => {
        if (!dtTableEl) return;
        if (!apiUrl) { console.error('[GoldenReferenceSlim] window.API.deven is required.'); return; }

        list = await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'client',
            bulk: bulkOptions,
            ajax: { url: apiUrl + '/api/golden-reference-slim', type: 'GET', xhrFields: { withCredentials: true } },
            actions: { onRowAction: { delete: deleteRow } },
            toolbar: { addNewText: L().AddNew, onAddNew: () => list.openCreate(), exportColumns: [2, 3, 4, 5, 6], colvisColumns: [2, 3, 4, 5, 6] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadLookupOptions },
            savedView: { moduleKey: 'DevEnablement', pageKey: 'GoldenReferenceSlim', saveViewColumnIndexes: [2, 3, 4, 5, 6], defaultVisibleColumnIndexes: [2, 3, 4, 5, 6], baseOrder: [[2, 'asc']] },
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateDetailsOffcanvas },
            form: { formId: 'formGoldenReferenceSlim', offcanvasId: 'offcanvasCreateEdit', alertId: 'formGoldenReferenceSlimAlert', saveBtnId: 'btnSaveSlim', labelId: 'offcanvasCreateEditLabel', reset: resetFormFields, load: loadFormFields, submit: submitForm },
            config: {
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'id', name: 'checkbox' },
                    { data: 'code', name: 'code' },
                    { data: 'name', name: 'name' },
                    { data: 'referenceType', name: 'referenceType' },
                    { data: 'priority', name: 'priority' },
                    { data: 'isActive', name: 'isActive' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, orderable: false, searchable: false, responsivePriority: 3, className: 'dt-checkboxes-cell cell-fit', render: (data) => `<input type="checkbox" class="dt-checkboxes form-check-input" value="${data}">` },
                    { targets: 2, render: (data) => `<span class="fw-medium text-heading">${data ?? ''}</span>` },
                    { targets: 4, render: (data) => data ? `<span class="badge bg-label-info">${getReferenceTypeMap()[data] || data}</span>` : '' },
                    {
                        targets: 6,
                        render: (data, type) => type === 'display'
                            ? window.DitenDataTable.renderStatusBadge(data, getStatusMap())
                            : (getStatusMap()[String(!!data)] || { title: L().Unknown }).title
                    },
                    {
                        targets: -1,
                        title: L().Actions,
                        searchable: false,
                        orderable: false,
                        className: 'cell-fit all',
                        render: (data, type, full) => {
                            const rowJson = JSON.stringify(full).replace(/'/g, "&#39;");
                            return window.DitenDataTable.renderActions([
                                { key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-json': rowJson, 'title': L().QuickView } },
                                { key: 'edit', className: 'js-edit-item', icon: 'bx bx-edit', text: L().Edit, attrs: { 'data-id': full.id, 'data-json': rowJson } },
                                { key: 'delete', className: 'delete-record text-danger', icon: 'bx bx-trash', text: L().Delete, attrs: { 'data-json': rowJson } }
                            ]);
                        }
                    }
                ]
            }
        });
    };

    const bindEvents = () => {
        byId('oc-btn-edit')?.addEventListener('click', () => {
            const id = byId('oc-btn-edit')?.dataset.editId;
            if (id) list?.openEdit(id);
        });
        byId('offcanvasCreateEdit')?.addEventListener('show.bs.offcanvas', () => setTimeout(initOffcanvasSelect2, 50));
    };

    return {
        init: function () {
            initDataTable();
            bindEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => GoldenReferenceSlimList.init());
