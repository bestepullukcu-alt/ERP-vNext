'use strict';

// MOD-0142 Receiving (GRN) Details — the Reverse lifecycle action (ASSUMPTION-GRN-02).
// The state-changing call goes through the same-origin controller proxy (/Grn/reverse/{id}), which keeps the
// Idempotency-Key server-side. Confirmation uses the premium shared helpers (window.showConfirm/showToast,
// premium-modal-standard MOD-0013). A Posted GRN is corrected by an INVENTORY REVERSAL, never by an edit.
(function () {
    const root = document.querySelector('.grn-details');
    if (!root) return;
    const grnId = root.getAttribute('data-grn-id');
    if (!grnId) return;

    const L = Object.assign({}, window.L10n || {});
    try {
        const l10nPayload = document.getElementById('grn-details-l10n');
        if (l10nPayload) Object.assign(L, JSON.parse(l10nPayload.textContent || '{}'));
    } catch (e) { console.error('[Grn Details] L10n payload parse failed.', e); }
    const enc = encodeURIComponent(grnId);
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

    // ── Reverse ──
    const reverseBtn = document.getElementById('btnReverseGrn');
    reverseBtn?.addEventListener('click', () => {
        window.showConfirm?.(L.ConfirmReverse || L.AreYouSure, async () => {
            try {
                const res = await fetch(`/Grn/reverse/${enc}`, { method: 'POST', credentials: 'same-origin' });
                if (!res.ok) { toast(await readErrors(res), 'error'); return; }
                toast(L.ReverseSuccess || L.RecordSaved, 'success');
                setTimeout(() => window.location.reload(), 900);
            } catch (error) {
                console.error(error);
                toast(L.ErrorOccurred, 'error');
            }
        }, { type: 'danger', confirmButtonText: L.ActionReverse });
    });
})();
