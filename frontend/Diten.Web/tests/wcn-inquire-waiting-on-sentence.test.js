const fs = require("fs");
const path = require("path");

/*
 * ══ BL-351 — THE "WAITING ON" REFUSAL READS ITS OWN SENTENCE, AT THE ONE PLACE INQUIRE IS SENT ══════════
 *
 * THE MEASURED DEFECT. The server answers TASK_ASSIGNEE_NOT_ASSIGNABLE for two different refusals — the
 * assignment guard's ("this person cannot be assigned work") and InquireTaskItemHandler's ("this person cannot
 * be waited on"). Tasks/api.js mapped that one code twice in the same object literal; JavaScript kept the LAST
 * value, so the waiting-on refusal read the assignment sentence and ErrorWaitingOnNotAssignable, present in all
 * seven languages, was never shown to anybody.
 *
 * The fix has two halves and this file guards the second: api.js now carries INQUIRE_REASON_CODE_OVERRIDES
 * (guarded by task-assignee-not-assignable-message-split.test.js), and the ONLY place the shell dispatches
 * `inquire` — submitRealTransition in WorkCenterNext/app.js — must pass it. An override map nobody passes is
 * the same defect wearing a comment.
 */
const APP = fs.readFileSync(
  path.resolve(__dirname, "..", "wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");

const submitRealTransition = () => {
  const start = APP.indexOf("const submitRealTransition = async (");
  expect(start).toBeGreaterThan(-1);
  const end = APP.indexOf("── Phase 2 writes", start);
  expect(end).toBeGreaterThan(start);
  return APP.slice(start, end);
};

describe("BL-351: submitRealTransition asks for the waiting-on sentence when the action is inquire", () => {
  it("derives the override from the action code, and only for inquire", () => {
    expect(submitRealTransition()).toMatch(
      /const overrides = action\.code === 'inquire' \? global\.TasksApi\.INQUIRE_REASON_CODE_OVERRIDES : undefined;/);
  });

  it("passes the override to EVERY failureMessage call in the function — none is left on the base map alone", () => {
    const fn = submitRealTransition();
    const calls = fn.match(/failureMessage\([^)]*\)/g) || [];
    expect(calls.length).toBeGreaterThanOrEqual(2);
    for (const call of calls) {
      expect(call).toBe("failureMessage(result, overrides)");
    }
  });

  it("is the only dispatcher of inquire, so one wiring is the whole fix", () => {
    // TRANSITION_BODIES.inquire is the request shape; the function under test is the only caller that submits it.
    expect(APP).toMatch(/^\s*inquire: \(\{ expectedVersion, reason, waitingOnUserId \}\) =>/m);
    const dispatchers = (APP.match(/const submit\w+Transition = async \(/g) || []);
    expect(dispatchers).toEqual(["const submitRealTransition = async ("]);
  });
});
