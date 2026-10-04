'use strict';

const ShipmentCreate = (function () {
    const L = window.L10n || {};
    // Q371: one Idempotency-Key per form instance — the contract's "intent". It used to be re-minted whenever the
    // payload changed, so a retry after a 503 (unknown commit) with any edit created a second shipment instead of
    // reaching 409 IDEMPOTENCY_KEY_REUSED. The contract keeps no receipt for failed requests, so the same key stays
    // valid across corrected submits; a new Create page is a new intent.
    const intentKey = crypto.randomUUID();
    const lineFields = ['lineNumber', 'itemId', 'skuId', 'quantity', 'uomId', 'inventoryReferenceId'];
    const uuid = () => crypto.randomUUID();
    const value = (id) => document.getElementById(id)?.value || '';
    const toUtc = (input) => input ? new Date(input).toISOString() : null;
    const token = () => document.querySelector('#formShipment input[name="__RequestVerificationToken"]')?.value || '';
    const showError = (message) => {
        const alert = document.getElementById('formShipmentAlert');
        alert.textContent = message; alert.classList.remove('d-none'); alert.focus();
    };
    const syncLineAccessibility = () => {
        Array.from(document.querySelectorAll('.shipment-line')).forEach((line, index) => {
            lineFields.forEach((field) => {
                const input = line.querySelector(`[data-field="${field}"]`);
                const label = line.querySelector(`[data-field-label="${field}"]`);
                if (!input || !label) return;
                input.id = `shipmentLine_${index}_${field}`;
                label.htmlFor = input.id;
            });
        });
    };
    const addLine = (focusNewLine = false) => {
        const fragment = document.getElementById('shipmentLineTemplate').content.cloneNode(true);
        const line = fragment.querySelector('.shipment-line');
        line.querySelector('.remove-line').addEventListener('click', () => {
            const lines = Array.from(document.querySelectorAll('.shipment-line'));
            if (lines.length <= 1) return;
            const index = lines.indexOf(line);
            line.remove();
            syncLineAccessibility();
            const remaining = document.querySelectorAll('.shipment-line');
            const focusTarget = remaining[Math.min(index, remaining.length - 1)]?.querySelector('[data-field="lineNumber"]')
                || document.getElementById('btnAddLine');
            focusTarget?.focus();
        });
        document.getElementById('shipmentLines').appendChild(fragment);
        syncLineAccessibility();
        if (focusNewLine) line.querySelector('[data-field="lineNumber"]')?.focus();
    };
    const linePayload = (line) => Object.fromEntries(lineFields
        .map((name) => [name, line.querySelector(`[data-field="${name}"]`).value]));
    const payload = () => ({
        sourceModule: value('sourceModule'), sourceType: value('sourceType'), sourceDocumentId: value('sourceDocumentId'),
        warehouseReferenceId: value('warehouseReferenceId'), shipToReference: value('shipToReference'),
        plannedShipAt: toUtc(value('plannedShipAt')), lines: Array.from(document.querySelectorAll('.shipment-line')).map(linePayload),
        plannedDeliverAt: value('plannedDeliverAt') ? toUtc(value('plannedDeliverAt')) : null
    });
    const validate = (body) => body.sourceModule && body.sourceType && body.sourceDocumentId && body.warehouseReferenceId
        && body.shipToReference && body.plannedShipAt && body.lines.length > 0 && body.lines.every((line) =>
            line.lineNumber && /^[0-9a-f-]{36}$/i.test(line.itemId) && /^[0-9a-f-]{36}$/i.test(line.skuId)
            && /^-?[0-9]+(\.[0-9]+)?$/.test(line.quantity) && line.uomId);
    const failure = async (response) => {
        let body = null; try { body = await response.json(); } catch (_) { }
        const code = body?.error?.code || '';
        const message = ({ IDEMPOTENCY_KEY_REUSED: L.idempotencyKeyReused, PERSISTENCE_UNAVAILABLE: L.persistenceUnavailable,
            INTERNAL_ERROR: L.internalError })[code] || (response.status === 403 ? L.accessDenied : L.validationError);
        const correlation = body?.error?.correlationId || response.headers.get('X-Correlation-Id');
        showError(`${message}${correlation ? ` ${L.supportReference}: ${correlation}` : ''}`);
    };
    const submit = async (event) => {
        event.preventDefault();
        const body = payload();
        if (!validate(body)) { showError(L.validationError); return; }
        const button = document.getElementById('btnSaveShipment'); button.disabled = true;
        try {
            const response = await fetch('/SupplyChain/Shipments/api', {
                method: 'POST', credentials: 'same-origin', headers: { 'Content-Type': 'application/json',
                    'RequestVerificationToken': token(), 'Idempotency-Key': intentKey, 'X-Correlation-Id': uuid() }, body: JSON.stringify(body)
            });
            if (!response.ok) { await failure(response); return; }
            const result = await response.json();
            window.showToast?.(result.idempotentReplay ? L.replay : L.success, 'success');
            window.location.assign(`/SupplyChain/Shipments/Details/${encodeURIComponent(result.shipmentId)}`);
        } catch (error) { showError(L.persistenceUnavailable); }
        finally { button.disabled = false; }
    };
    const init = () => {
        document.getElementById('btnAddLine')?.addEventListener('click', () => addLine(true));
        document.getElementById('formShipment')?.addEventListener('submit', submit);
        addLine();
        window.DitenDateField?.enhance(document);
    };
    document.addEventListener('DOMContentLoaded', init);
    return { init, payload };
})();
