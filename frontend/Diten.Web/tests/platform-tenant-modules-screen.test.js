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
const expiryBlock = slice("const todayIsoDate", "const removeModuleEntitlementOverride");
const fetchBlock = source.slice(source.indexOf("const extractErrorMessage"), source.indexOf("const escapeHtml"))
  + slice("const fetchJson = async", "const imageMarkup");
// Keys the tab's flows read besides the refusal sentences and the action labels.
const FLOW_KEYS = ["NoChangesMade", "PermissionDenied", "EntitlementOutcomeUnknown"];

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
  FLOW_KEYS.forEach((key) => { labels[key] = resxValue(lang, key); });
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

  test.each(LANGS)("[%s] a 403 is said with its own sentence, never the general one", (lang) => {
    const labels = labelsFor(lang);
    const text = rules.entitlementRefusalText({ status: 403, code: null, message: "Permission denied." }, labels);

    expect(text).toBe(resxValue(lang, "PermissionDenied"));
    expect(text).toBeTruthy();
    expect(text).not.toBe(labels.ErrorOccurred);
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

    // Business refusals AND the validators' curated codes (TenantModuleEntitlementValidationCodeTests pins that every
    // validator rule answers with one of these): the screen has a sentence for every code the server can send.
    expect(server.length).toBe(16);
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
      ...FLOW_KEYS])];

    mapped.forEach((key) => {
      expect(resxValue(lang, key), `TenantsIndex.${lang}.resx ${key}`).toBeTruthy();
      expect(l10n, `_IndexL10n ${key}`).toMatch(new RegExp(`\\b${key} = (Shared)?Localizer\\["${key}"\\]`));
    });

    const inResx = [...resx(lang).matchAll(/<data name="(Entitlement[A-Za-z]+)"/g)].map((m) => m[1]).sort();
    const expected = [...Object.values(rules.ENTITLEMENT_REFUSAL_KEYS), ...FLOW_KEYS].filter((k) => k.startsWith("Entitlement")).sort();
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

  test("a list that could not be reloaded says nothing about the change: the outcome is unknown", () => {
    expect(rules.entitlementChangeOutcome(stored, null)).toBe("unknown");
    expect(rules.entitlementChangeOutcome(planLine, undefined)).toBe("unknown");
  });

  test("a plan line whose access did not move was not suspended", () => {
    expect(rules.entitlementChangeOutcome(planLine, [{ ...planLine }])).toBe("unchanged");
    expect(rules.entitlementChangeOutcome(planLine, [{ ...planLine, effectiveAccess: "BlockedByOverride" }])).toBe("changed");
  });

  const run = (rowsAfter, { loadFails = false } = {}) => {
    const toasts = [];
    let reloads = 0;
    const window = { showToast: (text, kind) => toasts.push([text, kind]) };
    const table = {
      // What DataTables does when the list request fails: the ajax hook hands it an empty table and the reload's
      // callback still runs. The hook is what records the failure (moduleEntitlementsLoadFailed).
      ajax: { reload: (done) => { reloads += 1; done(); } },
      rows: () => ({ data: () => ({ toArray: () => (loadFails ? [] : rowsAfter) }) })
    };
    const labels = { ...labelsFor("tr"), RecordSaved: "Kayıt kaydedildi." };
    // eslint-disable-next-line no-new-func
    const api = new Function("L", "window", "moduleEntitlementsDt", "moduleEntitlementsLoadFailed",
      rulesBlock + outcomeBlock + "; return { showEntitlementActionError, reportEntitlementChange };")(labels, window, table, loadFails);
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

  test("a list that failed to reload never says 'saved': it says the result is unknown and asks the list again", async () => {
    const { api, toasts, labels, reloads } = run([{ ...stored, rowVersion: "BBBB" }], { loadFails: true });

    await api.reportEntitlementChange(stored, labels.RecordSaved);

    expect(toasts).toEqual([[resxValue("tr", "EntitlementOutcomeUnknown"), "warning"]]);
    expect(toasts.flat()).not.toContain(labels.RecordSaved);
    expect(reloads()).toBe(2);
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

describe("the list's own request records whether it failed", () => {
  // The ajax hook of the Modules table, sliced from details.js as it ships.
  const hookSource = slice("ajax: (_data, callback) => {", "paging: true").replace(/^ajax: /, "").replace(/,\s*$/, "");
  const hook = (fetchJson) => {
    // eslint-disable-next-line no-new-func
    return new Function("fetchJson", "window", "L", "apiBase", "tenantId",
      "let moduleEntitlementsLoadFailed = 'untouched'; const ajax = " + hookSource
      + "; return { ajax, failed: () => moduleEntitlementsLoadFailed };")(
      fetchJson, { showToast: () => {} }, { ErrorOccurred: "x" }, "/api", "t1");
  };
  const settle = () => new Promise((resolve) => setTimeout(resolve, 0));

  test("a failed load hands the table an empty list AND marks the load as failed", async () => {
    const api = hook(() => Promise.reject(new Error("down")));
    let handed = null;
    api.ajax({}, (data) => { handed = data; });
    await settle();
    expect(handed).toEqual({ data: [] });
    expect(api.failed()).toBe(true);
  });

  test("a successful load clears the mark", async () => {
    const api = hook(() => Promise.resolve([{ moduleCode: "CRM" }]));
    let handed = null;
    api.ajax({}, (data) => { handed = data; });
    await settle();
    expect(handed).toEqual({ data: [{ moduleCode: "CRM" }] });
    expect(api.failed()).toBe(false);
  });
});

describe("a refusal keeps its code on every path of the request", () => {
  const request = (status, body) => {
    const window = { DitenHttp: { isJsonMediaType: () => true }, DtDefaults: {} };
    const fetch = async () => ({
      status,
      ok: status >= 200 && status < 300,
      redirected: false,
      url: "",
      headers: { get: () => "application/json" },
      text: async () => JSON.stringify(body)
    });
    // eslint-disable-next-line no-new-func
    const fetchJson = new Function("L", "window", "fetch", "getAuthHeaders", "unwrap",
      fetchBlock + "; return fetchJson;")({ ErrorOccurred: "general" }, window, fetch, () => ({}), (p) => p?.data ?? p);
    return fetchJson("/x").then(() => null, (error) => error);
  };

  test("a validation problem (400) carries its reason_code to the bridge", async () => {
    const error = await request(400, { title: "Validation Error", status: 400, detail: "x", errors: { "Request.ExpiryDateUtc": ["x"] }, reason_code: "ENTITLEMENT_EXPIRY_IN_PAST" });
    expect(error.code).toBe("ENTITLEMENT_EXPIRY_IN_PAST");
    expect(error.status).toBe(400);
    expect(rules.entitlementRefusalText(error, labelsFor("tr"))).toBe(resxValue("tr", "EntitlementExpiryInPast"));
  });

  test("an envelope that answers 200 with isSuccessful:false carries its code too", async () => {
    const error = await request(200, { isSuccessful: false, errors: ["Entitlement was modified by another process."], reasonCode: "ENTITLEMENT_STALE" });
    expect(error.code).toBe("ENTITLEMENT_STALE");
    expect(rules.entitlementRefusalText(error, labelsFor("en"))).toBe(resxValue("en", "EntitlementStale"));
  });

  test("a 403 is marked as one", async () => {
    const error = await request(403, {});
    expect(error.status).toBe(403);
    expect(rules.entitlementRefusalText(error, labelsFor("tr"))).toBe(resxValue("tr", "PermissionDenied"));
  });
});

describe("extending an expiry", () => {
  const open = (row, today = new Date()) => {
    const sent = [];
    const toasts = [];
    let dialog = null;
    const window = {
      showConfirm: (title, callback, options) => { dialog = { title, callback, options }; },
      showToast: (text, kind) => toasts.push([text, kind])
    };
    const labels = labelsFor("tr");
    class FixedDate extends Date {
      constructor(...args) { super(...(args.length ? args : [today.getTime()])); }
    }
    // eslint-disable-next-line no-new-func
    const api = new Function("L", "window", "fetchJson", "apiBase", "tenantId", "getAuthHeaders", "reportEntitlementChange", "showEntitlementActionError", "Date",
      expiryBlock + "; return { openModuleEntitlementExpiryEditor };")(
      labels, window,
      async (url, options) => { sent.push({ url, body: JSON.parse(options.body) }); return null; },
      "/api", "t1", () => ({}), async () => {}, () => {}, FixedDate);
    api.openModuleEntitlementExpiryEditor(row);
    return { dialog, sent, toasts, labels };
  };
  const row = { physicalEntitlementId: "e1", moduleCode: "CRM", rowVersion: "AAAA", expiryDateUtc: "2026-12-31T23:59:59Z", reason: "r" };

  test("the date box opens on the row's current date (through the dialog's own didOpen seam)", () => {
    const { dialog } = open(row, new Date("2026-10-03T10:00:00Z"));
    expect(dialog.options.inputType).toBe("date");
    expect(dialog.options.inputAttributes).toEqual({ min: "2026-10-03" });
    expect(dialog.options.inputRequired).toBe(true);

    const box = { value: "" };
    dialog.options.didOpen({}, { getInput: () => box });
    expect(box.value).toBe("2026-12-31");
    expect(() => dialog.options.didOpen(null, undefined)).not.toThrow();
  });

  test("an empty date is refused in the dialog and is never sent — it does not remove the expiry", async () => {
    const { dialog, sent, toasts, labels } = open(row, new Date("2026-10-03T10:00:00Z"));
    expect(dialog.options.inputValidator("")).toBe(labels.EntitlementExpiryRequired);

    await dialog.callback("");
    expect(sent).toEqual([]);
    expect(toasts).toEqual([[labels.EntitlementExpiryRequired, "error"]]);
  });

  test("a past date is refused in the dialog and is never sent", async () => {
    const { dialog, sent, labels } = open(row, new Date("2026-10-03T10:00:00Z"));
    expect(dialog.options.inputValidator("2026-10-02")).toBe(labels.EntitlementExpiryInPast);
    expect(dialog.options.inputValidator("2026-10-03")).toBeNull();

    await dialog.callback("2026-10-02");
    expect(sent).toEqual([]);
  });

  test("a new date is sent as the END of that day, with the row's version", async () => {
    const { dialog, sent } = open(row, new Date("2026-10-03T10:00:00Z"));

    await dialog.callback("2027-01-15");

    expect(sent).toHaveLength(1);
    expect(sent[0].url).toBe("/api/t1/commercial/module-entitlements/e1/expiry");
    expect(sent[0].body).toEqual({ expiryDateUtc: "2027-01-15T23:59:59.000Z", reason: "r", rowVersion: "AAAA" });
  });

  test("the same date changes nothing and is not sent", async () => {
    const { dialog, sent, toasts, labels } = open(row, new Date("2026-10-03T10:00:00Z"));
    await dialog.callback("2026-12-31");
    expect(sent).toEqual([]);
    expect(toasts).toEqual([[labels.NoChangesMade, "info"]]);
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
