/**
 * Tenant User-Role Assignment (FE-C 3/3, MOD-0018-FU9) — card redesign.
 * Component-based screen (user selector + state tabs + assigned/unassigned role cards + right-hand
 * summary panel). NOT a datatable. UserRoleAssignState (pure, vitest-tested) drives row affordances;
 * FE-B (window.Permissions) gates assign/remove (auth.users.assign-role) — UX only, backend authoritative.
 *
 * Data comes straight from the gateway, no fabrication:
 *   GET /api/roles         → [{ id, name, displayName, description, isSystem, permissionCount }]
 *   GET /api/users/{id}    → { firstName, lastName, email, lastLoginAt, roles: [name…] }
 * "Total permissions" is the SUM of each assigned role's permissionCount (the API exposes no
 * cross-role permission union), so it is exact for non-overlapping roles and matches a single role.
 */
'use strict';

const UserRoleAssignments = (function () {
    const apiUrl = window.API?.auth;
    let L = window.L10n || {};
    const can = (key) => window.Permissions?.has?.(key) === true;
    const canAssign = () => can('auth.users.assign-role');

    let rolesCatalog = [];           // [{ id, name, displayName, description, isSystem, permissionCount }]
    let currentUserId = null;
    let currentUser = null;          // last-loaded user detail (name/email/lastLoginAt)
    let assignedRoleNames = new Set();
    let activeState = '';            // '' | 'assigned' | 'unassigned'

    const els = {};
    const cacheEls = () => {
        els.userSelect = document.getElementById('uraUserSelect');
        els.search = document.getElementById('uraSearch');
        els.alert = document.getElementById('uraAlert');
        els.empty = document.getElementById('uraEmpty');
        els.content = document.getElementById('uraContent');
        els.assignedSection = document.getElementById('uraAssignedSection');
        els.unassignedSection = document.getElementById('uraUnassignedSection');
        els.assignedList = document.getElementById('uraAssignedList');
        els.unassignedList = document.getElementById('uraUnassignedList');
        els.summary = document.getElementById('uraSummary');
        els.assignedCount = document.getElementById('uraAssignedCount');
        els.roleTpl = document.getElementById('uraRoleCardTemplate');
        els.activeTpl = document.getElementById('uraActiveRowTemplate');
        // Right panel
        els.panel = document.getElementById('uraUserPanel');
        els.userInitials = document.getElementById('uraUserInitials');
        els.userName = document.getElementById('uraUserName');
        els.userEmail = document.getElementById('uraUserEmail');
        els.userLastLoginWrap = document.getElementById('uraUserLastLoginWrap');
        els.userLastLogin = document.getElementById('uraUserLastLogin');
        els.statRoles = document.getElementById('uraStatRoles');
        els.statPerms = document.getElementById('uraStatPerms');
        els.activeList = document.getElementById('uraActiveList');
        els.activeEmpty = document.getElementById('uraActiveEmpty');
        els.tabs = Array.prototype.slice.call(document.querySelectorAll('.ura-state-tab'));
    };

    const getAuthHeaders = () => window.DitenDataTable?.getAuthHeaders?.() || {};
    const getAntiForgeryToken = () =>
        document.querySelector('#uraAntiForgery input[name="__RequestVerificationToken"]')?.value || '';

    const showError = (msg) => { if (els.alert) { els.alert.textContent = msg || L.ErrorOccurred || ''; els.alert.classList.remove('d-none'); } };
    const clearError = () => els.alert?.classList.add('d-none');

    const unwrap = (json) => {
        if (json?.data?.data !== undefined) return json.data.data;
        return json?.data ?? json?.Data ?? json;
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

    // ── formatting helpers (no fabricated data) ──────────────────────────────────────────────────
    const fmt = (template, value) => String(template || '').replace('{0}', value);
    const permCount = (role) => Number(role?.permissionCount) || 0;
    const isAssigned = (role) => assignedRoleNames.has(String(role.name || '').toLowerCase());

    // FIX-ROLEPERMS-ROLENAME-L10N-STICKY — a SYSTEM role's display name is localized from its CODE (role.name) so the
    // UI language wins over the stored (single-language) DisplayName; custom roles keep their own DisplayName.
    const roleLabel = (role) => {
        if (!role) return '';
        if (role.isSystem) return (L.RoleNames && L.RoleNames[role.name]) || role.displayName || role.name || '';
        return role.displayName || role.name || '';
    };

    const initialsOf = (user) => {
        const a = (user?.firstName || '').trim();
        const b = (user?.lastName || '').trim();
        const letters = ((a[0] || '') + (b[0] || '')).toUpperCase();
        return letters || (user?.email || '?').charAt(0).toUpperCase();
    };

    const relativeLastLogin = (iso) => {
        if (!iso) return '';
        const then = new Date(iso).getTime();
        if (!isFinite(then)) return '';
        const diffMs = Date.now() - then;
        const lang = (document.documentElement.getAttribute('lang') || 'en').split('-')[0];
        try {
            const rtf = new Intl.RelativeTimeFormat(lang, { numeric: 'auto' });
            const sec = Math.round(diffMs / 1000);
            const units = [['year', 31536000], ['month', 2592000], ['day', 86400], ['hour', 3600], ['minute', 60]];
            for (const [unit, span] of units) {
                if (Math.abs(sec) >= span) return rtf.format(-Math.round(sec / span), unit);
            }
            return rtf.format(-sec, 'second');
        } catch {
            return new Date(iso).toLocaleString(lang);
        }
    };

    // ── role card (left) ─────────────────────────────────────────────────────────────────────────
    const buildRoleCard = (role) => {
        const assigned = isAssigned(role);
        const state = window.UserRoleAssignState.evaluate(role, assigned, canAssign());
        const node = els.roleTpl.content.firstElementChild.cloneNode(true);

        node.dataset.search = ((roleLabel(role)) + ' ' + (role.displayName || '') + ' ' + (role.name || '')).toLowerCase();
        node.dataset.state = assigned ? 'assigned' : 'unassigned';

        const iconWrap = node.querySelector('.ura-role-icon');
        iconWrap.classList.add(assigned ? 'bg-label-primary' : 'bg-label-secondary');
        if (assigned) node.classList.add('ura-role-card--assigned');

        node.querySelector('.ura-role-name').textContent = roleLabel(role);
        const codeEl = node.querySelector('.ura-role-code');
        if (role.name) codeEl.textContent = role.name; else codeEl.remove();

        const desc = node.querySelector('.ura-role-desc');
        if (role.description) desc.textContent = role.description; else desc.remove();

        node.querySelector('.ura-role-permcount-text').textContent = fmt(L.PermissionCountFormat, permCount(role));

        const typeBadge = node.querySelector('.ura-role-type-badge');
        typeBadge.classList.remove('d-none');
        if (role.isSystem) { typeBadge.classList.add('bg-label-primary'); typeBadge.textContent = L.TypeSystem || ''; }
        else { typeBadge.classList.add('bg-label-info'); typeBadge.textContent = L.TypeCustom || ''; }

        const assignedBadge = node.querySelector('.ura-role-assigned-badge');
        const assignBtn = node.querySelector('.ura-assign');
        const removeBtn = node.querySelector('.ura-remove');

        if (state.assigned) {
            node.querySelector('.ura-role-assigned-text').textContent = L.Assigned || '';
            assignedBadge.classList.remove('d-none');
        } else {
            assignedBadge.remove();
        }
        if (state.assignable) {
            node.querySelector('.ura-assign-text').textContent = L.Assign || '';
            assignBtn.classList.remove('d-none');
            assignBtn.addEventListener('click', () => doAssign(role));
        } else {
            assignBtn.remove();
        }
        if (state.removable) {
            node.querySelector('.ura-remove-text').textContent = L.Remove || '';
            removeBtn.classList.remove('d-none');
            removeBtn.addEventListener('click', () => doRevoke(role));
        } else {
            removeBtn.remove();
        }
        return node;
    };

    // ── active-role row (right panel) ────────────────────────────────────────────────────────────
    const buildActiveRow = (role) => {
        const node = els.activeTpl.content.firstElementChild.cloneNode(true);
        node.querySelector('.ura-active-name').textContent = roleLabel(role);
        node.querySelector('.ura-active-permcount').textContent = fmt(L.PermissionCountFormat, permCount(role));
        const removeBtn = node.querySelector('.ura-active-remove');
        if (canAssign()) removeBtn.addEventListener('click', () => doRevoke(role));
        else removeBtn.remove();
        return node;
    };

    const sortedCatalog = () => rolesCatalog.slice()
        .sort((a, b) => (a.displayName || a.name || '').localeCompare(b.displayName || b.name || ''));

    // ── render ───────────────────────────────────────────────────────────────────────────────────
    const renderRolePanel = () => {
        if (!currentUser) { els.panel.classList.add('d-none'); return; }
        els.panel.classList.remove('d-none');

        els.userInitials.textContent = initialsOf(currentUser);
        const name = [currentUser.firstName, currentUser.lastName].filter(Boolean).join(' ');
        els.userName.textContent = name || currentUser.email || '';
        els.userEmail.textContent = currentUser.email || '';

        const rel = relativeLastLogin(currentUser.lastLoginAt);
        if (rel) { els.userLastLogin.textContent = rel; els.userLastLoginWrap.classList.remove('d-none'); }
        else { els.userLastLoginWrap.classList.add('d-none'); }

        const assignedRoles = sortedCatalog().filter(isAssigned);
        els.statRoles.textContent = assignedRoles.length;
        els.statPerms.textContent = assignedRoles.reduce((sum, r) => sum + permCount(r), 0);

        els.activeList.innerHTML = '';
        if (assignedRoles.length) {
            const frag = document.createDocumentFragment();
            assignedRoles.forEach((r) => frag.appendChild(buildActiveRow(r)));
            els.activeList.appendChild(frag);
            els.activeList.classList.remove('d-none');
            els.activeEmpty.classList.add('d-none');
        } else {
            els.activeList.classList.add('d-none');
            els.activeEmpty.classList.remove('d-none');
        }
    };

    const renderLists = () => {
        // No user selected → prompt only.
        if (!currentUserId) {
            els.content.classList.add('d-none');
            els.empty.classList.remove('d-none');
            els.empty.querySelector('.card-body').textContent = L.SelectUserPrompt || '';
            els.assignedCount.textContent = '';
            els.summary.textContent = '';
            return;
        }
        els.empty.classList.add('d-none');
        els.content.classList.remove('d-none');

        els.assignedList.innerHTML = '';
        els.unassignedList.innerHTML = '';

        const assignedFrag = document.createDocumentFragment();
        const unassignedFrag = document.createDocumentFragment();
        let assignedTotal = 0;
        sortedCatalog().forEach((role) => {
            if (isAssigned(role)) { assignedFrag.appendChild(buildRoleCard(role)); assignedTotal++; }
            else { unassignedFrag.appendChild(buildRoleCard(role)); }
        });
        els.assignedList.appendChild(assignedFrag);
        els.unassignedList.appendChild(unassignedFrag);
        els.assignedCount.textContent = '(' + assignedTotal + ')';

        applyFilters();
    };

    // Tab state + search decide which cards/sections show; summary counts what's visible.
    const applyFilters = () => {
        const q = (els.search?.value || '').trim().toLowerCase();
        let visible = 0;
        els.content.querySelectorAll('.ura-role-card').forEach((card) => {
            const matchesSearch = !q || (card.dataset.search || '').includes(q);
            const matchesState = !activeState || card.dataset.state === activeState;
            const show = matchesSearch && matchesState;
            card.classList.toggle('d-none', !show);
            if (show) visible++;
        });

        // Hide a section when the active tab excludes it or it has no visible cards.
        const assignedVisible = els.assignedList.querySelectorAll('.ura-role-card:not(.d-none)').length;
        const unassignedVisible = els.unassignedList.querySelectorAll('.ura-role-card:not(.d-none)').length;
        els.assignedSection.classList.toggle('d-none', activeState === 'unassigned' || assignedVisible === 0);
        els.unassignedSection.classList.toggle('d-none', activeState === 'assigned' || unassignedVisible === 0);

        els.summary.textContent = fmt(L.RolesShownFormat, visible);
    };

    const render = () => { renderLists(); renderRolePanel(); };

    // ── mutations ────────────────────────────────────────────────────────────────────────────────
    const doAssign = async (role) => {
        if (!currentUserId) return;
        clearError();
        try {
            const json = await postForm('/UserRoleAssignments/assign', currentUserId, role.id);
            if (!json.success) { showError((json.errors || [])[0]); return; }
            window.showToast?.(L.RecordCreated, 'success');
            await loadUserRoles(currentUserId);
            render();
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
                render();
            } catch (e) { console.error(e); showError(L.ErrorOccurred); }
        };
        if (window.showConfirm) {
            window.showConfirm(L.AreYouSure, run, { entityName: role.displayName || role.name, type: 'danger', confirmButtonText: L.Remove || L.Delete });
        } else {
            run();
        }
    };

    // ── data load ────────────────────────────────────────────────────────────────────────────────
    const loadUserRoles = async (userId) => {
        assignedRoleNames = new Set();
        const user = await apiGet('/api/users/' + userId);
        currentUser = user || null;
        const roles = (user && Array.isArray(user.roles)) ? user.roles : [];
        roles.forEach((r) => { if (r) assignedRoleNames.add(String(r).toLowerCase()); });
    };

    const onUserChange = async () => {
        currentUserId = els.userSelect.value || null;
        clearError();
        if (!currentUserId) { currentUser = null; render(); return; }
        try { await loadUserRoles(currentUserId); render(); }
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

    const onTabClick = (tab) => {
        activeState = tab.dataset.state || '';
        els.tabs.forEach((t) => {
            const on = t === tab;
            t.classList.toggle('active', on);
            t.setAttribute('aria-selected', on ? 'true' : 'false');
        });
        applyFilters();
    };

    // User picker — searchable single select2 (consistent with the RoleAssignments role select).
    const initUserSelect2 = () => {
        if (!window.jQuery || !window.jQuery.fn?.select2) return;
        const $sel = window.jQuery(els.userSelect);
        if (!$sel.length) return;
        if ($sel.hasClass('select2-hidden-accessible')) $sel.select2('destroy');
        if (!$sel.parent().hasClass('position-relative')) $sel.wrap('<div class="position-relative"></div>');
        $sel.select2({
            dropdownParent: $sel.parent(),
            placeholder: $sel.find('option:first').text(),
            allowClear: false,
            width: '100%'
        });
    };

    const init = async () => {
        cacheEls();
        if (!els.userSelect || !els.assignedList || !els.roleTpl) return;
        if (!apiUrl) { console.error('[UserRoleAssignments] window.API.auth is required.'); return; }
        L = window.L10n || {};

        // select2 emits change via jQuery's trigger, which does NOT reach vanilla addEventListener — bind through jQuery.
        const bindChange = (el, handler) => {
            if (!el) return;
            if (window.jQuery) window.jQuery(el).on('change', handler);
            else el.addEventListener('change', handler);
        };
        bindChange(els.userSelect, onUserChange);
        els.search?.addEventListener('input', applyFilters);
        els.tabs.forEach((tab) => tab.addEventListener('click', () => onTabClick(tab)));

        // Upgrade the user picker to select2 BEFORE the async fetches so it never flashes as a raw native
        // select; select2 reads <option>s live, so users appended by loadUsers() still appear + stay searchable.
        initUserSelect2();

        try { rolesCatalog = (await apiGet('/api/roles')) || []; if (!Array.isArray(rolesCatalog)) rolesCatalog = []; }
        catch (e) { console.error(e); showError(L.ErrorOccurred); }

        try { await loadUsers(); } catch (e) { console.error(e); showError(L.ErrorOccurred); }
        render();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => UserRoleAssignments.init());
