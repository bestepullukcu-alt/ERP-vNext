/**
 * MOD-0177 R7a — Vaka Detayı (pack §11.1, §11.3, §8.3–§8.8; mockup "isCase"). Reads: ONE door /Recall/data/cases/{id}?part=… (case, scope, diff,
 * stock, customers, returns, effectiveness, timeline). Commands: each through its own door /Recall/cmd/cases/{id}/… (antiforgery header,
 * Idempotency-Key = the body's key, If-Match = the case version where the record is versioned). Which button shows is RecallCaseView.offers
 * (key + status + four-eyes); the signed decisions are RecallCaseDecision (prepare → MOD-0022 dialog → command). After every write the case
 * and the open tab are read again — the server's answer is the truth. No movement is ever posted from here (D-3, D-27).
 */
'use strict';

(function () {
    if (typeof document === 'undefined' || !window.RecallUi || !window.RecallCaseView || !window.RecallCaseDecision) return;
    const ui = window.RecallUi;
    const view = window.RecallCaseView;
    const decision = window.RecallCaseDecision;
    const { t, esc, byId } = ui;
    const root = byId('rc-case');
    if (!root) return;
    const caseId = root.dataset.caseId;
    const me = root.dataset.userId;
    const state = { detail: null, offers: {}, loaded: {}, data: {}, selected: new Set(), diff: null, dialog: null, pendingReason: null, row: null };

    // ── calls ─────────────────────────────────────────────────────────────────────────────────────────────────────
    const antiforgery = () => document.querySelector('#rc-antiforgery input[name="__RequestVerificationToken"]')?.value || '';
    const write = async (path, body, options = {}) => {
        const headers = { Accept: 'application/json', 'Content-Type': 'application/json', RequestVerificationToken: antiforgery() };
        if (options.ifMatch) headers['If-Match'] = options.ifMatch;
        try {
            const res = await fetch(path, { method: 'POST', credentials: 'same-origin', headers, body: JSON.stringify(body) });
            const json = await res.json().catch(() => null);
            return { ok: res.ok, status: res.status, json };
        } catch (error) {
            return { ok: false, status: 0, json: null };
        }
    };
    const deps = {
        write,
        newKey: () => ui.newKey().replace(/^r6-/, 'r7-'),
        sign: (subject) => (window.ESign?.open ? window.ESign.open(subject) : Promise.resolve(null))
    };
    const read = async (part, query = '') => {
        const reply = await ui.get(`/Recall/data/cases/${encodeURIComponent(caseId)}${part ? `?part=${part}${query}` : ''}`);
        if (reply.ok && reply.json?.labelsAvailable === false) byId('rc-labels-unavailable')?.classList.remove('d-none');
        return reply;
    };
    const sentence = (reply, kind) => {
        const code = reply?.json?.error?.code;
        const key = code ? view.errorKey(code, kind) : null;
        return key && ui.has(key) ? t(key) : ui.errorText(reply);
    };

    // ── formatting ───────────────────────────────────────────────────────────────────────────────────────────────
    const qty = (n, unit) => `${ui.qty(n)}${unit ? ' ' + esc(unit) : ''}`;
    const person = (u) => esc(u?.name || u?.userId || '');
    const when = (wire) => {
        if (!wire) return '';
        const d = new Date(wire);
        return Number.isNaN(d.getTime()) ? esc(wire) : esc(d.toLocaleString(document.documentElement.lang || undefined, { dateStyle: 'short', timeStyle: 'short' }));
    };
    const product = (o) => esc(o?.itemLabel || o?.item?.itemId || '');
    const show = (el, on) => el?.classList.toggle('d-none', !on);
    const action = (name) => document.querySelector(`[data-rc-action="${name}"]`);
    const modal = (id) => window.bootstrap.Modal.getOrCreateInstance(byId(id));
    const toast = (text) => window.showToast?.(text, 'success');
    const fail = (box, reply, kind) => { if (box) { box.textContent = sentence(reply, kind); box.classList.remove('d-none'); } };

    // ── the case head, steps, buttons ─────────────────────────────────────────────────────────────────────────────
    const drawHead = (c) => {
        byId('rc-h-number').textContent = c.number;
        document.querySelector('.breadcrumb .active')?.replaceChildren(document.createTextNode(t('Recall.Case.Crumb', c.number)));
        byId('rc-h-badges').innerHTML = `${ui.typeBadge('Case')} ${ui.classBadge(c.class)} <span class="badge bg-label-secondary">${esc(t('Recall.Level.' + c.level) || c.level)}</span> ${ui.statusBadge(c.status)}`;
        byId('rc-h-product').textContent = c.itemLabel || c.item?.itemId || '';
        byId('rc-h-lot').textContent = c.rootLot?.lotNumber || '';
        const enterReturn = byId('rc-enter-return'); // R7c: drawn only for a holder of inventory.movements.create (MOD-0173's key)
        if (enterReturn) {
            const href = view.customerReturnHref(c);
            enterReturn.classList.toggle('d-none', !href);
            if (href) enterReturn.setAttribute('href', href);
        }
        byId('rc-h-reason').textContent = [c.reason, c.reasonReference].filter(Boolean).join(' · ');
        byId('rc-h-opened').innerHTML = `${person(c.owner)} · ${when(c.openedAtUtc)}`;
        const final = byId('rc-h-final');
        if (c.closure) {
            final.innerHTML = esc(t('Recall.Case.ClosedInfo', c.closure.closedBy?.name || c.closure.closedBy?.userId, ui.date(c.closure.closedBy?.atUtc)))
                + (c.closure.signatureId ? ` · <span class="font-monospace">${esc(t('Recall.Case.Signature', c.closure.signatureId))}</span>` : '');
        } else if (c.cancellation) {
            final.textContent = t('Recall.Case.CancelledInfo', c.cancellation.cancelledBy?.name || c.cancellation.cancelledBy?.userId, c.cancellation.reason);
        }
        show(final, !!(c.closure || c.cancellation));

        const current = view.step(c.status);
        document.querySelectorAll('#rc-steps [data-step]').forEach((li, i) => {
            const mark = li.querySelector('[data-step-mark]');
            mark.className = `badge rounded-pill ${i < current ? 'bg-success' : i === current ? 'bg-primary' : 'bg-label-secondary'}`;
            mark.innerHTML = i < current ? '<i class="bx bx-check"></i>' : String(i + 1);
            li.classList.toggle('fw-semibold', i === current);
        });

        const o = state.offers;
        show(byId('rc-sod-note'), o.approveSod || o.closeSod);
        byId('rc-sod-note').textContent = o.approveSod ? t('Recall.Case.YouSubmitted') : o.closeSod ? t('Recall.Case.YouCompleted') : '';
        const rev = c.pendingRevision;
        show(byId('rc-revision-note'), !!rev);
        if (rev) byId('rc-revision-note').textContent = t('Recall.Case.PendingRevision', rev.revision, (rev.addedNodeKeys || []).length);
        const buttons = { cancel: o.cancel, close: o.close, rescan: o.rescan, 'save-marks': o.mark, submit: o.submit, return: o.returnScope, approve: o.approve,
            'eff-start': o.effStart, 'eff-complete': o.effComplete };
        Object.entries(buttons).forEach(([name, on]) => show(action(name), !!on));
        // four-eyes (CT-R1): for the excluded user the button is not hidden but REMOVED from the page — it is not drawn at all
        if (o.approveSod) action('approve')?.remove();
        if (o.closeSod) action('close')?.remove();
    };

    const loadCase = async () => {
        const reply = await read('');
        if (!reply.ok) { ui.showError(byId('rc-error'), reply.status === 404 ? t('Recall.Case.NotFound') : sentence(reply)); return false; }
        state.detail = reply.json.data;
        state.offers = view.offers(state.detail, ui.can, me);
        drawHead(state.detail);
        return true;
    };

    // ── tabs ─────────────────────────────────────────────────────────────────────────────────────────────────────
    const drawScope = (scope) => {
        const items = scope.items || [];
        byId('rc-scope-summary').innerHTML = esc(t('Recall.Scope.Summary', items.length, scope.excludedCount, scope.revision))
            + (scope.sha256 ? ` · <span class="font-monospace" title="${esc(scope.sha256)}">${esc(t('Recall.Scope.Seal', scope.sha256.slice(0, 12)))}</span>` : '');
        byId('rc-scope-hint').textContent = scope.editable ? t('Recall.Scope.EditHint')
            : state.detail?.approvedScope ? t('Recall.Scope.LockedHint') : t('Recall.Scope.PendingHint');
        const editable = scope.editable && state.offers.mark;
        const rows = view.treeOrder(items).map(({ item: i, depth }) => {
            const place = i.kind === 'Shipment' ? esc(i.customerRef || '') : esc([i.warehouseLabel, i.locationLabel].filter(Boolean).join(' · '));
            const quantity = i.quantity == null ? '' : qty(i.quantity, i.baseUomId);
            const markable = editable && !i.isRoot; // the root stays; excluding an item excludes its subtree (CT-R6, the server's rule)
            const markCell = i.isRoot
                ? `<span class="badge bg-label-primary">${esc(t('Recall.Scope.Root'))}</span>`
                : markable
                    ? `<div class="d-flex flex-column gap-1"><div class="form-check form-switch mb-0"><input class="form-check-input" type="checkbox" data-mark="${esc(i.nodeKey)}" ${i.included ? 'checked' : ''}>`
                      + `<label class="form-check-label small">${esc(t(i.included ? 'Recall.Scope.Included' : 'Recall.Scope.Excluded'))}</label></div>`
                      + `<input type="text" class="form-control form-control-sm ${i.included ? 'd-none' : ''}" data-reason="${esc(i.nodeKey)}" maxlength="1000" placeholder="${esc(t('Recall.Scope.ExclusionReason'))}" value="${esc(i.exclusionReason || '')}"></div>`
                    : `<span class="badge ${i.effectivelyIncluded ? 'bg-label-success' : 'bg-label-secondary'}">${esc(t(i.included ? (i.effectivelyIncluded ? 'Recall.Scope.Included' : 'Recall.Scope.ExcludedByParent') : 'Recall.Scope.Excluded'))}</span>`
                      + (i.exclusionReason ? `<div class="small text-muted">${esc(i.exclusionReason)}</div>` : '');
            return `<tr class="${i.effectivelyIncluded ? '' : 'opacity-75'}"><td style="padding-inline-start:${0.75 + depth * 1.25}rem">${depth > 0 ? '<i class="bx bx-subdirectory-right text-muted me-1"></i>' : ''}${esc(t('Recall.Kind.' + i.kind) || i.kind)}${i.new ? ` <span class="badge bg-label-warning">${esc(t('Recall.Scope.New'))}</span>` : ''}</td>`
                + `<td>${ui.productCell(i, i.lot?.lotNumber)}</td><td class="small">${place}</td><td class="text-end">${quantity}</td><td>${markCell}</td></tr>`;
        });
        byId('rc-scope-table').querySelector('tbody').innerHTML = rows.join('') || `<tr><td colspan="5" class="text-muted">${esc(t('Recall.Common.None'))}</td></tr>`;
        byId('rc-scope-table').querySelectorAll('[data-mark]').forEach((box) => box.addEventListener('change', () => {
            const reason = byId('rc-scope-table').querySelector(`[data-reason="${CSS.escape(box.dataset.mark)}"]`);
            show(reason, !box.checked);
            box.nextElementSibling.textContent = t(box.checked ? 'Recall.Scope.Included' : 'Recall.Scope.Excluded');
        }));
    };

    const drawDiff = (diff) => {
        state.diff = diff;
        const added = diff.added || [];
        const removed = diff.removed || [];
        let html = added.length === 0 && removed.length === 0 ? `<p class="small text-success mb-0">${esc(t('Recall.Scope.DiffNone'))}</p>` : '';
        if (added.length > 0) {
            html += `<p class="small mb-1">${esc(t('Recall.Scope.DiffFound', added.length))}</p><ul class="list-unstyled small mb-2">`
                + added.map((n) => `<li class="bg-label-warning rounded px-2 py-1 mb-1">${state.offers.revise ? `<input class="form-check-input me-2" type="checkbox" checked data-add="${esc(n.nodeKey)}">` : ''}`
                    + `${esc(t('Recall.Kind.' + n.kind) || n.kind)} · ${esc(n.lot?.lotNumber || '')} ${n.quantity != null ? '· ' + qty(n.quantity, n.baseUomId) : ''} ${n.customerRef ? '· ' + esc(n.customerRef) : ''}</li>`).join('')
                + '</ul>';
        }
        if (removed.length > 0) html += `<p class="small text-muted mb-0">${esc(t('Recall.Scope.DiffRemoved', removed.length))}</p>`;
        byId('rc-diff-body').innerHTML = html;
        show(byId('rc-diff'), true);
        show(byId('rc-revise-box'), state.offers.revise && added.length > 0);
    };

    const requestCell = (line) => {
        const r = line.request;
        if (!r) return `<span class="badge bg-label-secondary">${esc(t('Recall.Stock.NoRequest'))}</span>`;
        const tone = { Held: 'bg-label-success', PartiallyHeld: 'bg-label-warning', NotNeeded: 'bg-label-info', Pending: 'bg-label-secondary' }[r.status] || 'bg-label-danger';
        const why = r.notNeededReason ? ` · ${esc(t('Recall.NotNeeded.' + r.notNeededReason))}` : '';
        const reserved = (r.reservations || []).map((x) => `${esc(x.holderModule || '')} ${esc(x.sourceDocumentId || '')} ${ui.qty(x.remainingQuantity)}`).join(', ');
        const retry = state.offers.quarantine && view.retryable(line)
            ? ` <button type="button" class="btn btn-xs btn-sm btn-label-warning py-0" data-retry="${esc(r.requestId)}">${esc(t('Recall.Stock.Retry'))}</button>` : '';
        return `<span class="badge ${tone}">${esc(t('Recall.ReqStatus.' + r.status))}</span>${why}${retry}`
            + (reserved ? `<div class="small text-muted">${esc(t('Recall.Stock.Reserved', ''))}${reserved}</div>` : '');
    };

    const drawStock = (lines) => {
        state.selected.clear();
        const body = lines.map((l) => {
            const pick = state.offers.quarantine && view.requestable(l) ? `<input class="form-check-input" type="checkbox" data-pick="${esc(l.nodeKey)}">` : '';
            const holds = (l.request?.holds || []).map((h) => `<a href="/Quarantine/Details/${encodeURIComponent(h.holdId)}" class="font-monospace small">${esc(h.holdNumber)}</a>`).join(' ') || '—';
            const segregated = Number(l.segregatedQuantity) > 0 ? `<span class="badge bg-label-success">${esc(t('Recall.Stock.Segregated', ui.qty(l.segregatedQuantity)))}</span>` : '';
            const segButton = state.offers.segregate && view.segregatable(l) && byId('rc-seg-modal')
                ? ` <button type="button" class="btn btn-sm btn-label-primary py-0" data-segregate="${esc(l.nodeKey)}">${esc(t('Recall.Stock.Segregate'))}</button>` : '';
            return `<tr><td>${pick}</td><td class="small">${esc([l.warehouseLabel, l.locationLabel].filter(Boolean).join(' · '))}</td><td class="font-monospace small">${esc(l.lot?.lotNumber || '')}</td>`
                + `<td class="text-end">${qty(l.quantity, l.baseUomId)}${l.stockStatus ? `<div class="small text-muted">${esc(t('Recall.StockStatus.' + l.stockStatus) || l.stockStatus)}</div>` : ''}</td>`
                + `<td>${requestCell(l)}</td><td>${holds}</td><td>${segregated || '—'}${segButton}</td></tr>`;
        });
        byId('rc-stock-table').querySelector('tbody').innerHTML = body.join('') || `<tr><td colspan="7" class="text-muted">${esc(t('Recall.Stock.Empty'))}</td></tr>`;
        // the mockup's "Toplam 950 adet · D01 650 · D02 300": per SKU + unit (never across units), then per warehouse of the same unit
        const totals = {};
        lines.forEach((l) => {
            const k = `${l.item?.skuId}|${l.baseUomId}`;
            const sum = totals[k] || (totals[k] = { q: 0, u: l.baseUomId, wh: {} });
            sum.q += Number(l.quantity || 0);
            const w = l.warehouseLabel || l.warehouseId || '—';
            sum.wh[w] = (sum.wh[w] || 0) + Number(l.quantity || 0);
        });
        byId('rc-stock-totals').textContent = Object.values(totals).length
            ? t('Recall.Stock.Totals', Object.values(totals).map((x) => `${ui.qty(x.q)} ${x.u}`
                + (Object.keys(x.wh).length > 1 ? ' (' + Object.entries(x.wh).map(([w, q]) => `${w} ${ui.qty(q)}`).join(' · ') + ')' : '')).join(' · '))
            : '';
        const send = action('quarantine-send');
        const sync = () => { if (send) { send.textContent = t('Recall.Stock.SendSelected', state.selected.size); send.disabled = state.selected.size === 0; } };
        show(send, state.offers.quarantine && lines.some(view.requestable));
        sync();
        byId('rc-stock-table').querySelectorAll('[data-pick]').forEach((box) => box.addEventListener('change', () => {
            if (box.checked) state.selected.add(box.dataset.pick); else state.selected.delete(box.dataset.pick);
            sync();
        }));
        byId('rc-stock-table').querySelectorAll('[data-retry]').forEach((b) => b.addEventListener('click', () => run(() => decision.command(deps, caseId, `quarantine/${b.dataset.retry}/retry`, {}), 'stock')));
        byId('rc-stock-table').querySelectorAll('[data-segregate]').forEach((b) => b.addEventListener('click', () => openSegregation(lines.find((l) => l.nodeKey === b.dataset.segregate))));
    };

    const delivery = (s) => {
        if (s.read === 'Ok') return s.actualDeliverAt ? esc(t('Recall.Cust.Delivered', ui.date(s.actualDeliverAt))) : esc(t('Recall.Cust.NotDelivered', s.status || ''));
        return `<span class="text-muted">${esc(t('Recall.Read.' + s.read))}</span>`;
    };

    const drawCustomers = (rows, effectiveness) => {
        state.data.customers = rows;
        const notified = rows.filter((r) => r.notification).length;
        byId('rc-cust-summary').textContent = t('Recall.Cust.Summary', notified, rows.length);
        const body = rows.flatMap((r) => r.shipments.map((s, i) => {
            const eff = view.customerRows(effectiveness, r.customerRef).find((e) => e.skuId === s.item?.skuId && e.baseUomId === s.baseUomId);
            const doc = (s.movements || []).map((m) => `${esc(m.number || m.transactionId)} · ${esc(ui.date(m.postingDate))}`).join('<br>') || esc(s.sourceDocumentId || '');
            const first = i === 0;
            const n = r.notification;
            const consumed = (r.consumptions || []).reduce((a, d) => a + Number(d.quantity || 0), 0);
            const noteCell = first
                ? `<span class="badge ${n ? 'bg-label-success' : 'bg-label-danger'}">${esc(t(n ? 'Recall.Cust.Notified' : 'Recall.Cust.NotNotified'))}</span>`
                  + (n ? `<div class="small text-muted">${esc(ui.date(n.notifiedOn))} · ${esc(t('Recall.Channel.' + n.channel))}${n.note ? ' · ' + esc(n.note) : ''}</div>` : '')
                  + (consumed > 0 ? `<div class="small text-muted">${esc(t('Recall.Cust.Consumed', ui.qty(consumed)))}</div>` : '')
                  + `<div class="d-flex flex-wrap gap-1 mt-1">`
                  + (state.offers.notify && byId('rc-notify-modal') ? `<button type="button" class="btn btn-sm btn-label-primary py-0" data-notify="${esc(r.customerRef)}">${esc(t('Recall.Cust.Mark'))}</button>` : '')
                  + (state.offers.consume && byId('rc-consume-modal') ? `<button type="button" class="btn btn-sm btn-label-secondary py-0" data-consume="${esc(r.customerRef)}">${esc(t('Recall.Cust.Consumption'))}</button>` : '')
                  + '</div>'
                : '';
            return `<tr><td>${first ? `<span class="fw-medium text-heading">${esc(r.label || r.customerRef)}</span><div class="small text-muted font-monospace">${esc(r.customerRef)}</div>` : ''}</td>`
                + `<td class="small">${doc}<div class="text-muted">${product(s)} · <span class="font-monospace">${esc(s.lot?.lotNumber || '')}</span></div></td><td class="small">${delivery(s)}</td>`
                + `<td class="text-end">${qty(s.quantity, s.baseUomId)}</td><td class="text-end">${eff ? qty(eff.returned, eff.baseUomId) : '—'}</td><td class="text-end">${eff ? qty(eff.open, eff.baseUomId) : '—'}</td><td>${noteCell}</td></tr>`;
        }));
        byId('rc-cust-table').querySelector('tbody').innerHTML = body.join('') || `<tr><td colspan="7" class="text-muted">${esc(t('Recall.Cust.Empty'))}</td></tr>`;
        byId('rc-cust-table').querySelectorAll('[data-notify]').forEach((b) => b.addEventListener('click', () => openNotify(rows.find((r) => r.customerRef === b.dataset.notify))));
        byId('rc-cust-table').querySelectorAll('[data-consume]').forEach((b) => b.addEventListener('click', () => openConsume(rows.find((r) => r.customerRef === b.dataset.consume))));
    };

    const drawReturns = (returns) => {
        const links = returns.links || [];
        byId('rc-ret-links').querySelector('tbody').innerHTML = links.map((l) => `<tr><td class="font-monospace small">${esc(l.transactionNumber || l.transactionId)}</td><td class="small">${esc(l.customerRef)}</td>`
            + `<td class="font-monospace small">${esc(l.lot?.lotNumber || '')}</td><td class="text-end">${qty(l.quantity, l.baseUomId)}</td><td class="small">${esc(ui.date(l.postedAtUtc))}</td>`
            + `<td><span class="badge bg-label-info">${esc(t('Recall.Ret.Source.' + l.source))}</span></td>`
            // D-28 (RECALL 1.7.0): the hold MOD-0175 opened for THIS movement → its quarantine record page; none → "—" (never another hold)
            + `<td>${l.holdId ? `<a href="/Quarantine/Details/${encodeURIComponent(l.holdId)}" class="font-monospace small">${esc(l.holdNumber || l.holdId)} →</a>` : '—'}</td></tr>`).join('')
            || `<tr><td colspan="7" class="text-muted">${esc(t('Recall.Ret.Empty'))}</td></tr>`;
        const candidates = returns.candidates || [];
        byId('rc-ret-candidates').querySelector('tbody').innerHTML = candidates.map((c) => {
            const button = c.eligible && state.offers.link && byId('rc-link-modal')
                ? `<button type="button" class="btn btn-sm btn-primary py-0" data-link="${esc(c.transactionId)}">${esc(t('Recall.Ret.Link'))}</button>`
                : c.eligible ? '' : `<span class="small text-muted">${esc(t('Recall.Ret.Ineligible.' + c.ineligibleReason))}</span>`;
            return `<tr><td class="font-monospace small">${esc(c.transactionNumber || c.transactionId)}</td><td class="font-monospace small">${esc(c.lot?.lotNumber || '')}</td>`
                + `<td class="text-end">${qty(c.quantity, c.baseUomId)}</td><td class="small">${esc(c.toStockStatus ? t('Recall.StockStatus.' + c.toStockStatus) || c.toStockStatus : '')}</td>`
                + `<td class="small">${esc(ui.date(c.postedAtUtc))}</td><td>${button}</td></tr>`;
        }).join('') || `<tr><td colspan="6" class="text-muted">${esc(t('Recall.Ret.NoCandidates'))}</td></tr>`;
        byId('rc-ret-candidates').querySelectorAll('[data-link]').forEach((b) => b.addEventListener('click', () => openLink(candidates.find((c) => c.transactionId === b.dataset.link))));
    };

    const percent = (p) => `%${String(p).replace('.', ',')}`;
    const drawEffectiveness = (eff) => {
        state.data.effectiveness = eff;
        byId('rc-eff-source').textContent = eff.source === 'Stored' ? t('Recall.Eff.Stored', ui.date(eff.computedAtUtc)) : t('Recall.Eff.Live');
        const target = t('Recall.Eff.Target', eff.targetPercent, eff.class);
        byId('rc-eff-totals').innerHTML = (eff.totals || []).map((x) => `<div class="col-12 col-lg-6"><div class="card h-100"><div class="card-body p-3">`
            + `<div class="small text-muted mb-1">${esc(x.baseUomId)} · <span class="font-monospace">${esc(x.skuId)}</span></div>`
            + `<div class="small">${esc(t('Recall.Eff.Shipped'))} ${qty(x.shipped)} − ${esc(t('Recall.Eff.Returned'))} ${qty(x.returned)} − ${esc(t('Recall.Eff.Consumed'))} ${qty(x.consumed)} = <strong>${esc(t('Recall.Eff.Open'))} ${qty(x.open, x.baseUomId)}</strong></div>`
            + `<div class="d-flex align-items-center gap-2 mt-2"><h5 class="mb-0">${esc(percent(x.percent))}</h5><span class="badge ${x.met ? 'bg-label-success' : 'bg-label-danger'}">${esc(t(x.met ? 'Recall.Eff.Met' : 'Recall.Eff.NotMet'))}</span><small class="text-muted">${esc(target)}</small></div>`
            + '</div></div></div>').join('') || `<div class="col-12 text-muted small">${esc(t('Recall.Eff.Empty'))}</div>`;
        byId('rc-eff-table').querySelector('tbody').innerHTML = (eff.rows || []).map((r) => `<tr><td class="small">${esc(r.customerRef)}${r.notified ? '' : ` <span class="badge bg-label-danger">${esc(t('Recall.Cust.NotNotified'))}</span>`}</td>`
            + `<td class="text-end">${qty(r.shipped, r.baseUomId)}</td><td class="text-end">${qty(r.returned)}</td><td class="text-end">${qty(r.consumed)}</td><td class="text-end">${qty(r.open)}</td>`
            + `<td><span class="badge ${r.met ? 'bg-label-success' : 'bg-label-warning'}">${esc(percent(r.percent))}</span></td></tr>`).join('')
            || `<tr><td colspan="6" class="text-muted">${esc(t('Recall.Eff.Empty'))}</td></tr>`;
    };

    const drawTimeline = (page) => {
        const items = (page.items || []).slice().reverse();
        byId('rc-timeline').innerHTML = items.map((e) => `<li class="timeline-item timeline-item-transparent"><span class="timeline-point timeline-point-primary"></span>`
            + `<div class="timeline-event"><div class="timeline-header mb-1"><h6 class="mb-0">${esc(t('Recall.Event.' + e.kind) || e.kind)}</h6><small class="text-muted">${when(e.atUtc)}</small></div>`
            + `<p class="small mb-1">${person(e.actor)}${e.text ? ' · ' + esc(e.text) : ''}</p>`
            + (e.signatureId ? `<span class="badge bg-label-success"><i class="bx bx-pen me-1"></i>${esc(t('Recall.Case.Signature', e.signatureId))}</span>` : '')
            + '</div></li>').join('') || `<li class="text-muted small">${esc(t('Recall.Tl.Empty'))}</li>`;
    };

    const loaders = {
        scope: async () => { const r = await read('scope'); if (r.ok) drawScope(r.json.data); else ui.showError(byId('rc-error'), sentence(r)); },
        stock: async () => { const r = await read('stock'); if (r.ok) drawStock(r.json.data || []); else ui.showError(byId('rc-error'), sentence(r)); },
        customers: async () => {
            const [r, e] = await Promise.all([read('customers'), read('effectiveness')]);
            if (r.ok) drawCustomers(r.json.data || [], e.ok ? e.json.data : null); else ui.showError(byId('rc-error'), sentence(r));
        },
        returns: async () => { const r = await read('returns'); if (r.ok) drawReturns(r.json.data || {}); else ui.showError(byId('rc-error'), sentence(r)); },
        effectiveness: async () => { const r = await read('effectiveness'); if (r.ok) drawEffectiveness(r.json.data); else ui.showError(byId('rc-error'), sentence(r)); },
        timeline: async () => { const r = await read('timeline', '&page=1&pageSize=200'); if (r.ok) drawTimeline(r.json.data || {}); else ui.showError(byId('rc-error'), sentence(r)); }
    };
    const activeTab = () => document.querySelector('#rc-case-tabs .nav-link.active')?.dataset.rcTab || 'scope';

    /** After a write: the case + the open tab again (other tabs reload when opened). */
    const refresh = async (tab) => {
        state.loaded = {};
        ui.hideError(byId('rc-error'));
        if (await loadCase()) {
            const open = tab || activeTab();
            state.loaded[open] = true;
            await loaders[open]();
        }
    };
    const run = async (call, tab, onError) => {
        const result = await call();
        if (result.ok) { toast(t('Recall.Case.Saved')); await refresh(tab); return true; }
        if (result.reply?.json?.error?.code === 'RECALL_CONFLICT') await refresh(tab);
        if (onError) onError(result.reply); else ui.showError(byId('rc-error'), sentence(result.reply));
        return false;
    };

    // ── unsigned commands ───────────────────────────────────────────────────────────────────────────────────────
    const saveMarks = () => {
        const marks = [...byId('rc-scope-table').querySelectorAll('[data-mark]')].map((box) => {
            const mark = { nodeKey: box.dataset.mark, included: box.checked };
            if (!box.checked) mark.exclusionReason = byId('rc-scope-table').querySelector(`[data-reason="${CSS.escape(box.dataset.mark)}"]`)?.value.trim() || '';
            return mark;
        });
        if (marks.some((m) => !m.included && m.exclusionReason.length < decision.MIN)) { ui.showError(byId('rc-error'), t('Recall.Errors.RECALL_SCOPE_EXCLUSION_REASON_REQUIRED')); return; }
        if (marks.length === 0) return;
        run(() => decision.command(deps, caseId, 'marks', { marks }, state.detail.version), 'scope');
    };

    const askReason = (title, verb) => {
        state.pendingReason = verb;
        byId('rc-reason-title').textContent = title;
        byId('rc-reason-text').value = '';
        byId('rc-reason-text').classList.remove('is-invalid');
        ui.hideError(byId('rc-reason-error'));
        modal('rc-reason-modal').show();
    };
    byId('rc-reason-form')?.addEventListener('submit', async (event) => {
        event.preventDefault();
        const reason = byId('rc-reason-text').value.trim();
        if (reason.length < decision.MIN) { byId('rc-reason-text').classList.add('is-invalid'); return; }
        const ok = await run(() => decision.command(deps, caseId, state.pendingReason, { reason }, state.detail.version), 'scope', (reply) => fail(byId('rc-reason-error'), reply));
        if (ok) modal('rc-reason-modal').hide();
    });

    const openNotify = (row) => {
        state.row = row;
        byId('rc-notify-customer').textContent = row.label || row.customerRef;
        byId('rc-notify-date').value = new Date().toISOString().slice(0, 10);
        byId('rc-notify-note').value = '';
        ui.hideError(byId('rc-notify-error'));
        modal('rc-notify-modal').show();
    };
    byId('rc-notify-form')?.addEventListener('submit', async (event) => {
        event.preventDefault();
        const body = { customerRef: state.row.customerRef, notifiedOn: byId('rc-notify-date').value, channel: byId('rc-notify-channel').value };
        const note = byId('rc-notify-note').value.trim();
        if (note) body.note = note;
        if (state.row.notification) body.supersedesId = state.row.notification.notificationId;
        if (await run(() => decision.command(deps, caseId, 'notifications', body), 'customers', (reply) => fail(byId('rc-notify-error'), reply))) modal('rc-notify-modal').hide();
    });

    const openConsume = (row) => {
        state.row = row;
        byId('rc-consume-customer').textContent = row.label || row.customerRef;
        const select = byId('rc-consume-sku');
        select.replaceChildren(...row.shipments.filter((s, i, all) => all.findIndex((x) => x.item?.skuId === s.item?.skuId && x.baseUomId === s.baseUomId) === i)
            .map((s) => { const o = new Option(`${s.itemLabel || s.item?.itemId} · ${s.baseUomId}`, s.item?.skuId); o.dataset.level = s.item?.skuLevel; o.dataset.unit = s.baseUomId; return o; }));
        byId('rc-consume-unit').textContent = select.selectedOptions[0]?.dataset.unit ? `(${select.selectedOptions[0].dataset.unit})` : '';
        byId('rc-consume-qty').value = '';
        byId('rc-consume-date').value = new Date().toISOString().slice(0, 10);
        byId('rc-consume-evidence').value = '';
        ui.hideError(byId('rc-consume-error'));
        modal('rc-consume-modal').show();
    };
    byId('rc-consume-sku')?.addEventListener('change', (event) => { byId('rc-consume-unit').textContent = `(${event.target.selectedOptions[0]?.dataset.unit || ''})`; });
    byId('rc-consume-form')?.addEventListener('submit', async (event) => {
        event.preventDefault();
        const option = byId('rc-consume-sku').selectedOptions[0];
        const body = { customerRef: state.row.customerRef, skuId: option?.value, skuLevel: option?.dataset.level, quantity: Number(byId('rc-consume-qty').value),
            declaredOn: byId('rc-consume-date').value, evidence: byId('rc-consume-evidence').value.trim() };
        if (await run(() => decision.command(deps, caseId, 'consumptions', body), 'customers', (reply) => fail(byId('rc-consume-error'), reply))) modal('rc-consume-modal').hide();
    });

    const openLink = async (candidate) => {
        state.row = candidate;
        byId('rc-link-movement').textContent = `${candidate.transactionNumber || candidate.transactionId} · ${candidate.lot?.lotNumber || ''} · ${ui.qty(candidate.quantity)} ${candidate.baseUomId}`;
        const customers = state.data.customers || (await read('customers')).json?.data || [];
        state.data.customers = customers;
        byId('rc-link-customer').replaceChildren(...customers.map((c) => new Option(c.label || c.customerRef, c.customerRef)));
        byId('rc-link-reference').value = '';
        ui.hideError(byId('rc-link-error'));
        modal('rc-link-modal').show();
    };
    byId('rc-link-form')?.addEventListener('submit', async (event) => {
        event.preventDefault();
        const body = { transactionId: state.row.transactionId, customerRef: byId('rc-link-customer').value };
        const reference = byId('rc-link-reference').value.trim();
        if (reference) body.returnReference = reference;
        if (await run(() => decision.command(deps, caseId, 'return-links', body), 'returns', (reply) => fail(byId('rc-link-error'), reply))) modal('rc-link-modal').hide();
    });

    const openSegregation = (line) => {
        state.row = line;
        byId('rc-seg-line').textContent = `${[line.warehouseLabel, line.locationLabel].filter(Boolean).join(' · ')} · ${line.lot?.lotNumber || ''}`;
        byId('rc-seg-qty').value = String(line.request?.heldQuantity || line.quantity || '');
        byId('rc-seg-unit').textContent = `(${line.baseUomId})`;
        byId('rc-seg-note').value = '';
        ui.hideError(byId('rc-seg-error'));
        modal('rc-seg-modal').show();
    };
    byId('rc-seg-form')?.addEventListener('submit', async (event) => {
        event.preventDefault();
        const body = { nodeKey: state.row.nodeKey, requestId: state.row.request?.requestId, locationId: state.row.locationId, quantity: Number(byId('rc-seg-qty').value) };
        const note = byId('rc-seg-note').value.trim();
        if (note) body.note = note;
        if (await run(() => decision.command(deps, caseId, 'segregations', body), 'stock', (reply) => fail(byId('rc-seg-error'), reply))) modal('rc-seg-modal').hide();
    });

    // ── the signed decisions: dialog step 1 (prepare) → step 2 (MOD-0022) → command ─────────────────────────────────
    const TITLES = { approve: 'Recall.Sign.ApproveTitle', complete: 'Recall.Sign.CompleteTitle', close: 'Recall.Sign.CloseTitle' };
    const openDecision = async (kind) => {
        const c = state.detail;
        let effectiveness = c.effectiveness;
        if (kind !== 'approve' && !effectiveness) effectiveness = (await read('effectiveness')).json?.data;
        const openRemains = kind === 'close' && view.hasOpen(effectiveness);
        state.dialog = { kind, openRemains, prepared: null };
        byId('rc-sign-title').textContent = t(TITLES[kind]);
        const lines = kind === 'approve'
            ? [t('Recall.Scope.Summary', c.counts?.scopeItems ?? '', c.counts?.excludedItems ?? '', c.scopeRevision)]
            : (effectiveness?.totals || []).map((x) => `${t('Recall.Eff.Shipped')} ${ui.qty(x.shipped)} − ${t('Recall.Eff.Returned')} ${ui.qty(x.returned)} − ${t('Recall.Eff.Consumed')} ${ui.qty(x.consumed)} = ${t('Recall.Eff.Open')} ${ui.qty(x.open)} ${x.baseUomId} · ${percent(x.percent)}`);
        if (kind !== 'approve' && effectiveness) lines.push(t('Recall.Eff.Target', effectiveness.targetPercent, effectiveness.class));
        if (kind === 'close') {
            // the mockup's closure summary (L:301): the quarantine requests with their status and the notified customers
            const stock = (await read('stock')).json?.data || [];
            const requests = stock.filter((l) => l.request).map((l) => `${(l.request.holds || []).map((h) => h.holdNumber).join(', ') || l.lot?.lotNumber} (${t('Recall.ReqStatus.' + l.request.status)})`);
            lines.push(t('Recall.Sign.LineQuarantine', requests.join(', ') || t('Recall.Common.None')));
            lines.push(t('Recall.Sign.LineNotified', c.counts?.notified ?? 0, c.counts?.customers ?? 0));
        }
        byId('rc-sign-lines').innerHTML = lines.map((l) => `<li>${esc(l)}</li>`).join('');
        document.querySelectorAll('[data-sign-field]').forEach((f) => show(f, false));
        show(document.querySelector('[data-sign-field="note"]'), kind !== 'close');
        byId('rc-sign-note-label').textContent = t(kind === 'approve' ? 'Recall.Sign.ApproveNote' : 'Recall.Sign.CompleteNote');
        show(document.querySelector('[data-sign-field="summary"]'), kind === 'close');
        show(document.querySelector('[data-sign-field="acceptance"]'), openRemains);
        ['rc-sign-note', 'rc-sign-summary', 'rc-sign-acceptance'].forEach((id) => { byId(id).value = ''; byId(id).classList.remove('is-invalid'); byId(id).disabled = false; });
        show(byId('rc-sign-ready'), false);
        ui.hideError(byId('rc-sign-error'));
        show(byId('rc-sign-next'), true);
        show(byId('rc-sign-commit'), false);
        byId('rc-sign-step1').className = 'fw-medium';
        byId('rc-sign-step2').className = 'text-muted';
        modal('rc-sign-modal').show();
    };
    const dialogInput = () => ({
        caseId, version: state.detail.version, note: byId('rc-sign-note').value, summary: byId('rc-sign-summary').value,
        acceptance: byId('rc-sign-acceptance').value, openRemains: state.dialog.openRemains
    });
    byId('rc-sign-form')?.addEventListener('submit', async (event) => {
        event.preventDefault();
        ui.hideError(byId('rc-sign-error'));
        const first = await decision.prepare(deps, state.dialog.kind, dialogInput());
        if (!first.ok) {
            if (first.step === 'validate') {
                Object.keys(first.errors).forEach((field) => byId(`rc-sign-${field}`)?.classList.add('is-invalid'));
                return;
            }
            fail(byId('rc-sign-error'), first.reply, state.dialog.kind);
            if (first.reply?.json?.error?.code === 'RECALL_CONFLICT') await refresh();
            return;
        }
        state.dialog.prepared = first.prepared;
        ['rc-sign-note', 'rc-sign-summary', 'rc-sign-acceptance'].forEach((id) => { byId(id).disabled = true; });
        byId('rc-sign-step1').className = 'text-muted';
        byId('rc-sign-step2').className = 'fw-medium';
        byId('rc-sign-ready').textContent = first.signatureRequired ? t('Recall.Sign.ToSign') : t('Recall.Sign.NoSignature');
        show(byId('rc-sign-ready'), true);
        byId('rc-sign-commit-text').textContent = t(first.signatureRequired ? 'Recall.Sign.SignSave' : 'Recall.Sign.Save');
        show(byId('rc-sign-next'), false);
        show(byId('rc-sign-commit'), true);
    });
    byId('rc-sign-commit')?.addEventListener('click', async () => {
        const { kind, prepared } = state.dialog;
        if (decision.needsSignature(prepared) && !window.ESign?.open) { ui.showError(byId('rc-sign-error'), t('Recall.Errors.ESIGN_UNAVAILABLE')); return; }
        const result = await decision.signAndCommit(deps, kind, prepared, dialogInput());
        if (result.ok) {
            modal('rc-sign-modal').hide();
            toast(result.signatureId ? t('Recall.Sign.DoneSigned', result.signatureId) : t('Recall.Case.Saved'));
            await refresh();
            return;
        }
        if (result.cancelled) { ui.showError(byId('rc-sign-error'), t('Recall.Sign.Cancelled')); return; }
        fail(byId('rc-sign-error'), result.reply, kind);
    });

    // ── wiring ──────────────────────────────────────────────────────────────────────────────────────────────────
    const on = (name, handler) => action(name)?.addEventListener('click', handler);
    on('save-marks', saveMarks);
    on('submit', () => run(() => decision.command(deps, caseId, 'submit', {}, state.detail.version), 'scope'));
    on('return', () => askReason(t('Recall.Reason.ReturnTitle'), 'return'));
    on('cancel', () => askReason(t('Recall.Reason.CancelTitle'), 'cancel'));
    on('approve', () => openDecision('approve'));
    on('eff-start', () => run(() => decision.command(deps, caseId, 'effectiveness/start', {}, state.detail.version), 'effectiveness'));
    on('eff-complete', () => openDecision('complete'));
    on('close', () => openDecision('close'));
    on('rescan', async () => {
        const r = await read('diff');
        if (r.ok) drawDiff(r.json.data); else ui.showError(byId('rc-error'), sentence(r));
    });
    on('revise', () => {
        const addNodeKeys = [...document.querySelectorAll('#rc-diff [data-add]:checked')].map((b) => b.dataset.add);
        const reason = byId('rc-revise-reason').value.trim();
        if (addNodeKeys.length === 0 || reason.length < decision.MIN) { ui.showError(byId('rc-error'), t('Recall.Common.ReasonMin')); return; }
        run(() => decision.command(deps, caseId, 'revise', { addNodeKeys, reason }, state.detail.version), 'scope').then((ok) => { if (ok) show(byId('rc-diff'), false); });
    });
    on('quarantine-send', () => {
        const nodeKeys = [...state.selected];
        if (nodeKeys.length > 0) run(() => decision.command(deps, caseId, 'quarantine', { nodeKeys }), 'stock');
    });
    document.querySelectorAll('#rc-case-tabs [data-rc-tab]').forEach((tab) => tab.addEventListener('shown.bs.tab', () => {
        const name = tab.dataset.rcTab;
        if (!state.loaded[name]) { state.loaded[name] = true; loaders[name](); }
    }));

    document.addEventListener('DOMContentLoaded', async () => {
        if (await loadCase()) { state.loaded.scope = true; await loaders.scope(); }
    });
})();
