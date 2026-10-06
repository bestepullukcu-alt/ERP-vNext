const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * MOD-0280-FU01 T2b (pack §19.2, U1) — the Task Center's time card on the REAL app.js: the figures come from the
 * provider's `timeEntries` block, the timer buttons are the provider's own startTimer / stopTimer actions dispatched on
 * the one work-item action path, the top-bar chip is told to ask again, a switch offers "Undo", and the browser timer
 * that measured nothing is gone — code and resx.
 */
const TASK_ID = "5b1b4a2e-0c3d-4e5f-8a9b-0c1d2e3f4a5b";
const webRoot = path.resolve(__dirname, "..");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const APP = fs.readFileSync(path.join(webRoot, "wwwroot/assets/js/WorkCenterNext/app.js"), "utf8");
const MOCK = fs.readFileSync(path.join(webRoot, "wwwroot/assets/js/WorkCenterNext/mock-data.js"), "utf8");
const code = (src) => src.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");
const resx = (lang) => fs.readFileSync(path.join(webRoot, `Resources/Views/WorkCenterNext/WorkCenterNextIndex.${lang}.resx`), "utf8");

const action = (codeName) => ({
  code: codeName,
  label: { kind: "resource", key: codeName === "startTimer" ? "WorkAggregation_Action_StartTimer" : "WorkAggregation_Action_StopTimer" },
  semanticType: "custom", enabled: true, source: "provider", disabledReasonCode: null, disabledReason: null,
  requiresConfirmation: false, requiresReason: false, requiresEvidence: false, supportsBulk: false, riskLevel: "normal"
});

