const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * WP-WEB-ROLE-PERMISSION-LABELS-01 (BL-508 (2)(3), BL-507, BL-342) — every verb and every row name the Role Permissions
 * screen draws is said in all seven languages, or it is named in the shrinking ledger
 * (docs/roadmap/backlog/role-permission-label-ledger.md). A module that ships without its own verbs and row names
 * turns this red.
 *
 * The catalog is READ FROM PRODUCTION, never listed here:
 *   · the Auth seed catalog (DataSeeder.BuildCanonicalPermissions: `new("crm", "planned-visit", "read-all", …,
 *     moduleOverride: "crm-planned-visit")`) — the grant sets above it are not the catalog;
 *   · every module manifest's permission fields (RequiredPermission / RequiredPermissionKey / PermissionKey, and the
 *     positional key of ModuleManifestPage / ModuleManifestAction / ModuleManifestPermission), a constant resolved to
 *     its literal wherever the services declare it.
 * Each key is stored as Auth stores it: Resource and Action through PermissionSegmentNormalizer (ported below and
 * pinned to PermissionSegmentNormalizerTests' own examples), the module by seed moduleOverride > manifest ModuleCode >
 * PermissionModuleAttribution.Derive (the key prefix, or the resource head under a service namespace).
 * The verb and row CODES are what the PRODUCTION PermLabel.split makes of that row — the rule is not copied here.
 *
 * Group headers are not in the ledger: the live group is whatever the last sync wrote (a manifest ModuleCode
 * overwrites the seed's moduleOverride), which a static reader cannot know. The ten groups BL-507 / BL-342 name are
 * measured by name below.
 */

const WEB = path.resolve(__dirname, "..");
const REPO = path.resolve(WEB, "..", "..");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const read = (...parts) => fs.readFileSync(path.join(REPO, ...parts), "utf8");

const loadPermLabel = () => {
  delete window.PermLabel;
  loadScript("wwwroot/assets/js/Governance/RoleAssignments/perm-label.js");
  return window.PermLabel;
};
const loadModuleLabel = () => {
  delete window.ModuleLabel;
  loadScript("wwwroot/assets/js/Governance/RoleAssignments/module-label.js");
  return window.ModuleLabel;
};

// ── PermissionSegmentNormalizer (Auth Domain), ported: split BEFORE lowercasing, each dotted level on its own ──
const isUpper = (c) => !!c && c !== c.toLowerCase() && c === c.toUpperCase();
const isLower = (c) => !!c && c !== c.toUpperCase() && c === c.toLowerCase();
const isDigit = (c) => !!c && /[0-9]/.test(c);
const normalizeLevel = (level) => {
  let out = "";
  const separate = () => { if (out.length > 0 && out[out.length - 1] !== "-") { out += "-"; } };
  for (let i = 0; i < level.length; i++) {
    const ch = level[i];
    if (ch === "_" || ch === "-" || ch === " ") { separate(); continue; }
    if (isUpper(ch) && i > 0) {
      const previous = level[i - 1];
      if (isLower(previous) || isDigit(previous) || (isUpper(previous) && isLower(level[i + 1]))) { separate(); }
    }
    out += ch.toLowerCase();
  }
  return out.replace(/^-+|-+$/g, "");
};
const normalizeSegment = (value) => String(value ?? "").trim().split(".").filter(Boolean)
  .map(normalizeLevel).filter(Boolean).join(".");

// ── C# source helpers ──
const withoutLineComments = (src) => src.split("\n").filter((line) => !/^\s*\/\//.test(line)).join("\n");
// The argument text of every `opener` call, up to its own closing paren (strings respected).
const calls = (src, opener) => {
  const out = [];
  let at = src.indexOf(opener);
  while (at >= 0) {
    let depth = 0;
    let inString = false;
    let j = at + opener.length - 1;
    for (; j < src.length; j++) {
      const c = src[j];
      if (inString) { if (c === "\\") { j++; } else if (c === "\"") { inString = false; } continue; }
      if (c === "\"") { inString = true; } else if (c === "(") { depth++; } else if (c === ")") { depth--; if (depth === 0) { break; } }
    }
    out.push(src.slice(at + opener.length, j));
    at = src.indexOf(opener, j);
  }
  return out;
};
// Top-level comma split of an argument list.
const args = (text) => {
  const out = [];
  let depth = 0;
  let inString = false;
  let current = "";
  for (let k = 0; k < text.length; k++) {
    const c = text[k];
    if (inString) { current += c; if (c === "\\") { current += text[++k]; } else if (c === "\"") { inString = false; } continue; }
    if (c === "\"") { inString = true; current += c; continue; }
    if ("([{".includes(c)) { depth++; }
    if (")]}".includes(c)) { depth--; }
    if (c === "," && depth === 0) { out.push(current.trim()); current = ""; } else { current += c; }
  }
  if (current.trim()) { out.push(current.trim()); }
  return out;
};
const literal = (arg) => { const m = /^"([^"]*)"$/.exec(arg || ""); return m ? m[1] : null; };
const walk = (dir, accept, out = []) => {
  fs.readdirSync(dir, { withFileTypes: true }).forEach((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (!["bin", "obj", "node_modules", "tests"].includes(entry.name)) { walk(full, accept, out); }
    } else if (accept(entry.name)) { out.push(full); }
  });
  return out;
};

// ── PermissionModuleAttribution.Derive, with its service namespaces read from the source ──
const attribution = read("services", "Diten.AuthService", "src", "Diten.AuthService.Domain", "Authorization", "PermissionModuleAttribution.cs");
const serviceNamespaces = [...(attribution.match(/ServiceNamespaces\s*=\s*new HashSet<string>\([^)]*\)\s*\{([^}]*)\}/) || ["", ""])[1]
  .matchAll(/"([^"]+)"/g)].map((m) => m[1].toLowerCase());
