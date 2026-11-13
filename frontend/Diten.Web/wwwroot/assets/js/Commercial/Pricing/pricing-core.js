/**
 * WP-VW-W6 · W6-WEB-b — screen D's RULES, without the DOM (Node-tested): the list badges and rule statuses, what each
 * record lets the caller do (four eyes: the preparer never gets the decision buttons — D13), the import preview's counts,
 * the change history rows, and the inputs as the CRM takes them.
 *
 * NO PRICE, MF RATIO, COMBINED DISCOUNT OR OUTCOME IS COMPUTED HERE (AC-EVAL-3): the MF ratio beside "10+2" and every
 * trial result are the CRM's answer (the trial endpoint / the rule's own DTO). "10+2" is only READ (PreOrdersCore.parseMf).
 */
(function (root) {
    'use strict';

    const P = () => root.PreOrdersCore;

    /** The list badge (§4.1.1: the status, and the phase of a published list read on the day). */
    const BADGES = {
        'draft': { key: 'Badge_draft', tone: 'secondary', icon: 'bx-edit-alt' },
        'in_review': { key: 'Badge_in_review', tone: 'warning', icon: 'bx-time-five' },
        'planned': { key: 'Badge_planned', tone: 'info', icon: 'bx-calendar' },
        'in-force': { key: 'Badge_in_force', tone: 'success', icon: 'bx-check-circle' },
        'past': { key: 'Badge_past', tone: 'secondary', icon: 'bx-history' },
        'discarded': { key: 'Badge_discarded', tone: 'dark', icon: 'bx-trash' }
    };
    const RULE_STATUSES = {
        'draft': { key: 'RuleSt_draft', tone: 'secondary', icon: 'bx-edit-alt' },
        'in_review': { key: 'RuleSt_in_review', tone: 'warning', icon: 'bx-time-five' },
        'active': { key: 'RuleSt_active', tone: 'success', icon: 'bx-check-circle' },
        'superseded': { key: 'RuleSt_superseded', tone: 'secondary', icon: 'bx-history' },
        'retired': { key: 'RuleSt_retired', tone: 'secondary', icon: 'bx-archive' },
        'discarded': { key: 'RuleSt_discarded', tone: 'dark', icon: 'bx-trash' }
    };
    const KINDS = ['official', 'company'];
    const TIERS = ['pharmacist-purchase', 'depot', 'public'];
    const HISTORY_TYPES = ['price-list', 'price-list-item', 'pricing-rule', 'fx-rate'];
    const HISTORY_ACTIONS = ['created', 'updated', 'version-created', 'discarded', 'submitted', 'published', 'returned', 'refused', 'withdrawn', 'superseded', 'retired'];
    const IMPORT_COUNTS = ['matched', 'unmatched', 'retired', 'duplicate', 'invalidPrice'];
    const IMPORT_DIFF = ['added', 'removed', 'changed', 'vatChanged'];

    const badgeOf = (b) => BADGES[b] || { key: 'Badge_unknown', tone: 'secondary', icon: 'bx-help-circle' };
    const ruleStatusOf = (s) => RULE_STATUSES[s] || { key: 'RuleSt_unknown', tone: 'secondary', icon: 'bx-help-circle' };

    /**
     * What a list / rule lets the caller do now (the CRM checks every one again). FOUR EYES: a record the caller prepared
     * (`mine`, set by the proxy) never offers its decision — D13; a submitted record is not edited; a published list is not
     * edited (a new version is made).
     */
    function listActions(list, f) {
        const s = (list || {}).status;
        const mine = !!(list && list.mine);
        return {
            edit: s === 'draft' && !!f.canUpdate,
            discard: s === 'draft' && !!f.canCreate,
            submit: s === 'draft' && !!f.canSubmit,
            decide: s === 'in_review' && !!f.canPublish && !mine,
            ownDecision: s === 'in_review' && !!f.canPublish && mine,
            newVersion: s === 'published' && !!f.canCreate
        };
    }

    function ruleActions(rule, f) {
        const s = (rule || {}).status;
        const mine = !!(rule && rule.mine);
        return {
            edit: s === 'draft' && !!f.canUpdate,
            discard: s === 'draft' && !!f.canUpdate,
            submit: s === 'draft' && !!f.canSubmit,
            decide: s === 'in_review' && !!f.canPublish && !mine,
            ownDecision: s === 'in_review' && !!f.canPublish && mine,
            revise: s === 'active' && !!f.canUpdate,
            retire: s === 'active' && !!f.canUpdate
        };
    }

    /** The import preview's numbers (step 2), each on its own: what will enter the draft and what will not. */
    function importSummary(imp) {
        const i = imp || {};
        const counts = IMPORT_COUNTS.map((k) => ({ key: 'Imp_' + k, count: Number(i[k]) || 0, tone: k === 'matched' ? 'success' : 'warning' }));
        const diff = IMPORT_DIFF.map((k) => ({ key: 'Diff_' + k, count: Number(i[k]) || 0 }));
        return {
            total: Number(i.totalRows) || 0,
            counts: counts,
            diff: diff,
            compared: !!i.comparedPriceListId,
            canConvert: i.status === 'validated' && (Number(i.matched) || 0) > 0,
            converted: i.status === 'converted' ? i.convertedPriceListId || null : null,
            discarded: i.status === 'discarded'
        };
    }

    /** A percentage typed in the page's culture ("12,5") → 12.5; "" → null; out of 0 ≤ x ≤ 100 or 2+ decimals → 'invalid'. */
    function parsePct(text) {
        const s = String(text == null ? '' : text).trim().replace(',', '.');
        if (!s) return null;
        if (!/^\d{1,3}(\.\d)?$/.test(s)) return 'invalid';
        const n = Number(s);
        return n >= 0 && n <= 100 ? n : 'invalid';
    }

    /** A whole number ≥ 0 ("" → null). */
    function parseCount(text) {
        const s = String(text == null ? '' : text).trim();
        if (!s) return null;
        return /^\d{1,7}$/.test(s) ? Number(s) : 'invalid';
    }

    /** A price / rate typed in the page's culture → a number with at most `digits` decimals; "" → null; else 'invalid'. */
    function parseAmount(text, digits) {
        const s = String(text == null ? '' : text).trim().replace(/\s/g, '').replace(',', '.');
        if (!s) return null;
        if (!new RegExp('^\\d{1,9}(\\.\\d{1,' + (digits || 4) + '})?$').test(s)) return 'invalid';
        return Number(s);
    }

    const bad = (v) => v === 'invalid';

    /** The rule form → PricingRuleInput, or { error: key } (the CRM checks soft ≤ hard and the rest again). */
    function ruleInput(v) {
        const mfSoft = P().parseMf(v.mfSoft), mfHard = P().parseMf(v.mfHard);
        const fields = { discSoft: parsePct(v.discSoft), discHard: parsePct(v.discHard), combSoft: parsePct(v.combSoft), combHard: parsePct(v.combHard),
            min: parseCount(v.min), softMax: parseCount(v.softMax), hardMax: parseCount(v.hardMax) };
        if (bad(mfSoft) || bad(mfHard)) return { error: 'ErrMf' };
        if (bad(fields.discSoft) || bad(fields.discHard) || bad(fields.combSoft) || bad(fields.combHard)) return { error: 'ErrPct' };
        if (bad(fields.min) || bad(fields.softMax) || bad(fields.hardMax)) return { error: 'ErrCount' };
        if (!v.legalEntityId || !v.skuId) return { error: 'ErrRuleKey' };
        return {
            input: {
                legalEntityId: v.legalEntityId, skuId: v.skuId, segmentCode: null,
                validFrom: v.validFrom || null, validTo: v.validTo || null,
                maxDiscountPct: { soft: fields.discSoft, hard: fields.discHard },
                maxMf: { soft: mfSoft, hard: mfHard },
                maxCombinedPct: { soft: fields.combSoft, hard: fields.combHard },
                quantity: { min: fields.min, softMax: fields.softMax, hardMax: fields.hardMax },
                expectedVersion: v.expectedVersion == null ? null : v.expectedVersion
            }
        };
    }

    /** One price list item row → PriceListItemInput, or { error }. At least one tier; VAT 0–100 (the CRM checks again). */
    function itemInput(v) {
        const depot = parseAmount(v.depot, 4), pharmacist = parseAmount(v.pharmacist, 4), pub = parseAmount(v.public, 4), vat = parsePct(v.vat);
        if (!v.skuId) return { error: 'ErrItemSku' };
        if (bad(depot) || bad(pharmacist) || bad(pub)) return { error: 'ErrPrice' };
        if (depot == null && pharmacist == null && pub == null) return { error: 'ErrNoTier' };
        if (vat == null || bad(vat)) return { error: 'ErrVat' };
        return { input: { itemId: v.itemId || null, skuId: v.skuId, depotPrice: depot, pharmacistPurchasePrice: pharmacist, publicPrice: pub, vatRate: vat } };
    }

    /** The trial form → PricingTrialInput, or { error }. The segment is not sent (the first version has none — AÇIK-3). */
    function trialInput(v) {
        const mf = P().parseMf(v.mf);
        const disc = parsePct(v.discount);
        const qty = parseCount(v.quantity);
        if (!v.legalEntityId) return { error: 'ErrTrialCompany' };
        if (!v.skuId) return { error: 'ErrItemSku' };
        if (qty == null || bad(qty) || qty < 1) return { error: 'ErrCount' };
        if (bad(mf)) return { error: 'ErrMf' };
        if (bad(disc)) return { error: 'ErrPct' };
        return { input: { legalEntityId: v.legalEntityId, countryCode: null, asOf: v.asOf || null, tier: v.tier || null, pharmacySegmentCode: null,
            lines: [{ skuId: v.skuId, quantity: qty, mf: mf, discountPct: disc }] } };
    }

    /** One change history row: the type, the action and each field's old → new (the CRM's text, shown as it is). */
    function historyRow(c) {
        const x = c || {};
        return {
            typeKey: HISTORY_TYPES.indexOf(x.entityType) >= 0 ? 'Type_' + x.entityType.replace(/-/g, '_') : 'Type_unknown',
            actionKey: HISTORY_ACTIONS.indexOf(x.action) >= 0 ? 'Act_' + x.action.replace(/-/g, '_') : 'Act_unknown',
            changes: (x.changes || []).map((f) => ({ field: f.field, old: f.old == null ? '' : String(f.old), new: f.new == null ? '' : String(f.new) }))
        };
    }

    const api = {
        BADGES: BADGES, RULE_STATUSES: RULE_STATUSES, KINDS: KINDS, TIERS: TIERS, HISTORY_TYPES: HISTORY_TYPES, HISTORY_ACTIONS: HISTORY_ACTIONS,
        badgeOf: badgeOf, ruleStatusOf: ruleStatusOf, listActions: listActions, ruleActions: ruleActions, importSummary: importSummary,
        parsePct: parsePct, parseCount: parseCount, parseAmount: parseAmount, ruleInput: ruleInput, itemInput: itemInput, trialInput: trialInput,
        historyRow: historyRow
    };
    root.PricingCore = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
