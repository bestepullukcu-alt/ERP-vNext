const fs = require("fs");
const path = require("path");

/*
 * S10B LIVE PASS, 2026-09-13 — after BL-381 routed "schedule review meeting" to the scheduler, the dialog still never
 * opened: neither Task Center host view loaded assets/js/Meetings/api.js, so openMeetingScheduler threw
 * "Cannot read properties of undefined (reading 'lookupTypes')" inside a promise and nothing reached the screen.
 * Every jsdom test stubbed MeetingsApi itself, which is exactly why none of them could see the missing script.
 */
const root = path.resolve(__dirname, "..");
const read = (rel) => fs.readFileSync(path.join(root, rel), "utf8");

describe("the Task Center host views load everything app.js writes through", () => {
  it.each(["Views/WorkCenterNext/Index.cshtml", "Views/WorkCenterNext/Details.cshtml"])(
    "%s loads Meetings/api.js before app.js", (view) => {
      const html = read(view);
      const meetings = html.indexOf("assets/js/Meetings/api.js");
      const app = html.indexOf("assets/js/WorkCenterNext/app.js");
      expect(meetings, "Meetings/api.js is not loaded").toBeGreaterThan(-1);
      expect(meetings, "Meetings/api.js must load before app.js").toBeLessThan(app);
    });

  it("names MeetingsApi as a write dependency, so a view that forgets it is reported at boot", () => {
    const app = read("wwwroot/assets/js/WorkCenterNext/app.js");
    const line = app.split("\n").find((l) => l.includes("const WRITE_DEPENDENCIES"));
    expect(line).toContain("'MeetingsApi'");
  });
});
