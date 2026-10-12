/**
 * MOD-0175 Q5 — Disposition (pack §11.3, §12 V10–V13 mirrored for the reader; the SERVER decides). Kinds offered = the record's
 * allowedActions (Dispose:Destroy / ReturnToSupplier / Rework / Sample — a lot-blocked lot offers Destroy only, CT-D4). The box
 * "Onaylanınca Stok Defteri'nde oluşacak hareket": İmha → SCRAP, İade → SUPPLIER_RETURN, Yeniden İşleme → STATUS_TRANSFER
 * Bloke → Karantina, Numune → LOCATION_TRANSFER to the warehouse's sample location — the status STAYS Bloke (§21.3 #4, KQ-E).
 * Step 1 → createDecision (draft, nothing moves) → step 2 = the server's plannedMovements + MOD-0022 signature → submitDecision.
 */
'use strict';

(function () {
    const ui = window.QuarantineUi;
    const { t, esc, qty } = ui;
    const root = ui.byId('qr-disposition');
    if (!root) return;
    const blockedId = root.dataset.blockedId;
    const form = ui.byId('qr-disposition-form');
    let data = null;
    let draft = null;
    let step = 1;
    let suppliersLoaded = false;
    const keys = new Map();

    const show = (el, on) => el?.classList.toggle('d-none', !on);
    const kind = () => form.querySelector('input[name="qr-kind"]:checked')?.value || '';
    const blocked = () => data.blocked;
    const where = () => [blocked().warehouseLabel, blocked().locationLabel].filter(Boolean).join(' · ');

    const moveText = () => {
        const k = kind();
        const q = Number(ui.byId('qr-qty').value);
        if (!k || !(q > 0)) return '';
        const amount = `${qty(q)} ${ui.uom(blocked())}`;
        const target = k === 'Sample' ? (data.sampleLocation?.label || '—') : where();
        return t('Quarantine.Disposition.Move.Line', t('Quarantine.Disposition.Move.' + k), amount, target);
    };

    const refreshForm = () => {
        const k = kind();
        show(ui.byId('qr-return-wrap'), k === 'ReturnToSupplier');
        show(ui.byId('qr-destroy-wrap'), k === 'Destroy');
        show(ui.byId('qr-sample-missing'), k === 'Sample' && !data.sampleLocation);
        form.querySelectorAll('.qr-option').forEach((label) => {
            const on = label.querySelector('input').checked;
            label.classList.toggle('border-primary', on);
            label.classList.toggle('bg-label-primary', on);
        });
        const text = moveText();
        ui.byId('qr-move-text').textContent = text;
        show(ui.byId('qr-move-box'), !!text);
        if (k === 'ReturnToSupplier' && !suppliersLoaded) loadSuppliers();
    };

    const loadSuppliers = async () => {
        suppliersLoaded = true;
        const reply = await ui.get('/Quarantine/lookups/suppliers');
        const select = ui.byId('qr-supplier');
        const items = reply.ok ? (reply.json?.items || []) : [];
        items.forEach((o) => select.add(new Option(o.text || o.value, o.value)));
        show(ui.byId('qr-supplier-unavailable'), !reply.ok || items.length === 0);
    };

    const validate = () => {
        ui.clearFieldErrors(form);
        let ok = true;
        const k = kind();
        const q = Number(ui.byId('qr-qty').value);
        const max = Number(blocked().quantity);
        if (!k) { ui.fieldError(form, 'kind', t('Quarantine.Disposition.Err.Kind')); ok = false; }
        if (!(q > 0) || q > max) { ui.fieldError(form, 'qty', t('Quarantine.Disposition.Err.Qty', `${qty(max)} ${ui.uom(blocked())}`)); ok = false; }
        if (k === 'ReturnToSupplier') {
            if (!ui.byId('qr-supplier').value) { ui.fieldError(form, 'supplier', t('Quarantine.Disposition.Err.Supplier')); ok = false; }
            if (!ui.byId('qr-returndoc').value.trim()) { ui.fieldError(form, 'returnDoc', t('Quarantine.Disposition.Err.ReturnDoc')); ok = false; }
        }
        if (k === 'Destroy') {
            if (!ui.byId('qr-method').value) { ui.fieldError(form, 'method', t('Quarantine.Disposition.Err.Method')); ok = false; }
            if (!ui.byId('qr-date').value) { ui.fieldError(form, 'date', t('Quarantine.Disposition.Err.Date')); ok = false; }
        }
        if (k === 'Sample' && !data.sampleLocation) ok = false;
        if (ui.byId('qr-reason').value.trim().length < 10) { ui.fieldError(form, 'reason', t('Quarantine.Common.ReasonMin')); ok = false; }
        return ok;
    };

    const request = () => {
        const k = kind();
        const disposition = { kind: k, quantity: Number(ui.byId('qr-qty').value) };
        if (k === 'ReturnToSupplier') {
            disposition.supplierId = ui.byId('qr-supplier').value;
            disposition.returnDocumentNo = ui.byId('qr-returndoc').value.trim();
        }
        if (k === 'Destroy') {
            disposition.destructionMethod = ui.byId('qr-method').value;
            disposition.destructionDate = ui.byId('qr-date').value;
        }
        const body = { kind: 'Disposition', targetId: blockedId, reason: ui.byId('qr-reason').value.trim(), disposition };
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
            const required = await ui.showSignStep(draft, blocked());
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
        await ui.announceSent(sent.decision, '/Quarantine/Blocked');
    };

    const render = () => {
        const b = blocked();
        const unit = ui.uom(b);
        ui.byId('qr-p-title').textContent = t('Quarantine.Disposition.Title', b.blockedNumber || '');
        ui.byId('qr-p-name').textContent = b.itemLabel || '';
        ui.byId('qr-p-sub').textContent = [b.lot?.lotNumber, where(), t('Quarantine.Disposition.BlockedQty', `${qty(b.quantity)} ${unit}`)].filter(Boolean).join(' · ');
        form.querySelectorAll('[data-uom]').forEach((el) => { el.textContent = unit; });
        ui.byId('qr-qty-hint').textContent = t('Quarantine.Disposition.Qty.Hint', `${qty(b.quantity)} ${unit}`);
        ui.byId('qr-qty').max = String(b.quantity);
        const allowed = (b.allowedActions || []).filter((a) => a.startsWith('Dispose:')).map((a) => a.slice('Dispose:'.length));
        form.querySelectorAll('[data-kind]').forEach((label) => show(label.closest('.col-12'), allowed.includes(label.dataset.kind)));
        if (b.status !== 'Open' || b.activeDecision || allowed.length === 0) {
            ui.showError(ui.byId('qr-page-error'), t('Quarantine.Disposition.Closed'));
            form.querySelectorAll('input, select, textarea, button[type="submit"]').forEach((el) => { el.disabled = true; });
        }
        refreshForm();
    };

    document.addEventListener('DOMContentLoaded', async () => {
        ui.loadCounts(null);
        ui.counter(ui.byId('qr-reason'));
        const reply = await ui.get(`/Quarantine/data/blocked/${encodeURIComponent(blockedId)}`);
        if (!reply.ok) {
            ui.showError(ui.byId('qr-page-error'), reply);
            form.classList.add('d-none');
            return;
        }
        data = reply.json;
        render();
        form.addEventListener('change', refreshForm);
        form.addEventListener('input', refreshForm);
        ui.byId('qr-back').addEventListener('click', () => goStep(1));
        form.addEventListener('submit', onSubmit);
    });
})();
