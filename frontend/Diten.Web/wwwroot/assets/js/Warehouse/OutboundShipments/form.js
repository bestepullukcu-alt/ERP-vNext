/**
 * MOD-0178 DL-6 — the outbound Create / Edit pages (Golden Reference Compact form; pack §9, §8.17).
 * MOD-0178-FU01 DL-1 (FU01 pack §8.2, §8.3, D-1, D-4) — the lines are a TABLE (#outbound-lines-table, DataTable v2, client mode: the source is the
 * page's in-memory line array, at most 100) and a line is added / edited / given another lot in the side panel (#offcanvasOutboundLine).
 *
 * CREATE: depo (LOCATION, company's active warehouses) → optional Staging (the warehouse's stockable nodes — only when the warehouse has several
 * Staging nodes; the service checks the role) → lines in the panel (product → SKU of the product → its BASE unit from PRODUCT-MASTER, quantity,
 * order line, optional lot change + FEFO reason; "Satırı önizle" previews THAT line with its chosen lot) → "Tahsisi önizle" (every line with its
 * lotOverride in one request — §8.17 step 7: lots in FEFO order, allocatable, the company's FEFO mode — NOT a reservation) → "Kaydet" (the
 * allocation is recomputed on save; 422 WMS_ALLOCATION_FAILED marks the failing lines).
 * EDIT: the header only (order ref, ship-to, note) — If-Match; the lines and the allocation never change: the same table, read-only.
 *
 * WP-0178-E3-FIX-01 is carried, not rewritten: rules.lineBody (the chosen lot goes into the preview), rules.lotNeed, rules.quantityText, a lot
 * change previews at once, the deviation note, the Staging refusal on its field.
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

    /** A change listener that hears both a native change and select2's (jQuery-triggered) one. */
    const onChange = (el, handler) => {
        if (!el) return;
        if (window.jQuery) window.jQuery(el).on('change', handler);
        else el.addEventListener('change', handler);
    };

    /** Redraws select2's own box after the value was set in code (the namespaced event moves no other listener). */
    const resync = (el) => { if (el && window.jQuery?.fn?.select2) window.jQuery(el).trigger('change.select2'); };

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

    const header = () => {
        const body = { warehouseId: value('WarehouseId'), stagingLocationId: value('StagingLocationId') || null, orderRef: value('OrderRef') || null, note: value('Note') || null };
        const shipTo = { name: value('ShipToName'), country: value('ShipToCountry'), city: value('ShipToCity') || null, postalCode: value('ShipToPostalCode') || null, line1: value('ShipToLine1') || null, line2: value('ShipToLine2') || null };
        return { body, shipTo };
    };

    const previewBody = (lineBodies) => {
        const { body, shipTo } = header();
        return Object.assign({}, body, { shipTo: shipTo.name && shipTo.country ? shipTo : null, lines: lineBodies });
    };

    // E3-FIX-01 (item 4): a refusal about the chosen Staging location points at that field, not only at the top of the form
    const StagingCodes = { WMS_TARGET_NOT_ALLOWED: 'StagingNotAllowed', WMS_STAGING_NOT_CONFIGURED: 'Warehouse.Error.WMS_STAGING_NOT_CONFIGURED' };
    const stagingError = (answer) => {
        const word = StagingCodes[answer?.json?.error?.code];
        fieldError('StagingLocationIdError', word ? W(word) : '');
        return word ? W(word) : null;
    };

    const failureWord = (code) => W(`Warehouse.Outbound.LineFailure.${code}`) || W(`Warehouse.Error.${code}`) || W('Warehouse.Error.Invalid');

    // ─── The lines (create): the in-memory array the table and the panel share (FU01 §8.2) ───────────────────────────────────────────
    // line = { uid, orderLineId, itemId, itemText, sku, skuText, uomId, quantity, lotId, lotNumber, reason, preview, stale, error }
    let lines = [];
    let uidSeq = 0;
    let busy = false;

    const blankLine = () => ({
        uid: null, orderLineId: rules.nextOrderLine(lines), itemId: '', itemText: '', sku: '', skuText: '', uomId: '', quantity: '', lotId: '', lotNumber: '',
        reason: '', preview: null, stale: false, error: null
    });

    const firstFree = (preview) => (preview?.lots || []).find((l) => l.fefoRank === 1) || null;
    const lotNumberOf = (preview, lotId) => (preview?.lots || []).find((l) => l.lotId === lotId)?.lotNumber || '';

    const badge = (status) => {
        const b = rules.lineBadge(status);
        return `<span class="badge ${b.cls}" data-line-status="${esc(status)}"><i class="bx ${b.icon} me-1" aria-hidden="true"></i>${esc(W(b.word))}</span>`;
    };

    const reasonNote = (reason) => (String(reason ?? '').trim()
        ? `<small class="d-block text-muted text-break" data-fefo-reason>${esc(W('LinesTable.FefoReasonLabel'))} ${esc(String(reason).trim())}</small>` : '');

    /** Why a line is "Karşılanamaz" (under its badge): the server's line code, a refusal of this line, or a chosen lot that left the list. */
    const statusNote = (line, index) => {
        if (line.error) {
            const e = line.error;
            const text = e.requested !== null && e.requested !== undefined
                ? format(W('AllocationLineFailed'), index + 1, failureWord(e.code), qty(e.requested), qty(e.allocatable))
                : (e.message || failureWord(e.code));
            return `<small class="d-block text-danger text-break" data-line-failure>${esc(text)}</small>`;
        }
        if (line.stale || !line.preview) return '';
        if (line.preview.failureCode) return `<small class="d-block text-danger text-break" data-line-failure>${esc(failureWord(line.preview.failureCode))}</small>`;
        if (!rules.lotEligible(line.preview, line.lotId)) return `<small class="d-block text-danger text-break" data-line-failure>${esc(W('LotNoLongerEligible'))}</small>`;
        return '';
    };

    /** The lot cell: the CHOSEN lot (D-4), or "FEFO" with the lot the preview suggests; the FEFO reason under it (r3 — not under the status). */
    const lotCell = (line) => {
        let main;
        if (line.lotId) main = line.lotNumber || lotNumberOf(line.preview, line.lotId) || '-';
        else {
            const first = firstFree(line.preview);
            main = first ? format(W('LinesTable.LotFefo'), first.lotNumber) : W('LineFefoFirst');
        }
        return `<span data-line-lot>${esc(main)}</span>${reasonNote(line.reason)}`;
    };

    const menuCell = (line) => `<div class="dropdown"><button type="button" class="btn btn-sm btn-icon btn-text-secondary rounded-pill dropdown-toggle hide-arrow wh-touch" data-bs-toggle="dropdown" aria-expanded="false" aria-label="${esc(L().Actions || W('Actions'))}" data-line-menu="${esc(line.uid)}"><i class="bx bx-dots-vertical-rounded" aria-hidden="true"></i></button>`
        + '<div class="dropdown-menu dropdown-menu-end">'
        + `<button type="button" class="dropdown-item" data-line-action="edit" data-uid="${esc(line.uid)}"><i class="bx bx-edit me-1" aria-hidden="true"></i>${esc(W('Warehouse.Outbound.Action.Edit'))}</button>`
        + `<button type="button" class="dropdown-item" data-line-action="lot" data-uid="${esc(line.uid)}"><i class="bx bx-layer me-1" aria-hidden="true"></i>${esc(W('LinePanel.ChangeLot'))}</button>`
        + `<button type="button" class="dropdown-item text-danger" data-line-action="delete" data-uid="${esc(line.uid)}"><i class="bx bx-trash me-1" aria-hidden="true"></i>${esc(W('LinesTable.Delete'))}</button>`
        + '</div></div>';

    /** One table row from its cells (§8.2 column order); `menu` = the row menu (Create only — Edit is read-only, AC-11). */
    const tableRow = (cells, { uid, menu }) => `<tr data-line-row="${esc(uid)}">`
        + `<td>${cells.orderLine}</td><td>${cells.product}</td><td>${cells.sku}</td><td class="text-end text-nowrap">${cells.quantity}</td>`
        + `<td>${cells.lot}</td><td class="text-end text-nowrap">${cells.allocatable}</td><td>${cells.status}</td>`
        + (menu ? `<td class="cell-fit text-end">${menuCell({ uid })}</td>` : '')
        + '</tr>';

    const columnCount = () => byId('outbound-lines-table')?.querySelectorAll('thead th').length || 7;

    const renderLines = () => {
        const body = byId('outbound-lines-body');
        if (!body) return;
        const rows = lines.map((line, i) => tableRow({
            orderLine: esc(line.orderLineId),
            product: esc(line.itemText || '-'),
            sku: esc(line.skuText || '-'),
            quantity: `${esc(qty(String(line.quantity).trim().replace(',', '.')))} ${esc(line.uomId)}`,
            lot: lotCell(line),
            allocatable: esc(!line.stale && line.preview?.allocatable != null ? qty(line.preview.allocatable) : '-'),
            status: badge(rules.lineStatus(line)) + statusNote(line, i)
        }, { uid: line.uid, menu: mode !== 'edit' }));
        const skeleton = busy ? `<tr data-skeleton class="placeholder-glow"><td colspan="${columnCount()}"><span class="placeholder col-12"></span></td></tr>` : '';
        body.innerHTML = rows.length
            ? rows.join('') + skeleton
            : `<tr data-lines-empty><td colspan="${columnCount()}" class="text-center text-muted py-4">${esc(W('LinesTable.Empty'))}</td></tr>` + skeleton;
        const full = !rules.canAddLine(lines.length);
        const add = byId('btnAddLine');
        if (add) add.disabled = busy || full;
        byId('outbound-lines-limit')?.classList.toggle('d-none', !full);
        const previewButton = byId('btnPreview');
        if (previewButton) previewButton.disabled = busy || !lines.length;
    };

    /** Depo / Staging / a line changed: every preview is out of date (the chosen lots stay — AC-8). */
    const staleAll = () => {
        lines.forEach((line) => {
            if (line.preview) line.stale = true;
            line.error = null;
        });
        renderLines();
    };

    // ─── The table preview: every line, each with its lotOverride, in one request (§8.3, AC-6) ─────────────────────────────────────
    const runPreview = async () => {
        showAlert('');
        fieldError('StagingLocationIdError', '');
        if (!value('WarehouseId')) { fieldError('WarehouseIdError', W('ValidationWarehouse')); return; }
        fieldError('WarehouseIdError', '');
        if (!lines.length) { fieldError('LinesError', W('ValidationLines')); return; }
        const bodies = lines.map((line) => rules.lineBody(line));
        if (bodies.some((r) => r.error)) { fieldError('LinesError', W('ValidationLine')); return; }
        fieldError('LinesError', '');
        busy = true;
        renderLines();
        const answer = await post('/Warehouse/OutboundShipments/preview', previewBody(bodies.map((r) => r.line)));
        busy = false;
        if (!answer.ok) {
            // a refusal of one line (a lot out of the list, a FEFO violation) points at that line; the others stay "Önizlenmedi"
            const errors = rules.lineErrors(answer.json?.error);
            lines.forEach((line, i) => {
                line.preview = null;
                line.stale = false;
                line.error = errors[i] ? Object.assign({ message: failure(answer) }, errors[i]) : null;
            });
            renderLines();
            showAlert(stagingError(answer) || failure(answer));
            return;
        }
        const mapped = rules.mapPreview(answer.json, lines.length);
        lines.forEach((line, i) => {
            line.preview = mapped[i];
            line.stale = false;
            line.error = null;
            if (line.lotId) line.lotNumber = lotNumberOf(line.preview, line.lotId) || line.lotNumber;
        });
        renderLines();
    };

    // ─── The line panel (§8.3): 6 fields, "Satırı önizle", "Satırı kaydet" (Enter) ─────────────────────────────────────────────────────
    let draft = null;
    const panel = () => byId('offcanvasOutboundLine');
    const offcanvas = () => (panel() && window.bootstrap?.Offcanvas ? window.bootstrap.Offcanvas.getOrCreateInstance(panel()) : null);
    const panelAlert = (text) => { const box = byId('lineAlert'); if (box) { box.textContent = text || ''; box.classList.toggle('d-none', !text); } };
    const selectedText = (el) => el?.selectedOptions?.[0]?.textContent?.trim() || '';

    const setChoice = (el, id, text) => {
        if (!el) return;
        el.innerHTML = '<option value=""></option>';
        if (id) el.add(new Option(text || id, id, true, true));
        resync(el);
    };

    const setUomText = (uom) => {
        const el = byId('lineUom');
        if (el) el.textContent = uom ? format(el.dataset.template || '{0}', uom) : '-';
    };

    /** A product / SKU / quantity change: the panel's preview no longer answers this line. */
    const staleDraft = () => {
        if (!draft) return;
        if (draft.preview) draft.stale = true;
        draft.error = null;
        renderPanelPreview();
    };

    // the line's unit is the SKU's BASE unit (§8.17 step 1 — the service refuses another with 422 WMS_UOM_NOT_BASE)
    const loadUom = async () => {
        if (!draft) return;
        draft.uomId = '';
        setUomText('');
        const sku = rules.parseSku(draft.sku);
        if (!draft.itemId || !sku) return;
        const answer = await getJson(`/Warehouse/OutboundShipments/sku-facts?${new URLSearchParams({ itemId: draft.itemId, skuId: sku.skuId, skuLevel: sku.skuLevel })}`);
        if (!answer.ok || !answer.json?.available || !answer.json.baseUom) { panelAlert(W('SkuUnitUnavailable')); return; }
        draft.uomId = answer.json.baseUom;
        setUomText(draft.uomId);
    };

    /** AC-3: a new product empties the SKU and its unit, and select2 shows the empty SKU. */
    const clearSku = () => {
        draft.sku = '';
        draft.skuText = '';
        draft.uomId = '';
        setChoice(byId('lineSku'), '', '');
        setUomText('');
    };

    const onProductChange = () => {
        if (!draft) return;
        draft.itemId = byId('lineProduct')?.value || '';
        draft.itemText = selectedText(byId('lineProduct'));
        clearSku();
        staleDraft();
    };

    const onSkuChange = () => {
        if (!draft) return;
        draft.sku = byId('lineSku')?.value || '';
        draft.skuText = selectedText(byId('lineSku'));
        staleDraft();
        loadUom();
    };

    /** The lot list: "FEFO'ya göre (önerilen: …)" then the preview's free lots; a chosen lot that left the list stays chosen (AC-8). */
    const refreshLotOptions = () => {
        const select = byId('lineLot');
        if (!select || !draft) return;
        const lots = draft.preview?.lots || [];
        const first = firstFree(draft.preview);
        let html = `<option value="">${esc(first ? format(W('LinePanel.LotFefo'), first.lotNumber) : W('LineFefoFirst'))}</option>`;
        html += lots.map((l) => `<option value="${esc(l.lotId)}">${esc(format(W('LotOption'), l.lotNumber, l.fefoRank, l.expiryDate || '-', qty(l.allocatable)))}</option>`).join('');
        if (draft.lotId && !lots.some((l) => l.lotId === draft.lotId)) {
            html += `<option value="${esc(draft.lotId)}" data-lot-left>${esc(`${draft.lotNumber || draft.lotId} — ${W('LotNoLongerEligible')}`)}</option>`;
        }
        select.innerHTML = html;
        select.value = draft.lotId || '';
    };

    /** The reason field: only for a lot that is not the first free one; its need follows the company's FEFO mode (§8.3 field 6). */
    const refreshReason = () => {
        if (!draft) return;
        const need = rules.lotNeed(draft.preview, draft.lotId);
        byId('lineReasonBlock')?.classList.toggle('d-none', need === 'None');
        const hint = byId('lineReasonHint');
        if (hint) hint.textContent = need === 'Blocked' ? W('DeviationBlocked') : need === 'Required' ? W('DeviationNeedsReason') : need === 'Optional' ? W('DeviationOptional') : '';
        const save = byId('btnLineSave');
        if (save) save.disabled = need === 'Blocked';
    };

    const renderPanelPreview = () => {
        const host = byId('linePreviewBody');
        const box = byId('linePreview');
        if (!host || !box || !draft) return;
        const p = draft.preview;
        if (!p && !draft.error) { box.classList.add('d-none'); host.innerHTML = ''; return; }
        const parts = [];
        if (draft.stale) parts.push(badge('Stale'));
        if (draft.error) parts.push(`<div class="small text-danger" data-line-failure>${esc(draft.error.message || failureWord(draft.error.code))}</div>`);
        if (p) {
            const first = firstFree(p);
            if (first) parts.push(`<div class="small" data-preview-fefo>${esc(format(W('LinePanel.FefoSuggestion'), first.lotNumber, first.expiryDate || '-'))}</div>`);
            // D-4: the allocation the answer made — from the CHOSEN lot when the line has one
            (p.allocations || []).forEach((a) => parts.push(`<div class="small" data-preview-allocation>${esc(format(W('PreviewAllocation'), a.lotNumber || '-', qty(a.quantity), a.fefoRank ?? '-'))}</div>`));
            parts.push(`<div class="small" data-preview-allocatable>${esc(format(W('LineAllocatable'), qty(p.allocatable)))}</div>`);
            if (p.failureCode) parts.push(`<div class="small text-danger" data-line-failure>${esc(failureWord(p.failureCode))}</div>`);
            if (!rules.lotEligible(p, draft.lotId)) parts.push(`<div class="small text-danger" data-line-failure>${esc(W('LotNoLongerEligible'))}</div>`);
            if (p.deviation) {
                parts.push(`<div class="small text-warning" data-preview-deviation>${esc(format(W('PreviewChosenLot'), p.deviation.firstEligibleLotNumber || '-', p.deviation.firstEligibleExpiry || '-'))}</div>`);
                const modeName = p.deviation.mode || p.mode;
                if (modeName) parts.push(`<div class="small text-warning" data-preview-mode>${esc(format(W('LinePanel.ModeLabel'), W(`LinePanel.Mode.${modeName}`) || modeName))}</div>`);
            }
        }
        host.innerHTML = parts.join('');
        box.classList.remove('d-none');
    };

    const readDraft = () => {
        if (!draft) return;
        draft.quantity = (byId('lineQuantity')?.value || '').trim();
        draft.orderLineId = (byId('lineOrderLine')?.value || '').trim();
        draft.reason = byId('lineReason')?.value || '';
    };

    const openPanel = (uid, focus) => {
        if (!uid && !rules.canAddLine(lines.length)) { fieldError('LinesError', W('LineLimitReached')); return; }
        const line = uid ? lines.find((l) => l.uid === uid) : null;
        if (uid && !line) return;
        draft = line ? Object.assign({}, line) : blankLine();
        const title = byId('offcanvasOutboundLineLabel');
        if (title) title.textContent = (line ? title.dataset.titleEdit : title.dataset.titleAdd) || title.textContent;
        setChoice(byId('lineProduct'), draft.itemId, draft.itemText);
        setChoice(byId('lineSku'), draft.sku, draft.skuText);
        setUomText(draft.uomId);
        // the reader's culture without a thousands separator — the form an input can take back (rules.lineBody reads "1,5" / "1.5")
        byId('lineQuantity').value = draft.quantity ? rules.quantityText(String(draft.quantity).trim().replace(',', '.'), culture(), false) : '';
        byId('lineOrderLine').value = draft.orderLineId;
        byId('lineReason').value = draft.reason || '';
        panelAlert('');
        refreshLotOptions();
        refreshReason();
        renderPanelPreview();
        offcanvas()?.show();
        if (focus === 'lot') {
            byId('lineLot')?.focus();
            if (!draft.preview || draft.stale) runPanelPreview(); // "Lot değiştir" on a line not previewed: first its own preview (§8.2)
        }
    };

    /** "Satırı önizle": THIS line alone, with its lotOverride (AC-5) — the header and the line, nothing written. */
    const runPanelPreview = async () => {
        if (!draft) return;
        readDraft();
        panelAlert('');
        if (!value('WarehouseId')) { panelAlert(W('ValidationWarehouse')); return; }
        const result = rules.lineBody(draft);
        if (result.error) { panelAlert(W(result.error)); return; }
        const button = byId('btnLinePreview');
        if (button) button.disabled = true;
        const answer = await post('/Warehouse/OutboundShipments/preview', previewBody([result.line]));
        if (button) button.disabled = false;
        if (!answer.ok) {
            const errors = rules.lineErrors(answer.json?.error);
            draft.preview = null;
            draft.stale = false;
            draft.error = errors[0] ? Object.assign({ message: failure(answer) }, errors[0]) : null;
            panelAlert(stagingError(answer) || failure(answer));
            refreshReason();
            renderPanelPreview();
            return;
        }
        draft.preview = rules.mapPreview(answer.json, 1)[0];
        draft.stale = false;
        draft.error = null;
        if (draft.lotId) draft.lotNumber = lotNumberOf(draft.preview, draft.lotId) || draft.lotNumber;
        refreshLotOptions();
        refreshReason();
        renderPanelPreview();
    };

    const onLotChange = () => {
        if (!draft) return;
        draft.lotId = byId('lineLot')?.value || '';
        draft.lotNumber = draft.lotId ? lotNumberOf(draft.preview, draft.lotId) || draft.lotNumber : '';
        refreshReason();
        // E3-FIX-01 (item 4), carried: a lot change is previewed at once — the preview shows the CHOSEN lot, not the FEFO suggestion again
        if (draft.preview) runPanelPreview();
    };

    /** "Satırı kaydet" (Enter): the page's line array only — local, undoable; the 101st line is refused here too (AC-4). */
    const savePanel = () => {
        if (!draft) return;
        readDraft();
        panelAlert('');
        if (!draft.uid && !rules.canAddLine(lines.length)) { panelAlert(W('LineLimitReached')); return; }
        if (rules.orderLineTaken(lines, draft.orderLineId, draft.uid)) { panelAlert(W('LinePanel.OrderLineTaken')); return; }
        const error = rules.lineSaveError(draft);
        if (error) { panelAlert(W(error)); return; }
        const line = Object.assign({}, draft, { error: null });
        if (line.uid) {
            lines = lines.map((l) => (l.uid === line.uid ? line : l));
        } else {
            uidSeq += 1;
            line.uid = `line-${uidSeq}`;
            lines.push(line);
        }
        draft = null;
        fieldError('LinesError', '');
        offcanvas()?.hide();
        renderLines();
    };

    /** "Sil": a local removal behind the product's one confirm (MOD-0013); nothing saved is deleted. */
    const deleteLine = (uid) => {
        const line = lines.find((l) => l.uid === uid);
        if (!line) return;
        const remove = () => {
            lines = lines.filter((l) => l.uid !== uid);
            staleAll(); // the other lines' allocation may change without this one
        };
        if (!window.showConfirm) { remove(); return; }
        window.showConfirm(format(W('LinesTable.DeleteConfirm'), line.orderLineId), remove, {
            type: 'danger',
            icon: 'bx-trash',
            entityName: `${line.orderLineId} · ${line.skuText || line.itemText || ''}`,
            confirmButtonText: W('LinesTable.Delete'),
            cancelButtonText: W('LinePanel.Discard') // r3 F-1 (DL-1): the shared default "İptal" beside "Sil" would read like undoing the line
        });
    };

    // ─── Save ────────────────────────────────────────────────────────────────
    const saveKey = newKey();
    let original = null; // edit: the outbound as read (for the changed members)

    const allocationFailure = (answer) => {
        const failed = answer?.json?.error?.details?.lines || [];
        if (!failed.length) return failure(answer);
        return [failure(answer), ...failed.map((l) => format(W('AllocationLineFailed'), l.lineSeq, W(`Warehouse.Outbound.LineFailure.${l.code}`) || l.code, qty(l.requested), qty(l.allocatable)))].join(' ');
    };

    /** The lines as the proxy wants them, in TABLE order, or null with the field error shown (AC-9, AC-7, AC-8). */
    const linesBody = () => {
        if (!lines.length) { fieldError('LinesError', W('ValidationLines')); return null; }
        for (const line of lines) {
            const error = rules.lineSaveError(line);
            if (error) { fieldError('LinesError', format(W('LinesTable.LineError'), line.orderLineId, W(error))); return null; }
        }
        fieldError('LinesError', '');
        return lines.map((line) => rules.lineBody(line).line);
    };

    const submitCreate = async () => {
        const { body, shipTo } = header();
        if (!body.warehouseId) { fieldError('WarehouseIdError', W('ValidationWarehouse')); return; }
        fieldError('WarehouseIdError', '');
        if (!shipTo.name || !shipTo.country) { fieldError('ShipToError', W('ValidationShipTo')); return; }
        fieldError('ShipToError', '');
        const sent = linesBody();
        if (!sent) return;
        fieldError('StagingLocationIdError', '');
        const answer = await post('/Warehouse/OutboundShipments/create', Object.assign({ idempotencyKey: saveKey, shipTo, lines: sent }, body));
        if (!answer.ok) {
            // AC-10: 422 WMS_ALLOCATION_FAILED (details.lines[]) and a one-line refusal (details.lineSeq) land on their rows; no outbound, no redirect
            const errors = rules.lineErrors(answer.json?.error);
            if (Object.keys(errors).length) {
                lines.forEach((line, i) => { line.error = errors[i] ? Object.assign({ message: failure(answer) }, errors[i]) : null; });
                renderLines();
            }
            showAlert(stagingError(answer) || allocationFailure(answer));
            return;
        }
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

    /** Edit: the saved lines in the same table, read-only (AC-11) — lot(s) with the saved FEFO reason under each, allocated, reservation state. */
    const renderEditLines = (saved) => {
        const body = byId('outbound-lines-body');
        if (!body) return;
        const sum = (values) => {
            const total = values.reduce((s, v) => s + (Number.parseFloat(v) || 0), 0);
            return String(Number(total.toFixed(6)));
        };
        const rows = (saved || []).map((l) => {
            const allocations = l.allocations || [];
            const lot = allocations.length
                ? allocations.map((a) => `<div data-alloc-lot><span data-line-lot>${esc(a.lotNumber || '-')}</span>${reasonNote(a.fefoBypassReason)}</div>`).join('')
                : esc('-');
            const states = Array.from(new Set(allocations.map((a) => W(`Warehouse.Outbound.ReleaseState.${a.reservationReleaseState || 'None'}`))));
            return tableRow({
                orderLine: esc(l.orderLineId),
                product: esc(l.product || '-'),
                sku: esc(l.sku || '-'),
                quantity: `${esc(qty(l.quantity))} ${esc(l.uom)}`,
                lot,
                allocatable: esc(allocations.length ? qty(sum(allocations.map((a) => a.quantity))) : '-'),
                status: esc(states.join(', ') || '-')
            }, { uid: l.lineSeq, menu: false });
        });
        body.innerHTML = rows.length ? rows.join('') : `<tr data-lines-empty><td colspan="${columnCount()}" class="text-center text-muted py-4">-</td></tr>`;
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
        renderEditLines(o.lines);
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
        onChange(byId('WarehouseId'), () => { window.jQuery?.(byId('StagingLocationId')).val(null).trigger('change'); staleAll(); });
        onChange(byId('StagingLocationId'), staleAll);
        byId('btnAddLine')?.addEventListener('click', () => openPanel(null));
        byId('btnPreview')?.addEventListener('click', runPreview);
        byId('outbound-lines-body')?.addEventListener('click', (event) => {
            const button = event.target.closest('[data-line-action]');
            if (!button) return;
            const { lineAction, uid } = button.dataset;
            if (lineAction === 'edit') openPanel(uid);
            else if (lineAction === 'lot') openPanel(uid, 'lot');
            else if (lineAction === 'delete') deleteLine(uid);
        });

        // the panel: select2 lists open INSIDE the panel (dropdownParent — a list under the offcanvas would be unreachable)
        const parent = panel() && window.jQuery ? window.jQuery(panel()) : undefined;
        const product = byId('lineProduct');
        const sku = byId('lineSku');
        select2(product, '/Warehouse/OutboundShipments/products', null, { dropdownParent: parent });
        select2(sku, '/Warehouse/OutboundShipments/skus', () => ({ itemId: draft?.itemId || '' }), { dropdownParent: parent });
        onChange(product, onProductChange);
        onChange(sku, onSkuChange);
        byId('lineQuantity')?.addEventListener('input', staleDraft);
        byId('lineLot')?.addEventListener('change', onLotChange);
        byId('lineReason')?.addEventListener('input', () => { if (draft) draft.reason = byId('lineReason').value; });
        byId('btnLinePreview')?.addEventListener('click', runPanelPreview);
        byId('btnLineSave')?.addEventListener('click', savePanel);
        renderLines();
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => OutboundShipmentForm.init());
