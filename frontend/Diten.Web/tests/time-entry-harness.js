const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * MOD-0280-FU01 T2a — the harness for My Timesheet and the timer chip. The page's REAL scripts are loaded
 * (TimeEntry/core.js, api.js, index.js, shared/timer-chip.js); the only thing faked is the network — `fetch` answers
 * from a route table the test writes, and every call is recorded so a test can read the exact body the page sent.
 */

const webRoot = path.resolve(__dirname, "..");

/** The page's DOM, taken from Index.cshtml's ids. A guard test (time-entry-page.test.js) checks every id below is
 * really in the view, so this fixture cannot drift from it silently. */
const PAGE_IDS = [
  "timeEntryPage", "teWeekLabel", "tePrevWeek", "teToday", "teNextWeek", "teStatus", "teMissingDays", "teActionsMenu",
  "teCopyPrevious", "teFillFromPlan", "teReviewSuggestions", "teLoading", "teContent", "teBanner", "teHistory",
  "teNotice", "teRowAdd", "teReview", "teGrid", "teDayView", "teFooterActions", "teSide", "teSideToggle", "teSideBody",
  "teDisclaimer"
];

function pageHtml({ canUpdate = true, week = "" } = {}) {
  return `
    <div class="time-entry-page" id="timeEntryPage" data-can-update="${canUpdate ? "true" : "false"}" data-week="${week}">
      <header>
        <p id="teWeekLabel"></p>
        <button type="button" id="tePrevWeek"><i class="bx bx-chevron-left"></i></button>
        <button type="button" id="teToday">Today</button>
        <button type="button" id="teNextWeek"><i class="bx bx-chevron-right"></i></button>
        <span id="teStatus" hidden></span>
        <span id="teMissingDays" hidden></span>
        <div id="teActionsMenu" hidden>
          <button type="button" id="teCopyPrevious"></button>
          <button type="button" id="teFillFromPlan"></button>
          <button type="button" id="teReviewSuggestions"></button>
        </div>
      </header>
      <p id="teLoading">Loading</p>
      <div id="teContent" hidden>
        <div id="teBanner"></div>
        <div id="teHistory" hidden></div>
        <div id="teNotice"></div>
        <section>
          <div id="teRowAdd"></div>
          <div id="teReview" hidden></div>
          <div id="teGrid"></div>
          <div id="teDayView"></div>
          <div id="teFooterActions"></div>
        </section>
        <aside id="teSide">
          <button type="button" id="teSideToggle"></button>
          <div id="teSideBody"></div>
        </aside>
      </div>
      <p id="teDisclaimer">disclaimer</p>
    </div>`;
}

/** The l10n payload: every key maps to itself, so assertions read the key the page chose. */
function l10nScript() {
  const payload = { CategoryLabels: { "TimeEntry.Category.ADMINISTRATION": "Administration" } };
  return `<script id="time-entry-l10n" type="application/json">${JSON.stringify(payload)}</script>`;
}

const TASK_A = "aaaaaaaa-0000-0000-0000-00000000000a";
const TASK_B = "bbbbbbbb-0000-0000-0000-00000000000b";
const TASK_C = "cccccccc-0000-0000-0000-00000000000c";

const WEEK_DATES = ["2026-10-05", "2026-10-06", "2026-10-07", "2026-10-08", "2026-10-09", "2026-10-10", "2026-10-11"];

