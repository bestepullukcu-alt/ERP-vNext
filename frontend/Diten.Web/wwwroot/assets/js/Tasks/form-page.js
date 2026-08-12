'use strict';

/*
 * MOD-0024 — the detailed (compact) create/edit page. Picks up any draft handed over by the quick-create
 * offcanvas so switching depth loses nothing (DEV-1), and never sends lifecycle or spentHours.
 */
(function (global) {
    const t = (key) => global.TasksL10n?.t?.(key) ?? key;

    // Where a completed create/edit returns to: the Task Center owns the personal work list.
    const WORK_CENTER_URL = '/WorkCenterNext';

    // MOD-0024's own module code, as declared by its manifest (`TaskManifestProvider.ModuleCode`). A field
    // definition may be restricted to one consuming module; this form only offers the ones that reach it.
    const TASK_MODULE_CODE = 'tasks';

    const el = (id) => document.getElementById(id);
    const setValue = (id, value) => { const node = el(id); if (node && value != null) { node.value = value; } };
    const setChecked = (id, value) => { const node = el(id); if (node) { node.checked = !!value; } };

    const readForm = () => ({
        title: el('taskTitle')?.value,
        description: el('taskDescription')?.value,
        priority: el('taskPriority')?.value,
        assignmentTarget: el('taskAssignmentTarget')?.value,
        assigneeUserId: el('taskAssignee')?.value,
        poolPositionId: el('taskPoolPosition')?.value,
        // No organizationUnitId and no plannedDate: the form asks for neither. The unit is the server's cascade
        // (a typed value used to win it and misfile the task), and the planned date is the Plan transition's.
        dueAt: el('taskDueAt')?.value,
        startAt: el('taskStartAt')?.value,
        estimateHours: el('taskEstimateHours')?.value,
        tags: el('taskTags')?.value,
        reviewRequired: el('taskReviewRequired')?.checked,
        reviewerCandidateUserId: el('taskReviewer')?.value,
        approvalRequired: el('taskApprovalRequired')?.checked,
        approvalManagerUserId: el('taskApprovalManager')?.value,
        // The watcher picker is a multi-select, so its answer is a LIST of identities. buildCreatePayload turns
        // them into the TaskWatcherRequest rows the server declares.
        watchers: Array.from(el('taskWatchers')?.selectedOptions || [])
            .map((option) => option.value)
            .filter(Boolean),
        emailNotificationsEnabled: el('taskEmailNotifications')?.checked,
        // BL-065 — the ticked events and the lead time. buildCreatePayload drops both when the channel is off.
        notifyOnEvents: Array.from(document.querySelectorAll('[name="notifyOnEvents"]'))
            .filter((box) => box.checked)
            .map((box) => box.value),
        reminderLeadDays: el('taskReminderLeadDays')?.value,
        delegationAllowed: el('taskDelegationAllowed')?.checked
    });

    const writeForm = (draft) => {
        if (!draft) { return; }
        setValue('taskTitle', draft.title);
        setValue('taskDescription', draft.description);
        setValue('taskPriority', draft.priority);
        setValue('taskAssignmentTarget', draft.assignmentTarget);
        setValue('taskAssignee', draft.assigneeUserId);
        setValue('taskPoolPosition', draft.poolPositionId);
        setValue('taskDueAt', (draft.dueAt || '').slice(0, 10));
        setValue('taskStartAt', (draft.startAt || '').slice(0, 10));
        setValue('taskEstimateHours', draft.estimateHours);
        setValue('taskTags', Array.isArray(draft.tags) ? draft.tags.join(', ') : draft.tags);
        setChecked('taskReviewRequired', draft.reviewRequired);
        setValue('taskReviewer', draft.reviewerCandidateUserId);
        setChecked('taskApprovalRequired', draft.approvalRequired);
        setValue('taskApprovalManager', draft.approvalManagerUserId);
        // Watchers are rows on the wire and identities in the control; the picker is already filled with the
        // people lookup by now, so the stored identities come back as NAMES.
        global.TaskForm.selectWatchers(el('taskWatchers'), draft.watchers);
        if (draft.emailNotificationsEnabled !== undefined) {
            setChecked('taskEmailNotifications', draft.emailNotificationsEnabled);
        }
        // BL-065 — null means the owner never chose, and the server emails about everything for such a task; the
        // card has to show that rather than an empty list.
        global.TaskForm.applyNotificationPreferences(
            document, draft.notifyOnEvents ?? null, draft.reminderLeadDays ?? null);
        setChecked('taskDelegationAllowed', draft.delegationAllowed);
    };

    /* ── Configurable fields (Phase 5) ────────────────────────────────────────────────────────────────────
     *
     * The form has carried `#taskCustomFields`/`#taskCustomFieldsRow` since Phase 1 with NO code touching
     * either id: a tenant could define a field and never see it here. This is the code that fills them.
     *
     * Definitions live for the page's lifetime, because both the save and the edit hydration have to read the
     * SAME list the controls were rendered from — re-fetching could render one shape and post another.
     */
    let customFieldDefinitions = [];

    /*
     * What the TASK holds for fields this form no longer displays.
     *
     * The update handler assigns plannedDate/startAt/estimateHours unconditionally, so an edit that simply omits
     * them wipes them. Captured at hydration and handed to buildUpdatePayload, which puts back only the ones
     * that have no control on screen — so removing a control cannot delete the data behind it.
     */
    let withheldOnEdit = {};

    const customFieldsRow = () => el('taskCustomFieldsRow');

    // Which definitions this surface offers: live, and either module-agnostic or claimed by MOD-0024 itself.
    const applicableDefinitions = (rows) => (rows || []).filter((definition) =>
        definition
        && definition.isActive !== false
        && (!definition.appliesToModuleCode || definition.appliesToModuleCode === TASK_MODULE_CODE));

    /*
     * Resolve every option-driven field's list BEFORE rendering, so a field is either offered complete or not
     * offered at all. Person fields reuse the assignable-people list the assignee picker already loaded — the
     * same people, labelled the same way, rather than a second vocabulary for the same concept.
     */
    const loadCustomFieldOptions = async (definitions, people) => {
        const byCode = {};

        const personLabels = {
            placeholder: t('assigneeSelectPlaceholder'),
            empty: t('assigneeEmpty'),
            nameUnavailable: t('personNameUnavailable')
        };
        const personOptions = (people || []).map((row) => ({
            value: row.userId,
            label: global.TaskForm.formatPersonLabel(row, personLabels.nameUnavailable)
        }));

        await Promise.all(definitions.map(async (definition) => {
            const kind = global.TaskForm.customFieldControlKind(definition);
            if (kind === 'person') {
                byCode[definition.code] = personOptions;
                return;
            }
            if (kind !== 'select' && kind !== 'record') { return; }

            /*
             * A record source is resolved HERE, beside the other two kinds, and that placement is the point.
             * Its first page is fetched the same way, judged by the same rule, and dropped by the same code —
             * so an unresolvable module source hides its field for exactly the reason a mistyped lookup key
             * does, with no second rule to keep in step.
             */
            const result = kind === 'record'
                ? await global.TasksApi.fieldRecords(definition.code)
                : await global.TasksApi.fieldOptions(definition.code);

            if (result.ok && Array.isArray(result.data) && result.data.length > 0) {
                byCode[definition.code] = result.data;
                return;
            }
            // Not silent: renderCustomFields will drop the field, and this says which source failed and how.
            global.console?.warn?.(
                `[Tasks] options for field "${definition.code}" could not be resolved `
                + `(${definition.optionsSourceKind}/${definition.optionsSourceKey || '—'}): `
                + `status ${result.status}${result.reasonCode ? `, ${result.reasonCode}` : ''}.`);
        }));

        return byCode;
    };

    /*
     * The server search a record picker runs, handed to select2's `ajax` rather than to a search box of our own.
     *
     * This used to be a hand-rolled <input type="search"> above each record control, with select2's own search
     * switched off — two controls for one field, because select2's local search filters only the options already
     * in the DOM and a record control holds one page of a much larger source. `ajax` is the feature for exactly
     * that case: the user types in the picker, select2 asks this. The debounce moved to select2's `delay` and
     * the stale-answer guard into the transport (both in TaskForm.enhanceSelects); what stays here is the one
     * thing this file owns — reaching the API.
     */
    const searchRecords = async (code, term) => {
        const result = await global.TasksApi.fieldRecords(code, { term });
        if (result.ok) { return result.data || []; }

        // Not silent: an unreachable source reads as "no results" in the picker, and this says why.
        global.console?.warn?.(
            `[Tasks] searching records for field "${code}" failed `
            + `(status ${result.status}${result.reasonCode ? `, ${result.reasonCode}` : ''}).`);
        return [];
    };

    /*
     * Resolve the identities a task already carries back into records, so the EDIT form can display them.
     *
     * Without this the picker holds only its first page, and a task pointing at anything outside that page
     * cannot render its own value — the control keeps the old one and the save posts a different record than the
     * screen showed. Yesterday's round caught this exact shape on date fields.
     */
    const resolveStoredRecords = async (definitions, values) => {
        const byCode = {};
        const wanted = (definitions || []).filter(
            (definition) => global.TaskForm.customFieldControlKind(definition) === 'record');

        await Promise.all(wanted.map(async (definition) => {
            const ids = (values || [])
                .filter((entry) => entry?.definitionCode === definition.code && entry.value)
                .map((entry) => String(entry.value));
            if (ids.length === 0) { return; }

            const result = await global.TasksApi.fieldRecords(definition.code, { ids });
            if (result.ok && Array.isArray(result.data)) {
                byCode[definition.code] = result.data;
                return;
            }
            // The value is still written back — losing it would be worse — but it will read as unavailable
            // rather than as a raw identity (BL-049), and the reason is here rather than nowhere.
            global.console?.warn?.(
                `[Tasks] stored records for field "${definition.code}" could not be resolved `
                + `(status ${result.status}${result.reasonCode ? `, ${result.reasonCode}` : ''}).`);
        }));

        return byCode;
    };

    const bootCustomFields = async (people) => {
        const row = customFieldsRow();
        const section = el('taskCustomFields');
        if (!row || !section) { return; }

        const result = await global.TasksApi.fieldDefinitions();
        if (!result.ok) {
            global.console?.warn?.(
                `[Tasks] the configurable field catalogue could not be read (status ${result.status}); the `
                + 'section stays hidden.');
            return;
        }

        customFieldDefinitions = applicableDefinitions(result.data);
        if (customFieldDefinitions.length === 0) { return; }

        const options = await loadCustomFieldOptions(customFieldDefinitions, people);
        const rendered = global.TaskForm.renderCustomFields(row, customFieldDefinitions, options, {
            optionPlaceholder: t('customFieldOptionPlaceholder'),
            booleanYes: t('customFieldBooleanYes'),
            booleanNo: t('customFieldBooleanNo'),
            recordSearchPlaceholder: t('customFieldRecordSearchPlaceholder'),
            translate: t
        });

        // No definition survived rendering ⇒ the section stays hidden. An empty heading would announce a
        // capability the page cannot offer.
        section.classList.toggle('d-none', rendered.length === 0);
    };

    /*
     * Put the user in front of the field the dialog just named.
     *
     * Two details the obvious one-liner gets wrong. A select2 control's own <select> is HIDDEN — select2 renders
     * a sibling in its place — so both the scroll and the focus have to go to what is actually on screen. And the
     * scroll targets the field's COLUMN, so the label scrolls into view with the control rather than under the
     * sticky header.
     */
    const focusMissing = (control) => {
        if (!control) { return; }

        const column = control.closest('[data-task-field], .col-12, .col-md-4, .col-md-6, .col-md-8') || control;
        column.scrollIntoView({ behavior: 'smooth', block: 'center' });

        const rendered = control.classList.contains('select2-hidden-accessible')
            ? control.parentElement?.querySelector('.select2-selection')
            : control;
        rendered?.focus?.({ preventScroll: true });
    };

    /*
     * BL-072 — render the server's exclusion breakdown under the assignee picker, or nothing at all.
     *
     * Class toggle, never an inline style (FG-003). `textContent`, never `innerHTML`: the sentence is built from
     * translated templates and integers, and there is no reason for this node to be able to render markup.
     */
    const renderExcludedHint = (excluded) => {
        const node = el('taskAssigneeExcluded');
        if (!node) { return; }

        const text = global.TaskForm.describeExcludedCandidates(excluded, t);
        node.textContent = text;
        node.classList.toggle('d-none', text.length === 0);
    };

    /*
     * BL-023 — say (and MEAN) that upward work is a request.
     *
     * The direction comes from the SERVER, asked per chosen person, because the reporting chain lives there and
     * a browser guess would drift from what the create handler actually does. The button's word and the
     * server's behaviour therefore cannot disagree — which is the whole point: a control that quietly behaves
     * differently from its label is the defect this project keeps correcting.
     *
     * Fail-safe direction: an unreachable answer leaves the ordinary "Oluştur", because wrongly promising a
     * request is worse than wrongly promising an assignment (the server still opens the request either way).
     */
    let upwardCheck = 0;

    const applyUpwardDirection = (isUpward) => {
        const submit = el('taskSubmit');
        if (submit) {
            const label = isUpward
                ? submit.getAttribute('data-task-label-upward')
                : submit.getAttribute('data-task-label-default');
            if (label) { submit.textContent = label; }
        }
        el('taskForm')?.querySelectorAll('[data-task-field="upwardRequest"]').forEach((node) => {
            node.classList.toggle('d-none', !isUpward);
        });
    };

    const refreshAssignmentDirection = async () => {
        const target = el('taskAssignmentTarget')?.value;
        const assignee = el('taskAssignee')?.value;

        // Only a PERSON target can be upward: a pool has no single holder, and self-assignment never is.
        if (target !== 'Person' || !assignee) { applyUpwardDirection(false); return; }

        // Sequence guard: a slow answer for a previously chosen person must not relabel the button after the
        // user has moved on — the same stale-answer failure the record search had.
        const mine = ++upwardCheck;
        const result = await global.TasksApi.assignmentDirection(assignee);
        if (mine !== upwardCheck) { return; }
        applyUpwardDirection(!!(result.ok && result.data && result.data.isUpward));
    };

    const syncVisibility = () => {
        const form = el('taskForm');
        global.TaskForm.applyTargetVisibility(form, el('taskAssignmentTarget')?.value);
        // The approval manager only matters when approval is actually requested.
        form?.querySelectorAll('[data-task-field="approvalManager"]').forEach((node) => {
            node.classList.toggle('d-none', !el('taskApprovalRequired')?.checked);
        });
        // Same rule for the reviewer, keyed off its OWN switch.
        form?.querySelectorAll('[data-task-field="reviewer"]').forEach((node) => {
            node.classList.toggle('d-none', !el('taskReviewRequired')?.checked);
        });
        /*
         * The approval-request EMAIL follows the approval switch for the same reason the manager field does: no
         * approval is ever requested unless the switch is on, so offering the box there is a promise nothing
         * keeps. The claim event is target-driven instead, and rides applyTargetVisibility above.
         */
        const approvalOn = !!el('taskApprovalRequired')?.checked;
        form?.querySelectorAll('[data-task-field="notifyApprovalRequested"]').forEach((node) => {
            node.classList.toggle('d-none', !approvalOn);
            const box = node.querySelector('input');
            if (box) { box.disabled = !approvalOn; }
        });
        /*
         * BL-065 — the preferences belong to the channel, and the lead time belongs to the reminder. Nobody
         * should be tuning which events reach a switched-off channel, and a lead time with the due-soon reminder
         * unticked is a control that does nothing.
         */
        const emailOn = !!el('taskEmailNotifications')?.checked;
        form?.querySelectorAll('[data-task-field="notificationPrefs"]').forEach((node) => {
            node.classList.toggle('d-none', !emailOn);
        });
        const dueSoonOn = !!el('taskNotifyDueSoon')?.checked;
        form?.querySelectorAll('[data-task-field="reminderLead"]').forEach((node) => {
            node.classList.toggle('d-none', !(emailOn && dueSoonOn));
        });
    };

    const boot = async () => {
        const form = el('taskForm');
        if (!form) { return; }

        const mode = form.getAttribute('data-task-mode') || 'create';
        const taskId = form.getAttribute('data-task-id');

        /*
         * One labels object for every picker on the page, and it is also what select2's row templates read —
         * two vocabularies for the same list is how the pool row ends up reading "{0} kişi" on one surface.
         */
        const personLabels = {
            placeholder: t('assigneeSelectPlaceholder'),
            empty: t('assigneeEmpty'),
            nameUnavailable: t('personNameUnavailable'),
            holderCount: t('pickerHolderCount')
        };

        const positions = await global.TasksApi.assignablePositions();
        if (positions.ok) {
            global.TaskForm.renderPositionOptions(el('taskPoolPosition'), positions.data || [], personLabels);
        }

        /*
         * TWO people lists, and this is the one place the difference is made — BL-057.
         *
         * All four person pickers used to draw from ONE list, so narrowing "the list" would have been a
         * one-line change and would have silently killed intra-group approval: a task produced in GMG TR is
         * legitimately approved in GMG AZ by somebody who is neither above nor below the author, in another
         * company. Every leg of the assignment rule fails for that person and the work is still entirely
         * proper — because approval authority belongs to the PROCESS, not to the requester.
         *
         *   assignableRows → who may RECEIVE the work   → company-scoped   → assignee, watchers
         *   decisionRows   → who may DECIDE about it    → scope-EXEMPT     → reviewer, approval manager
         *
         * Watchers ride the scoped list deliberately: watching is not deciding, it is seeing — and letting
         * another company's employee watch a task is a data-access decision (Poland is inside the EU/GDPR,
         * Turkey is not), so it follows the receiving rule rather than the deciding one.
         *
         * Loaded before any draft is written back, so a handed-over assignee id can select its option.
         */
        const people = await global.TasksApi.assignablePeople();
        // The lookup answers `{ people, excluded }` now — only the server can say WHY somebody is missing.
        const assignableRows = people.ok ? people.data?.people || [] : [];
        global.TaskForm.renderPersonOptions(el('taskAssignee'), assignableRows, personLabels);
        global.TaskForm.renderPersonOptions(el('taskWatchers'), assignableRows, personLabels, { multiple: true });

        // BL-072 — say why the list is short, from the server's own breakdown. Never inferred here: the client
        // cannot tell "nobody holds a position" from "they work for another company".
        renderExcludedHint(people.ok ? people.data?.excluded : null);

        /*
         * The reviewer and the approval manager are the same QUESTION as the assignee — "which person?" — and
         * are rendered by the same function, but they are not the same ANSWER. They were bare text inputs whose
         * contents went straight to a Guid parameter, which meant the only correct way to fill them was to type
         * a GUID.
         */
        const decisions = await global.TasksApi.decisionMakers();
        const decisionRows = decisions.ok ? decisions.data?.people || [] : [];
        global.TaskForm.renderPersonOptions(el('taskReviewer'), decisionRows, personLabels);
        global.TaskForm.renderPersonOptions(el('taskApprovalManager'), decisionRows, personLabels);

        // Before any hydration: the controls have to EXIST before stored values can be written into them.
        // Person-typed configurable fields follow the ASSIGNMENT rule: they name someone who does the work.
        await bootCustomFields(assignableRows);

        if (mode === 'create') {
            // Continue the quick-create draft if one was handed over.
            const draft = global.TaskForm.readDraft();
            if (draft) {
                writeForm(draft);
                global.TaskForm.clearDraft();
            }
        } else if (taskId) {
            const existing = await global.TasksApi.get(taskId);
            if (existing.ok && existing.data) {
                writeForm(existing.data);
                withheldOnEdit = {
                    plannedDate: (existing.data.plannedDate || '').slice(0, 10) || null,
                    startAt: (existing.data.startAt || '').slice(0, 10) || null,
                    estimateHours: existing.data.estimateHours ?? null
                };
                form.setAttribute('data-task-version', existing.data.version);
                // Effort actuals are visible on edit but never editable.
                ['spentHours', 'remainingHours'].forEach((field) => {
                    form.querySelectorAll(`[data-task-field="${field}"]`)
                        .forEach((node) => node.classList.remove('d-none'));
                });
                setValue('taskSpentHours', existing.data.spentHours);
                setValue('taskRemainingHours', existing.data.remainingHours ?? '');
                // The stored configurable values. Without this the edit posts back a payload with every one of
                // them emptied: a read-only create would have become a data-LOSING edit.
                //
                // Record fields need their identities resolved FIRST — the picker holds one page, and a stored
                // value outside it has no option to select without this.
                const storedValues = existing.data.fieldValues || [];
                const storedRecords = await resolveStoredRecords(customFieldDefinitions, storedValues);
                global.TaskForm.writeCustomFieldValues(
                    customFieldsRow(), storedValues, storedRecords,
                    { recordUnavailable: t('customFieldRecordUnavailable') });
            }
        }

        /*
         * select2 LAST, after every control exists and after hydration has written its values in.
         *
         * Order is the whole point: the assignee/pool pickers are filled by fetch, the configurable controls are
         * created by renderCustomFields, and an edit writes stored values into all of them. Binding earlier would
         * enhance the two markup selects and leave everything built afterwards bare — the partial fix that looks
         * finished because the static half of the form is correct.
         */
        global.TaskForm.enhanceSelects(form, { searchRecords, rowLabels: personLabels });
        // flatpickr after hydration too, for the same reason: it reads the input's value when it initialises, so
        // a picker built before the stored date was written in would open on today instead of the task's date.
        global.TaskForm.enhanceDates(form);
        // Chips after hydration for the same reason as the rest: Tagify reads the input's value when it starts.
        global.TaskForm.enhanceTags(form);

        el('taskAssignmentTarget')?.addEventListener('change', syncVisibility);
        // BL-023 — the direction depends on WHO, so both controls have to re-ask.
        el('taskAssignmentTarget')?.addEventListener('change', refreshAssignmentDirection);
        el('taskAssignee')?.addEventListener('change', refreshAssignmentDirection);
        el('taskApprovalRequired')?.addEventListener('change', syncVisibility);
        el('taskReviewRequired')?.addEventListener('change', syncVisibility);
        el('taskEmailNotifications')?.addEventListener('change', syncVisibility);
        el('taskNotifyDueSoon')?.addEventListener('change', syncVisibility);
        syncVisibility();

        /*
         * The save is bound to the FORM's submit, not to the button's click: the actions now sit in the header
         * and reach the form through `form="taskForm"`, which is a submit — and it also means Enter in a text
         * field saves, like every other form in the app. preventDefault keeps the browser from navigating; the
         * payload still goes through TasksApi exactly as before.
         */
        form.addEventListener('submit', async (event) => {
            event.preventDefault();
            const fieldValues = global.TaskForm.readCustomFieldValues(customFieldsRow(), customFieldDefinitions);
            const draft = { ...readForm(), fieldValues };

            const check = global.TaskForm.validateDraft(draft);
            // A required configurable field left empty blocks the save here AND is refused by the server. Both
            // sides hold the rule on purpose: a client-only rule is a suggestion.
            const fieldCheck = global.TaskForm.validateCustomFields(customFieldDefinitions, fieldValues);
            if (!check.valid || !fieldCheck.valid) {
                /*
                 * NAME the fields. "Zorunlu" alone, on a nine-card form, tells the user a rule they already know
                 * and nothing about where to look — finding the empty control meant reading the DOM.
                 */
                const missing = global.TaskForm.missingRequiredFields(
                    check, fieldCheck, customFieldDefinitions, t);
                // And take the user there AFTER the dialog closes: the first missing field is the topmost one,
                // because the validator reports them in the form's own order. A dialog that names a field nine
                // cards down is only half an answer. Focusing while the dialog is still open would fight it for
                // the focus and leave the confirm button unreachable by keyboard.
                await global.DitenModal.warning({
                    title: t('requiredFieldHint'),
                    message: `${t('requiredFieldsMissing')} ${missing.map((entry) => entry.label).join(', ')}`,
                    confirmButtonText: t('actionOk')
                });
                focusMissing(missing.length > 0 ? el(missing[0].id) : null);
                return;
            }

            const result = mode === 'edit'
                ? await global.TasksApi.update(
                    taskId,
                    global.TaskForm.buildUpdatePayload(
                        draft, form.getAttribute('data-task-version'), withheldOnEdit))
                : await global.TasksApi.create(global.TaskForm.buildCreatePayload(draft));

            if (result.ok) {
                global.TaskForm.clearDraft();
                // Awaited so the acknowledgement is actually seen; navigating immediately would kill it.
                await global.DitenModal.success({
                    title: t(mode === 'edit' ? 'toastSaved' : 'toastCreated'),
                    timer: 1600
                });
                // The Task Center is the single personal entry point, so saving returns there — not to a
                // competing task list (MOD-0024 pack §5 / the manifest's own note).
                global.location.href = WORK_CENTER_URL;
            } else {
                // Reason-code driven, so "no organization unit" reads as itself rather than a generic failure.
                global.DitenModal.error({
                    title: global.TasksApi.failureMessage(result),
                    confirmButtonText: t('actionOk')
                });
            }
        });
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})(typeof window !== 'undefined' ? window : globalThis);
