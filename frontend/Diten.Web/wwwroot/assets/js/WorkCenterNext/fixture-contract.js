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
        if (fixture.personal?.snoozedUntil && fixture.normalizedStatus === 'Waiting' && fixture.waitingContext?.type === 'personalSnooze') {
            push(errors, fixture, 'SNOOZE_MUST_NOT_CREATE_WAITING', 'personal.snoozedUntil');
        }
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
        enums: { WORK_INTENTS, ASSIGNMENT_MODES, OWNERSHIP_STATES, ADMISSION_STATES, NORMALIZED_STATUSES, TASK_LIFECYCLES, EXECUTION_STATES, TIMER_STATES, SYSTEM_STATES, ACTION_DEPTHS, REVIEW_MEETING_REQUIREMENTS, CAPABILITIES, VALUE_TYPES },
        limits: LIMITS,
        isLabel,
        isSafeLink,
        validateWorkItem,
        validateTrigger,
        validateCatalog
    };
})(typeof window !== 'undefined' ? window : globalThis);
