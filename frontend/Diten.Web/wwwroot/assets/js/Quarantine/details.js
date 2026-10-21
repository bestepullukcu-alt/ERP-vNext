/**
 * MOD-0175 Q5 — Karantina Kaydı Detayı (pack §11.3). One call (/Quarantine/data/hold/{id}) fills the mockup sections and the
 * new "İş akışı" box (§21.3 #10). Toolbar / box buttons are pack §14 action codes, each shown only when QuarantineUi.can(code)
 * AND the server's state allows it:
 *   STARTREVIEW (İncelemeye al — open hold, Waiting) · DECIDE (open hold, no decision in flight) · REFRESHWORKFLOW (a decision
 *   waiting for / just past approval) · WITHDRAWDECISION (own decision, PendingApproval) · REAPPLYDECISION (ApplyFailed) ·
 *   SUBMITDECISION link to Karar Gönder (own Draft / StartFailed decision).
 * WP-0174-ITEM: "Notu düzenle" (updateHold, PATCH with the page ETag — FU-0175-Q5-01) for a holder of quarantine.holds.review
 * (the STARTREVIEW code carries that key) while the hold is open; people are shown by name (FU-0175-Q5-02, QuarantineUi.person).
 * Every answer of the service is shown in the reader's language (Quarantine.Errors.{CODE}).
 */
'use strict';

