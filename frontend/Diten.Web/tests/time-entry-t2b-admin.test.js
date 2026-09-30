const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");
const { weekPayload, entry, installNetwork, ok, refused, flush, bootPage, readSource, TASK_A, TASK_B } = require("./time-entry-harness");

/*
 * MOD-0280-FU01 T2b — the approvals list and week, the category catalogue, the settings page, and the four live
 * findings on My Timesheet (E1–E4). The pages' real scripts; only the network is fake.
 */

const webRoot = path.resolve(__dirname, "..");
const ids = (view) => Array.from(readSource(view).matchAll(/id="([A-Za-z][\w-]*)"/g)).map(([, id]) => id);

function loadCore() {
  delete window.TimeEntryCore;
  loadScript("wwwroot/assets/js/TimeEntry/core.js");
}

// ── Approvals list ─────────────────────────────────────────────────────────────────────────────────────────────

describe("approvals list — bulk approve takes UNMARKED weeks only", () => {
  const clean = { weekId: "w-clean", approvalTaskId: "t-1", flaggedDates: [], autoClosedDates: [], holidayDates: [], outsideWorkingMinutes: 0 };
  const rows = [
    clean,
    Object.assign({}, clean, { weekId: "w-11h", approvalTaskId: "t-2", flaggedDates: ["2026-10-05"] }),
    Object.assign({}, clean, { weekId: "w-midnight", approvalTaskId: "t-3", autoClosedDates: ["2026-10-06"] }),
    Object.assign({}, clean, { weekId: "w-outside", approvalTaskId: "t-4", outsideWorkingMinutes: 30 }),
    Object.assign({}, clean, { weekId: "w-holiday", approvalTaskId: "t-5", holidayDates: ["2026-10-07"] }),
    Object.assign({}, clean, { weekId: "w-noTask", approvalTaskId: null })
  ];

  // The page script declares a top-level const (the Golden Reference shape), so it is loaded ONCE for the block.
  beforeAll(() => {
    loadCore();
    loadScript("wwwroot/assets/js/TimeEntry/Approvals/index.js");
  });

  beforeEach(() => {
    window.L10n = { BulkApproveConfirm: "Approve {0}?", BulkApproveDone: "{0} approved, {1} skipped.", BulkNothingSelected: "none" };
    window.showToast = vi.fn();
    window.showConfirm = vi.fn((_t, yes) => yes());
  });

  it("names every mark a bulk approval must not skip", () => {
    const list = window.TimeApprovalsList;
    expect(list.isMarked(rows[0])).toBe(false);
    rows.slice(1, 5).forEach((row) => expect(list.isMarked(row), row.weekId).toBe(true));
  });

  it("filters a selection down to unmarked, decidable weeks — even when select-all ticked the others", () => {
    const sendable = window.TimeApprovalsList.approvableIds(rows.map((r) => r.weekId), rows);
    expect(sendable).toEqual(["w-clean"]);
  });

  it("sends ONLY the unmarked weeks to the bulk endpoint", async () => {
    const calls = installNetwork([["POST", "/TimeEntry/Approvals/api/bulk", () => ok({ approved: ["w-clean"], skipped: [] })]]);
    window.TimeApprovalsList.bulkApprove({ ids: rows.map((r) => r.weekId), rows });
    await flush();
    const post = calls.find((c) => c.method === "POST");
    expect(post.body).toEqual({ weekIds: ["w-clean"] });
    expect(window.showToast).toHaveBeenCalledWith("1 approved, 0 skipped.", "success");
  });

  it("sends nothing when only marked weeks were selected", async () => {
    const calls = installNetwork([]);
    window.TimeApprovalsList.bulkApprove({ ids: ["w-11h", "w-holiday"], rows });
    await flush();
    expect(calls).toHaveLength(0);
    expect(window.showToast).toHaveBeenCalledWith("none", "warning");
  });
});

// ── Approval week ──────────────────────────────────────────────────────────────────────────────────────────────

