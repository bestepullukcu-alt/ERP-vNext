const fs = require("fs");
const path = require("path");

/*
 * ══ MOD-0357 S4 — THE TASK CENTER'S "SCHEDULE A REVIEW MEETING" IS A REAL CALL, NOT THE OLD MOCK ═══════════
 *
 * Until S4, `scheduleReviewMeeting` in WorkCenterNext/app.js was a showcase: a Swal presence-check, a made-up
 * `MTG-<id>` meeting id stamped onto the item, no request anywhere. S4 replaced it with the receiving side —
 * MeetingsApi.scheduleReviewMeetingForTask → POST api/v1/meetings/tasks/{taskId}/schedule-review-meeting — and
 * kept the mock ONLY for showcase fixtures (`isFixtureShowcase(item)`, which have no record to write).
 *
 * The agent that built S4 measured that restoring the old `if (!global.Swal)` short-circuit turned NO existing
 * test red (its report, §3(d)). This file is that missing guard. It reads the scheduler's own source, because the
 * defect is structural: a real task quietly taking the fixture path looks, on screen, exactly like success.
 */
const APP = fs.readFileSync(
  path.resolve(__dirname, "..", "wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");

const scheduler = () => {
  const start = APP.indexOf("const openMeetingScheduler = ");
  expect(start).toBeGreaterThan(-1);
  const end = APP.indexOf("const openLogTime = ", start);
  expect(end).toBeGreaterThan(start);
  return APP.slice(start, end);
};

describe("MOD-0357 S4: the review-meeting action reaches the real receiving endpoint", () => {
  it("calls MeetingsApi.scheduleReviewMeetingForTask for a real task", () => {
    // The scheduler collects type + date/time through the shared dialogs and hands off to `submitReviewMeeting`, which is
    // where the request is actually sent; the scheduler must reach that helper, and the helper must make the call.
    expect(scheduler()).toMatch(/\bsubmitReviewMeeting\(/);
    const start = APP.indexOf("const submitReviewMeeting = ");
    expect(start).toBeGreaterThan(-1);
    const helperBody = APP.slice(start, start + 4000);
    expect(helperBody).toMatch(/global\.MeetingsApi\.scheduleReviewMeetingForTask\(/);
  });

  it("routes ONLY showcase fixtures to the fixture apply, and decides that before anything real happens", () => {
    const src = scheduler();
    const realStart = src.indexOf("global.MeetingsApi.lookupTypes()");
    expect(realStart).toBeGreaterThan(-1);
    const gate = src.slice(0, realStart);
    const real = src.slice(realStart);
    // The fixture branch is the FIRST thing the scheduler does, and it is the only place the fixture apply is called.
    expect(gate).toMatch(/isFixtureShowcase\(item\)/);
    expect(real).not.toMatch(/applyReviewMeetingFixture\(/);
  });

  it("never short-circuits on Swal's presence and never fabricates a meeting id", () => {
    const src = scheduler();
    expect(src, "the old `if (!global.Swal)` mock path came back").not.toMatch(/global\.Swal/);
    expect(src, "a fabricated MTG- meeting id came back").not.toMatch(/MTG-/);
    expect(src).not.toMatch(/Swal\.fire\(/);
  });

  it("the old real-item mock body no longer exists anywhere in app.js", () => {
    // Only the fixture-scoped variant may remain.
    expect(APP).not.toMatch(/const applyReviewMeeting = /);
    expect(APP).toMatch(/const applyReviewMeetingFixture = /);
  });
});
