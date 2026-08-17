/**
 * Platform Self Effective Access (FE-E, MOD-0018-FU9)
 * Component-based, read-only. Renders the FU14 self-explain DTO as TWO separate observations
 * (permission gate + data scope) — never a combined "Allowed" verdict (SelfAccessView.build, pure +
 * vitest-tested, enforces this). GETs the gateway self-explain endpoint via the server-side proxy.
 */
'use strict';

const SelfAccessPage = (function () {
    let L = window.L10n || {};
    const els = {};

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

    const explain = async () => {
        clearError();
        const key = (els.key?.value || '').trim();
        if (!key) { showError(L.ValidationFailed || ''); return; }

        const params = new URLSearchParams();
        params.set('permissionKey', key);
        const mod = (els.module?.value || '').trim();
        const feat = (els.feature?.value || '').trim();
        if (mod) params.set('moduleCode', mod);
        if (feat) params.set('featureCode', feat);

        if (els.btn) els.btn.disabled = true;
        try {
            const res = await fetch('/Platform/SelfAccess/api?' + params.toString(), { method: 'GET', credentials: 'same-origin' });
            if (!res.ok) { showError(L.ErrorOccurred || ''); return; }
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
        els.form.addEventListener('submit', (e) => { e.preventDefault(); explain(); });
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => SelfAccessPage.init());