describe("the approver's read-only week", () => {
  const WEEK_URL = "/TimeEntry/Approvals/api/w-1";
  const week = () => weekPayload({
    weekId: "w-1", displayName: "Ayşe Yılmaz", status: "Submitted", revisionNumber: 2, correctionOfRevision: 1,
    submittedAtUtc: "2026-10-09T09:00:00Z", approvalTaskId: "task-9", approvalTaskVersion: 4,
    flaggedDates: ["2026-10-05"], autoClosedDates: [], holidayDates: [], outsideWorkingMinutes: 0,
    inForceRevisionNumber: 1,
    entries: [
      entry("2026-10-05", 690, { taskItemId: TASK_A, source: "Timer", taskTitle: "Batch record", capturedMinutes: 720, editedFromTimer: true }),
      entry("2026-10-06", 60, { categoryCode: "ADMINISTRATION" })
    ],
    correctionChanges: [
      { localDate: "2026-10-05", taskItemId: TASK_A, categoryCode: null, source: "Timer", sourceRef: null, previousMinutes: 600, currentMinutes: 690, taskTitle: "Batch record" },
      { localDate: "2026-10-06", taskItemId: null, categoryCode: "ADMINISTRATION", source: "Manual", sourceRef: null, previousMinutes: null, currentMinutes: 60 },
      { localDate: "2026-10-07", taskItemId: TASK_B, categoryCode: null, source: "Manual", sourceRef: null, previousMinutes: 45, currentMinutes: null, taskTitle: null }
    ]
  });

  async function bootWeek({ canApprove = true, canReject = true, routes = [] } = {}) {
    loadCore();
    document.body.innerHTML = `
      <div id="timeApprovalWeek" data-week-id="w-1" data-can-approve="${canApprove}" data-can-reject="${canReject}">
        <h5 id="taTitle"></h5><p id="taMeta"></p><div id="taNotice"></div><p id="taLoading"></p>
        <div id="taContent" hidden><div id="taMarks"></div><p id="taInForce" hidden></p><div id="taGrid"></div>
          <section id="taChangesCard" hidden><div id="taChanges"></div></section><p id="taCannotEdit"></p><div id="taDecision"></div></div>
      </div>`;
    window.L10n = {};
    window.showToast = vi.fn();
    window.showConfirm = vi.fn((_t, yes) => yes());
    window.__timeApprovalNoAutoInit = true;
    const calls = installNetwork(routes.concat([["GET", WEEK_URL, () => ok(week())]]));
    delete window.TimeApprovalWeek;
    loadScript("wwwroot/assets/js/TimeEntry/Approvals/details.js");
    await window.TimeApprovalWeek.init();
    await flush();
    return calls;
  }

  it("every id the test draws is in Details.cshtml", () => {
    const view = ids("Views/TimeEntry/Approvals/Details.cshtml");
    ["timeApprovalWeek", "taTitle", "taMeta", "taNotice", "taLoading", "taContent", "taMarks", "taInForce", "taGrid",
      "taChangesCard", "taChanges", "taCannotEdit", "taDecision"].forEach((id) => expect(view, id).toContain(id));
  });

  it("shows the person's correction of captured time WITH the value first measured, and no edit control", async () => {
    await bootWeek();
    const mark = document.querySelector("#taGrid [data-captured]");
    expect(mark.getAttribute("data-captured")).toBe("720");
    expect(mark.textContent).toContain("CapturedValue");
    expect(document.querySelector("#taGrid input, #taGrid textarea")).toBeNull();
    expect(document.getElementById("taMarks").textContent).toContain("MarkFlagged");
  });

  it("shows what the correction changes against the revision in force: changed, added, removed", async () => {
    await bootWeek();
    expect(document.getElementById("taChangesCard").hidden).toBe(false);
    const rowsEls = Array.from(document.querySelectorAll("#taChanges tbody tr"));
    expect(rowsEls).toHaveLength(3);
    expect(rowsEls[0].querySelector(".time-entry-change-before").textContent).toBe("10:00");
    expect(rowsEls[0].querySelector(".time-entry-change-after").textContent).toBe("11:30");
    expect(rowsEls[1].classList.contains("time-entry-change-added")).toBe(true);
    expect(rowsEls[2].classList.contains("time-entry-change-removed")).toBe(true);
    expect(rowsEls[2].textContent).toContain("UnreadableTask");
    expect(document.getElementById("taInForce").hidden).toBe(false);
  });

  it("a return without a reason is stopped on the page — nothing is sent", async () => {
    const calls = await bootWeek();
    document.getElementById("taReject").click();
    document.getElementById("taRejectSend").click();
    await flush();
    expect(document.getElementById("taRejectError").hidden).toBe(false);
    expect(calls.some((c) => c.method === "POST")).toBe(false);
  });

  it("a return with its reason goes to the decisions route with the approval task (Task Center's path)", async () => {
    const calls = await bootWeek({ routes: [["POST", "/TimeEntry/Approvals/api/decisions/reject", () => ok({})]] });
    document.getElementById("taReject").click();
    document.getElementById("taRejectReason").value = "Tuesday is missing";
    document.getElementById("taRejectSend").click();
    await flush();
    const post = calls.find((c) => c.method === "POST");
    expect(post.body).toEqual({ approvalTaskId: "task-9", expectedVersion: 4, comment: "Tuesday is missing" });
    expect(document.getElementById("taDecision").children).toHaveLength(0);
  });

  it("approve asks first, then sends the approval task and its version, with no comment", async () => {
    const calls = await bootWeek({ routes: [["POST", "/TimeEntry/Approvals/api/decisions/approve", () => ok({})]] });
    document.getElementById("taApprove").click();
    await flush();
    expect(window.showConfirm).toHaveBeenCalled();
    expect(calls.find((c) => c.method === "POST").body).toEqual({ approvalTaskId: "task-9", expectedVersion: 4, comment: null });
  });

  it("MOD-0023's own refusal reads as its sentence", async () => {
    await bootWeek({ routes: [["POST", "/TimeEntry/Approvals/api/decisions/reject", () => refused(400, "WORKFLOW_REJECT_COMMENT_REQUIRED")]] });
    window.L10n = { ErrRejectCommentRequired: "A reason is required." };
    document.getElementById("taReject").click();
    document.getElementById("taRejectReason").value = " x ";
    document.getElementById("taRejectSend").click();
    await flush();
    expect(document.getElementById("taNotice").textContent).toBe("A reason is required.");
  });

  it("without the workflow keys no decision button is drawn", async () => {
    await bootWeek({ canApprove: false, canReject: false });
    expect(document.getElementById("taDecision").children).toHaveLength(0);
  });
});

