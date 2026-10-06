const {
  PAGE_IDS, weekPayload, entry, installNetwork, ok, refused, flush, bootPage, readSource, TASK_A, TASK_B, TASK_C
} = require("./time-entry-harness");

/*
 * MOD-0280-FU01 T2a — My Timesheet on the DOM (pack §21.2 U1–U5). The page's real scripts; only the network is fake.
 */

const WEEK = "/TimeEntry/api/weeks/2026-W41";

function standardRoutes(payload, extra = []) {
  return extra.concat([
    ["GET", /\/TimeEntry\/api\/weeks\/2026-W41$/, () => ok(payload)],
    ["GET", "/TimeEntry/api/categories", () => ok([{ code: "ADMINISTRATION", labelText: null, labelResourceKey: "TimeEntry.Category.ADMINISTRATION", isActive: true }])],
    ["GET", "/TimeEntry/api/timer", () => ok({ timerEnabled: false, disabledReason: "TIMER_DISABLED_FOR_LEGAL_ENTITY", running: null, closedAtMidnightYesterday: [] })]
  ]);
}

const grid = () => document.getElementById("teGrid");
const bodyRows = () => Array.from(grid().querySelectorAll("tbody tr.time-entry-row"));
const cellsOf = (tr) => Array.from(tr.querySelectorAll("td.time-entry-cell"));

describe("the fixture is the view", () => {
  it("every id the harness draws is really in Index.cshtml", () => {
    const view = readSource("Views/TimeEntry/Index.cshtml");
    PAGE_IDS.forEach((id) => expect(view, id).toContain(`id="${id}"`));
  });
});

describe("the week grid (desktop)", () => {
  it("draws rows × Mon–Sun with row totals, day totals and the server's day target", async () => {
    const payload = weekPayload({
      entries: [
        entry("2026-10-05", 120, { taskItemId: TASK_A, taskTitle: "Batch record review" }),
        entry("2026-10-06", 90, { taskItemId: TASK_A, taskTitle: "Batch record review" }),
        entry("2026-10-05", 60, { categoryCode: "ADMINISTRATION" })
      ]
    });
    installNetwork(standardRoutes(payload));
    await bootPage({ week: "2026-W41" });

    const rows = bodyRows();
    expect(rows).toHaveLength(2);
    expect(rows[0].querySelector(".time-entry-row-title").textContent).toBe("Batch record review");
    expect(rows[1].querySelector(".time-entry-row-title").textContent).toBe("Administration");
    expect(cellsOf(rows[0])).toHaveLength(7);
    expect(rows[0].querySelector(".time-entry-total-col").textContent).toBe("3:30");

    const totals = Array.from(grid().querySelectorAll("tfoot .time-entry-day-total")).map((td) => td.textContent);
    expect(totals.slice(0, 3)).toEqual(["3:00", "1:30", "0:00"]);
    const targets = Array.from(grid().querySelectorAll("tfoot .time-entry-day-target")).map((td) => td.textContent);
    expect(targets).toEqual(["8:00", "8:00", "8:00", "8:00", "8:00", "—", "—"]);
  });

  it("names an inaccessible task neutrally, never by its id", async () => {
    installNetwork(standardRoutes(weekPayload({ entries: [entry("2026-10-05", 60, { taskItemId: TASK_B, taskTitle: null })] })));
    await bootPage({ week: "2026-W41" });
    const title = bodyRows()[0].querySelector(".time-entry-row-title");
    expect(title.textContent).toBe("UnreadableTask");
    expect(grid().textContent).not.toContain(TASK_B);
  });

  it("closes future days: no input in Thu–Sun (today is Wednesday), weekend shaded from the payload", async () => {
    installNetwork(standardRoutes(weekPayload({ entries: [entry("2026-10-05", 60, { taskItemId: TASK_A, taskTitle: "A" })] })));
    await bootPage({ week: "2026-W41" });
    const cells = cellsOf(bodyRows()[0]);
    expect(cells.slice(0, 3).every((td) => td.querySelector("input"))).toBe(true);
    expect(cells.slice(3).some((td) => td.querySelector("input"))).toBe(false);
    expect(cells[5].classList.contains("time-entry-day-weekend")).toBe(true);
    expect(cells[3].classList.contains("time-entry-day-future")).toBe(true);
  });

  it("shows the 660-minute flag from the server and the 960-minute refusal through the bridge", async () => {
    const payload = weekPayload({ entries: [entry("2026-10-05", 700, { taskItemId: TASK_A, taskTitle: "A" })] });
    payload.days[0].isFlagged = true;
    payload.days[0].recordedMinutes = 700;
    installNetwork(standardRoutes(payload, [["PUT", `${WEEK}/entries`, () => refused(400, "TIME_ENTRY_DAY_IMPLAUSIBLE")]]));
    await bootPage({ week: "2026-W41" });

    const monday = grid().querySelector('tfoot .time-entry-day-total[data-date="2026-10-05"]');
    expect(monday.classList.contains("time-entry-day-flagged")).toBe(true);
    expect(monday.querySelector(".time-entry-mark-flag")).not.toBeNull();

    const input = grid().querySelector('input[data-date="2026-10-06"]');
    input.value = "16:30";
    input.dispatchEvent(new Event("change"));
    await window.TimeEntryPage.save();
    await flush();
    expect(window.showToast).toHaveBeenCalledWith("ErrDayImplausible", "error");
  });
});

