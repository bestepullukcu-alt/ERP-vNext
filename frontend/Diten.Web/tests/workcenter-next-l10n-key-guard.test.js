const fs = require("fs");
const path = require("path");

/*
 * ══ EVERY KEY THE TASK CENTER ASKS FOR MUST EXIST ═════════════════════════════════════════════════════════
 *
 * THE DEFECT THIS REPLACES A HUMAN FOR. `t('ErrorCommentTextInvalid')` (app.js) had no entry in any of the
 * seven WorkCenterNextIndex resx files, so a user who tried to save an empty comment was shown the raw key —
 * in all seven languages. It was found by reading the file by hand. This test is that reading, made repeatable.
 *
 * ⚠ THREE TRAPS a hand-measurement fell into, and every one of them is a test below:
 *
 *   (a) COMMENTS. A key quoted inside a comment is not a call. `Tasks/index.l10n.js` explains a casing bug by
 *       writing `t('ErrorOccurred')` in prose; a naive grep reports a defect that does not exist. Everything
 *       here is measured on comment-stripped source, and the stripper has its own test.
 *
 *   (b) FAMILIES. `t('AuditEvent' + code…)` is not a key, it is sixteen keys. Skipping it silently would let
 *       the guard go quiet exactly where the risk is highest — a new activity code with no sentence behind it.
 *       Each family is checked against its DOMAIN, parsed from the source of truth (the executable contract,
 *       or the C# enum), and an UNDECLARED family fails the test rather than being ignored.
 *
 *   (c) TWO BRIDGES. `quick-create.js` sits in the WorkCenterNext folder and resolves through the TASKS
 *       payload — camelCase keys from a different resx. Checking its seven keys against the WorkCenterNext
 *       resx would report seven defects where there are none. Each file is classified by READING which
 *       bridge it binds to, and a file whose binding cannot be determined fails.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const WCN_JS = path.resolve(__dirname, "..", "wwwroot", "assets", "js", "WorkCenterNext");
const WCN_RESX = (locale) =>
  path.resolve(__dirname, "..", "Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${locale}.resx`);
const TASKS_RESX = path.resolve(__dirname, "..", "Resources", "Views", "Tasks", "TasksIndex.en.resx");
const TASKS_BRIDGE = path.resolve(__dirname, "..", "Views", "Tasks", "_IndexL10n.cshtml");

const LOCALES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

/*
 * The six SharedResource keys the WorkCenterNext bridge copies in on top of the module resx
 * (Views/WorkCenterNext/_L10n.cshtml). It copies these SIX and no more — the rest of SharedResource never
 * reaches this page, so "it is in SharedResource" is not an answer here.
 */
const SHARED_KEYS_ON_THE_PAGE = ["DtInfo", "DtInfoEmpty", "DtInfoFiltered", "DtEmptyTable", "DtNoRecords", "DtZeroRecords"];

// ── source readers ────────────────────────────────────────────────────────────

/**
 * Remove comments while leaving string literals intact. Works for JS and C# alike: both spell comments
 * `//` and `/* *\/` and both have `"` / `'` strings.
 */
const stripComments = (source) => {
  const out = [];
  let i = 0;
  let state = null;
  while (i < source.length) {
    const c = source[i];
    if (state === null) {
      if (c === "/" && source[i + 1] === "*") { state = "block"; i += 2; continue; }
      if (c === "/" && source[i + 1] === "/") { state = "line"; i += 2; continue; }
      if (c === '"' || c === "'" || c === "`") { state = c; out.push(c); i += 1; continue; }
      out.push(c); i += 1;
    } else if (state === "block") {
      if (c === "*" && source[i + 1] === "/") { state = null; i += 2; continue; }
      i += 1;
    } else if (state === "line") {
      if (c === "\n") { state = null; out.push("\n"); }
      i += 1;
    } else {
      if (c === "\\") { out.push(source[i], source[i + 1]); i += 2; continue; }
      if (c === state) { state = null; }
      out.push(c); i += 1;
    }
  }
  return out.join("");
};

const read = (file) => fs.readFileSync(file, "utf8");
const code = (file) => stripComments(read(file));

