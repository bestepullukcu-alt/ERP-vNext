/**
 * Tenant Roles — list screen (MOD-0018-FU9 FE-C). Golden Slim shape (offcanvas create/edit + quick view), CLIENT data.
 *
 * THE LIST IS A COMPONENT (BL-440, WP-ROLES-CLOSE-01): search, sort, filters, Save View, the filter bar, the offcanvas
 * plumbing and the form lifecycle are DitenDataTable.createList's. A tenant's roles are a bounded set (pack:
 * data_mode client, max 200): GET /api/roles returns all of them, the browser filters. Before: 770 lines, a private
 * copy of the Save View state machine and a browser export nobody had to be allowed to use. What stays here is the
 * screen's own business only.
 *
 * NO SELECTION COLUMN, deliberately: there is no bulk endpoint for roles (`HasSelection = false`).
 *
 * WHO ENFORCES WHAT. window.Permissions only decides what is DRAWN. For create / edit / delete / read the authority is
 * AuthService's [HasPermission] on api/roles — a hidden button is not a protection, the endpoint is.
 * ⚠ auth.roles.export IS DIFFERENT, and this must not be read as more than it is: there is no export endpoint. The
 * file is made in the browser from the rows GET api/roles already returned under auth.roles.read, so the export key
 * governs the Action MENU and nothing else. Anyone who can read the list holds its data; the key decides whether the
 * screen offers them Print / CSV / Excel / PDF / Copy. (A server-side export, with the key on its endpoint, is what a
 * server-mode list has — Users; this bounded client-mode list does not.)
 */
'use strict';