describe("typing into a cell", () => {
  it("reads 1,5 as 1:30, tells the person when it rounds, and refuses nonsense in place", async () => {
    installNetwork(standardRoutes(weekPayload({ entries: [entry("2026-10-05", 60, { taskItemId: TASK_A, taskTitle: "A" })] })));
    await bootPage({ week: "2026-W41" });

    let input = grid().querySelector('input[data-date="2026-10-06"]');
    input.value = "1,5";
    input.dispatchEvent(new Event("change"));
    expect(window.TimeEntryPage.state().rows[0].cells["2026-10-06"].minutes).toBe(90);

    input = grid().querySelector('input[data-date="2026-10-07"]');
    input.value = "1:20";
    input.dispatchEvent(new Event("change"));
    expect(window.TimeEntryPage.state().rows[0].cells["2026-10-07"].minutes).toBe(75);
    expect(document.getElementById("teNotice").textContent).toBe("RoundedTo");

    input = grid().querySelector('input[data-date="2026-10-05"]');
    input.value = "abc";
    input.dispatchEvent(new Event("change"));
    expect(input.classList.contains("is-invalid")).toBe(true);
    expect(window.TimeEntryPage.state().rows[0].cells["2026-10-05"].minutes).toBe(60);
  });

  it("moves with the arrow keys and Enter", async () => {
    installNetwork(standardRoutes(weekPayload({
      entries: [entry("2026-10-05", 60, { taskItemId: TASK_A, taskTitle: "A" }), entry("2026-10-05", 30, { taskItemId: TASK_B, taskTitle: "B" })]
    })));
    await bootPage({ week: "2026-W41" });
    const at = (pos) => grid().querySelector(`[data-pos="${pos}"]`);

    at("0:0").dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowRight", bubbles: true }));
    expect(document.activeElement.getAttribute("data-pos")).toBe("0:1");
    document.activeElement.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", bubbles: true }));
    expect(document.activeElement.getAttribute("data-pos")).toBe("1:1");
    document.activeElement.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowLeft", bubbles: true }));
    expect(document.activeElement.getAttribute("data-pos")).toBe("1:0");
  });
});

