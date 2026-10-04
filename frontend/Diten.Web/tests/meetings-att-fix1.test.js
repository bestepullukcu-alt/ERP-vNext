const fs = require("fs");
const path = require("path");
const vm = require("vm");
const { loadScript } = require("./load-script");

/*
 * WP-MEETINGS-ATTENDEE-SEARCH-01 · ATT-FIX1 (BL-531 review) — measured on the PRODUCTION Meetings scripts with only
 * the network seam, DataTables and jQuery doubled:
 *  1. the three Meetings lists draw a typed title / name as TEXT (a `<img onerror>` title never becomes an element);
 *  3. the report's organizer filter is filled from the report's own rows — no people search, "show all" reachable;
 *  4. a search term that is too short once trimmed asks nothing and says "at least two characters";
 *  5. the visible placeholder of a people picker is the markup's one (on the real select2);
 *  6. "change organizer" with nobody chosen is refused with its own sentence;
 *  7. the list page pages through every meeting (≤ 200 a call) instead of asking for 1000.
 */

const WEB = path.resolve(__dirname, "..");
const read = (...parts) => fs.readFileSync(path.join(WEB, ...parts), "utf8");
const flush = async () => { for (let i = 0; i < 8; i += 1) { await new Promise((r) => setTimeout(r, 0)); } };
const XSS = '<img src=x onerror="window.__pwned=1">';
const ORG = "11111111-0000-0000-0000-000000000001";
const GONE = "11111111-0000-0000-0000-000000000002";

/** What the browser does with a column's rendered HTML. */
const drawn = (html) => { const cell = document.createElement("div"); cell.innerHTML = html; return cell; };

/** Boots one Meetings list script with DataTables doubled; answers the options it handed DataTables for ITS table
 *  (a script loaded by an earlier test still listens for DOMContentLoaded, so only this table's call counts). Each
 *  script is loaded once per file: its top-level `const` cannot be declared twice. */
const booted = {};
const bootList = async (script, tableClass) => {
  if (booted[script]) { return booted[script]; }
  document.body.innerHTML = `<table class="${tableClass}"></table>`;
  let captured = null;
  global.DtDefaults = { exportButtons: () => [], create: (c) => c };
  global.DitenDataTable = {
    createCrudTable: (options) => {
      if (options.tableEl && options.tableEl.classList.contains(tableClass)) { captured = options; }
      return { on() {}, rows: () => ({ data: () => ({ toArray: () => [] }) }) };
    },
    renderActions: () => "",
    getAuthHeaders: () => ({})
  };
  global.MeetingsL10n = { t: (key) => key };
  global.MeetingsApi = Object.assign(global.MeetingsApi || {}, {
    lookupTypes: async () => ({ ok: true, data: [{ id: "t1", name: XSS }] }),
    listAll: async () => ({ ok: true, data: [] })
  });
  loadScript(script);
  document.dispatchEvent(new Event("DOMContentLoaded"));
  await flush();
  expect(captured, `${script} built no table`).toBeTruthy();
  booted[script] = captured;
  return captured;
};

const renderOf = (options, target) => options.config.columnDefs.find((c) => c.targets === target).render;

describe("ATT-FIX1 (1) — the Meetings lists draw what people typed as text, never as markup", () => {
  afterEach(() => { delete window.__pwned; });

  it("the meetings list: title, type name and organizer name", async () => {
    const options = await bootList("wwwroot/assets/js/Meetings/index.js", "datatables-meetings");
    const row = { id: "m1", title: XSS, meetingTypeId: "t1", organizerUserId: ORG, organizerDisplayName: XSS };

    [renderOf(options, 1)(XSS, "display", row), renderOf(options, 2)("t1", "display", row), renderOf(options, 5)(ORG, "display", row)]
      .forEach((html) => {
        const cell = drawn(html);
        expect(cell.querySelector("img"), `an element was drawn from: ${html}`).toBeNull();
        expect(cell.textContent).toContain("<img");
      });
  });

  it("the series list: series name and type name", async () => {
    const options = await bootList("wwwroot/assets/js/Meetings/series/index.js", "datatables-meeting-series");
    [renderOf(options, 1)(XSS), renderOf(options, 2)(XSS)].forEach((html) => {
      expect(drawn(html).querySelector("img")).toBeNull();
      expect(drawn(html).textContent).toContain("<img");
    });
  });

  it("the meeting types list: type name", async () => {
    const options = await bootList("wwwroot/assets/js/Meetings/types/index.js", "datatables-meeting-types");
    const html = renderOf(options, 1)(XSS);
    expect(drawn(html).querySelector("img")).toBeNull();
    expect(drawn(html).textContent).toContain("<img");
  });
});

