const fs = require("fs");
const path = require("path");

/*
 * UAS-001 — A PAGE THE USER MAY NOT READ DRAWS NOTHING BUT THE EXPLANATION.
 *
 * WHAT WAS SEEN, and why no test saw it. The owner opened /Roles with a user holding no roles permission and got:
 * the heading, four KPI cards reading 0, a "+ Add Role" button, a spinner that never stopped, "0 of 0 records",
 * and an English "Permission denied" toast over the top. Nothing was broken — the backend refused correctly. The
 * SCREEN was the defect, and no test could have caught it, because the test user holds every permission. A guard
 * for this rule has to read the MARKUP's decision, not exercise the page.
 *
 * WHAT IS PINNED HERE:
 *   1. the gate exists, on the canonical key, and stands BEFORE anything the page would draw
 *   2. the refusal is a partial inside the shell — never a redirect, never the shell-less 401 page
 *   3. no technical text reaches the user, and the message says what to DO
 *   4. all seven languages carry it
 *
 * ⚠ SCOPE. Two screens this round. The other 35 controllers that carry only [Authorize] are named in
 * docs/roadmap/backlog/product-backlog.md, not silently blessed here.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");

const PARTIAL = () => read("Views", "Shared", "_AccessDenied.cshtml");
/*
 * What the USER actually receives: the partial with its Razor comment block and its directives removed. The
 * comment block quotes "403", "Forbidden" and "Permission denied" on purpose — it is the file explaining what it
 * must never render — and a guard that reads it would report the documentation as the defect.
 */
const PARTIAL_BODY = () => PARTIAL()
  .replace(/@\*[\s\S]*?\*@/g, "")
  .replace(/^\s*@(?:model|using|inject|inherits)\b.*$/gm, "");

/** The screens ported in this round, with the canonical PKS-001 key each one's data actually needs. */
const GATED = {
  "Governance/Roles/Index.cshtml": "auth.roles.read",
  "Governance/Users/Index.cshtml": "auth.users.read",
  // Added when the Role Permissions screen was gated. The map is the guard: a screen absent from it is
  // simply not checked, which is why removing the gate from this very view broke nothing until this line existed.
  "Governance/RoleAssignments/Index.cshtml": "auth.roles.assign-permission",
  "Governance/UserRoleAssignments/Index.cshtml": "auth.users.assign-role",
  // WP-USERS-ROLES-SMALL-01 (BL-617) — the permission catalog, on the key AuthService reads it with.
  "Governance/Permissions/Index.cshtml": "auth.roles.read",
  // MOD-0357 S3 — the four Meetings screens, each on the key its own data actually needs (create/update stay
  // separate from read: a caller who may read a meeting is not automatically who may write one).
  "Meetings/Index.cshtml": "platform.meetings.read",
  "Meetings/Create.cshtml": "platform.meetings.create",
  "Meetings/Edit.cshtml": "platform.meetings.update",
  "Meetings/Details.cshtml": "platform.meetings.read",
  // S8 — the meeting-type setting screen, gated on types-manage alone (no separate read key for it: managing
  // the catalogue and choosing a type from it are different actions, and choosing needs only meetings.read).
  "Meetings/MeetingTypes/Index.cshtml": "platform.meetings.types-manage",
  "Meetings/MeetingTypes/Create.cshtml": "platform.meetings.types-manage",
  "Meetings/MeetingTypes/Edit.cshtml": "platform.meetings.types-manage",
  // WP-MDM-BRAND-ACCEPT-01 FIX1 (item 5) — the Brand pages.
  "MasterData/Brands/Index.cshtml": "mdm.brands.read",
  "MasterData/Brands/Create.cshtml": "mdm.brands.create",
  "MasterData/Brands/Edit.cshtml": "mdm.brands.update",
  "MasterData/Brands/Details.cshtml": "mdm.brands.read",
  // WP-URA-FIX-01 FIX2 — the MDM screens whose controller answers the page's 403 itself (measured: the completeness
  // test below derives that set from the controllers and fails while any of them is missing here).
  "MasterDataManagement/GlobalProducts/Index.cshtml": "mdm.global-products.read",
  "MasterDataManagement/FinishedGoods/Index.cshtml": "mdm.finished-goods.read",
  "MasterDataManagement/Gskus/Index.cshtml": "mdm.gskus.read",
  "MasterDataManagement/Lskus/Index.cshtml": "mdm.lskus.read"
};

