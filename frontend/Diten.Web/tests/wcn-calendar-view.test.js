const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");
const { bootSurface, app } = require("./wcn-boot");

/*
 * WP-UI-CALENDAR-VIEW-01 (calendar 2b) — the Task Center's calendar, measured on the REAL pieces:
 *   the vendored FullCalendar 6.1.15 bundle, shared/diten-zoned-time.js, shared/diten-calendar.js and app.js.
 * The ONLY stubs are the network seams (fetchWorkItems in wcn-boot, fetchCalendar and dispatchAction here) and the
 * layout numbers jsdom does not compute (a row's rectangle), which a drop is resolved against.
 *
 * What a jsdom test cannot do is a pointer DRAG: FullCalendar's own gesture needs real layout. So a move, a resize
 * and a drag-out are driven through FullCalendar's own callback emitter (`calendar.trigger`) with the calendar's
 * real EventApi objects, and a drop from the panel through a real `drop` DOM event on a real calendar cell. The
 * gesture itself is measured live (CT).
 *
 * Tenant: Europe/Istanbul (UTC+3). "Today" is Wednesday 2026-10-07; the week is Mon 5 – Sun 11 October.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const APP = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const TODAY = "2026-10-07";
const ZONE = "Europe/Istanbul";

const id = (n) => `dddddddd-0000-0000-0000-${String(n).padStart(12, "0")}`;
const MEETING_ID = "eeeeeeee-0000-0000-0000-000000000001";

const action = (code, extra = {}) => Object.assign({
  code, label: { kind: "display", text: code, locale: "und" },
  semanticType: code === "plan" ? "plan" : "complete", enabled: true, source: "provider",
  disabledReasonCode: null, disabledReason: null,
  requiresConfirmation: false, requiresReason: false, requiresEvidence: false,
  supportsBulk: false, riskLevel: "normal"
}, extra);

const task = (n, { planned = null, estimate = null, actions = [action("plan")] } = {}) => ({
  fixtureKind: "workItem",
  id: id(n),
  workIntent: "task",
  assignmentMode: "direct",
  ownershipState: "owned",
  admissionState: "admitted",
  normalizedStatus: "Pending",
  taskLifecycle: planned ? "Planned" : "Open",
  executionState: "notStarted",
  timerState: "notApplicable",
  systemState: "fresh",
  actionDepth: "inline",
  title: { kind: "display", text: `İş ${n}`, locale: "und" },
  nativeStatus: { code: "Open", label: { kind: "display", text: "Open", locale: "und" } },
  source: { providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: id(n), deepLink: `/Tasks/${id(n)}` },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["execution"],
  actions,
  primaryActionCode: actions[0] ? actions[0].code : null,
  overflowActionCodes: [],
  concurrency: { kind: "version", token: String(10 + n) },
  waitingContext: null,
  escalation: null,
  dueAt: "2026-10-20T00:00:00+00:00",
  plannedDate: planned,
  estimateHours: estimate,
  slaState: "no-sla"
});

const feed = (overrides = {}) => Object.assign({
  timeZoneId: ZONE,
  from: "2026-09-28",
  to: "2026-11-08",
  tasks: [],
  meetings: [],
  days: [],
  unplannedCount: 7,
  planPassedCount: 3,
  pendingInviteCount: 2
}, overrides);

/** Working days Mon–Fri with an Istanbul 09:00–18:00 window (06:00–15:00Z), a weekend and one holiday. */
const weekDays = () => ["2026-10-05", "2026-10-06", "2026-10-07", "2026-10-08", "2026-10-09", "2026-10-10", "2026-10-11"]
  .map((date, i) => {
    if (i >= 5) { return { date, dayKind: "weekend", holidayName: null, windows: [], resolvedFrom: "tenantDefault", calendarUnresolved: false }; }
    if (date === "2026-10-08") { return { date, dayKind: "holiday", holidayName: "Test Bayramı", windows: [], resolvedFrom: "tenantDefault", calendarUnresolved: false }; }
    return { date, dayKind: "workingDay", holidayName: null, windows: [{ startAt: `${date}T06:00:00Z`, endAt: `${date}T15:00:00Z` }], resolvedFrom: "tenantDefault", calendarUnresolved: false };
  });

const CHROME = {
  CalendarToday: "BUGÜN-KÖPRÜ", CalendarMonth: "AY-KÖPRÜ", CalendarWeek: "HAFTA-KÖPRÜ", CalendarDay: "GÜN-KÖPRÜ",
  CalendarPrevious: "ÖNCEKİ", CalendarNext: "SONRAKİ", CalendarAllDay: "TÜM-GÜN-KÖPRÜ", CalendarMore: "+{0} DAHA", CalendarNoEvents: "YOK"
};

let calls;
let toasts;
let feedAnswer;
let fetchedRanges;

beforeAll(() => {
  loadScript("wwwroot/assets/vendor/libs/fullcalendar/fullcalendar.js");
  loadScript("wwwroot/assets/js/shared/diten-calendar.js");
});

afterEach(() => {
  document.documentElement.lang = "tr";
  document.getElementById("diten-calendar-l10n")?.remove();
});

const tick = () => new Promise((resolve) => { setTimeout(resolve, 0); });
const settle = async () => { for (let i = 0; i < 6; i += 1) { await tick(); } };

/**
 * Boots the page on a tab, switches to the calendar view, and stubs the two calendar network seams. The chrome
 * strings are put in the head (the partial's payload), where a body re-render cannot wipe them.
 */
const boot = async ({ items, tab = "islerim", feedData = feed(), dispatch = () => ({ ok: true, status: 200, data: { warnings: [] } }), lang = "tr" }) => {
  document.documentElement.lang = lang;
  document.getElementById("diten-calendar-l10n")?.remove();
  const payload = document.createElement("script");
  payload.id = "diten-calendar-l10n";
  payload.type = "application/json";
  payload.textContent = JSON.stringify(CHROME);
  document.head.appendChild(payload);

  /*
   * v2 F4 — the calendar's "today" is the TENANT's day right now (Date.now in the feed's zone), not the page's
   * pinned today; so the wall clock itself is pinned too: 09:00Z = 12:00 in Istanbul on Wednesday 7 October.
   * restoreMocks (vitest.config) puts it back after every test.
   */
  vi.spyOn(Date, "now").mockReturnValue(Date.parse(`${TODAY}T09:00:00Z`));
  calls = [];
  toasts = [];
  fetchedRanges = [];
  feedAnswer = feedData;
  global.showToast = (message, type) => toasts.push({ message, type: type || "success" });

  await bootSurface({
    rootAttrs: 'data-wcn-page="list"',
    items,
    now: () => new Date(TODAY + "T12:00:00"),
    wcn: { t: (k) => k, tf: (k, ...a) => (a.length ? `${k}:${a.join("|")}` : k), tn: (k) => k }
  });
  global.WorkCenterNextApi.fetchCalendar = (from, to) => {
    fetchedRanges.push(`${from}|${to}`);
    // A CIRCUIT BREAKER, not a convenience: a render that re-asks the feed every time loops forever (fetch →
    // render → datesSet → fetch). Past 20 asks the stub stops answering, so such a regression FAILS on the count
    // below instead of hanging the whole run (measured: it hung the worker for 20 minutes before this).
    if (fetchedRanges.length > 20) { return new Promise(() => {}); }
    return Promise.resolve(typeof feedAnswer === "function" ? feedAnswer(from, to) : { ok: true, status: 200, data: feedAnswer });
  };
  global.WorkCenterNextApi.dispatchAction = (itemId, actionCode, providerCode, payload) => {
    calls.push({ itemId, actionCode, providerCode, payload });
    return Promise.resolve(dispatch(itemId, actionCode, payload));
  };
  global.TasksApi.planWarningMessage = (w) => `WARN:${w.code}:${w.title || ""}`;

  app().querySelector(`[data-wcn-tab="${tab}"]`).click();
  await tick();
  app().querySelector('[data-wcn-view="calendar"]').click();
  await settle();
};

