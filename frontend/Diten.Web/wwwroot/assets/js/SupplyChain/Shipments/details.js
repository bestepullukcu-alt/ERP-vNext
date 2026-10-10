'use strict';

const ShipmentDetails = (function () {
    const host = document.getElementById('shipment-details');
    const shipmentId = host?.dataset.shipmentId;
    const L = window.L10n || {};
    let permissions = { canDispatch: false, canCancel: false, canCapturePod: false };
    let shipment = null;
    const intents = new Map();
    const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
    const transitions = { Draft: ['Planned', 'Cancelled'], Planned: ['Dispatched', 'Cancelled'],
        Dispatched: ['InTransit', 'Delivered', 'Exception'], InTransit: ['Delivered', 'Exception'],
        Exception: ['InTransit', 'Cancelled'], Delivered: ['Closed'], Closed: [], Cancelled: [] };
    const uuid = () => crypto.randomUUID();
    // Q371: status names are shown localized; the value sent and compared stays the contract's English name.
    const statusLabel = (status) => (L.statuses || {})[status] || status;
    const authoritativeRoot = () => typeof shipment?.lifecycleCorrelationId === 'string'
        && uuidPattern.test(shipment.lifecycleCorrelationId) ? shipment.lifecycleCorrelationId : null;
    const token = (form) => form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const text = (id, value) => { document.getElementById(id).textContent = value ?? L.unavailable; };
    const date = (value) => value ? new Intl.DateTimeFormat(document.documentElement.lang || 'en', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : L.unavailable;
    const readPermissions = () => { try { permissions = Object.assign(permissions, JSON.parse(document.getElementById('shipment-permissions')?.textContent || '{}')); } catch (_) { } };
    const showError = (message, id = 'detailsAlert') => { const el = document.getElementById(id); el.textContent = message; el.classList.remove('d-none'); el.focus?.(); };
    let loadVersion = 0;
    // Q371: the skeleton stands in for the cards until the first load ends, whatever its outcome.
    const hideSkeleton = () => { const el = document.getElementById('shipmentDetailsSkeleton'); if (el) el.style.display = 'none'; };
    const setDetailSurfaceVisible = (visible) => {
        const elements = [
            document.querySelector('#shipment-details .row.g-6'),
            document.getElementById('shipmentActions'),
            document.getElementById('offcanvasTransition'),
            document.getElementById('offcanvasPod')
        ].filter(Boolean);
        elements.forEach((element) => {
            element.hidden = !visible;
            element.toggleAttribute('inert', !visible);
            element.setAttribute('aria-hidden', visible ? 'false' : 'true');
        });
        if (!visible) {
            document.querySelectorAll('#offcanvasTransition, #offcanvasPod').forEach((element) => window.bootstrap?.Offcanvas.getInstance(element)?.hide());
            document.getElementById('shipmentActions')?.replaceChildren();
        }
    };
    const renderSafeNotFound = (message) => {
        shipment = null;
        text('shipmentNumber', L.notFound);
        setDetailSurfaceVisible(false);
        showError(message);
    };
    const error = async (response, alertId) => {
        let body = null; try { body = await response.json(); } catch (_) { }
        const code = body?.error?.code || '';
        const message = ({ SHIPMENT_NOT_FOUND: L.notFound, INVALID_SHIPMENT_TRANSITION: L.invalidTransition,
            POD_ALREADY_CAPTURED: L.podAlreadyCaptured, IDEMPOTENCY_KEY_REUSED: L.idempotencyKeyReused,
            PERSISTENCE_UNAVAILABLE: L.persistenceUnavailable, INTERNAL_ERROR: L.internalError })[code]
            || (response.status === 404 ? L.notFound : response.status === 403 ? L.accessDenied : L.validationError);
        const correlation = body?.error?.correlationId || response.headers.get('X-Correlation-Id');
        showError(`${message}${correlation ? ` ${L.supportReference}: ${correlation}` : ''}`, alertId);
        return code;
    };
    // Q374: a datetime-local field holds the user's LOCAL wall clock; the wire is UTC (pack §12). The field is filled and
    // read as local time and converted only here, so the field, the request and the displayed value are one instant.
    const pad = (n) => String(n).padStart(2, '0');
    const localInputValue = (when) => `${when.getFullYear()}-${pad(when.getMonth() + 1)}-${pad(when.getDate())}T${pad(when.getHours())}:${pad(when.getMinutes())}`;
    const zoneLabel = (when) => { const offset = -when.getTimezoneOffset(); const sign = offset < 0 ? '-' : '+';
        return `${Intl.DateTimeFormat().resolvedOptions().timeZone || ''} (UTC${sign}${pad(Math.floor(Math.abs(offset) / 60))}:${pad(Math.abs(offset) % 60)})`.trim(); };
    const prefillNow = (id) => { const now = new Date(); document.getElementById(id).value = localInputValue(now);
        const zone = document.getElementById(`${id}Zone`); if (zone) zone.textContent = zoneLabel(now); };
    // Client check before any request: an empty date used to throw inside the submit handler and show nothing.
    // Q382: the parse is pure and returned below so Node can run it at fixed zones; instant() is its only DOM caller.
    const parseLocalInput = (raw) => {
        const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?$/.exec(raw);
        if (!match) return null;
        const [, year, month, day, hour, minute, second] = match.map(Number);
        const value = new Date(year, month - 1, day, hour, minute, second || 0);
        // A wall-clock time that does not exist (out-of-range parts, or a skipped daylight-saving hour) is rejected, not shifted.
        const exact = value.getFullYear() === year && value.getMonth() === month - 1 && value.getDate() === day
            && value.getHours() === hour && value.getMinutes() === minute;
        return exact ? value : null;
    };
    const instant = (id) => parseLocalInput(document.getElementById(id).value);
    const rejectInvalid = (form, checks, alertId) => {
        form.querySelectorAll('.is-invalid').forEach((field) => field.classList.remove('is-invalid'));
        document.getElementById(alertId)?.classList.add('d-none');
        const failed = checks.filter(([, valid]) => !valid).map(([id]) => document.getElementById(id));
        if (!failed.length) return false;
        failed.forEach((field) => field.classList.add('is-invalid'));
        showError(L.validationError, alertId); failed[0].focus();
        return true;
    };
    const renderLines = (lines) => {
        const body = document.getElementById('detailLines'); body.replaceChildren();
        (lines || []).forEach((line) => { const row = body.insertRow();
            [line.lineNumber, line.itemId, line.skuId, line.quantity, line.uomId, line.inventoryReferenceId || L.unavailable]
                .forEach((item) => { const cell = row.insertCell(); cell.textContent = item; }); });
    };
    const renderPod = (pod) => {
        document.getElementById('podEmpty').classList.toggle('d-none', !!pod);
        document.getElementById('podDetails').classList.toggle('d-none', !pod);
        if (!pod) return;
        text('podRecipient', pod.recipientName); text('podReceived', date(pod.receivedAt));
        text('podEvidence', (pod.evidenceReferenceIds || []).join(', ')); text('podNote', pod.note);
    };
    const allowedTargets = (status) => (transitions[status] || []).filter((target) =>
        target === 'Cancelled' ? permissions.canCancel : permissions.canDispatch);
    const renderActions = () => {
        const actions = document.getElementById('shipmentActions'); actions.replaceChildren();
        if (allowedTargets(shipment.status).length) {
            const button = document.createElement('button'); button.className = 'btn btn-primary'; button.textContent = L.transition;
            button.addEventListener('click', () => { const select = document.getElementById('targetStatus');
                select.replaceChildren(...allowedTargets(shipment.status).map((target) => new Option(statusLabel(target), target)));
                prefillNow('occurredAt');
                bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasTransition')).show(); }); actions.appendChild(button);
        }
        if (permissions.canCapturePod && ['Dispatched', 'InTransit'].includes(shipment.status)) {
            const button = document.createElement('button'); button.className = 'btn btn-success'; button.textContent = L.capturePod;
            button.addEventListener('click', () => { prefillNow('receivedAt');
                bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('offcanvasPod')).show(); }); actions.appendChild(button);
        }
    };
    const render = (data) => {
        hideSkeleton(); setDetailSurfaceVisible(true);
        document.getElementById('detailsAlert')?.classList.add('d-none');
        shipment = data; text('shipmentNumber', data.shipmentNumber); text('detailStatus', statusLabel(data.status));
        text('detailSource', data.sourceDocumentId); text('detailWarehouse', data.warehouseReferenceId); text('detailShipTo', data.shipToReference);
        text('detailRoot', data.lifecycleCorrelationId); text('detailPlannedShip', date(data.plannedShipAt)); text('detailPlannedDeliver', date(data.plannedDeliverAt));
        text('detailCarrier', data.carrierId); text('detailLoad', data.loadId); renderLines(data.lines); renderPod(data.pod); renderActions();
    };
    const load = async () => {
        const version = ++loadVersion;
        try { const response = await fetch(`/SupplyChain/Shipments/api/${shipmentId}`, { credentials: 'same-origin', headers: { 'X-Correlation-Id': uuid() } });
            if (version !== loadVersion) return;
            if (!response.ok) {
                hideSkeleton();
                let body = null; try { body = await response.clone().json(); } catch (_) { }
                const code = body?.error?.code || '';
                if (response.status === 404 || code === 'SHIPMENT_NOT_FOUND') {
                    const correlation = body?.error?.correlationId || response.headers.get('X-Correlation-Id');
                    renderSafeNotFound(`${L.notFound}${correlation ? ` ${L.supportReference}: ${correlation}` : ''}`);
                    return;
                }
                text('shipmentNumber', L.unavailable); await error(response); return;
            }
            render(await response.json()); }
        catch (_) { hideSkeleton(); text('shipmentNumber', L.unavailable); showError(L.persistenceUnavailable); }
    };
    const send = async (kind, payload, form, alertId) => {
        const correlationId = authoritativeRoot();
        if (!correlationId) { showError(L.validationError, alertId); return; }
        const body = JSON.stringify(payload); const previous = intents.get(kind);
        const intent = previous?.body === body && previous?.correlationId === correlationId
            ? previous : { body, key: uuid(), correlationId }; intents.set(kind, intent);
        const execute = async () => {
            try { const response = await fetch(`/SupplyChain/Shipments/api/${shipmentId}/${kind}`, { method: 'POST', credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token(form), 'Idempotency-Key': intent.key, 'X-Correlation-Id': intent.correlationId }, body: intent.body });
                if (!response.ok) { const code = await error(response, alertId); if (code === 'IDEMPOTENCY_KEY_REUSED') intents.delete(kind); await load(); return; }
                const result = await response.json(); window.showToast?.(result.idempotentReplay ? L.replay : L.success, 'success');
                bootstrap.Offcanvas.getInstance(form.closest('.offcanvas'))?.hide(); await load();
            } catch (_) { showError(L.persistenceUnavailable, alertId); }
        };
        window.showConfirm?.(L.confirmTitle, execute, { confirmButtonText: L.confirmAction });
    };
    const init = () => {
        if (!host) return; readPermissions();
        document.getElementById('formTransition')?.addEventListener('submit', (event) => { event.preventDefault();
            const occurredAt = instant('occurredAt');
            if (rejectInvalid(event.currentTarget, [['targetStatus', !!document.getElementById('targetStatus').value], ['occurredAt', !!occurredAt]], 'transitionAlert')) return;
            send('transition', { targetStatus: document.getElementById('targetStatus').value,
                occurredAt: occurredAt.toISOString(),
                reasonCode: document.getElementById('reasonCode').value || null, note: document.getElementById('transitionNote').value || null }, event.currentTarget, 'transitionAlert'); });
        document.getElementById('formPod')?.addEventListener('submit', (event) => { event.preventDefault();
            const recipientName = document.getElementById('recipientName').value.trim(); const receivedAt = instant('receivedAt');
            const evidenceReferenceIds = document.getElementById('evidenceReferenceIds').value.split(/\r?\n|,/).map((v) => v.trim()).filter(Boolean);
            if (rejectInvalid(event.currentTarget, [['recipientName', !!recipientName], ['receivedAt', !!receivedAt], ['evidenceReferenceIds', evidenceReferenceIds.length > 0]], 'podAlert')) return;
            // The note field has its own id: Details also renders the stored note as #podNote, which getElementById found first.
            send('pod', { recipientName, receivedAt: receivedAt.toISOString(), evidenceReferenceIds,
                note: document.getElementById('podNoteInput').value || null }, event.currentTarget, 'podAlert'); });
        ['formTransition', 'formPod'].forEach((id) => document.getElementById(id)?.addEventListener('input', (event) => event.target.classList.remove('is-invalid')));
        // Q371 (C-08): the note is capped at 1000 by maxlength, which used to cut longer text silently; the counter makes the cap visible.
        document.querySelectorAll('[data-note-counter]').forEach((field) => {
            const counter = document.getElementById(field.dataset.noteCounter);
            const update = () => { if (!counter) return; counter.textContent = `${field.value.length} / ${field.maxLength}`; counter.classList.toggle('text-danger', field.value.length >= field.maxLength); };
            field.addEventListener('input', update); update();
        });
        load();
    };
    document.addEventListener('DOMContentLoaded', init);
    // localInputValue and parseLocalInput touch no DOM; they are returned for the Node date tests (Q382), nothing else uses them.
    return { init, allowedTargets, localInputValue, parseLocalInput };
})();
