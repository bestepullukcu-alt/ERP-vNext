const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

const scriptRoot = "wwwroot/assets/js/WorkCenterNext/";
const FIXTURE_PATH = path.resolve(__dirname, "fixtures", "task-inquiry-provider-projection.json");

/*
 * BL-439 — the JS half of the inquiry golden pair. The C# side (TaskInquiryProviderContractGoldenTests) runs the
 * REAL TaskWorkItemProvider for a waiting task — as its addressee sees it (with and without the read key) and as
 * its holder sees it once the answer is in — and writes each projection VERBATIM into this fixture. Every cell
 * goes through the REAL validateWorkItem here, never a copy of its rules: validateItems DROPS what it rejects, so
 * a contract error is a question that silently never reaches the person it is for.
 */
describe("Task inquiry — real validateWorkItem, 0 errors per cell", () => {
  let matrix;

  beforeAll(() => {
    matrix = JSON.parse(fs.readFileSync(FIXTURE_PATH, "utf8"));
  });

  beforeEach(() => {
    delete global.WorkCenterNextContract;
    global.WCN = { t: (key) => key };
    loadScript(scriptRoot + "fixture-contract.js");
  });

  it("carries all three cells, none of them empty — a vacuous pass would hide everything", () => {
    expect(Object.keys(matrix).sort()).toEqual(["asked_none", "asked_read", "holder_answered"]);
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

  it("the addressee's cells are a question answered only by `answer`", () => {
    ["asked_read", "asked_none"].forEach((cell) => {
      const projection = matrix[cell];
      expect(projection.workIntent, cell).toBe("inquiry");
      expect(projection.actions.map((a) => a.code), cell).toEqual(["answer"]);
      expect(projection.primaryActionCode, cell).toBe("answer");
      expect(projection.summary.kind, cell).toBe("display");
    });
    // Without the read key the button is still THERE — disabled, with its reason — never silently missing.
    expect(matrix.asked_none.actions[0].enabled).toBe(false);
    expect(matrix.asked_none.actions[0].disabledReasonCode).toBe("PERMISSION_DENIED");
  });

  it("the holder's cell is the task itself, carrying who answered", () => {
    const projection = matrix.holder_answered;
    expect(projection.workIntent).toBe("task");
    expect(projection.inquiryAnswer.answeredBy.displayName).toBe("Ayşe Yılmaz");
    expect(projection.inquiryAnswer.answer.text).toBe("Cuma günü tedarikçiden.");
    expect(projection.activity.map((entry) => entry.event && entry.event.code)).toContain("inquiryAnswered");
  });

  /*
   * MUTATION GUARD — the contract must actually police the new shape, or the cells above pass because nothing
   * looked. Each breakage below is one a provider could plausibly ship.
   */
  it("rejects an inquiry that grew a second action, lost its primary, or carries a malformed answer block", () => {
    const validate = (projection) => global.WorkCenterNextContract.validateWorkItem(projection);
    const clone = (value) => JSON.parse(JSON.stringify(value));

    const twoActions = clone(matrix.asked_read);
    twoActions.actions.push(Object.assign(clone(twoActions.actions[0]), { code: "complete" }));
    expect(validate(twoActions).errors.map((e) => e.code)).toContain("INQUIRY_ACTIONS_INVALID");

    const noPrimary = clone(matrix.asked_read);
    noPrimary.primaryActionCode = null;
    expect(validate(noPrimary).errors.map((e) => e.code)).toContain("INQUIRY_PRIMARY_ACTION_INVALID");

    const badAnswer = clone(matrix.holder_answered);
    badAnswer.inquiryAnswer.at = "not a date";
    expect(validate(badAnswer).errors.map((e) => e.code)).toContain("INQUIRY_ANSWER_AT_INVALID");
  });
});
