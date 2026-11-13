/**
 * WP-VW-W6 · W6-WEB-b — a price list draft by hand (EK-1). Create: the head only (then the draft opens to take its items).
 * Edit: the head + the items — new rows (a product from the product master), changed rows and removed rows go in ONE save
 * with the version read (a moved version is 409, said in words). Each row is checked here for its shape only (a tier or
 * more, VAT 0–100: PricingCore.itemInput); the CRM checks it again exactly as an imported row (Retired, one SKU once).
 */
'use strict';

const PriceListForm = (function () {
    const U = window.PreOrdersUi;
    const K = window.PricingCore;
    const PU = window.PricingUi;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const root = byId('pr-form-root');
    const endpoint = PU.api + '/price-lists';
    let list = null;
    let rows = [];
    let removed = [];
    let seq = 0;

    const alertBox = (t) => { const a = byId('pr-alert'); a.textContent = t || ''; a.classList.toggle('d-none', !t); if (t) a.scrollIntoView({ block: 'nearest' }); };
    const amount = (v) => (v == null ? '' : String(v));

    const rowHtml = (r) => '<div class="row g-2 align-items-end pr-item" data-key="' + r.key + '">'
        + '<div class="col-12 col-lg-4"><label class="form-label small" for="pr-sku-' + r.key + '">' + U.esc(L().ColSku) + '</label>'
        + (r.itemId ? '<div class="form-control-plaintext text-heading">' + U.bidi(r.skuText || '') + '</div>'
            : '<div class="diten-field"><i class="bx bx-capsule diten-field-icon" aria-hidden="true"></i><select class="form-select pr-sku" id="pr-sku-' + r.key + '" data-key="' + r.key + '"><option value=""></option></select></div>') + '</div>'
        + [['depot', 'ColDepotPrice'], ['pharmacist', 'ColPharmacistPrice'], ['public', 'ColPublicPrice'], ['vat', 'ColVat']].map((f) => '<div class="col-6 col-lg-2"><label class="form-label small" for="pr-' + f[0] + '-' + r.key + '">' + U.esc(L()[f[1]]) + '</label>'
            + '<div class="diten-field"><i class="bx ' + (f[0] === 'vat' ? 'bx-receipt' : 'bx-money') + ' diten-field-icon" aria-hidden="true"></i><input type="text" inputmode="decimal" class="form-control pr-field" id="pr-' + f[0] + '-' + r.key + '" data-key="' + r.key + '" data-field="' + f[0] + '" value="' + U.esc(r[f[0]]) + '" /></div></div>').join('')
        + '<div class="col-12 col-lg-auto"><button type="button" class="btn btn-sm btn-icon btn-label-danger pr-remove" data-key="' + r.key + '" aria-label="' + U.esc(L().RemoveRow) + '" title="' + U.esc(L().RemoveRow) + '"><i class="bx bx-trash"></i></button></div></div>';

    const draw = () => {
        const host = byId('pr-items');
        host.innerHTML = rows.map(rowHtml).join('') || '<p class="text-muted small mb-0">' + U.esc(L().NoItems) + '</p>';
        host.querySelectorAll('select.pr-sku').forEach((el) => PU.skuPicker(el, (v) => { const r = rows.find((x) => x.key === el.getAttribute('data-key')); if (r) { r.skuId = v.id; r.skuText = v.text; r.dirty = true; } }));
    };

    const headInput = () => ({
        name: byId('pr-name').value.trim(), legalEntityId: byId('pr-company').value || null, currency: byId('pr-currency').value.trim().toUpperCase(),
        kind: byId('pr-kind').value, validFrom: byId('pr-from').value || null, validTo: byId('pr-to').value || null
    });

    const save = async () => {
        const head = headInput();
        if (!head.name || (!list && !head.legalEntityId) || !/^[A-Z]{3}$/.test(head.currency) || !head.validFrom) { alertBox(L().ErrListHead); return; }
        let r;
        if (!list) {
            r = await U.request('POST', endpoint, head);
            if (!r.ok) { alertBox(U.errorText(r)); return; }
            window.location.href = '/Commercial/Pricing/PriceLists/Edit/' + encodeURIComponent(U.data(r).id);
            return;
        }

        const items = [];
        for (const row of rows.filter((x) => x.dirty)) {
            const one = K.itemInput(row);
            if (one.error) { alertBox(L()[one.error]); return; }
            items.push(one.input);
        }

        // W6-FIX-a (G-4) — the company is the draft's from its creation: the edit does not send it (the CRM's update has no
        // such field and refused the body), and the field is locked on the edit page.
        delete head.legalEntityId;
        r = await U.request('PUT', endpoint + '/' + encodeURIComponent(list.id), Object.assign(head, { items: items, removeItemIds: removed, expectedVersion: list.version }));
        if (!r.ok) { alertBox(r.status === 409 && !window.CommercialRules?.has(window.PreOrdersCore.errorCode(r.body)) ? L().Err_conflict : U.errorText(r)); return; }
        U.toast(L().DraftSaved, 'success');
        window.location.href = '/Commercial/Pricing/PriceLists/Details/' + encodeURIComponent(list.id);
    };

    const init = async () => {
        if (!root || !U || !K || !PU) return;
        const altFormat = L().DateFormat || 'Y-m-d';
        ['pr-from', 'pr-to'].forEach((id) => { const n = byId(id); if (n && typeof n.flatpickr === 'function') { n.flatpickr({ dateFormat: 'Y-m-d', altInput: true, altFormat: altFormat, allowInput: true }); } });
        const id = root.getAttribute('data-id');
        await PU.fillCompanies(byId('pr-company'), null);
        if (id) {
            const r = await U.request('GET', endpoint + '/' + encodeURIComponent(id));
            list = U.data(r);
            if (!list) { alertBox(U.errorText(r)); return; }
            byId('pr-name').value = list.name || '';
            await PU.fillCompanies(byId('pr-company'), list.legalEntityId);
            byId('pr-company').disabled = true;
            byId('pr-currency').value = list.currency || '';
            byId('pr-kind').value = list.kind || 'official';
            byId('pr-from')._flatpickr ? byId('pr-from')._flatpickr.setDate(list.validFrom, false) : (byId('pr-from').value = list.validFrom || '');
            if (list.validTo) { byId('pr-to')._flatpickr ? byId('pr-to')._flatpickr.setDate(list.validTo, false) : (byId('pr-to').value = list.validTo); }
            if (list.status !== 'draft') { alertBox(window.CommercialRules?.error('price_list_not_editable') || ''); byId('pr-save').disabled = true; return; }
            const items = U.data(await U.request('GET', endpoint + '/' + encodeURIComponent(id) + '/items?page=1&pageSize=200')) || {};
            rows = (items.items || []).map((i) => ({ key: 'r' + (++seq), itemId: i.id, skuId: i.skuId, skuText: i.skuName || i.skuCode, depot: amount(i.depotPrice), pharmacist: amount(i.pharmacistPurchasePrice), public: amount(i.publicPrice), vat: amount(i.vatRate) }));
            byId('pr-items-card').classList.remove('d-none');
            draw();
        }

        byId('pr-items').addEventListener('input', (e) => { const r = rows.find((x) => x.key === e.target.getAttribute('data-key')); if (r && e.target.dataset.field) { r[e.target.dataset.field] = e.target.value; r.dirty = true; } });
        byId('pr-items').addEventListener('click', (e) => {
            const b = e.target.closest && e.target.closest('.pr-remove');
            if (!b) return;
            const r = rows.find((x) => x.key === b.getAttribute('data-key'));
            if (r && r.itemId) removed.push(r.itemId);
            rows = rows.filter((x) => x !== r);
            draw();
        });
        byId('pr-add-item').addEventListener('click', () => { rows.push({ key: 'r' + (++seq), itemId: null, skuId: null, depot: '', pharmacist: '', public: '', vat: '', dirty: true }); draw(); });
        byId('pr-save').addEventListener('click', save);
    };

    return { init: init };
})();

document.addEventListener('DOMContentLoaded', () => PriceListForm.init());
