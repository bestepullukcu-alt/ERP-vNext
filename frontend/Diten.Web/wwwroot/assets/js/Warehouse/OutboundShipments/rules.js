/**
 * MOD-0178 DL-6 — the PURE rules of the outbound screens (pack §8.18, §8.21, §8.24, §11.3; the PutawayTasks/rules.js pattern — an extra file of the
 * Compact set, Node measures it: frontend/Diten.Web.Tests/JavaScript/Node/warehouse-s2-rules.test.mjs).
 *
 *  - The STATUS MENU is §8.18's transitions, nothing more (AC-34: "Packed'te yalnız Sevke hazır ve İptal"):
 *      Allocated → İptal · Picked → Paketle, İptal · Packed → Sevke hazır, İptal · ReadyToShip → Sevkiyatı kontrol et · Shipped / Cancelled → none.
 *    "Düzenle" (header only) is the Details page's own button on Allocated / Picked / Packed (§11.3), not a transition.
 *  - A key the reader lacks hides the entry (pack §11.3 keys); the service stays the authority (409 / 422).
 *  - An Item-level outbound (§8.24) keeps "Sevke hazır" on the menu but DISABLED with its reason (AC-33 (vi)): the release would answer 409
 *    WMS_ITEM_LEVEL_NOT_PUBLISHABLE until MVP-6 is aligned.
 *  - "Hiçbir şey toplanmadı — iptal edin": Allocated, every pick task terminal and nothing picked (§8.18).
 */
'use strict';

