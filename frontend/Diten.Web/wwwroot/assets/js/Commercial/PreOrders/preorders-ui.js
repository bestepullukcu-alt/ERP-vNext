/**
 * WP-VW-W6 · W6-WEB-a — the pre-order screens' shared drawing (B quick view, A, Details, C, C-link, E): the request helper,
 * the badges, the facts, the lines, the totals with the local equivalent, the timeline and the reason dialog. Every word is
 * window.L10n (the page resource + the shared rule messages); every data text is escaped and isolated (<bdi>) for RTL;
 * dates and numbers are in the application's culture (<html lang>). Amounts are the CRM's (MOD-0156) — drawn, never computed.
 */
(function (root) {
    'use strict';

    const C = root.PreOrdersCore;
    const R = () => root.CommercialRules;
    const L = () => root.L10n || {};
    const culture = () => ((root.document && root.document.documentElement && root.document.documentElement.lang) || '').trim() || 'en';
    const dateCulture = () => (/^en(-|$)/i.test(culture()) ? 'en-GB' : culture());
    const esc = (s) => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const bidi = (s) => '<bdi>' + esc(s) + '</bdi>';
    const fmt = (t, args) => (args || []).reduce((x, v, i) => x.split('{' + i + '}').join(String(v)), String(t || ''));
    const T = (key, ...args) => fmt(L()[key] || '', args);
    /** A template whose {n} slots are DATA: each escaped + isolated, the words around them escaped. */
    const Th = (key, ...args) => String(L()[key] || '').split(/(\{\d+\})/).map((p) => {
        const m = /^\{(\d+)\}$/.exec(p);
        return m ? bidi(args[Number(m[1])] == null ? '' : args[Number(m[1])]) : esc(p);
    }).join('');
    const dtf = (o) => { try { return new Intl.DateTimeFormat(dateCulture(), o); } catch (e) { return new Intl.DateTimeFormat('en-GB', o); } };
    const day = (v) => {
        if (!v) return '';
        const d = /^\d{4}-\d{2}-\d{2}$/.test(String(v)) ? new Date(v + 'T00:00:00Z') : new Date(v);
        return isNaN(d) ? '' : dtf({ day: 'numeric', month: 'short', year: 'numeric', timeZone: /^\d{4}-\d{2}-\d{2}$/.test(String(v)) ? 'UTC' : undefined }).format(d);
    };
    const moment = (v) => { const d = new Date(v); return v && !isNaN(d) ? dtf({ day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(d) : ''; };
    const money = (a, c) => C.money(a, c, culture());
    const pct = (v) => C.pct(v, culture());

    const headers = (json) => {
        const h = (root.DitenDataTable && root.DitenDataTable.getAuthHeaders && root.DitenDataTable.getAuthHeaders(json)) || {};
        if (json && !h['Content-Type']) h['Content-Type'] = 'application/json';
        return h;
    };
    /** fetch → { ok, status, body } (never throws; a network failure is status 0). */
    const request = (method, url, body) => root.fetch(url, {
        method: method, credentials: 'same-origin', headers: headers(body !== undefined),
        body: body === undefined ? undefined : JSON.stringify(body)
    }).then((r) => r.text().then((t) => { let b = null; try { b = t ? JSON.parse(t) : null; } catch (e) { b = null; } return { ok: r.ok, status: r.status, body: b }; }))
        .catch(() => ({ ok: false, status: 0, body: null }));
    const data = (r) => (r && r.ok && r.body ? (r.body.data !== undefined ? r.body.data : r.body) : null);

    /** The words of a failed answer: the rule source's message for its code, else the page's generic words by status. */
    const errorText = (r) => {
        const code = C.errorCode(r && r.body);
        const own = code && R() ? R().error(code) : null;
        if (own) return own;
        if (r && r.status === 403) return L().Err_permission_denied || '';
        if (r && r.status === 404) return L().Err_not_found || '';
        if (r && (r.status === 0 || r.status === 502 || r.status === 503)) return L().Err_unavailable || '';
        return L().Err_generic || '';
    };

    const statusBadge = (status) => {
        const s = C.statusOf(status);
        return '<span class="badge bg-label-' + s.tone + ' d-inline-flex align-items-center gap-1 text-nowrap"><i class="bx ' + s.icon + '"></i>' + esc(L()[s.key] || status || '') + '</span>';
    };
    const outcomeBadge = (outcome) => {
        const o = C.outcomeOf(outcome);
        return '<span class="badge bg-label-' + o.tone + ' d-inline-flex align-items-center gap-1 text-nowrap po-outcome po-outcome-' + esc(outcome || 'ok') + '"><i class="bx ' + o.icon + '"></i>' + esc(R() ? R().outcome(outcome || 'ok') : '') + '</span>';
    };
    const depotResponseBadge = (status) => (status ? '<span class="badge bg-label-info text-nowrap">' + esc(L()['DR_' + status] || status) + '</span>' : '');

    /** A finding in words (the shared rule source; {skuId} named by the line's product). */
    const findingHtml = (f, skuName) => '<span class="po-msg po-msg-' + esc(f.severity || '') + '">' + (R() ? R().html(f, { skuName: () => skuName, culture: culture() }) : esc(f.code)) + '</span>';

    const facts = (order) => {
        const o = order || {};
        const rows = [
            ['FactCode', o.code], ['FactDate', day(o.createdAt)], ['FactPharmacy', (o.pharmacy || {}).name], ['FactDepot', (o.depot || {}).name],
            ['FactRep', (o.rep || {}).displayName], ['FactDelivery', day(o.requestedDeliveryDate)], ['FactSeller', (o.seller || {}).name],
            ['FactMode', o.priceMode === 'priced' ? T('ModePriced', o.currency || '') : T('ModeQuantity')]
        ].filter((r) => r[1]);
        return '<dl class="po-facts row g-2 mb-0">' + rows.map((r) => '<div class="col-6 col-md-3"><dt class="small text-muted fw-normal">' + esc(L()[r[0]] || '') + '</dt><dd class="mb-0 text-heading">' + bidi(r[1]) + '</dd></div>').join('') + '</dl>';
    };

    /** The order's lines: product (name · code · barcode), quantity, MF (+ free boxes), and in priced mode discount, unit price, VAT, line total; the outcome + messages. */
    const linesTable = (order, opts) => {
        const o = order || {};
        const priced = o.priceMode === 'priced';
        const showRules = !(opts && opts.noRules);
        const head = ['ColProduct', 'ColQty', 'ColMf'].concat(priced ? ['ColDiscount', 'ColUnitPrice', 'ColVat', 'ColLineTotal'] : []).concat(showRules ? ['ColRule'] : []);
        const rows = (o.lines || []).map((l) => {
            const sku = l.sku || {};
            const meta = [sku.code, sku.gtin ? T('GtinShort', sku.gtin) : ''].filter(Boolean).join(' · ');
            const cells = [
                '<div class="text-heading">' + bidi(sku.name || sku.code || '') + '</div>' + (meta ? '<small class="text-muted">' + bidi(meta) + '</small>' : ''),
                esc(C.num(l.quantity, culture(), 0)),
                l.mf ? bidi(C.mfText(l.mf)) + (l.freeQuantity ? '<small class="text-muted d-block">' + esc(T('FreeBoxes', C.num(l.freeQuantity, culture(), 0))) + '</small>' : '') : '—'
            ];
            if (priced) cells.push(l.discountPct == null ? '—' : esc(pct(l.discountPct)), esc(money(l.unitPrice, o.currency)), l.vatRate == null ? '—' : esc(pct(l.vatRate)), '<span class="text-heading">' + esc(money(l.netAmount, o.currency)) + '</span>');
            if (showRules) {
                const msgs = (l.findings || []).map((f) => findingHtml(f, sku.name || sku.code)).join('');
                cells.push(outcomeBadge(l.outcome) + (msgs ? '<div class="po-msgs">' + msgs + '</div>' : ''));
            }
            return '<tr>' + cells.map((c) => '<td>' + c + '</td>').join('') + '</tr>';
        }).join('');
        return '<div class="table-responsive"><table class="table table-sm align-middle po-lines mb-0"><thead><tr>' + head.map((h) => '<th>' + esc(L()[h] || '') + '</th>').join('') + '</tr></thead><tbody>' + rows + '</tbody></table></div>';
    };

    /** Net · VAT · total · effective discount · the local equivalent (only with an FX snapshot) — or the quantity-mode note. */
    const totals = (order, opts) => {
        const o = order || {};
        if (o.priceMode !== 'priced' || !o.totals) return '<div class="alert alert-info d-flex gap-2 align-items-start mb-0 py-2"><i class="bx bx-info-circle mt-1"></i><span>' + esc(L().QuantityModeNote || '') + '</span></div>';
        const t = o.totals;
        const rows = [['TotalNet', money(t.net, o.currency)], ['TotalVat', money(t.vat, o.currency)]];
        if (!(opts && opts.hideEffective)) rows.push(['TotalEffective', pct(t.effectiveDiscountPct)]);
        const fx = C.fxArgs(o.fx, culture(), day);
        return '<div class="po-totals">' + rows.map((r) => '<div class="d-flex justify-content-between"><span class="text-muted">' + esc(L()[r[0]] || '') + '</span><span>' + esc(r[1]) + '</span></div>').join('')
            + '<div class="d-flex justify-content-between fw-semibold text-heading border-top pt-1 mt-1"><span>' + esc(L().TotalGross || '') + '</span><span>' + esc(money(t.gross, o.currency)) + '</span></div>'
            + (fx ? '<small class="text-muted d-block mt-1">' + Th('FxLine', ...fx) + '</small>' : '') + '</div>';
    };

    const timelineHtml = (order) => {
        const items = C.timeline(order);
        if (!items.length) return '<p class="text-muted small mb-0">' + esc(L().TimelineEmpty || '') + '</p>';
        return '<ul class="po-timeline list-unstyled mb-0">' + items.map((e) => '<li class="d-flex gap-2"><span class="po-tl-dot bg-label-' + e.tone + '"><i class="bx ' + e.icon + '"></i></span><div><div class="text-heading">' + esc(L()[e.key] || e.type) + '</div><small class="text-muted">' + esc(moment(e.at)) + '</small>'
            + (e.reason ? '<small class="d-block">' + Th('ReasonLine', e.reason) + '</small>' : '')
            + (e.code && R() && R().error(e.code) ? '<small class="d-block text-muted">' + esc(R().error(e.code)) + '</small>' : '') + '</div></li>').join('') + '</ul>';
    };

    /** The transmission trail (screen E): each attempt with its outcome; the last status. */
    const trailHtml = (order) => {
        const tr = (order && order.transmission) || null;
        if (!tr || !(tr.attempts || []).length) return '<span class="text-muted small">' + esc(L().TrailNone || '') + '</span>';
        return tr.attempts.map((a, i) => '<span class="badge bg-label-' + (a.status === 'sent' ? 'success' : 'danger') + ' d-inline-flex align-items-center gap-1 me-1 mb-1"><i class="bx ' + (a.status === 'sent' ? 'bx-send' : 'bx-error') + '"></i>'
            + esc(T('TrailAttempt', i + 1, moment(a.at), L()['Trail_' + a.status] || a.status)) + '</span>').join('');
    };

    /**
     * The reason dialog (reject / request changes / cancel — the reason is REQUIRED, D5): resolves the typed reason, or
     * null when dismissed. The confirm button is disabled until a reason is typed; Ctrl / ⌘ + Enter confirms (a
     * consequential act, UXH-001 chord) — a plain Enter is a new line of the textarea.
     */
    const askReason = (title, text, button) => new Promise((resolve) => {
        const el = root.document.getElementById('po-reason-modal');
        if (!el || !root.bootstrap) { resolve(null); return; }
        const area = el.querySelector('#po-reason-text');
        const ok = el.querySelector('#po-reason-ok');
        el.querySelector('#po-reason-title').textContent = title;
        el.querySelector('#po-reason-body').textContent = text;
        ok.textContent = button;
        area.value = '';
        ok.disabled = true;
        let done = false;
        const finish = (v) => { if (done) return; done = true; area.removeEventListener('input', onInput); ok.removeEventListener('click', onOk); area.removeEventListener('keydown', onKey); el.removeEventListener('hidden.bs.modal', onHide); modal.hide(); resolve(v); };
        const onInput = () => { ok.disabled = !area.value.trim(); };
        const onOk = () => { if (area.value.trim()) finish(area.value.trim()); };
        const onKey = (e) => { if (e.key === 'Enter' && (e.ctrlKey || e.metaKey) && area.value.trim()) { e.preventDefault(); onOk(); } };
        const onHide = () => finish(null);
        area.addEventListener('input', onInput); ok.addEventListener('click', onOk); area.addEventListener('keydown', onKey); el.addEventListener('hidden.bs.modal', onHide);
        const modal = root.bootstrap.Modal.getOrCreateInstance(el);
        modal.show();
        setTimeout(() => area.focus(), 200);
    });

    /**
     * Screen A — the INPUTS of one line: product, quantity, MF and, in priced mode only, the discount. In quantity mode no
     * discount and no price field exists at all (AC-PRICE-3: the CRM refuses a discount there anyway). The price is never an
     * input: it is drawn by lineResult from the CRM's answer, behind a lock.
     */
    const formLineHtml = (line, priced, locked) => {
        const k = esc(line.key);
        const dis = locked ? ' disabled' : '';
        const sku = '<div class="col-12 ' + (priced ? 'col-md-5' : 'col-md-6') + '"><label class="form-label small" for="po-sku-' + k + '">' + esc(L().ColProduct || '') + '</label>'
            + '<div class="diten-field"><i class="bx bx-capsule diten-field-icon" aria-hidden="true"></i><select class="form-select po-sku" id="po-sku-' + k + '" data-key="' + k + '"' + dis + '>'
            + (line.skuId ? '<option value="' + esc(line.skuId) + '" selected>' + esc(line.skuText || line.skuId) + '</option>' : '<option value=""></option>') + '</select></div></div>';
        const qty = '<div class="col-4 ' + (priced ? 'col-md-2' : 'col-md-3') + '"><label class="form-label small" for="po-qty-' + k + '">' + esc(L().ColQty || '') + '</label>'
            + '<div class="diten-field"><i class="bx bx-package diten-field-icon" aria-hidden="true"></i><input type="number" inputmode="numeric" min="1" step="1" class="form-control po-qty" id="po-qty-' + k + '" data-key="' + k + '" value="' + esc(line.quantity == null ? '' : line.quantity) + '"' + dis + ' /></div></div>';
        const mf = '<div class="col-4 ' + (priced ? 'col-md-2' : 'col-md-2') + '"><label class="form-label small" for="po-mf-' + k + '">' + esc(L().ColMf || '') + '</label>'
            + '<div class="diten-field"><i class="bx bx-gift diten-field-icon" aria-hidden="true"></i><input type="text" inputmode="numeric" class="form-control po-mf" id="po-mf-' + k + '" data-key="' + k + '" placeholder="10+2" value="' + esc(line.mf || '') + '"' + dis + ' /></div></div>';
        const discount = priced
            ? '<div class="col-4 col-md-2"><label class="form-label small" for="po-disc-' + k + '">' + esc(L().ColDiscount || '') + '</label>'
                + '<div class="diten-field"><i class="bx bx-purchase-tag-alt diten-field-icon" aria-hidden="true"></i><input type="text" inputmode="decimal" class="form-control po-disc" id="po-disc-' + k + '" data-key="' + k + '" value="' + esc(line.discount == null ? '' : line.discount) + '"' + dis + ' /></div></div>'
            : '';
        const remove = locked ? '' : '<div class="col-12 col-md-1 d-flex align-items-end justify-content-end"><button type="button" class="btn btn-sm btn-icon btn-label-danger po-remove" data-key="' + k + '" aria-label="' + esc(L().RemoveLine || '') + '" title="' + esc(L().RemoveLine || '') + '"><i class="bx bx-trash"></i></button></div>';
        return '<div class="po-line border rounded p-3" data-key="' + k + '"><div class="row g-2">' + sku + qty + mf + discount + remove + '</div><div class="po-line-result mt-2" id="po-res-' + k + '"></div></div>';
    };

    /**
     * Screen A — what the CRM answered for one line (the preview / the order): in priced mode the locked unit price and the
     * line total; the free boxes and the combined discount; the price source; the outcome badge and the rule messages.
     */
    const lineResult = (result, ctx) => {
        if (!result) return '';
        const c = ctx || {};
        const parts = [];
        if (c.priced && result.unitPrice != null) {
            parts.push('<div class="d-flex flex-wrap justify-content-between gap-2"><span class="text-muted"><i class="bx bx-lock-alt me-1"></i>' + esc(T('PerBox', money(result.unitPrice, c.currency))) + '</span><strong class="text-heading">' + esc(money(result.netAmount, c.currency)) + '</strong></div>');
        }
        const meta = [];
        if (result.freeQuantity) meta.push(T('FreeBoxes', C.num(result.freeQuantity, culture(), 0)));
        if (c.priced && result.combinedDiscountPct != null) meta.push(T('CombinedLine', pct(result.combinedDiscountPct)));
        else if (result.mfRatioPct != null && result.freeQuantity) meta.push(T('MfRatioLine', pct(result.mfRatioPct)));
        if (meta.length) parts.push('<small class="text-muted d-block">' + esc(meta.join(' · ')) + '</small>');
        if (c.priceSource) parts.push('<small class="text-muted d-block"><i class="bx bx-purchase-tag-alt me-1"></i>' + c.priceSource + '</small>');
        const msgs = (result.findings || []).map((f) => findingHtml(f, c.skuName)).join('');
        parts.push('<div class="d-flex flex-wrap align-items-start gap-2 mt-1">' + outcomeBadge(result.outcome) + (msgs ? '<div class="po-msgs">' + msgs + '</div>' : '') + '</div>');
        return parts.join('');
    };

    /** Signature · approval (or "approved automatically") · transmission · cancellation · the depot's answer. */
    const stateHtml = (order) => {
        const o = order || {};
        const rows = [];
        const row = (icon, html) => rows.push('<li class="d-flex gap-2 mb-2"><i class="bx ' + icon + ' mt-1"></i><div>' + html + '</div></li>');
        if (o.signature) row('bx-pen', Th('SignedBy', o.signature.signerName, moment(o.signature.signedAt)));
        else row('bx-pen', '<span class="text-muted">' + esc(L().SignatureNone || '') + '</span>');
        const a = o.approval;
        if (a && a.auto) row('bx-check-double', esc(L().AutoApproved || ''));
        else if (a && a.decision) {
            const names = (o.approvalRequirement && o.approvalRequirement.approverNames) || {};
            const by = names[a.decidedByUserId] || '';
            row(a.decision === 'approve' ? 'bx-check' : 'bx-x-circle', Th('Decision_' + String(a.decision).replace(/-/g, '_'), by, moment(a.decidedAt)) + (a.reason ? '<small class="d-block">' + Th('ReasonLine', a.reason) + '</small>' : ''));
        } else if (o.status === 'pending_approval') {
            const names = Object.values((o.approvalRequirement && o.approvalRequirement.approverNames) || {}).filter(Boolean).join(', ');
            row('bx-time-five', names ? Th('AwaitingApprover', names) : esc(L().AwaitingApproverUnknown || ''));
        }
        const t = o.transmission;
        if (t && t.transmittedAt) row('bx-send', Th('TransmittedAt', (o.depot || {}).name || '', moment(t.transmittedAt)));
        else if (t && t.lastStatus === 'failed') row('bx-error text-danger', esc(L().TransmissionFailedLine || ''));
        if (o.cancellation) row('bx-block', Th('CancelledLine', moment(o.cancellation.at)) + '<small class="d-block">' + Th('ReasonLine', o.cancellation.reason) + '</small>');
        if (o.depotResponse) {
            const lines = (o.depotResponse.partialLines || []).map((p) => {
                const l = (o.lines || []).find((x) => x.lineNo === p.lineNo) || {};
                return '<small class="d-block">' + Th('PartialLine', (l.sku || {}).name || '', C.num(p.deliveredQuantity, culture(), 0), C.num(l.quantity, culture(), 0)) + '</small>';
            }).join('');
            row('bx-package', depotResponseBadge(o.depotResponse.status) + lines + (o.depotResponse.note ? '<small class="d-block">' + bidi(o.depotResponse.note) + '</small>' : ''));
        }
        if (o.note) row('bx-note', Th('NoteLine', o.note));
        return '<ul class="list-unstyled mb-0 po-state">' + rows.join('') + '</ul>';
    };

    /** Screen C — "Neden onayımda?": the tenant setting and / or each line over a soft limit with its messages. */
    const whyHtml = (order) => {
        const why = C.whyOnMe(order);
        if (!why.length) return '<p class="text-muted small mb-0">' + esc(L().WhyMeNone || '') + '</p>';
        return why.map((w) => '<div class="d-flex gap-2 mb-2"><i class="bx ' + w.icon + ' mt-1"></i><div><div class="text-heading">' + esc(L()[w.key] || '') + '</div>'
            + w.lines.map((l) => '<small class="d-block"><strong>' + bidi(l.name) + '</strong> · ' + l.findings.map((f) => (R() ? R().html(f, { skuName: () => l.name, culture: culture() }) : esc(f.code))).join(' · ') + '</small>').join('')
            + '</div></div>').join('');
    };

    /**
     * Screen C — every line's ENTERED value with the LIMIT under it (from the findings' parameters: the CRM's evaluation),
     * the line total in priced mode and the outcome. A limit appears only where a rule spoke; nothing is computed.
     */
    const limitsTable = (order) => {
        const o = order || {};
        const priced = o.priceMode === 'priced';
        const head = ['ColProduct', 'ColQty', 'ColMf'].concat(priced ? ['ColDiscount', 'ColCombined', 'ColLineTotal'] : []).concat(['ColRule']);
        const cell = (value, limit) => '<div class="text-heading">' + (value === '' || value == null ? '—' : esc(value)) + '</div>' + (limit ? '<small class="po-limit">' + esc(limit) + '</small>' : '');
        const rows = (o.lines || []).map((l) => {
            const lim = C.limitCells(l);
            const sku = l.sku || {};
            const limitText = (c, asPct) => (c ? T(c.severity === 'hard' ? 'LimitHard' : 'LimitSoft', asPct ? pct(c.limitPct != null ? c.limitPct : c.limit) : c.limit) : '');
            const cells = [
                '<div class="text-heading">' + bidi(sku.name || sku.code || '') + '</div>' + (sku.gtin ? '<small class="text-muted">' + esc(T('GtinShort', sku.gtin)) + '</small>' : ''),
                cell(C.num(l.quantity, culture(), 0), limitText(lim.quantity, false)),
                cell(l.mf ? C.mfText(l.mf) + (l.mfRatioPct != null ? ' (' + pct(l.mfRatioPct) + ')' : '') : '', lim.mf ? T(lim.mf.severity === 'hard' ? 'LimitHard' : 'LimitSoft', lim.mf.limit + (lim.mf.limitPct != null ? ' (' + pct(lim.mf.limitPct) + ')' : '')) : '')
            ];
            if (priced) cells.push(cell(l.discountPct == null ? '' : pct(l.discountPct), limitText(lim.discount, true)), cell(l.combinedDiscountPct == null ? '' : pct(l.combinedDiscountPct), limitText(lim.combined, true)), '<span class="text-heading">' + esc(money(l.netAmount, o.currency)) + '</span>');
            cells.push(outcomeBadge(l.outcome));
            return '<tr>' + cells.map((c) => '<td>' + c + '</td>').join('') + '</tr>';
        }).join('');
        return '<div class="table-responsive"><table class="table table-sm align-middle po-lines mb-0"><thead><tr>' + head.map((h) => '<th>' + esc(L()[h] || '') + '</th>').join('') + '</tr></thead><tbody>' + rows + '</tbody></table></div>';
    };

    /** Screen C — the pharmacy's six months: a bar per month (orders) and the table (orders · boxes · total PER currency). */
    const historyHtml = (buckets) => {
        const max = Math.max(1, ...buckets.map((b) => b.orders));
        const month = (m) => { try { return new Intl.DateTimeFormat(dateCulture(), { month: 'short', year: '2-digit', timeZone: 'UTC' }).format(new Date(m + '-01T00:00:00Z')); } catch (e) { return m; } };
        const totalText = (b) => Object.keys(b.totals).sort().map((c) => money(b.totals[c], c)).join(' · ') || '—';
        const bars = '<div class="po-bars d-flex align-items-end gap-2 mb-3" role="img" aria-label="' + esc(L().HistoryTitle || '') + '">' + buckets.map((b) => '<div class="po-bar-col flex-fill text-center"><small class="d-block text-muted">' + esc(C.num(b.orders, culture(), 0)) + '</small><div class="po-bar" style="--po-bar:' + Math.round((b.orders / max) * 100) + '%"></div><small class="d-block text-muted">' + esc(month(b.month)) + '</small></div>').join('') + '</div>';
        const table = '<div class="table-responsive"><table class="table table-sm mb-0"><thead><tr><th>' + esc(L().ColMonth || '') + '</th><th>' + esc(L().ColOrders || '') + '</th><th>' + esc(L().ColBoxes || '') + '</th><th>' + esc(L().ColTotal || '') + '</th></tr></thead><tbody>'
            + buckets.map((b) => '<tr><td>' + esc(month(b.month)) + '</td><td>' + esc(C.num(b.orders, culture(), 0)) + '</td><td>' + esc(C.num(b.quantity, culture(), 0)) + '</td><td>' + esc(totalText(b)) + '</td></tr>').join('') + '</tbody></table></div>';
        return bars + table;
    };

    const toast = (text, kind) => { if (root.showToast) root.showToast(text, kind || 'info'); };

    const api = {
        esc: esc, bidi: bidi, T: T, Th: Th, day: day, moment: moment, money: money, pct: pct, culture: culture, request: request, data: data,
        errorText: errorText, statusBadge: statusBadge, outcomeBadge: outcomeBadge, depotResponseBadge: depotResponseBadge, findingHtml: findingHtml,
        facts: facts, linesTable: linesTable, totals: totals, timelineHtml: timelineHtml, trailHtml: trailHtml, askReason: askReason, toast: toast,
        formLineHtml: formLineHtml, lineResult: lineResult, stateHtml: stateHtml, whyHtml: whyHtml, limitsTable: limitsTable, historyHtml: historyHtml
    };
    root.PreOrdersUi = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof window !== 'undefined' ? window : globalThis);
