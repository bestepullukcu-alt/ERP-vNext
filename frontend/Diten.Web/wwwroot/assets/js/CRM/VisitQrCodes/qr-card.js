/**
 * WP-VW-W3E (W3E-WEB-a) — the institution QR card on the account details (Q1 a–d), the regenerate (Q3) / revoke (Q4)
 * dialogs and the history timeline. The state is the CRM's (GET /CRM/VisitQrCodes/api/accounts/{id}); the preview is a
 * DECORATION (no payload is read here — D-8, the code itself only on the print page with crm.visit-qr.manage). Without the
 * manage key the buttons are not drawn at all (Q1d). Texts: window.VisitQrL10n; rules: window.VisitQrCore.
 */
(function (window, document) {
    'use strict';
    const root = document.getElementById('vq-card');
    if (!root || !window.VisitQrCore) { return; }
    const C = window.VisitQrCore;
    const F = window.VisitPlanningFormat || null;
    const L = window.VisitQrL10n || {};
    const api = root.getAttribute('data-api-base') || '/CRM/VisitQrCodes/api';
    const printBase = root.getAttribute('data-print-base') || '/CRM/VisitQrCodes';
    const accountId = root.getAttribute('data-account-id');
    const accountName = root.getAttribute('data-account-name') || '';
    const perms = { read: true, manage: root.getAttribute('data-can-manage') === 'true' };
    const decoy = root.getAttribute('data-decoy') || '';

    const el = id => document.getElementById(id);
    const esc = s => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const fmt = (t, ...a) => String(t == null ? '' : t).replace(/\{(\d+)\}/g, (m, i) => (a[+i] == null ? '' : String(a[+i])));
    const bidi = s => '<bdi>' + esc(s) + '</bdi>';
    const lang = () => ((document.documentElement && document.documentElement.lang) || 'tr').trim() || 'tr';
    const dateOnly = iso => {
        const d = new Date(iso || '');
        if (isNaN(d)) { return '—'; }
        try { return new Intl.DateTimeFormat(/^en(-|$)/i.test(lang()) ? 'en-GB' : lang(), { day: 'numeric', month: 'short', year: 'numeric' }).format(d); } catch (e) { return d.toISOString().slice(0, 10); }
    };
    const time = iso => {
        const d = new Date(iso || '');
        return isNaN(d) ? '' : String(d.getHours()).padStart(2, '0') + ':' + String(d.getMinutes()).padStart(2, '0');
    };
    const dateTime = iso => (F && F.dateTime ? F.dateTime(iso) : dateOnly(iso) + ' ' + time(iso));
    /** "today 10:42" / "yesterday 16:20" / "11 Oct 2026" (the list and the card read the same). */
    const when = iso => {
        if (!iso) { return L.NeverScanned || '—'; }
        const k = C.whenKey(iso, Date.now(), -new Date().getTimezoneOffset());
        return k ? fmt(L[k], time(iso)) : dateOnly(iso);
    };

    const call = (method, path, body) => window.fetch(api + path, {
        method: method,
        credentials: 'same-origin',
        headers: Object.assign({ Accept: 'application/json' }, body ? { 'Content-Type': 'application/json' } : {}),
        body: body ? JSON.stringify(body) : undefined
    }).then(r => r.text().then(t => { let b = null; try { b = t ? JSON.parse(t) : null; } catch (e) { b = null; } return { ok: r.ok, status: r.status, body: b }; }))
        .catch(() => ({ ok: false, status: 0, body: null }));

    let state = null;

    const show = (id, on) => { const n = el(id); if (n) { n.classList.toggle('d-none', !on); } };
    function toast(key, version, withPrint) {
        el('vq-toast-text').textContent = fmt(L[key] || '', version == null ? '' : version);
        show('vq-toast-print', !!withPrint && perms.manage);
        show('vq-toast', true);
    }
    function error(text, retry) {
        el('vq-error-text').textContent = text || '';
        show('vq-retry', !!retry);
        show('vq-error', !!text);
    }

    function render() {
        const m = C.cardModel(state, perms);
        const chip = el('vq-chip');
        chip.className = 'vq-chip vq-chip-' + m.status;
        chip.textContent = L[m.chipKey] || '';
        show('vq-chip', true);
        show('vq-history-link', m.showHistory);
        let html = '';
        if (m.status === 'none') {
            html = '<div class="vq-state"><div class="vq-tile" aria-hidden="true"><i class="bx bx-qr"></i></div>'
                + '<div class="vq-info" style="gap:12px"><div class="vq-text">' + esc(L.NoneText) + '</div>'
                + (m.inherited ? '<div class="vq-callout vq-callout-info"><i class="bx bx-info-circle"></i><span>' + fmt(esc(L.InheritedInfo), bidi(m.inherited.accountName)) + '</span></div>' : '')
                + (m.canEdit ? '<div><button type="button" class="vq-btn vq-btn-p" data-vq="generate"><i class="bx bx-plus"></i>' + esc(L.Generate) + '</button></div>' : '')
                + '</div></div>';
        } else if (m.status === 'active') {
            html = '<div class="vq-state"><div class="vq-preview" aria-hidden="true"><img src="' + esc(decoy) + '" alt="" />'
                + '<div class="vq-preview-lock"><i class="bx bx-lock-alt"></i>' + esc(L.PreviewLocked) + '</div></div>'
                + '<div class="vq-info"><div class="vq-info-strong">' + fmt(esc(L.ActiveLine), m.version, bidi(m.issuedBy || '—'), esc(dateOnly(m.issuedAt))) + '</div>'
                + '<div>' + fmt(esc(L.ScanLine), esc(when(m.lastScannedAt)), m.recent) + '</div>'
                + '<div class="vq-note">' + esc(L.ValidNote) + '</div>'
                + (m.canEdit
                    ? '<div class="vq-actions"><a class="vq-btn vq-btn-p" id="vq-print" href="' + esc(C.printUrl(printBase, accountId)) + '"><i class="bx bx-printer"></i>' + esc(L.PrintButton) + '</a>'
                      + '<button type="button" class="vq-btn vq-btn-o" data-vq="regen"><i class="bx bx-refresh"></i>' + esc(L.RegenerateButton) + '</button>'
                      + '<button type="button" class="vq-btn vq-btn-s" data-vq="revoke"><i class="bx bx-block"></i>' + esc(L.RevokeButton) + '</button></div>'
                    : '<div class="vq-ro" id="vq-readonly"><i class="bx bx-lock-alt"></i>' + esc(L.ReadOnly) + '</div>')
                + '</div></div>';
        } else {
            html = '<div class="vq-state"><div class="vq-tile vq-tile-revoked" aria-hidden="true"><i class="bx bx-block"></i></div>'
                + '<div class="vq-info"><div class="vq-info-strong">' + fmt(esc(L.RevokedLine), bidi(m.revokedBy || '—'), esc(dateOnly(m.revokedAt))) + '</div>'
                + (m.reason ? '<div>' + fmt(esc(L.ReasonLine), bidi(m.reason)) + '</div>' : '')
                + '<div class="vq-note">' + fmt(esc(L.RevokedNote), m.version) + '</div>'
                + (m.inherited ? '<div class="vq-callout vq-callout-info"><i class="bx bx-info-circle"></i><span>' + fmt(esc(L.InheritedInfo), bidi(m.inherited.accountName)) + '</span></div>' : '')
                + (m.canEdit ? '<div style="margin-top:10px"><button type="button" class="vq-btn vq-btn-p" data-vq="generate"><i class="bx bx-plus"></i>' + esc(L.NewCode) + '</button></div>' : '')
                + '</div></div>';
        }

        el('vq-state').innerHTML = html;
        const toastPrint = el('vq-toast-print');
        if (toastPrint) { toastPrint.setAttribute('href', C.printUrl(printBase, accountId)); }
    }

    function load() {
        error('');
        call('GET', '/accounts/' + encodeURIComponent(accountId)).then(r => {
            if (r.ok && r.body && r.body.data) { state = r.body.data; render(); return; }
            error(L.LoadFailed || '', true);
        });
    }

    const modal = id => (window.bootstrap ? window.bootstrap.Modal.getOrCreateInstance(el(id)) : null);

    function generate(button) {
        if (!perms.manage) { return; }
        const before = state;
        if (button) { button.disabled = true; }
        call('POST', '/accounts/' + encodeURIComponent(accountId) + '/issue', C.issueBody(state, null)).then(r => {
            if (button) { button.disabled = false; }
            if (r.ok && r.body && r.body.data) {
                state = r.body.data; render(); error('');
                toast(C.toastKey(before), state.version, true);
                return;
            }
            error(L[C.errorKey(r)] || L.Err_generic || '');
        });
    }

    function openRegen() {
        if (!perms.manage || !state) { return; }
        const m = C.cardModel(state, perms);
        el('vq-regen-warn').innerHTML = fmt(esc(L.RegenWarn), bidi(accountName));
        el('vq-regen-versions').textContent = fmt(L.VersionArrow, m.version, m.nextVersion);
        el('vq-regen-reason').value = '';
        show('vq-regen-error', false);
        modal('vq-regen').show();
    }

    function confirmRegen() {
        const before = state;
        const button = el('vq-regen-confirm');
        button.disabled = true;
        call('POST', '/accounts/' + encodeURIComponent(accountId) + '/issue', C.issueBody(state, el('vq-regen-reason').value)).then(r => {
            button.disabled = false;
            if (r.ok && r.body && r.body.data) {
                modal('vq-regen').hide();
                state = r.body.data; render(); error('');
                toast(C.toastKey(before), state.version, true);
                return;
            }
            const box = el('vq-regen-error');
            box.textContent = L[C.errorKey(r)] || L.Err_generic || '';
            show('vq-regen-error', true);
        });
    }

    function syncRevoke() {
        const rs = C.reasonState(el('vq-revoke-reason').value);
        el('vq-revoke-count').textContent = fmt(L.ReasonCount, rs.length);
        el('vq-revoke-confirm').disabled = !rs.valid;
    }

    function openRevoke() {
        if (!perms.manage || !state) { return; }
        el('vq-revoke-text').innerHTML = fmt(esc(L.RevokeText), bidi(accountName));
        el('vq-revoke-reason').value = '';
        show('vq-revoke-error', false);
        syncRevoke();
        modal('vq-revoke').show();
    }

    function confirmRevoke() {
        const body = C.revokeBody(state, el('vq-revoke-reason').value);
        const box = el('vq-revoke-error');
        if (!body) { box.textContent = L.ReasonRequired || ''; show('vq-revoke-error', true); return; }
        const button = el('vq-revoke-confirm');
        button.disabled = true;
        call('POST', '/accounts/' + encodeURIComponent(accountId) + '/revoke', body).then(r => {
            if (r.ok && r.body && r.body.data) {
                modal('vq-revoke').hide();
                state = r.body.data; render(); error('');
                toast('ToastRevoked', null, false);
                return;
            }
            syncRevoke();
            box.textContent = L[C.errorKey(r)] || L.Err_generic || '';
            show('vq-revoke-error', true);
        });
    }

    function openHistory() {
        const list = el('vq-history-list');
        list.innerHTML = '<div class="vq-skel"><i class="bx bx-loader-alt bx-spin"></i></div>';
        modal('vq-history').show();
        call('GET', '/accounts/' + encodeURIComponent(accountId) + '/history').then(r => {
            const items = r.ok && r.body && Array.isArray(r.body.data) ? r.body.data : null;
            if (!items) { list.innerHTML = '<div class="vq-err">' + esc(L.LoadFailed) + '</div>'; return; }
            if (!items.length) { list.innerHTML = '<div class="vq-note">' + esc(L.NoHistory) + '</div>'; return; }
            list.innerHTML = items.map(ev => {
                const line = C.eventLine(ev);
                let title = fmt(esc(L[line.key]), line.version);
                if (line.paperKey) { title += ' · ' + (line.copies != null ? fmt(esc(L.PrintedDetail), esc(L[line.paperKey]), line.copies) : esc(L[line.paperKey])); }
                return '<div class="vq-ev"><div class="vq-ev-rail"><span class="vq-ev-dot vq-tone-' + line.tone + '"></span><span class="vq-ev-line"></span></div>'
                    + '<div class="vq-ev-body"><div class="vq-ev-top"><span class="vq-ev-title">' + title + '</span><span class="vq-ev-when">' + esc(dateTime(line.at)) + '</span></div>'
                    + '<div class="vq-ev-who">' + bidi(line.by || '—') + '</div>'
                    + (line.reason ? '<div class="vq-ev-reason">' + fmt(esc(L.ReasonLine), bidi(line.reason)) + '</div>' : '') + '</div></div>';
            }).join('');
        });
    }

    root.addEventListener('click', e => {
        const b = e.target.closest('[data-vq]');
        if (!b) { return; }
        const a = b.getAttribute('data-vq');
        if (a === 'generate') { generate(b); } else if (a === 'regen') { openRegen(); } else if (a === 'revoke') { openRevoke(); }
    });
    el('vq-history-link').addEventListener('click', openHistory);
    el('vq-retry').addEventListener('click', load);
    if (el('vq-regen-confirm')) { el('vq-regen-confirm').addEventListener('click', confirmRegen); }
    if (el('vq-revoke-confirm')) { el('vq-revoke-confirm').addEventListener('click', confirmRevoke); }
    if (el('vq-revoke-reason')) { el('vq-revoke-reason').addEventListener('input', syncRevoke); }

    load();
}(window, document));
