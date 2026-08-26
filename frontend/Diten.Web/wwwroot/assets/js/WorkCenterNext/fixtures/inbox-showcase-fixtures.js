'use strict';

(function (global) {
    const f = global.WorkCenterNextFixtureFactory;
    if (!f) { throw new Error('WorkCenterNextFixtureFactory is required.'); }
    const { resource, action, disabledAction, source, personal, base } = f;

    const inbox = (id, intent, titleKey, overrides) => base(id, intent, titleKey, Object.assign({
        ownershipState: 'assigned',
        admissionState: intent === 'approval' || intent === 'review' ? 'admitted' : 'pendingAcceptance',
        normalizedStatus: 'Pending',
        nativeStatus: { code: 'INBOX_ATTENTION', label: resource('StatusPending') },
        executionState: intent === 'task' ? 'notStarted' : 'notApplicable',
        timerState: intent === 'task' ? 'inactive' : 'notApplicable',
        personal: personal({ seen: false }),
        title: resource(titleKey),
        summary: resource(`${titleKey}Summary`)
    }, overrides || {}));

    const fixtures = [
        inbox('INBOX-TASK-01', 'task', 'InboxTitleTaskVendorVerification', {
            assignmentMode: 'direct',
            workItemCapabilities: ['planning', 'execution', 'activity', 'businessContext', 'relatedRecords'],
            source: source('master-data', 'VendorVerificationTask', 'VEND-58821'),
            concurrency: { kind: 'version', token: '101' },
            actions: [action('accept'), action('plan'), action('dispute', { requiresReason: true }), action('reassign', { requiresReason: true })],
            primaryActionCode: 'accept',
            secondaryActionCodes: ['plan'],
            overflowActionCodes: ['dispute', 'reassign'],
            dueAt: '2026-07-26'
        }),
        inbox('INBOX-APPROVAL-01', 'approval', 'InboxTitleApprovalBudgetRevision', {
            assignmentMode: 'approval',
            viewerRole: 'Approver',
            source: source('finance', 'BudgetRevisionApproval', 'BUD-2026-Q3-17'),
            concurrency: { kind: 'etag', token: 'budget-17' },
            actions: [
                action('approve', { requiresConfirmation: true }),
                action('scheduleReviewMeeting', { label: resource('ActReviewMeeting'), input: 'meeting' }),
                action('requestInfo', { requiresReason: true }),
                action('return', { requiresReason: true }),
                action('delegate', { requiresReason: true }),
                action('reject', { requiresReason: true })
            ],
            primaryActionCode: 'approve',
            secondaryActionCodes: ['scheduleReviewMeeting'],
            overflowActionCodes: ['requestInfo', 'return', 'delegate', 'reject'],
            dueAt: '2026-07-25'
        }),
        inbox('INBOX-REVIEW-OPTIONAL-MEETING', 'review', 'InboxTitleReviewOptionalMeeting', {
            assignmentMode: 'approval',
            viewerRole: 'Reviewer',
            source: source('quality', 'TaskCompletionReview', 'CAPA-2231-R1'),
            concurrency: { kind: 'version', token: 'review-optional-3' },
            reviewMeetingPolicy: { requirement: 'optional', meetingId: null, scheduledAt: null },
            actions: [
                action('signoff', { requiresConfirmation: true }),
                action('scheduleReviewMeeting', { label: resource('ActReviewMeeting'), input: 'meeting' }),
                action('requestInfo', { requiresReason: true }),
                action('return', { requiresReason: true })
            ],
            primaryActionCode: 'signoff',
            secondaryActionCodes: ['scheduleReviewMeeting'],
            overflowActionCodes: ['requestInfo', 'return'],
            dueAt: '2026-07-27'
        }),
        inbox('INBOX-REVIEW-REQUIRED-MEETING', 'review', 'InboxTitleReviewRequiredMeeting', {
            assignmentMode: 'approval',
            viewerRole: 'Reviewer',
            source: source('project-governance', 'TaskCompletionReview', 'PRJ-104-R2'),
            concurrency: { kind: 'version', token: 'review-required-8' },
            reviewMeetingPolicy: { requirement: 'required', meetingId: null, scheduledAt: null },
            escalated: true,
            actions: [
                disabledAction('signoff', 'REVIEW_MEETING_REQUIRED', 'ActionDisabledReviewMeetingRequired', { requiresConfirmation: true }),
                action('scheduleReviewMeeting', { label: resource('ActReviewMeeting'), input: 'meeting' }),
                action('requestInfo', { requiresReason: true }),
                action('return', { requiresReason: true })
            ],
            primaryActionCode: 'scheduleReviewMeeting',
            secondaryActionCodes: [],
            overflowActionCodes: ['signoff', 'requestInfo', 'return'],
            dueAt: '2026-07-24'
        }),
        inbox('INBOX-ISSUE-01', 'issue', 'InboxTitleIssueIntegrationFailure', {
            assignmentMode: 'direct',
            admissionState: 'admitted',   // act-directly: no accept gate — resolve/inquire on the spot
            source: source('integration-monitoring', 'IntegrationIssue', 'INT-4471'),
            concurrency: { kind: 'version', token: 'issue-7' },
            actions: [action('resolve', { requiresConfirmation: true }), action('requestInfo', { requiresReason: true }), action('reassign', { requiresReason: true })],
            primaryActionCode: 'resolve',
            secondaryActionCodes: ['requestInfo'],
            overflowActionCodes: ['reassign'],
            dueAt: '2026-07-25'
        }),
        inbox('INBOX-EXCEPTION-01', 'exception', 'InboxTitleExceptionVatMismatch', {
            assignmentMode: 'direct',
            admissionState: 'admitted',   // act-directly: no accept gate — resolve/inquire on the spot
            source: source('tax', 'VatReconciliationException', 'VAT-7781'),
            concurrency: { kind: 'version', token: 'exception-4' },
            actions: [action('resolve', { requiresConfirmation: true }), action('requestInfo', { requiresReason: true }), action('reassign', { requiresReason: true })],
            primaryActionCode: 'resolve',
            secondaryActionCodes: ['requestInfo'],
            overflowActionCodes: ['reassign'],
            dueAt: '2026-07-23'
        })
    ];

    global.WorkCenterNextFixtures = global.WorkCenterNextFixtures || {};
    global.WorkCenterNextFixtures.inboxShowcase = fixtures;
})(typeof window !== 'undefined' ? window : globalThis);
