/**
 * Notification Settings - Details page (MOD-0027-FU02).
 * Read-only settings view + resolved fallback panel + per-tenant delete.
 * Fallback behavior is server-resolved; a missing platform default renders a controlled error, never a fake fallback.
 */
'use strict';

(function () {
    const apiBase = '/Platform/NotificationSettings/api';
    const root = document.getElementById('settingsDetailsRoot');
    if (!root) return;

    const tenantId = root.dataset.targetTenantId || '';
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

    const loadResolved = async () => {
        const resolvedError = document.getElementById('resolvedError');
        const resolvedResult = document.getElementById('resolvedResult');
        resolvedError?.classList.add('d-none');
        try {
            const res = await fetch(`${apiBase}/${tenantId}/resolved`, { credentials: 'same-origin' });
            const body = await res.json().catch(() => null);
            if (!res.ok) {
                resolvedResult?.classList.add('d-none');
                if (resolvedError) {
                    resolvedError.textContent = errorsOf(body).join(' ') || L().ResolvedUnavailable || '';
                    resolvedError.classList.remove('d-none');
                }
                return;
            }
            const dto = unwrap(body);
            setText('resolvedSource', dto?.isPlatformDefault
                ? (L().ResolvedFromPlatformDefault || 'Platform default')
                : (L().ResolvedFromTenant || 'Tenant-specific'));
            setText('resolvedProvider', dto?.providerCode);
            setText('resolvedSenderEmail', dto?.senderEmail);
            setText('resolvedFallbackPolicy', dto?.fallbackPolicy);
            resolvedResult?.classList.remove('d-none');
        } catch (error) {
            console.error('[NotificationSettings Details Resolved] Failed.', error);
            if (resolvedError) {
                resolvedError.textContent = L().ResolvedUnavailable || '';
                resolvedError.classList.remove('d-none');
            }
        }
    };

    const deleteSettings = () => {
        window.showConfirm?.(L().DeleteConfirm, async () => {
            try {
                const res = await fetch(`${apiBase}/${tenantId}`, {
                    method: 'DELETE',
                    credentials: 'same-origin'
                });
                if (!res.ok) throw new Error('Delete failed.');
                window.showToast?.(L().RecordDeleted, 'success');
                window.location.href = '/Platform/NotificationSettings';
            } catch (error) {
                console.error(error);
                window.showToast?.(L().ErrorOccurred, 'error');
            }
        }, { entityName: tenantId, type: 'danger', confirmButtonText: L().Delete });
    };

    const load = async () => {
        if (!tenantId) return;
        try {
            const res = await fetch(`${apiBase}/${tenantId}`, { credentials: 'same-origin' });
            const body = await res.json();
            if (!res.ok) {
                showError(errorsOf(body).join(' '));
                return;
            }
            const dto = unwrap(body);
            if (!dto) { showError(); return; }
            setText('d-tenant', dto.tenantId);
            setText('d-provider', dto.providerCode);
            setText('d-host', dto.host);
            setText('d-port', dto.port);
            setText('d-useSsl', dto.useSsl ? 'SSL' : '-');
            setText('d-credentialSecretRef', dto.credentialSecretRef);
            setText('d-senderEmail', dto.senderEmail);
            setText('d-senderName', dto.senderName);
            setText('d-replyToEmail', dto.replyToEmail);
            setText('d-fallbackPolicy', dto.fallbackPolicy);
            setText('d-updatedAt', dto.updatedAt ? new Date(dto.updatedAt).toLocaleString(window.CurrentLanguage || undefined) : '-');
            const statusEl = document.getElementById('d-isEnabled');
            if (statusEl) {
                statusEl.className = `badge ${dto.isEnabled ? 'bg-label-success' : 'bg-label-secondary'}`;
                statusEl.innerText = dto.isEnabled ? (L().Active || 'Active') : (L().Passive || 'Passive');
            }
            const editBtn = document.getElementById('btnEditSettings');
            if (editBtn) {
                editBtn.href = `/Platform/NotificationSettings/Edit/${tenantId}`;
                editBtn.classList.remove('d-none');
            }
            const deleteBtn = document.getElementById('btnDeleteSettings');
            if (deleteBtn) {
                deleteBtn.classList.remove('d-none');
                deleteBtn.addEventListener('click', deleteSettings);
            }
            void loadResolved();
        } catch (error) {
            console.error('[NotificationSettings Details] Load failed.', error);
            showError();
        }
    };

    document.addEventListener('DOMContentLoaded', () => {
        void load();
        document.getElementById('btnLoadResolved')?.addEventListener('click', loadResolved);
    });
})();
