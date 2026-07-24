'use strict';

(function (global) {
    const f = global.WorkCenterNextFixtureFactory;
    if (!f) { throw new Error('WorkCenterNextFixtureFactory is required.'); }
    const { resource, action, source } = f;

    const fixtures = [{
        fixtureKind: 'triggerOnly',
        id: 'TRG-MEETING-01',
        triggerType: 'meetingInvite',
        title: resource('InboxTitleMeetingInvite'),
        summary: resource('InboxTitleMeetingInviteSummary'),
        source: source('calendar', 'MeetingInvitation', 'MTG-2026-118', { deepLink: '/Calendar/Meetings/MTG-2026-118' }),
        systemState: 'fresh',
        concurrency: { kind: 'etag', token: 'meeting-18' },
        actions: [
            action('acceptMeeting', { label: resource('ActAccept') }),
            action('declineMeeting', { label: resource('ActReject'), requiresReason: true })
        ],
        primaryActionCode: 'acceptMeeting',
        secondaryActionCodes: [],
        overflowActionCodes: ['declineMeeting'],
        responseBehavior: 'remove',
        expectation: { surfaceMode: 'triggerResponse', primaryActionCode: 'acceptMeeting' }
    }];

    global.WorkCenterNextFixtures = global.WorkCenterNextFixtures || {};
    global.WorkCenterNextFixtures.triggerOnly = fixtures;
})(typeof window !== 'undefined' ? window : globalThis);
