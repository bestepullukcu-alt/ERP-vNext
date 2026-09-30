const { loadScript } = require("./load-script");
const {
  weekPayload, entry, installNetwork, ok, refused, flush, bootPage, readSource, TASK_A, TASK_B, TASK_C
} = require("./time-entry-harness");

/*
 * MOD-0280-FU01 T2a — CT acceptance round (P-2026-09-30-01 v3): M1–M5, L7, L8, L10. The page's and the chip's real
 * scripts; only the network is fake.
 */

const WEEK = "/TimeEntry/api/weeks/2026-W41";
const TIMER_OFF = { timerEnabled: false, disabledReason: "TIMER_DISABLED_FOR_LEGAL_ENTITY", running: null, closedAtMidnightYesterday: [] };

function routes(payload, extra = []) {
  return extra.concat([
    ["GET", /\/TimeEntry\/api\/weeks\/2026-W41$/, () => ok(payload)],
    ["GET", "/TimeEntry/api/categories", () => ok([{ code: "ADMINISTRATION", labelText: "Administration", isActive: true }])],
    ["GET", "/TimeEntry/api/timer", () => ok(TIMER_OFF)]
  ]);
}

const oneRow = () => weekPayload({ entries: [entry("2026-10-05", 60, { taskItemId: TASK_A, taskTitle: "A" })] });
const grid = () => document.getElementById("teGrid");
const input = (date, host = "#teGrid") => document.querySelector(`${host} input[data-row-key][data-date="${date}"]`);
const notice = () => document.getElementById("teNotice");
const type = (node, value) => { node.value = value; node.dispatchEvent(new Event("change")); };

describe("M1 — notices are VISIBLE, and nothing is emptied by a rounding", () => {
  it("the status line is visible and live, not a screen-reader-only region", async () => {
    installNetwork(routes(oneRow()));
    await bootPage({ week: "2026-W41" });
    type(input("2026-10-06"), "1:20");
    await flush();
    expect(notice().textContent).toBe("RoundedTo");
    expect(notice().classList.contains("visually-hidden")).toBe(false);
    expect(notice().hidden).toBe(false);
    const view = readSource("Views/TimeEntry/Index.cshtml");
    expect(view).toContain('<div class="time-entry-notice" id="teNotice" role="status" aria-live="polite"></div>');
    expect(view).not.toMatch(/visually-hidden[^>]*id="teNotice"/);
  });

  it.each(["7dk", "0:05", "5m"])("%s would round to zero: refused, the cell keeps 1:00, and the page says why", async (typed) => {
    installNetwork(routes(oneRow()));
    await bootPage({ week: "2026-W41" });
    type(input("2026-10-05"), typed);
    await flush();
    expect(window.TimeEntryPage.state().rows[0].cells["2026-10-05"].minutes).toBe(60);
    expect(input("2026-10-05").value).toBe("1:00");
    expect(notice().textContent).toBe("NoticeTooSmall");
    expect(notice().className).toContain("time-entry-notice-warning");
  });

  it.each(["0", ""])("only an explicit %j clears the cell", async (typed) => {
    installNetwork(routes(oneRow()));
    await bootPage({ week: "2026-W41" });
    type(input("2026-10-05"), typed);
    await flush();
    expect(window.TimeEntryPage.state().rows[0].cells["2026-10-05"].minutes).toBe(0);
  });

  it("a bare number of 10 or more is read as hours AND the page says so (15 → 15:00)", async () => {
    installNetwork(routes(oneRow()));
    await bootPage({ week: "2026-W41" });
    type(input("2026-10-06"), "15");
    await flush();
    expect(window.TimeEntryPage.state().rows[0].cells["2026-10-06"].minutes).toBe(900);
    expect(notice().textContent).toBe("BareHoursRead");

    type(input("2026-10-07"), "8");
    await flush();
    expect(notice().textContent).toBe("");
  });
});