describe("captured rows (timer, meeting) are corrected in the side panel", () => {
  it("sends the corrected captured row with its source, and never an untouched one", async () => {
    const payload = weekPayload({
      entries: [
        entry("2026-10-05", 120, { taskItemId: TASK_A, source: "Timer", taskTitle: "A" }),
        entry("2026-10-06", 45, { taskItemId: TASK_B, source: "Timer", taskTitle: "B" }),
        entry("2026-10-06", 30, { categoryCode: "ADMINISTRATION", source: "Manual" })
      ]
    });
    const calls = installNetwork(standardRoutes(payload, [["PUT", `${WEEK}/entries`, () => ok({ version: 4 })]]));
    await bootPage({ week: "2026-W41" });

    const captured = grid().querySelector('button.time-entry-cell-captured[data-date="2026-10-05"]');
    expect(captured.textContent).toBe("2:00");
    captured.click();
    const panel = document.getElementById("teCorrectPanel");
    expect(panel.textContent).toContain("MeasuredValue");
    document.getElementById("teCorrectValue").value = "1:45";
    document.getElementById("teCorrectApply").click();

    await window.TimeEntryPage.save();
    await flush();
    const put = calls.find((c) => c.method === "PUT");
    expect(put.body.expectedVersion).toBe(3);
    expect(put.body.entries).toEqual([
      { localDate: "2026-10-05", taskItemId: TASK_A, categoryCode: null, durationMinutes: 105, note: null, source: "Timer", sourceRef: null },
      { localDate: "2026-10-06", taskItemId: null, categoryCode: "ADMINISTRATION", durationMinutes: 30, note: null, source: "Manual", sourceRef: null }
    ]);
    expect(put.body.entries.some((e) => e.taskItemId === TASK_B)).toBe(false);
  });
});

describe("suggestions sit inside their day", () => {
  const withSuggestion = () => weekPayload({
    suggestions: [{ id: "s-1", meetingId: "m-1", title: "Weekly QA sync", localDate: "2026-10-06", proposedMinutes: 60, state: "Open", minutesStatus: "confirmed", acceptedEntryId: null }]
  });

  it("draws a meeting suggestion as a grey value in ITS day, accepted or declined from the cell", async () => {
    const calls = installNetwork(standardRoutes(withSuggestion(), [
      ["POST", `${WEEK}/suggestions/s-1/dismiss`, () => ok({ suggestionId: "s-1", state: "Dismissed" })]
    ]));
    await bootPage({ week: "2026-W41" });

    const row = grid().querySelector('tr[data-suggestion-id="s-1"]');
    const cells = cellsOf(row);
    expect(cells[1].querySelector(".time-entry-ghost-value").textContent).toBe("1:00");
    expect(cells.filter((td) => td.querySelector(".time-entry-suggestion"))).toHaveLength(1);
    expect(row.textContent).toContain("ConfirmedByMinutes");

    row.querySelector("[data-suggestion-dismiss]").click();
    await flush();
    expect(calls.some((c) => c.method === "POST" && c.url.endsWith("/suggestions/s-1/dismiss"))).toBe(true);
  });

  it("'Review suggestions' adds the ticked ones in one go", async () => {
    const calls = installNetwork(standardRoutes(withSuggestion(), [
      ["POST", `${WEEK}/suggestions/s-1/accept`, () => ok({ suggestionId: "s-1", state: "Accepted", entryId: "e-9", weekVersion: 4 })],
      ["GET", `${WEEK}/plan-fill-in`, () => ok({ weekKey: "2026-W41", localToday: "2026-10-07", rows: [{ localDate: "2026-10-05", taskItemId: TASK_C, durationMinutes: 45, taskTitle: "Line clearance" }] })],
      ["PUT", `${WEEK}/entries`, () => ok({ version: 4 })]
    ]));
    await bootPage({ week: "2026-W41" });
    await window.TimeEntryPage.fillFromPlan();

    document.getElementById("teReviewSuggestions").click();
    const boxes = Array.from(document.querySelectorAll("#teReview input[type=checkbox]"));
    expect(boxes.map((b) => b.getAttribute("data-kind")).sort()).toEqual(["meeting", "plan"]);
    boxes.forEach((b) => { b.checked = true; });
    await window.TimeEntryPage.addSelectedSuggestions();
    await flush();

    const put = calls.find((c) => c.method === "PUT");
    expect(put.body.entries).toContainEqual(expect.objectContaining({ taskItemId: TASK_C, durationMinutes: 45, source: "Plan" }));
    const accept = calls.find((c) => c.url.endsWith("/suggestions/s-1/accept"));
    expect(accept.body).toEqual({ expectedVersion: 3, taskItemId: null, categoryCode: null });
  });

  it("fill from plan shows grey values only; nothing is written until a value is taken", async () => {
    const calls = installNetwork(standardRoutes(weekPayload(), [
      ["GET", `${WEEK}/plan-fill-in`, () => ok({ rows: [{ localDate: "2026-10-06", taskItemId: TASK_C, durationMinutes: 45, taskTitle: "Line clearance" }] })]
    ]));
    await bootPage({ week: "2026-W41" });
    await window.TimeEntryPage.fillFromPlan();

    const ghost = grid().querySelector("button.time-entry-ghost");
    expect(ghost.textContent).toBe("0:45");
    expect(grid().querySelector(".time-entry-row-ghost .time-entry-row-title").textContent).toBe("Line clearance");
    expect(calls.some((c) => c.method !== "GET")).toBe(false);
    ghost.click();
    expect(window.TimeEntryPage.state().rows.find((r) => r.taskItemId === TASK_C).source).toBe("Plan");
  });
});

