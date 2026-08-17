/**
 * Tenant Role-Permission Assignment (FE-C 2/3, MOD-0018-FU9)
 * Component-based screen (role selector + permission list-group + GrantSource badges) — NOT a
 * datatable. GrantSource rules (RoleGrantState): System=Baseline (locked), Module=entitlement
 * (locked), Manual=removable; new assignments become Manual. FE-B (window.Permissions) gates the
 * assign/remove affordances — UX only; backend [HasPermission] is authoritative.
 */
'use strict';

const RoleAssignments = (function () {
    const apiUrl = window.API?.auth;
    let L = window.L10n || {};
    const can = (key) => window.Permissions?.has?.(key) === true;
    const canAssign = () => can('auth.roles.assign-permission');

    let catalog = [];          // [{ id, key, module, resource, action, displayName }]
    let currentRoleId = null;
    let grantsByPermissionId = {};

    const els = {};
    const cacheEls = () => {
        els.roleSelect = document.getElementById('raRoleSelect');
        els.search = document.getElementById('raSearch');
        els.list = document.getElementById('raList');
        els.empty = document.getElementById('raEmpty');
        els.alert = document.getElementById('raAlert');
        els.tpl = document.getElementById('raRowTemplate');
    };

    const getAuthHeaders = () => window.DitenDataTable?.getAuthHeaders?.() || {};
    const getAntiForgeryToken = () =>
        document.querySelector('#raAntiForgery input[name="__RequestVerificationToken"]')?.value || '';

    const showError = (msg) => {
        if (!els.alert) return;
        els.alert.textContent = msg || L.ErrorOccurred || '';
        els.alert.classList.remove('d-none');
    };
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

    const postForm = async (url, roleId, permissionId) => {
        const body = new URLSearchParams();
        body.set('roleId', roleId);
        body.set('permissionId', permissionId);
        const res = await fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'RequestVerificationToken': getAntiForgeryToken(), 'Content-Type': 'application/x-www-form-urlencoded', ...getAuthHeaders() },
            body
        });
        return res.json();
    };

    const badgeFor = (badge) => {
        if (!badge) return null;
        if (badge.key === 'Baseline') return { text: L.BadgeBaseline || 'Baseline', cls: 'bg-label-secondary' };
        if (badge.key === 'Module') return { text: (L.BadgeModule || 'Module') + ': ' + (badge.moduleCode || '?'), cls: 'bg-label-info' };
        return { text: L.BadgeManual || 'Manual', cls: 'bg-label-primary' };
    };

    const renderRow = (perm) => {
        const grant = grantsByPermissionId[perm.id] || null;
        const state = window.RoleGrantState.evaluate(perm, grant, canAssign());

        const node = els.tpl.content.firstElementChild.cloneNode(true);
        node.dataset.key = (perm.key || '').toLowerCase();
        node.dataset.permissionId = perm.id;
        node.querySelector('.ra-key').textContent = perm.key || '';
        node.querySelector('.ra-display').textContent = perm.displayName || '';

        const badgeEl = node.querySelector('.ra-badge');
        const b = badgeFor(state.badge);
        if (b) { badgeEl.textContent = b.text; badgeEl.className = 'badge ra-badge ' + b.cls; }
        else badgeEl.remove();

        const lockEl = node.querySelector('.ra-lock');
        const assignBtn = node.querySelector('.ra-assign');
        const removeBtn = node.querySelector('.ra-remove');

        if (state.locked) lockEl.classList.remove('d-none');
        if (state.assignable) {
            assignBtn.classList.remove('d-none');
            assignBtn.addEventListener('click', () => doAssign(perm));
        }
        if (state.removable) {
            removeBtn.classList.remove('d-none');
            removeBtn.addEventListener('click', () => doRevoke(perm));
        }
        return node;
    };

    const renderList = () => {
        els.list.innerHTML = '';
        if (!currentRoleId) {
            els.list.classList.add('d-none');
            els.empty.classList.remove('d-none');
            els.empty.textContent = L.SelectRolePrompt || '';
            return;
        }
        if (!catalog.length) {
            els.list.classList.add('d-none');
            els.empty.classList.remove('d-none');
            els.empty.textContent = L.NoPermissions || '';
            return;
        }
        const frag = document.createDocumentFragment();
        catalog.slice()
            .sort((a, b) => (a.key || '').localeCompare(b.key || ''))
            .forEach((perm) => frag.appendChild(renderRow(perm)));
        els.list.appendChild(frag);
        els.list.classList.remove('d-none');
        els.empty.classList.add('d-none');
        applySearch();
    };

    const applySearch = () => {
        const q = (els.search?.value || '').trim().toLowerCase();
        els.list.querySelectorAll('.ra-row').forEach((row) => {
            row.classList.toggle('d-none', q.length > 0 && !(row.dataset.key || '').includes(q));
        });
    };

    const loadRolePermissions = async (roleId) => {
        grantsByPermissionId = {};
        const data = await apiGet('/api/roles/' + roleId + '/permissions');
        const list = (data && Array.isArray(data.permissions)) ? data.permissions : [];
        list.forEach((g) => { if (g && g.permissionId) grantsByPermissionId[g.permissionId] = g; });
    };

    const doAssign = async (perm) => {
        if (!currentRoleId) return;
        clearError();
        try {
            const json = await postForm('/RoleAssignments/assign', currentRoleId, perm.id);
            if (!json.success) { showError((json.errors || [])[0]); return; }
            window.showToast?.(L.RecordCreated, 'success');
            await loadRolePermissions(currentRoleId);
            renderList();
        } catch (e) { console.error(e); showError(L.ErrorOccurred); }
    };

    const doRevoke = (perm) => {
        if (!currentRoleId) return;
        const run = async () => {
            clearError();
            try {
                const json = await postForm('/RoleAssignments/revoke', currentRoleId, perm.id);
                if (!json.success) { showError((json.errors || [])[0]); return; }
                window.showToast?.(L.RecordDeleted, 'success');
                await loadRolePermissions(currentRoleId);
                renderList();
            } catch (e) { console.error(e); showError(L.ErrorOccurred); }
        };
        if (window.showConfirm) {
            window.showConfirm(L.AreYouSure, run, { entityName: perm.key, type: 'danger', confirmButtonText: L.Delete });
        } else {
            run();
        }
    };

    const onRoleChange = async () => {
        currentRoleId = els.roleSelect.value || null;
        clearError();
        if (!currentRoleId) { renderList(); return; }
        try {
            await loadRolePermissions(currentRoleId);
            renderList();
        } catch (e) { console.error(e); showError(L.ErrorOccurred); }
    };

    const loadRoles = async () => {
        const roles = await apiGet('/api/roles');
        const list = Array.isArray(roles) ? roles : [];
        list.sort((a, b) => (a.displayName || a.name || '').localeCompare(b.displayName || b.name || ''));
        list.forEach((r) => {
            const opt = document.createElement('option');
            opt.value = r.id;
            opt.textContent = (r.displayName || r.name || '') + (r.name && r.displayName ? ' (' + r.name + ')' : '');
            els.roleSelect.appendChild(opt);
        });
    };

    const init = async () => {
        cacheEls();
        if (!els.roleSelect || !els.list || !els.tpl) return;
        if (!apiUrl) { console.error('[RoleAssignments] window.API.auth is required.'); return; }
        L = window.L10n || {};

        els.roleSelect.addEventListener('change', onRoleChange);
        els.search?.addEventListener('input', applySearch);

        try {
            catalog = (await apiGet('/api/permissions')) || [];
            if (!Array.isArray(catalog)) catalog = [];
        } catch (e) { console.error(e); showError(L.ErrorOccurred); }

        try { await loadRoles(); } catch (e) { console.error(e); showError(L.ErrorOccurred); }
        renderList();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => RoleAssignments.init());
