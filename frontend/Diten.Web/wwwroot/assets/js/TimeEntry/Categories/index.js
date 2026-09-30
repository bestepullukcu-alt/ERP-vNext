/**
 * MOD-0280-FU01 T2b — Work categories (Golden Reference Slim, client mode, ≤ 50 rows).
 *
 * The page writes only what is its own: columns, the quick view, the form fields and the endpoints. Nothing is ever
 * deleted (D10): a category is activated or deactivated. The CODE is immutable once saved — on edit its field is
 * read-only and the update always sends the code as it was stored, never what the field happens to hold (Platform
 * refuses a changed one too: WORK_CATEGORY_CODE_IMMUTABLE). A recommended category's label is its translation
 * (LabelResourceKey) until someone types their own.
 */
'use strict';

const TimeCategoriesList = (function () {
    let list = null;
    let editing = null;

    const endpoint = '/TimeEntry/Categories/api';
    const dtTableEl = document.querySelector('.datatables-timecategories');
    const L = () => window.L10n || {};
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const byId = (id) => document.getElementById(id);
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const fmt = (text, ...values) => values.reduce((out, value, i) => out.split(`{${i}}`).join(String(value)), String(text || ''));
    const failure = (status, reasonCode) => window.TimeEntryCore
        ? window.TimeEntryCore.failureMessage({ status, reasonCode }, (k) => L()[k] || k)
        : (L().ErrorOccurred || '');

    /** A category's label as the reader sees it: the tenant's own text, else its translation, else the code. */
    const labelOf = (row) => row?.labelText || (row?.labelResourceKey && (L().CategoryLabels || {})[row.labelResourceKey]) || row?.code || '';

    const getStatusMap = () => ({
        true: { title: L().Active, class: 'bg-label-success' },
        false: { title: L().Passive, class: 'bg-label-secondary' }
    });

    let rowsOverride = null;
    const rows = () => rowsOverride || list?.dt?.rows?.().data?.().toArray?.() || [];
    const rowById = (id) => rows().find((row) => String(row.id) === String(id)) || null;

    const send = async (method, url, body) => {
        const res = await fetch(url, { method, credentials: 'same-origin', headers: getAuthHeaders(true), body: body === undefined ? undefined : JSON.stringify(body) });
        let json = null;
        try { const text = await res.text(); json = text ? JSON.parse(text) : null; } catch (error) { json = null; }
        return { ok: res.ok, status: res.status, data: json?.data, reasonCode: json?.reason_code || null };
    };

    // ─── Quick view ──────────────────────────────────────────────────────────
    const populateDetailsOffcanvas = (data) => {
        byId('oc-title').innerText = labelOf(data) || '-';
        byId('oc-subtitle').innerText = data.code || '-';
        byId('oc-code').innerText = data.code || '-';
        byId('oc-label').innerText = labelOf(data) || '-';
        byId('oc-counts').innerText = data.countsAsWork ? L().CountsAsWorkYes : L().CountsAsWorkNo;
        byId('oc-desc').innerText = data.description || '-';
        const status = getStatusMap()[String(!!data.isActive)];
        const statusEl = byId('oc-status');
        statusEl.className = `badge ${status.class}`;
        statusEl.innerText = status.title || '-';
        const editBtn = byId('oc-btn-edit');
        if (editBtn) editBtn.dataset.editId = data.id;
    };

    // ─── Form ────────────────────────────────────────────────────────────────
    const setCodeLocked = (locked) => {
        const code = byId('categoryCode');
        code.readOnly = locked;
        code.classList.toggle('time-entry-code-locked', locked);
        byId('categoryCodeHint').textContent = locked ? (L().CodeLocked || '') : (L().CodeHint || '');
    };

    const resetFormFields = () => {
        editing = null;
        byId('categoryId').value = '';
        byId('categoryCode').value = '';
        byId('categoryLabel').value = '';
        byId('categoryDescription').value = '';
        byId('categorySortOrder').value = '0';
        byId('categoryCountsAsWork').checked = true;
        setCodeLocked(false);
    };

    const loadFormFields = async (id) => {
        const row = rowById(id);
        if (!row) throw new Error('Category not in the list.');
        editing = { id: row.id, code: row.code, version: row.version };
        byId('categoryId').value = row.id;
        byId('categoryCode').value = row.code || '';
        byId('categoryLabel').value = labelOf(row);
        byId('categoryDescription').value = row.description || '';
        byId('categorySortOrder').value = row.sortOrder ?? 0;
        byId('categoryCountsAsWork').checked = !!row.countsAsWork;
        setCodeLocked(true);
    };

    /** The body the form sends. On edit the code is the STORED one — the field is read-only and never trusted. */
    const bodyOf = (isEdit) => {
        const base = {
            labelText: byId('categoryLabel').value.trim(),
            description: byId('categoryDescription').value.trim() || null,
            countsAsWork: byId('categoryCountsAsWork').checked,
            sortOrder: Number(byId('categorySortOrder').value) || 0
        };
        return isEdit
            ? Object.assign({ expectedVersion: editing?.version ?? 0, code: editing?.code }, base)
            : Object.assign({ code: byId('categoryCode').value.trim().toUpperCase() }, base);
    };

    const submitForm = async (formData, isEdit) => {
        const result = isEdit
            ? await send('PUT', `${endpoint}/${encodeURIComponent(editing?.id || '')}`, bodyOf(true))
            : await send('POST', endpoint, bodyOf(false));
        return result.ok
            ? { success: true }
            : { success: false, errors: [failure(result.status, result.reasonCode)] };
    };

    // ─── Activate / deactivate — never delete ────────────────────────────────
    const toggleActive = ({ row }) => {
        if (!row?.id) return;
        const activate = !row.isActive;
        window.showConfirm?.(activate ? L().ActivateConfirm : L().DeactivateConfirm, async () => {
            const result = await send('POST', `${endpoint}/${encodeURIComponent(row.id)}/${activate ? 'activate' : 'deactivate'}`, { expectedVersion: row.version });
            if (!result.ok) {
                window.showToast?.(failure(result.status, result.reasonCode), 'error');
                return;
            }
            window.showToast?.(activate ? L().Activated : L().Deactivated, 'success');
            list?.reload();
        }, { entityName: labelOf(row), type: activate ? 'primary' : 'danger', confirmButtonText: activate ? L().Activate : L().Deactivate });
    };

    const installRecommended = () => {
        window.showConfirm?.(L().InstallRecommendedConfirm, async () => {
            const result = await send('POST', `${endpoint}/install-recommended`, {});
            if (!result.ok) {
                window.showToast?.(failure(result.status, result.reasonCode), 'error');
                return;
            }
            window.showToast?.(fmt(L().InstallRecommendedDone, (result.data?.installed || []).length, (result.data?.alreadyPresent || []).length), 'success');
            list.reload();
        }, { type: 'primary', confirmButtonText: L().InstallRecommended });
    };

    // ─── The list ────────────────────────────────────────────────────────────
    const initDataTable = async () => {
        if (!dtTableEl) return;

        list = await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'client',
            ajax: { url: `${endpoint}/manage`, type: 'GET', headers: getAuthHeaders(), xhrFields: { withCredentials: true } },
            actions: { onRowAction: { toggleActive } },
            toolbar: { addNewText: L().AddNew, onAddNew: () => list.openCreate(), exportColumns: [1, 2, 3, 4, 5], colvisColumns: [1, 2, 3, 4, 5] },
            filters: {
                hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse',
                fields: [{ id: 'filterStatus', key: 'status', kind: 'multi', matches: (row, selected) => !selected.length || selected.includes(row.isActive ? 'Active' : 'Passive') }]
            },
            savedView: { moduleKey: 'TimeEntry', pageKey: 'Categories', saveViewColumnIndexes: [1, 2, 3, 4, 5], defaultVisibleColumnIndexes: [1, 2, 3, 4, 5], baseOrder: [[4, 'asc']] },
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateDetailsOffcanvas },
            form: { formId: 'formWorkCategory', offcanvasId: 'offcanvasCreateEdit', alertId: 'formWorkCategoryAlert', saveBtnId: 'btnSaveCategory', labelId: 'offcanvasCreateEditLabel', reset: resetFormFields, load: loadFormFields, submit: submitForm },
            config: {
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'code', name: 'code' },
                    { data: 'labelText', name: 'label' },
                    { data: 'countsAsWork', name: 'countsAsWork' },
                    { data: 'sortOrder', name: 'sortOrder' },
                    { data: 'isActive', name: 'isActive' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading">${esc(data)}</span>` },
                    { targets: 2, render: (data, type, full) => esc(labelOf(full)) },
                    { targets: 3, render: (data, type) => type === 'display' ? esc(data ? L().CountsAsWorkYes : L().CountsAsWorkNo) : String(!!data) },
                    {
                        targets: 5,
                        render: (data, type) => type === 'display'
                            ? window.DitenDataTable.renderStatusBadge(data, getStatusMap())
                            : (getStatusMap()[String(!!data)] || { title: L().Unknown }).title
                    },
                    {
                        targets: -1, title: L().Actions, searchable: false, orderable: false, className: 'cell-fit all',
                        render: (data, type, full) => {
                            const rowJson = JSON.stringify(full).replace(/'/g, '&#39;');
                            return window.DitenDataTable.renderActions([
                                { key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-json': rowJson, 'title': L().QuickView } },
                                { key: 'edit', className: 'js-edit-item', icon: 'bx bx-edit', text: L().Edit, attrs: { 'data-id': full.id, 'data-json': rowJson } },
                                {
                                    key: 'toggleActive',
                                    className: full.isActive ? 'text-danger' : 'text-success',
                                    icon: full.isActive ? 'bx bx-block' : 'bx bx-check-circle',
                                    text: full.isActive ? L().Deactivate : L().Activate,
                                    attrs: { 'data-json': rowJson }
                                }
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
        byId('btnInstallRecommended')?.addEventListener('click', installRecommended);
    };

    return {
        init: () => { initDataTable(); bindEvents(); },
        // Seams for the tests.
        bodyOf, loadFormFields, resetFormFields, submitForm, toggleActive, labelOf, list: () => list,
        useRows: (value) => { rowsOverride = value; }
    };
})();

window.TimeCategoriesList = TimeCategoriesList;
document.addEventListener('DOMContentLoaded', () => TimeCategoriesList.init());
