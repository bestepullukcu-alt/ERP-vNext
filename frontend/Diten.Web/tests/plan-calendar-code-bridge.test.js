const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * WP-TASK-CALENDAR-ENGINE-01 (F) — the plan block's coded refusals and warnings reach the reader as a sentence in
 * their own language.
 *
 * THE CHAIN, every link measured on the production file that carries it:
 *   C# constant (TaskModels.cs / WorkCalendarModels.cs)  →  the wire's reason_code / warnings[].code
 *   → Tasks/api.js  REASON_CODE_MESSAGE_KEYS  or  PLAN_WARNING_MESSAGE_KEYS
 *   → Views/Tasks/_IndexL10n.cshtml  (the page payload does NOT auto-enumerate its resx)
 *   → Resources/Views/Tasks/TasksIndex.{en,tr,fr,es,zh,ar,ru}.resx
 *
 * A code missing from any link shows the generic "an error occurred" (or the raw key) — exactly the failure this
 * bridge exists to prevent, and one this repository has shipped more than once.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const API_JS = path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "js", "Tasks", "api.js");
const PAYLOAD = path.join(repoRoot, "frontend", "Diten.Web", "Views", "Tasks", "_IndexL10n.cshtml");
const RESX_DIR = path.join(repoRoot, "frontend", "Diten.Web", "Resources", "Views", "Tasks");
const PLATFORM = path.join(repoRoot, "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features");
const TASK_MODELS = path.join(PLATFORM, "Tasks", "TaskModels.cs");
const CALENDAR_MODELS = path.join(PLATFORM, "WorkAggregation", "Calendar", "WorkCalendarModels.cs");
const LANGUAGES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

// code → [the C# file that declares it, the api.js map that carries it]
const REFUSALS = {
  TASK_PLAN_NOT_HOLDER: TASK_MODELS,
  TASK_PLAN_CONFLICT: TASK_MODELS,
  TASK_PLAN_DURATION_INVALID: TASK_MODELS,
  TASK_UNPLAN_NOT_ALLOWED: TASK_MODELS,
  WORK_CALENDAR_RANGE_INVALID: CALENDAR_MODELS
};
const WARNINGS = {
  TASK_PLAN_OVERLAPS_MEETING: TASK_MODELS,
  TASK_PLAN_OUTSIDE_WORKING_HOURS: TASK_MODELS
};

const resxName = (messageKey) => messageKey.charAt(0).toUpperCase() + messageKey.slice(1);

const bootApi = () => {
  delete global.TasksApi;
  // Echoes the key back, so an assertion names the key the code chose rather than a translation.
  global.TasksL10n = { t: (key) => key };
  loadScript("wwwroot/assets/js/Tasks/api.js");
};

const rows = () => [
  ...Object.entries(REFUSALS).map(([code, source]) => ({ code, source, kind: "refusal" })),
  ...Object.entries(WARNINGS).map(([code, source]) => ({ code, source, kind: "warning" }))
];

const messageKeyOf = ({ code, kind }) =>
  kind === "refusal"
    ? global.TasksApi.REASON_CODE_MESSAGE_KEYS[code]
    : global.TasksApi.PLAN_WARNING_MESSAGE_KEYS[code];

describe("WP-TASK-CALENDAR-ENGINE-01: plan codes ⇔ bridge ⇔ payload ⇔ resx", () => {
  beforeEach(bootApi);

  it.each(rows())("$code is declared by the server as a constant", ({ code, source }) => {
    expect(fs.readFileSync(source, "utf8")).toMatch(new RegExp(`public const string \\w+ = "${code}";`));
  });

  it.each(rows())("$code has a message key in api.js", (row) => {
    expect(messageKeyOf(row), `${row.code} is not mapped in Tasks/api.js`).toBeTruthy();
  });

  it.each(rows())("$code reaches the page payload", (row) => {
    const name = resxName(messageKeyOf(row));
    expect(fs.readFileSync(PAYLOAD, "utf8")).toContain(`${name} = Localizer["${name}"]`);
  });

  it.each(rows())("$code is translated in all seven languages", (row) => {
    const name = resxName(messageKeyOf(row));
    const missing = LANGUAGES.filter((language) => {
      const file = path.join(RESX_DIR, `TasksIndex.${language}.resx`);
      const match = fs.readFileSync(file, "utf8").match(
        new RegExp(`<data name="${name}"[^>]*>\\s*<value>([^<]+)</value>`));
      return !match || !match[1].trim();
    });
    expect(missing, `${name} missing or empty in: ${missing.join(", ")}`).toEqual([]);
  });

  it("the meeting warning keeps its title placeholder in every language", () => {
    const missing = LANGUAGES.filter((language) => {
      const text = fs.readFileSync(path.join(RESX_DIR, `TasksIndex.${language}.resx`), "utf8");
      const match = text.match(/<data name="WarningPlanOverlapsMeeting"[^>]*>\s*<value>([^<]+)<\/value>/);
      return !match || !match[1].includes("{0}");
    });
    expect(missing).toEqual([]);
  });
});

describe("WP-TASK-CALENDAR-ENGINE-01: what the reader is shown", () => {
  beforeEach(bootApi);

  it("a requester's plan refusal reads as its own sentence, not the generic 403", () => {
    const message = global.TasksApi.failureMessage({ ok: false, status: 403, reasonCode: "TASK_PLAN_NOT_HOLDER" });
    expect(message).toBe("errorPlanNotHolder");
    expect(message).not.toBe("errorNoAccess");
  });

  it("a block conflict is a RULE, not a lost race", () => {
    const result = { ok: false, status: 409, reasonCode: "TASK_PLAN_CONFLICT" };
    expect(global.TasksApi.isTransitionBlocked(result)).toBe(true);
    expect(global.TasksApi.isConcurrencyConflict(result)).toBe(false);
    expect(global.TasksApi.failureMessage(result)).toBe("errorPlanConflict");
  });

  it("an unplan with nothing to take back is a rule too", () => {
    const result = { ok: false, status: 409, reasonCode: "TASK_UNPLAN_NOT_ALLOWED" };
    expect(global.TasksApi.isTransitionBlocked(result)).toBe(true);
    expect(global.TasksApi.failureMessage(result)).toBe("errorUnplanNotAllowed");
  });

  it("the meeting warning names the meeting", () => {
    global.TasksL10n = { t: (key) => (key === "warningPlanOverlapsMeeting" ? "Toplantı: {0}" : key) };
    expect(global.TasksApi.planWarningMessage({ code: "TASK_PLAN_OVERLAPS_MEETING", title: "Haftalık kalite" }))
      .toBe("Toplantı: Haftalık kalite");
  });

  it("an unknown warning is loud, not silent", () => {
    const warnings = [];
    const original = global.console.warn;
    global.console.warn = (m) => warnings.push(String(m));
    try {
      expect(global.TasksApi.planWarningMessage({ code: "TASK_PLAN_SOMETHING_NEW" })).toBeNull();
      expect(warnings.join(" ")).toContain("TASK_PLAN_SOMETHING_NEW");
    } finally {
      global.console.warn = original;
    }
  });
});
