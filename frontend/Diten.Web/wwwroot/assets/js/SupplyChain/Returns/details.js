'use strict';

// MOD-0186 r10 §35.5 (DL-4) — the return details page. Reads api/{returnId} (getReturn with names) and, only with
// quarantine.holds.read, api/{returnId}/holds (listHoldsBySource). Values are written with textContent; no GUID is shown as
// text (RB-24): the customer is a name and code, "old record", "not found" or "name unavailable" (RB-25).
const ReturnDetails = (function () {
    const host = document.getElementById('return-details');
    const returnId = host?.dataset.returnId || '';
    const legalEntityId = host?.dataset.legalEntityId || '';
    const canReadHolds = host?.dataset.canReadHolds === 'true';
    const t = (key) => (window.L10n || {})[key] || '';
    const isText = (v) => typeof v === 'string';
    const scoped = (path) => `${path}?legalEntityId=${encodeURIComponent(legalEntityId)}`;
    const STATUS_KEY = { Requested: 'StatusRequested', Authorized: 'StatusAuthorized', Rejected: 'StatusRejected', InTransit: 'StatusInTransit',
        Received: 'StatusReceived', Dispositioned: 'StatusDispositioned', Closed: 'StatusClosed', Cancelled: 'StatusCancelled' };
    const POSTING_KEY = { Posted: 'ReceiptLinePosted', NotPosted: 'ReceiptLineNotPosted' };
    const HOLD_KEY = { Open: 'HoldStatus_Open', DecisionPending: 'HoldStatus_DecisionPending', Decided: 'HoldStatus_Decided' };
    const REASONS = ['DAMAGED', 'WRONG_ITEM', 'WRONG_QUANTITY', 'EXPIRED', 'QUALITY_COMPLAINT', 'CUSTOMER_REFUSED', 'OTHER'];
    const own = (map, key) => (isText(key) && Object.prototype.hasOwnProperty.call(map, key) ? map[key] : undefined);

    const customerText = (cell) => {
        switch (cell?.state) {
            case 'resolved': return [cell.code, cell.name].filter((x) => isText(x) && x.trim()).join(' · ') || t('NameUnavailable');
            case 'oldRecord': return t('OldRecord');
            case 'notFound': return t('NameNotFound');
            default: return t('NameUnavailable');
        }
    };
    const nameText = (cell) => (cell?.state === 'resolved' && isText(cell.label) ? cell.label : t('NameUnavailable'));
    // OD-186-2: a listed code shows its label; an old free-text return shows its stored text as written.
    const reasonText = (code) => (REASONS.includes(code) ? t(`Reason_${code}`) : (isText(code) && code ? code : t('NotProvided')));
    // VER F-3: a NotPosted line shows why (PRE_WP_P1 or the unpostedLines reason), as the list's receipt result does.
    const POSTING_REASON_KEY = { PRE_WP_P1: 'ReceiptReasonPreWpP1', ITEM_NOT_ACTIVE: 'InventoryRefusedItemNotActive', SKU_LEVEL_NOT_STOCKABLE: 'InventoryRefusedSkuLevelNotStockable',
        UNKNOWN_ITEM: 'InventoryRefusedUnknownItem', INTEGRITY_FAULT: 'ReceiptReasonIntegrityFault' };
    const postingText = (line) => {
        if (!own(POSTING_KEY, line?.inventoryPosting)) return t('NotPostedYet');
        const label = t(POSTING_KEY[line.inventoryPosting]);
        if (line.inventoryPosting !== 'NotPosted' || !isText(line.inventoryPostingReason) || !line.inventoryPostingReason) return label;
        const reason = own(POSTING_REASON_KEY, line.inventoryPostingReason) ? t(POSTING_REASON_KEY[line.inventoryPostingReason]) : line.inventoryPostingReason;
        return `${label}: ${reason}`;
    };
    const date = (v) => (v ? new Intl.DateTimeFormat(document.documentElement.lang || 'en', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(v)) : t('NotProvided'));
    const text = (id, value) => { const el = document.getElementById(id); if (el) el.textContent = value; };
    const holdLinks = (holds) => {
        const box = document.createElement('span');
        (holds || []).filter((h) => isText(h?.holdNumber)).forEach((hold, index) => {
            if (index) box.appendChild(document.createTextNode(', '));
            const link = document.createElement('a');
            link.href = `/Quarantine/Details/${encodeURIComponent(hold.holdId || '')}`;
            link.dir = 'ltr';
            link.textContent = hold.holdNumber;
            box.appendChild(link);
            const status = own(HOLD_KEY, hold.status);
            if (status) box.appendChild(document.createTextNode(` (${t(status)})`));
        });
        return box;
    };
    const lineCells = (line) => [line.shipmentLineNumber, nameText(line.sku), line.lot ? nameText(line.lot) : '—', line.quantity, line.uomId, postingText(line)];

    const showError = (status, reference) => {
        const el = document.getElementById('returnDetailsError');
        el.textContent = (status === 404 ? t('ErrReturnNotFound') : t('DetailsErrorState')) + (reference ? ` ${t('SupportReference')}: ${reference}` : '');
        el.classList.remove('d-none'); el.focus?.();
        text('detailRmaNumber', t('ReturnDetails'));
    };
    const render = (data) => {
        text('detailRmaNumber', data.rmaNumber || t('NotProvided'));
        text('detailStatus', own(STATUS_KEY, data.status) ? t(STATUS_KEY[data.status]) : t('NotProvided'));
        const shipment = document.getElementById('detailShipment');
        shipment.replaceChildren();
        if (isText(data.shipmentNumber) && data.shipmentNumber) {
            const link = document.createElement('a');
            link.href = scoped(`/SupplyChain/Shipments/Details/${encodeURIComponent(data.shipmentId || '')}`);
            link.dir = 'ltr'; link.textContent = data.shipmentNumber;
            shipment.appendChild(link);
        } else shipment.textContent = t('NameUnavailable');
        if (data.customer !== null && data.customer !== undefined) text('detailCustomer', customerText(data.customer));
        text('detailReason', reasonText(data.reasonCode));
        text('detailCreated', date(data.createdAt));
        const body = document.getElementById('detailLines');
        body.replaceChildren();
        (data.lines || []).forEach((line) => {
            const row = body.insertRow();
            row.dataset.lineNumber = line.shipmentLineNumber || '';
            lineCells(line).forEach((value, index) => { const cell = row.insertCell(); cell.textContent = value ?? ''; if (index === 3) cell.className = 'text-end'; });
            if (canReadHolds) { const holds = row.insertCell(); holds.className = 'js-line-holds'; }
        });
        document.getElementById('returnDetailsCards').hidden = false;
    };
    // RB-10: without quarantine.holds.read the block is absent and no request is made.
    const loadHolds = async () => {
        if (!canReadHolds) return;
        try {
            const response = await fetch(scoped(`/SupplyChain/Returns/api/${encodeURIComponent(returnId)}/holds`), { credentials: 'same-origin', headers: { 'X-Correlation-Id': crypto.randomUUID() } });
            if (!response.ok) throw new Error(String(response.status));
            const body = await response.json();
            (body.lines || []).forEach((line) => {
                const cell = Array.from(document.querySelectorAll('#detailLines tr')).find((row) => row.dataset.lineNumber === line.shipmentLineNumber)?.querySelector('.js-line-holds');
                if (!cell) return;
                // VER F-4: without a readable shipment no line can be matched, so the cell says nothing rather than "no record".
                const none = body.linesMatched === false ? '—' : (line.inventoryPosting === 'NotPosted' ? postingText(line) : t('NoHold'));
                cell.replaceChildren(line.holds?.length ? holdLinks(line.holds) : document.createTextNode(none));
            });
            if (body.other?.length) {
                document.getElementById('detailOtherHoldsList').replaceChildren(holdLinks(body.other));
                document.getElementById('detailOtherHolds').classList.remove('d-none');
            }
        } catch (_) { document.getElementById('detailHoldsError')?.classList.remove('d-none'); }
    };
    const init = async () => {
        if (!host) return;
        try {
            const response = await fetch(scoped(`/SupplyChain/Returns/api/${encodeURIComponent(returnId)}`), { credentials: 'same-origin', headers: { 'X-Correlation-Id': crypto.randomUUID() } });
            document.getElementById('returnDetailsSkeleton').style.display = 'none';
            if (response.status === 401) { window.DtDefaults?.handleUnauthorized?.(); return; }
            if (!response.ok) {
                let body = null; try { body = await response.json(); } catch (_) { }
                showError(response.status, body?.error?.correlationId || response.headers.get('X-Correlation-Id'));
                return;
            }
            render(await response.json());
            await loadHolds();
        } catch (_) {
            document.getElementById('returnDetailsSkeleton').style.display = 'none';
            showError(0, '');
        }
    };
    document.addEventListener('DOMContentLoaded', () => { void init(); });
    return { init, customerText, reasonText, lineCells };
})();
