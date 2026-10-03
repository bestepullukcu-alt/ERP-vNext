const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * WP-MDM-ABB-ACCEPT-01 FIX1 C2 — THE TASK CENTER'S REAL BODY FOR AN ABB DECISION.
 *
 * The browser half of a cross-service contract. This test builds the body the Task Center posts for an approve on
 * an ABB item at version 0 — from app.js's own TRANSITION_BODIES and stableWorkItemActionIdentity, through
 * work-items-api.js's own dispatchAction — and pins it to the fixture file. The MDM half
 * (ProductAbbreviationWorkItemContractTests.The_Task_Centers_real_body_crosses_Platform_and_binds_in_MDM) reads THE
 * SAME FILE, sends it through Platform's real HttpWorkItemActionDispatcher and MDM's own model binding. A change to
 * either side's shape turns one of the two red; neither side keeps its own copy of the other.
 *
 * Two facts it holds: version 0 IS sent (a record that starts there is writable), and the idempotency key is sent
 * (MDM used to refuse it as an unknown field, so no ABB decision could cross at all).
 */
const APP = fs.readFileSync(
  path.resolve(__dirname, "..", "wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
const FIXTURE = path.resolve(__dirname, "fixtures", "abb-work-item-action-approve.wcn-body.json");
const ITEM_ID = "6b0f5d43-2c1e-4d8a-9a57-0c2f6f1e7a10";
const PROVIDER = "mdm-product-abbreviations";

const slice = (from, to, returns) => {
  const start = APP.indexOf(from);
  const end = APP.indexOf(to, start);
  expect(start).toBeGreaterThan(-1);
  expect(end).toBeGreaterThan(start);
  return Function(`${APP.slice(start, end)}; return ${returns};`)();
};

describe("the Task Center's approve body for an ABB item", () => {
  let posted;

  beforeEach(() => {
    posted = [];
    global.fetch = async (url, options) => {
      posted.push({ url, body: JSON.parse(options.body) });
      return { ok: true, status: 200, json: async () => ({ data: null }) };
    };
    loadScript("wwwroot/assets/js/WorkCenterNext/work-items-api.js");
  });

  it("is exactly the shared fixture MDM binds", async () => {
    const bodies = slice("const TRANSITION_BODIES", "const CLOSURE_DISPOSITIONS", "TRANSITION_BODIES");
    const { stableWorkItemActionIdentity } = slice(
      "const hashActionAttemptTuple", "const submitRealTransition", "{ stableWorkItemActionIdentity }");
    const item = { id: ITEM_ID, source: { providerCode: PROVIDER }, concurrency: { kind: "version", token: "0" } };
    const expectedVersion = Number(item.concurrency.token);
    const idempotencyKey = stableWorkItemActionIdentity(item.source.providerCode, item.id, "approve", expectedVersion);
    // The assembly submitRealTransition performs (pinned to its source below).
    const body = {
      ...(bodies.approve || bodies.__default)({ expectedVersion, reason: "", outcomeCode: undefined, closureFieldValues: undefined }),
      ...(idempotencyKey ? { idempotencyKey } : {})
    };

    await global.WorkCenterNextApi.dispatchAction(item.id, "approve", item.source.providerCode, body);

    expect(posted).toHaveLength(1);
    expect(posted[0].body).toEqual(JSON.parse(fs.readFileSync(FIXTURE, "utf8")));
    expect(posted[0].body.payload.expectedVersion).toBe(0);
    expect(posted[0].body.payload.idempotencyKey).toMatch(/^wcn-action-v1:/);
  });

  it("is assembled by submitRealTransition the way this test assembles it", () => {
    const submit = APP.slice(APP.indexOf("const submitRealTransition"), APP.indexOf("const applyAction"));
    expect(submit).toContain("const expectedVersion = Number(projectedToken ?? 0);");
    expect(submit).toContain("stableWorkItemActionIdentity(item.source?.providerCode, item.id, action.code, expectedVersion)");
    expect(submit).toContain("...(idempotencyKey ? { idempotencyKey } : {})");
    expect(APP).toContain("(TRANSITION_BODIES[actionCode] || TRANSITION_BODIES.__default)(parts)");
  });
});