/** A week payload as Platform sends it (camelCase). Today = Wednesday 2026-10-07: Thu–Sun are future. */
function weekPayload(overrides = {}) {
  const days = WEEK_DATES.map((date, index) => ({
    date,
    dayKind: index >= 5 ? "weekend" : "workingDay",
    isHalfDay: false,
    holidayName: null,
    targetMinutes: index >= 5 ? 0 : 480,
    recordedMinutes: 0,
    isFlagged: false,
    isFuture: index > 2
  }));
  return Object.assign({
    weekKey: "2026-W41",
    weekStartDate: "2026-10-05",
    timeZoneId: "Europe/Istanbul",
    localToday: "2026-10-07",
    weekId: "11111111-1111-1111-1111-111111111111",
    revisionNumber: 1,
    status: "Draft",
    version: 3,
    editable: true,
    notEditableReason: null,
    insideEditWindow: true,
    reopenActive: false,
    reopenReason: null,
    correctionOfRevision: null,
    correctionReason: null,
    inForce: null,
    submittedAtUtc: null,
    approvedAtUtc: null,
    lastRejectedAtUtc: null,
    lastRejectionReason: null,
    finalizationBlockedReason: null,
    flaggedDates: [],
    totalMinutes: 0,
    days,
    entries: [],
    tooShortToCount: [],
    timerOutsideOpenWeek: [],
    timerDraftPending: [],
    suggestions: [],
    submittedByUserId: null,
    submittedByDisplayName: null,
    approvedByUserId: null,
    approvedByDisplayName: null,
    lastRejectedByUserId: null,
    lastRejectedByDisplayName: null,
    assignedApproverUserId: null,
    assignedApproverDisplayName: null
  }, overrides);
}

function entry(date, minutes, extra = {}) {
  return Object.assign({
    id: `e-${date}-${extra.taskItemId || extra.categoryCode || "x"}-${extra.source || "Manual"}`,
    localDate: date,
    durationMinutes: minutes,
    taskItemId: null,
    categoryCode: null,
    source: "Manual",
    note: null,
    outsideWorkingMinutes: 0,
    editedFromTimer: false,
    minutesConflict: false,
    sourceRef: null,
    capturedMinutes: null,
    taskTitle: null
  }, extra);
}

/**
 * Installs a fake fetch. `routes` is a list of [method, pathPrefixOrRegex, handler(body, url) → {status, body}]; the
 * first match answers. Unmatched calls answer 404 so a missing route is loud.
 */
function installNetwork(routes) {
  const calls = [];
  global.fetch = window.fetch = vi.fn(async (url, options = {}) => {
    const method = (options.method || "GET").toUpperCase();
    const body = options.body ? JSON.parse(options.body) : undefined;
    calls.push({ method, url, body });
    const route = routes.find(([m, p]) => m === method && (p instanceof RegExp ? p.test(url) : url.startsWith(p)));
    const answer = route ? await route[2](body, url) : { status: 404, body: { isSuccessful: false } };
    const status = answer.status || 200;
    return {
      ok: status >= 200 && status < 300,
      status,
      text: async () => JSON.stringify(answer.body === undefined ? {} : answer.body)
    };
  });
  return calls;
}

const ok = (data) => ({ status: 200, body: { data, isSuccessful: true, statusCode: 200 } });
const refused = (status, code) => ({ status, body: { isSuccessful: false, statusCode: status, errors: ["x"], reason_code: code } });

const flush = async (times = 6) => {
  for (let i = 0; i < times; i += 1) {
    // eslint-disable-next-line no-await-in-loop
    await new Promise((resolve) => setTimeout(resolve, 0));
  }
};

/** Boots the page's real scripts on the fixture DOM. */
async function bootPage({ canUpdate = true, week = "", dir = "ltr", lang = "en" } = {}) {
  document.documentElement.setAttribute("dir", dir);
  document.documentElement.setAttribute("lang", lang);
  document.body.innerHTML = l10nScript() + pageHtml({ canUpdate, week });
  window.showToast = vi.fn();
  window.showConfirm = vi.fn((_title, onYes) => onYes());
  window.__timeEntryNoAutoInit = true;
  delete window.TimeEntryCore;
  delete window.TimeEntryApi;
  delete window.TimeEntryPage;
  loadScript("wwwroot/assets/js/TimeEntry/core.js");
  loadScript("wwwroot/assets/js/TimeEntry/api.js");
  loadScript("wwwroot/assets/js/TimeEntry/index.js");
  await window.TimeEntryPage.init();
  await flush();
  return window.TimeEntryPage;
}

function readSource(relative) {
  return fs.readFileSync(path.join(webRoot, relative), "utf8");
}

module.exports = {
  PAGE_IDS, pageHtml, weekPayload, entry, installNetwork, ok, refused, flush, bootPage, readSource,
  TASK_A, TASK_B, TASK_C, WEEK_DATES, webRoot
};
