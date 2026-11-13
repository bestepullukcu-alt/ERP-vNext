/**
 * MOD-0178 DL-6 — the outbound's Details page (Golden Reference Compact; pack §8.18, §8.21, §8.22, §8.24, §11.3).
 *
 * One read: '/Warehouse/OutboundShipments/outbound/{id}' (the view + its timeline). The action bar is OutboundRules.menu — the §8.18 status
 * transitions for this reader's keys: "Paketle" (→ the Packing page), "Sevke hazır" (DISABLED with its reason on an Item outbound — §8.24,
 * AC-33 (vi)), "Sevkiyatı kontrol et", "İptal"; "Düzenle" is the header button on Allocated / Picked / Packed. A list row's menu lands here with
 * #Release / #Cancel / #SyncDispatch and opens that dialog. Writes go to the same-origin proxy only; the service stays the authority.
 */
'use strict';

const OutboundShipmentDetails = (function () {
    const rules = window.OutboundRules;
    const root = document.getElementById('outbound-details');
    const outboundId = root?.dataset.outboundId || '';
    const L = () => window.L10n || {};
    const W = (key) => (L().Words || {})[key] || '';
    const byId = (id) => document.getElementById(id);
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    const can = (key) => window.Permissions?.has?.(key) === true;
    const getAuthHeaders = (includeJson = false) => window.DitenDataTable?.getAuthHeaders?.(includeJson) || {};
    const newKey = () => (window.crypto?.randomUUID ? window.crypto.randomUUID() : 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
        const r = Math.random() * 16 | 0;
        return (c === 'x' ? r : (r & 0x3 | 0x8)).toString(16);
    }));
    const token = () => document.querySelector('#outboundActionToken input[name="__RequestVerificationToken"]')?.value || '';
    // E3-FIX-01 (G-1): every quantity / weight / dimension the page shows is in the reader's culture (tr "2", "1,5"); what it sends is unchanged
    const culture = () => window.CurrentLanguage || document.documentElement.lang || undefined;
    const qty = (value) => rules.quantityText(value, culture());
    const format =(template, ...values) => values.reduce((text, value, i) => text.replace(`{${i}}`, value ?? ''), template || '');
    const fmtDate = (value) => {
        if (!value) return '';
        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString(window.CurrentLanguage || undefined);
    };
    const setText = (id, value) => { const el = byId(id); if (el) el.textContent = value == null || value === '' ? '-' : String(value); };

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
    const showAlert = (id, text) => { const box = byId(id); if (box) { box.textContent = text || ''; box.classList.toggle('d-none', !text); } };

    let current = null; // { outbound, etag }

    // ─── Render ──────────────────────────────────────────────────────────────
    const renderLines = (lines) => {
        byId('d-lines').innerHTML = (lines || []).map((l) => {
            const allocations = (l.allocations || []).map((a) => `<tr data-alloc-seq="${esc(a.allocSeq)}"><td>${esc(a.allocSeq)}</td><td>${esc(a.lotNumber || '-')}</td>`
                + `<td>${esc(a.fefoRank ?? '-')}</td><td class="text-end">${esc(qty(a.quantity))}</td><td class="text-end">${esc(qty(a.picked ?? '0'))}</td><td class="text-end">${esc(qty(a.packed ?? '0'))}</td>`
                + `<td><small>${esc((a.bins || []).map((b) => `${b.location} (${qty(b.quantity)})`).join(', '))}</small></td>`
                + `<td><small>${esc(W(`Warehouse.Outbound.ReleaseState.${a.reservationReleaseState || 'None'}`))}</small>`
                + (a.fefoBypassReason ? `<small class="d-block text-muted text-break">${esc(a.fefoBypassReason)}</small>` : '') + '</td></tr>').join('');
            return `<div class="border rounded p-3" data-line-seq="${esc(l.lineSeq)}"><div class="d-flex flex-wrap justify-content-between gap-2 mb-2">`
                + `<strong>${esc(l.orderLineId)} · ${esc(l.sku)}</strong><span>${esc(qty(l.quantity))} ${esc(l.uom)}</span></div>`
                + '<div class="table-responsive"><table class="table table-sm mb-0"><thead><tr>'
                + ['AllocSeq', 'AllocLot', 'AllocFefoRank', 'AllocQuantity', 'AllocPicked', 'AllocPacked', 'AllocBins', 'AllocRelease'].map((k) => `<th scope="col">${esc(W(k))}</th>`).join('')
                + `</tr></thead><tbody>${allocations}</tbody></table></div></div>`;
        }).join('');
    };

    const renderTasks = (tasks) => {
        const canPick = can('warehouse.pick-tasks.read');
        byId('d-pick-tasks').innerHTML = (tasks || []).map((t) => `<div class="d-flex flex-wrap justify-content-between gap-2 small" data-pick-task="${esc(t.id)}">`
            + `<span class="font-monospace">${canPick ? `<a href="/Warehouse/PickTasks">${esc(t.number)}</a>` : esc(t.number)}</span>`
            + `<span>${esc(t.from)} · ${esc(t.lotNumber || '-')}</span><span>${esc(qty(t.confirmed))} / ${esc(qty(t.planned))}</span>`
            + `<span class="badge bg-label-secondary">${esc(W(`Warehouse.Status.${t.status}`))}</span></div>`).join('');
        byId('d-pick-tasks-empty').classList.toggle('d-none', (tasks || []).length > 0);
    };

    const renderPackages = (packages) => {
        byId('d-packages').innerHTML = (packages || []).map((p) => `<div class="d-flex flex-wrap justify-content-between gap-2 small" data-package="${esc(p.id)}">`
            + `<span class="font-monospace">${esc(p.number)}</span><span>${esc((p.contents || []).map((c) => format(W('PackageContentLine'), c.allocSeq, qty(c.quantity))).join(', '))}</span>`
            + `<span>${esc([p.weightKg ? `${qty(p.weightKg)} kg` : '', rules.dimensionsText(p.dimensionsCm, culture())].filter(Boolean).join(' · '))}</span>`
            + `<span class="badge bg-label-secondary">${esc(W(`Warehouse.Package.Status.${p.status}`))}</span></div>`).join('');
        byId('d-packages-empty').classList.toggle('d-none', (packages || []).length > 0);
    };

    const renderShipment = (o) => {
        const card = byId('d-shipment-card');
        const has = !!o.shipment || (o.shortDispatch || []).length > 0;
        card.classList.toggle('d-none', !has);
        if (!has) return;
        byId('d-shipment').innerHTML = o.shipment ? `<div class="small">${esc(format(W('ShipmentLine'), o.shipment.number, o.shipment.status, fmtDate(o.shipment.observedAt)))}</div>` : '';
        byId('d-short-dispatch').innerHTML = (o.shortDispatch || []).map((s) => `<div class="small text-warning">${esc(format(W('ShortDispatchLine'), s.sku, qty(s.expected), qty(s.issued), s.reason))}</div>`).join('');
    };

    const renderTimeline = (timeline, available) => {
        byId('d-timeline').innerHTML = (timeline || []).map((e) => `<li class="timeline-item timeline-item-transparent" data-event-kind="${esc(e.kind)}">`
            + '<span class="timeline-point timeline-point-primary"></span><div class="timeline-event"><div class="timeline-header mb-1">'
            + `<h6 class="mb-0">${esc(W(`Warehouse.Outbound.Event.${e.kind}`))}</h6><small class="text-muted">${esc(fmtDate(e.atUtc))}</small></div>`
            + `<small class="text-muted">${esc(e.system ? W('SystemActor') : (e.actor?.name || W('UnknownUser')))}</small></div></li>`).join('');
        byId('d-timeline-unavailable').classList.toggle('d-none', available !== false);
    };

    const ActionIcon = { Pack: 'bx bx-package', Release: 'bx bx-send', SyncDispatch: 'bx bx-refresh', Cancel: 'bx bx-x-circle' };

    const renderActions = (o) => {
        byId('d-actions').innerHTML = rules.menu(o, can).map((state) => {
            const word = W(rules.ActionWord[state.action]);
            const reason = state.enabled ? '' : W(rules.reasonWord(state.reasonCode));
            const cls = state.dangerous ? 'btn-label-danger' : state.action === 'Release' || state.action === 'Pack' ? 'btn-primary' : 'btn-label-secondary';
            const control = state.action === 'Pack' && state.enabled
                ? `<a class="btn ${cls} wh-touch" href="${esc(rules.actionUrl(o, 'Pack'))}" data-d-action="Pack"><i class="${ActionIcon.Pack} me-1"></i>${esc(word)}</a>`
                : `<button type="button" class="btn ${cls} wh-touch" data-d-action="${state.action}"${state.enabled ? '' : ' disabled aria-disabled="true"'} title="${esc(reason)}"><i class="${ActionIcon[state.action]} me-1"></i>${esc(word)}</button>`;
            return `<div class="d-flex flex-column">${control}${reason ? `<small class="text-muted wh-menu-reason" data-d-reason="${esc(state.reasonCode)}">${esc(reason)}</small>` : ''}</div>`;
        }).join('');
        byId('d-actions').classList.toggle('d-none', !byId('d-actions').innerHTML);
        const edit = byId('d-edit');
        edit.classList.toggle('d-none', !rules.canEdit(o, can));
        edit.href = `/Warehouse/OutboundShipments/Edit/${encodeURIComponent(o.id)}`;
    };

    const render = (answer) => {
        current = { outbound: answer.outbound, etag: answer.etag };
        const o = answer.outbound;
        setText('d-number', o.number);
        const badge = rules.statusBadge(o.status);
        const status = byId('d-status');
        status.className = `badge ${badge.cls}`;
        status.textContent = W(badge.word);
        byId('d-item-level').classList.toggle('d-none', !o.itemLevel);
        byId('d-nothing-picked').classList.toggle('d-none', !rules.nothingPicked(o));
        byId('d-names').classList.toggle('d-none', answer.namesAvailable !== false);
        setText('d-warehouse', o.warehouse);
        setText('d-staging', o.staging);
        setText('d-order-ref', o.orderRef);
        setText('d-created', [fmtDate(o.createdAtUtc), o.createdBy].filter(Boolean).join(' · '));
        setText('d-ready', fmtDate(o.readyAt));
        setText('d-shipto-name', o.shipTo?.name);
        setText('d-shipto-country', o.shipTo?.country);
        setText('d-shipto-city', o.shipTo?.city);
        setText('d-shipto-postal', o.shipTo?.postalCode);
        setText('d-shipto-line1', o.shipTo?.line1);
        setText('d-shipto-line2', o.shipTo?.line2);
        setText('d-note', o.note);
        const cancel = byId('d-cancel-reason');
        cancel.textContent = o.cancelReason ? format(W('CancelReasonLine'), o.cancelReason) : '';
        cancel.classList.toggle('d-none', !o.cancelReason);
        renderLines(o.lines);
        renderTasks(o.pickTasks);
        renderPackages(o.packages);
        renderShipment(o);
        renderTimeline(answer.timeline, answer.timelineAvailable);
        renderActions(o);
    };

    const load = async () => {
        showAlert('d-error', '');
        if (!outboundId) { showAlert('d-error', W('Warehouse.Error.UNKNOWN_OUTBOUND')); return false; }
        const answer = await getJson(`/Warehouse/OutboundShipments/outbound/${encodeURIComponent(outboundId)}`);
        if (!answer.ok) { showAlert('d-error', failure(answer)); return false; }
        render(answer.json);
        return true;
    };

    // ─── Dialogs ─────────────────────────────────────────────────────────────
    // E3-FIX-01 (item 1): "Sevke hazır", its FEFO reasons and "İptal" are the product's ONE confirm (window.showConfirm, MOD-0013) — the same shape
    // as "Sevkiyatı kontrol et" below and Packing's "Koliyi kapat": the icon ring, the record's number, the theme's buttons. The flow and the
    // idempotency keys are unchanged: one key per opened dialog; the FEFO reasons are a new body, so a new key.
    const reasonValidator = (value) => (String(value ?? '').trim().length < 10 ? W('ReasonRequired') : null);

    const openCancel = () => {
        const key = newKey();
        window.showConfirm?.(W('Warehouse.Outbound.Action.Cancel.Confirm') || L().AreYouSure, async (reason) => {
            const answer = await post(`/Warehouse/OutboundShipments/cancel/${encodeURIComponent(outboundId)}`,
                { idempotencyKey: key, ifMatch: current.etag, reason: String(reason ?? '').trim() });
            if (!answer.ok) { window.showToast?.(failure(answer), 'error'); return; }
            window.showToast?.(answer.json?.message || '', 'success');
            await load();
        }, {
            type: 'danger',
            icon: 'bx-x-circle',
            entityName: current.outbound.number,
            confirmButtonText: W('Warehouse.Outbound.Action.Cancel'),
            cancelButtonText: L().Close, // the confirm button is already "İptal": the way out must not read the same
            showInput: true,
            inputType: 'textarea',
            inputLabel: W('FieldReason'),
            inputPlaceholder: W('ReasonHint'),
            inputAttributes: { maxlength: 1000 },
            inputValidator: reasonValidator
        });
    };

    const sendRelease = async (key, rows) => {
        const answer = await post(`/Warehouse/OutboundShipments/release/${encodeURIComponent(outboundId)}`, rules.releaseBody(key, current.etag, rows));
        if (answer.ok) {
            window.showToast?.(answer.json?.message || '', 'success');
            await load();
            return;
        }
        // r22: a 422 FEFO_BYPASS_REASON_REQUIRED comes back with its deviations — a reason is asked for each, then ONE request sends them all
        const deviations = answer.json?.error?.code === 'FEFO_BYPASS_REASON_REQUIRED' && !rows.length ? rules.deviations(answer.json.error, current.outbound) : [];
        if (deviations.length) { askReasons(deviations, [], newKey()); return; } // a new body is a new request (the old key carried no reasons)
        window.showToast?.(failure(answer), 'error');
    };

    // One standard dialog per deviating allocation (the shared confirm takes one field); closing any of them sends nothing.
    const askReasons = (deviations, written, key) => {
        const row = deviations[written.length];
        if (!row) { sendRelease(key, written); return; }
        window.showConfirm?.(W('ReleaseReasonsIntro') || L().AreYouSure, (reason) => {
            askReasons(deviations, written.concat({ allocSeq: row.allocSeq, reason: String(reason ?? '').trim() }), key);
        }, {
            icon: 'bx-error',
            entityName: current.outbound.number,
            subtext: '',
            confirmButtonText: W('Warehouse.Outbound.Action.Release'),
            showInput: true,
            inputType: 'textarea',
            inputLabel: format(W('ReleaseReasonLabel'), row.allocSeq, row.lotNumber, row.firstEligibleExpiry || '-'),
            inputAttributes: { maxlength: 1000 },
            inputValidator: reasonValidator
        });
    };

    const openRelease = () => {
        const key = newKey();
        window.showConfirm?.(W('Warehouse.Outbound.Action.Release.Confirm') || L().AreYouSure, () => sendRelease(key, []), {
            icon: 'bx-send',
            entityName: current.outbound.number,
            confirmButtonText: W('Warehouse.Outbound.Action.Release')
        });
    };

    const sync = () => {
        window.showConfirm?.(W('Warehouse.Outbound.Action.SyncDispatch.Confirm') || L().AreYouSure, async () => {
            const answer = await post(`/Warehouse/OutboundShipments/sync/${encodeURIComponent(outboundId)}`, { idempotencyKey: newKey(), ifMatch: current.etag });
            if (!answer.ok) { window.showToast?.(failure(answer), 'error'); return; }
            window.showToast?.(answer.json?.message || '', answer.json?.dispatchSeen ? 'success' : 'info');
            await load();
        }, { entityName: current.outbound.number, confirmButtonText: W('Warehouse.Outbound.Action.SyncDispatch') });
    };

    const open = (action) => {
        if (!current) return;
        const state = rules.menu(current.outbound, can).find((s) => s.action === action);
        if (!state || !state.enabled) return; // a disabled / absent entry opens nothing (the reason is on the bar)
        if (action === 'Cancel') openCancel();
        else if (action === 'Release') openRelease();
        else if (action === 'SyncDispatch') sync();
    };

    const init = async () => {
        if (!root) return;
        byId('d-actions')?.addEventListener('click', (event) => {
            const button = event.target.closest('button[data-d-action]');
            if (button && !button.disabled) open(button.dataset.dAction);
        });
        if (await load()) {
            const hash = (window.location.hash || '').replace('#', '');
            if (hash) open(hash);
        }
    };

    return { init };
})();

document.addEventListener('DOMContentLoaded', () => OutboundShipmentDetails.init());
