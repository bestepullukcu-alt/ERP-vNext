const fs = require("fs");
const path = require("path");
const vm = require("vm");
const { loadScript } = require("./load-script");

/*
 * WP-UI-MEETINGS-CALENDAR-01 (MOD-0357 S3b 2c) — the Meetings page's calendar view, measured on the REAL pieces:
 * the vendored FullCalendar 6.1.15, shared/diten-zoned-time.js, shared/diten-calendar.js, shared/diten-invite-card.js,
 * Meetings/api.js, Meetings/index.js (the page's own filter rule) and Meetings/calendar.js. The ONLY stub is the
 * network (global.fetch, answering like the three same-origin addresses do) and the two page-level dialogs.
 *
 * Tenant: Europe/Istanbul (UTC+3). "Now" is Wednesday 2026-10-07 09:00Z; the month grid is 28 Sep – 8 Nov.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const ZONE = "Europe/Istanbul";

const mid = (n) => `aaaaaaaa-0000-0000-0000-${String(n).padStart(12, "0")}`;
const TYPE_A = "bbbbbbbb-0000-0000-0000-000000000001";
const TYPE_B = "bbbbbbbb-0000-0000-0000-000000000002";
const ORGANIZER = "cccccccc-0000-0000-0000-000000000001";
const INVITE_FIXTURE = JSON.parse(fs.readFileSync(path.join(__dirname, "fixtures", "meeting-invite-provider-projection.json"), "utf8"));

const row = (n, start, extra = {}) => Object.assign({
  id: mid(n), title: `Toplantı ${n}`, meetingTypeId: TYPE_A, meetingTypeName: "Tip A",
  startAt: start, endAt: new Date(Date.parse(start) + 3600000).toISOString(),
  organizerUserId: ORGANIZER, lifecycle: 0, iAmAttendee: true, hasLinkedTasks: false
}, extra);

/*
 * 1 accepted · 2 pending · 3 declined (on it, not in my feed) · 4 cancelled · 5 somebody else's (read-all view)
 * · 6 far outside the grid.
 */
const ROWS = () => [
  row(1, "2026-10-07T07:00:00Z"),
  row(2, "2026-10-08T07:00:00Z"),
  row(3, "2026-10-09T07:00:00Z"),
  row(4, "2026-10-06T07:00:00Z", { lifecycle: 1 }),
  row(5, "2026-10-10T07:00:00Z", { iAmAttendee: false, meetingTypeId: TYPE_B }),
  row(6, "2026-12-01T07:00:00Z")
];

const feedMeeting = (n, response, extra = {}) => {
  const r = ROWS().find((x) => x.id === mid(n));
  return Object.assign({ meetingId: r.id, title: r.title, startAt: r.startAt, endAt: r.endAt, response, overlapsPlan: false, planOverlap: null }, extra);
};

const FEED = (extra = {}) => Object.assign({
  timeZoneId: ZONE, from: "2026-09-28", to: "2026-11-08", tasks: [],
  meetings: [feedMeeting(1, "accepted"), feedMeeting(2, "pending")],
  days: [], unplannedCount: 0, planPassedCount: 0, pendingInviteCount: 1
}, extra);

const CHROME = {
  CalendarToday: "BUGÜN", CalendarMonth: "AY", CalendarWeek: "HAFTA", CalendarDay: "GÜN", CalendarPrevious: "ÖNCEKİ",
  CalendarNext: "SONRAKİ", CalendarAllDay: "TÜM-GÜN", CalendarMore: "+{0}", CalendarNoEvents: "YOK",
  CalendarUnresolved: "TAKVİM-TANIMSIZ", InviteCardType: "DAVET", InviteAccept: "KABUL", InviteDecline: "RET",
  InviteOverlapTitle: "ÇAKIŞIYOR", InviteOverlapText: "ÇAKIŞMA {0} ({1})", InviteAcceptAnyway: "YİNE-DE"
};

