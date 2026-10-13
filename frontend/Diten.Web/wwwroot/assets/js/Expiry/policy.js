/**
 * MOD-0176 F5 — Raf Ömrü Kuralları ve Ayarlar (pack §4.1; mockup "set" + "hist"). /Expiry/data/policy → the policy (+ its ETag) and the
 * history; /Expiry/data/policy/products → the read-only product view. "Değişiklikleri kaydet" lists the changed fields in the reason modal
 * (≥ 10) and PUTs the WHOLE policy with If-Match (the server writes one history row per changed field; a stale version → 409
 * EXPIRY_CONFLICT, said in the modal). The ranges are the server's; this file only blocks an empty field.
 */
'use strict';

(function () {
    const ui = window.ExpiryUi;
    const { t, esc } = ui;
    const TYPES = ['FEFO_BYPASS', 'BELOW_MIN_REMAINING', 'EXPIRY_DATE_OVERRIDE', 'EXPIRED_ON_HAND'];
    const MODES = ['fefoOutboundMode', 'belowMinRemainingMode', 'dateOverrideMode'];
    const state = { policy: null, etag: null, key: null };

    const num = (id) => ui.byId(id)?.value === '' ? null : Number(ui.byId(id)?.value);
    const read = () => ({
        approachingDays: num('ex-p-approaching'),
        criticalDays: num('ex-p-critical'),
        fefoOutboundMode: document.querySelector('input[name="ex-p-fefoOutboundMode"]:checked')?.value,
        belowMinRemainingMode: document.querySelector('input[name="ex-p-belowMinRemainingMode"]:checked')?.value,
        dateOverrideMode: document.querySelector('input[name="ex-p-dateOverrideMode"]:checked')?.value,
        slaHours: Object.fromEntries(TYPES.map((type) => [type, num(`ex-p-sla-${type}`)])),
        expiredOnHandThresholdDays: num('ex-p-threshold')
    });
    const fill = (p) => {
        ui.byId('ex-p-approaching').value = p.approachingDays;
        ui.byId('ex-p-critical').value = p.criticalDays;
        MODES.forEach((m) => { const r = ui.byId(`ex-p-${m}-${p[m]}`); if (r) r.checked = true; });
        TYPES.forEach((type) => { ui.byId(`ex-p-sla-${type}`).value = p.slaHours?.[type] ?? ''; });
        ui.byId('ex-p-threshold').value = p.expiredOnHandThresholdDays;
        ui.byId('ex-policy-meta').textContent = p.isDefault ? t('Expiry.Policy.Defaults')
            : t('Expiry.Policy.UpdatedBy', ui.dateTime(p.updatedAtUtc), ui.person(p.updatedBy));
    };

    const label = (field) => (field.startsWith('slaHours.') ? t('Expiry.Policy.SlaOf', t('Expiry.Type.' + field.slice(9))) : t('Expiry.Policy.Field.' + field));
    const shown = (field, value) => {
        if (value === null || value === undefined || value === '') return '—';
        if (MODES.includes(field)) return t('Expiry.Policy.Mode.' + value) || value;
        return field.startsWith('slaHours.') ? t('Expiry.Policy.Hours', value) : t('Expiry.Common.Days', value);
    };
    const diff = () => {
        const before = state.policy;
        const after = read();
        const changes = [];
        ['approachingDays', 'criticalDays', ...MODES, 'expiredOnHandThresholdDays'].forEach((f) => {
            if (String(before[f]) !== String(after[f])) changes.push([f, before[f], after[f]]);
        });
        TYPES.forEach((type) => {
            if (String(before.slaHours?.[type]) !== String(after.slaHours[type])) changes.push([`slaHours.${type}`, before.slaHours?.[type], after.slaHours[type]]);
        });
        return changes;
    };
    const dirty = () => {
        if (!state.policy || !ui.byId('ex-p-save')) return;
        const any = diff().length > 0;
        ui.byId('ex-p-save').disabled = !any;
        ui.byId('ex-p-reset').disabled = !any;
        ui.byId('ex-p-dirty')?.classList.toggle('d-none', !any);
    };

    const history = (page) => {
        const rows = page?.items || [];
        ui.byId('ex-policy-history-table').querySelector('tbody').innerHTML = rows.length
            ? rows.map((h) => `<tr><td><small class="d-block" dir="ltr">${esc(ui.dateTime(h.changedAtUtc))}</small><small class="text-muted">${esc(ui.person(h.changedBy))}</small></td>`
                + `<td>${esc(label(h.field))}</td><td><span class="text-muted">${esc(shown(h.field, h.oldValue))}</span> → <strong>${esc(shown(h.field, h.newValue))}</strong></td>`
                + `<td class="small">${esc(h.reason)}</td></tr>`).join('')
            : `<tr><td colspan="4" class="text-muted">${esc(t('Expiry.Policy.HistoryEmpty'))}</td></tr>`;
    };

    const load = async () => {
        const reply = await ui.get('/Expiry/data/policy');
        if (!reply.ok) { ui.showError(ui.byId('ex-error'), reply); return; }
        ui.hideError(ui.byId('ex-error'));
        state.policy = reply.json.policy;
        state.etag = reply.json.etag || `W/"${state.policy.version}"`;
        fill(state.policy);
        history(reply.json.history);
        dirty();
    };

    const products = async () => {
        const body = ui.byId('ex-policy-products-table').querySelector('tbody');
        const reply = await ui.get('/Expiry/data/policy/products');
        if (!reply.ok) { body.innerHTML = `<tr><td colspan="5" class="text-danger">${esc(ui.errorText(reply))}</td></tr>`; return; }
        const days = (row, value) => (!row.read ? '<span class="text-muted">—</span>'
            : value === null || value === undefined ? `<span class="badge bg-label-warning">${esc(t('Expiry.Policy.Undefined'))}</span>`
            : `<span dir="ltr">${esc(t('Expiry.Common.Days', value))}</span>`);
        const items = reply.json.items || [];
        body.innerHTML = items.length
            ? items.map((r) => `<tr><td>${ui.productCell(r)}</td><td><span class="badge bg-label-secondary">${esc(r.skuLevel)}</span></td>`
                + `<td class="text-end">${days(r, r.shelfLifeDays)}</td><td class="text-end">${days(r, r.retestDays)}</td><td class="text-end">${days(r, r.minRemainingShelfLifeDays)}</td></tr>`).join('')
            : `<tr><td colspan="5" class="text-muted">${esc(t('Expiry.Policy.ProductsEmpty'))}</td></tr>`;
    };

    const openSave = () => {
        const changes = diff();
        const empty = Object.entries(read()).some(([, v]) => v === null || v === undefined) || Object.values(read().slaHours).some((v) => v === null);
        ui.byId('ex-p-invalid').textContent = empty ? t('Expiry.Policy.FillAll') : '';
        if (empty || changes.length === 0) return;
        ui.byId('ex-p-diff').innerHTML = changes.map(([f, a, b]) => `<li>${esc(label(f))}: ${esc(shown(f, a))} → <strong>${esc(shown(f, b))}</strong></li>`).join('');
        ui.byId('ex-p-reason').value = '';
        ui.byId('ex-p-modal').querySelector('[data-err="reason"]').textContent = '';
        ui.hideError(ui.byId('ex-p-modal-error'));
        state.key = ui.newKey(); // one intent per opened modal: a double click replays, never saves twice
        counter();
        window.bootstrap.Modal.getOrCreateInstance(ui.byId('ex-p-modal')).show();
    };
    const counter = () => {
        const n = ui.byId('ex-p-reason').value.trim().length;
        ui.byId('ex-p-counter').textContent = t('Expiry.Drawer.Counter', n, 10);
    };
    const save = async () => {
        const reason = ui.byId('ex-p-reason').value.trim();
        if (reason.length < 10) { ui.byId('ex-p-modal').querySelector('[data-err="reason"]').textContent = t('Expiry.Drawer.ReasonMin'); return; }
        const body = { idempotencyKey: state.key, ...read(), reason };
        const reply = await ui.call('PUT', '/Expiry/api/policy', body, { idempotencyKey: state.key, ifMatch: state.etag });
        if (!reply.ok) { ui.showError(ui.byId('ex-p-modal-error'), reply); return; }
        window.bootstrap.Modal.getOrCreateInstance(ui.byId('ex-p-modal')).hide();
        window.showToast?.(t('Expiry.Policy.Saved'), 'success');
        await load();
    };

    document.addEventListener('DOMContentLoaded', async () => {
        ui.byId('ex-policy-form')?.addEventListener('input', dirty);
        ui.byId('ex-policy-form')?.addEventListener('change', dirty);
        ui.byId('ex-p-reset')?.addEventListener('click', () => { fill(state.policy); dirty(); });
        ui.byId('ex-p-save')?.addEventListener('click', openSave);
        ui.byId('ex-p-reason')?.addEventListener('input', counter);
        ui.byId('ex-p-confirm')?.addEventListener('click', save);
        let productsLoaded = false;
        ui.byId('ex-policy-products-tab')?.addEventListener('shown.bs.tab', () => { if (!productsLoaded) { productsLoaded = true; products(); } });
        await load();
    });
})();
