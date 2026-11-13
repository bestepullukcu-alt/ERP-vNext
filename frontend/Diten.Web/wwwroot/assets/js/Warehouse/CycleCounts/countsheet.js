/**
 * MOD-0181 DL-4 — Sayım Formu (COUNTSHEET), phone first (AC-39: 375 px, no horizontal scroll, touch targets ≥ 48 px).
 *
 * The lines come bin by bin from '/Warehouse/CycleCounts/lines/{countId}' (the service's pages of 200). A blind count's expected quantity is ABSENT
 * from the service's answer before submit (pack §8.2 rule 5): this script draws an expected quantity only when the answer carries one — it never
 * hides a number it received. Entry, "Bulunan stok ekle" and "Gönder" exist only for cycle-count.counts.record (the server still answers 403) and
 * only while the count is Released / Counting; Submitted, Closed and Cancelled counts are read-only.
 */
'use strict';

const CountSheet = (function () {
    const root = '/Warehouse/CycleCounts';
    const host = document.getElementById('cc-sheet');
    const countId = host?.getAttribute('data-count-id') || '';
    const byId = (id) => document.getElementById(id);
    // DL-4 FIX-01: the page writes its payload with the MVC JSON options (camelCase), so its root names are `words`, `cancel`, `save` — read
    // exactly those (CycleCountScreensTests holds the rendered names and the names read here equal)
    let payload = {};
    try { payload = JSON.parse(byId('countsheet-l10n')?.textContent || '{}') || {}; } catch (error) { payload = {}; }
    const words = payload.words || {};
    const W = (key) => words[key] || '';
    const format = (template, ...values) => values.reduce((text, value, i) => text.replace(`{${i}}`, value ?? ''), template || '');
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const can = (key) => window.Permissions?.has?.(key) === true;
    const token = () => document.querySelector('#formCountSheet input[name="__RequestVerificationToken"]')?.value || '';
    const newKey = () => (window.crypto?.randomUUID ? window.crypto.randomUUID() : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
        const r = Math.random() * 16 | 0;
        return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
    }));
    const headers = (json) => Object.assign({}, window.DitenDataTable?.getAuthHeaders?.(json) || (json ? { 'Content-Type': 'application/json' } : {}));

    const getJson = async (url) => {
        const res = await fetch(url, { credentials: 'same-origin', headers: headers(false) });
        let json = null;
        try { json = await res.json(); } catch (error) { json = null; }
        return { ok: res.ok, status: res.status, json };
    };

    const post = async (url, body) => {
        const res = await fetch(url, { method: 'POST', credentials: 'same-origin', headers: Object.assign(headers(true), { RequestVerificationToken: token(), 'Content-Type': 'application/json' }), body: JSON.stringify(body) });
        let json = null;
        try { json = await res.json(); } catch (error) { json = null; }
        return { ok: res.ok, status: res.status, json };
    };

    const failure = (answer) => answer?.json?.error?.message || W('CycleCount.Error.Generic');
    const showAlert = (id, text) => { const box = byId(id); if (box) { box.textContent = text || ''; box.classList.toggle('d-none', !text); } };

    let count = null;
    let etag = null;
    let lines = [];
    const pending = new Map(); // lineId → the idempotency key of the entry being saved (a retry reuses it)

    const editable = () => can('cycle-count.counts.record') && count && ['Released', 'Counting'].includes(count.status);
    const isCounted = (line) => line.countedQuantity !== null && line.countedQuantity !== undefined && line.countedQuantity !== '';
    const lineOpen = (line) => ['Open', 'Counted'].includes(line.status);

    // ─── drawing ────────────────────────────────────────────────────────────
    const lineHtml = (line) => {
        const expected = line.expectedQuantity !== null && line.expectedQuantity !== undefined
            ? `<small class="text-muted d-block">${esc(format(W('ExpectedValue'), line.expectedQuantity))}</small>` : '';
        // DL-4 FIX-01: the lot NUMBER (the proxy reads it from the lot register); never the lot id — an unread number is said in words
        const lot = line.lotId ? `<small class="text-muted d-block">${esc(W('FieldLot'))}: ${esc(line.lotNumber || W('LotUnknown'))}</small>` : '';
        const found = line.origin === 'Found' ? ` <span class="badge bg-label-info">${esc(W('FoundBadge'))}</span>` : '';
        const status = `<span class="badge bg-label-${isCounted(line) ? 'success' : 'secondary'}" data-line-status>${esc(W(`CycleCount.LineStatus.${line.status}`))}</span>`;
        const entry = editable() && lineOpen(line)
            ? `<div class="d-flex align-items-center gap-2 mt-2 flex-wrap">
                   <button type="button" class="btn btn-label-secondary cc-touch" data-step="-1" aria-label="${esc(W('Decrease'))}"><i class="bx bx-minus"></i></button>
                   <input type="text" inputmode="decimal" class="form-control form-control-lg cc-qty" data-qty value="${esc(line.countedQuantity ?? '')}"
                          aria-label="${esc(W('FieldCounted'))}" autocomplete="off" />
                   <button type="button" class="btn btn-label-secondary cc-touch" data-step="1" aria-label="${esc(W('Increase'))}"><i class="bx bx-plus"></i></button>
                   <button type="button" class="btn btn-primary cc-touch" data-save><i class="bx bx-check me-1"></i>${esc(W('CycleCount.Action.Confirm'))}</button>
               </div>
               <small class="text-danger d-block mt-1" data-line-error></small>`
            : `<div class="mt-1 fs-5">${isCounted(line) ? esc(line.countedQuantity) : '<span class="text-muted">–</span>'}</div>`;
        return `<div class="cc-sheet-line" data-line-id="${esc(line.lineId)}">
                    <div class="d-flex justify-content-between gap-2">
                        <div class="min-w-0"><span class="fw-medium">${esc(line.sku || W('NameUnknown'))}</span>${found}${lot}${expected}</div>
                        <div>${status}</div>
                    </div>
                    ${entry}
                </div>`;
    };

    const draw = () => {
        const bins = new Map();
        lines.forEach((line) => {
            if (!bins.has(line.locationId)) bins.set(line.locationId, { name: line.location || W('NameUnknown'), lines: [] });
            bins.get(line.locationId).lines.push(line);
        });
        const container = byId('cc-sheet-bins');
        container.innerHTML = bins.size === 0
            ? `<div class="text-muted">${esc(W('NoLines'))}</div>`
            : [...bins.values()].map((bin) => `<div class="card"><div class="card-header py-2 d-flex justify-content-between">
                    <span class="fw-medium"><i class="bx bx-grid-alt me-1"></i>${esc(bin.name)}</span>
                    <small class="text-muted">${esc(format(W('BinProgress'), bin.lines.filter(isCounted).length, bin.lines.length))}</small></div>
                  <div class="card-body d-flex flex-column gap-3">${bin.lines.map(lineHtml).join('')}</div></div>`).join('');
        const uncounted = lines.filter((l) => lineOpen(l) && !isCounted(l)).length;
        const bar = byId('cc-uncounted');
        if (bar) bar.textContent = uncounted ? format(W('UncountedLines'), uncounted) : W('AllCounted');
        byId('cc-sheet-bar')?.classList.toggle('d-none', !editable());
    };

    const load = async () => {
        showAlert('cc-sheet-error', '');
        const head = await getJson(`${root}/count/${encodeURIComponent(countId)}`);
        if (!head.ok) { byId('cc-sheet-loading')?.classList.add('d-none'); showAlert('cc-sheet-error', failure(head)); return; }
        count = head.json.count;
        etag = head.json.etag;
        byId('cc-sheet-title').textContent = `${W('CountSheetTitle')} · ${count.number}`;
        byId('cc-sheet-subtitle').textContent = `${count.warehouse || ''} · ${W(`CycleCount.Status.${count.status}`)}`;
        lines = [];
        for (let page = 1; ; page++) {
            const answer = await getJson(`${root}/lines/${encodeURIComponent(countId)}?page=${page}`);
            if (!answer.ok) { showAlert('cc-sheet-error', failure(answer)); break; }
            lines = lines.concat(answer.json.items || []);
            if (lines.length >= (answer.json.total || 0) || !(answer.json.items || []).length) break;
        }
        byId('cc-sheet-loading')?.classList.add('d-none');
        // the blind notice only says why no expected quantity is shown; the absence comes from the service
        byId('cc-sheet-blind')?.classList.toggle('d-none', !(count.blind && ['Released', 'Counting'].includes(count.status)));
        const readonly = { Submitted: 'ReadonlySubmitted', Closed: 'ReadonlyClosed', Cancelled: 'ReadonlyCancelled', Draft: 'ReadonlyDraft' }[count.status];
        showAlert('cc-sheet-readonly', readonly ? W(readonly) : '');
        draw();
    };

    // ─── entry ──────────────────────────────────────────────────────────────
    const step = (row, delta) => {
        const input = row.querySelector('[data-qty]');
        const current = parseFloat(String(input.value || '0').replace(',', '.'));
        const next = Math.max(0, (Number.isFinite(current) ? current : 0) + delta);
        input.value = String(next);
    };

    const save = async (row) => {
        const lineId = row.getAttribute('data-line-id');
        const line = lines.find((l) => l.lineId === lineId);
        const input = row.querySelector('[data-qty]');
        const error = row.querySelector('[data-line-error]');
        const value = String(input.value || '').trim();
        error.textContent = '';
        if (!/^\d+([.,]\d+)?$/.test(value)) { error.textContent = W('CycleCount.Error.COUNT_QUANTITY_INVALID'); return; }
        if (!pending.has(lineId)) pending.set(lineId, newKey());
        const button = row.querySelector('[data-save]');
        button.disabled = true;
        const answer = await post(`${root}/record/${encodeURIComponent(countId)}/${encodeURIComponent(lineId)}`,
            { idempotencyKey: pending.get(lineId), ifMatch: line.etag, countedQuantity: value });
        button.disabled = false;
        if (!answer.ok) {
            error.textContent = failure(answer);
            if (answer.status === 409 || answer.status === 412) pending.delete(lineId);
            return;
        }
        pending.delete(lineId);
        Object.assign(line, answer.json.line, { location: line.location, sku: line.sku, lotNumber: line.lotNumber });
        if (count.status === 'Released') count.status = 'Counting';
        window.showToast?.(answer.json.message || '', 'success');
        draw();
    };

    // ─── found stock ────────────────────────────────────────────────────────
    let foundKey = null;
    const select2 = (el, url, data, map, parent) => {
        if (!el || !window.jQuery?.fn?.select2 || window.jQuery(el).hasClass('select2-hidden-accessible')) return;
        window.jQuery(el).select2({
            dropdownParent: window.jQuery(parent), width: '100%', allowClear: true, placeholder: window.jQuery(el).data('placeholder') || '',
            ajax: { url, dataType: 'json', delay: 250, data: (params) => Object.assign({ q: params.term || '' }, data ? data() : {}),
                    processResults: (answer) => ({ results: map(answer?.items || []) }) }
        });
    };

    const openFound = () => {
        if (!editable()) return;
        foundKey = newKey();
        showAlert('foundAlert', '');
        const scope = new Set(count.scopeBinIds || []);
        const names = new Map([...lines.map((l) => [l.locationId, l.location]), ...(count.scopeBins || []).map((b) => [b.id, b.name])].filter(([, n]) => n));
        const bin = byId('foundBin');
        bin.innerHTML = '<option value=""></option>' + [...scope].map((id) => `<option value="${esc(id)}">${esc(names.get(id) || W('NameUnknown'))}</option>`).join('');
        select2(byId('foundProduct'), `${root}/products`, null, (items) => items.map((i) => ({ id: i.value, text: i.sub ? `${i.text} · ${i.sub}` : i.text })), '#offcanvasFound');
        select2(byId('foundSku'), `${root}/product-skus`, () => ({ itemId: byId('foundProduct').value }), (items) => items.map((i) => ({ id: i.value, text: i.text })), '#offcanvasFound');
        // DL-4 FIX-01: the lot is CHOSEN by its number among the SKU's registered lots (value = the lot id the service needs)
        select2(byId('foundLot'), `${root}/lots`, () => {
            const sku = byId('foundSku').value || '';
            const split = sku.indexOf(':');
            return split > 0 ? { skuLevel: sku.slice(0, split), skuId: sku.slice(split + 1) } : {};
        }, (items) => items.map((i) => ({ id: i.value, text: i.text })), '#offcanvasFound');
        window.jQuery?.('#foundProduct').val(null).trigger('change');
        window.jQuery?.('#foundSku').val(null).trigger('change');
        window.jQuery?.('#foundLot').val(null).trigger('change');
        ['foundQuantity', 'foundScanRef'].forEach((id) => { byId(id).value = ''; });
        window.bootstrap?.Offcanvas.getOrCreateInstance(byId('offcanvasFound')).show();
    };

    const saveFound = async () => {
        showAlert('foundAlert', '');
        const sku = byId('foundSku').value || '';
        const split = sku.indexOf(':');
        const quantity = byId('foundQuantity').value.trim();
        if (!byId('foundBin').value || !byId('foundProduct').value || split < 1) { showAlert('foundAlert', W('ValidationFound')); return; }
        if (!/^\d+([.,]\d+)?$/.test(quantity)) { showAlert('foundAlert', W('CycleCount.Error.COUNT_QUANTITY_INVALID')); return; }
        const answer = await post(`${root}/add-line/${encodeURIComponent(countId)}`, {
            idempotencyKey: foundKey, locationId: byId('foundBin').value, itemId: byId('foundProduct').value,
            skuLevel: sku.slice(0, split), skuId: sku.slice(split + 1), lotId: byId('foundLot').value || null,
            countedQuantity: quantity, scanRef: byId('foundScanRef').value.trim() || null
        });
        if (!answer.ok) { showAlert('foundAlert', failure(answer)); if (answer.status === 409) foundKey = newKey(); return; }
        window.bootstrap?.Offcanvas.getOrCreateInstance(byId('offcanvasFound')).hide();
        window.showToast?.(answer.json.message || '', 'success');
        await load();
    };

    // ─── submit ─────────────────────────────────────────────────────────────
    const submit = () => {
        if (!editable()) return;
        const uncounted = lines.filter((l) => lineOpen(l) && !isCounted(l)).length;
        const text = uncounted ? format(W('SubmitConfirmUncounted'), uncounted) : W('SubmitConfirm');
        const run = async () => {
            const answer = await post(`${root}/submit/${encodeURIComponent(countId)}`, { idempotencyKey: newKey(), ifMatch: etag });
            if (!answer.ok) { showAlert('cc-sheet-error', failure(answer)); return; }
            window.showToast?.(answer.json.message || '', 'success');
            await load();
        };
        window.showConfirm?.(text, run, { entityName: count.number, type: 'warning', confirmButtonText: W('CycleCount.Action.Submit') });
    };

    const bind = () => {
        byId('cc-sheet-bins')?.addEventListener('click', (event) => {
            const row = event.target.closest('.cc-sheet-line');
            if (!row) return;
            const stepButton = event.target.closest('[data-step]');
            if (stepButton) { step(row, Number(stepButton.getAttribute('data-step'))); return; }
            if (event.target.closest('[data-save]')) save(row);
        });
        byId('cc-sheet-bins')?.addEventListener('keydown', (event) => {
            if (event.key === 'Enter' && event.target.matches('[data-qty]')) { event.preventDefault(); save(event.target.closest('.cc-sheet-line')); }
        });
        window.jQuery?.('#foundProduct').on('change', () => window.jQuery('#foundSku').val(null).trigger('change'));
        window.jQuery?.('#foundSku').on('change', () => window.jQuery('#foundLot').val(null).trigger('change'));
        byId('btnAddFound')?.addEventListener('click', openFound);
        byId('btnSaveFound')?.addEventListener('click', saveFound);
        byId('btnSubmitCount')?.addEventListener('click', submit);
    };

    return { init: () => { if (!host || !countId) return; bind(); load(); } };
})();

document.addEventListener('DOMContentLoaded', () => CountSheet.init());
