/**
 * WP-VW-W6 · W6-WEB-a — C (the approval, Work Center deep link) and C-link (the e-mail link, after sign-in).
 *
 * C reads the order and the pharmacy's six months; C-link first resolves its token (POST, the token in the body) to one
 * of the link states — valid (with the order) · consumed · expired · superseded · not found — and draws only the valid
 * one's decision. Both decide through the SAME CRM command (MOD-0168 §4.5: two ways in, one rule): approve, reject or
 * request changes — the last two with a required reason (D2); from the link the token travels with the decision and is
 * consumed once. The first decision wins: a second one is 409 pre_order_already_decided, said in the rule source's words.
 */
'use strict';

const PreOrderApproval = (function () {
    const C = window.PreOrdersCore;
    const U = window.PreOrdersUi;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const root = byId('po-root');
    const endpoint = '/Commercial/PreOrders/api';
    const isLink = () => !!root && root.getAttribute('data-screen') === 'link';
    let order = null;
    let token = null;

    const alertBox = (text) => { const a = byId('po-alert'); a.textContent = text || ''; a.classList.toggle('d-none', !text); };

    const totalHtml = (o) => {
        if (o.priceMode !== 'priced' || !o.totals) return '<span class="text-muted">' + U.esc(L().QuantityModeShort) + '</span>';
        const fx = C.fxArgs(o.fx, U.culture(), U.day);
        return '<div class="fs-5 fw-semibold text-heading">' + U.esc(U.money(o.totals.gross, o.currency)) + '</div>' + (fx ? '<small class="text-muted">' + U.Th('FxLine', ...fx) + '</small>' : '');
    };

    const draw = () => {
        const o = order;
        byId('po-title').innerHTML = U.Th('ApproveTitleOrder', o.code);
        if (byId('po-status')) { byId('po-status').innerHTML = U.statusBadge(o.status); }
        byId('po-facts').innerHTML = U.facts(o);
        byId('po-total').innerHTML = totalHtml(o);
        byId('po-why').innerHTML = U.whyHtml(o);
        byId('po-limits').innerHTML = U.limitsTable(o);
        if (byId('po-you')) {
            const req = o.approvalRequirement || {};
            // V-10 — the Reports-to seat by its NAME (the code only when no name is known)
            byId('po-you').innerHTML = U.Th('YouAreReportsTo', (o.rep || {}).displayName || '', req.approverPositionName || req.approverPositionCode || '');
        }
        if (byId('po-link-code')) { byId('po-link-code').innerHTML = U.Th('ApproveTitleOrder', o.code); byId('po-link-pharmacy').innerHTML = U.bidi((o.pharmacy || {}).name || ''); }
        const can = !!(o.can && o.can.decide);
        ['po-approve', 'po-reject', 'po-changes'].forEach((id) => { const b = byId(id); if (b) { b.disabled = !can; } });
        byId('po-body').classList.remove('d-none');
        if (!can && !isLink()) { result('info', 'bx-info-circle', L().NotYourDecision); }
    };

    const result = (tone, icon, text) => {
        const box = byId('po-result');
        if (!box) return;
        box.className = 'alert alert-' + tone + ' d-flex gap-2 align-items-start';
        byId('po-result-icon').className = 'bx ' + icon + ' mt-1';
        byId('po-result-text').textContent = text || '';
    };

    const linkState = (state, title, text) => {
        const card = byId('po-link-state');
        const icons = { valid: 'bx-check-circle text-success', consumed: 'bx-check-double text-secondary', expired: 'bx-time text-warning', superseded: 'bx-revision text-warning', notfound: 'bx-link-alt text-danger' };
        byId('po-link-state-icon').className = 'bx fs-1 mb-2 d-block ' + (icons[state] || icons.notfound);
        byId('po-link-state-title').textContent = title || '';
        byId('po-link-state-text').textContent = text || '';
        card.classList.remove('d-none');
        byId('po-body').classList.add('d-none');
    };

    const loadHistory = async () => {
        const host = byId('po-history');
        if (!host) return;
        byId('po-history-title').innerHTML = U.Th('HistoryTitleOf', (order.pharmacy || {}).name || '');
        const r = await U.request('GET', endpoint + '/orders/' + encodeURIComponent(order.id) + '/pharmacy-history');
        const d = U.data(r);
        // V-9 (W6-FIX-d) — the months, their amounts and their effective discount are the CRM's (no arithmetic here)
        host.innerHTML = d ? U.historyHtml(d.months || []) : '<p class="text-muted small mb-0">' + U.esc(U.errorText(r)) + '</p>';
    };

    /** G-7 (W6-FIX-b) — "Approve" asks first, through the shared confirmation; the other two ask for their reason. */
    const decide = async (decision) => {
        let reason = null;
        if (decision === 'approve') {
            U.confirm(L().ApproveConfirmTitle, U.T('ApproveConfirmText', order.code || ''), L().Approve, 'success', () => send(decision, null));
            return;
        }
        const words = decision === 'reject' ? ['RejectTitle', 'RejectText', 'Reject'] : ['ChangesTitle', 'ChangesText', 'RequestChanges'];
        reason = await U.askReason(L()[words[0]], L()[words[1]], L()[words[2]]);
        if (!reason) return;
        await send(decision, reason);
    };

    const send = async (decision, reason) => {
        ['po-approve', 'po-reject', 'po-changes'].forEach((id) => { byId(id).disabled = true; });
        const body = { decision: decision, reason: reason, expectedVersion: order.version };
        if (isLink()) { body.linkToken = token; }
        const r = await U.request('POST', endpoint + '/orders/' + encodeURIComponent(order.id) + '/decision', body);
        if (!r.ok) { alertBox(U.errorText(r)); draw(); return; }
        alertBox('');
        order = U.data(r) || order;
        const key = 'Decided_' + decision.replace(/-/g, '_');
        if (isLink()) { linkState('consumed', L()[key], L().LinkUsedNow); return; }
        draw();
        ['po-approve', 'po-reject', 'po-changes'].forEach((id) => { byId(id).disabled = true; });
        result(decision === 'approve' ? 'success' : 'warning', decision === 'approve' ? 'bx-check-circle' : 'bx-revision', L()[key]);
    };

    const initLink = async () => {
        token = root.getAttribute('data-link-token') || '';
        if (!token) { linkState('notfound', L().Link_notfound, L().Link_notfound_text); return; }
        const r = await U.request('POST', endpoint + '/approval-link', { token: token });
        const d = U.data(r);
        if (!d) { linkState('notfound', L().Link_notfound, r.status === 404 ? L().Link_notfound_text : U.errorText(r)); return; }
        const key = C.linkStateKey(d.state);
        if (d.state !== 'valid' || !d.preOrder) { linkState(d.state, L()[key], L()[key + '_text']); return; }
        const note = byId('po-link-note');
        byId('po-link-note-text').textContent = U.T('LinkOnce', C.num(C.hoursLeft(d.expiresAt, Date.now()), U.culture(), 0));
        note.classList.remove('d-none');
        order = d.preOrder;
        draw();
    };

    const initApprove = async () => {
        const r = await U.request('GET', endpoint + '/orders/' + encodeURIComponent(root.getAttribute('data-order-id')));
        const o = U.data(r);
        if (!o) { alertBox(U.errorText(r)); return; }
        order = o;
        draw();
        if (o.status !== 'pending_approval') { result('info', 'bx-info-circle', U.T('AlreadyDecided', L()[C.statusOf(o.status).key] || o.status)); }
        loadHistory();
    };

    const init = () => {
        if (!root || !C || !U) return;
        byId('po-approve').addEventListener('click', () => decide('approve'));
        byId('po-reject').addEventListener('click', () => decide('reject'));
        byId('po-changes').addEventListener('click', () => decide('request-changes'));
        if (isLink()) { initLink(); } else { initApprove(); }
    };

    return { init: init };
})();

document.addEventListener('DOMContentLoaded', () => PreOrderApproval.init());