describe("ATT-FIX1 (7) — the meetings list pages through every meeting, ≤ 200 a call", () => {
  it("MeetingsApi.listAll asks page after page of 200 and hands back every row", async () => {
    const asked = [];
    global.fetch = async (url) => {
      asked.push(decodeURIComponent(url));
      const page = Number(new URLSearchParams(decodeURIComponent(url).split("?query=")[1]).get("page"));
      const count = page < 3 ? 200 : 50;
      const items = Array.from({ length: count }, (_, i) => ({ id: `${page}-${i}` }));
      return { ok: true, status: 200, json: async () => ({ data: { items, totalCount: 450 } }) };
    };
    delete global.MeetingsApi;
    loadScript("wwwroot/assets/js/Meetings/api.js");

    const res = await global.MeetingsApi.listAll({ includeNames: false });

    expect(res.ok).toBe(true);
    expect(res.data).toHaveLength(450);
    expect(asked).toHaveLength(3);
    asked.forEach((url, i) => {
      const query = new URLSearchParams(url.split("?query=")[1]);
      expect(query.get("page")).toBe(String(i + 1));
      expect(query.get("pageSize")).toBe("200");
      expect(query.get("includeNames")).toBe("false");
    });
    delete global.fetch;
  });

  it("the list page's table reads through listAll, and the shared table passes a function source through", async () => {
    const options = await bootList("wwwroot/assets/js/Meetings/index.js", "datatables-meetings");
    expect(typeof options.ajax).toBe("function");
    global.MeetingsApi.listAll = async () => ({ ok: true, data: [{ id: "a" }, { id: "b" }] });
    const delivered = await new Promise((resolve) => options.ajax({}, resolve));
    expect(delivered.data.map((r) => r.id)).toEqual(["a", "b"]);

    let built = null;
    global.DataTable = function DataTable(el, config) { built = config; return { on() {} }; };
    global.DtDefaults = { create: (c) => c, exportButtons: () => [] };
    delete global.DitenDataTable;
    loadScript("wwwroot/assets/js/diten-datatable.js");
    const source = () => {};
    global.DitenDataTable.createCrudTable({ tableEl: document.createElement("table"), ajax: source, config: {} });
    expect(built.ajax).toBe(source);
  });

  it("the filter names an organizer by the row's own name, an unnamed one 'unknown user' — never the id", async () => {
    await bootList("wwwroot/assets/js/Meetings/index.js", "datatables-meetings");   // the page itself, loaded once
    const filter = document.createElement("select");
    filter.id = "filterOrganizer";
    const types = document.createElement("select");
    types.id = "filterMeetingType";
    document.body.append(filter, types);
    global.$ = (selector) => {
      const node = document.querySelector(selector);
      return { empty() { node.innerHTML = ""; return this; }, append(option) { node.appendChild(option); return this; } };
    };
    const list = vm.runInThisContext("MeetingsList");
    list.populateFilterOptions([
      { organizerUserId: ORG, organizerDisplayName: "Org Kişi" },
      { organizerUserId: GONE, organizerDisplayName: null }
    ], []);

    const texts = Array.from(filter.options).map((o) => o.textContent);
    expect(texts).toEqual(["Org Kişi", "unknownUser"]);
    expect(texts.join(" ")).not.toContain(GONE);
  });
});

