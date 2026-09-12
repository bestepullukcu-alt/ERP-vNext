'use strict';

(function (global) {
    const f = global.WorkCenterNextFixtureFactory;
    if (!f) { throw new Error('WorkCenterNextFixtureFactory is required.'); }
    const { resource, action, source } = f;
    void resource; void action; void source;

    // meetingInvite'ın gösterim amaçlı (triggerOnly) örneği kaldırıldı: Görev Merkezi artık
    // gerçek MeetingWorkItemProvider verisini gösteriyor (bkz. S5c, WP-MG-MOD0357-S5C-INVITE-CARD-01).
    const fixtures = [];

    global.WorkCenterNextFixtures = global.WorkCenterNextFixtures || {};
    global.WorkCenterNextFixtures.triggerOnly = fixtures;
})(typeof window !== 'undefined' ? window : globalThis);
