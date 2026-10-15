/**
 * MOD-0177 R6 — İzleme, the PURE part (no DOM; the Node test runs it against THIS file). One list of nodes — the service's rule-10 order
 * (pack §8.2) — becomes two drawings that show the SAME nodes: the tree (depth-first by parentKey) and the table (the service's order). A
 * gap is a red row in both. Another company never appears as a row: the service sends it as ONE count (CT-R8, gap OTHER_COMPANY_ACTIVITY),
 * which the page shows in a band.
 */
'use strict';

var RecallTraceView = (function () {
    const LOT_KINDS = ['RawMaterialLot', 'FinishedGoodLot', 'Lot'];

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
            place = n.customerRef ? t('Recall.Shipment.Customer', n.customerRef) : t('Recall.Shipment.UnknownCustomer');
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

    return { describe, tableRows, treeRows, summary, isLot };
})();

if (typeof window !== 'undefined') window.RecallTraceView = RecallTraceView;