describe("M2 — the focus survives the rebuild", () => {
  const twoRows = () => weekPayload({
    entries: [entry("2026-10-05", 60, { taskItemId: TASK_A, taskTitle: "A" }), entry("2026-10-05", 30, { taskItemId: TASK_B, taskTitle: "B" })]
  });
  const pos = (p) => grid().querySelector(`[data-pos="${p}"]`);

  // jsdom does not move the focus on a Tab key; the browser's order is: change on the old cell, then focus on the new
  // one. The test does exactly that — the page's rebuild runs after it, and must put the focus back.
  async function leaveTo(from, to) {
    const source = pos(from);
    source.focus();
    source.value = "2:00";
    source.dispatchEvent(new Event("change"));
    const target = pos(to);
    target.focus();
    await flush();
    return target;
  }

  it("Tab: the next cell keeps the focus after the grid is rebuilt", async () => {
    installNetwork(routes(twoRows()));
    await bootPage({ week: "2026-W41" });
    const before = await leaveTo("0:0", "0:1");
    expect(before.isConnected).toBe(false); // the grid WAS rebuilt
    expect(document.activeElement.getAttribute("data-pos")).toBe("0:1");
    expect(document.activeElement.isConnected).toBe(true);
    expect(window.TimeEntryPage.state().rows[0].cells["2026-10-05"].minutes).toBe(120);
  });

  it("Shift+Tab: the previous cell keeps the focus", async () => {
    installNetwork(routes(twoRows()));
    await bootPage({ week: "2026-W41" });
    await leaveTo("1:2", "1:1");
    expect(document.activeElement.getAttribute("data-pos")).toBe("1:1");
    expect(document.activeElement.isConnected).toBe(true);
  });

  it("a click on another row's cell keeps the focus there", async () => {
    installNetwork(routes(twoRows()));
    await bootPage({ week: "2026-W41" });
    const source = pos("0:1");
    source.focus();
    source.value = "0:45";
    source.dispatchEvent(new Event("change"));
    const target = pos("1:2");
    target.focus();
    target.click();
    await flush();
    expect(document.activeElement.getAttribute("data-pos")).toBe("1:2");
    expect(document.activeElement.isConnected).toBe(true);
  });

  it("the phone's day list keeps the focus too", async () => {
    installNetwork(routes(twoRows()));
    await bootPage({ week: "2026-W41" });
    document.querySelector('#teDayView [data-day="2026-10-05"]').click();
    const inputs = document.querySelectorAll("#teDayView input");
    inputs[0].focus();
    inputs[0].value = "1:30";
    inputs[0].dispatchEvent(new Event("change"));
    const key = inputs[1].getAttribute("data-row-key");
    inputs[1].focus();
    await flush();
    expect(document.activeElement.closest("#teDayView")).not.toBeNull();
    expect(document.activeElement.getAttribute("data-row-key")).toBe(key);
    expect(document.activeElement.isConnected).toBe(true);
  });
});