/** The FIRST argument expression of a call whose opening paren ends at `start`. */
const firstArgument = (source, start) => {
  let depth = 0;
  let i = start;
  let quote = null;
  const buf = [];
  while (i < source.length) {
    const c = source[i];
    if (quote) {
      buf.push(c);
      if (c === "\\") { buf.push(source[i + 1]); i += 2; continue; }
      if (c === quote) { quote = null; }
      i += 1; continue;
    }
    if (c === '"' || c === "'" || c === "`") { quote = c; buf.push(c); i += 1; continue; }
    if ("([{".includes(c)) { depth += 1; buf.push(c); i += 1; continue; }
    if (")]}".includes(c)) {
      if (depth === 0) { return buf.join(""); }
      depth -= 1; buf.push(c); i += 1; continue;
    }
    if (c === "," && depth === 0) { return buf.join(""); }
    buf.push(c); i += 1;
  }
  return buf.join("");
};

const resxNames = (file) => {
  const names = new Set();
  const source = read(file);
  const re = /<data\s+name="([^"]+)"/g;
  let m;
  while ((m = re.exec(source)) !== null) { names.add(m[1]); }
  return names;
};

/**
 * Every `t(...)` / `tf(...)` / `tn(...)` call in a file, classified.
 *
 * `keys`      — a full key the file asks for by name (a bare literal, or either arm of a ternary / `||`)
 * `families`  — a `'Prefix' + expression` concatenation: a family of keys, not one key
 * `maps`      — `SOME_MAP[expr]`: the keys are the map's own values, resolved below
 * `fromData`  — the key arrives in the payload (`action.labelKey`, `entry.eventKey`); see the note at its use
 */
const callSites = (source) => {
  const keys = new Set();
  const families = new Set();
  const maps = new Set();
  const fromData = [];
  const re = /(?<![A-Za-z0-9_$.])(?:t|tf|tn)\(/g;
  let m;
  while ((m = re.exec(source)) !== null) {
    const arg = firstArgument(source, m.index + m[0].length).trim();
    const literals = [...arg.matchAll(/'([^']*)'/g)].map((x) => x[1]);

    if (/^'[^']*'$/.test(arg)) { keys.add(literals[0]); continue; }

    const concat = /^'([^']+)'\s*\+/.exec(arg);
    if (concat) { families.add(concat[1]); continue; }
    if (arg.includes("`")) { families.add(arg); continue; }

    const map = /^([A-Z][A-Z0-9_]*)\s*\[/.exec(arg);
    if (map) {
      maps.add(map[1]);
      // `MAP[x] || 'Fallback'` — the fallback is a real key in its own right.
      const fallback = /\|\|\s*'([^']+)'/.exec(arg);
      if (fallback) { keys.add(fallback[1]); }
      continue;
    }

    if (literals.length) {
      /*
       * A ternary or an `||` chain. Both arms are real keys — EXCEPT a literal used as a COMPARISON operand
       * (`key === 'subtasks' ? 'SubtasksLabel' : 'ChecklistLabel'`), which is a value being tested, not a key
       * being asked for. Reporting those was the fourth trap, found while writing this test.
       */
      const comparands = new Set(
        [...arg.matchAll(/(?:[!=]==?\s*'([^']*)')|(?:'([^']*)'\s*[!=]==?)/g)].map((x) => x[1] ?? x[2]));
      literals.filter((lit) => !comparands.has(lit)).forEach((lit) => keys.add(lit));
      continue;
    }

    fromData.push(arg.replace(/\s+/g, " ").slice(0, 60));
  }
  return { keys, families, maps, fromData };
};

/**
 * The PascalCase string VALUES of a `const NAME = { … }` object literal — never its keys.
 *
 * ⚠ `{ 'Pending': 'StatusPending', … }` has quoted PascalCase on BOTH sides, and only the right-hand side is a
 * resource key. Reading both reported four defects that were not there ("Pending", "Done", …) the first time
 * this ran. `SYSSTATE` nests one level (`{ stale: { key: 'BannerStale', … } }`), so a value is taken wherever a
 * colon precedes it, at any depth.
 */
const mapValues = (source, name) => {
  const at = source.indexOf(`const ${name} = `);
  if (at < 0) { return null; }
  const open = source.indexOf("{", at);
  if (open < 0) { return null; }
  let depth = 0;
  let i = open;
  for (; i < source.length; i += 1) {
    if (source[i] === "{") { depth += 1; }
    if (source[i] === "}") { depth -= 1; if (depth === 0) { break; } }
  }
  const body = source.slice(open, i + 1);
  return [...body.matchAll(/:\s*'([A-Z][A-Za-z0-9_]*)'/g)].map((m) => m[1]);
};

