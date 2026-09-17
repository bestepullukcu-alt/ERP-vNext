'use strict';

/*
 * MOD-0357 S4/S6 — the "create task from meeting" dialog, extracted from Meetings/form.js so the Minutes editor
 * (S6, a decision row's own "Görev oluştur") can open the SAME dialog the Details page's agenda-item tasks
 * already use, rather than a second hand-rolled copy (WP's own instruction: "S4 diyaloğunu yeniden kullan").
 * One body, two callers, distinguished only by which of `agendaItemId`/`decisionCode` they pass — the two are
 * mutually exclusive bridge moments and never both set by the same caller.
 */
(function (global) {
    const esc = (value) => String(value ?? '')
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

    // Platform/Workflow/workflow.api.js's own precedent — the caller-supplied key K11 needs.
    const newIdempotencyKey = () => {
        if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') { return crypto.randomUUID(); }
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
            const r = (Math.random() * 16) | 0;
            const v = c === 'x' ? r : (r & 0x3) | 0x8;
            return v.toString(16);
        });
    };

    /**
     * @param {object} options
     * @param {string} options.meetingId
     * @param {Function} options.t                 translate(key)
     * @param {string} [options.agendaItemId]       the agenda line this task is "from" (Details page)
     * @param {string} [options.decisionCode]       the decision this task is "from" (Minutes editor, S6)
     * @param {Function} [options.onCreated]        called with the create result's `data` on success
     */
    const open = (options) => {
        const t = options.t;
        if (!global.Swal || !global.DitenDialog) { return; }
        const dialogLook = global.DitenDialog.dialogLook();
        const dialogIcon = global.DitenDialog.dialogIcon('info', 'bx-task');

        global.Swal.fire(Object.assign({
            title: dialogIcon + '<span>' + esc(t('createTaskFromMeeting')) + '</span>',
            html: `<label class="form-label d-block text-start" for="mtgTaskTitle">${esc(t('taskTitleLabel'))}</label>`
                + `<input type="text" id="mtgTaskTitle" class="form-control" maxlength="200" autocomplete="off" />`
                + `<label class="form-label d-block text-start mt-3" for="mtgTaskAssignee">${esc(t('taskAssigneeLabel'))}</label>`
                + `<select id="mtgTaskAssignee" class="form-select"><option value="">${esc(t('taskAssigneeSelf'))}</option></select>`
                + `<label class="form-label d-block text-start mt-3" for="mtgTaskDueAt">${esc(t('taskDueAtLabel'))}</label>`
                + `<input type="text" id="mtgTaskDueAt" class="form-control wcn-date-input" autocomplete="off" />`,
            showCancelButton: true,
            confirmButtonText: t('createTaskFromMeeting'),
            cancelButtonText: t('cancel'),
            didOpen: async (popup) => {
                const dateInput = document.getElementById('mtgTaskDueAt');
                if (global.flatpickr) {
                    global.flatpickr(dateInput, { enableTime: false, dateFormat: 'Y-m-d', disableMobile: true });
                }
                // The SAME assignable-people lookup TaskWorkItemProvider's own reassign dialog reads — never a
                // second, looser list built for this one dialog.
                const peopleResult = await global.TasksApi?.assignablePeople?.();
                const people = peopleResult?.ok ? peopleResult.data : [];
                const select = document.getElementById('mtgTaskAssignee');
                people.forEach((person) => {
                    select.appendChild(new Option(person.displayName || person.userId, person.userId));
                });
                global.DitenDialog.bindDialogSelect2(select, popup);
            },
            preConfirm: () => {
                const title = String(document.getElementById('mtgTaskTitle')?.value || '').trim();
                if (!title) { global.Swal.showValidationMessage(t('taskTitleRequired')); return false; }
                const assigneeUserId = String(document.getElementById('mtgTaskAssignee')?.value || '').trim() || null;
                const dueAtLocal = String(document.getElementById('mtgTaskDueAt')?.value || '').trim();
                const dueAtDate = dueAtLocal ? new Date(`${dueAtLocal}T00:00:00`) : null;
                const dueAt = dueAtDate && !Number.isNaN(dueAtDate.getTime()) ? dueAtDate.toISOString() : null;
                return { title, assigneeUserId, dueAt };
            }
        }, dialogLook)).then(async (res) => {
            if (!res.isConfirmed || !res.value) { return; }
            const result = await global.MeetingsApi.createTaskFromMeeting(options.meetingId, {
                title: res.value.title,
                description: null,
                assigneeUserId: res.value.assigneeUserId,
                dueAt: res.value.dueAt,
                agendaItemId: options.agendaItemId || null,
                taskTypeId: null,
                idempotencyKey: newIdempotencyKey(),
                decisionCode: options.decisionCode || null
            });
            if (!result.ok) {
                global.DitenModal?.error?.({ title: t('errorOccurred'), message: global.MeetingsApi.failureMessage(result) });
                return;
            }
            global.DitenModal?.success?.({ title: t('toastTaskCreated'), timer: 1200 });
            options.onCreated?.(result.data);
        });
    };

    global.MeetingsTaskFromMeetingDialog = { open };
})(typeof window !== 'undefined' ? window : globalThis);
