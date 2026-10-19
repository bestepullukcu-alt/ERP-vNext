/**
 * WP-VP-4B (MK-1, brief §2) — the "New plan" right-hand drawer, shared by the list and the detail.
 *   - Country (WP-VP-4D, E4-4B-4): the rep's country, read-only. There is no rep-country field yet, so it is the
 *     country of the open periods (country-scoped); a choice only when those periods span several countries — never the
 *     tenant's whole country list.
 *   - Period: automatic — the period active today, else the next one; only active / future periods are offered.
 *   - Opening week: every week of the period that is NOT over yet (a past week is never listed — E7-B5); default = the
 *     current week, else the period's first open week.
 *   - Rep: the signed-in person (resources/me), read-only. No audience or play picker at all (K-3, K-4).
 *   - One plan per rep + period: an existing plan is announced up front and on the server's 409
 *     planning_session_exists, with "Go to plan" (the id from the answer). Up front it is the SAME plan the server would
 *     name (E4-4B-1): the OLDEST not-archived plan of the rep + period.
 * Opens from any [data-vp-new-plan] element or window.VisitPlanningNewPlan.open().
 */
(function (window, document) {
    'use strict';
    const drawer = document.getElementById('vp-new-plan-drawer');
    if (!drawer) return;

    const L = window.L10n || {};
    const base = '/CRM/VisitPlanning/api';
    const el = id => document.getElementById(id);
    const esc = s => { const d = document.createElement('div'); d.textContent = s == null ? '' : String(s); return d.innerHTML; };

    const api = (path, options) => {
        options = options || {};
        options.credentials = 'same-origin';
        options.headers = Object.assign({ Accept: 'application/json', 'Content-Type': 'application/json' }, options.headers || {});
        return fetch(base + path, options).then(r => r.text().then(t => { let b = null; try { b = t ? JSON.parse(t) : null; } catch (e) { b = null; } return { ok: r.ok, status: r.status, body: b }; }));
    };
    const items = body => { if (!body) return []; const d = body.data !== undefined ? body.data : body; return Array.isArray(d) ? d : (d && Array.isArray(d.items) ? d.items : []); };

    // ── dates (local days) ──
    const day = v => { const d = new Date(v); if (isNaN(d)) return null; d.setHours(0, 0, 0, 0); return d; };
    const today = () => { const d = new Date(); d.setHours(0, 0, 0, 0); return d; };
    const mondayOf = date => { const d = new Date(date); d.setHours(0, 0, 0, 0); d.setDate(d.getDate() - ((d.getDay() + 6) % 7)); return d; };
    const ymd = d => d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0');
    const isoWeek = date => { const d = new Date(date); d.setHours(0, 0, 0, 0); d.setDate(d.getDate() + 3 - ((d.getDay() + 6) % 7)); const w1 = new Date(d.getFullYear(), 0, 4); return 1 + Math.round(((d - w1) / 86400000 - 3 + ((w1.getDay() + 6) % 7)) / 7); };
    const dm = d => window.VisitPlanningFormat.dayMonth(d); // WP-VP-4H — the app's language

    /** The period's weeks a plan may OPEN on: Monday-weeks touching the period whose Sunday is today or later — a week
     *  that is already over is never listed. */
    const openingWeeks = (period, now) => {
        const out = [];
        if (!period || !period.start || !period.end) return out;
        for (let mon = mondayOf(period.start); mon <= period.end; mon = new Date(mon.getTime() + 7 * 86400000)) {
            const sun = new Date(mon.getTime() + 6 * 86400000);
            if (sun < now) continue; // already over (E7-B5)
            const shownEnd = sun > period.end ? period.end : sun;
            out.push({ value: ymd(mon), mon: new Date(mon), sun, label: (L.WeekNumberLabel || '{0}. Hafta').replace('{0}', isoWeek(mon)) + ' · ' + dm(mon) + ' – ' + dm(shownEnd) });
        }
        return out;
    };
    /** Only periods that are active today or still ahead. */
    const openPeriods = (periods, now) => periods.filter(p => p.end && p.end >= now).sort((a, b) => a.start - b.start);
    const defaultPeriod = (list, now) => list.find(p => p.start <= now && now <= p.end) || list[0] || null;

    let countries = [], periods = [], me = null, plans = [], loaded = null;

    const load = () => loaded || (loaded = Promise.all([
        api('/scope-options').then(r => {
            const d = (r.body && (r.body.data !== undefined ? r.body.data : r.body)) || {};
            countries = (d.countries || d.Countries || []).map(c => ({ code: String(c.value || c.code || c.id || '').toUpperCase(), name: c.label || c.name || c.value || c.code })).filter(c => c.code);
        }).catch(() => { countries = []; }),
        api('/cycle-periods').then(r => {
            periods = items(r.body).map(p => ({
                id: p.cyclePeriodId || p.id,
                name: p.cycleName || p.cycleCode || p.name || (p.cyclePeriodId || p.id),
                scopeType: String(p.scopeType || '').toLowerCase(),
                country: String(p.countryScope || p.country || '').toUpperCase(),
                start: day(p.startDate || p.start), end: day(p.endDate || p.end)
            })).filter(p => p.id && p.start && p.end);
        }).catch(() => { periods = []; }),
        api('/me').then(r => { me = ((r.body && r.body.data && r.body.data.items) || [])[0] || null; }).catch(() => { me = null; }),
        api('/sessions').then(r => { plans = items(r.body); }).catch(() => { plans = []; })
    ]));

    // The countries a plan can be made for: those of the open, country-scoped periods (E4-4B-4).
    const periodCountries = () => Array.from(new Set(openPeriods(periods, today())
        .filter(p => p.scopeType === 'country' && p.country).map(p => p.country)));
    // WP-VP-4J (6) — the country in the UI language ("Türkiye" / "Turkey" / "تركيا"): the browser's region names for the
    // application's culture (<html lang>), else the scope options' label, else the code.
    const regionNames = (() => {
        try { return new Intl.DisplayNames([window.VisitPlanningFormat ? window.VisitPlanningFormat.culture() : 'tr'], { type: 'region' }); } catch (e) { return null; }
    })();
    const countryName = code => {
        let local = null;
        try { local = regionNames && /^[A-Z]{2}$/.test(code) ? regionNames.of(code) : null; } catch (e) { local = null; }
        if (local && local !== code) return local;
        const c = countries.find(x => x.code === code);
        return c ? c.name : code;
    };
    const selectedCountry = () => {
        const list = periodCountries();
        if (list.length <= 1) return list[0] || '';
        const sel = el('vp-np-country');
        return sel ? sel.value : '';
    };
    const periodsFor = country => openPeriods(periods.filter(p => p.scopeType !== 'country' || !country || p.country === country), today());

    // The server's rule (3A, CreatePlanningSession 409): the OLDEST not-archived plan of the rep + period.
    const existingPlanFor = periodId => plans
        .filter(s => (s.cyclePeriodId === periodId)
            && String(s.status || '').toLowerCase() !== 'archived'
            && (!me || !me.resourceId || String(s.resourceId || '').toLowerCase() === String(me.resourceId).toLowerCase()))
        .sort((a, b) => String(a.createdAt || '').localeCompare(String(b.createdAt || '')))[0];

    const showExists = id => {
        const box = el('vp-np-exists'); if (!box) return;
        box.classList.toggle('d-none', !id);
        const go = el('vp-np-goto'); if (go && id) go.setAttribute('href', '/CRM/VisitPlanning/Details/' + encodeURIComponent(id));
        el('vp-np-save').disabled = !!id || !el('vp-np-period').value || !el('vp-np-week').value;
    };
    const showError = text => { const b = el('vp-np-error'); if (b) { b.textContent = text || ''; b.classList.toggle('d-none', !text); } };

    const renderWeeks = () => {
        const p = periods.find(x => x.id === el('vp-np-period').value);
        const now = today();
        const weeks = openingWeeks(p, now);
        const sel = el('vp-np-week');
        sel.innerHTML = weeks.map(w => '<option value="' + esc(w.value) + '">' + esc(w.label) + '</option>').join('');
        const current = weeks.find(w => now >= w.mon && now <= w.sun);
        if (current) sel.value = current.value; else if (weeks.length) sel.value = weeks[0].value;
        const existing = p ? existingPlanFor(p.id) : null;
        showExists(existing ? (existing.planningSessionId || existing.id) : null);
    };
    const renderPeriods = () => {
        const list = periodsFor(selectedCountry());
        const sel = el('vp-np-period');
        sel.innerHTML = list.map(p => '<option value="' + esc(p.id) + '">' + esc(p.name) + '</option>').join('');
        const def = defaultPeriod(list, today());
        if (def) sel.value = def.id;
        el('vp-np-no-period').classList.toggle('d-none', list.length > 0);
        el('vp-np-period-hint').classList.toggle('d-none', list.length === 0);
        sel.disabled = list.length === 0;
        renderWeeks();
    };
    const renderCountry = () => {
        const text = el('vp-np-country-text'), sel = el('vp-np-country');
        const wrap = el('vp-np-country-wrap') || text;
        const list = periodCountries();
        if (list.length > 1) {
            wrap.classList.add('d-none'); sel.classList.remove('d-none');
            sel.innerHTML = list.map(code => '<option value="' + esc(code) + '">' + esc(countryName(code)) + '</option>').join('');
            const def = defaultPeriod(openPeriods(periods, today()), today());
            if (def && def.scopeType === 'country' && list.indexOf(def.country) > -1) sel.value = def.country;
        } else {
            sel.classList.add('d-none'); wrap.classList.remove('d-none');
            text.value = list.length === 1 ? countryName(list[0]) : '—';
        }
        el('vp-np-rep').value = me ? (me.displayName || me.resourceId || '—') : '—';
    };

    const save = () => {
        showError('');
        const periodId = el('vp-np-period').value, week = el('vp-np-week').value;
        if (!periodId || !week) return;
        const btn = el('vp-np-save'); btn.disabled = true;
        // A new plan starts with no targets; the server writes the signed-in rep as its resource.
        api('/sessions', {
            method: 'POST',
            body: JSON.stringify({ cyclePeriodId: periodId, targetWeekStart: week, selectedAccountIds: [], selectedPharmacyIds: [], selectedContacts: [] })
        }).then(r => {
            if (r.ok && r.body && r.body.data) {
                window.location.assign('/CRM/VisitPlanning/Details/' + encodeURIComponent(r.body.data) + '?week=' + encodeURIComponent(week));
                return;
            }
            const errors = (r.body && r.body.errors) || [];
            if (r.status === 409 && errors[0] === 'planning_session_exists') {
                showExists((r.body && r.body.data) || errors[2]); // "You already have a plan for this period" → Go to plan
                return;
            }
            btn.disabled = false;
            showError(errors.length ? errors.join(' · ') : (L.ErrorOccurred || ('HTTP ' + r.status)));
        });
    };

    const open = () => {
        showError('');
        const canvas = window.bootstrap ? window.bootstrap.Offcanvas.getOrCreateInstance(drawer) : null;
        if (canvas) canvas.show();
        load().then(() => { renderCountry(); renderPeriods(); });
    };

    el('vp-np-country')?.addEventListener('change', renderPeriods);
    el('vp-np-period')?.addEventListener('change', renderWeeks);
    el('vp-np-week')?.addEventListener('change', () => { const p = el('vp-np-period').value; const ex = existingPlanFor(p); showExists(ex ? (ex.planningSessionId || ex.id) : null); });
    el('vp-np-save')?.addEventListener('click', save);
    document.addEventListener('click', e => { const t = e.target.closest('[data-vp-new-plan]'); if (t) { e.preventDefault(); open(); } });

    window.VisitPlanningNewPlan = Object.freeze({ open, openingWeeks, openPeriods });
})(window, document);