describe("ATT-FIX1 (3, 8c) — the report's organizer filter and its words", () => {
  const MARKUP = `
    <div id="mrTiles"></div><span id="mrStatus"></span><div id="mrNoAccess" class="d-none"></div>
    <input id="mrFrom" value="2026-10-01" /><input id="mrTo" value="2026-10-31" />
    <select id="mrMeetingType"><option value=""></option></select>
    <select id="mrOrganizer" data-placeholder="ShowAll"><option value="">ShowAll</option></select>
    <span id="mrTileMeetingCount"></span><span id="mrTileAttendanceRate"></span><span id="mrTileDecisionCount"></span>
    <span id="mrTileOpenActions"></span><span id="mrTileOverdueActions"></span>
    <div id="mrMeetingsCard"></div><table><tbody id="mrMeetingsBody"></tbody></table><p id="mrMeetingsEmpty"></p>
    <div id="mrDecisionsCard"></div><table><tbody id="mrDecisionsBody"></tbody></table><p id="mrDecisionsEmpty"></p>
    <div id="mrActionsCard"></div><div id="mrActionsSkeleton"></div><p id="mrActionsEmpty"></p><table id="mrActionsTable"></table>`;

  const report = (meetings) => ({
    totals: { meetingCount: meetings.length, attendanceRatePercent: null, decisionCount: 0, openActionCount: 0, overdueActionCount: 0 },
    meetings, decisions: [], actions: []
  });

  it("is filled from the organizers the report showed, asks no people search, and 'show all' stays reachable", async () => {
    document.body.innerHTML = MARKUP;
    let answer = report([
      { title: "A", meetingTypeName: "T", startAt: "2026-10-05T09:00:00Z", organizerUserId: ORG, organizerDisplayName: "Org Kişi", attendeeCount: 0, respondedCount: 0 },
      { title: "B", meetingTypeName: "T", startAt: "2026-10-06T09:00:00Z", organizerUserId: GONE, attendeeCount: 0, respondedCount: 0 }
    ]);
    const searched = [];
    global.fetch = async () => ({ ok: true, status: 200, json: async () => ({ data: answer }) });
    global.MeetingsApi = {
      lookupTypes: async () => ({ ok: true, data: [] }),
      lookupAttendees: async (args) => { searched.push(args); return { ok: true, data: [] }; }
    };
    global.MeetingReportL10n = { t: (key) => key };
    delete global.jQuery;
    delete global.$;
    loadScript("wwwroot/assets/vendor/libs/jquery/jquery.js");
    loadScript("wwwroot/assets/vendor/libs/select2/select2.js");
    delete global.MeetingReportScreen;
    loadScript("wwwroot/assets/js/Meetings/Report/index.js");
    const screen = global.MeetingReportScreen;

    screen.initPickers();
    await screen.loadReport();

    const organizer = document.getElementById("mrOrganizer");
    const options = () => Array.from(organizer.options).map((o) => [o.value, o.textContent]);
    expect(options()).toEqual([["", "ShowAll"], [ORG, "Org Kişi"], [GONE, "unknownUser"]]);
    expect(searched, "the report asked the people directory").toEqual([]);

    // Narrowed to one organizer, the others stay offered; and back to "show all".
    global.jQuery(organizer).val(ORG).trigger("change");
    answer = report([{ title: "A", meetingTypeName: "T", startAt: "2026-10-05T09:00:00Z", organizerUserId: ORG, organizerDisplayName: "Org Kişi", attendeeCount: 0, respondedCount: 0 }]);
    await screen.loadReport();
    expect(organizer.value).toBe(ORG);
    expect(options().map(([value]) => value)).toEqual(["", ORG, GONE]);
    global.jQuery(organizer).val("").trigger("change");
    expect(screen.buildQuery().has("organizerUserId")).toBe(false);
    delete global.fetch;
  });

  it("its words resolve through the camelCase payload the server really sends", () => {
    document.body.innerHTML = '<script id="meeting-report-l10n" type="application/json">{"errorNoAccess":"Bu rapora erişiminiz yok.","lifecycleOpen":"Açık"}</script>' + MARKUP;
    delete global.MeetingReportL10n;
    loadScript("wwwroot/assets/js/Meetings/Report/index.l10n.js");
    delete global.MeetingReportScreen;
    loadScript("wwwroot/assets/js/Meetings/Report/index.js");

    global.MeetingReportScreen.showNoAccess();
    expect(document.getElementById("mrNoAccess").textContent).toBe("Bu rapora erişiminiz yok.");
    expect(global.MeetingReportScreen.lifecycleLabel("Open")).toBe("Açık");
  });
});

