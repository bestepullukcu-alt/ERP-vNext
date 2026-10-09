/**
 * MOD-0175 Q5 — Karar Gönder (pack §11.1 DECISIONSUBMIT). A DRAFT made elsewhere — the KQ-I lot release (TRACE 202,
 * pendingRequestId = this decision) or a StartFailed decision — is signed and sent here: the server's decision as summary +
 * MOD-0022 dialog (QuarantineUi.signAndSubmit) → submitDecision → 202 "Onay bekliyor". Only the decider may send it
 * (QUARANTINE_NOT_DECIDER); a decision that left Draft / StartFailed is shown with its state and nothing to press.
 */
'use strict';

(function () {
    const ui = window.QuarantineUi;
    const { t, esc } = ui;
    const root = ui.byId('qr-submit');
    if (!root) return;
    const decisionId = root.dataset.decisionId;
    let data = null;

    const back = () => {
        const d = data.decision;
        if (d.holdId) return `/Quarantine/Details/${encodeURIComponent(d.holdId)}`;
        if (d.blockedStockId) return '/Quarantine/Blocked';
        return d.lotId ? ui.lotUrl(d.lotId) : '/Quarantine';
    };

    const render = async () => {
        const d = data.decision;
        const record = data.record;
        ui.byId('qr-p-title').textContent = t('Quarantine.Submit.Title', d.decisionNumber);
        ui.byId('qr-p-name').textContent = record?.itemLabel || (data.lot ? `${t('Quarantine.Col.Lot')} ${data.lot.lotNumber}` : '');
        ui.byId('qr-p-sub').innerHTML = `${esc(t('Quarantine.DecisionKind.' + d.kind))} · ${ui.decisionBadge(d.state)}`;
        ui.byId('qr-cancel').href = back();
        const decision = Object.assign({}, d, { lotNumber: data.lot?.lotNumber });
        const required = await ui.showSignStep(decision, record);
        const primary = ui.byId('qr-primary');
        if (!['Draft', 'StartFailed'].includes(d.state)) {
            ui.showError(ui.byId('qr-page-error'), t('Quarantine.Submit.NotDraft', t('Quarantine.Workflow.' + d.state) || d.state));
            ui.byId('qr-p-intro').classList.add('d-none');
            return;
        }
        primary.innerHTML = `<i class="bx ${required ? 'bx-pen' : 'bx-send'} me-1"></i>${esc(required ? t('Quarantine.Decide.Button.SignSend') : t('Quarantine.Decide.Button.Send'))}`;
        primary.classList.toggle('d-none', !ui.can('SUBMITDECISION'));
    };

    const send = async () => {
        const primary = ui.byId('qr-primary');
        ui.hideError(ui.byId('qr-form-error'));
        primary.disabled = true;
        const sent = await ui.signAndSubmit(data.decision);
        primary.disabled = false;
        if (sent.cancelled) {
            ui.toast(t('Quarantine.Sign.Cancelled'), 'warning');
            return;
        }
        if (!sent.ok) {
            ui.showError(ui.byId('qr-form-error'), sent.reply);
            return;
        }
        await ui.announceSent(sent.decision, back());
    };

    document.addEventListener('DOMContentLoaded', async () => {
        ui.loadCounts(null);
        const reply = await ui.get(`/Quarantine/data/decision/${encodeURIComponent(decisionId)}`);
        if (!reply.ok) {
            ui.showError(ui.byId('qr-page-error'), reply);
            return;
        }
        data = reply.json;
        await render();
        ui.byId('qr-primary').addEventListener('click', send);
    });
})();
