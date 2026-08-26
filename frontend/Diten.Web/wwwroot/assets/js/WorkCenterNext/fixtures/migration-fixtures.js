'use strict';

(function (global) {
    global.WorkCenterNextFixtures = global.WorkCenterNextFixtures || {};
    global.WorkCenterNextFixtures.migration = [
        {
            fixtureKind: 'migration',
            legacyKind: 'PendingApproval',
            id: 'LEGACY-APP-01',
            titleKey: 'FixtureTitleLegacyApproval',
            status: 'PendingApproval',
            itemType: 'approval',
            accepted: false,
            claimed: true,
            sourceModule: 'Legacy Workflow',
            sourceId: 'LEG-APR-1'
        },
        {
            fixtureKind: 'migration',
            legacyKind: 'LegacyBlocker',
            id: 'LEGACY-BLOCK-01',
            titleKey: 'FixtureTitleLegacyBlocker',
            status: 'Open',
            itemType: 'task',
            accepted: true,
            claimed: true,
            blocker: { isBlocked: true, messageKey: 'ActionDisabledDependencyBlocked' },
            sourceModule: 'Legacy Tasks',
            sourceId: 'LEG-TASK-1'
        },
        {
            fixtureKind: 'migration',
            legacyKind: 'LegacyInformationRequest',
            id: 'LEGACY-INFO-01',
            titleKey: 'FixtureTitleLegacyInformation',
            status: 'Waiting',
            itemType: 'task',
            accepted: true,
            claimed: true,
            waitingOn: 'Deniz Koç',
            sourceModule: 'Legacy Tasks',
            sourceId: 'LEG-INFO-1'
        }
    ];
})(typeof window !== 'undefined' ? window : globalThis);
