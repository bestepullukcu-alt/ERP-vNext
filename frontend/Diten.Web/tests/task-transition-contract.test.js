const fs = require("fs");
const path = require("path");

/*
 * BL-043 — the client's transition body and the server's DTO, checked against EACH OTHER.
 *
 * The client posted one generic body to all ten transitions: {expectedVersion, reasonCode, note}. Three endpoints
 * ask for something else — InquireTaskItemRequest(ExpectedVersion, Reason), ReturnTaskItemRequest(same),
 * ReassignTaskItemRequest(ExpectedVersion, AssigneeUserId, Reason) — so all three answered
 * 400 "The Reason field is required." and had never once worked from the UI. The Waiting segment could not be
 * filled by any route, and the return and reassign flows were entirely dead.
 *
 * Nothing declared the disagreement, which is why it survived: the client's shape lived in app.js, the server's in
 * TaskModels.cs, and no artefact mentioned both. This test IS that artefact. It parses the real C# records and the
 * real client map — neither is restated here — so a DTO that gains or renames a field fails here rather than in a
 * user's click.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const APP_JS = path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "js", "WorkCenterNext", "app.js");
const TASK_MODELS = path.join(
  repoRoot, "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features", "Tasks", "TaskModels.cs");

/** Field names of a `public sealed record X(...)` in TaskModels.cs, camelCased to the JSON the client sends. */
const serverFields = (recordName) => {
  const source = fs.readFileSync(TASK_MODELS, "utf8");
  const match = new RegExp(`public sealed record ${recordName}\\(([^)]*)\\)`).exec(source);
  expect(match, `${recordName} not found in TaskModels.cs`).toBeTruthy();

  return match[1]
    .split(",")
    .map((part) => part.trim().split(/\s+/).pop())     // "int ExpectedVersion" → "ExpectedVersion"
    .filter(Boolean)
    .map((name) => name.charAt(0).toLowerCase() + name.slice(1));
};

/** The keys the client's declared builder emits for one action. */
const clientFields = (actionCode) => {
  const source = fs.readFileSync(APP_JS, "utf8");
  const start = source.indexOf("const TRANSITION_BODIES = {");
  expect(start, "TRANSITION_BODIES is not declared in app.js").toBeGreaterThan(-1);
  const map = source.slice(start, source.indexOf("};", start));

  const line = new RegExp(`(?:^|\\n)\\s*${actionCode}:[^\\n]*`).exec(map);
  expect(line, `${actionCode} has no entry in TRANSITION_BODIES`).toBeTruthy();

  // The object literal the builder returns: `({ expectedVersion, assigneeUserId, reason })`
  // Anchored at the END so it takes the RETURNED literal, not the parameter destructure, and tolerant of the
  // trailing comma every entry in the map carries.
  const body = /\(\{([^}]*)\}\),?\s*$/.exec(line[0].trim());
  expect(body, `${actionCode}'s builder does not return an object literal`).toBeTruthy();
  return body[1].split(",").map((part) => part.trim().split(":")[0].trim()).filter(Boolean);
};

describe("every transition sends exactly what its endpoint declares", () => {
  const PAIRS = [
    ["inquire", "InquireTaskItemRequest"],
    ["return", "ReturnTaskItemRequest"],
    ["reassign", "ReassignTaskItemRequest"]
  ];

  it.each(PAIRS)("%s matches %s field for field", (actionCode, recordName) => {
    // Set comparison, not order: JSON binding does not care about order, and pinning it would fail on a harmless
    // reshuffle while missing the thing that actually broke — a field that is absent or differently named.
    expect(new Set(clientFields(actionCode))).toEqual(new Set(serverFields(recordName)));
  });

  it("sends `reason`, which is the field the server requires", () => {
    /*
     * The specific regression. `reasonCode: null` + `note` was the old body; the server never binds either to
     * Reason, so Reason arrived null and model binding refused the whole request.
     */
    PAIRS.forEach(([actionCode]) => {
      const fields = clientFields(actionCode);
      expect(fields, `${actionCode} does not send reason`).toContain("reason");
      expect(fields, `${actionCode} still sends the old generic reasonCode`).not.toContain("reasonCode");
      expect(fields, `${actionCode} still sends the old generic note`).not.toContain("note");
    });
  });

  it("names the person for reassign, which is why the field-name fix alone was not enough", () => {
    // Even with `reason` correct, reassign fails without AssigneeUserId — the dialog never asked for it.
    expect(clientFields("reassign")).toContain("assigneeUserId");
  });

  it("leaves the other transitions on the generic body they correctly use", () => {
    /*
     * Non-vacuity in the other direction: the generic body was never wrong for the seven endpoints that take
     * TaskTransitionRequest(ExpectedVersion, ReasonCode, Note). "Fix the three" must not become "change all ten".
     */
    const source = fs.readFileSync(APP_JS, "utf8");
    const map = source.slice(source.indexOf("const TRANSITION_BODIES = {"), source.indexOf("};", source.indexOf("const TRANSITION_BODIES = {")));

    expect(map).toContain("__default");
    const fallback = /__default:[^\n]*/.exec(map)[0];
    expect(fallback).toContain("reasonCode");
    expect(fallback).toContain("note");
  });
});

describe("the dialog cannot submit something the server will refuse", () => {
  const app = () => fs.readFileSync(APP_JS, "utf8");

  it("declares which actions require a reason and which require an assignee", () => {
    expect(app()).toContain("REASON_REQUIRED_ACTIONS");
    expect(app()).toContain("ASSIGNEE_REQUIRED_ACTIONS");
  });

  it("refuses to confirm a reassign with no person chosen", () => {
    // A dialog that can be confirmed into a guaranteed 400 is the same defect wearing a different hat.
    expect(app()).toContain("ReassignAssigneeRequired");
    expect(app()).toMatch(/if \(!assigneeUserId\) \{ global\.Swal\.showValidationMessage/);
  });

  it("offers the SAME people the create form offers, because that is what the server accepts", () => {
    expect(app()).toMatch(/ASSIGNEE_REQUIRED_ACTIONS[\s\S]{0,4000}?TasksApi\.assignablePeople\(\)/);
  });
});

describe("the four new strings exist in all seven tenant languages", () => {
  const LOCALES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
  const KEYS = [
    "ReassignAssigneeLabel",
    "ReassignAssigneePlaceholder",
    "ReassignAssigneeRequired",
    "ReassignNoAssignableUsers"
  ];

  const resx = (locale) => fs.readFileSync(
    path.join(repoRoot, "frontend", "Diten.Web", "Resources", "Views", "WorkCenterNext",
      `WorkCenterNextIndex.${locale}.resx`), "utf8");

  const value = (locale, key) => {
    const match = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(resx(locale));
    return match ? match[1].trim() : null;
  };

  it.each(LOCALES)("%s defines all four", (locale) => {
    KEYS.forEach((key) => expect(value(locale, key), `${key} missing in ${locale}`).toBeTruthy());
  });

  it("translates them rather than leaving English in place", () => {
    // The l10n gate: a reader seeing a language they did not choose concludes the system is broken.
    KEYS.forEach((key) => {
      const english = value("en", key);
      LOCALES.filter((l) => l !== "en").forEach((locale) => {
        expect(value(locale, key), `${key}/${locale} is still the English text`).not.toBe(english);
      });
    });
  });
});
