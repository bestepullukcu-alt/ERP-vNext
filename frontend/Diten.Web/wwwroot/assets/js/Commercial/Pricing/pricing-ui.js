/**
 * WP-VW-W6 · W6-WEB-b — screen D's shared drawing: the badges, the approval panel (rounds, decisions and the FOUR EYES:
 * the decision buttons only for a record the caller did not prepare, else the note why not — D13), the price list items
 * with their difference to the previous version, the rule limits, the history row, the selling-company options and the
 * product picker. It reuses W6-WEB-a's helpers (PreOrdersUi: request, words, money / percent, the reason dialog) and the
 * shared rule messages (CommercialRules) — nothing is copied. Every amount is the CRM's; nothing is computed here.
 */
(function (root) {
    'use strict';

    const U = () => root.PreOrdersUi;
    const K = () => root.PricingCore;
    const L = () => root.L10n || {};
    const $ = () => root.jQuery;
    const esc = (s) => U().esc(s);
    const api = '/Commercial/Pricing/api';

    const tone = (b, icon, text) => '<span class="badge bg-label-' + b + ' d-inline-flex align-items-center gap-1 text-nowrap"><i class="bx ' + icon + '"></i>' + esc(text) + '</span>';
    const badge = (b) => { const x = K().badgeOf(b); return tone(x.tone, x.icon, L()[x.key] || b || ''); };
    const ruleStatus = (s) => { const x = K().ruleStatusOf(s); return tone(x.tone, x.icon, L()[x.key] || s || ''); };
    const kind = (k) => L()['Kind_' + k] || k || '';
    const tier = (t) => L()['Tier_' + String(t || '').replace(/-/g, '_')] || t || '';
    const validity = (from, to) => (to ? U().T('ValidityRange', U().day(from), U().day(to)) : U().T('ValidityFrom', U().day(from)));
    const num = (v, d) => (v == null || v === '' ? '—' : root.PreOrdersCore.num(v, U().culture(), d == null ? 4 : d));

    /** The rounds (submitted by · when · outcome) and the decisions (by · outcome · comment) of a list / rule. */
    const approvalHtml = (rec) => {
        const r = rec || {};
        const rounds = (r.rounds || []).map((x) => '<li class="mb-1"><small>' + U().Th('RoundLine', x.roundNo, x.submittedBy || '', U().moment(x.submittedAt))
            + (x.outcome ? ' · ' + esc(L()['Outcome_' + String(x.outcome).replace(/-/g, '_')] || x.outcome) : ' · ' + esc(L().RoundOpen || '')) + '</small></li>').join('');
        const decisions = (r.decisions || []).map((d) => '<li class="mb-1"><small>' + U().Th('DecisionLine', d.by || '', L()['Outcome_' + String(d.outcome).replace(/-/g, '_')] || d.outcome, U().moment(d.at))
            + (d.comment ? '<span class="d-block text-muted">' + U().Th('ReasonLine', d.comment) + '</span>' : '') + '</small></li>').join('');
        if (!rounds && !decisions) return '<p class="text-muted small mb-0">' + esc(L().NoRounds || '') + '</p>';
        return '<ul class="list-unstyled mb-0">' + rounds + decisions + '</ul>';
    };

    /**
     * The decision area of a record in review: publish / return for an approver who did NOT prepare it; the four-eyes note
     * for one who did (the server refuses that decision with 403 anyway — the page does not offer it).
     */
    const decisionHtml = (actions) => {
        if (actions.decide) {
            return '<div class="d-flex flex-wrap gap-2" id="pr-decide"><button type="button" class="btn btn-success" id="pr-publish"><i class="bx bx-check me-1"></i>' + esc(L().Publish) + '</button>'
                + '<button type="button" class="btn btn-label-danger" id="pr-return"><i class="bx bx-undo me-1"></i>' + esc(L().ReturnDraft) + '</button></div>'
                + '<small class="text-muted d-block mt-2">' + esc(L().DecisionNote) + '</small>';
        }

        if (actions.ownDecision) return '<div class="alert alert-info d-flex gap-2 mb-0 py-2" id="pr-own"><i class="bx bx-shield-quarter mt-1"></i><span>' + esc(L().FourEyesOwn) + '</span></div>';
        return '';
    };

    /** The items of a list with their difference to the previous version (added · changed with the old values · removed). */
    const itemsTable = (items, removed, diffOn) => {
        const head = ['ColSku', 'ColBarcode', 'ColDepotPrice', 'ColPharmacistPrice', 'ColPublicPrice', 'ColVat'].concat(diffOn ? ['ColDiff'] : []);
        const old = (now, before) => (diffOn && before != null && Number(before) !== Number(now) ? '<small class="d-block text-muted pr-old">' + esc(num(before)) + '</small>' : '');
        const row = (i, removedRow) => {
            const d = i.diff || {};
            const cells = [
                '<div class="text-heading">' + U().bidi(i.skuName || i.skuCode || '') + '</div><small class="text-muted">' + U().bidi(i.skuCode || '') + '</small>',
                i.gtin ? U().bidi(i.gtin) : '<span class="text-muted">—</span>',
                esc(num(i.depotPrice)) + old(i.depotPrice, d.previousDepotPrice),
                esc(num(i.pharmacistPurchasePrice)) + old(i.pharmacistPurchasePrice, d.previousPharmacistPurchasePrice),
                esc(num(i.publicPrice)) + old(i.publicPrice, d.previousPublicPrice),
                esc(num(i.vatRate, 1)) + old(i.vatRate, d.previousVatRate)
            ];
            if (diffOn) {
                const k = removedRow ? 'removed' : (d.kind || 'unchanged');
                cells.push('<span class="badge bg-label-' + ({ added: 'success', changed: 'warning', removed: 'danger' }[k] || 'secondary') + '">' + esc(L()['Diff_' + k] || k) + '</span>');
            }
            return '<tr class="' + (removedRow ? 'pr-removed' : '') + '">' + cells.map((c) => '<td>' + c + '</td>').join('') + '</tr>';
        };
        const body = (items || []).map((i) => row(i, false)).join('') + (diffOn ? (removed || []).map((i) => row(i, true)).join('') : '');
        return '<div class="table-responsive"><table class="table table-sm align-middle mb-0 pr-items"><thead><tr>' + head.map((h) => '<th>' + esc(L()[h] || '') + '</th>').join('') + '</tr></thead><tbody>'
            + (body || '<tr><td colspan="' + head.length + '" class="text-muted">' + esc(L().NoItems || '') + '</td></tr>') + '</tbody></table></div>';
    };

    /** A soft / hard pair of a rule ("soft 10 · hard 15"); the MF pair names the CRM's own ratio of each term. */
    const pair = (soft, hard, fmt) => {
        if (soft == null && hard == null) return '<span class="text-muted">—</span>';
        return '<span class="pr-soft">' + esc(soft == null ? '—' : fmt(soft)) + '</span> · <span class="pr-hard">' + esc(hard == null ? '—' : fmt(hard)) + '</span>';
    };
    const pctText = (v) => U().pct(v);
    const mfTerm = (t) => (t ? t.text + ' (' + U().pct(t.ratioPct) + ')' : null);
    const limitsHtml = (r) => {
        const q = r.quantity || {};
        return {
            discount: pair((r.maxDiscountPct || {}).soft, (r.maxDiscountPct || {}).hard, pctText),
            mf: pair(mfTerm((r.maxMf || {}).soft), mfTerm((r.maxMf || {}).hard), (x) => x),
            combined: pair((r.maxCombinedPct || {}).soft, (r.maxCombinedPct || {}).hard, pctText),
            quantity: (q.min == null && q.softMax == null && q.hardMax == null) ? '<span class="text-muted">—</span>'
                : esc(U().T('QuantityLimits', q.min == null ? '—' : q.min, q.softMax == null ? '—' : q.softMax, q.hardMax == null ? '—' : q.hardMax))
        };
    };

    let companies = null;
    /** The selling companies (MDM), read once per page: [{ id, text, sub: country }]. */
    const loadCompanies = () => {
        if (companies) return companies;
        companies = U().request('GET', api + '/lookups/legal-entities').then((r) => ((U().data(r) || {}).items || []));
        return companies;
    };
    const companyName = (list, id) => { const c = (list || []).find((x) => x.id === id); return c ? c.text : ''; };

    /** A select of the selling companies (value = legal entity id). */
    const fillCompanies = async (el, selected, allText) => {
        if (!el) return;
        const list = await loadCompanies();
        el.innerHTML = (allText !== undefined ? '<option value="">' + esc(allText) + '</option>' : '<option value=""></option>')
            + list.map((c) => '<option value="' + esc(c.id) + '"' + (c.id === selected ? ' selected' : '') + '>' + esc(c.text + (c.sub ? ' · ' + c.sub : '')) + '</option>').join('');
        if (selected && !list.some((c) => c.id === selected)) el.insertAdjacentHTML('beforeend', '<option value="' + esc(selected) + '" selected>' + esc(selected) + '</option>');
        if ($()) $()(el).trigger('change.select2');
    };

    /** The product picker: product-master stockable items (Active only), searched as you type. */
    const skuPicker = (el, onPick) => {
        const jq = $();
        if (!jq || !jq.fn.select2 || !el) return;
        jq(el).select2({
            width: '100%', placeholder: el.getAttribute('data-placeholder') || L().ProductPlaceholder || '', minimumInputLength: 2,
            language: { inputTooShort: () => L().SearchHint || '', noResults: () => L().NoMatch || '', searching: () => L().Searching || '' },
            ajax: {
                delay: 250,
                transport: (params, success, failure) => U().request('GET', api + '/lookups/skus?q=' + encodeURIComponent((params.data && params.data.term) || '')).then((r) => (r.ok ? success(r.body) : failure())),
                processResults: (body) => ({ results: (((body || {}).data || {}).items || []).map((o) => ({ id: o.id, text: o.text + (o.sub ? ' · ' + o.sub : '') })) })
            }
        });
        jq(el).on('select2:select', (e) => onPick && onPick({ id: e.params.data.id, text: e.params.data.text }));
    };

    /** The change history row's cells. */
    const historyCells = (c) => {
        const h = K().historyRow(c);
        return {
            when: esc(U().moment(c.at)),
            who: U().bidi(c.byUserId || ''),
            what: '<div class="text-heading">' + esc(L()[h.typeKey] || c.entityType || '') + '</div><small class="text-muted">' + esc(L()[h.actionKey] || c.action || '') + (c.entityVersion ? ' · v' + esc(c.entityVersion) : '') + '</small>',
            changes: h.changes.length ? h.changes.map((f) => '<div class="small"><span class="text-muted">' + U().bidi(f.field) + '</span> ' + U().bidi(f.old || '—') + ' → <strong>' + U().bidi(f.new || '—') + '</strong></div>').join('') : '<span class="text-muted">—</span>'
        };
    };

    /** Publish (a consequential act: the browser's confirm) / return (a reason is REQUIRED) of a list or a rule. */
    const decide = async (path, record, publish) => {
        let reason = null;
        if (publish) {
            if (!root.confirm(L().PublishConfirm || '')) return null;
        } else {
            reason = await U().askReason(L().ReturnTitle, L().ReturnText, L().ReturnDraft);
            if (!reason) return null;
        }
        return U().request('POST', path + '/decision', { decision: publish ? 'publish' : 'return', reason: reason, expectedVersion: record.version });
    };

    const out = { api: api, badge: badge, ruleStatus: ruleStatus, kind: kind, tier: tier, validity: validity, num: num, approvalHtml: approvalHtml,
        decisionHtml: decisionHtml, itemsTable: itemsTable, limitsHtml: limitsHtml, loadCompanies: loadCompanies, companyName: companyName,
        fillCompanies: fillCompanies, skuPicker: skuPicker, historyCells: historyCells, decide: decide };
    root.PricingUi = out;
    if (typeof module !== 'undefined' && module.exports) module.exports = out;
})(typeof window !== 'undefined' ? window : globalThis);
