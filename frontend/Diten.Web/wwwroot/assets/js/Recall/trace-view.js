/**
 * MOD-0177 R6 — İzleme, the PURE part (no DOM; the Node test runs it against THIS file). One list of nodes — the service's rule-10 order
 * (pack §8.2) — becomes two drawings that show the SAME nodes: the tree (depth-first by parentKey) and the table (the service's order). A
 * gap is a red row in both. Another company never appears as a row: the service sends it as ONE count (CT-R8, gap OTHER_COMPANY_ACTIVITY),
 * which the page shows in a band.
 */
'use strict';

var RecallTraceView = (function () {
    const LOT_KINDS = ['RawMaterialLot', 'FinishedGoodLot', 'Lot'];
    const GUID = /^\s*[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\s*$/i;
    const PAGE = 25;

    /**
     * MOD-0177-FU01 (pack §8.8) — a customer as text, never its CRM key: "{name} · {code}"; Unresolved / NotFound / Forbidden with the
     * distinguisher (ship-to · shipment no); Deferred = "the name is not on this page"; a key without a CRM id is its own label.
     */
    const customerText = (c, t) => {
        if (!c) return '';
        const base = { Unresolved: 'Recall.Customer.Unresolved', NotFound: 'Recall.Customer.NotFound', Forbidden: 'Recall.Customer.Forbidden' }[c.state];
        if (c.state === 'Resolved') return t('Recall.Customer.NameCode', c.name, c.code);
        if (c.state === 'Deferred') return t('Recall.Customer.Deferred');
        if (base) {
            const parts = [c.shipTo, c.shipmentNumber].filter((p) => p && !GUID.test(p));
            const with_ = parts.length === 2 ? t('Recall.Customer.Distinguisher', parts[0], parts[1]) : parts[0];
            return with_ ? t(base + 'With', with_) : t(base);
        }
        return GUID.test(c.label || '') ? '' : c.label || '';
    };

    /** The distinct customer keys of the trace's Shipment nodes, ordinal (the service's customer page order). */
    const customerKeys = (trace) => [...new Set((trace?.nodes || []).filter((n) => n.kind === 'Shipment' && n.customerRef).map((n) => n.customerRef))].sort();

    /** The customer page selector: shown only past 25 customers. */
    const customerPager = (total, page) => ({ show: total > PAGE, from: Math.min((page - 1) * PAGE + 1, total), to: Math.min(page * PAGE, total), total,
        prev: page > 1, next: page * PAGE < total });

    /** A drawn Shipment node whose CRM customer the caller may not read: the CSV would be refused (403) — the link is not drawn (§8.8). */
    const exportForbidden = (trace) => (trace?.nodes || []).some((n) => n.kind === 'Shipment' && n.customer?.state === 'Forbidden');

    /** One node → what both drawings print (title, sub, place, quantity, reference, date, badges). */
    const describe = (n, t) => {
        const kind = n.untracked ? t('Recall.Kind.Input') : (t('Recall.Kind.' + n.kind) || n.kind);
        const product = n.itemLabel || n.item?.itemId || '';
        const lot = n.lot?.lotNumber || '';
        const quantity = n.quantity == null ? null : { value: n.quantity, unit: n.baseUomId || '' };
        const ref = n.movement?.sourceDocumentId || n.movement?.transactionNumber || '';
        const date = n.movement?.postingDate || '';
        const badges = [];
        let title = lot || product;
        let sub = lot ? product : '';
        let place = '';
        if (n.untracked) badges.push({ key: 'Recall.Badge.Untracked', tone: 'secondary' });
        if (n.kind === 'Stock') {
            title = [n.warehouseLabel || n.warehouseId, n.locationLabel || n.locationId].filter(Boolean).join(' · ');
            sub = [product, lot].filter(Boolean).join(' · ');
            place = n.warehouseLabel || n.warehouseId || '';
            if (n.stockStatus) badges.push({ key: 'Recall.StockStatus.' + n.stockStatus, tone: n.stockStatus === 'AVAILABLE' ? 'info' : 'warning', raw: n.stockStatus });
        } else if (n.kind === 'Shipment') {
            title = ref || t('Recall.Kind.Shipment');
            // MOD-0177-FU01: the customer display, never the CRM key (a bare key without a display is shown only when it is not a GUID)
            const who = n.customer ? customerText(n.customer, t) : (n.customerRef && !GUID.test(n.customerRef) ? n.customerRef : '');
            place = who ? t('Recall.Shipment.Customer', who) : t('Recall.Shipment.UnknownCustomer');
            sub = [product, lot].filter(Boolean).join(' · ');
            if (n.shipment && n.shipment.read && !['Ok', 'NotApplicable'].includes(n.shipment.read)) badges.push({ key: 'Recall.Shipment.Unreadable', tone: 'warning' });
        } else if (n.kind === 'Gap') {
            title = t('Recall.Gap.' + n.gapCode) || n.gapCode || '';
            sub = n.text || '';
        } else if (n.kind === 'Input' && !n.untracked) {
            title = ref || product;
            sub = product;
        }
        return { key: n.nodeKey, parent: n.parentKey || null, level: n.level, kind: n.kind, kindText: kind, gap: n.kind === 'Gap', title, sub,
            product, lot, place, quantity, ref, date, badges };
    };

    /** The table: the service's order, one row per node. */
    const tableRows = (trace, t) => (trace?.nodes || []).map((n) => describe(n, t));

    /** The tree: depth-first from the roots (no parent, or a parent that is not in the list), children in the service's order. */
    const treeRows = (trace, t) => {
        const nodes = trace?.nodes || [];
        const keys = new Set(nodes.map((n) => n.nodeKey));
        const children = new Map();
        const roots = [];
        nodes.forEach((n) => {
            if (n.parentKey && keys.has(n.parentKey) && n.parentKey !== n.nodeKey) {
                if (!children.has(n.parentKey)) children.set(n.parentKey, []);
                children.get(n.parentKey).push(n);
            } else {
                roots.push(n);
            }
        });
        const out = [];
        const seen = new Set();
        const walk = (n, depth, ancestors) => {
            if (seen.has(n.nodeKey)) return; // a node is drawn once (a cycle edge never repeats it)
            seen.add(n.nodeKey);
            const kids = children.get(n.nodeKey) || [];
            out.push({ ...describe(n, t), depth, hasChildren: kids.length > 0, ancestors });
            kids.forEach((k) => walk(k, depth + 1, [...ancestors, n.nodeKey]));
        };
        roots.forEach((r) => walk(r, 0, []));
        nodes.forEach((n) => { if (!seen.has(n.nodeKey)) walk(n, 0, []); }); // nothing is lost, even a malformed parent chain
        return out;
    };

    /** The figures above the result (from the nodes and the service's summary). */
    const summary = (trace) => {
        const nodes = trace?.nodes || [];
        const gaps = (trace?.gaps || []).filter((g) => g.code !== 'OTHER_COMPANY_ACTIVITY');
        return {
            finishedGoodLots: trace?.summary?.finishedGoodLots ?? nodes.filter((n) => n.kind === 'FinishedGoodLot').length,
            stockLines: nodes.filter((n) => n.kind === 'Stock').length,
            shipments: nodes.filter((n) => n.kind === 'Shipment').length,
            gaps: gaps.length,
            otherCompany: Number(trace?.summary?.otherCompanyActivityCount ?? 0),
            truncated: !!trace?.truncated,
            seconds: (Number(trace?.durationMs) || 0) / 1000
        };
    };

    const isLot = (n) => LOT_KINDS.includes(n.kind);
    /** WP-MVP1-FIX-LIVE2 (finding 6): where a newly opened case is shown — its Vaka Detayı; null for anything but a case id. */
    const caseHref = (caseId) => (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(String(caseId || ''))
        ? `/Recall/Cases/Details/${encodeURIComponent(caseId)}` : null);

    return { describe, tableRows, treeRows, summary, isLot, caseHref, customerText, customerKeys, customerPager, exportForbidden };
})();

if (typeof window !== 'undefined') window.RecallTraceView = RecallTraceView;
