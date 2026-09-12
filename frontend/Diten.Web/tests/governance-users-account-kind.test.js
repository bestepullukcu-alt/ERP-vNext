const fs = require("fs");
const path = require("path");

/*
 * WP-INFRA-AUTH-ACCOUNT-KIND-01 — the account-kind surface on the tenant Users screen.
 *
 * WHAT IS PINNED, and why each line is a guard and not a description:
 *   1. The classification controls are DRAWN only for a holder of auth.users.account-kind.manage — the create
 *      offcanvas select and the quick view's "Change type" sit inside a Razor gate on that exact key (UAS-001:
 *      no disabled control for the others). Reading the markup is the only way to see this: the test user holds
 *      every permission, so a rendered page would never show the gap.
 *   2. The JS gates the same action on the same key, and posts to the same-origin /Users/api proxy — never to a
 *      service port. The frontend rule (AGENTS.md §3) is that 5001 never addresses 5056 directly.
 *   3. The kind travels as the enum NAME (Unknown/Human/Service) and an unknown value collapses to Unknown —
 *      the unconfirmed state, never a guessed "Human". The owner's decision is that nobody becomes Human by
 *      structure, and the client must not undo that with a default.
 *   4. All seven tenant languages carry every new key (localization-standard: Tenant = 7).
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");

const KEY = "auth.users.account-kind.manage";
const KINDS = ["Unknown", "Human", "Service"];
const NEW_L10N_KEYS = [
  "AccountKind", "AccountKindUnknown", "AccountKindHuman", "AccountKindService",
  "ChangeAccountKind", "AccountKindChanged", "AccountKindCreateHint"
];
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

/** The Razor `@if (Perms.Has("<key>")) { ... }` block that contains `marker`, or null when it is not gated. */
const gatedBlockContaining = (source, marker) => {
  const at = source.indexOf(marker);
  if (at < 0) { return null; }
  const gate = `@if (Perms.Has("${KEY}"))`;
  const openedAt = source.lastIndexOf(gate, at);
  if (openedAt < 0) { return null; }
  // Walk the braces from the gate's "{" to be sure the marker is INSIDE this block, not after it closed.
  const braceAt = source.indexOf("{", openedAt + gate.length);
  let depth = 0;
  for (let i = braceAt; i < source.length; i++) {
    if (source[i] === "{") { depth++; }
    else if (source[i] === "}") { depth--; if (depth === 0) { return i > at ? source.slice(openedAt, i + 1) : null; } }
  }
  return null;
};

describe("the classification controls exist only for the holder of the explicit-grant-only key", () => {
  const offcanvas = () => read("Views", "Governance", "Users", "_CreateEditOffcanvas.cshtml");
  const quickView = () => read("Views", "Governance", "Users", "_DetailsQuickView.cshtml");

  test("the create offcanvas injects the permission snapshot and gates the kind select on the key", () => {
    const source = offcanvas();
    expect(source).toMatch(/@inject\s+Diten\.Web\.Services\.IPermissionSnapshot\s+Perms/);
    const block = gatedBlockContaining(source, 'id="userAccountKind"');
    expect(block, "the account-kind select is drawn outside the account-kind.manage gate").toBeTruthy();
    expect(block).toMatch(/name="AccountKind"/);
  });

  test("the create select offers the three enum NAMES and an empty default that means Unknown", () => {
    const block = gatedBlockContaining(offcanvas(), 'id="userAccountKind"');
    expect(block).toMatch(/<option value="">/);
    expect(block).toMatch(/<option value="Human">/);
    expect(block).toMatch(/<option value="Service">/);
    // No numeric values: the DTO contract is the enum name as a string.
    expect(block).not.toMatch(/<option value="\d+"/);
  });

  test("the quick view shows the kind badge to every reader but gates 'Change type' on the key", () => {
    const source = quickView();
    expect(source).toMatch(/@inject\s+Diten\.Web\.Services\.IPermissionSnapshot\s+Perms/);
    // The badge is outside any gate: a reader with auth.users.read sees the FACT.
    expect(gatedBlockContaining(source, 'id="oc-accountkind"'), "the badge itself must not be gated").toBeNull();
    // The change control is inside the gate.
    const block = gatedBlockContaining(source, 'id="oc-btn-accountkind"');
    expect(block, "the change button is drawn outside the account-kind.manage gate").toBeTruthy();
    expect(gatedBlockContaining(source, 'id="oc-accountkind-select"'), "the select is drawn outside the gate").toBeTruthy();
    KINDS.forEach((k) => expect(block).toMatch(new RegExp(`<option value="${k}">`)));
  });

  test("no control is merely disabled for the unauthorized — it is absent (UAS-001)", () => {
    // A `disabled` classification control would advertise a capability the caller does not hold.
    const kindMarkup = [offcanvas(), quickView()].map((s) => (s.match(/id="(?:userAccountKind|oc-accountkind-select|oc-btn-accountkind)"[^>]*>/g) || []).join(" ")).join(" ");
    expect(kindMarkup).not.toMatch(/\bdisabled\b/);
  });
});

