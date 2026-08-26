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
      "fixtures/islerim-showcase-fixtures.js",
      "fixtures/havuz-showcase-fixtures.js",
      "fixtures/gecmis-showcase-fixtures.js",
      "fixtures/edge-case-fixtures.js",
      "fixtures/provider-examples/enterprise-strategy-fixtures.js",
      "fixtures/provider-examples/documentation-fixtures.js",
      "fixtures/trigger-only-fixtures.js",
      "fixtures/migration-fixtures.js",
      "migration-fixture-adapter.js",
      "mock-data.js"
    ].forEach((file) => loadScript(scriptRoot + file));
  });

  /*
   * DISCOVERED, never listed. This test used to name five groups by hand — and that hard-coded list is exactly
   * how three broken fixtures reached live verification: `havuzShowcase`, `gecmisShowcase` and `islerimShowcase`
   * were added, were never named here, and so were never validated. Two HAVUZ items were missing `pool.label.locale`
   * and one GECMIS item carried an inherited `businessContext` with no capability; mapPayload DROPPED all three,
   * so the Pool showcase rendered 1 of 3 items and History 2 of 3.
   *
   * Discovery makes the next group covered on the day it is written rather than on the day someone remembers.
   */
  const workItemGroups = () => Object.keys(global.WorkCenterNextFixtures)
    .filter((name) => name !== "triggerOnly" && name !== "migration")
    .filter((name) => Array.isArray(global.WorkCenterNextFixtures[name]));

  it("validates every work item in EVERY fixture group, discovered not listed", () => {
    workItemGroups().forEach((group) => {
      global.WorkCenterNextFixtures[group].forEach((fixture) => {
        const verdict = global.WorkCenterNextContract.validateWorkItem(fixture);
        // The id and the errors go in the message: "expected true, got false" names neither the item that broke
        // nor the rule it broke, and a dropped item is invisible on screen by definition.
        expect(verdict.valid, `${group}/${fixture.id}: ${JSON.stringify(verdict.errors)}`).toBe(true);
      });
    });
  });

  it("actually discovers the showcase groups, and none of them is empty", () => {
    /*
     * Non-vacuity for the discovery itself. `forEach` over an empty list passes, and a typo in a filename would
     * silently reduce this whole suite to zero assertions — the failure mode the hard-coded list already had,
     * reintroduced by a subtler route.
     */
    const groups = workItemGroups();

    ["canonical", "inboxShowcase", "islerimShowcase", "havuzShowcase", "gecmisShowcase",
     "edgeCases", "enterpriseStrategy", "documentation"].forEach((name) => {
      expect(groups).toContain(name);
      expect(global.WorkCenterNextFixtures[name].length).toBeGreaterThan(0);
    });
  });

  it("REJECTS a fixture that breaks the contract, which is what makes the sweep a test", () => {
    /*
     * The vacuity guard, run rather than asserted in a report: take a real, passing fixture and reintroduce the
     * exact defect this ticket fixed — a display label with no locale. If the contract accepts it, every
     * expectation above is decoration.
     */
    const good = global.WorkCenterNextFixtures.havuzShowcase.find((item) => item.id === "HAVUZ-CLAIM-01");
    expect(global.WorkCenterNextContract.validateWorkItem(good)).toMatchObject({ valid: true });

    const broken = JSON.parse(JSON.stringify(good));
    delete broken.pool.label.locale;

    const verdict = global.WorkCenterNextContract.validateWorkItem(broken);
    expect(verdict.valid).toBe(false);
    expect(verdict.errors.map((error) => error.code)).toContain("POOL_LABEL_INVALID");
  });

  it("validates every trigger", () => {
    global.WorkCenterNextFixtures.triggerOnly.forEach((fixture) => {
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
