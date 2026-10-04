const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * BL-531 (WP-MEETINGS-ATTENDEE-SEARCH-01) — the Meetings screens no longer download the people directory.
 * Measured on the PRODUCTION Meetings scripts with only fetch-level seams (MeetingsApi) and jQuery stubbed:
 *  - create / details / series / report pickers SEARCH (≥ 2 characters) through the ONE shared transport;
 *  - details, minutes editor and list name people from the meeting's OWN read, never the directory;
 *  - a stored series is shown named, and an untouched save posts its organizer and attendees back unchanged;
 *  - an id that cannot be named reads "unknown user", never a GUID.
 */

const WEB = path.resolve(__dirname, "..");
const read = (...parts) => fs.readFileSync(path.join(WEB, ...parts), "utf8");
const flush = async () => { for (let i = 0; i < 6; i += 1) { await new Promise((r) => setTimeout(r, 0)); } };

const ORGANIZER = "11111111-0000-0000-0000-000000000001";
const ATTENDEE = "11111111-0000-0000-0000-000000000002";
const GONE = "11111111-0000-0000-0000-000000000003";
const AYSE = { userId: "22222222-0000-0000-0000-000000000001", displayName: "Ayşe Kaya", positionName: "Kalite Müdürü", organizationUnitName: "Kalite" };

let select2Settings;
let calls;

/** The jQuery surface the Meetings pages use, over the real DOM. */
const stubJQuery = () => {
  const jq = (selectorOrNode) => {
    const nodes = typeof selectorOrNode === "string"
      ? Array.from(document.querySelectorAll(selectorOrNode))
      : [selectorOrNode].filter(Boolean);
    const api = {
      length: nodes.length,
      hasClass: () => false,
      select2(settings) { nodes.forEach((n) => { select2Settings[n.id] = settings; }); return api; },
      find: () => ({ remove: () => {} }),
      append(option) { nodes.forEach((n) => n.appendChild(option)); return api; },
      trigger() { return api; },
      on() { return api; },
      val(value) {
        const node = nodes[0];
        if (value === undefined) {
          if (!node) { return undefined; }
          return node.multiple ? Array.from(node.selectedOptions).map((o) => o.value) : node.value;
        }
        nodes.forEach((n) => {
          const wanted = (Array.isArray(value) ? value : [value]).map(String);
          Array.from(n.options).forEach((o) => { o.selected = wanted.includes(o.value); });
        });
        return api;
      }
    };
    return api;
  };
  jq.fn = { select2: () => {} };
  global.$ = jq;
  global.jQuery = jq;
};

const stubApi = (overrides = {}) => {
  global.MeetingsApi = Object.assign({
    lookupAttendees: async (args) => { calls.push(["lookupAttendees", args]); return { ok: true, status: 200, data: [AYSE] }; },
    lookupTypes: async () => ({ ok: true, data: [] }),
    listQuery: async () => ({ ok: true, data: { items: [] } }),
    linkedTasks: async () => ({ ok: true, data: [] }),
    failureMessage: (res) => (res?.status === 429 ? "errorPeopleSearchRateLimited" : "errorOccurred")
  }, overrides);
};

const searchWith = (settings, term) => new Promise((resolve) =>
  settings.ajax.transport({ data: { term } }, (answer) => resolve({ results: answer.results }), (error) => resolve({ failed: error })));

beforeEach(() => {
  select2Settings = {};
  calls = [];
  stubJQuery();
  global.MeetingsL10n = { t: (key) => key };
  global.MeetingSeriesL10n = { t: (key) => key };
  global.MeetingReportL10n = { t: (key) => key };
  global.DitenModal = { success: () => {}, error: () => {} };
  global.DitenRelatedRecords = { renderRelatedRows: () => "", relatedRecordRow: () => "" };
  delete global.DitenPeopleSearch;
  loadScript("wwwroot/assets/js/shared/diten-people-search.js");
});

// ── the shared transport ─────────────────────────────────────────────────────────────────────────────────────

