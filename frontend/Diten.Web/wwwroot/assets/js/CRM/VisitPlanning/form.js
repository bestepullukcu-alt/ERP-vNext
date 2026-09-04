/**
 * MOD-0155-FU05 Visit Planning — session create / edit form (Golden Compact). PLAN SETUP ONLY.
 * Country → filters Cycle periods (tenant-wide OR the chosen country's). Week is DERIVED from the period's
 * startDate→endDate as Monday-based spans, labelled by ISO-8601 week number ("36. Hafta · 31 Ağu – 6 Eyl 2026"), and
 * the chosen week's Monday (yyyy-MM-dd) is carried to Details as ?week. Segment is a multi-select (UI) but only the FIRST
 * is sent (backend SegmentId is single). Targets are chosen on Details; the saved session is target-less.
 */
(function (window, document) {
    'use strict';
    const root = document.getElementById('visit-planning-form');
    if (!root) return;

    const L = window.L10n || {};
    const $ = window.jQuery;
    const base = '/CRM/VisitPlanning/api';
    const mode = root.dataset.mode;
    const sessionId = root.dataset.sessionId || null;
    let currentVersion = null;
    let allPeriods = [];

    const el = id => document.getElementById(id);
    const headers = () => ({ Accept: 'application/json', 'Content-Type': 'application/json' });
    const opt = (value, label) => { const o = document.createElement('option'); o.value = value; o.textContent = label; return o; };

    const api = (path, options) => {
        options = options || {};
        options.credentials = 'same-origin';
        options.headers = Object.assign(headers(), options.headers || {});
        return fetch(base + path, options).then(r => r.text().then(text => {
            let body = null; try { body = text ? JSON.parse(text) : null; } catch (e) { body = null; }
            return { ok: r.ok, status: r.status, body };
        }));
    };
    const items = body => {
        if (!body) return [];
        const data = body.data !== undefined ? body.data : body;
        if (Array.isArray(data)) return data;
        if (data && Array.isArray(data.items)) return data.items;
        return [];
    };
    const errorText = r => (r.body && r.body.errors && r.body.errors.length) ? r.body.errors.join(' · ') : (r.body && r.body.message) || ('HTTP ' + r.status);

    const initSelect2 = () => {
        if (!$ || !$.fn.select2) return;
        root.querySelectorAll('.select2').forEach(s => {
            const $s = $(s);
            if (!$s.hasClass('select2-hidden-accessible')) $s.select2({ width: '100%', dropdownParent: $(document.body), placeholder: $s.data('placeholder') || '' });
        });
    };
    const refreshSelect2 = id => { if ($ && $.fn.select2) $(el(id)).trigger('change.select2'); };

    // ── date / ISO-week helpers ──
    const asDate = v => { const d = new Date(v); return isNaN(d) ? null : (d.setHours(0, 0, 0, 0), d); };
    const nextMonday = () => { const d = new Date(); d.setHours(0, 0, 0, 0); d.setDate(d.getDate() + ((1 - d.getDay() + 7) % 7)); return d; };
    const mondayOf = date => { const d = new Date(date); d.setHours(0, 0, 0, 0); d.setDate(d.getDate() - ((d.getDay() + 6) % 7)); return d; };
    const ymd = d => { const t = (d instanceof Date) ? d : new Date(d); return isNaN(t) ? '' : t.getFullYear() + '-' + String(t.getMonth() + 1).padStart(2, '0') + '-' + String(t.getDate()).padStart(2, '0'); };
    // ISO-8601 week number: the Thursday of the week decides its year + number; weeks start Monday.
    const isoWeek = date => {
        const d = new Date(date); d.setHours(0, 0, 0, 0);
        d.setDate(d.getDate() + 3 - ((d.getDay() + 6) % 7));           // Thursday of this ISO week
        const week1 = new Date(d.getFullYear(), 0, 4);                 // Jan 4 is always in ISO week 1
        return 1 + Math.round(((d - week1) / 86400000 - 3 + ((week1.getDay() + 6) % 7)) / 7);
    };
    const weekNumberLabel = n => (L.WeekNumberLabel || '{0}. ' + (L.WeekLabel || 'Week')).replace('{0}', n);
    const dm = d => d.toLocaleDateString(undefined, { day: 'numeric', month: 'short' });

    // ── Country → Cycle-period filter. Countries come from the cycle-period scope-options (resolved COUNTRY_CODES), so
    //    the codes match the periods' CountryScope exactly. Shape: data.countries = [{ value, label }]. ──
    const loadCountries = () => api('/scope-options').then(r => {
        const picker = el('vp-country'); picker.innerHTML = '<option value=""></option>';
        const data = (r.body && (r.body.data !== undefined ? r.body.data : r.body)) || {};
        const countries = data.countries || data.Countries || [];
        (Array.isArray(countries) ? countries : []).forEach(c => {
            const code = c.value || c.code || c.isoCode || c.id;
            const name = c.label || c.name || c.text || code;
            if (code) picker.appendChild(opt(code, name));
        });
    }).catch(() => {});

    const loadPeriods = () => api('/cycle-periods').then(r => {
        allPeriods = items(r.body).map(p => ({
            id: p.cyclePeriodId || p.id,
            name: p.cycleName || p.cycleCode || p.name || (p.cyclePeriodId || p.id),
            scopeType: String(p.scopeType || p.ScopeType || '').toLowerCase(),
            countryScope: String(p.countryScope || p.CountryScope || p.country || '').toUpperCase(),
            start: asDate(p.startDate || p.StartDate || p.start),
            end: asDate(p.endDate || p.EndDate || p.end)
        })).filter(p => p.id);
    });

    // tenant-wide OR country-scoped to the chosen country.
    const filterPeriods = country => {
        const picker = el('vp-period');
        const c = String(country || '').toUpperCase();
        const list = c ? allPeriods.filter(p => p.scopeType === 'tenant' || (p.scopeType === 'country' && p.countryScope === c)) : [];
        const keep = picker.value;
        picker.innerHTML = '<option value=""></option>' + list.map(p => '<option value="' + p.id + '">' + (p.name || p.id) + '</option>').join('');
        picker.disabled = !c;
        if ($ && $.fn.select2) $(picker).prop('disabled', !c);
        const hint = el('vp-period-hint'); if (hint) hint.textContent = c ? '' : (L.SelectCountryFirst || '');
        if (keep && list.some(p => p.id === keep)) picker.value = keep; else picker.value = '';
        refreshSelect2('vp-period');
        populateWeeks(picker.value);
    };

    // ── Week — derived from the period's start/end, ISO-labelled, value = the week's Monday (yyyy-MM-dd) ──
    const periodById = id => allPeriods.find(p => p.id === id);
    const deriveWeeks = period => {
        if (!period || !period.start || !period.end) return [];
        const weeks = [];
        let mon = mondayOf(period.start);
        while (mon <= period.end) {
            const fullSun = new Date(mon.getTime() + 6 * 86400000);
            const shownEnd = fullSun > period.end ? period.end : fullSun;
            weeks.push({ value: ymd(mon), iso: isoWeek(mon), mon: new Date(mon), sun: fullSun, label: weekNumberLabel(isoWeek(mon)) + ' · ' + dm(mon) + ' – ' + dm(shownEnd) + ' ' + mon.getFullYear() });
            mon = new Date(mon.getTime() + 7 * 86400000);
        }
        return weeks;
    };
    const defaultWeekValue = weeks => {
        if (!weeks.length) return '';
        const nm = nextMonday();
        const hit = weeks.find(w => nm >= w.mon && nm <= w.sun);
        return hit ? hit.value : (nm < weeks[0].mon ? weeks[0].value : weeks[weeks.length - 1].value);
    };
    const populateWeeks = periodId => {
        const sel = el('vp-week-form'); if (!sel) return;
        const weeks = deriveWeeks(periodById(periodId));
        const keep = sel.value;
        sel.innerHTML = weeks.map(w => '<option value="' + w.value + '">' + w.label + '</option>').join('');
        if (keep && weeks.some(w => w.value === keep)) sel.value = keep;
        else { const def = defaultWeekValue(weeks); if (def) sel.value = def; }
    };

    // ── other loaders ──
    const loadUsers = () => api('/users?pageSize=500').then(r => {
        const picker = el('vp-resource'); picker.innerHTML = '<option value=""></option>';
        const list = items(r.body);
        list.forEach(u => {
            const id = u.id || u.userId || u.UserId || u.value;
            const label = u.displayName || u.fullName || u.name || u.userName || u.email || id;
            if (id) { const o = opt(id, label); o.setAttribute('data-name', label); picker.appendChild(o); }
        });
        const note = el('vp-users-note');
        if (note) note.textContent = (r.ok && list.length) ? '' : (L.UsersUnavailable || '');
    }).catch(() => { const note = el('vp-users-note'); if (note) note.textContent = L.UsersUnavailable || ''; });

    const loadSegments = () => api('/segments').then(r => {
        const picker = el('vp-segment'); picker.innerHTML = '';
        items(r.body).forEach(s => { const id = s.segmentId || s.id; if (id) picker.appendChild(opt(id, s.name || s.segmentName || s.code || id)); });
    });

    const loadStrategyTemplates = () => api('/strategy-templates').then(r => {
        const picker = el('vp-strategy'); const list = items(r.body);
        list.forEach(s => { const id = s.strategyTemplateId || s.id; if (id) picker.appendChild(opt(id, s.name || s.templateName || s.code || id)); });
        const note = el('vp-strategy-note');
        if (note) note.textContent = (r.ok && list.length) ? '' : (L.StrategyTemplatesUnavailable || '');
    }).catch(() => { const note = el('vp-strategy-note'); if (note) note.textContent = L.StrategyTemplatesUnavailable || ''; });

    // ── edit preselect ──
    const loadSession = () => {
        if (mode !== 'edit' || !sessionId) return Promise.resolve();
        return api('/sessions/' + sessionId).then(r => {
            if (!r.ok || !r.body || !r.body.data) return;
            const s = r.body.data;
            currentVersion = s.version;
            // Derive the country from the saved period (country-scoped → its country; tenant → any country enables it).
            const per = periodById(s.cyclePeriodId);
            const countrySel = el('vp-country');
            let country = (per && per.scopeType === 'country' && per.countryScope) || (s.countryCode || s.country || '');
            country = String(country || '').toUpperCase();
            if (!country && countrySel.options.length > 1) country = countrySel.options[1].value; // first real country
            if (country) { countrySel.value = country; refreshSelect2('vp-country'); }
            filterPeriods(country);
            if (s.cyclePeriodId) {
                el('vp-period').value = s.cyclePeriodId; refreshSelect2('vp-period'); populateWeeks(s.cyclePeriodId);
                // Preselect the SAVED week (fall back to populateWeeks' default only when absent).
                const wk = el('vp-week-form');
                if (wk && s.targetWeekStart && Array.prototype.some.call(wk.options, o => o.value === s.targetWeekStart)) {
                    wk.value = s.targetWeekStart; refreshSelect2('vp-week-form');
                }
            }
            if (s.resourceId) { el('vp-resource').value = s.resourceId; refreshSelect2('vp-resource'); }
            if (s.segmentId) { const seg = el('vp-segment'); Array.prototype.forEach.call(seg.options, o => { o.selected = (o.value === s.segmentId); }); refreshSelect2('vp-segment'); }
            if (s.strategyTemplateId) { el('vp-strategy').value = s.strategyTemplateId; refreshSelect2('vp-strategy'); }
        });
    };

    // ── save (target-less; segment = first of the multi-select) ──
    const firstSegment = () => { const seg = el('vp-segment'); const v = Array.prototype.slice.call(seg.selectedOptions).map(o => o.value).filter(Boolean); return v.length ? v[0] : null; };
    const buildPayload = () => {
        const resSel = el('vp-resource').selectedOptions[0];
        return {
            cyclePeriodId: el('vp-period').value,
            resourceId: el('vp-resource').value,
            resourceType: 'person',
            resourceDisplayName: resSel ? (resSel.getAttribute('data-name') || resSel.textContent) : null,
            selectedAccountIds: [],
            selectedPharmacyIds: [],
            selectedContacts: [],
            segmentId: firstSegment(),
            campaignId: null,
            strategyTemplateId: el('vp-strategy').value || null
        };
    };

    const showError = msg => { const b = el('vp-form-error'); if (b) { b.textContent = msg; b.classList.remove('d-none'); } };
    const clearError = () => { const b = el('vp-form-error'); if (b) b.classList.add('d-none'); };

    const save = () => {
        clearError();
        const payload = buildPayload();
        if (!payload.cyclePeriodId || !payload.resourceId) { showError(L.FormValidationError || L.ErrorOccurred || 'Please complete the required fields.'); return; }

        const isEdit = mode === 'edit' && sessionId;
        const url = isEdit ? '/sessions/' + sessionId : '/sessions';
        const method = isEdit ? 'PUT' : 'POST';
        if (isEdit) payload.expectedVersion = currentVersion;
        const week = el('vp-week-form') ? el('vp-week-form').value : ''; // the week's Monday (yyyy-MM-dd)
        if (week) payload.targetWeekStart = week; // PERSIST the chosen week on the session (Details/Edit read it back)

        api(url, { method, body: JSON.stringify(payload) }).then(r => {
            if (r.ok) {
                const newId = (r.body && r.body.data) || sessionId;
                window.showToast?.(isEdit ? (L.RecordUpdated || 'Saved') : (L.RecordCreated || 'Created'), 'success');
                const q = week ? ('?week=' + encodeURIComponent(week)) : '';
                setTimeout(() => window.location.assign('/CRM/VisitPlanning/Details/' + newId + q), 500);
            } else {
                showError(errorText(r));
            }
        });
    };

    el('vp-save')?.addEventListener('click', save);
    // #vp-country and #vp-period are select2 — they raise `change` through jQuery.trigger, which a native
    // addEventListener never sees. Bind through jQuery when select2 is present, native listener otherwise.
    const onChange = (id, fn) => {
        if ($ && $.fn.select2) { $('#' + id).on('change', fn); } else { el(id)?.addEventListener('change', fn); }
    };
    onChange('vp-country', () => filterPeriods(el('vp-country').value));
    onChange('vp-period', () => populateWeeks(el('vp-period').value));

    Promise.all([loadCountries(), loadPeriods(), loadUsers(), loadSegments(), loadStrategyTemplates()])
        .then(() => { initSelect2(); filterPeriods(el('vp-country').value); return loadSession(); });
})(window, document);