/** The Index markup the calendar script works on (Views/Meetings/Index.cshtml, trimmed to what it touches). */
const PAGE = `
  <div id="inlineFilterHost"><div class="collapse" id="inlineFilterCollapse"></div></div>
  <div class="btn-group mc-view-switch">
    <button type="button" class="btn active" data-mc-view="list" aria-pressed="true">L</button>
    <button type="button" class="btn" data-mc-view="calendar" aria-pressed="false">C</button>
  </div>
  <div data-mc-list-section><table class="datatables-meetings"></table></div>
  <div class="d-none" data-mc-calendar-section>
    <aside class="card mc-invites"><span data-mc-invite-count>0</span><div data-mc-invite-list></div></aside>
    <section><button type="button" data-mc-calendar-filter></button><div data-mc-calendar-filter-slot></div>
      <div data-mc-calendar-notes></div><div data-mc-calendar-host></div></section>
  </div>`;

let server;       // what the fake network answers
let listCalls;    // every list query the page sent, decoded
let responds;     // every respond POST
let navigated;
let modals;

const tick = () => new Promise((resolve) => { setTimeout(resolve, 0); });
const settle = async () => { for (let i = 0; i < 10; i += 1) { await tick(); } };

const json = (status, body) => Promise.resolve({ ok: status >= 200 && status < 300, status, json: () => Promise.resolve(body) });

/** The list API as Platform answers it: StartAt in [fromUtc, toUtc), the two flags, the single-value ids, paging. */
const answerList = (query) => {
  const q = new URLSearchParams(query);
  listCalls.push(Object.fromEntries(q.entries()));
  let rows = server.rows.filter((r) => {
    if (q.get("fromUtc") && Date.parse(r.startAt) < Date.parse(q.get("fromUtc"))) { return false; }
    if (q.get("toUtc") && Date.parse(r.startAt) >= Date.parse(q.get("toUtc"))) { return false; }
    if (q.get("meetingTypeId") && r.meetingTypeId !== q.get("meetingTypeId")) { return false; }
    if (q.get("organizerUserId") && r.organizerUserId !== q.get("organizerUserId")) { return false; }
    if (q.get("iAmAttendeeOnly") === "true" && !r.iAmAttendee) { return false; }
    if (q.get("hasLinkedTasksOnly") === "true" && !r.hasLinkedTasks) { return false; }
    return true;
  });
  const size = Math.min(Number(q.get("pageSize") || 25), server.maxPageSize);
  const page = Number(q.get("page") || 1);
  const total = server.fakeTotal ?? rows.length;
  rows = rows.slice((page - 1) * size, page * size);
  if (server.fakeTotal) { rows = [server.rows[0]]; }
  return json(200, { data: { items: rows, totalCount: total } });
};

const fakeFetch = (url, init = {}) => {
  const u = new URL(url, "http://localhost");
  if (u.pathname === "/Meetings/api/list") { return answerList(u.searchParams.get("query") || ""); }
  if (u.pathname === "/WorkCenterNext/api/calendar") {
    const feed = typeof server.feed === "function" ? server.feed(u.searchParams.get("from"), u.searchParams.get("to")) : server.feed;
    return feed ? json(200, { data: feed }) : json(500, {});
  }
  if (u.pathname === "/WorkCenterNext/api/work-items") { return json(200, { data: { items: server.workItems } }); }
  const respond = /^\/Meetings\/api\/([^/]+)\/respond$/.exec(u.pathname);
  if (respond && init.method === "POST") {
    responds.push({ id: respond[1], body: JSON.parse(init.body) });
    return json(200, { data: {} });
  }
  return json(404, {});
};

/** index.js declares a top-level const, so each test loads it inside its own function scope. */
const loadIndex = () => {
  const file = web("wwwroot", "assets", "js", "Meetings", "index.js");
  vm.runInThisContext(`(function () {\n${fs.readFileSync(file, "utf8")}\n})();`, { filename: file });
};

beforeAll(() => {
  loadScript("wwwroot/assets/vendor/libs/fullcalendar/fullcalendar.js");
  loadScript("wwwroot/assets/js/shared/diten-zoned-time.js");
  loadScript("wwwroot/assets/js/shared/diten-calendar.js");
  loadScript("wwwroot/assets/js/shared/diten-invite-card.js");
});

afterEach(() => {
  global.MeetingsCalendar?.destroy?.();
  document.documentElement.lang = "tr";
  document.getElementById("diten-calendar-l10n")?.remove();
  window.history.replaceState(null, "", "/Meetings");
  delete global.showConfirm;
});