const cal = () => global.__wcnCalendar.calendar;
const host = () => app().querySelector("[data-wcn-calendar-host]");

const toWeek = async () => {
  host().querySelector(".fc-timeGridWeek-button").click();
  await settle();
};

/** A real `drop` DOM event on a real calendar element, carrying an item id the way a panel card does. */
const dropOn = async (target, itemId, clientY = 0) => {
  const event = new Event("drop", { bubbles: true, cancelable: true });
  Object.defineProperty(event, "dataTransfer", {
    value: { types: [global.DitenCalendar.DRAG_TYPE], getData: (type) => (type === global.DitenCalendar.DRAG_TYPE ? itemId : "") }
  });
  Object.defineProperty(event, "clientY", { value: clientY });
  target.dispatchEvent(event);
  await settle();
};

/** jsdom has no layout: give the 15-minute slot rows a 10px-tall stack so a clientY means a time. */
const stackSlotRows = () => {
  host().querySelectorAll("td.fc-timegrid-slot-lane[data-time]").forEach((row, i) => {
    row.getBoundingClientRect = () => ({ top: i * 10, bottom: i * 10 + 10, left: 0, right: 100, width: 100, height: 10 });
  });
};

// ── the planning board (İşlerim) ──────────────────────────────────────────────────────────────────────────

describe("dropping a card from the panel plans it", () => {
  it("a DAY in the month view is a day plan, dressed with the tenant offset", async () => {
    await boot({ items: [task(1)] });
    const cell = host().querySelector('.fc-daygrid-day[data-date="2026-10-09"]');
    expect(cell, "the month view did not draw the 9th").not.toBeNull();

    await dropOn(cell, id(1));

    expect(calls).toEqual([{
      itemId: id(1), actionCode: "plan", providerCode: "tasks",
      payload: { expectedVersion: 11, plannedDate: "2026-10-09T00:00:00+03:00" }
    }]);
  });

  it("a TIME in the week view is a block: 10:00 Istanbul is 07:00Z, length = the estimate", async () => {
    await boot({ items: [task(1, { estimate: 1.5 })] });
    await toWeek();
    stackSlotRows();
    const column = host().querySelector('.fc-timegrid-col[data-date="2026-10-07"]');

    await dropOn(column, id(1), 405); // row 40 = 10:00

    expect(calls[0].payload).toEqual({ expectedVersion: 11, plannedStartAt: "2026-10-07T07:00:00.000Z", plannedDurationMinutes: 90 });
  });

  it("no estimate means 60 minutes", async () => {
    await boot({ items: [task(1)] });
    await toWeek();
    stackSlotRows();

    await dropOn(host().querySelector('.fc-timegrid-col[data-date="2026-10-06"]'), id(1), 575); // row 57 = 14:15

    expect(calls[0].payload).toEqual({ expectedVersion: 11, plannedStartAt: "2026-10-06T11:15:00.000Z", plannedDurationMinutes: 60 });
  });

  it("a card the reader may not plan carries nothing and plans nothing", async () => {
    await boot({ items: [task(1, { actions: [action("plan", { enabled: false, disabledReasonCode: "PERMISSION_DENIED", disabledReason: { kind: "resource", key: "WorkAggregation_ActionDisabled_PermissionDenied" } })] })] });

    await dropOn(host().querySelector('.fc-daygrid-day[data-date="2026-10-09"]'), id(1));

    expect(calls).toEqual([]);
  });
});

describe("moving, stretching and taking a block back off", () => {
  const planned = () => feed({
    tasks: [{
      taskId: id(1), title: "İş 1", priority: "Medium", lifecycle: "Planned", plannedDate: "2026-10-07",
      plannedStartAt: "2026-10-07T07:00:00Z", plannedDurationMinutes: 60, plannedEndAt: "2026-10-07T08:00:00Z",
      remainingMinutes: null, dueAt: null, conflict: false
    }],
    days: weekDays()
  });

  it("moving a block is a re-plan at the new time, same length", async () => {
    await boot({ items: [task(1, { planned: "2026-10-07" })], feedData: planned() });
    await toWeek();
    const event = cal().getEventById(id(1));
    expect(event, "the block is not on the calendar").not.toBeNull();
    expect(event.start.toISOString(), "the block is not drawn at Istanbul wall-clock time").toBe("2026-10-07T10:00:00.000Z");

    event.setDates("2026-10-08T13:30:00Z", "2026-10-08T14:30:00Z");
    cal().trigger("eventDrop", { event, oldEvent: event, revert: () => {}, view: cal().view });
    await settle();

    expect(calls[0]).toMatchObject({ actionCode: "plan", payload: { expectedVersion: 11, plannedStartAt: "2026-10-08T10:30:00.000Z", plannedDurationMinutes: 60 } });
  });

  it("stretching a block changes its length", async () => {
    await boot({ items: [task(1, { planned: "2026-10-07" })], feedData: planned() });
    await toWeek();
    const event = cal().getEventById(id(1));

    event.setEnd("2026-10-07T11:45:00Z");
    cal().trigger("eventResize", { event, oldEvent: event, revert: () => {}, view: cal().view });
    await settle();

    expect(calls[0].payload).toEqual({ expectedVersion: 11, plannedStartAt: "2026-10-07T07:00:00.000Z", plannedDurationMinutes: 105 });
  });

  it("dragging a block back onto the panel is `unplan`", async () => {
    await boot({ items: [task(1, { planned: "2026-10-07" })], feedData: planned() });
    await toWeek();
    const panel = app().querySelector("[data-wcn-calpanel-dropzone]");
    panel.getBoundingClientRect = () => ({ left: 0, right: 300, top: 0, bottom: 800, width: 300, height: 800 });
    global.__wcnCalendar.calendar; // the seam is the mounted controller
    const event = cal().getEventById(id(1));

    // The component was mounted with THIS panel as its drop-out target; a re-render would have re-mounted it.
    cal().trigger("eventDragStop", { event, jsEvent: { clientX: 120, clientY: 300 }, view: cal().view });
    await settle();

    expect(calls).toEqual([{ itemId: id(1), actionCode: "unplan", providerCode: "tasks", payload: { expectedVersion: 11 } }]);
  });

  it("a drag that ends anywhere else is not an unplan", async () => {
    await boot({ items: [task(1, { planned: "2026-10-07" })], feedData: planned() });
    await toWeek();
    const panel = app().querySelector("[data-wcn-calpanel-dropzone]");
    panel.getBoundingClientRect = () => ({ left: 0, right: 300, top: 0, bottom: 800, width: 300, height: 800 });

    cal().trigger("eventDragStop", { event: cal().getEventById(id(1)), jsEvent: { clientX: 900, clientY: 300 }, view: cal().view });
    await settle();

    expect(calls).toEqual([]);
  });
});

