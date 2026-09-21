'use strict';

// MOD-0141 Purchase Order Details — approve lifecycle action (Draft → Approved).
// State-changing call goes through the same-origin controller proxy (/PurchaseOrders/approve/{id}), which keeps the
// Idempotency-Key server-side. Confirmation uses the premium shared helpers (window.showConfirm/showToast,
// premium-modal-standard MOD-0013).
(function () {
    const root = document.querySelector('.purchase-order-details');
    if (!root) return;
    const poId = root.getAttribute('data-po-id');
    if (!poId) return;

    const L = Object.assign({}, window.L10n || {});
    try {
        const l10nPayload = document.getElementById('purchase-order-details-l10n');
        if (l10nPayload) Object.assign(L, JSON.parse(l10nPayload.textContent || '{}'));
    } catch (e) { console.error('[PurchaseOrder Details] L10n payload parse failed.', e); }
    const enc = encodeURIComponent(poId);
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

    // ── Approve (Draft → Approved) ──
    const approveBtn = document.getElementById('btnApprovePo');
    approveBtn?.addEventListener('click', () => {
        window.showConfirm?.(L.ConfirmApprove || L.AreYouSure, async () => {
            try {
                const res = await fetch(`/PurchaseOrders/approve/${enc}`, { method: 'POST', credentials: 'same-origin' });
                if (!res.ok) { toast(await readErrors(res), 'error'); return; }
                toast(L.ApproveSuccess || L.RecordSaved, 'success');
                setTimeout(() => window.location.reload(), 900);
            } catch (error) {
                console.error(error);
                toast(L.ErrorOccurred, 'error');
            }
        }, { type: 'question', confirmButtonText: L.ActionApprove });
    });
})();
