/**
 * WP-VW-W6 · W6-WEB-a — the pharmacy pre-order screens' RULES, without the DOM (Node-tested): the status and outcome
 * vocabulary, the MF / discount input, what keeps "Send" closed, the approval box, the timeline, the "entered ↔ limit"
 * cells of screen C, the six-month history, the link states, the period window and the number / money / percent words.
 *
 * NOTHING HERE COMPUTES A PRICE, A FREE BOX, A DISCOUNT OR A TOTAL (MOD-0168 §4.2, AC-PRICE-2): every amount and every
 * finding is MOD-0156's answer (the preview / the order); this file only reads and words it. Words are keys of the page's
 * resource (window.L10n); the rule messages are the shared commercial-rules.js.
 */
(function (root) {
    'use strict';

    const STATUSES = {
        draft: { key: 'St_draft', tone: 'secondary', icon: 'bx-edit-alt' },
        pending_approval: { key: 'St_pending_approval', tone: 'warning', icon: 'bx-time-five' },
        approved: { key: 'St_approved', tone: 'info', icon: 'bx-check' },
        transmitted: { key: 'St_transmitted', tone: 'success', icon: 'bx-send' },
        transmission_failed: { key: 'St_transmission_failed', tone: 'danger', icon: 'bx-error' },
        rejected: { key: 'St_rejected', tone: 'danger', icon: 'bx-x-circle' },
        cancelled: { key: 'St_cancelled', tone: 'secondary', icon: 'bx-block' }
    };
    const STATUS_CODES = Object.keys(STATUSES);

    const OUTCOMES = {
        ok: { tone: 'success', icon: 'bx-check-circle' },
        soft: { tone: 'warning', icon: 'bx-error' },
        hard: { tone: 'danger', icon: 'bx-block' }
    };

    const DEPOT_RESPONSES = ['delivered', 'partial'];

    /** The event log types (PreOrderEventTypes) → their timeline icon / tone; the words are Ev_{type}. */
    const EVENTS = {
        'created': { icon: 'bx-file-blank', tone: 'secondary' },
        'updated': { icon: 'bx-edit-alt', tone: 'secondary' },
        'discarded': { icon: 'bx-trash', tone: 'secondary' },
        'signature-captured': { icon: 'bx-pen', tone: 'primary' },
        'signature-cleared': { icon: 'bx-eraser', tone: 'warning' },
        'submitted': { icon: 'bx-upload', tone: 'primary' },
        'auto-approved': { icon: 'bx-check-double', tone: 'success' },
        'approval-requested': { icon: 'bx-user-check', tone: 'warning' },
        'approver-unresolved': { icon: 'bx-user-x', tone: 'warning' },
        'approved': { icon: 'bx-check', tone: 'success' },
        'rejected': { icon: 'bx-x-circle', tone: 'danger' },
        'changes-requested': { icon: 'bx-revision', tone: 'warning' },
        'transmission-attempted': { icon: 'bx-loader-circle', tone: 'secondary' },
        'transmitted': { icon: 'bx-send', tone: 'success' },
        'transmission-failed': { icon: 'bx-error', tone: 'danger' },
        'cancelled': { icon: 'bx-block', tone: 'secondary' },
        'depot-response-recorded': { icon: 'bx-package', tone: 'info' },
        'copied-from': { icon: 'bx-copy', tone: 'secondary' }
    };

    const LINK_STATES = ['valid', 'expired', 'consumed', 'superseded'];

    // ── the period window (the twin of PreOrdersController.ResolveRange) ─────────────────────────────────────────
    const iso = (d) => d.toISOString().slice(0, 10);
    const addDays = (isoDay, n) => { const d = new Date(isoDay + 'T00:00:00Z'); d.setUTCDate(d.getUTCDate() + n); return iso(d); };
    const DAY = /^\d{4}-\d{2}-\d{2}$/;

    /** "" = no window · last7 · last30 · month · custom (from ≤ to) → { from, to } | { error }. */
    function rangeOf(period, today, from, to) {
        switch (String(period || '').trim().toLowerCase()) {
            case '': return { from: null, to: null };
            case 'last7': return { from: addDays(today, -6), to: today };
            case 'last30': return { from: addDays(today, -29), to: today };
            case 'month': return { from: today.slice(0, 8) + '01', to: today };
            case 'custom':
                if (!DAY.test(String(from || '')) || !DAY.test(String(to || ''))) return { error: 'ErrRangeMissing' };
                return String(to) < String(from) ? { error: 'ErrRangeOrder' } : { from: from, to: to };
            default: return { error: 'ErrRangeMissing' };
        }
    }

    const customRangeError = (period, from, to) => (period === 'custom' ? (rangeOf(period, '2000-01-01', from, to).error || null) : null);

    // ── the inputs (the server checks them again) ────────────────────────────────────────────────────────────────
    /** "10+2" → { buy: 10, free: 2 }; "" → null; anything else → 'invalid' (pre_order_mf_invalid). */
    function parseMf(text) {
        const s = String(text == null ? '' : text).trim();
        if (!s) return null;
        const m = /^(\d{1,4})\s*\+\s*(\d{1,4})$/.exec(s);
        if (!m) return 'invalid';
        const buy = Number(m[1]); const free = Number(m[2]);
        return buy >= 1 && free >= 1 ? { buy: buy, free: free } : 'invalid';
    }

    const mfText = (mf) => (mf && mf.buy ? mf.buy + '+' + mf.free : '');

    /** A discount typed in the page's culture ("12,5" or "12.5") → 12.5; "" → null; out of 0 ≤ x < 100 or 2+ decimals → 'invalid'. */
    function parseDiscount(text) {
        const s = String(text == null ? '' : text).trim().replace(',', '.');
        if (!s) return null;
        if (!/^\d{1,2}(\.\d)?$/.test(s)) return 'invalid';
        const n = Number(s);
        return n >= 0 && n < 100 ? n : 'invalid';
    }

    /** The lines as the CRM takes them (PreOrderLineInput); in quantity mode NO discount travels (AC-PRICE-3). */
    function linesInput(lines, priced) {
        return (lines || []).filter((l) => l && l.skuId).map((l) => {
            const mf = parseMf(l.mf);
            const d = priced ? parseDiscount(l.discount) : null;
            return {
                skuId: l.skuId,
                quantity: Number.isFinite(Number(l.quantity)) && String(l.quantity).trim() !== '' ? Math.trunc(Number(l.quantity)) : null,
                mf: mf && mf !== 'invalid' ? mf : null,
                discountPct: typeof d === 'number' ? d : null
            };
        });
    }

    /** The first input problem of the lines (a key of the page), or null. */
    function inputProblem(lines, priced) {
        for (const l of lines || []) {
            if (!l || !l.skuId) continue;
            const q = Number(l.quantity);
            if (!Number.isInteger(q) || q < 1) return 'ErrQuantity';
            if (parseMf(l.mf) === 'invalid') return 'ErrMf';
            if (priced && parseDiscount(l.discount) === 'invalid') return 'ErrDiscount';
        }
        return null;
    }

    /**
     * Why "Send" is closed (DESIGN-GAP §1 A: hard limit / no signature / no line — and, for this page, the depot, an unsaved
     * change and the submit key). Empty = it may be pressed; the CRM still refuses on its own (AC-FLOW-3).
     */
    function sendBlockers(s) {
        const why = [];
        if (!s.canSubmit) why.push('Why_no_permission');
        if (!s.lineCount) why.push('Why_no_lines');
        if (s.outcome === 'hard') why.push('Why_hard');
        if (!s.hasDepot) why.push('Why_no_depot');
        if (!s.hasSignature) why.push('Why_no_signature');
        if (s.dirty) why.push('Why_unsaved');
        return why;
    }

    /**
     * The "Onay gerekir mi?" box: from the preview (approvalRequired + reasons) and the context's approver preview
     * (PLT-W6 line managers: LineManager = the Reports-to seat; ManagerChain = the seat was vacant, one up; None).
     */
    function approvalBox(preview, context) {
        const p = preview || {};
        const reasons = (p.approvalReasons || []).map((r) => (r === 'tenant-setting' ? 'Need_tenant_setting' : r === 'soft-exceeded' ? 'Need_soft' : null)).filter(Boolean);
        const approver = (context && context.approver) || {};
        const names = (approver.managers || []).map((m) => m.displayName).filter(Boolean);
        let line;
        if (!approver.available) line = { key: 'Approver_unavailable', args: [] };
        else if (!names.length || approver.resolution === 'None') line = { key: 'Approver_none', args: [] };
        else if (approver.resolution === 'ManagerChain') line = { key: 'Approver_chain', args: [names.join(', ')] };
        else line = { key: 'Approver_line', args: [names.join(', ')] };
        return {
            known: !!preview,
            required: !!p.approvalRequired,
            answerKey: !preview ? 'Need_unknown' : (p.approvalRequired ? 'Need_yes' : 'Need_no'),
            reasons: reasons,
            approver: line,
            position: approver.positionName || approver.positionCode || ''
        };
    }

    /** The order's timeline (the append-only event log, oldest first): { type, key, at, icon, tone, reason }. */
    function timeline(order) {
        return ((order && order.events) || []).slice().sort((a, b) => String(a.at).localeCompare(String(b.at))).map((e) => {
            const known = EVENTS[e.type];
            const payload = e.payload || {};
            return {
                type: e.type,
                key: known ? 'Ev_' + e.type.replace(/-/g, '_') : 'Ev_unknown',
                at: e.at,
                icon: known ? known.icon : 'bx-circle',
                tone: known ? known.tone : 'secondary',
                reason: payload.reason || null,
                code: payload.code || payload.reasonCode || null
            };
        });
    }

    /** What screen A shows for an order that is no longer an editable draft (and the way out it offers). */
    function lockState(order) {
        if (!order) return { locked: false };
        const can = order.can || {};
        if (order.status === 'draft') return { locked: !can.edit, noteKey: can.edit ? null : 'Lock_not_owner' };
        if (order.status === 'pending_approval') return { locked: true, noteKey: 'Lock_pending', action: null };
        if (order.status === 'rejected' || order.status === 'cancelled') {
            return { locked: true, noteKey: order.status === 'rejected' ? 'Lock_rejected' : 'Lock_cancelled', action: can.copy ? 'copy' : null };
        }
        return { locked: true, noteKey: 'Lock_sent', action: can.cancel ? 'cancel-new' : null };
    }

    /** The findings of a line grouped by the field they speak about (screen C: entered ↔ limit). */
    const FIELD_OF = { discount: 'discount', mf: 'mf', combined: 'combined', quantity: 'quantity' };
    function limitCells(line) {
        const cells = {};
        (line && line.findings || []).forEach((f) => {
            const family = String(f.code || '').split('.')[0];
            const field = FIELD_OF[family];
            if (!field) return;
            const p = f.params || {};
            const limit = field === 'combined' ? p.limitPct : (field === 'quantity' && f.code === 'quantity.below-min' ? p.min : (field === 'mf' ? p.limit : p.limit));
            const worse = !cells[field] || (f.severity === 'hard' && cells[field].severity !== 'hard');
            if (worse) cells[field] = { code: f.code, severity: f.severity, limit: limit == null ? null : String(limit), limitPct: p.limitPct == null ? null : String(p.limitPct) };
        });
        return cells;
    }

    /** "Neden onayımda?": the tenant setting and / or the lines over a soft limit (the codes of those lines). */
    function whyOnMe(order) {
        const req = (order && order.approvalRequirement) || {};
        const why = [];
        (req.reasons || []).forEach((r) => {
            if (r === 'tenant-setting') why.push({ key: 'Why_me_tenant', icon: 'bx-cog', lines: [] });
            if (r === 'soft-exceeded') {
                const lines = ((order && order.lines) || []).filter((l) => l.outcome === 'soft' || (l.findings || []).some((f) => f.severity === 'soft'));
                why.push({ key: 'Why_me_soft', icon: 'bx-error', lines: lines.map((l) => ({ lineNo: l.lineNo, name: (l.sku || {}).name || '', findings: (l.findings || []).filter((f) => f.severity === 'soft') })) });
            }
        });
        return why;
    }


    const linkStateKey = (state) => (LINK_STATES.indexOf(state) >= 0 ? 'Link_' + state : 'Link_notfound');
    const hoursLeft = (expiresAt, nowMs) => {
        const t = Date.parse(expiresAt || '');
        if (!Number.isFinite(t)) return 0;
        return Math.max(0, Math.ceil((t - nowMs) / 3600000));
    };

    // ── words for numbers (the page's culture; no fixed tr-TR — DESIGN-GAP §4) ───────────────────────────────────
    const nf = (culture, o) => { try { return new Intl.NumberFormat(culture, o); } catch (e) { return new Intl.NumberFormat('en', o); } };
    /** An amount with its currency code BESIDE it (every amount names its currency, MOD-0168 §4.2); '' without an amount. */
    const money = (amount, currency, culture) => (amount == null || amount === '' ? '' : nf(culture, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Number(amount)) + (currency ? ' ' + String(currency).toUpperCase() : ''));
    /** A percentage value (12.5 → "%12,5" in Turkish, "12.5%" in English). */
    const pct = (value, culture) => (value == null || value === '' ? '' : nf(culture, { style: 'percent', maximumFractionDigits: 1 }).format(Number(value) / 100));
    const num = (value, culture, digits) => (value == null || value === '' ? '' : nf(culture, { maximumFractionDigits: digits == null ? 2 : digits }).format(Number(value)));
    const rate = (value, culture) => (value == null ? '' : nf(culture, { maximumFractionDigits: 6 }).format(Number(value)));

    /** The local equivalent line's arguments (FxLine): ≈ amount · currency · rate · date · source; null without an FX snapshot. */
    function fxArgs(fx, culture, dayText) {
        if (!fx || fx.localGross == null) return null;
        return [num(fx.localGross, culture, 2), String(fx.localCurrency || '').toUpperCase(), rate(fx.rate, culture), dayText ? dayText(fx.rateDate) : String(fx.rateDate || ''), String(fx.source || '')];
    }

    /** The first error code of a CRM / proxy answer envelope (the screen words it from the rule source). */
    function errorCode(body) {
        const errors = body && (body.errors || (body.Errors));
        if (Array.isArray(errors) && errors.length && typeof errors[0] === 'string') return errors[0];
        const codes = body && body.errorCodes;
        if (Array.isArray(codes) && codes.length && codes[0] && codes[0].code) return codes[0].code;
        return null;
    }

    const statusOf = (s) => STATUSES[s] || { key: 'St_unknown', tone: 'secondary', icon: 'bx-help-circle' };
    const outcomeOf = (o) => OUTCOMES[o] || OUTCOMES.ok;

    // ── W6-FIX-b — the summary, the line's price source, the send button, the previous orders ────────────────────

    /**
     * V-3 — the summary rows IN ORDER, each naming the CRM field it shows (W6-FIX-c: totals.listSubtotal / discountAmount
     * beside net / vat / gross / effectiveDiscountPct). A row whose field the answer does not carry is left out — nothing
     * is derived from the others (MOD-0168 §4.2). `vatRate` is the lines' ONE rate (vatRateOf), else the row has no rate.
     */
    function summaryRows(totals, opts) {
        const t = totals || {};
        const o = opts || {};
        const has = (f) => t[f] != null && t[f] !== '';
        const rows = [];
        if (has('listSubtotal')) rows.push({ key: 'TotalListSubtotal', field: 'listSubtotal', kind: 'money' });
        if (has('discountAmount')) rows.push({ key: 'TotalDiscount', field: 'discountAmount', kind: 'discount' });
        if (has('net')) rows.push({ key: 'TotalNet', field: 'net', kind: 'money' });
        if (has('vat')) rows.push(o.vatRate != null ? { key: 'TotalVatRate', field: 'vat', kind: 'money', rate: o.vatRate } : { key: 'TotalVat', field: 'vat', kind: 'money' });
        if (!o.hideEffective && has('effectiveDiscountPct')) rows.push({ key: 'TotalEffective', field: 'effectiveDiscountPct', kind: 'pct' });
        return rows;
    }

    /** The lines' VAT rate when they all carry the same one (the CRM's per line); null when they differ or none is known. */
    function vatRateOf(lines) {
        const rates = (lines || []).map((l) => (l && l.vatRate != null ? Number(l.vatRate) : null)).filter((r) => r != null);
        if (!rates.length) return null;
        return rates.every((r) => r === rates[0]) ? rates[0] : null;
    }

    /** V-2 — the product's facts as the MDM snapshot carries them (form · pack · GTIN); each only when known. */
    function skuFacts(sku) {
        const s = sku || {};
        const parts = [];
        if (s.form) parts.push({ key: null, value: String(s.form) });
        if (s.packQuantity != null && s.packQuantity !== '') parts.push({ key: null, pack: true, value: String(s.packQuantity), unit: s.packUomCode || '' });
        if (s.gtin) parts.push({ key: 'GtinShort', value: String(s.gtin) });
        return parts;
    }

    /** V-5 — the send button's words: to the manager when the CRM's preview says an approval is needed, else plain send. */
    const sendLabelKey = (preview, order) => {
        const req = order && order.status === 'draft' && order.approvalRequirement;
        if (preview) return preview.approvalRequired ? 'SendForApproval' : 'Send';
        return req && req.required ? 'SendForApproval' : 'Send';
    };

    /** V-4 — "product × quantity" of an order's lines (the order's own snapshot; nothing summed). */
    const productSummary = (order) => ((order && order.lines) || []).map((l) => ({ name: (l.sku || {}).name || (l.sku || {}).code || '', quantity: l.quantity }));

    const api = {
        STATUSES: STATUSES, STATUS_CODES: STATUS_CODES, OUTCOMES: OUTCOMES, EVENTS: EVENTS, LINK_STATES: LINK_STATES, DEPOT_RESPONSES: DEPOT_RESPONSES,
        rangeOf: rangeOf, customRangeError: customRangeError,
        parseMf: parseMf, mfText: mfText, parseDiscount: parseDiscount, linesInput: linesInput, inputProblem: inputProblem,
        sendBlockers: sendBlockers, approvalBox: approvalBox, timeline: timeline, lockState: lockState,
        limitCells: limitCells, whyOnMe: whyOnMe, linkStateKey: linkStateKey, hoursLeft: hoursLeft,
        money: money, pct: pct, num: num, fxArgs: fxArgs, errorCode: errorCode, statusOf: statusOf, outcomeOf: outcomeOf,
        summaryRows: summaryRows, vatRateOf: vatRateOf, skuFacts: skuFacts, sendLabelKey: sendLabelKey, productSummary: productSummary
    };
    root.PreOrdersCore = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
