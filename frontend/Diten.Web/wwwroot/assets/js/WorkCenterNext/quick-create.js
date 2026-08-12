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

        /*
         * A due date is required for every target, so default it to today rather than leaving an empty required
         * field the user has to discover. Written THROUGH the picker when one is bound: setting `.value`
         * directly leaves flatpickr's own state stale, so the calendar would open on the wrong month and the
         * next pick would fight the text in the box.
         */
        const dueAt = el('quickDueAt');
        if (dueAt && !dueAt.value) {
            const today = new Date().toISOString().slice(0, 10);
            if (dueAt._flatpickr) { dueAt._flatpickr.setDate(today, false); } else { dueAt.value = today; }
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
            /*
             * A REQUIRED configurable field this offcanvas does not carry. Quick create is deliberately a short
             * form — it has title, target, due date and priority, and nothing else — so once a tenant marks a
             * configurable field required, no draft made here can satisfy the server.
             *
             * Repeating the refusal would leave the user pressing a button that cannot ever work. Instead the
             * draft is HANDED OVER to the detailed form, which renders those fields: the same depth handover
             * "Detaylı form" already performs, taken automatically at the one moment it is forced.
             */
            if (result.reasonCode === 'TASK_FIELD_VALUE_INVALID') {
                await global.DitenModal.warning({
                    title: global.TasksApi.failureMessage(result),
                    confirmButtonText: t('actionOpenDetailed')
                });
                openDetailed();
                return null;
            }

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

        /*
         * People who currently hold a position, and who I may hand work to (BL-057 narrows this to my company
         * scope). An empty list is explained, not left blank.
         *
         * `.data.people` — the lookup answers an OBJECT, `{ people, excluded }`, since BL-057. This file kept
         * passing `.data` straight through, and an object is not an array: the picker rendered its "nobody
         * holds a position" empty state on every load, on a tenant full of people. The full form was updated in
         * that round and this shortcut was not.
         */
        const people = await global.TasksApi.assignablePeople();
        const peopleRows = people.ok ? people.data?.people || [] : [];
        global.TaskForm.renderPersonOptions(el('quickAssignee'), peopleRows, personLabels());

        /*
         * The FULL FORM's own enhancers, called rather than copied. The two surfaces share one draft, so a
         * second implementation here would be a second truth about the same value — the date would render one
         * way before "Detaylı form" and another way after it.
         *
         * enhanceSelects wraps each control in a `.position-relative` div and points select2's dropdownParent
         * at that wrapper. That is also what makes select2 usable INSIDE an offcanvas: the dropdown is rendered
         * next to its control instead of being appended to <body>, so it inherits the offcanvas's stacking
         * context and cannot end up painted underneath the backdrop.
         *
         * Both run AFTER the options are rendered — select2 copies the option list at bind time, so binding an
         * empty picker first would produce a permanently empty dropdown.
         */
        const offcanvasRoot = el(OFFCANVAS_ID);
        global.TaskForm.enhanceSelects(offcanvasRoot);
        global.TaskForm.enhanceDates(offcanvasRoot);
    };

    global.WcnQuickCreate = { TASK_CREATED_EVENT, open, close, submit, readDraft, openDetailed, wire };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', wire);
    } else {
        wire();
    }
})(typeof window !== 'undefined' ? window : globalThis);
