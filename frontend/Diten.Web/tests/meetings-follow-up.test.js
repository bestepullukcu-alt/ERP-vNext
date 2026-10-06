const { loadScript } = require("./load-script");

/*
 * MOD-0357 S7 (K6) — the frontend half of continuation scheduling: the follow-up dialog's own submit/validation
 * flow, and Details.cshtml's rendering of the carried-forward badge, the two cross-link rows, and the
 * "Devam toplantısı planla" button's ALWAYS-visible posture (not gated by `editable`, unlike every other
 * Details-page control — see form.js's own comment on why).
 */

const SOURCE_ID = "11111111-1111-1111-1111-111111111111";
const TYPE_ID = "22222222-2222-2222-2222-222222222222";
const NEW_MEETING_ID = "33333333-3333-3333-3333-333333333333";
const PREV_MEETING_ID = "44444444-4444-4444-4444-444444444444";
const NEXT_MEETING_ID = "55555555-5555-5555-5555-555555555555";

const flushMicrotasks = () => new Promise((resolve) => setTimeout(resolve, 0));

describe("MOD-0357 S7: the follow-up dialog", () => {
  let capturedConfig;
  let capturedPayload;
  let successCalls;
  let validationMessages;

  const stubDialogGlobals = ({ carriedAgendaItemCount }) => {
    capturedPayload = null;
    successCalls = [];
    validationMessages = [];

    global.MeetingsL10n = { t: (key) => key };
    global.DitenDialog = { dialogLook: () => ({}), dialogIcon: () => "", bindDialogSelect2: () => {} };
    global.DitenModal = { success: (opts) => successCalls.push(opts), error: () => {} };
    global.MeetingsApi = {
      lookupTypes: async () => ({ ok: true, data: [{ id: TYPE_ID, name: "Yönetim Gözden Geçirmesi" }] }),
      scheduleFollowUp: async (id, payload) => {
        capturedPayload = { id, payload };
        return { ok: true, data: { meetingId: NEW_MEETING_ID, carriedAgendaItemCount } };
      },
      failureMessage: () => "error"
    };
    global.Swal = {
      showValidationMessage: (msg) => validationMessages.push(msg),
      fire: (config) => {
        capturedConfig = config;
        // A real SweetAlert renders `html` into the DOM before calling didOpen/preConfirm; this mock does the
        // same minimal thing so the dialog's own document.getElementById calls resolve to real elements.
        const container = document.createElement("div");
        container.innerHTML = config.html;
        document.body.appendChild(container);
        return {
          then: (onFulfilled) => {
            // Mirrors what a real confirm click does: run didOpen, then preConfirm, then hand the resolved
            // value to the caller's own .then — never a second, looser re-implementation of SweetAlert's flow.
            return Promise.resolve()
              .then(() => config.didOpen?.(container))
              .then(() => {
                const value = config.preConfirm();
                return onFulfilled(value === false ? { isConfirmed: false } : { isConfirmed: true, value });
              });
          }
        };
      }
    };
  };

  beforeEach(() => {
    delete global.MeetingsFollowUpDialog;
  });

  it("shows a validation message and calls the API with nothing when a required field is missing", async () => {
    stubDialogGlobals({ carriedAgendaItemCount: 0 });
    loadScript("wwwroot/assets/js/Meetings/follow-up-dialog.js");

    global.MeetingsFollowUpDialog.open({
      meeting: { id: SOURCE_ID, title: "Kaynak Toplantı", meetingTypeId: TYPE_ID },
      t: (key) => key
    });
    await flushMicrotasks();
    await flushMicrotasks();
    // Start/End left blank on purpose.

    expect(validationMessages).toContain("formValidationError");
    expect(capturedPayload).toBeNull();
  });

  it("submits the expected payload and reports the carried count in its success message", async () => {
    stubDialogGlobals({ carriedAgendaItemCount: 2 });
    loadScript("wwwroot/assets/js/Meetings/follow-up-dialog.js");

    global.MeetingsFollowUpDialog.open({
      meeting: { id: SOURCE_ID, title: "Kaynak Toplantı", meetingTypeId: TYPE_ID },
      t: (key) => key
    });
    // The mocked Swal.fire defers didOpen/preConfirm to a microtask (mirroring the real SweetAlert flow) — the
    // fields are filled HERE, synchronously, before that chain has a chance to run.
    document.getElementById("mtgFollowUpStartAt").value = "2026-10-01 09:00";
    document.getElementById("mtgFollowUpEndAt").value = "2026-10-01 10:00";
    await flushMicrotasks();
    await flushMicrotasks();

    expect(capturedPayload.id).toBe(SOURCE_ID);
    expect(capturedPayload.payload.meetingTypeId).toBe(TYPE_ID);
    expect(capturedPayload.payload.startAt).toBe(new Date("2026-10-01T09:00").toISOString());
    expect(capturedPayload.payload.endAt).toBe(new Date("2026-10-01T10:00").toISOString());
    expect(typeof capturedPayload.payload.idempotencyKey).toBe("string");
    expect(successCalls).toHaveLength(1);
    expect(successCalls[0].title).toBe("toastFollowUpScheduled");
  });

  it("reports the NO-open-work message when nothing was carried (AC5)", async () => {
    stubDialogGlobals({ carriedAgendaItemCount: 0 });
    loadScript("wwwroot/assets/js/Meetings/follow-up-dialog.js");

    global.MeetingsFollowUpDialog.open({
      meeting: { id: SOURCE_ID, title: "Kaynak Toplantı", meetingTypeId: TYPE_ID },
      t: (key) => key
    });
    document.getElementById("mtgFollowUpStartAt").value = "2026-10-01 09:00";
    document.getElementById("mtgFollowUpEndAt").value = "2026-10-01 10:00";
    await flushMicrotasks();
    await flushMicrotasks();

    expect(successCalls[0].title).toBe("toastFollowUpScheduledNoOpenWork");
  });

  it("rejects an end time at or before the start time client-side", async () => {
    stubDialogGlobals({ carriedAgendaItemCount: 0 });
    loadScript("wwwroot/assets/js/Meetings/follow-up-dialog.js");

    global.MeetingsFollowUpDialog.open({
      meeting: { id: SOURCE_ID, title: "Kaynak Toplantı", meetingTypeId: TYPE_ID },
      t: (key) => key
    });
    document.getElementById("mtgFollowUpStartAt").value = "2026-10-01 10:00";
    document.getElementById("mtgFollowUpEndAt").value = "2026-10-01 09:00";
    await flushMicrotasks();
    await flushMicrotasks();

    expect(validationMessages).toContain("errorEndBeforeStart");
    expect(capturedPayload).toBeNull();
  });
});

