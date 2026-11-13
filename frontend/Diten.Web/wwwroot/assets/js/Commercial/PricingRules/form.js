/**
 * WP-VW-W6 · W6-WEB-b — a rule draft: create (company + product + limits), edit a draft (PUT with the version read) or
 * revise an active rule (POST …/revisions { changeKind: "revise", rule } — a new version draft; the active one stays until
 * the second person approves). The MF ratio beside each "10+2" is the CRM's: the trial endpoint evaluates one line of the
 * product with that MF and the page shows the line's mfRatioPct — no formula here (AC-EVAL-3).
 */
'use strict';

const PricingRuleForm = (function () {
    const U = window.PreOrdersUi;
    const P = window.PreOrdersCore;
    const K = window.PricingCore;
    const PU = window.PricingUi;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const root = byId('pr-form-root');
    const endpoint = PU.api + '/rules';
    let rule = null;
    let skuId = null;
    const tokens = { soft: 0, hard: 0 };

    const alertBox = (t) => { const a = byId('pr-alert'); a.textContent = t || ''; a.classList.toggle('d-none', !t); if (t) a.scrollIntoView({ block: 'nearest' }); };
    const val = (id) => byId(id).value;
    const pctOut = (v) => (v == null ? '' : String(v));

    /** The CRM's MF ratio of a term ("= %16,7"), asked from the trial endpoint; nothing when the term or the key is not ready. */
    const showRatio = async (side) => {
        const out = byId('pr-mf-' + side + '-ratio');
        const term = P.parseMf(val('pr-mf-' + side));
        const company = val('pr-company');
        if (!term || term === 'invalid' || !company || !skuId) { out.textContent = term === 'invalid' ? (L().ErrMf || '') : ''; return; }
        const token = ++tokens[side];
        const r = await U.request('POST', endpoint + '/trial', { legalEntityId: company, lines: [{ skuId: skuId, quantity: term.buy, mf: term, discountPct: null }] });
        if (token !== tokens[side]) return;
        const line = ((U.data(r) || {}).lines || [])[0];
        out.textContent = line && line.mfRatioPct != null ? U.T('MfRatioIs', U.pct(line.mfRatioPct)) : '';
    };

    const fill = (r) => {
        const m = r.maxMf || {};
        byId('pr-disc-soft').value = pctOut((r.maxDiscountPct || {}).soft);
        byId('pr-disc-hard').value = pctOut((r.maxDiscountPct || {}).hard);
        byId('pr-mf-soft').value = m.soft ? m.soft.text : '';
        byId('pr-mf-hard').value = m.hard ? m.hard.text : '';
        byId('pr-comb-soft').value = pctOut((r.maxCombinedPct || {}).soft);
        byId('pr-comb-hard').value = pctOut((r.maxCombinedPct || {}).hard);
        byId('pr-qty-min').value = pctOut((r.quantity || {}).min);
        byId('pr-qty-soft').value = pctOut((r.quantity || {}).softMax);
        byId('pr-qty-hard').value = pctOut((r.quantity || {}).hardMax);
        if (r.validFrom) { byId('pr-from')._flatpickr ? byId('pr-from')._flatpickr.setDate(r.validFrom, false) : (byId('pr-from').value = r.validFrom); }
        if (r.validTo) { byId('pr-to')._flatpickr ? byId('pr-to')._flatpickr.setDate(r.validTo, false) : (byId('pr-to').value = r.validTo); }
        ['soft', 'hard'].forEach((s) => { const t = (m[s] || null); byId('pr-mf-' + s + '-ratio').textContent = t ? U.T('MfRatioIs', U.pct(t.ratioPct)) : ''; });
    };

    const save = async () => {
        const made = K.ruleInput({
            legalEntityId: val('pr-company'), skuId: skuId, validFrom: val('pr-from'), validTo: val('pr-to'),
            discSoft: val('pr-disc-soft'), discHard: val('pr-disc-hard'), mfSoft: val('pr-mf-soft'), mfHard: val('pr-mf-hard'),
            combSoft: val('pr-comb-soft'), combHard: val('pr-comb-hard'), min: val('pr-qty-min'), softMax: val('pr-qty-soft'), hardMax: val('pr-qty-hard'),
            expectedVersion: rule ? rule.version : null
        });
        if (made.error) { alertBox(L()[made.error]); return; }
        let r;
        if (!rule) r = await U.request('POST', endpoint, made.input);
        else if (rule.status === 'active') r = await U.request('POST', endpoint + '/' + encodeURIComponent(rule.id) + '/revisions', { changeKind: 'revise', rule: made.input });
        else r = await U.request('PUT', endpoint + '/' + encodeURIComponent(rule.id), made.input);
        if (!r.ok) { alertBox(r.status === 409 && !window.CommercialRules?.has(P.errorCode(r.body)) ? L().Err_conflict : U.errorText(r)); return; }
        const saved = U.data(r);
        const id = (saved && (saved.rule ? saved.rule.id : saved.id)) || (rule && rule.id);
        window.location.href = '/Commercial/Pricing/Rules/Details/' + encodeURIComponent(id);
    };

    const init = async () => {
        if (!root || !U || !K || !PU) return;
        const altFormat = L().DateFormat || 'Y-m-d';
        ['pr-from', 'pr-to'].forEach((id) => { const n = byId(id); if (n && typeof n.flatpickr === 'function') { n.flatpickr({ dateFormat: 'Y-m-d', altInput: true, altFormat: altFormat, allowInput: true }); } });
        await PU.fillCompanies(byId('pr-company'), null);
        const id = root.getAttribute('data-id');
        if (id) {
            const r = await U.request('GET', endpoint + '/' + encodeURIComponent(id));
            const d = U.data(r);
            rule = d && d.rule;
            if (!rule) { alertBox(U.errorText(r)); return; }
            await PU.fillCompanies(byId('pr-company'), rule.legalEntityId);
            byId('pr-company').disabled = true;                                            // the key of the rule
            skuId = rule.skuId;
            byId('pr-sku').innerHTML = '<option value="' + U.esc(rule.skuId) + '" selected>' + U.esc(rule.skuName || rule.skuCode || rule.skuId) + '</option>';
            byId('pr-sku').disabled = true;
            fill(rule);
            if (rule.status !== 'draft' && rule.status !== 'active') { alertBox(window.CommercialRules?.error('pricing_rule_not_editable') || ''); byId('pr-save').disabled = true; return; }
            if (rule.status === 'active') { byId('pr-save').innerHTML = '<i class="bx bx-revision me-1"></i>' + U.esc(L().SaveRevision); }
        } else {
            PU.skuPicker(byId('pr-sku'), (v) => { skuId = v.id; showRatio('soft'); showRatio('hard'); });
        }

        ['soft', 'hard'].forEach((s) => byId('pr-mf-' + s).addEventListener('input', () => showRatio(s)));
        byId('pr-company').addEventListener('change', () => { showRatio('soft'); showRatio('hard'); });
        byId('pr-save').addEventListener('click', save);
    };

    return { init: init };
})();

document.addEventListener('DOMContentLoaded', () => PricingRuleForm.init());
