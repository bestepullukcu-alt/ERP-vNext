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

  // MOD-0357 S5c — the meetingInvite extension to the WC-1 contract (WORK_INTENTS + the 5 MEETING_INVITE_*
  // rules in validateWorkItem). No showcase fixture carries this type (meetingInvite is real
  // MeetingWorkItemProvider data only, never a fixture — see the "one visible Inbox example for every work
  // intent" test above, which deliberately excludes it), so the fixture is hand-built here from the SAME
  // factory every canonical fixture uses, per this file's own posture: the contract, not one sample of it.
  describe("meetingInvite (MOD-0357 S5c)", () => {
    const validMeetingInvite = () => {
      const f = global.WorkCenterNextFixtureFactory;
      return f.base("WC-MEETING-INVITE-TEST", "meetingInvite", "ChipMeetingInvite", {
        ownershipState: "notApplicable",
        admissionState: "notApplicable",
        normalizedStatus: "Pending",
        nativeStatus: { code: "Pending", label: f.resource("WorkAggregation_NativeStatus_Pending") },
        workItemCapabilities: [],
        businessContext: undefined,
        concurrency: { kind: "version", token: "1" },
        actions: [
          f.action("acceptInvite", { label: f.resource("WorkAggregation_Action_AcceptInvite") }),
          f.action("declineInvite", { label: f.resource("WorkAggregation_Action_DeclineInvite") })
        ],
        primaryActionCode: "acceptInvite",
        secondaryActionCodes: ["declineInvite"],
        overflowActionCodes: [],
        source: f.source("meetings", "MGMT-REVIEW", "MTG-TEST-01", { deepLink: "/Meetings/MTG-TEST-01" }),
        dueAt: "2026-09-20"
      });
    };

    it("accepts a well-formed invite", () => {
      expect(global.WorkCenterNextContract.validateWorkItem(validMeetingInvite())).toMatchObject({ valid: true });
    });

    it("REJECTS an invite with no deep link back to the meeting", () => {
      const broken = validMeetingInvite();
      broken.source.deepLink = null;

      const verdict = global.WorkCenterNextContract.validateWorkItem(broken);
      expect(verdict.valid).toBe(false);
      expect(verdict.errors.map((e) => e.code)).toContain("MEETING_INVITE_DEEPLINK_REQUIRED");
    });

    it("REJECTS an invite with no due date", () => {
      const broken = validMeetingInvite();
      broken.dueAt = null;

      const verdict = global.WorkCenterNextContract.validateWorkItem(broken);
      expect(verdict.valid).toBe(false);
      expect(verdict.errors.map((e) => e.code)).toContain("MEETING_INVITE_DUE_AT_REQUIRED");
    });

    it("REJECTS an invite whose primary action is not acceptInvite", () => {
      const broken = validMeetingInvite();
      broken.primaryActionCode = "declineInvite";

      const verdict = global.WorkCenterNextContract.validateWorkItem(broken);
      expect(verdict.valid).toBe(false);
      expect(verdict.errors.map((e) => e.code)).toContain("MEETING_INVITE_PRIMARY_ACTION_INVALID");
    });

    it("REJECTS an invite whose secondary action is not declineInvite", () => {
      const broken = validMeetingInvite();
      broken.secondaryActionCodes = [];

      const verdict = global.WorkCenterNextContract.validateWorkItem(broken);
      expect(verdict.valid).toBe(false);
      expect(verdict.errors.map((e) => e.code)).toContain("MEETING_INVITE_SECONDARY_ACTION_INVALID");
    });

    /*
     * AC6 red→green proof (c): "sözleşmeden acceptInvite silinince öz-test kırmızı" — removing acceptInvite
     * from the action set (not just from the placement fields above) turns this fixture invalid, closing the
     * action-vocabulary rule rather than only the placement rules.
     */
    it("REJECTS an invite missing the acceptInvite action entirely", () => {
      const broken = validMeetingInvite();
      broken.actions = broken.actions.filter((action) => action.code !== "acceptInvite");

      const verdict = global.WorkCenterNextContract.validateWorkItem(broken);
      expect(verdict.valid).toBe(false);
      expect(verdict.errors.map((e) => e.code)).toContain("MEETING_INVITE_ACTIONS_INVALID");
    });

    it("REJECTS an invite carrying a third action beyond accept/decline", () => {
      const f = global.WorkCenterNextFixtureFactory;
      const broken = validMeetingInvite();
      broken.actions.push(f.action("snooze", { label: f.resource("ActSnooze") }));
      broken.overflowActionCodes = ["snooze"];

      const verdict = global.WorkCenterNextContract.validateWorkItem(broken);
      expect(verdict.valid).toBe(false);
      expect(verdict.errors.map((e) => e.code)).toContain("MEETING_INVITE_ACTIONS_INVALID");
    });
  });

  it("has no triggerOnly showcase sample left (MOD-0357 S5c retired the last one)", () => {
    /*
     * This used to `forEach` over `triggerOnly` and validate each one — an empty array now makes that loop
     * vacuous, exactly the failure mode this file's own "discovered, never listed" comment warns about above.
     * Made explicit instead of left silent. `validateTrigger` itself is still exercised, by a hand-built
     * fixture, in workcenter-next-resolvers.test.js ("never sends trigger-only fixtures into Task Detail").
     */
    expect(global.WorkCenterNextFixtures.triggerOnly).toEqual([]);
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

  /*
   * MOD-0357 S9 · CT fix-up F1 — on a MOD-0024 task the review-meeting gate holds back the DECISION
   * (submitReview/complete), never `start`: holding the meeting is part of the work. No showcase fixture carries a
   * Required task (live TaskWorkItemProvider data does, and work-items-api.js DROPS whatever this rule rejects), so
   * the items are cloned from the canonical task fixtures and given a Required policy here.
   */
  describe("review meeting gate on a MOD-0024 task (MOD-0357 S9)", () => {
    const requiredTask = (id, minutesPublished) => {
      const f = global.WorkCenterNextFixtureFactory;
      const fixture = JSON.parse(JSON.stringify(
        global.WorkCenterNextFixtures.canonical.find((item) => item.id === id)));
      fixture.reviewMeetingPolicy = { requirement: "required", meetingId: null, scheduledAt: null, minutesPublished };
      fixture.actions.push(f.action("scheduleReviewMeeting", { label: f.resource("ActReviewMeeting"), input: "meeting" }));
      fixture.overflowActionCodes = [...(fixture.overflowActionCodes || []), "scheduleReviewMeeting"];
      return fixture;
    };
    const withComplete = (fixture, complete) => {
      fixture.actions = fixture.actions.map((action) => (action.code === "complete" ? complete : action));
      return fixture;
    };
    const codes = (fixture) => global.WorkCenterNextContract.validateWorkItem(fixture).errors.map((e) => e.code);

    it("accepts an ENABLED start while the minutes are unpublished — start is not a decision", () => {
      const fixture = requiredTask("WC-TASK-PLANNED", false);
      expect(fixture.actions.find((action) => action.code === "start")).toMatchObject({ enabled: true });
      expect(global.WorkCenterNextContract.validateWorkItem(fixture)).toMatchObject({ valid: true });
    });

    it("accepts complete disabled with REVIEW_MEETING_REQUIRED while the minutes are unpublished", () => {
      const f = global.WorkCenterNextFixtureFactory;
      const fixture = withComplete(requiredTask("WC-TASK-ACTIVE-NO-TIMER", false),
        f.disabledAction("complete", "REVIEW_MEETING_REQUIRED", "ActionDisabledReviewMeetingRequired", { requiresConfirmation: true }));
      expect(global.WorkCenterNextContract.validateWorkItem(fixture)).toMatchObject({ valid: true });
    });

    it("accepts complete disabled for an EARLIER gate (approval wins the precedence)", () => {
      const f = global.WorkCenterNextFixtureFactory;
      const fixture = withComplete(requiredTask("WC-TASK-ACTIVE-NO-TIMER", false),
        f.disabledAction("complete", "APPROVAL_PENDING", "ActionDisabledApprovalPending", { requiresConfirmation: true }));
      expect(global.WorkCenterNextContract.validateWorkItem(fixture)).toMatchObject({ valid: true });
    });

    it("REJECTS an enabled complete while the minutes are unpublished", () => {
      const fixture = requiredTask("WC-TASK-ACTIVE-NO-TIMER", false);
      expect(fixture.actions.find((action) => action.code === "complete")).toMatchObject({ enabled: true });
      expect(codes(fixture)).toContain("REVIEW_MEETING_REQUIRED_MUST_BLOCK_DECISION");
    });

    it("REJECTS an enabled submitReview while the minutes are unpublished", () => {
      const f = global.WorkCenterNextFixtureFactory;
      const fixture = withComplete(requiredTask("WC-TASK-ACTIVE-NO-TIMER", false),
        f.action("submitReview", { requiresConfirmation: true }));
      fixture.primaryActionCode = "submitReview";
      expect(codes(fixture)).toContain("REVIEW_MEETING_REQUIRED_MUST_BLOCK_DECISION");
    });

    it("accepts an enabled complete once the minutes are published", () => {
      const fixture = requiredTask("WC-TASK-ACTIVE-NO-TIMER", true);
      expect(global.WorkCenterNextContract.validateWorkItem(fixture)).toMatchObject({ valid: true });
    });
  });
});
