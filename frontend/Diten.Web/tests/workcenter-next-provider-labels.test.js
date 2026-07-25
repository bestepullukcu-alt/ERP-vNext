const fs = require("fs");
const path = require("path");

/*
 * Every resource label a work-item provider emits must exist in all seven WorkCenterNext resx files.
 *
 * `resolveLabel` falls back to the key itself when a lookup misses, so a missing entry does not throw — it
 * renders "WorkAggregation_Action_Start" on screen. That is how MOD-0024's action names shipped untranslated
 * while the DISABLED-reason keys (added earlier) resolved correctly.
 *
 * The key list is read from the PROVIDER SOURCE, never hand-maintained here: a hardcoded list in the test would
 * drift from the code exactly like the resx did. Dynamic keys (prefix + enum value) are expanded by reading the
 * enum members from their own source file too.
 */
const REPO = path.resolve(__dirname, "..", "..", "..");
const RESX_DIR = path.resolve(__dirname, "..", "Resources", "Views", "WorkCenterNext");
const LOCALES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

const PROVIDER_SOURCES = [
  "services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Providers/TaskWorkItemProvider.cs",
  "services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/Services/WorkItemProjectionService.cs"
];

// prefix constant in the provider → the enum whose members complete the key
const DYNAMIC_KEYS = [
  {
    prefixConstant: "NativeStatusKeyPrefix",
    source: "services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Providers/TaskWorkItemProvider.cs",
    enumFile: "services/Diten.Platform/src/Diten.Platform.Domain/Enums/Tasks/TaskEnums.cs",
    enumName: "TaskLifecycle"
  },
  {
    prefixConstant: "NativeStatusKeyPrefix",
    source: "services/Diten.Platform/src/Diten.Platform.Application/Features/WorkAggregation/Services/WorkItemProjectionService.cs",
    enumFile: "services/Diten.Platform/src/Diten.Platform.Domain/Enums/Workflow/ApprovalTaskStatus.cs",
    enumName: "ApprovalTaskStatus"
  }
];

const readRepo = (relative) => fs.readFileSync(path.join(REPO, relative), "utf8");

// C# comments quote keys while explaining them; a scanner that reads prose reports phantom keys.
const stripCsComments = (source) => source
  .replace(/\/\*[\s\S]*?\*\//g, "")
  .replace(/^\s*\/\/.*$/gm, "");

const enumMembers = (relative, enumName) => {
  const source = stripCsComments(readRepo(relative));
  const body = new RegExp(`enum\\s+${enumName}\\s*\\{([^}]*)\\}`).exec(source);
  if (!body) { return []; }
  return body[1]
    .split(",")
    .map((entry) => entry.split("=")[0].trim())
    .filter((name) => /^[A-Za-z_][A-Za-z0-9_]*$/.test(name));
};

const staticKeys = () => {
  const keys = new Set();
  PROVIDER_SOURCES.forEach((relative) => {
    const source = stripCsComments(readRepo(relative));
    const literal = /"(WorkAggregation_[A-Za-z][A-Za-z0-9_]*)"/g;
    let match;
    while ((match = literal.exec(source)) !== null) {
      // A bare prefix (…_TaskStatus_) is completed dynamically; it is not a key on its own.
      if (!match[1].endsWith("_")) { keys.add(match[1]); }
    }
  });
  return keys;
};

const dynamicKeys = () => {
  const keys = new Set();
  DYNAMIC_KEYS.forEach(({ prefixConstant, source, enumFile, enumName }) => {
    const providerSource = stripCsComments(readRepo(source));
    const prefix = new RegExp(`${prefixConstant}\\s*=\\s*"([^"]+)"`).exec(providerSource);
    if (!prefix) { return; }
    enumMembers(enumFile, enumName).forEach((member) => keys.add(prefix[1] + member));
  });
  return keys;
};

const resxKeys = (locale) => {
  const xml = fs.readFileSync(path.join(RESX_DIR, `WorkCenterNextIndex.${locale}.resx`), "utf8");
  const keys = new Set();
  const entry = /<data name="([^"]+)"/g;
  let match;
  while ((match = entry.exec(xml)) !== null) { keys.add(match[1]); }
  return keys;
};