describe("M3 — unsaved edits are never thrown away", () => {
  it("a refused save during 'Review suggestions' does NOT reload: the edits stay and the page says so", async () => {
    const payload = weekPayload({
      entries: [entry("2026-10-05", 60, { taskItemId: TASK_A, taskTitle: "A" })],
      suggestions: [{ id: "s-1", meetingId: "m-1", title: "QA sync", localDate: "2026-10-06", proposedMinutes: 60, state: "Open", minutesStatus: "none" }]
    });
    const calls = installNetwork(routes(payload, [
      ["GET", `${WEEK}/plan-fill-in`, () => ok({ rows: [{ localDate: "2026-10-06", taskItemId: TASK_C, durationMinutes: 45, taskTitle: "C" }] })],
      ["PUT", `${WEEK}/entries`, () => refused(409, "TIMESHEET_CONCURRENCY_CONFLICT")]
    ]));
    await bootPage({ week: "2026-W41" });
    type(input("2026-10-06"), "2:00");
    await flush();
    await window.TimeEntryPage.fillFromPlan();
    document.getElementById("teReviewSuggestions").click();
    document.querySelectorAll("#teReview input[type=checkbox]").forEach((b) => { b.checked = true; });

    const weekReads = () => calls.filter((c) => c.method === "GET" && /weeks\/2026-W41$/.test(c.url)).length;
    const readsBefore = weekReads();
    await window.TimeEntryPage.addSelectedSuggestions();
    await flush();

    expect(weekReads()).toBe(readsBefore);
    expect(calls.some((c) => c.url.endsWith("/suggestions/s-1/accept"))).toBe(false);
    const rows = window.TimeEntryPage.state().rows;
    expect(rows.find((r) => r.taskItemId === TASK_A).cells["2026-10-06"].minutes).toBe(120);
    expect(rows.find((r) => r.taskItemId === TASK_C).cells["2026-10-06"].minutes).toBe(45);
    expect(notice().textContent).toBe("SaveFailedKept");
    expect(window.showToast).toHaveBeenCalledWith("ErrConcurrencyConflict", "error");
  });

  it("the browser's Back to another week asks first (in the page) and stays put on 'no'", async () => {
    const calls = installNetwork(routes(oneRow(), [["GET", /weeks\/2026-W40$/, () => ok(weekPayload({ weekKey: "2026-W40" }))]]));
    await bootPage({ week: "2026-W41" });
    window.showConfirm = vi.fn(); // the person says "no": the callback never runs
    type(input("2026-10-06"), "1:00");
    await flush();

    window.dispatchEvent(new PopStateEvent("popstate", { state: { week: "2026-W40" } }));
    await flush();
    expect(window.showConfirm).toHaveBeenCalledWith("DiscardChangesConfirm", expect.any(Function));
    expect(calls.some((c) => /weeks\/2026-W40$/.test(c.url))).toBe(false);
    expect(window.TimeEntryPage.state().weekKey).toBe("2026-W41");

    window.showConfirm.mock.calls[0][1]();
    await flush();
    expect(window.TimeEntryPage.state().weekKey).toBe("2026-W40");
  });

  it("closing the page with unsaved edits raises the browser's own leave warning; a clean page does not", async () => {
    installNetwork(routes(oneRow()));
    await bootPage({ week: "2026-W41" });
    const clean = new Event("beforeunload", { cancelable: true });
    window.dispatchEvent(clean);
    expect(clean.defaultPrevented).toBe(false);

    type(input("2026-10-06"), "1:00");
    await flush();
    const leaving = new Event("beforeunload", { cancelable: true });
    window.dispatchEvent(leaving);
    expect(leaving.defaultPrevented).toBe(true);
  });
});

describe("M4 — rows without time are marked, kept across a reload, and warned about", () => {
  async function withPendingRow(extraRoutes = []) {
    const calls = installNetwork(routes(oneRow(), extraRoutes.concat([
      ["GET", "/TimeEntry/api/task-options", () => ok([{ taskItemId: TASK_C, title: "Line clearance", status: "InProgress" }])]
    ])));
    await bootPage({ week: "2026-W41" });
    window.TimeEntryPage.addTaskRow({ taskItemId: TASK_C, title: "Line clearance" });
    return calls;
  }

  it("marks a row with no time as pending, on the row itself", async () => {
    await withPendingRow();
    const row = grid().querySelector(`tr[data-row-key^="Manual|${TASK_C}"]`);
    expect(row.classList.contains("time-entry-row-pending")).toBe(true);
    expect(row.querySelector(".time-entry-pending").textContent).toBe("PendingRow");
    expect(grid().querySelector(`tr[data-row-key^="Manual|${TASK_A}"] .time-entry-pending`)).toBeNull();
  });

  it("keeps the pending row when the week is reloaded after a save", async () => {
    const calls = await withPendingRow([["PUT", `${WEEK}/entries`, () => ok({ version: 4 })]]);
    type(input("2026-10-06"), "1:00");
    await flush();
    await window.TimeEntryPage.save();
    await flush();
    expect(calls.filter((c) => c.method === "GET" && /weeks\/2026-W41$/.test(c.url)).length).toBeGreaterThan(1);
    expect(window.TimeEntryPage.state().rows.some((r) => r.taskItemId === TASK_C)).toBe(true);
    expect(calls.find((c) => c.method === "PUT").body.entries.some((e) => e.taskItemId === TASK_C)).toBe(false);
  });

  it("leaving the week with pending rows says they will not be saved", async () => {
    await withPendingRow();
    window.showConfirm = vi.fn();
    window.TimeEntryPage.goToWeek("2026-W40");
    expect(window.showConfirm).toHaveBeenCalledWith("PendingRowsLeaveConfirm", expect.any(Function));

    type(input("2026-10-06"), "1:00");
    await flush();
    window.showConfirm = vi.fn();
    window.TimeEntryPage.goToWeek("2026-W40");
    expect(window.showConfirm).toHaveBeenCalledWith("LeaveDirtyAndPendingConfirm", expect.any(Function));
  });
});