describe("copy previous week", () => {
  it("adds rows without time and never overwrites a row that is already there", async () => {
    installNetwork(standardRoutes(weekPayload({ entries: [entry("2026-10-05", 120, { taskItemId: TASK_A, taskTitle: "A" })] }), [
      ["GET", /\/weeks\/2026-W40$/, () => ok(weekPayload({
        weekKey: "2026-W40",
        entries: [entry("2026-09-28", 300, { taskItemId: TASK_A, taskTitle: "A" }), entry("2026-09-29", 60, { taskItemId: TASK_B, taskTitle: "B" })]
      }))]
    ]));
    await bootPage({ week: "2026-W41" });
    await window.TimeEntryPage.copyPreviousWeek();

    const rows = window.TimeEntryPage.state().rows;
    expect(rows.map((r) => r.taskItemId)).toEqual([TASK_A, TASK_B]);
    expect(rows[0].cells["2026-10-05"].minutes).toBe(120);
    expect(rows[1].cells).toEqual({});
  });
});

describe("the add-row picker uses the server's task options", () => {
  it("offers exactly what task-options returned and adds a Manual row", async () => {
    installNetwork(standardRoutes(weekPayload(), [
      ["GET", "/TimeEntry/api/task-options", () => ok([{ taskItemId: TASK_C, title: "Line clearance", status: "InProgress" }])]
    ]));
    await bootPage({ week: "2026-W41" });
    document.getElementById("teAddTaskRow").click();
    await flush();
    const select = document.getElementById("tePickerSelect");
    expect(Array.from(select.options).map((o) => o.textContent)).toEqual(["PickTask", "Line clearance"]);
    select.value = TASK_C;
    select.dispatchEvent(new Event("change"));
    const row = window.TimeEntryPage.state().rows.find((r) => r.taskItemId === TASK_C);
    expect(row.source).toBe("Manual");
    expect(bodyRows()[0].querySelector(".time-entry-row-title").textContent).toBe("Line clearance");
  });
});

