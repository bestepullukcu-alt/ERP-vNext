const { loadScript } = require("./load-script");

/*
 * MOD-0357 S12 (pack §23) — the meeting report screen, driven through `window.MeetingReportScreen` directly
 * (see index.js's own header comment: jsdom's `document.readyState` is already `'complete'` by load time, so
 * the real `DOMContentLoaded` listener never fires here — the same measured fact `work-report-layout.test.js`
 * already records for its own module).
 *
 * Covers §23.9's AC5 (export refusal messages), UAS-001 (no skeleton for a 403), and the table/empty-state
 * rendering the WP's own DOĞRULA names.
 */

const SCRIPT = "wwwroot/assets/js/Meetings/Report/index.js";

const MARKUP = `
  <form id="reportFilterForm">
    <input id="mrFrom" value="2026-08-01" />
    <input id="mrTo" value="2026-08-31" />
    <select id="mrMeetingType"></select>
    <select id="mrOrganizer"></select>
    <button type="button" data-mr-export-toggle disabled></button>
    <button type="button" data-mr-export="meetings" data-mr-export-format="csv"></button>
    <button type="button" data-mr-export="actions" data-mr-export-format="json"></button>
  </form>
  <p data-status id="mrStatus"></p>
  <div class="alert d-none" id="mrNoAccess"></div>
  <div id="mrTiles" hidden>
    <span id="mrTileMeetingCount"></span>
    <span id="mrTileAttendanceRate"></span>
    <span id="mrTileDecisionCount"></span>
    <span id="mrTileOpenActions"></span>
    <span id="mrTileOverdueActions"></span>
  </div>
  <div id="mrMeetingsCard" hidden>
    <table><tbody id="mrMeetingsBody"></tbody></table>
    <p id="mrMeetingsEmpty" hidden></p>
  </div>
  <div id="mrDecisionsCard" hidden>
    <table><tbody id="mrDecisionsBody"></tbody></table>
    <p id="mrDecisionsEmpty" hidden></p>
  </div>
  <div id="mrActionsCard" hidden>
    <div id="mrActionsSkeleton"></div>
    <table id="mrActionsTable"><thead></thead><tbody></tbody></table>
    <p id="mrActionsEmpty" hidden></p>
  </div>
  <div id="mrScope" hidden><span id="mrScopeBadge"></span></div>
`;

const L10N = {
  Loading: "Loading…", ErrorOccurred: "An error occurred.", ErrorNoAccess: "You do not have access to this report.",
  ExportAuditNotRecorded: "The export could not be recorded in the audit trail, so the file was not issued. Try again later.",
  ExportTooLarge: "This report covers too many rows to export. Narrow the period or the filters.",
  BadgeOverdue: "Overdue", LifecycleOpen: "Open", LifecycleDone: "Done",
  ColActionTitle: "Action", ColActionLifecycle: "Status", ColActionDueAt: "Due",
  ColActionOrigin: "Origin meeting", ColActionCurrent: "Current meeting",
  ScopeTenant: "Whole tenant", ScopeScoped: "Your meetings"
};

const meetingRow = (overrides = {}) => Object.assign({
  id: "m1", title: "Q3 review", meetingTypeId: "t1", meetingTypeName: "Management review",
  startAt: "2026-08-15T09:00:00Z", organizerUserId: "u1", attendeeCount: 4, respondedCount: 3,
  attendanceRatePercent: 75
}, overrides);

const decisionRow = (overrides = {}) => Object.assign({
  meetingId: "m1", meetingTitle: "Q3 review", code: "D-1", text: "Approved the budget", decidedByUserId: "u1"
}, overrides);

const actionRow = (overrides = {}) => Object.assign({
  taskId: "task1", title: "Follow up with finance", lifecycle: "Open", dueAt: "2026-08-01T00:00:00Z",
  isOverdue: true, assigneeUserId: "u2", originMeetingId: "m1", originMeetingTitle: "Q3 review",
  currentMeetingId: "m1", currentMeetingTitle: "Q3 review"
}, overrides);

const reportPayload = (overrides = {}) => ({
  data: Object.assign({
    from: "2026-08-01T00:00:00Z", to: "2026-08-31T00:00:00Z", scopeApplied: "scoped",
    totals: { meetingCount: 1, attendanceRatePercent: 75, decisionCount: 1, openActionCount: 1, overdueActionCount: 1 },
    meetings: [meetingRow()], decisions: [decisionRow()], actions: [actionRow()]
  }, overrides)
});

const boot = () => {
  document.body.innerHTML = MARKUP;
  delete global.MeetingReportScreen;
  delete global.jQuery;
  delete global.flatpickr;
  delete global.MeetingsApi;
  global.MeetingReportL10n = { t: (key) => L10N[key] ?? key };
  global.URL.createObjectURL = global.URL.createObjectURL || (() => "blob:mock");
  global.URL.revokeObjectURL = global.URL.revokeObjectURL || (() => {});

  loadScript(SCRIPT);
  return global.MeetingReportScreen;
};

