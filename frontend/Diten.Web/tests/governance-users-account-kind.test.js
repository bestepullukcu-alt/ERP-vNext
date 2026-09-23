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

/*
 * ─── The classification selects are select2, and select2 has two seams that markup cannot show ───────────────
 *
 * The owner's question was simply "why is this not a select2 like the rest of the screen". Wrapping it is one
 * class; what makes the wrap SAFE is the pair of seams the library introduces, and both are invisible to a
 * reader of the .cshtml:
 *
 *   1. select2 paints its own box NEXT TO the <select> and hides the original. A value written to `.value`
 *      therefore leaves the painted box showing the previous choice — the create form's reset walked straight
 *      into this.
 *   2. That painted box is a SIBLING. `d-none` on the <select> hides nothing the user can see — and this screen
 *      uses exactly that toggle to withdraw the "Change type" control when the permission snapshot disagrees
 *      with the server gate. A hide that silently stopped working is a control on screen the server refuses.
 *
 * So these tests drive the module's OWN functions (UsersList.offcanvasSelects) against the REAL vendored
 * jQuery and select2, on markup lifted out of the real Razor views. A test with its own copy of the helpers, or
 * its own hand-written <select>, would keep passing after either seam broke.
 */
describe("the account-kind selects are select2, and both of its seams are honoured", () => {
  const vm = require("vm");
  const { loadScript } = require("./load-script");

  /** The real <select> from the view, with the Razor calls reduced to their key names. */
  const selectFromView = (source, id) => {
    const from = source.indexOf(`<select id="${id}"`);
    const to = source.indexOf("</select>", from);
    expect(from, `${id} is no longer in the view`).toBeGreaterThan(-1);
    return source.slice(from, to + "</select>".length).replace(/@Localizer\["([^"]+)"\]/g, "$1");
  };

  let usersList;
  beforeAll(() => {
    // One load per file: `const UsersList` cannot be declared twice in the same context.
    loadScript("wwwroot/assets/vendor/libs/jquery/jquery.js");
    loadScript("wwwroot/assets/vendor/libs/select2/select2.js");
    loadScript("wwwroot/assets/js/Governance/Users/index.js");
    usersList = vm.runInThisContext("UsersList");
  });

  beforeEach(() => {
    const create = selectFromView(read("Views", "Governance", "Users", "_CreateEditOffcanvas.cshtml"), "userAccountKind");
    const quick = selectFromView(read("Views", "Governance", "Users", "_DetailsQuickView.cshtml"), "oc-accountkind-select");
    document.body.innerHTML =
      `<div class="offcanvas" id="offcanvasCreateEdit">${create}</div>` +
      `<div class="offcanvas" id="offcanvasDetailsPreview">${quick}</div>`;
    usersList.offcanvasSelects.init();
  });

  const created = () => document.getElementById("userAccountKind");
  const quickView = () => document.getElementById("oc-accountkind-select");
  const paintedBoxOf = (el) => el.nextElementSibling;
  const shownText = (el) => paintedBoxOf(el).querySelector(".select2-selection__rendered").textContent.trim();

  test("both selects are wrapped — the view marks them and the module wraps what the view marked", () => {
    [created(), quickView()].forEach((el) => {
      expect(el.classList.contains("select2-offcanvas"), `${el.id} lost the select2 marker in the view`).toBe(true);
      expect(el.classList.contains("select2-hidden-accessible"), `${el.id} was not wrapped`).toBe(true);
      expect(paintedBoxOf(el).classList.contains("select2-container")).toBe(true);
    });
  });

  test("the dropdown opens INSIDE its own offcanvas, not on <body>", () => {
    // Without dropdownParent select2 appends to <body>, which sits below the offcanvas in the stacking
    // context — the list opens behind the panel, which is the whole reason this screen never got select2.
    [["userAccountKind", "offcanvasCreateEdit"], ["oc-accountkind-select", "offcanvasDetailsPreview"]]
      .forEach(([selectId, panelId]) => {
        global.jQuery(`#${selectId}`).select2("open");
        const dropdown = document.querySelector(".select2-dropdown");
        expect(dropdown, `${selectId} opened no dropdown`).toBeTruthy();
        expect(dropdown.closest(`#${panelId}`), `${selectId} opens its list outside ${panelId}`).toBeTruthy();
        global.jQuery(`#${selectId}`).select2("close");
      });
  });

  test("seam 1 — a value written through the module repaints the box (the reset path)", () => {
    usersList.offcanvasSelects.setValue(created(), "Service");
    expect(created().value).toBe("Service");
    expect(shownText(created()), "the painted box still shows the previous choice").toBe("AccountKindService");

    // The reset the create offcanvas performs: back to the empty option, which MEANS Unknown.
    usersList.offcanvasSelects.setValue(created(), "");
    expect(created().value).toBe("");
    expect(shownText(created())).toBe("AccountKindUnknown");
  });

  test("seam 2 — hiding through the module hides the painted box too (the permission withdrawal)", () => {
    const el = quickView();
    usersList.offcanvasSelects.setHidden(el, true);
    expect(el.classList.contains("d-none")).toBe(true);
    expect(paintedBoxOf(el).classList.contains("d-none"), "the control the user can actually see stayed visible").toBe(true);

    usersList.offcanvasSelects.setHidden(el, false);
    expect(paintedBoxOf(el).classList.contains("d-none")).toBe(false);
  });

  test("the module uses those two functions — no raw .value or d-none on either select", () => {
    const source = read("wwwroot", "assets", "js", "Governance", "Users", "index.js");
    // A raw assignment or toggle would work in the markup and silently stop working under select2.
    expect(source).not.toMatch(/kindSelect\.value\s*=/);
    expect(source).not.toMatch(/kindSelect\.classList\.toggle\('d-none'/);
    expect(source).toMatch(/setSelectValue\(document\.getElementById\('userAccountKind'\), ''\)/);
    expect(source).toMatch(/setSelectValue\(kindSelect, normalizeAccountKind\(data\.accountKind\)\)/);
    expect(source).toMatch(/setSelectHidden\(kindSelect, !canManageKind\(\)\)/);
  });
});

/*
 * ── THE TYPE IS CHANGED FROM THE EDIT FORM TOO (owner report, 2026-09-23) ────────────────────────────────
 *
 * The owner opened Edit, looked for "change type", and found nothing: it lived only in the quick view. The
 * reason was sound — `UpdateUser` carries no kind, so a saveable box on the edit form would be the defect we
 * had just fixed on the task form, a promise the server drops. The cure is the same shape as the handover:
 * the field is SHOWN read-only and the change is offered as the audited call it already is.
 *
 * ⚠ AND ONE CALL, TWO DOORS. The quick view and the edit form now post through a single function. Two copies
 * of a write that carries a permission and an audit trail is how they end up disagreeing.
 */
describe("the account type can be changed from the edit form, without becoming a saveable field", () => {
  const offcanvas = () => read("Views", "Governance", "Users", "_CreateEditOffcanvas.cshtml");
  const js = () => read("wwwroot", "assets", "js", "Governance", "Users", "index.js");

  test("edit shows the type read-only, in the product's immutable-field paint", () => {
    const source = offcanvas();
    const row = source.slice(source.indexOf('id="userAccountKindReadRow"'), source.indexOf("AccountKindEditHint"));
    expect(row, "the read-only row is gone").toBeTruthy();
    expect(row).toMatch(/id="userAccountKindRead"[^>]*readonly/);
    expect(row, "an unpainted readonly box reads as editable").toMatch(/bg-label-secondary/);
    expect(row, "the change button is missing beside it").toContain('id="btnUserAccountKindChange"');
  });

  test("both rows sit inside the same explicit-grant-only gate", () => {
    const block = gatedBlockContaining(offcanvas(), 'id="userAccountKindReadRow"');
    expect(block, "the read-only row is drawn outside the account-kind.manage gate").toBeTruthy();
    expect(block, "the create picker and the edit row drifted apart").toContain('id="userAccountKindRow"');
  });

  test("create shows the picker, edit shows the read-only row — never both", () => {
    const source = js();
    expect(source).toMatch(/userAccountKindRow'\)\?\.classList\.toggle\('d-none', !isCreate\)/);
    expect(source).toMatch(/userAccountKindReadRow'\)\?\.classList\.toggle\('d-none', isCreate\)/);
  });

  test("the new value is chosen inside the SHARED confirm — this screen opens no dialog of its own", () => {
    const ask = js().slice(js().indexOf("const askAccountKindChange"), js().indexOf("const openCreateOffcanvas"));
    expect(ask).toContain("window.showConfirm");
    expect(ask).toMatch(/inputType: 'select'/);
    expect(ask).toMatch(/inputOptions:/);
    expect(ask, "the picker offers something the enum does not have").toMatch(/Unknown:[\s\S]{0,60}Human:[\s\S]{0,60}Service:/);
    expect(ask, "a no-op write is sent when the reader picks what is already set").toMatch(/kind === current/);
    expect(js(), "a second dialog was opened instead of the shared one").not.toMatch(/\bSwal\.fire\(/);
  });

  test("one network call serves both doors", () => {
    const source = js();
    const posts = source.match(/\/Users\/api\/\$\{id\}\/account-kind/g) || [];
    expect(posts.length, "the account-kind write exists in more than one place again").toBe(1);
    expect(source).toContain("const postAccountKind = async (id, kind, offcanvasToHide)");
    // Both doors reach it.
    expect(source).toMatch(/postAccountKind\(id, kind, getOcDetailsInstance\(\)\)/);
    expect(source).toMatch(/postAccountKind\(id, kind, getOcCreateEditInstance\(\)\)/);
  });

  test("the box shows a word, never the enum number", () => {
    expect(js()).toMatch(/kindRead\.value = accountKindLabel\(currentKind\)/);
  });

  ["AccountKindEditHint", "AccountKindNewLabel", "AccountKindNewRequired"].forEach((key) => {
    test(`${key} exists in all seven languages, translated`, () => {
      const en = new RegExp(`<data name="${key}"[^>]*>\\s*<value>([^<]+)</value>`)
        .exec(read("Resources", "Views", "Governance", "Users", "UsersIndex.en.resx"))[1];
      LANGS.forEach((lang) => {
        const resx = read("Resources", "Views", "Governance", "Users", `UsersIndex.${lang}.resx`);
        const m = new RegExp(`<data name="${key}"[^>]*>\\s*<value>([^<]+)</value>`).exec(resx);
        expect(m, `${key} missing in ${lang}`).toBeTruthy();
        if (lang !== "en") {
          expect(m[1], `${lang} carries untranslated English for ${key}`).not.toBe(en);
        }
      });
      expect(read("Views", "Governance", "Users", "_IndexL10n.cshtml"))
        .toMatch(new RegExp(`${key} = Localizer\\["${key}"\\]\\.Value`));
    });
  });
});
