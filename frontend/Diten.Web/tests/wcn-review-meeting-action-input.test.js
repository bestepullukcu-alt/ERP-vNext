const { SCRIPT_ROOT } = require("./wcn-boot");
const { loadScript } = require("./load-script");
const golden = require("./fixtures/task-provider-review-meeting-matrix.json");

/*
 * S10 LIVE PASS, 2026-09-13 — "Toplantı planla" in the Task Center did nothing but show a red toast.
 *
 * app.js opens the meeting scheduler only for an action whose presentation `input` is 'meeting'. The server's
 * WorkItemActionDto has no `input` field, and mock-data's mapper derived one only for `plan`, so a REAL
 * scheduleReviewMeeting action fell through to the generic dispatch: 400 WORK_ITEM_ACTION_UNKNOWN. The S4 test
 * that accepted this read the scheduler's SOURCE TEXT and never ran a real action through the mapper.
 *
 * This feeds the task provider's own golden output (TaskProviderContractGoldenTests pins that file against the
 * real C# provider) through the real presentation mapper, so the wiring is measured on what the server sends.
 */
describe("a real scheduleReviewMeeting action opens the meeting scheduler", () => {
  const load = () => {
    ["WorkCenterNextData", "WorkCenterNextApi", "WorkCenterNextContract", "WorkCenterNextFixtures"]
      .forEach((key) => { delete global[key]; });
    delete global.WCN;
    document.body.innerHTML = '<div id="wcnApp"></div>';
    loadScript(SCRIPT_ROOT + "l10n.js");
    Object.assign(global.WCN, { t: (key) => key, tf: (key) => key, tn: (key) => key });
    loadScript(SCRIPT_ROOT + "fixture-contract.js");
    loadScript(SCRIPT_ROOT + "task-detail-resolver.js");
    loadScript(SCRIPT_ROOT + "trigger-response-resolver.js");
    loadScript(SCRIPT_ROOT + "mock-data.js");
    return global.WorkCenterNextData;
  };

  it("maps the provider's enabled action to input 'meeting'", () => {
    const data = load();
    const cell = golden.holder_open_none_yes;
    expect(cell.actions.find((a) => a.code === "scheduleReviewMeeting").input).toBeUndefined(); // the wire has none

    const presented = data.toPresentation(cell, { provenance: "real" });
    const action = presented.actions.find((a) => a.code === "scheduleReviewMeeting");

    expect(action).toBeDefined();
    expect(action.input).toBe("meeting");
  });

  it("still maps plan to a date picker — the neighbouring rule is untouched", () => {
    const data = load();
    const presented = data.toPresentation(
      { ...golden.holder_open_none_yes, actions: [...golden.holder_open_none_yes.actions, { ...golden.holder_open_none_yes.actions[0], code: "plan" }] },
      { provenance: "real" });
    expect(presented.actions.find((a) => a.code === "plan").input).toBe("date");
  });
});
