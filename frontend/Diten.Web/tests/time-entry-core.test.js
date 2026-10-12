const { loadScript } = require("./load-script");
const { weekPayload, entry, TASK_A, TASK_B, TASK_C } = require("./time-entry-harness");

/*
 * MOD-0280-FU01 T2a — My Timesheet's pure half (TimeEntry/core.js), loaded as the page loads it.
 * What is measured here is TRANSLATION only: typed text → minutes, payload → rows, rows → save body. The rules
 * themselves (limits, flags, future days) are the server's and are read from the payload.
 */

let core;
beforeEach(() => {
  delete window.TimeEntryCore;
  loadScript("wwwroot/assets/js/TimeEntry/core.js");
  core = window.TimeEntryCore;
});

describe("typed durations (U5: 1:30, 1,5, 1.5, 90dk)", () => {
  it.each([
    ["1:30", 90], ["1,5", 90], ["1.5", 90], ["90dk", 90], ["90m", 90], ["90 dk", 90], ["2", 120], ["0:45", 45],
    ["1,25", 75], ["8h", 480], ["45min", 45]
  ])("reads %s as %i minutes", (text, minutes) => {
    const parsed = core.parseDuration(text);
    expect(parsed.ok).toBe(true);
    expect(parsed.minutes).toBe(minutes);
    expect(parsed.rounded).toBe(false);
  });

  it.each([
    ["1:20", 75], ["1:23", 90], ["8m", 15], ["1:07", 60], ["1:08", 75]
  ])("snaps %s to the nearest quarter hour (%i) and SAYS it did", (text, minutes) => {
    const parsed = core.parseDuration(text);
    expect(parsed.minutes).toBe(minutes);
    expect(parsed.rounded).toBe(true);
  });

  it("clears on empty or zero and refuses what is not a duration", () => {
    expect(core.parseDuration("")).toMatchObject({ ok: true, minutes: 0, empty: true });
    expect(core.parseDuration("0")).toMatchObject({ ok: true, minutes: 0, empty: true });
    ["abc", "1:75", "1..5", "-1", "1:3", "12:00:00"].forEach((text) => {
      expect(core.parseDuration(text).ok, text).toBe(false);
    });
  });

  // CT acceptance (2026-09-30): a bare number is HOURS, so a three-digit one ("130", "480") is almost always a person
  // typing minutes without a unit. It must be refused, not read as 130 hours — widening the hours pattern passed the suite.
  it("refuses a three-digit bare number instead of reading it as hours", () => {
    ["130", "480", "100"].forEach((text) => {
      expect(core.parseDuration(text).ok, text).toBe(false);
    });
    expect(core.parseDuration("130dk")).toMatchObject({ ok: true, minutes: 135 });
  });

  it("formats minutes as h:mm, never as a decimal", () => {
    expect(core.formatMinutes(90)).toBe("1:30");
    expect(core.formatMinutes(480)).toBe("8:00");
    expect(core.formatMinutes(15)).toBe("0:15");
    expect(core.formatMinutes(0)).toBe("");
  });
});

describe("ISO weeks", () => {
  it("names the week a date falls in, with the week-year rule (2027-01-01 is 2026-W53)", () => {
    expect(core.weekKeyOfDate("2026-10-07")).toBe("2026-W41");
    expect(core.weekKeyOfDate("2027-01-01")).toBe("2026-W53");
    expect(core.weekKeyOfDate("2026-01-01")).toBe("2026-W01");
    expect(core.mondayOfWeekKey("2026-W41")).toBe("2026-10-05");
  });

  it("moves a week back and forth across a year boundary", () => {
    expect(core.shiftWeek("2026-W41", -1)).toBe("2026-W40");
    expect(core.shiftWeek("2026-W53", 1)).toBe("2027-W01");
    expect(core.shiftWeek("2027-W01", -1)).toBe("2026-W53");
  });

  it("accepts only a real week key", () => {
    expect(core.isWeekKey("2026-W41")).toBe(true);
    ["2026-41", "2026-W54", "2026-W00", "x", ""].forEach((k) => expect(core.isWeekKey(k), k).toBe(false));
  });
});

