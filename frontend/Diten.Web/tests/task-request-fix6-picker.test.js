const { loadScript } = require("./load-script");

/*
 * WP-WCN-TASK-REQUEST-01 FIX6 — the "link an existing task" picker searches the SERVER with what the reader types
 * (items 1 and 5), through an OPTIONAL remote mode of the shared dialog binder that every other caller leaves off.
 *
 * select2 itself is not loaded in this suite; a jQuery double records the configuration the shared binder hands to
 * select2, and the remote transport in it is driven directly — the same configuration select2 would drive.
 *
 * Content, not counts: every test loads form.js again, and jsdom keeps the earlier instances' document listeners, so
 * one click reaches every instance loaded so far (the same note as workcenter-next-detail-page.test.js).
 */

const MEETING_ID = "11111111-1111-1111-1111-111111111111";
const settle = () => new Promise((resolve) => setTimeout(resolve, 0));
let configs;

const installJQuery = () => {
  configs = [];
  const jq = (element) => ({
    hasClass: () => false,
    data: () => "",
    select2: (config) => { configs.push({ element, config }); }
  });
  jq.fn = { select2: true };
  global.jQuery = jq;
};

const detailsDom = () => {
  document.body.innerHTML = `
    <div class="meetings-details" id="meetingDetailsRoot" data-meeting-id="${MEETING_ID}">
      <div id="detailsActionBar"><button id="btnScheduleFollowUp" class="d-none"></button><a id="btnOpenMinutes" class="d-none"></a>
        <button id="btnReassignOrganizer" class="d-none"></button><button id="btnCancelMeeting" class="d-none"></button><a id="btnEditMeeting" class="d-none"></a></div>
      <div id="meetingNotFound" class="d-none"></div>
      <div id="meetingDetailsBody" class="d-none">
        <div id="dTitle"></div><div id="dType"></div><div id="dStatus"></div><div id="dStartAt"></div><div id="dEndAt"></div>
        <div id="dLocation"></div><div id="dOrganizer"></div><div id="dDescription"></div>
        <div id="dCancellationReasonRow" class="d-none"><div id="dCancellationReason"></div></div>
        <div id="dFollowUpOfRow" class="d-none"><a id="dFollowUpOfLink"></a></div><div id="dFollowedByRow" class="d-none"><a id="dFollowedByLink"></a></div>
        <ul id="agendaList"></ul><p id="noAgendaHint" class="d-none"></p>
        <div id="agendaAddRow" class="d-none"><input id="newAgendaItemText" /><button id="btnAddAgendaItem"></button></div>
        <div id="taskAddRow"><button id="btnCreateTaskFromMeeting"></button><button id="btnLinkExistingTask"></button></div>
        <div id="linkedTasksList"></div><p id="noLinkedTasksHint" class="d-none"></p>
        <ul id="attendeesList"></ul><div id="attendeeAddRow" class="d-none"><select id="newAttendeeUserId"></select><button id="btnAddAttendee"></button></div>
      </div>
    </div>`;
};

/** Opens the picker; `answers` is what linkCandidates returns, by term (null = the opening list). */
const openPicker = async (answers) => {
  detailsDom();
  global.jQuery = undefined;   // the page boots without select2's jQuery double (it uses jQuery for other widgets)
  const asked = [];
  const confirms = [];
  const errors = [];
  global.MeetingsL10n = { t: (key) => `m:${key}` };
  global.DitenPersonPicker = { renderPersonOptions: () => {}, buildSearchableDropdownAdapter: () => ({}) };
  // Merge WP-CT-LANE-TKO-MERGE-01 — the lane's Meetings/form.js builds its people pickers with DitenPeopleSearch (BL-531).
  // TKO review, small item 4 — the REAL shared module, not a stand-in: a change to its contract shows here.
  loadScript("wwwroot/assets/js/shared/diten-people-search.js");
  global.DitenModal = { success: () => {}, error: (opts) => errors.push(opts) };
  global.showConfirm = (title, onConfirm, options) => { confirms.push(options); };
  global.Swal = undefined;
  global.MeetingsApi = {
    lookupAttendees: async () => ({ ok: true, data: { people: [] } }),
    get: async () => ({ ok: true, data: { id: MEETING_ID, title: "T", meetingTypeId: "t", organizerUserId: "u1", lifecycle: 0, version: 1, attendees: [], agendaItems: [] } }),
    linkedTasks: async () => ({ ok: true, data: [] }),
    failureMessage: () => "error"
  };
  global.TasksApi = {
    linkCandidates: async (term, limit) => { asked.push({ term, limit }); return answers[term ?? "__open"]; }
  };
  delete global.DitenDialog;
  loadScript("wwwroot/assets/js/shared/diten-dialog.js");
  delete global.DitenRelatedRecords;
  loadScript("wwwroot/assets/js/shared/diten-related-records.js");
  loadScript("wwwroot/assets/js/Meetings/form.js");
  document.dispatchEvent(new Event("DOMContentLoaded"));
  for (let i = 0; i < 4; i++) { await settle(); }
  installJQuery();             // select2 on the page from here on: the picker can search the server
  document.getElementById("btnLinkExistingTask").click();
  for (let i = 0; i < 4; i++) { await settle(); }

  const options = confirms[confirms.length - 1];
  let bound = null;
  if (options) {
    // What SweetAlert does before didOpen: the <select> built from inputOptions, inside the popup.
    const popup = document.createElement("div");
    popup.className = "swal2-popup";
    const select = document.createElement("select");
    select.className = "swal2-select";
    Object.entries(options.inputOptions).forEach(([id, text]) => select.appendChild(new Option(text, id)));
    popup.appendChild(select);
    document.body.appendChild(popup);
    options.didOpen(popup);
    bound = configs.find((entry) => entry.element === select)?.config ?? null;
  }
  return { asked, options, errors, bound };
};