describe("what the engine answers is shown, not decided", () => {
  const planned = () => feed({
    tasks: [{
      taskId: id(1), title: "İş 1", priority: "Medium", lifecycle: "Planned", plannedDate: "2026-10-07",
      plannedStartAt: "2026-10-07T07:00:00Z", plannedDurationMinutes: 60, plannedEndAt: "2026-10-07T08:00:00Z",
      remainingMinutes: null, dueAt: null, conflict: false
    }]
  });

  it("a 409 conflict is REVERTED and names the other block and its hours", async () => {
    const dispatch = () => ({
      ok: false, status: 409, reasonCode: "TASK_PLAN_CONFLICT",
      data: { conflict: { title: "Rapor taslağı", startAt: "2026-10-08T07:00:00Z", endAt: "2026-10-08T08:00:00Z" } }
    });
    await boot({ items: [task(1, { planned: "2026-10-07" })], feedData: planned(), dispatch });
    await toWeek();
    const event = cal().getEventById(id(1));
    let reverted = 0;

    event.setDates("2026-10-08T10:30:00Z", "2026-10-08T11:30:00Z");
    cal().trigger("eventDrop", { event, oldEvent: event, revert: () => { reverted += 1; }, view: cal().view });
    await settle();

    expect(reverted, "the dropped block stayed where the engine refused it").toBe(1);
    const said = toasts.find((t) => t.type === "error");
    expect(said.message).toMatch(/^CalConflictWith:Rapor taslağı\|10.00–11.00$/);
  });

  it("a saved plan's warnings are said, and the block is marked", async () => {
    const dispatch = () => ({
      ok: true, status: 200,
      data: { warnings: [{ code: "TASK_PLAN_OVERLAPS_MEETING", title: "Haftalık kalite" }], truncated: false, remainingMinutes: null }
    });
    await boot({ items: [task(1, { planned: "2026-10-07" })], feedData: planned(), dispatch });
    await toWeek();
    const event = cal().getEventById(id(1));

    event.setDates("2026-10-07T11:00:00Z", "2026-10-07T12:00:00Z");
    cal().trigger("eventDrop", { event, oldEvent: event, revert: () => {}, view: cal().view });
    await settle();

    expect(toasts.map((t) => t.message)).toContain("WARN:TASK_PLAN_OVERLAPS_MEETING:Haftalık kalite");
    expect(toasts.find((t) => t.message.startsWith("WARN:")).type).toBe("info");
    const block = host().querySelector(".dc-task-block");
    expect(block.classList.contains("dc-warned"), "the warned block carries no mark").toBe(true);
    expect(block.querySelector(".dc-event-warning")).not.toBeNull();
  });

  it("a block cut at the end of the day says how much is left, and the chip shows it", async () => {
    const cut = feed({
      tasks: [{
        taskId: id(1), title: "İş 1", priority: "Medium", lifecycle: "Planned", plannedDate: "2026-10-07",
        plannedStartAt: "2026-10-07T13:00:00Z", plannedDurationMinutes: 120, plannedEndAt: "2026-10-07T15:00:00Z",
        remainingMinutes: 120, dueAt: null, conflict: false
      }]
    });
    const dispatch = () => ({ ok: true, status: 200, data: { warnings: [], truncated: true, remainingMinutes: 120 } });
    await boot({ items: [task(1, { estimate: 4 })], feedData: cut, dispatch });
    await toWeek();
    stackSlotRows();

    await dropOn(host().querySelector('.fc-timegrid-col[data-date="2026-10-07"]'), id(1), 645); // 16:00

    expect(toasts.find((t) => t.type === "warning").message).toBe("CalTruncated:120");
    expect(host().querySelector(".dc-event-chip").textContent).toBe("CalChipRemaining:120");
  });

  it("a block the engine flags as overlapping is drawn with the conflict frame", async () => {
    const both = feed({
      tasks: [id(1), id(2)].map((taskId) => ({
        taskId, title: taskId, priority: "Medium", lifecycle: "Planned", plannedDate: "2026-10-07",
        plannedStartAt: "2026-10-07T07:00:00Z", plannedDurationMinutes: 60, plannedEndAt: "2026-10-07T08:00:00Z",
        remainingMinutes: null, dueAt: null, conflict: true
      }))
    });
    await boot({ items: [task(1, { planned: "2026-10-07" }), task(2, { planned: "2026-10-07" })], feedData: both });
    await toWeek();

    expect(host().querySelectorAll(".dc-conflict")).toHaveLength(2);
  });
});

describe("meetings are shown, never moved", () => {
  const withMeetings = () => feed({
    meetings: [
      { meetingId: MEETING_ID, title: "Bekleyen toplantı", startAt: "2026-10-07T11:00:00Z", endAt: "2026-10-07T12:00:00Z", response: "pending" },
      { meetingId: "eeeeeeee-0000-0000-0000-000000000002", title: "Kabul edilen", startAt: "2026-10-06T08:00:00Z", endAt: "2026-10-06T09:00:00Z", response: "accepted" }
    ]
  });

  it("a meeting cannot be dragged or stretched", async () => {
    await boot({ items: [task(1)], feedData: withMeetings() });
    await toWeek();

    const meeting = cal().getEventById("meeting:" + MEETING_ID);
    expect(meeting.startEditable).toBe(false);
    expect(meeting.durationEditable).toBe(false);
    expect(host().querySelectorAll(".dc-meeting.fc-event-draggable")).toHaveLength(0);
  });

  it("a pending meeting is dashed, an accepted one is filled, and only the feed's meetings are drawn", async () => {
    await boot({ items: [task(1)], feedData: withMeetings() });
    await toWeek();

    expect(host().querySelectorAll(".dc-meeting")).toHaveLength(2);
    expect(host().querySelectorAll(".dc-meeting-pending")).toHaveLength(1);
    expect(host().querySelectorAll(".dc-meeting-accepted")).toHaveLength(1);
  });

  it("dropping a meeting's move does not write (defence in depth)", async () => {
    await boot({ items: [task(1)], feedData: withMeetings() });
    await toWeek();
    const meeting = cal().getEventById("meeting:" + MEETING_ID);
    let reverted = 0;

    cal().trigger("eventDrop", { event: meeting, oldEvent: meeting, revert: () => { reverted += 1; }, view: cal().view });
    await settle();

    expect(calls).toEqual([]);
    expect(reverted).toBe(1);
  });
});