// ── which file talks to which bridge ─────────────────────────────────────────

const BRIDGES = { WCN: "workcenternext", TASKS: "tasks", SELF: "the bridge itself" };

/** Read a file's binding rather than assuming it — trap (c). */
const bridgeOf = (file, source) => {
  if (path.basename(file) === "l10n.js") { return BRIDGES.SELF; }
  const decl = /const\s+t\s*=\s*([^;]+);/.exec(source);
  if (!decl) { return null; }
  if (decl[1].includes("TasksL10n")) { return BRIDGES.TASKS; }
  if (decl[1].includes("WCN")) { return BRIDGES.WCN; }
  return null;
};

const jsFiles = fs.readdirSync(WCN_JS).filter((f) => f.endsWith(".js")).map((f) => path.join(WCN_JS, f));

// ── the tests ────────────────────────────────────────────────────────────────

describe("(a) a key quoted in a comment is not a call", () => {
  it("strips block and line comments but keeps strings", () => {
    const sample = [
      "// t('GhostFromALineComment')",
      "/* t('GhostFromABlockComment') */",
      "const real = t('RealKey'); // t('GhostAfterCode')",
      "const notAComment = 'https://example.com/x';"
    ].join("\n");

    const stripped = stripComments(sample);

    expect(stripped).toContain("RealKey");
    expect(stripped).not.toContain("GhostFromALineComment");
    expect(stripped).not.toContain("GhostFromABlockComment");
    expect(stripped).not.toContain("GhostAfterCode");
    // A `//` inside a string is not a comment — stripping it would corrupt the source being measured.
    expect(stripped).toContain("https://example.com/x");
  });

  it("is not vacuous: the repo really does quote keys inside comments", () => {
    // The exact prose that would have produced a phantom defect, in the Tasks bridge's own file.
    const file = path.resolve(__dirname, "..", "wwwroot", "assets", "js", "Tasks", "index.l10n.js");
    expect(read(file)).toContain("t('ErrorOccurred')");
    expect(code(file)).not.toContain("t('ErrorOccurred')");
  });
});