const item = (overrides) => Object.assign({
  fixtureKind: "workItem", id: TASK_ID, workIntent: "task", assignmentMode: "direct", ownershipState: "owned",
  admissionState: "admitted", normalizedStatus: "InProgress", taskLifecycle: "InProgress", executionState: "active",
  timerState: "inactive", systemState: "fresh", actionDepth: "inline",
  title: { kind: "display", text: "Batch record review", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: { providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID, deepLink: `/Tasks/${TASK_ID}` },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution", "timeTracking"],
  timeEntries: { draftMinutes: 30, submittedMinutes: 60, approvedMinutes: 125 },
  actions: [action("startTimer")],
  concurrency: { kind: "version", token: "3" },
  waitingContext: null, escalation: null, dueAt: "2026-10-30T00:00:00+00:00"
}, overrides);

// A translator that shows its arguments, so "1:05" and "0:30" are told apart without a resource file.
const wcn = { t: (key) => key, tf: (key, ...args) => `${key}(${args.join(",")})`, tn: (key) => key };

async function bootDetail(projection) {
  window.showToast = vi.fn();
  window.DitenTimerChip = { refresh: vi.fn(() => Promise.resolve()) };
  window.DitenTimerShared = { read: vi.fn(() => Promise.resolve({ ok: true, data: { running: null } })) };
  await bootSurface({ rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`, items: [projection], wcn });
  await new Promise((r) => setTimeout(r, 0));
}

const flush = async () => { for (let i = 0; i < 6; i += 1) { await new Promise((r) => setTimeout(r, 0)); } };

afterEach(() => {
  delete window.DitenTimerChip;
  delete window.DitenTimerShared;
  document.getElementById("wcnTimerUndo")?.remove();
});

describe("the time card reads the provider's real timeEntries block", () => {
  it("shows my draft, the submitted and the approved time — not a browser total", async () => {
    await bootDetail(item());
    const card = app().querySelector(".wcn-timesheet");
    expect(card).not.toBeNull();
    expect(card.querySelector(".wcn-ts-draft").textContent).toContain("TimeHM(0,30)");
    expect(card.querySelector(".wcn-ts-submitted").textContent).toContain("TimeHM(1,0)");
    expect(card.querySelector(".wcn-ts-approved").textContent).toContain("TimeHM(2,5)");
    expect(app().querySelector('.wcn-ts-sheet').getAttribute("href")).toBe("/TimeEntry");
  });

  it("draws no card when time is not tracked", async () => {
    await bootDetail(item({ workItemCapabilities: ["planning", "execution"], timeEntries: undefined, actions: [] }));
    expect(app().querySelector(".wcn-timesheet")).toBeNull();
  });
});

describe("start and stop are the provider's own actions, on the one action path", () => {
  it("offers Start on the card, dispatches startTimer, and tells the chip to ask again", async () => {
    await bootDetail(item());
    const calls = [];
    global.WorkCenterNextApi.dispatchAction = async (itemId, actionCode, providerCode, body) => {
      calls.push({ itemId, actionCode, providerCode, body });
      return { ok: true, status: 200, data: {} };
    };

    const start = app().querySelector('.wcn-ts-actions [data-wcn-action="startTimer"]');
    expect(start).not.toBeNull();
    start.click();
    await flush();

    expect(calls).toHaveLength(1);
    expect(calls[0]).toMatchObject({ itemId: TASK_ID, actionCode: "startTimer", providerCode: "tasks" });
    expect(window.DitenTimerChip.refresh).toHaveBeenCalled();
  });

  it("offers Stop and says the timer runs when the server says it runs", async () => {
    await bootDetail(item({ timerState: "running", actions: [action("stopTimer")] }));
    expect(app().querySelector('.wcn-ts-actions [data-wcn-action="stopTimer"]')).not.toBeNull();
    expect(app().querySelector(".wcn-ts-state").textContent).toBe("TimerRunningNow");
  });

  it("a switch away from another task offers Undo, which calls TimeEntry's own undo-switch", async () => {
    await bootDetail(item());
    global.WorkCenterNextApi.dispatchAction = async () => ({ ok: true, status: 200, data: {} });
    window.DitenTimerShared.read = vi.fn(() => Promise.resolve({
      ok: true, data: { running: { taskItemId: TASK_ID, switchToken: "tok-1", undoUntilUtc: new Date(Date.now() + 60000).toISOString() } }
    }));
    const posts = [];
    global.fetch = window.fetch = vi.fn(async (url, options) => {
      posts.push({ url, body: JSON.parse(options.body) });
      return { ok: true, status: 200, json: async () => ({ data: {} }) };
    });

    app().querySelector('[data-wcn-action="startTimer"]').click();
    await flush();
    const bar = document.getElementById("wcnTimerUndo");
    expect(bar).not.toBeNull();
    expect(bar.textContent).toContain("TimerSwitchedText");

    bar.querySelector("[data-wcn-timer-undo]").click();
    await flush();
    expect(posts).toEqual([{ url: "/TimeEntry/api/timer/undo-switch", body: { switchToken: "tok-1" } }]);
    expect(document.getElementById("wcnTimerUndo")).toBeNull();
  });

  it("a timer refusal is said in its own words (switched off for the legal entity)", async () => {
    await bootDetail(item());
    global.WorkCenterNextApi.dispatchAction = async () => ({ ok: false, status: 409, reasonCode: "TIMER_DISABLED_FOR_LEGAL_ENTITY" });

    app().querySelector('[data-wcn-action="startTimer"]').click();
    await flush();

    expect(window.showToast).toHaveBeenCalledWith("TimerErrDisabled", "error");
    expect(window.DitenTimerChip.refresh).toHaveBeenCalled();
  });
});

describe("the browser timer is gone — code and words", () => {
  it("no fold, no invented anchor, no local timesheet, no 'timer started (mock)' toast", () => {
    const stripped = code(APP);
    expect(stripped).not.toContain("foldTimer");
    expect(stripped).not.toMatch(/item\.timesheet\b/);
    expect(stripped).not.toContain("ToastTimerStarted");
    expect(stripped).not.toContain("'timerStart'");
    expect(code(MOCK)).not.toMatch(/item\.timesheet\s*=|37 \* 60000/);
  });

  it("the mock-timer resx keys are gone in all seven languages, the card's own words are there", () => {
    LANGS.forEach((lang) => {
      const xml = resx(lang);
      ["ToastTimerStarted", "TimerFollowsStatusHint", "TimerRunning\"", "TimeLoggedLabel", "TimerStateRunning"].forEach((key) => {
        expect(xml, `${lang} still has ${key}`).not.toContain(`name="${key}`);
      });
      ["TimeDraftLabel", "TimeSubmittedLabel", "TimeApprovedLabel", "TimerRunningNow", "OpenMyTimesheet", "TimerUndo",
        "TimerErrDisabled", "WorkAggregation_Action_StartTimer", "WorkAggregation_Action_StopTimer"].forEach((key) => {
        expect(xml, `${lang} lacks ${key}`).toContain(`name="${key}"`);
      });
    });
  });
});