describe("the working day is drawn from the feed", () => {
  it("weekend and holiday days are shaded, the holiday carries its name", async () => {
    await boot({ items: [task(1)], feedData: feed({ days: weekDays() }) });

    expect(host().querySelector('.fc-daygrid-day[data-date="2026-10-10"]').classList.contains("dc-day-weekend")).toBe(true);
    const holiday = host().querySelector('.fc-daygrid-day[data-date="2026-10-08"]');
    expect(holiday.classList.contains("dc-day-holiday")).toBe(true);
    expect(host().querySelector(".dc-holiday-band").textContent).toContain("Test Bayramı");
  });

  it("hours outside the windows are non-business in the week view", async () => {
    await boot({ items: [task(1)], feedData: feed({ days: weekDays() }) });
    await toWeek();

    const hours = cal().getOption("businessHours");
    expect(hours).toContainEqual({ daysOfWeek: [3], startTime: "09:00", endTime: "18:00" });
    expect(hours.some((h) => h.daysOfWeek.includes(4)), "the holiday got working hours").toBe(false);
    expect(hours.some((h) => h.daysOfWeek.includes(6)), "Saturday got working hours").toBe(false);
    expect(host().querySelectorAll(".fc-non-business").length).toBeGreaterThan(0);
  });
});

describe("the left panel", () => {
  it("counts come from the FEED, not from the cards on screen", async () => {
    await boot({ items: [task(1), task(2)], feedData: feed({ unplannedCount: 7, planPassedCount: 3, pendingInviteCount: 2 }) });

    const count = (panel) => app().querySelector(`[data-wcn-calpanel-count="${panel}"]`).textContent;
    expect(count("unplanned")).toBe("7");
    expect(count("planPassed")).toBe("3");
    expect(count("invites")).toBe("2");
    expect(app().querySelectorAll(".wcn-calpanel-list [data-wcn-plan-drag]")).toHaveLength(2);
  });

  it("each card is the shared splitCard, draggable, with its estimate and its own Planla button", async () => {
    await boot({ items: [task(1, { estimate: 2 })] });

    const card = app().querySelector(`.wcn-calpanel .wcn-splitcard[data-wcn-plan-drag="${id(1)}"]`);
    expect(card).not.toBeNull();
    expect(card.getAttribute("draggable")).toBe("true");
    expect(card.querySelector(".wcn-calchip-estimate").textContent).toContain("CalChipEstimate:2");
    expect(card.querySelector('[data-wcn-action="plan"]')).not.toBeNull();
  });

  it("the plan-passed tab lists plans whose day is behind the tenant's today", async () => {
    await boot({ items: [task(1, { planned: "2026-10-01" }), task(2, { planned: "2026-10-20" })] });

    app().querySelector('[data-wcn-calpanel="planPassed"]').click();
    await settle();

    const cards = [...app().querySelectorAll(".wcn-calpanel-list .wcn-splitcard")].map((c) => c.getAttribute("data-wcn-row"));
    expect(cards).toEqual([id(1)]);
    expect(app().querySelector(".wcn-calchip-passed")).not.toBeNull();
  });

  it("a panel card starts an HTML5 drag with its id", async () => {
    await boot({ items: [task(1)] });
    const card = app().querySelector(`[data-wcn-plan-drag="${id(1)}"]`);
    const put = {};
    const event = new Event("dragstart", { bubbles: true });
    Object.defineProperty(event, "dataTransfer", { value: { setData: (k, v) => { put[k] = v; }, effectAllowed: "" } });

    card.dispatchEvent(event);

    expect(put[global.DitenCalendar.DRAG_TYPE]).toBe(id(1));
  });

  it("the feed is asked once per range, not once per render", async () => {
    await boot({ items: [task(1)] });
    const before = fetchedRanges.length;

    app().querySelector('[data-wcn-calpanel="invites"]').click();
    await settle();

    expect(fetchedRanges.length).toBe(before);
    expect(new Set(fetchedRanges).size).toBe(fetchedRanges.length);
    expect(fetchedRanges.length, "the feed is re-asked on every render").toBeLessThanOrEqual(2);
  });

  it("a refused feed is said, not drawn as an empty calendar", async () => {
    await boot({ items: [task(1)], feedData: () => ({ ok: false, status: 400, reasonCode: "WORK_CALENDAR_RANGE_INVALID", data: null }) });

    expect(app().querySelector(".wcn-calview-note")).not.toBeNull();
  });
});

describe("read-only tabs", () => {
  const inboxItem = () => Object.assign(task(9), { assignmentMode: "direct", admissionState: "pendingAcceptance", actions: [action("accept")], primaryActionCode: "accept" });

  it("the same component, but nothing can be moved or dropped, and nothing asks the feed", async () => {
    await boot({ items: [inboxItem()], tab: "inbox" });

    expect(cal().getOption("editable")).toBe(false);
    expect(app().querySelector("[data-wcn-calpanel-dropzone]")).toBeNull();
    expect(fetchedRanges).toEqual([]);
    await dropOn(host().querySelector('.fc-daygrid-day[data-date="2026-10-09"]'), id(9));
    expect(calls).toEqual([]);
  });

  it("shows the tab's items on their due dates", async () => {
    await boot({ items: [inboxItem()], tab: "inbox" });

    const due = cal().getEventById("due:" + id(9));
    expect(due).not.toBeNull();
    expect(due.startStr).toBe("2026-10-20");
  });
});

describe("language and direction", () => {
  it("FullCalendar's own buttons read the bridge (SharedResource), not an English default", async () => {
    await boot({ items: [task(1)] });

    expect(host().querySelector(".fc-today-button").textContent).toBe("BUGÜN-KÖPRÜ");
    expect(host().querySelector(".fc-dayGridMonth-button").textContent).toBe("AY-KÖPRÜ");
    await toWeek();
    expect(host().querySelector(".fc-timegrid-axis-cushion").textContent).toBe("TÜM-GÜN-KÖPRÜ");
  });

  it("Arabic is right-to-left", async () => {
    await boot({ items: [task(1)], lang: "ar" });

    expect(host().classList.contains("fc-direction-rtl")).toBe(true);
  });

  it("Turkish is left-to-right and dates come from Intl in the page language", async () => {
    await boot({ items: [task(1)] });

    expect(host().classList.contains("fc-direction-ltr")).toBe(true);
    expect(host().querySelector(".fc-toolbar-title").textContent).toMatch(/Ekim 2026/);
  });
});

// ── the source guarantees ─────────────────────────────────────────────────────────────────────────────────