describe("M5 — one timer request per page, a remembered 'off', and a quiet web tier", () => {
  it("My Timesheet and the chip share ONE timer request", async () => {
    const calls = installNetwork(routes(oneRow()));
    await bootPage({ week: "2026-W41", withChip: true });
    expect(calls.filter((c) => c.url === "/TimeEntry/api/timer")).toHaveLength(1);
  });

  it("'the timer is off' is remembered for ten minutes: the next page sends no timer request at all", async () => {
    let calls = installNetwork(routes(oneRow()));
    await bootPage({ week: "2026-W41", withChip: true });
    expect(document.getElementById("timeEntryTimerChip")).toBeNull();
    const session = window.sessionStorage;

    // the next page in the same tab (same sessionStorage)
    document.body.innerHTML = `<ul><li id="timeEntryTimerChip" hidden data-user="user-1" data-l10n='{}'>
      <a data-timer-link></a><span data-timer-title></span><span data-timer-elapsed></span><button data-timer-stop></button></li></ul>`;
    calls = installNetwork(routes(oneRow()));
    delete window.DitenTimerShared;
    window.__timerChipNoAutoInit = true;
    loadScript("wwwroot/assets/js/shared/timer-chip.js");
    expect(window.sessionStorage).toBe(session);
    await window.DitenTimerChip.init();
    await flush();
    expect(calls.filter((c) => c.url === "/TimeEntry/api/timer")).toHaveLength(0);
    expect(document.getElementById("timeEntryTimerChip")).toBeNull();

    // ten minutes later it asks again
    const now = Date.now();
    const spy = vi.spyOn(Date, "now").mockReturnValue(now + 10 * 60 * 1000 + 1);
    document.body.innerHTML = `<ul><li id="timeEntryTimerChip" hidden data-user="user-1" data-l10n='{}'>
      <a data-timer-link></a><span data-timer-title></span><span data-timer-elapsed></span><button data-timer-stop></button></li></ul>`;
    delete window.DitenTimerShared;
    loadScript("wwwroot/assets/js/shared/timer-chip.js");
    await window.DitenTimerChip.init();
    await flush();
    expect(calls.filter((c) => c.url === "/TimeEntry/api/timer")).toHaveLength(1);
    spy.mockRestore();
  });

  it("the web tier logs an unreachable service as a Warning (checked in Web.Tests); the source says so", () => {
    const controller = readSource("Controllers/TimeEntryController.cs");
    expect(controller).toMatch(/when \(ex is HttpRequestException or TaskCanceledException\)[\s\S]{0,400}LogWarning/);
  });
});

describe("L7 — 'Rejected' only while the rejection is the latest word", () => {
  it("rejected → resubmitted → withdrawn is a plain draft again", async () => {
    installNetwork(routes(weekPayload({
      status: "Draft", lastRejectedAtUtc: "2026-10-05T10:00:00Z", lastRejectionReason: "Tuesday is missing",
      lastRejectedByDisplayName: "Selin Öztürk", submittedAtUtc: "2026-10-06T09:00:00Z",
      entries: [entry("2026-10-05", 60, { taskItemId: TASK_A, taskTitle: "A" })]
    })));
    await bootPage({ week: "2026-W41" });
    expect(document.getElementById("teStatus").textContent).toBe("StatusDraft");
    expect(document.getElementById("teRejectedBand")).toBeNull();
    expect(document.getElementById("teSubmit").textContent).toBe("SubmitWeek");
  });

  it("a rejection newer than the last submission is still shown as rejected", async () => {
    installNetwork(routes(weekPayload({
      status: "Draft", lastRejectedAtUtc: "2026-10-06T10:00:00Z", lastRejectionReason: "x", submittedAtUtc: "2026-10-06T09:00:00Z"
    })));
    await bootPage({ week: "2026-W41" });
    expect(document.getElementById("teStatus").textContent).toBe("StatusRejected");
    expect(document.getElementById("teRejectedBand")).not.toBeNull();
  });
});