// ── Categories ─────────────────────────────────────────────────────────────────────────────────────────────────

describe("work categories — the code never changes", () => {
  const stored = { id: "c-1", code: "QUALITY", labelText: "Quality", labelResourceKey: null, description: null, countsAsWork: true, sortOrder: 5, isActive: true, version: 3 };

  beforeAll(() => {
    loadCore();
    loadScript("wwwroot/assets/js/TimeEntry/Categories/index.js");
  });

  beforeEach(() => {
    document.body.innerHTML = `
      <input id="categoryId"><input id="categoryCode"><div id="categoryCodeHint"></div><input id="categoryLabel">
      <textarea id="categoryDescription"></textarea><input id="categorySortOrder"><input type="checkbox" id="categoryCountsAsWork">`;
    window.L10n = { CodeLocked: "locked", CodeHint: "hint", CategoryLabels: { "TimeEntry.Category.TRAINING": "Training" } };
    window.TimeCategoriesList.useRows([stored]);
  });

  it("every form id the test draws is in the offcanvas", () => {
    const view = ids("Views/TimeEntry/Categories/_CreateEditOffcanvas.cshtml");
    ["categoryId", "categoryCode", "categoryCodeHint", "categoryLabel", "categoryDescription", "categorySortOrder",
      "categoryCountsAsWork", "offcanvasCreateEdit", "formWorkCategory", "btnSaveCategory"].forEach((id) => expect(view, id).toContain(id));
  });

  it("on edit the code field is locked and the update sends the STORED code, whatever the field holds", async () => {
    await window.TimeCategoriesList.loadFormFields("c-1");
    expect(document.getElementById("categoryCode").readOnly).toBe(true);
    document.getElementById("categoryCode").value = "HACKED";
    document.getElementById("categoryLabel").value = "Quality review";
    const body = window.TimeCategoriesList.bodyOf(true);
    expect(body).toMatchObject({ code: "QUALITY", expectedVersion: 3, labelText: "Quality review" });
  });

  it("a new category sends the typed code, upper-cased", () => {
    window.TimeCategoriesList.resetFormFields();
    document.getElementById("categoryCode").value = "travel_abroad";
    document.getElementById("categoryLabel").value = "Travel abroad";
    expect(window.TimeCategoriesList.bodyOf(false)).toMatchObject({ code: "TRAVEL_ABROAD", labelText: "Travel abroad" });
    expect(document.getElementById("categoryCode").readOnly).toBe(false);
  });

  it("a recommended category reads its translation until someone types their own", () => {
    expect(window.TimeCategoriesList.labelOf({ code: "TRAINING", labelText: null, labelResourceKey: "TimeEntry.Category.TRAINING" })).toBe("Training");
  });

  it("deactivating never deletes: it posts deactivate with the row's version", async () => {
    window.showConfirm = vi.fn((_t, yes) => yes());
    window.showToast = vi.fn();
    const calls = installNetwork([["POST", "/TimeEntry/Categories/api/c-1/deactivate", () => ok(stored)]]);
    window.TimeCategoriesList.toggleActive({ row: stored });
    await flush();
    expect(calls).toHaveLength(1);
    expect(calls[0].body).toEqual({ expectedVersion: 3 });
    expect(readSource("wwwroot/assets/js/TimeEntry/Categories/index.js")).not.toMatch(/method:\s*'DELETE'|'DELETE'/);
  });
});

