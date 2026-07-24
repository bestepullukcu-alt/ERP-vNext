'use strict';

// İşlerim (My Work) showcase — real, varied in-progress work the viewer has taken on.
// Mirrors the canonical WC-TASK-* variants but with real content + variety (module,
// SLA, priority, requester, type) so İşlerim reads as an actual work list, not 5
// identical placeholders. Segments derive from status/lifecycle (Aktif/Bekleyen/Planlı).
(function (global) {
    const f = global.WorkCenterNextFixtureFactory;
    if (!f) { throw new Error('WorkCenterNextFixtureFactory is required.'); }
    const { resource, action, disabledAction, source, personal, base } = f;

    const req = (id, name) => ({ id, displayName: name });
    // base() ignores the titleKey arg (defaults title to the type label). Set the real
    // title/summary explicitly so İşlerim reads as real work, not "Görev/Sorun".
    const work = (id, intent, titleKey, overrides) => base(id, intent, titleKey,
        Object.assign({ title: resource(titleKey) }, overrides || {}));

    const fixtures = [
        // ── AKTİF — MAX VERİ ──────────────────────────────────────────────────
        // Fully populated task — every render-able capability carries real content
        // (checklist · subtasks · dependencies · attachments · business context · time
        // · plan · activity). The richest surface for designing the Task Detail page.
        work('ISLERIM-WORK-ACTIVE', 'task', 'IsTitleElimination', {
            summary: resource('IsSummaryElimination'),
            taskLifecycle: 'InProgress', executionState: 'active', timerState: 'running',
            nativeStatus: { code: 'IN_PROGRESS', label: resource('StatusInProgress') },
            workItemCapabilities: ['planning', 'execution', 'timeTracking', 'checklist', 'subtasks', 'dependencies', 'attachments', 'activity', 'businessContext', 'relatedRecords'],
            checklist: { items: [
                { id: 'C1', label: resource('IsChkMatchBalances'), completed: true, required: true },
                { id: 'C2', label: resource('IsChkEliminate'), completed: false, required: true },
                { id: 'C3', label: resource('IsChkReconcileFx'), completed: false, required: true },
                { id: 'C4', label: resource('IsChkClose'), completed: false, required: false }
            ] },
            subtasks: { mode: 'full', items: [
                { id: 'S1', title: 'Q2 hesap bakiyelerini içe aktar', status: 'done' },
                { id: 'S2', title: 'Şirketler arası kalemleri işaretle', status: 'in-progress' },
                { id: 'S3', title: 'Fark kaydı öner', status: 'not-started' },
                { id: 'S4', title: 'Cetveli kontrol et', status: 'not-started' }
            ] },
            dependencies: [
                { id: 'D1', title: resource('IsDepLedgerClose'), type: 'FS', state: 'done', direction: 'pred' },
                { id: 'D2', title: resource('IsDepConsolidation'), type: 'FS', state: 'not-started', direction: 'succ' }
            ],
            attachments: [
                { id: 'A1', label: resource('IsAttachTrialBalance'), version: 3 },
                { id: 'A2', label: resource('IsAttachEliminationTemplate'), version: 1 }
            ],
            businessContext: { sections: [{ title: { key: 'IsCtxFinancials' }, fields: [
                { label: resource('IsFactAmount'), value: '₺2.400.000', valueType: 'currency' },
                { label: resource('IsFactCurrency'), value: 'TRY', valueType: 'text' },
                { label: resource('IsFactCostCenter'), value: 'OPS-100', valueType: 'text' },
                { label: resource('IsFactPeriod'), value: '2026 Q2', valueType: 'text' }
            ] }] },
            activity: [
                { actor: 'Deniz Koç', kind: 'comment', text: 'Q2 mizanı ekte, banka mutabakatı tamam.', at: '2026-07-24 09:10' },
                { actor: 'Selin Aras', kind: 'comment', text: 'Şirketler arası kalemleri işaretliyorum, öğleden sonra biter.', at: '2026-07-24 11:30' }
            ],
            personal: personal({ plannedDate: '2026-07-25', note: 'Konsolidasyon çalışmasından önce bitmeli.' }),
            timeEntries: [], priority: 'high',
            requester: req('USR-201', 'Deniz Koç'),
            source: source('finance', 'CloseTask', 'FIN-7781'),
            concurrency: { kind: 'version', token: 'is-01' }, dueAt: '2026-07-25',
            actions: [action('pause'), action('complete', { requiresConfirmation: true }), action('logTime', { input: 'minutes' }), action('requestInfo', { requiresReason: true }), action('reassign', { requiresReason: true })],
            primaryActionCode: 'complete', secondaryActionCodes: ['pause'], overflowActionCodes: ['logTime', 'requestInfo', 'reassign'],
            expectation: { surfaceMode: 'execution', readOnly: false, primaryActionCode: 'complete' }
        }),
        // Issue you've taken on — act-directly (Çöz), no accept gate.
        work('ISLERIM-WORK-ISSUE', 'issue', 'IsTitlePricingRegression', {
            summary: resource('IsSummaryPricingRegression'),
            nativeStatus: { code: 'INVESTIGATING', label: resource('StatusInProgress') },
            workItemCapabilities: ['activity', 'attachments', 'businessContext', 'relatedRecords'],
            attachments: [], priority: 'medium',
            requester: req('USR-202', 'Mert Aksoy'),
            source: source('integration-monitoring', 'IncidentIssue', 'INC-88120'),
            concurrency: { kind: 'version', token: 'is-02' }, dueAt: '2026-07-27',
            actions: [action('resolve', { requiresConfirmation: true }), action('requestInfo', { requiresReason: true }), action('reassign', { requiresReason: true })],
            primaryActionCode: 'resolve', secondaryActionCodes: ['requestInfo'], overflowActionCodes: ['reassign'],
            expectation: { surfaceMode: 'investigation', readOnly: false, primaryActionCode: 'resolve' }
        }),
        // Blocked by a predecessor — Başlat disabled, overdue.
        work('ISLERIM-WORK-BLOCKED', 'task', 'IsTitleOnboardingChecklist', {
            summary: resource('IsSummaryOnboardingChecklist'),
            taskLifecycle: 'Open', executionState: 'notStarted',
            workItemCapabilities: ['execution', 'checklist', 'dependencies', 'activity', 'businessContext', 'relatedRecords'],
            checklist: { items: [] },
            dependencies: [{ id: 'DEP-ONB', title: resource('IsDepSignedContract'), type: 'FS', state: 'inProgress', blocking: true }],
            priority: 'medium', requester: req('USR-203', 'Ece Yıldırım'),
            source: source('hr', 'OnboardingTask', 'ONB-5521'),
            concurrency: { kind: 'version', token: 'is-03' }, dueAt: '2026-07-23',
            actions: [disabledAction('start', 'DEPENDENCY_BLOCKED', 'ActionDisabledDependencyBlocked')],
            blockedState: { blocked: true, affectedActionCodes: ['start'], blockers: [{ code: 'DEPENDENCY_BLOCKED', label: resource('IsDepSignedContract') }] },
            primaryActionCode: 'start',
            expectation: { surfaceMode: 'execution', readOnly: false, primaryActionCode: 'start', criticalBannerCode: 'hardBlocked' }
        }),
        // Delegated — you handle it on behalf of Aylin Ersoy ("X adına").
        work('ISLERIM-WORK-DELEGATED', 'task', 'IsTitleVendorPrequal', {
            summary: resource('IsSummaryVendorPrequal'),
            taskLifecycle: 'InProgress', executionState: 'active', timerState: 'inactive',
            nativeStatus: { code: 'IN_PROGRESS', label: resource('StatusInProgress') },
            workItemCapabilities: ['execution', 'checklist', 'activity', 'businessContext', 'relatedRecords'],
            checklist: { items: [] },
            delegationContext: { id: 'USR-104', displayName: 'Aylin Ersoy' },
            priority: 'medium', requester: req('USR-204', 'Burak Şahin'),
            source: source('procurement', 'SourcingTask', 'TED-4471'),
            concurrency: { kind: 'version', token: 'is-04' }, dueAt: '2026-07-28',
            actions: [action('pause'), action('complete', { requiresConfirmation: true })],
            primaryActionCode: 'complete', secondaryActionCodes: ['pause'],
            expectation: { surfaceMode: 'execution', readOnly: false, primaryActionCode: 'complete' }
        }),
        // Snoozed — personal signal (stays in Aktif per Fable's law), low priority.
        work('ISLERIM-WORK-SNOOZED', 'task', 'IsTitleRackAudit', {
            summary: resource('IsSummaryRackAudit'),
            taskLifecycle: 'InProgress', executionState: 'active',
            nativeStatus: { code: 'IN_PROGRESS', label: resource('StatusInProgress') },
            workItemCapabilities: ['execution', 'checklist', 'activity', 'businessContext', 'relatedRecords'],
            checklist: { items: [] },
            personal: personal({ snoozedUntil: '2026-07-30' }),
            priority: 'low', requester: req('USR-205', 'Aylin Ersoy'),
            source: source('quality', 'CountTask', 'CYC-3402'),
            concurrency: { kind: 'version', token: 'is-05' }, dueAt: '2026-07-29',
            actions: [action('complete', { requiresConfirmation: true })],
            primaryActionCode: 'complete',
            expectation: { surfaceMode: 'execution', readOnly: false, primaryActionCode: 'complete', noticeCodes: ['snoozed'] }
        }),
        // ── BEKLEYEN ──────────────────────────────────────────────────────────
        // Waiting on information — paired waitingContext, high priority.
        work('ISLERIM-WORK-WAITING', 'task', 'IsTitleCapaEvidence', {
            summary: resource('IsSummaryCapaEvidence'),
            normalizedStatus: 'Waiting', taskLifecycle: 'Waiting', executionState: 'paused', timerState: 'inactive',
            nativeStatus: { code: 'WAITING_INFORMATION', label: resource('StatusWaiting') },
            waitingContext: { type: 'information', waitingOn: req('USR-206', 'Merve Şahin'), since: '2026-07-24T09:00:00+03:00', expectedUntil: null },
            workItemCapabilities: ['execution', 'checklist', 'activity', 'businessContext', 'relatedRecords'],
            checklist: { items: [] }, priority: 'high',
            requester: req('USR-206', 'Merve Şahin'),
            source: source('quality', 'DeviationTask', 'CAPA-2231'),
            concurrency: { kind: 'version', token: 'is-06' }, dueAt: '2026-07-26',
            actions: [action('resume'), action('reassign', { requiresReason: true })],
            primaryActionCode: 'resume', overflowActionCodes: ['reassign'],
            expectation: { surfaceMode: 'execution', readOnly: false, primaryActionCode: 'resume', noticeCodes: ['waiting'] }
        }),
        // Review with a scheduled approval meeting — waits until the meeting is held.
        work('ISLERIM-WORK-REVIEW-MEETING', 'review', 'IsTitleNdaReview', {
            summary: resource('IsSummaryNdaReview'),
            normalizedStatus: 'Waiting',
            nativeStatus: { code: 'WAITING_MEETING', label: resource('StatusWaiting') },
            waitingContext: { type: 'meeting', waitingOn: req('USR-207', 'Yasemin Ak'), since: '2026-07-24T11:00:00+03:00', expectedUntil: '2026-07-28' },
            workItemCapabilities: ['activity', 'attachments', 'businessContext', 'relatedRecords'],
            attachments: [], priority: 'medium',
            requester: req('USR-207', 'Yasemin Ak'),
            source: source('legal', 'ContractReview', 'CTR-1180'),
            concurrency: { kind: 'version', token: 'is-07' }, dueAt: '2026-07-27',
            actions: [action('signoff', { requiresConfirmation: true }), action('requestInfo', { requiresReason: true }), action('return', { requiresReason: true })],
            primaryActionCode: 'signoff', secondaryActionCodes: ['requestInfo'], overflowActionCodes: ['return'],
            expectation: { surfaceMode: 'review', readOnly: false, primaryActionCode: 'signoff', noticeCodes: ['waiting'] }
        }),
        // ── PLANLI ────────────────────────────────────────────────────────────
        // Planned for a future personal date, not yet started, low priority.
        work('ISLERIM-WORK-PLANNED', 'task', 'IsTitleDepreciation', {
            summary: resource('IsSummaryDepreciation'),
            normalizedStatus: 'InProgress', taskLifecycle: 'Planned', executionState: 'notStarted',
            nativeStatus: { code: 'PLANNED', label: resource('LifecyclePlanned') },
            workItemCapabilities: ['planning', 'execution', 'checklist', 'activity', 'businessContext', 'relatedRecords'],
            checklist: { items: [] },
            personal: personal({ plannedDate: '2026-07-30' }),
            priority: 'low', requester: req('USR-201', 'Deniz Koç'),
            source: source('finance', 'CloseTask', 'FIN-7790'),
            concurrency: { kind: 'version', token: 'is-08' }, dueAt: '2026-07-30',
            actions: [action('start'), action('replan')],
            primaryActionCode: 'start', secondaryActionCodes: ['replan'],
            expectation: { surfaceMode: 'execution', readOnly: false, primaryActionCode: 'start' }
        })
    ];

    global.WorkCenterNextFixtures = global.WorkCenterNextFixtures || {};
    global.WorkCenterNextFixtures.islerimShowcase = fixtures;
})(typeof window !== 'undefined' ? window : globalThis);
