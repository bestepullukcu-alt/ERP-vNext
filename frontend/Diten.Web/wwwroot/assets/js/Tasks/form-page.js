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
            if (kind !== 'select') { return; }

            const result = await global.TasksApi.fieldOptions(definition.code);
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
        global.TaskForm.renderPersonOptions(el('taskAssignee'), people.ok ? people.data || [] : [], {
            placeholder: t('assigneeSelectPlaceholder'),
            empty: t('assigneeEmpty'),
            nameUnavailable: t('personNameUnavailable')
        });

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
                global.TaskForm.writeCustomFieldValues(customFieldsRow(), existing.data.fieldValues || []);
            }
        }

        el('taskAssignmentTarget')?.addEventListener('change', syncVisibility);
        el('taskApprovalRequired')?.addEventListener('change', syncVisibility);
        el('taskReviewRequired')?.addEventListener('change', syncVisibility);
        syncVisibility();

        el('taskSubmit')?.addEventListener('click', async () => {
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
