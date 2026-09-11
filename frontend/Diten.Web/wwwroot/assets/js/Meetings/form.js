'use strict';

/*
 * MOD-0357 S3 — Create/Edit/Details logic, in one file per this WP's own file contract
 * (wwwroot/assets/js/Meetings/{index.js, index.l10n.js, form.js, api.js}). Which branch runs is decided by which
 * root element the page actually has: #meetingForm (Create/Edit) or #meetingDetailsRoot (Details).
 *
 * ⚠ THE BACKEND HAS NO "SET AGENDA AT CREATE TIME" ENDPOINT. CreateMeetingRequest carries no agenda field —
 * AddAgendaItemCommand needs a meeting id that does not exist until the create call returns. So agenda items are
 * only ever added on the Details page (after create redirects there), never on the Create form itself, whatever
 * the pack's own field-count implied. Reported as a corrected scope decision in this WP's report.
 */
(function () {
    const t = (key) => window.MeetingsL10n?.t?.(key) ?? key;

    const toIsoOrNull = (datetimeLocalValue) => {
        if (!datetimeLocalValue) { return null; }
        const d = new Date(datetimeLocalValue);
        return Number.isNaN(d.getTime()) ? null : d.toISOString();
    };
    const toDatetimeLocal = (isoValue) => {
        if (!isoValue) { return ''; }
        const d = new Date(isoValue);
        if (Number.isNaN(d.getTime())) { return ''; }
        const pad = (n) => String(n).padStart(2, '0');
        return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    };

    /*
     * select2 4.0.13 wires DropdownSearch only for single selects; composed here (Utils.Decorate) the same way
     * Governance/RoleAssignments/index.js does, so the searchable dropdown also opens for the attendee MULTI
     * select — otherwise a tenant with dozens of eligible people has no way to find one by typing.
     */
    let searchableDropdownAdapter = null;
    const buildSearchableDropdownAdapter = () => {
        if (searchableDropdownAdapter) { return searchableDropdownAdapter; }
        const amd = window.jQuery?.fn?.select2?.amd;
        if (!amd?.require) { return undefined; }
        try {
            const Dropdown = amd.require('select2/dropdown');
            const DropdownSearch = amd.require('select2/dropdown/search');
            const AttachBody = amd.require('select2/dropdown/attachBody');
            const Utils = amd.require('select2/utils');
            searchableDropdownAdapter = Utils.Decorate(Utils.Decorate(Dropdown, DropdownSearch), AttachBody);
            return searchableDropdownAdapter;
        } catch (e) {
            console.warn('[Meetings] dropdown search adapter unavailable; falling back to the default.', e);
            return undefined;
        }
    };

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

    const initFormPage = async () => {
        const form = document.getElementById('meetingForm');
        const mode = form.dataset.formMode;
        const isEdit = mode === 'edit';
        const meetingId = document.getElementById('meetingId')?.value || '';

        const [typesResult, attendeesResult, listResult] = await Promise.all([
            window.MeetingsApi.lookupTypes(),
            isEdit ? Promise.resolve({ ok: true, data: { people: [] } }) : window.MeetingsApi.lookupAttendees(),
            isEdit ? Promise.resolve({ ok: true, data: { items: [] } }) : window.MeetingsApi.list('pageSize=1000')
        ]);

        const types = typesResult.ok ? (typesResult.data || []) : [];
        populateOptions('#fieldMeetingTypeId', types, 'id', 'name');
        initSelect2('#fieldMeetingTypeId', { placeholder: t('selectPlaceholder') });

        if (isEdit) {
            document.getElementById('createOnlySection')?.classList.add('d-none');
        } else {
            const people = attendeesResult.ok ? (attendeesResult.data?.people || []) : [];
            populateOptions('#fieldAttendeeUserIds', people, 'userId', 'displayName');
            document.getElementById('noEligibleAttendeesHint')?.classList.toggle('d-none', people.length > 0);
            initSelect2('#fieldAttendeeUserIds', {
                placeholder: t('attendeesPlaceholder'),
                dropdownAdapter: buildSearchableDropdownAdapter(),
                closeOnSelect: false
            });

            const meetings = listResult.ok ? (listResult.data?.items || []) : [];
            populateOptions('#fieldFollowUpOfMeetingId', meetings, 'id', 'title');
            initSelect2('#fieldFollowUpOfMeetingId', { placeholder: t('showAll'), allowClear: true });
        }

        if (isEdit && meetingId) {
            const result = await window.MeetingsApi.get(meetingId);
            if (!result.ok) {
                window.DitenModal?.error?.({ title: t('errorOccurred'), message: window.MeetingsApi.failureMessage(result) });
                window.location.href = '/Meetings';
                return;
            }
            const meeting = result.data;
            document.getElementById('fieldTitle').value = meeting.title || '';
            $('#fieldMeetingTypeId').val(meeting.meetingTypeId).trigger('change');
            document.getElementById('fieldStartAt').value = toDatetimeLocal(meeting.startAt);
            document.getElementById('fieldEndAt').value = toDatetimeLocal(meeting.endAt);
            document.getElementById('fieldLocation').value = meeting.location || '';
            document.getElementById('fieldDescription').value = meeting.description || '';
            document.getElementById('meetingExpectedVersion').value = String(meeting.version);
        }

        form.addEventListener('submit', (e) => {
            e.preventDefault();
            void submitForm(form, isEdit, meetingId);
        });
    };

    const clearFieldErrors = () => {
        document.getElementById('formValidationSummary')?.classList.add('d-none');
        ['fieldTitleError', 'fieldEndAtError'].forEach((id) => document.getElementById(id)?.classList.add('d-none'));
    };

    const showFieldError = (id, message) => {
        const el = document.getElementById(id);
        if (!el) { return; }
        el.textContent = message;
        el.classList.remove('d-none');
    };

    const submitForm = async (form, isEdit, meetingId) => {
        clearFieldErrors();

        const title = document.getElementById('fieldTitle').value.trim();
        const meetingTypeId = document.getElementById('fieldMeetingTypeId').value;
        const startAtLocal = document.getElementById('fieldStartAt').value;
        const endAtLocal = document.getElementById('fieldEndAt').value;
        const location = document.getElementById('fieldLocation').value.trim() || null;
        const description = document.getElementById('fieldDescription').value.trim() || null;

        if (!title || !meetingTypeId || !startAtLocal || !endAtLocal) {
            document.getElementById('formValidationSummary').textContent = t('formValidationError') || t('errorOccurred');
            document.getElementById('formValidationSummary').classList.remove('d-none');
            return;
        }

        // AC4 — client-side gate; the server has the final word (400 MEETING_END_BEFORE_START either way).
        if (new Date(endAtLocal) <= new Date(startAtLocal)) {
            showFieldError('fieldEndAtError', t('errorEndBeforeStart'));
            return;
        }

        const startAt = toIsoOrNull(startAtLocal);
        const endAt = toIsoOrNull(endAtLocal);

        const result = isEdit
            ? await window.MeetingsApi.update(meetingId, {
                title, meetingTypeId, startAt, endAt, location, description,
                expectedVersion: Number(document.getElementById('meetingExpectedVersion').value)
            })
            : await window.MeetingsApi.create({
                title, meetingTypeId, startAt, endAt, location, description,
                organizerUserId: null,
                followUpOfMeetingId: document.getElementById('fieldFollowUpOfMeetingId')?.value || null,
                attendeeUserIds: $('#fieldAttendeeUserIds').val() || []
            });

        if (!result.ok) {
            if (window.MeetingsApi.isConcurrencyConflict(result)) {
                window.DitenModal?.error?.({ title: t('errorOccurred'), message: t('errorConcurrencyConflict') });
                return;
            }
            if (result.reasonCode === 'MEETING_END_BEFORE_START') {
                showFieldError('fieldEndAtError', t('errorEndBeforeStart'));
                return;
            }
            document.getElementById('formValidationSummary').textContent = window.MeetingsApi.failureMessage(result);
            document.getElementById('formValidationSummary').classList.remove('d-none');
            return;
        }

        window.DitenModal?.success?.({ title: isEdit ? t('recordUpdated') : t('recordCreated'), timer: 1200 });
        const targetId = isEdit ? meetingId : result.data.id;
        window.location.href = `/Meetings/${targetId}`;
    };

    // ── Details ──────────────────────────────────────────────────────────────────────────────────────────────

    /*
     * MEASURED LIVE (smoke test): Platform serializes enums as their INTEGER ordinal, not the C# name — the
     * wire carries `"lifecycle": 0`, never `"lifecycle": "Scheduled"`. Mapping by string name here silently
     * matched nothing, which made `cancelled`/`completed` below permanently false and left every editing
     * control visible on an actually-cancelled meeting — exactly what AC5 forbids. Fixed by indexing on the
     * same ordinal Diten.Platform.Domain.Enums.Meetings declares (Scheduled=0, Cancelled=1, Completed=2 /
     * Pending=0, Accepted=1, Declined=2), not by name.
     */
    const MEETING_LIFECYCLE = { SCHEDULED: 0, CANCELLED: 1, COMPLETED: 2 };
    const INVITATION_RESPONSE = { PENDING: 0, ACCEPTED: 1, DECLINED: 2 };

    const statusLabelFor = (lifecycle) => ([
        t('statusScheduled'), t('statusCancelled'), t('statusCompleted')
    ][lifecycle] ?? lifecycle);

    const invitationLabelFor = (response) => ([
        t('invitationPending'), t('invitationAccepted'), t('invitationDeclined')
    ][response] ?? response);

    let currentMeeting = null;
    let eligiblePeopleById = {};

    const renderMeeting = (meeting) => {
        currentMeeting = meeting;
        document.getElementById('dTitle').textContent = meeting.title;
        document.getElementById('dType').textContent = meeting.meetingTypeName || '-';
        document.getElementById('dStartAt').textContent = new Date(meeting.startAt).toLocaleString(window.CurrentLanguage || undefined);
        document.getElementById('dEndAt').textContent = new Date(meeting.endAt).toLocaleString(window.CurrentLanguage || undefined);
        document.getElementById('dLocation').textContent = meeting.location || '-';
        document.getElementById('dOrganizer').textContent = eligiblePeopleById[meeting.organizerUserId] || meeting.organizerUserId;
        document.getElementById('dDescription').textContent = meeting.description || '-';
        document.getElementById('dStatus').innerHTML =
            `<span class="badge ${meeting.lifecycle === MEETING_LIFECYCLE.CANCELLED ? 'bg-label-secondary' : meeting.lifecycle === MEETING_LIFECYCLE.COMPLETED ? 'bg-label-success' : 'bg-label-info'}">${statusLabelFor(meeting.lifecycle)}</span>`;

        const cancelled = meeting.lifecycle === MEETING_LIFECYCLE.CANCELLED;
        const completed = meeting.lifecycle === MEETING_LIFECYCLE.COMPLETED;
        document.getElementById('dCancellationReasonRow')?.classList.toggle('d-none', !cancelled);
        if (cancelled) { document.getElementById('dCancellationReason').textContent = meeting.cancellationReason || '-'; }

        // AC5 — Cancelled/Completed: editing controls are withdrawn, not merely disabled (UAS-001's own posture).
        const editable = !cancelled && !completed;
        document.getElementById('btnEditMeeting')?.classList.toggle('d-none', !editable);
        if (editable) { document.getElementById('btnEditMeeting').href = `/Meetings/${meeting.id}/Edit`; }
        document.getElementById('btnCancelMeeting')?.classList.toggle('d-none', !editable);
        document.getElementById('btnReassignOrganizer')?.classList.toggle('d-none', !editable);
        document.getElementById('agendaAddRow')?.classList.toggle('d-none', !editable);
        document.getElementById('attendeeAddRow')?.classList.toggle('d-none', !editable);

        const attendeesList = document.getElementById('attendeesList');
        attendeesList.innerHTML = '';
        (meeting.attendees || []).forEach((a) => {
            const li = document.createElement('li');
            li.className = 'list-group-item d-flex align-items-center justify-content-between';
            li.innerHTML = `<span>${eligiblePeopleById[a.userId] || a.userId} <span class="badge bg-label-secondary ms-1">${invitationLabelFor(a.invitationResponse)}</span></span>`;
            if (editable) {
                const removeBtn = document.createElement('button');
                removeBtn.type = 'button';
                removeBtn.className = 'btn btn-sm btn-text-danger';
                removeBtn.innerHTML = '<i class="bx bx-x"></i>';
                removeBtn.title = t('removeAttendee');
                removeBtn.addEventListener('click', () => void removeAttendee(a.userId));
                li.appendChild(removeBtn);
            }
            attendeesList.appendChild(li);
        });

        const agendaList = document.getElementById('agendaList');
        agendaList.innerHTML = '';
        const items = (meeting.agendaItems || []).slice().sort((x, y) => x.sortOrder - y.sortOrder);
        document.getElementById('noAgendaHint')?.classList.toggle('d-none', items.length > 0);
        items.forEach((item) => {
            const li = document.createElement('li');
            li.className = 'list-group-item d-flex align-items-center justify-content-between';
            li.innerHTML = `<span>${item.text}</span>`;
            if (editable) {
                const removeBtn = document.createElement('button');
                removeBtn.type = 'button';
                removeBtn.className = 'btn btn-sm btn-text-danger';
                removeBtn.innerHTML = '<i class="bx bx-x"></i>';
                removeBtn.addEventListener('click', () => void removeAgendaItem(item.id));
                li.appendChild(removeBtn);
            }
            agendaList.appendChild(li);
        });
    };

    const removeAttendee = async (userId) => {
        const result = await window.MeetingsApi.removeAttendee(currentMeeting.id, userId);
        if (!result.ok) { window.DitenModal?.error?.({ title: t('errorOccurred'), message: window.MeetingsApi.failureMessage(result) }); return; }
        await reloadMeeting();
    };

    const removeAgendaItem = async (itemId) => {
        const result = await window.MeetingsApi.deleteAgendaItem(currentMeeting.id, itemId);
        if (!result.ok) { window.DitenModal?.error?.({ title: t('errorOccurred'), message: window.MeetingsApi.failureMessage(result) }); return; }
        await reloadMeeting();
    };

    const renderLinkedTasks = (tasks) => {
        const list = document.getElementById('linkedTasksList');
        list.innerHTML = '';
        document.getElementById('noLinkedTasksHint')?.classList.toggle('d-none', tasks.length > 0);
        tasks.forEach((task) => {
            const li = document.createElement('li');
            li.className = 'list-group-item';
            li.innerHTML = `<a href="${task.link}">${task.title}</a>`;
            list.appendChild(li);
        });
    };

    const reloadMeeting = async (meetingId) => {
        const id = meetingId || currentMeeting?.id;
        const [meetingResult, tasksResult] = await Promise.all([
            window.MeetingsApi.get(id),
            window.MeetingsApi.linkedTasks(id)
        ]);
        if (!meetingResult.ok) {
            document.getElementById('meetingNotFound')?.classList.remove('d-none');
            document.getElementById('meetingDetailsBody')?.classList.add('d-none');
            return;
        }
        document.getElementById('meetingNotFound')?.classList.add('d-none');
        document.getElementById('meetingDetailsBody')?.classList.remove('d-none');
        renderMeeting(meetingResult.data);
        renderLinkedTasks(tasksResult.ok ? (tasksResult.data || []) : []);
    };

    const initDetailsPage = async () => {
        const root = document.getElementById('meetingDetailsRoot');
        const meetingId = root.dataset.meetingId;

        const attendeesResult = await window.MeetingsApi.lookupAttendees();
        const people = attendeesResult.ok ? (attendeesResult.data?.people || []) : [];
        eligiblePeopleById = {};
        people.forEach((p) => { eligiblePeopleById[p.userId] = p.displayName || p.userId; });
        populateOptions('#newAttendeeUserId', people, 'userId', 'displayName');
        initSelect2('#newAttendeeUserId', { placeholder: t('attendeesPlaceholder'), dropdownAdapter: buildSearchableDropdownAdapter() });
        populateOptions('#reassignOrganizerUserId', people, 'userId', 'displayName');
        initSelect2('#reassignOrganizerUserId', { placeholder: t('newOrganizer'), dropdownAdapter: buildSearchableDropdownAdapter() });

        await reloadMeeting(meetingId);

        document.getElementById('btnAddAttendee')?.addEventListener('click', async () => {
            const userId = $('#newAttendeeUserId').val();
            if (!userId) { return; }
            const result = await window.MeetingsApi.addAttendees(currentMeeting.id, { userIds: [userId] });
            if (!result.ok) { window.DitenModal?.error?.({ title: t('errorOccurred'), message: window.MeetingsApi.failureMessage(result) }); return; }
            $('#newAttendeeUserId').val(null).trigger('change');
            await reloadMeeting();
        });

        document.getElementById('btnAddAgendaItem')?.addEventListener('click', async () => {
            const text = document.getElementById('newAgendaItemText').value.trim();
            if (!text) { return; }
            const result = await window.MeetingsApi.addAgendaItem(currentMeeting.id, { text });
            if (!result.ok) { window.DitenModal?.error?.({ title: t('errorOccurred'), message: window.MeetingsApi.failureMessage(result) }); return; }
            document.getElementById('newAgendaItemText').value = '';
            await reloadMeeting();
        });

        document.getElementById('btnCancelMeeting')?.addEventListener('click', () => {
            document.getElementById('cancelMeetingReason').value = '';
            document.getElementById('cancelMeetingReasonError')?.classList.add('d-none');
            bootstrap.Modal.getOrCreateInstance(document.getElementById('cancelMeetingModal')).show();
        });

        document.getElementById('btnConfirmCancelMeeting')?.addEventListener('click', async () => {
            const reason = document.getElementById('cancelMeetingReason').value.trim();
            if (!reason) {
                document.getElementById('cancelMeetingReasonError')?.classList.remove('d-none');
                return;
            }
            const result = await window.MeetingsApi.cancel(currentMeeting.id, { reason, expectedVersion: currentMeeting.version });
            bootstrap.Modal.getInstance(document.getElementById('cancelMeetingModal'))?.hide();
            if (!result.ok) {
                window.DitenModal?.error?.({
                    title: t('errorOccurred'),
                    message: window.MeetingsApi.isConcurrencyConflict(result) ? t('errorConcurrencyConflict') : window.MeetingsApi.failureMessage(result)
                });
                return;
            }
            await reloadMeeting();
        });

        document.getElementById('btnReassignOrganizer')?.addEventListener('click', () => {
            $('#reassignOrganizerUserId').val(currentMeeting.organizerUserId).trigger('change');
            bootstrap.Modal.getOrCreateInstance(document.getElementById('reassignOrganizerModal')).show();
        });

        document.getElementById('btnConfirmReassignOrganizer')?.addEventListener('click', async () => {
            const newOrganizerUserId = $('#reassignOrganizerUserId').val();
            if (!newOrganizerUserId) { return; }
            const result = await window.MeetingsApi.reassignOrganizer(currentMeeting.id, {
                newOrganizerUserId, expectedVersion: currentMeeting.version
            });
            bootstrap.Modal.getInstance(document.getElementById('reassignOrganizerModal'))?.hide();
            if (!result.ok) {
                window.DitenModal?.error?.({
                    title: t('errorOccurred'),
                    message: window.MeetingsApi.isConcurrencyConflict(result) ? t('errorConcurrencyConflict') : window.MeetingsApi.failureMessage(result)
                });
                return;
            }
            await reloadMeeting();
        });
    };

    document.addEventListener('DOMContentLoaded', () => {
        if (document.getElementById('meetingForm')) { void initFormPage(); }
        if (document.getElementById('meetingDetailsRoot')) { void initDetailsPage(); }
    });
})();
