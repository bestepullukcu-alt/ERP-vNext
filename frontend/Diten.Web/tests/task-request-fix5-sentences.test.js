const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");
const { bootSurface, app } = require("./wcn-boot");

/*
 * WP-WCN-TASK-REQUEST-01 FIX5 (item 5) — the rules FIX4 wired without a test of their own, measured on the page:
 *   the subtask cancel and the reassign dialog read the withdraw-failed refusal as the ACT sentence;
 *   the minutes editor shows TASK_NOT_ACCESSIBLE as a sentence;
 *   the codes the screens compare against are the C# constants, letter for letter.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const read = (...parts) => fs.readFileSync(path.join(repoRoot, ...parts), "utf8");
const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

describe("the subtask cancel says nothing was done when the linked workflow could not be withdrawn", () => {
  const TASK_ID = "c1c1c1c1-c1c1-4c1c-8c1c-c1c1c1c1c1c1";
  const item = {
    fixtureKind: "workItem", id: TASK_ID, workIntent: "task", assignmentMode: "direct", ownershipState: "owned",
    admissionState: "admitted", normalizedStatus: "InProgress", taskLifecycle: "InProgress", executionState: "active",
    timerState: "notApplicable", systemState: "fresh", actionDepth: "inline",
    title: { kind: "display", text: "Üst iş", locale: "und" },
    nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
    source: { providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID, deepLink: `/Tasks/${TASK_ID}` },
    assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
    requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
    lifecycleOwner: "tasks", workItemCapabilities: ["planning", "execution", "subtasks"],
    subtasks: { mode: "full", items: [{ id: "s1", title: "Alt iş", status: "in-progress", canCancel: true }] },
    actions: [], concurrency: { kind: "version", token: "1" }, waitingContext: null, escalation: null, dueAt: null
  };

  it("toasts the act sentence for TASK_REVIEW_CANCEL_FAILED", async () => {
    const toasts = [];
    global.showToast = (message, type) => { toasts.push({ message, type }); };
    global.showConfirm = (message, onConfirm) => { onConfirm(); };
    await bootSurface({ rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`, items: [item] });
    global.TasksL10n = { t: (key) => `t:${key}` };
    loadScript("wwwroot/assets/js/Tasks/api.js");
    global.TasksApi.get = () => Promise.resolve({ ok: true, status: 200, data: { version: 2 } });
    global.TasksApi.transition = () => Promise.resolve({ ok: false, status: 409, reasonCode: "TASK_REVIEW_CANCEL_FAILED", errors: [] });

    const button = app().querySelector("[data-wcn-subtask-cancel]");
    expect(button, "the subtask row offers no cancel").toBeTruthy();
    button.click();
    for (let i = 0; i < 5; i++) { await settle(); }

    const errors = toasts.filter((entry) => entry.type === "error").map((entry) => entry.message);
    expect(errors).toContain("t:errorWorkflowWithdrawFailedOnAct");
    expect(errors).not.toContain("t:errorReviewCancelFailed");
    delete global.showToast;
    delete global.showConfirm;
  });
});

describe("the reassign dialog says nothing was done when the linked workflow could not be withdrawn", () => {
  let toasts;
  const mount = (answer) => {
    toasts = [];
    document.body.innerHTML =
      '<input id="taskAssignmentTargetRead" /><input id="taskAssigneeRead" />' +
      '<select id="taskAssignee"><option value="me" selected>Ali</option></select>' +
      '<select id="taskPoolPosition"></select>' +
      '<button id="btnTaskReassign" class="d-none"></button>';
    global.window.L10n = {};
    global.window.CurrentUser = { id: "me", displayName: "Ali" };
    const look = () => ({});
    look.iconHtml = () => "";
    look.description = "d";
    global.window.DitenDialogAppearance = look;
    global.window.Swal = { fire: () => Promise.resolve({ isConfirmed: true, value: { to: "u-2", reason: "izin" } }), showValidationMessage: () => {} };
    global.window.showToast = (message, type) => { toasts.push({ message, type }); };
    delete global.TasksApi;
    global.TasksL10n = { t: (key) => `t:${key}` };
    loadScript("wwwroot/assets/js/Tasks/api.js");
    global.TasksApi.assignablePeople = () => Promise.resolve({ ok: true, data: [{ userId: "u-2", displayName: "Ayşe" }] });
    global.TasksApi.transition = () => Promise.resolve(answer);
    delete global.TaskReassign;
    loadScript("wwwroot/assets/js/Tasks/reassign.js");
    global.window.TaskReassign.mount("t-1",
      { assignmentTarget: "Person", assigneeUserId: "me", createdByUserId: "x", lifecycle: "InProgress", version: 3 });
  };

  it("maps TASK_APPROVAL_CANCEL_FAILED to the act sentence", async () => {
    mount({ ok: false, status: 409, reasonCode: "TASK_APPROVAL_CANCEL_FAILED", errors: ["server words"] });
    document.getElementById("btnTaskReassign").click();
    for (let i = 0; i < 5; i++) { await settle(); }
    expect(toasts.map((entry) => entry.message)).toEqual(["t:errorWorkflowWithdrawFailedOnAct"]);
  });

  it("keeps the server's own words for any other refusal", async () => {
    mount({ ok: false, status: 409, reasonCode: "TASK_REASSIGN_NOT_PERMITTED", errors: ["server words"] });
    document.getElementById("btnTaskReassign").click();
    for (let i = 0; i < 5; i++) { await settle(); }
    expect(toasts.map((entry) => entry.message)).toEqual(["server words"]);
  });
});

describe("the minutes editor shows an unreadable decision task as a sentence", () => {
  it("never prints TASK_NOT_ACCESSIBLE", async () => {
    const MEETING_ID = "11111111-1111-1111-1111-111111111111";
    document.body.innerHTML = `
      <div id="minutesEditorRoot" data-meeting-id="${MEETING_ID}" data-can-write="true" data-can-publish="true">
        <div id="minutesActionBar"><button id="btnSaveDraft" class="d-none"></button><button id="btnPublish" class="d-none"></button><button id="btnCorrect" class="d-none"></button></div>
        <div id="minutesNotFound" class="d-none"></div>
        <div id="minutesEditorBody" class="d-none">
          <span id="mMeetingTitle"></span><span id="mStatusBadge"></span>
          <div><button id="btnAddDecision" class="d-none"></button><div id="decisionsList"></div><p id="noDecisionsHint" class="d-none"></p>
            <div id="addedLaterTasksSection" class="d-none"><div id="addedLaterTasksList"></div></div></div>
          <ul id="attendanceList"></ul><p id="noAttendeesHint" class="d-none"></p><ul id="versionHistoryList"></ul>
        </div>
      </div>`;
    global.MeetingsL10n = { t: (key) => `m:${key}` };
    global.MinutesEditorL10n = { t: (key) => key };
    global.DitenDialog = { dialogLook: () => ({}), dialogIcon: () => "" };
    global.DitenModal = { success: () => {}, error: () => {} };
    global.showConfirm = () => {};
    global.MeetingsApi = {
      get: async () => ({ ok: true, data: { id: MEETING_ID, title: "Toplantı", attendees: [] } }),
      getMinutes: async () => ({ ok: true, data: { versions: [{
        id: "v1", versionNumber: 1, status: 0, version: 1, attendance: [],
        decisions: [{ code: "D-1", text: "Karar", decidedByUserId: null, recordLinkId: "l1" }],
        actionReferences: [], publishedAtUtc: null, publishedByUserId: null, correctionOfVersionNumber: null, correctionReason: null
      }] } }),
      lookupAttendees: async () => ({ ok: true, data: { people: [] } }),
      linkedTasks: async () => ({ ok: true, data: [
        { recordLinkId: "l1", linkType: "decision", taskId: "t1", title: "TASK_NOT_ACCESSIBLE", link: "/WorkCenterNext/Details/t1" }
      ] }),
      failureMessage: () => "error"
    };
    delete global.DitenRelatedRecords;
    loadScript("wwwroot/assets/js/shared/diten-related-records.js");
    loadScript("wwwroot/assets/js/Meetings/minutes-editor.js");
    for (let i = 0; i < 4; i++) { await settle(); }

    const text = document.getElementById("decisionsList").textContent;
    expect(text).toContain("m:linkedTaskNotAccessible");
    expect(text).not.toContain("TASK_NOT_ACCESSIBLE");
  });
});

describe("the link picker says when the server stopped at its ceiling", () => {
  const MEETING_ID = "11111111-1111-1111-1111-111111111111";
  const LANGUAGES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

  const open = async (answer) => {
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
    const confirms = [];
    global.MeetingsL10n = { t: (key) => `m:${key}` };
    global.DitenPersonPicker = { renderPersonOptions: () => {}, buildSearchableDropdownAdapter: () => ({}) };
    global.DitenModal = { success: () => {}, error: () => {} };
    global.jQuery = undefined;
    global.showConfirm = (title, onConfirm, options) => { confirms.push(options); };
    global.MeetingsApi = {
      lookupAttendees: async () => ({ ok: true, data: { people: [] } }),
      get: async () => ({ ok: true, data: { id: MEETING_ID, title: "T", meetingTypeId: "t", organizerUserId: "u1", lifecycle: 0, version: 1, attendees: [], agendaItems: [] } }),
      linkedTasks: async () => ({ ok: true, data: [] }),
      failureMessage: () => "error"
    };
    global.TasksApi = { linkCandidates: async () => answer };
    delete global.DitenRelatedRecords;
    loadScript("wwwroot/assets/js/shared/diten-related-records.js");
    loadScript("wwwroot/assets/js/Meetings/form.js");
    document.dispatchEvent(new Event("DOMContentLoaded"));
    for (let i = 0; i < 4; i++) { await settle(); }
    // FIX6 — "narrow your search" only when the term can be sent: select2 is on the page.
    const jq = () => ({ hasClass: () => false, data: () => "", select2: () => {} });
    jq.fn = { select2: true };
    global.jQuery = jq;
    document.getElementById("btnLinkExistingTask").click();
    for (let i = 0; i < 4; i++) { await settle(); }
    return confirms;
  };

  it("shows the narrow-your-search sentence when truncated, and lists the items it got", async () => {
    const confirms = await open({ ok: true, data: { items: [{ id: "t1", title: "Okunur iş" }], truncated: true } });
    const options = confirms[confirms.length - 1];
    expect(options, "the picker never opened").toBeTruthy();
    expect(options.subtext).toBe("m:linkExistingTaskTruncated");
    expect(options.inputOptions.t1).toBe("Okunur iş");
  });

  it("says nothing extra when the search was complete", async () => {
    const confirms = await open({ ok: true, data: { items: [], truncated: false } });
    expect(confirms[confirms.length - 1].subtext).toBe("");
  });

  it("is translated ×7 and carried through the bridge", () => {
    const resx = (language) => read("frontend", "Diten.Web", "Resources", "Views", "Meetings", `MeetingsIndex.${language}.resx`);
    const value = (language) => (resx(language).match(/<data name="LinkExistingTaskTruncated"[^>]*>\s*<value>([\s\S]*?)<\/value>/) || [])[1];
    expect(read("frontend", "Diten.Web", "Views", "Meetings", "_IndexL10n.cshtml"))
      .toContain('LinkExistingTaskTruncated = Localizer["LinkExistingTaskTruncated"]');
    for (const language of LANGUAGES) {
      expect(value(language), `missing in ${language}`).toBeTruthy();
      if (language !== "en") expect(value(language)).not.toBe(value("en"));
    }
  });
});

describe("the codes the screens compare against are the server's constants", () => {
  const constant = (file, name) => {
    const match = read(...file).match(new RegExp(`const string ${name} = "([^"]+)";`));
    expect(match, `${name} not found`).toBeTruthy();
    return match[1];
  };
  const FEATURES = ["services", "Diten.Platform", "src", "Diten.Platform.Application", "Features"];
  const JS = (...p) => ["frontend", "Diten.Web", "wwwroot", "assets", "js", ...p];

  it("MEETING_CARRIED_OPEN_ACTION", () => {
    const code = constant([...FEATURES, "Meetings", "MeetingModels.cs"], "CarriedOpenAction");
    expect(read(...JS("Meetings", "form.js"))).toContain(`const CARRIED_OPEN_ACTION = '${code}';`);
  });

  it("TASK_NOT_ACCESSIBLE", () => {
    const code = constant([...FEATURES, "Tasks", "TaskLinks.cs"], "InaccessibleTitleCode");
    expect(read(...JS("Meetings", "form.js"))).toContain(`const TASK_NOT_ACCESSIBLE = '${code}';`);
    expect(read(...JS("Meetings", "minutes-editor.js"))).toContain(`title === '${code}'`);
    expect(read(...JS("WorkCenterNext", "app.js"))).toContain(`record.title === '${code}'`);
  });
});
