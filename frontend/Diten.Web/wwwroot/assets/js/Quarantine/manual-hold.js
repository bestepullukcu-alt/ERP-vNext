/**
 * WP-MVP1-FU-CLEAN1 (FU-0175-UI-MANUALHOLD-01) — "Stoğu karantinaya al" on the quarantine queue: quarantineStock, mode MANUAL, one bucket of a
 * chosen AVAILABLE stock row (QUARANTINE-BUNDLE QuarantineStockRequest / Bucket). The rules (QuarantineManualHoldRules) are pure and measured
 * by Node; the page part draws only when the modal is on the page (the view includes it for quarantine.holds.create only — UAS-001).
 * The confirmation body is markup → subtextHtml (the chosen line through textContent); the reason (≥ 10) is required by the dialog.
 */
'use strict';

const QuarantineManualHoldRules = (() => {
    /** A quantity the bucket can take: a number above 0, at most the row's on-hand; '' / NaN / 0 / above → the error. */
    const quantityError = (value, row) => {
        if (!row) return true;
        const n = Number(String(value ?? '').replace(',', '.'));
        return !(Number.isFinite(n) && n > 0 && n <= Number(row.onHand));
    };

    /** The quarantineStock body of a Manual hold of ONE row (the contract's required fields; never a status or a mode beside Manual). */
    const request = (row, quantity, reason, idempotencyKey) => ({
        mode: 'Manual',
        buckets: [{ itemId: row.itemId, skuId: row.skuId, skuLevel: row.skuLevel, warehouseId: row.warehouseId, locationId: row.locationId,
            quantity: Number(String(quantity).replace(',', '.')), lotId: row.lotId }],
        reason: String(reason ?? '').trim(),
        idempotencyKey
    });

    /** The line the confirmation shows (text). */
    const line = (row, quantity) => [row.item, `${row.warehouse || row.warehouseId} · ${row.location || row.locationId}`, `${quantity} ${row.baseUomId || ''}`.trim()]
        .filter(Boolean).join(' — ');

    const reasonError = (value) => String(value ?? '').trim().length < 10;

    return { quantityError, request, line, reasonError };
})();

if (typeof window !== 'undefined') window.QuarantineManualHoldRules = QuarantineManualHoldRules;

(function () {
    if (typeof document === 'undefined' || !window.QuarantineUi) return;
    const ui = window.QuarantineUi;
    const rules = QuarantineManualHoldRules;
    const t = ui.t;
    const modal = () => ui.byId('qr-manual-modal');
    if (!modal()) return;
    let rows = [];
    let key = null;

    const loadStock = async (lotId) => {
        const select = ui.byId('qr-manual-row');
        select.innerHTML = '<option value=""></option>';
        select.disabled = true;
        ui.byId('qr-manual-norows').classList.add('d-none');
        ui.byId('qr-manual-available').textContent = '';
        if (!lotId) return;
        const reply = await ui.get(`/Quarantine/lookups/stock?${new URLSearchParams({ lotId })}`);
        rows = reply.ok ? (reply.json?.items || []) : [];
        rows.forEach((r, i) => select.add(new Option(`${r.warehouse || r.warehouseId} · ${r.location || r.locationId} — ${ui.qty(r.onHand)} ${r.baseUomId || ''}`, String(i))));
        select.disabled = rows.length === 0;
        ui.byId('qr-manual-norows').classList.toggle('d-none', rows.length > 0);
    };

    const chosen = () => {
        const value = ui.byId('qr-manual-row').value;
        return value === '' ? undefined : rows[Number(value)];
    };

    const open = () => {
        const form = ui.byId('qr-manual-form');
        form.reset();
        ui.clearFieldErrors(form);
        ui.hideError(ui.byId('qr-manual-error'));
        key = ui.newKey();
        rows = [];
        const select = ui.byId('qr-manual-row');
        select.innerHTML = '<option value=""></option>';
        select.disabled = true;
        ui.byId('qr-manual-uom').textContent = '';
        ui.byId('qr-manual-available').textContent = '';
        if (window.jQuery?.fn?.select2) window.jQuery('#qr-manual-lot').val(null).trigger('change');
        window.bootstrap.Modal.getOrCreateInstance(modal()).show();
    };

    const confirm = (row, quantity) => {
        const template = ui.byId('qr-manual-confirm-template');
        const body = template.content.firstElementChild.cloneNode(true);
        body.querySelector('[data-manual-line]').textContent = rules.line(row, quantity);
        window.showConfirm?.(t('Quarantine.ManualHold.ConfirmTitle'), async (reason) => {
            const reply = await ui.write('POST', 'holds', rules.request(row, quantity, reason, key));
            if (!reply.ok) {
                window.DitenModal?.error({ title: t('Quarantine.ManualHold.Action'), message: ui.errorText(reply) });
                return;
            }
            ui.toast(t('Quarantine.ManualHold.Done', (reply.json || []).map((h) => h.holdNumber).join(', ')), 'success');
            window.QuarantineQueue?.reload?.();
            ui.loadCounts('QUARANTINEQUEUE');
        }, {
            type: 'warning',
            subtextHtml: body.outerHTML,
            confirmButtonText: t('Quarantine.Action.Quarantine'),
            cancelButtonText: t('Quarantine.Common.Cancel'),
            showInput: true,
            inputType: 'textarea',
            inputLabel: t('Quarantine.Field.Reason'),
            inputAttributes: { maxlength: 1000 },
            inputValidator: (value) => (rules.reasonError(value) ? t('Quarantine.Common.ReasonMin') : null)
        });
    };

    const init = () => {
        const lot = ui.byId('qr-manual-lot');
        if (window.jQuery?.fn?.select2) {
            window.jQuery(lot).select2({
                dropdownParent: window.jQuery(modal()),
                placeholder: lot.dataset.placeholder || '',
                allowClear: true,
                width: '100%',
                ajax: {
                    url: '/Quarantine/lookups/lots',
                    dataType: 'json',
                    delay: 250,
                    data: (params) => ({ q: params.term || '' }),
                    processResults: (data) => ({ results: (data?.items || []).map((i) => ({ id: i.value, text: i.text || i.value })) })
                }
            }).on('change', () => loadStock(lot.value));
        } else {
            lot.addEventListener('change', () => loadStock(lot.value));
        }
        ui.byId('qr-manual-row').addEventListener('change', () => {
            const row = chosen();
            ui.byId('qr-manual-uom').textContent = row?.baseUomId || '';
            ui.byId('qr-manual-available').textContent = row ? t('Quarantine.BlockStock.Available', ui.qty(row.onHand), row.baseUomId || '') : '';
        });
        ui.byId('qr-manual-form').addEventListener('submit', (event) => {
            event.preventDefault();
            const form = ui.byId('qr-manual-form');
            ui.clearFieldErrors(form);
            const row = chosen();
            const quantity = ui.byId('qr-manual-qty').value;
            if (rules.quantityError(quantity, row)) {
                ui.fieldError(form, 'qty', t('Quarantine.BlockStock.QtyInvalid'));
                return;
            }
            window.bootstrap.Modal.getOrCreateInstance(modal()).hide();
            confirm(row, quantity);
        });
    };

    init();
    window.QuarantineManualHold = { open };
})();
