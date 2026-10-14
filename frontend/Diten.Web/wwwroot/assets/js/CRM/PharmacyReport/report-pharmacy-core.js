/**
 * WP-VW-W4 (W4-WEB-b) — the pharmacy visit report's PURE rules: no DOM, no fetch. Loaded before pharmacy-report.js; the
 * Web tests run these functions in Node. Texts are KEYS (the page resolves them through window.PharmacyReportL10n).
 * The CRM decides (report v2: kind, codes, completeness, the 60-minute rule); these rules only mirror it so the rep sees
 * what is missing BEFORE the submit — the server's missing[] always wins.
 *
 *   STEPS / stepOf(missing)            the mockup's six pharmacy steps; a missing answer → its step
 *   productIdsOf(prefill)              the plan's products (the pre-fill's competitor probes, in plan order)
 *   competitorsListed(prefill)         MDM answered for the first plan product ⇒ "competitor or none" is asked (server rule)
 *   gskuProductIds(pharmacy, map)      the global products of the stock then sales GSKUs, once each (W4-FIX-d G-9)
 *   draftOf(prefill)                   the editable body (every list / object present; OffLabelRequest never sent)
 *   seedRows(pharmacy, gskus, ids)     one stock + one sales row per GSKU of a plan product (existing rows kept)
 *   missing(body, ctx)                 the server's pharmacy completeness: AE (+ note), met role, visit kind, every stock
 *                                      row's level, every sales row's TREND (boxes / month optional), competitor or none
 *   proposalErrors(name, company)      a "not in the list" competitor needs a name and a company (≤ 200)
 *   toggleCompetitor / addProposal     the competitor chips; a proposal is { competitorProductId: null, proposed: true }
 *   requestBody(id, body, version)     the draft / submit body; amendBody(reason, …) the reasoned correction
 *   modeOf(prefill) / AMEND_CODE       draft | resubmit (finalised, the CRM decides the window) | the 409 → amend
 *   errorKey(response)                 a CRM refusal → the user's text key (never a raw code)
 *
 * The shared report core (W4-WEB-a, report-core.js) is reached ONLY through its extension point: when it is loaded this
 * file registers itself as the 'pharmacy' kind through its registerKind hook. Nothing here reads or changes report-core.js.
 */