const boot = async ({ url = "/Meetings?view=calendar", rows = ROWS(), feed = FEED(), workItems = [], maxPageSize = 1000, fakeTotal = null, lang = "tr" } = {}) => {
  document.documentElement.lang = lang;
  const payload = document.createElement("script");
  payload.id = "diten-calendar-l10n";
  payload.type = "application/json";
  payload.textContent = JSON.stringify(CHROME);
  document.head.appendChild(payload);
  document.body.innerHTML = PAGE;
  window.history.replaceState(null, "", url);

  vi.spyOn(Date, "now").mockReturnValue(Date.parse("2026-10-07T09:00:00Z"));
  server = { rows, feed, workItems, maxPageSize, fakeTotal };
  listCalls = [];
  responds = [];
  navigated = [];
  modals = [];
  global.fetch = vi.fn(fakeFetch);
  global.MeetingsL10n = { t: (key) => key };
  global.DitenModal = { success: (o) => modals.push(["success", o.title]), error: (o) => modals.push(["error", o.message]) };

  delete global.MeetingsApi;
  loadScript("wwwroot/assets/js/Meetings/api.js");
  loadIndex();
  global.__mcNoAutoInit = true;
  loadScript("wwwroot/assets/js/Meetings/calendar.js");
  global.MeetingsCalendar.navigate = (target) => navigated.push(target);
  await global.MeetingsCalendar.init();
  await settle();
};

const cal = () => global.__mcCalendar.calendar;
const section = () => document.querySelector("[data-mc-calendar-section]");
const drawnIds = () => cal().getEvents().filter((e) => e.display !== "background").map((e) => e.id).sort();

// ── A. the switch ────────────────────────────────────────────────────────────────────────────────────────

describe("the list ⇄ calendar switch lives in the URL, the list is the default", () => {
  it("no ?view: the list is open and no calendar is built", async () => {
    await boot({ url: "/Meetings" });

    expect(section().classList.contains("d-none")).toBe(true);
    expect(document.querySelector("[data-mc-list-section]").classList.contains("d-none")).toBe(false);
    expect(global.__mcCalendar).toBeFalsy();
  });

  it("clicking Calendar opens it and writes ?view=calendar; clicking List takes it out again", async () => {
    await boot({ url: "/Meetings" });

    document.querySelector('[data-mc-view="calendar"]').click();
    await settle();
    expect(window.location.search).toBe("?view=calendar");
    expect(section().classList.contains("d-none")).toBe(false);
    expect(document.querySelector('[data-mc-view="calendar"]').getAttribute("aria-pressed")).toBe("true");
    expect(global.__mcCalendar).toBeTruthy();

    document.querySelector('[data-mc-view="list"]').click();
    await settle();
    expect(window.location.search).toBe("");
    expect(section().classList.contains("d-none")).toBe(true);
  });

  it("the ONE filter bar sits in the open view — a table finishing behind the calendar does not pull it back", async () => {
    await boot();
    const slot = document.querySelector("[data-mc-calendar-filter-slot]");
    expect(document.getElementById("inlineFilterHost").parentNode).toBe(slot);

    global.MeetingsList.mountInlineFilter();

    expect(document.getElementById("inlineFilterHost").parentNode).toBe(slot);
  });

  it("?view=calendar opens straight on the calendar, in the tenant zone the feed names", async () => {
    await boot();

    expect(section().classList.contains("d-none")).toBe(false);
    expect(global.__mcCalendar.zone).toBe(ZONE);
    expect(cal().getOption("editable")).toBe(false);
  });
});

// ── B. the same set as the list ─────────────────────────────────────────────────────────────────────────

