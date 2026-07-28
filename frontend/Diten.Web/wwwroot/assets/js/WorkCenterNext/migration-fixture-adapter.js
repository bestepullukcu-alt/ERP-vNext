'use strict';

(function (global) {
    const adaptLegacyFixture = (legacy) => {
        const f = global.WorkCenterNextFixtureFactory;
        if (!f || !legacy || legacy.fixtureKind !== 'migration') { return null; }
        const { resource, action, disabledAction, source, base } = f;
        const common = {
            source: source('legacy-adapter', legacy.itemType === 'approval' ? 'Approval' : 'Task', legacy.sourceId),
            migrationNotice: { code: legacy.legacyKind, label: resource('MigrationAdaptedNotice') }
        };
        if (legacy.legacyKind === 'PendingApproval') {
            return base(legacy.id, 'approval', legacy.titleKey, Object.assign(common, {
                assignmentMode: 'approval',
                ownershipState: 'assigned',
                admissionState: 'admitted',
                normalizedStatus: 'Pending',
                nativeStatus: { code: 'LEGACY_PENDING_APPROVAL', label: resource('MigrationLegacyPendingApproval') },
                concurrency: { kind: 'opaque', token: `${legacy.id}-1` },
                actions: [action('approve', { requiresConfirmation: true }), action('reject', { requiresReason: true })],
                primaryActionCode: 'approve',
                overflowActionCodes: ['reject'],
                expectation: { surfaceMode: 'decision', readOnly: false, primaryActionCode: 'approve', noticeCodes: ['migration'] }
            }));
        }
        if (legacy.legacyKind === 'LegacyBlocker') {
            return base(legacy.id, 'task', legacy.titleKey, Object.assign(common, {
                actions: [disabledAction('start', 'LEGACY_BLOCKED', legacy.blocker?.messageKey || 'ActionDisabledDependencyBlocked')],
                blockedState: { blocked: true, affectedActionCodes: ['start'], blockers: [{ code: 'LEGACY_BLOCKED', label: resource('MigrationLegacyBlocker') }] },
                primaryActionCode: 'start',
                expectation: { surfaceMode: 'execution', readOnly: false, primaryActionCode: 'start', criticalBannerCode: 'hardBlocked', noticeCodes: ['migration'] }
            }));
        }
        return base(legacy.id, 'task', legacy.titleKey, Object.assign(common, {
            normalizedStatus: 'Waiting',
            taskLifecycle: 'Waiting',
            executionState: 'paused',
            nativeStatus: { code: 'LEGACY_WAITING_INFORMATION', label: resource('MigrationLegacyInformationRequest') },
            waitingContext: {
                type: 'externalInformation',
                waitingOn: { id: 'legacy-person', displayName: legacy.waitingOn || 'Unknown' },
                since: '2026-07-24T09:00:00+03:00',
                expectedUntil: null
            },
            concurrency: { kind: 'opaque', token: `${legacy.id}-1` },
            actions: [action('resume')],
            primaryActionCode: 'resume',
            expectation: { surfaceMode: 'execution', readOnly: false, primaryActionCode: 'resume', noticeCodes: ['waiting', 'migration'] }
        }));
    };

    global.WorkCenterNextMigrationAdapter = { adaptLegacyFixture };
})(typeof window !== 'undefined' ? window : globalThis);