describe("the JS gates on the same key and talks only to the same-origin proxy", () => {
  const js = () => read("wwwroot", "assets", "js", "Governance", "Users", "index.js");

  test("the change action is gated on the explicit-grant-only key", () => {
    const source = js();
    expect(source).toMatch(new RegExp(`can\\('${KEY.replace(/\./g, "\\.")}'\\)`));
    // The handler bails out when the key is absent, before any confirm or fetch.
    const handler = source.slice(source.indexOf("oc-btn-accountkind')?.addEventListener"));
    expect(handler.indexOf("if (!canManageKind()) return;")).toBeGreaterThan(-1);
    expect(handler.indexOf("if (!canManageKind()) return;")).toBeLessThan(handler.indexOf("window.showConfirm"));
  });

  test("the change goes through window.showConfirm (one dialog body, BL-367) and POSTs to /Users/api", () => {
    const source = js();
    expect(source).toMatch(/window\.showConfirm\?\.\(L\.ChangeAccountKind/);
    expect(source).toMatch(/fetch\(`\/Users\/api\/\$\{id\}\/account-kind`/);
    // Never a service port, never the gateway from the browser for this mutation.
    expect(source).not.toMatch(/localhost:505\d/);
    expect(source).not.toMatch(/\$\{apiUrl\}\/api\/users\/\$\{[a-z]+\}\/account-kind/);
  });

  test("the request body carries the enum NAME under `kind`", () => {
    expect(js()).toMatch(/body:\s*JSON\.stringify\(\{\s*kind\s*\}\)/);
  });

  test("an unknown or numeric kind collapses to Unknown, never to Human", () => {
    // Load only the helper, by evaluating its declaration in isolation (the file is an IIFE tied to the DOM).
    const source = js();
    const start = source.indexOf("const ACCOUNT_KINDS");
    const end = source.indexOf("const accountKindLabel");
    expect(start).toBeGreaterThan(-1);
    expect(end).toBeGreaterThan(start);
    // eslint-disable-next-line no-new-func
    const normalize = new Function(source.slice(start, end) + "; return normalizeAccountKind;")();
    expect(normalize("human")).toBe("Human");
    expect(normalize("SERVICE")).toBe("Service");
    expect(normalize(undefined)).toBe("Unknown");
    expect(normalize("robot")).toBe("Unknown");
    expect(normalize(1)).toBe("Human");
    expect(normalize(7)).toBe("Unknown");
    expect(normalize("")).toBe("Unknown");
  });

  test("the table carries the account-kind column between Roles and Status", () => {
    const source = js();
    const roles = source.indexOf("{ data: 'roles', name: 'roles' }");
    const kind = source.indexOf("{ data: 'accountKind', name: 'accountKind' }");
    const status = source.indexOf("{ data: 'isActive', name: 'isActive' }");
    expect(roles).toBeGreaterThan(-1);
    expect(kind).toBeGreaterThan(roles);
    expect(status).toBeGreaterThan(kind);
    expect(read("Views", "Governance", "Users", "_DataTable.cshtml")).toMatch(/@Localizer\["AccountKind"\]/);
  });

  test("the filter offers the three kinds and the create form's default is the unconfirmed state", () => {
    const filter = read("Views", "Governance", "Users", "_Filter.cshtml");
    expect(filter).toMatch(/id="filterAccountKind"/);
    KINDS.forEach((k) => expect(filter).toMatch(new RegExp(`<option value="${k}">`)));
    expect(js()).toMatch(/matchesAccountKindFilter\(appliedFilters\.accountKinds, row\.accountKind\)/);
  });
});

describe("the MVC proxy exists for the three endpoints and forwards the kind on create", () => {
  const controller = () => read("Controllers", "UsersController.cs");

  test("the three proxies are declared under /Users/api and the POST is antiforgery-protected", () => {
    const source = controller();
    expect(source).toMatch(/\[HttpGet\("api\/lookup"\)\]/);
    expect(source).toMatch(/\[HttpGet\("api\/\{id:guid\}\/account-assertion"\)\]/);
    const post = source.indexOf('[HttpPost("api/{id:guid}/account-kind")]');
    expect(post).toBeGreaterThan(-1);
    expect(source.slice(post, post + 200)).toMatch(/\[ValidateAntiForgeryToken\]/);
    // Upstream targets are the gateway's /api/users routes, not a service port.
    expect(source).toMatch(/\/api\/users\/lookup/);
    expect(source).toMatch(/\/api\/users\/\{id\}\/account-assertion/);
    expect(source).toMatch(/\/api\/users\/\{id\}\/account-kind/);
    expect(source).not.toMatch(/localhost:5056/);
  });

  test("create forwards accountKind as null when unset — never a default kind", () => {
    const source = controller();
    expect(source).toMatch(/accountKind = string\.IsNullOrWhiteSpace\(accountKind\) \? null : accountKind/);
  });
});

describe("all seven tenant languages carry the new keys", () => {
  LANGS.forEach((lang) => {
    test(`UsersIndex.${lang}.resx`, () => {
      const resx = read("Resources", "Views", "Governance", "Users", `UsersIndex.${lang}.resx`);
      NEW_L10N_KEYS.forEach((key) => {
        const m = new RegExp(`<data name="${key}" xml:space="preserve"><value>([^<]+)</value></data>`).exec(resx);
        expect(m, `${key} missing in ${lang}`).toBeTruthy();
        expect(m[1].trim().length, `${key} empty in ${lang}`).toBeGreaterThan(0);
      });
    });
  });

  test("non-English files are translated, not copies of the English values", () => {
    const en = read("Resources", "Views", "Governance", "Users", "UsersIndex.en.resx");
    const value = (resx, key) => new RegExp(`<data name="${key}" xml:space="preserve"><value>([^<]+)</value>`).exec(resx)[1];
    LANGS.filter((l) => l !== "en").forEach((lang) => {
      const resx = read("Resources", "Views", "Governance", "Users", `UsersIndex.${lang}.resx`);
      const same = NEW_L10N_KEYS.filter((k) => value(resx, k) === value(en, k));
      expect(same, `${lang} carries untranslated English for: ${same.join(", ")}`).toEqual([]);
    });
  });

  test("the L10n bridge and the loader's required list carry the keys the JS reads", () => {
    const bridge = read("Views", "Governance", "Users", "_IndexL10n.cshtml");
    const loader = read("wwwroot", "assets", "js", "Governance", "Users", "index.l10n.js");
    ["AccountKind", "AccountKindUnknown", "AccountKindHuman", "AccountKindService", "ChangeAccountKind", "AccountKindChanged"]
      .forEach((key) => {
        expect(bridge).toMatch(new RegExp(`${key} = Localizer\\["${key}"\\]\\.Value`));
        expect(loader).toMatch(new RegExp(`'${key}'`));
      });
  });
});