describe("the calendar draws the LIST's set, narrowed to the visible range", () => {
  it("asks the list API for the visible range (FromUtc a day early, ToUtc = the end of the last day) through the proxy's query", async () => {
    await boot();

    expect(listCalls.length).toBeGreaterThan(0);
    const last = listCalls[listCalls.length - 1];
    // Month grid 28 Sep – 8 Nov, Istanbul: 27 Sep 00:00 local = 26 Sep 21:00Z (a meeting that started the evening before
    // still reaches into the grid) … 9 Nov 00:00 local = 8 Nov 21:00Z (the end of the last day shown).
    expect(last.fromUtc).toBe("2026-09-26T21:00:00.000Z");
    expect(last.toUtc).toBe("2026-11-08T21:00:00.000Z");
    expect(Number(last.pageSize)).toBeGreaterThan(25);
    // ATT-FIX1/2 — the calendar draws no organizer names, so it asks for none.
    expect(last.includeNames).toBe("false");
  });

  it("accepted and pending are drawn; declined, cancelled and out-of-range are not", async () => {
    await boot();

    expect(drawnIds()).toEqual([mid(1), mid(2), mid(5)].sort());
  });

  it("accepted is FILLED, pending is DASHED, a meeting I am not on is the quiet 'other'", async () => {
    await boot();

    expect(cal().getEventById(mid(1)).classNames).toContain("dc-meeting-accepted");
    expect(cal().getEventById(mid(2)).classNames).toContain("dc-meeting-pending");
    expect(cal().getEventById(mid(5)).classNames).toContain("dc-meeting-other");
  });

  it("times are the tenant's wall clock (07:00Z = 10:00 in Istanbul)", async () => {
    await boot();

    expect(cal().getEventById(mid(1)).start.toISOString()).toBe("2026-10-07T10:00:00.000Z");
  });

  it("a filter change redraws the calendar with the SAME rule the table uses", async () => {
    await boot();
    expect(drawnIds()).toContain(mid(1));

    global.MeetingsList.applyFilters({ meetingType: [TYPE_B] });
    await settle();

    expect(drawnIds()).toEqual([mid(5)]);
    // …and the table's own predicate agrees row by row (one rule, not two).
    const filters = global.MeetingsList.getAppliedFilters();
    expect(ROWS().filter((r) => global.MeetingsList.matchesFilters(r, filters)).map((r) => r.id)).toContain(mid(5));
  });

  it("a filter the API cannot narrow (the page's own 'from' date) is still applied — by the table's own rule", async () => {
    await boot();

    global.MeetingsList.applyFilters({ from: "2026-10-08" });
    await settle();

    expect(listCalls[listCalls.length - 1].from, "the page filter leaked into the API query").toBeUndefined();
    expect(drawnIds()).toEqual([mid(2), mid(5)].sort());
  });

  it("'only where I am an attendee' drops the meeting I am not on", async () => {
    await boot();

    global.MeetingsList.applyFilters({ iAmAttendee: true });
    await settle();

    expect(drawnIds()).not.toContain(mid(5));
    expect(listCalls[listCalls.length - 1].iAmAttendeeOnly).toBe("true");
  });

  it("every page of the answer is read — a range bigger than one page is not cut", async () => {
    await boot({ maxPageSize: 1 });

    expect(drawnIds()).toEqual([mid(1), mid(2), mid(5)].sort());
    expect(new Set(listCalls.map((c) => c.page)).size).toBeGreaterThan(1);
  });

  it("a range the API cannot give in full is SAID and nothing is drawn", async () => {
    await boot({ fakeTotal: 1000000 });

    expect(document.querySelector("[data-mc-calendar-notes] .mc-calendar-error")).not.toBeNull();
    expect(drawnIds()).toEqual([]);
  });
});

// ── C. clicks and moves ─────────────────────────────────────────────────────────────────────────────────

describe("the calendar is read-only and opens the meeting", () => {
  it("a click on a meeting opens its details", async () => {
    await boot();
    const event = cal().getEventById(mid(1));

    cal().trigger("eventClick", { event, el: document.createElement("a"), jsEvent: { target: document.body }, view: cal().view });

    expect(navigated).toEqual([`/Meetings/${mid(1)}`]);
  });

  it("nothing on it can be moved", async () => {
    await boot();

    expect(cal().getOption("editable")).toBe(false);
    expect(cal().getEventById(mid(1)).startEditable).toBe(false);
  });
});

// ── D. day facts ────────────────────────────────────────────────────────────────────────────────────────