describe("the calendar assets load only where a calendar is drawn", () => {
  const views = () => {
    const out = [];
    const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).forEach((e) => {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) { walk(p); } else if (e.name.endsWith(".cshtml")) { out.push(p); }
    });
    walk(web("Views"));
    return out;
  };

  it("the tenant shell loads no FullCalendar and no CDN copy of it", () => {
    const shell = read("Views", "Shared", "_LayoutTenantShell.cshtml");
    expect(shell.toLowerCase()).not.toContain("fullcalendar");
  });

  it("only the partial loads the vendored bundle, only from assets, never a CDN", () => {
    const partial = read("Views", "Shared", "_CalendarAssets.cshtml");
    expect(partial).toContain("~/assets/vendor/libs/fullcalendar/fullcalendar.js");
    expect(partial).not.toMatch(/https?:\/\//);
    const loaders = views()
      .filter((file) => !file.endsWith("_Layout.cshtml") && !file.endsWith("_CalendarAssets.cshtml"))
      .filter((file) => /vendor\/libs\/fullcalendar|fullcalendar@/i.test(fs.readFileSync(file, "utf8")));
    expect(loaders, "a page loads FullCalendar outside the partial").toEqual([]);
  });

  it("only the Task Center list page includes the partial", () => {
    const including = views().filter((file) => fs.readFileSync(file, "utf8").includes("_CalendarAssets"))
      .map((file) => path.relative(web("Views"), file));
    expect(including).toEqual([path.join("WorkCenterNext", "Index.cshtml")]);
  });

  it("the chrome strings exist in SharedResource in all seven languages and reach the payload", () => {
    const partial = read("Views", "Shared", "_CalendarAssets.cshtml");
    Object.keys(CHROME).forEach((key) => {
      expect(partial, `${key} is not in the page payload`).toContain(`["${key}"] = SharedLocalizer["${key}"]`);
      LANGS.forEach((lang) => {
        const resx = read("Resources", `SharedResource.${lang}.resx`);
        const match = resx.match(new RegExp(`<data name="${key}"[^>]*>\\s*<value>([^<]+)</value>`));
        expect(match && match[1].trim(), `${lang} is missing ${key}`).toBeTruthy();
      });
    });
  });
});

