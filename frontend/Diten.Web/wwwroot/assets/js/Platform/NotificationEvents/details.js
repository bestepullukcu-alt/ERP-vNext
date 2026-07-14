/**
 * Notification Events - Details page (MOD-0027-FU03).
 * Read-only event contract view + conditional archive. Data comes from the real /Platform/NotificationEvents/api proxy.
 */
'use strict';

(function () {
    const apiBase = '/Platform/NotificationEvents/api';
    const root = document.getElementById('eventDetailsRoot');
    if (!root) return;

    const eventCode = root.dataset.eventCode || '';
    const L = () => window.L10n || {};

    const unwrap = (payload) => payload?.data ?? payload?.Data ?? null;
    const errorsOf = (payload) => payload?.errors || payload?.Errors || [];
    const setText = (id, value) => {
        const el = document.getElementById(id);
        if (el) el.innerText = (value === null || value === undefined || value === '') ? '-' : String(value);
    };
    const showError = (message) => {
        const el = document.getElementById('detailsError');
        if (!el) return;
        el.textContent = message || L().ErrorOccurred || '';
        el.classList.remove('d-none');
    };

    const renderVars = (id, vars) => {
        const tbody = document.getElementById(id);
        if (!tbody) return;
        tbody.innerHTML = '';
        if (!vars || !vars.length) {
            const tr = document.createElement('tr');
            tr.innerHTML = `<td colspan="3" class="text-muted small">${L().NoVariables || '-'}</td>`;
            tbody.appendChild(tr);
            return;
        }
        vars.forEach((v) => {
            const tr = document.createElement('tr');
            const req = v.isRequired ? (L().Yes || 'Yes') : (L().No || 'No');
            tr.innerHTML = `<td class="font-monospace">${v.name}</td><td>${v.type}</td><td>${req}</td>`;
            tbody.appendChild(tr);
        });
    };

    const statusLabel = (s) => ({
        Draft: L().StatusDraft, Active: L().StatusActive, Deprecated: L().StatusDeprecated, Archived: L().StatusArchived
    }[s] || s);

    const archiveEvent = (dto) => {
        window.showConfirm?.(L().ArchiveConfirm, async () => {
            try {
                const res = await fetch(`${apiBase}/events/${dto.id}/archive`, { method: 'POST', credentials: 'same-origin' });
                if (!res.ok) throw new Error('Archive failed.');
                window.showToast?.(L().EventArchived, 'success');
                window.location.href = '/Platform/NotificationEvents';
            } catch (error) {
                console.error(error);
                window.showToast?.(L().ErrorOccurred, 'error');
            }
        }, { entityName: dto.eventCode, type: 'danger', confirmButtonText: L().Archive });
    };

    const load = async () => {
        if (!eventCode) { showError(); return; }
        try {
            const res = await fetch(`${apiBase}/events/${encodeURIComponent(eventCode)}`, { credentials: 'same-origin' });
            const body = await res.json();
            if (!res.ok) { showError(errorsOf(body).join(' ')); return; }
            const dto = unwrap(body);
            if (!dto) { showError(); return; }

            setText('detailsTitle', dto.eventCode);
            setText('d-eventCode', dto.eventCode);
            setText('d-channel', dto.channel);
            setText('d-ownerModule', dto.ownerModuleId);
            setText('d-ownerDomain', dto.ownerDomain);
            setText('d-ownerService', dto.ownerService);
            setText('d-targetPage', dto.targetPageCode);
            setText('d-requiredPermission', dto.requiredPermissionKey);
            // MOD-0027-FU03A (Bridge) — read-only source model fields.
            setText('d-sourceType', dto.sourceType);
            setText('d-ownerArea', dto.ownerArea);
            setText('d-targetRoute', dto.targetRoute);
            setText('d-defaultTemplateKey', dto.defaultTemplateKey);
            setText('d-usageType', dto.usageType);
            setText('d-canTenantOverride', dto.canTenantOverride ? (L().Yes || 'Yes') : (L().No || 'No'));
            setText('d-manifestSource', dto.manifestSource);
            setText('d-manifestVersion', dto.manifestVersion);
            setText('d-severity', dto.defaultSeverity);
            setText('d-linkPolicy', dto.linkPolicy);
            setText('d-lastSynced', dto.lastSyncedAt ? new Date(dto.lastSyncedAt).toLocaleString(window.CurrentLanguage || undefined) : '-');

            const statusEl = document.getElementById('d-status');
            if (statusEl) {
                const map = { Draft: 'bg-label-warning', Active: 'bg-label-success', Deprecated: 'bg-label-secondary', Archived: 'bg-label-dark' };
                statusEl.className = `badge ${map[dto.status] || 'bg-label-primary'}`;
                statusEl.innerText = statusLabel(dto.status);
            }

            renderVars('d-requiredVars', dto.requiredVariables);
            renderVars('d-optionalVars', dto.optionalVariables);

            // Validation note derived from status (per-event persisted issues are a backend follow-up).
            if (dto.status === 'Active') {
                document.getElementById('d-validationActive')?.classList.remove('d-none');
            } else if (dto.status === 'Draft') {
                document.getElementById('d-validationDraft')?.classList.remove('d-none');
            }

            const archiveBtn = document.getElementById('btnArchiveEvent');
            if (archiveBtn && dto.status !== 'Archived') {
                archiveBtn.classList.remove('d-none');
                archiveBtn.addEventListener('click', () => archiveEvent(dto));
            }
        } catch (error) {
            console.error('[NotificationEvents Details] Load failed.', error);
            showError();
        }
    };

    document.addEventListener('DOMContentLoaded', () => { void load(); });
})();
