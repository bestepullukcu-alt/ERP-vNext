const fs = require("fs");
const path = require("path");

/*
 * WP-AUTH-INVITED-LIFECYCLE-01 — the invited account on the tenant Users screen.
 *
 * The owner found three things live: a new user read "Passive" (the word for an account an administrator switched
 * off), "Activate" on it produced an "Active" account that still could not sign in, and a deleted user's e-mail
 * could never be used again. AuthService now derives a third status (Invited) and refuses the activation; these
 * tests pin what the SCREEN does with that, against the production index.js, views and resx — never a copy.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");
const js = () => read("wwwroot", "assets", "js", "Governance", "Users", "index.js");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const NEW_KEYS = ["StatusInvited", "InvitationPendingHint", "ErrorUserEmailTaken", "ErrorUserInvitationPending"];

/** The module's own userStatusOf, evaluated in isolation (the file is an IIFE bound to the DOM). */
const loadUserStatusOf = () => {
  const source = js();
  const start = source.indexOf("const userStatusOf");
  const end = source.indexOf("const getStatusMap");
  expect(start).toBeGreaterThan(-1);
  expect(end).toBeGreaterThan(start);
  // eslint-disable-next-line no-new-func
  return new Function(source.slice(start, end) + "; return userStatusOf;")();
};

describe("three states, as AuthService derives them", () => {
  test("the server's status wins over isActive — an invited account never reads Active or Passive", () => {
    const statusOf = loadUserStatusOf();
    expect(statusOf({ status: "Invited", isActive: false })).toBe("Invited");
    expect(statusOf({ status: "Invited", isActive: true })).toBe("Invited"); // the measured bad state
    expect(statusOf({ status: "Inactive", isActive: false })).toBe("Passive");
    expect(statusOf({ status: "Active", isActive: true })).toBe("Active");
    // A row without `status` (older service) falls back to isActive — never to Invited, which only the server knows.
    expect(statusOf({ isActive: true })).toBe("Active");
    expect(statusOf({ isActive: false })).toBe("Passive");
  });

  test("each state has its own word and its own colour", () => {
    const source = js();
    const map = source.slice(source.indexOf("const getStatusMap"), source.indexOf("const statusOfRow"));
    expect(map).toMatch(/Active: \{ title: L\(\)\.Active, class: 'bg-label-success' \}/);
    expect(map).toMatch(/Passive: \{ title: L\(\)\.Passive, class: 'bg-label-secondary' \}/);
    expect(map).toMatch(/Invited: \{ title: L\(\)\.StatusInvited, class: 'bg-label-info' \}/);
    const classes = [...map.matchAll(/class: '([^']+)'/g)].map((m) => m[1]);
    expect(new Set(classes).size, "two states share a colour").toBe(3);
  });

  test("the list, the filter, the quick view and the KPIs all read the derived status", () => {
    const source = js();
    // The column renders the row's status, not the boolean helper that only knows true/false.
    expect(source).toMatch(/\? renderStatusBadge\(full\)\s*: statusOfRow\(full\)\.title/);
    expect(source, "the column fell back to the two-state boolean badge").not.toMatch(/DitenDataTable\.renderStatusBadge\(data, getStatusMap\(\)\)/);
    // BL-440 package 5 — the FILTER and the KPIs read the status on the SERVICE now: the filter sends the service's
    // three words as `status`, and the cards show its tenant-wide summary (Passive = Inactive, never the invited).
    expect(source).toMatch(/\{ id: 'filterStatus', key: 'status', kind: 'multi' \}/);
    expect(source).toMatch(/const status = statusOfRow\(data\);/);
    expect(source).toMatch(/'kpi-users-active': 'active'/);
    expect(source).toMatch(/'kpi-users-passive': 'passive'/);
    const filter = read("Views", "Governance", "Users", "_Filter.cshtml");
    ["Active", "Inactive", "Invited"].forEach((v) => expect(filter).toContain(`<option value="${v}">`));
    expect(filter, "the service refuses 'Passive' (USERS_LIST_STATUS_INVALID) — the value is its word, Inactive").not.toContain('<option value="Passive">');
    expect(filter).toMatch(/<option value="Inactive">@SharedLocalizer\["Passive"\]<\/option>/);
    expect(filter).toMatch(/<option value="Invited">@Localizer\["StatusInvited"\]<\/option>/);
  });
});

