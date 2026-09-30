const { loadScript } = require("./load-script");
const { installNetwork, ok, flush, readSource, TASK_A } = require("./time-entry-harness");

/*
 * MOD-0280-FU01 T2a (pack §21.2 U1) — the top-bar timer chip (shared/timer-chip.js), the real script on the partial's
 * markup. It exists only while a timer runs; the timer switched off for the legal entity → not drawn at all.
 */

const CHIP_L10N = JSON.stringify({
  UnreadableTask: "UnreadableTask", OpenTask: "OpenTask", TimerStopped: "TimerStopped", TimerStopFailed: "TimerStopFailed",
  MidnightNotice: "MidnightNotice", CategoryLabels: { "TimeEntry.Category.ADMINISTRATION": "Administration" }
});

function chipHtml(user = "user-1") {
  return `<ul><li class="time-entry-timer-chip" id="timeEntryTimerChip" hidden data-user="${user}" data-l10n='${CHIP_L10N}'>
    <span class="time-entry-timer-chip-body">
      <a data-timer-link href="/TimeEntry"><span data-timer-title></span><span data-timer-elapsed></span></a>
      <button type="button" data-timer-stop>Stop</button>
    </span></li></ul>`;
}

const timer = (overrides = {}) => Object.assign({ timerEnabled: true, disabledReason: null, running: null, closedAtMidnightYesterday: [] }, overrides);
const runningOn = (extra = {}) => Object.assign({
  segmentId: "seg-1", taskItemId: TASK_A, categoryCode: null, startedAtUtc: new Date(Date.now() - 65 * 60 * 1000).toISOString(),
  localDate: "2026-10-07", startSource: "TaskStarted", switchToken: null, undoUntilUtc: null, taskTitle: "Deviation DEV-17"
}, extra);

async function bootChip(routes, { user = "user-1" } = {}) {
  document.body.innerHTML = chipHtml(user);
  window.showToast = vi.fn();
  window.__timerChipNoAutoInit = true;
  delete window.DitenTimerChip;
  const calls = installNetwork(routes);
  loadScript("wwwroot/assets/js/shared/timer-chip.js");
  await window.DitenTimerChip.init();
  await flush();
  return calls;
}

const chip = () => document.getElementById("timeEntryTimerChip");

/** This jsdom has no localStorage; the browser's is replaced by an in-memory one the test can also make throw. */
function installStorage({ throwing = false } = {}) {
  const data = new Map();
  const storage = {
    getItem: (k) => { if (throwing) { throw new Error("blocked"); } return data.has(k) ? data.get(k) : null; },
    setItem: (k, v) => { if (throwing) { throw new Error("blocked"); } data.set(k, String(v)); },
    removeItem: (k) => data.delete(k),
    clear: () => data.clear()
  };
  Object.defineProperty(window, "localStorage", { value: storage, configurable: true, writable: true });
  return storage;
}

beforeEach(() => installStorage());

afterEach(() => {
  vi.useRealTimers();
});

