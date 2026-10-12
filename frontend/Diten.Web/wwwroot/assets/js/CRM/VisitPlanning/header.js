/**
 * WP-VP-4B / 4D — the Visit Planning DETAIL header (brief §3, mockup "03 Plan detayı"): the summary (period, rep name,
 * the selected week, its status badge and date range), the status-based actions with their status sentence, the reopen
 * dialog, the two capacity cards (this week / the period, bar + note) and the page bands.
 * Reads window.VisitPlanningPage (page.js) only; the Targets + Route code (details.js) owns its own views.
 *
 *   ONE source of the selected week: VisitPlanningPage.state.weekStart. Every 'week-change' (the Weeks strip, "Open next
 *   week", the Route's own week selector, ?week=) refreshes the summary, the actions and the capacity TOGETHER (4D — the
 *   summary used to keep an old week). There is no week dropdown here any more.
 *
 *   week status → actions: VisitPlanningPage.actionsFor (draft: save targets / build route / approve · empty: save
 *   targets / build route + "build this week" · approved: reopen / next open week · past: none · legacy committed plan:
 *   none + band). Every action button carries data-vp-action="<key>" and is shown only when that key is offered AND the
 *   server rendered it (permission flags).
 *
 *   currentWeekStart / nextDraftWeekStart (WP-VP-4A) with fallbacks; a reopen proxy 404 without a body says "reopen is
 *   not available yet". Other parts ask for the reopen dialog with page.request('reopen-week').
 */
