/**
 * Tenant Users — list screen (MOD-0018-FU9 FE-C). Golden Slim shape (offcanvas create/edit + quick view), SERVER data.
 *
 * THE LIST IS A COMPONENT (BL-440 package 5, WP-UI-USERS-LIST-01): paging, search, sort, filters, Save View, the
 * filter bar, the offcanvas plumbing and the form lifecycle are DitenDataTable.createList's. AuthService pages:
 * GET /api/users?start&length&search&orderBy&orderDir&status&roleId&accountKind → { data: { items, total,
 * filteredTotal, summary } }. Before: 1162 lines, pageSize=1000 filtered in the browser (the 1001st user never
 * appeared, the KPIs counted a truncated set). What stays here is the screen's own business only.
 *
 * NO SELECTION COLUMN, deliberately (K17): there is no bulk endpoint for users (`HasSelection = false`).
 */
'use strict';

const UsersList = (function () {
    let list = null;

    const dtTableEl = document.querySelector('.datatables-users');
    const apiUrl = window.API?.auth;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    // The screen's own POSTs (kebab actions, the type change) carry the antiforgery token the form renders.
    const postHeaders = (includeJson = false) => Object.assign(
        { 'RequestVerificationToken': byId('formUser')?.querySelector('input[name="__RequestVerificationToken"]')?.value || '' },
        getAuthHeaders(includeJson));

    // FE-B (window.Permissions) — UX gates only; AuthService's [HasPermission] is the authority.
    const can = (key) => window.Permissions?.has?.(key) === true;
    const canCreate = () => can('auth.users.create');
    const canUpdate = () => can('auth.users.update');
    const canDelete = () => can('auth.users.delete');
    // Explicit-grant-only key (owner decision 2026-09-11): never in any default role.
    const canManageKind = () => can('auth.users.account-kind.manage');

    // ─── Account kind (the AuthService enum, by NAME) ───────────────────────
    // The DTO carries the NAME; anything else collapses to "Unknown" — the unconfirmed state, never a guessed "Human".
    const ACCOUNT_KINDS = ['Unknown', 'Human', 'Service'];
    const normalizeAccountKind = (value) => {
        if (typeof value === 'number') return ACCOUNT_KINDS[value] || 'Unknown';
        const text = typeof value === 'string' ? value.trim().toLowerCase() : '';
        return ACCOUNT_KINDS.find((k) => k.toLowerCase() === text) || 'Unknown';
    };
    const accountKindLabel = (value) => {
        const kind = normalizeAccountKind(value);
        return L()['AccountKind' + kind] || kind;
    };
    const accountKindBadgeClass = (value) => ({ Unknown: 'bg-label-secondary', Human: 'bg-label-info', Service: 'bg-label-warning' })[normalizeAccountKind(value)];

    /*
     * WP-AUTH-INVITED-LIFECYCLE-01 — THREE states from AuthService's `status`: Invited (password never set) ·
     * Inactive · Active. The filter sends the SERVICE's words; the badge key for Inactive is the screen's 'Passive'.
     * No `status` (older service) → isActive, never Invited, which only the server can know.
     */
    const userStatusOf = (row) => {
        const status = String(row?.status ?? '').trim().toLowerCase();
        if (status === 'invited') return 'Invited';
        if (status === 'inactive' || status === 'passive') return 'Passive';
        if (status === 'active') return 'Active';
        return row?.isActive ? 'Active' : 'Passive';
    };
    // One colour per state: an invited account must never look like one an administrator switched off.
    const getStatusMap = () => ({
        Active: { title: L().Active, class: 'bg-label-success' },
        Passive: { title: L().Passive, class: 'bg-label-secondary' },
        Invited: { title: L().StatusInvited, class: 'bg-label-info' }
    });
    const statusOfRow = (row) => getStatusMap()[userStatusOf(row)];

    // ─── Badges and role chips ───────────────────────────────────────────────
    const escapeHtml = (v) => String(v ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    const renderStatusBadge = (row) => `<span class="badge ${statusOfRow(row).class}">${escapeHtml(statusOfRow(row).title || '')}</span>`;
    const renderAccountKindBadge = (value) =>
        `<span class="badge ${accountKindBadgeClass(value)}">${escapeHtml(accountKindLabel(value))}</span>`;
    // One info chip per role. Table (collapse:true): one nowrap line, overflow folds into a "+N" dropdown.
    // Offcanvas (collapse:false): wraps and shows every role.
    const ROLE_CHIP_MAX = 3;
    const roleChip = (r) => `<span class="badge bg-label-info">${escapeHtml(r)}</span>`;
    const renderRoleChips = (roles, options) => {
        const collapse = options?.collapse === true;
        const max = options?.max || ROLE_CHIP_MAX;
        const names = Array.isArray(roles) ? roles.map((r) => String(r ?? '').trim()).filter(Boolean) : [];
        if (!names.length) return '<span class="text-muted">-</span>';
        const tooltip = escapeHtml(names.join(', '));
        if (!collapse) return `<span class="d-inline-flex flex-wrap gap-1" title="${tooltip}">${names.map(roleChip).join('')}</span>`;
        const rest = names.slice(max);
        let html = `<span class="d-inline-flex flex-nowrap align-items-center gap-1" title="${tooltip}">${names.slice(0, max).map(roleChip).join('')}`;
        if (rest.length) {
            html += '<span class="dropdown d-inline-block">'
                + `<a href="javascript:;" class="badge bg-label-secondary text-decoration-none" data-bs-toggle="dropdown" aria-expanded="false" title="${tooltip}">+${rest.length}</a>`
                + `<span class="dropdown-menu p-1">${rest.map((r) => `<span class="d-block px-2 py-1">${roleChip(r)}</span>`).join('')}</span>`
                + '</span>';
        }
        return html + '</span>';
    };

    // ─── Filters: the service filters, so no field carries a row matcher ─────
    // Keys are the service's parameter names; role values are role IDS (roleId is a Guid on the service).
    const filterFields = [
        { id: 'filterStatus', key: 'status', kind: 'multi' },
        { id: 'filterRoles', key: 'roleId', kind: 'multi' },
        { id: 'filterAccountKind', key: 'accountKind', kind: 'multi' }
    ];
    const loadRoleOptions = async ({ fillSelect }) => {
        const select = byId('filterRoles');
        if (!select) return;
        // Without auth.roles.read this is a 403: the role filter stays empty, the list itself is unaffected.
        const res = await fetch(`${apiUrl}/api/roles`, { method: 'GET', credentials: 'include', headers: getAuthHeaders() });
        if (!res.ok) return;
        const roles = window.DitenDataTable.unwrapResponseData(await res.json());
        fillSelect(select, roles
            .map((r) => ({ value: r.id, text: r.name }))
            .sort((a, b) => String(a.text).localeCompare(String(b.text))));
    };

    // ─── KPI cards: the service's tenant-wide summary, independent of every filter ──
    // `invited` arrives too — there is no fifth card (owner decision), and Passive does not count the invited.
    const KPI_CARDS = { 'kpi-users-total': 'total', 'kpi-users-active': 'active', 'kpi-users-passive': 'passive', 'kpi-users-norole': 'noRole' };
    const writeKpis = (json) => {
        const summary = json?.data?.summary;
        if (summary) Object.entries(KPI_CARDS).forEach(([id, key]) => { if (byId(id)) byId(id).textContent = String(summary[key] ?? 0); });
    };

    // ─── Select2 in the offcanvases ──────────────────────────────────────────
    // select2 inside an offcanvas needs `dropdownParent` = that panel, or the list opens BEHIND it (appended to <body>).
    const initOffcanvasSelect2 = () => {
        if (!window.jQuery || !$.fn.select2) return;
        [
            { panel: '#offcanvasCreateEdit', select: '#userAccountKind', width: '100%', selectionCssClass: 'form-select' },
            // Sits inline beside the "Change type" button, so it keeps the small, shrink-to-fit shape it had.
            { panel: '#offcanvasDetailsPreview', select: '#oc-accountkind-select', width: 'auto', selectionCssClass: 'form-select form-select-sm' }
        ].forEach(({ panel, select, width, selectionCssClass }) => {
            const $panel = $(panel);
            const $el = $panel.find(select);
            if (!$el.length) return; // Razor draws neither control without auth.users.account-kind.manage.
            if ($el.hasClass('select2-hidden-accessible')) $el.select2('destroy');
            $el.select2({ dropdownParent: $panel, width, selectionCssClass, minimumResultsForSearch: Infinity });
        });
    };
    // select2 paints its own box: a bare `.value` write leaves it showing the previous choice. Every write goes here.
    const setSelectValue = (el, value) => {
        if (!el) return;
        el.value = value;
        if (window.jQuery && $.fn.select2 && $(el).hasClass('select2-hidden-accessible')) $(el).trigger('change.select2');
    };
    // ⚠ The painted box is a SIBLING: `d-none` on the select alone hides nothing (the permission withdrawal).
    const setSelectHidden = (el, hidden) => {
        if (!el) return;
        el.classList.toggle('d-none', hidden);
        if (window.jQuery) $(el).next('.select2-container').toggleClass('d-none', hidden);
    };

    // ─── Quick view: only how the fields are filled ──────────────────────────
    const populateDetailsOffcanvas = (data) => {
        byId('oc-title').innerText = [data.firstName, data.lastName].filter(Boolean).join(' ') || data.email || '-';
        byId('oc-subtitle').innerText = data.email || '-';
        byId('oc-email').innerText = data.email || '-';
        byId('oc-firstname').innerText = data.firstName || '-';
        byId('oc-lastname').innerText = data.lastName || '-';
        const rolesEl = byId('oc-roles');
        if (rolesEl) rolesEl.innerHTML = renderRoleChips(data.roles);

        const statusEl = byId('oc-status');
        const status = statusOfRow(data);
        statusEl.className = `badge ${status.class}`;
        statusEl.innerText = status.title || '-';
        byId('oc-invite-pending-hint')?.classList.toggle('d-none', userStatusOf(data) !== 'Invited');

        // Account kind: the badge for every reader; the change control only where the server drew it AND the
        // snapshot agrees (the server gate is the one that matters; this keeps the two from disagreeing).
        const kindEl = byId('oc-accountkind');
        if (kindEl) {
            kindEl.className = `badge ${accountKindBadgeClass(data.accountKind)}`;
            kindEl.innerText = accountKindLabel(data.accountKind);
        }
        const kindSelect = byId('oc-accountkind-select');
        const kindBtn = byId('oc-btn-accountkind');
        if (kindSelect) {
            setSelectValue(kindSelect, normalizeAccountKind(data.accountKind));
            setSelectHidden(kindSelect, !canManageKind());
        }
        if (kindBtn) {
            kindBtn.dataset.userId = data.id || '';
            kindBtn.dataset.userEmail = data.email || '';
            kindBtn.classList.toggle('d-none', !canManageKind());
        }

        // Security metrics (from the row — no extra fetch).
        [['oc-lastlogin', data.lastLoginAt ? new Date(data.lastLoginAt).toLocaleString() : (L().Never || '')],
            ['oc-failedlogins', String(data.failedLoginAttempts ?? 0)], ['oc-mfastatus', L().MFATenantPolicy || '']]
            .forEach(([id, value]) => { if (byId(id)) byId(id).innerText = value; });

        const editBtn = byId('oc-btn-edit');
        if (editBtn) {
            editBtn.dataset.editId = data.id;
            editBtn.classList.toggle('d-none', !canUpdate());
        }
    };

    // ─── Form: only the fields ───────────────────────────────────────────────
    const setCreateMode = (isCreate) => {
        // Invitation hint shows on create; status switch shows on edit. No password field (invite flow).
        byId('userInviteHint')?.classList.toggle('d-none', !isCreate);
        // WP-AUTH-USER-KIND-UPDATE-01 — the kind select is the SAME control on create and edit, saved by the form's
        // own button (UpdateUser carries it). Only the create-time hint ("leave unset…") is create-only.
        byId('userAccountKindCreateHint')?.classList.toggle('d-none', !isCreate);
        byId('userActiveRow')?.classList.toggle('d-none', isCreate);
        byId('userInvitePendingRow')?.classList.add('d-none'); // edit decides after the load
        // Immutable on edit, and it must LOOK immutable (Bootstrap 5 has no [readonly] background).
        const emailEl = byId('userEmail');
        if (emailEl) {
            emailEl.readOnly = !isCreate;
            emailEl.classList.toggle('bg-label-secondary', !isCreate);
        }
        byId('userEmailHelp')?.classList.toggle('d-none', isCreate);
    };
    // The factory resets before BOTH create and edit; edit's load then switches the mode.
    const resetFormFields = () => {
        setCreateMode(true);
        byId('userItemId').value = '';
        byId('userEmail').value = '';
        byId('userFirstName').value = '';
        byId('userLastName').value = '';
        byId('userIsActive').checked = true;
        setSelectValue(byId('userAccountKind'), '');
    };
    const loadFormFields = async (id) => {
        setCreateMode(false);
        const res = await fetch(`/Users/get/${id}`, { credentials: 'same-origin', headers: getAuthHeaders() });
        const json = await res.json();
        if (!json.success || !json.data) throw new Error('Failed to load user.');
        const d = json.data;
        byId('userItemId').value = d.id || '';
        byId('userEmail').value = d.email || '';
        byId('userFirstName').value = d.firstName || '';
        byId('userLastName').value = d.lastName || '';
        byId('userIsActive').checked = !!d.isActive;
        // WP-AUTH-INVITED-LIFECYCLE-01 — no activation switch for an invited account (it activates on password set).
        const invited = userStatusOf(d) === 'Invited';
        byId('userActiveRow')?.classList.toggle('d-none', invited);
        byId('userInvitePendingRow')?.classList.toggle('d-none', !invited);
        // The kind starts from what AuthService reports; Unknown is the empty option (the proxy maps it back).
        const currentKind = normalizeAccountKind(d.accountKind);
        setSelectValue(byId('userAccountKind'), currentKind === 'Unknown' ? '' : currentKind);
    };
    // A refusal tagged with a stable code (the proxy's `errorCode`) is shown in the reader's language.
    const ERROR_CODE_KEYS = { USER_EMAIL_TAKEN: 'ErrorUserEmailTaken', USER_INVITATION_PENDING: 'ErrorUserInvitationPending' };
    const localizedErrors = (json) => {
        const key = ERROR_CODE_KEYS[(json || {}).errorCode];
        if (key && L()[key]) return [L()[key]];
        return (json && Array.isArray(json.errors) && json.errors.length) ? json.errors : [L().ErrorOccurred];
    };
    const submitForm = async (formData, isEdit, { editingId, headers }) => {
        const url = isEdit ? `/Users/edit/${editingId}` : '/Users/create';
        const res = await fetch(url, { method: 'POST', credentials: 'same-origin', headers, body: formData });
        const json = await res.json();
        if (!json.success) return Object.assign({}, json, { errors: localizedErrors(json) });
        // Dev-only: create returns a set-password link (null in prod), shown once the list has taken the save.
        if (!isEdit && json.setupUrl) window.setTimeout(() => showInviteLink(json.setupUrl), 0);
        return json;
    };

    // The quick view's "Change type" — its own audited route; the edit form's select rides "Update" instead.
    const postAccountKind = async (id, kind) => {
        try {
            const res = await fetch(`/Users/api/${id}/account-kind`, { method: 'POST', credentials: 'same-origin', headers: postHeaders(true), body: JSON.stringify({ kind }) });
            const json = await res.json().catch(() => ({}));
            if (!res.ok) throw new Error((json.errors && json.errors[0]) || json.detail || L().ErrorOccurred);
            window.bootstrap?.Offcanvas.getInstance(byId('offcanvasDetailsPreview'))?.hide();
            list.reload('AccountKindChanged');
        } catch (error) {
            console.error('[Users] Account kind change failed.', error);
            window.showToast?.(error.message || L().ErrorOccurred, 'error');
        }
    };

    /*
     * Dev-only: the copyable set-password link (setupUrl is null in prod). ⚠ IT WEARS THE PRODUCT'S DIALOG: a raw
     * `Swal.fire` (a field + copy button) decides the CONTENT, never the LOOK — `width` is the one geometry it sets.
     */
    const showInviteLink = (link) => {
        const S = window.Swal;
        const look = window.DitenDialogAppearance;
        if (!S || typeof look !== 'function') { window.showToast?.(String(link), 'info'); return; }
        const safe = escapeHtml(link);
        S.fire({
            ...look({ width: '520px' }),
            // ⚠ THE PICTURE RIDES THE TITLE, as `showConfirm` composes it — the package collapses the icon slot.
            title: look.iconHtml(null, 'bx-link-alt') + `<span>${L().InviteLinkTitle}</span>`,
            html: `<p class="${look.description}">${L().InviteLinkHint}</p>`
                + '<div class="input-group">'
                + `<input id="inviteLinkInput" type="text" class="form-control" readonly value="${safe}">`
                + `<button id="inviteLinkCopyBtn" type="button" class="btn btn-primary" title="${L().Copy}"><i class="bx bx-copy"></i></button>`
                + '</div>',
            // "Kapat", not "İptal": nothing is being cancelled — the invitation has already been sent.
            confirmButtonText: L().Close,
            showCancelButton: false,
            didOpen: () => {
                const input = byId('inviteLinkInput');
                input?.addEventListener('focus', () => input.select());
                byId('inviteLinkCopyBtn')?.addEventListener('click', async () => {
                    try { await navigator.clipboard.writeText(link); }
                    catch (e) { input?.select(); try { document.execCommand('copy'); } catch (e2) { } }
                    window.showToast?.(L().Copied, 'success');
                });
            }
        });
    };

    // ─── Kebab actions: the endpoints and the confirms are the screen's ──────
    // ⚠ EACH CONFIRM CARRIES THE GLYPH OF THE MENU ITEM THAT OPENED IT (owner report, 2026-09-23). → MVC proxy.
    const adminActions = {
        disable: { className: 'js-user-disable text-warning', icon: 'bx-minus-circle', text: 'Disable', url: (id) => `/Users/disable/${id}`, toast: 'UserDisabled', type: 'warning' },
        enable: { className: 'js-user-enable text-success', icon: 'bx-check-circle', text: 'Enable', url: (id) => `/Users/enable/${id}`, toast: 'UserEnabled', type: 'primary' },
        resend: { className: 'js-user-resend', icon: 'bx-mail-send', text: 'ResendInvitation', url: (id) => `/Users/resend-invite/${id}`, toast: 'InvitationResent', type: 'primary' },
        reset: { className: 'js-user-reset', icon: 'bx-key', text: 'ResetPassword', url: (id) => `/Users/reset-password/${id}`, toast: 'PasswordReset', type: 'warning' }
    };
    const runAdminAction = (cfg) => ({ id, row }) => {
        if (!id) return;
        window.showConfirm?.(L().AreYouSure, async () => {
            try {
                const res = await fetch(cfg.url(id), { method: 'POST', credentials: 'same-origin', headers: postHeaders() });
                const json = await res.json().catch(() => ({}));
                if (!res.ok) throw new Error(localizedErrors(json)[0] || L().ErrorOccurred);
                // Dev-only: resend/reset return a copyable set-password link → the dialog, not the toast.
                if (json.setupUrl) {
                    list.dt.ajax.reload(null, false);
                    showInviteLink(json.setupUrl);
                } else {
                    list.reload(cfg.toast);
                }
            } catch (error) {
                console.error('[Users] Admin action failed.', error);
                window.showToast?.(error.message || L().ErrorOccurred, 'error');
            }
        }, { entityName: row?.email, type: cfg.type, icon: cfg.icon, confirmButtonText: L()[cfg.text] || '' });
    };
    const adminAction = (key, full, rowJson, extraAttrs) => {
        const cfg = adminActions[key];
        return { key, className: cfg.className, icon: `bx ${cfg.icon}`, text: L()[cfg.text], attrs: Object.assign({ 'data-json': rowJson }, extraAttrs || {}) };
    };
    // Bulgu #10: the delete confirm says what happens — roles removed, not recoverable, the e-mail free again.
    const deleteRow = ({ row }) => {
        if (!row?.id) return;
        window.showConfirm?.(L().AreYouSure, async () => {
            try {
                const res = await fetch(`${apiUrl}/api/users/${row.id}`, { method: 'DELETE', credentials: 'include', headers: getAuthHeaders() });
                if (!res.ok) throw new Error('Delete failed.');
                list.reload('RecordDeleted');
            } catch (error) {
                console.error(error);
                window.showToast?.(L().ErrorOccurred, 'error');
            }
        }, { entityName: row.email, subtext: L().DeleteUserConfirmText, type: 'danger', icon: 'bx-trash', confirmButtonText: L().Delete });
    };
    const renderRowActions = (data, type, full) => {
        const rowJson = JSON.stringify(full).replace(/'/g, '&#39;');
        const actions = [{ key: 'quickView', className: 'js-quick-view me-1', icon: 'bx bx-show', attrs: { 'data-json': rowJson, 'title': L().QuickView } }];
        if (canUpdate()) actions.push({ key: 'edit', className: 'js-edit-item', icon: 'bx bx-edit', text: L().Edit, attrs: { 'data-json': rowJson } });
        if (canUpdate() && full.isActive) actions.push(adminAction('disable', full, rowJson));
        // WP-AUTH-INVITED-LIFECYCLE-01 — never "Activate" an invited account (AuthService refuses it too): it
        // activates itself when the person redeems the link.
        if (canUpdate() && !full.isActive && userStatusOf(full) !== 'Invited') actions.push(adminAction('enable', full, rowJson));
        // For an invited account resend is the ONLY way forward; the tooltip says why reset and activate are absent.
        if (canCreate() && full.mustChangePassword) {
            actions.push(adminAction('resend', full, rowJson, userStatusOf(full) === 'Invited' ? { title: L().InvitationPendingHint || '' } : null));
        }
        if (canUpdate() && full.isActive && !full.mustChangePassword) actions.push(adminAction('reset', full, rowJson));
        if (canDelete()) actions.push({ key: 'delete', className: 'delete-record text-danger', icon: 'bx bx-trash', text: L().Delete, attrs: { 'data-json': rowJson } });
        return window.DitenDataTable.renderActions(actions);
    };

    // ─── The list ────────────────────────────────────────────────────────────
    const initDataTable = async () => {
        if (!dtTableEl) return;
        if (!apiUrl) { console.error('[Users] window.API.auth is required.'); return; }

        list = await window.DitenDataTable.createList({
            tableEl: dtTableEl,
            dataMode: 'server',
            ajax: { url: apiUrl + '/api/users', type: 'GET', xhrFields: { withCredentials: true } },
            onResponse: writeKpis,
            // quickView and edit are the factory's (quickView/form below); the rest are the screen's endpoints.
            actions: { onRowAction: Object.assign({ delete: deleteRow }, ...Object.keys(adminActions).map((key) => ({ [key]: runAdminAction(adminActions[key]) }))) },
            // No "+ Add" at all without auth.users.create (UAS-001: absent, not disabled).
            toolbar: { addNewText: canCreate() ? L().AddNew : '', onAddNew: () => list.openCreate(), exportColumns: [1, 2, 3, 4, 5, 6], colvisColumns: [1, 2, 3, 4, 5, 6] },
            filters: { hostId: 'inlineFilterHost', collapseId: 'inlineFilterCollapse', fields: filterFields, loadOptions: loadRoleOptions },
            savedView: { moduleKey: 'Governance', pageKey: 'Users', saveViewColumnIndexes: [1, 2, 3, 4, 5, 6], defaultVisibleColumnIndexes: [1, 2, 3, 4, 5, 6], baseOrder: [[1, 'asc']] },
            quickView: { offcanvasId: 'offcanvasDetailsPreview', populate: populateDetailsOffcanvas },
            form: { formId: 'formUser', offcanvasId: 'offcanvasCreateEdit', alertId: 'formUserAlert', saveBtnId: 'btnSaveUser', labelId: 'offcanvasCreateEditLabel', reset: resetFormFields, load: loadFormFields, submit: submitForm },
            config: {
                // No selection column: every column after the control column and before the actions reorders.
                colReorder: { columns: ':gt(0):not(:last-child)' },
                // `data` names are the service's orderBy whitelist (email, firstName, lastName, accountKind, status).
                columns: [
                    { data: 'id', name: 'control' }, { data: 'email', name: 'email' }, { data: 'firstName', name: 'firstName' }, { data: 'lastName', name: 'lastName' },
                    { data: 'roles', name: 'roles' }, { data: 'accountKind', name: 'accountKind' }, { data: 'status', name: 'status' }, { data: 'id', name: 'action' }
                ],
                columnDefs: [
                    { targets: 0, className: 'control', searchable: false, orderable: false, responsivePriority: 2, render: () => '' },
                    { targets: 1, render: (data) => `<span class="fw-medium text-heading">${escapeHtml(data)}</span>` },
                    { targets: 4, orderable: false, className: 'text-start', render: (data, type) => type === 'display' ? renderRoleChips(data, { collapse: true }) : (Array.isArray(data) ? data.join(', ') : '') },
                    { targets: 5, className: 'text-start', render: (data, type) => type === 'display' ? renderAccountKindBadge(data) : accountKindLabel(data) },
                    { targets: 6, defaultContent: '', render: (data, type, full) => type === 'display' ? renderStatusBadge(full) : statusOfRow(full).title },
                    { targets: -1, title: L().Actions, searchable: false, orderable: false, className: 'cell-fit all', render: renderRowActions }
                ]
            }
        });
    };

    const bindEvents = () => {
        // Change account kind (quick view) → same-origin proxy → AuthService POST /api/users/{id}/account-kind.
        // One dialog body (window.showConfirm, BL-367); the entity line names the account and the target kind.
        byId('oc-btn-accountkind')?.addEventListener('click', () => {
            if (!canManageKind()) return;
            const btn = byId('oc-btn-accountkind');
            const id = btn?.dataset.userId;
            const kind = normalizeAccountKind(byId('oc-accountkind-select')?.value);
            if (!id) return;
            window.showConfirm?.(L().ChangeAccountKind, () => postAccountKind(id, kind),
                { entityName: `${btn?.dataset.userEmail || ''} → ${accountKindLabel(kind)}`, type: 'primary', icon: 'bx-id-card', confirmButtonText: L().ChangeAccountKind });
        });
        byId('oc-btn-edit')?.addEventListener('click', () => {
            const id = byId('oc-btn-edit')?.dataset.editId;
            if (id) list?.openEdit(id);
        });
    };

    return {
        init: function () { initOffcanvasSelect2(); initDataTable(); bindEvents(); },
        // Exposed so the tests drive THESE select2 seams with the real vendored library, not a copy.
        offcanvasSelects: { init: initOffcanvasSelect2, setValue: setSelectValue, setHidden: setSelectHidden }
    };
})();

document.addEventListener('DOMContentLoaded', () => UsersList.init());
