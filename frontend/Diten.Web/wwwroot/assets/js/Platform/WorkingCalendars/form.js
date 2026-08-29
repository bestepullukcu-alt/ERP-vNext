/**
 * Working Calendars — Create/Edit form.
 * Every option list is served by the API: countries from the MOD-0048 reference set, everything else from the
 * module contract. Nothing is hardcoded here, so the form can never offer a value the backend rejects.
 */
'use strict';

(function () {
    const form = document.getElementById('workingCalendarForm');
    if (!form) return;

    const endpoint = '/Platform/WorkingCalendars/api';
    const L = window.L10n || {};

    const idField = form.querySelector('[name="Id"]');
    const calendarId = idField?.value || '';
    const isEdit = !!calendarId;

    const countrySelect = document.getElementById('CountryCode');
    const statusSelect = document.getElementById('CalendarStatus');
    const weekendSelect = document.getElementById('WeekendDays');
    const inheritHint = document.getElementById('weekendInheritanceHint');
    const inheritText = document.getElementById('weekendInheritanceText');

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

    const fill = (select, items, { includeEmpty = false, emptyText = '' } = {}) => {
        if (!select) return;
        select.innerHTML = '';
        if (includeEmpty) {
            const opt = document.createElement('option');
            opt.value = '';
            opt.textContent = emptyText;
            select.appendChild(opt);
        }
        items.forEach(({ value, text }) => {
            if (value == null) return;
            const opt = document.createElement('option');
            opt.value = value;
            opt.textContent = text ?? value;
            select.appendChild(opt);
        });
    };

    const labelFor = (token) => L[token] || token;

    /** Country layer inherits nothing, so the hint only ever applies to an override row. */
    const refreshInheritanceHint = (effectiveWeekend, inherited) => {
        if (!inheritHint || !inheritText) return;
        const selected = Array.from(weekendSelect?.selectedOptions || []).map((o) => o.value);
        const shouldShow = selected.length === 0 && Array.isArray(effectiveWeekend) && effectiveWeekend.length > 0 && inherited;
        if (!shouldShow) {
            inheritHint.classList.add('d-none');
            return;
        }
        const readable = effectiveWeekend.map(labelFor).join(', ');
        inheritText.textContent = `${L.InheritedFromCountry || 'Inherited from the country calendar'}: ${readable}`;
        inheritHint.classList.remove('d-none');
    };

    const loadContract = async () => {
        const res = await fetch(`${endpoint}/contract`, { credentials: 'same-origin', headers: getAuthHeaders() });
        if (!res.ok) return null;
        const contract = unwrap(await res.json()) || {};

        // No scope selector on this surface: the platform contract slice returns ['country'] only and the form
        // posts it as a fixed hidden value.
        fill(weekendSelect, (contract.dayOfWeek || contract.DayOfWeek || []).map((d) => ({ value: d, text: labelFor(d) })));

        // Archived is reached through the archive action, never picked in a form.
        const statuses = (contract.statuses || contract.Statuses || []).filter((s) => s !== 'archived');
        fill(statusSelect, statuses.map((s) => ({ value: s, text: labelFor(`Status${s.charAt(0).toUpperCase()}${s.slice(1)}`) })));
        return contract;
    };

    const loadCountries = async () => {
        const res = await fetch(`${endpoint}/countries`, { credentials: 'same-origin', headers: getAuthHeaders() });
        if (!res.ok) return;
        const items = unwrap(await res.json()) || [];
        fill(countrySelect, items.map((c) => ({ value: c.code || c.value, text: c.name || c.code })), {
            includeEmpty: true, emptyText: '—'
        });
    };

    const applyExisting = (dto) => {
        if (!dto) return;
        const set = (name, value) => {
            const el = form.querySelector(`[name="${name}"]`);
            if (el && value != null) el.value = value;
        };
        set('CalendarCode', dto.calendarCode);
        set('CalendarName', dto.calendarName);
        set('Description', dto.description);
        set('CountryCode', dto.countryCode);
        set('CalendarYear', dto.calendarYear);
        // ScopeType is deliberately NOT written back from the DTO: the hidden input is pinned to 'country' and a
        // country row can never carry another scope, so echoing the server value would only add a way to drift.
        set('CalendarStatus', dto.calendarStatus);
        set('Source', dto.source);
        set('Notes', dto.notes);
        set('ExpectedVersion', dto.version);

        const declared = Array.isArray(dto.weekendDays) ? dto.weekendDays : [];
        Array.from(weekendSelect?.options || []).forEach((opt) => { opt.selected = declared.includes(opt.value); });

        refreshInheritanceHint(dto.effectiveWeekendDays, dto.weekendInherited);
    };

    const collectPayload = () => {
        const value = (name) => form.querySelector(`[name="${name}"]`)?.value ?? '';
        return {
            calendarCode: value('CalendarCode'),
            calendarName: value('CalendarName'),
            description: value('Description') || null,
            countryCode: value('CountryCode'),
            calendarYear: Number(value('CalendarYear')),
            // Platform surface authors the country layer only. Fixed here rather than read from a control, and
            // re-checked server-side (`platform_surface_is_country_only`).
            scopeType: 'country',
            organizationUnitId: null,
            // Empty selection posts null (= inherit), never an empty array (= no weekend). The two are different
            // answers and collapsing them here would silently give an org a weekend it never asked for.
            weekendDays: (() => {
                const selected = Array.from(weekendSelect?.selectedOptions || []).map((o) => o.value);
                return selected.length ? selected : null;
            })(),
            calendarStatus: value('CalendarStatus'),
            source: value('Source') || 'manual',
            notes: value('Notes') || null,
            expectedVersion: Number(value('ExpectedVersion') || 0)
        };
    };

    const submit = async (event) => {
        event.preventDefault();
        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        const payload = collectPayload();
        const url = isEdit ? `${endpoint}/${calendarId}` : endpoint;
        const method = isEdit ? 'PUT' : 'POST';

        try {
            const res = await fetch(url, {
                method,
                credentials: 'same-origin',
                headers: getAuthHeaders(true),
                body: JSON.stringify(payload)
            });

            if (!res.ok) {
                const body = await res.json().catch(() => null);
                window.showToast?.(apiError(body), 'error');
                return;
            }

            window.showToast?.(isEdit ? (L.RecordUpdated || L.RecordSaved) : (L.RecordCreated || L.RecordSaved), 'success');
            window.location.href = '/Platform/WorkingCalendars';
        } catch (error) {
            console.error('[WorkingCalendars Form] Save failed.', error);
            window.showToast?.(L.ErrorOccurred, 'error');
        }
    };

    /**
     * Select2 init — ORDER IS THE WHOLE POINT.
     *
     * The golden reference initialises Select2 on DOMContentLoaded because its options are rendered server-side.
     * This form is No-ViewModel: every option arrives from a fetch AFTER that event. Initialising at the reference's
     * moment would snapshot three empty controls and the options loaded a tick later would never be shown.
     *
     * So this runs last: options populated → (edit) existing values applied → THEN Select2 reads a select that is
     * already complete and already has the right option marked selected.
     *
     * The underlying <select> stays the source of truth: Select2 writes straight through to it, so collectPayload()
     * keeps reading `select.value` and the posted payload is unchanged. `change.select2` is re-broadcast as a native
     * `change` so the weekend inheritance hint still fires — Select2 suppresses the native event otherwise.
     */
    const initSelect2 = () => {
        if (typeof window.jQuery !== 'function') return;
        const $ = window.jQuery;
        if (typeof $.fn?.select2 !== 'function') return;

        $('.select2', form).each(function () {
            const $el = $(this);
            if ($el.hasClass('select2-hidden-accessible')) return;
            $el.wrap('<div class="position-relative"></div>').select2({ dropdownParent: $el.parent() });
            $el.on('change.select2', function () {
                this.dispatchEvent(new Event('change', { bubbles: true }));
            });
        });
    };

    const init = async () => {
        await Promise.all([loadContract(), loadCountries()]);

        if (isEdit) {
            const res = await fetch(`${endpoint}/${calendarId}`, { credentials: 'same-origin', headers: getAuthHeaders() });
            if (res.ok) applyExisting(unwrap(await res.json()));
        }

        weekendSelect?.addEventListener('change', () => refreshInheritanceHint([], false));
        form.addEventListener('submit', submit);

        initSelect2();
    };

    document.addEventListener('DOMContentLoaded', () => void init());
})();
