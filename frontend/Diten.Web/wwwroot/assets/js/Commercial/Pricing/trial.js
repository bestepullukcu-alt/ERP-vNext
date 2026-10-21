/**
 * WP-VW-W6 · W6-WEB-b — the rule trial: one line sent to the CRM's evaluation (POST …/rules/trial — saved nowhere) and its
 * answer drawn: the outcome, the rule messages (the shared CommercialRules source), the price list, unit price, free boxes,
 * MF ratio, combined discount, net / VAT and the rule that applied. No arithmetic here (AC-EVAL-3).
 */
'use strict';

const PricingTrial = (function () {
    const U = window.PreOrdersUi;
    const K = window.PricingCore;
    const PU = window.PricingUi;
    const R = () => window.CommercialRules;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    let skuId = null;
    let skuText = '';

    const alertBox = (t) => { const a = byId('pr-alert'); a.textContent = t || ''; a.classList.toggle('d-none', !t); };

    /** The answer of the evaluation for the one line (the CRM's numbers, worded). */
    const resultHtml = (res, name) => {
        const line = (res.lines || [])[0] || {};
        const priced = res.mode === 'priced';
        const facts = [
            ['TrialMode', priced ? U.T('ModePricedWith', res.currency || '') : L().ModeQuantityOnly],
            ['TrialPriceList', res.priceList ? res.priceList.name : null],
            ['ColUnitPrice', priced ? U.money(line.unitPrice, res.currency) : null],
            ['TrialFree', line.freeQuantity ? window.PreOrdersCore.num(line.freeQuantity, U.culture(), 0) : '0'],
            ['TrialMfRatio', line.mfRatioPct != null ? U.pct(line.mfRatioPct) : null],
            ['TrialCombined', priced && line.combinedDiscountPct != null ? U.pct(line.combinedDiscountPct) : null],
            ['TrialNet', priced ? U.money(line.netAmount, res.currency) : null],
            ['TrialVat', priced ? U.money(line.vatAmount, res.currency) : null],
            ['TrialRule', line.rule ? U.T('RuleRef', line.rule.ruleVersion, L()['Specificity_' + line.rule.specificity] || line.rule.specificity) : L().TrialNoRule]
        ].filter((f) => f[1] != null && f[1] !== '');
        const msgs = (line.findings || []).map((f) => '<div class="pr-trial-msg pr-trial-' + U.esc(f.severity) + '">' + (R() ? R().html(f, { skuName: () => name, culture: U.culture() }) : U.esc(f.code)) + '</div>').join('');
        return '<div class="d-flex align-items-center gap-2 mb-3">' + U.outcomeBadge(line.outcome || res.outcome) + '<span class="text-muted small">' + U.bidi(name) + '</span></div>'
            + '<dl class="row g-2 mb-3">' + facts.map((f) => '<div class="col-6 col-md-4"><dt class="small text-muted fw-normal">' + U.esc(L()[f[0]]) + '</dt><dd class="mb-0 text-heading">' + U.bidi(f[1]) + '</dd></div>').join('') + '</dl>'
            + (msgs || '<p class="text-muted small mb-0">' + U.esc(L().TrialNoFindings) + '</p>');
    };

    const run = async (e) => {
        e.preventDefault();
        const made = K.trialInput({ legalEntityId: byId('pr-company').value, skuId: skuId, quantity: byId('pr-qty').value, mf: byId('pr-mf').value,
            discount: byId('pr-disc').value, asOf: byId('pr-asof').value, tier: byId('pr-tier').value });
        if (made.error) { alertBox(L()[made.error]); return; }
        const r = await U.request('POST', PU.api + '/rules/trial', made.input);
        const d = U.data(r);
        if (!r.ok || !d) { alertBox(U.errorText(r)); return; }
        alertBox('');
        byId('pr-trial-result').innerHTML = resultHtml(d, skuText);
    };

    const init = async () => {
        if (!byId('pr-trial-root') || !U || !K || !PU) return;
        const n = byId('pr-asof');
        if (n && typeof n.flatpickr === 'function') { n.flatpickr({ dateFormat: 'Y-m-d', altInput: true, altFormat: L().DateFormat || 'Y-m-d', allowInput: true }); }
        await PU.fillCompanies(byId('pr-company'), null);
        PU.skuPicker(byId('pr-sku'), (v) => { skuId = v.id; skuText = v.text; });
        byId('pr-trial-form').addEventListener('submit', run);
    };

    return { init: init, resultHtml: resultHtml };
})();

if (typeof document !== 'undefined') { document.addEventListener('DOMContentLoaded', () => PricingTrial.init()); }