describe("submit, withdraw, correction, rejected", () => {
  it("submits with the week's version", async () => {
    const calls = installNetwork(standardRoutes(weekPayload({ entries: [entry("2026-10-05", 60, { taskItemId: TASK_A, taskTitle: "A" })] }), [
      ["POST", `${WEEK}/submit`, () => ok({ status: "Submitted", version: 4 })]
    ]));
    await bootPage({ week: "2026-W41" });
    document.getElementById("teSubmit").click();
    await flush();
    expect(calls.find((c) => c.url.endsWith("/submit")).body).toEqual({ expectedVersion: 3 });
  });

  it("a submitted week says whose approval it waits for and offers Withdraw", async () => {
    const calls = installNetwork(standardRoutes(weekPayload({
      status: "Submitted", editable: false, submittedAtUtc: "2026-10-06T08:00:00Z", submittedByDisplayName: "Ayşe Yılmaz",
      assignedApproverUserId: "u-9", assignedApproverDisplayName: "Selin Öztürk"
    }), [["POST", `${WEEK}/withdraw`, () => ok({ status: "Draft", version: 5 })]]));
    await bootPage({ week: "2026-W41" });

    expect(document.getElementById("teWaitingBand").textContent).toBe("WaitingApprovalOf");
    expect(document.getElementById("teSubmit")).toBeNull();
    expect(document.getElementById("teStatus").textContent).toBe("StatusSubmitted");
    document.getElementById("teWithdraw").click();
    await flush();
    expect(calls.find((c) => c.url.endsWith("/withdraw")).body).toEqual({ expectedVersion: 3 });
  });

  it("asks for a correction reason ON the page; an empty reason is stopped before the network", async () => {
    const calls = installNetwork(standardRoutes(weekPayload({ status: "Approved", editable: false, approvedAtUtc: "2026-10-09T08:00:00Z", approvedByDisplayName: "Selin Öztürk" }), [
      ["POST", `${WEEK}/corrections`, () => ok({ status: "Draft", version: 1 })]
    ]));
    await bootPage({ week: "2026-W41" });
    const promptSpy = vi.spyOn(window, "prompt").mockImplementation(() => null);

    document.getElementById("teRequestCorrection").click();
    expect(document.getElementById("teCorrectionReason")).not.toBeNull();
    document.getElementById("teCorrectionSend").click();
    await flush();
    expect(document.getElementById("teCorrectionError").hidden).toBe(false);
    expect(document.getElementById("teCorrectionError").textContent).toBe("ErrCorrectionReasonRequired");
    expect(calls.some((c) => c.url.endsWith("/corrections"))).toBe(false);

    document.getElementById("teCorrectionReason").value = "Tuesday went to the wrong task";
    document.getElementById("teCorrectionSend").click();
    await flush();
    expect(calls.find((c) => c.url.endsWith("/corrections")).body).toEqual({ reason: "Tuesday went to the wrong task" });
    expect(promptSpy).not.toHaveBeenCalled();
  });

  it("a rejected week shows the reason band, stays editable and offers Submit again", async () => {
    installNetwork(standardRoutes(weekPayload({
      status: "Draft", lastRejectedAtUtc: "2026-10-06T10:00:00Z", lastRejectionReason: "Tuesday is missing",
      lastRejectedByDisplayName: "Selin Öztürk", submittedAtUtc: "2026-10-05T16:00:00Z", submittedByDisplayName: "Ayşe Yılmaz",
      entries: [entry("2026-10-05", 60, { taskItemId: TASK_A, taskTitle: "A" })]
    })));
    await bootPage({ week: "2026-W41" });
    expect(document.getElementById("teRejectedBand").textContent).toBe("RejectedBand");
    expect(document.getElementById("teStatus").textContent).toBe("StatusRejected");
    expect(document.getElementById("teSubmit").textContent).toBe("Resubmit");
    expect(grid().querySelector("input.time-entry-cell-input")).not.toBeNull();
  });

  it("the approval history strip lists submitted → returned → approved in time order", async () => {
    installNetwork(standardRoutes(weekPayload({
      status: "Approved", editable: false,
      submittedAtUtc: "2026-10-07T08:00:00Z", submittedByDisplayName: "Ayşe Yılmaz",
      lastRejectedAtUtc: "2026-10-06T10:00:00Z", lastRejectedByDisplayName: "Selin Öztürk", lastRejectionReason: "x",
      approvedAtUtc: "2026-10-08T09:00:00Z", approvedByDisplayName: "Selin Öztürk"
    })));
    await bootPage({ week: "2026-W41" });
    const items = Array.from(document.querySelectorAll("#teHistory .time-entry-history-item"));
    expect(items.map((li) => li.className.replace("time-entry-history-item ", ""))).toEqual([
      "time-entry-history-rejected", "time-entry-history-submitted", "time-entry-history-approved"
    ]);
  });
});

describe("the phone view", () => {
  it("shows one day with a day switcher; switching the day switches the list", async () => {
    installNetwork(standardRoutes(weekPayload({
      entries: [entry("2026-10-05", 60, { taskItemId: TASK_A, taskTitle: "A" }), entry("2026-10-06", 30, { categoryCode: "ADMINISTRATION" })]
    })));
    await bootPage({ week: "2026-W41" });
    const view = document.getElementById("teDayView");
    const tabs = view.querySelectorAll(".time-entry-day-tab");
    expect(tabs).toHaveLength(7);
    expect(view.querySelector('.time-entry-day-tab[aria-selected="true"]').getAttribute("data-day")).toBe("2026-10-07"); // today

    view.querySelector('[data-day="2026-10-05"]').click();
    const input = document.querySelector('#teDayView input[data-date="2026-10-05"]');
    expect(input.value).toBe("1:00");
    view.querySelector('[data-day="2026-10-09"]').click(); // Friday — future
    expect(document.querySelector("#teDayView input")).toBeNull();
  });
});

