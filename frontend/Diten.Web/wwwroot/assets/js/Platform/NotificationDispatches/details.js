/**
 * Notification Dispatches - Details page (MOD-0027-FU02).
 * Read-only. Shows ONLY metadata, recipient COUNTS, sanitized variables, redacted error,
 * correlation id and a backend-truncated safe preview inside a sandboxed iframe.
 * The full email body, recipient addresses and Bcc are never requested or rendered.
 */
'use strict';

(function () {
    const apiBase = '/Platform/NotificationDispatches/api';
    const root = document.getElementById('dispatchDetailsRoot');
    if (!root) return;

    const dispatchId = root.dataset.dispatchId || '';
    const tenantId = root.dataset.targetTenantId || '';
    const CANCELLABLE = ['Queued'];
    const L = () => window.L10n || {};

    const unwrap = (payload) => payload?.data ?? payload?.Data ?? null;
    const errorsOf = (payload) => payload?.errors || payload?.Errors || [];
    const setText = (id, value) => {
        const el = document.getElementById(id);
        if (el) el.innerText = (value === null || value === undefined || value === '') ? '-' : String(value);
    };
    const fmt = (v) => (v ? new Date(v).toLocaleString(window.CurrentLanguage || undefined) : '-');
    const showError = (message) => {
        const el = document.getElementById('detailsError');
        if (!el) return;
        el.textContent = message || L().ErrorOccurred || '';
        el.classList.remove('d-none');
    };

    const renderPreview = (dto) => {
        const frame = document.getElementById('d-previewFrame');
        const noPreview = document.getElementById('d-noPreview');
        const html = dto?.bodyHtmlPreview;
        const text = dto?.bodyTextPreview;
        if (frame && html) {
            frame.srcdoc = html;
            frame.classList.remove('d-none');
            noPreview?.classList.add('d-none');
        } else if (frame && text) {
            // Escape into a plain-text document; the iframe stays fully sandboxed (no scripts, no same-origin).
            const escaped = text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
            frame.srcdoc = `<pre style="white-space:pre-wrap;font-family:monospace;margin:0;padding:8px;">${escaped}</pre>`;
            frame.classList.remove('d-none');
            noPreview?.classList.add('d-none');
        } else {
            frame?.classList.add('d-none');
            noPreview?.classList.remove('d-none');
        }
    };

    const cancelDispatch = () => {
        window.showConfirm?.(L().CancelConfirm, async () => {
            try {
                const res = await fetch(`${apiBase}/${dispatchId}/cancel?targetTenantId=${encodeURIComponent(tenantId)}`, {
                    method: 'POST',
                    credentials: 'same-origin'
                });
                if (!res.ok) throw new Error('Cancel failed.');
                window.showToast?.(L().DispatchCancelled, 'success');
                window.location.href = '/Platform/NotificationDispatches';
            } catch (error) {
                console.error(error);
                window.showToast?.(L().ErrorOccurred, 'error');
            }
        }, { entityName: dispatchId, type: 'danger', confirmButtonText: L().CancelDispatch });
    };

    const load = async () => {
        if (!dispatchId || !tenantId) { showError(); return; }
        try {
            const res = await fetch(`${apiBase}/${dispatchId}?targetTenantId=${encodeURIComponent(tenantId)}`, { credentials: 'same-origin' });
            const body = await res.json();
            if (!res.ok) { showError(errorsOf(body).join(' ')); return; }
            const dto = unwrap(body);
            if (!dto) { showError(); return; }

            setText('d-templateKey', dto.templateKey);
            setText('d-channel', dto.channel);
            setText('d-locale', dto.locale);
            setText('d-provider', dto.providerCode);
            setText('d-providerMessageId', dto.providerMessageId);
            setText('d-subject', dto.subject);
            setText('d-retryCount', dto.retryCount);
            setText('d-queuedAt', fmt(dto.queuedAt));
            setText('d-sentAt', fmt(dto.sentAt));
            setText('d-failedAt', fmt(dto.failedAt));
            // Counts only — never the recipient addresses, never Bcc content.
            setText('d-toCount', Array.isArray(dto.to) ? dto.to.length : 0);
            setText('d-ccCount', dto.ccCount ?? 0);
            setText('d-bccCount', dto.bccCount ?? 0);
            setText('d-errorCode', dto.errorCode);
            setText('d-errorMessage', dto.errorMessage);
            setText('d-correlationId', dto.correlationId);

            const statusEl = document.getElementById('d-status');
            if (statusEl) {
                const map = {
                    Queued: 'bg-label-warning', Sent: 'bg-label-success',
                    Failed: 'bg-label-danger', Cancelled: 'bg-label-secondary'
                };
                statusEl.className = `badge ${map[dto.status] || 'bg-label-primary'}`;
                const labels = {
                    Queued: L().StatusQueued, Sent: L().StatusSent,
                    Failed: L().StatusFailed, Cancelled: L().StatusCancelled
                };
                statusEl.innerText = labels[dto.status] || dto.status || '-';
            }

            const varsEl = document.getElementById('d-variables');
            if (varsEl) {
                let pretty = dto.variablesJson || '';
                try { pretty = JSON.stringify(JSON.parse(dto.variablesJson || '{}'), null, 2); } catch (e) { /* keep raw sanitized string */ }
                varsEl.innerText = pretty || '-';
            }

            renderPreview(dto);

            const cancelBtn = document.getElementById('btnCancelDispatch');
            if (cancelBtn && CANCELLABLE.includes(dto.status)) {
                cancelBtn.classList.remove('d-none');
                cancelBtn.addEventListener('click', cancelDispatch);
            }
        } catch (error) {
            console.error('[NotificationDispatches Details] Load failed.', error);
            showError();
        }
    };

    document.addEventListener('DOMContentLoaded', () => { void load(); });
})();
