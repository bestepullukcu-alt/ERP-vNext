/**
 * Platform Self Effective Access (FE-E, MOD-0018-FU9)
 * Component-based, read-only. Renders the FU14 self-explain DTO as TWO separate observations
 * (permission gate + data scope) — never a combined "Allowed" verdict (SelfAccessView.build, pure +
 * vitest-tested, enforces this). GETs the gateway self-explain endpoint via the server-side proxy.
 *
 * FIX-MYACCESS-UX: module/permission-key are cascading select2 dropdowns sourced from the module
 * catalog (module list → its pages' RequiredPermission ∪ actions' PermissionKey). Error states now
 * surface the backend's real message instead of a generic string.
 */
'use strict';

const SelfAccessPage = (function () {
    let L = window.L10n || {};
    const els = {};

    // Module catalog proxy endpoints (existing routes on ModuleCatalogController).
    const MODULE_LIST_URL = '/Platform/ModuleCatalog/api?page=1&pageSize=200&sort=sortOrder';
    const modulePagesUrl = (code) => `/Platform/ModuleCatalog/api/${encodeURIComponent(code)}/pages`;
    const pageActionsUrl = (pageId) => `/Platform/ModuleCatalog/api/pages/${encodeURIComponent(pageId)}/actions`;

    const cacheEls = () => {
        els.form = document.getElementById('saForm');
        els.key = document.getElementById('saPermissionKey');
        els.module = document.getElementById('saModuleCode');
        els.feature = document.getElementById('saFeatureCode');
        els.alert = document.getElementById('saAlert');
        els.empty = document.getElementById('saEmpty');
        els.result = document.getElementById('saResult');
        els.btn = document.getElementById('btnExplain');
    };

    const showError = (msg) => { if (els.alert) { els.alert.textContent = msg || L.ErrorOccurred || ''; els.alert.classList.remove('d-none'); } };
    const clearError = () => els.alert?.classList.add('d-none');

    const setText = (id, value) => { const el = document.getElementById(id); if (el) el.textContent = (value === null || value === undefined || value === '') ? (L.NotAvailable || '-') : String(value); };
    const yesNoBadge = (id, on) => {
        const el = document.getElementById(id);
        if (!el) return;
        el.textContent = on ? (L.Yes || 'Yes') : (L.No || 'No');
        el.className = 'badge ' + (on ? 'bg-label-success' : 'bg-label-secondary');
    };

    const matchLabel = (match) => {
        switch (match) {
            case 'canonical': return L.MatchCanonical || match;
            case 'legacy-alias': return L.MatchLegacyAlias || match;
            case 'missing': return L.MatchMissing || match;
            case 'bypass-platform-admin': return L.MatchBypassPlatformAdmin || match;
            case 'bypass-partner-admin': return L.MatchBypassPartnerAdmin || match;
            default: return match || (L.NotAvailable || '-');
        }
    };

    const renderList = (id, items) => {
        const el = document.getElementById(id);
        if (!el) return;
        el.innerHTML = '';
        (Array.isArray(items) ? items : []).forEach((t) => {
            const li = document.createElement('li');
            li.textContent = t;
            el.appendChild(li);
        });
    };

    const renderBadges = (id, items, cls) => {
        const el = document.getElementById(id);
        if (!el) return;
        el.innerHTML = '';
        const list = Array.isArray(items) ? items : [];
        if (!list.length) { el.textContent = L.NotAvailable || '-'; return; }
        list.forEach((t) => {
            const span = document.createElement('span');
            span.className = 'badge ' + (cls || 'bg-label-primary');
            span.textContent = t;
            el.appendChild(span);
        });
    };

    const render = (vm) => {
        // Observation 1 — permission gate
        setText('sa-perm-required', vm.permission.requiredPermission);
        yesNoBadge('sa-perm-satisfied', vm.permission.satisfied);
        const matchEl = document.getElementById('sa-perm-match');
        if (matchEl) { matchEl.textContent = matchLabel(vm.permission.match); matchEl.className = 'badge bg-label-primary'; }
        yesNoBadge('sa-perm-alias', vm.permission.matchedViaLegacyAlias);

        // Observation 2 — data scope
        document.getElementById('sa-scope-unavailable')?.classList.toggle('d-none', !vm.scope.unavailable);
        renderBadges('sa-scope-kinds', vm.scope.kinds, 'bg-label-info');
        const countsEl = document.getElementById('sa-scope-counts');
        if (countsEl) {
            const entries = Object.keys(vm.scope.counts || {});
            countsEl.innerHTML = '';
            if (!entries.length) { countsEl.textContent = L.NotAvailable || '-'; }
            else entries.forEach((k) => {
                const span = document.createElement('span');
                span.className = 'badge bg-label-secondary';
                span.textContent = `${k}: ${vm.scope.counts[k]}`;
                countsEl.appendChild(span);
            });
        }
        renderList('sa-scope-notes', vm.scope.notes);

        // Context / meta
        setText('sa-mode', vm.meta.mode);
        setText('sa-actor', vm.meta.actorType);
        setText('sa-tenant', vm.meta.tenantId);
        setText('sa-token-exp', vm.meta.tokenExpiresAtUtc);
        renderList('sa-freshness', vm.meta.freshnessNotes);

        els.empty?.classList.add('d-none');
        els.result?.classList.remove('d-none');
    };

    const unwrap = (json) => {
        if (json?.data?.data !== undefined) return json.data.data;
        return json?.data ?? json?.Data ?? json;
    };

    // Pull the backend's real message out of a failed Response (envelope: errors[] / message / data),
    // falling back to the generic string only when nothing usable is present.
    const extractError = async (res) => {
        let body;
        try { body = await res.json(); } catch { return L.ErrorOccurred || ''; }
        const errs = body?.errors ?? body?.Errors;
        if (Array.isArray(errs) && errs.length) return errs.filter(Boolean).join(' ');
        const msg = body?.message ?? body?.Message ?? body?.detail ?? body?.title
            ?? (typeof body?.data === 'string' ? body.data : null);
        return (msg && String(msg).trim()) || (L.ErrorOccurred || '');
    };

    // Auth-failure bodies carry a redirectUrl (ProxyAuthFailure.PlatformLoginPayload). Honour it so an
    // expired session bounces to login instead of showing a confusing inline error.
    const handleAuthRedirect = async (res) => {
        if (res.status !== 401) return false;
        try {
            const body = await res.clone().json();
            if (body?.redirectUrl) { window.location.href = body.redirectUrl; return true; }
        } catch { /* no body — fall through to inline error */ }
        return false;
    };

    const unwrapRows = (json) => {
        const data = json?.data ?? json?.Data;
        if (Array.isArray(data)) return data;
        if (Array.isArray(data?.items)) return data.items;
        if (Array.isArray(data?.Items)) return data.Items;
        if (Array.isArray(json)) return json;
        return [];
    };

    // select2 helpers ------------------------------------------------------------------------------

    const initSelect2 = () => {
        if (typeof jQuery === 'undefined' || !jQuery.fn.select2) return;
        $('.sa-select2').each(function () {
            const $this = $(this);
            if ($this.hasClass('select2-hidden-accessible')) $this.select2('destroy');
            $this.select2({
                dropdownParent: $(document.body),
                dropdownCssClass: 'dt-inline-filter-dropdown',
                selectionCssClass: 'form-select',
                width: '100%',
                allowClear: false,
                placeholder: $this.data('placeholder') || ''
            });
        });
    };

    const setSelectOptions = (select, options, { disabled } = {}) => {
        if (!select) return;
        const placeholder = select.querySelector('option[value=""]')?.textContent || '';
        select.innerHTML = '';
        select.appendChild(new Option(placeholder, ''));
        options.forEach((opt) => select.appendChild(new Option(opt.text, opt.value)));
        select.value = '';
        if (disabled !== undefined) select.disabled = disabled;
        if (typeof jQuery !== 'undefined') {
            const $sel = jQuery(select);
            if (disabled !== undefined) $sel.prop('disabled', disabled);
            $sel.val('').trigger('change.select2');
        }
    };

    const loadModules = async () => {
        try {
            const res = await fetch(MODULE_LIST_URL, { credentials: 'same-origin' });
            if (await handleAuthRedirect(res)) return;
            if (!res.ok) { showError(await extractError(res)); return; }
            const rows = unwrapRows(await res.json());
            const options = rows
                .map((r) => ({
                    value: r.moduleCode || r.ModuleCode || '',
                    text: r.displayName || r.DisplayName || r.moduleCode || r.ModuleCode || ''
                }))
                .filter((o) => o.value)
                .sort((a, b) => a.text.localeCompare(b.text));
            setSelectOptions(els.module, options);
        } catch (e) {
            console.error('[SelfAccess] Module list load failed.', e);
            showError(L.ErrorOccurred || '');
        }
    };

    // Collect the module's permission keys: pages[].requiredPermission ∪ actions[].permissionKey.
    const loadPermissionKeys = async (moduleCode) => {
        // Reset child while loading; keep disabled until populated.
        setSelectOptions(els.key, [], { disabled: true });
        if (!moduleCode) return;

        try {
            const pagesRes = await fetch(modulePagesUrl(moduleCode), { credentials: 'same-origin' });
            if (await handleAuthRedirect(pagesRes)) return;
            if (!pagesRes.ok) { showError(await extractError(pagesRes)); return; }
            const pages = unwrapRows(await pagesRes.json());

            const keys = new Set();
            pages.forEach((p) => {
                const perm = p.requiredPermission || p.RequiredPermission;
                if (perm) keys.add(perm.trim());
            });

            // Per-page actions (best-effort: a failed page's actions are skipped, not fatal).
            const actionLists = await Promise.all(pages.map(async (p) => {
                const pageId = p.id || p.Id;
                if (!pageId) return [];
                try {
                    const aRes = await fetch(pageActionsUrl(pageId), { credentials: 'same-origin' });
                    if (!aRes.ok) return [];
                    return unwrapRows(await aRes.json());
                } catch { return []; }
            }));
            actionLists.forEach((actions) => actions.forEach((a) => {
                const key = a.permissionKey || a.PermissionKey;
                if (key) keys.add(key.trim());
            }));

            const options = Array.from(keys).sort().map((k) => ({ value: k, text: k }));
            setSelectOptions(els.key, options, { disabled: options.length === 0 });
        } catch (e) {
            console.error('[SelfAccess] Permission key load failed.', e);
            showError(L.ErrorOccurred || '');
        }
    };

    // ----------------------------------------------------------------------------------------------

    const explain = async () => {
        clearError();
        const mod = (els.module?.value || '').trim();
        const key = (els.key?.value || '').trim();
        if (!mod || !key) { showError(L.ValidationFailed || ''); return; }

        const params = new URLSearchParams();
        params.set('permissionKey', key);
        params.set('moduleCode', mod);
        const feat = (els.feature?.value || '').trim();
        if (feat) params.set('featureCode', feat);

        if (els.btn) els.btn.disabled = true;
        try {
            const res = await fetch('/Platform/SelfAccess/api?' + params.toString(), { method: 'GET', credentials: 'same-origin' });
            if (await handleAuthRedirect(res)) return;
            if (!res.ok) { showError(await extractError(res)); return; }
            const dto = unwrap(await res.json());
            const vm = window.SelfAccessView.build(dto);
            if (!vm) { showError(L.ErrorOccurred || ''); return; }
            render(vm);
        } catch (e) {
            console.error(e);
            showError(L.ErrorOccurred || '');
        } finally {
            if (els.btn) els.btn.disabled = false;
        }
    };

    const init = () => {
        cacheEls();
        if (!els.form) return;
        L = window.L10n || {};
        initSelect2();
        loadModules();

        // saModuleCode is a select2 dropdown — its selection fires change via jQuery .trigger('change'),
        // which a native addEventListener('change') does NOT catch. Bind through jQuery so the cascade runs
        // (fall back to native when jQuery is absent — defensive).
        const onModuleChange = () => {
            clearError();
            loadPermissionKeys((els.module?.value || '').trim());
        };
        if (window.jQuery && els.module) {
            window.jQuery(els.module).on('change', onModuleChange);
        } else {
            els.module?.addEventListener('change', onModuleChange);
        }
        els.form.addEventListener('submit', (e) => { e.preventDefault(); explain(); });
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => SelfAccessPage.init());
