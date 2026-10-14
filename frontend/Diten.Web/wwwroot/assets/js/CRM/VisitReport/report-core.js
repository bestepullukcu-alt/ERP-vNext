/**
 * WP-VW-W4 (W4-WEB-a) — the visit report v2's PURE rules (no DOM, no fetch, no clock), shared by the doctor report
 * (WEB-a) and the pharmacy report (WEB-b ADDS its pharmacy functions here; the signatures below stay as they are).
 * Browser: window.VisitReportCore. Node: module.exports (the Web tests).
 *
 *   optionLabel(option, lang) / options(prefill, set, lang)   a BRD value in the page language (label_xx, else display name, else code)
 *   bodyOf(prefill)                       the editable body (a deep copy of the pre-filled / stored v2 body)
 *   contextOf(prefill)                    the completeness context (planned products, due potentials / total, listed competitors)
 *   indicationTotal(potential)            the patients of a product's indication rows
 *   doctorTotal(body, prefill)            the doctor's monthly total (this report, else the profile)
 *   hasIndications(ctx, productId)        the product has approved indication × profile rows (W4-FIX-b, F-5)
 *   potentialAsked(section, ctx)          the product has rows AND the numbers are due (the server's pre-fill flag
 *                                         productSections[].potentialAsked) or the rep pressed "update"
 *   totalRule(body, prefill, ctx)         the products whose indications exceed the doctor's total (only when both are known)
 *   potentialClass(total, threshold)      low < lowBelow ≤ medium ≤ highAbove < high
 *   opportunity(potentialClass, share)    THE matrix (§2.3) — preview only, the server's value is the record
 *   preview(section, prefill, ctx)        { total, potentialClass, opportunity, server } for the B card
 *   suggestionKey(commitment, stage)      commitment:{code} | stage:{next} | null (the server rule, to show it before a save)
 *   suggestionText(key, L, stageLabel)    the key in the rep's words
 *   missing(body, ctx, extra)             the server's completeness rules + the total rule + the amendment reason
 *   missingDoctor(body, ctx, need)        the doctor part (products, doctor level, off-label)
 *   steps(kind, body, names)              the wizard steps (general → products → safety → doctor → summary)
 *   stepOf(missingRow, kind)              the step a missing answer jumps to
 *   missingByStep(rows, kind)             { stepKey: count }
 *   editWindow(submittedAt, nowMs)        the 60-minute free correction ({ open, minutesLeft })
 *   hoursLeft(deadline, nowMs)            the report deadline chip
 *   cycleAdvantage(competitor, code)      none → + (advantage) → − (disadvantage) → none
 *   toggleCompetitor(section, option)     select / deselect an MDM competitor (clears "uses none")
 *   addProposal(section, name, company, answer)   the rep's "not in the list" (the MDM answer, or kept for the submit)
 *   libraryAnswers(presentation, productId, category)   the W3 objection library answers of a product + category
 *   gskuOptions(items, productId)         the product's GSKUs for the sample
 *   changesOf(amendments)                 the amendment rows "path: old → new"
 *   requestBody(plannedVisitId, body, version)            the v2 draft / submit body
 *   amendBody(reason, plannedVisitId, body, version)      the v2 amendment body
 *   errorText(errors, L)                  an API refusal in the user's language
 */
