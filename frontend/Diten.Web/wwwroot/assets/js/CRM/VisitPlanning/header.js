/**
 * WP-VP-4B — the Visit Planning DETAIL header (brief §3): summary, the week picker (every week of the period with its
 * status badge), the status-based actions, the reopen dialog, the week / period capacity cards and the page bands.
 * Reads window.VisitPlanningPage (page.js) only; the Targets + Route code (details.js) owns its own views.
 *
 *   week status → actions: VisitPlanningPage.actionsFor (draft: save targets / build route / approve · empty: save
 *   targets / build route · approved: reopen / next open week · past: none · legacy committed plan: none + band).
 *   Every action button carries data-vp-action="<key>" and is shown only when that key is offered AND the server
 *   rendered it (permission flags).
 *
 * Fields that arrive with WP-VP-4A are optional here (graceful fallback):
 *   currentWeekStart     → default week; without it the first draft week, else the first week not yet over;
 *   nextDraftWeekStart   → "Open next week"; without it the next draft / empty week after the selected one;
 *   reopen proxy         → a 404 without a body says "reopen is not available yet" instead of a raw HTTP code.
 */
(function (window, document) {
    'use strict';
    const root = document.getElementById('visit-planning-details');
    const page = window.VisitPlanningPage;
    if (!root || !page) return;

    const L = window.L10n || {};
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
    const requestedWeek = (() => { const w = new URLSearchParams(window.location.search).get('week'); return /^\d{4}-\d{2}-\d{2}$/.test(w || '') ? w : null; })();

    // ── labels ──
    const STATUS_LABEL = { past: 'WeekStatusPast', approved: 'WeekStatusApproved', draft: 'WeekStatusDraft', empty: 'WeekStatusEmpty' };
    const STATUS_TONE = { past: 'secondary', approved: 'success', draft: 'primary', empty: 'secondary' };
    const statusLabel = s => L[STATUS_LABEL[s]] || s || '—';
    const localDate = ws => new Date(ws + 'T00:00:00'); // yyyy-MM-dd as a LOCAL day (never shifted a day by UTC parsing)
    const dm = d => d.toLocaleDateString(undefined, { day: '2-digit', month: 'short' });
    const weekTitle = w => (L.WeekNumberLabel || '{0}. Hafta').replace('{0}', w.isoWeek);
    const weekRange = w => { const f = localDate(w.from || w.weekStart), t = localDate(w.to || w.weekStart); return isNaN(f) || isNaN(t) ? '' : dm(f) + ' – ' + dm(t); };
    const hours = minutes => {
        if (minutes == null || isNaN(minutes)) return '—';
        const h = Math.round((Number(minutes) / 60) * 10) / 10;
        return (L.HoursFormat || '{0} h').replace('{0}', h.toLocaleString(undefined, { maximumFractionDigits: 1 }));
    };

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

    // ── week picker ──
    const renderPicker = () => {
        const sel = el('vp-hdr-week'); if (!sel) return;
        const list = page.weeks();
        if (!list.length) { sel.innerHTML = '<option value="">' + esc(L.Loading || '…') + '</option>'; sel.disabled = true; return; }
        sel.disabled = false;
        sel.innerHTML = list.map(w => '<option value="' + esc(w.weekStart) + '">' + esc(weekTitle(w) + ' · ' + weekRange(w) + ' · ' + statusLabel(w.status)) + '</option>').join('');
        if (page.state.weekStart && list.some(w => w.weekStart === page.state.weekStart)) sel.value = page.state.weekStart;
    };

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
            const item = btn.closest('[data-vp-action-item]') || btn;
            item.classList.toggle('d-none', !show);
        });
        const badge = el('vp-hdr-week-status');
        if (badge) { badge.textContent = statusLabel(status); badge.className = 'badge bg-label-' + (STATUS_TONE[status] || 'secondary'); }
        setText('vp-d-status-strip', legacy ? (L.LegacyPlanBadge || '') : statusLabel(status));
        if (w) setText('vp-d-week', weekTitle(w) + ' · ' + weekRange(w));

        const band = el('vp-week-band'); if (!band) return;
        let text = '';
        if (legacy) text = L.LegacyPlanBand || '';
        else if (status === 'approved') text = L.WeekLocked || '';
        else if (status === 'past') text = L.PastWeekBand || '';
        band.innerHTML = text ? '<div class="alert alert-info py-2 small mb-3" role="status"><i class="bx bx-lock-alt me-1" aria-hidden="true"></i>' + esc(text) + '</div>' : '';
        // The legacy band replaces the generic read-only notice (one sentence, not two).
        el('vp-readonly-notice')?.classList.toggle('d-none', legacy);
    };

    // ── capacity cards (C5): this week and the period, minutes shown as hours ──
    const renderCapacity = () => {
        const p = page.state.preview;
        const ws = page.state.weekStart;
        const wc = p && Array.isArray(p.weekCapacity) ? p.weekCapacity.find(c => c.weekStart === ws) : null;
        const pc = p ? p.periodCapacity : null;
        setText('vp-cap-week', wc ? hours(wc.capacityMinutes) : '—');
        setText('vp-cap-week-planned', wc ? hours(wc.plannedMinutes) : '—');
        setText('vp-cap-period', pc ? hours(pc.capacityMinutes) : '—');
        setText('vp-cap-period-planned', pc ? hours(pc.plannedMinutes) : '—');
        const pct = pc && pc.capacityMinutes > 0 ? Math.min(100, Math.round(pc.plannedMinutes / pc.capacityMinutes * 100)) : 0;
        const bar = el('vp-cap-period-bar');
        if (bar) { bar.style.width = pct + '%'; bar.setAttribute('aria-valuenow', String(pct)); bar.classList.toggle('bg-warning', pct >= 100); }
        setText('vp-cap-period-pct', pc && pc.capacityMinutes > 0 ? pct + '%' : '');
        const note = el('vp-cap-note');
        if (note) {
            const fallback = pc && pc.budgetSource === 'default_hours';
            note.textContent = fallback ? (L.CapacityDefaultHours || '') : '';
            note.classList.toggle('d-none', !fallback);
        }
    };

    const refresh = () => { renderPicker(); renderActions(); renderCapacity(); };

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
        setText('vp-reopen-count', String(v.trim().length) + ' / ' + REOPEN_MIN);
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
        else if (key === 'reopenWeek') openReopen();
        else if (key === 'nextWeek') { const nx = nextOpenWeek(page.state.weekStart); if (nx) page.selectWeek(nx, 'header'); }
    };
    root.addEventListener('click', e => {
        const btn = e.target.closest('[data-vp-action]'); if (!btn || btn.disabled) return;
        e.preventDefault();
        onAction(btn.getAttribute('data-vp-action'));
    });
    el('vp-hdr-week')?.addEventListener('change', function () { if (this.value) page.selectWeek(this.value, 'header'); });
    el('vp-reopen-reason')?.addEventListener('input', syncReopen);
    el('vp-reopen-confirm')?.addEventListener('click', confirmReopen);

    page.on('session', s => {
        setText('vp-d-rep', (s && (s.resourceDisplayName || s.resourceId)) || '—');
        if (!page.state.weekStart) { const d = defaultWeek(); if (d) page.selectWeek(d, 'default'); }
        refresh();
    });
    page.on('session-error', message => {
        const band = el('vp-week-band');
        if (band) band.innerHTML = '<div class="alert alert-danger py-2 small mb-3" role="alert"><i class="bx bx-error-circle me-1" aria-hidden="true"></i>' + esc(message || L.DetailLoadError || '') + '</div>';
        const sel = el('vp-hdr-week'); if (sel) { sel.innerHTML = ''; sel.disabled = true; }
        document.querySelectorAll('[data-vp-action]').forEach(b => (b.closest('[data-vp-action-item]') || b).classList.add('d-none'));
    });
    page.on('preview', () => {
        const ws = page.state.weekStart;
        if (!ws || !page.week(ws)) { const d = defaultWeek(); if (d) page.selectWeek(d, 'force'); }
        refresh();
    });
    page.on('week-change', () => refresh());

    renderPicker();
})(window, document);