describe("L10 — plan values, zero corrections, limits in sentences", () => {
  it("a plan value for a task that already has a Manual row goes INTO that row — never a Manual+Plan twin", async () => {
    const calls = installNetwork(routes(oneRow(), [
      ["GET", `${WEEK}/plan-fill-in`, () => ok({ rows: [{ localDate: "2026-10-06", taskItemId: TASK_A, durationMinutes: 45, taskTitle: "A" }] })],
      ["PUT", `${WEEK}/entries`, () => ok({ version: 4 })]
    ]));
    await bootPage({ week: "2026-W41" });
    await window.TimeEntryPage.fillFromPlan();
    grid().querySelector("button.time-entry-ghost").click();
    const rows = window.TimeEntryPage.state().rows.filter((r) => r.taskItemId === TASK_A);
    expect(rows).toHaveLength(1);
    await window.TimeEntryPage.save();
    const body = calls.find((c) => c.method === "PUT").body.entries.filter((e) => e.taskItemId === TASK_A);
    expect(body.map((e) => `${e.localDate}:${e.source}`)).toEqual(["2026-10-05:Manual", "2026-10-06:Manual"]);
  });

  it("correcting a captured cell to zero is refused with its own sentence, not the quarter-hour one", async () => {
    installNetwork(routes(weekPayload({ entries: [entry("2026-10-05", 120, { taskItemId: TASK_A, source: "Timer", taskTitle: "A" })] })));
    await bootPage({ week: "2026-W41" });
    grid().querySelector("button.time-entry-cell-captured").click();
    document.getElementById("teCorrectValue").value = "0";
    document.getElementById("teCorrectApply").click();
    expect(notice().textContent).toBe("CapturedZeroNotAllowed");
    expect(window.TimeEntryPage.state().rows[0].cells["2026-10-05"].minutes).toBe(120);
  });

  it("a limit in a sentence comes from the payload's Limits, not from the translation", async () => {
    installNetwork(routes(oneRow(), [["PUT", `${WEEK}/entries`, () => refused(400, "TIME_ENTRY_DAY_IMPLAUSIBLE")]]));
    await bootPage({ week: "2026-W41", l10n: { ErrDayImplausible: "max {maxDayHours} h", Limits: { StepMinutes: 15, MaxRowMinutes: 960, ImplausibleDayMinutes: 840, NoteMaxLength: 500 } } });
    type(input("2026-10-06"), "1:00");
    await flush();
    await window.TimeEntryPage.save();
    expect(window.showToast).toHaveBeenCalledWith("max 14 h", "error");
  });

  it("no translation types a limit as a number", () => {
    const fs = require("fs");
    const path = require("path");
    const dir = path.join(__dirname, "..", "Resources", "Views", "TimeEntry");
    ["en", "tr", "fr", "es", "zh", "ar", "ru"].forEach((language) => {
      const xml = fs.readFileSync(path.join(dir, `TimeEntryIndex.${language}.resx`), "utf8");
      ["ErrStepInvalid", "ErrDayImplausible", "ErrNoteTooLong", "RoundedTo", "CapturedZeroNotAllowed"].forEach((key) => {
        const value = new RegExp(`<data name="${key}"[^>]*><value>([\\s\\S]*?)</value>`).exec(xml)[1];
        expect(value, `${language}:${key}`).not.toMatch(/\b(15|16|500|960)\b/);
        expect(value, `${language}:${key}`).toMatch(/\{(step|maxRowHours|maxDayHours|noteMax)\}/);
      });
    });
  });
});

