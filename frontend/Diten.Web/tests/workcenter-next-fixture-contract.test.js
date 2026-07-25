const { loadScript } = require("./load-script");

const scriptRoot = "wwwroot/assets/js/WorkCenterNext/";

describe("WorkCenterNext canonical fixture contract", () => {
  beforeEach(() => {
    // WC-1b DEC-1 — the showcase fixture catalog is now gated by a SERVER-set flag. These contract tests are
    // explicitly about that catalog, so they opt in the same way Development does.
    document.body.innerHTML = '<div id="wcnApp" data-wcn-fixtures="showcase"></div>';
    delete global.WorkCenterNextContract;
    delete global.WorkCenterNextFixtureFactory;
    delete global.WorkCenterNextFixtures;
    delete global.WorkCenterNextMigrationAdapter;
    delete global.WorkCenterNextData;
    global.WCN = { t: (key) => key };
    [
      "fixture-contract.js",
      "fixtures/canonical-fixtures.js",
      "fixtures/inbox-showcase-fixtures.js",
      "fixtures/edge-case-fixtures.js",
      "fixtures/provider-examples/enterprise-strategy-fixtures.js",
      "fixtures/provider-examples/documentation-fixtures.js",
      "fixtures/trigger-only-fixtures.js",
      "fixtures/migration-fixtures.js",
      "migration-fixture-adapter.js",
      "mock-data.js"
    ].forEach((file) => loadScript(scriptRoot + file));
  });

  it("validates every work item, provider example and trigger", () => {
    const groups = global.WorkCenterNextFixtures;
    ["canonical", "inboxShowcase", "edgeCases", "enterpriseStrategy", "documentation"].forEach((group) => {
      groups[group].forEach((fixture) => {
        expect(global.WorkCenterNextContract.validateWorkItem(fixture)).toMatchObject({ valid: true });
      });
    });
    groups.triggerOnly.forEach((fixture) => {
      expect(global.WorkCenterNextContract.validateTrigger(fixture)).toMatchObject({ valid: true });
    });
  });

  it("adapts legacy migration records before canonical validation", () => {
    global.WorkCenterNextFixtures.migration.forEach((legacy) => {
      const fixture = global.WorkCenterNextMigrationAdapter.adaptLegacyFixture(legacy);
      expect(fixture.fixtureKind).toBe("workItem");
      expect(fixture.migrationNotice).toBeTruthy();
      expect(global.WorkCenterNextContract.validateWorkItem(fixture)).toMatchObject({ valid: true });
    });
  });

  it("keeps snooze personal and preserves lifecycle status", () => {
    const fixture = global.WorkCenterNextFixtures.canonical.find((item) => item.id === "WC-TASK-SNOOZED");
    expect(fixture.personal.snoozedUntil).toBeTruthy();
    expect(fixture.normalizedStatus).toBe("InProgress");
    expect(fixture.taskLifecycle).toBe("InProgress");
    expect(fixture.waitingContext).toBeUndefined();
  });

  it("preserves Inbox unread and projected row actions", () => {
    const items = global.WorkCenterNextData.buildItems();
    const acceptance = items.find((item) => item.id === "WC-TASK-ACCEPT");
    const approval = items.find((item) => item.id === "WC-APPROVAL-SIMPLE");
    expect(acceptance).toMatchObject({ tab: "inbox", isUnread: true });
    expect(acceptance.actions.map((action) => action.code)).toEqual(["accept", "plan", "reassign"]);
    expect(approval).toMatchObject({ tab: "inbox", isUnread: true });
    expect(approval.actions.map((action) => action.code)).toEqual(["approve", "reject", "requestInfo"]);
  });

  it("provides one visible Inbox example for every work intent", () => {
    const visibleInbox = global.WorkCenterNextData.buildItems()
      .filter((item) => item.catalogVisible && item.tab === "inbox");
    expect(new Set(visibleInbox.map((item) => item.itemType))).toEqual(
      new Set(["task", "approval", "review", "issue", "exception"])
    );
    expect(visibleInbox).toHaveLength(6);
  });

  it("blocks direct signoff when the review meeting is required", () => {
    const fixture = global.WorkCenterNextFixtures.inboxShowcase
      .find((item) => item.id === "INBOX-REVIEW-REQUIRED-MEETING");
    expect(fixture.reviewMeetingPolicy.requirement).toBe("required");
    expect(fixture.primaryActionCode).toBe("scheduleReviewMeeting");
    expect(fixture.actions.find((action) => action.code === "signoff")).toMatchObject({
      enabled: false,
      disabledReasonCode: "REVIEW_MEETING_REQUIRED"
    });
    expect(global.WorkCenterNextContract.validateWorkItem(fixture)).toMatchObject({ valid: true });
  });
});
