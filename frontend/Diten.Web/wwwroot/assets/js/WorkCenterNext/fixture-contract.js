'use strict';

(function (global) {
    const WORK_INTENTS = ['task', 'approval', 'review', 'issue', 'exception'];
    const ASSIGNMENT_MODES = ['direct', 'approval', 'groupQueue', 'offered'];
    const OWNERSHIP_STATES = ['unowned', 'assigned', 'owned', 'notApplicable'];
    const ADMISSION_STATES = ['pendingAcceptance', 'pendingClaim', 'pendingOffer', 'admitted', 'notApplicable'];
    const NORMALIZED_STATUSES = ['Pending', 'InProgress', 'Waiting', 'Done', 'Cancelled'];
    const TASK_LIFECYCLES = ['Open', 'Planned', 'InProgress', 'Waiting', 'PendingReview', 'Done', 'Cancelled', 'notApplicable'];
    const EXECUTION_STATES = ['notStarted', 'active', 'paused', 'notApplicable'];
    const TIMER_STATES = ['inactive', 'running', 'paused', 'notApplicable'];
    const SYSTEM_STATES = ['fresh', 'stale', 'sourceUnavailable', 'authorityEnded', 'processing', 'reconciliationRequired'];
    const ACTION_DEPTHS = ['inline', 'deeplink'];
    const REVIEW_MEETING_REQUIREMENTS = ['notAllowed', 'optional', 'required'];
    /*
     * WHY a work item is parked. Declared here because it was NOT: the fixtures said information/meeting, the
     * projection said externalInformation/approval/review, and nothing checked either — so the two drifted apart
     * silently and a task waiting on an approval was told it was waiting on external input.
     *
     * The canonical vocabulary is the PROJECTION's, because that is what real items emit and because approval and
     * review name the gates the engine actually has. `information` is gone: it meant the same as
     * externalInformation.
     *
     * `meeting` is FIXTURE-ONLY today — no provider emits it. It is kept rather than deleted because the contract
     * already models meetings (reviewMeetingPolicy, the scheduleReviewMeeting action), so this is a designed state
     * awaiting its provider (Faz 3b / BL-026), not an invented one; the showcase exists to demonstrate exactly
     * such states. Remove it if that work is ever abandoned.
     */
    const WAITING_CONTEXT_TYPES = ['externalInformation', 'approval', 'review', 'meeting'];
    /*
     * A subtask's state. Declared for the same reason the waiting vocabulary now is: it existed in the provider,
     * in the shell's icon map and in its label map, and in none of them as a stated contract — so a value added
     * on one side would reach the other as a blank row.
     *
     * `cancelled` is deliberately distinct from `not-started`: called-off work is not waiting to begin, and
     * BL-035's "a cancelled subtask does not gate its parent" rule needs the two to be different values.
     */
    const SUBTASK_STATUSES = ['not-started', 'in-progress', 'done', 'cancelled'];
    /*
     * How urgent the work is. Three levels, PascalCase (owner decision, 2026-07-29 / BL-032).
     *
     * PascalCase because the ENGINE already stores exactly this (TaskPriority = Low|Medium|High) and both write
     * surfaces already post it — only the fixtures said 'high', which is why nothing ever matched and the column
     * was hidden rather than fixed. Display is a separate concern: Turkish screens read Düşük/Orta/Yüksek, the
     * contract stays Low/Medium/High.
     *
     * Three rather than five: with no SLA engine yet (WC-2) more levels would be false precision, and "P1"
     * promises a response we cannot make. Three→five is additive; five→three is a migration.
     */
    const PRIORITIES = ['Low', 'Medium', 'High'];
    /*
     * How work stands against its deadline (WC-2). Declared here because it was NOT: the state existed in the
     * browser's own SLA maths, in the shell's colour map, in its group-heading map and in its filter, and in none
     * of them as a stated contract — the fifth value anyone added on one side would have reached the other as an
     * uncoloured chip with a blank heading. That is the same disease this session met four times over.
     *
     * The vocabulary is declared; the THRESHOLD is not. Where the warning window opens is a policy a tenant may
     * tune (WorkAggregation:Sla:DueSoonWithinWorkingDays on the server), and freezing a business rule into a
     * shape validator would have made changing it a frontend edit. The two sides must agree on the WORDS, not on
     * the number of days.
     *
     * `no-sla` is a first-class state, not an absence: work with no deadline is a legitimate, common situation
     * and reporting it as on-track would claim a comfort nobody measured.
     */
    const SLA_STATES = ['overdue', 'due-soon', 'on-track', 'no-sla'];
    /*
     * A typed dependency edge, in the ENGINE's vocabulary (TaskDependencyType) for the same reason priority is:
     * one canonical spelling on the wire, display abbreviations ("FS") built in the shell where the 7 languages
     * live. Fixtures said 'FS' and the engine said 'FinishToStart', so a real dependency would have reached the
     * shell's type map as a miss and rendered as a bare unknown token.
     *
     * A dependency's STATE is the predecessor task's state, so it reuses SUBTASK_STATUSES rather than declaring a
     * second vocabulary for the same idea — and that is what gives it `cancelled`, which the blocking rule needs:
     * a called-off predecessor blocks nothing.
     */
    const DEPENDENCY_TYPES = ['FinishToStart', 'FinishToFinish', 'StartToStart', 'StartToFinish'];
    const DEPENDENCY_DIRECTIONS = ['pred', 'succ'];
    /*
     * One feed, two kinds — the shape SAP, Oracle and ServiceNow all use: an audit trail and the notes people
     * write are read together, because "what happened" and "what someone said about it" answer one question.
     *
     * MOD-0024 emits only `comment` today. `event` is declared because the fixtures demonstrate it and the
     * renderer already handles it, but no provider produces one: there is no lifecycle event log, and deriving a
     * timeline from the timestamps a task happens to carry would silently omit accept/plan/claim/release/inquire.
     */
    const ACTIVITY_KINDS = ['comment', 'event'];

    /*
     * WHICH ACTS an `event` entry can report (WC-1). Mirrors MOD-0024's TaskTransitionCodes value for value.
     *
     * The shell turns each of these into a localized sentence, so the list is what it can NAME — a code outside
     * it renders as a generic "the task changed" line rather than as a raw token on screen.
     *
     * ⚠ Deliberately NOT enforced by validateItems, unlike ACTIVITY_KINDS above. An unknown kind is a broken
     * entry, but an unknown CODE is simply a server that shipped a new transition before this shell learned its
     * word for it — and pushing an error there would DROP the whole work item, taking its title, its actions and
     * everything else off the surface over one row in a feed. That is the failure this contract has already paid
     * for once with `dependencies`.
     */
    const ACTIVITY_EVENT_CODES = [
        'created', 'accepted', 'planned', 'started', 'resumed', 'waiting', 'submittedForReview',
        'reviewCancelled', 'completed', 'cancelled', 'claimed', 'released', 'reassigned', 'returned', 'unknown'
    ];

    /*
     * WHO a work item is waiting on: a TYPED identity, or nothing at all.
     *
     * Three shapes have been in circulation — {id, displayName, isCurrentUser} from the projection, a bare
     * {displayName} from the shell's own writer, and for a while a plain string carrying the REASON. The client
     * reads `.displayName`, so a string rendered as nothing and a name without an id cannot be acted on. `null`
     * is the honest answer when nobody can be resolved; a name with no identity behind it is not.
     */
    const isPersonRef = (value) =>
        value === null || value === undefined
            ? true
            : typeof value === 'object' && typeof value.id === 'string' && value.id.length > 0;
    const CAPABILITIES = [
        'planning', 'execution', 'timeTracking', 'checklist', 'subtasks', 'dependencies',
        'attachments', 'evidence', 'activity', 'processStages', 'businessContext', 'relatedRecords'
    ];
    const VALUE_TYPES = ['text', 'number', 'currency', 'percentage', 'date', 'datetime', 'boolean', 'status', 'person', 'reference', 'link'];
    const DATA_CAPABILITIES = {
        timeTracking: ['timeEntries'],
        checklist: ['checklist'],
        subtasks: ['subtasks'],
        dependencies: ['dependencies'],
        attachments: ['attachments'],
        evidence: ['evidence'],
        activity: ['activity'],
        processStages: ['processStages'],
        businessContext: ['businessContext'],
        relatedRecords: ['relatedRecords']
    };
    const LIMITS = {
        maxSections: 6,
        maxFieldsPerSection: 8,
        maxTextLengthPerField: 2000,
        maxPrimaryFields: 8,
        maxRelatedRecords: 20
    };

    const result = (errors) => ({ valid: errors.length === 0, errors });
    const push = (errors, fixture, code, path) => errors.push({ fixtureId: fixture?.id || 'unknown', code, path: path || null });
    const unique = (values) => Array.isArray(values) && new Set(values).size === values.length;
    const hasValue = (value) => Array.isArray(value) ? value.length > 0
        : value && typeof value === 'object' ? Object.keys(value).length > 0
            : value !== undefined && value !== null && value !== '';
    const isLabel = (label) => !!label && (
        (label.kind === 'resource' && typeof label.key === 'string' && label.key.length > 0 && label.text === undefined) ||
        (label.kind === 'display' && typeof label.text === 'string' && label.text.length > 0 &&
            typeof label.locale === 'string' && label.locale.length > 0 && label.key === undefined)
    );
    const isSafeLink = (value) => {
        if (typeof value !== 'string' || !value.trim()) { return false; }
        if (value.startsWith('/') && !value.startsWith('//')) { return true; }
        try { return new URL(value).protocol === 'https:'; } catch (_) { return false; }
    };
    const enabledInlineActions = (fixture) => (fixture.actions || [])
        .filter((action) => action.enabled && (action.depth || fixture.actionDepth || 'inline') === 'inline');

    /*
     * Is this work finished? EITHER field is enough, because the two can disagree on the wire and "finished" is
     * the stronger claim — the same definition mock-data.js exports and app.js reads, deliberately, so a fixture
     * the contract calls open cannot be one the surface files under History.
     */
    const isTerminalFixture = (fixture) => ['Done', 'Cancelled'].includes(fixture.normalizedStatus)
        || ['Done', 'Cancelled'].includes(fixture.taskLifecycle);

    const validateActionSet = (fixture, errors) => {
        if (!Array.isArray(fixture.actions)) {
            push(errors, fixture, 'ACTIONS_REQUIRED', 'actions');
            return;
        }
        const codes = fixture.actions.map((action) => action.code);
        if (!unique(codes) || codes.some((code) => typeof code !== 'string' || !code)) {
            push(errors, fixture, 'ACTION_CODE_UNIQUE_REQUIRED', 'actions');
        }
        fixture.actions.forEach((action, index) => {
            const path = `actions[${index}]`;
            if (!isLabel(action.label)) { push(errors, fixture, 'ACTION_LABEL_INVALID', `${path}.label`); }
            if (typeof action.enabled !== 'boolean') { push(errors, fixture, 'ACTION_ENABLED_REQUIRED', `${path}.enabled`); }
            if (!action.source) { push(errors, fixture, 'ACTION_SOURCE_REQUIRED', `${path}.source`); }
            if (action.enabled === false && (!action.disabledReasonCode || !isLabel(action.disabledReason))) {
                push(errors, fixture, 'DISABLED_REASON_REQUIRED', path);
            }
            if ('expectedVersion' in action || 'expectedConcurrencyToken' in action || 'requiresConcurrency' in action) {
                push(errors, fixture, 'ACTION_CONCURRENCY_DUPLICATE', path);
            }
        });
        if (enabledInlineActions(fixture).length && (!fixture.concurrency || !fixture.concurrency.kind || !fixture.concurrency.token)) {
            push(errors, fixture, 'CONCURRENCY_REQUIRED_FOR_ENABLED_INLINE_ACTION', 'concurrency');
        }
    };

    const validateBusinessContext = (fixture, errors) => {
        const context = fixture.businessContext;
        if (!context) { return; }
        const sections = Array.isArray(context.sections) ? context.sections : [];
        if (sections.length > LIMITS.maxSections) { push(errors, fixture, 'BUSINESS_CONTEXT_MAX_SECTIONS', 'businessContext.sections'); }
        let primaryCount = 0;
        sections.forEach((section, sectionIndex) => {
            const fields = Array.isArray(section.fields) ? section.fields : [];
            if (fields.length > LIMITS.maxFieldsPerSection) {
                push(errors, fixture, 'BUSINESS_CONTEXT_MAX_FIELDS', `businessContext.sections[${sectionIndex}].fields`);
            }
            fields.forEach((field, fieldIndex) => {
                const path = `businessContext.sections[${sectionIndex}].fields[${fieldIndex}]`;
                if (!VALUE_TYPES.includes(field.valueType)) { push(errors, fixture, 'BUSINESS_CONTEXT_VALUE_TYPE_INVALID', `${path}.valueType`); }
                if (!isLabel(field.label)) { push(errors, fixture, 'BUSINESS_CONTEXT_LABEL_INVALID', `${path}.label`); }
                if (field.importance === 'primary') { primaryCount += 1; }
                if (typeof field.value === 'string' && field.value.length > LIMITS.maxTextLengthPerField) {
                    push(errors, fixture, 'BUSINESS_CONTEXT_TEXT_TOO_LONG', `${path}.value`);
                }
                if (field.valueType === 'link' && field.value && !isSafeLink(field.value)) {
                    push(errors, fixture, 'BUSINESS_CONTEXT_LINK_UNSAFE', `${path}.value`);
                }
                if (field.redacted === true && hasValue(field.value)) {
                    push(errors, fixture, 'REDACTED_VALUE_MUST_BE_OMITTED', `${path}.value`);
                }
            });
        });
        if (primaryCount > LIMITS.maxPrimaryFields) { push(errors, fixture, 'BUSINESS_CONTEXT_MAX_PRIMARY_FIELDS', 'businessContext'); }
    };

    const validateCapabilities = (fixture, errors) => {
        const caps = fixture.workItemCapabilities;
        if (!Array.isArray(caps) || !unique(caps) || caps.some((cap) => !CAPABILITIES.includes(cap))) {
            push(errors, fixture, 'CAPABILITIES_INVALID', 'workItemCapabilities');
            return;
        }
        Object.entries(DATA_CAPABILITIES).forEach(([capability, fields]) => {
            fields.forEach((field) => {
                if (hasValue(fixture[field]) && !caps.includes(capability)) {
                    push(errors, fixture, 'CAPABILITY_REQUIRED_FOR_DATA', field);
                }
                if (caps.includes(capability) && fixture[field] === undefined) {
                    push(errors, fixture, 'CAPABILITY_CONTAINER_REQUIRED', field);
                }
            });
        });
        if ((fixture.timerState === 'running' || fixture.timerState === 'paused') && !caps.includes('timeTracking')) {
            push(errors, fixture, 'TIME_TRACKING_CAPABILITY_REQUIRED', 'timerState');
        }
    };

    const validatePlacement = (fixture, errors) => {
        const codes = new Set((fixture.actions || []).map((action) => action.code));
        const primary = fixture.primaryActionCode;
        const secondary = fixture.secondaryActionCodes || [];
        const overflow = fixture.overflowActionCodes || [];
        if (primary !== null && primary !== undefined && !codes.has(primary)) { push(errors, fixture, 'PRIMARY_ACTION_REFERENCE_INVALID', 'primaryActionCode'); }
        if (!unique(secondary) || secondary.some((code) => !codes.has(code))) { push(errors, fixture, 'SECONDARY_ACTION_REFERENCE_INVALID', 'secondaryActionCodes'); }
        if (!unique(overflow) || overflow.some((code) => !codes.has(code))) { push(errors, fixture, 'OVERFLOW_ACTION_REFERENCE_INVALID', 'overflowActionCodes'); }
        const all = [primary, ...secondary, ...overflow].filter(Boolean);
        if (!unique(all)) { push(errors, fixture, 'ACTION_PLACEMENT_OVERLAP', 'actions'); }
    };

    const validateWorkItem = (fixture) => {
        const errors = [];
        if (!fixture || fixture.fixtureKind !== 'workItem') { push(errors, fixture, 'WORK_ITEM_KIND_REQUIRED', 'fixtureKind'); return result(errors); }
        if (!fixture.id) { push(errors, fixture, 'ID_REQUIRED', 'id'); }
        if (!WORK_INTENTS.includes(fixture.workIntent)) { push(errors, fixture, 'WORK_INTENT_INVALID', 'workIntent'); }
        if (!ASSIGNMENT_MODES.includes(fixture.assignmentMode)) { push(errors, fixture, 'ASSIGNMENT_MODE_INVALID', 'assignmentMode'); }
        if (!OWNERSHIP_STATES.includes(fixture.ownershipState)) { push(errors, fixture, 'OWNERSHIP_STATE_INVALID', 'ownershipState'); }
        if (!ADMISSION_STATES.includes(fixture.admissionState)) { push(errors, fixture, 'ADMISSION_STATE_INVALID', 'admissionState'); }
        if (!NORMALIZED_STATUSES.includes(fixture.normalizedStatus)) { push(errors, fixture, 'NORMALIZED_STATUS_INVALID', 'normalizedStatus'); }
        if (!TASK_LIFECYCLES.includes(fixture.taskLifecycle)) { push(errors, fixture, 'TASK_LIFECYCLE_INVALID', 'taskLifecycle'); }
        if (fixture.workIntent !== 'task' && fixture.taskLifecycle !== 'notApplicable') { push(errors, fixture, 'NON_TASK_LIFECYCLE_NOT_APPLICABLE', 'taskLifecycle'); }
        if (!EXECUTION_STATES.includes(fixture.executionState)) { push(errors, fixture, 'EXECUTION_STATE_INVALID', 'executionState'); }
        if (!TIMER_STATES.includes(fixture.timerState)) { push(errors, fixture, 'TIMER_STATE_INVALID', 'timerState'); }
        if (!SYSTEM_STATES.includes(fixture.systemState)) { push(errors, fixture, 'SYSTEM_STATE_INVALID', 'systemState'); }
        if (!ACTION_DEPTHS.includes(fixture.actionDepth)) { push(errors, fixture, 'ACTION_DEPTH_INVALID', 'actionDepth'); }
        if (!fixture.nativeStatus?.code || !isLabel(fixture.nativeStatus?.label)) { push(errors, fixture, 'NATIVE_STATUS_INVALID', 'nativeStatus'); }
        if (!fixture.source?.providerCode || !fixture.source?.providerContractVersion || !fixture.source?.objectType || !fixture.source?.objectId) {
            push(errors, fixture, 'SOURCE_REQUIRED', 'source');
        }
        if (fixture.actionDepth === 'deeplink' && !isSafeLink(fixture.source?.deepLink)) { push(errors, fixture, 'DEEPLINK_REQUIRED', 'source.deepLink'); }
        if ((fixture.normalizedStatus === 'Waiting') !== !!fixture.waitingContext) { push(errors, fixture, 'WAITING_CONTEXT_BIDIRECTIONAL', 'waitingContext'); }
        // An unknown type is a CONTRACT error, not a rendering quirk: the shell can only translate what it is
        // told about, so a type nobody declared reaches the user as silence.
        if (fixture.waitingContext && !WAITING_CONTEXT_TYPES.includes(fixture.waitingContext.type)) {
            push(errors, fixture, 'WAITING_CONTEXT_TYPE_INVALID', 'waitingContext.type');
        }
        if (fixture.waitingContext && !isPersonRef(fixture.waitingContext.waitingOn)) {
            push(errors, fixture, 'WAITING_CONTEXT_WAITING_ON_INVALID', 'waitingContext.waitingOn');
        }
        if (fixture.personal?.snoozedUntil && fixture.normalizedStatus === 'Waiting' && fixture.waitingContext?.type === 'personalSnooze') {
            push(errors, fixture, 'SNOOZE_MUST_NOT_CREATE_WAITING', 'personal.snoozedUntil');
        }
        /*
         * WHICH queue (WC-3 / BL-031). The Pool tab's entire question is "which queue is this in", so a
         * groupQueue item that cannot answer it makes the tab meaningless — that silence is what the fabricated
         * "Operasyon Kuyruğu" label filled for every pooled item.
         *
         * `label` is OPTIONAL while `id` is not: a position that cannot be read leaves the queue unnamed, which
         * is the only honest third option beside printing a GUID and dropping the task. It is a DISPLAY label —
         * a position name is data someone typed, so a resource key would render as itself.
         *
         * This rule was added only AFTER the provider was verified to emit the field, deliberately: validateItems
         * DROPS an item it cannot validate, so requiring a field the provider does not yet send would have made
         * every pooled task vanish from the Pool tab for as long as the two were out of step.
         */
        if (fixture.assignmentMode === 'groupQueue') {
            if (!fixture.pool || typeof fixture.pool.id !== 'string' || !fixture.pool.id) {
                push(errors, fixture, 'POOL_REQUIRED_FOR_GROUP_QUEUE', 'pool');
            } else if (fixture.pool.label !== undefined && fixture.pool.label !== null
                && !(isLabel(fixture.pool.label) && fixture.pool.label.kind === 'display')) {
                push(errors, fixture, 'POOL_LABEL_INVALID', 'pool.label');
            }
        } else if (fixture.pool !== undefined && fixture.pool !== null) {
            // Work that is not queued has no queue. Saying it belongs to one is inventing a fact.
            push(errors, fixture, 'POOL_ON_NON_QUEUE_ITEM', 'pool');
        }
        // Optional field: a provider that does not rank its work omits it. Present-but-unknown is an error —
        // that is the state the shell used to render as an empty flag chip in an undefined colour.
        if (fixture.priority !== undefined && fixture.priority !== null && !PRIORITIES.includes(fixture.priority)) {
            push(errors, fixture, 'PRIORITY_INVALID', 'priority');
        }
        /*
         * slaState — validated WHEN PRESENT, never required (WC-2).
         *
         * The distinction is load-bearing, and BL-038 is why it is written down. validateItems DROPS an item
         * that fails validation, so a required field is not a nudge to providers — it is a delete. MOD-0023's
         * approval provider and MOD-0024's task provider both emit this today, but a third provider that does
         * not track deadlines must be able to stay silent and still have its work appear on the surface.
         *
         * Present-but-unknown IS an error: that is the state the shell renders as an uncoloured chip under a
         * blank group heading, which teaches the reader nothing and hides the drift that caused it.
         */
        if (fixture.slaState !== undefined && fixture.slaState !== null && !SLA_STATES.includes(fixture.slaState)) {
            push(errors, fixture, 'SLA_STATE_INVALID', 'slaState');
        }
        /*
         * closedAt — WHEN this work finished, and the only thing that lets a finished item's day count stop
         * moving (BL-046).
         *
         * It is declared here rather than left to slide in as an undeclared field, because in this repository an
         * undeclared field is a field that changes meaning without anyone noticing (BL-032). Same shape as
         * slaState: validated when PRESENT, never required — MOD-0023's approval provider has no closing
         * timestamp to give, and requiring one would DELETE its work from the surface rather than nudge it.
         *
         * Two things ARE errors. An unparseable instant, because the client does arithmetic on it and a silent
         * NaN is how "-2 days left" reached a live screen. And a closing instant on OPEN work, because that is
         * not a nuance — it is a contradiction, and the day count would freeze on an item that is still running.
         */
        if (fixture.closedAt !== undefined && fixture.closedAt !== null) {
            if (Number.isNaN(new Date(fixture.closedAt).getTime())) {
                push(errors, fixture, 'CLOSED_AT_INVALID', 'closedAt');
            } else if (!isTerminalFixture(fixture)) {
                push(errors, fixture, 'CLOSED_AT_ON_OPEN_ITEM', 'closedAt');
            }
        }
        /*
         * Activity entries. `at` is ABSOLUTE and a pre-computed "N days ago" is forbidden outright: whoever
         * computes it freezes it, and a projection that sat in a cache or a tab left open overnight then says
         * "today" about yesterday. Same defect class as the frozen showcase date.
         */
        (fixture.activity || []).forEach((entry, index) => {
            const path = `activity[${index}]`;
            if (!ACTIVITY_KINDS.includes(entry.kind)) { push(errors, fixture, 'ACTIVITY_KIND_INVALID', `${path}.kind`); }
            if (!entry.at) { push(errors, fixture, 'ACTIVITY_TIMESTAMP_REQUIRED', `${path}.at`); }
            if (entry.ago !== undefined) { push(errors, fixture, 'ACTIVITY_RELATIVE_TIME_FORBIDDEN', `${path}.ago`); }
            /*
             * A comment says something — EXCEPT a withdrawn one, which is the point of a tombstone (2026-08-14).
             *
             * Comments used to be immutable; the compromise that opened editing and withdrawal was the TRAIL, and
             * a withdrawal's trail is a row with its text GONE and `withdrawnAt` set. The words are cleared at
             * rest, not merely withheld, so requiring text here would reject exactly the shape the feature
             * produces — and a rejected item is DROPPED whole, taking the task's title and actions with it.
             *
             * An entry claiming both is still an error: a tombstone that still carries its sentence is not one.
             */
            if (entry.kind === 'comment' && entry.withdrawnAt && String(entry.text || '').trim()) {
                push(errors, fixture, 'ACTIVITY_WITHDRAWN_TEXT_FORBIDDEN', `${path}.text`);
            }
            if (entry.kind === 'comment' && !entry.withdrawnAt && !String(entry.text || '').trim()) {
                push(errors, fixture, 'ACTIVITY_COMMENT_TEXT_REQUIRED', `${path}.text`);
            }
        });
        (fixture.dependencies || []).forEach((dependency, index) => {
            const path = `dependencies[${index}]`;
            if (!dependency.id) { push(errors, fixture, 'DEPENDENCY_ID_REQUIRED', `${path}.id`); }
            if (!DEPENDENCY_TYPES.includes(dependency.type)) { push(errors, fixture, 'DEPENDENCY_TYPE_INVALID', `${path}.type`); }
            if (!SUBTASK_STATUSES.includes(dependency.state)) { push(errors, fixture, 'DEPENDENCY_STATE_INVALID', `${path}.state`); }
            if (!DEPENDENCY_DIRECTIONS.includes(dependency.direction)) { push(errors, fixture, 'DEPENDENCY_DIRECTION_INVALID', `${path}.direction`); }
        });
        (fixture.subtasks?.items || []).forEach((subtask, index) => {
            if (!SUBTASK_STATUSES.includes(subtask.status)) {
                push(errors, fixture, 'SUBTASK_STATUS_INVALID', `subtasks.items[${index}].status`);
            }
            if (!isPersonRef(subtask.assignee)) {
                push(errors, fixture, 'SUBTASK_ASSIGNEE_INVALID', `subtasks.items[${index}].assignee`);
            }
        });
        if (fixture.reviewMeetingPolicy) {
            const requirement = fixture.reviewMeetingPolicy.requirement;
            const byCode = new Map((fixture.actions || []).map((action) => [action.code, action]));
            const meetingAction = byCode.get('scheduleReviewMeeting');
            if (!REVIEW_MEETING_REQUIREMENTS.includes(requirement)) {
                push(errors, fixture, 'REVIEW_MEETING_REQUIREMENT_INVALID', 'reviewMeetingPolicy.requirement');
            }
            if (requirement !== 'notAllowed' && !meetingAction) {
                push(errors, fixture, 'REVIEW_MEETING_ACTION_REQUIRED', 'actions');
            }
            if (requirement === 'required' && !fixture.reviewMeetingPolicy.meetingId) {
                const decision = byCode.get('approve') || byCode.get('signoff');
                if (!decision || decision.enabled || decision.disabledReasonCode !== 'REVIEW_MEETING_REQUIRED') {
                    push(errors, fixture, 'REVIEW_MEETING_REQUIRED_MUST_BLOCK_DECISION', 'actions');
                }
            }
        }
        if (fixture.taskLifecycle === 'Done' && fixture.executionState === 'active') { push(errors, fixture, 'TERMINAL_EXECUTION_ACTIVE', 'executionState'); }
        if (['Done', 'Cancelled'].includes(fixture.normalizedStatus) && enabledInlineActions(fixture).length) {
            push(errors, fixture, 'TERMINAL_STATE_CHANGING_ACTION', 'actions');
        }
        validateActionSet(fixture, errors);
        validateCapabilities(fixture, errors);
        validateBusinessContext(fixture, errors);
        validatePlacement(fixture, errors);
        if ((fixture.relatedRecords || []).length > LIMITS.maxRelatedRecords) { push(errors, fixture, 'RELATED_RECORDS_MAX', 'relatedRecords'); }
        /*
         * blockedState { blocked, affectedActionCodes[], blockers[] }. The SHAPE is declared here because it was
         * not: the shell read `reasonKey` and `blockedBy`, nothing in the contract said either existed, and a
         * contract-shaped blockedState therefore rendered a banner with no sentence and no blockers in it.
         *
         * Each blocker may additionally name WHICH task, WHICH edge type and WHICH action it stops. Those three
         * are optional so a blocker that is not a dependency (a checklist item, later a subtask — BL-035) fits the
         * same shape, and so the enterprise-strategy example still validates unchanged.
         */
        if (fixture.blockedState) {
            const blocked = fixture.blockedState;
            if (typeof blocked.blocked !== 'boolean') { push(errors, fixture, 'BLOCKED_STATE_FLAG_REQUIRED', 'blockedState.blocked'); }
            if (!Array.isArray(blocked.affectedActionCodes) || !Array.isArray(blocked.blockers)) {
                push(errors, fixture, 'BLOCKED_STATE_SHAPE_INVALID', 'blockedState');
            }
            if (blocked.blocked && !(blocked.blockers || []).length) {
                // "Blocked, but nothing is blocking it" is the invented-data failure in banner form.
                push(errors, fixture, 'BLOCKED_STATE_BLOCKER_REQUIRED', 'blockedState.blockers');
            }
            /*
             * A blocker's CODE is deliberately NOT a closed vocabulary. The Task Center aggregates other modules'
             * work, and each provider names its own obstacles (DEPENDENCY_BLOCKED and SUBTASK_BLOCKED from
             * MOD-0024, VALIDATION_BLOCKED from enterprise-strategy); a fixed list here would reject a provider
             * for having a reason we had not thought of. What IS required is that the code be a real string and
             * the label a real label, so the banner can always name the thing in the way.
             */
            (blocked.blockers || []).forEach((blocker, index) => {
                const path = `blockedState.blockers[${index}]`;
                if (!blocker.code || !isLabel(blocker.label)) { push(errors, fixture, 'BLOCKER_INVALID', path); }
                // null means "not an edge" — an open subtask is a blocker with no dependency type at all, and
                // the wire may carry that either as an absent field or an explicit null.
                if (blocker.dependencyType !== undefined && blocker.dependencyType !== null
                    && !DEPENDENCY_TYPES.includes(blocker.dependencyType)) {
                    push(errors, fixture, 'BLOCKER_DEPENDENCY_TYPE_INVALID', `${path}.dependencyType`);
                }
                if (blocker.affectedActionCode !== undefined
                    && !(blocked.affectedActionCodes || []).includes(blocker.affectedActionCode)) {
                    push(errors, fixture, 'BLOCKER_ACTION_REFERENCE_INVALID', `${path}.affectedActionCode`);
                }
            });
        }
        if (fixture.blockedState?.affectedActionCodes) {
            const byCode = new Map((fixture.actions || []).map((action) => [action.code, action]));
            fixture.blockedState.affectedActionCodes.forEach((code) => {
                const action = byCode.get(code);
                if (!action || action.enabled || !action.disabledReasonCode || !isLabel(action.disabledReason)) {
                    push(errors, fixture, 'BLOCKER_ACTION_REFERENCE_INVALID', 'blockedState.affectedActionCodes');
                }
            });
        }
        return result(errors);
    };

    const validateTrigger = (fixture) => {
        const errors = [];
        if (!fixture || fixture.fixtureKind !== 'triggerOnly') { push(errors, fixture, 'TRIGGER_KIND_REQUIRED', 'fixtureKind'); return result(errors); }
        if (!fixture.id || !fixture.triggerType || !fixture.source || !SYSTEM_STATES.includes(fixture.systemState)) {
            push(errors, fixture, 'TRIGGER_REQUIRED_FIELDS', null);
        }
        const forbidden = ['assignmentMode', 'ownershipState', 'admissionState', 'taskLifecycle', 'executionState', 'timerState', 'waitingContext', 'workItemCapabilities'];
        forbidden.forEach((field) => { if (field in fixture) { push(errors, fixture, 'TRIGGER_WORK_ITEM_FIELD_FORBIDDEN', field); } });
        validateActionSet(fixture, errors);
        return result(errors);
    };

    const validateCatalog = (groups) => {
        const errors = [];
        const seen = new Set();
        Object.values(groups || {}).flat().forEach((fixture) => {
            if (seen.has(fixture.id)) { push(errors, fixture, 'FIXTURE_ID_DUPLICATE', 'id'); }
            seen.add(fixture.id);
            const validation = fixture.fixtureKind === 'triggerOnly' ? validateTrigger(fixture) : validateWorkItem(fixture);
            errors.push(...validation.errors);
        });
        return result(errors);
    };

    global.WorkCenterNextContract = {
        enums: { WORK_INTENTS, ASSIGNMENT_MODES, OWNERSHIP_STATES, ADMISSION_STATES, NORMALIZED_STATUSES, TASK_LIFECYCLES, EXECUTION_STATES, TIMER_STATES, SYSTEM_STATES, ACTION_DEPTHS, REVIEW_MEETING_REQUIREMENTS, WAITING_CONTEXT_TYPES, SUBTASK_STATUSES, PRIORITIES, SLA_STATES, DEPENDENCY_TYPES, DEPENDENCY_DIRECTIONS, ACTIVITY_KINDS, ACTIVITY_EVENT_CODES, CAPABILITIES, VALUE_TYPES },
        limits: LIMITS,
        isLabel,
        isSafeLink,
        validateWorkItem,
        validateTrigger,
        validateCatalog
    };
})(typeof window !== 'undefined' ? window : globalThis);
