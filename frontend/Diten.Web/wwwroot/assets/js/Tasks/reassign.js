'use strict';

/*
 * HANDING WORK ON, FROM THE TASK FORM — MOD-0024, owner report 2026-09-23.
 *
 * THE DEFECT THIS REPLACES. The edit form drew the whole assignment section and let the user change it, and
 * `UpdateTaskItemRequest` carries no assignment field at all: the new person travelled in the payload and the
 * server dropped it while the screen reported a saved change. Measured against the request record's twenty-one
 * fields — Title, Priority, DueAt, DelegationAllowed and the rest are there; AssigneeUserId is not.
 *
 * WHY A SEPARATE ACTION AND NOT A SAVEABLE FIELD. The server has always modelled a handover as an operation:
 * `POST /{id}/reassign` takes the new person AND a mandatory reason, refuses a pool task, and admits only the
 * current assignee or the requester. That is the same line SAP and Oracle draw — forwarding a work item is
 * recorded, because "who handed this to whom, when, and why" is the question asked six months later. Making
 * the field saveable would have answered the complaint and lost the reason.
 *
 * WHAT THIS FILE DOES NOT DO. It does not decide who may reassign; the server does, and its refusal is shown
 * verbatim. The button is hidden where the server is CERTAIN to refuse (a pool task, a closed task, a caller
 * who is neither the assignee nor the creator) so the screen does not offer what cannot happen — UAS-001's
 * reasoning applied to an action rather than a page.
 */
(function (global) {
    const el = (id) => global.document.getElementById(id);
    const L = () => global.L10n || {};
    const t = (key) => L()[key] || '';
    const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) =>
        ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    const CLOSED = ['Completed', 'Cancelled', 'Closed'];

    /** The server's own admission rule, mirrored — see the note above on why this is a display gate only. */
    const serverWouldRefuse = (task) => {
        const me = (global.CurrentUser && global.CurrentUser.id) || null;
        if (!task) { return true; }
        if (task.assignmentTarget === 'PositionPool') { return true; }
        if (CLOSED.includes(task.lifecycle) || CLOSED.includes(task.normalizedStatus)) { return true; }
        if (!me) { return true; }
        return String(task.assigneeUserId || '') !== me && String(task.createdByUserId || '') !== me;
    };

    /** The label the picker already resolved, so a GUID is never shown (pack §12 K6.4). */
    const selectedText = (selectId) => {
        const select = el(selectId);
        const option = select && select.options[select.selectedIndex];
        return (option && option.value) ? option.textContent.trim() : '';
    };

    const fillSummary = (task) => {
        const targetKeys = { SelfAssigned: 'targetSelf', Person: 'targetPerson', PositionPool: 'targetPool' };
        const targetBox = el('taskAssignmentTargetRead');
        if (targetBox) { targetBox.value = t(targetKeys[task.assignmentTarget] || '') || task.assignmentTarget || ''; }

        const whoBox = el('taskAssigneeRead');
        if (!whoBox) { return; }
        if (task.assignmentTarget === 'PositionPool') { whoBox.value = selectedText('taskPoolPosition'); return; }
        if (task.assignmentTarget === 'SelfAssigned') {
            whoBox.value = (global.CurrentUser && global.CurrentUser.displayName) || selectedText('taskAssignee') || '';
            return;
        }
        whoBox.value = selectedText('taskAssignee');
    };

    const openDialog = async (taskId, task) => {
        /*
         * ⚠ A RAW DIALOG, NAMED AS ONE. Two fields — the person and the reason — and the shared confirm
         * deliberately takes one input, so this cannot be a `showConfirm`; `_GlobalConfirmation.cshtml` says as
         * much in its own words. What "raw" may never mean is UNDRESSED: the published package below is what
         * every other multi-field dialog in the product wears (Meetings has three).
         *
         * It is called through `global.Swal` in full, not through a short alias. An earlier draft wrote
         * `const S = global.Swal` and the product-wide raw-dialog guard — which looks for `Swal.fire(` — did
         * not see it. A rule that a rename can walk past is not a rule, so the file names itself here and is
         * listed, with this reason, in both guards' exception lists.
         */
        const look = global.DitenDialogAppearance;
        if (!global.Swal || typeof look !== 'function') { return; }

        /*
         * The SAME list the create form offers, because it is the list the server validates against
         * (TaskAssigneeEligibility). Offering anyone else builds a dialog whose confirm is refused.
         */
        const res = await global.TasksApi.assignablePeople();
        const people = (res.ok ? res.data : []).filter((person) =>
            String(person.userId || person.id || '') !== String(task.assigneeUserId || ''));
        if (!people.length) {
            global.showToast?.(t('reassignNoPeople'), 'error');
            return;
        }

        const options = people.map((person) =>
            `<option value="${esc(person.userId || person.id)}">${esc(person.displayName || person.userId || person.id)}</option>`).join('');

        const result = await global.Swal.fire({
            ...look({ width: '520px' }),
            title: look.iconHtml(null, 'bx-transfer') + `<span>${esc(t('reassignTitle'))}</span>`,
            html: `<p class="${look.description}">${esc(t('reassignHint'))}</p>`
                + `<label class="form-label d-block w-100 text-start" for="taskReassignTo">${esc(t('reassignAssigneeLabel'))}</label>`
                + `<select id="taskReassignTo" class="form-select">`
                + `<option value="">${esc(t('reassignAssigneePlaceholder'))}</option>${options}</select>`
                + `<label class="form-label d-block w-100 text-start" for="taskReassignReason">${esc(t('reassignReasonLabel'))}</label>`
                + `<textarea id="taskReassignReason" class="form-control" rows="3" maxlength="500"`
                + ` placeholder="${esc(t('reassignReasonPlaceholder'))}"></textarea>`,
            showCancelButton: true,
            confirmButtonText: t('reassignAction'),
            cancelButtonText: t('cancel'),
            preConfirm: () => {
                const to = el('taskReassignTo').value;
                const reason = el('taskReassignReason').value.trim();
                // Both are refused by the server; saying so here saves a round trip, and the server still decides.
                if (!to) { global.Swal.showValidationMessage(t('reassignAssigneeRequired')); return false; }
                if (!reason) { global.Swal.showValidationMessage(t('reassignReasonRequired')); return false; }
                return { to, reason };
            }
        });
        if (!result.isConfirmed || !result.value) { return; }

        const response = await global.TasksApi.transition(taskId, 'reassign', {
            expectedVersion: Number(task.version) || 1,
            assigneeUserId: result.value.to,
            reason: result.value.reason
        });
        if (response.ok) {
            global.showToast?.(t('reassignDone'), 'success');
            // Reloaded rather than patched: the handover changes who owns the task, which changes what this
            // page may show. The server's answer to that question is the only one worth drawing.
            global.location.reload();
            return;
        }
        // The server's refusal, verbatim — it knows rules this screen deliberately does not duplicate.
        global.showToast?.((response.errors && response.errors[0]) || t('errorOccurred'), 'error');
    };

    global.TaskReassign = {
        /** Called by form-page.js once the edit form has been hydrated from the loaded task. */
        mount: (taskId, task) => {
            if (!task) { return; }
            fillSummary(task);
            const button = el('btnTaskReassign');
            if (!button || serverWouldRefuse(task)) { return; }
            button.classList.remove('d-none');
            button.addEventListener('click', () => { void openDialog(taskId, task); });
        }
    };
})(typeof window !== 'undefined' ? window : globalThis);
