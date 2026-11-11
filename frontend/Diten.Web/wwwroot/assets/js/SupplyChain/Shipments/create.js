'use strict';

// MOD-0183 r9 §24 DL-3 — Golden Compact create form with pickers only. No GUID is ever written as visible text, an option
// label, a tooltip or a title (SB-20): ids ride only in option values and in the request body. A stale picker response never
// fills a changed field; changing the warehouse clears every From location, changing the SKU clears its Lot and UoM (SB-7).
const ShipmentCreate = (function () {
    const L = window.L10n || {};
    const jq = window.jQuery;
    // Q371: one Idempotency-Key per form instance — the contract's "intent"; a new Create page is a new intent.
    const intentKey = crypto.randomUUID();
    // Q419: the intent's X-Correlation-Id is minted with its key and kept for every retry (lifecycle root).
    const intentRoot = crypto.randomUUID();
    const base = '/SupplyChain/Shipments/api';
    const lineFields = ['sku', 'lotId', 'quantity', 'fromLocationId', 'fefoBypassReason'];
    const fefoCodes = ['FEFO_BYPASS_REASON_REQUIRED', 'FEFO_ORDER_VIOLATION'];
    const customerRefPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
    const form = () => document.getElementById('formShipment');
    const company = () => form()?.dataset.companyId || '';
    const value = (id) => document.getElementById(id)?.value || '';
    const dateOptions = { enableTime: true, time_24hr: true, dateFormat: 'Y-m-d H:i' };
    const enhanceDates = () => window.DitenDateField?.enhance(document, dateOptions);
    const toUtc = (input) => input ? new Date(input.replace(' ', 'T')).toISOString() : null;
    const token = () => document.querySelector('#formShipment input[name="__RequestVerificationToken"]')?.value || '';
    const label = (code, name) => [code, name].filter((x) => x && String(x).trim()).join(' · ');
    const showError = (message) => {
        const alert = document.getElementById('formShipmentAlert');
        alert.textContent = message; alert.classList.remove('d-none'); alert.focus();
    };
    const hideError = () => document.getElementById('formShipmentAlert')?.classList.add('d-none');
    const getJson = async (url) => {
        const response = await fetch(url, { credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest', 'X-Correlation-Id': intentRoot } });
        if (response.status === 401) { window.DtDefaults?.handleUnauthorized?.(); throw new Error('unauthorized'); }
        if (!response.ok) { const error = new Error('picker'); error.status = response.status; throw error; }
        return response.json();
    };

    // A Select2 picker whose rows come from one of this page's same-origin adapters. `rows(data)` maps the adapter answer to
    // { id, text } rows; Select2 drops the answer of a superseded search, so a stale response never fills the field.
    const picker = (element, url, rows, extra = {}) => {
        if (!jq?.fn?.select2) return;
        jq(element).select2(Object.assign({
            width: '100%', placeholder: element.dataset.placeholder || '', allowClear: !element.required,
            language: { searching: () => L.pickerSearching, noResults: () => L.pickerNoResults, errorLoading: () => L.pickerUnavailable },
            ajax: {
                delay: 250,
                transport: (params, success, failure) => {
                    const target = url(params.data || {});
                    if (!target) { success({ results: [] }); return { abort() { } }; }
                    let cancelled = false;
                    getJson(target).then((data) => { if (!cancelled) success(data); }).catch(() => { if (!cancelled) failure(); });
                    return { abort() { cancelled = true; } };
                },
                processResults: (data, params) => ({ results: rows(data, params), pagination: { more: Boolean(data?.hasMore) } })
            }
        }, extra));
    };
    const clearSelect = (element) => {
        if (!element) return;
        element.innerHTML = '';
        if (jq?.fn?.select2 && jq(element).hasClass('select2-hidden-accessible')) jq(element).val(null).trigger('change');
    };

    // ── Sender: the company of the shared selector, read-only (SB-1) ─────────────────────────────────────────────────
    const loadSender = async () => {
        const target = document.getElementById('senderCompany');
        if (!company()) { target.textContent = L.senderCompanyRequired; return false; }
        try {
            const payload = await getJson('/SupplyChain/api/legal-entities');
            const rows = Array.isArray(payload) ? payload : (payload?.data ?? payload?.items ?? []);
            const match = rows.find((row) => String(row.legalEntityId ?? row.LegalEntityId ?? '') === company());
            target.textContent = match ? String(match.displayName ?? match.DisplayName ?? match.legalName ?? match.LegalName ?? match.code ?? L.nameUnavailable) : L.nameUnavailable;
        } catch (_) { target.textContent = L.nameUnavailable; }
        return true;
    };

    // ── Customer (§24.1): name and code in the list, address read-only after the choice ───────────────────────────────
    const customers = new Map();
    let customerAddressReady = false;
    const renderAddress = (address) => {
        const target = document.getElementById('customerAddress');
        target.textContent = '';
        // VER F-6: the user confirms the same text the adapter stores — CountryRef, CityRef, DistrictRef, AddressLine (SB-23).
        const parts = address ? [address.countryRef, address.cityRef, address.districtRef, address.addressLine].map((x) => (x || '').trim()).filter(Boolean) : [];
        customerAddressReady = parts.length > 0;
        if (!address) { target.textContent = '—'; return; }
        if (!customerAddressReady) { target.textContent = L.customerAddressMissing; return; }
        target.textContent = parts.join(', ');
    };
    const initCustomer = () => {
        const select = document.getElementById('customerRef');
        if (!select || form()?.dataset.customersEnabled !== 'true') return;
        picker(select, (p) => `${base}/customers?page=${p.page || 1}${p.term ? `&search=${encodeURIComponent(p.term)}` : ''}`,
            (data) => (data?.items || []).filter((row) => customerRefPattern.test(row.id)).map((row) => {
                customers.set(row.id, row);
                return { id: row.id, text: label(row.code, row.name) || L.nameUnavailable };
            }));
        let generation = 0;
        jq?.(select).on('change', async () => {
            const chosen = select.value;
            const mine = ++generation;
            renderAddress(null);
            if (!customerRefPattern.test(chosen)) return;
            document.getElementById('customerAddress').textContent = L.loading;
            try {
                const address = await getJson(`${base}/customers/${encodeURIComponent(chosen)}`);
                if (mine === generation) renderAddress(address);
            } catch (_) { if (mine === generation) document.getElementById('customerAddress').textContent = L.nameUnavailable; }
        });
    };

    // ── Warehouse (§24.2): GET /api/locations/search type=Warehouse, Active ──────────────────────────────────────────
    const initWarehouse = () => {
        const select = document.getElementById('warehouseReferenceId');
        picker(select, (p) => `${base}/warehouses${p.term ? `?search=${encodeURIComponent(p.term)}` : ''}`,
            (data) => (data?.items || []).map((row) => ({ id: row.id, text: label(row.code, row.name) || L.nameUnavailable })));
        jq?.(select).on('change', () => document.querySelectorAll('.shipment-line').forEach(resetLocation));
    };

    // ── Location tree picker (§24.2, SB-7): one layer at a time; only an isStockable node can be chosen; a node that is not
    // a Bin can be opened; no isStockable filter on the layer, so non-stockable parents stay visible. ──────────────────────
    const resetLocation = (line) => {
        const select = line.querySelector('[data-field="fromLocationId"]');
        const warehouse = document.getElementById('warehouseReferenceId');
        const chosen = warehouse?.value || '';
        line._layers = chosen ? [{ id: chosen }] : [];
        clearSelect(select);
        select.disabled = !chosen;
        line.querySelector('[data-location-help]').textContent = chosen ? '' : L.warehouseFirst;
    };
    const initLocation = (line) => {
        const select = line.querySelector('[data-field="fromLocationId"]');
        line._layers = [];
        picker(select, (p) => {
            const top = line._layers[line._layers.length - 1];
            return top ? `${base}/locations?parentId=${encodeURIComponent(top.id)}${p.term ? `&search=${encodeURIComponent(p.term)}` : ''}` : null;
        }, (data) => {
            const rows = [];
            if (line._layers.length > 1) rows.push({ id: 'up:', text: `← ${L.locationBack}` });
            (data?.items || []).forEach((node) => {
                const text = label(node.code, node.name) || L.nameUnavailable;
                if (node.isStockable) rows.push({ id: node.id, text });
                if (node.type !== 'Bin') rows.push({ id: `open:${node.id}`, text: `▸ ${text}`, open: true });
            });
            return rows;
        });
        jq?.(select).on('select2:selecting', (event) => {
            const id = event.params?.args?.data?.id || '';
            if (!id.startsWith('open:') && id !== 'up:') return;
            event.preventDefault();
            if (id === 'up:') line._layers.pop(); else line._layers.push({ id: id.slice(5) });
            jq(select).select2('close'); jq(select).select2('open');
        });
        resetLocation(line);
    };

    // ── Product / SKU and Lot (§24.2): name and canonicalCode; UoM read-only from the SKU; changing SKU clears Lot ──────
    const initSku = (line) => {
        const sku = line.querySelector('[data-field="sku"]');
        const lot = line.querySelector('[data-field="lotId"]');
        line._skus = new Map();
        picker(sku, (p) => `${base}/products${p.term ? `?q=${encodeURIComponent(p.term)}` : ''}`,
            (data) => (data?.items || []).map((row) => {
                line._skus.set(row.skuId, row);
                return { id: row.skuId, text: label(row.code, row.name) || L.nameUnavailable };
            }));
        picker(lot, (p) => {
            const chosen = line._skus.get(sku.value);
            return chosen ? `${base}/lots?skuId=${encodeURIComponent(chosen.skuId)}&skuLevel=${encodeURIComponent(chosen.skuLevel)}${p.term ? `&q=${encodeURIComponent(p.term)}` : ''}` : null;
        }, (data) => (data?.items || []).map((row) => ({ id: row.id, text: row.lotNumber })), { allowClear: true });
        jq?.(sku).on('change', () => {
            const chosen = line._skus.get(sku.value);
            clearSelect(lot);
            line.querySelector('[data-uom]').textContent = chosen?.uomId || '—';
            lot.disabled = !chosen?.lotControlled;
            line.querySelector('[data-lot-help]').textContent = !chosen ? L.productFirst : (chosen.lotControlled ? '' : L.lotNotTracked);
        });
    };

    // ── Lines: numbered 1, 2, 3 in order and renumbered after a removal (SB-6) ───────────────────────────────────────
    const syncLines = () => {
        Array.from(document.querySelectorAll('.shipment-line')).forEach((line, index) => {
            line.querySelector('[data-line-number]').textContent = String(index + 1);
            lineFields.forEach((field) => {
                const input = line.querySelector(`[data-field="${field}"]`);
                const fieldLabel = line.querySelector(`[data-field-label="${field}"]`);
                if (!input || !fieldLabel) return;
                input.id = `shipmentLine_${index}_${field}`;
                fieldLabel.htmlFor = input.id;
            });
        });
    };
    const addLine = (focusNewLine = false) => {
        const fragment = document.getElementById('shipmentLineTemplate').content.cloneNode(true);
        const line = fragment.querySelector('.shipment-line');
        document.getElementById('shipmentLines').appendChild(fragment);
        line.querySelector('.remove-line').addEventListener('click', () => {
            const lines = Array.from(document.querySelectorAll('.shipment-line'));
            if (lines.length <= 1) return;
            const index = lines.indexOf(line);
            line.remove();
            syncLines();
            const remaining = document.querySelectorAll('.shipment-line');
            const focusTarget = remaining[Math.min(index, remaining.length - 1)]?.querySelector('[data-field="quantity"]')
                || document.getElementById('btnAddLine');
            focusTarget?.focus();
        });
        syncLines();
        initSku(line);
        initLocation(line);
        if (focusNewLine) line.querySelector('[data-field="quantity"]')?.focus();
    };
    // §24.2 SB-8: the FEFO reason appears only after a FEFO refusal names the line, and is sent unchanged.
    const revealFefo = (lineNumber) => {
        const line = document.querySelectorAll('.shipment-line')[Number(lineNumber) - 1];
        line?.querySelector('[data-fefo]')?.classList.remove('d-none');
        line?.querySelector('[data-field="fefoBypassReason"]')?.setAttribute('required', 'required');
    };

    const linePayload = (line) => {
        const chosen = line._skus?.get(line.querySelector('[data-field="sku"]').value);
        const body = {
            itemId: chosen?.itemId || '', skuId: chosen?.skuId || '', skuLevel: chosen?.skuLevel || '', uomId: chosen?.uomId || '',
            quantity: line.querySelector('[data-field="quantity"]').value.trim(),
            fromLocationId: line.querySelector('[data-field="fromLocationId"]').value || ''
        };
        const lot = line.querySelector('[data-field="lotId"]').value;
        if (lot) body.lotId = lot; // left out when empty: SHIPMENT-BUNDLE 3.4.0 keeps the fingerprint of a line without it
        const fefo = line.querySelector('[data-fefo]').classList.contains('d-none') ? '' : line.querySelector('[data-field="fefoBypassReason"]').value;
        if (fefo.trim()) body.fefoBypassReason = fefo;
        return body;
    };
    // The company rides the query (R-2); the body carries no scope. No sourceModule, sourceType, shipToReference, lineNumber,
    // uomId choice or inventoryReferenceId: the adapter sets them (SB-5).
    const payload = () => ({
        sourceDocumentId: value('sourceDocumentId').trim(), warehouseReferenceId: value('warehouseReferenceId'),
        customerRef: value('customerRef'), plannedShipAt: toUtc(value('plannedShipAt')),
        lines: Array.from(document.querySelectorAll('.shipment-line')).map(linePayload),
        plannedDeliverAt: value('plannedDeliverAt') ? toUtc(value('plannedDeliverAt')) : null
    });
    const validate = (body) => body.sourceDocumentId && body.warehouseReferenceId && customerRefPattern.test(body.customerRef)
        && body.plannedShipAt && body.lines.length > 0 && body.lines.every((line) =>
            line.itemId && line.skuId && line.skuLevel && line.uomId && /^-?[0-9]+(\.[0-9]+)?$/.test(line.quantity) && line.fromLocationId);
    const failure = async (response) => {
        let body = null; try { body = await response.json(); } catch (_) { }
        const code = body?.error?.code || '';
        const details = body?.error?.details || {};
        if (code === 'INVENTORY_REFUSED' && fefoCodes.includes(details.inventoryCode)) revealFefo(details.lineNumber);
        const message = ({
            INVALID_CUSTOMER_ACCOUNT: L.invalidCustomerAccount, CUSTOMER_ADDRESS_MISSING: L.customerAddressMissing,
            IDEMPOTENCY_KEY_REUSED: L.idempotencyKeyReused, PERSISTENCE_UNAVAILABLE: L.persistenceUnavailable, INTERNAL_ERROR: L.internalError
        })[code] || (response.status === 403 ? L.accessDenied : L.validationError);
        const correlation = body?.error?.correlationId || response.headers.get('X-Correlation-Id');
        showError(`${message}${correlation ? ` ${L.supportReference}: ${correlation}` : ''}`);
    };
    const submit = async (event) => {
        event.preventDefault();
        hideError();
        if (!company()) { showError(L.senderCompanyRequired); return; }
        if (form()?.dataset.customersEnabled !== 'true') { showError(L.customerNoAccess); return; }
        const body = payload();
        if (!validate(body)) { showError(L.validationError); return; }
        if (!customerAddressReady) { showError(L.customerAddressMissing); return; }
        const button = document.getElementById('btnSaveShipment'); button.disabled = true;
        try {
            const response = await fetch(`${base}?legalEntityId=${encodeURIComponent(company())}`, {
                method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json',
                    'RequestVerificationToken': token(), 'Idempotency-Key': intentKey, 'X-Correlation-Id': intentRoot }, body: JSON.stringify(body)
            });
            if (!response.ok) { await failure(response); return; }
            const result = await response.json();
            window.showToast?.(result.idempotentReplay ? L.replay : L.success, 'success');
            window.location.assign(`/SupplyChain/Shipments/Details/${encodeURIComponent(result.shipmentId)}?legalEntityId=${encodeURIComponent(company())}`);
        } catch (error) { showError(L.persistenceUnavailable); }
        finally { button.disabled = false; }
    };
    const init = async () => {
        document.getElementById('btnAddLine')?.addEventListener('click', () => addLine(true));
        form()?.addEventListener('submit', submit);
        initWarehouse();
        initCustomer();
        addLine();
        enhanceDates();
        await loadSender();
    };
    document.addEventListener('DOMContentLoaded', () => { void init(); });
    return { init, payload, enhanceDates };
})();
