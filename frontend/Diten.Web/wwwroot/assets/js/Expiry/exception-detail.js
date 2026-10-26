/**
 * MOD-0176 F5 — İstisna Detayı (pack §11.1, §8.4; mockup "det" + "dp"). One door: /Expiry/data/exception/{id}. Draws the facts, "Ne oldu"
 * per type (the case's own facts — nothing computed here but the day difference of two dates the server gave), the timeline, the comment
 * box, and the decision. The calls of a decision are ExpiryDecision's (expiry-decision.js): prepare → the MOD-0022 dialog when the server
 * asks → resolve; close; take; comment. Buttons by action code (ExpiryUi.can) and by status (ExpiryDecision.offers).
 */
'use strict';

(function () {
    const ui = window.ExpiryUi;
    const decision = window.ExpiryDecision;
    const { t, esc } = ui;
    const root = ui.byId('ex-detail');
    const id = root?.getAttribute('data-exception-id') || '';
    const state = { record: null, prepared: null };
    const deps = {
        write: (method, path, body, options) => ui.write(method, path, body, options),
        newKey: ui.newKey,
        sign: (subject) => (window.ESign?.open ? window.ESign.open(subject) : Promise.resolve(null))
    };

    const dl = (pairs) => pairs.filter(([, v]) => v !== null && v !== undefined && v !== '')
        .map(([k, v]) => `<dt class="col-sm-5 text-muted fw-normal">${esc(k)}</dt><dd class="col-sm-7 mb-2">${v}</dd>`).join('');
    const days = (from, to) => (from && to ? Math.round((Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / 86400000) : null);
    const lotText = (number, expiry) => `<span class="font-monospace">${esc(number || '—')}</span>${expiry ? ` · ${esc(t('Expiry.Detail.ExpiryShort', ui.date(expiry)))}` : ''}`;
    const qtyText = (r) => (r.quantity == null ? null : `<span dir="ltr">${esc(ui.qty(r.quantity))} ${esc(r.baseUomId || t('Expiry.Common.NoUnit'))}</span>`);

    const facts = (r) => dl([
        [t('Expiry.Col.Type'), esc(t('Expiry.Type.' + r.type))],
        [t('Expiry.Col.Product'), ui.productCell(r)],
        [t('Expiry.Col.LotWarehouse'), `${lotText(r.lot?.lotNumber, r.lot?.expiryDate)}<br><small class="text-muted">${esc(r.warehouseLabel || '—')}</small>`],
        [t('Expiry.Detail.Movement'), r.sourceMovement
            ? `${esc(t('Expiry.Movement.' + (r.sourceMovement.movementType || 'OTHER')) || r.sourceMovement.movementType || '')} · <span class="font-monospace small">${esc(r.sourceMovement.transactionId)}</span>`
            : esc(r.origin === 'Manual' ? t('Expiry.Detail.OpenedManually') : t('Expiry.Detail.AutomaticCheck'))],
        [t('Expiry.Detail.Quantity'), qtyText(r)],
        [t('Expiry.Detail.When'), `${esc(ui.dateTime(r.openedAtUtc))} · ${esc(r.openedBy ? ui.person(r.openedBy) : t('Expiry.Common.System'))}`],
        [t('Expiry.Col.Assignee'), r.assignee ? esc(ui.person(r.assignee)) : `<span class="text-muted">${esc(t('Expiry.Inbox.Unassigned'))}</span>`]
    ]);

    /** "Ne oldu" — the per-type facts (pack §4.3 Facts; mockup L:278-282). */
    const what = (r) => {
        const f = r.facts || {};
        const box = ui.byId('ex-d-headline');
        const tone = { FEFO_BYPASS: 'alert-primary', BELOW_MIN_REMAINING: 'alert-warning', EXPIRY_DATE_OVERRIDE: 'alert-info', EXPIRED_ON_HAND: 'alert-danger' }[r.type] || 'alert-secondary';
        box.className = `alert mb-3 ${tone}`;
        let headline = '';
        let pairs = [];
        if (r.type === 'FEFO_BYPASS' && f.firstEligibleLotNumber) {
            const gap = days(f.firstEligibleExpiry, r.lot?.expiryDate);
            headline = t('Expiry.What.Fefo', gap ?? '—');
            pairs = [[t('Expiry.What.UsedLot'), lotText(r.lot?.lotNumber, r.lot?.expiryDate)], [t('Expiry.What.FirstLot'), lotText(f.firstEligibleLotNumber, f.firstEligibleExpiry)],
                [t('Expiry.What.Gap'), gap == null ? null : esc(t('Expiry.Common.Days', gap))], [t('Expiry.Detail.Quantity'), qtyText(r)],
                [t('Expiry.What.BypassReason'), f.bypassReason ? esc(f.bypassReason) : null]];
        } else if (r.type === 'BELOW_MIN_REMAINING' && f.requiredDays) {
            const missing = Number(f.requiredDays) - Number(f.remainingDays);
            headline = t('Expiry.What.BelowMin', missing);
            pairs = [[t('Expiry.Col.Expiry'), esc(ui.date(r.lot?.expiryDate))], [t('Expiry.What.Remaining'), esc(t('Expiry.Common.Days', f.remainingDays))],
                [t('Expiry.What.Required'), esc(t('Expiry.Common.Days', f.requiredDays))], [t('Expiry.What.Missing'), esc(t('Expiry.Common.Days', missing))],
                [t('Expiry.Detail.Quantity'), qtyText(r)]];
        } else if (r.type === 'EXPIRY_DATE_OVERRIDE' && (f.suggestedExpiry || f.suggestedRetest)) {
            const expiry = f.suggestedExpiry && f.suggestedExpiry !== f.enteredExpiry;
            const field = expiry ? t('Expiry.What.FieldExpiry') : t('Expiry.What.FieldRetest');
            const sug = expiry ? f.suggestedExpiry : f.suggestedRetest;
            const ent = expiry ? f.enteredExpiry : f.enteredRetest;
            headline = t('Expiry.What.Override', field, ui.date(sug) || '—', ui.date(ent) || '—');
            pairs = [[t('Expiry.What.Field'), esc(field)], [t('Expiry.What.Suggested'), esc(t('Expiry.What.SuggestedRule', ui.date(sug) || '—'))],
                [t('Expiry.What.Entered'), esc(ui.date(ent) || '—')], [t('Expiry.What.Gap'), days(sug, ent) == null ? null : esc(t('Expiry.Common.Days', days(sug, ent)))],
                [t('Expiry.What.UserReason'), f.reason ? esc(f.reason) : null], [t('Expiry.Col.Signature'), f.signatureId ? `<span class="font-monospace small">${esc(f.signatureId)}</span>` : null]];
        } else if (r.type === 'EXPIRED_ON_HAND' && f.daysSinceExpiry) {
            headline = t('Expiry.What.ExpiredOnHand', f.daysSinceExpiry);
            pairs = [[t('Expiry.Col.Expiry'), esc(ui.date(r.lot?.expiryDate))], [t('Expiry.Col.SinceExpiry'), esc(t('Expiry.Common.Days', f.daysSinceExpiry))],
                [t('Expiry.Detail.Quantity'), qtyText(r)]];
        } else {
            headline = t('Expiry.What.Manual');
            pairs = [[t('Expiry.What.OpenedBy'), r.openedBy ? esc(ui.person(r.openedBy)) : esc(t('Expiry.Common.System'))], [t('Expiry.Modal.Description'), r.description ? esc(r.description) : null]];
        }

        box.textContent = headline;
        ui.byId('ex-d-what').innerHTML = dl(pairs);
    };

    const DOT = { Opened: 'bg-primary', Taken: 'bg-info', Commented: 'bg-secondary', Resolved: 'bg-success', Closed: 'bg-secondary', Cancelled: 'bg-secondary', SlaBreached: 'bg-danger' };
    const timeline = (r) => {
        ui.byId('ex-d-timeline').innerHTML = (r.events || []).map((e) => {
            const title = e.action === 'Resolved' && e.resolution ? ui.resolutionText(e.resolution) : t('Expiry.Event.' + e.action);
            const chip = e.signatureId ? `<div class="mt-1"><span class="badge bg-label-success"><i class="bx bx-pen me-1"></i>${esc(t('Expiry.Detail.Signed'))} · <span class="font-monospace">${esc(e.signatureId)}</span></span></div>` : '';
            return `<li class="timeline-item timeline-item-transparent"><span class="timeline-point ${DOT[e.action] || 'bg-secondary'}"></span>`
                + `<div class="timeline-event"><div class="timeline-header"><h6 class="mb-0">${esc(title)}</h6></div>`
                + `<small class="text-muted">${esc(ui.dateTime(e.atUtc))} · ${esc(e.actor ? ui.person(e.actor) : t('Expiry.Common.System'))}</small>`
                + (e.text ? `<p class="mb-0 mt-1">${esc(e.text)}</p>` : '') + chip + '</div></li>';
        }).join('');
    };

    const show = (el, on) => el?.classList.toggle('d-none', !on);

    const decisionCard = (r) => {
        const offer = decision.offers(r.status);
        const decided = !!r.resolution;
        show(ui.byId('ex-d-decided'), decided);
        show(ui.byId('ex-d-pending'), !decided && offer.decide);
        if (decided) {
            ui.byId('ex-d-decided').innerHTML = `<div class="small text-muted mb-1">${esc(t('Expiry.Detail.ResolutionLabel'))}</div>`
                + `<div class="fw-medium mb-1">${esc(ui.resolutionText(r.resolution))}</div>`
                + (r.resolutionReason ? `<p class="mb-2">"${esc(r.resolutionReason)}"</p>` : '')
                + `<small class="text-muted d-block">${esc(r.resolvedBy ? ui.person(r.resolvedBy) : '')} · ${esc(ui.dateTime(r.resolvedAtUtc))}</small>`
                + (r.resolutionSignatureId ? `<span class="badge bg-label-success mt-2"><i class="bx bx-pen me-1"></i>${esc(t('Expiry.Detail.Signed'))} · <span class="font-monospace">${esc(r.resolutionSignatureId)}</span></span>` : '');
        }

        ui.byId('ex-d-decision-help') && (ui.byId('ex-d-decision-help').textContent = t('Expiry.Detail.DecisionHelp'));
        show(ui.byId('ex-d-take'), offer.take && ui.can('EXPIRY_EXCEPTION_TAKE'));
        show(ui.byId('ex-d-decide'), offer.decide && ui.can('EXPIRY_EXCEPTION_RESOLVE'));
        show(ui.byId('ex-d-close'), offer.close && ui.can('EXPIRY_EXCEPTION_CLOSE'));
    };

    const render = (r) => {
        state.record = r;
        ui.byId('ex-d-number').textContent = r.number;
        ui.byId('ex-d-type').innerHTML = ui.typeBadge(r.type);
        ui.byId('ex-d-status').innerHTML = ui.caseStatusBadge(r.status);
        ui.byId('ex-d-sla').innerHTML = ui.slaCell(r);
        ui.byId('ex-d-facts').innerHTML = facts(r);
        const lotLink = ui.byId('ex-d-lot-link');
        if (lotLink) lotLink.href = `/Lots/Details?${new URLSearchParams({ lotId: r.lot?.lotId || '' })}`;
        what(r);
        timeline(r);
        decisionCard(r);
        show(ui.byId('ex-d-comment'), !['Closed', 'Cancelled'].includes(r.status));
        show(ui.byId('ex-d-body'), true);
        show(ui.byId('ex-d-loading'), false);
    };

    const load = async () => {
        const reply = await ui.get(`/Expiry/data/exception/${encodeURIComponent(id)}`);
        if (!reply.ok) {
            show(ui.byId('ex-d-loading'), false);
            ui.showError(ui.byId('ex-d-error'), reply);
            return;
        }
        ui.hideError(ui.byId('ex-d-error'));
        render(reply.json.exception);
    };

    const afterWrite = async (result, toast) => {
        if (!result.ok) {
            ui.showError(ui.byId('ex-d-error'), result.reply);
            return false;
        }
        if (toast) window.showToast?.(toast, 'success');
        await load();
        return true;
    };

    // ── the drawer (two steps) ────────────────────────────────────────────────────────────────────────────────────
    const drawerEl = () => ui.byId('ex-drawer');
    const drawerError = () => ui.byId('ex-drawer-error');
    const selected = () => document.querySelector('input[name="ex-drawer-resolution"]:checked')?.value || '';
    const reasonText = () => ui.byId('ex-drawer-reason')?.value || '';
    const counter = () => {
        const n = reasonText().trim().length;
        const el = ui.byId('ex-drawer-counter');
        if (!el) return;
        el.textContent = t('Expiry.Drawer.Counter', n, decision.MIN_REASON);
        el.classList.toggle('text-success', n >= decision.MIN_REASON);
    };
    const step = (n) => {
        document.querySelectorAll('[data-ex-step]').forEach((li) => {
            const on = Number(li.getAttribute('data-ex-step')) <= n;
            li.classList.toggle('text-muted', !on);
            const dot = li.querySelector('[data-ex-step-dot]');
            dot?.classList.toggle('bg-primary', on);
            dot?.classList.toggle('bg-label-secondary', !on);
        });
        show(ui.byId('ex-drawer-step1'), n === 1);
        show(ui.byId('ex-drawer-step2'), n === 2);
        show(ui.byId('ex-drawer-next'), n === 1);
        show(ui.byId('ex-drawer-sign'), n === 2);
        show(ui.byId('ex-drawer-back'), n === 2);
    };
    const openDrawer = () => {
        const r = state.record;
        if (!r || !drawerEl() || !window.bootstrap) return;
        state.prepared = null;
        ui.byId('ex-drawer-number').textContent = r.number;
        ui.byId('ex-drawer-subject').textContent = [t('Expiry.Type.' + r.type), r.lot?.lotNumber, r.warehouseLabel].filter(Boolean).join(' · ');
        document.querySelectorAll('input[name="ex-drawer-resolution"]').forEach((i) => { i.checked = false; });
        ui.byId('ex-drawer-reason').value = '';
        drawerEl().querySelectorAll('[data-err]').forEach((e) => { e.textContent = ''; });
        ui.hideError(drawerError());
        show(ui.byId('ex-drawer-skipped'), false);
        counter();
        step(1);
        window.bootstrap.Offcanvas.getOrCreateInstance(drawerEl()).show();
    };
    const finish = async (result) => {
        if (!result.ok) {
            if (result.cancelled) { ui.showError(drawerError(), t('Expiry.Drawer.SignCancelled')); return; }
            ui.showError(drawerError(), result.reply);
            return;
        }
        window.bootstrap.Offcanvas.getOrCreateInstance(drawerEl()).hide();
        const label = ui.resolutionText(selected());
        window.showToast?.(result.signatureId ? t('Expiry.Drawer.SavedSigned', state.record.number, label) : t('Expiry.Drawer.Saved', state.record.number, label), 'success');
        await load();
    };
    const next = async () => {
        ui.hideError(drawerError());
        drawerEl().querySelectorAll('[data-err]').forEach((e) => { e.textContent = ''; });
        const input = { exceptionId: id, version: state.record.version, resolution: selected(), reason: reasonText() };
        const first = await decision.prepare(deps, input);
        if (!first.ok) {
            if (first.errors) Object.entries(first.errors).forEach(([k, key]) => { drawerEl().querySelector(`[data-err="${k}"]`).textContent = t(key); });
            else ui.showError(drawerError(), first.reply);
            return;
        }
        state.prepared = first.prepared;
        if (!first.signatureRequired) {
            // the company's MOD-0022 policy asks for no signature: the step is skipped and said so
            const skipped = ui.byId('ex-drawer-skipped');
            skipped.textContent = t('Expiry.Drawer.SignSkipped');
            show(skipped, true);
            await finish(await decision.signAndResolve(deps, state.prepared, input));
            return;
        }
        ui.byId('ex-drawer-summary').textContent = `${ui.resolutionText(input.resolution)} · ${state.record.number}`;
        ui.byId('ex-drawer-summary-reason').textContent = t('Expiry.Drawer.SummaryReason', input.reason.trim());
        step(2);
    };
    const sign = async () => {
        ui.hideError(drawerError());
        if (!window.ESign?.open) { ui.showError(drawerError(), t('Expiry.Errors.ESIGN_UNAVAILABLE')); return; }
        await finish(await decision.signAndResolve(deps, state.prepared, { exceptionId: id, resolution: selected(), reason: reasonText() }));
    };

    document.addEventListener('DOMContentLoaded', async () => {
        if (!root || !id) {
            ui.showError(ui.byId('ex-d-error'), t('Expiry.Errors.EXPIRY_EXCEPTION_NOT_FOUND'));
            show(ui.byId('ex-d-loading'), false);
            return;
        }
        ui.byId('ex-d-take')?.addEventListener('click', async () =>
            afterWrite(await decision.take(deps, { exceptionId: id, version: state.record.version }), t('Expiry.Detail.Taken', state.record.number)));
        ui.byId('ex-d-close')?.addEventListener('click', async () =>
            afterWrite(await decision.close(deps, { exceptionId: id, version: state.record.version }), t('Expiry.Detail.Closed', state.record.number)));
        const comment = ui.byId('ex-d-comment-text');
        comment?.addEventListener('input', () => { ui.byId('ex-d-comment-send').disabled = comment.value.trim().length === 0; });
        ui.byId('ex-d-comment-send')?.addEventListener('click', async () => {
            if (await afterWrite(await decision.comment(deps, { exceptionId: id, text: comment.value }), null)) {
                comment.value = '';
                ui.byId('ex-d-comment-send').disabled = true;
            }
        });
        ui.byId('ex-d-decide')?.addEventListener('click', openDrawer);
        ui.byId('ex-drawer-reason')?.addEventListener('input', counter);
        ui.byId('ex-drawer-next')?.addEventListener('click', next);
        ui.byId('ex-drawer-sign')?.addEventListener('click', sign);
        ui.byId('ex-drawer-back')?.addEventListener('click', () => step(1));
        await load();
    });
})();
