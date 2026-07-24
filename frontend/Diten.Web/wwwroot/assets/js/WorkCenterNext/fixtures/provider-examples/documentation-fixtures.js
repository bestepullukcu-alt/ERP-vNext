'use strict';

(function (global) {
    const f = global.WorkCenterNextFixtureFactory;
    if (!f) { throw new Error('WorkCenterNextFixtureFactory is required.'); }
    const { resource, disabledAction, source, base } = f;

    const fixtures = [
        base('DOC-REF-01', 'review', 'FixtureTitleDocumentReference', {
            normalizedStatus: 'Done',
            nativeStatus: { code: 'REFERENCE_ONLY', label: resource('StatusDone') },
            workItemCapabilities: ['attachments', 'activity', 'businessContext', 'relatedRecords'],
            attachments: [{
                id: 'DOC-18-V3',
                label: resource('ContextControlledDocument'),
                version: '3',
                accessState: 'allowed',
                deepLink: '/ControlledDocuments/Details/DOC-18'
            }],
            source: source('controlled-documents', 'ControlledDocumentVersion', 'DOC-18-V3', {
                sourceSystem: 'Diten.Platform',
                deepLink: '/ControlledDocuments/Details/DOC-18'
            }),
            actions: [],
            expectation: { surfaceMode: 'readonly', readOnly: true, primaryActionCode: null }
        }),
        base('DOC-EVIDENCE-01', 'task', 'FixtureTitleEvidenceRequired', {
            taskLifecycle: 'InProgress',
            executionState: 'active',
            nativeStatus: { code: 'IN_PROGRESS', label: resource('StatusInProgress') },
            workItemCapabilities: ['execution', 'evidence', 'activity', 'businessContext', 'relatedRecords'],
            evidence: { required: true, complete: false, items: [] },
            source: source('quality', 'CorrectiveActionTask', 'CAPA-2231'),
            actions: [disabledAction('complete', 'EVIDENCE_INCOMPLETE', 'ActionDisabledEvidenceIncomplete', { requiresEvidence: true })],
            blockedState: { blocked: true, affectedActionCodes: ['complete'], blockers: [{ code: 'EVIDENCE_INCOMPLETE', label: resource('EvidenceMissing') }] },
            primaryActionCode: 'complete',
            expectation: { surfaceMode: 'execution', readOnly: false, primaryActionCode: 'complete', criticalBannerCode: 'hardBlocked' }
        }),
        base('DOC-ACCESS-01', 'review', 'FixtureTitleDocumentRestricted', {
            normalizedStatus: 'Pending',
            nativeStatus: { code: 'REVIEW_REQUESTED', label: resource('StatusPending') },
            workItemCapabilities: ['attachments', 'activity', 'businessContext', 'relatedRecords'],
            attachments: [{ id: 'DOC-99-V1', label: resource('RedactedValue'), accessState: 'restricted', redacted: true }],
            source: source('controlled-documents', 'ControlledDocumentVersion', 'DOC-99-V1', {
                sourceSystem: 'Diten.Platform',
                deepLink: '/ControlledDocuments/Details/DOC-99'
            }),
            actions: [],
            expectation: { surfaceMode: 'readonly', readOnly: true, primaryActionCode: null }
        })
    ];

    global.WorkCenterNextFixtures = global.WorkCenterNextFixtures || {};
    global.WorkCenterNextFixtures.documentation = fixtures;
})(typeof window !== 'undefined' ? window : globalThis);
