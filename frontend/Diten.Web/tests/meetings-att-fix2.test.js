const fs = require("fs");
const path = require("path");
const vm = require("vm");
const { loadScript } = require("./load-script");

/*
 * WP-MEETINGS-ATTENDEE-SEARCH-01 · ATT-FIX2 — measured on the PRODUCTION Meetings scripts:
 *  3. a linked meeting the reader cannot open is named by a sentence, never by its id;
 *  4. listAll keeps each meeting once, fails whole on a failed page, and says when it stopped short;
 *  5. the list page's function source keeps the 401 / failure / truncation paths the object source had;
 *  8. names put into options are text (new Option), whatever they contain.
 */

const flush = async () => { for (let i = 0; i < 8; i += 1) { await new Promise((r) => setTimeout(r, 0)); } };
const ORG = "11111111-0000-0000-0000-000000000001";
const SECRET = "99999999-0000-0000-0000-000000000009";

describe("ATT-FIX2 (3) — a linked meeting the reader cannot open", () => {
  it("is named by the sentence, never by its id — both directions", async () => {
    document.body.innerHTML = `
      <div id="meetingDetailsRoot" data-meeting-id="m1">
        <div id="detailsActionBar"><button id="btnScheduleFollowUp"></button><a id="btnOpenMinutes"></a>
          <button id="btnReassignOrganizer"></button><button id="btnCancelMeeting"></button><a id="btnEditMeeting"></a></div>
        <div id="meetingNotFound"></div><div id="meetingDetailsBody">
          <div id="dTitle"></div><div id="dType"></div><div id="dStatus"></div><div id="dStartAt"></div><div id="dEndAt"></div>
          <div id="dLocation"></div><div id="dOrganizer"></div><div id="dDescription"></div>
          <div id="dCancellationReasonRow"><div id="dCancellationReason"></div></div>
          <div id="dFollowUpOfRow"><a id="dFollowUpOfLink"></a></div><div id="dFollowedByRow"><a id="dFollowedByLink"></a></div>
          <ul id="agendaList"></ul><p id="noAgendaHint"></p><div id="agendaAddRow"></div><div id="taskAddRow"></div>
          <div id="linkedTasksList"></div><p id="noLinkedTasksHint"></p>
          <ul id="attendeesList"></ul><div id="attendeeAddRow"><select id="newAttendeeUserId"></select><button id="btnAddAttendee"></button></div>
        </div>
      </div>`;
    global.MeetingsL10n = { t: (key) => key };
    global.DitenRelatedRecords = { renderRelatedRows: () => "" };
    global.DitenModal = { success: () => {}, error: () => {} };
    global.jQuery = undefined;
    global.MeetingsApi = {
      get: async () => ({ ok: true, data: {
        id: "m1", title: "T", lifecycle: 0, version: 1, startAt: "2026-10-05T09:00:00Z", endAt: "2026-10-05T10:00:00Z",
        organizerUserId: ORG, attendees: [],
        agendaItems: [{ id: "a1", text: '<img src=x onerror="window.__pwned=1">', sortOrder: 1 }],
        followUpOfMeetingId: SECRET, followUpOfMeetingTitle: null, followedByMeetingId: SECRET, followedByMeetingTitle: null
      } }),
      linkedTasks: async () => ({ ok: true, data: [] }),
      lookupTypes: async () => ({ ok: true, data: [] }),
      failureMessage: () => "x"
    };
    delete global.DitenPeopleSearch;
    loadScript("wwwroot/assets/js/shared/diten-people-search.js");
    loadScript("wwwroot/assets/js/Meetings/form.js");
    document.dispatchEvent(new Event("DOMContentLoaded"));
    await flush();

    ["dFollowUpOfLink", "dFollowedByLink"].forEach((id) => {
      const link = document.getElementById(id);
      expect(link.textContent, id).toBe("meetingNotAccessible");
      expect(link.textContent, id).not.toContain(SECRET);
    });
    // The agenda line (form.js's own innerHTML) is text too.
    expect(document.getElementById("agendaList").querySelector("img")).toBeNull();
    expect(document.getElementById("agendaList").textContent).toContain("<img");
  });
});