const OutboundRules = (function () {
    const Keys = {
        Create: 'warehouse.outbound-shipments.create',
        Edit: 'warehouse.outbound-shipments.update',
        Pack: 'warehouse.packages.create',
        Release: 'warehouse.outbound-shipments.release',
        SyncDispatch: 'warehouse.outbound-shipments.update',
        Cancel: 'warehouse.outbound-shipments.cancel'
    };

    /** §8.18 — the transitions of each visible status, in menu order. */
    const StatusMenu = {
        Allocated: ['Cancel'],
        Picked: ['Pack', 'Cancel'],
        Packed: ['Release', 'Cancel'],
        ReadyToShip: ['SyncDispatch'],
        Shipped: [],
        Cancelled: []
    };

    /** §8.18 Update column: the header can be edited while Allocated, Picked or Packed. */
    const EditStatuses = ['Allocated', 'Picked', 'Packed'];

    const ActionWord = {
        Edit: 'Warehouse.Outbound.Action.Edit', Pack: 'Warehouse.Outbound.Action.Pack', Release: 'Warehouse.Outbound.Action.Release',
        SyncDispatch: 'Warehouse.Outbound.Action.SyncDispatch', Cancel: 'Warehouse.Outbound.Action.Cancel'
    };

    const StatusClass = {
        Allocated: 'bg-label-info', Picked: 'bg-label-primary', Packed: 'bg-label-warning', ReadyToShip: 'bg-label-success', Shipped: 'bg-success',
        Cancelled: 'bg-label-secondary'
    };

    /** The status menu of an outbound for this reader: [{ action, enabled, reasonCode, dangerous }]. */
    const menu = (outbound, can) => (StatusMenu[outbound?.status] || [])
        .filter((action) => can(Keys[action]))
        .map((action) => {
            if (action === 'Release' && outbound.itemLevel) return { action, enabled: false, reasonCode: 'ItemLevelNotPublishable', dangerous: false };
            return { action, enabled: true, reasonCode: null, dangerous: action === 'Cancel' };
        });

    const canEdit = (outbound, can) => can(Keys.Edit) && EditStatuses.includes(outbound?.status);

    const reasonWord = (reasonCode) => (reasonCode ? `Warehouse.Outbound.Disabled.${reasonCode}` : null);

    const statusBadge = (status) => ({ word: `Warehouse.Outbound.Status.${status}`, cls: StatusClass[status] || 'bg-label-secondary' });

    /** Where a menu entry leads: "Paketle" to the Packing page; the dialogs live on the Details page (#Release, #Cancel, #SyncDispatch). */
    const actionUrl = (outbound, action) => (action === 'Pack'
        ? `/Warehouse/Packing/${encodeURIComponent(outbound.id)}`
        : `/Warehouse/OutboundShipments/Details/${encodeURIComponent(outbound.id)}#${action}`);

    const num = (value) => {
        const n = Number.parseFloat(String(value ?? '').replace(',', '.'));
        return Number.isFinite(n) ? n : 0;
    };

    /** §8.18: Allocated, at least one pick task, all of them terminal, and nothing picked → the only way out is "İptal". */
    const nothingPicked = (outbound) => {
        const tasks = outbound?.pickTasks || [];
        return outbound?.status === 'Allocated' && tasks.length > 0
            && tasks.every((t) => t.status === 'Completed' || t.status === 'Cancelled')
            && tasks.reduce((sum, t) => sum + num(t.confirmed), 0) === 0;
    };

    /** The lines of a 422 FEFO_BYPASS_REASON_REQUIRED: one reason row per deviating allocation, with its lot number from the outbound. */
    const deviations = (error, outbound) => {
        const lots = {};
        (outbound?.lines || []).forEach((l) => (l.allocations || []).forEach((a) => { lots[a.allocSeq] = a.lotNumber; }));
        return (error?.details?.deviations || []).map((d) => ({ allocSeq: Number(d.allocSeq), lotNumber: lots[d.allocSeq] || '', firstEligibleExpiry: d.firstEligibleExpiry || '' }));
    };

    /** The release body: the reasons the reader wrote (trimmed, non-empty); the service owns 10-1000. */
    const releaseBody = (key, etag, rows) => {
        const reasons = (rows || []).filter((r) => String(r.reason ?? '').trim()).map((r) => ({ allocSeq: Number(r.allocSeq), reason: String(r.reason).trim() }));
        return reasons.length ? { idempotencyKey: key, ifMatch: etag, fefoBypassReasons: reasons } : { idempotencyKey: key, ifMatch: etag };
    };

    /** "Level:skuId" (the SKU picker's value) → { skuLevel, skuId }; an Item is its own SKU (skuId = itemId, §8.24). */
    const parseSku = (value) => {
        const text = String(value ?? '');
        const at = text.indexOf(':');
        return at < 0 ? null : { skuLevel: text.slice(0, at), skuId: text.slice(at + 1) };
    };

    const isQuantity = (text) => /^\d{1,12}([.,]\d{1,6})?$/.test(String(text ?? '').trim()) && num(text) > 0;

    /**
     * One form line → the proxy's line (or a validation key): order line, item (itemId, skuId, skuLevel), base unit, quantity, optional lot override
     * with its FEFO reason. The service decides FEFO, Item resolution and the unit (§8.17).
     */
    const lineBody = (row) => {
        const sku = parseSku(row.sku);
        if (!String(row.orderLineId ?? '').trim() || !row.itemId || !sku || !row.uomId || !isQuantity(row.quantity)) return { error: 'ValidationLine' };
        const line = {
            orderLineId: String(row.orderLineId).trim(), itemId: row.itemId, skuId: sku.skuId, skuLevel: sku.skuLevel, uomId: row.uomId,
            quantity: String(row.quantity).trim().replace(',', '.')
        };
        if (row.lotId) line.lotOverride = { lotId: row.lotId, fefoBypassReason: String(row.reason ?? '').trim() || null };
        return { line };
    };

    /**
     * What a chosen lot asks (preview §8.17 step 3): the first free lot asks nothing; another lot follows the company's FEFO mode — Block refuses,
     * RequireReason needs 10-1000, Warn takes an optional reason.
     */
    const lotNeed = (preview, lotId) => {
        if (!lotId) return 'None';
        const first = (preview?.lots || []).find((l) => l.fefoRank === 1);
        if (first && first.lotId === lotId) return 'None';
        const mode = preview?.mode || 'RequireReason';
        return mode === 'Block' ? 'Blocked' : mode === 'Warn' ? 'Optional' : 'Required';
    };

    return { Keys, StatusMenu, EditStatuses, ActionWord, menu, canEdit, reasonWord, statusBadge, actionUrl, nothingPicked, deviations, releaseBody, parseSku, isQuantity, lineBody, lotNeed };
})();

if (typeof window !== 'undefined') window.OutboundRules = OutboundRules;
if (typeof module !== 'undefined' && module.exports) module.exports = OutboundRules;
