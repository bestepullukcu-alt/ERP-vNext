/**
 * WP-VW-W6 · W6-WEB-b — one rule: key, limits, versions, approval and the actions it lets the caller take. FOUR EYES: the
 * decision buttons only when PricingCore.ruleActions says `decide` (in review, the publish key, NOT prepared by the caller).
 */
'use strict';

const PricingRuleDetails = (function () {
    const U = window.PreOrdersUi;
    const K = window.PricingCore;
    const PU = window.PricingUi;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const root = byId('pr-detail-root');
    const endpoint = PU.api + '/rules';
    const flag = (n) => !!root && root.getAttribute('data-' + n) === 'true';
    let rule = null;

    const alertBox = (t) => { const a = byId('pr-alert'); a.textContent = t || ''; a.classList.toggle('d-none', !t); };
    const toggle = (id, on) => byId(id)?.classList.toggle('d-none', !on);

    const draw = (detail) => {
        const r = detail.rule;
        rule = r;
        byId('pr-title').innerHTML = U.bidi(r.skuName || r.skuCode || '') + ' <small class="text-muted">v' + U.esc(r.ruleVersion || 1) + '</small>';
        byId('pr-badge').innerHTML = PU.ruleStatus(r.status) + (r.changeKind && r.changeKind !== 'create' ? ' <span class="badge bg-label-info">' + U.esc(L()['Change_' + r.changeKind] || r.changeKind) + '</span>' : '');
        byId('pr-facts').innerHTML = '<dl class="row g-2 mb-0">' + [['ColCompany', r.legalEntityName], ['ColProduct', r.skuName || r.skuCode], ['ColValidity', r.validFrom || r.validTo ? PU.validity(r.validFrom, r.validTo) : L().AlwaysValid],
            ['SpecificityLabel', L()['Specificity_' + r.specificity] || String(r.specificity)]]
            .filter((x) => x[1]).map((x) => '<div class="col-6 col-md-3"><dt class="small text-muted fw-normal">' + U.esc(L()[x[0]]) + '</dt><dd class="mb-0 text-heading">' + U.bidi(x[1]) + '</dd></div>').join('') + '</dl>';
        const lim = PU.limitsHtml(r);
        byId('pr-limits').innerHTML = '<dl class="row g-2 mb-0">' + [['ColMaxDiscount', lim.discount], ['ColMaxMf', lim.mf], ['ColMaxCombined', lim.combined], ['ColQuantity', lim.quantity]]
            .map((x) => '<div class="col-6 col-md-3"><dt class="small text-muted fw-normal">' + U.esc(L()[x[0]]) + '</dt><dd class="mb-0">' + x[1] + '</dd></div>').join('') + '</dl>';
        byId('pr-approval').innerHTML = PU.approvalHtml(r);
        byId('pr-versions').innerHTML = '<ul class="list-unstyled mb-0">' + (detail.versions || []).map((v) => '<li class="d-flex flex-wrap align-items-center gap-2 mb-1"><a href="/Commercial/Pricing/Rules/Details/' + encodeURIComponent(v.id) + '">v' + U.esc(v.ruleVersion) + '</a>'
            + PU.ruleStatus(v.status) + '<small class="text-muted">' + U.esc(U.moment(v.activatedAt || v.createdAt)) + '</small></li>').join('') + '</ul>';
        const a = K.ruleActions(r, { canUpdate: flag('can-update'), canSubmit: flag('can-submit'), canPublish: flag('can-publish') });
        byId('pr-decision').innerHTML = PU.decisionHtml(a);
        toggle('pr-edit', a.edit);
        byId('pr-edit').setAttribute('href', '/Commercial/Pricing/Rules/Edit/' + encodeURIComponent(r.id));
        toggle('pr-revise', a.revise);
        byId('pr-revise').setAttribute('href', '/Commercial/Pricing/Rules/Edit/' + encodeURIComponent(r.id));
        toggle('pr-retire', a.retire);
        toggle('pr-discard', a.discard);
        toggle('pr-submit', a.submit);
        byId('pr-publish')?.addEventListener('click', () => decide(true));
        byId('pr-return')?.addEventListener('click', () => decide(false));
    };

    const load = async (id) => {
        const r = await U.request('GET', endpoint + '/' + encodeURIComponent(id || root.getAttribute('data-id')));
        const d = U.data(r);
        if (!d || !d.rule) { alertBox(U.errorText(r)); return; }
        draw(d);
    };

    const after = async (r, toastKey) => {
        if (!r) return;
        if (!r.ok) { alertBox(U.errorText(r)); return; }
        alertBox('');
        U.toast(L()[toastKey], 'success');
        const d = U.data(r);
        await load(d && (d.rule ? d.rule.id : d.id));
    };

    const decide = async (publish) => after(await PU.decide(endpoint + '/' + encodeURIComponent(rule.id), rule, publish), publish ? 'ApprovedToast' : 'ReturnedToast');

    const init = () => {
        if (!root || !U || !K || !PU) return;
        byId('pr-submit').addEventListener('click', async () => {
            if (!window.confirm(L().SubmitRuleConfirm || '')) return;
            after(await U.request('POST', endpoint + '/' + encodeURIComponent(rule.id) + '/submit', { expectedVersion: rule.version }), 'SubmittedToast');
        });
        byId('pr-discard').addEventListener('click', async () => {
            if (!window.confirm(L().DiscardConfirm || '')) return;
            const r = await U.request('POST', endpoint + '/' + encodeURIComponent(rule.id) + '/discard', { expectedVersion: rule.version });
            if (!r.ok) { alertBox(U.errorText(r)); return; }
            window.location.href = '/Commercial/Pricing/Rules';
        });
        byId('pr-retire').addEventListener('click', async () => {
            if (!window.confirm(L().RetireConfirm || '')) return;
            after(await U.request('POST', endpoint + '/' + encodeURIComponent(rule.id) + '/revisions', { changeKind: 'retire' }), 'RetireRequestedToast');
        });
        load();
    };

    return { init: init };
})();

document.addEventListener('DOMContentLoaded', () => PricingRuleDetails.init());
