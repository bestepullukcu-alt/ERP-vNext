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
  "Governance/RoleAssignments/Index.cshtml": "auth.roles.assign-permission"
};

describe("the gate stands in front of the page, not beside it", () => {
  Object.entries(GATED).forEach(([file, key]) => {
    const source = () => read("Views", file);

    test(`${file} checks ${key}`, () => {
      expect(source(), "the view does not inject the permission snapshot")
        .toMatch(/@inject\s+Diten\.Web\.Services\.IPermissionSnapshot\s+Perms/);
      expect(source(), `the view does not gate on ${key}`)
        .toMatch(new RegExp(`Perms\\.Has\\("${key.replace(/\./g, "\\.")}"\\)`));
    });

    test(`${file} renders the refusal and stops before drawing anything`, () => {
      const s = source();
      const gate = s.indexOf("Perms.Has(");
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
