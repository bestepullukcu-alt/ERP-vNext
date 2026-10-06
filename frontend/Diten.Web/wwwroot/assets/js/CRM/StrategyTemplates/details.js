/**
 * MOD-0167-FU04 Strategy Template Details — the three lifecycle actions and nothing else.
 * There is no apply/generate button here and there never will be: applying a play to a period is MOD-0155.
 */
(function (window, document) {
    'use strict';
    const endpoint = '/CRM/StrategyTemplates/api';
    const L = window.StrategyTemplatesL10n || window.L10n || {};

    const envelope = async response => {
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw Object.assign(new Error((body.errors || [L.ErrorState]).join(' · ')), { status: response.status });
        return body.data;
    };

    const post = async (url, successKey, redirectTo) => {
        try {
            const data = await envelope(await fetch(url, {
                method: 'POST', credentials: 'same-origin', headers: { Accept: 'application/json' }
            }));
            window.showToast?.(successKey, 'success');
            window.location.href = typeof redirectTo === 'function' ? redirectTo(data) : redirectTo;
        } catch (error) {
            window.showToast?.(error.message || L.ErrorState, 'error');
        }
    };

    document.addEventListener('click', event => {
        const button = event.target.closest('[data-action]');
        if (!button) return;
        const id = button.dataset.id;
        if (!id) return;

        if (button.dataset.action === 'activate') {
            event.preventDefault();
            window.showConfirm?.(L.ActivateStrategyTemplateConfirm,
                () => post(`${endpoint}/templates/${id}/activate`, L.RecordActivated, `/CRM/StrategyTemplates/Details/${id}`),
                { type: 'question', confirmButtonText: L.ActivateStrategyTemplate });
            return;
        }

        if (button.dataset.action === 'archive') {
            event.preventDefault();
            window.showConfirm?.(L.ArchiveStrategyTemplateConfirm,
                () => post(`${endpoint}/templates/${id}/archive`, L.RecordArchived, '/CRM/StrategyTemplates'),
                { type: 'warning', confirmButtonText: L.ArchiveStrategyTemplate });
            return;
        }

        if (button.dataset.action === 'new-version') {
            event.preventDefault();
            window.showConfirm?.(L.NewVersionConfirm,
                () => post(`${endpoint}/templates/${id}/new-version`, L.RecordCreated,
                    created => created ? `/CRM/StrategyTemplates/Edit/${created}` : '/CRM/StrategyTemplates'),
                { type: 'question', confirmButtonText: L.NewVersion });
        }
    });

    // WP-ST-DETAIL-2 — the "Sürüm geçmişi" panel. On the Detay page it fetches the play's lineage (newest-first, from the
    // DETAIL-1 /versions read) and lists each version: vN + a status badge + a date + a "geçerli" marker on the current
    // one. READ-ONLY and independent of the data-action flow above; a failed load shows a short message, never invents a
    // version. Only Bootstrap utility classes are used, so no extra stylesheet is needed on the Detay page.
    const esc = v => String(v ?? '').replace(/[&<>'"]/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[ch]));
    const versionStatus = status => {
        const s = String(status || '').toLowerCase();
        if (s === 'active' || s === 'published') return { label: L.VerStatusActive || s, tone: 'bg-label-success' };
        if (s === 'archived') return { label: L.VerStatusArchived || s, tone: 'bg-label-secondary' };
        return { label: L.VerStatusDraft || s, tone: 'bg-label-secondary' };
    };
    const versionDate = value => {
        if (!value) return '';
        const d = new Date(value);
        return Number.isNaN(d.getTime()) ? '' : d.toISOString().slice(0, 10);
    };

    const loadVersionHistory = async host => {
        const id = host.dataset.id;
        if (!id) { host.textContent = L.ErrorState || ''; return; }
        host.textContent = L.Loading || '';
        try {
            const data = await envelope(await fetch(`${endpoint}/templates/${id}/versions`, {
                credentials: 'same-origin', headers: { Accept: 'application/json' }
            }));
            const versions = data?.versions || data?.Versions || [];
            if (versions.length === 0) { host.textContent = L.EmptyState || ''; return; }
            // WP-ST-DETAIL-4 — a dotted timeline (mockup): each version is "vN · {status}" with a leading dot; the current
            // version carries a "geçerli" marker and a "last updated {date}" line, a frozen (superseded) version says its
            // bindings are immutable and view-only. The endpoint already sorts newest-first (TemplateVersion descending).
            host.innerHTML = versions.map(v => {
                const st = versionStatus(v.templateStatus ?? v.TemplateStatus);
                const date = versionDate(v.activatedAt ?? v.ActivatedAt ?? v.createdAt ?? v.CreatedAt);
                const isCurrent = (v.isCurrent ?? v.IsCurrent) === true;
                const ver = v.templateVersion ?? v.TemplateVersion;
                const sub = isCurrent ? (L.LastUpdatedTpl || '').replace('{date}', date) : (L.VersionFrozenNote || '');
                return `
                <div class="d-flex gap-2 pb-2">
                    <span class="rounded-circle flex-shrink-0 mt-1 ${isCurrent ? 'bg-primary' : 'bg-secondary'}" style="width: 8px; height: 8px;"></span>
                    <div class="flex-grow-1">
                        <div class="d-flex align-items-center gap-1 flex-wrap">
                            <span class="fw-semibold">v${esc(ver)}</span>
                            <span class="badge ${st.tone} rounded-pill">${esc(st.label)}</span>
                            ${isCurrent ? `<span class="badge bg-label-primary rounded-pill">${esc(L.CurrentVersion || '')}</span>` : ''}
                        </div>
                        <div class="text-muted small">${esc(sub)}</div>
                    </div>
                </div>`;
            }).join('');
        } catch (error) {
            host.textContent = error.message || L.ErrorState || '';
        }
    };

    const versionHost = document.getElementById('stVersionHistory');
    if (versionHost) loadVersionHistory(versionHost);

    // ===== WP-ST-DETAIL-3 — resolve reference NAMES from the SAME feeds the editor (form.js) uses; no new backend. Each
    // element carrying data-resolve="<kind>" data-id="<id>" is upgraded from its server-rendered code/GUID fallback to the
    // human name once the feed resolves it; an unresolved ref keeps its fallback (never an invented name). The feeds are
    // tenant-scoped by the proxy. READ-ONLY and independent of the data-action lifecycle flow above. =====
    const readJson = async url => {
        try { return await envelope(await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } })); }
        catch (error) { return null; }
    };

    // The MDM global-product selector caps PageSize at 100 (same as form.js loadAll), so the whole catalogue is pulled in
    // pageSize=100 pages guarded by totalCount and a hard page ceiling.
    const loadAllProducts = async () => {
        const items = [];
        try {
            for (let page = 1; page <= 50; page++) {
                const data = await envelope(await fetch(`${endpoint}/global-products?pageSize=100&pageNumber=${page}`, {
                    credentials: 'same-origin', headers: { Accept: 'application/json' }
                }));
                const rows = data?.items || data?.Items || [];
                rows.forEach(r => items.push(r));
                const total = Number(data?.totalCount ?? data?.TotalCount);
                if (rows.length === 0 || !Number.isFinite(total) || items.length >= total) break;
            }
        } catch (error) { /* graceful: rows stay on their code fallback */ }
        return items;
    };

    const nameMaps = {
        segment: {}, 'frequency-policy': {}, 'global-product': {}, content: {},
        country: {}, 'legal-entity': {}, 'business-unit': {}
    };

    const buildResolvers = async () => {
        const [segRes, polRes, pathRes, journeyRes, prodRows, scopeRes] = await Promise.all([
            readJson(`${endpoint}/segments?includeArchived=true`),
            readJson(`${endpoint}/visit-frequency-policies`),
            readJson(`${endpoint}/knowledge-paths`),
            readJson(`${endpoint}/content-engagement-journeys`),
            loadAllProducts(),
            readJson(`${endpoint}/scope-options`)
        ]);
        (segRes?.items || segRes?.Items || []).forEach(r => { const id = r.segmentId || r.id; if (id) nameMaps.segment[id] = r.segmentName || r.segmentCode; });
        (polRes?.items || polRes?.Items || []).forEach(r => { const id = r.policyId || r.id; if (id) nameMaps['frequency-policy'][id] = r.policyName || r.policyCode; });
        (pathRes?.items || pathRes?.Items || []).forEach(r => { const id = r.pathId || r.id; if (id) nameMaps.content[id] = r.pathName || r.pathCode; });
        (journeyRes?.items || journeyRes?.Items || []).forEach(r => { const id = r.journeyId || r.id; if (id) nameMaps.content[id] = r.journeyName || r.journeyCode; });
        prodRows.forEach(r => { if (r.id) nameMaps['global-product'][r.id] = r.globalProductName || r.canonicalCode; });
        (scopeRes?.countries || []).forEach(o => { if (o.value != null) nameMaps.country[String(o.value)] = o.label; });
        (scopeRes?.legalEntities || []).forEach(o => { if (o.value != null) nameMaps['legal-entity'][String(o.value)] = o.label; });
        (scopeRes?.businessUnits || []).forEach(o => { if (o.value != null) nameMaps['business-unit'][String(o.value)] = o.label; });
    };

    const applyResolvedNames = () => {
        document.querySelectorAll('[data-resolve][data-id]').forEach(elem => {
            const id = elem.dataset.id;
            if (!id) return;
            const name = nameMaps[elem.dataset.resolve]?.[id];
            if (name) elem.textContent = name;
        });
    };

    // WP-ST-DETAIL-3 — the human "BU OYUN NE YAPACAK?" sentence, composed from the resolved scope name + the DetailDto
    // counts (read from #stDetailData). Hidden until it can be built, so nothing is invented.
    const buildDetailSummary = () => {
        const box = document.getElementById('stDetailSummary');
        const dataEl = document.getElementById('stDetailData');
        const tpl = L.DetailSummaryTpl;
        if (!box || !dataEl || !tpl) return;
        let cfg = {};
        try { cfg = JSON.parse(dataEl.textContent || '{}'); } catch (error) { return; }
        const level = cfg.scopeLevel || '';
        let scope = '';
        if (level === 'country') scope = nameMaps.country[String(cfg.scopeCountry || '')] || cfg.scopeCountry || '';
        else if (level === 'legal-entity') scope = nameMaps['legal-entity'][String(cfg.scopeLegalEntity || '')] || '';
        else if (level === 'business-unit') scope = nameMaps['business-unit'][String(cfg.scopeBusinessUnit || '')] || cfg.scopeBusinessUnit || '';
        if (!scope) scope = L['ScopeType_' + level] || L.ScopeTypeTenant || level || '—';
        box.textContent = tpl
            .replace('{scope}', scope)
            .replace('{segments}', String(cfg.segments ?? 0))
            .replace('{products}', String(cfg.products ?? 0))
            .replace('{weight}', String(cfg.weight ?? 0))
            .replace('{contents}', String(cfg.contents ?? 0));
        box.classList.remove('d-none');
    };

    const initDetail = async () => {
        if (!document.querySelector('[data-resolve]') && !document.getElementById('stDetailSummary')) return;
        await buildResolvers();
        applyResolvedNames();
        buildDetailSummary();
    };
    initDetail();
})(window, document);
