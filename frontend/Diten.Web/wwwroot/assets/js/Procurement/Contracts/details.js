'use strict';

// MOD-0144 Contracting Details — contract lifecycle actions (activate / terminate). State-changing calls go
// through the same-origin controller proxy (/Contracts/...), which keeps the Idempotency-Key server-side.
// Confirmations use the premium shared helpers (window.showConfirm/showToast, premium-modal-standard MOD-0013).
(function () {
    const root = document.querySelector('.contracts-details');
    if (!root) return;
    const contractId = root.getAttribute('data-contract-id');
    if (!contractId) return;

    const L = Object.assign({}, window.L10n || {});
    try {
        const l10nPayload = document.getElementById('contracts-details-l10n');
        if (l10nPayload) Object.assign(L, JSON.parse(l10nPayload.textContent || '{}'));
    } catch (e) { console.error('[Contracts Details] L10n payload parse failed.', e); }
    const enc = encodeURIComponent(contractId);
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

    // ── Activate (Draft/InReview → Active; approval trail via MOD-0023) ──
    document.getElementById('btnActivateContract')?.addEventListener('click', () => {
        window.showConfirm?.(L.ConfirmActivate || L.AreYouSure, async () => {
            try {
                const res = await fetch(`/Contracts/activate/${enc}`, { method: 'POST', credentials: 'same-origin' });
                if (!res.ok) { toast(await readErrors(res), 'error'); return; }
                toast(L.ActivateSuccess || L.RecordSaved, 'success');
                setTimeout(() => window.location.reload(), 900);
            } catch (error) {
                console.error(error);
                toast(L.ErrorOccurred, 'error');
            }
        }, { type: 'question', confirmButtonText: L.ActionActivate });
    });

    // ── Terminate (Active → Terminated) ──
    document.getElementById('btnTerminateContract')?.addEventListener('click', () => {
        window.showConfirm?.(L.ConfirmTerminate || L.AreYouSure, async () => {
            try {
                const res = await fetch(`/Contracts/terminate/${enc}`, { method: 'POST', credentials: 'same-origin' });
                if (!res.ok) { toast(await readErrors(res), 'error'); return; }
                toast(L.TerminateSuccess || L.RecordSaved, 'success');
                setTimeout(() => window.location.reload(), 900);
            } catch (error) {
                console.error(error);
                toast(L.ErrorOccurred, 'error');
            }
        }, { type: 'danger', confirmButtonText: L.ActionTerminate });
    });
})();
