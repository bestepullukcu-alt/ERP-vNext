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
     * E3-FIX-01 (G-1) — a service decimal ("2.000", "1.500") as the reader writes numbers: the user's culture, no trailing zeros (tr "2", "1,5";
     * en "2", "1.5"), Latin digits. Display only: what the screens send stays the dot-decimal wire string. Anything that is not a plain decimal is
     * shown as given. `grouping` false = the form an input can take back (no thousands separator).
     */
    const quantityText = (value, culture, grouping = true) => {
        if (value === null || value === undefined || value === '') return '';
        const text = String(value).trim();
        if (!/^-?\d+(\.\d+)?$/.test(text)) return text;
        try {
            return new Intl.NumberFormat(culture || undefined, { maximumFractionDigits: 6, useGrouping: grouping, numberingSystem: 'latn' }).format(text);
        } catch (error) {
            return text;
        }
    };

    /** A package's "L x W x H" (cm) with each number in the reader's culture ("30.5x20x10" → tr "30,5x20x10"); anything else as given. */
    const dimensionsText = (value, culture) => {
        const text = String(value ?? '').trim();
        if (!/^\d+(\.\d+)?(x\d+(\.\d+)?){2}$/i.test(text)) return text;
        return text.split(/x/i).map((part) => quantityText(part, culture, false)).join('x');
    };

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

    // ─── MOD-0178-FU01 DL-1 — the lines table and the line panel (pack §8.2, §8.3, §12) ─────────────────────────────────────────────────────

    /** The service's line limit (WMS-BUNDLE CreateOutboundShipmentRequest.lines maxItems 100): the 101st line is added by no path. */
    const MaxLines = 100;
    const canAddLine = (count) => Number(count) < MaxLines;

    /** The next free order line number ("1", "2", …) — the panel's default; the reader may write another. */
    const nextOrderLine = (lines) => {
        const taken = new Set((lines || []).map((l) => String(l.orderLineId ?? '').trim()));
        let n = 1;
        while (taken.has(String(n))) n += 1;
        return String(n);
    };

    /** §12: the order line number is unique among the lines (client check only — the service has no such rule). `uid` = the line being edited. */
    const orderLineTaken = (lines, orderLineId, uid) => {
        const wanted = String(orderLineId ?? '').trim();
        return !!wanted && (lines || []).some((l) => l.uid !== uid && String(l.orderLineId ?? '').trim() === wanted);
    };

    /**
     * One answer line → what a table row / the panel keeps of it: the free lots (FEFO order), the company's FEFO mode, the allocations (the CHOSEN
     * lot when the line had a lot override — D-4), the deviation (FEFO suggestion), the allocatable quantity and a failure code.
     */
    const previewOf = (answerLine, mode) => (answerLine ? {
        lots: answerLine.lots || [], mode: mode || null, allocations: answerLine.allocations || [], deviation: answerLine.deviation || null,
        allocatable: answerLine.allocatable ?? answerLine.allocated ?? null, failureCode: answerLine.failureCode || null
    } : null);

    /** The preview answer mapped to the lines in table order: line i is the answer's lineSeq i + 1 (the request order — §8.3). */
    const mapPreview = (answer, count) => {
        const lines = answer?.lines || [];
        return Array.from({ length: Number(count) || 0 }, (_, i) => previewOf(lines.find((l) => Number(l.lineSeq) === i + 1), answer?.fefoOutboundMode));
    };

    /**
     * A refusal → the lines it names, by index (lineSeq − 1): a lot / FEFO / item refusal names one line in details.lineSeq; 422
     * WMS_ALLOCATION_FAILED lists details.lines[] { lineSeq, code, requested, allocatable } (§8.3, AC-7, AC-10).
     */
    const lineErrors = (error) => {
        const out = {};
        const details = error?.details || {};
        (details.lines || []).forEach((l) => {
            const seq = Number(l.lineSeq);
            if (seq >= 1) out[seq - 1] = { code: l.code || error?.code || null, requested: l.requested ?? null, allocatable: l.allocatable ?? null };
        });
        const seq = Number(details.lineSeq);
        if (seq >= 1 && !out[seq - 1]) out[seq - 1] = { code: error?.code || null, requested: null, allocatable: null };
        return out;
    };

    /** The chosen lot is still among the preview's free lots (the reader's choice is never reset silently — AC-8). */
    const lotEligible = (preview, lotId) => !lotId || !preview || (preview.lots || []).some((l) => l.lotId === lotId);

    /**
     * A line's state on the table (pack §8.2 rozetleri): NotPreviewed · Stale · Fulfillable · Unfulfillable · DeviationReasonRequired ·
     * DeviationReasonGiven · DeviationBlocked. `line` = { lotId, reason, preview, stale, error }.
     */
    const lineStatus = (line) => {
        if (line?.error) return 'Unfulfillable';
        if (line?.stale) return 'Stale';
        const preview = line?.preview;
        if (!preview) return 'NotPreviewed';
        if (preview.failureCode || !lotEligible(preview, line.lotId)) return 'Unfulfillable';
        const need = lotNeed(preview, line.lotId);
        if (need === 'Blocked') return 'DeviationBlocked';
        if (need === 'Required') return String(line.reason ?? '').trim().length >= 10 ? 'DeviationReasonGiven' : 'DeviationReasonRequired';
        if (need === 'Optional' && String(line.reason ?? '').trim()) return 'DeviationReasonGiven';
        return 'Fulfillable';
    };

    /** The badge of a line state: icon + word (never colour alone — §8.2). */
    const LineBadge = {
        NotPreviewed: { cls: 'bg-label-secondary', icon: 'bx-time' },
        Stale: { cls: 'bg-label-warning', icon: 'bx-history' },
        Fulfillable: { cls: 'bg-label-success', icon: 'bx-check-circle' },
        Unfulfillable: { cls: 'bg-label-danger', icon: 'bx-error-circle' },
        DeviationReasonRequired: { cls: 'bg-label-warning', icon: 'bx-error' },
        DeviationReasonGiven: { cls: 'bg-label-info', icon: 'bx-comment-check' },
        DeviationBlocked: { cls: 'bg-label-danger', icon: 'bx-block' }
    };
    const lineBadge = (status) => Object.assign({ word: `LineStatus.${status}` }, LineBadge[status] || LineBadge.NotPreviewed);

    /**
     * Can this line go into the save body as it is (the panel's "Satırı kaydet" and the page's "Kaydet")? A validation key or null:
     * a lot change needs a CURRENT preview of the line (a stale one is "yeniden önizleyin" — AC-8), then the FEFO mode decides (AC-7).
     */
    const lineSaveError = (line) => {
        if (lineBody(line).error) return 'ValidationLine';
        if (!line.lotId) return null;
        if (line.stale || !line.preview) return 'LinePanel.PreviewAgain';
        const need = lotNeed(line.preview, line.lotId);
        if (need === 'Blocked') return 'DeviationBlocked';
        if (need === 'Required' && String(line.reason ?? '').trim().length < 10) return 'DeviationNeedsReason';
        return null;
    };

    return {
        Keys, StatusMenu, EditStatuses, ActionWord, menu, canEdit, reasonWord, statusBadge, actionUrl, nothingPicked, deviations, releaseBody, parseSku, isQuantity,
        lineBody, lotNeed, quantityText, dimensionsText,
        MaxLines, canAddLine, nextOrderLine, orderLineTaken, previewOf, mapPreview, lineErrors, lotEligible, lineStatus, lineBadge, lineSaveError
    };
})();

if (typeof window !== 'undefined') window.OutboundRules = OutboundRules;
if (typeof module !== 'undefined' && module.exports) module.exports = OutboundRules;