describe("the old month grid is gone", () => {
  it("no renderCalendar, no hand-built grid, no month arrows", () => {
    expect(APP).not.toMatch(/const renderCalendar = /);
    expect(APP).not.toContain("wcn-cal-grid");
    expect(APP).not.toContain("data-wcn-cal-month");
    expect(APP).not.toContain("calendarMonth");
    expect(read("wwwroot", "assets", "css", "backbone-custom.css")).not.toContain(".wcn-cal-");
  });

  it("every tab's calendar is the shared component", () => {
    expect(APP).toContain("case 'calendar': main = renderCalendarView(); break;");
    expect(APP).toContain("global.DitenCalendar.create(host, {");
  });

  it("no inline style and no element.style in the calendar code", () => {
    const component = read("wwwroot", "assets", "js", "shared", "diten-calendar.js");
    const block = APP.slice(APP.indexOf("THE CALENDAR VIEW (WP-UI-CALENDAR-VIEW-01"), APP.indexOf("const renderKanban = () =>"));
    [component, block].forEach((source) => {
      expect(source).not.toMatch(/style="/);
      expect(source).not.toMatch(/\.style\./);
    });
  });
});

// ── CT acceptance (Control Tower, 2026-09-29): four behaviours the first sabotage round found unguarded ──────────

const feedTask = (n, extra = {}) => Object.assign({
  taskId: id(n), title: `İş ${n}`, priority: "Medium", lifecycle: "Planned", plannedDate: "2026-10-07",
  plannedStartAt: null, plannedDurationMinutes: null, plannedEndAt: null, remainingMinutes: null, dueAt: null, conflict: false
}, extra);

describe("names from the feed are text, never markup", () => {
  /*
   * A task title and a holiday name are typed by people (a task creator, a tenant admin). eventContent builds its
   * node with innerHTML for the chips and buttons, so the NAMES inside it must go through escapeHtml / textContent.
   * Sabotage that dropped either escape passed the whole suite before this test.
   */
  it("a task title and a holiday name are drawn as text, and no element is created from them", async () => {
    const title = '<img src="x" data-xss="title">';
    const holidayName = '<b data-xss="holiday">Bayram</b>';
    const days = weekDays().map((d) => (d.dayKind === "holiday" ? Object.assign({}, d, { holidayName }) : d));
    await boot({ items: [task(1, { planned: "2026-10-07" })], feedData: feed({ days, tasks: [feedTask(1, { title })] }) });

    expect(host().querySelector("[data-xss]"), "a name from the feed became markup").toBeNull();
    expect(host().querySelector(".dc-event-title").textContent).toBe(title);
    expect(host().querySelector(".dc-holiday-band").textContent).toContain(holidayName);
  });
});

describe("a range with no working window at all", () => {
  /*
   * FullCalendar reads an EMPTY businessHours list as "every hour is business". A week that is all holiday or
   * weekend must be drawn all non-working, so the component hands over one impossible entry instead.
   */
  it("is drawn as non-working, not as working around the clock", async () => {
    const closed = weekDays().map((d) => Object.assign({}, d, {
      dayKind: d.dayKind === "workingDay" ? "holiday" : d.dayKind,
      holidayName: d.holidayName || "Kapalı",
      windows: []
    }));
    await boot({ items: [task(1)], feedData: feed({ days: closed }) });
    await toWeek();

    const hours = cal().getOption("businessHours");
    expect(Array.isArray(hours) && hours.length > 0, "an empty list means 'every hour is business' to FullCalendar").toBe(true);
    expect(hours.every((h) => h.daysOfWeek.length === 0), "a closed week got a working weekday").toBe(true);
  });
});

describe("a button inside an event is that button's action, not a door to the item", () => {
  /*
   * openDetailPage navigates away; jsdom refuses navigation and location.assign cannot be stubbed here, so the test
   * reads its first side effect instead — rememberListUrl writes the return URL before navigating (same method as
   * wcn-kanban-drag.test.js).
   */
  it("the block's own Planla button does not open the detail page; a click on the block does", async () => {
    const block = feedTask(1, { plannedStartAt: "2026-10-07T07:00:00Z", plannedDurationMinutes: 60, plannedEndAt: "2026-10-07T08:00:00Z" });
    await boot({ items: [task(1, { planned: "2026-10-07" })], feedData: feed({ tasks: [block] }) });
    await toWeek();
    const event = cal().getEventById(id(1));
    const el = host().querySelector(".dc-task-block");
    const button = el && el.querySelector(".dc-event-plan");
    expect(button, "the block carries its own Planla button").not.toBeNull();
    const opened = () => !!global.sessionStorage.getItem("wcn:list-return-url");

    global.sessionStorage.clear();
    cal().trigger("eventClick", { el, event, jsEvent: { target: el.querySelector(".dc-event-title") }, view: cal().view });
    await tick();
    expect(opened(), "control: a click on the block itself must open the item, or this test proves nothing").toBe(true);

    global.sessionStorage.clear();
    cal().trigger("eventClick", { el, event, jsEvent: { target: button }, view: cal().view });
    await tick();
    expect(opened(), "a click on the block's own button also opened the item — the reader gets two actions").toBe(false);
  });
});

// ══ v2 — CT acceptance corrections (P-2026-09-29-02 v2, F1–F12) ══════════════════════════════════════════════

/** A promise the test resolves by hand, for answers that must arrive late or in a chosen order. */
const deferred = () => {
  let resolve;
  const promise = new Promise((r) => { resolve = r; });
  return { promise, resolve };
};

const blockFeed = (overrides = {}) => feed(Object.assign({
  tasks: [{
    taskId: id(1), title: "İş 1", priority: "Medium", lifecycle: "Planned", plannedDate: "2026-10-07",
    plannedStartAt: "2026-10-07T07:00:00Z", plannedDurationMinutes: 60, plannedEndAt: "2026-10-07T08:00:00Z",
    remainingMinutes: null, dueAt: null, conflict: false
  }],
  days: weekDays()
}, overrides));

describe("v2 F1 — the planning board writes only once it knows the tenant zone", () => {
  it("before the feed has answered: cards do not drag, a drop writes nothing", async () => {
    await boot({ items: [task(1)], feedData: () => new Promise(() => {}) });
    await toWeek();
    stackSlotRows();

    expect(app().querySelector("[data-wcn-plan-drag]"), "a card is draggable before the zone is known").toBeNull();
    expect(global.__wcnCalendar.isEditable()).toBe(false);
    await dropOn(host().querySelector('.fc-timegrid-col[data-date="2026-10-07"]'), id(1), 405);
    await dropOn(host().querySelector('.fc-timegrid-col[data-date="2026-10-07"]').closest(".fc") || host(), id(1), 405);

    expect(calls, "a drop before the feed was written in UTC").toEqual([]);
  });

  it("after a 500 on the first read: the same", async () => {
    await boot({ items: [task(1)], feedData: () => ({ ok: false, status: 500, reasonCode: null, data: null }) });

    await dropOn(host().querySelector('.fc-daygrid-day[data-date="2026-10-09"]'), id(1));

    expect(calls).toEqual([]);
    expect(app().querySelector("[data-wcn-plan-drag]")).toBeNull();
  });

  it("once the feed answers, the same drop writes (non-vacuity)", async () => {
    await boot({ items: [task(1)] });

    expect(app().querySelector("[data-wcn-plan-drag]")).not.toBeNull();
    await dropOn(host().querySelector('.fc-daygrid-day[data-date="2026-10-09"]'), id(1));

    expect(calls).toHaveLength(1);
  });
});

describe("v2 F3 — FullCalendar's now is the tenant's wall clock", () => {
  it("at 22:30 UTC it is already tomorrow in Istanbul, and the now line is at 01:30", async () => {
    await boot({ items: [task(1)] });
    Date.now.mockReturnValue(Date.parse("2026-10-06T22:30:00Z"));

    expect(cal().getOption("now")()).toBe("2026-10-07T01:30:00Z");
    cal().render();
    expect(host().querySelector(".fc-day-today").getAttribute("data-date")).toBe("2026-10-07");
  });
});

describe("v2 F4 — one 'today' on the calendar page: the tenant's", () => {
  it("plan-passed is measured against today IN THE FEED'S ZONE, not the page's pinned date", async () => {
    // The page's own today is pinned to 7 October (wcn-boot). In Tokyo it is already 9 October 01:00.
    const tokyo = feed({ timeZoneId: "Asia/Tokyo" });
    await boot({ items: [task(1, { planned: "2026-10-08" }), task(2, { planned: "2026-10-09" })], feedData: tokyo });
    Date.now.mockReturnValue(Date.parse("2026-10-08T16:00:00Z"));

    app().querySelector('[data-wcn-calpanel="planPassed"]').click();
    await settle();

    const cards = [...app().querySelectorAll(".wcn-calpanel-list .wcn-splitcard")].map((c) => c.getAttribute("data-wcn-row"));
    expect(cards, "plan-passed read the page's date instead of the tenant's").toEqual([id(1)]);
  });
});

describe("v2 F5 — a title in a toast is text, not markup", () => {
  const evil = '<img src=x onerror="alert(1)">';

  it("the saved-plan toast and a meeting warning escape their titles", async () => {
    const item = task(1);
    item.title = { kind: "display", text: evil, locale: "und" };
    const dispatch = () => ({ ok: true, status: 200, data: { warnings: [{ code: "TASK_PLAN_OVERLAPS_MEETING", title: evil }] } });
    await boot({ items: [item], dispatch });

    await dropOn(host().querySelector('.fc-daygrid-day[data-date="2026-10-09"]'), id(1));

    const text = toasts.map((t) => t.message).join(" ");
    expect(text).toContain("&lt;img");
    expect(text).not.toContain("<img");
  });

  it("the conflict toast escapes the other block's title", async () => {
    const dispatch = () => ({ ok: false, status: 409, reasonCode: "TASK_PLAN_CONFLICT", data: { conflict: { title: evil, startAt: "2026-10-08T07:00:00Z", endAt: "2026-10-08T08:00:00Z" } } });
    await boot({ items: [task(1)], dispatch });

    await dropOn(host().querySelector('.fc-daygrid-day[data-date="2026-10-09"]'), id(1));

    const said = toasts.find((t) => t.type === "error").message;
    expect(said).toContain("&lt;img");
    expect(said).not.toContain("<img");
  });
});

describe("v2 F7 — one plan write per item at a time", () => {
  it("two quick drops of the same card post once", async () => {
    const pending = deferred();
    await boot({ items: [task(1)], dispatch: () => pending.promise });
    const cell = host().querySelector('.fc-daygrid-day[data-date="2026-10-09"]');

    await dropOn(cell, id(1));
    await dropOn(host().querySelector('.fc-daygrid-day[data-date="2026-10-10"]'), id(1));

    expect(calls, "the second drag posted while the first was in flight").toHaveLength(1);
    pending.resolve({ ok: true, status: 200, data: { warnings: [] } });
    await settle();
  });

  it("a revert offered by a calendar that is gone does not throw", async () => {
    const pending = deferred();
    await boot({ items: [task(1, { planned: "2026-10-07" })], feedData: blockFeed(), dispatch: () => pending.promise });
    await toWeek();
    const event = cal().getEventById(id(1));
    event.setDates("2026-10-08T10:00:00Z", "2026-10-08T11:00:00Z");
    cal().trigger("eventDrop", { event, oldEvent: event, revert: () => {}, view: cal().view });

    expect(() => cal().trigger("eventDrop", { event, oldEvent: event, revert: () => { throw new Error("destroyed"); }, view: cal().view }))
      .not.toThrow();
    expect(calls).toHaveLength(1);
    pending.resolve({ ok: true, status: 200, data: { warnings: [] } });
    await settle();
  });
});

describe("v2 F8 — a late answer for an old range does not overwrite the new one", () => {
  it("month answers AFTER week: the week's feed stays", async () => {
    const answers = {};
    const feedFor = (title) => feed({ tasks: [{
      taskId: id(title === "ESKİ" ? 1 : 2), title, priority: "Medium", lifecycle: "Planned", plannedDate: "2026-10-07",
      plannedStartAt: null, plannedDurationMinutes: null, plannedEndAt: null, remainingMinutes: null, dueAt: null, conflict: false
    }] });
    await boot({
      items: [task(1, { planned: "2026-10-07" }), task(2, { planned: "2026-10-07" })],
      feedData: (from, to) => { answers[`${from}|${to}`] = answers[`${from}|${to}`] || deferred(); return answers[`${from}|${to}`].promise; }
    });
    const monthKey = Object.keys(answers)[0];

    host().querySelector(".fc-timeGridWeek-button").click();
    await settle();
    const weekKey = Object.keys(answers).find((k) => k !== monthKey);
    answers[weekKey].resolve({ ok: true, status: 200, data: feedFor("YENİ") });
    await settle();
    answers[monthKey].resolve({ ok: true, status: 200, data: feedFor("ESKİ") });
    await settle();

    const titles = cal().getEvents().map((e) => e.title);
    expect(titles).toContain("YENİ");
    expect(titles, "the stale month answer painted over the week").not.toContain("ESKİ");
  });
});

describe("v2 F9 — the day goes into the URL only when the reader moves", () => {
  it("opening the calendar writes no caldate; pressing next does", async () => {
    await boot({ items: [task(1)] });

    expect(new URL(global.location.href).searchParams.get("caldate")).toBeNull();
    host().querySelector(".fc-next-button").click();
    await settle();
    expect(new URL(global.location.href).searchParams.get("caldate")).toMatch(/^2026-11-/);
  });

  it("CT live: FullCalendar re-announcing the SAME range is not the reader moving", async () => {
    await boot({ items: [task(1)] });
    const view = cal().view;

    cal().trigger("datesSet", { start: view.activeStart, end: view.activeEnd, startStr: "", endStr: "", timeZone: "UTC", view });
    await settle();

    expect(new URL(global.location.href).searchParams.get("caldate"), "a repeat of the opening range wrote caldate").toBeNull();
  });
});

describe("CT live: a read-only tab has no working-day facts, so it shades nothing", () => {
  it("Gelen Kutusu's calendar draws no non-working shading and hands FullCalendar no business hours", async () => {
    const inboxItem = Object.assign(task(9), { assignmentMode: "direct", admissionState: "pendingAcceptance", actions: [action("accept")], primaryActionCode: "accept" });
    await boot({ items: [inboxItem], tab: "inbox" });

    expect(cal().getOption("businessHours"), "an impossible entry here greys the whole month").toBe(false);
    expect(host().querySelectorAll(".fc-non-business")).toHaveLength(0);
  });
});

describe("v2 F10 — one zone, and a conversion that fails moves nothing", () => {
  it("a zone the browser does not know: nothing is written, and the page converts in the component's zone", async () => {
    await boot({ items: [task(1)], feedData: feed({ timeZoneId: "Mars/Olympus" }) });

    expect(global.__wcnCalendar.zone).toBe("UTC");
    await dropOn(host().querySelector('.fc-daygrid-day[data-date="2026-10-09"]'), id(1));
    expect(calls).toEqual([]);
  });

  it("a conversion error reverts FIRST, then says so, and posts nothing", async () => {
    await boot({ items: [task(1, { planned: "2026-10-07" })], feedData: blockFeed() });
    await toWeek();
    const spy = vi.spyOn(global.DitenZonedTime, "toOffsetIso").mockImplementation(() => { throw new RangeError("bad"); });
    const order = [];
    const event = cal().getEventById(id(1));
    event.setAllDay(true);
    event.setStart("2026-10-09");
    const originalShow = global.showToast;
    global.showToast = (m, type) => { order.push("toast"); originalShow(m, type); };

    cal().trigger("eventDrop", { event, oldEvent: event, revert: () => order.push("revert"), view: cal().view });
    await settle();

    expect(order).toEqual(["revert", "toast"]);
    expect(calls).toEqual([]);
    spy.mockRestore();
  });
});

describe("v2 F11 — moving a block keeps its stored length across a DST jump", () => {
  it("Europe/Berlin 2026-03-29: a 120-minute block drawn 01:00–04:00 is moved as 120 minutes", async () => {
    const berlin = feed({
      timeZoneId: "Europe/Berlin",
      tasks: [{
        taskId: id(1), title: "Gece işi", priority: "Medium", lifecycle: "Planned", plannedDate: "2026-03-29",
        plannedStartAt: "2026-03-29T00:00:00Z", plannedDurationMinutes: 120, plannedEndAt: "2026-03-29T02:00:00Z",
        remainingMinutes: null, dueAt: null, conflict: false
      }]
    });
    await boot({ items: [task(1, { planned: "2026-03-29" })], feedData: berlin });
    Date.now.mockReturnValue(Date.parse("2026-03-29T09:00:00Z"));
    cal().changeView("timeGridWeek", "2026-03-29");
    await settle();
    const event = cal().getEventById(id(1));
    expect((event.end - event.start) / 60000, "the block is not drawn across the jump").toBe(180);

    event.setDates("2026-03-30T10:00:00Z", "2026-03-30T13:00:00Z");
    cal().trigger("eventDrop", { event, oldEvent: event, revert: () => {}, view: cal().view });
    await settle();

    expect(calls[0].payload).toEqual({ expectedVersion: 11, plannedStartAt: "2026-03-30T08:00:00.000Z", plannedDurationMinutes: 120 });
  });
});

describe("v2 F12 — a plan does not rebuild the calendar", () => {
  it("the same FullCalendar survives the write: view, instance and the scrolled position stay", async () => {
    await boot({ items: [task(1)], feedData: blockFeed({ tasks: [] }) });
    await toWeek();
    const before = cal();
    const scroller = [...host().querySelectorAll(".fc-scroller")].pop();
    scroller.scrollTop = 600; // 15:00 on a 10px-per-quarter stack

    stackSlotRows();
    await dropOn(host().querySelector('.fc-timegrid-col[data-date="2026-10-07"]'), id(1), 605);

    expect(calls).toHaveLength(1);
    expect(cal(), "the calendar was rebuilt by the plan").toBe(before);
    expect(cal().view.type).toBe("timeGridWeek");
    expect([...host().querySelectorAll(".fc-scroller")].pop().scrollTop, "the week jumped back to its default hour").toBe(600);
  });
});

describe("v2 F2 / F6 — re-planning from the list page", () => {
  /** The list view (not the calendar): the feed is only asked for the zone. */
  const bootList = async ({ items, zoneAnswer, dispatch }) => {
    vi.spyOn(Date, "now").mockReturnValue(Date.parse(`${TODAY}T09:00:00Z`));
    calls = [];
    toasts = [];
    global.showToast = (message, type) => toasts.push({ message, type: type || "success" });
    await bootSurface({ rootAttrs: 'data-wcn-page="list"', items, now: () => new Date(TODAY + "T12:00:00") });
    global.WorkCenterNextApi.fetchCalendar = () => Promise.resolve(zoneAnswer);
    global.WorkCenterNextApi.dispatchAction = (itemId, actionCode, providerCode, payload) => {
      calls.push({ itemId, actionCode, providerCode, payload });
      return Promise.resolve(dispatch ? dispatch() : { ok: true, status: 200, data: { warnings: [] } });
    };
    app().querySelector('[data-wcn-tab="islerim"]').click();
    await settle();
  };

  const stubSwal = (fill) => {
    global.Swal = {
      showValidationMessage: () => {},
      fire: (config) => {
        document.querySelectorAll(".swal2-popup").forEach((el) => el.remove());
        const popup = document.createElement("div");
        popup.className = "swal2-popup";
        popup.innerHTML = config.html;
        document.body.appendChild(popup);
        config.didOpen?.(popup);
        fill(popup);
        const value = config.preConfirm();
        return Promise.resolve(value ? { isConfirmed: true, value } : { isConfirmed: false });
      }
    };
  };

  afterEach(() => { delete global.Swal; document.querySelectorAll(".swal2-popup").forEach((el) => el.remove()); });

  it("F2: a BLOCK re-planned to another day stays a block — the dialog opens at its time and length", async () => {
    const blocked = task(1, { planned: "2026-10-07" });
    blocked.plannedStartAt = "2026-10-07T10:00:00Z"; // 13:00 in Istanbul
    blocked.plannedDurationMinutes = 90;
    let seededTime;
    let seededMinutes;
    stubSwal(() => {
      seededTime = document.getElementById("wcnPlanTime").value;
      seededMinutes = document.getElementById("wcnPlanDuration").value;
      document.getElementById("wcnPlanDay").value = "2026-10-09"; // ONLY the day changes
    });
    await bootList({ items: [blocked], zoneAnswer: { ok: true, status: 200, data: feed() } });
    // A planned task sits in İşlerim's "Planlı" segment, not the default "Aktif" one.
    app().querySelector('[data-wcn-seg="planli"]').click();
    await settle();

    app().querySelector(`[data-wcn-action="plan"][data-wcn-id="${id(1)}"]`).click();
    await settle();

    expect(seededTime, "the block opened as 'no time'").toBe("13:00");
    expect(seededMinutes).toBe("90");
    expect(calls[0].payload).toEqual({ expectedVersion: 11, plannedStartAt: "2026-10-09T10:00:00.000Z", plannedDurationMinutes: 90 });
  });

  it("F6: with the feed refused, a DAY plan still goes — bare, as before — and no time is offered", async () => {
    let timeDisabled;
    stubSwal(() => {
      timeDisabled = document.getElementById("wcnPlanTime").disabled;
      document.getElementById("wcnPlanDay").value = "2026-10-09";
    });
    await bootList({ items: [task(1)], zoneAnswer: { ok: false, status: 403, reasonCode: null, data: null } });

    app().querySelector(`[data-wcn-action="plan"][data-wcn-id="${id(1)}"]`).click();
    await settle();

    expect(timeDisabled).toBe(true);
    expect(calls).toHaveLength(1);
    expect(calls[0].payload).toEqual({ expectedVersion: 11, plannedDate: "2026-10-09" });
  });

  it("F6: …but a TIME without the zone is refused, not guessed", async () => {
    stubSwal(() => {
      const time = document.getElementById("wcnPlanTime");
      time.disabled = false;
      time.value = "10:00";
      document.getElementById("wcnPlanDay").value = "2026-10-09";
    });
    await bootList({ items: [task(1)], zoneAnswer: { ok: false, status: 503, reasonCode: null, data: null } });

    app().querySelector(`[data-wcn-action="plan"][data-wcn-id="${id(1)}"]`).click();
    await settle();

    expect(calls).toEqual([]);
    expect(toasts.find((t) => t.type === "error").message).toBe("CalZoneUnavailable");
  });

  it("CT: with the zone unreadable, a task that HAS a block is not turned into a day plan by changing only the day", async () => {
    const blocked = task(1, { planned: "2026-10-07" });
    blocked.plannedStartAt = "2026-10-07T10:00:00Z";
    blocked.plannedDurationMinutes = 90;
    stubSwal(() => { document.getElementById("wcnPlanDay").value = "2026-10-09"; });
    await bootList({ items: [blocked], zoneAnswer: { ok: false, status: 503, reasonCode: null, data: null } });
    app().querySelector('[data-wcn-seg="planli"]').click();
    await settle();

    app().querySelector(`[data-wcn-action="plan"][data-wcn-id="${id(1)}"]`).click();
    await settle();

    expect(calls, "the day plan went and the engine would have cleared the block").toEqual([]);
    expect(toasts.find((t) => t.type === "error").message).toBe("CalZoneUnavailable");
  });
});

describe("v2 — the test seam is cleared with the calendar", () => {
  it("leaving the calendar view clears global.__wcnCalendar", async () => {
    await boot({ items: [task(1)] });
    expect(global.__wcnCalendar).toBeTruthy();

    app().querySelector('[data-wcn-view="list"]').click();
    await settle();

    expect(global.__wcnCalendar).toBeNull();
  });
});

describe("CT live: the week fits a screen", () => {
  /*
   * Measured live 2026-09-29: height 'auto' + the vendored 52 px slot laid the whole 24 h out on the page (a 5 000 px
   * week). The week and day views have a fixed height and scroll inside; the month view still grows with its rows;
   * the quarter-hour slot size is set back under the component's own class.
   */
  it("week and day views have a fixed height, the month view grows, and the slot size is the component's own", async () => {
    await boot({ items: [task(1)] });
    expect(host().classList.contains("dc-calendar")).toBe(true);
    expect(cal().getCurrentData().options.height).toBe("auto");

    await toWeek();
    expect(cal().getCurrentData().options.height).toBe(global.DitenCalendar.TIMEGRID_HEIGHT);

    host().querySelector(".fc-timeGridDay-button").click();
    await settle();
    expect(cal().getCurrentData().options.height).toBe(global.DitenCalendar.TIMEGRID_HEIGHT);

    const css = read("wwwroot", "assets", "css", "backbone-custom.css");
    // Compound on the .fc root: a descendant selector never matched (measured live — the slot stayed 52 px).
    expect(css).toMatch(/\.dc-calendar\.fc \.fc-timegrid-slot \{ block-size: [0-9.]+rem; \}/);
    expect(host().classList.contains("fc"), "the component class and FullCalendar's root are the same element").toBe(true);
  });
});
