const { loadScript } = require("./load-script");

const root = "wwwroot/assets/js/WorkCenterNext/";

describe("WorkCenterNext resolvers", () => {
  beforeEach(() => {
    [
      "WorkCenterNextContract", "WorkCenterNextFixtureFactory", "WorkCenterNextFixtures",
      "WorkCenterNextTaskDetailResolver", "WorkCenterNextTriggerResponseResolver"
    ].forEach((name) => delete global[name]);
    [
      "fixture-contract.js",
      "fixtures/canonical-fixtures.js",
      "fixtures/inbox-showcase-fixtures.js",
      "fixtures/edge-case-fixtures.js",
      "fixtures/provider-examples/enterprise-strategy-fixtures.js",
      "fixtures/provider-examples/documentation-fixtures.js",
      "fixtures/trigger-only-fixtures.js",
      "task-detail-resolver.js",
      "trigger-response-resolver.js"
    ].forEach((file) => loadScript(root + file));
  });

  it("projects effective actions by code without changing action content", () => {
    const fixture = global.WorkCenterNextFixtures.canonical.find((item) => item.id === "WC-APPROVAL-SIMPLE");
    const surface = global.WorkCenterNextTaskDetailResolver.resolveTaskDetailSurface(fixture);
    expect(surface.primaryActionCode).toBe("approve");
    expect(surface.secondaryActionCodes).toEqual(["requestInfo"]);
    expect(surface.overflowActionCodes).toEqual(["reject"]);
    expect(fixture.actions.find((action) => action.code === "approve").enabled).toBe(true);
  });

  it("uses a single critical banner by safety precedence", () => {
    const fixture = global.WorkCenterNextFixtures.edgeCases.find((item) => item.id === "WC-EDGE-AUTHORITY");
    const surface = global.WorkCenterNextTaskDetailResolver.resolveTaskDetailSurface(fixture);
    expect(surface.readOnly).toBe(true);
    expect(surface.criticalBanner.code).toBe("authorityEnded");
  });

  it("never sends trigger-only fixtures into Task Detail", () => {
    const trigger = global.WorkCenterNextFixtures.triggerOnly[0];
    expect(global.WorkCenterNextTaskDetailResolver.resolveTaskDetailSurface(trigger).invalid).toBe(true);
    expect(global.WorkCenterNextTriggerResponseResolver.resolveTriggerResponse(trigger)).toMatchObject({
      invalid: false,
      surfaceMode: "triggerResponse",
      primaryActionCode: "acceptMeeting"
    });
  });

  it("matches every declared fixture expectation without mutating actions", () => {
    const groups = global.WorkCenterNextFixtures;
    ["canonical", "inboxShowcase", "edgeCases", "enterpriseStrategy", "documentation"].forEach((group) => {
      groups[group].forEach((fixture) => {
        const before = JSON.stringify(fixture.actions);
        const surface = global.WorkCenterNextTaskDetailResolver.resolveTaskDetailSurface(fixture);
        Object.entries(fixture.expectation || {}).forEach(([key, expected]) => {
          if (key === "noticeCodes") {
            expect(surface.notices.map((notice) => notice.code)).toEqual(expect.arrayContaining(expected));
          } else if (key === "criticalBannerCode") {
            expect(surface.criticalBanner?.code).toBe(expected);
          } else {
            expect(surface[key]).toEqual(expected);
          }
        });
        expect(JSON.stringify(fixture.actions)).toBe(before);
      });
    });
  });
});
