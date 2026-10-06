'use strict';

// MOD-0141 Requisition Details — submit lifecycle action (Draft → Submitted).
// State-changing call goes through the same-origin controller proxy (/Requisitions/submit/{id}), which keeps the
// Idempotency-Key server-side. Confirmation uses the premium shared helpers (window.showConfirm/showToast,
// premium-modal-standard MOD-0013).
(function () {
    const root = document.querySelector('.requisition-details');
    if (!root) return;
    const requisitionId = root.getAttribute('data-requisition-id');
    if (!requisitionId) return;

    const L = Object.assign({}, window.L10n || {});
    try {
        const l10nPayload = document.getElementById('requisition-details-l10n');
        if (l10nPayload) Object.assign(L, JSON.parse(l10nPayload.textContent || '{}'));
    } catch (e) { console.error('[Requisition Details] L10n payload parse failed.', e); }
    const enc = encodeURIComponent(requisitionId);
    const toast = (msg, type) => window.showToast?.(msg, type);

    const readErrors = async (res) => {
        try {
            const body = await res.json();
            const errs = body?.errors || body?.Errors;
            if (Array.isArray(errs) && errs.length) return errs.join(' ');
            const code = body?.error?.code || body?.data?.error?.code;
            if (code) return code;
        } catch (e) { }
        return L.ErrorOccurred || 'Error';
    };

    // ── Submit (Draft → Submitted) ──
    const submitBtn = document.getElementById('btnSubmitRequisition');
    submitBtn?.addEventListener('click', () => {
        window.showConfirm?.(L.ConfirmSubmit || L.AreYouSure, async () => {
            try {
                const res = await fetch(`/Requisitions/submit/${enc}`, { method: 'POST', credentials: 'same-origin' });
                if (!res.ok) { toast(await readErrors(res), 'error'); return; }
                toast(L.SubmitSuccess || L.RecordSaved, 'success');
                setTimeout(() => window.location.reload(), 900);
            } catch (error) {
                console.error(error);
                toast(L.ErrorOccurred, 'error');
            }
        }, { type: 'question', confirmButtonText: L.ActionSubmit });
    });
})();
