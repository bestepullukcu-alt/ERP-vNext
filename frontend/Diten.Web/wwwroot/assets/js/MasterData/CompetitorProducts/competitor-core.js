/**
 * WP-VW-W4 (W4-WEB-c) — the competitor products pages' PURE rules (no DOM, no fetch, no clock).
 * Browser: window.CompetitorCore. Node: module.exports (the Web tests).
 *
 *   recordActions(record, perms)          the buttons a record offers (status × mdm.competitor-products.manage)
 *   isOwn(decision, actor)                the author / last editor / submitter of a decision (SoD: never its approver)
 *   decisionState(decision)               the proposal card's state: waiting | draft | in_review | returned | applied (+ facts)
 *   decisionActions(decision, perms, actor)   prepare · submit · withdraw · approve · return (abilities × status × SoD)
 *   sodNote(decision, perms, actor)       "you prepared this; someone else approves in the Work Center"
 *   rejectReason(text) / reviewBody(decision, comment)    a rejection needs a reason (≤ 500); "return to draft" needs a note
 *   canAddMarket(rows, code) / addMarket(rows, code, currency)   the country table: a market only once
 *   pricePeriod(original, row)            a changed price / reimbursement / currency is a NEW PERIOD of the same row
 *   validate(form)                        the form's required fields (name, company, ≥ 1 ingredient, ≥ 1 of our products, markets)
 *   writeBody(form, version) / decisionBody(kind, input, version) / mergeBody(state, version)
 *   mergeCanAdvance(step, state) / mergeSubmitEnabled(state)    1 target · 2 compare · 3 effect + "I read it" (required)
 *   mergeDiffRows(source, target, label) / mergeMarketRows(source, target)   step 2: the field differences + the movable countries
 *   usageOf(usage, id) / countText(value) the CRM's aggregate counts ("<5" is shown as it comes — no small cell identifies a doctor)
 *   similarityTone(score)                 ≥ 80 "likely the same record"
 *   tags(text) / addTag(list, value)      the alias / ingredient chips (trimmed, case-insensitive once)
 *   errorText(errors, L) · fmt(template, …)
 */
