const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * WP-WEB-ROLE-PERMISSION-LABELS-01 (BL-508 (1)) — `read-all` is a REACH, not a verb.
 *
 * Measured on the owner's screen: "Görüntüle" stood beside an English "Read All", and the owner gave the Employee role
 * Read All — every task in the tenant — believing it was the plain view. read-all now reads as the verb it is
 * ("Görüntüle") with the same "Tüm kiracı" chip read-tenant-wide carries. Two CRM read-all keys grant more than reading;
 * they say "Yönet" and their tooltip says the write reach.
 *
 * The labels come from the PRODUCTION bridge (_IndexL10n.cshtml) and resx, in Turkish — the owner's language.
 */
const WEB = path.resolve(__dirname, "..");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const bridge = fs.readFileSync(path.join(WEB, "Views", "Governance", "RoleAssignments", "_IndexL10n.cshtml"), "utf8");
const resx = (lang) => {
  const xml = fs.readFileSync(path.join(WEB, "Resources", "Views", "Governance", "RoleAssignments", `RoleAssignmentsIndex.${lang}.resx`), "utf8");
  return new Map([...xml.matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)].map((m) => [m[1], m[2].trim()]));
};
// One bridge dictionary ({ code: Localizer["Key"] }) resolved in one language.
const dictionary = (name, values) => {
  const block = bridge.slice(bridge.indexOf(`${name} = new Dictionary<string, string>`));
  const body = block.slice(block.indexOf("{"), block.indexOf("}"));
  return Object.fromEntries([...body.matchAll(/\["([^"]+)"\]\s*=\s*Localizer\["([A-Za-z0-9_]+)"\]/g)]
    .map((m) => [m[1], values.get(m[2])]));
};
const l10n = (lang) => {
  const values = resx(lang);
  return {
    ActionVerbs: dictionary("ActionVerbs", values),
    ScopeLabels: dictionary("ScopeLabels", values),
    KeyHints: dictionary("KeyHints", values)
  };
};
const load = () => {
  delete window.PermLabel;
  loadScript("wwwroot/assets/js/Governance/RoleAssignments/perm-label.js");
  return window.PermLabel;
};
const TR = l10n("tr");

describe("read-all: the read verb plus the tenant-wide chip", () => {
  test("platform.tasks.read-all reads 'Görüntüle' with the 'Tüm kiracı' chip — the same chip read-tenant-wide has", () => {
    const PL = load();
    const readAll = PL.split({ key: "platform.tasks.read-all", module: "tasks", resource: "tasks", action: "read-all" }, TR);
    const tenantWide = PL.split({ key: "platform.tasks.work-report.read-tenant-wide", module: "tasks",
      resource: "tasks.work-report", action: "read-tenant-wide" }, TR);

    expect(readAll.action.label).toBe("Görüntüle");
    expect(readAll.action.code).toBe("read");
    expect(readAll.action.tone).toBe("info");
    expect(readAll.scope).toEqual({ code: "all", label: "Tüm kiracı" });
    expect(readAll.scope.label).toBe(tenantWide.scope.label);
    // the reach is never spelled out as the English word it hides in
    expect(JSON.stringify(readAll)).not.toMatch(/\bAll\b/);
  });

  test("every language names the read-all chip with the tenant-wide words, never a humanized 'All'", () => {
    const PL = load();
    LANGS.forEach((lang) => {
      const L = l10n(lang);
      const out = PL.split({ key: "platform.meetings.read-all", module: "meetings", resource: "meetings", action: "read-all" }, L);
      expect(out.scope.label, lang).toBe(resx(lang).get("ScopeLabel_TenantWide"));
      expect(out.scope.label, lang).not.toBe("All");
      expect(out.action.label, lang).toBe(resx(lang).get("ActionVerb_Read"));
    });
  });

  test.each([
    ["crm.visit-plan.read-all", "crm-visit-plan", "visit-plan", "KeyHint_CrmVisitPlanReadAll"],
    ["crm.planned-visit.read-all", "crm-planned-visit", "planned-visit", "KeyHint_CrmPlannedVisitReadAll"]
  ])("%s says 'Yönet' with the chip, and its tooltip says the write reach", (key, module, resource, hintKey) => {
    const PL = load();
    const out = PL.split({ key, module, resource, action: "read-all" }, TR);

    expect(out.action.label).toBe("Yönet");
    expect(out.action.tone).toBe("warning");          // the manage family, not the read one
    expect(out.action.icon).toBe("bx-edit");
    expect(out.action.code).toBe("read");             // display only: the assignment still posts the key
    expect(out.scope.label).toBe("Tüm kiracı");
    expect(out.action.hint).toBe(resx("tr").get(hintKey));
    expect(out.action.hint.length).toBeGreaterThan(20);
  });

  test("the key-level verbs and their hints are the same two keys, each hint in all seven languages", () => {
    const PL = load();
    expect(Object.keys(PL.KEY_VERBS).sort()).toEqual(Object.keys(TR.KeyHints).sort());
    LANGS.forEach((lang) => Object.values(l10n(lang).KeyHints).forEach((hint) => expect((hint || "").length, lang).toBeGreaterThan(20)));
  });

  test("another read-all key keeps the read verb: the precedence is per KEY, not per action", () => {
    const PL = load();
    const out = PL.split({ key: "crm.doctor-insights.read-all", module: "crm-doctor-insights", resource: "doctor-insights",
      action: "read-all" }, TR);
    expect(out.action.label).toBe("Görüntüle");
    expect(out.action.hint).toBe("");
  });
});

describe("only the WHOLE action read-all carries the chip", () => {
  test("run-all (a sweep group key, not a permission) stays one verb with no chip", () => {
    const out = load().split({ key: "document-governance.sweep.run-all", module: "document-governance", resource: "sweep",
      action: "run-all" }, TR);
    expect(out.scope).toBeNull();
    expect(out.action.code).toBe("run-all");
  });

  test.each(["install", "reinstall", "uninstall", "read-allowance", "pre-read-all"])("%s is not split", (action) => {
    const out = load().split({ key: `m.r.${action}`, module: "m", resource: "r", action }, TR);
    expect(out.scope).toBeNull();
    expect(out.action.code).toBe(action);
  });

  test("read-aggregate is a summary read: one verb, 'Özeti Görüntüle', no chip", () => {
    const out = load().split({ key: "crm.visit-report.read-aggregate", module: "crm-visit-report", resource: "visit-report",
      action: "read-aggregate" }, TR);
    expect(out.scope).toBeNull();
    expect(out.action.code).toBe("read-aggregate");
    expect(out.action.label).toBe("Özeti Görüntüle");
    expect(out.action.tone).toBe("info");
  });

  test("the bridge names the 'all' reach with the tenant-wide resource key, explicitly", () => {
    expect(bridge).toMatch(/\["all"\]\s*=\s*Localizer\["ScopeLabel_TenantWide"\]\.Value/);
  });
});

describe("the chip's tooltip keeps the write hint in every branch", () => {
  test("renderChip writes chip.title only through withHint", () => {
    const index = fs.readFileSync(path.join(WEB, "wwwroot", "assets", "js", "Governance", "RoleAssignments", "index.js"), "utf8");
    const start = index.indexOf("const renderChip = (perm) =>");
    const body = index.slice(start, index.indexOf("return { chip, state };", start));
    const assignments = [...body.matchAll(/chip\.title\s*=\s*([^;]+);/g)].map((m) => m[1]);

    expect(assignments.length).toBe(5);              // locked · assignable · removable · otherwise · assigned badge
    expect(assignments.filter((rhs) => !rhs.trim().startsWith("withHint("))).toEqual([]);
  });
});
