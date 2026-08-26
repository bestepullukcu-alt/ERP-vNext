'use strict';

// Havuz (Pool) showcase — unowned work waiting to be picked up. Two shapes:
//   • groupQueue (admissionState pendingClaim): a team queue → Üzerine al (claim).
//     REQUIRES pool { id, label:{kind:'display'} } (validator POOL_REQUIRED_FOR_GROUP_QUEUE);
//     pool.label is a DISPLAY string (a position name someone typed), never a resource key.
//   • offered (admissionState pendingOffer): offered to the viewer → Kabul et / Reddet.
//     Offered work is NOT queued → it carries NO pool (validator POOL_ON_NON_QUEUE_ITEM).
// Handler codes: claim/acceptOffer → owned+İşlerim, decline → removed, release → back to pool.
// acceptOffer/decline have no Act* resx keys → reuse ActAccept/ActReject via label override.
(function (global) {
    const f = global.WorkCenterNextFixtureFactory;
    if (!f) { throw new Error('WorkCenterNextFixtureFactory is required.'); }
    const { resource, action, source, personal, base } = f;

    /*
     * A display label needs THREE things, not two: fixture-contract.js requires `kind === 'display'`, a non-empty
     * `text`, a non-empty `locale`, and `key === undefined`. The locale was missing, so both HAVUZ-CLAIM fixtures
     * failed POOL_LABEL_INVALID and mapPayload DROPPED them — the Pool showcase rendered one item instead of
     * three, which is most of the reason the showcase exists.
     *
     * 'und' is not a placeholder: it is exactly what the server sends. WorkItemLabelDto.Display defaults to
     * WorkItemContract.LocaleUndetermined = "und" for text whose language nobody recorded — a position name
     * someone typed. A fixture that carried a different shape from the real payload would be testing something
     * the product never produces.
     */
    const displayLabel = (text) => ({ kind: 'display', text, locale: 'und' });

    const pooled = (id, intent, titleKey, overrides) => base(id, intent, titleKey, Object.assign({
        ownershipState: 'unowned',
        normalizedStatus: 'Pending',
        nativeStatus: { code: 'POOLED', label: resource('StatusPending') },
        executionState: intent === 'task' ? 'notStarted' : 'notApplicable',
        timerState: intent === 'task' ? 'inactive' : 'notApplicable',
        personal: personal({ seen: false }),
        title: resource(titleKey),
        summary: resource(`${titleKey}Summary`)
    }, overrides || {}));

    const fixtures = [
        // ── GROUP QUEUE — Finans analisti kuyruğu ────────────────────────────
        pooled('HAVUZ-CLAIM-01', 'task', 'HavuzTitleInvoiceMatch', {
            assignmentMode: 'groupQueue',
            admissionState: 'pendingClaim',
            pool: { id: 'POS-FIN-ANALYST', label: displayLabel('Finans Analisti — Muhasebe') },
            workItemCapabilities: ['planning', 'execution', 'activity', 'businessContext', 'relatedRecords'],
            source: source('finance', 'InvoiceMatchTask', 'INV-90112'),
            concurrency: { kind: 'version', token: 'pool-1' },
            actions: [action('claim'), action('reassign', { requiresReason: true })],
            primaryActionCode: 'claim',
            secondaryActionCodes: [],
            overflowActionCodes: ['reassign'],
            priority: 'Medium',
            dueAt: '2026-07-26'
        }),
        // ── GROUP QUEUE — Satınalma inceleme kuyruğu (escalated + due-soon) ───
        pooled('HAVUZ-CLAIM-02', 'task', 'HavuzTitlePrReview', {
            assignmentMode: 'groupQueue',
            admissionState: 'pendingClaim',
            escalated: true,
            pool: { id: 'POS-PROC-REVIEW', label: displayLabel('Satınalma İnceleme Kuyruğu') },
            workItemCapabilities: ['execution', 'activity', 'businessContext', 'relatedRecords'],
            source: source('procurement', 'PurchaseRequestReview', 'PR-5541'),
            concurrency: { kind: 'version', token: 'pool-2' },
            actions: [action('claim'), action('reassign', { requiresReason: true })],
            primaryActionCode: 'claim',
            secondaryActionCodes: [],
            overflowActionCodes: ['reassign'],
            priority: 'High',
            dueAt: '2026-07-25'
        }),
        // ── OFFERED — offered directly to the viewer (accept / decline) ───────
        pooled('HAVUZ-OFFER-01', 'issue', 'HavuzTitleAuditFinding', {
            assignmentMode: 'offered',
            admissionState: 'pendingOffer',
            ownershipState: 'assigned',
            workItemCapabilities: ['activity', 'businessContext', 'relatedRecords'],
            source: source('quality', 'AuditFindingReview', 'AUD-2207'),
            concurrency: { kind: 'version', token: 'offer-1' },
            actions: [
                action('acceptOffer', { label: resource('ActAccept') }),
                action('decline', { label: resource('ActReject') })
            ],
            primaryActionCode: 'acceptOffer',
            secondaryActionCodes: [],
            overflowActionCodes: ['decline'],
            priority: 'Medium',
            dueAt: '2026-07-27'
        })
    ];

    global.WorkCenterNextFixtures = global.WorkCenterNextFixtures || {};
    global.WorkCenterNextFixtures.havuzShowcase = fixtures;
})(typeof window !== 'undefined' ? window : globalThis);
