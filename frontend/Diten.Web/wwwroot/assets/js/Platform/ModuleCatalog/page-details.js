'use strict';

const ModulePageDetails = (function () {
    const table = document.getElementById('dt-module-page-actions');
    const pageId = table?.dataset.pageId || '';
    const moduleCode = table?.dataset.moduleCode || '';
    const pageCode = table?.dataset.pageCode || '';
    const endpoint = `/Platform/ModuleCatalog/api/pages/${encodeURIComponent(pageId)}/actions`;
    const offcanvasEl = document.getElementById('pageActionOffcanvas');
    const form = document.getElementById('pageActionForm');
    const L = window.L10n || {};
    let offcanvas;
    let rows = [];

    const fields = {
        id: document.getElementById('pageActionId'),
        actionCode: document.getElementById('pageActionCode'),
        displayName: document.getElementById('pageActionDisplayName'),
        permissionKey: document.getElementById('pageActionPermissionKey'),
        actionType: document.getElementById('pageActionType'),
        status: document.getElementById('pageActionStatus'),
        sortOrder: document.getElementById('pageActionSortOrder'),
        isDangerous: document.getElementById('pageActionIsDangerous'),
        isToolbarAction: document.getElementById('pageActionIsToolbarAction'),
        isRowAction: document.getElementById('pageActionIsRowAction'),
        description: document.getElementById('pageActionDescription')
    };

    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (char) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]));
    const normalizeActionCode = (value) => (value || '').toUpperCase().replace(/[^A-Z0-9]+/g, '_').replace(/_+/g, '_').replace(/^_|_$/g, '').slice(0, 80);
    const normalizePermission = (value) => (value || '').trim().toLowerCase().replace(/\s+/g, '').replace(/\.+/g, '.').replace(/^\.+|\.+$/g, '');
    const normalizePermissionSegment = (value) => (value || '')
        .trim()
        .toLowerCase()
        .replace(/[^a-z0-9]+/g, '-')
        .replace(/-+/g, '-')
        .replace(/^-|-$/g, '');

    const actionPermissionAliases = {
        ADD: 'create',
        CREATE: 'create',
        NEW: 'create',
        EDIT: 'edit',
        UPDATE: 'edit',
        VIEW: 'view',
        DETAILS: 'view',
        DETAIL: 'view',
        DELETE: 'delete',
        REMOVE: 'delete',
        ACTIVATE: 'activate',
        DEACTIVATE: 'deactivate',
        EXPORT: 'export',
        IMPORT: 'import',
        APPROVE: 'approve',
        REJECT: 'reject'
    };

    const statusBadge = (status) => {
        const cls = { Active: 'bg-label-success', Inactive: 'bg-label-warning', Deprecated: 'bg-label-danger', Draft: 'bg-label-secondary' }[status] || 'bg-label-secondary';
        return `<span class="badge ${cls}">${escapeHtml(L[`Status${status}`] || status || '-')}</span>`;
    };

    const permissionActionSegment = () => {
        const actionCode = normalizeActionCode(fields.actionCode?.value);
        if (!actionCode) return '';
        return actionPermissionAliases[actionCode] || normalizePermissionSegment(actionCode);
    };

    const generatePermissionKey = () => normalizePermission([
        normalizePermissionSegment(moduleCode),
        normalizePermissionSegment(pageCode),
        permissionActionSegment()
    ].filter(Boolean).join('.'));

    const syncPermissionKey = () => {
        if (!fields.permissionKey) return;
        fields.permissionKey.value = generatePermissionKey();
    };

    const render = () => {
        const body = table?.querySelector('tbody');
        if (!body) return;
        body.innerHTML = rows.map((row) => {
            const id = row.id || row.Id;
            return `<tr>
                <td><code>${escapeHtml(row.actionCode || row.ActionCode)}</code></td>
                <td>${escapeHtml(row.displayName || row.DisplayName)}</td>
                <td><code>${escapeHtml(row.permissionKey || row.PermissionKey)}</code></td>
                <td>${escapeHtml(row.actionType || row.ActionType)}</td>
                <td>${statusBadge(row.status || row.Status)}</td>
                <td class="text-end">${escapeHtml(row.sortOrder ?? row.SortOrder ?? 0)}</td>
                <td class="text-end">
                    <div class="dropdown">
                        <button class="btn btn-icon dropdown-toggle hide-arrow" data-bs-toggle="dropdown"><i class="bx bx-dots-vertical-rounded icon-md"></i></button>
                        <div class="dropdown-menu dropdown-menu-end">
                            <button class="dropdown-item btn-action-edit" data-id="${escapeHtml(id)}">${escapeHtml(L.Edit || 'Edit')}</button>
                            <button class="dropdown-item text-danger btn-action-delete" data-id="${escapeHtml(id)}">${escapeHtml(L.Delete || 'Delete')}</button>
                        </div>
                    </div>
                </td>
            </tr>`;
        }).join('');
    };

    const load = async () => {
        const response = await fetch(endpoint);
        if (!response.ok) {
            window.showToast?.(L.ErrorOccurred || 'Error occurred.', 'error');
            return;
        }
        const payload = await response.json();
        rows = payload?.data || payload?.Data || [];
        render();
    };

    const reset = () => {
        form?.reset();
        fields.id.value = '';
        fields.status.value = 'Draft';
        fields.actionType.value = 'Toolbar';
        fields.sortOrder.value = '0';
        fields.isToolbarAction.checked = true;
        fields.isRowAction.checked = false;
        fields.isDangerous.checked = false;
        syncPermissionKey();
    };

    const openCreate = () => {
        reset();
        offcanvas?.show();
    };

    const openEdit = (id) => {
        const row = rows.find((item) => String(item.id || item.Id) === String(id));
        if (!row) return;
        fields.id.value = row.id || row.Id || '';
        fields.actionCode.value = row.actionCode || row.ActionCode || '';
        fields.displayName.value = row.displayName || row.DisplayName || '';
        fields.permissionKey.value = row.permissionKey || row.PermissionKey || '';
        fields.actionType.value = row.actionType || row.ActionType || 'Toolbar';
        fields.status.value = row.status || row.Status || 'Draft';
        fields.sortOrder.value = row.sortOrder ?? row.SortOrder ?? 0;
        fields.isDangerous.checked = (row.isDangerous ?? row.IsDangerous ?? false) === true;
        fields.isToolbarAction.checked = (row.isToolbarAction ?? row.IsToolbarAction ?? false) === true;
        fields.isRowAction.checked = (row.isRowAction ?? row.IsRowAction ?? false) === true;
        fields.description.value = row.description || row.Description || '';
        syncPermissionKey();
        offcanvas?.show();
    };

    const payload = () => ({
        actionCode: normalizeActionCode(fields.actionCode.value),
        displayName: fields.displayName.value.trim(),
        permissionKey: normalizePermission(fields.permissionKey.value),
        actionType: fields.actionType.value,
        sortOrder: Number.parseInt(fields.sortOrder.value || '0', 10),
        isDangerous: fields.isDangerous.checked,
        isToolbarAction: fields.isToolbarAction.checked,
        isRowAction: fields.isRowAction.checked,
        status: fields.status.value,
        description: fields.description.value.trim() || null
    });

    const save = async (event) => {
        event.preventDefault();
        fields.actionCode.value = normalizeActionCode(fields.actionCode.value);
        syncPermissionKey();
        if (!form.checkValidity()) {
            form.classList.add('was-validated');
            return;
        }
        const id = fields.id.value;
        const response = await fetch(id ? `/Platform/ModuleCatalog/api/page-actions/${encodeURIComponent(id)}` : endpoint, {
            method: id ? 'PUT' : 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload())
        });
        if (!response.ok) {
            window.showToast?.(L.ErrorOccurred || 'Error occurred.', 'error');
            return;
        }
        offcanvas?.hide();
        await load();
        window.showToast?.(L.RecordSaved || 'Saved.', 'success');
    };

    const remove = async (id) => {
        const response = await fetch(`/Platform/ModuleCatalog/api/page-actions/${encodeURIComponent(id)}`, { method: 'DELETE' });
        if (!response.ok) {
            window.showToast?.(L.ErrorOccurred || 'Error occurred.', 'error');
            return;
        }
        await load();
        window.showToast?.(L.RecordDeleted || 'Deleted.', 'success');
    };

    const bind = () => {
        document.getElementById('btnAddPageAction')?.addEventListener('click', openCreate);
        fields.displayName?.addEventListener('input', () => {
            if (!fields.actionCode.value) {
                fields.actionCode.value = normalizeActionCode(fields.displayName.value);
                syncPermissionKey();
            }
        });
        fields.actionCode?.addEventListener('input', () => {
            fields.actionCode.value = normalizeActionCode(fields.actionCode.value);
            syncPermissionKey();
        });
        form?.addEventListener('submit', save);
        table?.addEventListener('click', (event) => {
            const edit = event.target.closest('.btn-action-edit');
            const del = event.target.closest('.btn-action-delete');
            if (edit) openEdit(edit.dataset.id);
            if (del) window.showConfirm?.(L.AreYouSure, () => remove(del.dataset.id), { type: 'delete', confirmButtonText: L.Delete });
        });
    };

    const init = () => {
        if (!table || !offcanvasEl) return;
        offcanvas = bootstrap.Offcanvas.getOrCreateInstance(offcanvasEl);
        bind();
        load();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => ModulePageDetails.init());
