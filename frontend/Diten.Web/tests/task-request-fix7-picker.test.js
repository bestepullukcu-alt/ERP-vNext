const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * WP-WCN-TASK-REQUEST-01 FIX7 — the remote picker's own mechanics, and what it says when the server does not answer:
 *  - item 1: a refused or unreadable answer is never an empty list (opening AND searching), and select2's texts
 *    (searching / noResults / errorLoading / inputTooShort) are the screen's resx sentences, in remote mode only;
 *  - item 2: the delay, the stale-answer guard (abort), the failure path, a short or emptied term, and a page without
 *    select2 (no "narrow your search" advice the reader could not follow);
 *  - item 3: every new key ×7 languages and in the _IndexL10n bridge.
 *
 * The harness is FIX6's (task-request-fix6-picker.test.js): a jQuery double records the configuration the shared
 * binder hands to select2, and the transport in it is driven the way select2 4.0.13's AjaxAdapter drives it — after
 * `ajax.delay`, with `abort()` on the previous request when a newer term starts.
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
const openPicker = async (answers, { select2 = true } = {}) => {
  detailsDom();
  global.jQuery = undefined;   // the page boots without select2's jQuery double (it uses jQuery for other widgets)
  const asked = [];
  const confirms = [];
  const errors = [];
  global.MeetingsL10n = { t: (key) => `m:${key}` };
  global.DitenPersonPicker = { renderPersonOptions: () => {}, buildSearchableDropdownAdapter: () => ({}) };
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
  if (select2) { installJQuery(); }   // select2 on the page from here on: the picker can search the server
  else { configs = []; }
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

const deferred = () => {
  let resolve;
  let reject;
  const promise = new Promise((res, rej) => { resolve = res; reject = rej; });
  return { promise, resolve, reject };
};

/** Drives the transport like select2 does; returns the handle plus what reached success / failure. */
const drive = (config, term) => {
  const seen = { success: [], failure: [] };
  const handle = config.ajax.transport({ data: { term } }, (d) => seen.success.push(d), (e) => seen.failure.push(e));
  return { handle, seen };
};

const opening = { ok: true, data: { items: [{ id: "t1", title: "İlk liste işi" }], truncated: false } };

describe("item 1 — a refused answer is never an empty list", () => {
  it("a refused opening call (403) says so in a sentence and opens no picker", async () => {
    const { options, errors } = await openPicker({ __open: { ok: false, status: 403, reasonCode: "FORBIDDEN" } });
    expect(options, "a refused list still opened the picker").toBeUndefined();
    expect(errors.map((e) => e.message)).toContain("m:linkExistingTaskListFailed");
  });

  it("a server error on the opening call (500) is the same sentence", async () => {
    const { options, errors } = await openPicker({ __open: { ok: false, status: 500 } });
    expect(options).toBeUndefined();
    expect(errors.map((e) => e.message)).toContain("m:linkExistingTaskListFailed");
  });

  it("a refused SEARCH reaches select2's failure, and errorLoading says the screen's sentence", async () => {
    const { bound } = await openPicker({ __open: opening, "bütçe": { ok: false, status: 500 } });
    const { seen } = drive(bound, "bütçe");
    await settle(); await settle();
    expect(seen.success, "a refused search answered as an (empty) result list").toEqual([]);
    expect(seen.failure).toHaveLength(1);
    expect(bound.language.errorLoading()).toBe("m:linkExistingTaskListFailed");
  });

  it("an unreadable SEARCH answer says it could not be read", async () => {
    const { bound } = await openPicker({ __open: opening, "bütçe": { ok: true, data: { unexpected: true } } });
    const { seen } = drive(bound, "bütçe");
    await settle(); await settle();
    expect(seen.success).toEqual([]);
    expect(seen.failure).toHaveLength(1);
    expect(bound.language.errorLoading()).toBe("m:linkExistingTaskListUnreadable");
  });

  it("speaks the screen's language for select2's own texts", async () => {
    const { bound } = await openPicker({ __open: opening });
    expect(bound.language.searching()).toBe("m:linkExistingTaskSearching");
    expect(bound.language.noResults()).toBe("m:linkExistingTaskNoResults");
    expect(bound.language.inputTooShort()).toBe("m:linkExistingTaskInputTooShort");
    expect(bound.language.errorLoading()).toBe("m:linkExistingTaskListFailed");
  });

  it("a successful search after a failed one no longer says the failure", async () => {
    const { bound } = await openPicker({
      __open: opening,
      "kırık": { ok: false, status: 500 },
      "sağlam": { ok: true, data: { items: [{ id: "t5", title: "Sağlam iş" }], truncated: false } }
    });
    drive(bound, "kırık"); await settle(); await settle();
    expect(bound.language.errorLoading()).toBe("m:linkExistingTaskListFailed");
    const { seen } = drive(bound, "sağlam"); await settle(); await settle();
    expect(seen.success[0].results).toEqual([{ id: "t5", text: "Sağlam iş" }]);
  });
});

describe("item 2 — the remote mechanics", () => {
  it("waits 250 ms after the last keystroke before asking (select2 holds the timer)", async () => {
    const { bound } = await openPicker({ __open: opening });
    expect(bound.ajax.delay).toBe(250);
  });

  it("a slow answer for 'ab' never overwrites the newer 'abc'", async () => {
    const slow = deferred();
    const { bound } = await openPicker({
      __open: opening,
      ab: slow.promise,
      abc: { ok: true, data: { items: [{ id: "t3", title: "ABC işi" }], truncated: false } }
    });
    const older = drive(bound, "ab");
    await settle();
    older.handle.abort();                 // select2 aborts the previous request when a newer term starts
    const newer = drive(bound, "abc");
    await settle(); await settle();
    slow.resolve({ ok: true, data: { items: [{ id: "t2", title: "AB işi" }], truncated: false } });
    await settle(); await settle();
    expect(newer.seen.success[0].results).toEqual([{ id: "t3", text: "ABC işi" }]);
    expect(older.seen.success, "the stale 'ab' answer reached select2").toEqual([]);
    expect(older.seen.failure).toEqual([]);
  });

  it("a stale FAILURE is not reported either", async () => {
    const slow = deferred();
    const { bound } = await openPicker({ __open: opening, ab: slow.promise });
    const older = drive(bound, "ab");
    await settle();
    older.handle.abort();
    slow.resolve({ ok: false, status: 500 });
    await settle(); await settle();
    expect(older.seen.failure).toEqual([]);
  });

  it("an empty term (the box just opened) shows the opening list without asking the server", async () => {
    const { asked, bound } = await openPicker({ __open: opening });
    const { seen } = drive(bound, "");
    await settle();
    expect(asked.filter((a) => a.term !== null)).toEqual([]);
    expect(seen.success[0].results).toEqual([{ id: "t1", text: "İlk liste işi" }]);
  });

  it("erasing below two characters goes back to the opening list, without asking", async () => {
    const { asked, bound } = await openPicker({
      __open: opening,
      abc: { ok: true, data: { items: [{ id: "t3", title: "ABC işi" }], truncated: false } }
    });
    drive(bound, "abc"); await settle(); await settle();
    const { seen } = drive(bound, "a"); await settle();
    expect(asked.filter((a) => a.term !== null)).toEqual([{ term: "abc", limit: 20 }]);
    expect(seen.success[0].results).toEqual([{ id: "t1", text: "İlk liste işi" }]);
  });

  it("without select2 on the page the 'narrow your search' advice is not given", async () => {
    const { options, bound } = await openPicker(
      { __open: { ok: true, data: { items: [{ id: "t1", title: "İş" }], truncated: true } } }, { select2: false });
    expect(options, "the picker did not open").toBeTruthy();
    expect(bound, "nothing could have been bound without select2").toBeNull();
    expect(options.subtext).toBe("");
  });
});

describe("item 4 — the shared binder's remote mode", () => {
  const bindRemote = (remoteSearch, extra = {}) => {
    installJQuery();
    delete global.DitenDialog;
    loadScript("wwwroot/assets/js/shared/diten-dialog.js");
    const popup = document.createElement("div");
    popup.className = "swal2-popup";
    const select = document.createElement("select");
    popup.appendChild(select);
    document.body.appendChild(popup);
    global.DitenDialog.bindDialogSelect2(select, popup, { remoteSearch, ...extra });
    return configs.find((entry) => entry.element === select).config;
  };

  it("a search that THROWS reaches failure, never hangs on 'Searching…'", async () => {
    const config = bindRemote({ search: () => { throw new Error("boom"); } });
    const { seen } = drive(config, "abc");
    await settle(); await settle();
    expect(seen.failure).toHaveLength(1);
    expect(seen.success).toEqual([]);
  });

  it("keeps the caller's minimumResultsForSearch, and shows the box from the first option when none is named", () => {
    expect(bindRemote({ search: async () => [] }).minimumResultsForSearch).toBe(0);
    expect(bindRemote({ search: async () => [] }, { minimumResultsForSearch: 5 }).minimumResultsForSearch).toBe(5);
  });

  it("without remoteSearch the binder hands select2 no language and no ajax (every other caller)", () => {
    installJQuery();
    delete global.DitenDialog;
    loadScript("wwwroot/assets/js/shared/diten-dialog.js");
    const select = document.createElement("select");
    document.body.appendChild(select);
    global.DitenDialog.bindDialogSelect2(select, null, {});
    const config = configs.find((entry) => entry.element === select).config;
    expect(config.ajax).toBeUndefined();
    expect(config.language).toBeUndefined();
    expect(config.minimumResultsForSearch).toBe(10);
  });
});

describe("item 3 — every picker sentence is translated ×7 and carried through the bridge", () => {
  const LANGUAGES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
  const KEYS = [
    "LinkExistingTaskListUnreadable",
    "LinkExistingTaskListFailed",
    "LinkExistingTaskSearching",
    "LinkExistingTaskNoResults",
    "LinkExistingTaskInputTooShort"
  ];
  const web = (...parts) => path.join(__dirname, "..", ...parts);
  const resx = (language) => fs.readFileSync(web("Resources", "Views", "Meetings", `MeetingsIndex.${language}.resx`), "utf8");
  const bridge = fs.readFileSync(web("Views", "Meetings", "_IndexL10n.cshtml"), "utf8");

  for (const key of KEYS) {
    it(`${key}`, () => {
      const value = (language) => (resx(language).match(new RegExp(`<data name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`)) || [])[1];
      expect(bridge).toContain(`${key} = Localizer["${key}"]`);
      for (const language of LANGUAGES) {
        expect(value(language), `${key} missing in ${language}`).toBeTruthy();
        expect(value(language).trim(), `${key} empty in ${language}`).not.toBe("");
        if (language !== "en") expect(value(language), `${key} in ${language} is the English text`).not.toBe(value("en"));
      }
    });
  }
});
