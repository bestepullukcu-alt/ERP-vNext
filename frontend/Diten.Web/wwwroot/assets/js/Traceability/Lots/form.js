/**
 * MOD-0174 P3 — Yeni Lot / Düzenle (pack §11.3): COMPACT form, 9 fields + summary. Create: Ürün / SKU + Lot No are
 * entered (live duplicate check "Bu ürün için bu lot numarası zaten var"); Edit: both read-only (immutable). A non-Draft
 * lot is edited only with a reason ≥ 10 characters (K-C, CT-D8): the fields stay locked until then (counter n/10) and the
 * "changes" panel lists old → new. One submit per form (the key was minted when the page opened). The MVC proxy repeats
 * the field rules (LotFormRules); the service decides, its refusal comes back to its field.
 */
'use strict';

const LotForm = (function () {
    const ui = window.TraceUi;
    const t = ui.t;
    const form = document.getElementById('formLot');
    const byId = (id) => document.getElementById(id);
    const isEdit = form?.dataset.mode === 'edit';
    const lotId = form?.dataset.lotId || '';
    const LOT = /^[A-Z0-9./-]{1,40}$/;

    const el = {
        item: byId('lotItemId'), sku: byId('lotSku'), lotNumber: byId('lotNumber'), country: byId('lotCountry'),
        manufacturerLot: byId('lotManufacturerLot'), supplierLot: byId('lotSupplierLot'), manufacturingDate: byId('lotManufacturingDate'),
        expiryDate: byId('lotExpiryDate'), retestDate: byId('lotRetestDate'), coa: byId('lotCoa'), reason: byId('lotReason'),
        submit: byId('btnLotSubmit')
    };
    const editable = ['manufacturerLot', 'supplierLot', 'manufacturingDate', 'expiryDate', 'retestDate', 'countryOfOrigin', 'coaReference'];
    const controlOf = { manufacturerLot: el.manufacturerLot, supplierLot: el.supplierLot, manufacturingDate: el.manufacturingDate, expiryDate: el.expiryDate, retestDate: el.retestDate, countryOfOrigin: el.country, coaReference: el.coa };
    const labelOf = { manufacturerLot: 'Form.ManufacturerLot', supplierLot: 'Form.SupplierLot', manufacturingDate: 'Form.ManufacturingDate', expiryDate: 'Form.ExpiryDate', retestDate: 'Form.RetestDate', countryOfOrigin: 'Form.Country', coaReference: 'Form.Coa' };
    const state = { original: null, status: 'Draft', busy: false };

    const value = (name) => (controlOf[name]?.value || '').trim();
    const select2 = (select, extra) => {
        if (!select || !window.jQuery?.fn?.select2) return;
        $(select).select2(Object.assign({ dropdownParent: $(select).closest('.diten-field'), width: '100%', placeholder: select.dataset.placeholder || '',
            language: { searching: () => t('Searching'), noResults: () => t('NoResults') } }, extra || {}));
    };

    const clearErrors = () => {
        form.querySelectorAll('[data-error-for]').forEach((n) => { n.textContent = ''; });
        form.querySelectorAll('.is-invalid').forEach((n) => n.classList.remove('is-invalid'));
        byId('lot-form-alert')?.classList.add('d-none');
    };
    const fieldError = (field, message) => {
        const node = form.querySelector(`[data-error-for="${field}"]`);
        if (!node) return false;
        node.textContent = message || '';
        return true;
    };

    // ── summary + edit locking (K-C) ───────────────────────────────────────────────────────────────────
    const setSummary = (name, text) => { const n = form.querySelector(`[data-summary="${name}"]`); if (n) n.textContent = text || '—'; };
    const locked = () => isEdit && state.status !== 'Draft' && (el.reason?.value || '').trim().length < 10;
    const applyLock = () => {
        if (!isEdit) return;
        const lock = locked();
        Object.values(controlOf).forEach((c) => {
            if (!c) return;
            c.disabled = lock;
            if (window.jQuery && $(c).hasClass('select2-hidden-accessible')) $(c).prop('disabled', lock);
        });
        byId('lot-locked-banner')?.classList.toggle('d-none', !lock || state.status === 'Draft');
        const counter = byId('lotReasonCounter');
        if (counter) counter.textContent = t('Form.ReasonCounter', Math.min((el.reason?.value || '').trim().length, 10));
    };
    const renderChanges = () => {
        const list = form.querySelector('[data-changes]');
        if (!list || !state.original) return;
        const changes = editable.filter((f) => value(f) !== (state.original[f] || ''));
        list.innerHTML = changes.length === 0
            ? `<li class="text-muted">${ui.esc(t('Form.NoChanges'))}</li>`
            : changes.map((f) => `<li class="mb-1"><span class="fw-medium">${ui.esc(t(labelOf[f]))}</span>: <span class="text-muted">${ui.esc(state.original[f] || '—')}</span> → ${ui.esc(value(f) || '—')}</li>`).join('');
    };

    // ── create: product → SKUs, duplicate check ────────────────────────────────────────────────────────
    const loadSkus = async () => {
        const itemId = el.item.value;
        el.sku.innerHTML = '<option value=""></option>';
        el.sku.disabled = !itemId;
        if (!itemId) return;
        const reply = await ui.getJson(`/Lots/lookups/skus?${new URLSearchParams({ itemId })}`);
        (reply.json?.items || []).forEach((o) => el.sku.add(new Option(o.text, o.value)));
        el.sku.disabled = false;
        if (window.jQuery) $(el.sku).prop('disabled', false).trigger('change.select2');
    };
    let duplicateTimer = null;
    const checkDuplicate = () => {
        window.clearTimeout(duplicateTimer);
        duplicateTimer = window.setTimeout(async () => {
            const number = (el.lotNumber.value || '').trim().toUpperCase();
            setSummary('lotNumber', number);
            fieldError('lotNumber', number && !LOT.test(number) ? t('Validation.LotFormat') : '');
            if (!el.sku.value || !LOT.test(number)) return;
            const reply = await ui.getJson(`/Lots/form/duplicate?${new URLSearchParams({ sku: el.sku.value, lotNumber: number })}`);
            if (reply.json?.duplicate) fieldError('lotNumber', reply.json.message || t('Form.Duplicate'));
        }, 350);
    };

    // ── edit: load the lot ──────────────────────────────────────────────────────────────────────────────
    const loadLot = async () => {
        const reply = await ui.getJson(`/Lots/data/${encodeURIComponent(lotId)}`);
        if (!reply.ok) { ui.showAlert(byId('lot-form-alert'), ui.errorText(reply), reply.json?.error?.correlationId); el.submit && (el.submit.disabled = true); return; }
        const lot = reply.json.lot;
        state.status = lot.status;
        byId('lotIfMatch').value = reply.json.etag || '';
        el.item.innerHTML = `<option value="${ui.esc(lot.itemId)}" selected>${ui.esc(lot.product || lot.itemId)}</option>`;
        el.sku.innerHTML = `<option value="" selected>${ui.esc(lot.sku || `${lot.skuLevel} ${lot.skuId}`)}</option>`;
        el.lotNumber.value = lot.lotNumber || '';
        el.manufacturerLot.value = lot.manufacturerLot || '';
        el.supplierLot.value = lot.supplierLot || '';
        el.manufacturingDate.value = ui.day(lot.manufacturingDate);
        el.expiryDate.value = ui.day(lot.expiryDate);
        el.retestDate.value = ui.day(lot.retestDate);
        el.country.value = lot.countryOfOrigin || '';
        if (window.jQuery) $(el.country).trigger('change.select2');
        el.coa.value = lot.coaReference || '';
        state.original = Object.fromEntries(editable.map((f) => [f, value(f)]));
        setSummary('product', lot.product || lot.itemId);
        setSummary('tracking', t(lot.trackingMode === 'LotAndSerial' ? 'Tracking.LotAndSerial' : 'Tracking.Lot'));
        setSummary('lotNumber', lot.lotNumber);
        setSummary('status', t('Status.' + lot.status));
        byId('lot-reason-card')?.classList.toggle('d-none', lot.status === 'Draft');
        applyLock();
        renderChanges();
    };

    // ── submit ──────────────────────────────────────────────────────────────────────────────────────────
    const body = () => ({
        idempotencyKey: byId('lotIdempotencyKey').value,
        itemId: isEdit ? null : el.item.value,
        sku: isEdit ? null : el.sku.value,
        lotNumber: isEdit ? null : (el.lotNumber.value || '').trim().toUpperCase(),
        manufacturerLot: value('manufacturerLot'), supplierLot: value('supplierLot'),
        manufacturingDate: value('manufacturingDate'), expiryDate: value('expiryDate'), retestDate: value('retestDate'),
        countryOfOrigin: value('countryOfOrigin'), coaReference: value('coaReference'),
        reason: (el.reason?.value || '').trim(), ifMatch: byId('lotIfMatch').value, origin: 'lot-form'
    });

    const check = (b) => {
        const errors = {};
        if (!isEdit) {
            if (!b.itemId) errors.itemId = t('Validation.Required');
            if (!b.sku) errors.sku = t('Validation.Required');
            if (!b.lotNumber) errors.lotNumber = t('Validation.Required');
            else if (!LOT.test(b.lotNumber)) errors.lotNumber = t('Validation.LotFormat');
        }
        const today = new Date().toISOString().slice(0, 10);
        if (b.manufacturingDate && b.manufacturingDate > today) errors.manufacturingDate = t('Validation.DatesOrder');
        if (b.manufacturingDate && b.expiryDate && b.expiryDate <= b.manufacturingDate) errors.expiryDate = t('Validation.DatesOrder');
        if (b.retestDate && ((b.manufacturingDate && b.retestDate < b.manufacturingDate) || (b.expiryDate && b.retestDate > b.expiryDate))) errors.retestDate = t('Validation.DatesOrder');
        if (isEdit && state.status !== 'Draft' && b.reason.length < 10) errors.reason = t('Validation.ReasonMin');
        return errors;
    };

    const submit = async (event) => {
        event.preventDefault();
        if (state.busy) return;
        clearErrors();
        const b = body();
        const errors = check(b);
        if (Object.keys(errors).length) {
            Object.entries(errors).forEach(([f, m]) => fieldError(f, m));
            ui.showAlert(byId('lot-form-alert'), t('Validation.FixFields'));
            return;
        }
        state.busy = true;
        if (el.submit) el.submit.disabled = true;
        const token = ui.antiforgery(form);
        const reply = await ui.postJson(isEdit ? `/Lots/edit/${encodeURIComponent(lotId)}` : '/Lots/create', b, token);
        if (reply.ok && reply.json?.success) { window.location.assign(reply.json.redirect || '/Lots'); return; }
        state.busy = false;
        if (el.submit) el.submit.disabled = false;
        if (reply.json?.fields) Object.entries(reply.json.fields).forEach(([f, m]) => fieldError(f, m));
        const error = reply.json?.error;
        if (error?.scope === 'field' && error.field) fieldError(error.field, error.message);
        ui.showAlert(byId('lot-form-alert'), error?.message || ui.errorText(reply), error?.correlationId);
    };

    const bind = () => {
        form.addEventListener('submit', submit);
        if (!isEdit) {
            select2(el.item, { ajax: { url: '/Lots/lookups/products', dataType: 'json', delay: 250, data: (p) => ({ q: p.term || '' }),
                processResults: (d) => ({ results: (d?.items || []).map((o) => ({ id: o.value, text: o.text })) }) } });
            select2(el.sku, { minimumResultsForSearch: 8 });
            $(el.item).on('change', async () => { setSummary('product', el.item.selectedOptions[0]?.text); await loadSkus(); checkDuplicate(); });
            $(el.sku).on('change', checkDuplicate);
            el.lotNumber.addEventListener('input', checkDuplicate);
            el.lotNumber.addEventListener('change', () => { el.lotNumber.value = el.lotNumber.value.trim().toUpperCase(); checkDuplicate(); });
        }
        select2(el.country, { allowClear: true });
        Object.values(controlOf).forEach((c) => c?.addEventListener('input', renderChanges));
        if (window.jQuery) $(el.country).on('change', renderChanges);
        el.reason?.addEventListener('input', applyLock);
    };

    return {
        init: () => {
            if (!form) return;
            bind();
            if (isEdit && lotId) loadLot();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => LotForm.init());
