// FEAT-NAVPREFS-DOMAINS — tenant Menu Settings editor: per-domain cards (reorder + rename) each holding draggable
// module rows (reorder + rename + show/hide), with a live sidebar preview + stat cards. Loads the entitled menu
// (GET api/menu) ∪ current preferences (GET api/preferences → { modules, domains }), and persists the FULL set
// (PUT api/preferences → { modules, domains }). ZERO inline styles — JS only toggles classes.
const TenantNavigationSettings = (function () {
    'use strict';

    const root = document.getElementById('tenantNavRoot');
    const groupsHost = document.getElementById('tenantNavGroups');
    const loadingEl = document.getElementById('tenantNavLoading');
    const emptyEl = document.getElementById('tenantNavEmpty');
    const alertEl = document.getElementById('tenantNavAlert');
    const saveBtn = document.getElementById('btnSaveTenantNav');
    const resetBtn = document.getElementById('btnResetTenantNav');
    const previewBody = document.getElementById('tnavPreviewBody');

    const menuUrl = root?.dataset?.menuUrl || '/TenantNavigation/api/menu';
    const prefsUrl = root?.dataset?.prefsUrl || '/TenantNavigation/api/preferences';

    let L = {};
    const sortables = [];
    let moduleSnapshot = new Map();  // moduleCode → { override, isHidden, index }
    let domainSnapshot = new Map();  // domainCode → { override, index }

    const HIDDEN_KEY = '__hidden__';

    const loadL10n = () => {
        try {
            const el = document.getElementById('tenant-navigation-l10n');
            L = el ? JSON.parse(el.textContent) : {};
        } catch (_) { L = {}; }

        // Server-rendered fallbacks: guarantee the column headings / labels are never empty even if the JSON
        // bridge fails to load, so "MENÜ ADI" / "GÖRÜNÜRLÜK" always render under each domain header.
        const ds = root?.dataset || {};
        const fallback = (key, value) => { if (!L[key] && value) L[key] = value; };
        fallback('ColName', ds.lColName);
        fallback('ColVisibility', ds.lColVisibility);
        fallback('VisibleSuffix', ds.lVisibleSuffix);
        fallback('HiddenGroup', ds.lHiddenGroup);
        fallback('RenamePlaceholder', ds.lRenamePlaceholder);
        fallback('ShowHide', ds.lShowHide);
        fallback('NoModules', ds.lNoModules);
    };

    const setText = (id, value) => {
        const el = document.getElementById(id);
        if (el) el.textContent = String(value);
    };

    const escapeHtml = (value) => String(value ?? '')
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');

    const normalizeDomain = (value) => String(value ?? '').replace(/[^a-z0-9]/gi, '').toUpperCase();

    // FIX-MODULE-ICON — sanitize a boxicons class from the menu; fall back to bx-box for empty/invalid values.
    const iconClass = (value) => {
        const v = String(value ?? '').trim();
        return /^bxs?-[a-z0-9-]+$/.test(v) ? v : 'bx-box';
    };

    const unwrap = (json) => (json && (json.data !== undefined ? json.data : json.Data)) ?? null;

    const fetchJson = async (url, options) => {
        const response = await fetch(url, Object.assign({ credentials: 'same-origin' }, options || {}));
        if (response.status === 401) {
            window.location.href = '/account/login';
            const err = new Error('auth');
            err.authHandled = true;
            throw err;
        }
        const json = await response.json().catch(() => null);
        if (!response.ok) {
            const err = new Error('request-failed');
            err.problem = json;
            throw err;
        }
        return unwrap(json);
    };

    const showError = (message) => {
        if (!alertEl) return;
        alertEl.textContent = message || L.ErrorOccurred || 'Error';
        alertEl.classList.remove('d-none');
    };
    const clearError = () => alertEl?.classList.add('d-none');
    const setBusy = (busy) => {
        if (saveBtn) saveBtn.disabled = busy;
        if (resetBtn) resetBtn.disabled = busy;
    };

    // Build domain groups from the (preference-applied) menu + the raw preferences. Modules hidden by preference are
    // absent from the menu, so they are reconstructed into a synthetic "Hidden" group so they can be un-hidden.
    const buildModel = (menu, prefs) => {
        const modulePrefs = (prefs && prefs.modules) || [];
        const domainPrefs = (prefs && prefs.domains) || [];

        const modulePrefByCode = new Map();
        modulePrefs.forEach((p) => { if (p && p.moduleCode) modulePrefByCode.set(String(p.moduleCode).toUpperCase(), p); });
        const domainPrefByKey = new Map();
        domainPrefs.forEach((p) => { if (p && p.domainCode) domainPrefByKey.set(normalizeDomain(p.domainCode), p); });

        const groups = [];
        const byKey = new Map();
        const seen = new Set();

        const ensureGroup = (key, domainCode, display, sort, canEdit) => {
            if (!byKey.has(key)) {
                const dPref = domainPrefByKey.get(normalizeDomain(domainCode));
                const g = {
                    key,
                    domainCode,
                    display,
                    sort,
                    canEdit,
                    overrideName: (dPref && dPref.displayNameOverride) ? dPref.displayNameOverride : '',
                    rows: []
                };
                byKey.set(key, g);
                groups.push(g);
            }
            return byKey.get(key);
        };

        (menu || []).forEach((m) => {
            const code = String(m.moduleCode || '');
            if (!code) return;
            seen.add(code.toUpperCase());
            const pref = modulePrefByCode.get(code.toUpperCase());
            const domainCode = String(m.domain || '');
            const display = (m.domainDisplayName || m.domain || 'Modules').trim() || 'Modules';
            const sort = Number.isFinite(m.domainSortOrder) ? m.domainSortOrder : 0;
            const groupKey = normalizeDomain(domainCode) || display.toUpperCase();
            ensureGroup(groupKey, domainCode, display, sort, true).rows.push({
                moduleCode: code,
                currentName: m.moduleDisplayName || code,
                overrideName: (pref && pref.displayNameOverride) ? pref.displayNameOverride : '',
                isHidden: false,
                icon: iconClass(m.icon) // FIX-MODULE-ICON — real module icon from the menu (bx-box fallback).
            });
        });

        const hiddenModules = modulePrefs.filter((p) =>
            p && p.isHidden && p.moduleCode && !seen.has(String(p.moduleCode).toUpperCase()));
        if (hiddenModules.length > 0) {
            const g = ensureGroup(HIDDEN_KEY, '', L.HiddenGroup || 'Hidden', Number.MAX_SAFE_INTEGER, false);
            hiddenModules.forEach((p) => g.rows.push({
                moduleCode: String(p.moduleCode),
                currentName: p.displayNameOverride || String(p.moduleCode),
                overrideName: p.displayNameOverride || '',
                isHidden: true,
                icon: 'bx-box' // hidden modules aren't in the menu payload → no icon; default until un-hidden + reload.
            }));
        }

        // Order domains by effective DomainSortOrder (Hidden group last via MAX_SAFE_INTEGER). Stable.
        groups.sort((a, b) => a.sort - b.sort);
        return groups;
    };

    const captureSnapshot = (groups) => {
        moduleSnapshot = new Map();
        domainSnapshot = new Map();
        let moduleIndex = 0;
        let domainIndex = 0;
        groups.forEach((g) => {
            if (g.canEdit) {
                domainSnapshot.set(g.domainCode, { override: g.overrideName.trim(), index: domainIndex++ });
            }
            g.rows.forEach((r) => {
                moduleSnapshot.set(r.moduleCode, { override: r.overrideName.trim(), isHidden: r.isHidden, index: moduleIndex++ });
            });
        });
    };

    const rowHtml = (row) => {
        const checked = row.isHidden ? '' : 'checked';
        const hiddenClass = row.isHidden ? ' tnav-row-hidden' : '';
        const eyeClass = row.isHidden ? 'bx-hide' : 'bx-show';
        const placeholder = escapeHtml(row.currentName || L.RenamePlaceholder || '');
        return `
            <li class="tnav-row d-flex align-items-center gap-2 p-3 border-bottom${hiddenClass}"
                data-module-code="${escapeHtml(row.moduleCode)}" data-original-name="${escapeHtml(row.currentName)}"
                data-icon="${escapeHtml(row.icon || 'bx-box')}">
                <i class="tnav-drag-handle bx bx-grid-vertical text-muted" role="button" aria-label="reorder"></i>
                <span class="tnav-icon-badge bg-label-primary"><i class="bx ${escapeHtml(row.icon || 'bx-box')}"></i></span>
                <div class="flex-grow-1">
                    <input type="text" class="tnav-name form-control"
                           value="${escapeHtml(row.overrideName)}" placeholder="${placeholder}" maxlength="120" />
                </div>
                <i class="tnav-eye bx ${eyeClass} text-muted"></i>
                <div class="form-check form-switch mb-0">
                    <input class="tnav-hide form-check-input" type="checkbox" role="switch" ${checked} aria-label="${escapeHtml(L.ShowHide || '')}" />
                </div>
            </li>`;
    };

    const headerHtml = (group) => {
        const handle = group.canEdit
            ? `<i class="tnav-domain-handle bx bx-grid-vertical text-muted" role="button" aria-label="reorder"></i>`
            : `<i class="bx bx-lock-alt text-muted"></i>`;
        const name = group.canEdit
            ? `<input type="text" class="tnav-domain-name form-control fw-semibold"
                      value="${escapeHtml(group.overrideName)}" placeholder="${escapeHtml(group.display)}" maxlength="120" />`
            : `<span class="tnav-domain-static text-uppercase text-heading fw-semibold">${escapeHtml(group.display)}</span>`;
        return `
            <div class="tnav-group-header d-flex align-items-center gap-2 p-3">
                ${handle}
                <span class="tnav-icon-badge bg-label-primary"><i class="bx bxs-folder"></i></span>
                ${name}
                <small class="text-muted tnav-group-count text-nowrap ms-2"></small>
                <span class="tnav-group-chevron text-muted ms-1" role="button" aria-label="collapse"><i class="bx bx-chevron-up"></i></span>
            </div>`;
    };

    const renderGroups = (groups) => {
        groupsHost.innerHTML = '';
        sortables.length = 0;

        groups.forEach((group) => {
            const card = document.createElement('div');
            card.className = 'card mb-4 tnav-group';
            card.dataset.domainCode = group.canEdit ? group.domainCode : '';
            card.innerHTML = `
                <div class="card-body p-0">
                    ${headerHtml(group)}
                    <div class="tnav-group-body">
                        <div class="tnav-col-head d-flex align-items-center text-muted small text-uppercase">
                            <span>${escapeHtml(L.ColName || '')}</span>
                            <span class="ms-auto">${escapeHtml(L.ColVisibility || '')}</span>
                        </div>
                        <ul class="tnav-list list-unstyled mb-0">${group.rows.map(rowHtml).join('')}</ul>
                    </div>
                </div>`;
            groupsHost.appendChild(card);

            const list = card.querySelector('.tnav-list');
            if (window.Sortable && list) {
                sortables.push(window.Sortable.create(list, {
                    handle: '.tnav-drag-handle',
                    animation: 150,
                    ghostClass: 'tnav-row-ghost',
                    onEnd: refresh
                }));
            }
        });

        // Domain-card reordering (drag a domain header handle). Editable cards only.
        if (window.Sortable && groupsHost) {
            sortables.push(window.Sortable.create(groupsHost, {
                handle: '.tnav-domain-handle',
                draggable: '.tnav-group',
                animation: 150,
                ghostClass: 'tnav-row-ghost',
                onEnd: refresh
            }));
        }
    };

    const computeRows = () => {
        const rows = [];
        let index = 0;
        groupsHost.querySelectorAll('.tnav-row').forEach((el) => {
            const code = el.dataset.moduleCode;
            if (!code) return;
            rows.push({
                el,
                code,
                originalName: el.dataset.originalName || code,
                override: (el.querySelector('.tnav-name')?.value || '').trim(),
                isHidden: el.querySelector('.tnav-hide')?.checked === false,
                index: index++
            });
        });
        return rows;
    };

    const renderPreview = () => {
        if (!previewBody) return;
        const groups = [];
        groupsHost.querySelectorAll('.tnav-group').forEach((card) => {
            const nameInput = card.querySelector('.tnav-domain-name');
            const display = nameInput
                ? (nameInput.value.trim() || nameInput.placeholder)
                : (card.querySelector('.tnav-domain-static')?.textContent || '');
            const items = [];
            card.querySelectorAll('.tnav-row').forEach((rowEl) => {
                if (rowEl.querySelector('.tnav-hide')?.checked === false) return; // hidden
                const override = (rowEl.querySelector('.tnav-name')?.value || '').trim();
                const original = rowEl.dataset.originalName || rowEl.dataset.moduleCode;
                items.push({ name: override || original, icon: iconClass(rowEl.dataset.icon) });
            });
            if (items.length > 0) groups.push({ display, items });
        });

        if (groups.length === 0) {
            previewBody.innerHTML = `<div class="tnav-preview-empty">${escapeHtml(L.NoModules || '')}</div>`;
            return;
        }

        previewBody.innerHTML = groups.map((g) => `
            <div class="tnav-preview-group">
                <div class="tnav-preview-group-title">${escapeHtml(g.display)}</div>
                ${g.items.map((it) => `
                    <div class="tnav-preview-item">
                        <i class="bx ${escapeHtml(it.icon)}"></i>
                        <span class="tnav-preview-item-text">${escapeHtml(it.name)}</span>
                    </div>`).join('')}
            </div>`).join('');
    };

    const computeDomainsChanged = () => {
        let changed = 0;
        let index = 0;
        groupsHost.querySelectorAll('.tnav-group').forEach((card) => {
            const code = card.dataset.domainCode || '';
            if (!code) return;
            const override = (card.querySelector('.tnav-domain-name')?.value || '').trim();
            const o = domainSnapshot.get(code);
            if (!o || o.override !== override || o.index !== index) changed += 1;
            index += 1;
        });
        return changed;
    };

    const refresh = () => {
        const rows = computeRows();

        rows.forEach((r) => {
            r.el.classList.toggle('tnav-row-hidden', r.isHidden);
            const eye = r.el.querySelector('.tnav-eye');
            if (eye) {
                eye.classList.toggle('bx-hide', r.isHidden);
                eye.classList.toggle('bx-show', !r.isHidden);
            }
        });

        groupsHost.querySelectorAll('.tnav-group').forEach((card) => {
            const groupRows = card.querySelectorAll('.tnav-row');
            let visibleInGroup = 0;
            groupRows.forEach((rel) => { if (rel.querySelector('.tnav-hide')?.checked !== false) visibleInGroup += 1; });
            const counter = card.querySelector('.tnav-group-count');
            if (counter) counter.textContent = `${visibleInGroup} / ${groupRows.length} ${L.VisibleSuffix || ''}`.trim();
        });

        renderPreview();

        const total = rows.length;
        const visible = rows.filter((r) => !r.isHidden).length;
        const hidden = total - visible;
        const moduleChanged = rows.filter((r) => {
            const o = moduleSnapshot.get(r.code);
            if (!o) return true;
            return o.override !== r.override || o.isHidden !== r.isHidden || o.index !== r.index;
        }).length;
        const changed = moduleChanged + computeDomainsChanged();

        setText('tnavStatTotal', total);
        setText('tnavStatVisible', visible);
        setText('tnavStatHidden', hidden);
        setText('tnavStatChanged', changed);
        setText('tnavPreviewCount', visible);
    };

    const buildPayload = () => {
        const modules = computeRows().map((r) => ({
            moduleCode: r.code,
            sortOrder: r.index,
            isHidden: r.isHidden,
            displayNameOverride: r.override.length ? r.override : null
        }));

        const domains = [];
        let dIndex = 0;
        groupsHost.querySelectorAll('.tnav-group').forEach((card) => {
            const code = card.dataset.domainCode || '';
            if (!code) return; // skip the synthetic Hidden group
            const override = (card.querySelector('.tnav-domain-name')?.value || '').trim();
            domains.push({ domainCode: code, sortOrder: dIndex++, displayNameOverride: override.length ? override : null });
        });

        return { modules, domains };
    };

    const load = async () => {
        clearError();
        loadingEl?.classList.remove('d-none');
        emptyEl?.classList.add('d-none');
        groupsHost.innerHTML = '';
        if (previewBody) previewBody.innerHTML = '';
        try {
            const [menu, prefs] = await Promise.all([fetchJson(menuUrl), fetchJson(prefsUrl)]);
            const groups = buildModel(menu || [], prefs || { modules: [], domains: [] });
            captureSnapshot(groups);

            loadingEl?.classList.add('d-none');
            const total = groups.reduce((n, g) => n + g.rows.length, 0);
            if (total === 0) {
                emptyEl?.classList.remove('d-none');
                refresh();
                return;
            }
            emptyEl?.classList.add('d-none');
            renderGroups(groups);
            refresh();
        } catch (error) {
            if (error.authHandled) return;
            loadingEl?.classList.add('d-none');
            showError(L.ErrorOccurred);
        }
    };

    const putPreferences = async (payload, successMsg) => {
        clearError();
        setBusy(true);
        try {
            await fetchJson(prefsUrl, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            });
            window.showToast?.(successMsg || L.Saved || 'Saved', 'success');
            await load();
        } catch (error) {
            if (error.authHandled) return;
            window.showToast?.(L.ErrorOccurred || 'Error', 'error');
            showError(L.ErrorOccurred);
        } finally {
            setBusy(false);
        }
    };

    const save = () => putPreferences(buildPayload(), L.Saved);

    const resetToDefault = () => {
        if (!window.confirm(L.ResetConfirm || 'Reset menu to default?')) return;
        putPreferences({ modules: [], domains: [] }, L.ResetDone || L.Saved);
    };

    const bindEvents = () => {
        saveBtn?.addEventListener('click', save);
        resetBtn?.addEventListener('click', resetToDefault);

        groupsHost.addEventListener('input', (e) => {
            if (e.target.classList.contains('tnav-name') || e.target.classList.contains('tnav-domain-name')) refresh();
        });
        groupsHost.addEventListener('change', (e) => {
            if (e.target.classList.contains('tnav-hide')) refresh();
        });
        // Collapse/expand a domain card by clicking its chevron only (so the name input / drag handle stay usable).
        groupsHost.addEventListener('click', (e) => {
            if (e.target.closest('.tnav-group-chevron')) {
                e.target.closest('.tnav-group')?.classList.toggle('tnav-collapsed');
            }
        });
    };

    return {
        init: () => {
            loadL10n();
            if (!root || !groupsHost) return;
            bindEvents();
            load();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => TenantNavigationSettings.init());