(function (root, factory) {
    const api = factory();
    if (typeof module === 'object' && module.exports) { module.exports = api; }
    if (root) { root.CompetitorCore = api; }
}(typeof window !== 'undefined' ? window : null, function () {
    'use strict';

    const MAX_NAME = 200;
    const MAX_REASON = 500;
    const MAX_NOTE = 2000;
    const MAX_ALIASES = 30;
    const MAX_INGREDIENTS = 10;
    const MAX_MARKETS = 60;
    const LIKELY_SAME = 80;
    const STATUSES = ['proposed', 'active', 'inactive', 'merged', 'rejected'];
    const DECISIONS = ['complete', 'merge', 'reject'];

    const clean = v => { const s = v == null ? '' : String(v).trim(); return s.length ? s : null; };
    const num = v => {
        if (v === null || v === undefined || v === '') { return null; }
        const n = Number(String(v).replace(/\s/g, '').replace(',', '.'));
        return isNaN(n) ? null : n;
    };
    const same = (a, b) => !!clean(a) && !!clean(b) && String(a).trim().toLowerCase() === String(b).trim().toLowerCase();

    // ── a record ──────────────────────────────────────────────────────────────────────────────────────────────
    /** view always; edit an active / inactive record; deactivate and merge an ACTIVE one — all with manage. */
    const recordActions = (record, perms) => {
        const out = ['view'];
        const status = record && record.status;
        if (!perms || !perms.manage) { return out; }
        if (status === 'active' || status === 'inactive') { out.push('edit'); }
        if (status === 'active') { out.push('deactivate', 'merge'); }
        return out;
    };

    const STATUS_TONES = { proposed: 'warning', active: 'success', inactive: 'secondary', merged: 'info', rejected: 'danger' };
    const statusTone = s => STATUS_TONES[s] || 'secondary';

    // ── the proposal decision (Workflow, G1) ──────────────────────────────────────────────────────────────────
    const openRound = d => ((d && d.reviewRounds) || []).filter(r => !r.outcome && !r.closedAt).pop() || null;
    const lastRound = d => ((d && d.reviewRounds) || []).slice().sort((a, b) => (a.roundNo || 0) - (b.roundNo || 0)).pop() || null;

    /** The author, the last editor or the submitter of the open (else the latest) round. */
    const isOwn = (decision, actor) => {
        if (!decision || !clean(actor)) { return false; }
        const round = openRound(decision) || lastRound(decision);
        return same(actor, decision.createdBy) || same(actor, decision.updatedBy) || same(actor, decision.submittedBy)
            || same(actor, round && round.submittedBy);
    };

    /**
     * The card: no decision → waiting; draft → draft (its action); in_review → in_review (who, when); returned → returned
     * (the note: the reviewer's "return" comment, the SoD refusal, or a withdrawal); applied → applied (who, when).
     */
    const decisionState = decision => {
        if (!decision) { return { key: 'waiting' }; }
        const status = decision.status;
        if (status === 'in_review') {
            const r = openRound(decision);
            return { key: 'in_review', action: decision.action, by: decision.submittedBy || (r && r.submittedBy), at: decision.submittedAt || (r && r.submittedAt) };
        }
        if (status === 'applied') { return { key: 'applied', action: decision.action, by: decision.appliedBy, at: decision.appliedAt }; }
        if (status === 'returned') {
            const r = lastRound(decision);
            const back = ((decision.decisions || []).filter(x => x.outcome === 'return' && (!r || x.roundNo === r.roundNo)).pop()) || null;
            const reason = r && r.reasonCode && /_sod$/.test(r.reasonCode) ? 'sod' : r && r.outcome === 'cancelled' ? 'withdrawn' : back ? 'returned' : 'returned';
            return { key: 'returned', action: decision.action, reason: reason, note: back ? back.comment || null : null, by: back ? back.by : (r && r.completedBy) || null, at: back ? back.at : (r && r.closedAt) || null };
        }
        return { key: 'draft', action: decision.action };
    };

    /**
     * The buttons of a proposal's decision. An ability alone never draws a button: the status must allow it, and the
     * approver's pair (approve / return) is NEVER offered to the decision's author / editor / submitter (SoD) — even if a
     * stale ability said so.
     */
    const decisionActions = (decision, perms, actor) => {
        const p = perms || {};
        if (!p.manage) { return []; }
        const out = [];
        const status = decision ? decision.status : null;
        const ab = (decision && decision.abilities) || {};
        if (!decision || status === 'draft' || status === 'returned') { out.push('prepare'); }
        if (decision && (status === 'draft' || status === 'returned') && ab.canSubmit) { out.push('submit'); }
        if (decision && status === 'in_review' && ab.canWithdraw) { out.push('withdraw'); }
        if (decision && status === 'in_review' && ab.canDecide && !isOwn(decision, actor)) { out.push('approve', 'return'); }
        return out;
    };

    const sodNote = (decision, perms, actor) => !!decision && decision.status === 'in_review' && !!(perms && perms.manage) && isOwn(decision, actor);

    const rejectReason = text => {
        const v = clean(text);
        if (!v) { return { ok: false, error: 'required' }; }
        if (v.length > MAX_REASON) { return { ok: false, error: 'too_long' }; }
        return { ok: true, value: v };
    };

    /** The approver's decision: approve, or return to draft WITH a note. */
    const reviewBody = (decision, comment) => {
        if (decision !== 'approve' && decision !== 'return') { return { ok: false, error: 'decision_invalid' }; }
        const note = clean(comment);
        if (decision === 'return' && !note) { return { ok: false, error: 'comment_required' }; }
        if (note && note.length > MAX_NOTE) { return { ok: false, error: 'too_long' }; }
        return { ok: true, body: { decision: decision, comment: note } };
    };

    // ── the country table ─────────────────────────────────────────────────────────────────────────────────────
    const code = c => String(c || '').trim().toUpperCase();
    const canAddMarket = (rows, market) => /^[A-Z]{2}$/.test(code(market)) && !(rows || []).some(r => code(r.marketCode) === code(market)) && (rows || []).length < MAX_MARKETS;

    /** A new row (today, unknown reimbursement) — or null when the market is already there / not a code. */
    const addMarket = (rows, market, currency, today) => {
        if (!canAddMarket(rows, market)) { return null; }
        const list = (rows || []).slice();
        list.push({ marketCode: code(market), boxPrice: null, currencyCode: code(currency) || '', reimbursementStatusCode: 'unknown', validFrom: today || null, source: null, isNew: true });
        return list;
    };

    /**
     * A stored row whose price, currency or reimbursement changed opens a NEW PERIOD of the same row (the server moves the
     * old one into the history); the start date must move forward. → null when nothing priced changed.
     */
    const pricePeriod = (original, row) => {
        if (!original || !row) { return null; }
        const changed = num(original.boxPrice) !== num(row.boxPrice) || code(original.currencyCode) !== code(row.currencyCode)
            || String(original.reimbursementStatusCode || '') !== String(row.reimbursementStatusCode || '');
        if (!changed) { return null; }
        const from = String(original.validFrom || '').slice(0, 10);
        const to = String(row.validFrom || '').slice(0, 10);
        return { from: num(original.boxPrice), to: num(row.boxPrice), dateMustMove: !to || to <= from };
    };

    // ── chips ─────────────────────────────────────────────────────────────────────────────────────────────────
    const addTag = (list, value, max) => {
        const v = clean(value);
        const out = (list || []).slice();
        if (!v || out.some(x => same(x, v)) || out.length >= (max || MAX_ALIASES)) { return out; }
        out.push(v);
        return out;
    };
    const tags = text => String(text || '').split(/[,;\n]/).reduce((acc, t) => addTag(acc, t, 999), []);

    // ── the form ──────────────────────────────────────────────────────────────────────────────────────────────
    /** → { ok, errors: { name?, company?, ingredients?, products?, markets?: 'duplicate' | 'price' | 'currency' } } */
    const validate = form => {
        const f = form || {};
        const e = {};
        const name = clean(f.name);
        if (!name) { e.name = 'required'; } else if (name.length > MAX_NAME) { e.name = 'too_long'; }
        if (!clean(f.company)) { e.company = 'required'; }
        if (!(f.activeIngredients || []).length) { e.ingredients = 'required'; }
        if (!(f.competesWithGlobalProductIds || []).length) { e.products = 'required'; }
        const rows = f.countryPrices || [];
        const seen = {};
        rows.forEach(r => {
            const c = code(r.marketCode);
            if (seen[c]) { e.markets = 'duplicate'; }
            seen[c] = true;
            if (!e.markets && (num(r.boxPrice) === null || num(r.boxPrice) < 0)) { e.markets = 'price'; }
            if (!e.markets && !/^[A-Z]{3}$/.test(code(r.currencyCode))) { e.markets = 'currency'; }
        });
        return { ok: Object.keys(e).length === 0, errors: e };
    };

    const writeBody = (form, version) => {
        const f = form || {};
        return {
            name: clean(f.name), aliases: (f.aliases || []).slice(), company: clean(f.company), activeIngredients: (f.activeIngredients || []).slice(),
            formCode: clean(f.formCode), strength: clean(f.strength), atcCode: clean(f.atcCode),
            competesWithGlobalProductIds: (f.competesWithGlobalProductIds || []).slice(), indicationNodeIds: (f.indicationNodeIds || []).slice(),
            countryPrices: (f.countryPrices || []).map(r => ({
                marketCode: code(r.marketCode), boxPrice: num(r.boxPrice), currencyCode: code(r.currencyCode),
                reimbursementStatusCode: r.reimbursementStatusCode || 'unknown', validFrom: r.validFrom ? String(r.validFrom).slice(0, 10) : null, source: clean(r.source)
            })),
            expectedVersion: version == null ? null : version
        };
    };

    /** The decision draft: complete (the form) · merge (target + countries) · reject (reason). */
    const decisionBody = (kind, input, version) => {
        const i = input || {};
        if (DECISIONS.indexOf(kind) < 0) { return { ok: false, error: 'decision_invalid' }; }
        const body = { action: kind, complete: null, targetId: null, moveMarketCodes: null, reason: null, expectedVersion: version == null ? null : version };
        if (kind === 'complete') {
            const v = validate(i.form);
            if (!v.ok) { return { ok: false, error: 'invalid', errors: v.errors }; }
            body.complete = writeBody(i.form, null);
            delete body.complete.expectedVersion;
        } else if (kind === 'merge') {
            if (!i.targetId) { return { ok: false, error: 'target_required' }; }
            if (!i.ack) { return { ok: false, error: 'ack_required' }; }
            body.targetId = i.targetId;
            body.moveMarketCodes = (i.moveMarketCodes || []).map(code);
        } else {
            const r = rejectReason(i.reason);
            if (!r.ok) { return { ok: false, error: 'reason_' + r.error }; }
            body.reason = r.value;
        }
        return { ok: true, body: body };
    };

    // ── merge (3 steps) ───────────────────────────────────────────────────────────────────────────────────────
    /** 1 → 2 needs a target; 2 → 3 always; on 3 the send needs "I read the effect". */
    const mergeCanAdvance = (step, state) => {
        const s = state || {};
        if (step === 1) { return !!s.targetId; }
        if (step === 2) { return !!s.targetId; }
        return false;
    };
    const mergeSubmitEnabled = state => !!(state && state.targetId && state.ack === true);

    /** Direct merge of two active records (no approval): the target + the countries + confirm. */
    const mergeBody = (state, version) => (mergeSubmitEnabled(state)
        ? { ok: true, body: { targetId: state.targetId, moveMarketCodes: (state.moveMarketCodes || []).map(code), confirm: true, expectedVersion: version == null ? null : version } }
        : { ok: false, error: state && state.targetId ? 'ack_required' : 'target_required' });

    const joined = v => (Array.isArray(v) ? v.join(', ') : (v == null ? '' : String(v)));
    /** Step 2: the survivor's values are kept; a different field is marked. */
    const mergeDiffRows = (source, target, label) => {
        const s = source || {}, t = target || {};
        const lab = label || (k => k);
        return [['name', s.name, t.name], ['company', s.company, t.company], ['activeIngredients', joined(s.activeIngredients), joined(t.activeIngredients)],
            ['formCode', s.formCode, t.formCode], ['strength', s.strength, t.strength], ['atcCode', s.atcCode, t.atcCode], ['aliases', joined(s.aliases), joined(t.aliases)]]
            .map(([f, a, b]) => ({ field: f, label: lab(f), a: a || '', b: b || '', diff: String(a || '').trim().toLowerCase() !== String(b || '').trim().toLowerCase() }));
    };

    /** Step 2: a country only the source has is movable; a country both have keeps the survivor's row. */
    const mergeMarketRows = (source, target) => {
        const s = (source && source.countryPrices) || [], t = (target && target.countryPrices) || [];
        const codes = [];
        s.concat(t).forEach(r => { const c = code(r.marketCode); if (codes.indexOf(c) < 0) { codes.push(c); } });
        return codes.map(c => {
            const a = s.find(r => code(r.marketCode) === c) || null;
            const b = t.find(r => code(r.marketCode) === c) || null;
            return { code: c, source: a, target: b, movable: !!a && !b };
        });
    };

    // ── usage (CRM aggregate, G2) ─────────────────────────────────────────────────────────────────────────────
    const usageOf = (usage, id) => (((usage && usage.items) || []).find(x => x.competitorProductId === id)) || null;
    /** A count as the CRM gives it: a number, or "<5" (never turned into a number). */
    const countText = v => (v === null || v === undefined || v === '' ? '0' : String(v));

    const similarityTone = score => (Number(score) >= LIKELY_SAME ? 'likely' : 'other');

    // ── words ─────────────────────────────────────────────────────────────────────────────────────────────────
    const errorText = (errors, L) => {
        const l = L || {};
        const list = errors || [];
        for (let i = 0; i < list.length; i++) {
            const t = l['Err_' + list[i]];
            if (t) { return t; }
        }
        return l.ActionFailed || '';
    };

    function fmt(template) {
        const args = Array.prototype.slice.call(arguments, 1);
        return String(template == null ? '' : template).replace(/\{(\d+)\}/g, (m, i) => (args[i] == null ? '' : String(args[i])));
    }

    return {
        MAX_NAME, MAX_REASON, MAX_NOTE, MAX_ALIASES, MAX_INGREDIENTS, MAX_MARKETS, LIKELY_SAME, STATUSES, DECISIONS,
        recordActions, statusTone, isOwn, decisionState, decisionActions, sodNote, rejectReason, reviewBody,
        canAddMarket, addMarket, pricePeriod, addTag, tags, validate, writeBody, decisionBody,
        mergeCanAdvance, mergeSubmitEnabled, mergeBody, mergeDiffRows, mergeMarketRows,
        usageOf, countText, similarityTone, errorText, fmt
    };
}));