describe("totals, target and missing days come from the payload", () => {
  it("adds up each day across rows", () => {
    const rows = core.buildRows(weekPayload({
      entries: [entry("2026-10-05", 120, { taskItemId: TASK_A }), entry("2026-10-05", 60, { categoryCode: "ADMINISTRATION" }),
        entry("2026-10-06", 30, { taskItemId: TASK_A })]
    }));
    const totals = core.dayTotals(rows, ["2026-10-05", "2026-10-06", "2026-10-07"]);
    expect(totals).toEqual({ "2026-10-05": 180, "2026-10-06": 30, "2026-10-07": 0 });
    expect(core.rowTotal(rows[0])).toBe(150);
  });

  it("counts a missing day only up to today and only where the server gives a target", () => {
    const payload = weekPayload();
    payload.days[0].recordedMinutes = 480; // Mon full
    payload.days[1].recordedMinutes = 300; // Tue short
    // Wed (today) 0 → short; Thu–Sun future or no target → never counted
    expect(core.missingDays(payload)).toEqual(["2026-10-06", "2026-10-07"]);
  });
});

describe("the save body", () => {
  const captured = () => core.buildRows(weekPayload({
    entries: [
      entry("2026-10-05", 120, { taskItemId: TASK_A, source: "Manual", taskTitle: "A" }),
      entry("2026-10-05", 95 - 5, { taskItemId: TASK_B, source: "Timer", taskTitle: "B" }),
      entry("2026-10-06", 60, { categoryCode: "INTERNAL_MEETING", source: "Meeting", sourceRef: "m-1" }),
      entry("2026-10-06", 45, { taskItemId: TASK_C, source: "Plan", taskTitle: "C" })
    ]
  }));

  it("sends every person-typed row with its source, and NO untouched captured row", () => {
    const body = core.buildSaveEntries(captured());
    expect(body).toEqual([
      { localDate: "2026-10-05", taskItemId: TASK_A, categoryCode: null, durationMinutes: 120, note: null, source: "Manual", sourceRef: null },
      { localDate: "2026-10-06", taskItemId: TASK_C, categoryCode: null, durationMinutes: 45, note: null, source: "Plan", sourceRef: null }
    ]);
  });

  it("sends a captured row once the person corrected it — with its source, and the meeting id for a meeting row", () => {
    const rows = captured();
    core.setCell(rows.find((r) => r.source === "Timer"), "2026-10-05", 60);
    core.setCell(rows.find((r) => r.source === "Meeting"), "2026-10-06", 60, "left early");
    const body = core.buildSaveEntries(rows);
    expect(body).toContainEqual({ localDate: "2026-10-05", taskItemId: TASK_B, categoryCode: null, durationMinutes: 60, note: null, source: "Timer", sourceRef: null });
    expect(body).toContainEqual({ localDate: "2026-10-06", taskItemId: null, categoryCode: "INTERNAL_MEETING", durationMinutes: 60, note: "left early", source: "Meeting", sourceRef: "m-1" });
    body.forEach((row) => expect(typeof row.source).toBe("string"));
  });

  it("drops a person-typed cell cleared to zero (the server removes a row left out)", () => {
    const rows = captured();
    core.setCell(rows[0], "2026-10-05", 0);
    expect(core.buildSaveEntries(rows).some((r) => r.taskItemId === TASK_A)).toBe(false);
    expect(core.isDirty(rows)).toBe(true);
  });
});

