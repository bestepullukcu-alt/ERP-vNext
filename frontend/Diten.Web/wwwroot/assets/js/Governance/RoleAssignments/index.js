/**
 * Tenant Role-Permission Assignment (FE-C 2/3, MOD-0018-FU9)
 * FEAT-ROLEPERMS-REDESIGN — two-panel Sneat layout: left = filters + collapsible module group cards of
 * permission row-cards; right = "Selected Role" panel (coverage %, stat cards, action distribution).
 * GrantSource rules (RoleGrantState): System=Baseline (locked), Module=entitlement (locked),
 * Manual=removable; new assignments become Manual. FE-B (window.Permissions) gates the assign/remove
 * affordances — UX only; backend [HasPermission] + the tenant-scope guard are authoritative.
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
    let currentStateFilter = ''; // '' | 'assigned' | 'unassigned'
    let assignedTotalCount = 0;  // role-wide assigned count (not filtered), for the header summary

    // FIX-ROLEPERMS-MODULE-CASE — case-insensitive module grouping; remember the first raw casing for display.
    let moduleCanonicalLabel = {}; // normalizedKey -> first-seen raw module string
    let rolesById = {};            // id -> role object (for the panel subtitle)

    const normalizeModuleKey = (module) => (module || '').trim().toLowerCase();

    // FIX-ROLEPERMS-ROLENAME-L10N-STICKY — a SYSTEM role's display name is localized from its CODE (role.name) so the
    // UI language wins over the stored (single-language) DisplayName; custom roles keep their own DisplayName.
    const roleLabel = (role) => {
        if (!role) return '';
        if (role.isSystem) return (L.RoleNames && L.RoleNames[role.name]) || role.displayName || role.name || '';
        return role.displayName || role.name || '';
    };

    // Action → glyph/tone for the row-card icon AND the action-distribution bars (single source, shared by both).
    // Unknown actions fall back to a neutral icon/tone.
    const ACTION_ICON = {
        read: 'bx-show', create: 'bx-plus', update: 'bx-edit', delete: 'bx-trash',
        archive: 'bx-archive-in', export: 'bx-export'
    };
    const ACTION_TONE = {
        read: 'info', create: 'primary', update: 'warning', delete: 'danger',
        archive: 'secondary', export: 'secondary'
    };
    const actionIcon = (action) => ACTION_ICON[(action || '').toLowerCase()] || 'bx-cog';
    const actionTone = (action) => ACTION_TONE[(action || '').toLowerCase()] || 'secondary';

    // FEAT-ROLEPERMS-LABEL-DERIVE — the row label is DERIVED from the permission KEY, not the drifted stored
    // DisplayName (which mixed languages and mislabelled shared keys, e.g. "Menu Settings"/"Archive"). Format is
    // "{Verb} · {Entity}": the action verb is localized (window.L10n.ActionVerbs, user's language), the entity is
    // the humanized resource segment (product term, kept language-neutral so new self-registered modules just work).
    const humanize = (s) => (s || '')
        .split(/[-.]/)
        .filter(Boolean)
        .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
        .join(' ');
    const deriveVerb = (action) => {
        const a = (action || '').toLowerCase();
        return (L.ActionVerbs && L.ActionVerbs[a]) || humanize(action);
    };
    const deriveLabel = (perm) => {
        const verb = deriveVerb(perm.action);
        const entity = humanize(perm.resource);
        return entity ? verb + ' · ' + entity : verb;
    };

    const els = {};
    const cacheEls = () => {
        els.roleSelect = document.getElementById('raRoleSelect');
        els.moduleFilter = document.getElementById('raModuleFilter');
        els.search = document.getElementById('raSearch');
        els.stateTabs = document.querySelectorAll('.ra-state-tab');
        els.summary = document.getElementById('raSummary');
        els.groups = document.getElementById('raGroups');
        els.empty = document.getElementById('raEmpty');
        els.alert = document.getElementById('raAlert');
        els.skeleton = document.getElementById('raSkeleton');
        els.rightSkeleton = document.getElementById('raRightSkeleton');
        els.groupTpl = document.getElementById('raGroupCardTemplate');
        els.rowTpl = document.getElementById('raRowCardTemplate');
        els.actionTpl = document.getElementById('raActionDistTemplate');
        // Right panel
        els.rolePanel = document.getElementById('raRolePanel');
        els.roleName = document.getElementById('raRoleName');
        els.roleSubtitle = document.getElementById('raRoleSubtitle');
        els.coveragePercent = document.getElementById('raCoveragePercent');
        els.coverageBar = document.getElementById('raCoverageBar');
        els.assignedRemaining = document.getElementById('raAssignedRemaining');
        els.statTotal = document.getElementById('raStatTotal');
        els.statAssigned = document.getElementById('raStatAssigned');
        els.statLocked = document.getElementById('raStatLocked');
        els.statUnassigned = document.getElementById('raStatUnassigned');
        els.actionDist = document.getElementById('raActionDist');
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

    const format = (template, ...args) => {
        let out = template || '';
        args.forEach((v, i) => { out = out.replace('{' + i + '}', v); });
        return out;
    };

    // Sets a progress-bar fill width WITHOUT an inline style: toggles a .ra-fill-N utility class (N in 5% steps),
    // so the rendered DOM carries no `style=` attribute (project rule). Removes any prior fill class first.
    const setFill = (el, pct) => {
        if (!el) return;
        const step = Math.max(0, Math.min(100, Math.round((Number(pct) || 0) / 5) * 5));
        el.className = el.className.replace(/\bra-fill-\d+\b/g, '').replace(/\s+/g, ' ').trim();
        el.classList.add('ra-fill-' + step);
    };

    const badgeFor = (badge) => {
        if (!badge) return null;
        if (badge.key === 'Baseline') return { text: L.BadgeBaseline || 'Baseline', cls: 'bg-label-secondary' };
        if (badge.key === 'Module') return { text: (L.BadgeModule || 'Module') + ': ' + (badge.moduleCode || '?'), cls: 'bg-label-info' };
        return { text: L.BadgeManual || 'Manual', cls: 'bg-label-primary' };
    };

    const lockHintFor = (state, moduleCode) => {
        if (state.source === 'System') return L.LockedHintSystem || L.LockedHint || '';
        if (state.source === 'Module') return (L.LockedHintModule || L.LockedHint || '').replace('{0}', moduleCode || '?');
        return L.LockedHint || '';
    };

    const groupLabel = (moduleKey) => moduleCanonicalLabel[moduleKey] || moduleKey || (L.ModuleUngrouped || 'Other');

    const renderRow = (perm) => {
        const grant = grantsByPermissionId[perm.id] || null;
        const state = window.RoleGrantState.evaluate(perm, grant, canAssign());
        const moduleCode = perm.module || '';
        const moduleKey = normalizeModuleKey(perm.module);

        const label = deriveLabel(perm);
        const node = els.rowTpl.content.firstElementChild.cloneNode(true);
        node.dataset.key = (perm.key || '').toLowerCase();
        // Search matches the VISIBLE derived label (not the stored DisplayName), so results track what the user sees.
        node.dataset.display = label.toLowerCase();
        node.dataset.permissionId = perm.id;
        node.dataset.module = moduleKey;
        node.dataset.assigned = state.assigned ? '1' : '0';
        node.dataset.locked = state.locked ? '1' : '0';

        node.querySelector('.ra-row-icon-wrap').classList.add('bg-label-' + actionTone(perm.action));
        node.querySelector('.ra-row-icon').classList.add(actionIcon(perm.action));
        node.querySelector('.ra-display').textContent = label;
        node.querySelector('.ra-key').textContent = perm.key || '';

        const badgeEl = node.querySelector('.ra-badge');
        const b = badgeFor(state.badge);
        if (b) { badgeEl.textContent = b.text; badgeEl.className = 'badge ra-badge ' + b.cls; }
        else badgeEl.remove();

        const lockEl = node.querySelector('.ra-lock');
        const assignBtn = node.querySelector('.ra-assign');
        const removeBtn = node.querySelector('.ra-remove');

        if (state.locked) {
            lockEl.classList.remove('d-none');
            lockEl.title = lockHintFor(state, moduleCode);
        }
        if (state.assignable) {
            assignBtn.querySelector('.ra-assign-text').textContent = L.Assign || 'Assign';
            assignBtn.classList.remove('d-none');
            assignBtn.addEventListener('click', () => doAssign(perm));
        }
        if (state.removable) {
            removeBtn.querySelector('.ra-remove-text').textContent = L.Delete || L.Remove || 'Remove';
            removeBtn.classList.remove('d-none');
            removeBtn.addEventListener('click', () => doRevoke(perm));
        }
        return { node, state };
    };

    const wireGroupToggle = (card) => {
        const toggle = card.querySelector('.ra-group-toggle');
        const setExpanded = (expanded) => {
            card.classList.toggle('ra-collapsed', !expanded);
            toggle.setAttribute('aria-expanded', expanded ? 'true' : 'false');
        };
        const flip = () => setExpanded(card.classList.contains('ra-collapsed'));
        toggle.addEventListener('click', flip);
        toggle.addEventListener('keydown', (e) => {
            if (e.key === 'Enter' || e.key === ' ' || e.key === 'Spacebar') { e.preventDefault(); flip(); }
        });
    };

    const renderGroupCard = (moduleKey, perms) => {
        const card = els.groupTpl.content.firstElementChild.cloneNode(true);
        card.dataset.module = moduleKey;
        card.querySelector('.ra-group-name').textContent = groupLabel(moduleKey);

        const body = card.querySelector('.ra-group-body');
        let assigned = 0;
        let locked = 0;
        perms.forEach((perm) => {
            const { node, state } = renderRow(perm);
            if (state.assigned) assigned++;
            if (state.locked) locked++;
            body.appendChild(node);
        });

        card.querySelector('.ra-group-count').textContent = format(L.CountBadge || '{0}/{1}', assigned, perms.length);
        const lockBadge = card.querySelector('.ra-group-locks');
        if (locked > 0) {
            card.querySelector('.ra-group-locks-count').textContent = locked;
            lockBadge.classList.remove('d-none');
        } else {
            lockBadge.remove();
        }

        wireGroupToggle(card);
        return card;
    };

    const hideSkeletons = () => {
        els.skeleton?.classList.add('d-none');
        els.rightSkeleton?.classList.add('d-none');
    };

    const setEmpty = (message) => {
        hideSkeletons();
        els.groups.classList.add('d-none');
        els.groups.innerHTML = '';
        els.rolePanel.classList.add('d-none');
        els.empty.classList.remove('d-none');
        els.empty.querySelector('.card-body').textContent = message || '';
        if (els.summary) els.summary.textContent = '';
    };

    const renderList = () => {
        hideSkeletons();
        if (!currentRoleId) { setEmpty(L.SelectRolePrompt || ''); return; }
        if (!catalog.length) { setEmpty(L.NoPermissions || ''); return; }

        // Group permissions by normalized module, each group's rows sorted by key.
        const groups = new Map();
        catalog.forEach((perm) => {
            const key = normalizeModuleKey(perm.module);
            if (!groups.has(key)) groups.set(key, []);
            groups.get(key).push(perm);
        });
        const orderedKeys = Array.from(groups.keys()).sort((a, b) => a.localeCompare(b));

        const frag = document.createDocumentFragment();
        orderedKeys.forEach((key) => {
            const perms = groups.get(key).slice().sort((a, b) => (a.key || '').localeCompare(b.key || ''));
            frag.appendChild(renderGroupCard(key, perms));
        });

        els.groups.innerHTML = '';
        els.groups.appendChild(frag);
        els.groups.classList.remove('d-none');
        els.empty.classList.add('d-none');

        renderRolePanel();
        applyFilters();
    };

    // Right "Selected Role" panel — coverage %, 4 stat cards, action distribution. Recomputed from the
    // current catalog + grants so it stays live after every assign/revoke.
    const renderRolePanel = () => {
        const role = rolesById[currentRoleId];
        const total = catalog.length;
        let assigned = 0;
        let locked = 0;
        // FEAT-ROLEPERMS-ACTIONDIST-DYNAMIC + -DEDUPE — one bucket per DISTINCT VERB (the displayed deriveVerb()),
        // not per raw action: synonymous actions that map to the same verb (e.g. read + view → "Görüntüle") merge
        // into ONE row with summed counts, so no verb ever repeats. `actions` tracks each contributing raw action's
        // count so the merged row can borrow the icon/tone of its dominant action.
        const verbStats = {}; // verbLabel -> { total, assigned, actions: { rawAction: count } }

        catalog.forEach((perm) => {
            const grant = grantsByPermissionId[perm.id] || null;
            const state = window.RoleGrantState.evaluate(perm, grant, canAssign());
            if (state.assigned) assigned++;
            if (state.locked) locked++;
            const ak = (perm.action || '').toLowerCase();
            const verb = deriveVerb(ak);
            let v = verbStats[verb];
            if (!v) v = verbStats[verb] = { total: 0, assigned: 0, actions: {} };
            v.total++;
            if (state.assigned) v.assigned++;
            v.actions[ak] = (v.actions[ak] || 0) + 1;
        });
        assignedTotalCount = assigned;
        const unassigned = total - assigned;
        const pct = total > 0 ? Math.round((assigned / total) * 100) : 0;

        const displayName = roleLabel(role);
        els.roleName.textContent = displayName;
        // Subtitle = the role's technical name when it differs from the display name (else hidden).
        const techName = role && role.name && role.name !== displayName ? role.name : '';
        els.roleSubtitle.textContent = techName;
        els.roleSubtitle.classList.toggle('d-none', !techName);

        els.coveragePercent.textContent = pct + '%';
        setFill(els.coverageBar, pct);
        const track = els.coverageBar.closest('.progress');
        if (track) track.setAttribute('aria-valuenow', String(pct));
        els.assignedRemaining.textContent = format(L.AssignedRemaining || '{0} / {1}', assigned, unassigned);

        els.statTotal.textContent = total;
        els.statAssigned.textContent = assigned;
        els.statLocked.textContent = locked;
        els.statUnassigned.textContent = unassigned;

        // Action distribution — one row per distinct VERB, labelled by the SAME deriveVerb() the permission rows use
        // (single source: panel says "Görüntüle" exactly like the rows, never a divergent "Okuma"). Synonymous
        // actions are already merged into one bucket; the icon/tone come from the verb's dominant raw action via the
        // row's actionIcon()/actionTone() mapping (neutral default when unmapped). Sorted by descending total (ties
        // broken by the verb label) so the biggest groups lead.
        els.actionDist.innerHTML = '';
        const dominantAction = (v) => Object.keys(v.actions)
            .reduce((best, a) => (v.actions[a] > v.actions[best] ? a : best), Object.keys(v.actions)[0]);
        const verbKeys = Object.keys(verbStats).sort((a, b) => {
            const byTotal = verbStats[b].total - verbStats[a].total;
            return byTotal !== 0 ? byTotal : a.localeCompare(b);
        });
        verbKeys.forEach((verb) => {
            const v = verbStats[verb];
            if (!v || v.total === 0) return;
            const row = els.actionTpl.content.firstElementChild.cloneNode(true);
            const bpct = Math.round((v.assigned / v.total) * 100);
            const dom = dominantAction(v);
            const tone = actionTone(dom);
            row.querySelector('.ra-action-icon-wrap').classList.add('bg-label-' + tone);
            row.querySelector('.ra-action-icon').classList.add(actionIcon(dom));
            row.querySelector('.ra-action-label').textContent = verb;
            row.querySelector('.ra-action-count').textContent = format(L.CountBadge || '{0}/{1}', v.assigned, v.total);
            const fill = row.querySelector('.ra-action-fill');
            fill.classList.add('bg-' + tone);
            setFill(fill, bpct);
            row.querySelector('.ra-action-track').setAttribute('aria-valuenow', String(bpct));
            els.actionDist.appendChild(row);
        });

        els.rolePanel.classList.remove('d-none');
    };

    const populateModuleFilter = () => {
        moduleCanonicalLabel = {};
        catalog.forEach((p) => {
            const key = normalizeModuleKey(p.module);
            if (key && !(key in moduleCanonicalLabel)) moduleCanonicalLabel[key] = (p.module || '').trim();
        });

        if (!els.moduleFilter) return;
        const keys = Object.keys(moduleCanonicalLabel).sort((a, b) => a.localeCompare(b));
        keys.forEach((key) => {
            const opt = document.createElement('option');
            opt.value = key;
            opt.textContent = moduleCanonicalLabel[key];
            els.moduleFilter.appendChild(opt);
        });
    };

    // Search text, module multi-select, and the assignment-state chip stack as AND conditions over each row.
    // A group card hides entirely when none of its rows match; the header summary reflects the filtered count.
    const applyFilters = () => {
        if (!currentRoleId || !catalog.length) return;
        const q = (els.search?.value || '').trim().toLowerCase();
        const selectedModules = els.moduleFilter
            ? Array.from(els.moduleFilter.selectedOptions).map((o) => o.value).filter(Boolean)
            : [];

        let visibleCount = 0;
        els.groups.querySelectorAll('.ra-group').forEach((card) => {
            let groupVisible = 0;
            card.querySelectorAll('.ra-row').forEach((row) => {
                const matchesSearch = q.length === 0
                    || (row.dataset.key || '').includes(q)
                    || (row.dataset.display || '').includes(q);
                const matchesModule = selectedModules.length === 0 || selectedModules.includes(row.dataset.module);
                const matchesState = !currentStateFilter
                    || (currentStateFilter === 'assigned' ? row.dataset.assigned === '1' : row.dataset.assigned === '0');
                const visible = matchesSearch && matchesModule && matchesState;
                row.classList.toggle('d-none', !visible);
                if (visible) { groupVisible++; visibleCount++; }
            });
            card.classList.toggle('d-none', groupVisible === 0);
        });

        if (els.summary) {
            const shown = format(L.ShowingCount || '{0}', visibleCount);
            const summary = format(L.PermissionSummary || '{0}/{1}', assignedTotalCount, catalog.length);
            els.summary.textContent = shown + ' · ' + summary;
        }
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
            rolesById[r.id] = r;
            const opt = document.createElement('option');
            opt.value = r.id;
            const label = roleLabel(r);
            opt.textContent = label + (r.name && r.name !== label ? ' (' + r.name + ')' : '');
            els.roleSelect.appendChild(opt);
        });
    };

    const normalizeString = (v) => (v == null ? '' : String(v)).trim();
    const normalizeArray = (v) => (Array.isArray(v) ? v : (v == null || v === '' ? [] : [v]));

    // Role select — plain single select2 (searchable), consistent with the rest of the app.
    const initSelect2 = (selector) => {
        if (!window.jQuery || !window.jQuery.fn?.select2) return;
        const $select = window.jQuery(selector);
        if (!$select.length) return;
        if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
        if (!$select.parent().hasClass('position-relative')) $select.wrap('<div class="position-relative"></div>');
        $select.select2({
            dropdownParent: $select.parent(),
            placeholder: $select.data('placeholder') || $select.find('option:first').text(),
            allowClear: false,
            width: '100%'
        });
    };

    // Module filter — full-size multi-select rendered in the GoldenReferenceCompact "placeholder + N count +
    // chevron + clear" style (selected values collapse to a count badge instead of overflowing chips).
    const syncModuleSummary = ($select) => {
        const $container = $select.next('.select2-container');
        const $selection = $container.find('.select2-selection--multiple');
        if (!$container.length || !$selection.length) return;
        let $summary = $selection.find('.dt-inline-filter-multi__summary');
        let $actions = $selection.find('.dt-inline-filter-multi__actions');
        let $count = $selection.find('.dt-inline-filter-multi__count');
        let $arrow = $selection.find('.select2-selection__arrow');
        if (!$summary.length) { $summary = window.jQuery('<span class="dt-inline-filter-multi__summary"></span>'); $selection.prepend($summary); }
        if (!$actions.length) { $actions = window.jQuery('<span class="dt-inline-filter-multi__actions"></span>'); $selection.append($actions); }
        if (!$count.length) { $count = window.jQuery('<span class="dt-inline-filter-multi__count badge rounded-pill bg-label-primary d-none"></span>'); $actions.append($count); }
        if (!$arrow.length) { $arrow = window.jQuery('<span class="select2-selection__arrow" role="presentation"><b role="presentation"></b></span>'); $selection.append($arrow); }
        const placeholder = normalizeString($select.data('placeholder'));
        const selectedValues = normalizeArray($select.val());
        const selectedTexts = ($select.select2('data') || []).map((i) => normalizeString(i.text)).filter(Boolean);
        $summary.text(placeholder);
        $selection.find('.select2-selection__rendered').attr('title', selectedTexts.join(', ') || placeholder);
        // The container class (ra-filter-multi) lands on the .select2-selection element in this select2 build,
        // so the has-value marker toggles there too (keeps CSS selectors on one element).
        $selection.toggleClass('dt-inline-filter-multi--has-value', selectedValues.length > 0);
        $count.toggleClass('d-none', selectedValues.length === 0).text(String(selectedValues.length));
        $actions.find('.dt-multi-clear-btn').remove();
        if (selectedValues.length > 0) {
            const label = L.Reset || '';
            const $clear = window.jQuery('<span class="dt-multi-clear-btn" role="button" aria-label="' + label + '" title="' + label + '">×</span>');
            $clear.on('mousedown', (e) => { e.preventDefault(); e.stopPropagation(); $select.val(null).trigger('change'); });
            $actions.append($clear);
        }
    };

    const initModuleSelect2 = (selector) => {
        if (!window.jQuery || !window.jQuery.fn?.select2) return;
        const $select = window.jQuery(selector);
        if (!$select.length) return;
        if ($select.hasClass('select2-hidden-accessible')) $select.select2('destroy');
        $select.select2({
            dropdownParent: window.jQuery(document.body),
            dropdownCssClass: 'dt-inline-filter-dropdown',
            containerCssClass: 'ra-filter-multi',
            placeholder: $select.data('placeholder') || '',
            minimumResultsForSearch: Infinity,
            closeOnSelect: false,
            width: '100%'
        });
        $select.on('change.ra-summary', () => syncModuleSummary($select));
        // Build the summary synchronously (select2 has rendered the selection by now); a rAF pass is a backstop
        // in case the selection DOM settles a frame later.
        syncModuleSummary($select);
        (window.requestAnimationFrame || window.setTimeout)(() => syncModuleSummary($select));
    };

    const init = async () => {
        cacheEls();
        if (!els.roleSelect || !els.groups || !els.rowTpl) return;
        if (!apiUrl) { console.error('[RoleAssignments] window.API.auth is required.'); return; }
        L = window.L10n || {};

        // select2 emits selection via jQuery's .trigger('change'), which does NOT reach vanilla
        // addEventListener('change') handlers — the two select2-backed selects MUST bind through jQuery.
        const bindChange = (el, handler) => {
            if (!el) return;
            if (window.jQuery) window.jQuery(el).on('change', handler);
            else el.addEventListener('change', handler);
        };
        bindChange(els.roleSelect, onRoleChange);
        bindChange(els.moduleFilter, applyFilters);
        els.search?.addEventListener('input', applyFilters);
        // WorkCenter-style pill tabs replace the radio group: toggle .active + read data-state.
        els.stateTabs?.forEach((tab) => tab.addEventListener('click', () => {
            els.stateTabs.forEach((t) => {
                const active = t === tab;
                t.classList.toggle('active', active);
                t.setAttribute('aria-selected', active ? 'true' : 'false');
            });
            currentStateFilter = tab.dataset.state || '';
            applyFilters();
        }));

        // Upgrade the native selects to select2 BEFORE the async fetches so they never flash as raw
        // (unstyled multi-row) controls. select2 reads <option>s live, so options appended later by
        // loadRoles()/populateModuleFilter() still appear in the dropdowns.
        initSelect2('#raRoleSelect');
        initModuleSelect2('#raModuleFilter');

        try {
            // FEAT-ROLEPERMS-TENANT-SCOPE — only the tenant-assignable subset (platform-admin & reference-data
            // excluded server-side), mirroring the AssignPermissionCommandHandler escalation guard.
            catalog = (await apiGet('/api/permissions/tenant-assignable')) || [];
            if (!Array.isArray(catalog)) catalog = [];
        } catch (e) { console.error(e); showError(L.ErrorOccurred); }

        try { await loadRoles(); } catch (e) { console.error(e); showError(L.ErrorOccurred); }
        populateModuleFilter();

        // Deep-link: /RoleAssignments?roleId=... preselects the role (e.g. from the Roles list
        // "Manage Permissions" action). Options exist and select2 is initialized by now.
        const preRoleId = new URLSearchParams(window.location.search).get('roleId');
        if (preRoleId && rolesById[preRoleId]) {
            if (window.jQuery) window.jQuery(els.roleSelect).val(preRoleId).trigger('change');
            else { els.roleSelect.value = preRoleId; onRoleChange(); }
        } else {
            renderList();
        }
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => RoleAssignments.init());