/*
 * WP-URA-FIX-01 FIX2 — views whose CONTROLLER answers the page's status code (`Response.StatusCode = 403`) are DERIVED
 * from the controllers, never listed by hand: a controller that refuses with its own reader (PermissionClaims) and a
 * view that decides with another (the Perms snapshot, exact `permission` claims only) disagree for "*", a plural
 * `permissions` claim or a comma-separated list — 200 with a refused face, or 403 with the page. For these views the
 * claims reader is REQUIRED (GlobalProducts EK item 11; URA FIX1).
 */
const CONTROLLERS = path.join(repoRoot, "frontend", "Diten.Web", "Controllers");
const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true })
  .flatMap((entry) => entry.isDirectory() ? walk(path.join(dir, entry.name)) : [path.join(dir, entry.name)]);
const STATUS_ANSWERED = (() => {
  const views = new Set();
  walk(CONTROLLERS).filter((file) => file.endsWith(".cs")).forEach((file) => {
    const code = fs.readFileSync(file, "utf8");
    if (!/Response\.StatusCode\s*=\s*(?:StatusCodes\.Status403Forbidden|403)\b/.test(code)) { return; }
    const root = (/const string ViewRoot\s*=\s*"~\/Views\/([^"]+)"/.exec(code) || [])[1];
    for (const m of code.matchAll(/View\(\$?"(?:~\/Views\/([^"{}]+\.cshtml)|\{ViewRoot\}\/(\w+)\.cshtml)"/g)) {
      if (m[1]) { views.add(m[1]); } else if (root) { views.add(`${root}/${m[2]}.cshtml`); }
    }
    // a `View($"{ViewRoot}/{view}.cshtml")` helper (Brands' Denied) serves the views the file names elsewhere
    if (root && /View\(\$"\{ViewRoot\}\/\{\w+\}\.cshtml"/.test(code)) {
      for (const m of code.matchAll(/(?:Denied|View)\("(\w+)"/g)) { views.add(`${root}/${m[1]}.cshtml`); }
    }
  });
  return [...views].filter((view) => fs.existsSync(web("Views", view))).sort();
})();

/** Status-answered views that decide through the CONTROLLER's own answer instead of reading claims: named, with why. */
const CONTROLLER_DECIDED = {
  "MasterDataManagement/ProductLegalEntityScopes/Index.cshtml":
    "reads ViewData[\"CanReadProductScope\"], which the controller sets from PermissionClaims itself — one reader by " +
    "construction; HTTP-tested by ProductLegalEntityScopesPageRenderTests"
};

/** Razor comments removed: a gate that only exists inside @* … *@ gates nothing. */
const stripRazorComments = (source) => source.replace(/@\*[\s\S]*?\*@/g, "");

const gateReaders = (key) => {
  const escaped = key.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  return {
    snapshot: new RegExp(`Perms\\.Has\\(\\s*"${escaped}"\\s*\\)`),
    // the full name, or the short one under `@using Diten.Web.Security`; any spacing inside the call
    claims: new RegExp(`(?:Diten\\.Web\\.Security\\.)?PermissionClaims\\.HasPermission\\(\\s*User\\s*,\\s*"${escaped}"\\s*\\)`)
  };
};

/**
 * The guard's DECISION, as a function, so it is itself tested on made-up view sources below (FIX2: CT sabotage U4 turned
 * the assertion into `expect(true)` and nothing noticed). Returns what is wrong; an empty list means the gate is right.
 */