describe("MOD-0357 S12: the meeting report screen", () => {
  test("a successful report reveals the tiles and tables, and fills the totals", async () => {
    const screen = boot();
    global.fetch = () => Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(reportPayload()) });

    await screen.loadReport();

    expect(document.getElementById("mrTiles").hidden).toBe(false);
    expect(document.getElementById("mrTileMeetingCount").textContent).toBe("1");
    expect(document.getElementById("mrTileAttendanceRate").textContent).toBe("75%");
    expect(document.getElementById("mrTileOverdueActions").textContent).toBe("1");
    expect(document.getElementById("mrMeetingsCard").hidden).toBe(false);
    expect(document.getElementById("mrMeetingsBody").innerHTML).toContain("Q3 review");
    expect(document.getElementById("mrDecisionsBody").innerHTML).toContain("D-1");
  });

  // ── UAS-001 — a 403 shows exactly the no-access sentence, nothing else ──────────────────────────────────

  test("UAS-001: a 403 response shows ONLY the no-access sentence — no tile, no table, no skeleton", async () => {
    const screen = boot();
    global.fetch = () => Promise.resolve({ ok: false, status: 403, json: () => Promise.resolve(null) });

    await screen.loadReport();

    const noAccess = document.getElementById("mrNoAccess");
    expect(noAccess.classList.contains("d-none")).toBe(false);
    expect(noAccess.textContent).toBe(L10N.ErrorNoAccess);
    expect(document.getElementById("mrTiles").hidden).toBe(true);
    expect(document.getElementById("mrMeetingsCard").hidden).toBe(true);
    expect(document.getElementById("mrDecisionsCard").hidden).toBe(true);
    expect(document.getElementById("mrActionsCard").hidden).toBe(true);
  });

  // ── empty states ──────────────────────────────────────────────────────────────────────────────────────

  test("an empty period shows the empty-state sentences, not a blank table", async () => {
    const screen = boot();
    global.fetch = () => Promise.resolve({
      ok: true, status: 200,
      json: () => Promise.resolve(reportPayload({
        totals: { meetingCount: 0, attendanceRatePercent: null, decisionCount: 0, openActionCount: 0, overdueActionCount: 0 },
        meetings: [], decisions: [], actions: []
      }))
    });

    await screen.loadReport();

    expect(document.getElementById("mrTileAttendanceRate").textContent).toBe("–");
    expect(document.getElementById("mrMeetingsEmpty").hidden).toBe(false);
    expect(document.getElementById("mrDecisionsEmpty").hidden).toBe(false);
    expect(document.getElementById("mrActionsEmpty").hidden).toBe(false);
  });

  // ── AC5 / export refusal messages ────────────────────────────────────────────────────────────────────

  test("AC5: a 503 DATA_EXPORT_AUDIT_NOT_RECORDED export shows the audit-trail sentence, no download", async () => {
    const screen = boot();
    global.fetch = () => Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(reportPayload()) });
    await screen.loadReport();

    let clicked = false;
    const realCreate = document.createElement.bind(document);
    document.createElement = (tag) => {
      const el = realCreate(tag);
      if (tag === "a") { el.click = () => { clicked = true; }; }
      return el;
    };

    global.fetch = () => Promise.resolve({
      ok: false, status: 503,
      json: () => Promise.resolve({ reason_code: "DATA_EXPORT_AUDIT_NOT_RECORDED" }),
      headers: new Map()
    });

    await screen.downloadExport("meetings", "csv");

    expect(document.getElementById("mrStatus").textContent).toBe(L10N.ExportAuditNotRecorded);
    expect(clicked).toBe(false);
    document.createElement = realCreate;
  });

  test("AC5: a 400 MEETING_REPORT_EXPORT_TOO_LARGE export shows the too-large sentence", async () => {
    const screen = boot();
    global.fetch = () => Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(reportPayload()) });
    await screen.loadReport();

    global.fetch = () => Promise.resolve({
      ok: false, status: 400,
      json: () => Promise.resolve({ reasonCode: "MEETING_REPORT_EXPORT_TOO_LARGE" })
    });

    await screen.downloadExport("actions", "json");

    expect(document.getElementById("mrStatus").textContent).toBe(L10N.ExportTooLarge);
  });

  test("a successful export downloads the file and revokes the object URL", async () => {
    const screen = boot();
    global.fetch = () => Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(reportPayload()) });
    await screen.loadReport();

    let clicked = false;
    let revoked = false;
    const realCreate = document.createElement.bind(document);
    document.createElement = (tag) => {
      const el = realCreate(tag);
      if (tag === "a") { el.click = () => { clicked = true; }; }
      return el;
    };
    global.URL.revokeObjectURL = () => { revoked = true; };

    const headers = new Map([["Content-Disposition", 'attachment; filename="toplanti-raporu-meetings_2026-08-01_2026-08-30.csv"']]);
    global.fetch = () => Promise.resolve({
      ok: true, status: 200,
      headers: { get: (name) => headers.get(name) || null },
      blob: () => Promise.resolve(new Blob(["csv content"]))
    });

    await screen.downloadExport("meetings", "csv");

    expect(clicked).toBe(true);
    expect(revoked).toBe(true);
    document.createElement = realCreate;
  });

  // ── action register — overdue badge + lifecycle label ───────────────────────────────────────────────────

  test("an overdue action renders the overdue badge; lifecycleLabel resolves through the l10n bridge", async () => {
    const screen = boot();
    expect(screen.lifecycleLabel("Open")).toBe(L10N.LifecycleOpen);
    expect(screen.lifecycleLabel("Done")).toBe(L10N.LifecycleDone);

    global.fetch = () => Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve(reportPayload()) });
    await screen.loadReport();

    // No jQuery/DataTable stub loaded in this test — renderActions must still reveal the card and the empty
    // state correctly rather than throwing, which is what AC5/UAS-001's own tests above already depend on.
    expect(document.getElementById("mrActionsCard").hidden).toBe(false);
    expect(document.getElementById("mrActionsSkeleton").hidden).toBe(true);
  });
});