(function (window, document) {
    'use strict';
    const root = document.getElementById('visit-planning-details');
    const page = window.VisitPlanningPage;
    if (!root || !page) return;

    const L = window.L10n || {};
    const VPF = window.VisitPlanningFormat; // WP-VP-4H — dates / numbers in the application's language
    const base = '/CRM/VisitPlanning/api';
    const sessionId = root.dataset.sessionId;
    const REOPEN_MIN = 10;
    const TOAST_KEY = 'vp-applied-toast'; // the same one-shot toast details.js shows after a reload
    page.setFlags({
        canGenerate: root.dataset.canGenerate === 'true',
        canApply: root.dataset.canApply === 'true',
        readOnly: root.dataset.readOnly === 'true'
    });

    const el = id => document.getElementById(id);
    const esc = s => { const d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; };
    const setText = (id, v) => { const n = el(id); if (n) n.textContent = v; };
    const fmt = (tpl, ...args) => args.reduce((t, a, i) => t.split('{' + i + '}').join(String(a)), String(tpl || ''));
    const requestedWeek = (() => { const w = new URLSearchParams(window.location.search).get('week'); return /^\d{4}-\d{2}-\d{2}$/.test(w || '') ? w : null; })();

    // ── labels ──
    const STATUS_LABEL = { past: 'WeekStatusPast', approved: 'WeekStatusApproved', draft: 'WeekStatusDraft', empty: 'WeekStatusEmpty' };
    const STATUS_TONE = { past: 'secondary', approved: 'success', draft: 'primary', empty: 'warning' };
    const STATUS_TEXT = { draft: 'StatusTextDraft', approved: 'StatusTextApproved', past: 'StatusTextPast', empty: 'StatusTextEmpty' };
    const statusLabel = s => L[STATUS_LABEL[s]] || s || '—';
    const localDate = ws => new Date(ws + 'T00:00:00'); // yyyy-MM-dd as a LOCAL day (never shifted a day by UTC parsing)
    const weekTitle = w => (L.WeekNumberLabel || '{0}. Hafta').replace('{0}', w.isoWeek);
    const weekRange = w => VPF.workRange(w.from || w.weekStart, w.to || w.weekStart);
    const weekYear = w => { const t = localDate(w.to || w.weekStart); return isNaN(t) ? '' : String(t.getFullYear()); };
    const hours = minutes => VPF.hours(minutes, L.HoursFormat || '{0} h');

    // ── default + next week ──
    const defaultWeek = () => {
        const list = page.weeks();
        if (!list.length) return null;
        const has = ws => ws && list.some(w => w.weekStart === ws);
        const s = page.state.session || {};
        if (has(requestedWeek)) return requestedWeek;
        if (has(s.currentWeekStart)) return s.currentWeekStart;                       // WP-VP-4A
        const draft = list.find(w => w.status === 'draft'); if (draft) return draft.weekStart;
        const open = list.find(w => w.status !== 'past'); if (open) return open.weekStart;
        return list[0].weekStart;
    };
    const nextOpenWeek = ws => {
        const list = page.weeks();
        const s = page.state.session || {};
        if (s.nextDraftWeekStart && s.nextDraftWeekStart !== ws && list.some(w => w.weekStart === s.nextDraftWeekStart)) return s.nextDraftWeekStart; // WP-VP-4A
        const at = list.findIndex(w => w.weekStart === ws);
        const next = list.slice(at + 1).find(w => w.status === 'draft' || w.status === 'empty');
        return next ? next.weekStart : null;
    };

    // ── summary: the selected week, its status badge and its date range (4D — always the page's week) ──
    const renderSummary = () => {
        const w = page.week(page.state.weekStart);
        const legacy = page.isLegacy();
        const status = w ? w.status : null;
        const badge = el('vp-d-status-strip');
        if (badge) {
            badge.textContent = legacy ? (L.LegacyPlanBadge || '') : statusLabel(status);
            badge.className = 'badge bg-label-' + (legacy ? 'success' : (STATUS_TONE[status] || 'secondary'));
        }
        setText('vp-d-week', w ? weekTitle(w) : '—');
        setText('vp-route-range', w ? weekRange(w) + ' · ' + weekYear(w) : '—');
    };

    // E4-4B-2 — the rep by name: the plan's resourceDisplayName; when that is missing or an e-mail address, the signed-in
    // person's directory name (resources/me) when the plan is theirs.
    let me = null;
    const looksLikeMail = v => /@/.test(String(v || ''));
    const renderRep = () => {
        const s = page.state.session || {};
        let name = s.resourceDisplayName;
        if ((!name || looksLikeMail(name)) && me && me.displayName && !looksLikeMail(me.displayName)
            && String(me.resourceId || '').toLowerCase() === String(s.resourceId || '').toLowerCase()) {
            name = me.displayName;
        }
        setText('vp-d-rep', name || s.resourceId || '—');
    };
    const loadMe = () => fetch(base + '/me', { credentials: 'same-origin', headers: { Accept: 'application/json' } })
        .then(r => (r.ok ? r.json() : null))
        .then(b => { me = (b && b.data && Array.isArray(b.data.items) && b.data.items[0]) || null; renderRep(); })
        .catch(() => { me = null; });

    // ── status actions + bands ──
    const renderActions = () => {
        const ws = page.state.weekStart;
        const w = page.week(ws);
        const legacy = page.isLegacy();
        const status = w ? w.status : null;
        const offered = page.actionsFor(status, legacy);
        document.querySelectorAll('[data-vp-action]').forEach(btn => {
            const key = btn.getAttribute('data-vp-action');
            let show = offered.indexOf(key) > -1;
            if (key === 'nextWeek') show = show && !!nextOpenWeek(ws);
            // 4D — an empty week offers "Build this week" (a fresh preview; nothing is written).
            if (key === 'generateWeek') show = !legacy && status === 'empty';
            const item = btn.closest('[data-vp-action-item]') || btn;
            item.classList.toggle('d-none', !show);
        });
        // 4D — the status sentence of the actions card (mockup).
        setText('vp-hdr-status-text', legacy ? (L.LegacyPlanBand || '') : (L[STATUS_TEXT[status]] || ''));

        const band = el('vp-week-band'); if (!band) return;
        let text = '';
        if (legacy) text = L.LegacyPlanBand || '';
        else if (status === 'approved') text = L.WeekLocked || '';
        else if (status === 'past') text = L.PastWeekBand || '';
        band.innerHTML = text ? '<div class="alert alert-info py-2 small mb-3" role="status"><i class="bx bx-lock-alt me-1" aria-hidden="true"></i>' + esc(text) + '</div>' : '';
        // The legacy band replaces the generic read-only notice (one sentence, not two).
        el('vp-readonly-notice')?.classList.toggle('d-none', legacy);
    };

    // ── capacity cards (C5 / 4D): this week and the period, minutes shown as hours, a bar and a note each ──
    const bar = (id, pct) => {
        const b = el(id); if (!b) return;
        b.style.width = pct + '%'; b.setAttribute('aria-valuenow', String(pct)); b.classList.toggle('bg-warning', pct >= 100);
    };
    const renderCapacity = () => {
        const p = page.state.preview;
        const ws = page.state.weekStart;
        const wc = p && Array.isArray(p.weekCapacity) ? p.weekCapacity.find(c => c.weekStart === ws) : null;
        const pc = p ? p.periodCapacity : null;
        // Without a capacity the period runs on the default hours: both cards say there is no capacity (mockup).
        const noCapacity = !!(pc && pc.budgetSource === 'default_hours');
        // 4H — "planned this week" through the one shared reading (the Targets summary uses the same).
        const load = VPF.weekLoad(p, ws);
        setText('vp-cap-week', wc ? hours(wc.capacityMinutes) : '—');
        setText('vp-cap-week-planned', load.planned != null ? hours(load.planned) : '—');
        setText('vp-cap-period', pc ? hours(pc.capacityMinutes) : '—');
        setText('vp-cap-period-planned', pc ? hours(pc.plannedMinutes) : '—');

        const weekPct = wc && wc.capacityMinutes > 0 ? Math.min(100, Math.round((load.planned || 0) / wc.capacityMinutes * 100)) : 0;
        bar('vp-cap-week-bar', weekPct);
        let weekNote = '';
        if (noCapacity) weekNote = L.CapacityMissingWeek || '';
        else if (wc && wc.capacityMinutes > 0) {
            weekNote = fmt(L.WeekCapacityNote || '{0}', weekPct)
                + (wc.holidays > 0 || wc.halfDays > 0 ? ' ' + (L.WeekCapacityHolidaysNote || '') : '');
        }
        setText('vp-cap-week-note', weekNote);

        const pct = pc && pc.capacityMinutes > 0 ? Math.min(100, Math.round(pc.plannedMinutes / pc.capacityMinutes * 100)) : 0;
        bar('vp-cap-period-bar', pct);
        setText('vp-cap-period-pct', pc && pc.capacityMinutes > 0 ? pct + '%' : '');
        const weeks = page.weeks();
        const plannedWeeks = weeks.filter(w => (w.visitCount || 0) > 0).length;
        setText('vp-cap-period-note', noCapacity ? (L.CapacityMissingPeriod || '')
            : (pc && pc.capacityMinutes > 0 ? fmt(L.PeriodCapacityNote || '{0} {1} {2}', pct, weeks.length, plannedWeeks) : ''));

        const note = el('vp-cap-note');
        if (note) {
            note.textContent = noCapacity ? (L.CapacityDefaultHours || '') : '';
            note.classList.toggle('d-none', !noCapacity);
        }
    };

    // 4D — the selected week drives the summary, the actions and the capacity TOGETHER.
    const refresh = () => { renderSummary(); renderActions(); renderCapacity(); };

    // ── reopen (gerekçe ≥ 10 → the reopen proxy; MK-4: the reason goes into the week's history) ──
    const reasonOk = v => String(v || '').trim().length >= REOPEN_MIN;
    const openReopen = () => {
        const w = page.week(page.state.weekStart); if (!w) return;
        setText('vp-reopen-title', (L.ReopenTitle || '{0}').replace('{0}', weekTitle(w)));
        const box = el('vp-reopen-reason'); if (box) box.value = '';
        syncReopen();
        const modal = el('vp-reopen-modal');
        if (modal && window.bootstrap) window.bootstrap.Modal.getOrCreateInstance(modal).show();
    };
    const syncReopen = () => {
        const v = el('vp-reopen-reason') ? el('vp-reopen-reason').value : '';
        const ok = reasonOk(v);
        const btn = el('vp-reopen-confirm'); if (btn) btn.disabled = !ok;
        setText('vp-reopen-count', VPF.ratio(v.trim().length, REOPEN_MIN));
    };
    const confirmReopen = () => {
        const reason = (el('vp-reopen-reason') ? el('vp-reopen-reason').value : '').trim();
        if (!reasonOk(reason)) return; // never sent below the minimum
        const ws = page.state.weekStart;
        const s = page.state.session || {};
        const btn = el('vp-reopen-confirm'); if (btn) btn.disabled = true;
        fetch(base + '/sessions/' + encodeURIComponent(sessionId) + '/weeks/' + encodeURIComponent(ws) + '/reopen', {
            method: 'POST', credentials: 'same-origin',
            headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
            body: JSON.stringify({ reason: reason, expectedVersion: s.version })
        }).then(r => r.text().then(t => { let b = null; try { b = t ? JSON.parse(t) : null; } catch (e) { b = null; } return { ok: r.ok, status: r.status, body: b }; }))
            .then(r => {
                if (r.ok) {
                    try { sessionStorage.setItem(TOAST_KEY, L.ReopenDone || 'Reopened'); } catch (e) { window.showToast?.(L.ReopenDone || 'Reopened', 'success'); }
                    window.location.reload();
                    return;
                }
                if (btn) btn.disabled = false;
                const msg = (r.status === 404 && !r.body) ? (L.ReopenUnavailable || 'HTTP 404')
                    : ((r.body && r.body.errors && r.body.errors.length) ? r.body.errors.join(' · ') : ('HTTP ' + r.status));
                window.showToast?.(msg, 'error');
            })
            .catch(() => { if (btn) btn.disabled = false; window.showToast?.(L.ErrorOccurred || 'Error', 'error'); });
    };

    // ── wiring ──
    const onAction = key => {
        if (key === 'approveWeek') page.request('approve-week');
        else if (key === 'saveTargets') page.request('save-targets');
        else if (key === 'generateRoute') page.request('generate-route');
        else if (key === 'generateWeek') page.request('reload-plan');
        else if (key === 'reopenWeek') openReopen();
        else if (key === 'nextWeek') { const nx = nextOpenWeek(page.state.weekStart); if (nx) page.selectWeek(nx, 'header'); }
    };
    root.addEventListener('click', e => {
        const btn = e.target.closest('[data-vp-action]'); if (!btn || btn.disabled) return;
        e.preventDefault();
        onAction(btn.getAttribute('data-vp-action'));
    });
    el('vp-reopen-reason')?.addEventListener('input', syncReopen);
    el('vp-reopen-confirm')?.addEventListener('click', confirmReopen);
    // 4D — the Weeks tab asks for the same reopen dialog (one component).
    page.on('request:reopen-week', () => openReopen());

    page.on('session', s => {
        setText('vp-d-rep', (s && (s.resourceDisplayName || s.resourceId)) || '—');
        renderRep();
        if (!page.state.weekStart) { const d = defaultWeek(); if (d) page.selectWeek(d, 'default'); }
        refresh();
    });
    page.on('session-error', message => {
        const band = el('vp-week-band');
        if (band) band.innerHTML = '<div class="alert alert-danger py-2 small mb-3" role="alert"><i class="bx bx-error-circle me-1" aria-hidden="true"></i>' + esc(message || L.DetailLoadError || '') + '</div>';
        document.querySelectorAll('[data-vp-action]').forEach(b => (b.closest('[data-vp-action-item]') || b).classList.add('d-none'));
    });
    page.on('preview', () => {
        const ws = page.state.weekStart;
        if (!ws || !page.week(ws)) { const d = defaultWeek(); if (d) page.selectWeek(d, 'force'); }
        refresh();
    });
    page.on('week-change', () => refresh());

    loadMe();
})(window, document);
