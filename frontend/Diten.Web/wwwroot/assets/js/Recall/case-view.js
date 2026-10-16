/**
 * MOD-0177 R7a — Vaka Detayı, the PURE part (no DOM; the Node test runs it against THIS file).
 *   offers(case, can, me) — which button the page shows. A button needs (1) its action's key (`can(code)`, pack §11.3 — without it the
 *   server draws nothing and this says false), (2) the status in which the service accepts it (pack §8.7), (3) four-eyes (CT-R1): the user
 *   who submitted the scope is never offered "Kapsamı onayla", the user who completed the effectiveness check never "Vakayı kapat". The
 *   server refuses all three anyway (403 / 409); the screen does not mislead.
 *   The other helpers: the steps bar index, the request statuses a line can retry / segregate, the open quantity of an effectiveness result,
 *   and the sentence key of a refusal (RECALL_SOD_VIOLATION reads differently on a closure).
 */
'use strict';

var RecallCaseView = (function () {
    const STEPS = ['Draft', 'PendingScopeApproval', 'Active', 'EffectivenessCheck', 'Closed'];
    const RETRYABLE = ['BlockedByReservation', 'NothingAvailable', 'Failed'];
    const HELD = ['Held', 'PartiallyHeld'];

    const same = (a, b) => !!a && !!b && String(a).toLowerCase() === String(b).toLowerCase();
    const underRecall = (status) => status === 'Active' || status === 'EffectivenessCheck';

    /** The steps bar: index of the status (Cancelled → -1: no step is current). */
    const step = (status) => STEPS.indexOf(status);

    /** c = getCase (CaseDetail), can = action code → bool, me = the signed-in user's id. */
    const offers = (c, can, me) => {
        const s = c?.status;
        const approvable = s === 'PendingScopeApproval' || (s === 'Active' && !!c.pendingRevision);
        const closable = s === 'EffectivenessCheck' && !!c.effectiveness;
        const submitter = c?.scopeSubmittedBy?.userId;
        const completer = c?.effectivenessCompletedBy?.userId;
        return {
            mark: s === 'Draft' && can('RECALL_SCOPE_MARK'),
            submit: s === 'Draft' && can('RECALL_SCOPE_SUBMIT'),
            returnScope: s === 'PendingScopeApproval' && can('RECALL_SCOPE_RETURN'),
            approve: approvable && can('RECALL_SCOPE_APPROVE') && !same(submitter, me),
            approveSod: approvable && can('RECALL_SCOPE_APPROVE') && same(submitter, me),
            rescan: !!c?.approvedScope,
            revise: s === 'Active' && !c.pendingRevision && !!c.approvedScope && can('RECALL_SCOPE_REVISE'),
            quarantine: underRecall(s) && can('RECALL_QUARANTINE_REQUEST'),
            segregate: underRecall(s) && can('RECALL_SEGREGATION_CONFIRM'),
            notify: underRecall(s) && can('RECALL_CUSTOMER_NOTIFY'),
            consume: underRecall(s) && can('RECALL_CONSUMPTION_DECLARE'),
            link: underRecall(s) && can('RECALL_RETURN_LINK'),
            effStart: s === 'Active' && can('RECALL_EFFECTIVENESS_START'),
            effComplete: s === 'EffectivenessCheck' && !c.effectiveness && can('RECALL_EFFECTIVENESS_COMPLETE'),
            close: closable && can('RECALL_CASE_CLOSE') && !same(completer, me),
            closeSod: closable && can('RECALL_CASE_CLOSE') && same(completer, me),
            cancel: (s === 'Draft' || s === 'PendingScopeApproval') && can('RECALL_CASE_CANCEL')
        };
    };

    /** A stock line: may its request be retried / its held quantity be confirmed as segregated (needs a location)? */
    const retryable = (line) => RETRYABLE.includes(line?.request?.status);
    const segregatable = (line) => HELD.includes(line?.request?.status) && !!line?.locationId;
    const requestable = (line) => !line?.request && line?.stockStatus === 'AVAILABLE' && Number(line?.quantity) > 0;

    /** An open quantity left in ANY SKU + unit total (closure then needs an acceptance reason). */
    const hasOpen = (effectiveness) => (effectiveness?.totals || []).some((t) => Number(t.open) > 0);

    /** The effectiveness rows of one customer (per SKU + unit — never added across units, D-20). */
    const customerRows = (effectiveness, customerRef) => (effectiveness?.rows || []).filter((r) => r.customerRef === customerRef);

    /**
     * The scope as the mockup's tree (L:258): every item under its parent (parentKey), depth-first from the root, siblings in the server's
     * order; an item whose parent is not in the list starts its own branch. Each item exactly once (a cycle cannot repeat a row).
     */
    const treeOrder = (items) => {
        const list = items || [];
        const keys = new Set(list.map((i) => i.nodeKey));
        const children = new Map();
        list.forEach((i) => { const p = i.parentKey && keys.has(i.parentKey) ? i.parentKey : ''; if (!children.has(p)) children.set(p, []); children.get(p).push(i); });
        const out = [];
        const seen = new Set();
        const visit = (item, depth) => {
            if (seen.has(item.nodeKey)) return;
            seen.add(item.nodeKey);
            out.push({ item, depth });
            (children.get(item.nodeKey) || []).forEach((c) => visit(c, depth + 1));
        };
        (children.get('') || []).forEach((r) => visit(r, 0));
        list.filter((i) => !seen.has(i.nodeKey)).forEach((i) => visit(i, 0));
        return out;
    };

    /** The vocabulary key of a refusal; the same SoD code is worded for the decision it refused. */
    const errorKey = (code, decision) => (code === 'RECALL_SOD_VIOLATION' && decision === 'close'
        ? 'Recall.Errors.RECALL_SOD_VIOLATION_CLOSE'
        : `Recall.Errors.${code}`);

    /**
     * WP-0177-R7c (D-27 closed, KR-H): "Müşteri iadesi gir" → the MOD-0173 form pre-filled with CUSTOMER_RETURN, the case's product / SKU / root lot
     * and QUARANTINE (a return is inspected first; the form offers only held statuses for this type). Only once the scope is approved (returns
     * link from then on) and only for a registered root lot; null otherwise. The form is MOD-0173's: it posts the movement, never 0177.
     */
    const customerReturnHref = (c) => {
        if (!c || !['Active', 'EffectivenessCheck'].includes(c.status)) return null;
        const lotId = c.rootLot?.lotId;
        if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(String(lotId || ''))) return null;
        const query = new URLSearchParams({ type: 'CUSTOMER_RETURN' });
        if (c.item?.itemId) query.set('itemId', c.item.itemId);
        if (c.item?.skuId && c.item?.skuLevel) query.set('sku', `${c.item.skuLevel}:${c.item.skuId}`);
        query.set('lotId', lotId);
        query.set('toStatus', 'QUARANTINE');
        return `/StockMovements/Create?${query}`;
    };

    return { STEPS, step, offers, retryable, segregatable, requestable, hasOpen, customerRows, treeOrder, errorKey, customerReturnHref };
})();

if (typeof window !== 'undefined') window.RecallCaseView = RecallCaseView;