describe("ATT-FIX1 (4, 5) — on the real select2: the trimmed term and the visible placeholder", () => {
  const bootReal = (markup) => {
    document.body.innerHTML = markup;
    delete global.$;
    delete global.jQuery;
    loadScript("wwwroot/assets/vendor/libs/jquery/jquery.js");
    loadScript("wwwroot/assets/vendor/libs/select2/select2.js");
    delete global.DitenPeopleSearch;
    loadScript("wwwroot/assets/js/shared/diten-people-search.js");
  };
  const words = { minimumLength: "type two", noResults: "nobody", searching: "searching", unknown: "?", rateLimited: "too many", failed: "failed" };

  it("' a' (one character once trimmed) asks nothing and says 'type at least two characters'", async () => {
    bootReal('<select id="p"><option value=""></option></select>');
    const asked = [];
    const $p = global.jQuery("#p");
    $p.select2(Object.assign({ dropdownParent: global.jQuery(document.body) },
      global.DitenPeopleSearch.options({ search: async (term) => { asked.push(term); return { ok: true, data: [] }; }, text: words })));
    $p.select2("open");
    const box = document.querySelector(".select2-search__field");
    box.value = " a";
    global.jQuery(box).trigger("input");
    await new Promise((r) => setTimeout(r, 400));
    await flush();
    expect(asked).toEqual([]);
    expect(document.querySelector(".select2-results__message").textContent).toBe("type two");
  });

  it("the attendee pickers show the markup's search hint (one source; the page code sets none)", () => {
    // The page's own markup, as the views write it.
    ["_Form.cshtml", "Details.cshtml"].forEach((view) =>
      expect(read("Views", "Meetings", view)).toContain('data-placeholder="@Localizer["PeopleSearchHint"]"'));
    expect(read("Views", "Meetings", "Series", "_Form.cshtml").match(/data-placeholder="@MeetingsLocalizer\["PeopleSearchHint"\]"/g)).toHaveLength(2);

    // The page's own select2 call (Meetings/form.js create branch) on the real select2: the hint is what shows.
    bootReal('<select id="fieldAttendeeUserIds" multiple data-placeholder="Ad ya da pozisyon yazın…"></select>');
    global.MeetingsL10n = { t: (key) => key };
    global.MeetingsApi = { failureMessage: () => "x" };
    const settings = global.DitenPeopleSearch.options({ search: async () => ({ ok: true, data: [] }), text: words });
    global.jQuery("#fieldAttendeeUserIds").select2(Object.assign({ dropdownParent: global.jQuery(document.body), width: "100%", closeOnSelect: false }, settings));
    const shown = document.querySelector(".select2-search__field").getAttribute("placeholder");
    expect(shown).toBe("Ad ya da pozisyon yazın…");
    expect(read("wwwroot", "assets", "js", "Meetings", "form.js")).not.toMatch(/placeholder:\s*t\('peopleSearchHint'\)/);
  });
});

describe("ATT-FIX1 (6) — 'change organizer' with nobody chosen is refused with its own words", () => {
  it("the window demands a person and names what is missing", async () => {
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
    let confirmOptions = null;
    global.showConfirm = (title, onConfirm, options) => { confirmOptions = options; };
    global.MeetingsL10n = { t: (key) => key };
    global.DitenRelatedRecords = { renderRelatedRows: () => "" };
    global.jQuery = undefined;
    global.MeetingsApi = {
      get: async () => ({ ok: true, data: { id: "m1", title: "T", lifecycle: 0, version: 1, startAt: "2026-10-05T09:00:00Z", endAt: "2026-10-05T10:00:00Z", organizerUserId: ORG, attendees: [], agendaItems: [] } }),
      linkedTasks: async () => ({ ok: true, data: [] }),
      lookupTypes: async () => ({ ok: true, data: [] }),
      failureMessage: () => "x"
    };
    delete global.DitenPeopleSearch;
    loadScript("wwwroot/assets/js/shared/diten-people-search.js");
    loadScript("wwwroot/assets/js/Meetings/form.js");
    document.dispatchEvent(new Event("DOMContentLoaded"));
    await flush();

    document.getElementById("btnReassignOrganizer").click();
    expect(confirmOptions.inputRequired).toBe(true);
    expect(confirmOptions.inputValidationMessage).toBe("newOrganizerRequired");
  });
});