describe("(c) each file is measured against the bridge it actually binds to", () => {
  const callers = jsFiles.filter((f) => callSites(code(f)).keys.size > 0);

  it("classifies every file that asks for a key", () => {
    const unclassified = callers.filter((f) => bridgeOf(f, code(f)) === null).map((f) => path.basename(f));
    // A new file with an unrecognised binding must FAIL here rather than be silently skipped.
    expect(unclassified).toEqual([]);
  });

  it("finds both bridges in use inside one folder — the trap itself", () => {
    const byBridge = {};
    callers.forEach((f) => { byBridge[path.basename(f)] = bridgeOf(f, code(f)); });

    expect(byBridge["app.js"]).toBe(BRIDGES.WCN);
    expect(byBridge["quick-create.js"]).toBe(BRIDGES.TASKS);
  });

  it("resolves quick-create's keys through the TASKS payload, where they are sound", () => {
    /*
     * The Tasks bridge is a HAND-MAINTAINED list (Views/Tasks/_IndexL10n.cshtml). A key must be BOTH published
     * there and present in the resx — the resx alone is not enough, which is the difference between the two
     * bridges and the reason this is checked in two steps.
     */
    const published = new Set(
      [...read(TASKS_BRIDGE).matchAll(/^\s*([A-Z][A-Za-z0-9]*)\s*=\s*\w*Localizer\[/gm)].map((m) => m[1]));
    const resx = resxNames(TASKS_RESX);
    const keys = [...callSites(code(path.join(WCN_JS, "quick-create.js"))).keys];

    expect(keys.length).toBeGreaterThan(0);
    keys.forEach((key) => {
      const pascal = key.charAt(0).toUpperCase() + key.slice(1);
      expect(published, `${key} is not published by the Tasks l10n bridge`).toContain(pascal);
      expect(resx, `${pascal} is not in TasksIndex.en.resx`).toContain(pascal);
    });
  });
});

/*
 * ⚠ THERE IS NO EXCEPTION LIST HERE ANY MORE, and that is the point — BL-309.
 *
 * One used to exist (`UNRENDERED_LABELS = ["OpenInSource"]`), excusing a resource label from the seven-language
 * rule because `task-detail-resolver.js` built it into a `sourceNavigation` object no surface read. The
 * producer has been deleted — source navigation renders through `DetailOpenSource` and `ActionCompleteInSource`,
 * both already in all seven files — so the exception went with its reason instead of outliving it.
 *
 * Nothing weaker replaced it: the walk below now covers every `kind: 'resource'` label in the folder with no
 * escape hatch at all, so re-adding an unrendered label — `sourceNavigation` included — turns this red on the
 * commit that adds it and asks for the seven translations.
 */
describe("every key the shell asks for by name exists", () => {
  const app = code(path.join(WCN_JS, "app.js"));
  const sites = callSites(app);
  const available = new Set([...resxNames(WCN_RESX("en")), ...SHARED_KEYS_ON_THE_PAGE]);

  it("has no missing literal keys", () => {
    expect(sites.keys.size).toBeGreaterThan(250);   // non-vacuity: the extractor really found the call sites
    const missing = [...sites.keys].filter((k) => !available.has(k)).sort();
    expect(missing).toEqual([]);
  });

  it("resolves every key-map the shell dereferences", () => {
    // `t(STATUS_KEY[s])` — the keys are the map's values. An unresolvable map name fails rather than being
    // skipped, so a map that moves out of app.js cannot take its keys out of this guard with it.
    expect(sites.maps.size).toBeGreaterThan(0);
    const missing = [];
    sites.maps.forEach((name) => {
      const values = mapValues(app, name);
      expect(values, `${name} is dereferenced by t() but not declared in app.js`).toBeTruthy();
      values.filter((v) => !available.has(v)).forEach((v) => missing.push(`${name} → ${v}`));
    });
    expect(missing.sort()).toEqual([]);
  });

  it("resolves the resource labels the in-repo fixtures carry", () => {
    // Showcase fixtures reach the same `t(label.key)` path real items do; a fixture key with no resx entry
    // renders the raw key on the demonstration surface.
    const keys = new Set();
    const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).forEach((entry) => {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) { walk(full); return; }
      if (!entry.name.endsWith(".js")) { return; }
      const source = code(full);
      /*
       * ⚠ TWO SPELLINGS, AND THE INLINE ONE WAS THE RARE ONE — this is why the count below is asserted.
       *
       * The object literal was all this matched, and MEASURED, exactly one file in the folder ever wrote it:
       * `task-detail-resolver.js`, for the `OpenInSource` label that BL-309 deleted. Every fixture builds its
       * labels through `resource('Key')` (declared in fixtures/canonical-fixtures.js) instead — 48 keys the
       * walk went straight past. So a test named for "the labels the in-repo fixtures carry" was checking
       * one key that was not a fixture's, and then excusing it.
       */
      [...source.matchAll(/kind:\s*'resource'[^}]*?key:\s*'([A-Za-z0-9_]+)'/g)].forEach((m) => keys.add(m[1]));
      [...source.matchAll(/\bresource\(\s*'([A-Za-z0-9_]+)'/g)].forEach((m) => keys.add(m[1]));
    });
    // The whole folder, not just fixtures/: the helper is declared in fixtures/ but a resolver or mapper may
    // build one of these labels too, and a walk scoped to one directory cannot see that.
    walk(WCN_JS);

    // Non-vacuity, and it is NOT decorative: at `> 0` this guard sat green while measuring a single key.
    // The floor is the fixture catalogue's real size, so an extractor that stops matching fails loudly.
    expect(keys.size, "the resource-label extractor has stopped finding the fixtures' labels").toBeGreaterThan(40);
    expect(
      [...keys].filter((k) => !available.has(k)).sort(),
      "a resource label with no resx entry renders as its raw key — translate it, or stop declaring it"
    ).toEqual([]);
  });

  /*
   * BL-309's other half. Deleting the unread producer is only half a fix: the field could come back, and a
   * `sourceNavigation` that nothing reads is dead weight whatever label it carries — the test above only
   * notices if the label happens to be a new one.
   *
   * So the shape itself is guarded. Either the field does not exist, or a surface renders it; a producer with
   * no reader fails here and names the choice: wire it up, or drop it.
   */
  it("has no sourceNavigation without a surface that renders it", () => {
    const byRole = { producers: [], readers: [] };
    jsFiles.filter((f) => code(f).includes("sourceNavigation"))
      .forEach((f) => byRole[path.basename(f) === "task-detail-resolver.js" ? "producers" : "readers"]
        .push(path.basename(f)));

    if (byRole.readers.length) { return; }   // rendered — the label check above now owns it
    expect(byRole.producers, "sourceNavigation is produced but no surface reads it (BL-309)").toEqual([]);
  });
});

