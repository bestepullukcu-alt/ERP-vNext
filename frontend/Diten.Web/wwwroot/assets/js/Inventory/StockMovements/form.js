/**
 * MOD-0173-FU01 UI-2 — the ONE movement form: S-3 New movement, Edit = "Correct" (KD-1) and StockLedger/Create
 * (KD-2, inbound types only). One script for the one _Form partial.
 *
 * DIRECTION MATRIX (pack §12): the movement type decides which source / target fields exist. A field the matrix hides
 * is hidden, not required and never posted; the MVC proxy applies the same matrix (MovementRules.Check) and MOD-0173
 * decides everything else — its refusal comes back to the field it is about.
 *
 * NO ARITHMETIC ON QUANTITIES HERE (UI-13): the base-unit hint, the serial counter's "required" and the stock-effect
 * panel are computed server-side in decimal (/StockMovements/lookups/product, /effect). This file formats decimal
 * strings for display and compares a COUNT of serial lines with the server's base quantity string — nothing else.
 *
 * ONE SUBMIT PER FORM: the Idempotency-Key was minted when the page opened; a double click is one request (busy
 * flag) and a second submit of the same form is a replay that shows the ORIGINAL receipt (pack §13).
 *
 * The browser talks only to same-origin /StockMovements/*: never to the Gateway, the inventory service or MDM.
 */
'use strict';

