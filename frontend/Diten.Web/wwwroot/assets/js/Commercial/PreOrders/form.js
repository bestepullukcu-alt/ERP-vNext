/**
 * WP-VW-W6 · W6-WEB-a — A: the representative's pre-order (mockup W6 A) — create, edit, sign, send, and the locked view
 * of a submitted order with its timeline and its way out.
 *
 * THE SERVER DECIDES, THE PAGE SHOWS. The company / country / currency / price mode are the context's (the rep's seat →
 * legal entity → MDM; never chosen). Every price, free box, combined discount, finding, total and FX line is the CRM's
 * preview (POST …/preview — MOD-0156's evaluation, nothing saved), asked again a moment after each change; the page
 * computes none of them (MOD-0168 §4.2). In quantity mode there is no discount and no price field (AC-PRICE-3).
 *
 * The draft is saved explicitly ("Save draft"; the first save gives the ORD number — D15). The pharmacist signs on the
 * pad (touch / pen / mouse); a change of lines or pharmacy after signing clears the signature (the CRM's rule). "Send" stays
 * closed with its reasons written under it; the CRM refuses on its own too (AC-FLOW-3). A submitted order is drawn locked:
 * the timeline, and "Cancel and open a new order" (approved / transmitted / failed — a reason is required, D5) or "Copy
 * and open a new order" (rejected / cancelled — D6).
 */
'use strict';

