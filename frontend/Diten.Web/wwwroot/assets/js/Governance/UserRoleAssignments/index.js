/**
 * Tenant User-Role Assignment (FE-C 3/3, MOD-0018-FU9)
 * Component-based screen (user selector + role list-group + <template> rows) — NOT a datatable.
 * UserRoleAssignState (pure, vitest-tested) drives row state. FE-B (window.Permissions) gates the
 * assign/remove affordances (auth.users.assign-role) — UX only; backend is authoritative.
 */
'use strict';

const UserRoleAssignments = (function () {
    const apiUrl = window.API?.auth;
    let L = window.L10n || {};
    const can = (key) => window.Permissions?.has?.(key) === true;
    const canAssign = () => can('auth.users.assign-role');

    let rolesCatalog = [];           // [{ id, name, displayName }]
    let currentUserId = null;
    let assignedRoleNames = new Set();

    const els = {};
    const cacheEls = () => {
        els.userSelect = document.getElementById('uraUserSelect');
        els.search = document.getElementById('uraSearch');
        els.list = document.getElementById('uraList');
        els.empty = document.getElementById('uraEmpty');
        els.alert = document.getElementById('uraAlert');
        els.tpl = document.getElementById('uraRowTemplate');
    };

    const getAuthHeaders = () => window.DitenDataTable?.getAuthHeaders?.() || {};
    const getAntiForgeryToken = () =>
        document.querySelector('#uraAntiForgery input[name="__RequestVerificationToken"]')?.value || '';

    const showError = (msg) => { if (els.alert) { els.alert.textContent = msg || L.ErrorOccurred || ''; els.alert.classList.remove('d-none'); } };
    const clearError = () => els.alert?.classList.add('d-none');

    const unwrap = (json) => {
        if (json?.data?.data !== undefined) return json.data.data;
        return json?.data ?? json?.Data ?? null;
    };

    const apiGet = async (path) => {
        const res = await fetch(apiUrl + path, { method: 'GET', credentials: 'include', headers: getAuthHeaders() });
        if (!res.ok) throw new Error('GET ' + path + ' failed: ' + res.status);
        return unwrap(await res.json());
    };

    const postForm = async (url, userId, roleId) => {
        const body = new URLSearchParams();
        body.set('userId', userId);
        body.set('roleId', roleId);
        const res = await fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'RequestVerificationToken': getAntiForgeryToken(), 'Content-Type': 'application/x-www-form-urlencoded', ...getAuthHeaders() },
            body
        });
        return res.json();
    };

    const renderRow = (role) => {
        const assigned = assignedRoleNames.has(String(role.name || '').toLowerCase());
        const state = window.UserRoleAssignState.evaluate(role, assigned, canAssign());

        const node = els.tpl.content.firstElementChild.cloneNode(true);
        node.dataset.search = ((role.displayName || '') + ' ' + (role.name || '')).toLowerCase();
        node.querySelector('.ura-display').textContent = role.displayName || role.name || '';
        node.querySelector('.ura-name').textContent = role.name || '';

        const assignedBadge = node.querySelector('.ura-assigned-badge');
        const assignBtn = node.querySelector('.ura-assign');
        const removeBtn = node.querySelector('.ura-remove');

        if (state.assigned) assignedBadge.classList.remove('d-none'); else assignedBadge.remove();
        if (state.assignable) { assignBtn.classList.remove('d-none'); assignBtn.addEventListener('click', () => doAssign(role)); }
        if (state.removable) { removeBtn.classList.remove('d-none'); removeBtn.addEventListener('click', () => doRevoke(role)); }
        return node;
    };

    const renderList = () => {
        els.list.innerHTML = '';
        if (!currentUserId) {
            els.list.classList.add('d-none');
            els.empty.classList.remove('d-none');
            els.empty.textContent = L.SelectUserPrompt || '';
            return;
        }
        if (!rolesCatalog.length) {
            els.list.classList.add('d-none');
            els.empty.classList.remove('d-none');
            els.empty.textContent = L.NoRoles || '';
            return;
        }
        const frag = document.createDocumentFragment();
        rolesCatalog.slice()
            .sort((a, b) => (a.displayName || a.name || '').localeCompare(b.displayName || b.name || ''))
            .forEach((role) => frag.appendChild(renderRow(role)));
        els.list.appendChild(frag);
        els.list.classList.remove('d-none');
        els.empty.classList.add('d-none');
        applySearch();
    };

    const applySearch = () => {
        const q = (els.search?.value || '').trim().toLowerCase();
        els.list.querySelectorAll('.ura-row').forEach((row) => {
            row.classList.toggle('d-none', q.length > 0 && !(row.dataset.search || '').includes(q));
        });
    };

    const loadUserRoles = async (userId) => {
        assignedRoleNames = new Set();
        const user = await apiGet('/api/users/' + userId);
        const roles = (user && Array.isArray(user.roles)) ? user.roles : [];
        roles.forEach((r) => { if (r) assignedRoleNames.add(String(r).toLowerCase()); });
    };

    const doAssign = async (role) => {
        if (!currentUserId) return;
        clearError();
        try {
            const json = await postForm('/UserRoleAssignments/assign', currentUserId, role.id);
            if (!json.success) { showError((json.errors || [])[0]); return; }
            window.showToast?.(L.RecordCreated, 'success');
            await loadUserRoles(currentUserId);
            renderList();
        } catch (e) { console.error(e); showError(L.ErrorOccurred); }
    };

    const doRevoke = (role) => {
        if (!currentUserId) return;
        const run = async () => {
            clearError();
            try {
                const json = await postForm('/UserRoleAssignments/revoke', currentUserId, role.id);
                if (!json.success) { showError((json.errors || [])[0]); return; }
                window.showToast?.(L.RecordDeleted, 'success');
                await loadUserRoles(currentUserId);
                renderList();
            } catch (e) { console.error(e); showError(L.ErrorOccurred); }
        };
        if (window.showConfirm) {
            window.showConfirm(L.AreYouSure, run, { entityName: role.displayName || role.name, type: 'danger', confirmButtonText: L.Delete });
        } else {
            run();
        }
    };

    const onUserChange = async () => {
        currentUserId = els.userSelect.value || null;
        clearError();
        if (!currentUserId) { renderList(); return; }
        try { await loadUserRoles(currentUserId); renderList(); }
        catch (e) { console.error(e); showError(L.ErrorOccurred); }
    };

    const loadUsers = async () => {
        const data = await apiGet('/api/users?page=1&pageSize=1000');
        const list = (data && Array.isArray(data.items)) ? data.items : (Array.isArray(data) ? data : []);
        list.sort((a, b) => (a.email || '').localeCompare(b.email || ''));
        list.forEach((u) => {
            const opt = document.createElement('option');
            opt.value = u.id;
            const name = [u.firstName, u.lastName].filter(Boolean).join(' ');
            opt.textContent = u.email + (name ? ' — ' + name : '');
            els.userSelect.appendChild(opt);
        });
    };

    const init = async () => {
        cacheEls();
        if (!els.userSelect || !els.list || !els.tpl) return;
        if (!apiUrl) { console.error('[UserRoleAssignments] window.API.auth is required.'); return; }
        L = window.L10n || {};

        els.userSelect.addEventListener('change', onUserChange);
        els.search?.addEventListener('input', applySearch);

        try { rolesCatalog = (await apiGet('/api/roles')) || []; if (!Array.isArray(rolesCatalog)) rolesCatalog = []; }
        catch (e) { console.error(e); showError(L.ErrorOccurred); }

        try { await loadUsers(); } catch (e) { console.error(e); showError(L.ErrorOccurred); }
        renderList();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => UserRoleAssignments.init());
