const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

const scriptRoot = "wwwroot/assets/js/WorkCenterNext/";
const FIXTURE_PATH = path.resolve(__dirname, "fixtures", "task-provider-review-meeting-matrix.json");

/*
 * BL-379 — the JS half of the golden-file pair. The C# side (TaskProviderContractGoldenTests) runs the REAL
 * TaskWorkItemProvider across the {holder, requester, third party} × {open, closed} × {no meeting, meeting
 * linked} × {Update yes, no} matrix and writes each cell's VERBATIM projection into this fixture; this file runs
 * every present cell through the REAL validateWorkItem (fixture-contract.js), never a copy of its rules. A
 * contract violation the C# side cannot see on its own (a JS-only rule, or a rule that drifted between the two)
 * still fails here.
 *
 * A `null` cell (the task never reaches that actor at all — e.g. a closed task in someone else's Outbox-only
 * view, or a third party under Self scope) has nothing to validate; its own presence in the fixture, and its
 * being null, is what the C# side's golden comparison already locks down.
 */
describe("Task provider review-meeting matrix — real validateWorkItem, 0 errors per cell", () => {
  let matrix;

  beforeAll(() => {
    matrix = JSON.parse(fs.readFileSync(FIXTURE_PATH, "utf8"));
  });

  beforeEach(() => {
    delete global.WorkCenterNextContract;
    global.WCN = { t: (key) => key };
    loadScript(scriptRoot + "fixture-contract.js");
  });

  it("the fixture has at least one present cell to check — a vacuous pass would hide everything", () => {
    const present = Object.values(matrix).filter((projection) => projection !== null);
    expect(present.length).toBeGreaterThan(0);
  });

  it("every present cell passes the real WC-1 contract with zero errors", () => {
    const failures = [];
    Object.entries(matrix).forEach(([cell, projection]) => {
      if (projection === null) { return; }
      const verdict = global.WorkCenterNextContract.validateWorkItem(projection);
      if (!verdict.valid || verdict.errors.length > 0) {
        failures.push(`${cell}: ${JSON.stringify(verdict.errors)}`);
      }
    });
    expect(failures).toEqual([]);
  });

  it("a task closed or held by a third party carries no reviewMeetingPolicy and no scheduleReviewMeeting action", () => {
    // The specific invariant BL-379/CT's corrected design is about, asserted directly rather than only through
    // the generic contract pass above — a future relaxation of validateWorkItem would not by itself catch this.
    Object.entries(matrix).forEach(([cell, projection]) => {
      if (projection === null) { return; }
      const isClosed = cell.includes("_closed_");
      const isThirdParty = cell.startsWith("thirdParty_");
      if (!isClosed && !isThirdParty) { return; }
      expect(projection.reviewMeetingPolicy, `${cell} should carry no policy`).toBeFalsy();
      const action = (projection.actions || []).find((a) => a.code === "scheduleReviewMeeting");
      expect(action, `${cell} should offer no scheduleReviewMeeting action`).toBeUndefined();
    });
  });
});