describe("BL-531 — shared/diten-people-search.js", () => {
  const words = { minimumLength: "min", noResults: "none", searching: "busy", unknown: "unknown", rateLimited: "too many", failed: "failed" };

  it("asks at two characters, labels name — position — unit, and drops the excluded ids whatever their case", async () => {
    const settings = global.DitenPeopleSearch.options({
      search: async () => ({ ok: true, data: [AYSE, { userId: ORGANIZER, displayName: "Org" }] }),
      text: words, exclude: () => [ORGANIZER.toUpperCase()]
    });
    expect(settings.minimumInputLength).toBe(2);
    expect(settings.ajax.delay).toBe(300);
    const { results } = await searchWith(settings, "ay");
    expect(results).toEqual([{ id: AYSE.userId, text: "Ayşe Kaya — Kalite Müdürü — Kalite" }]);
  });

  it("a failed read is said as a failure, never as 'nobody found'", async () => {
    const settings = global.DitenPeopleSearch.options({ search: async () => ({ ok: false, status: 429 }), text: words });
    const { failed } = await searchWith(settings, "ay");
    expect(failed).toBeTruthy();
    expect(settings.language.errorLoading()).toBe("too many");
    expect(settings.language.errorLoading()).not.toBe(settings.language.noResults());
    expect(settings.language.inputTooShort()).toBe("min");
  });

  it("an answer that arrives after a newer search is dropped", async () => {
    let releaseFirst;
    const answers = [new Promise((r) => { releaseFirst = r; }), Promise.resolve({ ok: true, data: [AYSE] })];
    const settings = global.DitenPeopleSearch.options({ search: () => answers.shift(), text: words });
    const seen = [];
    settings.ajax.transport({ data: { term: "ol" } }, (a) => seen.push(["old", a]), () => {});
    settings.ajax.transport({ data: { term: "ay" } }, (a) => seen.push(["new", a]), () => {});
    await flush();
    releaseFirst({ ok: true, data: [{ userId: GONE, displayName: "Stale" }] });
    await flush();
    expect(seen.map(([which]) => which)).toEqual(["new"]);
  });
});

describe("BL-531 — on the REAL select2 4.0.13: what the dropdown says after a search", () => {
  /*
   * The finding behind this block (BL-512 review): select2 reads `'status' in request` on whatever the transport
   * RETURNED before it shows `errorLoading`; a transport that returns nothing throws there and the list sits on
   * "searching…". Measured on the vendored bundle the pages load, not on a double of it.
   */
  // BL-512 FIX1 — the one rule: 429 has its own sentence, anything else is "the search could not be done".
  const words = { minimumLength: "min", noResults: "nobody found", searching: "searching", unknown: "unknown",
    rateLimited: "too many searches", failed: "could not search" };

  const typeInto = async (answer) => {
    document.body.innerHTML = '<select id="picker"><option value=""></option></select>';
    delete global.$;
    delete global.jQuery;
    loadScript("wwwroot/assets/vendor/libs/jquery/jquery.js");
    loadScript("wwwroot/assets/vendor/libs/select2/select2.js");
    const $picker = global.jQuery("#picker");
    $picker.select2(Object.assign({ dropdownParent: global.jQuery(document.body) },
      global.DitenPeopleSearch.options({ search: answer, text: words })));
    $picker.select2("open");
    const box = document.querySelector(".select2-search__field");
    box.value = "ay";
    global.jQuery(box).trigger("input");
    await new Promise((r) => setTimeout(r, global.DitenPeopleSearch.DELAY_MS + 100));
    await flush();
    const message = document.querySelector(".select2-results__message");
    return {
      message: message ? message.textContent : null,
      options: Array.from(document.querySelectorAll(".select2-results__option:not(.select2-results__message)")).map((o) => o.textContent)
    };
  };

  it("a found person is listed", async () => {
    const shown = await typeInto(async () => ({ ok: true, data: [AYSE] }));
    expect(shown.options).toEqual(["Ayşe Kaya — Kalite Müdürü — Kalite"]);
  });

  it("an empty answer says 'nobody found'", async () => {
    const shown = await typeInto(async () => ({ ok: true, data: [] }));
    expect(shown.message).toBe("nobody found");
  });

  it.each([[429, "too many searches"], [403, "could not search"], [503, "could not search"], [0, "could not search"]])(
    "a failed search (%i) says what failed — never 'searching…' forever, never 'nobody'", async (status, sentence) => {
      const shown = await typeInto(async () => ({ ok: false, status }));
      expect(shown.message).toBe(sentence);
    });

  it("a search that throws says the read failed", async () => {
    const shown = await typeInto(async () => { throw new Error("network"); });
    expect(shown.message).toBe("could not search");
  });
});