describe("day shading and the unresolved calendar", () => {
  it("a day the feed could not resolve puts the shared sentence over the calendar", async () => {
    const days = [{ date: "2026-10-07", dayKind: "workingDay", holidayName: null, windows: [], resolvedFrom: "tenantDefault", calendarUnresolved: true }];
    await boot({ feed: FEED({ days }) });

    const note = document.querySelector("[data-mc-calendar-notes] .dc-unresolved-note");
    expect(note).not.toBeNull();
    expect(note.textContent).toContain("TAKVİM-TANIMSIZ");
  });

  it("every day resolved: no sentence", async () => {
    await boot();

    expect(document.querySelector(".dc-unresolved-note")).toBeNull();
  });

  it("a holiday from the feed's days is drawn on its day", async () => {
    const days = [{ date: "2026-10-29", dayKind: "holiday", holidayName: "Bayram", windows: [], resolvedFrom: "tenantDefault", calendarUnresolved: false }];
    await boot({ feed: FEED({ days }) });

    expect(document.querySelector('.fc-daygrid-day[data-date="2026-10-29"]').classList.contains("dc-day-holiday")).toBe(true);
  });
});

// ── E. invitations ──────────────────────────────────────────────────────────────────────────────────────

const INVITE = () => Object.assign(JSON.parse(JSON.stringify(INVITE_FIXTURE.invitee_read_none)), { id: mid(2), dueAt: "2026-10-08T07:00:00+00:00" });

