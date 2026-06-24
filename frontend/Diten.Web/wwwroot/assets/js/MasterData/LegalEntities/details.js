'use strict';

// MOD-0220 — Legal Entity full details page (Görev 6). Loads the entity by id, resolves lookup labels via
// the Diten.Web proxy, renders all 8 sections + the system block, status badges, completeness, the
// referenceable flag, and manual lifecycle action buttons (Activate / Suspend / Archive by status).
(function () {
    const page = document.getElementById('le-details-page');
    if (!page) return;

    const endpoint = '/LegalEntities/api';
    const wizardUrl = '/LegalEntities/Wizard';
    const id = page.dataset.leId || '';
    let L = {};
    const lookupMaps = { legalForm: {}, organizationRole: {}, controlType: {}, accountingStandard: {}, taxRegime: {}, country: {}, currency: {} };
    const entityMap = {};

    const byId = (x) => document.getElementById(x);
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });

    const loadL10n = () => {
        const node = byId('legal-entities-details-l10n');
        if (!node) return;
        try { const raw = JSON.parse(node.textContent || '{}'); Object.keys(raw).forEach((k) => { L[k] = raw[k]; }); }
        catch (e) { console.error('[LE Details] L10n parse failed.', e); }
    };

    const unwrapList = (payload) => {
        const data = payload?.data ?? payload?.Data ?? [];
        if (Array.isArray(data)) return data;
        return data.items || data.Items || [];
    };

    const operationalBadge = (s) => {
        s = String(s || '').toUpperCase();
        const map = {
            DRAFT: ['bg-label-secondary', L.StatusDraft], INREVIEW: ['bg-label-info', L.StatusInReview],
            APPROVED: ['bg-label-info', L.StatusApproved], ACTIVE: ['bg-label-success', L.StatusActive],
            SUSPENDED: ['bg-label-warning', L.StatusSuspended], ARCHIVED: ['bg-label-secondary text-muted', L.StatusArchived]
        };
        const m = map[s] || ['bg-label-info', s];
        return `<span class="badge ${m[0]}">${escapeHtml(m[1] || s)}</span>`;
    };
    const statutoryBadge = (s) => {
        s = String(s || '').toUpperCase();
        const map = { REGISTERED: ['bg-label-success', L.StatusRegistered], PENDING: ['bg-label-warning', L.StatusPending], SUSPENDED: ['bg-label-warning', L.StatusSuspended], DISSOLVED: ['bg-label-secondary text-muted', L.StatusDissolved] };
        const m = map[s]; return m ? `<span class="badge ${m[0]}">${escapeHtml(m[1])}</span>` : '';
    };
    const approvalBadge = (s) => {
        s = String(s || '').toUpperCase();
        const map = { DRAFT: ['bg-label-secondary', L.StatusDraft], SUBMITTED: ['bg-label-info', L.StatusSubmitted], APPROVED: ['bg-label-success', L.StatusApproved], REJECTED: ['bg-label-danger', L.StatusRejected] };
        const m = map[s]; return m ? `<span class="badge ${m[0]}">${escapeHtml(m[1])}</span>` : '';
    };
    const evidenceBadge = (s) => {
        s = String(s || '').toUpperCase();
        const map = { NOTSTARTED: ['bg-label-secondary', L.EvidenceNotStarted], COMPLETE: ['bg-label-info', L.EvidenceComplete], VERIFIED: ['bg-label-success', L.EvidenceVerified] };
        const m = map[s]; return m ? `<span class="badge ${m[0]}">${escapeHtml(m[1])}</span>` : '';
    };

    const lookupLabel = (mapKey, code) => (code ? (lookupMaps[mapKey][String(code)] || code) : '-');
    const parentLabel = (pid) => {
        if (!pid) return '-';
        const p = entityMap[String(pid)];
        return p ? (p.code ? `${p.code} — ${p.name}` : (p.name || pid)) : pid;
    };

    const fetchLookup = (mapKey, url) => fetch(url, { headers: getAuthHeaders() })
        .then((r) => r.ok ? r.json() : Promise.reject(r))
        .then((payload) => { unwrapList(payload).forEach((it) => { const code = it.code ?? it.Code ?? it.value ?? it.Value; if (code != null) lookupMaps[mapKey][String(code)] = it.name ?? it.Name ?? code; }); })
        .catch(() => {});

    const fetchEntities = () => fetch(endpoint, { headers: getAuthHeaders() })
        .then((r) => r.ok ? r.json() : Promise.reject(r))
        .then((payload) => { unwrapList(payload).forEach((e) => { const eid = e.legalEntityId || e.LegalEntityId || e.id; if (eid) entityMap[String(eid)] = { name: e.legalName || e.LegalName || '', code: e.code || e.Code || '' }; }); })
        .catch(() => {});

    const loadReference = () => Promise.all([
        fetchLookup('legalForm', `${endpoint}/lookups/legal-form`),
        fetchLookup('organizationRole', `${endpoint}/lookups/organization-role`),
        fetchLookup('controlType', `${endpoint}/lookups/control-type`),
        fetchLookup('accountingStandard', `${endpoint}/lookups/accounting-standard`),
        fetchLookup('taxRegime', `${endpoint}/lookups/tax-regime`),
        fetchLookup('country', `${endpoint}/platform-lookups/countries`),
        fetchLookup('currency', `${endpoint}/platform-lookups/currencies`),
        fetchEntities()
    ]);

    const fmtDate = (iso) => { if (!iso) return '-'; const d = new Date(iso); return Number.isNaN(d.getTime()) ? '-' : d.toLocaleString(); };
    const fmtAddress = (raw) => {
        if (!raw) return '-';
        try { const o = typeof raw === 'string' ? JSON.parse(raw) : raw; return [o.line1, o.line2, [o.postalCode, o.city].filter(Boolean).join(' '), o.state, o.country].filter(Boolean).join(', '); }
        catch { return typeof raw === 'string' ? raw : '-'; }
    };

    const renderSection = (hostId, rows) => {
        const host = byId(hostId);
        if (!host) return;
        host.innerHTML = rows.map(([label, value]) =>
            `<dt class="col-5 text-muted">${escapeHtml(label)}</dt><dd class="col-7">${value && value.startsWith('<') ? value : escapeHtml(value || '-')}</dd>`
        ).join('');
    };

    const patchLifecycle = (action, confirmKey, toastKey, name) => {
        const typeMap = { activate: 'primary', suspend: 'warning', archive: 'warning' };
        const btnMap = { activate: L.Activate, suspend: L.Suspend, archive: L.Archive };
        window.showConfirm?.(L[confirmKey] || L.AreYouSure, async () => {
            try {
                const res = await fetch(`${endpoint}/${encodeURIComponent(id)}/${action}`, { method: 'PATCH', headers: getAuthHeaders() });
                if (!res.ok) throw new Error('failed');
                window.showToast?.(L[toastKey] || '', 'success');
                load();
            } catch (e) { console.error(e); window.showToast?.(L.ErrorOccurred || '', 'error'); }
        }, { entityName: name, type: typeMap[action] || 'primary', confirmButtonText: btnMap[action] });
    };

    const renderLifecycleActions = (d) => {
        const host = byId('leDetailLifecycleActions');
        if (!host) return;
        const status = String(d.operationalStatus || d.lifecycleState || '').toUpperCase();
        const name = d.legalName || '';
        const btns = [];
        if (status === 'DRAFT' || status === 'INREVIEW' || status === 'APPROVED') btns.push(`<button type="button" class="btn btn-sm btn-success" data-act="activate"><i class="bx bx-check-circle me-1"></i>${escapeHtml(L.Activate || '')}</button>`);
        if (status === 'ACTIVE') btns.push(`<button type="button" class="btn btn-sm btn-warning" data-act="suspend"><i class="bx bx-pause-circle me-1"></i>${escapeHtml(L.Suspend || '')}</button>`);
        if (status === 'ACTIVE' || status === 'SUSPENDED') btns.push(`<button type="button" class="btn btn-sm btn-label-warning" data-act="archive"><i class="bx bx-archive-in me-1"></i>${escapeHtml(L.Archive || '')}</button>`);
        host.innerHTML = btns.length ? btns.join('') : '<span class="text-muted small">—</span>';
        host.querySelectorAll('[data-act]').forEach((b) => b.addEventListener('click', () => {
            const a = b.getAttribute('data-act');
            const map = { activate: ['ActivateConfirm', 'RecordActivated'], suspend: ['SuspendConfirm', 'RecordSuspended'], archive: ['ArchiveConfirm', 'RecordArchived'] };
            patchLifecycle(a, map[a][0], map[a][1], name);
        }));
    };

    const render = (d) => {
        byId('leDetailName').textContent = d.legalName || '-';
        byId('leDetailCode').textContent = d.code || '-';
        const editBtn = byId('leDetailEditBtn');
        if (editBtn) editBtn.href = `${wizardUrl}/${encodeURIComponent(id)}`;

        const status = String(d.operationalStatus || d.lifecycleState || '').toUpperCase();
        byId('leDetailOperational').innerHTML = operationalBadge(status);
        byId('leDetailStatutory').innerHTML = statutoryBadge(d.statutoryStatus);
        byId('leDetailApproval').innerHTML = approvalBadge(d.approvalStatus);
        byId('leDetailEvidence').innerHTML = evidenceBadge(d.evidenceStatus);
        const refOk = d.referenceable === true || d.referenceable === 'true';
        byId('leDetailReferenceable').innerHTML = `<span class="badge ${refOk ? 'bg-label-success' : 'bg-label-secondary'}">${escapeHtml(L.Referenceable || '')}: ${refOk ? (L.Yes || 'Yes') : (L.No || 'No')}</span>`;
        byId('leDetailLifecycle').innerHTML = `<span class="badge bg-label-secondary">${escapeHtml(L.LifecycleState || '')}: ${escapeHtml(d.lifecycleState || '-')}</span>`;

        const score = Math.max(0, Math.min(100, Number(d.completenessScore ?? 0)));
        byId('leDetailCompletenessLabel').textContent = `${score}%`;
        const bar = byId('leDetailCompletenessBar');
        if (bar) { bar.style.width = `${score}%`; bar.className = `progress-bar ${score >= 100 ? 'bg-success' : score >= 50 ? 'bg-info' : 'bg-warning'}`; }

        renderSection('leSecIdentity', [
            [L.Code, d.code], [L.LegalName, d.legalName], [L.DisplayName, d.displayName],
            [L.LegalForm, lookupLabel('legalForm', d.legalFormCode)], [L.OrgRole, lookupLabel('organizationRole', d.organizationRoleCode)]
        ]);
        renderSection('leSecStatutory', [
            [L.RegistrationNumber, d.registrationNumber], [L.TaxId, d.taxId],
            [L.Country, lookupLabel('country', d.countryCode)], [L.StatutoryStatus, statutoryBadge(d.statutoryStatus) || '-']
        ]);
        renderSection('leSecStructure', [
            [L.Parent, parentLabel(d.parentLegalEntityId)], [L.OwnershipPercent, d.ownershipPercent != null ? `${d.ownershipPercent}%` : '-'],
            [L.ControlType, lookupLabel('controlType', d.controlTypeCode)]
        ]);
        renderSection('leSecFinance', [
            [L.FiscalYearVariant, d.fiscalYearVariant], [L.AccountingStandard, lookupLabel('accountingStandard', d.accountingStandardCode)],
            [L.TaxRegime, lookupLabel('taxRegime', d.taxRegimeCode)], [L.BaseCurrency, lookupLabel('currency', d.baseCurrencyCode)]
        ]);
        renderSection('leSecAddresses', [
            [L.RegisteredAddress, fmtAddress(d.registeredAddressJson)], [L.CorrespondenceAddress, fmtAddress(d.correspondenceAddressJson)],
            [L.OfficialEmail, d.officialEmail], [L.OfficialPhone, d.officialPhone], [L.Website, d.website]
        ]);
        renderSection('leSecGovernance', [
            [L.ApprovalStatus, approvalBadge(d.approvalStatus) || '-'], [L.ReviewDue, fmtDate(d.reviewDueUtc)],
            [L.SourceSystem, d.sourceSystem], [L.LegacyCode, d.legacyCode]
        ]);
        renderSection('leSecEvidence', [
            [L.EvidenceStatus, evidenceBadge(d.evidenceStatus) || '-'], [L.CompletenessScore, `${score}%`]
        ]);
        renderSection('leSecSystem', [
            [L.Version, d.version], [L.CreatedAt, fmtDate(d.createdAt)], [L.UpdatedAt, fmtDate(d.updatedAt)]
        ]);

        renderLifecycleActions(d);

        byId('le-details-loading')?.classList.add('d-none');
        byId('le-details-content')?.classList.remove('d-none');
    };

    const load = async () => {
        try {
            const res = await fetch(`${endpoint}/${encodeURIComponent(id)}`, { headers: getAuthHeaders() });
            if (!res.ok) throw new Error('load failed');
            const payload = await res.json();
            render(payload.data || payload.Data || {});
        } catch (error) {
            console.error('[LE Details] Load failed.', error);
            const alertEl = byId('le-details-alert');
            if (alertEl) { alertEl.textContent = L.LoadFailed || 'Could not load.'; alertEl.classList.remove('d-none'); }
            byId('le-details-loading')?.classList.add('d-none');
        }
    };

    const init = async () => {
        loadL10n();
        await loadReference();
        await load();
    };

    init();
})();