describe("no administrator activation of an invited account, and the reason is on screen", () => {
  test("the kebab offers 'Activate' only to an account that is not invited", () => {
    const source = js();
    const enable = source.split("\n").find((l) => l.includes("adminAction('enable'"));
    expect(enable, "the kebab no longer draws 'Activate'").toBeTruthy();
    expect(enable).toMatch(/if \(canUpdate\(\) && !full\.isActive && userStatusOf\(full\) !== 'Invited'\)/);
  });

  test("the edit form swaps the active switch for the reason when the account is invited", () => {
    const offcanvas = read("Views", "Governance", "Users", "_CreateEditOffcanvas.cshtml");
    expect(offcanvas).toMatch(/<div class="col-12 d-none" id="userInvitePendingRow">\s*<div class="form-text">@Localizer\["InvitationPendingHint"\]<\/div>/);
    const source = js();
    const open = source.slice(source.indexOf("const loadFormFields"), source.indexOf("const ERROR_CODE_KEYS"));
    expect(open).toMatch(/const invited = userStatusOf\(d\) === 'Invited';/);
    expect(open).toMatch(/userActiveRow'\)\?\.classList\.toggle\('d-none', invited\)/);
    expect(open).toMatch(/userInvitePendingRow'\)\?\.classList\.toggle\('d-none', !invited\)/);
    // Create never shows the reason row.
    const mode = source.slice(source.indexOf("const setCreateMode"), source.indexOf("const resetFormFields"));
    expect(mode).toMatch(/userInvitePendingRow'\)\?\.classList\.add\('d-none'\)/);
  });

  test("why reset and activate are absent: the quick view says it, and so does the one action that remains", () => {
    const quick = read("Views", "Governance", "Users", "_DetailsQuickView.cshtml");
    expect(quick).toMatch(/<div id="oc-invite-pending-hint" class="form-text d-none">@Localizer\["InvitationPendingHint"\]<\/div>/);
    const source = js();
    expect(source).toMatch(/oc-invite-pending-hint'\)\?\.classList\.toggle\('d-none', userStatusOf\(data\) !== 'Invited'\)/);
    expect(source).toMatch(/adminAction\('resend', full, rowJson, userStatusOf\(full\) === 'Invited' \? \{ title: L\(\)\.InvitationPendingHint/);
    // Reset stays hidden while a password change is pending — the rule itself is unchanged.
    expect(source).toMatch(/canUpdate\(\) && full\.isActive && !full\.mustChangePassword/);
  });
});

describe("refusals tagged with a stable code are shown in the reader's language", () => {
  test("the two codes map to their keys, and both the form and the kebab actions use the mapping", () => {
    const source = js();
    expect(source).toMatch(/USER_EMAIL_TAKEN: 'ErrorUserEmailTaken'/);
    expect(source).toMatch(/USER_INVITATION_PENDING: 'ErrorUserInvitationPending'/);
    // BL-440 package 5: the factory shows `json.errors`; the page's submit hands it the LOCALIZED list.
    expect(source).toMatch(/if \(!json\.success\) return Object\.assign\(\{\}, json, \{ errors: localizedErrors\(json\) \}\);/);
    expect(source).toMatch(/throw new Error\(localizedErrors\(json\)\[0\] \|\| L\(\)\.ErrorOccurred\)/);
    expect(source, "the form still shows the raw gateway text").not.toMatch(/if \(!json\.success\) return json;/);
  });

  test("the proxy hands the code over on create, edit and every kebab action", () => {
    const controller = read("Controllers", "UsersController.cs");
    expect(controller).not.toMatch(/Json\(new \{ success = false, errors = await ExtractGatewayErrorsAsync\(response\) \}\)/);
    expect((controller.match(/: await GatewayFailureAsync\(response\);/g) || []).length).toBe(3);
    expect(controller).toMatch(/errorCode = await ExtractGatewayErrorCodeAsync\(response\)/);
  });
});

describe("seven languages", () => {
  LANGS.forEach((lang) => {
    test(`UsersIndex.${lang}.resx carries the four new keys`, () => {
      const resx = read("Resources", "Views", "Governance", "Users", `UsersIndex.${lang}.resx`);
      NEW_KEYS.forEach((key) => {
        const m = new RegExp(`<data name="${key}" xml:space="preserve"><value>([^<]+)</value></data>`).exec(resx);
        expect(m, `${key} missing in ${lang}`).toBeTruthy();
        expect(m[1].trim().length).toBeGreaterThan(0);
      });
    });
  });

  test("non-English values are translated, not English copies", () => {
    const value = (lang, key) => new RegExp(`<data name="${key}" xml:space="preserve"><value>([^<]+)</value>`)
      .exec(read("Resources", "Views", "Governance", "Users", `UsersIndex.${lang}.resx`))[1];
    LANGS.filter((l) => l !== "en").forEach((lang) => {
      const same = NEW_KEYS.filter((k) => value(lang, k) === value("en", k));
      expect(same, `${lang} carries untranslated English for: ${same.join(", ")}`).toEqual([]);
    });
  });

  test("the bridge and the loader's required list carry the keys the JS reads", () => {
    const bridge = read("Views", "Governance", "Users", "_IndexL10n.cshtml");
    const loader = read("wwwroot", "assets", "js", "Governance", "Users", "index.l10n.js");
    NEW_KEYS.forEach((key) => {
      expect(bridge).toMatch(new RegExp(`${key} = Localizer\\["${key}"\\]\\.Value`));
      expect(loader).toContain(`'${key}'`);
    });
  });
});
