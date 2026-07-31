'use strict';

// Geçmiş (History) showcase — real, varied terminal work (Done / Cancelled), read-only.
// Replaces the single WC-TASK-DONE placeholder so the Geçmiş tab reads as an actual
// 90-day archive: a completed task, an approval the viewer decided, and a cancelled
// task. Terminal items carry NO enabled inline actions (validator: TERMINAL_STATE_*),
// and are seen (personal.seen = true). Mirrors the inbox/İşlerim showcase pattern.
(function (global) {
    const f = global.WorkCenterNextFixtureFactory;
    if (!f) { throw new Error('WorkCenterNextFixtureFactory is required.'); }
    const { resource, source, personal, base } = f;

    // base() ignores the titleKey arg → set real title/summary explicitly.
    const done = (id, intent, titleKey, overrides) => base(id, intent, titleKey, Object.assign({
        ownershipState: intent === 'task' ? 'owned' : 'notApplicable',
        admissionState: 'admitted',
        executionState: 'notApplicable',
        timerState: 'notApplicable',
        actions: [],
        personal: personal({ seen: true }),
        title: resource(titleKey),
        summary: resource(`${titleKey}Summary`),
        expectation: { surfaceMode: 'readonly', readOnly: true, primaryActionCode: null }
    }, overrides || {}));

    const fixtures = [
        // ── DONE — completed task ────────────────────────────────────────────
        done('GECMIS-TASK-DONE-01', 'task', 'GecmisTitleQ1Close', {
            normalizedStatus: 'Done',
            taskLifecycle: 'Done',
            nativeStatus: { code: 'COMPLETED', label: resource('StatusDone') },
            workItemCapabilities: ['checklist', 'activity', 'businessContext', 'relatedRecords'],
            checklist: { items: [
                { id: 'H1', label: resource('GecmisChkReconcile'), completed: true, required: true },
                { id: 'H2', label: resource('GecmisChkReview'), completed: true, required: true },
                { id: 'H3', label: resource('GecmisChkPublish'), completed: true, required: true }
            ] },
            businessContext: { sections: [{ title: { key: 'IsCtxFinancials' }, fields: [
                { label: resource('IsFactPeriod'), value: '2026 Q1', valueType: 'text' },
                { label: resource('IsFactCostCenter'), value: 'FIN-000', valueType: 'text' }
            ] }] },
            activity: [
                { actor: 'Deniz Koç', kind: 'comment', text: 'Tüm birim mutabakatları tamam, kapanış onaylandı.', at: '2026-07-18 16:40' }
            ],
            source: source('finance', 'PeriodCloseTask', 'FIN-CLOSE-Q1', { deepLink: '/WorkCenterNext?source=finance&id=FIN-CLOSE-Q1' }),
            requester: { id: 'USR-201', displayName: 'Deniz Koç' },
            priority: 'High',
            dueAt: '2026-07-18'
        }),
        // ── DONE — approval the viewer decided ───────────────────────────────
        done('GECMIS-APPROVAL-DONE-01', 'approval', 'GecmisTitleSupplierContract', {
            normalizedStatus: 'Done',
            taskLifecycle: 'notApplicable',
            viewerRole: 'Approver',
            nativeStatus: { code: 'APPROVED', label: resource('StatusDone') },
            workItemCapabilities: ['activity', 'businessContext', 'relatedRecords'],
            businessContext: { sections: [{ title: { key: 'IsCtxFinancials' }, fields: [
                { label: resource('IsFactAmount'), value: '₺1.150.000', valueType: 'currency' },
                { label: resource('IsFactCurrency'), value: 'TRY', valueType: 'text' }
            ] }] },
            activity: [
                { actor: 'Selin Aras', kind: 'comment', text: 'Şartlar uygun, onaylandı; satınalmaya devredildi.', at: '2026-07-20 10:05' }
            ],
            source: source('procurement', 'FrameworkContractApproval', 'PROC-FC-338', { deepLink: '/WorkCenterNext?source=procurement&id=PROC-FC-338' }),
            requester: { id: 'USR-104', displayName: 'Aylin Ersoy' },
            priority: 'Medium',
            dueAt: '2026-07-20'
        }),
        // ── CANCELLED — superseded task ──────────────────────────────────────
        done('GECMIS-TASK-CANCELLED-01', 'task', 'GecmisTitleReportTemplate', {
            normalizedStatus: 'Cancelled',
            taskLifecycle: 'Cancelled',
            nativeStatus: { code: 'CANCELLED', label: resource('StatusCancelled') },
            workItemCapabilities: ['activity', 'relatedRecords'],
            /*
             * BOTH halves dropped, not one. base() hands every fixture a default
             * `businessContext: { sections: [] }`, so removing the capability alone left an inherited container
             * with no capability — CAPABILITY_REQUIRED_FOR_DATA, and the item was dropped from the History
             * showcase entirely.
             *
             * Dropping the data (rather than re-declaring the capability) is what the sibling fixtures actually
             * do: GECMIS-TASK-DONE-01 and GECMIS-APPROVAL-DONE-01 declare `businessContext` BECAUSE each supplies
             * real sections. The rule all three follow is "declare it when you have sections", and this one has
             * none — the inherited default is empty. Re-adding the capability would have declared a block that
             * renders nothing, which is the same mistake in the opposite direction.
             */
            businessContext: null,
            activity: [
                { actor: 'Selin Aras', kind: 'comment', text: 'Yeni raporlama modülü geldiği için iptal edildi.', at: '2026-07-15 13:20' }
            ],
            source: source('workcenter', 'Task', 'TASK-RPT-OLD', { deepLink: '/WorkCenterNext?source=workcenter&id=TASK-RPT-OLD' }),
            requester: { id: 'USR-OWN', displayName: 'Selin Aras' },
            priority: 'Low',
            dueAt: '2026-07-16'
        })
    ];

    global.WorkCenterNextFixtures = global.WorkCenterNextFixtures || {};
    global.WorkCenterNextFixtures.gecmisShowcase = fixtures;
})(typeof window !== 'undefined' ? window : globalThis);
