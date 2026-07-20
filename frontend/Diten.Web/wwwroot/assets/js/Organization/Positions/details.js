'use strict';

// MOD-0288 Phase 3 — Position details (read view). Loads the entity + lookups to resolve Org Unit / Reports-To
// names, renders the derived Vacant/Occupied badge and the read-only manager chain, formats enum + dates in the
// request culture, and wires archive / delete (which return to the list with a toast).
(function () {
    const page = document.getElementById('p-details-page');
    if (!page) return;

    const endpoint = '/Positions/api';
    const entityId = page.dataset.pId || '';
    let L = {};

    const byId = (id) => document.getElementById(id);
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });
    const fetchJson = (url) => fetch(url, { headers: getAuthHeaders() }).then((r) => r.ok ? r.json() : Promise.reject(r));
    const unwrapList = (payload) => {
        const data = payload?.data ?? payload?.Data ?? [];
        if (Array.isArray(data)) return data;
        return data.items || data.Items || [];
    };

    const loadL10n = () => {
        const node = byId('position-details-l10n');
        if (node) { try { L = JSON.parse(node.textContent || '{}'); } catch (e) { console.error('[Position Details] L10n parse failed.', e); } }
    };

    const setText = (id, value) => { const el = byId(id); if (el) el.textContent = (value == null || value === '') ? '-' : value; };
    const showAlert = (message) => { const el = byId('p-details-alert'); if (el) { el.textContent = message || ''; el.classList.toggle('d-none', !message); } };

    const fmtDate = (iso) => {
        if (!iso) return '-';
        const d = new Date(iso);
        return Number.isNaN(d.getTime()) ? '-' : d.toLocaleDateString(L.Locale || undefined);
    };
    const typeLabel = (t) => ({
        PERMANENT: L.TypePermanent, TEMPORARY: L.TypeTemporary, CONTRACTOR: L.TypeContractor, INTERN: L.TypeIntern
    }[String(t || '').toUpperCase()] || t || '-');
    const statusLabel = (s) => ({
        DRAFT: L.StatusDraft, ACTIVE: L.StatusActive, FROZEN: L.StatusFrozen, CLOSED: L.StatusClosed
    }[String(s || '').toUpperCase()] || s || '-');

    const getAntiForgeryToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const buildMap = (list, code, name) => {
        const map = {};
        (list || []).forEach((x) => {
            const id = x.id || x.Id;
            if (id) map[String(id)] = code(x) ? `${code(x)} — ${name(x) || ''}` : (name(x) || '');
        });
        return map;
    };

    const renderOccupancy = (d) => {
        const el = byId('p-detail-occupancy');
        if (!el) return;
        const vacant = d.isVacant ?? d.IsVacant ?? true;
        const count = d.activeAssignmentCount ?? d.ActiveAssignmentCount ?? 0;
        if (vacant) { el.textContent = L.Vacant || 'Vacant'; el.className = 'badge bg-label-secondary'; }
        else { el.textContent = `${L.Occupied || 'Occupied'} (${count})`; el.className = 'badge bg-label-success'; }
    };

    const renderManagerChain = (chain) => {
        const host = byId('pDetailManagerChain');
        if (!host) return;
        const nodes = (chain || []).filter((n) => (n.positionId || n.PositionId) !== undefined);
        if (!nodes.length) { host.textContent = L.NoManagerChain || '-'; return; }
        // Ordered by depth (self=0 → top). Indent each level.
        const ordered = nodes.slice().sort((a, b) => (a.depth ?? a.Depth ?? 0) - (b.depth ?? b.Depth ?? 0));
        host.innerHTML = ordered.map((n) => {
            const depth = n.depth ?? n.Depth ?? 0;
            const code = n.positionCode || n.PositionCode || '';
            const name = n.positionName || n.PositionName || '';
            const label = code ? `${code} — ${name}` : name;
            return `<div style="padding-left:${depth * 1.25}rem">${depth > 0 ? '<i class="bx bx-subdirectory-right me-1"></i>' : ''}${escapeHtml(label)}</div>`;
        }).join('');
    };

    const statusBadge = (s) => {
        const up = String(s || '').toUpperCase();
        const map = { DRAFT: 'bg-label-secondary', ACTIVE: 'bg-label-success', FROZEN: 'bg-label-warning', CLOSED: 'bg-label-secondary text-muted' };
        return `<span class="badge ${map[up] || 'bg-label-info'}">${escapeHtml(statusLabel(s))}</span>`;
    };
    const archivedBadge = (archived) => archived
        ? `<span class="badge bg-label-warning">${escapeHtml(L.Archived || 'Archived')}</span>`
        : '';

    // Actions (Archive / Delete) render as items inside the header "Actions" dropdown.
    const renderActionDropdown = (archived) => {
        const wrap = byId('p-detail-action-wrap');
        const menu = byId('p-detail-actions');
        if (!menu) return;
        const items = [];
        if (!archived) items.push(`<li><a class="dropdown-item text-warning" href="javascript:void(0);" data-act="archive"><i class="bx bx-archive-in me-2"></i>${escapeHtml(L.Archive || '')}</a></li>`);
        items.push(`<li><a class="dropdown-item text-danger" href="javascript:void(0);" data-act="delete"><i class="bx bx-trash me-2"></i>${escapeHtml(L.Delete || '')}</a></li>`);
        menu.innerHTML = items.join('');
        wrap?.classList.toggle('d-none', items.length === 0);
        menu.querySelectorAll('[data-act]').forEach((a) => a.addEventListener('click', () => {
            const act = a.getAttribute('data-act');
            if (act === 'archive') runAction(`${endpoint}/${encodeURIComponent(entityId)}/archive`, 'POST', L.ArchiveConfirm, L.RecordArchived);
            else if (act === 'delete') runAction(`${endpoint}/${encodeURIComponent(entityId)}`, 'DELETE', L.DeleteConfirm, L.RecordDeleted);
        }));
    };

    const render = (d, maps) => {
        const editLink = byId('p-detail-edit');
        if (editLink) editLink.href = `/Positions/Edit/${encodeURIComponent(entityId)}`;

        const name = d.name || d.Name || d.code || d.Code || '';
        const bc = byId('pDetailBreadcrumb');
        if (bc && name) bc.textContent = name;

        const archived = d.isArchived ?? d.IsArchived ?? false;
        byId('pDetailStatusBadge').innerHTML = statusBadge(d.status || d.Status);
        byId('pDetailArchivedBadge').innerHTML = archivedBadge(!!archived);

        setText('pDetailCode', d.code || d.Code);
        setText('pDetailName', d.name || d.Name);
        setText('pDetailJobTitle', d.jobTitle || d.JobTitle);
        setText('pDetailType', typeLabel(d.positionType || d.PositionType));
        setText('pDetailOrgUnit', maps.orgUnits[String(d.organizationUnitId || d.OrganizationUnitId)] || (d.organizationUnitId || d.OrganizationUnitId) || '-');
        const reportsTo = d.reportsToPositionId || d.ReportsToPositionId;
        setText('pDetailReportsTo', reportsTo ? (maps.positions[String(reportsTo)] || reportsTo) : (L.NoReportsTo || '-'));

        const fte = d.fte ?? d.Fte;
        setText('pDetailFte', fte == null ? '-' : fte);
        setText('pDetailEffectiveFrom', fmtDate(d.effectiveFrom || d.EffectiveFrom));
        setText('pDetailEffectiveTo', fmtDate(d.effectiveTo || d.EffectiveTo));

        setText('pDetailArchived', archived ? (L.Yes || 'Yes') : (L.No || 'No'));
        setText('pDetailCreatedAt', fmtDate(d.createdAt || d.CreatedAt));
        setText('pDetailUpdatedAt', fmtDate(d.updatedAt || d.UpdatedAt));

        renderOccupancy(d);
        renderActionDropdown(!!archived);
    };

    const load = async () => {
        try {
            const [entity, orgUnits, positions, chain] = await Promise.all([
                fetchJson(`${endpoint}/${encodeURIComponent(entityId)}`),
                fetchJson(`${endpoint}/org-units`).then(unwrapList).catch(() => []),
                fetchJson(endpoint).then(unwrapList).catch(() => []),
                fetchJson(`${endpoint}/${encodeURIComponent(entityId)}/manager-chain`).catch(() => null)
            ]);
            const maps = {
                orgUnits: buildMap(orgUnits, (u) => u.code || u.Code, (u) => u.name || u.Name),
                positions: buildMap(positions, (p) => p.code || p.Code, (p) => p.name || p.Name)
            };
            render(entity.data || entity.Data || {}, maps);
            const chainData = chain?.data || chain?.Data || {};
            renderManagerChain(chainData.chain || chainData.Chain || []);
        } catch (error) {
            console.error('[Position Details] Load failed.', error);
            showAlert(L.LoadFailed || 'Could not load.');
        }
    };

    const runAction = (url, method, confirmMsg, toastMsg) => {
        const doAction = async () => {
            try {
                const res = await fetch(url, { method, headers: { 'RequestVerificationToken': getAntiForgeryToken(), ...getAuthHeaders() } });
                if (res.ok) {
                    try { sessionStorage.setItem('p-toast', toastMsg || ''); } catch { /* ignore */ }
                    window.location.href = '/Positions';
                    return;
                }
                showAlert(L.ErrorOccurred || 'An error occurred.');
            } catch (error) {
                console.error('[Position Details] Action failed.', error);
                showAlert(L.ErrorOccurred || 'An error occurred.');
            }
        };
        if (confirmMsg) window.showConfirm?.(confirmMsg, doAction, { type: 'warning' });
        else doAction();
    };

    loadL10n();
    load();
})();