(function (root, factory) {
    const api = factory();
    if (typeof module === 'object' && module.exports) { module.exports = api; }
    if (root) { root.VisitReportCore = api; }
}(typeof window !== 'undefined' ? window : null, function () {
    'use strict';

    const EDIT_WINDOW_MINUTES = 60;
    const MIN_MINUTES = 1;
    const MAX_MINUTES = 240;
    const MAX_NEXT_MESSAGE = 500;
    const MAX_GENERAL_NOTE = 4000;
    const MAX_SAMPLE = 100;
    const ADOPTION_STAGES = ['unaware', 'aware', 'evaluating', 'trying', 'regular', 'advocate'];
    const SHARE_BUCKETS = ['none', 'occasional', 'half', 'mostly', 'always'];
    const RESOLUTIONS = ['yes', 'partly', 'no'];
    const KINDS = ['in_person', 'remote'];
    const SETS = {
        visitSuccess: 'crm-visit-success', commitment: 'crm-doctor-commitment', interest: 'crm-visit-interest',
        requestType: 'crm-visit-request-type', advantage: 'crm-competitor-advantage', adoptionStage: 'crm-adoption-stage',
        shareBucket: 'crm-share-bucket', typology: 'crm-adoption-typology', style: 'crm-communication-style',
        kol: 'crm-kol-level', objection: 'visit-objection-category', reimbursement: 'mdm-reimbursement-status'
    };

    const has = v => v !== null && v !== undefined && String(v).trim() !== '';
    const num = v => (v === null || v === undefined || v === '' || isNaN(Number(v)) ? null : Number(v));
    const copy = v => (v === undefined ? undefined : JSON.parse(JSON.stringify(v)));
    const pick = (o, a, b) => (o ? (o[a] !== undefined ? o[a] : o[b]) : undefined);

    // ── options ───────────────────────────────────────────────────────────────────────────────────────────────
    /** A BRD value in the page language: attributes.label_{lang}, else the display name, else the code. */
    const optionLabel = (option, lang) => {
        if (!option) { return ''; }
        const labels = option.labels || option.attributes || {};
        const l = String(lang || '').toLowerCase().split('-')[0];
        const own = l && l !== 'en' ? (labels['label_' + l] || labels['Label_' + l]) : null;
        return own || option.label || option.displayName || option.code || '';
    };

    const options = (prefill, set, lang) => ((prefill && prefill.optionSets && prefill.optionSets[set]) || [])
        .map(o => ({ code: o.code, label: optionLabel(o, lang) }));

    // ── the body + the context ────────────────────────────────────────────────────────────────────────────────
    const emptySection = (productId, unplanned) => ({
        productId: productId, unplanned: !!unplanned, presentedSteps: [], matchedPlan: null, minutes: null,
        visitSuccessCode: null, commitmentCode: null, interestCode: null, adoptionStageCode: null, potential: null,
        shareBucketCode: null, competitors: [], noCompetitor: false, objections: [], noObjection: false,
        requests: [], noRequest: false, sample: null, noSample: false, nextMessage: null, nextMessageSuggestion: null
    });

    /** The editable body: the stored (or pre-filled) v2 body, every list present. */
    const bodyOf = prefill => {
        const b = copy((prefill && prefill.body) || {}) || {};
        b.general = Object.assign({ visitKindCode: 'in_person', coVisitorUserIds: [], metContactId: null, metRoleCode: null, generalNote: null }, b.general || {});
        b.productSections = (b.productSections || []).map(s => Object.assign(emptySection(s.productId, s.unplanned), s, {
            competitors: s.competitors || [], objections: s.objections || [], requests: s.requests || [], presentedSteps: s.presentedSteps || []
        }));
        b.safety = Object.assign({ adverseEvent: null, adverseEventNote: null, offLabelRequest: null }, b.safety || {});
        b.followUp = Object.assign({ required: false, suggestedDate: null, note: null }, b.followUp || {});
        if (prefill && prefill.reportKind !== 'pharmacy') {
            b.doctor = Object.assign({ adoptionTypologyCode: null, communicationStyleCode: null, kolLevelCode: null, totalMonthlyPatients: null }, b.doctor || {});
        }
        return b;
    };

    /** The completeness context the server builds from the plan, the potentials and the MDM lookup — from the pre-fill. */
    const contextOf = prefill => {
        const p = prefill || {};
        const sections = (p.body && p.body.productSections) || [];
        const potentials = p.potentials || [];
        const profile = p.profile || null;
        // W4-FIX-b (F-5): the server says per product whether the potential is asked (potentialAsked) — the client does not
        // recompute it; a server without the flag (older pre-fill) falls back to "due".
        const flagged = sections.some(s => typeof s.potentialAsked === 'boolean');
        return {
            kind: p.reportKind || 'doctor',
            plannedProductIds: sections.filter(s => !s.unplanned).map(s => s.productId),
            duePotentialProductIds: flagged
                ? sections.filter(s => s.potentialAsked === true).map(s => s.productId)
                : potentials.filter(x => x.isDue).map(x => x.productId),
            indicationProductIds: Array.isArray(p.indicationRows)
                ? p.indicationRows.filter(x => x && (x.items || []).length > 0).map(x => x.productId)
                : null,
            totalDue: p.reportKind === 'pharmacy' ? false : (profile ? !!profile.totalIsDue : true),
            competitorListedProductIds: (p.competitors || []).filter(c => c.available).map(c => c.productId),
            pharmacyCompetitorsListed: !!(p.competitors || []).length && (p.competitors || []).some(c => c.available)
        };
    };

    // ── potential, the total rule, the matrix ─────────────────────────────────────────────────────────────────
    const indicationTotal = potential => {
        if (!potential || potential.noPatientsInIndications === true) { return potential && potential.noPatientsInIndications === true ? 0 : null; }
        const rows = potential.rows || [];
        return rows.length ? rows.reduce((a, r) => a + (num(r.monthlyPatients) || 0), 0) : null;
    };

    const doctorTotal = (body, prefill) => {
        const own = body && body.doctor ? num(body.doctor.totalMonthlyPatients) : null;
        if (own !== null) { return own; }
        return prefill && prefill.profile ? num(prefill.profile.totalMonthlyPatients) : null;
    };

    /** Approved indication × profile rows for the product? (no list in the context = unknown ⇒ yes, the old rule). */
    const hasIndications = (ctx, productId) => !ctx || !Array.isArray(ctx.indicationProductIds) || ctx.indicationProductIds.indexOf(productId) > -1;

    /** F-5: without indication rows there is nothing to answer — no question, no "no patients", no requirement. */
    const potentialAsked = (section, ctx) => !!section && hasIndications(ctx, section.productId)
        && ((ctx && (ctx.duePotentialProductIds || []).indexOf(section.productId) > -1) || !!(section.potential && section.potential.updated));

    const storedPotential = (prefill, productId) => ((prefill && prefill.potentials) || []).find(x => x.productId === productId) || null;

    /**
     * The doctor's total may not be exceeded (only when both are known): a product whose numbers are asked counts its rows,
     * a product not asked counts its stored total (a lower total may not fall below it). → [{ productId, sum, total, asked }]
     */
    const totalRule = (body, prefill, ctx) => {
        const total = doctorTotal(body, prefill);
        if (total === null) { return []; }
        const out = [];
        ((body && body.productSections) || []).forEach(s => {
            const asked = potentialAsked(s, ctx);
            const stored = storedPotential(prefill, s.productId);
            const sum = asked ? indicationTotal(s.potential) : (stored ? num(stored.indicationTotal) : null);
            if (sum !== null && sum > total) { out.push({ productId: s.productId, sum: sum, total: total, asked: asked }); }
        });
        return out;
    };

    const potentialClass = (total, threshold) => {
        const t = num(total);
        if (t === null || !threshold) { return null; }
        const low = num(pick(threshold, 'lowBelow', 'LowBelow'));
        const high = num(pick(threshold, 'highAbove', 'HighAbove'));
        if (low === null || high === null) { return null; }
        return t < low ? 'low' : t > high ? 'high' : 'medium';
    };

    /** §2.3 — the same table as the server (OpportunityRules.Classify); the server's answer is the record. */
    const opportunity = (cls, share) => {
        if (!has(cls) || !has(share)) { return null; }
        const s = String(share).trim().toLowerCase();
        if (SHARE_BUCKETS.indexOf(s) < 0) { return null; }
        if (s === 'mostly' || s === 'always') { return 'keep'; }
        const lowShare = s === 'none' || s === 'occasional';
        switch (String(cls).trim().toLowerCase()) {
            case 'high': return lowShare ? 'opportunity' : 'grow';
            case 'medium': return 'grow';
            case 'low': return 'low_priority';
            default: return null;
        }
    };

    /** The B card's computed line: the numbers given now, else the stored ones (the server's class + matrix). */
    const preview = (section, prefill, ctx) => {
        const stored = storedPotential(prefill, section && section.productId);
        const asked = potentialAsked(section, ctx);
        const threshold = stored ? stored.threshold : null;
        if (section && !hasIndications(ctx, section.productId)) {
            // no indication list ⇒ no potential: the preview is "—" (the share alone has no class)
            return { total: null, potentialClass: null, opportunity: null, threshold: threshold, asked: false, server: null };
        }
        const total = asked ? indicationTotal(section.potential) : (stored ? num(stored.indicationTotal) : null);
        const cls = asked ? potentialClass(total, threshold) : (stored ? stored.potentialClass || null : null);
        const share = section ? section.shareBucketCode : null;
        return { total: total, potentialClass: cls, opportunity: opportunity(cls, share), threshold: threshold, asked: asked,
            server: stored ? stored.opportunityCode || null : null };
    };

    // ── the next-message suggestion ───────────────────────────────────────────────────────────────────────────
    const suggestionKey = (commitment, stage) => {
        if (commitment === 'will_try' || commitment === 'will_prescribe' || commitment === 'wants_info') { return 'commitment:' + commitment; }
        const rank = ADOPTION_STAGES.indexOf(String(stage || '').trim().toLowerCase());
        return rank >= 0 && rank < ADOPTION_STAGES.length - 1 ? 'stage:' + ADOPTION_STAGES[rank + 1] : null;
    };

    /** commitment:x → the 'Suggest_commitment_' key; stage:y → the SuggestStage key with the stage's label. */
    const suggestionText = (key, L, stageLabel) => {
        if (!key) { return ''; }
        const l = L || {};
        const i = String(key).indexOf(':');
        const kind = String(key).slice(0, i), code = String(key).slice(i + 1);
        if (kind === 'commitment') { return l['Suggest_commitment_' + code] || ''; }
        if (kind === 'stage') { return fmt(l.SuggestStage || '{0}', typeof stageLabel === 'function' ? stageLabel(code) : (stageLabel || code)); }
        return '';
    };

    // ── completeness ──────────────────────────────────────────────────────────────────────────────────────────
    /**
     * The server's rules (VisitReportV2Rules.Missing) on the editable body + what the server only says later (the total
     * rule from the doctor insights recorder) + the amendment's reason. extra = { prefill, amend, reason }.
     * → [{ section, productId, field }]
     */
    const missing = (body, ctx, extra) => {
        const out = [];
        const need = (ok, section, productId, field) => { if (!ok) { out.push({ section: section, productId: productId || null, field: field }); } };
        const b = body || {};
        const c = ctx || {};
        const g = b.general || {};
        const safety = b.safety || {};
        need(KINDS.indexOf(g.visitKindCode) > -1, 'general', null, 'visitKindCode');
        need(safety.adverseEvent === true || safety.adverseEvent === false, 'safety', null, 'adverseEvent');
        need(safety.adverseEvent !== true || has(safety.adverseEventNote), 'safety', null, 'adverseEventNote');
        if (c.kind === 'pharmacy') {
            if (typeof api.missingPharmacy === 'function') { api.missingPharmacy(b, c, need); } // WEB-b
        } else {
            missingDoctor(b, c, need);
            totalRule(b, extra && extra.prefill, c).forEach(r => need(false, r.asked ? 'product' : 'doctor', r.asked ? r.productId : null,
                r.asked ? 'potentialExceedsTotal' : 'totalBelowProducts'));
        }
        if (extra && extra.amend) { need(has(extra.reason), 'summary', null, 'amendReason'); }
        return out;
    };

    const missingDoctor = (b, c, need) => {
        const g = b.general || {};
        const safety = b.safety || {};
        need(has(g.metContactId), 'general', null, 'metContactId');
        need(safety.offLabelRequest === true || safety.offLabelRequest === false, 'safety', null, 'offLabelRequest');
        need(!c.totalDue || (b.doctor && num(b.doctor.totalMonthlyPatients) !== null), 'doctor', null, 'totalMonthlyPatients');
        const sections = b.productSections || [];
        (c.plannedProductIds || []).filter(p => !sections.some(s => s.productId === p)).forEach(p => need(false, 'product', p, 'section'));
        sections.forEach(s => {
            const id = s.productId;
            const m = num(s.minutes);
            need(m !== null && m >= MIN_MINUTES && m <= MAX_MINUTES, 'product', id, 'minutes');
            need(has(s.visitSuccessCode), 'product', id, 'visitSuccessCode');
            need(has(s.commitmentCode), 'product', id, 'commitmentCode');
            need(has(s.interestCode), 'product', id, 'interestCode');
            need(has(s.adoptionStageCode), 'product', id, 'adoptionStageCode');
            need(has(s.shareBucketCode), 'product', id, 'shareBucketCode');
            const asked = potentialAsked(s, c);
            need(!asked || (s.potential && (s.potential.noPatientsInIndications === true || (s.potential.rows || []).length > 0)), 'product', id, 'potential');
            need((c.competitorListedProductIds || []).indexOf(id) < 0 || (s.competitors || []).length > 0 || !!s.noCompetitor, 'product', id, 'competitors');
            need((s.objections || []).length > 0 || !!s.noObjection, 'product', id, 'objections');
            need((s.requests || []).length > 0 || !!s.noRequest, 'product', id, 'requests');
            need(!!s.sample || !!s.noSample, 'product', id, 'sample');
            need(has(s.nextMessage), 'product', id, 'nextMessage');
            (s.objections || []).forEach(o => {
                need(has(o.categoryCode), 'product', id, 'objections.categoryCode');
                need(RESOLUTIONS.indexOf(o.resolved) > -1, 'product', id, 'objections.resolved');
            });
            (s.requests || []).forEach(r => need(has(r.typeCode), 'product', id, 'requests.typeCode'));
        });
    };

    // ── the steps ─────────────────────────────────────────────────────────────────────────────────────────────
    /** The doctor report: general → one step per product → follow-up + safety → the doctor profile card → summary. */
    const steps = (kind, body, names) => {
        if (kind === 'pharmacy' && typeof api.pharmacySteps === 'function') { return api.pharmacySteps(body, names); } // WEB-b
        const out = [{ key: 'general' }];
        ((body && body.productSections) || []).forEach(s => out.push({
            key: 'p:' + s.productId, productId: s.productId, unplanned: !!s.unplanned,
            name: (names && names[s.productId]) || null
        }));
        out.push({ key: 'safety' });
        if (kind !== 'pharmacy') { out.push({ key: 'doctor' }); }
        out.push({ key: 'summary' });
        return out;
    };

    const stepOf = (row, kind) => {
        if (!row) { return 'summary'; }
        if (kind === 'pharmacy' && typeof api.pharmacyStepOf === 'function') { return api.pharmacyStepOf(row); } // WEB-b
        if (row.section === 'product' && row.productId) { return 'p:' + row.productId; }
        if (row.section === 'general' || row.section === 'safety' || row.section === 'doctor') { return row.section; }
        return 'summary';
    };

    const missingByStep = (rows, kind) => {
        const out = {};
        (rows || []).forEach(r => { const k = stepOf(r, kind); out[k] = (out[k] || 0) + 1; });
        return out;
    };

    /** The A / B / C counts of a product step (phone header + the group chips). */
    const GROUP_OF = {
        minutes: 'a', visitSuccessCode: 'a', commitmentCode: 'a', interestCode: 'a', adoptionStageCode: 'a',
        potential: 'b', potentialExceedsTotal: 'b', shareBucketCode: 'b', competitors: 'b'
    };
    const groupCounts = (rows, productId) => {
        const out = { a: 0, b: 0, c: 0 };
        (rows || []).filter(r => r.section === 'product' && r.productId === productId && r.field !== 'section')
            .forEach(r => { out[GROUP_OF[r.field] || 'c']++; });
        return out;
    };

    // ── time ──────────────────────────────────────────────────────────────────────────────────────────────────
    /** The 60-minute free correction after the submit (the server's rule; the server decides). */
    const editWindow = (submittedAt, nowMs) => {
        const at = Date.parse(submittedAt || '');
        if (isNaN(at)) { return { open: false, minutesLeft: 0 }; }
        const left = EDIT_WINDOW_MINUTES - Math.floor((nowMs - at) / 60000);
        return left > 0 && nowMs >= at ? { open: true, minutesLeft: Math.min(left, EDIT_WINDOW_MINUTES) } : { open: false, minutesLeft: 0 };
    };

    const hoursLeft = (deadline, nowMs) => {
        const end = Date.parse(deadline || '');
        if (isNaN(end)) { return null; }
        return Math.max(0, Math.floor((end - nowMs) / 3600000));
    };

    const minutesBetween = (from, to) => {
        const a = Date.parse(from || ''), b = Date.parse(to || '');
        return isNaN(a) || isNaN(b) || b < a ? null : Math.round((b - a) / 60000);
    };

    // ── competitors, objections, samples ──────────────────────────────────────────────────────────────────────
    /** One tap: the advantage (+); two: the disadvantage (−); three: neither. (S2: optional.) */
    const cycleAdvantage = (competitor, code) => {
        const c = competitor;
        c.advantageCodes = c.advantageCodes || [];
        c.disadvantageCodes = c.disadvantageCodes || [];
        const plus = c.advantageCodes.indexOf(code), minus = c.disadvantageCodes.indexOf(code);
        if (plus > -1) { c.advantageCodes.splice(plus, 1); c.disadvantageCodes.push(code); return '-'; }
        if (minus > -1) { c.disadvantageCodes.splice(minus, 1); return ''; }
        c.advantageCodes.push(code);
        return '+';
    };

    const advantageOf = (competitor, code) => (competitor && (competitor.advantageCodes || []).indexOf(code) > -1 ? '+'
        : competitor && (competitor.disadvantageCodes || []).indexOf(code) > -1 ? '-' : '');

    const toggleCompetitor = (section, option) => {
        const list = section.competitors = section.competitors || [];
        const at = list.findIndex(c => c.competitorProductId === option.id);
        if (at > -1) { list.splice(at, 1); return false; }
        list.push({ competitorProductId: option.id, nameSnapshot: option.name || '', company: option.company || null,
            proposed: option.status === 'proposed', proposalForwarded: option.status === 'proposed', advantageCodes: [], disadvantageCodes: [] });
        section.noCompetitor = false;
        return true;
    };

    /** The rep's "not in the list": MDM answered (id, status, existing) — or not (kept with no id; the submit forwards it). */
    const addProposal = (section, name, company, answer) => {
        const n = String(name || '').trim(), f = String(company || '').trim();
        if (!n || !f) { return null; }
        const list = section.competitors = section.competitors || [];
        const id = answer && answer.id ? answer.id : null;
        if (id && list.some(c => c.competitorProductId === id)) { return list.find(c => c.competitorProductId === id); }
        const row = { competitorProductId: id, nameSnapshot: n, company: f,
            proposed: !answer || !answer.existing || answer.status === 'proposed', proposalForwarded: !!id,
            advantageCodes: [], disadvantageCodes: [] };
        if (answer && answer.existing && answer.status !== 'proposed') { row.proposed = false; }
        list.push(row);
        section.noCompetitor = false;
        return row;
    };

    /** The W3 library answers the presentation carried for a product (+ a category). */
    const libraryAnswers = (presentation, productId, category) => {
        const seen = {};
        const out = [];
        ((presentation && presentation.slides) || []).filter(s => s.productId === productId).forEach(s => (s.objections || []).forEach(o => {
            if (!o || !o.id || seen[o.id] || (has(category) && o.categoryCode !== category)) { return; }
            seen[o.id] = true;
            out.push({ id: o.id, categoryCode: o.categoryCode, question: o.question || '' });
        }));
        return out;
    };

    const gskuOptions = (items, productId) => (items || [])
        .filter(g => g && g.globalProductId === productId && String(g.lifecycleStatus || '').toLowerCase() !== 'retired')
        .map(g => ({ id: g.id, label: (g.globalProductName || '') + ' · ' + (g.packQuantity != null ? g.packQuantity : '') + ' ' + (g.packUomCode || '') + ' · ' + (g.canonicalCode || '') }));

    // ── amendments ────────────────────────────────────────────────────────────────────────────────────────────
    const shortJson = v => {
        if (v === null || v === undefined || v === 'null') { return '—'; }
        try { const x = JSON.parse(v); return typeof x === 'string' ? x : JSON.stringify(x); } catch (e) { return String(v); }
    };

    const changesOf = amendments => (amendments || []).map(a => ({
        at: a.at, by: a.byResourceId, reason: a.reason,
        changes: (a.changes || []).map(c => ({ path: c.path, old: shortJson(c.old), new: shortJson(c.new) }))
    }));

    // ── the request bodies ────────────────────────────────────────────────────────────────────────────────────
    const cleanSection = s => {
        const x = copy(s);
        delete x.presentedSteps; delete x.matchedPlan; delete x.nextMessageSuggestion; delete x.potentialAsked; // server members — recomputed
        x.minutes = num(x.minutes);
        if (x.potential) {
            x.potential.rows = (x.potential.rows || []).filter(r => num(r.monthlyPatients) !== null)
                .map(r => ({ indicationNodeId: r.indicationNodeId, profileNodeId: r.profileNodeId || null, monthlyPatients: num(r.monthlyPatients) }));
            if (x.potential.noPatientsInIndications !== true) { x.potential.noPatientsInIndications = null; }
        }
        if (x.sample) { x.sample = { gskuId: x.sample.gskuId, quantity: num(x.sample.quantity) || 0, lot: has(x.sample.lot) ? String(x.sample.lot).trim() : null }; }
        if (has(x.nextMessage)) { x.nextMessage = String(x.nextMessage).trim(); }
        x.objections = (x.objections || []).map(o => ({ categoryCode: o.categoryCode || null, objectionResponseId: o.objectionResponseId || null, note: has(o.note) ? o.note : null, resolved: o.resolved || null }));
        x.requests = (x.requests || []).map(r => ({ typeCode: r.typeCode || null, dueDate: has(r.dueDate) ? r.dueDate : null, note: has(r.note) ? r.note : null }));
        return x;
    };

    const requestBody = (plannedVisitId, body, version) => {
        const b = body || {};
        const out = {
            plannedVisitId: plannedVisitId,
            general: copy(b.general),
            productSections: (b.productSections || []).map(cleanSection),
            safety: copy(b.safety),
            followUp: copy(b.followUp),
            expectedVersion: version == null ? null : version
        };
        if (b.doctor) { out.doctor = copy(b.doctor); out.doctor.totalMonthlyPatients = num(out.doctor.totalMonthlyPatients); }
        if (b.pharmacy) { out.pharmacy = copy(b.pharmacy); }
        if (out.safety && out.safety.adverseEvent !== true) { out.safety.adverseEventNote = null; }
        return out;
    };

    const amendBody = (reason, plannedVisitId, body, version) => ({
        reason: String(reason || '').trim(), expectedVersion: version == null ? null : version,
        v2: requestBody(plannedVisitId, body, version)
    });

    // ── words ─────────────────────────────────────────────────────────────────────────────────────────────────
    const errorText = (errors, L) => {
        const l = L || {};
        const list = errors || [];
        for (let i = list.length - 1; i >= 0; i--) {
            const t = l['Err_' + list[i]];
            if (t) { return t; }
        }
        return l.ActionFailed || '';
    };

    function fmt(template) {
        const args = Array.prototype.slice.call(arguments, 1);
        return String(template == null ? '' : template).replace(/\{(\d+)\}/g, (m, i) => (args[i] == null ? '' : String(args[i])));
    }

    /** CT (W4 K13 I1): a pharmacy visit's report is its own page (W4-WEB-b) — the wizard hands the visit over to it. */
    const pharmacyReportUrl = (pharmacyBase, plannedVisitId) =>
        String(pharmacyBase || '/CRM/PharmacyReport').replace(/\/+$/, '') + '/' + encodeURIComponent(plannedVisitId || '');

    const api = {
        EDIT_WINDOW_MINUTES, MIN_MINUTES, MAX_MINUTES, MAX_NEXT_MESSAGE, MAX_GENERAL_NOTE, MAX_SAMPLE,
        ADOPTION_STAGES, SHARE_BUCKETS, RESOLUTIONS, KINDS, SETS,
        optionLabel, options, emptySection, bodyOf, contextOf,
        indicationTotal, doctorTotal, hasIndications, potentialAsked, storedPotential, totalRule, potentialClass, opportunity, preview,
        suggestionKey, suggestionText,
        missing, missingDoctor, steps, stepOf, missingByStep, groupCounts,
        editWindow, hoursLeft, minutesBetween,
        cycleAdvantage, advantageOf, toggleCompetitor, addProposal, libraryAnswers, gskuOptions,
        changesOf, requestBody, amendBody, errorText, fmt, pharmacyReportUrl
    };
    return api;
}));