// ── Settings ───────────────────────────────────────────────────────────────────────────────────────────────────

describe("settings — the timer switch needs its reason to go ON", () => {
  const LE_CH = "11111111-0000-0000-0000-00000000c0c0";
  const LE_TR = "22222222-0000-0000-0000-00000000f00d";

  async function bootSettings(extra = []) {
    loadCore();
    document.body.innerHTML = `<div id="timeEntrySettings"><div id="tsNotice"></div>
      <select id="tsPoolPosition"><option value="">none</option></select><button id="tsSavePool"></button>
      <p id="tsLoading"></p><div id="tsSwitches"></div></div>
      <script id="timesettings-l10n" type="application/json">{}</script>`;
    window.showToast = vi.fn();
    window.showConfirm = vi.fn((_t, yes) => yes());
    window.__timeSettingsNoAutoInit = true;
    const calls = installNetwork(extra.concat([
      ["GET", "/TimeEntry/api/settings/legal-entities", () => ok([{ legalEntityId: LE_TR, timerEnabled: true, changedAtUtc: "2026-09-01T10:00:00Z", reason: "KVKK basis", version: 2 }])],
      ["GET", "/TimeEntry/api/settings", () => ok({ timeAdminPoolPositionId: null, version: 1 })],
      ["GET", "/TimeEntry/Settings/lookup/positions", () => ok([{ id: "p-1", code: "HR-ADM", name: "Time admin" }])],
      ["GET", "/TimeEntry/Settings/lookup/legal-entities", () => ok([{ legalEntityId: LE_CH, code: "CH01", displayName: "Diten AG" }, { legalEntityId: LE_TR, code: "TR01", displayName: "Diten A.Ş." }])]
    ]));
    delete window.TimeEntrySettings;
    loadScript("wwwroot/assets/js/TimeEntry/Settings/index.js");
    await window.TimeEntrySettings.init();
    await flush();
    return calls;
  }

  it("lists every legal entity; one without a row is OFF", async () => {
    await bootSettings();
    const rows = window.TimeEntrySettings.rows();
    expect(rows.map((r) => [r.name, !!(r.setting && r.setting.timerEnabled)])).toEqual([["Diten AG", false], ["Diten A.Ş.", true]]);
    expect(document.querySelectorAll("#tsSwitches tbody tr.time-entry-switch-row")).toHaveLength(2);
  });

  it("switching ON without a reason is stopped on the page — nothing is sent", async () => {
    const calls = await bootSettings();
    const row = window.TimeEntrySettings.rows()[0];
    await window.TimeEntrySettings.switchOn(row, "   ");
    expect(calls.some((c) => c.method === "PUT")).toBe(false);
  });

  it("switching ON with its reason sends version 0 for an entity that has no row", async () => {
    const calls = await bootSettings([["PUT", `/TimeEntry/api/settings/legal-entities/${LE_CH}`, (body) => ok({ legalEntityId: LE_CH, timerEnabled: true, changedAtUtc: "2026-10-01T00:00:00Z", reason: body.reason, version: 1 })]]);
    await window.TimeEntrySettings.switchOn(window.TimeEntrySettings.rows()[0], "Counsel answered §22 (letter 2026-09-30)");
    const put = calls.find((c) => c.method === "PUT");
    expect(put.body).toEqual({ expectedVersion: 0, timerEnabled: true, reason: "Counsel answered §22 (letter 2026-09-30)" });
  });

  it("switching OFF asks first and sends the row's version with no reason", async () => {
    const calls = await bootSettings([["PUT", `/TimeEntry/api/settings/legal-entities/${LE_TR}`, () => ok({ legalEntityId: LE_TR, timerEnabled: false, version: 3 })]]);
    window.TimeEntrySettings.switchOff(window.TimeEntrySettings.rows()[1]);
    await flush();
    expect(window.showConfirm).toHaveBeenCalled();
    expect(calls.find((c) => c.method === "PUT").body).toEqual({ expectedVersion: 2, timerEnabled: false, reason: null });
  });

  // CT (BL-486 #2): an unreadable read is not an empty one — nothing is saved on top of a version the page never saw,
  // and a switched-on entity is never shown as OFF because its row could not be read.
  it("settings that could not be read lock the pool and the reminder — nothing is sent", async () => {
    const calls = await bootSettings([["GET", /\/TimeEntry\/api\/settings$/, () => refused(503)]]);
    expect(document.getElementById("tsPoolPosition").disabled).toBe(true);
    expect(document.getElementById("tsSavePool").disabled).toBe(true);
    expect(document.getElementById("tsNotice").textContent).toContain("SettingsUnavailable");
    await window.TimeEntrySettings.savePool();
    await window.TimeEntrySettings.saveReminder(true);
    expect(calls.some((c) => c.method === "PUT")).toBe(false);
  });

  it("switch rows that could not be read show no OFF badge and no switch button", async () => {
    await bootSettings([["GET", "/TimeEntry/api/settings/legal-entities", () => refused(503)]]);
    expect(document.querySelectorAll("#tsSwitches tr.time-entry-switch-row")).toHaveLength(0);
    expect(document.querySelectorAll("#tsSwitches button[data-switch]")).toHaveLength(0);
    expect(document.getElementById("tsSwitches").textContent).toContain("SettingsUnavailable");
  });

  it("the Swiss warning is on the page", () => {
    expect(readSource("Views/TimeEntry/Settings/Index.cshtml")).toContain('id="tsSwissWarning">@Localizer["SwissWarning"]');
  });
});

