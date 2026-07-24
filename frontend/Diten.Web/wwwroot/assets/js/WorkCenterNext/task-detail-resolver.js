'use strict';

(function (global) {
    const BANNER_PRECEDENCE = [
        'authorityEnded', 'sourceUnavailable', 'reconciliationRequired', 'stale',
        'claimedByOther', 'hardBlocked', 'overdue'
    ];
    const CAPABILITY_BLOCK = {
        planning: 'planning',
        execution: 'execution',
        timeTracking: 'timeTracking',
        checklist: 'checklist',
        subtasks: 'subtasks',
        dependencies: 'dependencies',
        attachments: 'attachments',
        evidence: 'evidence',
        activity: 'activity',
        processStages: 'processStages',
        businessContext: 'businessContext',
        relatedRecords: 'relatedRecords'
    };

    const bannerCandidates = (fixture) => {
        const candidates = [];
        if (fixture.systemState && fixture.systemState !== 'fresh' && fixture.systemState !== 'processing') {
            candidates.push(fixture.systemState);
        }
        if (fixture.claimedByOther) { candidates.push('claimedByOther'); }
        if (fixture.blockedState?.blocked) { candidates.push('hardBlocked'); }
        if (fixture.slaState === 'overdue') { candidates.push('overdue'); }
        return candidates;
    };
    const pickBanner = (fixture) => {
        const candidates = bannerCandidates(fixture);
        return BANNER_PRECEDENCE.find((code) => candidates.includes(code)) || null;
    };
    const surfaceMode = (fixture, readOnly) => {
        if (fixture.actionDepth === 'deeplink') { return 'deeplink'; }
        if (['stale', 'sourceUnavailable', 'reconciliationRequired'].includes(fixture.systemState)) { return 'recovery'; }
        if (readOnly) { return 'readonly'; }
        if (fixture.admissionState === 'pendingAcceptance') { return 'acceptance'; }
        if (fixture.admissionState === 'pendingClaim') { return 'claim'; }
        if (fixture.admissionState === 'pendingOffer') { return 'offer'; }
        if (fixture.workIntent === 'approval') { return 'decision'; }
        if (fixture.workIntent === 'review') { return 'review'; }
        if (fixture.workIntent === 'issue' || fixture.workIntent === 'exception') { return 'investigation'; }
        return 'execution';
    };
    const resolveNotices = (fixture, selectedBanner) => {
        const notices = [];
        if (fixture.waitingContext) { notices.push({ code: 'waiting', labelKey: 'NoticeWaiting' }); }
        if (fixture.taskLifecycle === 'PendingReview') { notices.push({ code: 'pendingReview', labelKey: 'NoticePendingReview' }); }
        if (fixture.personal?.snoozedUntil) { notices.push({ code: 'snoozed', labelKey: 'NoticeSnoozed' }); }
        if (fixture.slaState === 'due-soon') { notices.push({ code: 'dueSoon', labelKey: 'NoticeDueSoon' }); }
        if (fixture.planConflict) { notices.push({ code: 'planConflict', labelKey: 'NoticePlanConflict' }); }
        if (fixture.checklist?.nonBlockingIssue) { notices.push({ code: 'checklistNonBlocking', labelKey: 'NoticeChecklistNonBlocking' }); }
        if (fixture.migrationNotice) { notices.push({ code: 'migration', labelKey: 'MigrationAdaptedNotice' }); }
        return notices.filter((notice) => notice.code !== selectedBanner);
    };
    const resolveTaskDetailSurface = (fixture, interactionState) => {
        const validation = global.WorkCenterNextContract?.validateWorkItem(fixture);
        if (!validation?.valid) {
            return {
                invalid: true,
                validationErrors: validation?.errors || [{ fixtureId: fixture?.id || 'unknown', code: 'VALIDATOR_UNAVAILABLE' }],
                surfaceMode: 'readonly',
                readOnly: true,
                primaryActionCode: null,
                secondaryActionCodes: [],
                overflowActionCodes: [],
                visibleBlocks: [],
                notices: [],
                criticalBanner: { code: 'fixtureInvalid', labelKey: 'FixtureInvalidTitle' },
                personalActions: [],
                sourceNavigation: null
            };
        }
        const terminal = ['Done', 'Cancelled'].includes(fixture.normalizedStatus);
        const safetyReadOnly = ['authorityEnded', 'sourceUnavailable', 'stale', 'reconciliationRequired'].includes(fixture.systemState);
        const commandFree = fixture.actions.length === 0;
        const readOnly = terminal || safetyReadOnly || fixture.actionDepth === 'deeplink' || commandFree;
        const criticalCode = pickBanner(fixture);
        const submittingActionCode = interactionState?.submittingActionCode || null;
        const visibleBlocks = (fixture.workItemCapabilities || []).map((capability) => CAPABILITY_BLOCK[capability]).filter(Boolean);
        if (!visibleBlocks.includes('overview')) { visibleBlocks.unshift('overview'); }
        visibleBlocks.push('personal', 'moreDetails');
        return {
            invalid: false,
            surfaceMode: surfaceMode(fixture, readOnly),
            readOnly,
            primaryActionCode: fixture.primaryActionCode ?? null,
            secondaryActionCodes: (fixture.secondaryActionCodes || []).slice(),
            overflowActionCodes: (fixture.overflowActionCodes || []).slice(),
            visibleBlocks: Array.from(new Set(visibleBlocks)),
            notices: resolveNotices(fixture, criticalCode),
            criticalBanner: criticalCode ? { code: criticalCode, labelKey: `Banner${criticalCode.charAt(0).toUpperCase()}${criticalCode.slice(1)}` } : null,
            personalActions: terminal ? [] : ['pin', 'snooze', 'note'],
            sourceNavigation: fixture.source?.deepLink ? {
                label: { kind: 'resource', key: 'OpenInSource', args: {} },
                deepLink: fixture.source.deepLink
            } : null,
            submittingActionCode,
            interactionLocked: !!submittingActionCode
        };
    };

    global.WorkCenterNextTaskDetailResolver = {
        BANNER_PRECEDENCE,
        resolveTaskDetailSurface
    };
})(typeof window !== 'undefined' ? window : globalThis);