const search = (config, term) => new Promise((resolve, reject) => {
  config.ajax.transport({ data: { term } }, resolve, reject);
});

describe("the picker sends the typed term to the server", () => {
  const opening = { ok: true, data: { items: [{ id: "t1", title: "İlk liste işi" }], truncated: true } };

  it("binds the select in remote mode and opens on the server's first list", async () => {
    const { asked, options, bound } = await openPicker({ __open: opening });
    expect(asked.length).toBeGreaterThan(0);
    expect(asked.every((a) => a.term === null && a.limit === 20), "the opening list asked with a term").toBe(true);
    expect(options.inputOptions.t1).toBe("İlk liste işi");
    expect(bound, "the select was not bound").toBeTruthy();
    expect(typeof bound.ajax?.transport).toBe("function");
    // select2 is on the page, so the term CAN be sent: the truncated sentence is honest advice.
    expect(options.subtext).toBe("m:linkExistingTaskTruncated");
  });

  it("asks the server with a term of two or more characters and shows what it answers", async () => {
    const answers = {
      __open: opening,
      "bütçe": { ok: true, data: { items: [{ id: "t9", title: "Bütçe <b>kalemi</b>" }], truncated: false } }
    };
    const { asked, bound } = await openPicker(answers);

    const page = await search(bound, "bütçe");

    expect(asked.filter((a) => a.term !== null)).toEqual([{ term: "bütçe", limit: 20 }]);
    expect(page.results).toEqual([{ id: "t9", text: "Bütçe <b>kalemi</b>" }]);
    // The label stays TEXT: no templateResult / escapeMarkup override, so select2's own escaping applies.
    expect(bound.templateResult).toBeUndefined();
    expect(bound.escapeMarkup).toBeUndefined();
  });

  it("keeps the opening list for a one-letter term, without asking the server", async () => {
    const { asked, bound } = await openPicker({ __open: opening });
    const page = await search(bound, "b");
    expect(asked.filter((a) => a.term !== null), "a one-letter term went to the server").toEqual([]);
    expect(page.results).toEqual([{ id: "t1", text: "İlk liste işi" }]);
  });

  it("says a searched answer was cut short, as a line nobody can choose", async () => {
    const answers = { __open: opening, "iş": { ok: true, data: { items: [{ id: "t2", title: "Bir iş" }], truncated: true } } };
    const { bound } = await openPicker(answers);
    const page = await search(bound, "iş");
    expect(page.results).toEqual([
      { id: "t2", text: "Bir iş" },
      { id: "__truncated", text: "m:linkExistingTaskTruncated", disabled: true }
    ]);
  });
});

describe("the picker reads an older server's answer, and says when it cannot", () => {
  it("uses a plain array (a server from before FIX5) as the list", async () => {
    const { options, errors } = await openPicker({ __open: { ok: true, data: [{ id: "old1", title: "Eski sunucu işi" }] } });
    expect(errors).toHaveLength(0);
    expect(options.inputOptions.old1).toBe("Eski sunucu işi");
    expect(options.subtext).toBe("");
  });

  it("refuses to show an answer it cannot read as an empty list", async () => {
    const { options, errors } = await openPicker({ __open: { ok: true, data: { unexpected: true } } });
    expect(options, "an unreadable answer still opened the picker").toBeUndefined();
    expect(errors.length).toBeGreaterThan(0);
    expect(errors.every((e) => e.message === "m:linkExistingTaskListUnreadable")).toBe(true);
  });
});

describe("every other caller of the shared binder stays static", () => {
  it("the follow-up dialog's type select gets no remote mode", async () => {
    installJQuery();
    delete global.DitenDialog;
    loadScript("wwwroot/assets/js/shared/diten-dialog.js");
    global.DitenDialogAppearance = () => ({});
    global.DitenDialogAppearance.iconHtml = () => "";
    global.MeetingsApi = { lookupTypes: async () => ({ ok: true, data: [{ id: "ty1", name: "Tür" }] }) };
    let opened = null;
    global.Swal = { fire: (config) => { opened = config; return new Promise(() => {}); }, showValidationMessage: () => {} };
    delete global.MeetingsFollowUpDialog;
    loadScript("wwwroot/assets/js/Meetings/follow-up-dialog.js");
    global.MeetingsFollowUpDialog.open({ meeting: { id: MEETING_ID, title: "T", meetingTypeId: "ty1" }, t: (k) => k });
    const container = document.createElement("div");
    container.innerHTML = opened.html;
    document.body.appendChild(container);
    await opened.didOpen(container);

    const bound = configs.find((entry) => entry.element && entry.element.id === "mtgFollowUpType");
    expect(bound, "the follow-up dialog did not bind its select").toBeTruthy();
    expect(bound.config.ajax).toBeUndefined();
    expect(bound.config.minimumResultsForSearch).toBe(10);
  });
});