const RolesList = (function () {
    let list = null;

    const dtTableEl = document.querySelector('.datatables-roles');
    const apiUrl = window.API?.auth;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};

    const can = (key) => window.Permissions?.has?.(key) === true;
    const canCreate = () => can('auth.roles.create');
    const canUpdate = () => can('auth.roles.update');
    const canDelete = () => can('auth.roles.delete');
    // BL-452 package 3 — without auth.roles.export the Action menu carries no file entry. Menu only: see the header.
    const canExport = () => can('auth.roles.export');

    const escapeHtml = (v) => String(v ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

    // ─── Refusals: a stable code from AuthService → the reader's language (shared/diten-refusal.js) ──
    const ERROR_CODE_KEYS = { ROLE_ACTOR_REQUIRED: 'ErrorRoleActorRequired', ROLE_NOT_FOUND: 'ErrorRoleNotFound', ROLE_NAME_TAKEN: 'ErrorRoleNameTaken', ROLE_SYSTEM_NOT_DELETABLE: 'ErrorRoleSystemNotDeletable', ROLE_NAME_REQUIRED: 'ErrorRoleNameRequired', ROLE_NAME_TOO_LONG: 'ErrorRoleNameTooLong', ROLE_DISPLAY_NAME_REQUIRED: 'ErrorRoleDisplayNameRequired', ROLE_DISPLAY_NAME_TOO_LONG: 'ErrorRoleDisplayNameTooLong' };
    const refusalText = (json) => window.DitenRefusal.message(json, ERROR_CODE_KEYS, L(), 'Roles');
    // A form with two mistakes says both (the proxy lists every broken rule).
    const refusalTexts = (json) => window.DitenRefusal.messages(json, ERROR_CODE_KEYS, L(), 'Roles');

    // ─── Role type ───────────────────────────────────────────────────────────
    const typeLabel = (isSystem) => isSystem ? (L().RoleTypeSystem || '') : (L().RoleTypeCustom || '');
    const typeBadge = (isSystem) => `<span class="badge ${isSystem ? 'bg-label-primary' : 'bg-label-secondary'}">${escapeHtml(typeLabel(isSystem))}</span>`;

    // ─── Filters: the one rule the factory cannot guess (the type is a boolean on the row) ──
    const filterFields = [
        { id: 'filterType', key: 'type', kind: 'multi', matches: (row, selected) => !selected.length || selected.includes(row.isSystem ? 'System' : 'Custom') }
    ];

    // ─── KPI cards: counted from the whole loaded set (rows().data() ignores search and filters) ──
    const KPI = {
        'kpi-roles-total': (rows) => rows.length,
        'kpi-roles-system': (rows) => rows.filter((r) => r && r.isSystem).length,
        'kpi-roles-custom': (rows) => rows.filter((r) => r && !r.isSystem).length,
        'kpi-roles-users': (rows) => rows.reduce((sum, r) => sum + (Number(r && r.userCount) || 0), 0)
    };
    const updateKpis = (api) => {
        const rows = api.rows().data().toArray();
        Object.entries(KPI).forEach(([id, count]) => { if (byId(id)) byId(id).textContent = String(count(rows)); });
    };

    // ─── Permission module chips ─────────────────────────────────────────────
    // One info chip per module (Module N), from the list response (modulePermissions) — never a per-row fetch.
    // Table (collapse:true): one nowrap line, overflow folds into a "+N" dropdown. Offcanvas: wraps, shows all.
    const MODULE_CHIP_MAX = 3;
    const moduleEntries = (modulePermissions) => modulePermissions && typeof modulePermissions === 'object'
        ? Object.entries(modulePermissions).filter(([, c]) => Number(c) > 0)
        : [];
    const moduleChip = ([m, c]) => `<span class="badge bg-label-info">${escapeHtml(m)} ${escapeHtml(c)}</span>`;
    // What the cell says in words — the file carries the screen's content, not a bare count.
    const moduleText = (modulePermissions, total) => {
        const entries = moduleEntries(modulePermissions);
        return entries.length ? entries.map(([m, c]) => `${m} ${c}`).join(', ') : String(total ?? 0);
    };
    const renderModuleChips = (modulePermissions, total, options) => {
        const collapse = options?.collapse === true;
        const max = options?.max || MODULE_CHIP_MAX;
        const entries = moduleEntries(modulePermissions);
        const totalText = escapeHtml(total != null ? String(total) : '0');
        if (!entries.length) return `<span class="badge bg-label-info" title="${totalText}">${totalText}</span>`;

        const tooltip = escapeHtml(entries.map(([m, c]) => `${m}: ${c}`).join(', '));
        if (!collapse) return `<span class="d-inline-flex flex-wrap gap-1" title="${tooltip}">${entries.map(moduleChip).join('')}</span>`;

        const rest = entries.slice(max);
        let html = `<span class="d-inline-flex flex-nowrap align-items-center gap-1" title="${tooltip}">${entries.slice(0, max).map(moduleChip).join('')}`;
        if (rest.length) {
            html += '<span class="dropdown d-inline-block">'
                + `<a href="javascript:;" class="badge bg-label-secondary text-decoration-none" data-bs-toggle="dropdown" aria-expanded="false" title="${tooltip}">+${rest.length}</a>`
                + `<span class="dropdown-menu p-1">${rest.map((e) => `<span class="d-block px-2 py-1">${moduleChip(e)}</span>`).join('')}</span>`
                + '</span>';
        }
        return html + '</span>';
    };

    // ─── Quick view: only how the fields are filled ──────────────────────────
    const populateDetailsOffcanvas = (data) => {
        const setText = (id, value) => { const el = byId(id); if (el) { el.innerText = value; el.setAttribute('title', value); } };
        byId('oc-title').innerText = data.displayName || data.name || '-';
        byId('oc-subtitle').innerText = data.name || '-';
        setText('oc-name', data.name || '-');
        setText('oc-displayname', data.displayName || '-');
        const permsEl = byId('oc-permissions');
        if (permsEl) permsEl.innerHTML = renderModuleChips(data.modulePermissions, data.permissionCount);
        byId('oc-usercount').innerText = data.userCount != null ? String(data.userCount) : '-';
        byId('oc-desc').innerText = data.description || '-';
        const typeEl = byId('oc-type');
        if (typeEl) { typeEl.className = `badge ${data.isSystem ? 'bg-label-primary' : 'bg-label-secondary'}`; typeEl.innerText = typeLabel(!!data.isSystem); }
        const editBtn = byId('oc-btn-edit');
        if (editBtn) {
            editBtn.dataset.editId = data.id;
            // A system role, or a reader without the right, gets no edit affordance.
            editBtn.classList.toggle('d-none', !!data.isSystem || !canUpdate());
        }
    };

    // ─── Form: only the fields ───────────────────────────────────────────────
    // The name is the role's code: immutable once created, and it must LOOK immutable.
    const setNameImmutable = (immutable) => {
        const nameEl = byId('roleName');
        if (nameEl) { nameEl.readOnly = immutable; nameEl.classList.toggle('bg-label-secondary', immutable); }
        byId('roleNameHelp')?.classList.toggle('d-none', !immutable);
    };
    // The factory resets before BOTH create and edit; edit's load then locks the name.
    const resetFormFields = () => {
        setNameImmutable(false);
        byId('roleItemId').value = '';
        byId('roleName').value = '';
        byId('roleDisplayName').value = '';
        byId('roleDescription').value = '';
    };
    const loadFormFields = async (id) => {
        setNameImmutable(true);
        const res = await fetch(`/Roles/get/${id}`, { credentials: 'same-origin', headers: getAuthHeaders() });
        const json = await res.json();
        if (!json.success || !json.data) throw new Error('Failed to load role.');
        const d = json.data;
        byId('roleItemId').value = d.id || '';
        byId('roleName').value = d.name || '';
        byId('roleDisplayName').value = d.displayName || '';
        byId('roleDescription').value = d.description || '';
    };
    // The factory writes `errors` into the form's alert AS TEXT (it escapes every sentence); a refusal becomes one
    // sentence in the reader's language here — never the server's own.
    const submitForm = async (formData, isEdit, { editingId, headers }) => {
        const url = isEdit ? `/Roles/edit/${editingId}` : '/Roles/create';
        const res = await fetch(url, { method: 'POST', credentials: 'same-origin', headers, body: formData });
        const json = await res.json();
        return json.success ? json : Object.assign({}, json, { errors: refusalTexts(json) });
    };

    // ─── Delete: the endpoint is the screen's; the refusal is said in the reader's language ──
    const deleteRow = ({ row }) => {
        if (!row?.id) return;
        window.showConfirm?.(L().AreYouSure, async () => {
            try {
                const res = await fetch(`${apiUrl}/api/roles/${row.id}`, { method: 'DELETE', credentials: 'include', headers: getAuthHeaders() });
                if (!res.ok) {
                    window.showToast?.(refusalText(await res.json().catch(() => ({}))), 'error');
                    return;
                }
                list.reload('RecordDeleted');
            } catch (error) {
                console.error('[Roles] Delete failed.', error);
                window.showToast?.(L().ErrorOccurred, 'error');
            }
        }, { entityName: row.displayName || row.name, type: 'danger', icon: 'bx-trash', confirmButtonText: L().Delete });
    };

    const renderRowActions = (data, type, full) => {
        const rowJson = JSON.stringify(full).replace(/'/g, '&#39;');
        const actions = [{ key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-json': rowJson, 'title': L().QuickView } }];
        // Edit and Delete: the right, and never on a system role.
        if (canUpdate() && !full.isSystem) actions.push({ key: 'edit', className: 'js-edit-item', icon: 'bx bx-edit', text: L().Edit, attrs: { 'data-id': full.id, 'data-json': rowJson } });
        if (canDelete() && !full.isSystem) actions.push({ key: 'delete', className: 'delete-record text-danger', icon: 'bx bx-trash', text: L().Delete, attrs: { 'data-json': rowJson } });
        let html = window.DitenDataTable.renderActions(actions);
        // Manage Permissions — opens the Role Permissions screen on this role.
        const manageTitle = escapeHtml(L().ManagePermissions || '');
        const manageBtn = `<a href="/RoleAssignments?roleId=${encodeURIComponent(full.id)}" class="btn btn-icon me-1 js-manage-perms" title="${manageTitle}" aria-label="${manageTitle}"><i class="bx bx-key icon-md"></i></a>`;
        html = html.replace('<div class="d-flex align-items-center">', '<div class="d-flex align-items-center">' + manageBtn);
        // A system role is locked (no edit, no delete) — say so beside View.
        if (full.isSystem) {
            const lockTitle = escapeHtml(L().SystemRoleLocked || '');
            html = html.replace('</div>', `<span class="btn btn-icon text-muted pe-none" tabindex="-1" title="${lockTitle}" aria-label="${lockTitle}"><i class="bx bx-lock-alt icon-md"></i></span></div>`);
        }
        return html;
    };

    // ─── The list ────────────────────────────────────────────────────────────
    const initDataTable = async () => {
        if (!dtTableEl) return;
        if (!apiUrl) { console.error('[Roles] window.API.auth is required.'); return; }

        list = await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'client',
            ajax: { url: apiUrl + '/api/roles', type: 'GET', xhrFields: { withCredentials: true } },
            // quickView and edit are the factory's (quickView/form below); delete is the screen's endpoint.
            actions: { onRowAction: { delete: deleteRow } },
            // No "+ Add" at all without auth.roles.create (UAS-001: absent, not disabled). The file is a right of its
            // own: without auth.roles.export the Action menu carries no Print / CSV / Excel / PDF / Copy.
            toolbar: { addNewText: canCreate() ? L().AddNew : '', onAddNew: () => list.openCreate(), exportColumns: [1, 2, 3, 4, 5], colvisColumns: [1, 2, 3, 4, 5], exportPermitted: canExport() },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields },
            savedView: { moduleKey: 'Governance', pageKey: 'Roles', saveViewColumnIndexes: [1, 2, 3, 4, 5], defaultVisibleColumnIndexes: [1, 2, 3, 4, 5], baseOrder: [[1, 'asc']] },
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateDetailsOffcanvas },
            form: { formId: 'formRole', offcanvasId: 'offcanvasCreateEdit', alertId: 'formRoleAlert', saveBtnId: 'btnSaveRole', labelId: 'offcanvasCreateEditLabel', reset: resetFormFields, load: loadFormFields, submit: submitForm },
            config: {
                // No selection column: every column after the control column and before the actions reorders.
                colReorder: { columns: ':gt(0):not(:last-child)' },
                columns: [
                    { data: 'id', name: 'control' },
                    { data: 'name', name: 'name' },
                    { data: 'displayName', name: 'displayName' },
                    { data: 'isSystem', name: 'isSystem' },
                    { data: 'permissionCount', name: 'permissionCount' },
                    { data: 'userCount', name: 'userCount' },
                    { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data, type) => type === 'display' ? `<span class="fw-medium text-heading">${escapeHtml(data)}</span>` : (data ?? '') },
                    { targets: 2, render: (data, type) => type === 'display' ? escapeHtml(data) : (data ?? '') },
                    { targets: 3, render: (data, type) => type === 'display' ? typeBadge(!!data) : typeLabel(!!data) },
                    // Sorted by the number; shown, searched and exported as the modules the number is made of.
                    { targets: 4, className: 'text-start', render: (data, type, full) => type === 'display' ? renderModuleChips(full.modulePermissions, data, { collapse: true }) : (type === 'sort' || type === 'type' ? (data ?? 0) : moduleText(full.modulePermissions, data)) },
                    { targets: 5, className: 'text-center', render: (data, type) => type === 'display' ? `<span class="badge bg-label-secondary">${escapeHtml(data ?? 0)}</span>` : (data ?? 0) },
                    { targets: -1, title: L().Actions, searchable: false, orderable: false, className: 'cell-fit all', render: renderRowActions }
                ],
                drawCallback: function () { updateKpis(this.api()); }
            }
        });
    };

    const bindEvents = () => {
        byId('oc-btn-edit')?.addEventListener('click', () => {
            const id = byId('oc-btn-edit')?.dataset.editId;
            if (id) list?.openEdit(id);
        });
    };

    return {
        init: function () {
            initDataTable();
            bindEvents();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => RolesList.init());
