'use strict';

// MOD-0288 Phase 2 — Org Unit details (read view). Loads the entity + the same lookups the form uses to resolve
// Legal Entity / Parent / Manager names, renders enum + dates in the request culture, and wires the lifecycle
// actions (archive / delete) which return to the list with a toast.
(function () {
    const page = document.getElementById('ou-details-page');
    if (!page) return;

    const endpoint = '/OrganizationUnits/api';
    const entityId = page.dataset.ouId || '';
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
        const node = byId('org-unit-details-l10n');
        if (node) { try { L = JSON.parse(node.textContent || '{}'); } catch (e) { console.error('[OU Details] L10n parse failed.', e); } }
    };

    const setText = (id, value) => { const el = byId(id); if (el) el.textContent = (value == null || value === '') ? '-' : value; };

    const showAlert = (message) => {
        const el = byId('ou-details-alert');
        if (!el) return;
        el.textContent = message || '';
        el.classList.toggle('d-none', !message);
    };

    const fmtDate = (iso) => {
        if (!iso) return '-';
        const d = new Date(iso);
        return Number.isNaN(d.getTime()) ? '-' : d.toLocaleDateString(L.Locale || undefined);
    };

    const typeLabel = (t) => ({
        DEPARTMENT: L.TypeDepartment, DIVISION: L.TypeDivision, BRANCH: L.TypeBranch, TEAM: L.TypeTeam, HQ: L.TypeHQ
    }[String(t || '').toUpperCase()] || t || '-');

    const statusLabel = (s) => ({
        ACTIVE: L.StatusActive, INACTIVE: L.StatusInactive
    }[String(s || '').toUpperCase()] || s || '-');

    const getAntiForgeryToken = () =>
        document.querySelector('#ou-details-page input[name="__RequestVerificationToken"]')?.value
        || document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const statusBadge = (s) => {
        const cls = String(s || '').toUpperCase() === 'ACTIVE' ? 'bg-label-success' : 'bg-label-secondary';
        return `<span class="badge ${cls}">${escapeHtml(statusLabel(s))}</span>`;
    };
    const archivedBadge = (archived) => archived
        ? `<span class="badge bg-label-warning">${escapeHtml(L.Archived || 'Archived')}</span>`
        : '';

    // Actions (Archive / Delete) render as items inside the header "Actions" dropdown.
    const renderActionDropdown = (archived) => {
        const wrap = byId('ou-detail-action-wrap');
        const menu = byId('ou-detail-actions');
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
        const editLink = byId('ou-detail-edit');
        if (editLink) editLink.href = `/OrganizationUnits/Edit/${encodeURIComponent(entityId)}`;

        const name = d.name || d.Name || d.code || d.Code || '';
        const bc = byId('ouDetailBreadcrumb');
        if (bc && name) bc.textContent = name;

        const status = d.status || d.Status;
        const archived = d.isArchived ?? d.IsArchived ?? false;
        byId('ouDetailStatusBadge').innerHTML = statusBadge(status);
        byId('ouDetailArchivedBadge').innerHTML = archivedBadge(!!archived);

        setText('ouDetailCode', d.code || d.Code);
        setText('ouDetailName', d.name || d.Name);
        setText('ouDetailType', typeLabel(d.orgUnitType || d.OrgUnitType));
        setText('ouDetailLegalEntity', maps.legalEntities[String(d.legalEntityId || d.LegalEntityId)] || (d.legalEntityId || d.LegalEntityId) || '-');
        const parentId = d.parentOrganizationUnitId || d.ParentOrganizationUnitId;
        setText('ouDetailParent', parentId ? (maps.orgUnits[String(parentId)] || parentId) : (L.NoParent || '-'));

        const managerId = d.managerPositionId || d.ManagerPositionId;
        setText('ouDetailManager', managerId ? (maps.positions[String(managerId)] || managerId) : (L.NoManager || '-'));
        setText('ouDetailEffectiveFrom', fmtDate(d.effectiveFrom || d.EffectiveFrom));
        setText('ouDetailEffectiveTo', fmtDate(d.effectiveTo || d.EffectiveTo));
        setText('ouDetailDescription', d.description || d.Description);

        setText('ouDetailArchived', archived ? (L.Yes || 'Yes') : (L.No || 'No'));
        setText('ouDetailCreatedAt', fmtDate(d.createdAt || d.CreatedAt));
        setText('ouDetailUpdatedAt', fmtDate(d.updatedAt || d.UpdatedAt));

        renderActionDropdown(!!archived);
    };

    const buildMap = (list, idKeys, code, name) => {
        const map = {};
        (list || []).forEach((x) => {
            const id = idKeys.map((k) => x[k]).find(Boolean);
            if (id) map[String(id)] = code(x) ? `${code(x)} — ${name(x) || ''}` : (name(x) || '');
        });
        return map;
    };

    const load = async () => {
        try {
            const [entity, legalEntities, orgUnits, positions] = await Promise.all([
                fetchJson(`${endpoint}/${encodeURIComponent(entityId)}`),
                fetchJson(`${endpoint}/legal-entities`).then(unwrapList).catch(() => []),
                fetchJson(endpoint).then(unwrapList).catch(() => []),
                fetchJson(`${endpoint}/positions`).then(unwrapList).catch(() => [])
            ]);
            const maps = {
                legalEntities: buildMap(legalEntities, ['legalEntityId', 'LegalEntityId', 'id', 'Id'], (e) => e.code || e.Code, (e) => e.displayName || e.DisplayName || e.legalName || e.LegalName),
                orgUnits: buildMap(orgUnits, ['id', 'Id'], (u) => u.code || u.Code, (u) => u.name || u.Name),
                positions: buildMap(positions, ['id', 'Id'], (p) => p.code || p.Code, (p) => p.name || p.Name)
            };
            render(entity.data || entity.Data || {}, maps);
        } catch (error) {
            console.error('[OU Details] Load failed.', error);
            showAlert(L.LoadFailed || 'Could not load.');
        }
    };

    const runAction = (url, method, confirmMsg, toastMsg) => {
        const doAction = async () => {
            try {
                const res = await fetch(url, { method, headers: { 'RequestVerificationToken': getAntiForgeryToken(), ...getAuthHeaders() } });
                if (res.ok) {
                    try { sessionStorage.setItem('ou-toast', toastMsg || ''); } catch { /* ignore */ }
                    window.location.href = '/OrganizationUnits';
                    return;
                }
                showAlert(L.ErrorOccurred || 'An error occurred.');
            } catch (error) {
                console.error('[OU Details] Action failed.', error);
                showAlert(L.ErrorOccurred || 'An error occurred.');
            }
        };
        if (confirmMsg) window.showConfirm?.(confirmMsg, doAction, { type: 'warning' });
        else doAction();
    };

    loadL10n();
    load();
})();