describe("pending invitations: the ONE shared card, with its count", () => {
  it("the panel lists my pending invitations as DitenInviteCard cards, with the count", async () => {
    const render = vi.spyOn(global.DitenInviteCard, "render");
    await boot({ workItems: [INVITE(), { id: "x", workIntent: "task" }] });

    expect(document.querySelector("[data-mc-invite-count]").textContent).toBe("1");
    const card = document.querySelector(`[data-mc-invite-list] [data-dic-invite="${mid(2)}"]`);
    expect(card).not.toBeNull();
    expect(card.classList.contains("dic-card")).toBe(true);
    expect(render).toHaveBeenCalledTimes(1);
    expect(card.getAttribute("draggable"), "an invitation is answered, never dragged").toBe("false");
    expect(card.querySelector(".dic-card-organizer").textContent).toBe("Ayşe Yılmaz");
    expect(card.querySelector('[data-mc-invite-answer="Accept"]').textContent).toBe("KABUL");
  });

  it("a double click on Accept answers once", async () => {
    await boot({ workItems: [INVITE()] });
    const button = document.querySelector(`[data-dic-invite="${mid(2)}"] [data-mc-invite-answer="Accept"]`);

    button.click();
    button.click();
    await settle();

    expect(responds).toHaveLength(1);
  });

  it("Enter on the card opens the meeting", async () => {
    await boot({ workItems: [INVITE()] });
    const card = document.querySelector(`[data-dic-invite="${mid(2)}"]`);

    card.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", bubbles: true }));

    expect(navigated).toEqual([`/Meetings/${mid(2)}`]);
  });

  it("a failed first read is said, and the next switch to the calendar tries again", async () => {
    await boot({ url: "/Meetings", feed: null });
    document.querySelector('[data-mc-view="calendar"]').click();
    await settle();
    expect(global.__mcCalendar).toBeFalsy();
    expect(document.querySelector(".mc-calendar-error")).not.toBeNull();

    server.feed = FEED();
    document.querySelector('[data-mc-view="list"]').click();
    document.querySelector('[data-mc-view="calendar"]').click();
    await settle();

    expect(global.__mcCalendar).toBeTruthy();
  });

  it("an empty panel says so", async () => {
    await boot({ workItems: [] });

    expect(document.querySelector("[data-mc-invite-count]").textContent).toBe("0");
    expect(document.querySelector(".mc-invites-empty").textContent).toBe("calendarInvitesEmpty");
  });

  const withConfirm = (answer) => {
    const asked = [];
    global.showConfirm = (title, onConfirm, options) => {
      asked.push({ title, options });
      if (answer) { onConfirm(); } else { options.onCancel(); }
    };
    return asked;
  };

  const overlapping = () => FEED({
    meetings: [feedMeeting(1, "accepted"), feedMeeting(2, "pending", {
      overlapsPlan: true,
      planOverlap: { taskId: "t1", title: "Rapor <img>", startAt: "2026-10-08T07:00:00Z", endAt: "2026-10-08T07:30:00Z" }
    })]
  });

  const click = async (answer) => {
    document.querySelector(`[data-dic-invite="${mid(2)}"] [data-mc-invite-answer="${answer}"]`).click();
    await global.__mcLastAnswer;
    await settle();
  };

  it("accepting over one of my plan blocks WARNS (block and hours), then accepts when confirmed", async () => {
    const asked = withConfirm(true);
    await boot({ workItems: [INVITE()], feed: overlapping() });

    await click("Accept");

    expect(asked).toHaveLength(1);
    expect(asked[0].title).toBe("ÇAKIŞIYOR");
    // Handed over as TEXT: the shared confirm escapes at its door (WP-SHARED-CONFIRM-XSS-01). The card escaping
    // first would show "&lt;img&gt;" to the reader; that the dialog draws it as text is global-confirm-text-is-text's job.
    expect(asked[0].options.subtext).toContain("Rapor <img>");
    expect(asked[0].options.subtextHtml).toBeUndefined();
    expect(asked[0].options.subtext).toMatch(/10.00–10.30/);
    expect(responds).toEqual([{ id: mid(2), body: { response: "Accept" } }]);
    expect(modals).toContainEqual(["success", "inviteAccepted"]);
  });

  it("cancelling the warning accepts nothing", async () => {
    withConfirm(false);
    await boot({ workItems: [INVITE()], feed: overlapping() });

    await click("Accept");

    expect(responds).toEqual([]);
  });

  it("no overlap: accept asks nothing", async () => {
    const asked = withConfirm(true);
    await boot({ workItems: [INVITE()] });

    await click("Accept");

    expect(asked).toEqual([]);
    expect(responds).toEqual([{ id: mid(2), body: { response: "Accept" } }]);
  });

  it("decline is quiet: no question, no success dialog", async () => {
    const asked = withConfirm(true);
    await boot({ workItems: [INVITE()], feed: overlapping() });

    await click("Decline");

    expect(asked).toEqual([]);
    expect(responds).toEqual([{ id: mid(2), body: { response: "Decline" } }]);
    expect(modals).toEqual([]);
  });

  it("an invitation outside the visible range still gets the question — the feed is asked around its day", async () => {
    const asked = withConfirm(true);
    const invite = Object.assign(INVITE(), { id: mid(9), dueAt: "2027-02-01T07:00:00+00:00" });
    const feed = (from, to) => (from === "2027-01-31" && to === "2027-02-02"
      ? FEED({ meetings: [{ meetingId: mid(9), title: "Uzak", startAt: "2027-02-01T07:00:00Z", endAt: "2027-02-01T08:00:00Z", response: "pending", overlapsPlan: true, planOverlap: { taskId: "t", title: "Blok", startAt: "2027-02-01T07:00:00Z", endAt: "2027-02-01T08:00:00Z" } }] })
      : FEED());
    await boot({ workItems: [invite], feed });

    document.querySelector(`[data-dic-invite="${mid(9)}"] [data-mc-invite-answer="Accept"]`).click();
    await global.__mcLastAnswer;

    expect(asked).toHaveLength(1);
  });
});

// ── F. language, direction, sources ─────────────────────────────────────────────────────────────────────

describe("language and direction", () => {
  it("Arabic is right-to-left", async () => {
    await boot({ lang: "ar" });

    expect(document.querySelector("[data-mc-calendar-host]").classList.contains("fc-direction-rtl")).toBe(true);
  });

  it("the calendar chrome reads the shared payload", async () => {
    await boot();

    expect(document.querySelector(".fc-today-button").textContent).toBe("BUGÜN");
  });
});