describe("MOD-0357 S7: Details page — carried badge, cross-links, always-open follow-up button", () => {
  const buildDom = () => {
    document.body.innerHTML = `
      <div class="meetings-details" id="meetingDetailsRoot" data-meeting-id="${SOURCE_ID}">
        <div id="detailsActionBar">
          <button id="btnScheduleFollowUp" class="d-none"></button>
          <a id="btnOpenMinutes" class="d-none"></a>
          <button id="btnReassignOrganizer" class="d-none"></button>
          <button id="btnCancelMeeting" class="d-none"></button>
          <a id="btnEditMeeting" class="d-none"></a>
        </div>
        <div id="meetingNotFound" class="d-none"></div>
        <div id="meetingDetailsBody" class="d-none">
          <div id="dTitle"></div><div id="dType"></div><div id="dStatus"></div>
          <div id="dStartAt"></div><div id="dEndAt"></div><div id="dLocation"></div>
          <div id="dOrganizer"></div><div id="dDescription"></div>
          <div id="dCancellationReasonRow" class="d-none"><div id="dCancellationReason"></div></div>
          <div id="dFollowUpOfRow" class="d-none"><a id="dFollowUpOfLink"></a></div>
          <div id="dFollowedByRow" class="d-none"><a id="dFollowedByLink"></a></div>
          <ul id="agendaList"></ul>
          <p id="noAgendaHint" class="d-none"></p>
          <div id="agendaAddRow" class="d-none">
            <input id="newAgendaItemText" />
            <button id="btnAddAgendaItem"></button>
          </div>
          <div id="taskAddRow" class="d-none">
            <button id="btnCreateTaskFromMeeting"></button>
            <button id="btnLinkExistingTask"></button>
          </div>
          <div id="linkedTasksList"></div>
          <p id="noLinkedTasksHint" class="d-none"></p>
          <ul id="attendeesList"></ul>
          <div id="attendeeAddRow" class="d-none">
            <select id="newAttendeeUserId"></select>
            <button id="btnAddAttendee"></button>
          </div>
        </div>
      </div>`;
  };

  const stubDetailsGlobals = (meeting) => {
    global.MeetingsL10n = { t: (key) => key };
    global.DitenPersonPicker = { renderPersonOptions: () => {}, buildSearchableDropdownAdapter: () => ({}) };
    global.DitenRelatedRecords = { renderRelatedRows: () => "" };
    global.DitenModal = { success: () => {}, error: () => {} };
    global.jQuery = undefined;
    global.MeetingsApi = {
      lookupAttendees: async () => ({ ok: true, data: { people: [] } }),
      get: async () => ({ ok: true, data: meeting }),
      linkedTasks: async () => ({ ok: true, data: [] }),
      failureMessage: () => "error"
    };
  };

  beforeEach(() => {
    delete global.MeetingsFollowUpDialog;
    delete global.MeetingsApi;
    delete global.MeetingsL10n;
  });

  const load = async () => {
    buildDom();
    loadScript("wwwroot/assets/js/Meetings/form.js");
    document.dispatchEvent(new Event("DOMContentLoaded"));
    await flushMicrotasks();
    await flushMicrotasks();
    await flushMicrotasks();
  };

  it("marks a carried-forward agenda line with the badge, and leaves a plain line unmarked", async () => {
    stubDetailsGlobals({
      id: SOURCE_ID, title: "Toplantı", meetingTypeId: TYPE_ID, organizerUserId: "u1",
      lifecycle: 0, version: 1,
      agendaItems: [
        { id: "a1", text: "Taşınan görev", sortOrder: 0, recordLinkId: "l1", carriedFromMeetingId: PREV_MEETING_ID },
        { id: "a2", text: "Elle yazılan", sortOrder: 1, recordLinkId: null, carriedFromMeetingId: null }
      ],
      attendees: []
    });
    await load();

    const items = document.querySelectorAll("#agendaList li");
    expect(items[0].innerHTML).toContain("carriedFromPreviousMeetingBadge");
    expect(items[1].innerHTML).not.toContain("carriedFromPreviousMeetingBadge");
  });

  it("shows both cross-link rows when both are present, and hides each independently otherwise", async () => {
    stubDetailsGlobals({
      id: SOURCE_ID, title: "Toplantı", meetingTypeId: TYPE_ID, organizerUserId: "u1",
      lifecycle: 2, version: 1, agendaItems: [], attendees: [],
      followUpOfMeetingId: PREV_MEETING_ID, followUpOfMeetingTitle: "Önceki",
      followedByMeetingId: NEXT_MEETING_ID, followedByMeetingTitle: "Sonraki"
    });
    await load();

    expect(document.getElementById("dFollowUpOfRow").classList.contains("d-none")).toBe(false);
    expect(document.getElementById("dFollowUpOfLink").textContent).toBe("Önceki");
    expect(document.getElementById("dFollowUpOfLink").getAttribute("href")).toBe(`/Meetings/${PREV_MEETING_ID}`);
    expect(document.getElementById("dFollowedByRow").classList.contains("d-none")).toBe(false);
    expect(document.getElementById("dFollowedByLink").textContent).toBe("Sonraki");
  });

  it("hides both cross-link rows when the meeting has neither", async () => {
    stubDetailsGlobals({
      id: SOURCE_ID, title: "Toplantı", meetingTypeId: TYPE_ID, organizerUserId: "u1",
      lifecycle: 0, version: 1, agendaItems: [], attendees: []
    });
    await load();

    expect(document.getElementById("dFollowUpOfRow").classList.contains("d-none")).toBe(true);
    expect(document.getElementById("dFollowedByRow").classList.contains("d-none")).toBe(true);
  });

  it("keeps 'Devam toplantısı planla' visible on a CANCELLED meeting, unlike the editing controls", async () => {
    stubDetailsGlobals({
      id: SOURCE_ID, title: "Toplantı", meetingTypeId: TYPE_ID, organizerUserId: "u1",
      lifecycle: 1, version: 1, agendaItems: [], attendees: [], cancellationReason: "iptal"
    });
    await load();

    expect(document.getElementById("btnScheduleFollowUp").classList.contains("d-none")).toBe(false);
    // The editing-only controls stay withdrawn, per AC5's existing posture — proves this test seeded a
    // genuinely non-editable meeting, not one that happens to leave everything visible.
    expect(document.getElementById("agendaAddRow").classList.contains("d-none")).toBe(true);
    expect(document.getElementById("btnCancelMeeting").classList.contains("d-none")).toBe(true);
  });

  it("keeps 'Devam toplantısı planla' visible on a COMPLETED meeting", async () => {
    stubDetailsGlobals({
      id: SOURCE_ID, title: "Toplantı", meetingTypeId: TYPE_ID, organizerUserId: "u1",
      lifecycle: 2, version: 1, agendaItems: [], attendees: []
    });
    await load();

    expect(document.getElementById("btnScheduleFollowUp").classList.contains("d-none")).toBe(false);
  });

  it("opens the follow-up dialog with the current meeting when clicked", async () => {
    stubDetailsGlobals({
      id: SOURCE_ID, title: "Toplantı", meetingTypeId: TYPE_ID, organizerUserId: "u1",
      lifecycle: 0, version: 1, agendaItems: [], attendees: []
    });
    let openedWith = null;
    global.MeetingsFollowUpDialog = { open: (opts) => { openedWith = opts; } };
    await load();

    document.getElementById("btnScheduleFollowUp").click();

    expect(openedWith).not.toBeNull();
    expect(openedWith.meeting.id).toBe(SOURCE_ID);
  });
});