// ── 1. create ────────────────────────────────────────────────────────────────────────────────────────────────

describe("BL-531 — Meetings/form.js create: the attendee picker searches", () => {
  const boot = async () => {
    document.body.innerHTML = `
      <form id="meetingForm" data-form-mode="create"><input id="meetingId" value="" />
        <select id="fieldMeetingTypeId"></select>
        <div id="createOnlySection"><select id="fieldAttendeeUserIds" multiple></select><select id="fieldFollowUpOfMeetingId"></select></div>
      </form>`;
    stubApi();
    loadScript("wwwroot/assets/js/Meetings/form.js");
    document.dispatchEvent(new Event("DOMContentLoaded"));
    await flush();
  };

  it("reads no people list when the form opens, and binds a two-character search", async () => {
    await boot();
    expect(calls, "the people directory was read on open").toEqual([]);
    const settings = select2Settings.fieldAttendeeUserIds;
    expect(settings.minimumInputLength).toBe(2);
    expect(settings.placeholder).toBe("peopleSearchHint");
    const { results } = await searchWith(settings, "ay");
    expect(calls).toEqual([["lookupAttendees", { search: "ay" }]]);
    expect(results.map((r) => r.id)).toEqual([AYSE.userId]);
  });
});

// ── 2. details ───────────────────────────────────────────────────────────────────────────────────────────────