const derive = (keyNamespace, resource) => {
  const ns = String(keyNamespace).trim().toLowerCase();
  if (!serviceNamespaces.includes(ns)) { return ns; }
  return String(resource).trim().toLowerCase().split(".")[0] || ns;
};

// ── the seed catalog ──
const seeder = read("services", "Diten.AuthService", "src", "Diten.AuthService.Persistence", "Seed", "DataSeeder.cs");
const catalogStart = seeder.indexOf("BuildCanonicalPermissions() => new List<Permission>");
const catalogBlock = withoutLineComments(seeder.slice(catalogStart, seeder.indexOf("};", catalogStart)));
const seedRows = calls(catalogBlock, "new(").map(args)
  .filter((a) => literal(a[0]) && literal(a[1]) && literal(a[2]))
  .map((a) => {
    const override = a.find((arg) => arg.startsWith("moduleOverride:"));
    return { ns: literal(a[0]), resource: literal(a[1]), action: literal(a[2]),
      override: override ? literal(override.replace(/^moduleOverride:\s*/, "")) : null };
  });

// ── the manifests' permission fields ──
const KEY_SHAPE = /^[A-Za-z][A-Za-z0-9_-]*(?:\.[A-Za-z0-9_-]+){2,}$/;
const constants = new Map(); // "Class.Member" → key-shaped literal
walk(path.join(REPO, "services"), (name) => name.endsWith(".cs")).forEach((file) => {
  const src = fs.readFileSync(file, "utf8");
  if (!src.includes("const string")) { return; }
  const classes = [...src.matchAll(/\bclass\s+(\w+)/g)].map((m) => ({ at: m.index, name: m[1] }));
  for (const m of src.matchAll(/const string (\w+)\s*=\s*"([^"]+)"/g)) {
    const owner = classes.filter((c) => c.at < m.index).pop();
    if (owner && KEY_SHAPE.test(m[2])) { constants.set(`${owner.name}.${m[1]}`, m[2]); }
  }
});
const resolveKey = (expr, local, aliases, depth = 0) => {
  const e = String(expr || "").trim();
  if (depth > 3) { return null; }
  const lit = literal(e);
  if (lit !== null) { return KEY_SHAPE.test(lit) ? lit : null; }
  if (/^\w+$/.test(e)) { return local[e] !== undefined ? resolveKey(local[e], local, aliases, depth + 1) : null; }
  const m = /(\w+)\.(\w+)$/.exec(e);
  return m ? constants.get(`${aliases[m[1]] || m[1]}.${m[2]}`) || null : null;
};
const manifestFiles = walk(path.join(REPO, "services"),
  (name) => name.endsWith("ManifestProvider.cs") && name !== "IModuleManifestProvider.cs");
