/**
 * WP-VW-W6 · W6-WEB-b — one FX rate: create (company × pair × day + rate + source) or correct (the rate and the source;
 * the key stays — the CRM writes the old → new into the history). Shapes checked here (3-letter codes, ≤ 6 decimals);
 * the CRM checks them again and answers a second rate of the same key with 409 fx_rate_duplicate.
 */
'use strict';

const FxRateForm = (function () {
    const U = window.PreOrdersUi;
    const K = window.PricingCore;
    const PU = window.PricingUi;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const root = byId('pr-form-root');
    const endpoint = PU.api + '/fx-rates';
    let rate = null;

    const alertBox = (t) => { const a = byId('pr-alert'); a.textContent = t || ''; a.classList.toggle('d-none', !t); };

    const save = async () => {
        const body = {
            legalEntityId: byId('pr-company').value || null, fromCurrency: byId('pr-fromcur').value.trim().toUpperCase(), toCurrency: byId('pr-tocur').value.trim().toUpperCase(),
            rateDate: byId('pr-date').value || null, rate: K.parseAmount(byId('pr-rate').value, 6), source: byId('pr-source').value.trim(), expectedVersion: rate ? rate.version : null
        };
        if (!body.legalEntityId || !/^[A-Z]{3}$/.test(body.fromCurrency) || !/^[A-Z]{3}$/.test(body.toCurrency) || !body.rateDate || !body.source) { alertBox(L().ErrRateHead); return; }
        if (body.rate == null || body.rate === 'invalid' || body.rate <= 0) { alertBox(L().ErrRate); return; }
        const r = rate ? await U.request('PUT', endpoint + '/' + encodeURIComponent(rate.id), body) : await U.request('POST', endpoint, body);
        if (!r.ok) { alertBox(r.status === 409 && !window.CommercialRules?.has(window.PreOrdersCore.errorCode(r.body)) ? L().Err_conflict : U.errorText(r)); return; }
        window.location.href = '/Commercial/Pricing/FxRates';
    };

    const init = async () => {
        if (!root || !U || !K || !PU) return;
        const d = byId('pr-date');
        if (d && typeof d.flatpickr === 'function') d.flatpickr({ dateFormat: 'Y-m-d', altInput: true, altFormat: L().DateFormat || 'Y-m-d', allowInput: true });
        await PU.fillCompanies(byId('pr-company'), null);
        const id = root.getAttribute('data-id');
        if (id) {
            const r = await U.request('GET', endpoint + '/' + encodeURIComponent(id));
            rate = U.data(r);
            if (!rate) { alertBox(U.errorText(r)); return; }
            await PU.fillCompanies(byId('pr-company'), rate.legalEntityId);
            byId('pr-fromcur').value = rate.fromCurrency || '';
            byId('pr-tocur').value = rate.toCurrency || '';
            d._flatpickr ? d._flatpickr.setDate(rate.rateDate, false) : (d.value = rate.rateDate || '');
            byId('pr-rate').value = rate.rate == null ? '' : String(rate.rate);
            byId('pr-source').value = rate.source || '';
            ['pr-company', 'pr-fromcur', 'pr-tocur'].forEach((x) => { byId(x).disabled = true; });         // the key of a rate
            if (d._flatpickr && d._flatpickr.altInput) d._flatpickr.altInput.disabled = true; else d.disabled = true;
        }

        byId('pr-save').addEventListener('click', save);
    };

    return { init: init };
})();

document.addEventListener('DOMContentLoaded', () => FxRateForm.init());
