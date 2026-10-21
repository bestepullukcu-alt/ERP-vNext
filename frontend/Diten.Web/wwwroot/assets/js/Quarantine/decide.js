/**
 * MOD-0175 Q5 — Karar Ver (pack §11.3, §12 V1–V9 mirrored for the reader; the SERVER decides). Step 1 builds the request
 * (single card or "Kısmi karar" two lines — line 2 auto-fills the remainder until touched, the sum turns green when equal, KQ-A);
 * "İleri: Elektronik imza" → createDecision (a DRAFT: nothing moves; the same body replays the same key, a changed body after
 * "Geri" gets a new key — a draft does not lock the hold). Step 2 shows the server's draft and signs + sends it through
 * QuarantineUi.signAndSubmit (MOD-0022 dialog; policy off → sent unsigned). 202 → "Onaya gönderildi" → the record page.
 */
'use strict';

(function () {
    const ui = window.QuarantineUi;
    const { t, esc, qty } = ui;
    const root = ui.byId('qr-decide');
    if (!root) return;
    const holdId = root.dataset.holdId;
    const form = ui.byId('qr-decide-form');
    const lang = (document.documentElement.lang || 'tr').slice(0, 2);
    let hold = null;
    let draft = null;
    let step = 1;
    let line2Touched = false;
    const keys = new Map();

    const show = (el, on) => el?.classList.toggle('d-none', !on);
    const partial = () => ui.byId('qr-partial').checked;
    const singleDecision = () => form.querySelector('input[name="qr-decision"]:checked')?.value || '';
    const lineEls = () => [...form.querySelectorAll('[data-line]')];
    const lines = () => partial()
        ? lineEls().map((el, i) => ({ lineNo: i + 1, decision: el.querySelector('[data-line-decision]').value, quantity: Number(el.querySelector('[data-line-qty]').value) }))
        : (singleDecision() ? [{ lineNo: 1, decision: singleDecision(), quantity: Number(hold.quantity) }] : []);
    const decisions = () => lines().map((l) => l.decision).filter(Boolean);
    const retestNeeded = () => hold && (hold.source === 'Retest' || (hold.lot?.retestDate && ui.daysUntil(hold.lot.retestDate) <= 0));

    const refreshForm = () => {
        const d = decisions();
        show(ui.byId('qr-single'), !partial());
        show(ui.byId('qr-lines'), partial());
        show(ui.byId('qr-blockreason-wrap'), d.some((x) => x === 'Reject' || x === 'Block'));
        show(ui.byId('qr-coa-wrap'), d.includes('Release'));
        show(ui.byId('qr-retest-wrap'), d.includes('Release') && retestNeeded());
        form.querySelectorAll('.qr-option').forEach((label) => {
            const on = label.querySelector('input').checked;
            label.classList.toggle('border-primary', on);
            label.classList.toggle('bg-label-primary', on);
        });
        if (partial()) {
            const sum = lines().reduce((s, l) => s + (Number.isFinite(l.quantity) ? l.quantity : 0), 0);
            const out = ui.byId('qr-sum');
            out.textContent = `${qty(sum)} / ${qty(hold.quantity)} ${ui.uom(hold)}`;
            out.classList.toggle('text-success', Math.abs(sum - Number(hold.quantity)) < 1e-9);
        }
    };

    const autoFill = () => {
        if (line2Touched || !partial()) return;
        const [first, second] = lineEls();
        const q1 = Number(first.querySelector('[data-line-qty]').value);
        if (Number.isFinite(q1) && q1 > 0 && q1 < Number(hold.quantity)) second.querySelector('[data-line-qty]').value = String(Number(hold.quantity) - q1);
    };

    /** The reader's copy of V1–V5 + V9 presence; the server re-checks everything (its refusal is shown the same way). */
    const validate = () => {
        ui.clearFieldErrors(form);
        let ok = true;
        const l = lines();
        if (!partial() && !singleDecision()) { ui.fieldError(form, 'decision', t('Quarantine.Decide.Err.Decision')); ok = false; }
        if (partial()) {
            const sum = l.reduce((s, x) => s + (Number.isFinite(x.quantity) ? x.quantity : 0), 0);
            const bad = l.some((x) => !x.decision || !(x.quantity > 0)) || l[0].decision === l[1].decision || Math.abs(sum - Number(hold.quantity)) > 1e-9;
            if (bad) { ui.fieldError(form, 'lines', t('Quarantine.Decide.Err.Lines')); ok = false; }
        }
        const d = decisions();
        if (d.some((x) => x === 'Reject' || x === 'Block') && !ui.byId('qr-blockreason').value) { ui.fieldError(form, 'blockReason', t('Quarantine.Decide.Err.BlockReason')); ok = false; }
        if (ui.byId('qr-reason').value.trim().length < 10) { ui.fieldError(form, 'reason', t('Quarantine.Common.ReasonMin')); ok = false; }
        if (d.includes('Release') && !ui.byId('qr-coa').checked) { ui.fieldError(form, 'coa', t('Quarantine.Decide.Err.Coa')); ok = false; }
        if (d.includes('Release') && retestNeeded() && !ui.byId('qr-retest').value) { ui.fieldError(form, 'retest', t('Quarantine.Decide.Err.Retest')); ok = false; }
        return ok;
    };

    const request = () => {
        const d = decisions();
        const body = {
            kind: 'HoldDecision',
            targetId: holdId,
            lines: lines(),
            reason: ui.byId('qr-reason').value.trim(),
            coaChecked: d.includes('Release') && ui.byId('qr-coa').checked
        };
        if (d.some((x) => x === 'Reject' || x === 'Block')) body.blockReasonCode = ui.byId('qr-blockreason').value;
        if (d.includes('Release') && retestNeeded()) body.newRetestDate = ui.byId('qr-retest').value;
        const fingerprint = JSON.stringify(body);
        if (!keys.has(fingerprint)) keys.set(fingerprint, ui.newKey());
        body.idempotencyKey = keys.get(fingerprint);
        return body;
    };

    const goStep = async (n) => {
        step = n;
        show(ui.byId('qr-step1'), n === 1);
        show(ui.byId('qr-sign-step'), n === 2);
        show(ui.byId('qr-back'), n === 2);
        const primary = ui.byId('qr-primary');
        if (n === 1) {
            primary.textContent = t('Quarantine.Decide.Button.Next');
            document.querySelectorAll('[data-qr-step]').forEach((s) => {
                const on = s.getAttribute('data-qr-step') === '1';
                s.classList.toggle('text-muted', !on);
                s.querySelector('[data-qr-step-dot]')?.classList.toggle('bg-primary', on);
                s.querySelector('[data-qr-step-dot]')?.classList.toggle('bg-label-secondary', !on);
            });
            ui.byId('qr-step-skipped')?.classList.add('d-none');
        } else {
            const required = await ui.showSignStep(draft, hold);
            primary.innerHTML = `<i class="bx ${required ? 'bx-pen' : 'bx-send'} me-1"></i>${esc(required ? t('Quarantine.Decide.Button.SignSend') : t('Quarantine.Decide.Button.Send'))}`;
        }
        window.scrollTo({ top: 0, behavior: 'smooth' });
    };

    const onSubmit = async (event) => {
        event.preventDefault();
        const primary = ui.byId('qr-primary');
        ui.hideError(ui.byId('qr-form-error'));
        if (step === 1) {
            if (!validate()) return;
            primary.disabled = true;
            const reply = await ui.write('POST', 'decisions', request());
            primary.disabled = false;
            if (!reply.ok) {
                ui.showError(ui.byId('qr-form-error'), reply);
                return;
            }
            draft = reply.json;
            goStep(2);
            return;
        }
        primary.disabled = true;
        const sent = await ui.signAndSubmit(draft);
        primary.disabled = false;
        if (sent.cancelled) {
            ui.toast(t('Quarantine.Sign.Cancelled'), 'warning');
            return;
        }
        if (!sent.ok) {
            ui.showError(ui.byId('qr-form-error'), sent.reply);
            return;
        }
        await ui.announceSent(sent.decision, `/Quarantine/Details/${encodeURIComponent(holdId)}`);
    };

    const render = (data) => {
        hold = data.hold;
        const unit = ui.uom(hold);
        ui.byId('qr-p-title').textContent = t('Quarantine.Decide.Title', hold.holdNumber);
        ui.byId('qr-p-name').textContent = hold.itemLabel || '';
        ui.byId('qr-p-sub').textContent = [hold.lot?.lotNumber, [hold.warehouseLabel, hold.locationLabel].filter(Boolean).join(' · '), `${qty(hold.quantity)} ${unit}`].filter(Boolean).join(' · ');
        form.querySelectorAll('[data-uom]').forEach((el) => { el.textContent = unit; });
        ui.byId('qr-coa-ref').textContent = hold.lot?.coaReference ? `· ${hold.lot.coaReference}` : '';
        window.RecallBands?.decisionFor(hold.lot?.lotId); // WP-0177-R7c (CT-R9): the release warning reads the lot's recall status
        const fourEyes = ui.byId('qr-foureyes');
        fourEyes.querySelector('span').textContent = data.settings?.fourEyes === false ? t('Quarantine.Decide.FourEyes.Off') : t('Quarantine.Decide.FourEyes.On');
        show(fourEyes, true);
        if (hold.status === 'Decided' || hold.activeDecision) {
            ui.showError(ui.byId('qr-page-error'), t('Quarantine.Decide.Closed', t('Quarantine.HoldState.' + ui.holdState(hold))));
            form.querySelectorAll('input, select, textarea, button[type="submit"]').forEach((el) => { el.disabled = true; });
        }
        refreshForm();
    };

    const loadReasons = async () => {
        const reply = await ui.get(ui.api('block-reasons?activeOnly=true'));
        const select = ui.byId('qr-blockreason');
        (reply.ok && Array.isArray(reply.json) ? reply.json : []).forEach((r) => select.add(new Option(r.names?.[lang] || r.names?.en || r.code, r.code)));
    };

    document.addEventListener('DOMContentLoaded', async () => {
        ui.loadCounts(null);
        ui.counter(ui.byId('qr-reason'));
        const reply = await ui.get(`/Quarantine/data/hold/${encodeURIComponent(holdId)}`);
        if (!reply.ok) {
            ui.showError(ui.byId('qr-page-error'), reply);
            form.classList.add('d-none');
            return;
        }
        render(reply.json);
        loadReasons();
        form.addEventListener('change', refreshForm);
        form.addEventListener('input', (event) => {
            if (event.target.matches('[data-line="2"] [data-line-qty]')) line2Touched = true;
            if (event.target.matches('[data-line="1"] [data-line-qty]')) autoFill();
            refreshForm();
        });
        ui.byId('qr-partial').addEventListener('change', () => { line2Touched = false; refreshForm(); });
        ui.byId('qr-back').addEventListener('click', () => goStep(1));
        form.addEventListener('submit', onSubmit);
    });
})();
