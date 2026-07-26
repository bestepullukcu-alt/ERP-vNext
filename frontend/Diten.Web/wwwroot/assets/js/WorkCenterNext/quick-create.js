'use strict';

/*
 * MOD-0024 — quick task creation, owned by the Task Center.
 *
 * There is exactly ONE quick-create surface in the app and it lives here: the Task Center is the single personal
 * entry point, so "+ Yeni ▸ Görev" opens this offcanvas. /Tasks no longer offers a second one.
 *
 * Depth is a handover, not a fork: "Detaylı form" writes the CURRENT draft through TaskForm.writeDraft and sends
 * the user to /Tasks/Create, which reads that same draft (pack §12 K9 / DEV-1) — nothing typed is lost.
 *
 * On success it dispatches `wcn:task-created` rather than touching Task Center state directly, so app.js stays the
 * only owner of the work-item list.
 */
(function (global) {
    const TASK_CREATED_EVENT = 'wcn:task-created';
    const DETAILED_FORM_URL = '/Tasks/Create';
    const OFFCANVAS_ID = 'taskQuickCreate';

    const t = (key) => global.TasksL10n?.t?.(key) ?? key;
    const el = (id) => document.getElementById(id);

    // Keys are camelCase: the bridge serializes the C# property names through MVC's camelCase policy.
    const personLabels = () => ({
        placeholder: t('assigneeSelectPlaceholder'),
        empty: t('assigneeEmpty'),
        nameUnavailable: t('personNameUnavailable')
    });

    const readDraft = () => ({
        title: el('quickTitle')?.value,
        assignmentTarget: el('quickTarget')?.value,
        assigneeUserId: el('quickAssignee')?.value,
        poolPositionId: el('quickPoolPosition')?.value,
        dueAt: el('quickDueAt')?.value,
        priority: el('quickPriority')?.value
    });

    const resetDraft = () => {
        ['quickTitle', 'quickAssignee', 'quickDueAt'].forEach((id) => {
            const node = el(id);
            if (node) { node.value = ''; }
        });
    };

    const offcanvasInstance = () => {
        const node = el(OFFCANVAS_ID);
        if (!node || !global.bootstrap?.Offcanvas) { return null; }
        return global.bootstrap.Offcanvas.getOrCreateInstance(node);
    };

    const open = () => {
        const instance = offcanvasInstance();
        if (!instance) { return false; }

        // A due date is required for every target, so default it to today rather than leaving an empty required
        // field the user has to discover.
        const dueAt = el('quickDueAt');
        if (dueAt && !dueAt.value) {
            dueAt.value = new Date().toISOString().slice(0, 10);
        }

        instance.show();
        el('quickTitle')?.focus();
        return true;
    };

    const close = () => offcanvasInstance()?.hide();

    const submit = async () => {
        const draft = readDraft();
        const check = global.TaskForm.validateDraft(draft);
        if (!check.valid) {
            global.DitenModal.warning({ title: t('requiredFieldHint'), confirmButtonText: t('actionOk') });
            return null;
        }

        const result = await global.TasksApi.create(global.TaskForm.buildCreatePayload(draft));

        if (!result.ok) {
            // Chosen by reason code, so the user is told the actual cause (no position/unit, permission, …)
            // instead of a generic "an error occurred".
            global.DitenModal.error({
                title: global.TasksApi.failureMessage(result),
                confirmButtonText: t('actionOk')
            });
            return null;
        }

        global.TaskForm.clearDraft();
        resetDraft();
        close();
        document.dispatchEvent(new CustomEvent(TASK_CREATED_EVENT, { detail: result.data || null }));
        return result.data || null;
    };

    const openDetailed = () => {
        // Hand the draft over before navigating; the detailed form picks it up on load.
        global.TaskForm.writeDraft(readDraft());
        close();
        global.location.href = DETAILED_FORM_URL;
    };

    const wire = async () => {
        const target = el('quickTarget');
        if (!target || !el(OFFCANVAS_ID)) { return; }

        const offcanvas = el(OFFCANVAS_ID);
        const syncVisibility = () => global.TaskForm.applyTargetVisibility(offcanvas, target.value);
        target.addEventListener('change', syncVisibility);
        syncVisibility();

        el('quickSubmit')?.addEventListener('click', submit);
        el('quickMoreOptions')?.addEventListener('click', openDetailed);

        // Each option carries its organization unit so pooled work cannot silently reach the wrong facility.
        const positions = await global.TasksApi.assignablePositions();
        if (positions.ok) {
            global.TaskForm.renderPositionOptions(el('quickPoolPosition'), positions.data || []);
        }

        // People who currently hold a position. An empty list is explained, not left blank.
        const people = await global.TasksApi.assignablePeople();
        global.TaskForm.renderPersonOptions(el('quickAssignee'), people.ok ? people.data || [] : [], personLabels());
    };

    global.WcnQuickCreate = { TASK_CREATED_EVENT, open, close, submit, readDraft, openDetailed, wire };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', wire);
    } else {
        wire();
    }
})(typeof window !== 'undefined' ? window : globalThis);
