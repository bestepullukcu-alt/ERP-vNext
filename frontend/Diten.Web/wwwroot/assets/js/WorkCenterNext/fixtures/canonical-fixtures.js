'use strict';

(function (global) {
    const resource = (key, args) => ({ kind: 'resource', key, args: args || {} });
    const actionLabelKey = (code) => code === 'signoff'
        ? 'ActSignOff'
        : `Act${code.charAt(0).toUpperCase()}${code.slice(1)}`;
    const action = (code, options) => Object.assign({
        code,
        label: resource(actionLabelKey(code)),
        semanticType: code,
        enabled: true,
        source: 'provider',
        requiresConfirmation: false,
        requiresReason: false,
        requiresEvidence: false,
        supportsBulk: false,
        riskLevel: 'normal'
    }, options || {});
    const disabledAction = (code, reasonCode, reasonKey, options) => action(code, Object.assign({
        enabled: false,
        disabledReasonCode: reasonCode,
        disabledReason: resource(reasonKey)
    }, options || {}));
    const source = (providerCode, objectType, objectId, extra) => Object.assign({
        providerCode,
        providerContractVersion: '1.0',
        sourceSystem: providerCode,
        objectType,
        objectId,
        deepLink: `/WorkCenterNext?source=${encodeURIComponent(providerCode)}&id=${encodeURIComponent(objectId)}`
    }, extra || {});
    const personal = (extra) => Object.assign({
        pinned: false,
        snoozedUntil: null,
        seen: true,
        plannedDate: null,
        reminderAt: null,
        note: null
    }, extra || {});
    const base = (id, intent, titleKey, overrides) => Object.assign({
        fixtureKind: 'workItem',
        id,
        workIntent: intent,
        title: resource(`Type${intent.charAt(0).toUpperCase()}${intent.slice(1)}`),
        summary: resource('DetailSummary'),
        assignmentMode: 'direct',
        ownershipState: 'owned',
        admissionState: 'admitted',
        normalizedStatus: 'InProgress',
        taskLifecycle: intent === 'task' ? 'Open' : 'notApplicable',
        nativeStatus: { code: 'OPEN', label: resource('StatusInProgress') },
        executionState: intent === 'task' ? 'notStarted' : 'notApplicable',
        timerState: intent === 'task' ? 'inactive' : 'notApplicable',
        systemState: 'fresh',
        viewerRole: intent === 'approval' ? 'Approver' : intent === 'review' ? 'Reviewer' : 'Owner',
        delegationContext: null,
        workItemCapabilities: ['activity', 'businessContext', 'relatedRecords'],
        activity: [],
        businessContext: { sections: [] },
        relatedRecords: [],
        actionDepth: 'inline',
        blockedState: null,
        actions: [],
        concurrency: null,
        lifecycleOwner: null,
        personal: personal(),
        relatedWorkItems: [],
        priority: 'medium',
        requester: { id: 'USR-REQ', displayName: 'Deniz Koç' },
        assignee: { id: 'USR-OWN', displayName: 'Selin Aras' },
        dueAt: '2026-07-27',
        source: source('workcenter', 'WorkItem', id),
        primaryActionCode: null,
        secondaryActionCodes: [],
        overflowActionCodes: [],
        expectation: { readOnly: false }
    }, overrides || {});

    const fixtures = [
        base('WC-TASK-ACCEPT', 'task', 'FixtureTitleTaskAccept', {
            assignmentMode: 'direct',
            ownershipState: 'assigned',
            admissionState: 'pendingAcceptance',
            normalizedStatus: 'Pending',
            taskLifecycle: 'Open',
            nativeStatus: { code: 'ASSIGNED', label: resource('StatusPending') },
            workItemCapabilities: ['planning', 'execution', 'activity', 'businessContext', 'relatedRecords'],
            personal: personal({ seen: false }),
            concurrency: { kind: 'version', token: '11' },
            actions: [action('accept'), action('plan'), action('reassign', { requiresReason: true })],
            primaryActionCode: 'accept',
            secondaryActionCodes: ['plan'],
            overflowActionCodes: ['reassign'],
            expectation: { surfaceMode: 'acceptance', readOnly: false, primaryActionCode: 'accept' }
        }),
        base('WC-TASK-PLANNED', 'task', 'FixtureTitleTaskPlanned', {
            normalizedStatus: 'InProgress',
            taskLifecycle: 'Planned',
            nativeStatus: { code: 'PLANNED', label: resource('LifecyclePlanned') },
            workItemCapabilities: ['planning', 'execution', 'checklist', 'activity', 'businessContext', 'relatedRecords'],
            checklist: { items: [] },
            personal: personal({ plannedDate: '2026-07-29' }),
            concurrency: { kind: 'version', token: '12' },
            actions: [action('start'), action('replan')],
            primaryActionCode: 'start',
            secondaryActionCodes: ['replan'],
            expectation: { surfaceMode: 'execution', readOnly: false, primaryActionCode: 'start' }
        }),
        base('WC-TASK-ACTIVE-NO-TIMER', 'task', 'FixtureTitleTaskActive', {
            taskLifecycle: 'InProgress',
            executionState: 'active',
            timerState: 'inactive',
            nativeStatus: { code: 'IN_PROGRESS', label: resource('StatusInProgress') },
            workItemCapabilities: ['execution', 'timeTracking', 'checklist', 'subtasks', 'dependencies', 'attachments', 'activity', 'businessContext', 'relatedRecords'],
            timeEntries: [],
            checklist: { items: [{ id: 'C1', label: resource('FixtureChecklistVerify'), completed: true, required: true }] },
            subtasks: { mode: 'full', items: [] },
            dependencies: [],
            attachments: [],
            concurrency: { kind: 'version', token: '13' },
            actions: [action('pause'), action('complete', { requiresConfirmation: true })],
            primaryActionCode: 'complete',
            secondaryActionCodes: ['pause'],
            expectation: { surfaceMode: 'execution', readOnly: false, primaryActionCode: 'complete' }
        }),
        base('WC-TASK-WAITING', 'task', 'FixtureTitleTaskWaiting', {
            normalizedStatus: 'Waiting',
            taskLifecycle: 'Waiting',
            executionState: 'paused',
            timerState: 'inactive',
            nativeStatus: { code: 'WAITING_INFORMATION', label: resource('StatusWaiting') },
            waitingContext: {
                type: 'information',
                waitingOn: { id: 'USR-103', displayName: 'Deniz Koç' },
                since: '2026-07-24T10:30:00+03:00',
                expectedUntil: null
            },
            workItemCapabilities: ['execution', 'activity', 'businessContext', 'relatedRecords'],
            concurrency: { kind: 'version', token: '14' },
            actions: [action('resume'), action('reassign', { requiresReason: true })],
            primaryActionCode: 'resume',
            overflowActionCodes: ['reassign'],
            expectation: { surfaceMode: 'execution', readOnly: false, primaryActionCode: 'resume', noticeCodes: ['waiting'] }
        }),
        base('WC-TASK-BLOCKED', 'task', 'FixtureTitleTaskBlocked', {
            workItemCapabilities: ['execution', 'dependencies', 'activity', 'businessContext', 'relatedRecords'],
            dependencies: [{ id: 'DEP-1', title: resource('FixtureDependencyContract'), type: 'FS', state: 'inProgress', blocking: true }],
            actions: [disabledAction('start', 'DEPENDENCY_BLOCKED', 'ActionDisabledDependencyBlocked')],
            concurrency: null,
            blockedState: {
                blocked: true,
                affectedActionCodes: ['start'],
                blockers: [{ code: 'DEPENDENCY_BLOCKED', label: resource('FixtureDependencyContract') }]
            },
            primaryActionCode: 'start',
            expectation: { surfaceMode: 'execution', readOnly: false, primaryActionCode: 'start', criticalBannerCode: 'hardBlocked' }
        }),
        base('WC-APPROVAL-SIMPLE', 'approval', 'FixtureTitleApproval', {
            assignmentMode: 'approval',
            ownershipState: 'assigned',
            admissionState: 'admitted',
            normalizedStatus: 'Pending',
            nativeStatus: { code: 'PENDING_APPROVAL', label: resource('StatusPending') },
            source: source('workflow', 'ApprovalInstance', 'APR-104'),
            lifecycleOwner: { providerCode: 'workflow' },
            personal: personal({ seen: false }),
            concurrency: { kind: 'etag', token: 'approval-18' },
            actions: [action('approve', { requiresConfirmation: true }), action('reject', { requiresReason: true }), action('requestInfo', { requiresReason: true })],
            primaryActionCode: 'approve',
            secondaryActionCodes: ['requestInfo'],
            overflowActionCodes: ['reject'],
            expectation: { surfaceMode: 'decision', readOnly: false, primaryActionCode: 'approve' }
        }),
        base('WC-REVIEW-EVIDENCE', 'review', 'FixtureTitleReview', {
            normalizedStatus: 'Pending',
            nativeStatus: { code: 'REVIEW_REQUESTED', label: resource('StatusPending') },
            workItemCapabilities: ['evidence', 'activity', 'processStages', 'businessContext', 'relatedRecords'],
            evidence: { required: true, complete: false, items: [] },
            processStages: [],
            concurrency: { kind: 'version', token: '21' },
            actions: [disabledAction('signoff', 'EVIDENCE_INCOMPLETE', 'ActionDisabledEvidenceIncomplete', { requiresEvidence: true }), action('return', { requiresReason: true })],
            blockedState: { blocked: true, affectedActionCodes: ['signoff'], blockers: [{ code: 'EVIDENCE_INCOMPLETE', label: resource('EvidenceMissing') }] },
            primaryActionCode: 'signoff',
            overflowActionCodes: ['return'],
            expectation: { surfaceMode: 'review', readOnly: false, primaryActionCode: 'signoff', criticalBannerCode: 'hardBlocked' }
        }),
        base('WC-ISSUE-ACTIVE', 'issue', 'FixtureTitleIssue', {
            nativeStatus: { code: 'INVESTIGATING', label: resource('StatusInProgress') },
            concurrency: { kind: 'version', token: '31' },
            actions: [action('resolve', { requiresConfirmation: true }), action('reassign', { requiresReason: true })],
            primaryActionCode: 'resolve',
            overflowActionCodes: ['reassign'],
            expectation: { surfaceMode: 'investigation', readOnly: false, primaryActionCode: 'resolve' }
        }),
        base('WC-TASK-DONE', 'task', 'FixtureTitleTaskDone', {
            normalizedStatus: 'Done',
            taskLifecycle: 'Done',
            executionState: 'notApplicable',
            timerState: 'notApplicable',
            nativeStatus: { code: 'COMPLETED', label: resource('StatusDone') },
            actions: [],
            source: source('workcenter', 'Task', 'TASK-DONE', { deepLink: '/WorkCenterNext?source=workcenter&id=TASK-DONE' }),
            expectation: { surfaceMode: 'readonly', readOnly: true, primaryActionCode: null }
        }),
        base('WC-TASK-SNOOZED', 'task', 'FixtureTitleTaskSnoozed', {
            normalizedStatus: 'InProgress',
            taskLifecycle: 'InProgress',
            executionState: 'active',
            nativeStatus: { code: 'IN_PROGRESS', label: resource('StatusInProgress') },
            personal: personal({ snoozedUntil: '2026-07-30' }),
            workItemCapabilities: ['execution', 'activity', 'businessContext', 'relatedRecords'],
            concurrency: { kind: 'version', token: '41' },
            actions: [action('complete', { requiresConfirmation: true })],
            primaryActionCode: 'complete',
            expectation: { surfaceMode: 'execution', readOnly: false, primaryActionCode: 'complete', noticeCodes: ['snoozed'] }
        })
    ];

    global.WorkCenterNextFixtureFactory = { resource, action, disabledAction, source, personal, base };
    global.WorkCenterNextFixtures = global.WorkCenterNextFixtures || {};
    global.WorkCenterNextFixtures.canonical = fixtures;
})(typeof window !== 'undefined' ? window : globalThis);