describe("provider resource labels are translated", () => {
  let byLocale;
  let expected;

  beforeAll(() => {
    byLocale = Object.fromEntries(LOCALES.map((locale) => [locale, resxKeys(locale)]));
    expected = [...new Set([...staticKeys(), ...dynamicKeys()])].sort();
  });

  it("reads real keys out of the provider source (guards against a vacuous scan)", () => {
    // Sanity: the scan must find the keys we know both providers emit.
    expect(expected).toContain("WorkAggregation_Action_Start");
    expect(expected).toContain("WorkAggregation_Action_Approve");
    expect(expected).toContain("WorkAggregation_ActionDisabled_PermissionDenied");
    expect(expected.length).toBeGreaterThan(15);
  });

  it("expands dynamic status keys from the enums, not from a hand-written list", () => {
    // MOD-0024 lifecycles…
    expect(expected).toContain("WorkAggregation_TaskStatus_InProgress");
    expect(expected).toContain("WorkAggregation_TaskStatus_PendingReview");
    // …and MOD-0023 approval statuses.
    expect(expected).toContain("WorkAggregation_NativeStatus_WaitingApproval");
    expect(expected).toContain("WorkAggregation_NativeStatus_TimedOut");
  });

  it("does not treat a bare prefix as a key", () => {
    expect(expected.filter((key) => key.endsWith("_"))).toEqual([]);
  });

  it("has every emitted key in all 7 languages", () => {
    const missing = [];
    expected.forEach((key) => {
      LOCALES.forEach((locale) => {
        if (!byLocale[locale].has(key)) { missing.push(`${locale}/${key}`); }
      });
    });
    expect(missing).toEqual([]);
  });

  it("keeps action names as translatable resource keys, not raw display text", () => {
    // Deliberate contrast with the TITLE, which is a display label because the user typed it. "Start"/"Başlat"
    // is system vocabulary and must localize.
    const actionKeys = expected.filter((key) => key.startsWith("WorkAggregation_Action_"));
    expect(actionKeys.length).toBeGreaterThanOrEqual(8);

    const en = fs.readFileSync(path.join(RESX_DIR, "WorkCenterNextIndex.en.resx"), "utf8");
    const tr = fs.readFileSync(path.join(RESX_DIR, "WorkCenterNextIndex.tr.resx"), "utf8");
    const valueOf = (xml, key) => {
      const match = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(xml);
      return match ? match[1].trim() : null;
    };

    // Turkish and English must actually differ for these — an identical value means a forgotten translation.
    const untranslated = actionKeys.filter((key) => valueOf(en, key) === valueOf(tr, key));
    expect(untranslated).toEqual([]);
  });

  it("matches the wording the shell already uses for the same actions", () => {
    // The Task Center renders its own ActStart/ActClaim buttons too; one action must not read two ways.
    const tr = fs.readFileSync(path.join(RESX_DIR, "WorkCenterNextIndex.tr.resx"), "utf8");
    const valueOf = (key) => {
      const match = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(tr);
      return match ? match[1].trim() : null;
    };

    expect(valueOf("WorkAggregation_Action_Start")).toBe(valueOf("ActStart"));
    expect(valueOf("WorkAggregation_Action_Claim")).toBe(valueOf("ActClaim"));
    expect(valueOf("WorkAggregation_Action_Complete")).toBe(valueOf("ActComplete"));
    expect(valueOf("WorkAggregation_Action_Accept")).toBe(valueOf("ActAccept"));
    expect(valueOf("WorkAggregation_Action_Plan")).toBe(valueOf("ActPlan"));
    expect(valueOf("WorkAggregation_Action_Release")).toBe(valueOf("ActRelease"));

    // Cancel is deliberately NOT ActCancel/ReasonCancel: "İptal" is the dialog-dismiss button, whereas this
    // action cancels the TASK. Same word, different act — so it gets its own, unambiguous wording.
    expect(valueOf("WorkAggregation_Action_Cancel")).not.toBe(valueOf("ReasonCancel"));
  });
});
