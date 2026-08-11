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
        // Configurable fields (Phase 5). The server is the authority on these — a required field left empty, an
        // unknown definition code, a contract limit — and each refusal needs its own sentence, because "an error
        // occurred" beside a form of tenant-defined fields names nothing the user can act on.
        TASK_FIELD_VALUE_INVALID: 'errorFieldValueInvalid',
        TASK_FIELD_DEFINITION_UNKNOWN: 'errorFieldDefinitionUnknown',
        TASK_FIELD_LIMIT_EXCEEDED: 'errorFieldLimitExceeded',
        // A field pointing at a module record source nobody registered. Its own sentence, because the reader
        // can do nothing about it themselves — the definition is an administrator's to correct.
        FIELD_OPTION_SOURCE_INVALID: 'errorFieldOptionSourceInvalid',
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
        /*
         * BL-040/BL-048 — codes DERIVED from a FluentValidation rule, not curated by hand.
         *
         * `VALIDATION_<FIELD>_<RULE>` is what ValidationReasonCode builds when a rule does not name its own code,
         * so the field and the rule are in the code and the SERVER's English sentence never has to be shown. That
         * sentence is what carried the untranslated field name ("'Request Title', 200 karakterden…"): the raw
         * `errors` text is never rendered here, but without a mapped code the reader only got the generic
         * message. Two rules on Title, because "missing" and "too long" are different sentences in every
         * language.
         */
        VALIDATION_REQUEST_TITLE_NOT_EMPTY: 'errorTitleRequired',
        VALIDATION_REQUEST_TITLE_MAXIMUM_LENGTH: 'errorTitleTooLong',
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
        // ── Phase 5: configurable fields ─────────────────────────────────────
        // The catalogue the form renders. An ordinary task READ, not the manage permission: a user who may
        // create a task must be able to see the fields they are asked to fill.
        fieldDefinitions: () => request('GET', '/field-definitions'),
        /*
         * One field's option list, resolved SERVER-SIDE from the definition's own OptionsSourceKind/Key. The
         * browser never names a lookup key or a reference set: the definition does, so a tenant cannot reach a
         * data set merely by asking for it.
         */
        fieldOptions: (code) => request('GET', `/field-definitions/${encodeURIComponent(code)}/options`),
        /*
         * The same resolution, for a field whose values are ANOTHER MODULE'S RECORDS. Separate method, not a
         * separate contract: it answers the same TaskFieldOptionDto the line above does, because a picker must
         * not have to know which kind of source filled it.
         *
         * `term` is a SEARCH the server performs — a source can hold thousands of records, and the reason this
         * is not a dropdown is that they must never all cross the wire. `ids` is the edit path: identities
         * already on the task, resolved back into records the form can display.
         */
        fieldRecords: (code, options) => {
            const query = new URLSearchParams();
            if (options?.term) { query.set('term', options.term); }
            if (options?.ids?.length) { query.set('ids', options.ids.join(',')); }
            const suffix = query.toString();
            return request(
                'GET',
                `/field-definitions/${encodeURIComponent(code)}/records${suffix ? `?${suffix}` : ''}`);
        },
        /*
         * The sources an administrator may point a field at, for a chosen kind. Not part of the task form: this
         * feeds the field-definition SCREEN, where the source used to be typed by hand and a typo produced a
         * field that silently never appeared.
         */
        fieldOptionSources: (kind) =>
            request('GET', `/field-definitions/option-sources?kind=${encodeURIComponent(kind)}`),
        /*
         * BL-057 — TWO people lists, and they are two on purpose.
         *
         * `assignablePeople` answers "who may RECEIVE this work" and is limited to the actor's company scope:
         * same legal entity, below me in the reporting chain, or a scope granted to me. Its answer is an OBJECT
         * — `{ people, excluded }` — because only the server knows WHY somebody is missing (BL-072).
         *
         * `decisionMakers` answers "who may DECIDE about this work" (approver, reviewer) and is exempt from that
         * scope. A task produced in GMG TR is legitimately approved in GMG AZ by somebody who is neither above
         * nor below the author: approval authority belongs to the PROCESS, not to the requester. Serving both
         * from one call is the mistake that silently kills intra-group approval.
         */
        assignablePeople: () => request('GET', '/assignable-people'),
        decisionMakers: () => request('GET', '/decision-makers'),

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