/*
 * (b) FAMILIES — a prefix plus a value is not one key, it is a domain of keys.
 *
 * Each family names WHERE its domain comes from, and the domain is parsed from that source rather than
 * restated here: adding a seventeenth activity code, or an eighth approval status, turns this red on the
 * commit that adds it instead of on the screen that renders it.
 */
const contract = code(path.join(WCN_JS, "fixture-contract.js"));
const csharp = (relative) => code(path.join(repoRoot, relative));

const jsArray = (source, name) => {
  const at = source.indexOf(`const ${name} = [`);
  expect(at, `${name} is not declared in fixture-contract.js`).toBeGreaterThan(-1);
  const body = source.slice(at, source.indexOf("]", at));
  return [...body.matchAll(/'([^']+)'/g)].map((m) => m[1]);
};

const csEnum = (source, name) => {
  const at = source.indexOf(`enum ${name}`);
  expect(at, `${name} is not declared where it was expected`).toBeGreaterThan(-1);
  const body = source.slice(source.indexOf("{", at), source.indexOf("}", at));
  return [...body.matchAll(/([A-Za-z][A-Za-z0-9]*)\s*=\s*\d+/g)].map((m) => m[1]);
};

const csConsts = (source, className) => {
  const at = source.indexOf(className);
  const body = source.slice(at);
  return [...body.matchAll(/public const string \w+ = "([^"]+)"/g)].map((m) => m[1]);
};

const pascal = (value) => value.charAt(0).toUpperCase() + value.slice(1);

describe("(b) every declared key family is complete", () => {
  const available = new Set([...resxNames(WCN_RESX("en")), ...SHARED_KEYS_ON_THE_PAGE]);

  const FAMILIES = {
    // The activity feed's sentences. Sixteen codes, in the executable contract.
    AuditEvent: () => jsArray(contract, "ACTIVITY_EVENT_CODES").map(pascal),
    // The names of the fields an edit can touch. `customField` is excluded at the call site: a tenant field
    // carries its own label, so it never becomes an AuditField* key (see fieldChangeName in app.js).
    AuditField: () => csConsts(
      csharp("services/Diten.Platform/src/Diten.Platform.Domain/Entities/Tasks/TaskFieldChange.cs"),
      "TaskFieldChangeCodes").filter((f) => f !== "customField").map(pascal),
    Priority: () => jsArray(contract, "PRIORITIES"),
    /*
     * `Watcher` is excluded, and it is not an omission: the render site drops the role for a plain watcher
     * ("Ayşe Yılmaz (izleyici)" under a heading reading "İzleyiciler" says the same word twice), so
     * WatcherRoleWatcher is a key nothing can ever ask for. Adding it would be dead text in seven languages.
     */
    WatcherRole: () => csEnum(
      csharp("services/Diten.Platform/src/Diten.Platform.Domain/Enums/Tasks/TaskEnums.cs"),
      "TaskWatcherRole").filter((r) => r !== "Watcher")
  };

  it.each(Object.keys(FAMILIES))("%s covers its whole domain", (prefix) => {
    const domain = FAMILIES[prefix]();
    expect(domain.length).toBeGreaterThan(0);
    expect(domain.filter((value) => !available.has(prefix + value)).sort()).toEqual([]);
  });

  it("knows about every family the shell uses — an undeclared one fails here", () => {
    /*
     * The load-bearing half. Without it, a NEW `t('SomePrefix' + x)` would simply not be looked at, and the
     * guard would report green over precisely the kind of gap it exists to find.
     */
    const used = [...callSites(code(path.join(WCN_JS, "app.js"))).families].sort();
    expect(used).toEqual(Object.keys(FAMILIES).sort());
  });
});