describe("the chip exists only while a timer runs", () => {
  it("is not shown when no timer runs", async () => {
    await bootChip([["GET", "/TimeEntry/api/timer", () => ok(timer())]]);
    expect(chip()).not.toBeNull();
    expect(chip().hidden).toBe(true);
  });

  it("shows the task, the elapsed time from the SERVER's start, and links to the task in the Task Center", async () => {
    await bootChip([["GET", "/TimeEntry/api/timer", () => ok(timer({ running: runningOn() }))]]);
    expect(chip().hidden).toBe(false);
    expect(chip().querySelector("[data-timer-title]").textContent).toBe("Deviation DEV-17");
    expect(chip().querySelector("[data-timer-elapsed]").textContent).toMatch(/^1:0[45]:\d\d$/);
    expect(chip().querySelector("[data-timer-link]").getAttribute("href")).toBe(`/WorkCenterNext/Details/${TASK_A}`);
  });

  it("names an inaccessible task neutrally and a category by its label", async () => {
    await bootChip([["GET", "/TimeEntry/api/timer", () => ok(timer({ running: runningOn({ taskTitle: null }) }))]]);
    expect(chip().querySelector("[data-timer-title]").textContent).toBe("UnreadableTask");

    await bootChip([
      ["GET", "/TimeEntry/api/timer", () => ok(timer({ running: runningOn({ taskItemId: null, categoryCode: "ADMINISTRATION", taskTitle: null }) }))],
      ["GET", "/TimeEntry/api/categories", () => ok([{ code: "ADMINISTRATION", labelText: null, labelResourceKey: "TimeEntry.Category.ADMINISTRATION" }])]
    ]);
    expect(chip().querySelector("[data-timer-title]").textContent).toBe("Administration");
    expect(chip().querySelector("[data-timer-link]").getAttribute("href")).toBe("/TimeEntry");
  });

  it("Stop posts to the timer and takes the chip away", async () => {
    const calls = await bootChip([
      ["GET", "/TimeEntry/api/timer", () => ok(timer({ running: runningOn() }))],
      ["POST", "/TimeEntry/api/timer/stop", () => ok({ timer: timer(), stoppedSegmentId: "seg-1" })]
    ]);
    chip().querySelector("[data-timer-stop]").click();
    await flush();
    expect(calls.some((c) => c.method === "POST" && c.url === "/TimeEntry/api/timer/stop")).toBe(true);
    expect(chip().hidden).toBe(true);
    expect(window.showToast).toHaveBeenCalledWith("TimerStopped", "success");
  });

  it("with the timer switched off for the legal entity the chip is not drawn at all", async () => {
    await bootChip([["GET", "/TimeEntry/api/timer", () => ok(timer({ timerEnabled: false, disabledReason: "TIMER_DISABLED_FOR_LEGAL_ENTITY" }))]]);
    expect(chip()).toBeNull();
  });

  it("a failed read draws nothing either", async () => {
    await bootChip([["GET", "/TimeEntry/api/timer", () => ({ status: 403, body: {} })]]);
    expect(chip()).toBeNull();
  });

  it("asks the server again when the tab becomes visible — and never polls", async () => {
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    const calls = await bootChip([["GET", "/TimeEntry/api/timer", () => ok(timer({ running: runningOn() }))]]);
    const reads = () => calls.filter((c) => c.url === "/TimeEntry/api/timer").length;
    expect(reads()).toBe(1);
    vi.advanceTimersByTime(10 * 60 * 1000);
    expect(reads()).toBe(1);
    Object.defineProperty(document, "visibilityState", { value: "visible", configurable: true });
    document.dispatchEvent(new Event("visibilitychange"));
    await flush();
    expect(reads()).toBe(2);
  });
});

describe("the midnight notice", () => {
  const closed = [{ segmentId: "seg-0", localDate: "2026-10-06", taskItemId: TASK_A, categoryCode: null, durationSeconds: 3600 }];

  it("is shown once a day per person, not on every page", async () => {
    await bootChip([["GET", "/TimeEntry/api/timer", () => ok(timer({ closedAtMidnightYesterday: closed }))]]);
    expect(window.showToast).toHaveBeenCalledWith("MidnightNotice", "warning");

    await bootChip([["GET", "/TimeEntry/api/timer", () => ok(timer({ closedAtMidnightYesterday: closed }))]]);
    expect(window.showToast).not.toHaveBeenCalled();

    // another person in the same browser is told for themselves
    await bootChip([["GET", "/TimeEntry/api/timer", () => ok(timer({ closedAtMidnightYesterday: closed }))]], { user: "user-2" });
    expect(window.showToast).toHaveBeenCalledWith("MidnightNotice", "warning");
  });

  it("survives a browser that refuses storage", async () => {
    installStorage({ throwing: true });
    await bootChip([["GET", "/TimeEntry/api/timer", () => ok(timer({ closedAtMidnightYesterday: closed, running: runningOn() }))]]);
    expect(chip().hidden).toBe(false);
    expect(window.showToast).toHaveBeenCalledWith("MidnightNotice", "warning"); // told (again) rather than crashing
  });
});

describe("the partial and the shell", () => {
  it("renders only for a person who may use the timer, starts hidden, and the shell includes it on ONE line", () => {
    const partial = readSource("Views/Shared/_TimerChip.cshtml");
    expect(partial).toContain('@if (Perms.Has("time-entry.timesheets.update"))');
    expect(partial).toMatch(/id="timeEntryTimerChip" hidden/);
    const layout = readSource("Views/Shared/_LayoutTenantShell.cshtml");
    expect(layout.match(/_TimerChip/g)).toHaveLength(1);
    // next to the notification bell
    expect(layout.indexOf('<partial name="_TimerChip" />')).toBeLessThan(layout.indexOf("<!-- Notification -->"));
    expect(layout.indexOf("<!-- Notification -->") - layout.indexOf('<partial name="_TimerChip" />')).toBeLessThan(200);
  });
});
