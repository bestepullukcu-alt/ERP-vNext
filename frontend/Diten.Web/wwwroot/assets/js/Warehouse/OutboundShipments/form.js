/**
 * MOD-0178 DL-6 — the outbound Create / Edit pages (Golden Reference Compact form; pack §9, §8.17).
 *
 * CREATE: depo (LOCATION, company's active warehouses) → optional Staging (the warehouse's stockable nodes — only when the warehouse has several
 * Staging nodes; the service checks the role) → lines (product → SKU of the product → its BASE unit from PRODUCT-MASTER, quantity, order line,
 * optional lot change + FEFO reason) → "Tahsisi önizle" (§8.17 step 7: lots in FEFO order, allocatable, the company's FEFO mode — NOT a
 * reservation) → "Kaydet" (the allocation is recomputed on save; 422 WMS_ALLOCATION_FAILED lists the failing lines).
 * EDIT: the header only (order ref, ship-to, note) — If-Match; the lines and the allocation never change.
 *
 * Every call goes to the same-origin proxy (/Warehouse/OutboundShipments/...); the browser never calls the Gateway. The rules are OutboundRules.
 */
'use strict';

const OutboundShipmentForm = (function () {
    const rules = window.OutboundRules;
    const form = document.getElementById('outboundForm');
    const mode = form?.dataset.formMode || 'create';
    const outboundId = form?.dataset.outboundId || '';
    const L = () => window.L10n || {};
    const W = (key) => (L().Words || {})[key] || '';
    const byId = (id) => document.getElementById(id);
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const newKey = () => (window.crypto?.randomUUID ? window.crypto.randomUUID() : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
        const r = Math.random() * 16 | 0;
        return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
    }));
    const token = () => form?.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const format = (template, ...values) => values.reduce((text, value, i) => text.replace(`{${i}}`, value ?? ''), template || '');
    const value = (id) => (byId(id)?.value || '').trim();
    // E3-FIX-01 (G-1): quantities on screen are in the reader's culture (tr "2", "1,5"); the lines sent keep the dot-decimal wire string
    const culture = () => window.CurrentLanguage || document.documentElement.lang || undefined;
    const qty = (v) => rules.quantityText(v, culture());

    const getJson = async (url) => {
        const res = await fetch(url, { credentials: 'same-origin', headers: getAuthHeaders() });
        let json = null;
        try { json = await res.json(); } catch (error) { json = null; }
        return { ok: res.ok, status: res.status, json };
    };

    const post = async (url, body) => {
        const headers = Object.assign({}, getAuthHeaders(true), { RequestVerificationToken: token() });
        const res = await fetch(url, { method: 'POST', credentials: 'same-origin', headers, body: JSON.stringify(body) });
        let json = null;
        try { json = await res.json(); } catch (error) { json = null; }
        return { ok: res.ok, status: res.status, json };
    };

    const failure = (answer) => answer?.json?.error?.message || W('Warehouse.Error.Generic');
    const showAlert = (text) => { const box = byId('outbound-form-alert'); if (box) { box.textContent = text || ''; box.classList.toggle('d-none', !text); } };
    const fieldError = (id, text) => { const el = byId(id); if (el) el.textContent = text || ''; };

    const select2 = (el, url, data, extra = {}) => {
        if (!el || !window.jQuery?.fn?.select2) return;
        window.jQuery(el).select2(Object.assign({
            placeholder: window.jQuery(el).data('placeholder') || '',
            allowClear: true,
            width: '100%',
            language: { searching: () => L().Searching || '', noResults: () => L().NoResults || '' },
            ajax: {
                url,
                dataType: 'json',
                delay: 250,
                data: (params) => Object.assign({ q: params.term || '' }, data ? data() : {}),
                processResults: (answer) => ({ results: (answer?.items || []).map((i) => ({ id: i.value, text: i.sub ? `${i.text} — ${i.sub}` : i.text })) })
            }
        }, extra));
    };

    // ─── Lines (create) ──────────────────────────────────────────────────────
    let lineCount = 0;
    let preview = null; // previewOutboundAllocation answer

    const lineRow = (n) => `<div class="border rounded p-3" data-line="${n}">`
        + '<div class="row g-2 align-items-end">'
        + `<div class="col-12 col-md-2"><label class="form-label small mb-1" for="lineOrder${n}">${esc(W('LineOrderLine'))} <span class="text-danger">*</span></label>`
        + `<input id="lineOrder${n}" class="form-control" data-field="orderLineId" maxlength="100" value="${n}" required /></div>`
        + `<div class="col-12 col-md-4"><label class="form-label small mb-1" for="lineProduct${n}">${esc(W('LineProduct'))} <span class="text-danger">*</span></label>`
        + `<select id="lineProduct${n}" class="form-select" data-field="itemId" data-placeholder="${esc(W('LineProduct'))}"><option value=""></option></select></div>`
        + `<div class="col-12 col-md-3"><label class="form-label small mb-1" for="lineSku${n}">${esc(W('LineSku'))} <span class="text-danger">*</span></label>`
        + `<select id="lineSku${n}" class="form-select" data-field="sku" data-placeholder="${esc(W('LineSku'))}"><option value=""></option></select></div>`
        + `<div class="col-8 col-md-2"><label class="form-label small mb-1" for="lineQty${n}">${esc(W('LineQuantity'))} <span class="text-danger">*</span></label>`
        + `<div class="input-group"><input id="lineQty${n}" class="form-control wh-touch" data-field="quantity" inputmode="decimal" autocomplete="off" required />`
        + `<span class="input-group-text" data-field-uom>-</span></div></div>`
        + `<div class="col-4 col-md-1 d-flex"><button type="button" class="btn btn-label-danger wh-touch ms-auto" data-remove-line="${n}" aria-label="${esc(W('LineRemove'))}" title="${esc(W('LineRemove'))}"><i class="bx bx-trash"></i></button></div>`
        + `<div class="col-12 col-md-6 d-none" data-lot-block><label class="form-label small mb-1" for="lineLot${n}">${esc(W('LineLot'))}</label>`
        + `<select id="lineLot${n}" class="form-select" data-field="lotId"><option value="">${esc(W('LineFefoFirst'))}</option></select></div>`
        + `<div class="col-12 col-md-6 d-none" data-reason-block><label class="form-label small mb-1" for="lineReason${n}">${esc(W('LineReason'))}</label>`
        + `<input id="lineReason${n}" class="form-control" data-field="reason" maxlength="1000" autocomplete="off" /><small class="text-muted" data-reason-hint></small></div>`
        + '</div><input type="hidden" data-field="uomId" /></div>';

    const addLine = () => {
        lineCount += 1;
        const n = lineCount;
        byId('outbound-lines').insertAdjacentHTML('beforeend', lineRow(n));
        const product = byId(`lineProduct${n}`);
        const sku = byId(`lineSku${n}`);
        select2(product, '/Warehouse/OutboundShipments/products');
        select2(sku, '/Warehouse/OutboundShipments/skus', () => ({ itemId: product.value || '' }));
        window.jQuery?.(product).on('change', () => { window.jQuery(sku).val(null).trigger('change'); setUom(n, null); invalidatePreview(); });
        window.jQuery?.(sku).on('change', () => { loadUom(n); invalidatePreview(); });
        byId(`lineQty${n}`).addEventListener('input', invalidatePreview);
        // E3-FIX-01 (item 4): a lot change is previewed at once — the preview shows the CHOSEN lot, not the FEFO suggestion again
        byId(`lineLot${n}`).addEventListener('change', () => { refreshLot(n); if (preview) runPreview(); });
    };

    const setUom = (n, uom) => {
        const row = form.querySelector(`[data-line="${n}"]`);
        if (!row) return;
        row.querySelector('[data-field="uomId"]').value = uom || '';
        row.querySelector('[data-field-uom]').textContent = uom || '-';
    };

    // the line's unit is the SKU's BASE unit (§8.17 step 1 — the service refuses another with 422 WMS_UOM_NOT_BASE)
    const loadUom = async (n) => {
        setUom(n, null);
        const itemId = byId(`lineProduct${n}`)?.value;
        const sku = rules.parseSku(byId(`lineSku${n}`)?.value);
        if (!itemId || !sku) return;
        const answer = await getJson(`/Warehouse/OutboundShipments/sku-facts?${new URLSearchParams({ itemId, skuId: sku.skuId, skuLevel: sku.skuLevel })}`);
        if (!answer.ok || !answer.json?.available || !answer.json.baseUom) { showAlert(W('SkuUnitUnavailable')); return; }
        setUom(n, answer.json.baseUom);
    };

    const rowsOf = () => Array.from(form.querySelectorAll('[data-line]')).map((row) => {
        const field = (name) => row.querySelector(`[data-field="${name}"]`)?.value || '';
        return { n: Number(row.dataset.line), orderLineId: field('orderLineId'), itemId: field('itemId'), sku: field('sku'), uomId: field('uomId'), quantity: field('quantity'), lotId: field('lotId'), reason: field('reason') };
    });

    const header = () => {
        const body = { warehouseId: value('WarehouseId'), stagingLocationId: value('StagingLocationId') || null, orderRef: value('OrderRef') || null, note: value('Note') || null };
        const shipTo = { name: value('ShipToName'), country: value('ShipToCountry'), city: value('ShipToCity') || null, postalCode: value('ShipToPostalCode') || null, line1: value('ShipToLine1') || null, line2: value('ShipToLine2') || null };
        return { body, shipTo };
    };

    /** The lines as the proxy wants them, or null with the field error shown. */
    const linesBody = () => {
        const rows = rowsOf();
        if (!rows.length) { fieldError('LinesError', W('ValidationLines')); return null; }
        const lines = [];
        for (const row of rows) {
            const result = rules.lineBody(row);
            if (result.error) { fieldError('LinesError', W(result.error)); return null; }
            const need = rules.lotNeed(previewLine(row.n), row.lotId);
            if (need === 'Blocked') { fieldError('LinesError', W('DeviationBlocked')); return null; }
            if (need === 'Required' && String(row.reason).trim().length < 10) { fieldError('LinesError', W('DeviationNeedsReason')); return null; }
            lines.push(result.line);
        }
        fieldError('LinesError', '');
        return lines;
    };

    // ─── Preview (§8.17 step 7) ──────────────────────────────────────────────
    const previewLine = (n) => {
        if (!preview) return null;
        const index = rowsOf().findIndex((r) => r.n === n);
        const line = (preview.lines || [])[index];
        return line ? { lots: line.lots || [], mode: preview.fefoOutboundMode } : null;
    };

    const invalidatePreview = () => {
        preview = null;
        byId('outbound-preview')?.classList.add('d-none');
        form.querySelectorAll('[data-lot-block], [data-reason-block]').forEach((el) => el.classList.add('d-none'));
    };

    const refreshLot = (n) => {
        const row = form.querySelector(`[data-line="${n}"]`);
        const lotId = byId(`lineLot${n}`)?.value || '';
        const need = rules.lotNeed(previewLine(n), lotId);
        const block = row.querySelector('[data-reason-block]');
        block.classList.toggle('d-none', need === 'None');
        row.querySelector('[data-reason-hint]').textContent = need === 'Blocked' ? W('DeviationBlocked') : need === 'Required' ? W('DeviationNeedsReason') : need === 'Optional' ? W('DeviationOptional') : '';
    };

    /**
     * E3-FIX-01 (item 4): the line's chosen lot is IN the preview (the service previews a lot override and reports its FEFO deviation without
     * refusing — §8.17 step 7, `deviation`). When the chosen lot is not the first free one, the line says so and names the FEFO suggestion.
     */
    const deviationNote = (line) => {
        const d = line.deviation;
        if (!d) return '';
        return `<div class="small text-warning" data-preview-deviation>${esc(format(W('PreviewChosenLot'), d.firstEligibleLotNumber || '-', d.firstEligibleExpiry || '-'))}</div>`;
    };

    const renderPreview = () => {
        const host = byId('outbound-preview-lines');
        const rows = rowsOf();
        host.innerHTML = (preview.lines || []).map((line, i) => {
            const failed = line.failureCode ? `<div class="text-danger small">${esc(W(`Warehouse.Outbound.LineFailure.${line.failureCode}`) || W(`Warehouse.Error.${line.failureCode}`) || W('Warehouse.Error.Invalid'))}</div>` : '';
            const allocations = (line.allocations || []).map((a) => `<div class="small">${esc(format(W('PreviewAllocation'), a.lotNumber || '-', qty(a.quantity), a.fefoRank ?? '-'))}</div>`).join('');
            return `<div class="border rounded p-2" data-preview-line="${esc(line.lineSeq)}"><strong>${esc(line.orderLineId)}</strong> — ${esc(format(W('LineAllocatable'), qty(line.allocatable ?? line.allocated)))}${failed}${allocations}${deviationNote(line)}</div>`;
        }).join('');
        (preview.lines || []).forEach((line, i) => {
            const row = rows[i];
            if (!row) return;
            const select = byId(`lineLot${row.n}`);
            const lots = line.lots || [];
            select.innerHTML = `<option value="">${esc(W('LineFefoFirst'))}</option>` + lots.map((l) => `<option value="${esc(l.lotId)}">${esc(format(W('LotOption'), l.lotNumber, l.fefoRank, l.expiryDate || '-', qty(l.allocatable)))}</option>`).join('');
            // the reader's choice survives the redraw (it is what this preview was asked for)
            if (row.lotId && lots.some((l) => l.lotId === row.lotId)) select.value = row.lotId;
            select.closest('[data-lot-block]').classList.toggle('d-none', lots.length < 2);
            refreshLot(row.n);
        });
        byId('outbound-preview').classList.remove('d-none');
    };

    // E3-FIX-01 (item 4): a refusal about the chosen Staging location points at that field, not only at the top of the form
    const StagingCodes = { WMS_TARGET_NOT_ALLOWED: 'StagingNotAllowed', WMS_STAGING_NOT_CONFIGURED: 'Warehouse.Error.WMS_STAGING_NOT_CONFIGURED' };
    const stagingError = (answer) => {
        const word = StagingCodes[answer?.json?.error?.code];
        fieldError('StagingLocationIdError', word ? W(word) : '');
        return word ? W(word) : null;
    };

    const runPreview = async () => {
        showAlert('');
        fieldError('StagingLocationIdError', '');
        const { body, shipTo } = header();
        if (!body.warehouseId) { fieldError('WarehouseIdError', W('ValidationWarehouse')); return; }
        fieldError('WarehouseIdError', '');
        const rows = rowsOf().map((r) => rules.lineBody(r));
        if (!rows.length || rows.some((r) => r.error)) { fieldError('LinesError', W(rows.length ? 'ValidationLine' : 'ValidationLines')); return; }
        fieldError('LinesError', '');
        const answer = await post('/Warehouse/OutboundShipments/preview', Object.assign({}, body, { shipTo: shipTo.name && shipTo.country ? shipTo : null, lines: rows.map((r) => r.line) }));
        if (!answer.ok) { showAlert(stagingError(answer) || failure(answer)); return; }
        preview = answer.json;
        renderPreview();
    };

    // ─── Save ────────────────────────────────────────────────────────────────
    const saveKey = newKey();
    let original = null; // edit: the outbound as read (for the changed members)

    const allocationFailure = (answer) => {
        const lines = answer?.json?.error?.details?.lines || [];
        if (!lines.length) return failure(answer);
        return [failure(answer), ...lines.map((l) => format(W('AllocationLineFailed'), l.lineSeq, W(`Warehouse.Outbound.LineFailure.${l.code}`) || l.code, qty(l.requested), qty(l.allocatable)))].join(' ');
    };

    const submitCreate = async () => {
        const { body, shipTo } = header();
        if (!body.warehouseId) { fieldError('WarehouseIdError', W('ValidationWarehouse')); return; }
        fieldError('WarehouseIdError', '');
        if (!shipTo.name || !shipTo.country) { fieldError('ShipToError', W('ValidationShipTo')); return; }
        fieldError('ShipToError', '');
        const lines = linesBody();
        if (!lines) return;
        fieldError('StagingLocationIdError', '');
        const answer = await post('/Warehouse/OutboundShipments/create', Object.assign({ idempotencyKey: saveKey, shipTo, lines }, body));
        if (!answer.ok) { showAlert(stagingError(answer) || allocationFailure(answer)); return; }
        window.showToast?.(answer.json?.message || '', 'success');
        window.location.href = `/Warehouse/OutboundShipments/Details/${encodeURIComponent(answer.json.id)}`;
    };

    const submitEdit = async () => {
        if (!original) return;
        const { body, shipTo } = header();
        if (!shipTo.name || !shipTo.country) { fieldError('ShipToError', W('ValidationShipTo')); return; }
        fieldError('ShipToError', '');
        const o = original.outbound;
        const changes = {};
        if ((body.orderRef || null) !== (o.orderRef || null)) changes.orderRef = body.orderRef;
        if ((body.note || null) !== (o.note || null)) changes.note = body.note;
        const before = o.shipTo || {};
        if (['name', 'country', 'city', 'postalCode', 'line1', 'line2'].some((k) => (shipTo[k] || null) !== (before[k] || null))) changes.shipTo = shipTo;
        if (!Object.keys(changes).length) { showAlert(W('NoChanges')); return; }
        const answer = await post(`/Warehouse/OutboundShipments/edit/${encodeURIComponent(outboundId)}`, { idempotencyKey: saveKey, ifMatch: original.etag, changes });
        if (!answer.ok) { showAlert(failure(answer)); return; }
        window.showToast?.(answer.json?.message || '', 'success');
        window.location.href = `/Warehouse/OutboundShipments/Details/${encodeURIComponent(outboundId)}`;
    };

    const loadEdit = async () => {
        const answer = await getJson(`/Warehouse/OutboundShipments/outbound/${encodeURIComponent(outboundId)}`);
        if (!answer.ok) { showAlert(failure(answer)); return; }
        original = answer.json;
        const o = original.outbound;
        const setValue = (id, v) => { const el = byId(id); if (el) el.value = v || ''; };
        const warehouse = byId('WarehouseId');
        if (warehouse) { warehouse.innerHTML = ''; warehouse.add(new Option(o.warehouse || '', o.warehouseId || '', true, true)); }
        const staging = byId('StagingLocationId');
        if (staging) { staging.innerHTML = ''; staging.add(new Option(o.staging || '', o.stagingLocationId || '', true, true)); }
        setValue('OrderRef', o.orderRef);
        setValue('Note', o.note);
        setValue('ShipToName', o.shipTo?.name);
        setValue('ShipToCountry', o.shipTo?.country);
        setValue('ShipToCity', o.shipTo?.city);
        setValue('ShipToPostalCode', o.shipTo?.postalCode);
        setValue('ShipToLine1', o.shipTo?.line1);
        setValue('ShipToLine2', o.shipTo?.line2);
        const host = byId('outbound-lines-readonly');
        if (host) host.innerHTML = (o.lines || []).map((l) => `<div class="small">${esc(l.orderLineId)} · ${esc(l.sku)} · ${esc(qty(l.quantity))} ${esc(l.uom)}</div>`).join('');
        if (!rules.EditStatuses.includes(o.status)) { showAlert(W('Warehouse.Error.WMS_OUTBOUND_IMMUTABLE')); byId('btnSaveOutbound')?.setAttribute('disabled', 'disabled'); }
    };

    const init = () => {
        if (!form) return;
        form.addEventListener('submit', (event) => {
            event.preventDefault();
            showAlert('');
            (mode === 'edit' ? submitEdit : submitCreate)();
        });
        if (mode === 'edit') { loadEdit(); return; }
        select2(byId('WarehouseId'), '/Warehouse/OutboundShipments/warehouses', null, { allowClear: false });
        select2(byId('StagingLocationId'), '/Warehouse/OutboundShipments/stagings', () => ({ warehouseId: value('WarehouseId') }));
        window.jQuery?.(byId('WarehouseId')).on('change', () => { window.jQuery(byId('StagingLocationId')).val(null).trigger('change'); invalidatePreview(); });
        byId('btnAddLine')?.addEventListener('click', addLine);
        byId('btnPreview')?.addEventListener('click', runPreview);
        byId('outbound-lines')?.addEventListener('click', (event) => {
            const button = event.target.closest('[data-remove-line]');
            if (!button) return;
            form.querySelector(`[data-line="${button.dataset.removeLine}"]`)?.remove();
            invalidatePreview();
        });
        addLine();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => OutboundShipmentForm.init());
