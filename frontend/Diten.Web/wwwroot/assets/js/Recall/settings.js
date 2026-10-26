/**
 * MOD-0177 R7b — Ayarlar (pack §11.1, §4.14; mockup "isSet"). Draws the settings and their history; the validation, the proposal and the
 * two-step save are RecallSettingsForm's (settings-form.js, measured by Node). After a save both are read again.
 */
'use strict';

(function () {
    if (typeof document === 'undefined' || !window.RecallUi || !window.RecallSettingsForm) return;
    const ui = window.RecallUi;
    const form = window.RecallSettingsForm;
    const { t, esc, byId } = ui;
    const root = byId('rc-settings');
    if (!root) return;
    const state = { settings: null, prepared: null };
    const show = (el, on) => el?.classList.toggle('d-none', !on);
    const modal = (id) => window.bootstrap?.Modal?.getOrCreateInstance(byId(id)) || { show() {}, hide() {} };
    const language = document.documentElement.lang || undefined;
    const FIELDS = { drillTargetHours: 'rc-set-target', drillFrequencyWarningDays: 'rc-set-freq', e1: 'rc-set-e1', e2: 'rc-set-e2', e3: 'rc-set-e3', reason: 'rc-set-reason' };

    const antiforgery = () => document.querySelector('#rc-antiforgery input[name="__RequestVerificationToken"]')?.value || '';
    const deps = {
        newKey: () => ui.newKey().replace(/^r6-/, 'r7-'),
        sign: (subject) => (window.ESign?.open ? window.ESign.open(subject) : Promise.resolve(null)),
        write: async (path, body, options = {}) => {
            const headers = { Accept: 'application/json', 'Content-Type': 'application/json', RequestVerificationToken: antiforgery() };
            if (options.ifMatch) headers['If-Match'] = options.ifMatch;
            try {
                const res = await fetch(path, { method: 'POST', credentials: 'same-origin', headers, body: JSON.stringify(body) });
                return { ok: res.ok, status: res.status, json: await res.json().catch(() => null) };
            } catch (error) {
                return { ok: false, status: 0, json: null };
            }
        }
    };

    const values = () => Object.fromEntries(Object.entries(FIELDS).map(([k, id]) => [k, byId(id)?.value ?? '']));
    const fill = (settings) => {
        const v = form.read(settings);
        Object.entries(FIELDS).forEach(([k, id]) => { if (byId(id)) { byId(id).value = v[k]; byId(id).classList.remove('is-invalid'); } });
        byId('rc-set-meta').textContent = settings.isDefault
            ? t('Recall.Settings.Defaults')
            : t('Recall.Settings.Updated', window.RecallCaseView.personName(settings.updatedBy, t), settings.updatedAtUtc ? new Date(settings.updatedAtUtc).toLocaleString(language) : '');
        dirty();
    };
    const dirty = () => {
        const save = byId('rc-set-save');
        if (!save || !state.settings) return;
        const changed = form.changed(state.settings, values()).length > 0;
        save.disabled = !changed;
        show(byId('rc-set-reset'), changed);
    };
    Object.values(FIELDS).forEach((id) => byId(id)?.addEventListener('input', dirty));
    byId('rc-set-reset')?.addEventListener('click', () => fill(state.settings));

    const fieldName = (field) => t('Recall.Settings.Field.' + field.replace('.', '_')) || field;
    const loadHistory = async () => {
        const reply = await ui.get('/Recall/data/settings/history?page=1&pageSize=50');
        const items = reply.ok ? reply.json.data?.items || [] : [];
        byId('rc-set-history').innerHTML = items.map((h) => `<li class="list-group-item">`
            + `<div class="d-flex justify-content-between gap-2"><span class="fw-medium small">${esc(fieldName(h.field))}</span>`
            + `<span class="small text-muted">${esc(window.RecallCaseView.personName(h.changedBy, t))} · ${esc(new Date(h.changedAtUtc).toLocaleString(language))}</span></div>`
            + `<div class="small"><span class="text-muted text-decoration-line-through">${esc(h.oldValue ?? '—')}</span> <i class="bx bx-right-arrow-alt"></i> <strong>${esc(h.newValue ?? '—')}</strong></div>`
            + `<div class="small text-muted">${esc(t('Recall.Settings.ReasonShown', h.reason))}</div>`
            + (h.signatureId ? `<span class="badge bg-label-success mt-1"><i class="bx bx-pen me-1"></i>${esc(t('Recall.Case.Signature', h.signatureId))}</span>` : '')
            + '</li>').join('') || `<li class="list-group-item text-muted small">${esc(t('Recall.Settings.HistoryEmpty'))}</li>`;
        if (!reply.ok) ui.showError(byId('rc-error'), ui.errorText(reply));
    };
    const load = async () => {
        const reply = await ui.get('/Recall/data/settings');
        if (!reply.ok) { ui.showError(byId('rc-error'), ui.errorText(reply)); return; }
        state.settings = reply.json.data;
        fill(state.settings);
        await loadHistory();
    };

    // ── "Kaydet": prepare (rs-v1) → the MOD-0022 dialog when the policy asks → save ──────────────────────────────
    const finish = async (result) => {
        if (result.ok) {
            modal('rc-sign-modal').hide();
            const box = byId('rc-saved');
            box.textContent = result.signatureId ? t('Recall.Sign.DoneSigned', result.signatureId) : t('Recall.Settings.Saved');
            show(box, true);
            await load();
            return true;
        }
        const text = result.cancelled ? t('Recall.Sign.Cancelled') : result.reply ? ui.errorText(result.reply) : t('Recall.Errors.GENERIC');
        ui.showError(byId('rc-sign-modal')?.classList.contains('show') ? byId('rc-sign-error') : byId('rc-error'), text);
        return false;
    };
    byId('rc-settings-form')?.addEventListener('submit', async (event) => {
        event.preventDefault();
        ui.hideError(byId('rc-error'));
        show(byId('rc-saved'), false);
        const first = await form.prepare(deps, state.settings, values());
        Object.entries(FIELDS).forEach(([k, id]) => byId(id)?.classList.toggle('is-invalid', !!first.errors?.[k]));
        if (!first.ok) {
            if (first.reply) ui.showError(byId('rc-error'), ui.errorText(first.reply));
            else if (first.errors?.form) ui.showError(byId('rc-error'), t(first.errors.form));
            return;
        }
        state.prepared = first.prepared;
        if (!first.signatureRequired) { await finish(await form.save(deps, state.settings, state.prepared, values())); return; }
        // the policy asks: the dialog's step 2 (the decision is already prepared — its lines say what is signed)
        byId('rc-sign-title').textContent = t('Recall.Settings.SignTitle');
        byId('rc-sign-lines').innerHTML = form.changed(state.settings, values()).map((f) => `<li>${esc(fieldName(f))}</li>`).join('')
            + `<li>${esc(t('Recall.Settings.ReasonShown', values().reason))}</li>`;
        document.querySelectorAll('#rc-sign-fields [data-sign-field]').forEach((f) => show(f, false));
        byId('rc-sign-step1').className = 'text-muted';
        byId('rc-sign-step2').className = 'fw-medium';
        byId('rc-sign-ready').textContent = t('Recall.Sign.ToSign');
        show(byId('rc-sign-ready'), true);
        ui.hideError(byId('rc-sign-error'));
        byId('rc-sign-commit-text').textContent = t('Recall.Sign.SignSave');
        show(byId('rc-sign-next'), false);
        show(byId('rc-sign-commit'), true);
        modal('rc-sign-modal').show();
    });
    byId('rc-sign-form')?.addEventListener('submit', (event) => event.preventDefault());
    byId('rc-sign-commit')?.addEventListener('click', async () => {
        if (!window.ESign?.open) { ui.showError(byId('rc-sign-error'), t('Recall.Errors.ESIGN_UNAVAILABLE')); return; }
        await finish(await form.save(deps, state.settings, state.prepared, values()));
    });

    load();
})();
