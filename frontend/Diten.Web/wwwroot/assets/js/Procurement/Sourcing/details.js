'use strict';

// MOD-0145 Sourcing Details — RFx lifecycle actions (publish / submit bid / award) + bids sub-list.
// State-changing calls go through the same-origin controller proxy (/Sourcing/...), which keeps the
// Idempotency-Key server-side. Confirmations use the premium shared helpers (window.showConfirm/showToast,
// premium-modal-standard MOD-0013); data entry uses Bootstrap modals.
(function () {
    const root = document.querySelector('.sourcing-details');
    if (!root) return;
    const rfxId = root.getAttribute('data-rfx-id');
    if (!rfxId) return;

    const L = Object.assign({}, window.L10n || {});
    try {
        const l10nPayload = document.getElementById('sourcing-details-l10n');
        if (l10nPayload) Object.assign(L, JSON.parse(l10nPayload.textContent || '{}'));
    } catch (e) { console.error('[Sourcing Details] L10n payload parse failed.', e); }
    const enc = encodeURIComponent(rfxId);
    const toast = (msg, type) => window.showToast?.(msg, type);
    const jsonHeaders = { 'Content-Type': 'application/json' };

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

    // ── Bids sub-list ──
    const bidsBody = document.getElementById('bidsBody');
    const renderBids = (items) => {
        if (!bidsBody) return [];
        if (!Array.isArray(items) || !items.length) {
            bidsBody.innerHTML = `<tr><td colspan="4" class="text-muted text-center">${L.NotAvailable || '-'}</td></tr>`;
            return [];
        }
        bidsBody.innerHTML = items.map((b) => {
            const lines = Array.isArray(b.lines) ? b.lines.length : 0;
            return `<tr>
                <td class="fw-medium text-heading">${b.bidId ?? ''}</td>
                <td>${b.supplierId ?? ''}</td>
                <td>${lines}</td>
                <td>${b.evaluationScore ?? '-'}</td>
            </tr>`;
        }).join('');
        return items;
    };
    const loadBids = async () => {
        try {
            const res = await fetch(`/Sourcing/bids/${enc}`, { method: 'GET', credentials: 'same-origin' });
            if (!res.ok) return [];
            const body = await res.json();
            const items = body?.data?.items || body?.data?.Items || body?.items || [];
            const rendered = renderBids(items);
            populateAwardOptions(rendered);
            return rendered;
        } catch (error) {
            console.error('[Sourcing Details] Failed to load bids.', error);
            return [];
        }
    };

    // ── Publish ──
    const publishBtn = document.getElementById('btnPublishRfx');
    publishBtn?.addEventListener('click', () => {
        window.showConfirm?.(L.ConfirmPublish || L.AreYouSure, async () => {
            try {
                const res = await fetch(`/Sourcing/publish/${enc}`, { method: 'POST', credentials: 'same-origin' });
                if (!res.ok) { toast(await readErrors(res), 'error'); return; }
                toast(L.PublishSuccess || L.RecordSaved, 'success');
                setTimeout(() => window.location.reload(), 900);
            } catch (error) {
                console.error(error);
                toast(L.ErrorOccurred, 'error');
            }
        }, { type: 'question', confirmButtonText: L.ActionPublish });
    });

    // ── Submit bid ──
    const bidLinesBody = document.getElementById('bidLinesBody');
    document.getElementById('btnAddBidLine')?.addEventListener('click', () => {
        const rows = bidLinesBody?.querySelectorAll('.bid-line-row');
        const template = rows?.[rows.length - 1];
        if (!template) return;
        const clone = template.cloneNode(true);
        clone.querySelectorAll('.bid-line-input').forEach((i) => { i.value = ''; });
        bidLinesBody.appendChild(clone);
    });
    bidLinesBody?.addEventListener('click', (e) => {
        const btn = e.target.closest('.bid-line-remove');
        if (!btn) return;
        const rows = bidLinesBody.querySelectorAll('.bid-line-row');
        if (rows.length <= 1) return;
        btn.closest('.bid-line-row')?.remove();
    });

    const collectBidLines = () => {
        const rows = bidLinesBody?.querySelectorAll('.bid-line-row') || [];
        const lines = [];
        rows.forEach((row) => {
            const get = (f) => row.querySelector(`[data-bid-field="${f}"]`)?.value?.trim() || '';
            const itemId = get('itemId');
            const unitPrice = get('unitPrice');
            const leadRaw = get('leadTimeDays');
            if (!itemId && !unitPrice && !leadRaw) return;
            const line = { itemId, unitPrice };
            if (leadRaw !== '') { const n = parseInt(leadRaw, 10); if (!isNaN(n)) line.leadTimeDays = n; }
            lines.push(line);
        });
        return lines;
    };

    document.getElementById('btnSubmitBid')?.addEventListener('click', async () => {
        const supplierId = document.getElementById('bidSupplierId')?.value?.trim() || '';
        const lines = collectBidLines();
        if (!supplierId || !lines.length) { toast(L.FormValidationError || L.ErrorOccurred, 'error'); return; }
        try {
            const res = await fetch(`/Sourcing/bids/${enc}`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: jsonHeaders,
                body: JSON.stringify({ supplierId, lines })
            });
            if (!res.ok) { toast(await readErrors(res), 'error'); return; }
            toast(L.BidSuccess || L.RecordSaved, 'success');
            bootstrap.Modal.getInstance(document.getElementById('bidModal'))?.hide();
            loadBids();
        } catch (error) {
            console.error(error);
            toast(L.ErrorOccurred, 'error');
        }
    });

    // ── Award ──
    const populateAwardOptions = (bids) => {
        const sel = document.getElementById('awardedBidId');
        if (!sel) return;
        const placeholder = sel.querySelector('option[value=""]');
        sel.innerHTML = '';
        if (placeholder) sel.appendChild(placeholder);
        (bids || []).forEach((b) => {
            if (!b?.bidId) return;
            sel.appendChild(new Option(`${b.bidId} — ${b.supplierId || ''}`, b.bidId));
        });
    };

    document.getElementById('btnSubmitAward')?.addEventListener('click', async () => {
        const awardedBidId = document.getElementById('awardedBidId')?.value?.trim() || '';
        const rationale = document.getElementById('awardRationale')?.value?.trim() || '';
        if (!awardedBidId) { toast(L.FormValidationError || L.ErrorOccurred, 'error'); return; }
        try {
            const res = await fetch(`/Sourcing/award/${enc}`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: jsonHeaders,
                body: JSON.stringify({ awardedBidId, rationale: rationale || null })
            });
            if (!res.ok) { toast(await readErrors(res), 'error'); return; }
            toast(L.AwardSuccess || L.RecordSaved, 'success');
            bootstrap.Modal.getInstance(document.getElementById('awardModal'))?.hide();
            setTimeout(() => window.location.reload(), 900);
        } catch (error) {
            console.error(error);
            toast(L.ErrorOccurred, 'error');
        }
    });

    document.addEventListener('DOMContentLoaded', loadBids);
    if (document.readyState !== 'loading') loadBids();
})();
