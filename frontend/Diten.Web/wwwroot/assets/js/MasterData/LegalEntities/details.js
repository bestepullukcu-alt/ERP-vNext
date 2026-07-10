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
    const fmtShortDate = (iso) => { if (!iso) return '-'; const d = new Date(iso); return Number.isNaN(d.getTime()) ? '-' : d.toLocaleDateString(); };
    const fmtAddress = (raw) => {
        if (!raw) return '-';
        try { const o = typeof raw === 'string' ? JSON.parse(raw) : raw; return [o.line1, o.line2, [o.postalCode, o.city].filter(Boolean).join(' '), o.state, o.country].filter(Boolean).join(', '); }
        catch { return typeof raw === 'string' ? raw : '-'; }
    };

    // Golden-compact preview-field renderer: [label, value, icon] per row. Values are coerced to string first so
    // numeric fields (e.g. `version`) don't throw on .startsWith(). Badge HTML (starts with "<") is passed through.
    const renderSection = (hostId, rows, colClass) => {
        const host = byId(hostId);
        if (!host) return;
        const cc = colClass || 'col-12 col-md-6';
        host.innerHTML = rows.map(([label, value, icon]) => {
            const v = value == null ? '' : String(value);
            const cell = v.startsWith('<') ? v : escapeHtml(v === '' ? '-' : v);
            return `<div class="${cc}"><div class="backbone-preview-field"><i class="bx ${icon || 'bx-info-circle'}"></i>`
                + `<div><div class="backbone-preview-label">${escapeHtml(label)}</div>`
                + `<div class="backbone-preview-value mt-1">${cell}</div></div></div></div>`;
        }).join('');
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
        const host = byId('leDetailLifecycleActions'); // the dropdown <ul>
        const wrap = byId('leDetailActionWrap');       // the dropdown wrapper (hidden when no actions)
        if (!host) return;
        const status = String(d.operationalStatus || d.lifecycleState || '').toUpperCase();
        const name = d.legalName || '';
        const items = [];
        // İŞB — activatable from ANY non-active state: Draft/InReview/Approved (activate), Suspended (resume), Archived (restore).
        if (status && status !== 'ACTIVE') items.push(`<li><a class="dropdown-item text-success" href="javascript:void(0);" data-act="activate"><i class="bx bx-check-circle me-2"></i>${escapeHtml(L.Activate || '')}</a></li>`);
        if (status === 'ACTIVE') items.push(`<li><a class="dropdown-item text-warning" href="javascript:void(0);" data-act="suspend"><i class="bx bx-pause-circle me-2"></i>${escapeHtml(L.Suspend || '')}</a></li>`);
        if (status === 'ACTIVE' || status === 'SUSPENDED') items.push(`<li><a class="dropdown-item text-warning" href="javascript:void(0);" data-act="archive"><i class="bx bx-archive-in me-2"></i>${escapeHtml(L.Archive || '')}</a></li>`);
        host.innerHTML = items.join('');
        if (wrap) wrap.classList.toggle('d-none', items.length === 0);
        host.querySelectorAll('[data-act]').forEach((b) => b.addEventListener('click', () => {
            const a = b.getAttribute('data-act');
            const map = { activate: ['ActivateConfirm', 'RecordActivated'], suspend: ['SuspendConfirm', 'RecordSuspended'], archive: ['ArchiveConfirm', 'RecordArchived'] };
            patchLifecycle(a, map[a][0], map[a][1], name);
        }));
    };

    const render = (d) => {
        const bc = byId('leDetailBreadcrumb');
        if (bc) bc.textContent = d.legalName || d.code || (L.ViewDetails || bc.textContent);
        const editBtn = byId('leDetailEditBtn');
        if (editBtn) editBtn.href = `${wizardUrl}/${encodeURIComponent(id)}`;

        const status = String(d.operationalStatus || d.lifecycleState || '').toUpperCase();
        byId('leDetailOperational').innerHTML = operationalBadge(status);
        byId('leDetailStatutory').innerHTML = statutoryBadge(d.statutoryStatus);
        const refOk = d.referenceable === true || d.referenceable === 'true';
        byId('leDetailReferenceable').innerHTML = `<span class="badge ${refOk ? 'bg-label-success' : 'bg-label-secondary'}">${escapeHtml(L.Referenceable || '')}: ${refOk ? (L.Yes || 'Yes') : (L.No || 'No')}</span>`;
        byId('leDetailLifecycle').innerHTML = `<span class="badge bg-label-secondary">${escapeHtml(L.LifecycleState || '')}: ${escapeHtml(d.lifecycleState || '-')}</span>`;

        // ── Main column: Identity / Statutory & Tax / Addresses & Contacts (2-up fields) ──
        renderSection('leSecIdentity', [
            [L.Code, d.code, 'bx-purchase-tag-alt'], [L.LegalName, d.legalName, 'bx-file'],
            [L.DisplayName, d.displayName, 'bx-rename'], [L.LegalForm, lookupLabel('legalForm', d.legalFormCode), 'bx-buildings']
        ]);
        renderSection('leSecStatutory', [
            [L.RegistrationNumber, d.registrationNumber, 'bx-receipt'], [L.TaxId, d.taxId, 'bx-id-card'],
            [L.VatNumber, d.vatNumber, 'bx-barcode'], [L.Country, lookupLabel('country', d.countryCode), 'bx-map-pin'],
            [L.PlaceOfIncorporation, d.placeOfIncorporation, 'bx-buildings'], [L.StatutoryStatus, statutoryBadge(d.statutoryStatus) || '-', 'bx-check-shield'],
            [L.IncorporationDate, fmtShortDate(d.incorporationDate), 'bx-calendar'], [L.DissolutionDate, fmtShortDate(d.dissolutionDate), 'bx-calendar-x'],
            [L.BaseCurrency, lookupLabel('currency', d.baseCurrencyCode), 'bx-dollar-circle']
        ]);
        renderSection('leSecAddresses', [
            [L.RegisteredAddress, fmtAddress(d.registeredAddressJson), 'bx-map'], [L.CorrespondenceAddress, fmtAddress(d.correspondenceAddressJson), 'bx-envelope'],
            [L.OfficialEmail, d.officialEmail, 'bx-at'], [L.OfficialPhone, d.officialPhone, 'bx-phone'], [L.Website, d.website, 'bx-globe']
        ]);

        // ── Sidebar column: Structure / Finance / System (1-up fields) ──
        renderSection('leSecStructure', [
            [L.OrgRole, lookupLabel('organizationRole', d.organizationRoleCode), 'bx-sitemap'], [L.Parent, parentLabel(d.parentLegalEntityId), 'bx-network-chart'],
            [L.OwnershipPercent, d.ownershipPercent, 'bx-pie-chart-alt-2'], [L.ControlType, lookupLabel('controlType', d.controlTypeCode), 'bx-slider']
        ], 'col-12');
        renderSection('leSecFinance', [
            [L.FiscalYearVariant, d.fiscalYearVariant, 'bx-calendar-check'], [L.AccountingStandard, lookupLabel('accountingStandard', d.accountingStandardCode), 'bx-book'],
            [L.TaxRegime, lookupLabel('taxRegime', d.taxRegimeCode), 'bx-coin-stack']
        ], 'col-12');
        renderSection('leSecSystem', [
            [L.OperationalStatus, operationalBadge(status), 'bx-toggle-left'], [L.Referenceable, refOk ? (L.Yes || 'Yes') : (L.No || 'No'), 'bx-link'],
            [L.Version, d.version, 'bx-git-branch'], [L.CreatedAt, fmtDate(d.createdAt), 'bx-calendar-plus'], [L.UpdatedAt, fmtDate(d.updatedAt), 'bx-calendar-edit']
        ], 'col-12');

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
