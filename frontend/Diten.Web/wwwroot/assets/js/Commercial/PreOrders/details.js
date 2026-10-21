/**
 * WP-VW-W6 · W6-WEB-a — Details: one pre-order, read only, with the order's own abilities as buttons (can.*): edit (a
 * draft of the caller), the approval screen (the resolved approver), the document, cancel (a reason is required — D5),
 * copy into a new draft (rejected / cancelled — D6) and retry a failed transmission (sales operations). The CRM checks
 * each again; a refusal is said in the rule source's words.
 */
'use strict';

const PreOrderDetails = (function () {
    const U = window.PreOrdersUi;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const root = byId('po-root');
    const endpoint = '/Commercial/PreOrders/api';
    let order = null;

    const alertBox = (text) => { const a = byId('po-alert'); a.textContent = text || ''; a.classList.toggle('d-none', !text); };
    const toggle = (id, on) => byId(id)?.classList.toggle('d-none', !on);

    const draw = () => {
        const o = order;
        byId('po-title').innerHTML = U.Th('DetailsTitleOrder', o.code);
        byId('po-subtitle').innerHTML = U.bidi((o.pharmacy || {}).name || '') + (o.depot ? ' · ' + U.bidi(o.depot.name) : '');
        byId('po-status').innerHTML = U.statusBadge(o.status);
        byId('po-facts').innerHTML = U.facts(o);
        byId('po-lines').innerHTML = U.linesTable(o);
        byId('po-summary').innerHTML = U.totals(o);
        byId('po-state').innerHTML = U.stateHtml(o);
        byId('po-timeline').innerHTML = U.timelineHtml(o);
        const can = o.can || {};
        toggle('po-open-edit', can.edit);
        toggle('po-open-approve', can.decide);
        toggle('po-open-document', true);
        toggle('po-cancel', can.cancel);
        toggle('po-copy', can.copy);
        toggle('po-retry', can.retryTransmission && o.status === 'transmission_failed');
        byId('po-body').classList.remove('d-none');
    };

    const load = async () => {
        const r = await U.request('GET', endpoint + '/orders/' + encodeURIComponent(root.getAttribute('data-order-id')));
        const o = U.data(r);
        if (!o) { alertBox(U.errorText(r)); return; }
        order = o;
        alertBox('');
        draw();
    };

    const act = async (action, body) => {
        const r = await U.request('POST', endpoint + '/orders/' + encodeURIComponent(order.id) + '/' + action, body);
        if (!r.ok) { alertBox(U.errorText(r)); return null; }
        return U.data(r);
    };

    const init = () => {
        if (!root || !U) return;
        byId('po-cancel').addEventListener('click', async () => {
            const reason = await U.askReason(L().CancelTitle, L().CancelText, L().CancelConfirm);
            if (!reason) return;
            if (await act('cancel', { reason: reason, expectedVersion: order.version })) { U.toast(L().CancelledToast, 'success'); load(); }
        });
        byId('po-copy').addEventListener('click', async () => {
            const copy = await act('copy', {});
            if (copy) { window.location.href = '/Commercial/PreOrders/Edit/' + encodeURIComponent(copy.id); }
        });
        byId('po-retry').addEventListener('click', async () => {
            if (await act('retry-transmission', { expectedVersion: order.version })) { U.toast(L().RetryToast, 'info'); load(); }
        });
        load();
    };

    return { init: init };
})();

document.addEventListener('DOMContentLoaded', () => PreOrderDetails.init());
