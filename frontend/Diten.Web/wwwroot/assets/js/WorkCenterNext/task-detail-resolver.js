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
    /*
     * Why the work is parked, by TYPE. The vocabulary is the projection's (TaskWaitingTypes on the server); the
     * shell never guesses a reason it was not told.
     */
    const WAITING_NOTICE_KEY = {
        // Declared and validated by the executable contract (WAITING_CONTEXT_TYPES) — this map must cover
        // every value in it, and nothing else.
        externalInformation: 'NoticeWaitingExternal',
        approval: 'NoticeWaitingApproval',
        review: 'NoticeWaitingReview',
        // Fixture-only today: no provider emits it, but the contract already models meetings
        // (reviewMeetingPolicy / scheduleReviewMeeting), so it is a designed state awaiting its provider.
        meeting: 'NoticeWaitingMeeting'
    };
    const reportedUnknownWaitingTypes = new Set();

    const resolveNotices = (fixture, selectedBanner) => {
        const notices = [];
        /*
         * The notice must name the RIGHT wait. One key was used for every waitingContext, so a task waiting on an
         * APPROVAL was told it was waiting on external input — while the gates card on the same page said
         * "Approval: waiting for a decision". The page contradicted itself.
         *
         * An unmapped type prints nothing and says so: inventing a reason is worse than staying silent about one.
         */
        if (fixture.waitingContext) {
            const labelKey = WAITING_NOTICE_KEY[fixture.waitingContext.type];
            if (labelKey) {
                notices.push({ code: 'waiting', labelKey });
            } else if (!reportedUnknownWaitingTypes.has(fixture.waitingContext.type)) {
                reportedUnknownWaitingTypes.add(fixture.waitingContext.type);
                console.warn(
                    `[WorkCenterNext] No notice text for waitingContext.type "${fixture.waitingContext.type}" — `
                    + 'nothing is shown. Add it to WAITING_NOTICE_KEY and the 7 WorkCenterNext resx files.');
            }
        }
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
                personalActions: []
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
            /*
             * ⚠ NO `sourceNavigation` HERE — BL-309, and it is deliberate rather than forgotten.
             *
             * This used to return `{ label: { kind: 'resource', key: 'OpenInSource' }, deepLink }` for every
             * fixture carrying `source.deepLink`, and MEASURED: no surface in the repo read it. Source
             * navigation is already rendered, three times over, and none of it came through here:
             *   · the Source card's button        — `DetailOpenSource`     (app.js, `data-wcn-open`)
             *   · the inbox row's button          — `DetailOpenSource`     (app.js)
             *   · the `deeplink` lead link        — `ActionCompleteInSource` (action rail + mobile bar)
             * All three are in the seven resx files; `OpenInSource` never was, because nothing drew it.
             *
             * So this was a SECOND model of a decision the shell already makes — and a narrower one: the render
             * sites accept `item.source?.deepLink || item.deepLink || item.sourceDeepLink`, because the
             * presentation mapper flattens the link, while this only ever looked at `source.deepLink`. Reviving
             * it means wiring a surface to it AND adding `OpenInSource` to all seven resx.
             *
             * What the resolver still owns is the DECISION: `surfaceMode === 'deeplink'` is what the action rail
             * asks before it turns its primary into a link. One model, consumed.
             */
            submittingActionCode,
            interactionLocked: !!submittingActionCode
        };
    };

    global.WorkCenterNextTaskDetailResolver = {
        BANNER_PRECEDENCE,
        resolveTaskDetailSurface
    };
})(typeof window !== 'undefined' ? window : globalThis);
