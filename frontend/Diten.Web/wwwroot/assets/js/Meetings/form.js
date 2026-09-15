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

    /*
     * M3 — the start/end fields are flatpickr now (`enableTime: true, dateFormat: 'Y-m-d H:i'`,
     * WP-WC-SHARED-UI-01), not a native `datetime-local` input. flatpickr's own value is space-separated
     * ("2026-09-15 14:30"); the `T` is restored before handing it to `Date` because only the `T`-separated form
     * is guaranteed by the spec to parse as local time everywhere `Date` runs.
     */
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

    /*
     * select2 4.0.13 wires DropdownSearch only for single selects; the attendee MULTI select needs it too —
     * otherwise a tenant with dozens of eligible people has no way to find one by typing.
     *
     * Delegates to shared/diten-person-picker.js (WP-WC-SHARED-UI-01, E2) — this file carried its own copy,
     * identical to Governance/RoleAssignments/index.js's, and both now call the one place it is built.
     */
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
            // AssignablePersonDto rows, straight from the same lookup Tasks' own assignee picker uses — the
            // shared picker renders the avatar+name(+unit) row and its own "nobody eligible" state (E2).
            window.DitenPersonPicker.renderPersonOptions(
                document.getElementById('fieldAttendeeUserIds'),
                people,
                { placeholder: t('attendeesPlaceholder'), empty: t('noEligibleAttendees') },
                { multiple: true });
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
            document.getElementById('fieldStartAt').value = toFlatpickrValue(meeting.startAt);
            document.getElementById('fieldEndAt').value = toFlatpickrValue(meeting.endAt);
            document.getElementById('fieldLocation').value = meeting.location || '';
            document.getElementById('fieldDescription').value = meeting.description || '';
            document.getElementById('meetingExpectedVersion').value = String(meeting.version);
        }

        // M3 — bound AFTER any edit-mode pre-fill above, so flatpickr's constructor reads the field's real
        // value (if any) and starts the calendar on the right date instead of blank.
        window.DitenDateField?.enhance(form, { enableTime: true, dateFormat: 'Y-m-d H:i' });

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

        const startAt = toIsoOrNull(startAtLocal);
        const endAt = toIsoOrNull(endAtLocal);

        // AC4 — client-side gate; the server has the final word (400 MEETING_END_BEFORE_START either way).
        // Compared as the ISO strings just derived above, not the raw field values: flatpickr's own value is
        // space-separated ("2026-09-15 14:30"), a form `Date` is not guaranteed to parse the same way everywhere.
        if (new Date(endAt) <= new Date(startAt)) {
            showFieldError('fieldEndAtError', t('errorEndBeforeStart'));
            return;
        }

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
        // K8 box 1 — the backend has no create-time agenda field (see this file's own top comment), so the
        // type's AgendaTemplate is applied on the Details page's first load instead, right after creation. The
        // flag is one-shot and stripped from the URL immediately after use (see initDetailsPage below).
        window.location.href = isEdit ? `/Meetings/${targetId}` : `/Meetings/${targetId}?prefillAgenda=1`;
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
        // BL-390 — an organizer id the eligible-people lookup does not resolve (deleted/test identity) must
        // never render as the raw GUID on screen.
        document.getElementById('dOrganizer').textContent = eligiblePeopleById[meeting.organizerUserId] || t('unknownUser');
        document.getElementById('dDescription').textContent = meeting.description || '-';
        document.getElementById('dStatus').innerHTML =
            `<span class="badge ${meeting.lifecycle === MEETING_LIFECYCLE.CANCELLED ? 'bg-label-secondary' : meeting.lifecycle === MEETING_LIFECYCLE.COMPLETED ? 'bg-label-success' : 'bg-label-info'}">${statusLabelFor(meeting.lifecycle)}</span>`;

        const cancelled = meeting.lifecycle === MEETING_LIFECYCLE.CANCELLED;
        const completed = meeting.lifecycle === MEETING_LIFECYCLE.COMPLETED;
        document.getElementById('dCancellationReasonRow')?.classList.toggle('d-none', !cancelled);
        if (cancelled) { document.getElementById('dCancellationReason').textContent = meeting.cancellationReason || '-'; }

        // MOD-0357 S7 — cross-links. Shown regardless of lifecycle: a Cancelled/Completed meeting's own history
        // (what it followed, what followed it) does not stop being true once it is no longer editable.
        const followUpOfRow = document.getElementById('dFollowUpOfRow');
        followUpOfRow?.classList.toggle('d-none', !meeting.followUpOfMeetingId);
        if (meeting.followUpOfMeetingId) {
            const link = document.getElementById('dFollowUpOfLink');
            link.textContent = meeting.followUpOfMeetingTitle || meeting.followUpOfMeetingId;
            link.href = `/Meetings/${meeting.followUpOfMeetingId}`;
        }
        const followedByRow = document.getElementById('dFollowedByRow');
        followedByRow?.classList.toggle('d-none', !meeting.followedByMeetingId);
        if (meeting.followedByMeetingId) {
            const link = document.getElementById('dFollowedByLink');
            link.textContent = meeting.followedByMeetingTitle || meeting.followedByMeetingId;
            link.href = `/Meetings/${meeting.followedByMeetingId}`;
        }

        // MOD-0357 S7 — openable on ANY lifecycle (not gated by `editable`): continuing does not undo a
        // cancellation, and Completed is the common case a follow-up gets scheduled from.
        document.getElementById('btnScheduleFollowUp')?.classList.remove('d-none');

        // AC5 — Cancelled/Completed: editing controls are withdrawn, not merely disabled (UAS-001's own posture).
        const editable = !cancelled && !completed;
        // MOD-0357 S6 — minutes make sense for a Scheduled meeting (drafting ahead of/during it) and a
        // Completed one (reviewing what published) alike; only Cancelled withdraws the door, same posture as
        // every other Cancelled/Completed control above.
        document.getElementById('btnOpenMinutes')?.classList.toggle('d-none', cancelled);
        document.getElementById('btnEditMeeting')?.classList.toggle('d-none', !editable);
        if (editable) { document.getElementById('btnEditMeeting').href = `/Meetings/${meeting.id}/Edit`; }
        document.getElementById('btnCancelMeeting')?.classList.toggle('d-none', !editable);
        document.getElementById('btnReassignOrganizer')?.classList.toggle('d-none', !editable);
        document.getElementById('agendaAddRow')?.classList.toggle('d-none', !editable);
        document.getElementById('attendeeAddRow')?.classList.toggle('d-none', !editable);
        document.getElementById('taskAddRow')?.classList.toggle('d-none', !editable);

        const attendeesList = document.getElementById('attendeesList');
        attendeesList.innerHTML = '';
        (meeting.attendees || []).forEach((a) => {
            const li = document.createElement('li');
            li.className = 'list-group-item d-flex align-items-center justify-content-between';
            // BL-406 — a second badge, shown ONLY when this attendee's meeting mail permanently failed
            // (`mailUndelivered` comes straight off the API's MeetingAttendeeDto; never inferred client-side).
            const undeliveredBadge = a.mailUndelivered
                ? `<span class="badge bg-label-danger ms-1">${esc(t('mailUndeliveredBadge'))}</span>`
                : '';
            li.innerHTML = `<span>${eligiblePeopleById[a.userId] || t('unknownUser')} <span class="badge bg-label-secondary ms-1">${invitationLabelFor(a.invitationResponse)}</span>${undeliveredBadge}</span>`;
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
            // MOD-0357 S7 — a carried-forward line is marked by CarriedFromMeetingId, never inferred from
            // RecordLinkId alone (a manually typed line later linked via "link existing task" also gets one —
            // see AgendaItem.CarriedFromMeetingId's own doc comment). The task itself is already reachable
            // through the Linked Tasks section below; this badge is the "why is this line already here" answer.
            const carriedBadge = item.carriedFromMeetingId
                ? `<span class="badge bg-label-info ms-2">${esc(t('carriedFromPreviousMeetingBadge'))}</span>`
                : '';
            li.innerHTML = `<span>${esc(item.text)}${carriedBadge}</span>`;
            if (editable) {
                const rowActions = document.createElement('div');
                rowActions.className = 'd-flex gap-1';
                // MOD-0357 S4 — a line that already carries a RecordLink (the far end of a prepared task, or a
                // carried-over action) offers nothing more here; the ONE task it names is reached through
                // Linked Tasks below, never a second one from the same line.
                if (!item.recordLinkId) {
                    const createTaskBtn = document.createElement('button');
                    createTaskBtn.type = 'button';
                    createTaskBtn.className = 'btn btn-sm btn-text-primary';
                    createTaskBtn.innerHTML = '<i class="bx bx-plus"></i>';
                    createTaskBtn.title = t('createTaskFromAgendaItem');
                    createTaskBtn.addEventListener('click', () => openCreateTaskDialog(item.id));
                    rowActions.appendChild(createTaskBtn);
                }
                const removeBtn = document.createElement('button');
                removeBtn.type = 'button';
                removeBtn.className = 'btn btn-sm btn-text-danger';
                removeBtn.innerHTML = '<i class="bx bx-x"></i>';
                removeBtn.addEventListener('click', () => void removeAgendaItem(item.id));
                rowActions.appendChild(removeBtn);
                li.appendChild(rowActions);
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

    // Minimal HTML escaping — the same reason this exists in WorkCenterNext/app.js's own `esc`: a task title is
    // text someone typed, and string-building is how typed text becomes markup.
    const esc = (value) => String(value ?? '')
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

    /*
     * The row markup delegates to shared/diten-related-records.js (WP-WC-SHARED-UI-01, E3) — the same row
     * WorkCenterNext's own "related records" card uses. A linked task carries no `type` (every row here IS a
     * task, so a badge would repeat the same word on every line): `resolveTypeLabel` is omitted, and the row
     * prints without one.
     */
    const renderLinkedTasks = (tasks) => {
        const list = document.getElementById('linkedTasksList');
        document.getElementById('noLinkedTasksHint')?.classList.toggle('d-none', tasks.length > 0);
        list.innerHTML = window.DitenRelatedRecords.renderRelatedRows(
            tasks.map((task) => ({ id: '', title: task.title, link: task.link })),
            { esc, showId: false });
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

    /*
     * K8 box 1 — the meeting was JUST created (Create's own redirect carries `?prefillAgenda=1`, a one-shot
     * flag). If the agenda is still empty and the chosen type declares agenda lines, add each line as a REAL
     * agenda item through the same endpoint a manually-typed line would use — so it is independently editable
     * afterward, exactly like K8 requires, and no second "template" concept exists on the wire.
     */
    const prefillAgendaFromTypeIfRequested = async (meeting) => {
        const url = new URL(window.location.href);
        if (url.searchParams.get('prefillAgenda') !== '1') { return false; }
        url.searchParams.delete('prefillAgenda');
        window.history.replaceState({}, '', url.toString());

        if ((meeting.agendaItems || []).length > 0) { return false; }

        const typesResult = await window.MeetingsApi.lookupTypes();
        const types = typesResult.ok ? (typesResult.data || []) : [];
        const type = types.find((candidate) => candidate.id === meeting.meetingTypeId);
        const lines = (type?.agendaTemplate || []).filter((line) => String(line || '').trim().length > 0);
        if (lines.length === 0) { return false; }

        for (const line of lines) {
            // Sequential, not Promise.all — SortOrder is assigned server-side in arrival order, and the
            // template's own order must survive (pack §"AgendaItem" SortOrder rule).
            // eslint-disable-next-line no-await-in-loop
            await window.MeetingsApi.addAgendaItem(meeting.id, { text: line });
        }
        return true;
    };

    const initDetailsPage = async () => {
        const root = document.getElementById('meetingDetailsRoot');
        const meetingId = root.dataset.meetingId;

        const attendeesResult = await window.MeetingsApi.lookupAttendees();
        const people = attendeesResult.ok ? (attendeesResult.data?.people || []) : [];
        eligiblePeopleById = {};
        // BL-390 — the eligible-people lookup only carries ACTIVE tenant users; an organizer/attendee whose
        // account was deactivated or removed since the meeting was created falls out of it. A `displayName`
        // this sparse (missing) never gets backfilled with the raw id here — see dOrganizer/attendeesList above.
        people.forEach((p) => { eligiblePeopleById[p.userId] = p.displayName || t('unknownUser'); });
        // Same shared picker as the Create form's attendee select (E2) — avatar+name(+unit) rows, and its own
        // disabled/explained state when nobody is eligible, in place of the plain list this select used to get.
        // The reassign-organizer picker no longer lives on the page at all — M2 opens it through the shared
        // confirm's own select, built from `eligiblePeopleById` at click time.
        window.DitenPersonPicker.renderPersonOptions(
            document.getElementById('newAttendeeUserId'), people,
            { placeholder: t('attendeesPlaceholder'), empty: t('noEligibleAttendees') });
        initSelect2('#newAttendeeUserId', { placeholder: t('attendeesPlaceholder'), dropdownAdapter: buildSearchableDropdownAdapter() });

        await reloadMeeting(meetingId);
        if (currentMeeting && await prefillAgendaFromTypeIfRequested(currentMeeting)) {
            await reloadMeeting(meetingId);
        }

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

        document.getElementById('btnCreateTaskFromMeeting')?.addEventListener('click', () => openCreateTaskDialog(null));
        document.getElementById('btnLinkExistingTask')?.addEventListener('click', () => openLinkExistingTaskDialog());
        document.getElementById('btnScheduleFollowUp')?.addEventListener('click', () => {
            window.MeetingsFollowUpDialog.open({ meeting: currentMeeting, t });
        });

        /*
         * M1 — the shared confirm's TEXTAREA, in place of the hand-rolled `#cancelMeetingModal` (WP-WC-SHARED-UI-01).
         * Modeled on WorkCenterNext/app.js's `editComment` (a seeded/validated textarea through the same
         * component), not `withdrawComment` (which takes no input at all).
         */
        document.getElementById('btnCancelMeeting')?.addEventListener('click', () => {
            window.showConfirm(t('cancelMeeting'), async (rawReason) => {
                const reason = String(rawReason || '').trim();
                const result = await window.MeetingsApi.cancel(currentMeeting.id, { reason, expectedVersion: currentMeeting.version });
                if (!result.ok) {
                    window.DitenModal?.error?.({
                        title: t('errorOccurred'),
                        message: window.MeetingsApi.isConcurrencyConflict(result) ? t('errorConcurrencyConflict') : window.MeetingsApi.failureMessage(result)
                    });
                    return;
                }
                await reloadMeeting();
            }, {
                type: 'danger',
                // No generic "are you sure?" sentence: the field label already says what is being asked.
                subtext: '',
                confirmButtonText: t('cancelMeeting'),
                showInput: true,
                inputType: 'textarea',
                inputLabel: t('cancellationReason'),
                inputValidator: (value) => (String(value || '').trim() ? null : t('errorCancellationReasonRequired'))
            });
        });

        /*
         * M2 — the shared confirm's SELECT, in place of the hand-rolled `#reassignOrganizerModal`
         * (WP-WC-SHARED-UI-01). Modeled on WorkCenterNext/app.js's `openCreateInSource` (a select seeded through
         * `didOpen`, upgraded to searchable select2 via the shared dialog adapter).
         */
        document.getElementById('btnReassignOrganizer')?.addEventListener('click', () => {
            window.showConfirm(t('reassignOrganizer'), async (newOrganizerUserId) => {
                if (!newOrganizerUserId) { return; }
                const result = await window.MeetingsApi.reassignOrganizer(currentMeeting.id, {
                    newOrganizerUserId, expectedVersion: currentMeeting.version
                });
                if (!result.ok) {
                    window.DitenModal?.error?.({
                        title: t('errorOccurred'),
                        message: window.MeetingsApi.isConcurrencyConflict(result) ? t('errorConcurrencyConflict') : window.MeetingsApi.failureMessage(result)
                    });
                    return;
                }
                await reloadMeeting();
            }, {
                subtext: '',
                confirmButtonText: t('reassignOrganizer'),
                showInput: true,
                inputType: 'select',
                inputLabel: t('newOrganizer'),
                inputOptions: eligiblePeopleById,
                didOpen: (popup) => {
                    const box = (window.Swal && typeof window.Swal.getInput === 'function' && window.Swal.getInput())
                        || popup.querySelector('.swal2-select');
                    if (!box) { return; }
                    box.value = currentMeeting.organizerUserId || '';
                    window.DitenDialog?.bindDialogSelect2?.(box, popup);
                }
            });
        });
    };

    // ── S4 — the meeting↔task bridge ─────────────────────────────────────────────────────────────────────────

    /*
     * MOD-0357 S6 — the dialog body itself moved to shared/../Meetings/task-from-meeting-dialog.js, so the
     * Minutes editor's own "Görev oluştur" (a decision, not an agenda item) opens the SAME dialog rather than a
     * second hand-rolled copy. This stays a one-line delegation, the same shape `dialogIcon`/`dialogLook`
     * already took when THEY moved to shared/diten-dialog.js.
     */
    const openCreateTaskDialog = (agendaItemId) => {
        window.MeetingsTaskFromMeetingDialog.open({
            meetingId: currentMeeting.id,
            agendaItemId,
            t,
            onCreated: () => reloadMeeting()
        });
    };

    /*
     * "Link existing task" is ONE field — a search-select — so it goes through `window.showConfirm` exactly
     * like the reassign-organizer picker above (M2), never the raw-Swal path `openCreateTaskDialog` takes.
     */
    const openLinkExistingTaskDialog = () => {
        void (async () => {
            const candidatesResult = await window.TasksApi?.linkCandidates?.(null, 20);
            const candidates = candidatesResult?.ok ? candidatesResult.data : [];
            // A leading BLANK entry, deliberately: a native `<select>` otherwise opens on its first real option
            // already selected, and confirming without touching it would link a task nobody chose.
            const taskOptions = { '': t('linkExistingTaskPlaceholder') };
            candidates.forEach((candidate) => { taskOptions[candidate.id] = candidate.title; });

            window.showConfirm(t('linkExistingTask'), async (taskId) => {
                if (!taskId) { return; }
                const result = await window.MeetingsApi.linkExistingTask(currentMeeting.id, taskId, null);
                if (!result.ok) {
                    // MEETING_TASK_ALREADY_LINKED (409) reads through the SAME reason-code bridge as every
                    // other failure here — no special-cased sentence, the bridge already carries one for it.
                    window.DitenModal?.error?.({ title: t('errorOccurred'), message: window.MeetingsApi.failureMessage(result) });
                    return;
                }
                window.DitenModal?.success?.({ title: t('toastTaskLinked'), timer: 1200 });
                await reloadMeeting();
            }, {
                subtext: '',
                confirmButtonText: t('linkExistingTask'),
                showInput: true,
                inputType: 'select',
                inputLabel: t('linkExistingTaskLabel'),
                inputOptions: taskOptions,
                inputValidator: (value) => (value ? null : t('linkExistingTaskRequired')),
                didOpen: (popup) => {
                    const box = (window.Swal && typeof window.Swal.getInput === 'function' && window.Swal.getInput())
                        || popup.querySelector('.swal2-select');
                    if (box) { window.DitenDialog?.bindDialogSelect2?.(box, popup, { allowClear: false }); }
                }
            });
        })();
    };

    document.addEventListener('DOMContentLoaded', () => {
        if (document.getElementById('meetingForm')) { void initFormPage(); }
        if (document.getElementById('meetingDetailsRoot')) { void initDetailsPage(); }
    });
})();