describe("the pure rules behind them (core.js)", () => {
  let core;
  beforeEach(() => {
    delete window.TimeEntryCore;
    loadScript("wwwroot/assets/js/TimeEntry/core.js");
    core = window.TimeEntryCore;
  });

  it("refuses a value that rounds to zero, reads 10+ bare hours as hours and says so", () => {
    ["7dk", "0:05", "5m", "0,1"].forEach((text) => expect(core.parseDuration(text), text).toMatchObject({ ok: false, tooSmall: true }));
    expect(core.parseDuration("15")).toMatchObject({ ok: true, minutes: 900, bareHours: true });
    expect(core.parseDuration("9")).toMatchObject({ ok: true, minutes: 540, bareHours: false });
    expect(core.parseDuration("15h")).toMatchObject({ ok: true, minutes: 900, bareHours: false });
  });

  it("knows a rejection from a later submission", () => {
    expect(core.isRejectedNow({ status: "Draft", lastRejectedAtUtc: "2026-10-06T10:00:00Z" })).toBe(true);
    expect(core.isRejectedNow({ status: "Draft", lastRejectedAtUtc: "2026-10-06T10:00:00Z", submittedAtUtc: "2026-10-06T09:00:00Z" })).toBe(true);
    expect(core.isRejectedNow({ status: "Draft", lastRejectedAtUtc: "2026-10-06T10:00:00Z", submittedAtUtc: "2026-10-07T09:00:00Z" })).toBe(false);
    expect(core.isRejectedNow({ status: "Submitted", lastRejectedAtUtc: "2026-10-06T10:00:00Z" })).toBe(false);
  });

  it("merges pending rows back without doubling a target the fresh list already has", () => {
    const fresh = core.buildRows(weekPayload({ entries: [entry("2026-10-05", 60, { taskItemId: TASK_A, taskTitle: "A" })] }));
    const pending = [core.newRow("Manual", TASK_A, null, "A"), core.newRow("Manual", TASK_C, null, "C")];
    core.mergePending(fresh, pending);
    expect(fresh.map((r) => r.taskItemId)).toEqual([TASK_A, TASK_C]);
    expect(core.isPendingRow(fresh[1])).toBe(true);
    expect(core.isPendingRow(fresh[0])).toBe(false);
  });
});

describe("L8 — the chip never leaves a ghost", () => {
  it("a Stop refused because the timer already stopped elsewhere re-reads and hides the chip, with no error", async () => {
    let running = true;
    document.body.innerHTML = `<ul><li id="timeEntryTimerChip" hidden data-user="u" data-l10n='{}'>
      <a data-timer-link></a><span data-timer-title></span><span data-timer-elapsed></span><button data-timer-stop></button></li></ul>`;
    Object.defineProperty(window, "sessionStorage", { value: { getItem: () => null, setItem() {}, removeItem() {} }, configurable: true, writable: true });
    window.showToast = vi.fn();
    const calls = installNetwork([
      ["GET", "/TimeEntry/api/timer", () => ok({ timerEnabled: true, running: running ? { taskItemId: TASK_A, taskTitle: "A", startedAtUtc: new Date().toISOString() } : null, closedAtMidnightYesterday: [] })],
      ["POST", "/TimeEntry/api/timer/stop", () => refused(409, "TIMER_NOT_RUNNING")]
    ]);
    delete window.DitenTimerShared;
    window.__timerChipNoAutoInit = true;
    loadScript("wwwroot/assets/js/shared/timer-chip.js");
    await window.DitenTimerChip.init();
    await flush();
    const chip = document.getElementById("timeEntryTimerChip");
    expect(chip.hidden).toBe(false);

    running = false; // another tab stopped it
    await window.DitenTimerChip.stop();
    await flush();
    expect(calls.filter((c) => c.url === "/TimeEntry/api/timer")).toHaveLength(2);
    expect(chip.hidden).toBe(true);
    expect(window.showToast).not.toHaveBeenCalledWith("TimerStopFailed", "error");
  });
});
