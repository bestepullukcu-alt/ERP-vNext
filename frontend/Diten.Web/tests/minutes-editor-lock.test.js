const { loadScript } = require("./load-script");

/*
 * MOD-0357 S6 (WP-MG-MOD0357-S6-MINUTES-01, AC1/AC2) — K4's lock, PROVEN ON THE ACTUAL DOM the editor renders,
 * not inferred from source text: once the latest minutes version is Published, every content field (attendance,
 * decision text, decided-by) renders `disabled`, and "Karar Ekle" is withdrawn — the same UAS-001 posture
 * Details.cshtml's own Cancelled/Completed controls take. The one deliberate EXCEPTION is "Görev oluştur": K4/
 * ADR-003 explicitly allow a decision inside an already-published version to still produce a task afterward
 * (flagged `createdAfterMinutesPublished`), so that one button must stay live even while everything else locks.
 */

const MEETING_ID = "11111111-1111-1111-1111-111111111111";
const ATTENDEE_ID = "22222222-2222-2222-2222-222222222222";

const buildDom = (canWrite, canPublish) => {
  document.body.innerHTML = `
    <div id="minutesEditorRoot" data-meeting-id="${MEETING_ID}" data-can-write="${canWrite}" data-can-publish="${canPublish}">
      <div id="minutesActionBar">
        <button id="btnSaveDraft" class="d-none"></button>
        <button id="btnPublish" class="d-none"></button>
        <button id="btnCorrect" class="d-none"></button>
      </div>
      <div id="minutesNotFound" class="d-none"></div>
      <div id="minutesEditorBody" class="d-none">
        <span id="mMeetingTitle"></span>
        <span id="mStatusBadge"></span>
        <div>
          <button id="btnAddDecision" class="d-none"></button>
          <div id="decisionsList"></div>
          <p id="noDecisionsHint" class="d-none"></p>
          <div id="addedLaterTasksSection" class="d-none">
            <div id="addedLaterTasksList"></div>
          </div>
        </div>
        <ul id="attendanceList"></ul>
        <p id="noAttendeesHint" class="d-none"></p>
        <ul id="versionHistoryList"></ul>
      </div>
    </div>`;
};

const stubGlobals = ({ minutesVersions }) => {
  global.MeetingsL10n = { t: (key) => key };
  global.MinutesEditorL10n = { t: (key) => key };
  global.DitenRelatedRecords = { relatedRecordRow: () => "<a></a>" };
  global.DitenDialog = { dialogLook: () => ({}), dialogIcon: () => "" };
  global.DitenModal = { success: () => {}, error: () => {} };
  global.showConfirm = () => {};

  global.MeetingsApi = {
    get: async () => ({
      ok: true,
      data: { id: MEETING_ID, title: "Aylık Yönetim Gözden Geçirmesi", attendees: [{ userId: ATTENDEE_ID, invitationResponse: 1 }] }
    }),
    getMinutes: async () => ({ ok: true, data: { versions: minutesVersions } }),
    lookupAttendees: async () => ({ ok: true, data: { people: [{ userId: ATTENDEE_ID, displayName: "Ayşe Yılmaz" }] } }),
    linkedTasks: async () => ({ ok: true, data: [] }),
    failureMessage: () => "error"
  };
};

const flushMicrotasks = () => new Promise((resolve) => setTimeout(resolve, 0));

describe("MOD-0357 S6: the minutes editor locks on a Published version", () => {
  beforeEach(() => {
    delete global.MeetingsApi;
    delete global.MeetingsL10n;
    delete global.MinutesEditorL10n;
  });

  it("renders every field editable against a Draft version", async () => {
    buildDom(true, true);
    stubGlobals({
      minutesVersions: [{
        id: "v1", versionNumber: 1, status: 0, version: 1,
        attendance: [{ attendeeUserId: ATTENDEE_ID, status: 0 }],
        decisions: [{ code: "D-1", text: "İlk karar", decidedByUserId: null, recordLinkId: null }],
        actionReferences: [], publishedAtUtc: null, publishedByUserId: null,
        correctionOfVersionNumber: null, correctionReason: null
      }]
    });

    loadScript("wwwroot/assets/js/Meetings/minutes-editor.js");
    await flushMicrotasks();
    await flushMicrotasks();

    expect(document.getElementById("minutesEditorBody").classList.contains("d-none")).toBe(false);
    expect(document.querySelector(".js-attendance-status").disabled).toBe(false);
    expect(document.querySelector(".js-decision-text").disabled).toBe(false);
    expect(document.querySelector(".js-decision-decided-by").disabled).toBe(false);
    expect(document.getElementById("btnAddDecision").classList.contains("d-none")).toBe(false);
    expect(document.getElementById("btnSaveDraft").classList.contains("d-none")).toBe(false);
    expect(document.getElementById("btnPublish").classList.contains("d-none")).toBe(false);
    expect(document.getElementById("btnCorrect").classList.contains("d-none")).toBe(true);
    // The remove button IS the editability tell for a decision row — present only while unlocked.
    expect(document.querySelector(".js-remove-decision")).not.toBeNull();
  });

  it("locks every content field against a Published version, but keeps 'Görev oluştur' live", async () => {
    buildDom(true, true);
    stubGlobals({
      minutesVersions: [{
        id: "v1", versionNumber: 1, status: 1, version: 2,
        attendance: [{ attendeeUserId: ATTENDEE_ID, status: 0 }],
        decisions: [{ code: "D-1", text: "Yayımlanmış karar", decidedByUserId: ATTENDEE_ID, recordLinkId: null }],
        actionReferences: [], publishedAtUtc: "2026-09-12T00:00:00Z", publishedByUserId: ATTENDEE_ID,
        correctionOfVersionNumber: null, correctionReason: null
      }]
    });

    loadScript("wwwroot/assets/js/Meetings/minutes-editor.js");
    await flushMicrotasks();
    await flushMicrotasks();

    expect(document.querySelector(".js-attendance-status").disabled).toBe(true);
    expect(document.querySelector(".js-decision-text").disabled).toBe(true);
    expect(document.querySelector(".js-decision-decided-by").disabled).toBe(true);
    // The decided-by value survives the lock (read-only display, not a blank placeholder).
    expect(document.querySelector(".js-decision-decided-by").value).toBe(ATTENDEE_ID);
    expect(document.getElementById("btnAddDecision").classList.contains("d-none")).toBe(true);
    expect(document.querySelector(".js-remove-decision")).toBeNull();
    expect(document.getElementById("btnSaveDraft").classList.contains("d-none")).toBe(true);
    expect(document.getElementById("btnPublish").classList.contains("d-none")).toBe(true);
    expect(document.getElementById("btnCorrect").classList.contains("d-none")).toBe(false);

    // K4/ADR-003 — "added later" tasks are still possible on a Published decision; this affordance must NOT be
    // withdrawn by the same lock that freezes the decision's own text.
    expect(document.querySelector(".js-create-task-from-decision")).not.toBeNull();
  });

  it("shows read-only, not the write controls, when the caller cannot write minutes", async () => {
    buildDom(false, false);
    stubGlobals({ minutesVersions: [] });

    loadScript("wwwroot/assets/js/Meetings/minutes-editor.js");
    await flushMicrotasks();
    await flushMicrotasks();

    expect(document.getElementById("btnSaveDraft").classList.contains("d-none")).toBe(true);
    expect(document.getElementById("btnPublish").classList.contains("d-none")).toBe(true);
    expect(document.getElementById("btnCorrect").classList.contains("d-none")).toBe(true);
    expect(document.getElementById("btnAddDecision").classList.contains("d-none")).toBe(true);
  });
});
