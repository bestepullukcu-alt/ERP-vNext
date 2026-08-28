const fs = require("fs");
const path = require("path");

/*
 * The 409 SPLIT, and the code→resx bridge behind it.
 *
 * A blocked workflow transition and a lost concurrency race are both 409. The Task Center used to route every 409
 * to "someone changed it first, the screen was refreshed" — a confident, believable lie when the real reason was
 * "your approver has not decided yet". Nothing was overwritten and refreshing changes nothing, so the user is told
 * to do the one thing that cannot help.
 *
 * These tests pin: which 409s are races, which are rules, and that every mapped reason code has a translated
 * message in all seven tenant languages (an unmapped code must degrade loudly, never silently).
 */
const LOCALES = ["en", "fr", "es", "zh", "ar", "ru", "tr"];
const RESX_DIR = path.resolve(__dirname, "..", "Resources", "Views", "Tasks");
const API_JS = path.resolve(__dirname, "..", "wwwroot", "assets", "js", "Tasks", "api.js");
const BRIDGE = path.resolve(__dirname, "..", "Views", "Tasks", "_IndexL10n.cshtml");

// api.js is an IIFE that attaches itself to window/globalThis, so it is loaded the same way the other suites load
// it rather than re-implemented here.
const { loadScript } = require("./load-script");

const loadApi = () => {
  loadScript("wwwroot/assets/js/Tasks/api.js");
  return globalThis.window ? globalThis.window.TasksApi : globalThis.TasksApi;
};

const resxKeys = (locale) => {
  const xml = fs.readFileSync(path.join(RESX_DIR, `TasksIndex.${locale}.resx`), "utf8");
  const keys = {};
  const pattern = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
  let match;
  while ((match = pattern.exec(xml)) !== null) { keys[match[1]] = match[2]; }
  return keys;
};

// The bridge is a hand-listed anonymous object serialized through MVC's camelCase policy, so a resx key that is
// not listed there never reaches JS at all — the message would fall back to the generic error.
const bridgeExposedKeys = () => {
  const source = fs.readFileSync(BRIDGE, "utf8").replace(/\/\/[^\n]*/g, "");
  const keys = new Set();
  const pattern = /([A-Za-z0-9_]+)\s*=\s*Localizer\["([^"]+)"\]/g;
  let match;
  while ((match = pattern.exec(source)) !== null) { keys.add(match[2]); }
  return keys;
};

const pascal = (camel) => camel.charAt(0).toUpperCase() + camel.slice(1);

describe("MOD-0024 blocked-transition routing", () => {
  const api = loadApi();

  it("treats a workflow block as a RULE, not a race", () => {
    const blocked = { status: 409, reasonCode: "WORKFLOW_PENDING_APPROVAL" };

    expect(api.isTransitionBlocked(blocked)).toBe(true);
    expect(api.isConcurrencyConflict(blocked)).toBe(false);
  });

  it("still treats the concurrency code as a race", () => {
    const race = { status: 409, reasonCode: "TASK_CONCURRENCY_CONFLICT" };

    expect(api.isConcurrencyConflict(race)).toBe(true);
    expect(api.isTransitionBlocked(race)).toBe(false);
  });

  it("reads a bare 409 with no reason code as a race, which is the only honest reading of it", () => {
    expect(api.isConcurrencyConflict({ status: 409, reasonCode: null })).toBe(true);
  });

  it.each([
    "APPROVAL_PENDING",
    "CHECKLIST_INCOMPLETE",
    "WORKFLOW_PENDING_APPROVAL",
    "WORKFLOW_WAITING_EVIDENCE",
    "WORKFLOW_REJECTED",
    "WORKFLOW_CANCELLED",
    "WORKFLOW_NOT_TERMINAL_APPROVED",
    "WorkflowGateEvaluationFailed"
  ])("routes %s to the blocked branch, never to the concurrency message", (reasonCode) => {
    expect(api.isTransitionBlocked({ status: 409, reasonCode })).toBe(true);
    expect(api.isConcurrencyConflict({ status: 409, reasonCode })).toBe(false);
  });

  it("warns instead of silently mislabelling an UNKNOWN 409 code", () => {
    // A code the server grows later must surface in the console rather than being folded into either branch.
    const warnings = [];
    const originalWarn = console.warn;
    console.warn = (message) => warnings.push(message);
    try {
      const result = { status: 409, reasonCode: "SOME_FUTURE_CODE" };
      expect(api.isConcurrencyConflict(result)).toBe(false);
      expect(api.isTransitionBlocked(result)).toBe(false);
    } finally {
      console.warn = originalWarn;
    }

    expect(warnings.join(" ")).toContain("SOME_FUTURE_CODE");
  });

  it("every blocking reason code has a message key (no blocking code falls back to the generic error)", () => {
    const unmapped = [...api.BLOCKING_REASON_CODES].filter((code) => !api.REASON_CODE_MESSAGE_KEYS[code]);
    expect(unmapped).toEqual([]);
  });

  describe("the code→resx bridge", () => {
    const messageKeys = [...new Set(Object.values(loadApi().REASON_CODE_MESSAGE_KEYS))];

    it("maps at least the Phase 3 approval codes", () => {
      // Guards against a vacuous pass if the map is ever emptied.
      expect(messageKeys.length).toBeGreaterThanOrEqual(10);
    });

    it.each(LOCALES)("has every mapped message key translated in %s", (locale) => {
      const entries = resxKeys(locale);
      const missing = messageKeys.filter((key) => !(pascal(key) in entries));
      expect(missing).toEqual([]);
    });

    it("has no mapped message key left empty in any language", () => {
      LOCALES.forEach((locale) => {
        const entries = resxKeys(locale);
        messageKeys.forEach((key) => {
          expect((entries[pascal(key)] ?? "").trim().length).toBeGreaterThan(0);
        });
      });
    });

    it("exposes every mapped message key through the l10n bridge, or JS never sees it", () => {
      const exposed = bridgeExposedKeys();
      const notBridged = messageKeys.filter((key) => !exposed.has(pascal(key)));
      expect(notBridged).toEqual([]);
    });

    it("does not leave a non-English file carrying the English text for the new approval messages", () => {
      const en = resxKeys("en");
      const approvalKeys = messageKeys.map(pascal).filter((key) => key.startsWith("ErrorApproval"));
      expect(approvalKeys.length).toBeGreaterThan(0);

      LOCALES.filter((locale) => locale !== "en").forEach((locale) => {
        const entries = resxKeys(locale);
        approvalKeys.forEach((key) => {
          expect(entries[key]).not.toBe(en[key]);
        });
      });
    });
  });
});
