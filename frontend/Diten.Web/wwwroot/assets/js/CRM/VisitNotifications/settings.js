/**
 * WP-VW-W5 · W5-WEB (K-N5) — the notification settings card: the interim PV / MSL mailboxes (until K-W8) and the two
 * switches. Loaded ONLY for crm.visit-notifications.settings.manage (Index.cshtml renders neither the card nor this script
 * otherwise; the proxy checks the same key on GET and PUT). Reads once when the tab first shows, saves with the version it
 * read: a 409 (someone saved meanwhile) is said in words and the fields keep what was typed; a bad address is marked on
 * its field before anything is sent (the CRM's own rule, VisitNotificationsCore.validEmail) and again if the CRM refuses it.
 */
'use strict';

window.VisitNotificationsSettings = (function () {
    const endpoint = '/CRM/VisitNotifications/api/settings';
    const C = window.VisitNotificationsCore;
    const L = () => window.L10n || {};
    const byId = (id) => document.getElementById(id);
    const fmt = (t, ...a) => a.reduce((x, v, i) => x.split('{' + i + '}').join(String(v)), String(t || ''));
    const headers = (json) => Object.assign({}, window.DitenDataTable?.getAuthHeaders?.(json) || {}, json ? { 'Content-Type': 'application/json' } : {});
    let version = 0;
    let loaded = false;
    let busy = false;

    const request = (method, body) => fetch(endpoint, { method: method, credentials: 'same-origin', headers: headers(!!body), body: body ? JSON.stringify(body) : undefined })
        .then((r) => r.text().then((t) => { let b = null; try { b = t ? JSON.parse(t) : null; } catch (e) { b = null; } return { ok: r.ok, status: r.status, body: b }; }))
        .catch(() => ({ ok: false, status: 0, body: null }));

    const alertOf = (tone, text) => {
        const box = byId('vn-settings-alert');
        if (!box) { return; }
        box.className = 'alert alert-' + tone + (text ? '' : ' d-none');
        box.textContent = text || '';
    };

    const fieldError = (id, key) => {
        const input = byId(id);
        const msg = byId(id + '-error');
        if (input) { input.classList.toggle('is-invalid', !!key); }
        if (msg) { msg.textContent = key ? (L()[key] || '') : ''; }
    };

    const fill = (s) => {
        const set = (id, v) => { const n = byId(id); if (n) { n.value = v || ''; } };
        const check = (id, v) => { const n = byId(id); if (n) { n.checked = !!v; } };
        set('vn-pv-email', s.pvEmail);
        set('vn-msl-email', s.mslEmail);
        check('vn-report-summary', s.reportSummary !== false);
        check('vn-overdue-reminders', s.overdueReminders !== false);
        version = Number(s.version) || 0;
        const meta = byId('vn-settings-meta');
        if (meta) {
            meta.textContent = s.isDefault ? (L().SettingsDefault || '')
                : fmt(L().SettingsUpdated, s.updatedAt ? new Date(s.updatedAt).toLocaleString(document.documentElement.lang || undefined) : '—', s.updatedBy || '—');
        }
    };

    const load = () => {
        if (loaded || !byId('vn-settings-form')) { return Promise.resolve(); }
        loaded = true;
        return request('GET').then((res) => {
            if (res.ok && res.body && res.body.data) { fill(res.body.data); alertOf('', ''); return; }
            loaded = false;
            alertOf('danger', L()[C.errorKey(res)] || L().Err_generic || '');
        });
    };

    const form = () => ({
        pvEmail: byId('vn-pv-email')?.value || '',
        mslEmail: byId('vn-msl-email')?.value || '',
        reportSummary: !!byId('vn-report-summary')?.checked,
        overdueReminders: !!byId('vn-overdue-reminders')?.checked
    });

    const save = (event) => {
        if (event) { event.preventDefault(); }
        if (busy) { return Promise.resolve(); }
        const f = form();
        const pvBad = !C.validEmail(f.pvEmail);
        const mslBad = !C.validEmail(f.mslEmail);
        fieldError('vn-pv-email', pvBad ? 'ErrEmailField' : null);
        fieldError('vn-msl-email', mslBad ? 'ErrEmailField' : null);
        if (pvBad || mslBad) { return Promise.resolve(); }
        busy = true;
        const button = byId('vn-settings-save');
        if (button) { button.disabled = true; }
        return request('PUT', C.settingsBody(f, version)).then((res) => {
            busy = false;
            if (button) { button.disabled = false; }
            if (res.ok && res.body && res.body.data) {
                fill(res.body.data);
                alertOf('', '');
                window.showToast?.(L().SettingsSaved || '', 'success');
                return;
            }
            const key = C.errorKey(res);
            if (key === 'Err_visit_notification_email_invalid') {
                fieldError('vn-pv-email', f.pvEmail ? 'ErrEmailField' : null);
                fieldError('vn-msl-email', f.mslEmail ? 'ErrEmailField' : null);
            }
            // 409: the fields keep what was typed; the reader reloads to see the other change
            alertOf(key === 'Err_conflict' ? 'warning' : 'danger', L()[key] || L().Err_generic || '');
        });
    };

    const init = () => {
        const f = byId('vn-settings-form');
        if (!f || !C) { return; }
        f.addEventListener('submit', save);
        // a direct open of the settings tab (it is not the first one) loads at once
        if (byId('vn-pane-settings')?.classList.contains('active')) { load(); }
    };

    document.addEventListener('DOMContentLoaded', init);
    return { load: load, save: save, version: () => version };
})();
