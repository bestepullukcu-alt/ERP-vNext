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
        organizationUnitId: el('taskOrganizationUnit')?.value,
        dueAt: el('taskDueAt')?.value,
        startAt: el('taskStartAt')?.value,
        plannedDate: el('taskPlannedDate')?.value,
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
        setValue('taskOrganizationUnit', draft.organizationUnitId);
        setValue('taskDueAt', (draft.dueAt || '').slice(0, 10));
        setValue('taskStartAt', (draft.startAt || '').slice(0, 10));
        setValue('taskPlannedDate', (draft.plannedDate || '').slice(0, 10));
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
    };

    const boot = async () => {
        const form = el('taskForm');
        if (!form) { return; }

        const mode = form.getAttribute('data-task-mode') || 'create';
        const taskId = form.getAttribute('data-task-id');

        const positions = await global.TasksApi.assignablePositions();
        if (positions.ok) {
            global.TaskForm.renderPositionOptions(el('taskPoolPosition'), positions.data || []);
        }

        // People who currently hold a position (pack §12 K6.4). Loaded before any draft is written back, so a
        // handed-over assignee id can select its option.
        const people = await global.TasksApi.assignablePeople();
        const peopleRows = people.ok ? people.data || [] : [];
        const personLabels = {
            placeholder: t('assigneeSelectPlaceholder'),
            empty: t('assigneeEmpty'),
            nameUnavailable: t('personNameUnavailable')
        };
        global.TaskForm.renderPersonOptions(el('taskAssignee'), peopleRows, personLabels);

        /*
         * The reviewer, the approval manager and the watchers are the SAME question as the assignee — "which
         * person?" — so they are answered from the same lookup and rendered by the same function. They were bare
         * text inputs whose contents went straight to a Guid parameter, which meant the only correct way to fill
         * them was to type a GUID; and the watcher box, being a string where the payload wanted a list, threw its
         * contents away entirely.
         */
        global.TaskForm.renderPersonOptions(el('taskReviewer'), peopleRows, personLabels);
        global.TaskForm.renderPersonOptions(el('taskApprovalManager'), peopleRows, personLabels);
        global.TaskForm.renderPersonOptions(el('taskWatchers'), peopleRows, personLabels, { multiple: true });

        // Before any hydration: the controls have to EXIST before stored values can be written into them.
        await bootCustomFields(people.ok ? people.data || [] : []);

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
        global.TaskForm.enhanceSelects(form, { searchRecords });
        // flatpickr after hydration too, for the same reason: it reads the input's value when it initialises, so
        // a picker built before the stored date was written in would open on today instead of the task's date.
        global.TaskForm.enhanceDates(form);

        el('taskAssignmentTarget')?.addEventListener('change', syncVisibility);
        el('taskApprovalRequired')?.addEventListener('change', syncVisibility);
        el('taskReviewRequired')?.addEventListener('change', syncVisibility);
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
                global.DitenModal.warning({ title: t('requiredFieldHint'), confirmButtonText: t('actionOk') });
                return;
            }

            const payload = global.TaskForm.buildCreatePayload(draft);
            const result = mode === 'edit'
                ? await global.TasksApi.update(taskId, {
                    ...payload,
                    expectedVersion: Number(form.getAttribute('data-task-version') || 1)
                })
                : await global.TasksApi.create(payload);

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