(function (root, factory) {
    'use strict';
    const api = factory();
    if (typeof module === 'object' && module.exports) { module.exports = api; }
    if (root) {
        root.ReportPharmacyCore = api;
        if (root.ReportCore && typeof root.ReportCore.registerKind === 'function') { root.ReportCore.registerKind('pharmacy', api); }
    }
}(typeof window !== 'undefined' ? window : globalThis, function () {
    'use strict';

    const KIND = 'pharmacy';

    /** The mockup's pharmacy steps (03-rapor): Genel → Stok ve raf → Sipariş ve satış → Rakip ve tavsiye → Güvenlik → Özet. */
    const STEPS = ['general', 'stock', 'order', 'market', 'safety', 'summary'];

    /** The BRD sets of a pharmacy report (the pre-fill's optionSets). */
    const SETS = {
        metRole: 'crm-pharmacy-met-role', stockLevel: 'crm-pharmacy-stock-level', shelf: 'crm-shelf-position',
        trend: 'crm-sales-trend', recommendation: 'crm-recommendation-level', request: 'crm-visit-request-type',
        advantage: 'crm-competitor-advantage'
    };

    const MAX_NAME = 200;
    const MAX_NOTE = 4000;
    const MAX_REASON = 500;
    const MAX_BOXES = 100000;
    const MAX_REQUESTS = 50;

    const clone = v => JSON.parse(JSON.stringify(v == null ? null : v));
    const blank = v => v == null || String(v).trim() === '';
    const arr = v => (Array.isArray(v) ? v : []);

    // ── the plan ─────────────────────────────────────────────────────────────────────────────────────────────
    const productIdsOf = prefill => {
        const ids = [];
        arr(prefill && prefill.competitors).forEach(c => { if (c && c.productId && ids.indexOf(c.productId) < 0) { ids.push(c.productId); } });
        return ids;
    };

    /** The server asks "competitor or none" when MDM answered for the plan's FIRST product (VisitReportV2Service.ContextAsync). */
    const competitorsListed = prefill => {
        const first = arr(prefill && prefill.competitors)[0];
        return !!(first && first.available === true);
    };

    /** W4-FIX-d (G-9): the products of the stock rows, then the sales rows (GSKU → product map), once each, in that order —
     * the server probes the competitor list with the plan's first product, else the first of these. */
    const gskuProductIds = (pharmacy, map) => {
        const out = [];
        const m = map || {};
        arr(pharmacy && pharmacy.stock).concat(arr(pharmacy && pharmacy.sales)).forEach(r => {
            const id = r && m[r.gskuId];
            if (id && out.indexOf(id) < 0) { out.push(id); }
        });
        return out;
    };

    /** The MDM competitor options of every plan product, once each (id → item). */
    const competitorOptions = prefill => {
        const seen = {};
        const out = [];
        arr(prefill && prefill.competitors).forEach(c => arr(c && c.items).forEach(i => {
            if (i && i.id && !seen[i.id]) { seen[i.id] = true; out.push(i); }
        }));
        return out;
    };

    // ── the body ─────────────────────────────────────────────────────────────────────────────────────────────
    const draftOf = prefill => {
        const b = clone(prefill && prefill.body) || {};
        const g = b.general || {};
        const p = b.pharmacy || {};
        const s = b.safety || {};
        const shelf = p.shelf || {};
        const rec = p.recommendation || {};
        const tr = p.training || {};
        return {
            general: {
                visitKindCode: g.visitKindCode || null, coVisitorUserIds: arr(g.coVisitorUserIds), metContactId: null,
                metRoleCode: g.metRoleCode || null, generalNote: g.generalNote || ''
            },
            pharmacy: {
                stock: arr(p.stock).map(r => ({ gskuId: r.gskuId, levelCode: r.levelCode || null, nearExpiry: !!r.nearExpiry, returnIssue: !!r.returnIssue })),
                shelf: { positionCode: shelf.positionCode || null, materialStand: !!shelf.materialStand, photoEvidenceId: shelf.photoEvidenceId || null },
                sales: arr(p.sales).map(r => ({ gskuId: r.gskuId, trendCode: r.trendCode || null, boxesPerMonth: r.boxesPerMonth == null ? null : r.boxesPerMonth })),
                competitors: arr(p.competitors).map(c => ({
                    competitorProductId: c.competitorProductId || null, nameSnapshot: c.nameSnapshot || '', company: c.company || null,
                    proposed: !!c.proposed, proposalForwarded: !!c.proposalForwarded,
                    advantageCodes: arr(c.advantageCodes), disadvantageCodes: arr(c.disadvantageCodes)
                })),
                noCompetitor: !!p.noCompetitor,
                recommendation: { levelCode: rec.levelCode || null, linkedContactIds: arr(rec.linkedContactIds) },
                training: { given: tr.given == null ? null : !!tr.given, requests: arr(tr.requests).map(r => ({ typeCode: r.typeCode || null, dueDate: r.dueDate || null, note: r.note || null })) }
            },
            safety: { adverseEvent: s.adverseEvent == null ? null : !!s.adverseEvent, adverseEventNote: s.adverseEventNote || null, offLabelRequest: null },
            followUp: b.followUp ? { required: !!b.followUp.required, suggestedDate: b.followUp.suggestedDate || null, note: b.followUp.note || null } : { required: false, suggestedDate: null, note: null }
        };
    };

    /** One stock and one sales row per GSKU whose Global Product is on the plan; rows the rep already has are kept as they are. */
    const seedRows = (pharmacy, gskus, productIds) => {
        const p = clone(pharmacy) || {};
        p.stock = arr(p.stock);
        p.sales = arr(p.sales);
        const ids = arr(productIds);
        arr(gskus).filter(g => g && g.id && ids.indexOf(g.productId) > -1).forEach(g => {
            if (!p.stock.some(r => r.gskuId === g.id)) { p.stock.push({ gskuId: g.id, levelCode: null, nearExpiry: false, returnIssue: false }); }
            if (!p.sales.some(r => r.gskuId === g.id)) { p.sales.push({ gskuId: g.id, trendCode: null, boxesPerMonth: null }); }
        });
        return p;
    };

    /** A GSKU the rep adds by search (outside the plan products): a stock and a sales row. */
    const addGsku = (pharmacy, gskuId) => seedRows(pharmacy, [{ id: gskuId, productId: '*' }], ['*']);

    const removeGsku = (pharmacy, gskuId) => {
        const p = clone(pharmacy) || {};
        p.stock = arr(p.stock).filter(r => r.gskuId !== gskuId);
        p.sales = arr(p.sales).filter(r => r.gskuId !== gskuId);
        return p;
    };

    // ── completeness (mirror of VisitReportV2Rules.Missing, pharmacy branch) ─────────────────────────────────
    const missing = (body, ctx) => {
        const out = [];
        const need = (ok, section, productId, field) => { if (!ok) { out.push({ section, productId: productId || null, field }); } };
        const g = (body && body.general) || {};
        const s = (body && body.safety) || {};
        const p = (body && body.pharmacy) || {};
        need(g.visitKindCode === 'in_person' || g.visitKindCode === 'remote', 'general', null, 'visitKindCode');
        need(s.adverseEvent === true || s.adverseEvent === false, 'safety', null, 'adverseEvent');
        need(s.adverseEvent !== true || !blank(s.adverseEventNote), 'safety', null, 'adverseEventNote');
        need(!blank(g.metRoleCode), 'general', null, 'metRoleCode');
        arr(p.stock).forEach(r => need(!blank(r.levelCode), 'pharmacy', r.gskuId, 'stock.levelCode'));
        arr(p.sales).forEach(r => need(!blank(r.trendCode), 'pharmacy', r.gskuId, 'sales.trendCode'));
        need(!(ctx && ctx.competitorsListed) || arr(p.competitors).length > 0 || p.noCompetitor === true, 'pharmacy', null, 'competitors');
        return out;
    };

    /** A missing answer → the step that asks it. */
    const stepOf = m => {
        const f = (m && m.field) || '';
        if (m && m.section === 'safety') { return 'safety'; }
        if (m && m.section === 'general') { return 'general'; }
        if (f.indexOf('stock.') === 0 || f.indexOf('shelf.') === 0) { return 'stock'; }
        if (f.indexOf('sales.') === 0) { return 'order'; }
        if (f === 'competitors' || f.indexOf('recommendation') === 0 || f.indexOf('training') === 0) { return 'market'; }
        return 'summary';
    };

    /** The count of missing answers per step (the step tabs' "n eksik"). */
    const missingByStep = list => {
        const out = {};
        STEPS.forEach(s => { out[s] = 0; });
        arr(list).forEach(m => { out[stepOf(m)] += 1; });
        return out;
    };

    /** The text key of a missing answer (Miss_<field>). */
    const missingKey = m => 'Miss_' + String((m && m.field) || 'generic').replace(/\./g, '_');

    // ── the competitors ─────────────────────────────────────────────────────────────────────────────────────
    const toggleCompetitor = (pharmacy, item) => {
        const p = clone(pharmacy) || {};
        p.competitors = arr(p.competitors);
        const at = p.competitors.findIndex(c => c.competitorProductId === item.id);
        if (at > -1) { p.competitors.splice(at, 1); }
        else {
            p.competitors.push({ competitorProductId: item.id, nameSnapshot: item.name || '', company: item.company || null, proposed: false, proposalForwarded: false, advantageCodes: [], disadvantageCodes: [] });
            p.noCompetitor = false;
        }
        return p;
    };

    const proposalErrors = (name, company) => {
        const e = {};
        if (blank(name)) { e.name = 'ErrProposalName'; } else if (String(name).trim().length > MAX_NAME) { e.name = 'ErrTooLong'; }
        if (blank(company)) { e.company = 'ErrProposalCompany'; } else if (String(company).trim().length > MAX_NAME) { e.company = 'ErrTooLong'; }
        return e;
    };

    const addProposal = (pharmacy, name, company) => {
        const p = clone(pharmacy) || {};
        p.competitors = arr(p.competitors);
        p.competitors.push({ competitorProductId: null, nameSnapshot: String(name).trim(), company: String(company).trim(), proposed: true, proposalForwarded: false, advantageCodes: [], disadvantageCodes: [] });
        p.noCompetitor = false;
        return p;
    };

    /** "No competitor" clears the competitor list (the two answers exclude each other). */
    const setNoCompetitor = (pharmacy, on) => {
        const p = clone(pharmacy) || {};
        p.noCompetitor = !!on;
        if (on) { p.competitors = []; }
        return p;
    };

    /** One tap = our advantage (+), two taps = our disadvantage (−), a third clears (the mockup's rule). */
    const cycleAdvantage = (competitor, code) => {
        const c = clone(competitor);
        const adv = arr(c.advantageCodes);
        const dis = arr(c.disadvantageCodes);
        if (adv.indexOf(code) > -1) { c.advantageCodes = adv.filter(x => x !== code); c.disadvantageCodes = dis.concat([code]); }
        else if (dis.indexOf(code) > -1) { c.disadvantageCodes = dis.filter(x => x !== code); c.advantageCodes = adv; }
        else { c.advantageCodes = adv.concat([code]); c.disadvantageCodes = dis; }
        return c;
    };

    // ── the sales numbers ───────────────────────────────────────────────────────────────────────────────────
    /** Boxes / month: optional, a whole number 0..100000; '' = not given (null). */
    const boxesOf = text => {
        if (blank(text)) { return { value: null, error: null }; }
        const n = Number(String(text).trim());
        if (!Number.isInteger(n) || n < 0 || n > MAX_BOXES) { return { value: null, error: 'ErrBoxes' }; }
        return { value: n, error: null };
    };

    // ── the wire ─────────────────────────────────────────────────────────────────────────────────────────────
    const requestBody = (plannedVisitId, body, version) => {
        const b = clone(body) || {};
        if (b.safety) { b.safety.offLabelRequest = null; } // never a pharmacy question
        if (b.general) { b.general.metContactId = null; }
        return {
            plannedVisitId: plannedVisitId, general: b.general || null, productSections: [], doctor: null, pharmacy: b.pharmacy || null,
            safety: b.safety || null, followUp: b.followUp || null, expectedVersion: version == null ? null : version
        };
    };

    const amendBody = (reason, plannedVisitId, body, version) => ({ reason: String(reason || '').trim(), v2: requestBody(plannedVisitId, body, version) });

    const reasonError = reason => (blank(reason) ? 'ErrReason' : String(reason).trim().length > MAX_REASON ? 'ErrReasonLong' : null);

    const AMEND_CODE = 'visit_report_edit_window_closed';

    /** draft (nothing / a draft) · resubmit (finalised: the CRM accepts it inside 60 minutes, else answers AMEND_CODE). */
    const modeOf = prefill => {
        const st = prefill && prefill.reportStatus;
        return st === 'submitted' || st === 'amended' ? 'resubmit' : 'draft';
    };

    // ── the refusals ─────────────────────────────────────────────────────────────────────────────────────────
    const ERROR_CODES = [
        'report_incomplete', 'report_code_invalid', 'report_v2_invalid', 'reference_data_unavailable',
        'visit_report_edit_window_closed', 'visit_report_concurrency_conflict', 'visit_report_amendment_reason_required',
        'visit_report_not_finalised', 'visit_report_plan_cancelled', 'visit_report_deadline_passed', 'visit_not_yet_due',
        'visit_photo_invalid', 'shelf_photo_pharmacy_only', 'shelf_photo_report_submitted', 'shelf_photo_not_found',
        'visit_evidence_store_unavailable', 'dependency_unavailable'
    ];

    const codesOf = response => {
        const b = response && response.body;
        const out = [];
        if (b && Array.isArray(b.errors)) { b.errors.forEach(e => out.push(String(e))); }
        if (b && b.code) { out.push(String(b.code)); }
        if (b && b.error) { out.push(String(b.error)); }
        return out;
    };

    const errorKey = response => {
        const hit = codesOf(response).find(c => ERROR_CODES.indexOf(c) > -1);
        if (hit) { return 'Err_' + hit; }
        if (response && response.status === 409) { return 'Err_conflict'; }
        if (response && response.status === 403) { return 'Err_permission_denied'; }
        if (!response || response.status === 0 || response.status >= 500) { return 'Err_unavailable'; }
        return 'Err_generic';
    };

    const isAmendRefusal = response => codesOf(response).indexOf(AMEND_CODE) > -1;

    /** The server's missing[] of a refused submit (400 report_incomplete), else null. */
    const serverMissing = response => {
        const d = response && response.body && response.body.data;
        return d && Array.isArray(d.missing) && codesOf(response).indexOf('report_incomplete') > -1 ? d.missing : null;
    };

    // ── the options ──────────────────────────────────────────────────────────────────────────────────────────
    /**
     * A BRD option's label in the application language. W4-FIX-c (G-6) — the REAL pre-fill contract is
     * optionSets[set] = [{ code, label, labels: { label_tr, label_fr, … } }] (CRM VisitReportOptionDto: the BRD value's
     * attributes): another language reads labels['label_' + lang], English (or a missing translation) the label, then the
     * code — the same rule as report-core optionLabel.
     */
    const optionLabel = (option, lang) => {
        if (!option) { return ''; }
        const labels = option.labels || option.attributes || {};
        const l = String(lang || '').toLowerCase().split('-')[0];
        const own = l && l !== 'en' ? (labels['label_' + l] || labels['Label_' + l]) : null;
        return own || option.label || option.code || '';
    };

    const optionsOf = (prefill, set) => arr(prefill && prefill.optionSets && prefill.optionSets[set]);

    // ── W4-FIX-c ─────────────────────────────────────────────────────────────────────────────────────────────
    /** G-10 — what tells two same-named doctors apart: the specialty LABEL, the primary institution, the city (only what is known). */
    const contactDetail = c => [c && c.specialty, c && c.institution, c && c.city].filter(x => typeof x === 'string' && x.trim()).join(' · ');

    /**
     * G-6 — a MOD-0048 set as the Web proxy answers it ({ items: [{ code, label, isActive, sortOrder, attributes }] }) in the
     * option shape optionLabel reads: [{ code, label, labels }] (inactive values out, sortOrder order).
     */
    const referenceOptions = data => arr(data && (Array.isArray(data) ? data : data.items))
        .filter(v => v && v.code && v.isActive !== false)
        .map((v, i) => ({ v, i })).sort((a, b) => ((a.v.sortOrder || 0) - (b.v.sortOrder || 0)) || (a.i - b.i))
        .map(x => ({ code: String(x.v.code), label: x.v.label || null, labels: x.v.attributes || null }));

    /** G-7 — the header line under the title (mockup E6): the pharmacy, its city, the visit day — only what is known. */
    const headerParts = (target, day) => [target && target.name, target && target.city, day].filter(x => typeof x === 'string' && x.trim());

    return {
        KIND, STEPS, SETS, MAX_NAME, MAX_NOTE, MAX_REASON, MAX_BOXES, MAX_REQUESTS, AMEND_CODE, ERROR_CODES,
        productIdsOf, gskuProductIds, competitorsListed, competitorOptions, draftOf, seedRows, addGsku, removeGsku,
        missing, stepOf, missingByStep, missingKey, toggleCompetitor, proposalErrors, addProposal, setNoCompetitor,
        cycleAdvantage, boxesOf, requestBody, amendBody, reasonError, modeOf, errorKey, isAmendRefusal, serverMissing,
        optionLabel, optionsOf, contactDetail, headerParts, referenceOptions
    };
}));