// ── My Timesheet — CT live findings ────────────────────────────────────────────────────────────────────────────

describe("My Timesheet — T2a live findings E1–E4", () => {
  const WEEK = /\/TimeEntry\/api\/weeks\/2026-W41$/;
  const base = (payload) => [
    ["GET", WEEK, () => ok(payload)],
    ["GET", "/TimeEntry/api/categories", () => ok([])],
    ["GET", "/TimeEntry/api/timer", () => ok({ timerEnabled: false, running: null, closedAtMidnightYesterday: [] })]
  ];

  it("E1 — a day the calendar cannot resolve shows the calendar screens' own notice", async () => {
    const payload = weekPayload();
    payload.days[2].calendarUnresolved = true;
    installNetwork(base(payload));
    await bootPage({ week: "2026-W41" });
    const note = document.getElementById("teCalendarUnresolved");
    expect(note).not.toBeNull();
    expect(note.classList.contains("dc-unresolved-note")).toBe(true);
    expect(note.textContent).toBe("CalendarUnresolved");
  });

  it("E1 — a resolved week shows no notice", async () => {
    installNetwork(base(weekPayload()));
    await bootPage({ week: "2026-W41" });
    expect(document.getElementById("teCalendarUnresolved")).toBeNull();
  });

  it("E2 — an empty week cannot be submitted; one with time can", async () => {
    installNetwork(base(weekPayload()));
    await bootPage({ week: "2026-W41" });
    expect(document.getElementById("teSubmit").disabled).toBe(true);

    installNetwork(base(weekPayload({ entries: [entry("2026-10-05", 60, { taskItemId: TASK_A, taskTitle: "A" })] })));
    await bootPage({ week: "2026-W41" });
    expect(document.getElementById("teSubmit").disabled).toBe(false);
  });

  it("E3 — below 992 px the notices panel goes under the table", () => {
    const css = readSource("wwwroot/assets/css/backbone-custom.css");
    expect(css).toMatch(/\.time-entry-layout > \.time-entry-side \{ order: 2; \}/);
    expect(css).toMatch(/@media \(min-width: 992px\) \{\s*\.time-entry-layout \{ grid-template-columns: minmax\(0, 1fr\) 18rem; \}/);
    expect(css).not.toMatch(/@media \(min-width: 1200px\) \{\s*\.time-entry-layout/);
  });

  it("E4 — the correction panel lists the person's own timer runs behind the captured row", async () => {
    installNetwork(base(weekPayload({
      entries: [entry("2026-10-05", 120, { taskItemId: TASK_A, source: "Timer", taskTitle: "A" })],
      timerSegments: [
        { segmentId: "s1", localDate: "2026-10-05", taskItemId: TASK_A, categoryCode: null, startedAtUtc: "2026-10-05T06:00:00Z", stoppedAtUtc: "2026-10-05T07:00:00Z", durationSeconds: 3600, outsideWorkingMinutes: 0, startSource: "TaskStarted", stopReason: "TimerControl" },
        { segmentId: "s2", localDate: "2026-10-05", taskItemId: TASK_A, categoryCode: null, startedAtUtc: "2026-10-05T20:00:00Z", stoppedAtUtc: "2026-10-05T21:00:00Z", durationSeconds: 3600, outsideWorkingMinutes: 60, startSource: "TimerControl", stopReason: "LocalMidnight" },
        { segmentId: "s3", localDate: "2026-10-06", taskItemId: TASK_A, categoryCode: null, startedAtUtc: "2026-10-06T06:00:00Z", stoppedAtUtc: "2026-10-06T07:00:00Z", durationSeconds: 3600, outsideWorkingMinutes: 0, startSource: "TimerControl", stopReason: "TimerControl" }
      ]
    })));
    await bootPage({ week: "2026-W41" });
    document.querySelector('#teGrid button.time-entry-cell-captured[data-date="2026-10-05"]').click();
    const items = Array.from(document.querySelectorAll("#teSegments li"));
    expect(items).toHaveLength(2);                              // only that day's runs of that task
    expect(items[1].textContent).toContain("SegmentCutAtMidnight");
  });
});

// ── Cross-cutting: no inline style, logical sides ──────────────────────────────────────────────────────────────

describe("no inline style and right-to-left safe CSS", () => {
  const files = [
    "wwwroot/assets/js/TimeEntry/Approvals/index.js", "wwwroot/assets/js/TimeEntry/Approvals/details.js",
    "wwwroot/assets/js/TimeEntry/Categories/index.js", "wwwroot/assets/js/TimeEntry/Settings/index.js",
    "Views/TimeEntry/Approvals/Index.cshtml", "Views/TimeEntry/Approvals/Details.cshtml", "Views/TimeEntry/Categories/Index.cshtml",
    "Views/TimeEntry/Categories/_CreateEditOffcanvas.cshtml", "Views/TimeEntry/Settings/Index.cshtml"
  ];

  it.each(files)("%s writes no style", (file) => {
    expect(readSource(file)).not.toMatch(/\.style\.|\sstyle=|setAttribute\(\s*['"]style/);
  });

  it("the T2b CSS uses logical sides only", () => {
    const css = readSource("wwwroot/assets/css/backbone-custom.css");
    const block = css.slice(css.indexOf("MOD-0280-FU01 T2b — Task Center time card"));
    expect(block.length).toBeGreaterThan(200);
    expect(block).not.toMatch(/(margin|padding)-(left|right)\s*:|(^|[^-])(left|right)\s*:\s*\d|text-align:\s*(left|right)/m);
    expect(block).toContain('[dir="rtl"] .wcn-timer-undo');
  });
});