const StockMovementForm = (function () {
    const form = document.getElementById('formStockMovement');
    const byId = (id) => document.getElementById(id);

    const L10N = (() => {
        const payload = byId('stockmovement-form-l10n');
        const out = {};
        try {
            const raw = JSON.parse(payload?.textContent || '{}');
            Object.keys(raw).forEach((key) => { out[key.charAt(0).toUpperCase() + key.slice(1)] = raw[key]; });
        } catch (error) {
            console.error('[StockMovementForm] Localization payload could not be parsed.', error);
        }
        return out;
    })();
    const t = (key) => L10N[key] || '';
    // Each inserted value is a bidi ISOLATE (FSI…PDI): "2 KOLI = 24 ADET" inside an Arabic sentence otherwise reorders
    // its numbers and codes. Invisible in left-to-right languages.
    const fmt = (text, ...args) => String(text || '').replace(/\{(\d)\}/g, (match, index) => `⁨${args[index] ?? ''}⁩`);
    // MOD-0174 P3 — the lot field's own vocabulary (resx family LotFieldIndex, rendered by _Form as #lotfield-l10n).
    const LOT_L10N = (() => {
        try { return JSON.parse(byId('lotfield-l10n')?.textContent || '{}'); } catch (error) { return {}; }
    })();
    const lt = (key, ...args) => fmt(LOT_L10N[key], ...args);

    const lang = document.documentElement.lang || undefined;
    const numberFormat = new Intl.NumberFormat(lang, { maximumFractionDigits: 6 });
    const qty = (value) => {
        if (value === null || value === undefined || value === '') return '';
        try { return numberFormat.format(String(value)); } catch (error) { return String(value); }
    };

    const QUANTITY = /^\d+(\.\d{1,6})?$/;
    const ZERO = /^0+(\.0+)?$/;
    const LOT = /^[A-Z0-9./-]{1,40}$/;
    // MOD-0174 P3 — the serial shape of the approved mockup (pack §12 UI rules): 3–24 of A–Z 0–9 -.
    const SERIAL = /^[A-Z0-9-]{3,24}$/;
    // A GS1 / ITS code typed or scanned into the "one by one" box goes through the code check (K-B).
    const GS1 = /^\(01\)|^01\d{14}|\u001d/;
    const DIRECTION = {
        OPENING_BALANCE: 'in', COUNT_GAIN: 'in',
        COUNT_LOSS: 'out', SCRAP: 'out', WRITE_OFF: 'out',
        WAREHOUSE_TRANSFER: 'wh', LOCATION_TRANSFER: 'loc'
    };
    // Which conditional block each direction shows (pack §12 matrix; the lot of an outbound / transfer is the source's).
    const MATRIX = {
        in: { source: false, 'target-warehouse': true, 'target-location': true, 'target-status': true, lot: true },
        out: { source: true, 'target-warehouse': false, 'target-location': false, 'target-status': false, lot: false },
        wh: { source: true, 'target-warehouse': true, 'target-location': true, 'target-status': false, lot: false },
        loc: { source: true, 'target-warehouse': true, 'target-location': true, 'target-status': false, lot: false }
    };

    const el = {
        type: byId('movementType'), item: byId('itemId'), sku: byId('sku'), source: byId('source'),
        toWarehouse: byId('toWarehouseId'), toLocation: byId('toLocationId'), toStatus: byId('toStockStatus'),
        lot: byId('lotId'), lotTyped: byId('lotIdTyped'), serials: byId('serialNumbers'), quantity: byId('quantity'), uom: byId('uomId'),
        documentNo: byId('documentNo'), transactionDate: byId('transactionDate'), postingDate: byId('postingDate'),
        submit: byId('btnMovementSubmit')
    };

    const state = {
        facts: null,            // { available, lotControlled, serialControlled, baseUomId, units }
        sources: new Map(),     // source key → { warehouseId, locationId, lotId, stockStatus, onHand, baseUomId }
        baseQuantity: null,     // the server's base quantity for (quantity, unit), a decimal STRING
        busy: false,
        partial: false,         // KD-1: the reversal is posted, the corrected movement is not yet
        lastBody: null,
        lots: new Map(),        // MOD-0174 P3: registered lot id → picker option { lotNumber, status, disabled, retestDue, … }
        pendingLot: ''          // a lot to select once the SKU's lots are loaded (prefill, quick lot)
    };

    const direction = () => DIRECTION[el.type?.value] || null;
    const hasSelect2 = () => !!window.jQuery?.fn?.select2;

    // ── fetch helpers (same origin only) ───────────────────────────────────────────────────────────────────
    const getJson = async (url) => {
        try {
            const res = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
            return res.ok ? await res.json() : null;
        } catch (error) {
            return null;
        }
    };
    const lookup = (kind, params) => {
        const query = new URLSearchParams();
        Object.entries(params || {}).forEach(([key, value]) => { if (value !== null && value !== undefined && value !== '') query.set(key, value); });
        return getJson(`/StockMovements/lookups/${kind}?${query.toString()}`);
    };

    // ── select2 ───────────────────────────────────────────────────────────────────────────────────────────
    const select2 = (select, extra) => {
        if (!select || !hasSelect2()) return;
        const $el = $(select);
        if ($el.hasClass('select2-hidden-accessible')) $el.select2('destroy');
        $el.select2(Object.assign({
            dropdownParent: $el.closest('.diten-field'),
            width: '100%',
            placeholder: select.dataset.placeholder || '',
            language: { searching: () => t('Searching'), noResults: () => t('NoResults') }
        }, extra || {}));
    };
    // Rebuild a dependent select's options (form template: no greyed-out choices) and keep select2 in step WITHOUT
    // firing the page's own change handlers — a rebuild is not a reader's choice.
    const fill = (select, items, selected) => {
        if (!select) return;
        select.innerHTML = '';
        select.add(new Option('', ''));
        (items || []).forEach((item) => select.add(new Option(item.text, item.value, false, item.value === selected)));
        select.value = selected && (items || []).some((item) => item.value === selected) ? selected : '';
        if (hasSelect2()) $(select).trigger('change.select2');
    };
    const ensureOption = (select, value, text) => {
        if (!select || !value) return;
        if (!Array.from(select.options).some((o) => o.value === value)) select.add(new Option(text || value, value));
        select.value = value;
        if (hasSelect2()) $(select).trigger('change.select2');
    };
    const setEnabled = (select, enabled) => {
        if (!select) return;
        select.disabled = !enabled;
        if (hasSelect2()) $(select).prop('disabled', !enabled);
    };
    const hint = (id, text) => {
        const node = form.querySelector(`[data-hint-for="${id}"]`);
        if (node) node.textContent = text || '';
    };

    // ── errors (pack §13: the sentence sits under its field; no `was-validated` — it would re-pad select2) ──
    const clearErrors = () => {
        form.querySelectorAll('[data-error-for]').forEach((node) => { node.textContent = ''; });
        form.querySelectorAll('.is-invalid').forEach((node) => node.classList.remove('is-invalid'));
        ['movement-form-alert', 'movement-page-alert'].forEach((id) => byId(id)?.classList.add('d-none'));
        byId('lnkMovementRestart')?.classList.add('d-none');
    };
    const fieldError = (id, message) => {
        const node = form.querySelector(`[data-error-for="${id}"]`);
        if (!node) return false;
        node.textContent = message || '';
        const control = byId(id);
        if (control && !control.classList.contains('select2-hidden-accessible')) control.classList.add('is-invalid');
        return true;
    };
    const showAlert = (id, message, correlationId) => {
        const box = byId(id);
        if (!box) return;
        box.querySelector('[data-alert-text]').textContent = message || '';
        const reference = box.querySelector('[data-alert-reference]');
        if (reference) {
            reference.classList.toggle('d-none', !correlationId);
            reference.querySelector('[data-alert-correlation]').textContent = correlationId || '';
        }
        box.classList.remove('d-none');
        box.scrollIntoView({ behavior: 'smooth', block: 'center' });
    };

    // ── the direction matrix ────────────────────────────────────────────────────────────────────────────
    const applyType = () => {
        const dir = direction();
        const shown = dir ? MATRIX[dir] : { source: false, 'target-warehouse': false, 'target-location': false, 'target-status': false, lot: false };
        form.querySelector('[data-matrix-empty]')?.classList.toggle('d-none', !!dir);
        form.querySelectorAll('[data-matrix]').forEach((block) => {
            const visible = !!shown[block.getAttribute('data-matrix')];
            block.classList.toggle('d-none', !visible);
            block.querySelectorAll('select, input, textarea').forEach((control) => {
                // The lot block's own controls (picker, typed text, retest confirmation) are required by the facts, not the matrix.
                control.required = visible && !['lotId', 'lotIdTyped', 'lotRetestConfirm'].includes(control.id);
                control.setAttribute('aria-required', control.required ? 'true' : 'false');
            });
        });
        // LOCATION_TRANSFER: the warehouse is the source's own — shown, locked (pack §12 matrix).
        const locked = dir === 'loc';
        setEnabled(el.toWarehouse, !locked);
        hint('toWarehouseId', locked ? t('HintLocationTransferWarehouse') : '');
        if (locked) syncWarehouseFromSource();
        applyFacts();
        if (dir && dir !== 'in') loadSources();
        refreshEffect();
    };

    // ── product → SKU → facts, sources, lots ───────────────────────────────────────────────────────────
    const productUnavailable = (message) => {
        const box = byId('movement-product-unavailable');
        if (!box) return;
        box.textContent = message || '';
        box.classList.toggle('d-none', !message);
    };

    const loadSkus = async (prefill, prefillText) => {
        const itemId = el.item.value;
        fill(el.sku, [], '');
        setEnabled(el.sku, false);
        if (!itemId) { hint('sku', t('HintSelectProductFirst')); return; }
        const data = await lookup('skus', { itemId });
        const items = data?.items || [];
        fill(el.sku, items, prefill);
        // A prefilled SKU comes from a ledger row (S-1 "Movement", S-2 "Correct"): kept even if the picker cannot list it.
        if (prefill && el.sku.value !== prefill) ensureOption(el.sku, prefill, prefillText);
        setEnabled(el.sku, true);
        hint('sku', data === null || data.available === false ? t('PickerUnavailable') : items.length === 0 && !prefill ? t('HintSkuNone') : '');
    };

    const loadFacts = async () => {
        state.facts = null;
        state.baseQuantity = null;
        fill(el.uom, [], '');
        setEnabled(el.uom, false);
        productUnavailable('');
        if (!el.item.value || !el.sku.value) { applyFacts(); return; }
        const facts = await lookup('product', { itemId: el.item.value, sku: el.sku.value });
        state.facts = facts;
        if (!facts || facts.available === false) {
            productUnavailable(facts?.message || t('PickerUnavailable'));
        } else if (facts.blocked) {
            // WP-0173-PM1: PRODUCT-MASTER does not state a lot / serial flag (or a Gsku of a serial-tracked product) — the
            // form does not send; the service refuses the same way (422).
            productUnavailable(facts.blockedMessage || t('PickerUnavailable'));
        } else {
            const prefill = form.dataset.prefillUom || el.uom.value || facts.baseUomId || '';
            fill(el.uom, (facts.units || []).map((u) => ({ value: u, text: u })), prefill);
            setEnabled(el.uom, true);
        }
        applyFacts();
        refreshConversion();
    };

    // Lot required for a lot-controlled inbound; the serial box only for a serial-controlled product (pack §12).
    const applyFacts = () => {
        const facts = state.facts;
        const inbound = direction() === 'in';
        const lotRequired = !!(facts?.lotControlled && inbound);
        form.querySelector('[data-lot-required]')?.classList.toggle('d-none', !lotRequired);
        if (el.lot) { el.lot.required = lotRequired; el.lot.setAttribute('aria-required', lotRequired ? 'true' : 'false'); }
        const serial = !!facts?.serialControlled;
        form.querySelector('[data-serial-field]')?.classList.toggle('d-none', !serial);
        if (el.serials) el.serials.required = serial;
        updateSerialCounter();
    };

    const loadSources = async () => {
        const dir = direction();
        state.sources.clear();
        const keep = el.source.value || form.dataset.prefillSource || '';
        fill(el.source, [], '');
        setEnabled(el.source, false);
        if (!dir || dir === 'in') return;
        if (!el.sku.value) { hint('source', t('HintSourceSelectSku')); return; }
        const data = await lookup('sources', { sku: el.sku.value });
        const items = data?.items || [];
        items.forEach((item) => state.sources.set(item.value, item));
        fill(el.source, items, keep);
        setEnabled(el.source, items.length > 0);
        hint('source', data === null || data.available === false ? t('PickerUnavailable') : items.length === 0 ? t('HintSourceNone') : '');
        relabelSources();
        onSourceChanged();
    };

    // MOD-0174 P3 (pack §11.4): the SKU's REGISTERED lots — "lot no · SKT · status", sorted by SKT; Blocked / Expired
    // listed but disabled with the reason (CT-D4). Same-origin /Lots/form/lots (TRACE listLots); the decision of what is
    // selectable is the server's (LotFieldRules.PickerOptions), this file only draws it.
    // A disabled lot shows its reason IN PLACE of the status ("Bloke — stok alamaz" already names the status).
    const lotText = (o) => [o.lotNumber, o.expiryDate ? String(o.expiryDate).slice(0, 10) : null,
        o.disabled && o.disabledReason ? o.disabledReason : o.statusText].filter(Boolean).join(' · ');
    const typedLotOpen = () => !!byId('lotTypedGroup') && !byId('lotTypedGroup').classList.contains('d-none');
    const currentLot = () => {
        const typed = typedLotOpen() ? (el.lotTyped?.value || '').trim().toUpperCase() : '';
        return typed || el.lot?.value || '';
    };
    const showTypedLot = (text) => {
        byId('lotTypedGroup')?.classList.remove('d-none');
        byId('lotTypedHint')?.classList.remove('d-none');
        if (el.lotTyped && text !== undefined) el.lotTyped.value = text;
    };
    const loadLots = async () => {
        if (!el.lot) return;
        state.lots.clear();
        const keep = state.pendingLot || el.lot.value || '';
        el.lot.innerHTML = '';
        el.lot.add(new Option('', ''));
        if (!el.sku.value) { hint('lotId', ''); if (hasSelect2()) $(el.lot).trigger('change.select2'); onLotChanged(); return; }
        const data = await getJson(`/Lots/form/lots?${new URLSearchParams({ sku: el.sku.value }).toString()}`);
        const items = data?.items || [];
        items.forEach((o) => {
            state.lots.set(o.value, o);
            const option = new Option(lotText(o), o.value, false, false);
            option.disabled = !!o.disabled;
            el.lot.add(option);
        });
        el.lot.value = keep && state.lots.has(keep) && !state.lots.get(keep).disabled ? keep : '';
        state.pendingLot = '';
        hint('lotId', data === null || data.available === false ? (data?.message || lt('Lot.Unavailable')) : items.length === 0 ? lt('Lot.None') : '');
        if (hasSelect2()) $(el.lot).trigger('change.select2');
        relabelSources();
        onLotChanged();
    };
    // K-F: a lot whose retest date has passed — a warning and an explicit confirmation before it can be used.
    const onLotChanged = () => {
        const option = state.lots.get(el.lot?.value || '');
        const warn = byId('lotRetestWarn');
        if (warn) {
            const show = !typedLotOpen() && !!option?.retestDue;
            warn.classList.toggle('d-none', !show);
            warn.querySelector('[data-retest-text]').textContent = show ? lt('Lot.RetestWarn', option.retestDate ? String(option.retestDate).slice(0, 10) : '') : '';
            const box = byId('lotRetestConfirm');
            if (box && !show) box.checked = false;
        }
        refreshEffect();
    };
    // A source row of a registered lot is named by its lot NUMBER, not its id (the ledger stores the id, ASSUMPTION-0174-03).
    const relabelSources = () => {
        Array.from(el.source?.options || []).forEach((option) => {
            const row = state.sources.get(option.value);
            const lot = row?.lotId ? state.lots.get(row.lotId) : null;
            if (lot && option.text.includes(row.lotId)) option.text = option.text.replace(row.lotId, lot.lotNumber);
        });
        if (hasSelect2() && el.source) $(el.source).trigger('change.select2');
    };

    const syncWarehouseFromSource = () => {
        const row = state.sources.get(el.source.value);
        if (direction() !== 'loc' || !row) return;
        // Only a DIFFERENT warehouse invalidates the chosen target location (a prefilled one stays).
        const changed = el.toWarehouse.value !== row.warehouseId;
        const option = Array.from(el.toWarehouse.options).find((o) => o.value === row.warehouseId);
        ensureOption(el.toWarehouse, row.warehouseId, option?.text || row.warehouseId);
        if (changed) resetTargetLocation();
        else setEnabled(el.toLocation, true);
    };

    const onSourceChanged = () => {
        const row = state.sources.get(el.source.value);
        hint('source', row ? fmt(t('HintSourceStock'), qty(row.onHand), row.baseUomId || '') : form.querySelector('[data-hint-for="source"]')?.textContent);
        syncWarehouseFromSource();
        refreshEffect();
    };

    // ── targets ─────────────────────────────────────────────────────────────────────────────────────────
    const loadTargetWarehouses = async () => {
        const data = await lookup('targetwarehouses', {});
        const keep = form.dataset.prefillToWarehouse || '';
        fill(el.toWarehouse, data?.items || [], keep);
        if (keep && el.toWarehouse.value !== keep) ensureOption(el.toWarehouse, keep, form.dataset.prefillToWarehouseText || keep);
        if (data === null || data.available === false) hint('toWarehouseId', t('PickerUnavailable'));
    };

    const resetTargetLocation = () => {
        if (!el.toLocation) return;
        fill(el.toLocation, [], '');
        setEnabled(el.toLocation, !!el.toWarehouse.value);
        hint('toLocationId', t('HintTargetLocation'));
    };

    const initTargetLocationPicker = () => {
        select2(el.toLocation, {
            ajax: {
                url: '/StockMovements/lookups/targetlocations',
                dataType: 'json',
                delay: 250,
                data: (params) => ({ warehouseId: el.toWarehouse.value, q: params.term || '' }),
                processResults: (data) => {
                    const items = data?.items || [];
                    hint('toLocationId', data?.available === false ? t('PickerIncomplete') : items.length === 0 ? t('HintTargetLocationNone') : t('HintTargetLocation'));
                    return { results: items.map((item) => ({ id: item.value, text: item.text })) };
                },
                error: (xhr) => { if (xhr?.statusText !== 'abort') hint('toLocationId', t('PickerUnavailable')); }
            }
        });
    };

    // ── quantity → base unit hint, serial counter (server-computed, UI-13) ─────────────────────────────────
    let conversionTimer = null;
    const refreshConversion = () => {
        window.clearTimeout(conversionTimer);
        conversionTimer = window.setTimeout(async () => {
            state.baseQuantity = null;
            const quantity = el.quantity.value.trim();
            const uom = el.uom.value;
            const facts = state.facts;
            if (!facts || facts.available === false || !uom || !QUANTITY.test(quantity) || ZERO.test(quantity)) {
                hint('uomId', '');
                updateSerialCounter();
                return;
            }
            const data = await lookup('product', { itemId: el.item.value, sku: el.sku.value, quantity, uomId: uom });
            if (data?.convertible) {
                state.baseQuantity = data.baseQuantity;
                hint('uomId', uom === data.baseUomId ? '' : fmt(t('HintUnitBase'), qty(quantity), uom, qty(data.baseQuantity), data.baseUomId));
            } else if (data && data.available !== false) {
                hint('uomId', t('HintUnitNoConversion'));
            }
            updateSerialCounter();
            refreshEffect();
        }, 300);
    };

    const serialLines = () => (el.serials?.value || '').split('\n').map((line) => line.trim()).filter((line) => line.length > 0);
    const updateSerialCounter = () => {
        if (!state.facts?.serialControlled) { hint('serialNumbers', ''); return; }
        const count = serialLines().length;
        hint('serialNumbers', state.baseQuantity !== null
            ? fmt(t('HintSerialCounter'), count, qty(state.baseQuantity))
            : fmt(t('HintSerialCounterUnknown'), count));
        // MOD-0174 P3 (K-B): the counter turns green when the serial count equals the quantity (a COUNT, not arithmetic).
        const node = form.querySelector('[data-hint-for="serialNumbers"]');
        const equal = state.baseQuantity !== null && String(count) === String(state.baseQuantity);
        node?.classList.toggle('text-success', equal);
        if (node && state.baseQuantity !== null && !equal && /^\d+$/.test(String(state.baseQuantity))) {
            const diff = count - Number(state.baseQuantity);
            node.textContent += ' · ' + (diff > 0 ? lt('Serial.CounterOver', diff) : lt('Serial.CounterMore', -diff));
        } else if (node && equal) {
            node.textContent += ' · ' + lt('Serial.CounterOk');
        }
    };

    // ── MOD-0174 P3 — serial entry, three methods (K-B); every one ADDS to the one list ─────────────────────
    const serialMessage = (lines) => {
        const box = byId('serialMessages');
        if (box) box.textContent = lines.filter(Boolean).join(' ');
    };
    const addSerials = (list) => {
        const existing = new Set(serialLines().map((s) => s.toUpperCase()));
        const added = [];
        const duplicates = [];
        const bad = [];
        list.forEach((raw) => {
            const serial = String(raw || '').trim().toUpperCase();
            if (!serial) return;
            if (!SERIAL.test(serial)) { bad.push(serial); return; }
            if (existing.has(serial)) { duplicates.push(serial); return; }
            existing.add(serial);
            added.push(serial);
        });
        if (added.length) el.serials.value = (el.serials.value.trim() ? el.serials.value.replace(/\s+$/, '') + '\n' : '') + added.join('\n');
        serialMessage([
            added.length ? lt('Serial.Added', added.length) : '',
            duplicates.length ? lt('Serial.Duplicates', duplicates.slice(0, 5).join(', ')) : '',
            bad.length ? lt('Serial.BadFormat', bad.slice(0, 5).join(', ')) : ''
        ]);
        updateSerialCounter();
    };
    // Barkod (karekod): the service checks GTIN · serial · SKT · lot against the selected SKU and REGISTERED lot.
    const scanCode = async (code) => {
        const lotId = typedLotOpen() ? '' : (el.lot?.value || '');
        if (!lotId) { serialMessage([lt('Serial.ScanNeedsLot')]); return; }
        let reply = null;
        try {
            const res = await fetch('/Lots/form/gs1', {
                method: 'POST', credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', Accept: 'application/json', RequestVerificationToken: antiforgery() },
                body: JSON.stringify({ code, sku: el.sku.value, lotId })
            });
            reply = await res.json().catch(() => null);
        } catch (error) {
            reply = null;
        }
        const warning = byId('serialGtinWarning');
        if (warning && reply?.gtinSkipped) { warning.textContent = reply.warning || ''; warning.classList.remove('d-none'); }
        if (reply?.accepted && reply.serialNumber) { addSerials([reply.serialNumber]); return; }
        serialMessage(reply?.messages?.length ? reply.messages : [lt('Serial.CheckUnavailable')]);
    };
    const addRange = async () => {
        const start = (byId('serialRangeStart')?.value || '').trim();
        const end = (byId('serialRangeEnd')?.value || '').trim();
        const data = await getJson(`/Lots/form/range?${new URLSearchParams({ start, end }).toString()}`);
        const preview = byId('serialRangePreview');
        if (!data || data.error) { if (preview) preview.textContent = data?.error || lt('Serial.BadFormat', `${start} … ${end}`); return; }
        const serials = data.serials || [];
        if (preview) preview.textContent = lt('Serial.RangePreview', serials.length, serials[0] || '', serials[serials.length - 1] || '');
        addSerials(serials);
    };
    const addSingle = () => {
        const input = byId('serialSingle');
        const text = (input?.value || '').trim();
        if (!text) return;
        if (GS1.test(text)) scanCode(text); else addSerials([text]);
        if (input) input.value = '';
    };
    const bindSerialMethods = () => {
        form.querySelectorAll('[data-serial-method]').forEach((button) => button.addEventListener('click', () => {
            form.querySelectorAll('[data-serial-method]').forEach((b) => b.classList.toggle('active', b === button));
            form.querySelectorAll('[data-serial-panel]').forEach((p) => p.classList.toggle('d-none', p.dataset.serialPanel !== button.dataset.serialMethod));
        }));
        const enter = (id, handler) => byId(id)?.addEventListener('keydown', (event) => { if (event.key === 'Enter') { event.preventDefault(); handler(); } });
        enter('serialSingle', addSingle);
        enter('serialScan', () => { const input = byId('serialScan'); const code = (input?.value || '').trim(); if (code) scanCode(code); if (input) input.value = ''; });
        enter('serialRangeEnd', addRange);
        byId('btnSerialAdd')?.addEventListener('click', addSingle);
        byId('btnSerialRange')?.addEventListener('click', addRange);
    };

    // ── MOD-0174 P3 — "+ Yeni lot oluştur" (CT-D5a): the quick lot is Draft and comes back selected ──────────────
    const openQuickLot = () => {
        if (!el.sku.value) { hint('lotId', t('HintSelectProductFirst')); return; }
        window.QuickLot?.open({
            itemId: el.item.value,
            sku: el.sku.value,
            itemText: el.item.selectedOptions[0]?.text || '',
            skuText: el.sku.selectedOptions[0]?.text || '',
            token: antiforgery(),
            onCreated: async (created) => {
                state.pendingLot = created.lotId;
                if (typedLotOpen()) { byId('lotTypedGroup')?.classList.add('d-none'); byId('lotTypedHint')?.classList.add('d-none'); if (el.lotTyped) el.lotTyped.value = ''; }
                await loadLots();
                hint('lotId', lt('Lot.Created', created.lotNumber || ''));
            }
        });
    };

    // ── "Stock effect" (pack §12): before → after from the server; informational only ─────────────────────
    let effectTimer = null;
    const refreshEffect = () => {
        window.clearTimeout(effectTimer);
        effectTimer = window.setTimeout(async () => {
            const panel = byId('movement-effect');
            if (!panel) return;
            const empty = panel.querySelector('[data-effect-empty]');
            const loading = panel.querySelector('[data-effect-loading]');
            const rows = panel.querySelector('[data-effect-rows]');
            const quantity = el.quantity.value.trim();
            const ready = direction() && el.item.value && el.sku.value && el.uom.value && QUANTITY.test(quantity) && !ZERO.test(quantity);
            if (!ready) {
                rows.classList.add('d-none');
                loading.classList.add('d-none');
                empty.classList.remove('d-none');
                return;
            }
            empty.classList.add('d-none');
            loading.classList.remove('d-none');
            const data = await lookup('effect', {
                itemId: el.item.value, sku: el.sku.value, type: el.type.value, quantity, uomId: el.uom.value,
                source: el.source.value, toWarehouseId: el.toWarehouse.value, toLocationId: el.toLocation.value,
                toStockStatus: el.toStatus.value, lotId: currentLot()
            });
            loading.classList.add('d-none');
            const sides = data?.sides || [];
            if (sides.length === 0) {
                rows.classList.add('d-none');
                empty.textContent = data === null || state.facts?.available === false ? t('EffectUnavailable') : empty.textContent;
                empty.classList.remove('d-none');
                return;
            }
            rows.innerHTML = '';
            sides.forEach((side) => {
                const li = document.createElement('li');
                li.className = 'mb-3';
                const head = document.createElement('div');
                head.className = 'small text-body-secondary';
                head.textContent = `${side.side === 'source' ? t('EffectSource') : t('EffectTarget')} · ${side.label}`;
                const value = document.createElement('div');
                // "before → after" is always drawn left to right: in an RTL page the bidi algorithm would otherwise
                // show "0 → 24" as "24 → 0" — the opposite movement.
                value.dir = 'ltr';
                value.className = `d-inline-block font-monospace fw-medium${side.negative ? ' text-danger' : ''}`;
                value.textContent = `${qty(side.before)} → ${qty(side.after)} ${data.baseUomId || ''}`.trim();
                li.append(head, value);
                if (side.negative) {
                    const note = document.createElement('div');
                    note.className = 'small text-danger';
                    note.textContent = t('EffectNegative');
                    li.append(note);
                }
                rows.append(li);
            });
            rows.classList.remove('d-none');
        }, 400);
    };

    // ── read, check, post ───────────────────────────────────────────────────────────────────────────────
    const isoOf = (input) => {
        const date = input?._flatpickr?.selectedDates?.[0];
        return date ? date.toISOString() : '';
    };

    const readBody = () => {
        const dir = direction();
        const body = {
            idempotencyKey: byId('idempotencyKey').value,
            movementType: el.type.value,
            itemId: el.item.value,
            sku: el.sku.value,
            quantity: el.quantity.value.trim(),
            uomId: el.uom.value,
            documentNo: el.documentNo.value.trim(),
            transactionDate: isoOf(el.transactionDate),
            postingDate: isoOf(el.postingDate),
            entry: form.dataset.mode === 'entry',
            itemText: el.item.selectedOptions[0]?.text || '',
            skuText: el.sku.selectedOptions[0]?.text || ''
        };
        if (dir && dir !== 'in') body.source = el.source.value;
        if (dir === 'in' || dir === 'wh' || dir === 'loc') {
            body.toWarehouseId = el.toWarehouse.value;
            body.toLocationId = el.toLocation.value;
        }
        if (dir === 'in') {
            body.toStockStatus = el.toStatus.value;
            body.lotId = currentLot();
        }
        if (state.facts?.serialControlled) body.serialNumbers = el.serials.value;
        return body;
    };

    // The same shape rules as MovementRules.Check (the server repeats them; MOD-0173 decides the rest).
    const check = (body) => {
        const errors = {};
        const dir = direction();
        const required = (id, value) => { if (!value) errors[id] = t('ValRequired'); };
        required('movementType', body.movementType);
        required('itemId', body.itemId);
        required('sku', body.sku);
        if (!QUANTITY.test(body.quantity) || ZERO.test(body.quantity)) errors.quantity = t('ValQuantity');
        required('uomId', body.uomId);
        if (!body.transactionDate) errors.transactionDate = t('ValDate');
        if (!body.postingDate) errors.postingDate = t('ValDate');
        const source = state.sources.get(body.source || '');
        if (state.facts?.blocked) errors.sku = state.facts.blockedMessage || t('PickerUnavailable');
        if (dir && dir !== 'in') required('source', body.source);
        if (dir === 'in' || dir === 'wh' || dir === 'loc') {
            required('toWarehouseId', body.toWarehouseId);
            required('toLocationId', body.toLocationId);
        }
        if (dir === 'in') {
            required('toStockStatus', body.toStockStatus);
            // A typed lot keeps the W-UI-02 shape; a registered lot is an id from the picker (pack §11.4).
            if (body.lotId && typedLotOpen() && !LOT.test(body.lotId)) errors.lotId = t('ValLotFormat');
            if (!body.lotId && state.facts?.lotControlled) errors.lotId = t('ValRequired');
            const picked = typedLotOpen() ? null : state.lots.get(body.lotId || '');
            if (picked?.disabled) errors.lotId = picked.disabledReason || t('ValRequired');
            else if (picked?.retestDue && !byId('lotRetestConfirm')?.checked) errors.lotId = lt('Lot.RetestConfirmRequired');
        }
        if (dir === 'wh' && source && body.toWarehouseId === source.warehouseId) errors.toWarehouseId = t('ValSameWarehouse');
        if (dir === 'loc' && source && body.toLocationId === source.locationId) errors.toLocationId = t('ValSameLocation');
        if (state.facts?.serialControlled) {
            const lines = serialLines();
            if (new Set(lines).size !== lines.length) errors.serialNumbers = t('ValSerialDuplicate');
            else if (state.baseQuantity !== null && String(lines.length) !== String(state.baseQuantity)) {
                errors.serialNumbers = fmt(t('ValSerialCount'), lines.length, qty(state.baseQuantity));
            }
        }
        return errors;
    };

    const antiforgery = () => form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const correctionBody = (movement) => ({
        transactionId: byId('originalTransactionId').value,
        transactionNumber: byId('originalTransactionNumber').value,
        reversalKey: byId('reversalKey').value,
        reversalAt: byId('reversalAt').value,
        originalItemId: byId('originalItemId').value,
        originalSkuId: byId('originalSkuId').value,
        originalSkuLevel: byId('originalSkuLevel').value,
        originalQuantity: byId('originalQuantity').value,
        originalUomId: byId('originalUomId').value,
        movement
    });

    const setBusy = (busy) => {
        state.busy = busy;
        [el.submit, byId('btnPartialRetry'), byId('btnMovementRetry')].forEach((button) => {
            if (!button) return;
            button.disabled = busy;
            button.setAttribute('aria-busy', busy ? 'true' : 'false');
        });
    };

    const post = async (body) => {
        // A double click (or Enter held) is ONE request: the same form, the same key, the busy flag.
        if (state.busy) return;
        setBusy(true);
        state.lastBody = body;
        const payload = form.dataset.mode === 'correct' ? correctionBody(body) : body;
        let res = null;
        let json = null;
        try {
            res = await fetch(form.dataset.postUrl, {
                method: 'POST',
                credentials: 'same-origin',
                // FU-0174-P3-07 (K-F): the retest confirmation travels with the post — the MVC refuses a retest-past lot without it.
                headers: {
                    'Content-Type': 'application/json', Accept: 'application/json', RequestVerificationToken: antiforgery(),
                    'X-Lot-Retest-Confirmed': byId('lotRetestConfirm')?.checked ? 'true' : 'false'
                },
                body: JSON.stringify(payload)
            });
            json = await res.json().catch(() => null);
        } catch (error) {
            res = null;
        }

        if (res?.ok && json?.success) {
            window.location.assign(json.redirect || '/StockMovements/Details');
            return;
        }
        setBusy(false);
        handleRefusal(res, json);
    };

    const handleRefusal = (res, json) => {
        if (!res) { showAlert('movement-page-alert', t('ErrServiceUnavailable')); return; }
        if (res.status === 401) { showAlert('movement-form-alert', t('ErrSessionExpired')); return; }

        if (json?.fields) {
            Object.entries(json.fields).forEach(([id, message]) => fieldError(id, message));
            showAlert('movement-form-alert', json.error?.message || t('ValFixFields'));
            return;
        }

        const error = json?.error || {};
        if (json?.partial) {
            // KD-1: (1) posted, (2) refused — say so, keep the keys, offer the retry.
            state.partial = true;
            const box = byId('movement-partial');
            if (box) {
                box.querySelector('[data-partial-text]').textContent = fmt(t('PartialText'), json.reversalNumber || '', error.message || '');
                box.classList.remove('d-none');
                box.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
            if (error.scope === 'field' && error.field) fieldError(error.field, error.message);
            return;
        }

        switch (error.scope) {
            case 'field':
                if (!fieldError(error.field, error.message)) showAlert('movement-form-alert', error.message, error.correlationId);
                else showAlert('movement-form-alert', t('ValFixFields'), error.correlationId);
                break;
            case 'page':
                showAlert('movement-page-alert', error.message || t('ErrServiceUnavailable'), error.correlationId);
                break;
            case 'restart': {
                showAlert('movement-form-alert', error.message, error.correlationId);
                const link = byId('lnkMovementRestart');
                if (link) { link.href = window.location.pathname + window.location.search; link.classList.remove('d-none'); }
                break;
            }
            default:
                showAlert('movement-form-alert', error.message || t('ErrServiceUnavailable'), error.correlationId);
        }
    };

    const submit = (event) => {
        event?.preventDefault();
        if (state.busy) return;
        clearErrors();
        const body = readBody();
        const errors = check(body);
        if (Object.keys(errors).length > 0) {
            Object.entries(errors).forEach(([id, message]) => fieldError(id, message));
            showAlert('movement-form-alert', t('ValFixFields'));
            return;
        }

        if (form.dataset.mode === 'correct' && !state.partial) {
            // KD-1: two records — say so before anything is posted (one dialog implementation: window.showConfirm).
            const number = byId('originalTransactionNumber').value || byId('originalTransactionId').value;
            window.showConfirm?.(t('ConfirmCorrectTitle'), () => post(body), {
                type: 'danger',
                subtext: fmt(t('ConfirmCorrectText'), number),
                confirmButtonText: t('ConfirmCorrectButton'),
                cancelButtonText: t('Cancel')
            });
            return;
        }
        post(body);
    };

    // ── wiring + prefill ────────────────────────────────────────────────────────────────────────────────
    const initDates = () => {
        if (!window.DitenDateField) return;
        window.DitenDateField.enhance(form, { enableTime: true, time_24hr: true, dateFormat: 'Y-m-d H:i' });
        const now = new Date();
        el.transactionDate?._flatpickr?.setDate(now, false);
        el.postingDate?._flatpickr?.setDate(now, false);
    };

    const initPickers = () => {
        select2(el.type, { minimumResultsForSearch: Infinity });
        select2(el.toStatus, { minimumResultsForSearch: Infinity });
        select2(el.uom, { minimumResultsForSearch: Infinity });
        // Required pickers carry no "clear" control: a required field is changed, never emptied (and the theme draws the
        // clear mark over the field icon).
        select2(el.sku, { minimumResultsForSearch: 8 });
        select2(el.source);
        select2(el.toWarehouse);
        select2(el.lot, { allowClear: true });
        initTargetLocationPicker();
        select2(el.item, {
            minimumInputLength: 0,
            ajax: {
                url: '/StockMovements/lookups/products',
                dataType: 'json',
                delay: 250,
                data: (params) => ({ q: params.term || '' }),
                processResults: (data) => {
                    productUnavailable(data?.available === false ? t('PickerUnavailable') : '');
                    return { results: (data?.items || []).map((item) => ({ id: item.value, text: item.text })) };
                },
                error: (xhr) => { if (xhr?.statusText !== 'abort') productUnavailable(t('PickerUnavailable')); }
            }
        });
    };

    const bind = () => {
        const on = (select, handler) => { if (select) $(select).on('change', handler); };
        on(el.type, () => { clearErrors(); applyType(); });
        on(el.item, async () => { clearErrors(); await loadSkus(); await onSkuChanged(); });
        on(el.sku, () => { clearErrors(); onSkuChanged(); });
        on(el.source, onSourceChanged);
        on(el.toWarehouse, () => { resetTargetLocation(); refreshEffect(); });
        on(el.toLocation, refreshEffect);
        on(el.toStatus, refreshEffect);
        on(el.lot, onLotChanged);
        el.lotTyped?.addEventListener('input', refreshEffect);
        el.lotTyped?.addEventListener('change', () => { el.lotTyped.value = el.lotTyped.value.trim().toUpperCase(); onLotChanged(); });
        byId('btnTypedLot')?.addEventListener('click', () => { showTypedLot(); el.lotTyped?.focus(); onLotChanged(); });
        byId('btnQuickLot')?.addEventListener('click', openQuickLot);
        bindSerialMethods();
        on(el.uom, () => { refreshConversion(); refreshEffect(); });
        el.quantity?.addEventListener('input', () => { refreshConversion(); refreshEffect(); });
        el.serials?.addEventListener('input', updateSerialCounter);
        form.addEventListener('submit', submit);
        byId('btnMovementRetry')?.addEventListener('click', () => { if (state.lastBody) { clearErrors(); post(state.lastBody); } });
    };

    const onSkuChanged = async () => {
        await loadFacts();
        await Promise.all([loadLots(), direction() && direction() !== 'in' ? loadSources() : Promise.resolve()]);
        refreshEffect();
    };

    const prefill = async () => {
        const data = form.dataset;
        if (data.prefillType) { el.type.value = data.prefillType; $(el.type).trigger('change.select2'); }
        applyType();
        if (data.prefillToStatus) { el.toStatus.value = data.prefillToStatus; $(el.toStatus).trigger('change.select2'); }
        if (data.prefillQuantity) el.quantity.value = data.prefillQuantity;
        // A registered lot is selected once the SKU's lots are loaded; a typed one (W-UI-02) goes to the typed box with its badge.
        if (data.prefillLot) {
            if (/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(data.prefillLot)) state.pendingLot = data.prefillLot;
            else showTypedLot(data.prefillLot);
        }
        await loadTargetWarehouses();
        if (data.prefillToLocation && el.toWarehouse.value) {
            ensureOption(el.toLocation, data.prefillToLocation, data.prefillToLocationText || data.prefillToLocation);
        }
        setEnabled(el.toLocation, !!el.toWarehouse.value);
        if (data.prefillItem) {
            ensureOption(el.item, data.prefillItem, data.prefillItemText || data.prefillItem);
            await loadSkus(data.prefillSku || '', data.prefillSkuText || '');
            if (el.sku.value) await onSkuChanged();
        }
        if (direction() === 'loc') setEnabled(el.toWarehouse, false);
        refreshConversion();
        refreshEffect();
    };

    return {
        init: async function () {
            if (!form) return;
            initDates();
            initPickers();
            await prefill();
            bind();
        }
    };
})();

document.addEventListener('DOMContentLoaded', () => StockMovementForm.init());