describe("copy previous week — rows only, never over an existing row", () => {
  it("adds the missing targets as EMPTY manual rows and leaves existing rows alone", () => {
    const current = core.buildRows(weekPayload({ entries: [entry("2026-10-05", 120, { taskItemId: TASK_A, taskTitle: "A" })] }));
    const before = JSON.stringify(current[0]);
    const previous = weekPayload({
      weekKey: "2026-W40",
      entries: [
        entry("2026-09-28", 300, { taskItemId: TASK_A, taskTitle: "A" }),
        entry("2026-09-29", 240, { taskItemId: TASK_B, taskTitle: "B" }),
        entry("2026-09-29", 60, { taskItemId: TASK_C, taskTitle: null }), // no longer readable
        entry("2026-09-30", 60, { categoryCode: "ADMINISTRATION" }),
        entry("2026-09-30", 60, { categoryCode: "RETIRED" })
      ]
    });

    const added = core.copyRowsFromWeek(current, previous, ["ADMINISTRATION"]);

    expect(added.map((r) => r.taskItemId || r.categoryCode)).toEqual([TASK_B, "ADMINISTRATION"]);
    added.forEach((row) => {
      expect(row.source).toBe("Manual");
      expect(row.cells).toEqual({});
    });
    expect(JSON.stringify(current[0])).toBe(before);
    expect(core.buildSaveEntries(current)).toHaveLength(1); // copied rows carry no minutes
  });
});

describe("plan values", () => {
  it("only fills a cell that is empty, and accepting one makes a Plan row", () => {
    const rows = core.buildRows(weekPayload({ entries: [entry("2026-10-05", 60, { taskItemId: TASK_A, taskTitle: "A" })] }));
    const ghosts = core.planGhosts(rows, [
      { localDate: "2026-10-05", taskItemId: TASK_A, durationMinutes: 90 },
      { localDate: "2026-10-06", taskItemId: TASK_A, durationMinutes: 45 },
      { localDate: "2026-10-06", taskItemId: TASK_B, durationMinutes: 30, taskTitle: "B" }
    ]);
    expect(Object.keys(ghosts).sort()).toEqual([`${TASK_A}|2026-10-06`, `${TASK_B}|2026-10-06`]);

    const row = core.acceptPlanGhost(rows, ghosts[`${TASK_B}|2026-10-06`]);
    expect(row.source).toBe("Plan");
    expect(core.buildSaveEntries(rows)).toContainEqual(expect.objectContaining({ taskItemId: TASK_B, source: "Plan", durationMinutes: 30 }));
  });
});

describe("keyboard", () => {
  const size = { rows: 3, cols: 7 };
  it("moves right/left/up/down and Enter goes down", () => {
    expect(core.nextCell({ row: 1, col: 2 }, "ArrowRight", size, false)).toEqual({ row: 1, col: 3 });
    expect(core.nextCell({ row: 1, col: 2 }, "ArrowLeft", size, false)).toEqual({ row: 1, col: 1 });
    expect(core.nextCell({ row: 1, col: 2 }, "ArrowDown", size, false)).toEqual({ row: 2, col: 2 });
    expect(core.nextCell({ row: 1, col: 2 }, "Enter", size, false)).toEqual({ row: 2, col: 2 });
    expect(core.nextCell({ row: 0, col: 0 }, "ArrowUp", size, false)).toEqual({ row: 0, col: 0 });
    expect(core.nextCell({ row: 2, col: 6 }, "ArrowRight", size, false)).toEqual({ row: 2, col: 6 });
  });

  it("in a right-to-left page the LEFT arrow goes to the next day", () => {
    expect(core.nextCell({ row: 1, col: 2 }, "ArrowLeft", size, true)).toEqual({ row: 1, col: 3 });
    expect(core.nextCell({ row: 1, col: 2 }, "ArrowRight", size, true)).toEqual({ row: 1, col: 1 });
  });
});

describe("reason codes become sentences", () => {
  const t = (key) => `[${key}]`;
  it("maps a known code, says 'no permission' for a bare 403, and falls back to the generic sentence", () => {
    expect(core.failureMessage({ status: 400, reasonCode: "TIME_ENTRY_DAY_IMPLAUSIBLE" }, t)).toBe("[ErrDayImplausible]");
    expect(core.failureMessage({ status: 400, reasonCode: "WORKFLOW_REJECT_COMMENT_REQUIRED" }, t)).toBe("[ErrRejectCommentRequired]");
    expect(core.failureMessage({ status: 403, reasonCode: null }, t)).toBe("[ErrForbidden]");
    expect(core.failureMessage({ status: 500, reasonCode: "SOMETHING_NEW" }, t)).toBe("[ErrGeneric]");
  });
});