describe("the source guarantees", () => {
  const CALENDAR_JS = read("wwwroot", "assets", "js", "Meetings", "calendar.js");
  const CARD_JS = read("wwwroot", "assets", "js", "shared", "diten-invite-card.js");
  const APP = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
  const INDEX = read("Views", "Meetings", "Index.cshtml");

  it("the invitation card is drawn in ONE module; both pages call it, neither draws its own", () => {
    expect(CALENDAR_JS).toContain("global.DitenInviteCard.render(");
    expect(APP).toContain("global.DitenInviteCard.render(");
    [CALENDAR_JS, APP].forEach((source) => {
      expect(source).not.toContain("data-dic-invite=");
      expect(source).not.toContain("dic-card-title");
    });
    expect(CARD_JS).toContain("dic-card-title");
  });

  it("no inline style and no element.style in the new code", () => {
    [CALENDAR_JS, CARD_JS, INDEX].forEach((source) => {
      expect(source).not.toMatch(/style="/);
      expect(source).not.toMatch(/\.style\./);
    });
  });

  it("the Meetings page loads the calendar assets and its own script, after the filter rule", () => {
    expect(INDEX).toContain('<partial name="~/Views/Shared/_CalendarAssets.cshtml" />');
    expect(INDEX.indexOf('src="~/assets/js/Meetings/index.js"')).toBeGreaterThan(0);
    expect(INDEX.indexOf('src="~/assets/js/Meetings/index.js"')).toBeLessThan(INDEX.indexOf('src="~/assets/js/Meetings/calendar.js"'));
  });

  it("no CDN", () => {
    [CALENDAR_JS, CARD_JS, INDEX, read("Views", "Shared", "_CalendarAssets.cshtml")].forEach((source) => {
      expect(source).not.toMatch(/https?:\/\//);
    });
  });
});

// ── G. seven languages ──────────────────────────────────────────────────────────────────────────────────

const valueOf = (file, key) => {
  const match = fs.readFileSync(file, "utf8").match(new RegExp(`<data name="${key}"[^>]*>\\s*<value>([^<]+)</value>`));
  return match ? match[1].trim() : null;
};

describe("every new sentence exists in seven languages and reaches its payload", () => {
  const MEETINGS_KEYS = ["ViewSwitchLabel", "ViewList", "ViewCalendar", "CalendarInvitesTitle", "CalendarInvitesEmpty",
    "CalendarLegendAccepted", "CalendarLegendPending", "CalendarLegendOther", "CalendarLoadFailed", "InviteAccepted"];
  const SHARED_KEYS = ["CalendarUnresolved", "InviteCardType", "InviteAccept", "InviteDecline", "InviteOverlapTitle",
    "InviteOverlapText", "InviteAcceptAnyway"];

  it.each(MEETINGS_KEYS)("Meetings %s: 7 languages, translated, in the page payload", (key) => {
    const en = valueOf(web("Resources", "Views", "Meetings", "MeetingsIndex.en.resx"), key);
    expect(en, `en is missing ${key}`).toBeTruthy();
    LANGS.filter((l) => l !== "en").forEach((lang) => {
      const value = valueOf(web("Resources", "Views", "Meetings", `MeetingsIndex.${lang}.resx`), key);
      expect(value, `${lang} is missing ${key}`).toBeTruthy();
      expect(value, `${lang} ${key} is the English text`).not.toBe(en);
    });
    expect(read("Views", "Meetings", "_IndexL10n.cshtml")).toContain(`${key} = Localizer["${key}"].Value`);
  });

  it.each(SHARED_KEYS)("SharedResource %s: 7 languages, translated, in the calendar payload", (key) => {
    const en = valueOf(web("Resources", "SharedResource.en.resx"), key);
    expect(en).toBeTruthy();
    LANGS.filter((l) => l !== "en").forEach((lang) => {
      const value = valueOf(web("Resources", `SharedResource.${lang}.resx`), key);
      expect(value, `${lang} is missing ${key}`).toBeTruthy();
      expect(value, `${lang} ${key} is the English text`).not.toBe(en);
    });
    expect(read("Views", "Shared", "_CalendarAssets.cshtml")).toContain(`["${key}"] = SharedLocalizer["${key}"].Value`);
  });

  it("the Task Center's 'cut at the day end' sentence exists in seven languages", () => {
    const en = valueOf(web("Resources", "Views", "WorkCenterNext", "WorkCenterNextIndex.en.resx"), "CalTruncatedNoEstimate");
    expect(en).toBeTruthy();
    LANGS.filter((l) => l !== "en").forEach((lang) => {
      const value = valueOf(web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "CalTruncatedNoEstimate");
      expect(value, lang).toBeTruthy();
      expect(value).not.toBe(en);
    });
  });

  it("the overlap sentence keeps both slots in every language", () => {
    LANGS.forEach((lang) => {
      const value = valueOf(web("Resources", `SharedResource.${lang}.resx`), "InviteOverlapText");
      expect(value, lang).toContain("{0}");
      expect(value, lang).toContain("{1}");
    });
  });
});

// ── CT acceptance (Control Tower, 2026-09-29): two behaviours the CT sabotage round found unguarded ──────────────

describe("CT: names are text, and a cancelled meeting is never drawn", () => {
  /*
   * The invite card builds its markup as a string; the meeting title comes from whoever organised it. Dropping the
   * escape on the title passed the whole suite before this test.
   */
  it("an invitation whose title is markup is drawn as text, no element is created from it", async () => {
    const title = '<img src="x" data-xss="invite">';
    const invite = INVITE();
    invite.title = { kind: "display", text: title, locale: "und" };
    await boot({ workItems: [invite] });

    const card = document.querySelector(`[data-mc-invite-list] [data-dic-invite="${mid(2)}"]`);
    expect(card, "the invitation card is on the panel").not.toBeNull();
    expect(document.querySelector("[data-xss]"), "an invitation title became markup").toBeNull();
    expect(card.querySelector(".dic-card-title").textContent).toBe(title);
  });

  /*
   * The fixture's cancelled meeting (row 4) is one I am ON, so the "declined" rule already hides it and the
   * cancelled filter was never exercised: removing it passed the suite. A cancelled meeting I am NOT on (a reader
   * who sees every meeting) must not be drawn either.
   */
  it("a cancelled meeting I am not on is not drawn as somebody else's meeting", async () => {
    const rows = ROWS().concat([row(7, "2026-10-09T10:00:00Z", { iAmAttendee: false, lifecycle: 1 })]);
    await boot({ rows });

    expect(drawnIds()).not.toContain(mid(7));
    expect(drawnIds()).toContain(mid(5));
  });
});

describe("CT: the list reads more than 25, and the page can say an answer failed", () => {
  /*
   * The proxy (MeetingsController.ApiList(string? query)) forwards ONE `query` parameter; `?pageSize=1000` never
   * reached Platform, so the table stopped at the default 25 while the calendar (listQuery) read everything — two
   * different sets on one page, and the organizer filter built from the table's rows missed the rest.
   */
  // ATT-FIX1 — a list page is at most 200 rows on the server; the table and the create form PAGE through every
  // meeting (MeetingsApi.listAll, measured in meetings-attendee-search.test.js) instead of asking for 1000 at once.
  it("the table and the create form page through the list instead of asking for 1000 rows", () => {
    const index = read("wwwroot", "assets", "js", "Meetings", "index.js");
    const form = read("wwwroot", "assets", "js", "Meetings", "form.js");
    expect(index).toContain("window.MeetingsApi.listAll()");
    expect(index).not.toMatch(/pageSize=1000/);
    expect(form).toContain("window.MeetingsApi.listAll({ titlesOnly: true })");   // ATT-FIX2 — the light read
    expect(form).not.toMatch(/pageSize: 1000/);
  });

  /* calendar.js reports answer errors through DitenModal, which the tenant shell does not load. */
  it("the Meetings page loads DitenModal before the calendar script", () => {
    const view = read("Views", "Meetings", "Index.cshtml");
    const modal = view.indexOf("~/assets/js/shared/premium-modal.js");
    expect(modal, "premium-modal.js is not loaded on the Meetings page").toBeGreaterThan(-1);
    expect(modal).toBeLessThan(view.indexOf("~/assets/js/Meetings/calendar.js"));
  });

  /* A range the reader has paged away from must stop asking page after page (up to 50 calls per range). */
  it("the list loop checks whether its range is still wanted before every page", () => {
    const calendar = read("wwwroot", "assets", "js", "Meetings", "calendar.js");
    expect(calendar).toMatch(/for \(let page = 1; page <= MAX_PAGES; page \+= 1\) \{[\s\S]{0,300}if \(isStale\(\)\) \{ return null; \}/);
    expect(calendar).toContain("fetchListSet(fromUtc, toUtc, () => generation !== state.generation)");
  });
});