describe("unauthorized and read-only", () => {
  it("a 403 draws the explanation only — no grid, no buttons (UAS-001)", async () => {
    installNetwork([
      ["GET", WEEK, () => refused(403, null)],
      ["GET", "/TimeEntry/api/", () => refused(403, null)]
    ]);
    await bootPage({ week: "2026-W41" });
    const page = document.getElementById("timeEntryPage");
    expect(page.querySelector(".diten-access-denied")).not.toBeNull();
    expect(page.querySelector("button")).toBeNull();
    expect(page.querySelector("table")).toBeNull();
  });

  it("without the update key the grid is read-only and no write button is drawn", async () => {
    installNetwork(standardRoutes(weekPayload({ entries: [entry("2026-10-05", 60, { taskItemId: TASK_A, taskTitle: "A" })] })));
    await bootPage({ week: "2026-W41", canUpdate: false });
    expect(grid().querySelector("input")).toBeNull();
    expect(document.getElementById("teFooterActions").children).toHaveLength(0);
    expect(document.getElementById("teAddTaskRow")).toBeNull();
  });
});

describe("right to left (ar)", () => {
  it("mirrors the week chevrons and makes the LEFT arrow go to the next day", async () => {
    installNetwork(standardRoutes(weekPayload({ entries: [entry("2026-10-05", 60, { taskItemId: TASK_A, taskTitle: "A" })] })));
    await bootPage({ week: "2026-W41", dir: "rtl", lang: "ar" });
    expect(document.querySelector("#tePrevWeek i").className).toContain("bx-chevron-right");
    expect(document.querySelector("#teNextWeek i").className).toContain("bx-chevron-left");

    grid().querySelector('[data-pos="0:0"]').dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowLeft", bubbles: true }));
    expect(document.activeElement.getAttribute("data-pos")).toBe("0:1");
  });

  it("the page's CSS uses logical sides only, so it mirrors without a second rule set", () => {
    const css = readSource("wwwroot/assets/css/backbone-custom.css");
    const block = css.slice(css.indexOf("MOD-0280-FU01 T2a — My Timesheet"));
    expect(block.length).toBeGreaterThan(100);
    expect(block).not.toMatch(/(margin|padding)-(left|right)\s*:|(^|[^-])(left|right)\s*:\s*\d|text-align:\s*(left|right)/m);
  });
});

describe("no inline style (FG-003)", () => {
  it("draws no style attribute and its scripts never write element.style", async () => {
    installNetwork(standardRoutes(weekPayload({
      entries: [entry("2026-10-05", 60, { taskItemId: TASK_A, taskTitle: "A", source: "Timer" }), entry("2026-10-06", 30, { categoryCode: "ADMINISTRATION" })],
      suggestions: [{ id: "s-1", meetingId: "m", title: "M", localDate: "2026-10-06", proposedMinutes: 30, state: "Open", minutesStatus: "none" }]
    })));
    await bootPage({ week: "2026-W41" });
    expect(document.querySelectorAll("#timeEntryPage [style]")).toHaveLength(0);
    ["wwwroot/assets/js/TimeEntry/index.js", "wwwroot/assets/js/TimeEntry/core.js", "wwwroot/assets/js/shared/timer-chip.js",
      "Views/TimeEntry/Index.cshtml", "Views/Shared/_TimerChip.cshtml"].forEach((file) => {
      const source = readSource(file);
      expect(source, file).not.toMatch(/\.style\.|\sstyle=|setAttribute\(\s*['"]style/);
    });
  });
});

describe("the disclaimer", () => {
  it("is on the page in every state", () => {
    expect(readSource("Views/TimeEntry/Index.cshtml")).toContain('id="teDisclaimer">@Localizer["Disclaimer"]');
  });
});