describe("ATT-FIX2 (4) — MeetingsApi.listAll", () => {
  const load = (answer) => {
    const asked = [];
    global.fetch = async (url) => {
      const query = new URLSearchParams(decodeURIComponent(url).split("?query=")[1]);
      asked.push(query);
      return answer(Number(query.get("page")));
    };
    delete global.MeetingsApi;
    loadScript("wwwroot/assets/js/Meetings/api.js");
    return asked;
  };
  const page = (items, totalCount) => ({ ok: true, status: 200, json: async () => ({ data: { items, totalCount } }) });
  afterEach(() => { delete global.fetch; });

  it("a meeting met twice across pages is kept once", async () => {
    const rows = (from) => Array.from({ length: 200 }, (_, i) => ({ id: `r${from + i}` }));
    load((n) => (n === 1 ? page(rows(0), 400) : page([...rows(199)].slice(0, 200), 400)));
    const res = await global.MeetingsApi.listAll();
    expect(res.ok).toBe(true);
    const ids = res.data.map((r) => r.id);
    expect(new Set(ids).size).toBe(ids.length);
    expect(ids).toHaveLength(399);
  });

  it("a failed page fails the whole read, keeps its status, and shows no half list", async () => {
    load((n) => (n === 1 ? page(Array.from({ length: 200 }, (_, i) => ({ id: `r${i}` })), 600) : { ok: false, status: 401, json: async () => ({}) }));
    const res = await global.MeetingsApi.listAll();
    expect(res.ok).toBe(false);
    expect(res.status).toBe(401);
    expect(res.data).toEqual([]);
  });

  it("a set larger than fifty pages is not cut silently: truncated", async () => {
    const asked = load((n) => page(Array.from({ length: 200 }, (_, i) => ({ id: `p${n}-${i}` })), 20000));
    const res = await global.MeetingsApi.listAll();
    expect(asked).toHaveLength(50);
    expect(res.ok).toBe(true);
    expect(res.truncated).toBe(true);
    expect(res.data).toHaveLength(10000);
  });
});

describe("ATT-FIX2 (5) — the meetings list's function source keeps the failure paths", () => {
  let options;
  let toasts;
  let unauthorized;

  beforeAll(async () => {
    document.body.innerHTML = '<table class="datatables-meetings"></table>';
    global.DtDefaults = { exportButtons: () => [], create: (c) => c, handleUnauthorized: () => { unauthorized += 1; } };
    global.DitenDataTable = {
      createCrudTable: (o) => { if (o.tableEl?.classList.contains("datatables-meetings")) { options = o; } return { on() {} }; },
      renderActions: () => "", getAuthHeaders: () => ({})
    };
    global.MeetingsL10n = { t: (key) => key };
    global.MeetingsApi = { lookupTypes: async () => ({ ok: true, data: [] }), listAll: async () => ({ ok: true, data: [] }) };
    loadScript("wwwroot/assets/js/Meetings/index.js");
    document.dispatchEvent(new Event("DOMContentLoaded"));
    await flush();
  });

  beforeEach(() => {
    toasts = [];
    unauthorized = 0;
    global.showToast = (message, type) => toasts.push([message, type]);
  });

  const read = (answer) => {
    global.MeetingsApi.listAll = answer;
    return new Promise((resolve) => options.ajax({}, resolve));
  };

  it("a 401 renews the session through the shared DataTables helper", async () => {
    const delivered = await read(async () => ({ ok: false, status: 401, data: [] }));
    expect(unauthorized).toBe(1);
    expect(delivered.data).toEqual([]);
  });

  it("another failure is said and the table still draws (empty)", async () => {
    const delivered = await read(async () => ({ ok: false, status: 500, data: [] }));
    expect(toasts).toEqual([["errorOccurred", "error"]]);
    expect(delivered.data).toEqual([]);
  });

  it("a truncated set is said, and what was read is shown", async () => {
    const delivered = await read(async () => ({ ok: true, status: 200, data: [{ id: "a" }], truncated: true }));
    expect(toasts).toEqual([["meetingsListTruncated", "warning"]]);
    expect(delivered.data).toEqual([{ id: "a" }]);
  });

  it("a read that throws is said and never leaves the table waiting", async () => {
    const delivered = await read(async () => { throw new Error("network"); });
    expect(toasts).toEqual([["errorOccurred", "error"]]);
    expect(delivered.data).toEqual([]);
  });

  it("the filter's options are text: a name with markup becomes no element", () => {
    const filter = document.createElement("select");
    filter.id = "filterOrganizer";
    const types = document.createElement("select");
    types.id = "filterMeetingType";
    document.body.append(filter, types);
    global.$ = (selector) => {
      const node = document.querySelector(selector);
      return { empty() { node.innerHTML = ""; return this; }, append(option) { node.appendChild(option); return this; } };
    };
    vm.runInThisContext("MeetingsList").populateFilterOptions([{ organizerUserId: ORG, organizerDisplayName: "<b>Org</b>" }], []);
    expect(filter.querySelector("b")).toBeNull();
    expect(filter.options[0].textContent).toBe("<b>Org</b>");
  });
});

describe("ATT-FIX2 (8) — the report's organizer options are text", () => {
  it("a name with markup becomes no element", () => {
    document.body.innerHTML = '<select id="mrOrganizer"><option value="">ShowAll</option></select>';
    global.MeetingReportL10n = { t: (key) => key };
    global.jQuery = undefined;
    delete global.MeetingReportScreen;
    loadScript("wwwroot/assets/js/Meetings/Report/index.js");
    global.MeetingReportScreen.rememberOrganizers([{ organizerUserId: ORG, organizerDisplayName: "<b>Org</b>" }]);
    const select = document.getElementById("mrOrganizer");
    expect(select.querySelector("b")).toBeNull();
    expect(select.options[1].textContent).toBe("<b>Org</b>");
  });
});