describe("(b) the keys the SERVER puts on the wire exist too", () => {
  const available = new Set([...resxNames(WCN_RESX("en")), ...SHARED_KEYS_ON_THE_PAGE]);
  const platform = path.join(repoRoot, "services", "Diten.Platform", "src", "Diten.Platform.Application");

  /** Every `"WorkAggregation_…"` literal the projection can emit — comments stripped, per trap (a). */
  const emitted = () => {
    const found = new Map();   // key → file, so a failure names where it came from
    const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).forEach((entry) => {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) { if (entry.name !== "obj" && entry.name !== "bin") { walk(full); } return; }
      if (!entry.name.endsWith(".cs")) { return; }
      [...code(full).matchAll(/"(WorkAggregation_[A-Za-z0-9_]*)"/g)]
        .forEach((m) => found.set(m[1], path.basename(full)));
    });
    walk(platform);
    return found;
  };

  const PREFIX_DOMAINS = {
    WorkAggregation_TaskStatus_: () => csEnum(
      csharp("services/Diten.Platform/src/Diten.Platform.Domain/Enums/Tasks/TaskEnums.cs"), "TaskLifecycle"),
    WorkAggregation_NativeStatus_: () => csEnum(
      csharp("services/Diten.Platform/src/Diten.Platform.Domain/Enums/Workflow/ApprovalTaskStatus.cs"),
      "ApprovalTaskStatus"),
    /*
     * The SYSTEM closure outcomes. A third concatenated prefix, and it belongs here rather than as a whole key
     * for the same reason the two above do — but the domain is read differently: these are not an enum, they are
     * the catalogue's `Entry(CODE, "KeySuffix", …)` rows. The SUFFIX is what completes the key, so that is what
     * is read, and every one of the five must have a sentence behind it.
     *
     * This matters more than the count suggests: a system outcome ships to EVERY tenant in seven languages, so a
     * catalogue entry whose resx line was forgotten would put a raw key in front of every user of the product.
     */
    WorkAggregation_ClosureOutcome_: () => [...csharp(
      "services/Diten.Platform/src/Diten.Platform.Application/Features/Tasks/Services/TaskClosureOutcomeCatalog.cs")
      .matchAll(/Entry\([A-Za-z]+,\s*"([A-Za-z0-9]+)"/g)].map((m) => m[1])
  };

  it("every whole key the providers emit has a sentence behind it", () => {
    const found = emitted();
    expect(found.size).toBeGreaterThan(20);
    const missing = [...found.entries()]
      .filter(([key]) => !Object.keys(PREFIX_DOMAINS).includes(key))   // the two prefixes are checked below
      .filter(([key]) => !available.has(key))
      .map(([key, file]) => `${key} (${file})`)
      .sort();
    expect(missing).toEqual([]);
  });

  it.each(Object.keys(PREFIX_DOMAINS))("%s covers its whole enum", (prefix) => {
    const domain = PREFIX_DOMAINS[prefix]();
    expect(domain.length).toBeGreaterThan(0);
    expect(domain.filter((value) => !available.has(prefix + value)).sort()).toEqual([]);
  });

  it("the prefixes really are still concatenated on the server", () => {
    // Non-vacuity for the check above: if the server stopped building these keys by concatenation, the enum
    // test would be guarding something nobody emits.
    const found = emitted();
    Object.keys(PREFIX_DOMAINS).forEach((prefix) => {
      expect(found.has(prefix), `${prefix} is no longer emitted as a prefix — re-derive its domain`).toBe(true);
    });
  });
});

describe("(g) seven languages, or it is not localized", () => {
  const perLocale = Object.fromEntries(LOCALES.map((l) => [l, resxNames(WCN_RESX(l))]));

  it.each(LOCALES.filter((l) => l !== "en"))("%s carries exactly the same keys as en", (locale) => {
    const en = perLocale.en;
    expect([...en].filter((k) => !perLocale[locale].has(k)).sort()).toEqual([]);
    expect([...perLocale[locale]].filter((k) => !en.has(k)).sort()).toEqual([]);
  });

  it("translates the three keys this round added rather than leaving English in place", () => {
    const value = (locale, key) => {
      const m = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(read(WCN_RESX(locale)));
      return m ? m[1].trim() : null;
    };
    // `No` is deliberately absent: "No" is the correct Spanish word for it, so demanding a difference would
    // demand a wrong translation. The others must differ from English in every language.
    ["ErrorCommentTextInvalid", "Yes"].forEach((key) => {
      const english = value("en", key);
      expect(english).toBeTruthy();
      LOCALES.filter((l) => l !== "en").forEach((locale) => {
        expect(value(locale, key), `${key}/${locale} is still the English text`).not.toBe(english);
      });
    });
  });
});
