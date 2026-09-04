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
    // "string? Secondary = null" → "Secondary". A default value used to be swallowed whole and the parameter
    // read back as "null", which would have quietly excused any client field named after an optional parameter.
    .map((part) => part.trim().split("=")[0].trim().split(/\s+/).pop())
    .filter(Boolean)
    .map((name) => name.charAt(0).toLowerCase() + name.slice(1));
};

/** The keys the client's declared builder emits for one action. */
const clientFields = (actionCode) => {
  const source = fs.readFileSync(APP_JS, "utf8");
  const start = source.indexOf("const TRANSITION_BODIES = {");
  expect(start, "TRANSITION_BODIES is not declared in app.js").toBeGreaterThan(-1);
  const map = source.slice(start, source.indexOf("};", start));

  /*
   * ⚠ THE ENTRY, NOT THE LINE. This used to read one line per action, which quietly made "a builder must fit on
   * one line" a rule of this codebase — and it broke the moment `inquire` grew a second parameter and wrapped.
   * The formatting of the map is not what this guard is for.
   *
   * Read from the action's key to the start of the NEXT key (or the end of the map), so a builder may span as
   * many lines as it needs.
   */
  const keyAt = new RegExp(`(?:^|\\n)\\s*${actionCode}:`).exec(map);
  expect(keyAt, `${actionCode} has no entry in TRANSITION_BODIES`).toBeTruthy();
  const rest = map.slice(keyAt.index + keyAt[0].length);
  const nextKey = /\n\s*(?:[A-Za-z_$][\w$]*|__default):/.exec(rest);
  const entry = (nextKey ? rest.slice(0, nextKey.index) : rest)
    // Comments stripped FIRST: a `//` line explaining the NEXT entry sits after this one's literal, and the
    // end-anchored match below would otherwise never reach the literal at all.
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .replace(/(^|[^:])\/\/.*$/gm, "$1")
    .trim();

  // The object literal the builder returns: `({ expectedVersion, assigneeUserId, reason })`
  // Anchored at the END so it takes the RETURNED literal, not the parameter destructure, and tolerant of the
  // trailing comma every entry in the map carries.
  const body = /\(\{([^}]*)\}\),?\s*$/.exec(entry);
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
     *
     * ⚠ READ THROUGH `clientFields`, NOT OFF ONE LINE. This used to be a one-line regex match on the
     * `__default:` key, which made "the
     * fallback builder fits on a single line" an unwritten rule of app.js — the same mistake the entry reader
     * above already had to correct once. The builder wraps the moment it takes a second parameter, and it now
     * does.
     */
    const fields = new Set(clientFields("__default"));
    expect(fields).toEqual(new Set(serverFields("TaskTransitionRequest")));
  });

  it("sends a real closure outcome instead of the hard-coded null it shipped with", () => {
    /*
     * ⚠ THE REGRESSION THIS FILE EXISTS TO STOP, IN ITS SECOND FORM.
     *
     * The generic builder read `({ expectedVersion, reason }) => ({ expectedVersion, reasonCode: null, … })`.
     * The FIELD was right — the guard above was green throughout — and the VALUE was a literal `null`, so
     * `TaskItem.ClosureReasonCode` was written null on every close since the engine shipped. Field-name
     * agreement is not payload agreement, and that gap is exactly wide enough to hide an empty column for
     * months.
     *
     * So this asserts the value's SOURCE: the builder must take an outcome from its caller and pass it through.
     */
    const source = fs.readFileSync(APP_JS, "utf8");
    const start = source.indexOf("const TRANSITION_BODIES = {");
    const map = source.slice(start, source.indexOf("};", start));
    const fallback = map.slice(map.indexOf("__default:"));

    expect(fallback, "the fallback builder no longer accepts an outcome").toContain("outcomeCode");
    expect(fallback, "reasonCode is a hard-coded null again — nothing will ever be recorded")
      .not.toMatch(/reasonCode:\s*null/);
    expect(fallback).toMatch(/reasonCode:\s*outcomeCode/);
  });

  it("carries the chosen outcome all the way from the dialog to the request", () => {
    /*
     * The other half of the same defect: a builder that accepts an outcome is useless if no call site passes
     * one. Each hop is named, because a break in ANY of them restores the empty column silently.
     */
    const source = fs.readFileSync(APP_JS, "utf8");
    expect(source, "the dialog no longer hands the outcome to applyAction")
      .toMatch(/applyAction\(item, action, res\.value\.reason, undefined, undefined, res\.value\.outcomeCode\)/);
    expect(source, "applyAction no longer forwards the outcome")
      .toMatch(/submitRealTransition\(item, action, reason, assigneeUserId, waitingOnUserId, outcomeCode\)/);
    expect(source, "the body builder is no longer given the outcome").toMatch(/outcomeCode \}\)\);/);
  });

  it("only asks for an outcome when the task's TYPE offers one", () => {
    /*
     * BACKWARD COMPATIBILITY, asserted rather than promised. A hundred-odd tasks are open against types with no
     * dictionary; if the picker ever became unconditional, every one of them would meet a required field that
     * has no rows to choose from and could not be closed at all.
     */
    const source = fs.readFileSync(APP_JS, "utf8");
    expect(source).toContain("const closureOutcomes = closureOutcomesFor(item, action);");
    expect(source, "the picker stopped being conditional").toContain("if (closureOutcomes.length) {");
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

/*
 * ══ BL-050 — the same two-sided method, applied to LOOKUP responses ═══════════════════════════════════════════
 *
 * The transition-body checks above exist because BL-043 drifted between two files that never mentioned each
 * other. BL-050 is the SAME defect one layer over: the reassign picker read `person.id`, and
 * AssignablePersonDto(Guid UserId, …) serialises `userId` — there is no `id`. Every <option> got value="",
 * validation refused every choice, and no request was ever made. The name looked right because `displayName`
 * was read correctly, which is exactly what hid it.
 *
 * The transition tests could not have caught this: they read REQUEST bodies, and this is a RESPONSE field. So the
 * method is extended rather than the list of cases — the server record's fields and the client's read sites are
 * both parsed from source and compared, with nothing restated here.
 */
/**
 * Every place a lookup row's field becomes an <option> VALUE — which is precisely where BL-050 broke and
 * precisely what a broad `person.X` sweep cannot see: the same files read unrelated `person.name` from fixtures
 * and use one `row` name for two different lookups. The option value is the field that must exist, because an
 * `undefined` there renders as "" and the control silently accepts nothing.
 */
const OPTION_VALUE_READS = (file) => {
  const source = fs.readFileSync(path.join(repoRoot, file), "utf8");
  return source
    .split("\n")
    .filter((line) => /option\.value\s*=|<option value=/.test(line))
    .flatMap((line) =>
      [...line.matchAll(/\b(?:person|row|item|choice|p)\.([A-Za-z][A-Za-z0-9]*)\b/g)].map((m) => m[1]));
};

const LOOKUP_READERS = [
  ["AssignablePersonDto", "frontend/Diten.Web/wwwroot/assets/js/WorkCenterNext/app.js", "userId"],
  ["AssignablePersonDto", "frontend/Diten.Web/wwwroot/assets/js/Tasks/form.js", "userId"],
  ["AssignablePositionDto", "frontend/Diten.Web/wwwroot/assets/js/Tasks/form.js", "positionId"],
  // Phase 5 — a configurable field's option list is a THIRD lookup whose rows become <option> values, so it
  // joins the same guard rather than being trusted because it is new.
  ["TaskFieldOptionDto", "frontend/Diten.Web/wwwroot/assets/js/Tasks/form.js", "value"]
];

describe("lookup responses are read by the field the server actually sends", () => {
  it.each([
    ["AssignablePersonDto", "userId"],
    ["AssignablePositionDto", "positionId"],
    ["TaskFieldOptionDto", "value"]
  ])(
    "%s declares the id field its clients use", (recordName, idField) => {
      // The anchor: rename the DTO field and this fails before any client assertion can mislead.
      expect(serverFields(recordName)).toContain(idField);
    });

  it("no <option> takes its value from a field no lookup DTO declares", () => {
    /*
     * THE BL-050 regression, stated generally rather than as "person.id is wrong". `person.id` named nothing;
     * `undefined` inside a template literal renders as the empty string instead of throwing, so every option had
     * value="", validation refused every choice, and no request was ever made. The displayed NAME was correct,
     * which is what kept it invisible.
     */
    const declared = new Set([
      ...serverFields("AssignablePersonDto"),
      ...serverFields("AssignablePositionDto"),
      // A configurable field's options are resolved server-side and flattened to {value,label}; the control
      // renders `choice.value`, so that field has to be one the record actually declares.
      ...serverFields("TaskFieldOptionDto")
    ]);

    [...new Set(LOOKUP_READERS.map(([, file]) => file))].forEach((file) => {
      const invented = [...new Set(OPTION_VALUE_READS(file))].filter((name) => !declared.has(name));
      expect(invented, `${file} builds an <option> value from ${invented.join("/")}, which no lookup DTO declares`)
        .toEqual([]);
    });
  });

  it("actually inspects some option values, so an empty sweep cannot pass", () => {
    // Non-vacuity: a regex that matched nothing would satisfy the assertion above forever.
    [...new Set(LOOKUP_READERS.map(([, file]) => file))].forEach((file) => {
      expect(OPTION_VALUE_READS(file).length, `${file}: the option-value scan found nothing to check`)
        .toBeGreaterThan(0);
    });
  });

  /*
   * The same two-sided method for the two records this round added to the wire. Neither becomes an <option>
   * through a line the scan above can see — one is read inside a label formatter, the other inside a helper that
   * builds the element — so the read sites are named here instead of guessed at. What is NOT restated is either
   * record's field list: both come from TaskModels.cs.
   */
  it.each([
    /*
     * A record's option carries a THIRD field beyond value/label: the disambiguating line that holds the
     * business key. It is the field that keeps a GUID off the screen while still telling the reader which
     * record they picked, so a rename on the server must fail here rather than as a picker of bare names.
     */
    [
      "TaskFieldOptionDto",
      "frontend/Diten.Web/wwwroot/assets/js/Tasks/form.js",
      /\bchoice\.([A-Za-z][A-Za-z0-9]*)\b/g
    ],
    /*
     * The administrator's source chooser. Its <option> values are source KEYS, and a key read from a field the
     * server does not send would render the whole list unselectable — BL-050 moved to the admin screen.
     */
    [
      "TaskFieldOptionSourceDto",
      "frontend/Diten.Web/wwwroot/assets/js/Tasks/FieldDefinitions/form.js",
      /\bsource\.([A-Za-z][A-Za-z0-9]*)\b/g
    ]
  ])("%s is read only by fields it declares", (recordName, file, pattern) => {
    const source = fs.readFileSync(path.join(repoRoot, file), "utf8")
      // Comments strip first: these files DOCUMENT the shapes they consume, and an assertion that cannot tell
      // prose from code would forbid explaining the contract it guards.
      .replace(/\/\*[\s\S]*?\*\//g, "")
      .replace(/\/\/[^\n]*/g, "");

    // PascalCase spellings are the same field: this repo's payloads have arrived both ways, and the readers
    // accept either. The claim is about which FIELD is read, not about its casing.
    const declared = new Set(serverFields(recordName));
    const read = [...new Set([...source.matchAll(pattern)].map((m) => m[1]))];

    // Non-vacuity: a pattern that matched nothing would satisfy the assertion below forever.
    expect(read.length, `${file}: the ${recordName} read scan found nothing to check`).toBeGreaterThan(0);

    const invented = read.filter(
      (name) => !declared.has(name) && !declared.has(name.charAt(0).toLowerCase() + name.slice(1)));
    expect(invented, `${file} reads ${invented.join("/")}, which ${recordName} does not declare`).toEqual([]);
  });

  it("reads a person's id from ONE place in app.js, not three across the repo", () => {
    /*
     * Why a single reader rather than three correct reads: the repo already HAD the right answer written twice
     * (app.js's `person.userId || person.id` and form.js's `row.userId`) and still shipped the wrong one a third
     * time. Three spellings of one fact is the condition that produced BL-050; one is the fix.
     *
     * The `|| person.id` fallback goes with them — a defensive read of a field that does not exist is what made
     * `person.id` look plausible in the first place.
     */
    const raw = fs.readFileSync(APP_JS, "utf8");
    // Comments stripped first: this file DOCUMENTS the old spellings so the next reader knows what was wrong,
    // and an assertion that cannot tell prose from code would forbid explaining the defect it guards.
    const app = raw.replace(/\/\*[\s\S]*?\*\//g, "").replace(/\/\/[^\n]*/g, "");

    expect(app, "app.js has no single person-id reader").toMatch(/const personUserId\s*=/);
    expect(app, "app.js still falls back to the non-existent person.id").not.toContain("person.userId || person.id");
    expect(app, "app.js still reads person.id directly").not.toMatch(/\bperson\.id\b/);
  });
});
