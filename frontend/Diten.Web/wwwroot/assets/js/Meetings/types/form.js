'use strict';

/*
 * S8 — Meeting Type Create/Edit logic. The AgendaTemplate repeatable list is driven entirely client-side
 * (no server model binding here, unlike Views/Tasks/TaskTypes' own _ClosureOutcomeRow.cshtml): rows are
 * cloned from the <template> in _Form.cshtml and read straight off the DOM on submit.
 */
(function () {
    const t = (key) => window.MeetingTypesL10n?.t?.(key) ?? key;
    const st = (key) => window.MeetingsL10n?.t?.(key) ?? key;

    const MAX_AGENDA_LINES = 20;
    const MAX_AGENDA_LINE_LENGTH = 200;

    // ── Agenda template repeater ─────────────────────────────────────────────────────────────────────────────

    const agendaList = () => document.querySelector('[data-agenda-template-list]');
    const agendaRows = () => Array.from(agendaList()?.querySelectorAll('[data-agenda-template-row]') || []);

    const refreshAgendaEmptyState = () => {
        const empty = document.querySelector('[data-agenda-template-empty]');
        empty?.classList.toggle('d-none', agendaRows().length > 0);
        const addBtn = document.querySelector('[data-agenda-template-add]');
        // K8 field rule — ≤ 20 lines; the Add button withdraws rather than accepting a click that would 400.
        addBtn?.classList.toggle('d-none', agendaRows().length >= MAX_AGENDA_LINES);
    };

    const bindAgendaRow = (row) => {
        row.querySelector('[data-agenda-template-remove]')?.addEventListener('click', () => {
            row.remove();
            refreshAgendaEmptyState();
        });
        row.querySelector('[data-agenda-template-up]')?.addEventListener('click', () => {
            const prev = row.previousElementSibling;
            if (prev) { row.parentElement.insertBefore(row, prev); }
        });
        row.querySelector('[data-agenda-template-down]')?.addEventListener('click', () => {
            const next = row.nextElementSibling;
            if (next) { row.parentElement.insertBefore(next, row); }
        });
    };

    const addAgendaRow = (text) => {
        if (agendaRows().length >= MAX_AGENDA_LINES) { return; }
        const template = document.querySelector('[data-agenda-template-row-template]');
        const row = template.content.firstElementChild.cloneNode(true);
        const textInput = row.querySelector('[data-agenda-template-text]');
        textInput.value = text || '';
        bindAgendaRow(row);
        agendaList().appendChild(row);
        refreshAgendaEmptyState();
    };

    const initAgendaTemplateEditor = (lines) => {
        agendaList().innerHTML = '';
        (lines || []).forEach((line) => addAgendaRow(line));
        refreshAgendaEmptyState();
        document.querySelector('[data-agenda-template-add]')?.addEventListener('click', () => addAgendaRow(''));
    };

    const readAgendaTemplate = () => agendaRows()
        .map((row) => row.querySelector('[data-agenda-template-text]').value.trim())
        .filter((line) => line.length > 0)
        .slice(0, MAX_AGENDA_LINES)
        .map((line) => line.slice(0, MAX_AGENDA_LINE_LENGTH));

    // ── DefaultActionTaskTypeId picker ───────────────────────────────────────────────────────────────────────

    const initSelect2 = (selector, options) => {
        if (!window.jQuery || !$.fn.select2) { return; }
        const $s = $(selector);
        if (!$s.length) { return; }
        if ($s.hasClass('select2-hidden-accessible')) { $s.select2('destroy'); }
        $s.select2(Object.assign({ dropdownParent: $(document.body), width: '100%' }, options || {}));
    };

    const loadActiveTaskTypes = async () => {
        try {
            const response = await fetch('/Tasks/api/task-types/active', {
                headers: { Accept: 'application/json' },
                credentials: 'same-origin'
            });
            if (!response.ok) { return []; }
            const payload = await response.json();
            return payload?.data ?? [];
        } catch (_) {
            // Fail-safe: a user who can manage meeting types but holds no platform.tasks.read grant still gets
            // a working form — the picker is simply empty (DefaultActionTaskTypeId is optional, pack §"MeetingType").
            return [];
        }
    };

    const populateTaskTypeOptions = (taskTypes) => {
        const $s = $('#fieldDefaultActionTaskTypeId');
        $s.find('option:not([value=""])').remove();
        (taskTypes || []).forEach((tt) => $s.append(new Option(tt.name, tt.id)));
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
        const form = document.getElementById('meetingTypeForm');
        const mode = form.dataset.formMode;
        const isEdit = mode === 'edit';
        const meetingTypeId = document.getElementById('meetingTypeId')?.value || '';

        const taskTypes = await loadActiveTaskTypes();
        populateTaskTypeOptions(taskTypes);
        initSelect2('#fieldDefaultActionTaskTypeId', { placeholder: st('showAll'), allowClear: true });

        if (isEdit && meetingTypeId) {
            const result = await window.MeetingsApi.typesGet(meetingTypeId);
            if (!result.ok) {
                window.DitenModal?.error?.({ title: st('errorOccurred'), message: window.MeetingsApi.failureMessage(result) });
                window.location.href = '/Meetings/MeetingTypes';
                return;
            }
            const meetingType = result.data;
            document.getElementById('fieldName').value = meetingType.name || '';
            $('#fieldDefaultActionTaskTypeId').val(meetingType.defaultActionTaskTypeId || '').trigger('change');
            document.getElementById('fieldIsQualityRecord').checked = !!meetingType.isQualityRecord;
            document.getElementById('fieldRequiresESignature').checked = !!meetingType.requiresESignature;
            document.getElementById('fieldAttendanceMandatory').checked = !!meetingType.attendanceMandatory;
            document.getElementById('meetingTypeExpectedVersion').value = String(meetingType.version);
            initAgendaTemplateEditor(meetingType.agendaTemplate);
        } else {
            initAgendaTemplateEditor([]);
        }

        form.addEventListener('submit', (e) => {
            e.preventDefault();
            void submitForm(form, isEdit, meetingTypeId);
        });
    };

    const submitForm = async (form, isEdit, meetingTypeId) => {
        clearFieldErrors();

        const name = document.getElementById('fieldName').value.trim();
        if (!name) {
            document.getElementById('formValidationSummary').textContent = st('formValidationError') || st('errorOccurred');
            document.getElementById('formValidationSummary').classList.remove('d-none');
            return;
        }

        const payload = {
            name,
            agendaTemplate: readAgendaTemplate(),
            defaultActionTaskTypeId: document.getElementById('fieldDefaultActionTaskTypeId').value || null,
            isQualityRecord: document.getElementById('fieldIsQualityRecord').checked,
            requiresESignature: document.getElementById('fieldRequiresESignature').checked,
            attendanceMandatory: document.getElementById('fieldAttendanceMandatory').checked
        };

        const result = isEdit
            ? await window.MeetingsApi.typesUpdate(meetingTypeId, Object.assign({}, payload, {
                expectedVersion: Number(document.getElementById('meetingTypeExpectedVersion').value)
            }))
            : await window.MeetingsApi.typesCreate(payload);

        if (!result.ok) {
            if (window.MeetingsApi.isConcurrencyConflict(result)) {
                // Not st('errorConcurrencyConflict') — that sentence says "meeting", and Platform has no
                // distinct MEETING_TYPE_CONCURRENCY_CONFLICT code to key a second message off (measured: the
                // handler reuses MEETING_CONCURRENCY_CONFLICT for both). This screen's own key says "type".
                window.DitenModal?.error?.({ title: st('errorOccurred'), message: t('errorConcurrencyConflict') });
                return;
            }
            if (result.reasonCode === 'MEETING_TYPE_NAME_DUPLICATE') {
                showFieldError('fieldNameError', st('errorTypeNameDuplicate'));
                return;
            }
            document.getElementById('formValidationSummary').textContent = window.MeetingsApi.failureMessage(result);
            document.getElementById('formValidationSummary').classList.remove('d-none');
            return;
        }

        window.DitenModal?.success?.({ title: isEdit ? st('recordUpdated') : st('recordCreated'), timer: 1200 });
        window.location.href = '/Meetings/MeetingTypes';
    };

    document.addEventListener('DOMContentLoaded', () => {
        if (document.getElementById('meetingTypeForm')) { void initFormPage(); }
    });
})();