(function () {
    const ui = window.QuarantineUi;
    const { t, esc, qty, date, dateTime } = ui;
    const root = ui.byId('qr-details');
    if (!root) return;
    const holdId = root.dataset.holdId;
    let state = null;

    const set = (name, html) => {
        const el = root.querySelector(`[data-f="${name}"]`);
        if (el) el.innerHTML = html;
    };
    const show = (el, on) => el?.classList.toggle('d-none', !on);

    const HISTORY_DOT = {
        Quarantined: 'warning', ReviewStarted: 'primary', DecisionSubmitted: 'info', Approved: 'success', Applied: 'success',
        Rejected: 'danger', Cancelled: 'secondary', TimedOut: 'secondary', Withdrawn: 'secondary', ApplyFailed: 'danger',
        FourEyesViolation: 'danger', Blocked: 'danger', ReviewCompleted: 'primary', Disposed: 'info', Unblocked: 'success', SampleRetained: 'primary',
        NoteUpdated: 'secondary'
    };
    const signatureChip = (label, id) => id
        ? `<span class="badge bg-label-primary d-inline-flex align-items-center gap-1 mt-1" title="${esc(id)}"><i class="bx bx-pen"></i>${esc(t(label, String(id).slice(0, 8)))}</span>`
        : '';

    const renderHistory = (history) => {
        const list = ui.byId('qr-d-history');
        if (!history?.length) {
            list.innerHTML = `<li class="text-muted small">${esc(t('Quarantine.Details.History.Empty'))}</li>`;
            return;
        }
        list.innerHTML = history.slice().reverse().map((h) => `
            <li class="timeline-item timeline-item-transparent">
                <span class="timeline-point timeline-point-${HISTORY_DOT[h.action] || 'secondary'}"></span>
                <div class="timeline-event pb-3">
                    <div class="timeline-header mb-1"><h6 class="mb-0">${esc(t('Quarantine.History.' + h.action) || h.action)}</h6><small class="text-muted">${esc(dateTime(h.atUtc))}</small></div>
                    <div class="small text-muted">${ui.personHtml(h.by)}</div>
                    ${h.reason ? `<div class="small mt-1">“${esc(h.reason)}”</div>` : ''}
                    ${h.noteChange ? `<div class="small mt-1">${esc(t('Quarantine.Details.History.NoteChange', h.noteChange.previousNoteLength, h.noteChange.noteLength, h.noteChange.previousAttachmentCount, h.noteChange.attachmentCount))}</div>` : ''}
                    <div class="d-flex flex-wrap gap-1">${signatureChip('Quarantine.Details.History.Signature', h.signatureId)}${signatureChip('Quarantine.Details.History.ApproverSignature', h.approverSignatureId)}</div>
                </div>
            </li>`).join('');
    };

    const renderMoves = (moves) => {
        const body = root.querySelector('#qr-d-moves tbody');
        if (!moves?.length) {
            body.innerHTML = `<tr><td colspan="6" class="text-muted small">${esc(t('Quarantine.Details.Moves.Empty'))}</td></tr>`;
            return;
        }
        body.innerHTML = moves.map((m) => `<tr>
            <td class="font-monospace small">${esc(m.transactionNumber)}</td>
            <td class="small">${esc(dateTime(m.atUtc))}</td>
            <td class="small">${esc(t('Quarantine.MovementType.' + m.movementType) || m.movementType)}</td>
            <td>${m.fromStockStatus || m.toStockStatus ? `${ui.stockBadge(m.fromStockStatus)} <i class="bx bx-right-arrow-alt text-muted"></i> ${ui.stockBadge(m.toStockStatus)}` : '—'}</td>
            <td class="text-end" dir="ltr">${esc(qty(m.quantity))}</td>
            <td class="small">${esc(m.byUserName || t('Quarantine.Details.Moves.UnknownUser'))}</td>
        </tr>`).join('');
    };

    const renderWorkflow = (hold, decision, me) => {
        const body = ui.byId('qr-wf-body');
        const stateBox = ui.byId('qr-wf-state');
        const refresh = ui.byId('qr-wf-refresh');
        const submit = ui.byId('qr-wf-submit');
        const withdraw = ui.byId('qr-wf-withdraw');
        const reapply = ui.byId('qr-wf-reapply');
        [refresh, submit, withdraw, reapply].forEach((b) => show(b, false));
        if (!decision) {
            stateBox.innerHTML = '';
            const last = (hold.decisions || []).slice(-1)[0];
            body.innerHTML = `<div class="text-muted">${esc(t('Quarantine.Workflow.NoDecision'))}</div>`
                + (last ? `<div class="mt-2">${esc(t('Quarantine.Workflow.LastDecision'))}: <span class="font-monospace">${esc(last.decisionNumber)}</span> ${ui.decisionBadge(last.state)}</div>` : '');
            return;
        }
        stateBox.innerHTML = ui.decisionBadge(decision.state);
        const wf = decision.workflow || {};
        const rows = [
            [t('Quarantine.Workflow.Decision'), `<span class="font-monospace">${esc(decision.decisionNumber)}</span> · ${esc(t('Quarantine.DecisionKind.' + decision.kind))}`],
            [t('Quarantine.Workflow.Decider'), ui.personHtml(decision.decider)],
            [t('Quarantine.Workflow.Approver'), wf.approver?.userId ? ui.personHtml(wf.approver) : '—'],
            [t('Quarantine.Workflow.Instance'), wf.instanceId ? `<span class="font-monospace small">${esc(String(wf.instanceId).slice(0, 8))}</span>` : '—']
        ];
        if (decision.deciderSignatureId) rows.push([t('Quarantine.Workflow.DeciderSignature'), signatureChip('Quarantine.Details.History.Signature', decision.deciderSignatureId)]);
        if (wf.approverSignatureId) rows.push([t('Quarantine.Workflow.ApproverSignature'), signatureChip('Quarantine.Details.History.ApproverSignature', wf.approverSignatureId)]);
        body.innerHTML = rows.map(([k, v]) => `<div class="d-flex justify-content-between gap-2 mb-1"><span class="text-muted">${esc(k)}</span><span class="text-end">${v}</span></div>`).join('')
            + (decision.error ? `<div class="alert alert-danger py-2 small mt-2 mb-0">${esc(t('Quarantine.Errors.' + decision.error.code) || decision.error.message || decision.error.code)}</div>` : '');
        const mine = me && decision.decider?.userId && me.toLowerCase() === decision.decider.userId.toLowerCase();
        show(refresh, ui.can('REFRESHWORKFLOW') && ['Submitted', 'PendingApproval', 'Approved'].includes(decision.state));
        show(withdraw, !!withdraw && ui.can('WITHDRAWDECISION') && mine && decision.state === 'PendingApproval');
        show(reapply, !!reapply && ui.can('REAPPLYDECISION') && decision.state === 'ApplyFailed');
        if (submit && ui.can('SUBMITDECISION') && mine && ['Draft', 'StartFailed'].includes(decision.state)) {
            submit.href = `/Quarantine/Decisions/${encodeURIComponent(decision.decisionId)}/Submit`;
            show(submit, true);
        }
    };

    const render = (data) => {
        state = data;
        const h = data.hold;
        const unit = ui.uom(h);
        ui.byId('qr-d-name').textContent = h.itemLabel || h.item?.itemId || '';
        ui.byId('qr-d-stock').innerHTML = ui.stockBadge(h.stockStatus);
        ui.byId('qr-d-state').innerHTML = ui.holdBadge(h);
        ui.byId('qr-d-sub').textContent = [h.skuLabel, ui.levelLabel(h.item?.skuLevel), h.holdNumber].filter(Boolean).join(' · ');
        const crumb = ui.byId('qr-crumb');
        if (crumb) crumb.textContent = `${t('Quarantine.Tabs.Queue')} / ${h.holdNumber}`;
        const lotButton = ui.byId('qr-d-lot');
        if (h.lot?.lotId && /^[0-9a-f-]{36}$/i.test(h.lot.lotId)) {
            lotButton.href = ui.lotUrl(h.lot.lotId);
            show(lotButton, true);
        }
        const open = h.status !== 'Decided';
        show(ui.byId('qr-d-decide'), ui.can('DECIDE') && open && !h.activeDecision);
        show(ui.byId('qr-d-review'), ui.can('STARTREVIEW') && open && h.reviewState === 'Waiting' && !h.activeDecision);
        // QUARANTINE 1.2.0 (FU-0175-ITEM-03): a submitted decision locks the note until it ends — the explanation replaces the button.
        const noteLocked = !!h.activeDecision;
        show(ui.byId('qr-d-note-edit'), ui.can('STARTREVIEW') && open && !noteLocked);
        show(ui.byId('qr-d-note-locked'), ui.can('STARTREVIEW') && open && noteLocked);
        closeNoteEditor();
        const late = ui.byId('qr-d-late');
        show(late, !!h.late && open);
        if (h.late) late.querySelector('span').textContent = t('Quarantine.Details.LateAlert', h.waitDays, h.targetDays);

        const days = ui.daysUntil(h.lot?.expiryDate);
        set('lot', ui.lotLink(h.lot));
        set('expiry', h.lot?.expiryDate ? `${esc(date(h.lot.expiryDate))}${days != null && days <= 90 ? ` <small class="text-danger">· ${esc(t('Quarantine.Common.DaysLeft', Math.max(0, days)))}</small>` : ''}` : '—');
        set('quantity', `<span dir="ltr">${esc(qty(h.quantity))} ${esc(unit)}</span>`);
        set('where', `${esc(h.warehouseLabel || '')}<small class="d-block text-muted">${esc(h.locationLabel || '')}</small>`);
        set('wait', `<span class="${h.late ? 'text-warning' : ''}">${esc(t('Quarantine.Common.Days', h.waitDays))}</span><small class="d-block text-muted">${esc(t('Quarantine.Common.Target', h.targetDays))}</small>`);
        set('source', `${esc(t('Quarantine.Source.' + h.source) || h.source)}<small class="d-block text-muted font-monospace">${esc(h.sourceDocument?.number || h.sourceDocument?.text || '')}</small>`);
        window.RecallBands?.holdSource(null, h); // WP-0177-R7c: "Kaynak: Geri çağırma RC-…" (link only with recall.cases.read)
        set('createdBy', `${ui.personHtml(h.createdBy)}<small class="d-block text-muted">${esc(dateTime(h.enteredAtUtc))}</small>`);
        set('coa', esc(h.lot?.coaReference || '—'));
        set('lotStatus', h.lot?.lotStatus ? `<span class="badge bg-label-secondary">${esc(t('Quarantine.LotStatus.' + h.lot.lotStatus) || h.lot.lotStatus)}</span>` : '—');
        set('note', h.analysisNote ? esc(h.analysisNote) : `<span class="text-muted">${esc(t('Quarantine.Details.Quality.NoNote'))}</span>`);
        set('attachments', (h.attachments || []).length
            ? h.attachments.map((a) => `<span class="badge bg-label-secondary"><i class="bx bx-paperclip me-1"></i>${esc(a.name)} · <span class="font-monospace">${esc(a.dmsRef)}</span></span>`).join('')
            : '<span class="text-muted">—</span>');
        renderMoves(h.movements);
        renderHistory(h.history);
        renderWorkflow(h, data.decision, data.me);
    };

    const load = async () => {
        const reply = await ui.get(`/Quarantine/data/hold/${encodeURIComponent(holdId)}`);
        if (!reply.ok) {
            ui.showError(ui.byId('qr-page-error'), reply);
            return;
        }
        ui.hideError(ui.byId('qr-page-error'));
        render(reply.json);
    };

    const failed = (title, reply) => window.DitenModal?.error({ title, message: ui.errorText(reply) });

    // ── analysis note (updateHold) ──
    const noteEditor = () => ui.byId('qr-d-note-editor');
    function closeNoteEditor() {
        show(noteEditor(), false);
        show(root.querySelector('[data-f="note"]'), true);
    }

    ui.byId('qr-d-note-edit')?.addEventListener('click', () => {
        const input = ui.byId('qr-d-note-input');
        input.value = state?.hold?.analysisNote || '';
        show(root.querySelector('[data-f="note"]'), false);
        show(noteEditor(), true);
        input.focus();
    });
    ui.byId('qr-d-note-cancel')?.addEventListener('click', closeNoteEditor);
    ui.byId('qr-d-note-save')?.addEventListener('click', async (event) => {
        const button = event.currentTarget;
        button.disabled = true;
        const note = ui.byId('qr-d-note-input').value.trim();
        const reply = await ui.write('PATCH', `holds/${encodeURIComponent(holdId)}`, { analysisNote: note }, state.etag);
        button.disabled = false;
        if (!reply.ok) return failed(t('Quarantine.Details.Quality.EditNote'), reply);
        ui.toast(t('Quarantine.Details.Quality.NoteSaved'), 'success');
        await load();
    });

    ui.byId('qr-wf-refresh')?.addEventListener('click', async (event) => {
        event.currentTarget.disabled = true;
        const reply = await ui.write('POST', `decisions/${encodeURIComponent(state.decision.decisionId)}/refresh`);
        event.currentTarget.disabled = false;
        if (!reply.ok) return failed(t('Quarantine.Workflow.Refresh'), reply);
        ui.toast(t('Quarantine.Workflow.Refreshed', t('Quarantine.Workflow.' + reply.json?.state) || reply.json?.state || ''), 'success');
        await load();
    });

    ui.byId('qr-wf-reapply')?.addEventListener('click', () => {
        window.showConfirm?.(t('Quarantine.Workflow.Reapply'), async () => {
            const reply = await ui.write('POST', `decisions/${encodeURIComponent(state.decision.decisionId)}/apply`);
            if (!reply.ok) return failed(t('Quarantine.Workflow.Reapply'), reply);
            ui.toast(t('Quarantine.Workflow.Reapplied'), 'success');
            await load();
        }, { type: 'warning', subtext: t('Quarantine.Workflow.ReapplyText'), confirmButtonText: t('Quarantine.Workflow.Reapply'), cancelButtonText: t('Quarantine.Common.Cancel') });
    });

    ui.byId('qr-wf-withdraw')?.addEventListener('click', () => {
        window.showConfirm?.(t('Quarantine.Workflow.WithdrawTitle'), async (reason) => {
            const reply = await ui.write('POST', `decisions/${encodeURIComponent(state.decision.decisionId)}/withdraw`, { reason: (reason || '').trim() });
            if (!reply.ok) return failed(t('Quarantine.Workflow.Withdraw'), reply);
            ui.toast(t('Quarantine.Workflow.WithdrawDone'), 'success');
            await load();
        }, {
            type: 'warning',
            subtext: t('Quarantine.Workflow.WithdrawText'),
            confirmButtonText: t('Quarantine.Workflow.Withdraw'),
            cancelButtonText: t('Quarantine.Common.Cancel'),
            showInput: true,
            inputType: 'textarea',
            inputLabel: t('Quarantine.Field.Reason'),
            inputAttributes: { maxlength: 1000 },
            inputValidator: (value) => ((value || '').trim().length < 10 ? t('Quarantine.Common.ReasonMin') : null)
        });
    });

    ui.byId('qr-d-review')?.addEventListener('click', () => {
        window.showConfirm?.(t('Quarantine.Details.StartReview'), async (note) => {
            const reply = await ui.write('POST', `holds/${encodeURIComponent(holdId)}/review`, { note: (note || '').trim() || undefined }, state.etag);
            if (!reply.ok) return failed(t('Quarantine.Details.StartReview'), reply);
            ui.toast(t('Quarantine.Details.ReviewStarted'), 'success');
            await load();
        }, {
            type: 'info',
            subtext: t('Quarantine.Details.StartReviewText'),
            confirmButtonText: t('Quarantine.Details.StartReview'),
            cancelButtonText: t('Quarantine.Common.Cancel'),
            showInput: true,
            inputType: 'textarea',
            inputLabel: t('Quarantine.Details.ReviewNote'),
            inputAttributes: { maxlength: 2000 }
        });
    });

    document.addEventListener('DOMContentLoaded', () => {
        ui.loadCounts(null);
        load();
    });
})();