describe("BL-531 — Meetings/form.js details: names come with the meeting; add / reassign search", () => {
  const MEETING = {
    id: "33333333-0000-0000-0000-000000000001", title: "Kalite", lifecycle: 0, version: 2, startAt: "2026-10-05T09:00:00Z", endAt: "2026-10-05T10:00:00Z",
    organizerUserId: ORGANIZER, organizerDisplayName: "Org Kişi",
    attendees: [{ userId: ATTENDEE, displayName: "Katılımcı <b>Bir</b>", invitationResponse: 0 }, { userId: GONE, displayName: null, invitationResponse: 0 }],
    agendaItems: []
  };
  let confirmOptions;

  const boot = async () => {
    document.body.innerHTML = `
      <div id="meetingDetailsRoot" data-meeting-id="${MEETING.id}">
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
    stubApi({ get: async () => ({ ok: true, data: MEETING }) });
    confirmOptions = null;
    global.showConfirm = (title, onConfirm, options) => { confirmOptions = options; };
    global.DitenDialog = { bindDialogSelect2: (box, popup, opts) => { select2Settings.reassign = opts; } };
    loadScript("wwwroot/assets/js/Meetings/form.js");
    document.dispatchEvent(new Event("DOMContentLoaded"));
    await flush();
  };

  it("reads no people list, and shows the organizer and attendees by the names the meeting carries", async () => {
    await boot();
    expect(calls).toEqual([]);
    expect(document.getElementById("dOrganizer").textContent).toBe("Org Kişi");
    const listed = document.getElementById("attendeesList").textContent;
    expect(listed).toContain("Katılımcı <b>Bir</b>");   // escaped: shown as text, not markup
    expect(listed).toContain("unknownUser");
    expect(listed).not.toContain(GONE);
  });

  it("'add attendee' searches and never offers the organizer or someone already invited", async () => {
    await boot();
    const settings = select2Settings.newAttendeeUserId;
    expect(settings.minimumInputLength).toBe(2);
    global.MeetingsApi.lookupAttendees = async () => ({ ok: true, data: [AYSE, { userId: ORGANIZER }, { userId: ATTENDEE }] });
    const { results } = await searchWith(settings, "ka");
    expect(results.map((r) => r.id)).toEqual([AYSE.userId]);
  });

  it("'change organizer' lists nobody up front and searches once open, without the current organizer", async () => {
    await boot();
    document.getElementById("btnReassignOrganizer").click();
    expect(confirmOptions.inputOptions).toEqual({ "": "peopleSearchHint" });
    const box = document.createElement("select");
    box.className = "swal2-select";
    const popup = document.createElement("div");
    popup.appendChild(box);
    confirmOptions.didOpen(popup);
    global.MeetingsApi.lookupAttendees = async () => ({ ok: true, data: [AYSE, { userId: ORGANIZER }] });
    const { results } = await searchWith(select2Settings.reassign, "or");
    expect(results.map((r) => r.id)).toEqual([AYSE.userId]);
  });
});

// ── 3. series ────────────────────────────────────────────────────────────────────────────────────────────────

describe("BL-531 — Meetings/series/form.js: search, named stored people, untouched save keeps them", () => {
  const SERIES_ID = "44444444-0000-0000-0000-000000000001";
  let sent;

  const boot = async () => {
    document.body.innerHTML = `
      <form id="meetingSeriesForm" data-form-mode="edit"><input id="meetingSeriesId" value="${SERIES_ID}" />
        <input id="fieldName" /><select id="fieldMeetingTypeId"><option value="type-1">T</option></select><input id="fieldLocation" />
        <select id="fieldFrequency"><option value="1">1</option></select><input id="fieldInterval" /><input id="fieldDurationMinutes" />
        <input id="fieldStartsAt" /><input id="fieldEndsAt" /><input id="fieldLeadTimeDays" />
        <select id="fieldOrganizerUserId"><option value=""></option></select><select id="fieldAttendeeUserIds" multiple></select>
        <input type="checkbox" id="fieldChainAsFollowUp" /><input type="checkbox" id="fieldIsActive" />
        <span id="fieldLastGeneratedAt"></span><input id="meetingSeriesExpectedVersion" />
        <div id="formValidationSummary"></div><div id="fieldNameError"></div>
      </form>`;
    sent = null;
    stubApi({
      seriesGet: async () => ({
        ok: true,
        data: {
          id: SERIES_ID, name: "Aylık", meetingTypeId: "type-1", frequency: 1, interval: 1, durationMinutes: 60,
          startsAt: "2026-10-05T09:00:00Z", endsAt: null, leadTimeDays: 14, chainAsFollowUp: false, isActive: true, version: 4,
          organizerUserId: ORGANIZER, organizerDisplayName: "Org Kişi",
          attendeeUserIds: [ATTENDEE, GONE],
          attendees: [{ userId: ATTENDEE, displayName: "Katılımcı Bir" }, { userId: GONE, displayName: null }]
        }
      }),
      seriesUpdate: async (id, payload) => { sent = payload; return { ok: true }; },
      isConcurrencyConflict: () => false
    });
    global.location = { set href(value) { void value; }, get href() { return ""; } };
    loadScript("wwwroot/assets/js/Meetings/series/form.js");
    document.dispatchEvent(new Event("DOMContentLoaded"));
    await flush();
  };

  it("reads no people list; both pickers search", async () => {
    await boot();
    expect(calls).toEqual([]);
    ["fieldOrganizerUserId", "fieldAttendeeUserIds"].forEach((id) => {
      expect(select2Settings[id].minimumInputLength, id).toBe(2);
      expect(typeof select2Settings[id].ajax.transport, id).toBe("function");
    });
  });

  it("shows the stored organizer and attendees by name, an unnamed one as 'unknown user' — never the id", async () => {
    await boot();
    const organizer = document.getElementById("fieldOrganizerUserId");
    expect(organizer.value).toBe(ORGANIZER);
    expect(organizer.selectedOptions[0].textContent).toBe("Org Kişi");
    const texts = Array.from(document.getElementById("fieldAttendeeUserIds").selectedOptions).map((o) => o.textContent);
    expect(texts).toEqual(["Katılımcı Bir", "unknownUser"]);
    expect(texts.join(" ")).not.toContain(GONE);
  });

  it("an untouched save posts the stored organizer and BOTH attendees back unchanged", async () => {
    await boot();
    document.getElementById("fieldStartsAt").value = "2026-10-05 09:00";
    document.getElementById("fieldName").value = "Aylık";
    document.getElementById("meetingSeriesForm").dispatchEvent(new Event("submit", { cancelable: true }));
    await flush();
    expect(sent, "the series was not saved").toBeTruthy();
    expect(sent.organizerUserId).toBe(ORGANIZER);
    expect(sent.attendeeUserIds).toEqual([ATTENDEE, GONE]);
  });
});

// ── 4. minutes editor · 5. list · 6. report ───────────────────────────────────────────────────────────────

describe("BL-531 — screens that only NAME a meeting's own people never ask the directory", () => {
  it("minutes editor: no directory read; names come from the meeting", async () => {
    document.body.innerHTML = `
      <div id="minutesEditorRoot" data-meeting-id="m1" data-can-write="true" data-can-publish="true">
        <div id="minutesNotFound"></div><div id="minutesEditorBody"><span id="mMeetingTitle"></span><span id="mStatusBadge"></span>
        <button id="btnAddDecision"></button><div id="decisionsList"></div><p id="noDecisionsHint"></p>
        <div id="addedLaterTasksSection"><div id="addedLaterTasksList"></div></div>
        <ul id="attendanceList"></ul><p id="noAttendeesHint"></p><ul id="versionHistoryList"></ul></div>
      </div>`;
    global.MinutesEditorL10n = { t: (key) => key };
    global.DitenDialog = { dialogLook: () => ({}), dialogIcon: () => "" };
    global.showConfirm = () => {};
    stubApi({
      get: async () => ({ ok: true, data: { id: "m1", title: "T", organizerUserId: ORGANIZER, organizerDisplayName: "Org Kişi",
        attendees: [{ userId: ATTENDEE, displayName: "Katılımcı Bir" }, { userId: GONE, displayName: null }] } }),
      getMinutes: async () => ({ ok: true, data: { versions: [] } })
    });
    loadScript("wwwroot/assets/js/Meetings/minutes-editor.js");
    await flush();
    expect(calls).toEqual([]);
    const names = document.getElementById("attendanceList").textContent;
    expect(names).toContain("Katılımcı Bir");
    expect(names).toContain("unknownUser");
    expect(names).not.toContain(GONE);
  });

  it("list: the organizer name is the row's own; the page never asks for attendees", () => {
    const index = read("wwwroot", "assets", "js", "Meetings", "index.js");
    expect(index).not.toMatch(/lookupAttendees/);
    expect(index).toContain("full?.organizerDisplayName || organizerNamesById[data] || L.UnknownUser");
    expect(index).toMatch(/rows\.forEach\(\(r\) => \{ if \(r\.organizerDisplayName\)/);
  });

  it("report: organizer filter searches; each row shows the organizer's name or 'unknown user', never the id", async () => {
    document.body.innerHTML = `
      <div id="mrTiles"></div><select id="mrMeetingType"></select><select id="mrOrganizer"><option value=""></option></select>
      <input id="mrFrom" /><input id="mrTo" />
      <table><tbody id="mrMeetingsBody"></tbody></table><p id="mrMeetingsEmpty"></p><div id="mrMeetingsCard"></div>`;
    stubApi();
    loadScript("wwwroot/assets/js/Meetings/Report/index.js");
    const screen = global.MeetingReportScreen;
    expect(screen, "the report screen did not expose itself").toBeTruthy();
    expect(calls).toEqual([]);
    screen.renderMeetings([
      { title: "A", meetingTypeName: "T", startAt: "2026-10-05T09:00:00Z", organizerUserId: ORGANIZER, organizerDisplayName: "Org Kişi", attendeeCount: 1, respondedCount: 1, attendanceRatePercent: 100 },
      { title: "B", meetingTypeName: "T", startAt: "2026-10-05T09:00:00Z", organizerUserId: GONE, attendeeCount: 0, respondedCount: 0, attendanceRatePercent: null }
    ]);
    const body = document.getElementById("mrMeetingsBody").textContent;
    expect(body).toContain("Org Kişi");
    expect(body).toContain("unknownUser");
    expect(body).not.toContain(GONE);
  });

  it("report page loads MeetingsApi and the shared search before its own script", () => {
    const view = read("Views", "Meetings", "Report", "Index.cshtml");
    const at = (needle) => view.indexOf(needle);
    expect(at("Meetings/api.js")).toBeGreaterThan(-1);
    expect(at("shared/diten-people-search.js")).toBeGreaterThan(-1);
    expect(at("Meetings/api.js")).toBeLessThan(at("Meetings/Report/index.js"));
    expect(at("shared/diten-people-search.js")).toBeLessThan(at("Meetings/Report/index.js"));
  });

  it("every page whose picker searches loads the shared transport before its script", () => {
    [["Create.cshtml", "Meetings/form.js"], ["Details.cshtml", "Meetings/form.js"],
      ["Series/Create.cshtml", "Meetings/series/form.js"], ["Series/Edit.cshtml", "Meetings/series/form.js"]].forEach(([file, script]) => {
      const view = read("Views", "Meetings", ...file.split("/"));
      expect(view.indexOf("shared/diten-people-search.js"), file).toBeGreaterThan(-1);
      expect(view.indexOf("shared/diten-people-search.js"), file).toBeLessThan(view.lastIndexOf(`~/assets/js/${script}`));
    });
  });
});

// ── api.js ───────────────────────────────────────────────────────────────────────────────────────────────────

describe("BL-531 — MeetingsApi.lookupAttendees is search-only", () => {
  it("sends the typed term URL-encoded, hands out the plain array, and names the rate limit", async () => {
    const asked = [];
    global.fetch = async (url) => { asked.push(url); return { ok: true, status: 200, json: async () => ({ data: { people: [AYSE] } }) }; };
    delete global.MeetingsApi;
    loadScript("wwwroot/assets/js/Meetings/api.js");
    const res = await global.MeetingsApi.lookupAttendees({ search: "ş&a" });
    expect(asked).toEqual([`/Meetings/api/lookups/attendees?search=${encodeURIComponent("ş&a")}`]);
    expect(res.data).toEqual([AYSE]);
    expect(global.MeetingsApi.failureMessage({ ok: false, status: 429, reasonCode: "PEOPLE_SEARCH_RATE_LIMITED" })).toBe("errorPeopleSearchRateLimited");
    delete global.fetch;
  });
});

// ── strings ──────────────────────────────────────────────────────────────────────────────────────────────────

describe("BL-531 — the search sentences exist in all seven languages", () => {
  const LOCALES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
  const value = (xml, key) => {
    const match = new RegExp(`<data name="${key}"[^>]*>\\s*<value>([^<]*)</value>`).exec(xml);
    return match ? match[1].trim() : null;
  };
  const SETS = [
    ["Meetings/MeetingsIndex", "Views/Meetings/_IndexL10n.cshtml",
      ["PeopleSearchHint", "PeopleSearchMinimumLength", "PeopleSearchNoResults", "PeopleSearching", "ErrorPeopleSearchRateLimited"]],
    ["Meetings/Report/MeetingReportIndex", "Views/Meetings/Report/_IndexL10n.cshtml",
      ["PeopleSearchMinimumLength", "PeopleSearchNoResults", "PeopleSearching", "UnknownUser", "ErrorPeopleSearchRateLimited"]]
  ];

  it.each(SETS)("%s: every key in every language, none a copy of the English, all on the bridge", (resx, bridge, keys) => {
    const english = read("Resources", "Views", ...`${resx}.en.resx`.split("/"));
    LOCALES.forEach((locale) => {
      const xml = read("Resources", "Views", ...`${resx}.${locale}.resx`.split("/"));
      keys.forEach((key) => {
        expect(value(xml, key), `${key} is missing in ${locale}`).toBeTruthy();
        if (locale !== "en") { expect(value(xml, key), `${key} in ${locale} is the English sentence`).not.toBe(value(english, key)); }
      });
    });
    const payload = read(...bridge.split("/"));
    keys.forEach((key) => expect(payload, `${key} is not on ${bridge}`).toContain(`Localizer["${key}"]`));
  });
});
