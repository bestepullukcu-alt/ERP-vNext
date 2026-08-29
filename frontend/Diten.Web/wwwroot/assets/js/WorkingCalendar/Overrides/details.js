/**
 * Working Calendars — Details page.
 * Read-only view of one calendar plus a resolution probe. The probe is the important part: it surfaces the
 * provider's answer AND its reason codes, including "no calendar data", which is an answer rather than an error.
 */
'use strict';

(function () {
    const root = document.querySelector('.working-calendar-override-details');
    if (!root) return;

    const endpoint = '/WorkingCalendar/Overrides/api';
    const calendarId = root.getAttribute('data-calendar-id') || location.pathname.split('/').filter(Boolean).pop();
    const L = window.L10n || {};

    const getAuthHeaders = (includeJson = false) =>
        window.DitenDataTable?.getAuthHeaders?.(includeJson) || (includeJson ? { 'Content-Type': 'application/json' } : {});

    /**
     * Pulls the real reason out of the API's error envelope.
     * The envelope is {data, statusCode, isSuccessful, errors:[...], reason_code} — there is NO `message` field, so
     * reading `body.message` always fell through to the generic toast and hid the actual 409/400 reason (duplicate
     * code, reserved day type, concurrency). `message` is still tried second for the ProblemDetails-shaped replies
     * the Gateway emits on its own (those carry `title`/`detail` instead).
     */
    const apiError = (body) =>
        (Array.isArray(body?.errors) && body.errors[0])
        || body?.message
        || body?.detail
        || L.ErrorOccurred;
    const unwrap = (payload) => payload?.data ?? payload?.Data ?? payload;
    const setText = (id, value) => {
        const el = document.getElementById(id);
        if (el) el.textContent = (value === null || value === undefined || value === '') ? '—' : String(value);
    };
    /**
     * Contract tokens are hyphenated slugs ("working-day-override"), which cannot be C# identifiers, so their
     * labels arrive in nested maps. Falling through to the raw token is the last resort — it keeps the screen
     * working rather than blank, and a missing label is visible instead of silently rendering an empty cell.
     */
    const labelFor = (token) => {
        if (!token) return '';
        return L.DayTypeLabels?.[token]
            || L.RecurrenceLabels?.[token]
            || L.DayStatusLabels?.[token]
            || L.ScopeTypeLabels?.[token]
            || L[token]
            || token;
    };

    let current = null;
    let contract = null;
    let legalEntityLabels = new Map();
    // A day can only be authored while the calendar is still writable. An archived calendar accepts nothing, so the
    // buttons are not rendered at all rather than rendered and then refused.
    let canWrite = false;

    const statusClass = (status) => ({
        active: 'bg-label-success',
        draft: 'bg-label-warning',
        archived: 'bg-label-secondary'
    }[status] || 'bg-label-primary');

    const escapeHtml = (value) => String(value ?? '')
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

    const renderDays = (days) => {
        const body = document.getElementById('wc-days-body');
        if (!body) return;
        if (!Array.isArray(days) || days.length === 0) {
            body.innerHTML = `<tr><td colspan="8" class="text-muted">${L.NotAvailable || '—'}</td></tr>`;
            return;
        }
        // An archived day stays listed but greyed out and without actions: it is history, not a live entry, and
        // hiding it would silently rewrite what the calendar used to say.
        body.innerHTML = days.map((d) => {
            const archived = d.dayStatus === 'archived';
            const actions = archived || !canWrite
                ? ''
                : `<button type="button" class="btn btn-sm btn-icon btn-text-secondary js-edit-day" data-day-id="${d.dayId}" title="${escapeHtml(L.Edit)}"><i class="icon-base bx bx-edit"></i></button>
                   <button type="button" class="btn btn-sm btn-icon btn-text-danger js-archive-day" data-day-id="${d.dayId}" title="${escapeHtml(L.Archive)}"><i class="icon-base bx bx-archive"></i></button>`;
            return `
            <tr class="${archived ? 'text-muted' : ''}">
                <td>${escapeHtml(d.dayCode)}</td>
                <td>${escapeHtml(d.dayName)}</td>
                <td>${escapeHtml(d.date)}</td>
                <td>${d.observedDate ? escapeHtml(d.observedDate) : '—'}</td>
                <td><span class="badge bg-label-info">${escapeHtml(labelFor(d.dayType))}</span></td>
                <td>${d.isHalfDay ? '✓' : '—'}</td>
                <td><span class="badge ${archived ? 'bg-label-secondary' : 'bg-label-success'}">${escapeHtml(labelFor(d.dayStatus))}</span></td>
                <td class="text-end">${actions}</td>
            </tr>`;
        }).join('');
    };

    const render = (dto) => {
        current = dto;
        const readOnly = dto.isReadOnly === true || dto.isCountryLayer === true;
        canWrite = !readOnly && dto.calendarStatus !== 'archived';
        setText('wc-title', dto.calendarName);
        setText('wc-subtitle', `${dto.calendarCode} · ${dto.countryCode} · ${dto.calendarYear}`);
        setText('wc-code', dto.calendarCode);
        setText('wc-name', dto.calendarName);
        setText('wc-description', dto.description);
        setText('wc-country', dto.countryCode);
        setText('wc-year', dto.calendarYear);
        setText('wc-scope', labelFor(dto.scopeType));
        setText('wc-org-unit', dto.organizationUnitId);
        setText('wc-legal-entity', dto.legalEntityId
            ? (legalEntityLabels.get(String(dto.legalEntityId).toLowerCase()) || L.LegalEntitiesUnavailable)
            : null);
        setText('wc-notes', dto.notes);

        const statusEl = document.getElementById('wc-status');
        if (statusEl) {
            statusEl.className = `badge ${statusClass(dto.calendarStatus)}`;
            statusEl.textContent = labelFor(`Status${(dto.calendarStatus || '').charAt(0).toUpperCase()}${(dto.calendarStatus || '').slice(1)}`);
        }

        const weekend = Array.isArray(dto.effectiveWeekendDays) ? dto.effectiveWeekendDays : [];
        setText('wc-weekend', weekend.length ? weekend.map(labelFor).join(', ') : null);

        // The inherited case is stated in words. An override that inherits must not look unconfigured.
        const inheritedBox = document.getElementById('wc-weekend-inherited');
        const inheritedText = document.getElementById('wc-weekend-inherited-text');
        if (inheritedBox && inheritedText) {
            if (dto.weekendInherited && weekend.length) {
                inheritedText.textContent = `${L.InheritedFromCountry || 'Inherited from the country calendar'}: ${weekend.map(labelFor).join(', ')}`;
                inheritedBox.classList.remove('d-none');
            } else {
                inheritedBox.classList.add('d-none');
            }
        }

        renderDays(dto.days);

        const editBtn = document.getElementById('wc-btn-edit');
        if (editBtn) {
            editBtn.href = canWrite ? `/WorkingCalendar/Overrides/Edit/${dto.id}` : '#';
            editBtn.classList.toggle('d-none', !canWrite);
        }
        document.getElementById('wc-btn-activate')?.classList.toggle(
            'd-none', !canWrite || dto.calendarStatus !== 'draft');
        document.getElementById('wc-btn-archive')?.classList.toggle('d-none', !canWrite);
        // Holidays stay editable on an ACTIVE calendar on purpose: official dates get declared and shifted mid-year,
        // and a frozen active calendar could not follow that. Only identity is frozen (§8.6).
        document.getElementById('wc-btn-add-day')?.classList.toggle('d-none', !canWrite);

        const probeInput = document.getElementById('wc-probe-date');
        if (probeInput && !probeInput.value) probeInput.value = `${dto.calendarYear}-01-01`;
    };

    const loadLegalEntities = async () => {
        try {
            const res = await fetch(`${endpoint}/legal-entities`, { credentials: 'same-origin', headers: getAuthHeaders() });
            if (!res.ok) return;
            const items = unwrap(await res.json()) || [];
            legalEntityLabels = new Map(items.map((entity) => [
                String(entity.legalEntityId || entity.id).toLowerCase(),
                [entity.code, entity.displayName || entity.legalName].filter(Boolean).join(' — ')
            ]));
        } catch (error) {
            console.warn('[WorkingCalendarOverrides Details] Legal entities unavailable.', error);
        }
    };

    const load = async () => {
        try {
            const [res] = await Promise.all([
                fetch(`${endpoint}/${calendarId}`, { credentials: 'same-origin', headers: getAuthHeaders() }),
                loadLegalEntities()
            ]);
            if (!res.ok) {
                window.showToast?.(L.ErrorOccurred, 'error');
                return;
            }
            render(unwrap(await res.json()));
        } catch (error) {
            console.error('[WorkingCalendarOverrides Details] Load failed.', error);
            window.showToast?.(L.ErrorOccurred, 'error');
        }
    };

    const lifecycle = async (action) => {
        if (!current) return;
        try {
            const res = await fetch(`${endpoint}/${current.id}/${action}`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: getAuthHeaders(true),
                body: JSON.stringify({ expectedVersion: current.version })
            });
            if (!res.ok) {
                const body = await res.json().catch(() => null);
                window.showToast?.(apiError(body), 'error');
                return;
            }
            window.showToast?.(L.RecordUpdated || L.RecordSaved, 'success');
            await load();
        } catch (error) {
            console.error(`[WorkingCalendarOverrides Details] ${action} failed.`, error);
            window.showToast?.(L.ErrorOccurred, 'error');
        }
    };

    /**
     * An unresolved probe is NOT an error. "calendar_missing" means nobody has entered that country/year yet, and
     * the page says so in those words instead of showing a red failure or, worse, a confident wrong answer.
     */
    const resolutionPresentation = (resolution) => ({
        resolved: { cls: 'bg-label-success', text: L.ResolutionResolved || 'resolved' },
        calendar_missing: { cls: 'bg-label-secondary', text: L.ResolutionCalendarMissing || L.NoCalendarData || 'no calendar data' },
        year_missing: { cls: 'bg-label-secondary', text: L.ResolutionYearMissing || 'year missing' },
        country_unknown: { cls: 'bg-label-warning', text: L.ResolutionCountryUnknown || 'country unknown' },
        invalid_range: { cls: 'bg-label-warning', text: L.ErrorOccurred || 'invalid range' }
    }[resolution] || { cls: 'bg-label-primary', text: resolution });

    const probe = async () => {
        const date = document.getElementById('wc-probe-date')?.value;
        if (!date || !current) return;

        const params = new URLSearchParams({ op: 'is-working-day', date, countryCode: current.countryCode });
        if (current.organizationUnitId) params.set('organizationUnitId', current.organizationUnitId);
        if (current.legalEntityId) params.set('legalEntityId', current.legalEntityId);

        try {
            const res = await fetch(`${endpoint}/resolve?${params.toString()}`, {
                credentials: 'same-origin', headers: getAuthHeaders()
            });
            const dto = unwrap(await res.json());
            const box = document.getElementById('wc-probe-result');
            const badge = document.getElementById('wc-probe-badge');
            const reason = document.getElementById('wc-probe-reason');
            const codes = document.getElementById('wc-probe-codes');
            if (!box || !badge || !reason || !codes) return;

            const presentation = resolutionPresentation(dto?.resolution);
            const isWorking = dto?.isWorkingDay;
            badge.className = `badge ${dto?.resolution === 'resolved' ? (isWorking ? 'bg-label-success' : 'bg-label-danger') : presentation.cls}`;
            badge.textContent = dto?.resolution === 'resolved'
                ? (isWorking ? (L.StatusActive || 'working day') : (L.Passive || 'non-working day'))
                : presentation.text;

            // The provider's own explanation is shown verbatim; the UI does not paraphrase or hide it.
            reason.textContent = dto?.selectionReason || '';
            codes.innerHTML = (dto?.reasonCodes || [])
                .map((c) => `<span class="badge bg-label-secondary">${c}</span>`)
                .join('');
            box.classList.remove('d-none');
        } catch (error) {
            console.error('[WorkingCalendarOverrides Details] Probe failed.', error);
            window.showToast?.(L.ErrorOccurred, 'error');
        }
    };

    // ── Day authoring ────────────────────────────────────────────────────────

    const dayEl = (id) => document.getElementById(id);
    const dayError = (message) => {
        const box = dayEl('day-editor-error');
        if (!box) return;
        if (!message) {
            box.classList.add('d-none');
            box.textContent = '';
            return;
        }
        box.textContent = message;
        box.classList.remove('d-none');
    };

    const fillSelect = (select, values) => {
        if (!select) return;
        select.innerHTML = '';
        values.forEach((v) => {
            const opt = document.createElement('option');
            opt.value = v;
            opt.textContent = labelFor(v);
            select.appendChild(opt);
        });
    };

    /**
     * Day types come from the contract, never from markup. That is what keeps the two surfaces honest: the tenant
     * slice simply does not contain public/religious/moveable holiday, so the tenant dialog structurally cannot
     * offer a country-layer type. If someone bypasses the UI, the backend still answers 400
     * day_type_reserved_for_country_layer.
     */
    const loadContract = async () => {
        if (contract) return contract;
        try {
            const res = await fetch(`${endpoint}/contract`, { credentials: 'same-origin', headers: getAuthHeaders() });
            if (!res.ok) return null;
            contract = unwrap(await res.json()) || {};
            fillSelect(dayEl('day-type'), contract.dayTypes || contract.DayTypes || []);
            fillSelect(dayEl('day-recurrence'), contract.recurrences || contract.Recurrences || []);
            return contract;
        } catch (error) {
            console.error('[WorkingCalendarOverrides Day] Contract load failed.', error);
            return null;
        }
    };

    const syncHalfDayAvailability = () => {
        // A compensation day both forces work and cannot be "half" — the backend rejects the combination
        // (half_day_on_override), so the checkbox is disabled rather than offered and then refused.
        const isOverride = dayEl('day-type')?.value === 'working-day-override';
        const checkbox = dayEl('day-half-day');
        if (!checkbox) return;
        if (isOverride) {
            checkbox.checked = false;
        }
        checkbox.disabled = isOverride;
        dayEl('day-halfday-wrapper')?.classList.toggle('opacity-50', isOverride);
        const hint = dayEl('day-type-hint');
        if (hint) hint.textContent = isOverride ? (L.WorkingDayOverrideHint || '') : '';
    };

    const openDayEditor = async (day) => {
        await loadContract();
        dayError(null);

        dayEl('day-id').value = day?.dayId || '';
        dayEl('day-code').value = day?.dayCode || '';
        dayEl('day-name').value = day?.dayName || '';
        dayEl('day-date').value = day?.date || (current ? `${current.calendarYear}-01-01` : '');
        dayEl('day-observed').value = day?.observedDate || '';
        dayEl('day-notes').value = day?.notes || '';
        if (day?.dayType) dayEl('day-type').value = day.dayType;
        if (day?.recurrence) dayEl('day-recurrence').value = day.recurrence;
        dayEl('day-half-day').checked = !!day?.isHalfDay;
        syncHalfDayAvailability();

        const title = document.getElementById('offcanvasDayEditorLabel');
        if (title) title.textContent = day ? (L.EditDay || L.Edit) : (L.AddDay || '');

        const el = document.getElementById('offcanvasDayEditor');
        if (el) bootstrap.Offcanvas.getOrCreateInstance(el).show();
    };

    const closeDayEditor = () => {
        const el = document.getElementById('offcanvasDayEditor');
        if (el) bootstrap.Offcanvas.getOrCreateInstance(el).hide();
    };

    const saveDay = async (event) => {
        event.preventDefault();
        const form = dayEl('dayEditorForm');
        if (!form?.checkValidity()) {
            form?.reportValidity();
            return;
        }
        if (!current) return;

        const dayId = dayEl('day-id').value;
        const payload = {
            dayId: dayId || null,
            dayCode: dayEl('day-code').value.trim(),
            dayName: dayEl('day-name').value.trim(),
            date: dayEl('day-date').value,
            observedDate: dayEl('day-observed').value || null,
            dayType: dayEl('day-type').value,
            recurrence: dayEl('day-recurrence').value,
            isHalfDay: dayEl('day-half-day').checked,
            notes: dayEl('day-notes').value.trim() || null,
            expectedVersion: current.version
        };

        try {
            const res = await fetch(`${endpoint}/${current.id}/days`, {
                method: 'POST',
                credentials: 'same-origin',
                headers: getAuthHeaders(true),
                body: JSON.stringify(payload)
            });

            if (!res.ok) {
                // 400 (reserved day type, year mismatch, half-day-on-override) and 409 (duplicate code/date,
                // stale version) are shown exactly as the server phrased them. Nothing is paraphrased or invented.
                const body = await res.json().catch(() => null);
                dayError(apiError(body));
                return;
            }

            closeDayEditor();
            window.showToast?.(dayId ? (L.RecordUpdated || L.RecordSaved) : (L.RecordCreated || L.RecordSaved), 'success');
            await load();
        } catch (error) {
            console.error('[WorkingCalendarOverrides Day] Save failed.', error);
            dayError(L.ErrorOccurred);
        }
    };

    /** Archive, never delete — the day stops answering questions but stays in the record. */
    const archiveDay = (dayId) => {
        if (!current || !dayId) return;
        window.showConfirm?.(L.ArchiveDayConfirm || L.AreYouSure, async () => {
            try {
                const res = await fetch(`${endpoint}/${current.id}/days/${dayId}/archive`, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: getAuthHeaders(true),
                    body: JSON.stringify({ expectedVersion: current.version })
                });
                if (!res.ok) {
                    const body = await res.json().catch(() => null);
                    window.showToast?.(apiError(body), 'error');
                    return;
                }
                window.showToast?.(L.RecordUpdated || L.RecordSaved, 'success');
                await load();
            } catch (error) {
                console.error('[WorkingCalendarOverrides Day] Archive failed.', error);
                window.showToast?.(L.ErrorOccurred, 'error');
            }
        }, { type: 'danger', confirmButtonText: L.Archive });
    };

    const bindDayEditor = () => {
        dayEl('wc-btn-add-day')?.addEventListener('click', () => void openDayEditor(null));
        dayEl('dayEditorForm')?.addEventListener('submit', saveDay);
        dayEl('day-type')?.addEventListener('change', syncHalfDayAvailability);

        // Delegation: the day table is re-rendered after every save, so per-row handlers would be lost.
        dayEl('wc-days-body')?.addEventListener('click', (event) => {
            const editBtn = event.target.closest('.js-edit-day');
            if (editBtn) {
                const day = (current?.days || []).find((d) => d.dayId === editBtn.getAttribute('data-day-id'));
                void openDayEditor(day || null);
                return;
            }
            const archiveBtn = event.target.closest('.js-archive-day');
            if (archiveBtn) {
                archiveDay(archiveBtn.getAttribute('data-day-id'));
            }
        });
    };

    document.addEventListener('DOMContentLoaded', () => {
        void load();
        bindDayEditor();
        document.getElementById('wc-btn-activate')?.addEventListener('click', () => {
            window.showConfirm?.(L.ActivateConfirm || L.AreYouSure, () => void lifecycle('activate'),
                { entityName: current?.calendarName, type: 'warning', confirmButtonText: L.Activate });
        });
        document.getElementById('wc-btn-archive')?.addEventListener('click', () => {
            window.showConfirm?.(L.ArchiveConfirm || L.AreYouSure, () => void lifecycle('archive'),
                { entityName: current?.calendarName, type: 'danger', confirmButtonText: L.Archive });
        });
        document.getElementById('wc-btn-probe')?.addEventListener('click', () => void probe());
    });
})();
