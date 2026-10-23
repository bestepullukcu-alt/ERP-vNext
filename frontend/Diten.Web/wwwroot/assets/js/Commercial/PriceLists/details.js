/**
 * WP-VW-W6 · W6-WEB-b — one price list: head, approval, items (+ the difference to the previous version on demand) and
 * the actions the list lets the caller take. FOUR EYES: the decision buttons are drawn only when PricingCore.listActions
 * says `decide` (in review, the publish key, NOT prepared by the caller — the proxy's `mine`); the preparer sees the note.
 */
'use strict';

const PriceListDetails = (function () {
    const U = window.PreOrdersUi;
    const K = window.PricingCore;
    const PU = window.PricingUi;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const root = byId('pr-detail-root');
    const endpoint = PU.api + '/price-lists';
    const flag = (n) => !!root && root.getAttribute('data-' + n) === 'true';
    let list = null;

    const alertBox = (t) => { const a = byId('pr-alert'); a.textContent = t || ''; a.classList.toggle('d-none', !t); };
    const toggle = (id, on) => byId(id)?.classList.toggle('d-none', !on);

    const loadItems = async () => {
        const diff = byId('pr-diff').checked;
        const r = await U.request('GET', endpoint + '/' + encodeURIComponent(list.id) + '/items?page=1&pageSize=200' + (diff ? '&diff=previous' : ''));
        const d = U.data(r);
        if (!d) { byId('pr-items').innerHTML = '<p class="text-muted small mb-0">' + U.esc(U.errorText(r)) + '</p>'; return; }
        byId('pr-items').innerHTML = PU.itemsTable(d.items, d.removed, diff);
        byId('pr-items-more').textContent = d.total > (d.items || []).length ? U.T('ItemsShown', (d.items || []).length, d.total) : '';
    };

    const draw = () => {
        const l = list;
        byId('pr-title').innerHTML = U.bidi(l.name) + ' <small class="text-muted">v' + U.esc(l.listVersion || 1) + '</small>';
        byId('pr-badge').innerHTML = PU.badge(l.badge);
        byId('pr-facts').innerHTML = '<dl class="row g-2 mb-0">' + [['ColCompany', l.legalEntityName || l.legalEntityCode], ['ColCountry', l.countryCode], ['ColCurrency', l.currency],
            ['ColKind', PU.kind(l.kind)], ['ColValidity', PU.validity(l.validFrom, l.effectiveValidTo || l.validTo)], ['ColItems', U.T('ItemsCount', l.itemCount || 0)],
            ['PublishedLine', l.publishedAt ? U.moment(l.publishedAt) : null]]
            .filter((r) => r[1]).map((r) => '<div class="col-6 col-md-3"><dt class="small text-muted fw-normal">' + U.esc(L()[r[0]] || '') + '</dt><dd class="mb-0 text-heading">' + U.bidi(r[1]) + '</dd></div>').join('') + '</dl>';
        byId('pr-approval').innerHTML = PU.approvalHtml(l);
        const a = K.listActions(l, { canUpdate: flag('can-update'), canCreate: flag('can-create'), canSubmit: flag('can-submit'), canPublish: flag('can-publish') });
        byId('pr-decision').innerHTML = PU.decisionHtml(a);
        toggle('pr-edit', a.edit);
        byId('pr-edit').setAttribute('href', '/Commercial/Pricing/PriceLists/Edit/' + encodeURIComponent(l.id));
        toggle('pr-discard', a.discard);
        toggle('pr-submit', a.submit);
        toggle('pr-version', a.newVersion);
        byId('pr-publish')?.addEventListener('click', () => decide(true));
        byId('pr-return')?.addEventListener('click', () => decide(false));
    };

    const after = async (r, toastKey) => {
        if (!r) return;
        if (!r.ok) { alertBox(U.errorText(r)); return; }
        alertBox('');
        U.toast(L()[toastKey], 'success');
        await load();
    };

    const decide = async (publish) => after(await PU.decide(endpoint + '/' + encodeURIComponent(list.id), list, publish), publish ? 'PublishedToast' : 'ReturnedToast');

    const load = async () => {
        const r = await U.request('GET', endpoint + '/' + encodeURIComponent(root.getAttribute('data-id')));
        const l = U.data(r);
        if (!l) { alertBox(U.errorText(r)); return; }
        list = l;
        draw();
        loadItems();
    };

    const init = () => {
        if (!root || !U || !K || !PU) return;
        byId('pr-diff').addEventListener('change', loadItems);
        // G-6 (W6-FIX-b) — submit and discard ask through the shared confirmation, never the browser's box
        byId('pr-submit').addEventListener('click', () => U.confirm(L().SubmitForPublishing, L().SubmitConfirm, L().SubmitForPublishing, 'warning', async () => {
            after(await U.request('POST', endpoint + '/' + encodeURIComponent(list.id) + '/submit', { expectedVersion: list.version }), 'SubmittedToast');
        }));
        byId('pr-discard').addEventListener('click', () => U.confirm(L().DiscardDraft, L().DiscardConfirm, L().DiscardDraft, 'danger', async () => {
            const r = await U.request('POST', endpoint + '/' + encodeURIComponent(list.id) + '/discard', { expectedVersion: list.version });
            if (!r.ok) { alertBox(U.errorText(r)); return; }
            window.location.href = '/Commercial/Pricing/PriceLists';
        }));
        byId('pr-version').addEventListener('click', async () => {
            const r = await U.request('POST', endpoint + '/' + encodeURIComponent(list.id) + '/versions', {});
            const v = U.data(r);
            if (!r.ok || !v) { alertBox(U.errorText(r)); return; }
            window.location.href = '/Commercial/Pricing/PriceLists/Edit/' + encodeURIComponent(v.id);
        });
        load();
    };

    return { init: init };
})();

document.addEventListener('DOMContentLoaded', () => PriceListDetails.init());