const PreOrderForm = (function () {
    const C = window.PreOrdersCore;
    const U = window.PreOrdersUi;
    const L = () => window.L10n || {};
    const $ = window.jQuery;
    const byId = (id) => document.getElementById(id);
    const root = byId('po-root');
    const endpoint = '/Commercial/PreOrders/api';
    const flag = (name) => !!root && root.getAttribute('data-' + name) === 'true';

    const S = { order: null, context: null, preview: null, lines: [], pharmacy: null, depot: null, dirty: false, busy: false, seq: 0, previewToken: 0, previewTimer: null };

    const isPriced = () => {
        if (S.order && S.order.status !== 'draft') return S.order.priceMode === 'priced';
        if (S.preview) return S.preview.mode === 'priced';
        return !!S.context && S.context.priceMode === 'priced';
    };
    const currency = () => (S.order && S.order.status !== 'draft' ? S.order.currency : (S.preview ? S.preview.currency : (S.context && S.context.currency))) || '';
    const lock = () => C.lockState(S.order);
    const locked = () => !!lock().locked;
    const newKey = () => 'l' + (++S.seq);

    const alertBox = (text) => { const a = byId('po-alert'); if (!a) return; a.textContent = text || ''; a.classList.toggle('d-none', !text); if (text) { a.scrollIntoView({ block: 'nearest' }); } };

    // ── the "from the system 🔒" strip ────────────────────────────────────────────────────────────────────────
    const drawSystem = () => {
        const c = S.context || {};
        const o = S.order;
        const company = o ? (o.seller || {}).name : c.legalEntityName;
        const country = o ? o.countryCode : c.countryCode;
        const cur = currency() || (o ? o.localCurrencyCode : c.localCurrencyCode);
        byId('po-sys-company').innerHTML = U.bidi(company || '—');
        byId('po-sys-country').textContent = country || '';
        byId('po-sys-currency').textContent = cur || '';
        byId('po-sys-mode').textContent = isPriced() ? L().ModePricedBadge : L().ModeQuantityBadge;
        byId('po-currency').value = isPriced() ? cur : L().QuantityModeShort;
        const list = (o && o.price) || (S.preview && S.preview.priceList) || c.priceList;
        const tier = (o && o.price && o.price.tier) || (S.preview && S.preview.tier) || c.tier;
        byId('po-sys-price').innerHTML = isPriced() && list ? U.Th('PriceSource', list.name || '', L()['Tier_' + String(tier || '').replace(/-/g, '_')] || tier || '', U.day(list.validFrom)) : '';
        byId('po-lines-note').textContent = isPriced() ? L().LinesNotePriced : L().LinesNoteQuantity;
    };

    // ── the lines ────────────────────────────────────────────────────────────────────────────────────────────
    const resultOf = (index) => {
        if (locked() && S.order) return (S.order.lines || [])[index] || null;
        if (!S.preview) return null;
        const withSku = S.lines.map((l, i) => (l.skuId ? i : -1)).filter((i) => i >= 0);
        const at = withSku.indexOf(index);
        return at >= 0 ? (S.preview.lines || [])[at] || null : null;
    };

    const drawResults = () => {
        const priced = isPriced();
        const list = (S.order && S.order.price) || (S.preview && S.preview.priceList);
        S.lines.forEach((l, i) => {
            const el = byId('po-res-' + l.key);
            if (!el) return;
            const priceSource = priced && list ? U.Th('PriceSourceShort', list.name || '') : '';
            el.innerHTML = U.lineResult(resultOf(i), { priced: priced, currency: currency(), priceSource: priceSource, skuName: l.skuText });
        });
    };

    const skuPicker = (el) => {
        if (!$ || !$.fn.select2 || !el) return;
        $(el).select2({
            width: '100%',
            placeholder: L().ProductPlaceholder || '',
            minimumInputLength: 2,
            language: { inputTooShort: () => L().SearchHint || '', noResults: () => L().NoMatch || '', searching: () => L().Searching || '' },
            ajax: {
                delay: 250,
                transport: (params, success, failure) => U.request('GET', endpoint + '/lookups/skus?q=' + encodeURIComponent((params.data && params.data.term) || '')).then((r) => (r.ok ? success(r.body) : failure())),
                processResults: (body) => ({ results: (((body || {}).data || {}).items || []).map((o) => ({ id: o.id, text: o.text, sub: o.sub })) })
            },
            templateResult: (o) => (o.sub ? $('<div></div>').append($('<div></div>').text(o.text)).append($('<small class="text-muted"></small>').text(o.sub)) : o.text)
        });
        $(el).on('select2:select', (e) => {
            const line = S.lines.find((x) => x.key === el.getAttribute('data-key'));
            if (!line) return;
            line.skuId = e.params.data.id;
            line.skuText = e.params.data.text;
            changed();
        });
    };

    const drawLines = () => {
        const host = byId('po-lines');
        const priced = isPriced();
        const isLocked = locked();
        if ($) { host.querySelectorAll('select.po-sku').forEach((s) => { if ($(s).hasClass('select2-hidden-accessible')) { $(s).select2('destroy'); } }); }
        host.innerHTML = S.lines.map((l) => U.formLineHtml(l, priced, isLocked)).join('') || '<p class="text-muted small mb-0">' + U.esc(L().NoLinesYet) + '</p>';
        if (!isLocked) { host.querySelectorAll('select.po-sku').forEach(skuPicker); }
        byId('po-add-line').classList.toggle('d-none', isLocked);
        drawResults();
    };

    const onLineInput = (event) => {
        const t = event.target;
        const key = t && t.getAttribute && t.getAttribute('data-key');
        const line = key && S.lines.find((x) => x.key === key);
        if (!line) return;
        if (t.classList.contains('po-qty')) line.quantity = t.value;
        if (t.classList.contains('po-mf')) { line.mf = t.value; t.classList.toggle('is-invalid', C.parseMf(t.value) === 'invalid'); }
        if (t.classList.contains('po-disc')) { line.discount = t.value; t.classList.toggle('is-invalid', C.parseDiscount(t.value) === 'invalid'); }
        changed();
    };

    // ── the preview (MOD-0156's answer for the draft — nothing saved) ───────────────────────────────────────────
    const schedulePreview = () => {
        if (locked() || !S.context) return;
        clearTimeout(S.previewTimer);
        S.previewTimer = setTimeout(runPreview, 450);
    };
    const runPreview = async () => {
        const lines = C.linesInput(S.lines, isPriced());
        if (!S.pharmacy || !lines.length || C.inputProblem(S.lines, isPriced())) { S.preview = S.preview && lines.length ? S.preview : null; drawDynamic(); return; }
        const token = ++S.previewToken;
        const r = await U.request('POST', endpoint + '/preview', { pharmacyAccountId: S.pharmacy.id, lines: lines });
        if (token !== S.previewToken) return;
        if (r.ok) { S.preview = U.data(r); alertBox(''); } else { S.preview = null; alertBox(U.errorText(r)); }
        const pricedBefore = byId('po-lines').querySelector('.po-disc') !== null;
        if (pricedBefore !== isPriced() && S.lines.length) { drawLines(); }
        drawDynamic();
    };

    // ── the right column ─────────────────────────────────────────────────────────────────────────────────────
    const drawSummary = () => {
        const host = byId('po-summary');
        if (locked() && S.order) { host.innerHTML = U.totals(S.order); return; }
        const p = S.preview;
        if (!p) { host.innerHTML = isPriced() ? '<p class="text-muted small mb-0">' + U.esc(L().SummaryPending) + '</p>' : U.totals({ priceMode: 'quantity' }); return; }
        host.innerHTML = U.totals({ priceMode: p.mode, currency: p.currency, totals: p.totals, fx: p.fx });
    };

    const drawNeed = () => {
        const host = byId('po-need');
        if (S.order && S.order.status !== 'draft' && S.order.approvalRequirement) {
            const req = S.order.approvalRequirement;
            const names = Object.values(req.approverNames || {}).filter(Boolean).join(', ');
            host.innerHTML = '<p class="mb-1 text-heading">' + U.esc(req.required ? L().Need_yes : L().Need_no) + '</p>'
                + (req.reasons || []).map((r) => '<small class="d-block text-muted">' + U.esc(L()[r === 'tenant-setting' ? 'Need_tenant_setting' : 'Need_soft'] || '') + '</small>').join('')
                + (names ? '<small class="d-block mt-1">' + U.Th('Approver_line', names) + '</small>' : '');
            return;
        }
        const box = C.approvalBox(S.preview, S.context);
        host.innerHTML = '<p class="mb-1 text-heading"><i class="bx ' + (box.required ? 'bx-user-check text-warning' : 'bx-check-circle text-success') + ' me-1"></i>' + U.esc(L()[box.answerKey] || '') + '</p>'
            + box.reasons.map((k) => '<small class="d-block text-muted">' + U.esc(L()[k] || '') + '</small>').join('')
            + '<div class="d-flex align-items-start gap-2 mt-2"><span class="avatar avatar-sm bg-label-primary rounded-circle d-flex align-items-center justify-content-center"><i class="bx bx-user"></i></span><div><div>' + U.Th(box.approver.key, ...box.approver.args) + '</div>'
            + (box.position ? '<small class="text-muted">' + U.Th('ApproverPosition', box.position) + '</small>' : '') + '</div></div>';
    };

    const drawSignature = () => {
        const sig = S.order && S.order.signature;
        const isLocked = locked();
        byId('po-signed').classList.toggle('d-none', !sig);
        byId('po-sign-pad').classList.toggle('d-none', !!sig || isLocked);
        byId('po-resign').classList.toggle('d-none', !sig || isLocked);
        if (sig) { byId('po-signed').innerHTML = '<div class="po-signed text-heading"><i class="bx bx-check-shield text-success me-1"></i>' + U.Th('SignedBy', sig.signerName, U.moment(sig.signedAt)) + '</div><small class="text-muted">' + U.esc(L().SignedNote) + '</small>'; }
        if (!sig && isLocked) { byId('po-signed').classList.remove('d-none'); byId('po-signed').innerHTML = '<small class="text-muted">' + U.esc(L().SignatureNone) + '</small>'; }
    };

    const blockers = () => {
        const outcome = S.preview ? S.preview.outcome : null;
        return C.sendBlockers({
            canSubmit: flag('can-submit'),
            lineCount: S.lines.filter((l) => l.skuId).length,
            outcome: outcome,
            hasDepot: !!S.depot,
            hasSignature: !!(S.order && S.order.signature),
            dirty: S.dirty
        });
    };

    const drawSend = () => {
        const card = byId('po-send-card');
        const isLocked = locked();
        card.classList.toggle('d-none', isLocked);
        if (isLocked) return;
        const why = blockers();
        byId('po-send').disabled = why.length > 0 || S.busy;
        byId('po-send-why').innerHTML = why.map((k) => '<li class="d-flex gap-1"><i class="bx bx-block text-danger mt-1"></i><span>' + U.esc(L()[k] || '') + '</span></li>').join('');
        byId('po-save').classList.toggle('d-none', isLocked);
        byId('po-discard').classList.toggle('d-none', isLocked || !S.order);
    };

    const drawHeader = () => {
        const o = S.order;
        const status = byId('po-status');
        if (o) {
            byId('po-title').innerHTML = U.Th('FormTitleOrder', o.code);
            byId('po-subtitle').innerHTML = U.bidi((o.pharmacy || {}).name || '') + (o.rep && o.rep.displayName ? ' · ' + U.bidi(o.rep.displayName) : '');
            status.innerHTML = U.statusBadge(o.status);
            status.classList.remove('d-none');
        }
        const last = o ? C.timeline(o).filter((e) => e.type === 'changes-requested').pop() : null;
        const banner = byId('po-change-banner');
        const showBanner = !!(o && o.status === 'draft' && last);
        banner.classList.toggle('d-none', !showBanner);
        if (showBanner) { byId('po-change-text').innerHTML = '<strong>' + U.esc(L().ChangeBannerTitle) + '</strong> ' + (last.reason ? U.bidi(last.reason) : ''); }

        const card = byId('po-timeline-card');
        const showTimeline = !!(o && (o.submissionNo > 0 || o.status !== 'draft'));
        card.classList.toggle('d-none', !showTimeline);
        if (showTimeline) { byId('po-timeline').innerHTML = U.timelineHtml(o); }
        const st = lock();
        const note = byId('po-lock-note');
        note.classList.toggle('d-none', !(st.locked && st.noteKey));
        if (st.noteKey) { byId('po-lock-text').textContent = L()[st.noteKey] || ''; }
        const action = byId('po-lock-action');
        action.classList.toggle('d-none', !st.action);
        action.textContent = st.action === 'cancel-new' ? L().CancelAndNew : (st.action === 'copy' ? L().CopyAndNew : '');
        action.setAttribute('data-action', st.action || '');
        ['po-pharmacy', 'po-depot', 'po-delivery', 'po-note'].forEach((id) => { const n = byId(id); if (n) { n.disabled = st.locked; } });
        const fp = byId('po-delivery')._flatpickr;
        if (fp && fp.altInput) { fp.altInput.disabled = st.locked; }
    };

    const drawDynamic = () => { drawSystem(); drawResults(); drawSummary(); drawNeed(); drawSignature(); drawSend(); };
    const drawAll = () => { drawHeader(); drawSystem(); drawLines(); drawSummary(); drawNeed(); drawSignature(); drawSend(); };

    const changed = () => { S.dirty = true; drawSend(); schedulePreview(); };

    // ── the pickers ──────────────────────────────────────────────────────────────────────────────────────────
    const accountPicker = (id, source, onPick) => {
        const el = byId(id);
        if (!$ || !$.fn.select2 || !el) return;
        $(el).select2({
            width: '100%',
            placeholder: el.getAttribute('data-placeholder') || '',
            minimumInputLength: 2,
            language: { inputTooShort: () => L().SearchHint || '', noResults: () => L().NoMatch || '', searching: () => L().Searching || '' },
            ajax: {
                delay: 250,
                transport: (params, success, failure) => U.request('GET', endpoint + '/lookups/' + source + '?q=' + encodeURIComponent((params.data && params.data.term) || '')).then((r) => (r.ok ? success(r.body) : failure())),
                processResults: (body) => ({ results: (((body || {}).data || {}).items || []).map((o) => ({ id: o.id, text: o.text + (o.sub ? ' · ' + o.sub : '') })) })
            }
        });
        $(el).on('select2:select', (e) => onPick({ id: e.params.data.id, text: e.params.data.text }));
    };
    const setPicked = (id, value) => {
        const el = byId(id);
        if (!el) return;
        el.innerHTML = value ? '<option value="' + U.esc(value.id) + '" selected>' + U.esc(value.text) + '</option>' : '<option value=""></option>';
        if ($) { $(el).trigger('change.select2'); }
    };

    const loadPrevious = async () => {
        const host = byId('po-previous');
        if (!S.pharmacy) { host.innerHTML = '<p class="text-muted small mb-0">' + U.esc(L().PreviousOrdersNone) + '</p>'; return; }
        const r = await U.request('GET', endpoint + '/list?start=0&length=4&pharmacy=' + encodeURIComponent(S.pharmacy.id));
        const items = ((U.data(r) || {}).items || []).filter((x) => !S.order || x.id !== S.order.id).slice(0, 3);
        host.innerHTML = items.length
            ? '<ul class="list-unstyled mb-0 po-previous">' + items.map((x) => '<li class="d-flex flex-wrap align-items-center gap-2 py-1 border-bottom"><a class="fw-medium" href="/Commercial/PreOrders/Details/' + encodeURIComponent(x.id) + '">' + U.bidi(x.code) + '</a><small class="text-muted">' + U.esc(U.day(x.createdAt)) + '</small><small class="text-muted">' + U.esc(U.T('LinesCount', C.num(x.lineCount, U.culture(), 0))) + '</small><span class="ms-auto text-heading">' + U.esc(x.priceMode === 'priced' ? U.money(x.gross, x.currency) : L().QuantityModeShort) + '</span>' + U.statusBadge(x.status) + '</li>').join('') + '</ul>'
            : '<p class="text-muted small mb-0">' + U.esc(L().PreviousOrdersNone) + '</p>';
    };

    // ── save / sign / send / discard / the way out ──────────────────────────────────────────────────────────
    const input = () => ({
        pharmacyAccountId: S.pharmacy ? S.pharmacy.id : null,
        depotAccountId: S.depot ? S.depot.id : null,
        requestedDeliveryDate: byId('po-delivery').value || null,
        note: byId('po-note').value.trim() || null,
        visitId: (S.order && S.order.visitId) || root.getAttribute('data-visit-id') || null,
        lines: C.linesInput(S.lines, isPriced()),
        expectedVersion: S.order ? S.order.version : null
    });

    const fill = (order) => {
        S.order = order;
        S.pharmacy = order.pharmacy && order.pharmacy.accountId ? { id: order.pharmacy.accountId, text: order.pharmacy.name } : null;
        S.depot = order.depot && order.depot.accountId ? { id: order.depot.accountId, text: order.depot.name } : null;
        S.lines = (order.lines || []).map((l) => ({ key: newKey(), skuId: l.skuId, skuText: (l.sku || {}).name || (l.sku || {}).code || l.skuId, quantity: l.quantity, mf: C.mfText(l.mf), discount: l.discountPct == null ? '' : String(l.discountPct) }));
        setPicked('po-pharmacy', S.pharmacy);
        setPicked('po-depot', S.depot);
        const fp = byId('po-delivery')._flatpickr;
        if (fp) { fp.setDate(order.requestedDeliveryDate || null, false); } else { byId('po-delivery').value = order.requestedDeliveryDate || ''; }
        byId('po-note').value = order.note || '';
        S.dirty = false;
        drawAll();
        if (root.getAttribute('data-screen') === 'create' && window.history && window.history.replaceState) {
            window.history.replaceState(null, '', '/Commercial/PreOrders/Edit/' + encodeURIComponent(order.id));
            root.setAttribute('data-screen', 'edit');
        }
    };

    const save = async (quiet) => {
        const problem = C.inputProblem(S.lines, isPriced());
        if (problem) { alertBox(L()[problem] || ''); return false; }
        S.busy = true; drawSend();
        const r = S.order
            ? await U.request('PUT', endpoint + '/orders/' + encodeURIComponent(S.order.id), input())
            : await U.request('POST', endpoint + '/orders', input());
        S.busy = false;
        if (!r.ok) { alertBox(U.errorText(r)); drawSend(); return false; }
        alertBox('');
        fill(U.data(r));
        if (!quiet) { U.toast(L().DraftSaved, 'success'); }
        schedulePreview();
        return true;
    };

    // the signature pad: pointer events (touch, pen, mouse), drawn in the theme's text colour
    const pad = { drawing: false, empty: true };
    const initPad = () => {
        const canvas = byId('po-sign-canvas');
        if (!canvas || !canvas.getContext) return;
        const fit = () => { const r = canvas.getBoundingClientRect(); const ratio = window.devicePixelRatio || 1; canvas.width = Math.max(1, r.width * ratio); canvas.height = Math.max(1, r.height * ratio); const g = canvas.getContext('2d'); g.scale(ratio, ratio); g.lineWidth = 2; g.lineCap = 'round'; g.strokeStyle = getComputedStyle(canvas).color || 'black'; pad.empty = true; syncPad(); };
        const at = (e) => { const r = canvas.getBoundingClientRect(); return { x: e.clientX - r.left, y: e.clientY - r.top }; };
        canvas.addEventListener('pointerdown', (e) => { if (locked()) return; pad.drawing = true; canvas.setPointerCapture(e.pointerId); const p = at(e); const g = canvas.getContext('2d'); g.beginPath(); g.moveTo(p.x, p.y); });
        canvas.addEventListener('pointermove', (e) => { if (!pad.drawing) return; const p = at(e); const g = canvas.getContext('2d'); g.lineTo(p.x, p.y); g.stroke(); pad.empty = false; });
        ['pointerup', 'pointercancel', 'pointerleave'].forEach((n) => canvas.addEventListener(n, () => { if (pad.drawing) { pad.drawing = false; syncPad(); } }));
        byId('po-sign-clear').addEventListener('click', fit);
        byId('po-signer').addEventListener('input', syncPad);
        window.addEventListener('resize', () => { if (pad.empty) { fit(); } });
        requestAnimationFrame(fit);
    };
    const syncPad = () => { byId('po-sign-save').disabled = pad.empty || !byId('po-signer').value.trim() || S.busy; };

    const sign = async () => {
        if ((!S.order || S.dirty) && !(await save(true))) return;
        S.busy = true; syncPad();
        const image = byId('po-sign-canvas').toDataURL('image/png');
        const r = await U.request('POST', endpoint + '/orders/' + encodeURIComponent(S.order.id) + '/signature', { signerName: byId('po-signer').value.trim(), imageBase64: image, mediaType: 'image/png', expectedVersion: S.order.version });
        S.busy = false; syncPad();
        if (!r.ok) { alertBox(U.errorText(r)); return; }
        alertBox('');
        fill(U.data(r));
        U.toast(L().SignatureSaved, 'success');
        schedulePreview();
    };

    const send = async () => {
        if (blockers().length || !S.order) return;
        S.busy = true; drawSend();
        const r = await U.request('POST', endpoint + '/orders/' + encodeURIComponent(S.order.id) + '/submit', { expectedVersion: S.order.version });
        S.busy = false;
        if (!r.ok) { alertBox(U.errorText(r)); drawSend(); return; }
        alertBox('');
        const order = U.data(r);
        fill(order);
        U.toast(order.status === 'pending_approval' ? L().SubmittedPending : L().SubmittedApproved, 'success');
    };

    const discard = async () => {
        if (!S.order || !window.confirm(L().DiscardConfirm || '')) return;
        const r = await U.request('POST', endpoint + '/orders/' + encodeURIComponent(S.order.id) + '/discard', { expectedVersion: S.order.version });
        if (!r.ok) { alertBox(U.errorText(r)); return; }
        window.location.href = '/Commercial/PreOrders';
    };

    const wayOut = async () => {
        const action = byId('po-lock-action').getAttribute('data-action');
        if (!S.order || !action) return;
        if (action === 'cancel-new') {
            const reason = await U.askReason(L().CancelTitle, L().CancelText, L().CancelConfirm);
            if (!reason) return;
            const c = await U.request('POST', endpoint + '/orders/' + encodeURIComponent(S.order.id) + '/cancel', { reason: reason, expectedVersion: S.order.version });
            if (!c.ok) { alertBox(U.errorText(c)); return; }
        }
        const r = await U.request('POST', endpoint + '/orders/' + encodeURIComponent(S.order.id) + '/copy', {});
        const copy = U.data(r);
        if (!r.ok || !copy) { alertBox(U.errorText(r)); return; }
        window.location.href = '/Commercial/PreOrders/Edit/' + encodeURIComponent(copy.id);
    };

    const init = async () => {
        if (!root || !C || !U) return;
        const ctx = await U.request('GET', endpoint + '/context');
        S.context = U.data(ctx);
        const orderId = root.getAttribute('data-order-id');
        if (!S.context && !orderId) { alertBox(U.errorText(ctx)); ['po-save', 'po-add-line'].forEach((id) => { byId(id).disabled = true; }); drawSend(); return; }

        const altFormat = L().DateFormat || 'Y-m-d';
        const delivery = byId('po-delivery');
        if (delivery && typeof delivery.flatpickr === 'function') {
            delivery.flatpickr({ monthSelectorType: 'static', dateFormat: 'Y-m-d', altInput: true, altFormat: altFormat, allowInput: true, minDate: 'today', onChange: () => changed() });
            window.DitenDateField?.guardAgainstSilentMisparse?.(delivery._flatpickr, 'altFormat');
            delivery.closest('.diten-field')?.querySelector('.diten-field-icon')?.addEventListener('click', () => delivery._flatpickr?.open());
        }
        accountPicker('po-pharmacy', 'pharmacies', (v) => { S.pharmacy = v; changed(); loadPrevious(); });
        accountPicker('po-depot', 'depots', (v) => { S.depot = v; changed(); });
        byId('po-note').addEventListener('input', () => changed());
        byId('po-lines').addEventListener('input', onLineInput);
        byId('po-lines').addEventListener('click', (e) => {
            const b = e.target.closest && e.target.closest('.po-remove');
            if (!b) return;
            S.lines = S.lines.filter((l) => l.key !== b.getAttribute('data-key'));
            drawLines(); changed();
        });
        byId('po-add-line').addEventListener('click', () => { S.lines.push({ key: newKey(), skuId: null, skuText: '', quantity: '', mf: '', discount: '' }); drawLines(); drawSend(); });
        byId('po-save').addEventListener('click', () => save(false));
        byId('po-discard').addEventListener('click', discard);
        byId('po-send').addEventListener('click', send);
        byId('po-sign-save').addEventListener('click', sign);
        byId('po-resign').addEventListener('click', () => { byId('po-sign-pad').classList.remove('d-none'); byId('po-resign').classList.add('d-none'); });
        byId('po-lock-action').addEventListener('click', wayOut);
        initPad();

        if (orderId) {
            const r = await U.request('GET', endpoint + '/orders/' + encodeURIComponent(orderId));
            const order = U.data(r);
            if (!order) { alertBox(U.errorText(r)); return; }
            fill(order);
            loadPrevious();
        } else {
            S.lines.push({ key: newKey(), skuId: null, skuText: '', quantity: '', mf: '', discount: '' });
            drawAll();
        }
        schedulePreview();
    };

    return { init: init };
})();

document.addEventListener('DOMContentLoaded', () => PreOrderForm.init());
