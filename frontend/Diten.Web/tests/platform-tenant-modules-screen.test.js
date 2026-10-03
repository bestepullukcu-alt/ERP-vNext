const fs = require("fs");
const path = require("path");

/*
 * BL-500 — the tenant's Modules tab (Platform → Tenants → a tenant → Modules).
 *
 * These tests run the PRODUCTION code of details.js — the block that turns a row's `allowedActions` into buttons and a
 * refusal's code into a sentence, and the block that reports the outcome of an action — sliced out of the page's IIFE
 * (the way governance-users-quota-refusal.test.js runs the Users screen's bridge) against the real resx texts.
 *
 * The bridge guard reads the server's own constants (TenantModuleEntitlementRowActions.cs): every refusal code and
 * every action name the server can send has exactly one counterpart here, in both languages.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const LANGS = ["en", "tr"];

const source = read("wwwroot", "assets", "js", "Platform", "Tenants", "details.js");
const slice = (from, to) => {
  const start = source.indexOf(from);
  const end = source.indexOf(to, start);
  expect(start, from).toBeGreaterThan(-1);
  expect(end, to).toBeGreaterThan(start);
  return source.slice(start, end);
};
const rulesBlock = slice("const ENTITLEMENT_ROW_ACTIONS", "const renderModuleEntitlementActions");
const outcomeBlock = slice("const reloadModuleEntitlements", "const disableModuleEntitlement");

const resx = (lang) => read("Resources", "Views", "Platform", "Tenants", `TenantsIndex.${lang}.resx`);
const resxValue = (lang, key) => {
  const match = resx(lang).match(new RegExp(`<data name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`));
  return match ? match[1].replace(/&amp;/g, "&").replace(/&lt;/g, "<").replace(/&gt;/g, ">") : null;
};

// eslint-disable-next-line no-new-func
const rules = new Function(rulesBlock + "; return { ENTITLEMENT_ROW_ACTIONS, ENTITLEMENT_REFUSAL_KEYS, moduleEntitlementActionsFor, entitlementRefusalCode, entitlementRefusalText, entitlementChangeOutcome };")();

const labelsFor = (lang) => {
  const labels = { ErrorOccurred: lang === "tr" ? "Bir hata oluştu." : "An error occurred." };
  Object.values(rules.ENTITLEMENT_REFUSAL_KEYS).forEach((key) => { labels[key] = resxValue(lang, key); });
  Object.values(rules.ENTITLEMENT_ROW_ACTIONS).forEach((action) => { labels[action.label] = resxValue(lang, action.label); });
  labels.NoChangesMade = resxValue(lang, "NoChangesMade");
  return labels;
};

const serverConstants = (className) => {
  const cs = fs.readFileSync(path.join(repoRoot, "services", "Diten.Platform", "src", "Diten.Platform.Application",
    "Features", "Tenants", "Commercial", "Entitlements", "TenantModuleEntitlementRowActions.cs"), "utf8");
  const start = cs.indexOf(`public static class ${className}`);
  expect(start, className).toBeGreaterThan(-1);
  const body = cs.slice(start, cs.indexOf("\n}", start));
  return [...body.matchAll(/public const string \w+ = "([^"]+)";/g)].map((m) => m[1]);
};

describe("what a row of the Modules tab offers", () => {
  test("the buttons are exactly the server's allowedActions, in the server's order", () => {
    const actions = rules.moduleEntitlementActionsFor({ allowedActions: ["extendExpiry", "disable", "removeOverride"] }, labelsFor("en"));

    expect(actions.map((a) => a.key)).toEqual([
      "edit-module-entitlement-expiry", "disable-module-entitlement", "remove-module-entitlement-override"]);
    expect(actions.map((a) => a.text)).toEqual(["Extend Expiry", resxValue("en", "Disable"), resxValue("en", "RemoveManualOverride")]);
    expect(actions.every((a) => a.text)).toBe(true);
    expect(rules.moduleEntitlementActionsFor({ allowedActions: ["extendExpiry"] }, labelsFor("tr"))[0].text).toBe("Süreyi Uzat");
  });

  test("a row the server allows nothing on offers nothing — whatever else the row says", () => {
    // A stray manual override on a baseline module: it has an id, it is enabled, its source is ManualOverride.
    // The old screen drew Disable, Edit Expiry and Remove for it. The server allows none.
    const baseline = { physicalEntitlementId: "x", isEnabled: true, displaySource: "ManualOverride", isProjectionRow: false, allowedActions: [] };

    expect(rules.moduleEntitlementActionsFor(baseline, labelsFor("en"))).toEqual([]);
    expect(rules.moduleEntitlementActionsFor({ ...baseline, allowedActions: undefined }, labelsFor("en"))).toEqual([]);
  });

  test("the screen adds no condition of its own to what the server allows", () => {
    // An expired row reads isEnabled:false in the list. The old screen offered "enable" for that; the server offers
    // a new date. Whatever the row's other fields are, only allowedActions decides.
    const expired = { physicalEntitlementId: "x", isEnabled: false, effectiveAccess: "Expired", allowedActions: ["extendExpiry", "disable"] };
    const plan = { isProjectionRow: true, isEnabled: false, allowedActions: ["disable"] };

    expect(rules.moduleEntitlementActionsFor(expired, labelsFor("en")).map((a) => a.key)).toEqual([
      "edit-module-entitlement-expiry", "disable-module-entitlement"]);
    expect(rules.moduleEntitlementActionsFor(plan, labelsFor("en")).map((a) => a.key)).toEqual(["disable-module-entitlement"]);
  });

  test("an action name this screen does not know is not drawn", () => {
    expect(rules.moduleEntitlementActionsFor({ allowedActions: ["transfer", "enable"] }, labelsFor("en")).map((a) => a.key))
      .toEqual(["enable-module-entitlement"]);
  });

  test("the rule block holds no condition on the row's state", () => {
    const actionsFor = rulesBlock.slice(rulesBlock.indexOf("const moduleEntitlementActionsFor"), rulesBlock.indexOf("// A refusal's CODE"));
    expect(actionsFor).not.toMatch(/isEnabled|isProjectionRow|displaySource|effectiveAccess|physicalEntitlementId/);
    expect(source).not.toMatch(/visible: \(isProjection \|\| id\)/);
  });
});

describe("a refusal is said from its code, in the reader's language", () => {
  const codes = Object.keys(rules.ENTITLEMENT_REFUSAL_KEYS);

  test.each(LANGS.flatMap((lang) => codes.map((code) => [lang, code])))("[%s] %s has its own sentence", (lang, code) => {
    const labels = labelsFor(lang);
    const viaEnvelope = rules.entitlementRefusalText({ code, message: "Entitlement was modified by another process." }, labels);

    expect(viaEnvelope).toBe(labels[rules.ENTITLEMENT_REFUSAL_KEYS[code]]);
    expect(viaEnvelope).toBeTruthy();
    expect(viaEnvelope).not.toBe(labels.ErrorOccurred);
    expect(viaEnvelope).not.toContain(code);
    expect(viaEnvelope).not.toContain("modified by another process");
  });

  test("every sentence is its own, and Turkish is not a copy of English", () => {
    const en = labelsFor("en");
    const tr = labelsFor("tr");
    const keys = [...new Set(Object.values(rules.ENTITLEMENT_REFUSAL_KEYS))];

    expect(new Set(keys.map((k) => en[k])).size).toBe(keys.length);
    keys.forEach((key) => expect(tr[key], key).not.toBe(en[key]));
    expect(tr.NoChangesMade).not.toBe(en.NoChangesMade);
    expect(tr.ExtendExpiry).not.toBe(en.ExtendExpiry);
  });

  test("a module-limit refusal arrives as the error text and still reads from its code", () => {
    const labels = labelsFor("tr");
    expect(rules.entitlementRefusalText({ code: null, message: " QUOTA_LIMIT_EXCEEDED " }, labels)).toBe(labels.QuotaLimitExceeded);
  });

  test.each(LANGS)("[%s] an unknown code or no code at all is the general sentence — never the code, never the service's English", (lang) => {
    const labels = labelsFor(lang);
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});

    expect(rules.entitlementRefusalText({ code: "SOMETHING_NEW", message: "A brand new English refusal." }, labels)).toBe(labels.ErrorOccurred);
    expect(rules.entitlementRefusalText({ code: null, message: "Baseline modules are entitlement-free and cannot be removed." }, labels)).toBe(labels.ErrorOccurred);
    expect(rules.entitlementRefusalText(undefined, labels)).toBe(labels.ErrorOccurred);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });
});

describe("the bridge matches the server: code ⇔ sentence, action ⇔ button", () => {
  test("every refusal code the server defines has a sentence here, and no entitlement code here is unknown to the server", () => {
    const server = serverConstants("TenantModuleEntitlementRefusalCodes").sort();
    const screen = Object.keys(rules.ENTITLEMENT_REFUSAL_KEYS).filter((code) => code.startsWith("ENTITLEMENT_")).sort();

    expect(server.length).toBe(7);
    expect(screen).toEqual(server);
  });

  test("every action name the server defines has a button here, and no other", () => {
    expect(Object.keys(rules.ENTITLEMENT_ROW_ACTIONS).sort()).toEqual(serverConstants("TenantModuleEntitlementRowActions").sort());
  });

  test.each(LANGS)("[%s] every mapped key exists in the resx and is handed to the page; no entitlement sentence is left over", (lang) => {
    const l10n = read("Views", "Platform", "Tenants", "_IndexL10n.cshtml");
    const mapped = [...new Set([
      ...Object.values(rules.ENTITLEMENT_REFUSAL_KEYS),
      ...Object.values(rules.ENTITLEMENT_ROW_ACTIONS).map((a) => a.label),
      "NoChangesMade"])];

    mapped.forEach((key) => {
      expect(resxValue(lang, key), `TenantsIndex.${lang}.resx ${key}`).toBeTruthy();
      expect(l10n, `_IndexL10n ${key}`).toMatch(new RegExp(`\\b${key} = (Shared)?Localizer\\["${key}"\\]`));
    });

    const inResx = [...resx(lang).matchAll(/<data name="(Entitlement[A-Za-z]+)"/g)].map((m) => m[1]).sort();
    const expected = Object.values(rules.ENTITLEMENT_REFUSAL_KEYS).filter((k) => k.startsWith("Entitlement")).sort();
    expect(inResx).toEqual(expected);
  });
});

describe("what the screen says after an action", () => {
  const stored = { physicalEntitlementId: "e1", rowVersion: "AAAA", moduleCode: "CRM" };
  const planLine = { isProjectionRow: true, moduleCode: "PLAN", effectiveAccess: "Active" };

  test("a stored row that still carries its version was not changed", () => {
    expect(rules.entitlementChangeOutcome(stored, [{ ...stored }])).toBe("unchanged");
    expect(rules.entitlementChangeOutcome(stored, [{ ...stored, rowVersion: "BBBB" }])).toBe("changed");
    expect(rules.entitlementChangeOutcome(stored, [])).toBe("changed");
  });

  test("a plan line whose access did not move was not suspended", () => {
    expect(rules.entitlementChangeOutcome(planLine, [{ ...planLine }])).toBe("unchanged");
    expect(rules.entitlementChangeOutcome(planLine, [{ ...planLine, effectiveAccess: "BlockedByOverride" }])).toBe("changed");
  });

  const run = (rowsAfter) => {
    const toasts = [];
    let reloads = 0;
    const window = { showToast: (text, kind) => toasts.push([text, kind]) };
    const table = {
      ajax: { reload: (done) => { reloads += 1; done(); } },
      rows: () => ({ data: () => ({ toArray: () => rowsAfter }) })
    };
    const labels = { ...labelsFor("tr"), RecordSaved: "Kayıt kaydedildi." };
    // eslint-disable-next-line no-new-func
    const api = new Function("L", "window", "moduleEntitlementsDt",
      rulesBlock + outcomeBlock + "; return { showEntitlementActionError, reportEntitlementChange };")(labels, window, table);
    return { api, toasts, labels, reloads: () => reloads };
  };

  test("a request that changed nothing does not say 'saved'", async () => {
    const { api, toasts, labels } = run([{ ...stored }]);

    await api.reportEntitlementChange(stored, labels.RecordSaved);

    expect(toasts).toEqual([[labels.NoChangesMade, "info"]]);
  });

  test("a request that changed the row says 'saved'", async () => {
    const { api, toasts, labels } = run([{ ...stored, rowVersion: "BBBB" }]);

    await api.reportEntitlementChange(stored, labels.RecordSaved);

    expect(toasts).toEqual([[labels.RecordSaved, "success"]]);
  });

  test("a stale screen is told so and the list is reloaded; another refusal does not reload", () => {
    const stale = run([]);
    stale.api.showEntitlementActionError({ code: "ENTITLEMENT_STALE", message: "Entitlement was modified by another process." });
    expect(stale.toasts).toEqual([[stale.labels.EntitlementStale, "error"]]);
    expect(stale.reloads()).toBe(1);

    const baseline = run([]);
    baseline.api.showEntitlementActionError({ code: "ENTITLEMENT_MODULE_BASELINE", message: "x" });
    expect(baseline.toasts).toEqual([[baseline.labels.EntitlementModuleBaseline, "error"]]);
    expect(baseline.reloads()).toBe(0);

    const signedOut = run([]);
    signedOut.api.showEntitlementActionError({ authHandled: true });
    expect(signedOut.toasts).toEqual([]);
  });
});

describe("page standard", () => {
  const tab = slice("const ENTITLEMENT_ROW_ACTIONS", "const openSubscriptionActionModal");

  test("the modules tab asks through the shared dialog, never the browser's prompt, and writes no inline style", () => {
    expect(tab).not.toMatch(/window\.prompt|\balert\(|\bconfirm\(/);
    expect(tab).not.toMatch(/style="|\.style\./);
    expect(tab).toMatch(/inputType: 'date'/);
  });

  test("no action announces 'saved' without looking at what changed", () => {
    const handlers = slice("const disableModuleEntitlement", "const removeModuleEntitlementOverride");
    expect(handlers).not.toMatch(/showToast\?\.\(L\.RecordSaved/);
    expect(handlers.match(/reportEntitlementChange\(row/g)).toHaveLength(3);
  });

  test("the add form reports a refusal through the same bridge", () => {
    expect(source.match(/saveModuleEntitlement\(\)\.catch\(showEntitlementActionError\)/g)).toHaveLength(2);
  });
});
