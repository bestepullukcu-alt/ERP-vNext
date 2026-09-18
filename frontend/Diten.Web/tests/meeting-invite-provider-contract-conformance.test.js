const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

const scriptRoot = "wwwroot/assets/js/WorkCenterNext/";
const FIXTURE_PATH = path.resolve(__dirname, "fixtures", "meeting-invite-provider-projection.json");

/*
 * Go-live, 2026-09-13 — the JS half of the meeting-invite golden pair. The C# side
 * (MeetingInviteProviderContractGoldenTests) runs the REAL MeetingWorkItemProvider for one pending invitation across
 * {Read permission yes, no} × {no linked task, linked task} and writes each projection VERBATIM into this fixture;
 * this file runs every cell through the REAL validateWorkItem (fixture-contract.js), never a copy of its rules.
 *
 * Until this pair existed, the only meetingInvite the WC-1 validator had ever seen was hand-built in
 * workcenter-next-fixture-contract.test.js — the provider's real output was never checked against the contract.
 */
describe("Meeting invite provider — real validateWorkItem, 0 errors per cell", () => {
  let matrix;

  beforeAll(() => {
    matrix = JSON.parse(fs.readFileSync(FIXTURE_PATH, "utf8"));
  });

  beforeEach(() => {
    delete global.WorkCenterNextContract;
    global.WCN = { t: (key) => key };
    loadScript(scriptRoot + "fixture-contract.js");
  });

  it("the fixture carries all four cells, none of them empty — a vacuous pass would hide everything", () => {
    expect(Object.keys(matrix).sort()).toEqual([
      "invitee_none_linkedTask",
      "invitee_none_none",
      "invitee_read_linkedTask",
      "invitee_read_none"
    ]);
    Object.entries(matrix).forEach(([cell, projection]) => {
      expect(projection, `${cell} should be a projection`).toBeTruthy();
    });
  });

  it("every cell passes the real WC-1 contract with zero errors", () => {
    const failures = [];
    Object.entries(matrix).forEach(([cell, projection]) => {
      const verdict = global.WorkCenterNextContract.validateWorkItem(projection);
      if (!verdict.valid || verdict.errors.length > 0) {
        failures.push(`${cell}: ${JSON.stringify(verdict.errors)}`);
      }
    });
    expect(failures).toEqual([]);
  });

  it("every cell is a meeting invite answered only by accept or decline", () => {
    Object.entries(matrix).forEach(([cell, projection]) => {
      expect(projection.workIntent, cell).toBe("meetingInvite");
      expect(projection.actions.map((a) => a.code), cell).toEqual(["acceptInvite", "declineInvite"]);
      expect(projection.primaryActionCode, cell).toBe("acceptInvite");
      expect(projection.secondaryActionCodes, cell).toEqual(["declineInvite"]);
    });
  });
});