const gateProblems = (rawSource, key, { claimsRequired = false } = {}) => {
  const source = stripRazorComments(rawSource);
  const { snapshot, claims } = gateReaders(key);
  const problems = [];
  const claimsCall = claims.exec(source);
  if (!snapshot.test(source) && !claimsCall) { problems.push(`the view does not gate on ${key}`); }
  if (claimsRequired && !claimsCall) {
    problems.push(`the controller answers this page's status through PermissionClaims; the view must read ${key} with it`);
  }
  if (claimsCall && !claimsCall[0].startsWith("Diten.") && !/^\s*@using\s+Diten\.Web\.Security\s*$/m.test(source)) {
    problems.push("PermissionClaims is named without `@using Diten.Web.Security`");
  }
  if (snapshot.test(source) && !/@inject\s+Diten\.Web\.Services\.IPermissionSnapshot\s+Perms/.test(source)) {
    problems.push("the view does not inject the permission snapshot");
  }
  return problems;
};

describe("the gate stands in front of the page, not beside it", () => {
  Object.entries(GATED).forEach(([file, key]) => {
    const source = () => read("Views", file);

    /*
     * Two readers are accepted: the UX snapshot (`Perms.Has`, injected) and the controller's own reader
     * (`PermissionClaims.HasPermission(User, …)`). Where the controller answers the page's status code, only the
     * second (FIX2, derived set above).
     */
    const { snapshot: snapshotGate, claims: claimsGate } = gateReaders(key);
    const claimsRequired = STATUS_ANSWERED.includes(file);
    test(`${file} checks ${key}${claimsRequired ? " with the controller's reader" : ""}`, () => {
      expect(gateProblems(source(), key, { claimsRequired })).toEqual([]);
    });

    test(`${file} renders the refusal and stops before drawing anything`, () => {
      const s = stripRazorComments(source());
      const gate = Math.max(s.search(snapshotGate), s.search(claimsGate));
      const partial = s.indexOf('name="_AccessDenied"');
      const stop = s.indexOf("return;", gate);

      expect(partial, "the refusal partial is never rendered").toBeGreaterThan(-1);
      expect(stop, "the view falls through and keeps rendering").toBeGreaterThan(-1);
      expect(partial, "the partial is rendered before the gate is even evaluated").toBeGreaterThan(gate);
      expect(stop, "the partial renders but the page body follows it anyway").toBeGreaterThan(partial);

      /*
       * …and the page's OWN surface starts after the stop. This is the assertion that actually describes the
       * defect: a heading, a KPI card or a table that begins before `return;` is the half-drawn screen.
       */
      ["<h5", "kpi-", "_DataTable", "_Filter", "_CreateEditOffcanvas"].forEach((marker) => {
        const at = s.indexOf(marker);
        if (at < 0) { return; }
        expect(at, `\`${marker}\` is drawn before the unauthorized user is stopped`).toBeGreaterThan(stop);
      });

      // FIX2 — and NO markup line at all comes before the gate (a root `<div id="raRoot" data-…>` moved above it is the
      // page again, whatever it is called). C# lines in @{ } never start with "<"; @inject's generics are mid-line.
      const markupBeforeGate = s.slice(0, gate).split("\n").map((line) => line.trim()).filter((line) => line.startsWith("<"));
      expect(markupBeforeGate, "markup is drawn before the gate").toEqual([]);
    });

    test(`${file} does not redirect the unauthorized user away`, () => {
      // UAS-001 §2: the address bar stays right, the back button does not loop, and a granted permission is one
      // refresh away. A redirect destroys all three.
      const s = source();
      expect(s, "the view sends the user somewhere else instead of explaining")
        .not.toMatch(/RedirectToAction|Response\.Redirect|Context\.Response\.Redirect|location\.href/);
      expect(s, "the view falls back to the shell-less 401 page, which strips the user's menu")
        .not.toContain("NotAuthorized");
    });
  });
});

