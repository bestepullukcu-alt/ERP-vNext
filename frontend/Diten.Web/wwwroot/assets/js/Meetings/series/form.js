'use strict';

/*
 * S11 — Meeting Series Create/Edit logic, mirroring Meetings/types/form.js's shape for its own picker/submit
 * wiring, and Meetings/form.js's flatpickr datetime handling (M3) for StartsAt/EndsAt.
 */
(function () {
    const t = (key) => window.MeetingSeriesL10n?.t?.(key) ?? key;
    const st = (key) => window.MeetingsL10n?.t?.(key) ?? key;

    // ── datetime helpers (identical to Meetings/form.js's own toIsoOrNull/toFlatpickrValue) ──────────────────

    const toIsoOrNull = (flatpickrValue) => {
        if (!flatpickrValue) { return null; }
        const d = new Date(flatpickrValue.replace(' ', 'T'));
        return Number.isNaN(d.getTime()) ? null : d.toISOString();
    };
    const toFlatpickrValue = (isoValue) => {
        if (!isoValue) { return ''; }
        const d = new Date(isoValue);
        if (Number.isNaN(d.getTime())) { return ''; }
        const pad = (n) => String(n).padStart(2, '0');
        return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
    };

    const buildSearchableDropdownAdapter = () => window.DitenPersonPicker.buildSearchableDropdownAdapter();

    const initSelect2 = (selector, options) => {
        if (!window.jQuery || !$.fn.select2) { return; }
        const $s = $(selector);
        if (!$s.length) { return; }
        if ($s.hasClass('select2-hidden-accessible')) { $s.select2('destroy'); }
        $s.select2(Object.assign({ dropdownParent: $(document.body), width: '100%' }, options || {}));
    };

    const populateOptions = (selector, items, valueKey, textKey) => {
        const $s = $(selector);
        $s.find('option:not([value=""])').remove();
        items.forEach((item) => $s.append(new Option(item[textKey], item[valueKey])));
    };

    // ── Create / Edit ────────────────────────────────────────────────────────────────────────────────────────

    const clearFieldErrors = () => {
        document.getElementById('formValidationSummary')?.classList.add('d-none');
        document.getElementById('fieldNameError')?.classList.add('d-none');
    };

    const showFieldError = (id, message) => {
        const el = document.getElementById(id);
        if (!el) { return; }
        el.textContent = message;
        el.classList.remove('d-none');
    };

    const initFormPage = async () => {
        const form = document.getElementById('meetingSeriesForm');
        const mode = form.dataset.formMode;
        const isEdit = mode === 'edit';
        const seriesId = document.getElementById('meetingSeriesId')?.value || '';

        const [typesResult, attendeesResult] = await Promise.all([
            window.MeetingsApi.lookupTypes(),
            window.MeetingsApi.lookupAttendees()
        ]);

        const types = typesResult.ok ? (typesResult.data || []) : [];
        populateOptions('#fieldMeetingTypeId', types, 'id', 'name');
        initSelect2('#fieldMeetingTypeId', { placeholder: st('selectPlaceholder') });

        const people = attendeesResult.ok ? (attendeesResult.data?.people || []) : [];
        // AssignablePersonDto rows, straight from the same lookup Meetings' own attendee picker uses — the
        // shared picker renders the avatar+name(+unit) row and its own "nobody eligible" state (E2).
        window.DitenPersonPicker.renderPersonOptions(
            document.getElementById('fieldOrganizerUserId'), people,
            { placeholder: st('attendeesPlaceholder'), empty: st('noEligibleAttendees') });
        initSelect2('#fieldOrganizerUserId', {
            placeholder: st('attendeesPlaceholder'), dropdownAdapter: buildSearchableDropdownAdapter()
        });

        window.DitenPersonPicker.renderPersonOptions(
            document.getElementById('fieldAttendeeUserIds'), people,
            { placeholder: st('attendeesPlaceholder'), empty: st('noEligibleAttendees') }, { multiple: true });
        initSelect2('#fieldAttendeeUserIds', {
            placeholder: st('attendeesPlaceholder'), dropdownAdapter: buildSearchableDropdownAdapter(), closeOnSelect: false
        });

        if (isEdit && seriesId) {
            const result = await window.MeetingsApi.seriesGet(seriesId);
            if (!result.ok) {
                window.DitenModal?.error?.({ title: st('errorOccurred'), message: window.MeetingsApi.failureMessage(result) });
                window.location.href = '/Meetings/Series';
                return;
            }
            const series = result.data;
            document.getElementById('fieldName').value = series.name || '';
            $('#fieldMeetingTypeId').val(series.meetingTypeId).trigger('change');
            document.getElementById('fieldLocation').value = series.location || '';
            document.getElementById('fieldFrequency').value = String(series.frequency);
            document.getElementById('fieldInterval').value = String(series.interval);
            document.getElementById('fieldDurationMinutes').value = String(series.durationMinutes);
            document.getElementById('fieldStartsAt').value = toFlatpickrValue(series.startsAt);
            document.getElementById('fieldEndsAt').value = toFlatpickrValue(series.endsAt);
            document.getElementById('fieldLeadTimeDays').value = String(series.leadTimeDays);
            $('#fieldOrganizerUserId').val(series.organizerUserId).trigger('change');
            $('#fieldAttendeeUserIds').val(series.attendeeUserIds || []).trigger('change');
            document.getElementById('fieldChainAsFollowUp').checked = !!series.chainAsFollowUp;
            document.getElementById('fieldIsActive').checked = !!series.isActive;
            document.getElementById('fieldLastGeneratedAt').textContent = series.lastGeneratedAt
                ? new Date(series.lastGeneratedAt).toLocaleString(window.CurrentLanguage || undefined)
                : t('lastGeneratedAtNever');
            document.getElementById('meetingSeriesExpectedVersion').value = String(series.version);
        }

        // M3 — bound AFTER any edit-mode pre-fill above, so flatpickr's constructor reads the field's real
        // value (if any) and starts the calendar on the right date instead of blank.
        window.DitenDateField?.enhance(form, { enableTime: true, dateFormat: 'Y-m-d H:i' });

        form.addEventListener('submit', (e) => {
            e.preventDefault();
            void submitForm(form, isEdit, seriesId);
        });
    };

    const submitForm = async (form, isEdit, seriesId) => {
        clearFieldErrors();

        const name = document.getElementById('fieldName').value.trim();
        const meetingTypeId = document.getElementById('fieldMeetingTypeId').value;
        const startsAtLocal = document.getElementById('fieldStartsAt').value;
        const organizerUserId = document.getElementById('fieldOrganizerUserId').value;

        if (!name || !meetingTypeId || !startsAtLocal || !organizerUserId) {
            document.getElementById('formValidationSummary').textContent = st('formValidationError') || st('errorOccurred');
            document.getElementById('formValidationSummary').classList.remove('d-none');
            return;
        }

        const payload = {
            name,
            meetingTypeId,
            frequency: Number(document.getElementById('fieldFrequency').value),
            interval: Number(document.getElementById('fieldInterval').value) || 1,
            startsAt: toIsoOrNull(startsAtLocal),
            endsAt: toIsoOrNull(document.getElementById('fieldEndsAt').value),
            durationMinutes: Number(document.getElementById('fieldDurationMinutes').value) || 60,
            location: document.getElementById('fieldLocation').value.trim() || null,
            organizerUserId,
            attendeeUserIds: $('#fieldAttendeeUserIds').val() || [],
            leadTimeDays: Number(document.getElementById('fieldLeadTimeDays').value) || 14,
            chainAsFollowUp: document.getElementById('fieldChainAsFollowUp').checked,
            isActive: document.getElementById('fieldIsActive').checked
        };

        const result = isEdit
            ? await window.MeetingsApi.seriesUpdate(seriesId, Object.assign({}, payload, {
                expectedVersion: Number(document.getElementById('meetingSeriesExpectedVersion').value)
            }))
            : await window.MeetingsApi.seriesCreate(payload);

        if (!result.ok) {
            if (window.MeetingsApi.isConcurrencyConflict(result)) {
                window.DitenModal?.error?.({ title: st('errorOccurred'), message: st('errorConcurrencyConflict') });
                return;
            }
            if (result.reasonCode === 'MEETING_SERIES_NAME_DUPLICATE') {
                showFieldError('fieldNameError', st('errorSeriesNameDuplicate'));
                return;
            }
            document.getElementById('formValidationSummary').textContent = window.MeetingsApi.failureMessage(result);
            document.getElementById('formValidationSummary').classList.remove('d-none');
            return;
        }

        window.DitenModal?.success?.({ title: isEdit ? st('recordUpdated') : st('recordCreated'), timer: 1200 });
        window.location.href = '/Meetings/Series';
    };

    document.addEventListener('DOMContentLoaded', () => {
        if (document.getElementById('meetingSeriesForm')) { void initFormPage(); }
    });
})();
