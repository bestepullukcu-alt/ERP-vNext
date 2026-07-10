'use strict';

// MOD-0288 Phase 4 — Position Assignment details (read view). No backend GetById, so the record is resolved from
// the list; Position / User names come from the same lookups the form uses. Shows the derived Planned/Active/Ended
// status badge (server-computed) and wires delete (returns to the list with a toast).
(function () {
    const page = document.getElementById('a-details-page');
    if (!page) return;

    const endpoint = '/PositionAssignments/api';
    const entityId = page.dataset.aId || '';
    let L = {};

    const byId = (id) => document.getElementById(id);
    const escapeHtml = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const getAuthHeaders = () => ({ 'X-Requested-With': 'XMLHttpRequest' });

    const unwrapList = (payload) => {
        if (Array.isArray(payload)) return payload;
        const data = payload?.data ?? payload?.Data;
        if (Array.isArray(data)) return data;
        return payload?.items || payload?.Items || data?.items || data?.Items || [];
    };
    const fetchList = (url) => fetch(url, { headers: getAuthHeaders() }).then((r) => r.ok ? r.json() : Promise.reject(r)).then(unwrapList).catch(() => []);

    const loadL10n = () => {
        const node = byId('assignment-details-l10n');
        if (node) { try { L = JSON.parse(node.textContent || '{}'); } catch (e) { console.error('[Assignment Details] L10n parse failed.', e); } }
    };

    const setText = (id, value) => { const el = byId(id); if (el) el.textContent = (value == null || value === '') ? '-' : value; };
    const showAlert = (message) => { const el = byId('a-details-alert'); if (el) { el.textContent = message || ''; el.classList.toggle('d-none', !message); } };

    const fmtDate = (iso) => {
        if (!iso) return '-';
        const d = new Date(iso);
        return Number.isNaN(d.getTime()) ? '-' : d.toLocaleDateString(L.Locale || undefined);
    };
    const typeLabel = (t) => ({
        PRIMARY: L.TypePrimary, SECONDARY: L.TypeSecondary, ACTING: L.TypeActing, DELEGATED: L.TypeDelegated
    }[String(t || '').toUpperCase()] || t || '-');
    const reasonLabel = (r) => ({
        HIRE: L.ReasonHire, TRANSFER: L.ReasonTransfer, PROMOTION: L.ReasonPromotion, BACKFILL: L.ReasonBackfill
    }[String(r || '').toUpperCase()] || r || '-');

    const getAntiForgeryToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const buildMap = (list, code, name) => {
        const map = {};
        (list || []).forEach((x) => {
            const id = x.id || x.Id;
            if (id) map[String(id)] = code(x) ? `${code(x)} — ${name(x) || ''}` : (name(x) || '');
        });
        return map;
    };
    const userMap = (list) => {
        const map = {};
        (list || []).forEach((u) => {
            const id = u.id || u.Id || u.userId || u.UserId;
            const name = u.displayName || u.DisplayName || u.fullName || u.FullName
                || [u.firstName || u.FirstName, u.lastName || u.LastName].filter(Boolean).join(' ')
                || u.userName || u.UserName || '';
            const email = u.email || u.Email || '';
            if (id) map[String(id)] = name && email ? `${name} — ${email}` : (name || email || id);
        });
        return map;
    };

    const renderStatus = (status) => {
        const el = byId('a-detail-status');
        if (!el) return;
        const s = String(status || '').toUpperCase();
        const label = { PLANNED: L.StatusPlanned, ACTIVE: L.StatusActive, ENDED: L.StatusEnded }[s] || status || '';
        el.textContent = label;
        el.className = 'badge ' + (s === 'ACTIVE' ? 'bg-label-success' : s === 'PLANNED' ? 'bg-label-info' : 'bg-label-secondary');
    };

    const cancelledBadge = (cancelled) => cancelled
        ? `<span class="badge bg-label-danger">${escapeHtml(L.IsCancelled || 'Cancelled')}</span>`
        : '';

    // Only action for assignments is Delete (no archive endpoint); it lives in the header "Actions" dropdown.
    const renderActionDropdown = () => {
        const wrap = byId('a-detail-action-wrap');
        const menu = byId('a-detail-actions');
        if (!menu) return;
        menu.innerHTML = `<li><a class="dropdown-item text-danger" href="javascript:void(0);" data-act="delete"><i class="bx bx-trash me-2"></i>${escapeHtml(L.Delete || '')}</a></li>`;
        wrap?.classList.remove('d-none');
        menu.querySelector('[data-act="delete"]')?.addEventListener('click', () => del());
    };

    const render = (d, maps) => {
        const editLink = byId('a-detail-edit');
        if (editLink) editLink.href = `/PositionAssignments/Edit/${encodeURIComponent(entityId)}`;

        const positionLabel = maps.positions[String(d.positionId || d.PositionId)] || (d.positionId || d.PositionId) || '';
        const bc = byId('aDetailBreadcrumb');
        if (bc && positionLabel) bc.textContent = positionLabel;

        setText('aDetailPosition', positionLabel || '-');
        setText('aDetailUser', maps.users[String(d.userId || d.UserId)] || (d.userId || d.UserId) || '-');
        setText('aDetailType', typeLabel(d.assignmentType || d.AssignmentType));
        setText('aDetailEffectiveFrom', fmtDate(d.effectiveFrom || d.EffectiveFrom));
        setText('aDetailEffectiveTo', fmtDate(d.effectiveTo || d.EffectiveTo));

        const alloc = d.allocationPercent ?? d.AllocationPercent;
        setText('aDetailAllocation', alloc == null ? '-' : `${alloc}%`);
        setText('aDetailReason', reasonLabel(d.reason || d.Reason));
        const cancelled = d.isCancelled ?? d.IsCancelled ?? false;
        setText('aDetailCancelled', cancelled ? (L.Yes || 'Yes') : (L.No || 'No'));
        const cb = byId('aDetailCancelledBadge');
        if (cb) cb.innerHTML = cancelledBadge(!!cancelled);
        setText('aDetailNotes', d.notes || d.Notes);

        setText('aDetailCreatedAt', fmtDate(d.createdAt || d.CreatedAt));
        setText('aDetailUpdatedAt', fmtDate(d.updatedAt || d.UpdatedAt));

        renderStatus(d.derivedStatus || d.DerivedStatus);
        renderActionDropdown();
    };

    const load = async () => {
        try {
            const [all, positions, users] = await Promise.all([
                fetchList(endpoint),
                fetchList(`${endpoint}/positions`),
                fetchList(`${endpoint}/users`)
            ]);
            const match = (all || []).find((x) => String(x.id || x.Id) === String(entityId));
            if (!match) { showAlert(L.LoadFailed || 'Could not load.'); return; }
            const maps = {
                positions: buildMap(positions, (p) => p.code || p.Code, (p) => p.name || p.Name),
                users: userMap(users)
            };
            render(match, maps);
        } catch (error) {
            console.error('[Assignment Details] Load failed.', error);
            showAlert(L.LoadFailed || 'Could not load.');
        }
    };

    const del = () => {
        const doDelete = async () => {
            try {
                const res = await fetch(`${endpoint}/${encodeURIComponent(entityId)}`, { method: 'DELETE', headers: { 'RequestVerificationToken': getAntiForgeryToken(), ...getAuthHeaders() } });
                if (res.ok) {
                    try { sessionStorage.setItem('a-toast', L.RecordDeleted || ''); } catch { /* ignore */ }
                    window.location.href = '/PositionAssignments';
                    return;
                }
                showAlert(L.ErrorOccurred || 'An error occurred.');
            } catch (error) {
                console.error('[Assignment Details] Delete failed.', error);
                showAlert(L.ErrorOccurred || 'An error occurred.');
            }
        };
        if (L.DeleteConfirm) window.showConfirm?.(L.DeleteConfirm, doDelete, { type: 'warning' });
        else doDelete();
    };

    loadL10n();
    load();
})();
