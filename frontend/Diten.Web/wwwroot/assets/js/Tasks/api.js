'use strict';

/*
 * MOD-0024 — same-origin API client. Every call goes to /Tasks/api/* on this app; the JWT lives in an HTTP-only
 * cookie the server attaches, so no token and no service port ever appears in the browser.
 */
(function (global) {
    const BASE = '/Tasks/api';

    const request = async (method, path, body) => {
        let response;
        try {
            response = await global.fetch(`${BASE}${path}`, {
                method,
                headers: body ? { 'Content-Type': 'application/json', Accept: 'application/json' }
                              : { Accept: 'application/json' },
                credentials: 'same-origin',
                body: body ? JSON.stringify(body) : undefined
            });
        } catch (_) {
            return { ok: false, status: 0, reasonCode: 'UNAVAILABLE', data: null };
        }

        let payload = null;
        try { payload = await response.json(); } catch (_) { /* 204 and empty bodies are fine */ }

        return {
            ok: response.ok,
            status: response.status,
            // The upstream reason code is passed through so the UI can react precisely (e.g. a claim race).
            reasonCode: payload?.reason_code ?? payload?.reasonCode ?? null,
            data: payload?.data ?? null,
            errors: payload?.errors ?? []
        };
    };

    /*
     * Turns an API failure into the message the user should read, by REASON CODE rather than by passing server
     * text through — that is how the message stays in the user's language (the code→resx bridge).
     * Keys are camelCase because that is what the serialized l10n payload contains.
     */
    const REASON_CODE_MESSAGE_KEYS = {
        ORGANIZATION_UNIT_UNRESOLVED: 'errorOrganizationUnitUnresolved',
        TASK_ALREADY_CLAIMED: 'errorAlreadyClaimed',
        POSITION_NOT_ASSIGNABLE: 'errorPositionNotAssignable',
        // MOD-0024's own refusals.
        TASK_CONCURRENCY_CONFLICT: 'errorConcurrencyRefreshed',
        CHECKLIST_INCOMPLETE: 'errorChecklistIncomplete',
        // Commenting on a closed task, and a comment that is empty or over the length limit.
        TASK_COMMENT_TASK_CLOSED: 'errorCommentTaskClosed',
        TASK_COMMENT_TEXT_INVALID: 'errorCommentTextInvalid',
        // A plan write with no date at all (a 400, not a 409 — it never reaches BLOCKING_REASON_CODES).
        TASK_PLAN_DATE_REQUIRED: 'errorPlanDateRequired',
        APPROVAL_PENDING: 'errorApprovalPending',
        // An unmet predecessor. Same string the PROJECTION uses to disable the button, deliberately: the greyed
        // control and this refusal are one fact seen from two sides.
        DEPENDENCY_BLOCKED: 'errorDependencyBlocked',
        // An open subtask (BL-035). Same string the projection uses to disable `complete`, same reasoning.
        SUBTASK_BLOCKED: 'errorSubtaskBlocked',
        // Cancelling is the requester's right: an assignee gets 403 with this code. failureMessage checks the
        // reason code BEFORE the status, so this replaces the generic "you are not allowed" with the reason.
        TASK_CANCEL_NOT_REQUESTER: 'errorCancelNotRequester',
        TASK_WAITING_REASON_REQUIRED: 'errorWaitingReasonRequired',
        // Handing work back (return) or on (reassign): both refuse without a reason, and both refuse an actor
        // who is neither the holder nor the requester.
        TASK_HANDOVER_REASON_REQUIRED: 'errorHandoverReasonRequired',
        TASK_RETURN_NOT_ASSIGNEE: 'errorReturnNotAssignee',
        TASK_REASSIGN_NOT_PERMITTED: 'errorReassignNotPermitted',
        TASK_ASSIGNEE_NOT_ASSIGNABLE: 'errorAssigneeNotAssignable',
        // Every blocking code MOD-0023's gate can answer with, read from
        // EvaluateWorkflowTransitionGateHandler rather than guessed. A blocked transition used to arrive as a bare
        // 500, so none of these ever had a message.
        WORKFLOW_PENDING_APPROVAL: 'errorApprovalPending',
        WORKFLOW_WAITING_EVIDENCE: 'errorApprovalWaitingEvidence',
        WORKFLOW_REJECTED: 'errorApprovalRejected',
        WORKFLOW_CANCELLED: 'errorApprovalCancelled',
        WORKFLOW_NOT_TERMINAL_APPROVED: 'errorApprovalNotApproved',
        // The gate's own code when it cannot reach a verdict (kept at its original spelling, which is the value
        // already on the wire).
        WorkflowGateEvaluationFailed: 'errorApprovalGateUnavailable'
    };

    /*
     * A 409 carrying one of the gate's blocking codes is a RULE refusing the write, not a lost race. The two need
     * different messages and different recovery advice, so the caller must be able to tell them apart.
     */
    const BLOCKING_REASON_CODES = new Set([
        'APPROVAL_PENDING',
        'CHECKLIST_INCOMPLETE',
        'DEPENDENCY_BLOCKED',
        'SUBTASK_BLOCKED',
        'TASK_COMMENT_TASK_CLOSED',
        'WORKFLOW_PENDING_APPROVAL',
        'WORKFLOW_WAITING_EVIDENCE',
        'WORKFLOW_REJECTED',
        'WORKFLOW_CANCELLED',
        'WORKFLOW_NOT_TERMINAL_APPROVED',
        'WorkflowGateEvaluationFailed'
    ]);

    const isTransitionBlocked = (result) =>
        result?.status === 409 && BLOCKING_REASON_CODES.has(result?.reasonCode);

    /*
     * A concurrency conflict is the explicit code — OR a bare 409 with no code at all, which is the only honest
     * reading of "conflict, reason unstated". A 409 whose code we do not recognise is NOT silently folded in here:
     * it warns and falls through to the reason-code message path, so a new server code shows up in the console
     * instead of being mislabelled as someone else's edit.
     */
    const isConcurrencyConflict = (result) => {
        if (result?.status !== 409) { return result?.reasonCode === 'TASK_CONCURRENCY_CONFLICT'; }
        if (!result.reasonCode) { return true; }
        if (result.reasonCode === 'TASK_CONCURRENCY_CONFLICT') { return true; }
        if (!BLOCKING_REASON_CODES.has(result.reasonCode) && !REASON_CODE_MESSAGE_KEYS[result.reasonCode]) {
            global.console?.warn?.(
                `[TasksApi] unmapped 409 reason code "${result.reasonCode}" — add it to REASON_CODE_MESSAGE_KEYS ` +
                'and BLOCKING_REASON_CODES, plus the 7 TasksIndex resx files.');
        }
        return false;
    };

    const failureMessage = (result) => {
        const t = (key) => global.TasksL10n?.t?.(key) ?? key;
        const byReason = REASON_CODE_MESSAGE_KEYS[result?.reasonCode];
        if (byReason) { return t(byReason); }
        if (result?.reasonCode) {
            // Never silent: an unmapped code degrades to the generic message, and says so in the console so the
            // gap is findable instead of looking like an ordinary failure.
            global.console?.warn?.(
                `[TasksApi] no message key for reason code "${result.reasonCode}"; showing the generic error.`);
        }
        if (result?.status === 403) { return t('errorNoAccess'); }
        if (result?.status === 0) { return t('errorUnavailable'); }
        return t('errorOccurred');
    };

    global.TasksApi = {
        REASON_CODE_MESSAGE_KEYS,
        BLOCKING_REASON_CODES,
        isTransitionBlocked,
        isConcurrencyConflict,
        failureMessage,
        list: () => request('GET', '/list'),
        get: (id) => request('GET', `/${id}`),
        create: (payload) => request('POST', '', payload),
        update: (id, payload) => request('PUT', `/${id}`, payload),
        transition: (id, action, payload) => request('POST', `/${id}/${action}`, payload || {}),
        // Its own method, not `transition(id, 'plan', ...)`: the body shape is different (plannedDate, not a
        // reason code), and there is no ExpectedVersion-carrying generic path that would let a caller forget it.
        plan: (id, payload) => request('POST', `/${id}/plan`, payload),
        assignablePositions: () => request('GET', '/assignable-positions'),
        assignablePeople: () => request('GET', '/assignable-people'),

        // ── Phase 2 ──────────────────────────────────────────────────────────
        // expectedVersion guards the checklist RUN, which has its own version separate from the task's.
        setChecklistItemState: (taskId, payload) =>
            request('POST', `/${taskId}/checklist/items/state`, payload),
        addChecklistItem: (taskId, payload) => request('POST', `/${taskId}/checklist/items`, payload),
        // Comments are POST-only, deliberately: they are immutable, so there is no update or delete to call.
        addComment: (taskId, payload) => request('POST', `/${taskId}/comments`, payload),
        createFromTemplate: (payload) => request('POST', '/from-template', payload)
    };
})(typeof window !== 'undefined' ? window : globalThis);