describe("WP-URA-FIX-01 FIX2: the guard's own decision, measured on made-up views", () => {
  const KEY = "auth.users.assign-role";
  const page = (gate, head = "") => `${head}@{ Layout = "_LayoutTenantShell"; }\n${gate}\n{\n    <partial name="_AccessDenied" model="@Localizer["Title"].Value" />\n    return;\n}\n<h5>x</h5>`;

  test("a view with NO gate is refused", () => {
    expect(gateProblems(page("<div></div>"), KEY)).not.toEqual([]);
  });

  test("a gate that exists only inside a Razor comment is refused", () => {
    expect(gateProblems(page(`@* @if (!Perms.Has("${KEY}")) *@`, "@inject Diten.Web.Services.IPermissionSnapshot Perms\n"), KEY))
      .not.toEqual([]);
  });

  test("the snapshot reader is refused where the controller answers the status", () => {
    const snapshotView = page(`@if (!Perms.Has("${KEY}"))`, "@inject Diten.Web.Services.IPermissionSnapshot Perms\n");
    expect(gateProblems(snapshotView, KEY)).toEqual([]);
    expect(gateProblems(snapshotView, KEY, { claimsRequired: true })).not.toEqual([]);
  });

  test("the claims reader passes: full name, or short name under @using, with any spacing", () => {
    expect(gateProblems(page(`@if (!Diten.Web.Security.PermissionClaims.HasPermission(User, "${KEY}"))`), KEY, { claimsRequired: true }))
      .toEqual([]);
    expect(gateProblems(page(`@if (!PermissionClaims.HasPermission( User,"${KEY}" ))`, "@using Diten.Web.Security\n"), KEY,
      { claimsRequired: true })).toEqual([]);
  });

  test("the short name WITHOUT @using is refused (it would not compile, and must not pass the guard)", () => {
    expect(gateProblems(page(`@if (!PermissionClaims.HasPermission(User, "${KEY}"))`), KEY, { claimsRequired: true }))
      .not.toEqual([]);
  });

  test("a gate on ANOTHER key is refused", () => {
    expect(gateProblems(page('@if (!Diten.Web.Security.PermissionClaims.HasPermission(User, "auth.users.read"))'), KEY))
      .not.toEqual([]);
  });
});

describe("WP-URA-FIX-01 FIX2: every view whose controller answers its 403 is in the guard's map", () => {
  test("the derived set reaches the screens it must (guards against a vacuous scan)", () => {
    expect(STATUS_ANSWERED).toEqual(expect.arrayContaining([
      "Governance/UserRoleAssignments/Index.cshtml",
      "Governance/RoleAssignments/Index.cshtml",
      "MasterDataManagement/GlobalProducts/Index.cshtml",
      "MasterData/Brands/Index.cshtml"
    ]));
    expect(STATUS_ANSWERED.length).toBeGreaterThanOrEqual(8);
  });

  test("each is in GATED (and so read with the controller's reader) or named as controller-decided, with why", () => {
    const missing = STATUS_ANSWERED.filter((view) => !(view in GATED) && !(view in CONTROLLER_DECIDED));
    expect(missing, "a status-answered view the guard does not check").toEqual([]);
  });

  test("a controller-decided view really reads the controller's decision", () => {
    Object.keys(CONTROLLER_DECIDED).forEach((view) => {
      expect(STATUS_ANSWERED, `${view} is no longer status-answered; drop it from CONTROLLER_DECIDED`).toContain(view);
      expect(read("Views", view)).toMatch(/ViewData\["CanRead\w+"\]/);
    });
  });
});

