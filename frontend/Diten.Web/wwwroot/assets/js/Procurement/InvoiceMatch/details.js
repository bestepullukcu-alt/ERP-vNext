'use strict';

// MOD-0143 Invoice Capture & 3-Way Match Details — the runThreeWayMatch + resolveMatchException lifecycle actions.
// Both state-changing calls go through the same-origin controller proxy (/InvoiceMatch/match/{id},
// /InvoiceMatch/resolve/{exceptionId}), which keeps the Idempotency-Key server-side. Confirmation uses the premium
// shared helpers (window.showConfirm/showToast, premium-modal-standard MOD-0013). This module produces the match
// OUTCOME only — it never executes payment (AP/payment = Finance/Treasury).
(function () {
    const root = document.querySelector('.invoice-match-details');
    if (!root) return;
    const invoiceId = root.getAttribute('data-invoice-id');
    if (!invoiceId) return;
    const exceptionId = root.getAttribute('data-exception-id') || '';

    const L = Object.assign({}, window.L10n || {});
    try {
        const l10nPayload = document.getElementById('invoice-match-details-l10n');
        if (l10nPayload) Object.assign(L, JSON.parse(l10nPayload.textContent || '{}'));
    } catch (e) { console.error('[InvoiceMatch Details] L10n payload parse failed.', e); }

    const invEnc = encodeURIComponent(invoiceId);
    const toast = (msg, type) => window.showToast?.(msg, type);
    const esc = (v) => String(v ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);

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

    const resultText = (result) => {
        switch (String(result)) {
            case '0': case 'Matched': return L.ResultMatched || 'Matched';
            case '1': case 'MatchedWithinTolerance': return L.ResultMatchedWithinTolerance || 'MatchedWithinTolerance';
            case '2': case 'Exception': return L.ResultException || 'Exception';
            default: return String(result ?? '');
        }
    };

    const renderOutcome = (outcome) => {
        if (!outcome) return;
        const wrap = document.getElementById('matchOutcome');
        const badge = document.getElementById('matchResultBadge');
        if (wrap) wrap.classList.remove('d-none');
        if (badge) badge.textContent = resultText(outcome.result);
        const variances = Array.isArray(outcome.variances) ? outcome.variances : [];
        const varWrap = document.getElementById('matchVariancesWrap');
        const varBody = document.getElementById('matchVariancesBody');
        if (varBody) {
            varBody.innerHTML = variances.map((v) => {
                const within = v.withinTolerance ? (L.Yes || 'Yes') : (L.No || 'No');
                return '<tr>'
                    + '<td>' + esc(v.field) + '</td>'
                    + '<td>' + esc(v.expected) + '</td>'
                    + '<td>' + esc(v.actual) + '</td>'
                    + '<td>' + esc(within) + '</td>'
                    + '<td>' + esc(v.poLineId ?? '-') + '</td>'
                    + '</tr>';
            }).join('');
        }
        if (varWrap) varWrap.classList.toggle('d-none', variances.length === 0);
    };

    // ── Run 3-way match ──
    const matchBtn = document.getElementById('btnRunMatch');
    matchBtn?.addEventListener('click', () => {
        window.showConfirm?.(L.ConfirmRunMatch || L.AreYouSure, async () => {
            try {
                const res = await fetch(`/InvoiceMatch/match/${invEnc}`, { method: 'POST', credentials: 'same-origin' });
                if (!res.ok) { toast(await readErrors(res), 'error'); return; }
                let outcome = null;
                try { const body = await res.json(); outcome = body?.data || body?.Data || body; } catch (e) { }
                renderOutcome(outcome);
                toast(L.MatchSuccess || L.RecordSaved, 'success');
                setTimeout(() => window.location.reload(), 1200);
            } catch (error) {
                console.error(error);
                toast(L.ErrorOccurred, 'error');
            }
        }, { type: 'info', confirmButtonText: L.ActionRunMatch });
    });

    // ── Resolve exception (approve / reject / tolerance-override) ──
    const resolveBtn = document.getElementById('btnResolveException');
    resolveBtn?.addEventListener('click', () => {
        if (!exceptionId) return;
        window.showConfirm?.(L.ResolveTitle || L.AreYouSure, async (decision) => {
            if (!decision) return;
            try {
                const res = await fetch(`/InvoiceMatch/resolve/${encodeURIComponent(exceptionId)}`, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ decision })
                });
                if (!res.ok) { toast(await readErrors(res), 'error'); return; }
                toast(L.ResolveSuccess || L.RecordSaved, 'success');
                setTimeout(() => window.location.reload(), 900);
            } catch (error) {
                console.error(error);
                toast(L.ErrorOccurred, 'error');
            }
        }, {
            type: 'warning',
            showInput: true,
            inputType: 'select',
            inputOptions: {
                'approve': L.DecisionApprove || 'Approve',
                'reject': L.DecisionReject || 'Reject',
                'tolerance-override': L.DecisionToleranceOverride || 'Tolerance override'
            },
            inputLabel: L.ResolveDecisionLabel || '',
            inputRequired: true,
            confirmButtonText: L.ActionResolve
        });
    });
})();