const manifests = manifestFiles.map((file) => {
  const src = withoutLineComments(fs.readFileSync(file, "utf8"));
  const local = Object.fromEntries([...src.matchAll(/const string (\w+)\s*=\s*([^;]+);/g)].map((m) => [m[1], m[2].trim()]));
  const aliases = Object.fromEntries([...src.matchAll(/^using (\w+)\s*=\s*[\w.]*?(\w+);/gm)].map((m) => [m[1], m[2]]));
  const moduleCode = (src.match(/const string ModuleCode\s*=\s*"([^"]+)"/) || [])[1]
    || literal(((src.match(/ModuleCode:\s*([^,\n]+)/) || [])[1] || "").trim());
  const exprs = [...src.matchAll(/\b(?:RequiredPermission|RequiredPermissionKey|PermissionKey):\s*([^,)\n]+)/g)].map((m) => m[1]);
  calls(src, "new ModuleManifestAction(").map(args).forEach((a) => { if (a[2] && !/^\w+:/.test(a[2])) { exprs.push(a[2]); } });
  calls(src, "new ModuleManifestPage(").map(args).forEach((a) => { if (a[3] && !/^\w+:/.test(a[3])) { exprs.push(a[3]); } });
  calls(src, "new ModuleManifestPermission(").map(args).forEach((a) => { if (a[0] && !/^\w+:/.test(a[0])) { exprs.push(a[0]); } });
  return {
    file: path.basename(file),
    module: moduleCode ? moduleCode.toLowerCase() : null,
    keys: [...new Set(exprs.map((x) => resolveKey(x, local, aliases)).filter(Boolean).map((k) => k.toLowerCase()))],
    unresolved: exprs.filter((x) => !resolveKey(x, local, aliases)).map((x) => `${path.basename(file)}: ${x.trim()}`)
  };
}).sort((a, b) => a.file.localeCompare(b.file));

// ── one row per key, as Auth stores it ──
const catalog = new Map();
seedRows.forEach((row) => {
  const key = `${row.ns}.${row.resource}.${row.action}`.toLowerCase();
  if (!catalog.has(key)) {
    catalog.set(key, { key, ns: row.ns, rawResource: row.resource, resource: normalizeSegment(row.resource),
      action: normalizeSegment(row.action), override: row.override });
  }
});
const manifestModule = new Map();
manifests.forEach((manifest) => manifest.keys.forEach((key) => {
  if (manifest.module && !manifestModule.has(key)) { manifestModule.set(key, manifest.module); }
  if (!catalog.has(key)) {
    const parts = key.split(".");
    const resource = parts.slice(1, -1).join(".");
    catalog.set(key, { key, ns: parts[0], rawResource: resource, resource: normalizeSegment(resource),
      action: normalizeSegment(parts[parts.length - 1]), override: null });
  }
}));
const permissions = [...catalog.values()].map((row) => ({
  key: row.key,
  module: row.override || manifestModule.get(row.key) || derive(row.ns, row.rawResource),
  resource: row.resource,
  action: row.action
}));

// ── what the bridge says, per language ──
const resx = (...parts) => {
  const out = new Map();
  for (const m of read(...parts).matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)) { out.set(m[1], m[2].trim()); }
  return out;
};
const shared = Object.fromEntries(LANGS.map((l) => [l, resx("frontend", "Diten.Web", "Resources", `SharedResource.${l}.resx`)]));
const screen = Object.fromEntries(LANGS.map((l) => [l, resx("frontend", "Diten.Web", "Resources", "Views", "Governance",
  "RoleAssignments", `RoleAssignmentsIndex.${l}.resx`)]));
