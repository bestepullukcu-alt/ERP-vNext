/**
 * MOD-0174 P3 — the quick lot of the Stock Ledger lot field (pack §11.4, CT-D5a): the _LotQuickCreateModal partial (same
 * field rules as Yeni Lot, the SKU fixed to the movement's) posts to the same-origin /Lots/create (origin
 * "ledger-shortcut"); the saved lot is DRAFT and is handed back to the form, which selects it. The MVC repeats the field
 * rules (LotFormRules) and createLot enforces trace.lots.create. Nothing here runs until open() — this file is loaded by
 * the modal partial inside the page body, before the page's own libraries.
 */
'use strict';

window.QuickLot = (function () {
    const byId = (id) => document.getElementById(id);
    let current = null;
    let busy = false;

    const value = (id) => (byId(id)?.value || '').trim();
    const clearErrors = () => {
        document.querySelectorAll('#modalQuickLot [data-quick-error]').forEach((n) => { n.textContent = ''; });
        byId('quick-lot-alert')?.classList.add('d-none');
    };
    const showError = (message) => {
        const box = byId('quick-lot-alert');
        if (!box) return;
        box.querySelector('[data-alert-text]').textContent = message || '';
        box.classList.remove('d-none');
    };

    const save = async () => {
        if (busy || !current) return;
        clearErrors();
        busy = true;
        const button = byId('btnQuickLotSave');
        if (button) button.disabled = true;
        const lot = {
            idempotencyKey: value('quickLotKey'),
            itemId: current.itemId,
            sku: current.sku,
            lotNumber: value('quickLotNumber').toUpperCase(),
            manufacturerLot: value('quickLotManufacturerLot'),
            supplierLot: value('quickLotSupplierLot'),
            manufacturingDate: value('quickLotManufacturingDate'),
            expiryDate: value('quickLotExpiryDate'),
            retestDate: value('quickLotRetestDate'),
            countryOfOrigin: value('quickLotCountry'),
            coaReference: value('quickLotCoa'),
            origin: 'ledger-shortcut'
        };
        // WP-0176-F5 (KF-D): the same date rule as Yeni Lot — the override (+ the MOD-0022 signature when asked) before createLot
        if (window.ExpiryBands) {
            const band = await window.ExpiryBands.lot.beforeSave('quick', lot.lotNumber);
            if (!band.ok) {
                busy = false;
                if (button) button.disabled = false;
                showError(band.message || '');
                return;
            }
            if (band.dateOverride) lot.dateOverride = band.dateOverride;
        }
        let reply = null;
        try {
            const res = await fetch('/Lots/create', {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', Accept: 'application/json', RequestVerificationToken: current.token || '' },
                body: JSON.stringify(lot)
            });
            reply = { ok: res.ok, json: await res.json().catch(() => null) };
        } catch (error) {
            reply = { ok: false, json: null };
        }
        busy = false;
        if (button) button.disabled = false;

        if (reply.ok && reply.json?.success) {
            const done = current.onCreated;
            const modal = byId('modalQuickLot');
            if (modal && window.bootstrap) window.bootstrap.Modal.getOrCreateInstance(modal).hide();
            // A new key for the next quick lot of this page (one key = one lot).
            const key = byId('quickLotKey');
            if (key) key.value = window.crypto?.randomUUID?.() || key.value;
            done?.(reply.json);
            return;
        }
        Object.entries(reply.json?.fields || {}).forEach(([field, message]) => {
            const node = document.querySelector(`#modalQuickLot [data-quick-error="${field}"]`);
            if (node) node.textContent = message;
        });
        const error = reply.json?.error;
        if (error?.field) {
            const node = document.querySelector(`#modalQuickLot [data-quick-error="${error.field}"]`);
            if (node) node.textContent = error.message || '';
        }
        showError(window.ExpiryBands?.lot.refusal(reply.json) || error?.message || '');
    };

    let band = null; // WP-0176-F5: the date band of the quick lot (attached once, reads the SKU of the opened movement)
    const open = (options) => {
        const modal = byId('modalQuickLot');
        if (!modal || !window.bootstrap) return;
        current = options;
        clearErrors();
        ['quickLotNumber', 'quickLotManufacturerLot', 'quickLotSupplierLot', 'quickLotManufacturingDate', 'quickLotExpiryDate', 'quickLotRetestDate', 'quickLotCountry', 'quickLotCoa',
            'ex-band-quick-reason']
            .forEach((id) => { const n = byId(id); if (n) n.value = ''; });
        const sku = byId('quickLotSku');
        if (sku) sku.textContent = [options.itemText, options.skuText].filter(Boolean).join(' · ');
        band = band || window.ExpiryBands?.lot.attach('quick', {
            manufacturing: 'quickLotManufacturingDate', expiry: 'quickLotExpiryDate', retest: 'quickLotRetestDate', band: 'ex-band-quick', use: 'ex-band-quick-use',
            reasonGroup: 'ex-band-quick-reason-group', reason: 'ex-band-quick-reason', note: 'ex-band-quick-note'
        }, () => ({ itemId: current?.itemId, sku: current?.sku }));
        band?.();
        window.bootstrap.Modal.getOrCreateInstance(modal).show();
    };

    document.addEventListener('DOMContentLoaded', () => byId('btnQuickLotSave')?.addEventListener('click', save));

    return { open };
})();