describe("the refusal itself", () => {
  test("is a partial, drawn inside the shell", () => {
    // The 401 page sets `Layout = null` and is right to; at 403 the user is signed in and the menu is theirs.
    expect(PARTIAL_BODY(), "the 403 surface tears the shell away like the 401 page does")
      .not.toMatch(/Layout\s*=\s*null/);
    expect(PARTIAL_BODY(), "the surface is a whole document rather than a fragment")
      .not.toMatch(/<html|<!DOCTYPE/i);
  });

  test("wears .card, or it resolves no background and reads as a page that failed to draw", () => {
    // Same reasoning the Task Center's empty state is pinned to: --bs-card-bg is card-scoped, and a non-card
    // borrowing it resolves nothing.
    expect(PARTIAL()).toMatch(/class="card diten-access-denied"/);
  });

  test("styles through a class, never an inline style (FG-003)", () => {
    expect(PARTIAL(), "the partial carries an inline style").not.toMatch(/style="/);
    const css = fs.readFileSync(web("wwwroot", "assets", "css", "backbone-custom.css"), "utf8");
    expect(css, ".diten-access-denied has no rule of its own").toMatch(/\.diten-access-denied\s*[,{]/);
    /*
     * Not --bs-danger. UAS-001 §6: a missing permission is not a fault, and dressing it in the product's error
     * red is the toast defect again, one layer down.
     */
    const icon = /\.diten-access-denied\s*>\s*i\s*\{([^}]*)\}/.exec(css);
    expect(icon, "the icon has no rule").toBeTruthy();
    expect(icon[1], "the refusal is painted as an error").not.toMatch(/--bs-danger/);
  });

  test("shows the user no technical text", () => {
    // UAS-001 §5. "403"/"Forbidden"/"Permission denied" tell the user nothing they can act on.
    expect(PARTIAL_BODY()).not.toMatch(/\b403\b|Forbidden|Permission denied/);
  });

  test("names WHICH screen is closed, and takes that name from the caller", () => {
    // A refusal that does not say what was refused is indistinguishable from a broken page.
    expect(PARTIAL(), "the title does not interpolate the screen name")
      .toMatch(/SharedLocalizer\["AccessDeniedTitle",\s*Model\]/);
    Object.keys(GATED).forEach((file) => {
      expect(read("Views", file), `${file} does not pass its own screen name to the refusal`)
        .toMatch(/<partial name="_AccessDenied" model="@Localizer\["\w*Title"\]\.Value" \/>/);
    });
  });

  test("hardcodes nothing — every string comes from a resource", () => {
    const body = PARTIAL_BODY();
    const stray = [...body.matchAll(/>\s*([A-Za-zÇĞİÖŞÜçğıöşü][^<>@]{3,})\s*</g)].map((m) => m[1].trim());
    expect(stray, "these strings are hardcoded instead of localized").toEqual([]);
  });
});

describe("the refusal speaks all seven languages", () => {
  const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

  LANGS.forEach((lang) => {
    test(`${lang} carries both keys, with real text`, () => {
      const resx = read("Resources", `SharedResource.${lang}.resx`);
      ["AccessDeniedTitle", "AccessDeniedMessage"].forEach((key) => {
        const entry = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(resx);
        expect(entry, `${lang} is missing ${key}`).toBeTruthy();
        expect(entry[1].trim().length, `${lang}'s ${key} is empty`).toBeGreaterThan(3);
      });
    });

    test(`${lang}'s title keeps the {0} the caller fills`, () => {
      // A translation that drops the placeholder silently loses the screen's name — the one thing that makes
      // the refusal legible.
      const resx = read("Resources", `SharedResource.${lang}.resx`);
      const title = /name="AccessDeniedTitle"[^>]*>\s*<value>([\s\S]*?)<\/value>/.exec(resx)[1];
      expect(title, `${lang}'s title dropped {0}`).toContain("{0}");
    });
  });

  test("no translation leaks a status code at the user", () => {
    LANGS.forEach((lang) => {
      const resx = read("Resources", `SharedResource.${lang}.resx`);
      ["AccessDeniedTitle", "AccessDeniedMessage"].forEach((key) => {
        const value = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(resx)[1];
        expect(value, `${lang}'s ${key} shows a status code`).not.toMatch(/\b40[13]\b|Forbidden/);
      });
    });
  });
});