const bridge = read("frontend", "Diten.Web", "Views", "Governance", "RoleAssignments", "_IndexL10n.cshtml");
const bridgeVerbs = Object.fromEntries([...bridge.matchAll(/\["([^"]+)"\]\s*=\s*Localizer\["(ActionVerb_[A-Za-z0-9]+)"\]/g)]
  .map((m) => [m[1], m[2]]));
const verbSaid = (code) => !!bridgeVerbs[code] && LANGS.every((l) => (screen[l].get(bridgeVerbs[code]) || "").length > 0);
const rowSaid = (code, PL) => {
  const canonical = PL.normalize(code);
  return LANGS.every((l) => (shared[l].get(`Perm.Entity.${canonical}`) || shared[l].get(`Nav.Page.${canonical}`) || "").length > 0);
};

// ── the ledger ──
const LEDGER = read("docs", "roadmap", "backlog", "role-permission-label-ledger.md");
const ceilingMatch = LEDGER.match(/^Tavan: fiil (\d+) · satır (\d+)$/m);
const ledgerRows = [...LEDGER.matchAll(/^\| (fiil|satır) \| `([^`]+)` \|/gm)].map((m) => ({ kind: m[1], code: m[2] }));
const ledgerVerbs = ledgerRows.filter((r) => r.kind === "fiil").map((r) => r.code);
const ledgerEntities = ledgerRows.filter((r) => r.kind === "satır").map((r) => r.code);

const measure = () => {
  const PL = loadPermLabel();
  const verbs = new Set();
  const entities = new Set();
  permissions.forEach((perm) => {
    const out = PL.split(perm, {});
    verbs.add(out.action.code);
    if (out.entity.code) { entities.add(out.entity.code); }
  });
  return {
    PL,
    verbs: [...verbs].sort(),
    entities: [...entities].sort(),
    unsaidVerbs: [...verbs].filter((v) => !verbSaid(v)).sort(),
    unsaidEntities: [...entities].filter((e) => !rowSaid(e, PL)).sort()
  };
};

// Manifest permission fields that are computed, not written (ReturnPermissions.ForTarget(...), a helper's parameter):
// a static reader cannot resolve them. Pinned so a NEW unresolvable field is a red here, not a silent gap.
const KNOWN_UNRESOLVED = [
  "ReverseLogisticsManifestProvider.cs: ReturnPermissions.ForTarget(\"Authorized\")!",
  "ReverseLogisticsManifestProvider.cs: ReturnPermissions.ForTarget(\"Cancelled\")!",
  "ReverseLogisticsManifestProvider.cs: ReturnPermissions.ForTarget(\"Closed\")!",
  "ReverseLogisticsManifestProvider.cs: ReturnPermissions.ForTarget(\"Dispositioned\")!",
  "ReverseLogisticsManifestProvider.cs: ReturnPermissions.ForTarget(\"InTransit\")!",
  "ReverseLogisticsManifestProvider.cs: ReturnPermissions.ForTarget(\"Received\")!",
  "ReverseLogisticsManifestProvider.cs: ReturnPermissions.ForTarget(\"Rejected\")!",
  "TimeEntryManifestProvider.cs: permission",
  "WorkAggregationManifestProvider.cs: string.Empty"
];

describe("Role Permissions labels: said in seven languages or named in the ledger, and the ledger only shrinks", () => {
  test("the port of PermissionSegmentNormalizer answers every example its own C# tests pin", () => {
    const tests = read("services", "Diten.AuthService", "tests", "Diten.AuthService.Application.Tests", "Authorization",
      "PermissionSegmentNormalizerTests.cs");
    const theory = tests.slice(0, tests.indexOf("public void Normalizes_a_segment_to_lowercase_kebab"));
    const examples = [...theory.matchAll(/\[InlineData\((null|"[^"]*"),\s*"([^"]*)"\)\]/g)]
      .map((m) => [m[1] === "null" ? null : m[1].slice(1, -1), m[2]]);
    expect(examples.length).toBeGreaterThanOrEqual(14);
    expect(examples).toContainEqual(["PublishOverride", "publish-override"]);
    examples.forEach(([raw, expected]) => expect(normalizeSegment(raw), String(raw)).toBe(expected));
  });

  test("the reader reaches the catalog (guards against a vacuous pass)", () => {
    expect(serviceNamespaces).toEqual(["platform", "auth", "mdm"]);
    expect(seedRows.length).toBeGreaterThanOrEqual(290);
    expect(manifestFiles.length).toBeGreaterThanOrEqual(35);
    expect(permissions.length).toBeGreaterThanOrEqual(650);
    const byKey = Object.fromEntries(permissions.map((p) => [p.key, p]));
    // moduleOverride is read: the seed's own attribution, not the CRM manifest's "crm" and not the key prefix.
    expect(byKey["crm.planned-visit.read-all"]).toEqual(
      { key: "crm.planned-visit.read-all", module: "crm-planned-visit", resource: "planned-visit", action: "read-all" });
    // a snake_case seed action is stored kebab, the key keeps its own spelling (ADR-001)
    expect(byKey["mod0251.employee.view_sensitive"]).toMatchObject({ module: "mod0251", action: "view-sensitive" });
    // a manifest-only key (a constant in another file) carries its manifest's ModuleCode
    expect(byKey["time-entry.weeks.reopen"]).toMatchObject({ module: "time-entry", resource: "weeks", action: "reopen" });
    // a seed key with no override, under a service namespace, takes its resource head
    expect(byKey["platform.person.lookup_validation"]).toMatchObject({ module: "person", action: "lookup-validation" });
    expect(manifests.flatMap((m) => m.unresolved).sort()).toEqual([...KNOWN_UNRESOLVED].sort());
  });

  test("the module decides the row: seed moduleOverride > manifest ModuleCode > key prefix", () => {
    const PL = loadPermLabel();
    const key = { key: "crm.planned-visit.read-all", resource: "planned-visit", action: "read-all" };
    // as the reader attributes it (seed override) the row IS its group …
    expect(PL.split({ ...key, module: "crm-planned-visit" }, {}).entity.code).toBe("");
    // … and under the CRM manifest's module, which the live sync may have written last, it is its own row.
    expect(PL.split({ ...key, module: "crm" }, {}).entity.code).toBe("planned-visit");
    expect(PL.split({ key: "platform.tasks.read", module: "tasks", resource: "tasks", action: "read" }, {}).entity.code).toBe("");
  });

  test("every verb the catalog draws is said in seven languages or named in the ledger", () => {
    const { unsaidVerbs } = measure();
    expect(unsaidVerbs.filter((v) => !ledgerVerbs.includes(v)), "verbs with no words and no ledger row").toEqual([]);
  });

  test("every row name the catalog draws is said in seven languages or named in the ledger", () => {
    const { unsaidEntities } = measure();
    expect(unsaidEntities.filter((e) => !ledgerEntities.includes(e)), "row names with no words and no ledger row").toEqual([]);
  });

  test("a code that is said now is out of the ledger", () => {
    const { PL } = measure();
    expect(ledgerVerbs.filter((v) => verbSaid(v)), "verbs said in seven languages — remove their rows").toEqual([]);
    expect(ledgerEntities.filter((e) => rowSaid(e, PL)), "row names said in seven languages — remove their rows").toEqual([]);
  });

  test("the ledger names only codes the catalog really draws, each once", () => {
    const { verbs, entities } = measure();
    expect(ledgerVerbs.filter((v) => !verbs.includes(v))).toEqual([]);
    expect(ledgerEntities.filter((e) => !entities.includes(e))).toEqual([]);
    expect(new Set(ledgerVerbs).size).toBe(ledgerVerbs.length);
    expect(new Set(ledgerEntities).size).toBe(ledgerEntities.length);
  });

  test("the ledger's header names its row counts EXACTLY: every added or removed row edits the header too", () => {
    expect(ceilingMatch, "the ledger lost its ceiling line").not.toBeNull();
    expect(ledgerVerbs.length, "verb rows ≠ the header's count").toBe(Number(ceilingMatch[1]));
    expect(ledgerEntities.length, "row-name rows ≠ the header's count").toBe(Number(ceilingMatch[2]));
  });
});

describe("the verb bridge: kebab keys, a family each, seven languages each", () => {
  test("every ActionVerbs key is the display form normalizeAction gives — never snake_case", () => {
    const PL = loadPermLabel();
    const keys = Object.keys(bridgeVerbs);
    expect(keys.length).toBeGreaterThanOrEqual(115);
    expect(keys.filter((k) => k.includes("_") || PL.normalizeAction(k) !== k)).toEqual([]);
  });

  test("every bridged verb — dotted ones too — resolves to a family", () => {
    const PL = loadPermLabel();
    expect(Object.keys(bridgeVerbs).filter((v) => {
      const family = PL.resolveFamily(PL.normalizeAction(v));
      return !family || family === PL.UNFAMILIAR;
    })).toEqual([]);
  });

  test("every bridged verb has its words in all seven languages, and the six others are not English copies", () => {
    const gaps = [];
    Object.entries(bridgeVerbs).forEach(([code, key]) => LANGS.forEach((l) => {
      if (!(screen[l].get(key) || "").length) { gaps.push(`${l}/${key} (${code})`); }
    }));
    expect(gaps).toEqual([]);
    const { verbs } = measure();
    const copied = [];
    verbs.filter((v) => bridgeVerbs[v]).forEach((v) => LANGS.filter((l) => l !== "en").forEach((l) => {
      if (screen[l].get(bridgeVerbs[v]) === screen.en.get(bridgeVerbs[v])) { copied.push(`${l}/${v}`); }
    }));
    expect(copied).toEqual([]);
  });

  test("the catalog's verbs are all said today: the ledger carries no verb", () => {
    const { verbs, unsaidVerbs } = measure();
    expect(verbs.length).toBeGreaterThanOrEqual(110);
    expect(unsaidVerbs).toEqual([]);
  });
});

describe("group headers and row names BL-507 / BL-508 name", () => {
  const GROUPS = ["mod0251", "crm-account", "crm-contact", "crm-content-composition", "crm-doctor-insights", "crm-knowledge",
    "crm-planned-visit", "crm-territory", "crm-visit-plan", "crm-visit-report"];
  const ROWS = ["approvals", "categories", "settings", "timesheets", "weeks", "checklist-templates", "templates",
    "content-scope", "content-set", "eligibility", "visit-frequency-policy"];

  test.each(LANGS)("the ten groups have their own name in %s, not a humanized code", (lang) => {
    const ML = loadModuleLabel();
    const permNames = Object.fromEntries([...shared[lang].entries()].filter(([k]) => k.startsWith("Perm.Module."))
      .map(([k, v]) => [k.slice("Perm.Module.".length), v]));
    const map = ML.buildMap({}, [], permNames);
    GROUPS.forEach((code) => {
      const label = ML.resolve(code, map);
      expect(label, `${lang}/${code}`).toBe(shared[lang].get(`Perm.Module.${ML.normalize(code)}`));
      if (lang !== "en") { expect(label, `${lang}/${code}`).not.toBe(ML.humanize(code)); }
    });
  });

  test.each(LANGS)("the eleven named rows have their own name in %s", (lang) => {
    const PL = loadPermLabel();
    const permEntityNames = Object.fromEntries([...shared[lang].entries()].filter(([k]) => k.startsWith("Perm.Entity."))
      .map(([k, v]) => [k.slice("Perm.Entity.".length), v]));
    ROWS.forEach((code) => {
      const label = PL.split({ module: "m", resource: code, action: "read" }, { PermEntityNames: permEntityNames }).entity.label;
      expect(label, `${lang}/${code}`).toBe(shared[lang].get(`Perm.Entity.${PL.normalize(code)}`));
      // English names may read like the humanized code ("Approvals"); the six others must not be it
      if (lang !== "en") { expect(label, `${lang}/${code}`).not.toBe(PL.humanize(code)); }
    });
  });

  test("the row names never land under the sidebar's prefix (Nav.Page.* owes page help, and it is not ours)", () => {
    const PL = loadPermLabel();
    ROWS.forEach((code) => expect(shared.en.has(`Nav.Page.${PL.normalize(code)}`), code).toBe(false));
  });

  test("Perm.Entity wins over Nav.Page for the same code, whatever order the resource file lists them in", () => {
    const PL = loadPermLabel();
    const perm = { module: "time-entry", resource: "settings", action: "read" };
    const L = { EntityNames: { SETTINGS: "Menu name" }, PermEntityNames: { SETTINGS: "Screen name" } };
    expect(PL.split(perm, L).entity.label).toBe("Screen name");
    expect(PL.split(perm, { EntityNames: { SETTINGS: "Menu name" } }).entity.label).toBe("Menu name");
    // the bridge collects Perm.Entity.* into its own map and ships it beside EntityNames, not merged into it
    expect(bridge).toMatch(/const string PermEntityPrefix = "Perm\.Entity\.";/);
    expect(bridge).toMatch(/PermEntityNames = permEntityNames,/);
    expect(bridge).toMatch(/EntityNames = entityNames,/);
  });
});
