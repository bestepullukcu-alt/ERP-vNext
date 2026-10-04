const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");
const { bootSurface, app } = require("./wcn-boot");

/*
 * WP-WCN-TASK-REQUEST-01 FIX4 (items 1 + 2) — a task this reader may not read never shows as the code
 * TASK_NOT_ACCESSIBLE, and a carried-forward agenda line never shows as the code MEETING_CARRIED_OPEN_ACTION: both
 * are sentences in the reader's own language, on the Meetings details page and on the Task Center detail.
 * The carried line names its task only through the linked-task list the server answers per reader.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const WEB = path.join(repoRoot, "frontend", "Diten.Web");
const LANGUAGES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const flushMicrotasks = () => new Promise((resolve) => setTimeout(resolve, 0));

const resxValue = (file, key) => {
  const match = fs.readFileSync(file, "utf8").match(new RegExp(`<data name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`));
  return match ? match[1].trim() : null;
};

describe("Meetings details: a carried action and an unreadable task are sentences, not codes", () => {
  const MEETING_ID = "11111111-1111-1111-1111-111111111111";

  const buildDom = () => {
    document.body.innerHTML = `
      <div class="meetings-details" id="meetingDetailsRoot" data-meeting-id="${MEETING_ID}">
        <div id="detailsActionBar">
          <button id="btnScheduleFollowUp" class="d-none"></button><a id="btnOpenMinutes" class="d-none"></a>
          <button id="btnReassignOrganizer" class="d-none"></button><button id="btnCancelMeeting" class="d-none"></button>
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
          <ul id="agendaList"></ul><p id="noAgendaHint" class="d-none"></p>
          <div id="agendaAddRow" class="d-none"><input id="newAgendaItemText" /><button id="btnAddAgendaItem"></button></div>
          <div id="taskAddRow" class="d-none"><button id="btnCreateTaskFromMeeting"></button><button id="btnLinkExistingTask"></button></div>
          <div id="linkedTasksList"></div><p id="noLinkedTasksHint" class="d-none"></p>
          <ul id="attendeesList"></ul>
          <div id="attendeeAddRow" class="d-none"><select id="newAttendeeUserId"></select><button id="btnAddAttendee"></button></div>
        </div>
      </div>`;
  };

  beforeEach(() => {
    delete global.MeetingsApi;
    delete global.MeetingsL10n;
    delete global.DitenRelatedRecords;
  });

  it("names the carried line's task from the reader's linked-task list, and words an unreadable one", async () => {
    buildDom();
    global.MeetingsL10n = { t: (key) => key };
    global.DitenPersonPicker = { renderPersonOptions: () => {}, buildSearchableDropdownAdapter: () => ({}) };
    global.DitenModal = { success: () => {}, error: () => {} };
    global.jQuery = undefined;
    global.MeetingsApi = {
      lookupAttendees: async () => ({ ok: true, data: { people: [] } }),
      get: async () => ({
        ok: true,
        data: {
          id: MEETING_ID, title: "Toplantı", meetingTypeId: "t", organizerUserId: "u1", lifecycle: 0, version: 1,
          attendees: [],
          agendaItems: [
            { id: "a1", text: "MEETING_CARRIED_OPEN_ACTION", sortOrder: 0, recordLinkId: "l1", carriedFromMeetingId: "p" },
            { id: "a2", text: "MEETING_CARRIED_OPEN_ACTION", sortOrder: 1, recordLinkId: "l2", carriedFromMeetingId: "p" },
            { id: "a3", text: "Elle yazılan satır", sortOrder: 2, recordLinkId: null, carriedFromMeetingId: null }
          ]
        }
      }),
      linkedTasks: async () => ({
        ok: true,
        data: [
          { recordLinkId: "l1", linkType: "agenda", taskId: "t1", title: "Okunabilen iş", link: "/WorkCenterNext/Details/t1" },
          { recordLinkId: "l2", linkType: "agenda", taskId: "t2", title: "TASK_NOT_ACCESSIBLE", link: "/WorkCenterNext/Details/t2" }
        ]
      }),
      failureMessage: () => "error"
    };
    loadScript("wwwroot/assets/js/shared/diten-related-records.js");
    loadScript("wwwroot/assets/js/Meetings/form.js");
    document.dispatchEvent(new Event("DOMContentLoaded"));
    for (let i = 0; i < 4; i++) { await flushMicrotasks(); }

    const lines = [...document.querySelectorAll("#agendaList li")].map((li) => li.textContent);
    expect(lines[0]).toContain("carriedOpenActionLine: Okunabilen iş");
    expect(lines[1]).toContain("carriedOpenActionLine: linkedTaskNotAccessible");
    expect(lines[2]).toContain("Elle yazılan satır");
    const agenda = document.getElementById("agendaList").textContent;
    expect(agenda).not.toContain("MEETING_CARRIED_OPEN_ACTION");
    expect(agenda).not.toContain("TASK_NOT_ACCESSIBLE");

    const linked = document.getElementById("linkedTasksList").textContent;
    expect(linked).toContain("linkedTaskNotAccessible");
    expect(linked).not.toContain("TASK_NOT_ACCESSIBLE");
  });

  it("is translated ×7 and carried through the page's l10n bridge", () => {
    const bridge = fs.readFileSync(path.join(WEB, "Views", "Meetings", "_IndexL10n.cshtml"), "utf8");
    for (const key of ["CarriedOpenActionLine", "LinkedTaskNotAccessible"]) {
      expect(bridge).toContain(`${key} = Localizer["${key}"]`);
      const english = resxValue(path.join(WEB, "Resources", "Views", "Meetings", "MeetingsIndex.en.resx"), key);
      for (const language of LANGUAGES) {
        const value = resxValue(path.join(WEB, "Resources", "Views", "Meetings", `MeetingsIndex.${language}.resx`), key);
        expect(value, `${key} missing in ${language}`).toBeTruthy();
        if (language !== "en") expect(value, `${key} in ${language} is still English`).not.toBe(english);
      }
    }
  });
});

describe("Task Center detail: an unreadable related task is a sentence, not the code", () => {
  const TASK_ID = "b6b6b6b6-b6b6-4b6b-8b6b-b6b6b6b6b6b6";
  const item = {
    fixtureKind: "workItem", id: TASK_ID, workIntent: "task", assignmentMode: "direct", ownershipState: "owned",
    admissionState: "admitted", normalizedStatus: "InProgress", taskLifecycle: "InProgress", executionState: "active",
    timerState: "notApplicable", systemState: "fresh", actionDepth: "inline",
    title: { kind: "display", text: "Related records test task", locale: "und" },
    nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
    source: { providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID, deepLink: `/Tasks/${TASK_ID}` },
    assignee: { id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", isCurrentUser: true },
    requester: { id: "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", displayName: "Test Requester" },
    lifecycleOwner: "tasks",
    workItemCapabilities: ["planning", "execution", "relatedRecords"],
    relatedRecords: [
      { id: "t-hidden", type: "child", title: "TASK_NOT_ACCESSIBLE", link: "/WorkCenterNext/Details/t-hidden" },
      { id: "t-open", type: "child", title: "Görünen iş", link: "/WorkCenterNext/Details/t-open" }
    ],
    actions: [], concurrency: { kind: "version", token: "1" }, waitingContext: null, escalation: null,
    dueAt: "2026-09-30T00:00:00+00:00"
  };

  it("renders the sentence key for the unreadable row and the title for the readable one", async () => {
    await bootSurface({ rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`, items: [item] });

    const titles = [...app().querySelectorAll(".wcn-related-row strong")].map((s) => s.textContent);
    expect(titles).toEqual(["RelatedTaskNotAccessible", "Görünen iş"]);
    expect(app().textContent).not.toContain("TASK_NOT_ACCESSIBLE");
  });

  it("is translated ×7 in the Task Center's own resx", () => {
    const key = "RelatedTaskNotAccessible";
    const english = resxValue(path.join(WEB, "Resources", "Views", "WorkCenterNext", "WorkCenterNextIndex.en.resx"), key);
    for (const language of LANGUAGES) {
      const value = resxValue(path.join(WEB, "Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${language}.resx`), key);
      expect(value, `${key} missing in ${language}`).toBeTruthy();
      if (language !== "en") expect(value).not.toBe(english);
    }
  });
});
