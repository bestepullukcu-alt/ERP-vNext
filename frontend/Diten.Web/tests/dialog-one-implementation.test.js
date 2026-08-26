const fs = require("fs");
const path = require("path");

/*
 * THE GUARD — ONE CONFIRM IMPLEMENTATION, AND A CENSUS THAT CANNOT LIE (2026-08-24).
 *
 * WHY THIS FILE EXISTS. This product has one confirm dialog (`window.showConfirm`, declared in
 * `Views/Shared/_GlobalConfirmation.cshtml`) and three rule files that mandate it — `frontend-js-standard.md`,
 * `frontend-datatable-template.md`, `premium-modal-standard.md`. Measured against that mandate: 12 files call
 * `Swal.fire` directly anyway. A rule with no check is a wish.
 *
 * ⚠ AND THE CHECK MUST SEE BOTH CALL FORMS. The previous census matched only `showConfirm(` and therefore
 * missed `showConfirm?.(` — 16 plain against 58 optional. It reported 15 call sites where there were 74, and
 * it was GREEN the whole time. A guard that reports a fifth of the surface is worse than no guard, because it
 * is believed. Every regex here matches both.
 *
 * ⚠ NO HARD COUNTS. A `toBe(74)` breaks on the next legitimate caller and teaches the reader to raise the
 * number instead of looking. What is pinned is the RULE — "no raw dialog outside this named list" — and the
 * list is names, not a total.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const JS_ROOT = web("wwwroot", "assets", "js");

/** Both call forms of the shared confirm. */
const CALL = /showConfirm\s*\??\.?\s*\(/g;
/** A dialog opened WITHOUT the shared component. `window.`/`global.` prefixed forms included. */
const RAW = /(?:window\.|global\.)?Swal\.fire\s*\(/g;

const jsFiles = () => {
  const out = [];
  const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).forEach((e) => {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) { if (e.name !== "vendor" && e.name !== "node_modules") { walk(p); } }
    else if (e.name.endsWith(".js")) { out.push(p); }
  });
  walk(JS_ROOT);
  return out;
};
const rel = (f) => path.relative(JS_ROOT, f);
const read = (f) => fs.readFileSync(f, "utf8");
const count = (text, re) => (text.match(re) || []).length;

/*
 * ── THE EXCEPTION LIST ────────────────────────────────────────────────────────────────────────────────────
 *
 * Every file here opened a dialog without the shared component BEFORE this guard existed. They are listed so
 * the guard can be true today; each one is a debt with a named owner, not a blessing.
 *
 * ⚠ THE LOGIN FLOW IS DELIBERATELY UNTOUCHED (owner + CONTROL TOWER, 2026-08-24): `login.js`,
 * `forgot-password.js` and `reset-password.js` gate every entry into the product. If they break, nobody gets
 * in. They move only in a round of their own, after the surfaces behind them are settled.
 *
 * TO REMOVE A LINE: port that file to `window.showConfirm` and delete its entry. Never add a line to make a
 * red test green — that is the failure this file was written to stop.
 */
const KNOWN_RAW = [
  "Account/forgot-password.js",
  "Account/login.js",
  "Account/reset-password.js",
  "DocumentManagement/TemplateMasters/index.js",
  "Platform/Administrators/index.js",
  "Platform/AuditRetention/index.js",
  "WorkCenter/task-detail.js",
  "WorkCenterNext/app.js",
  "diten-unauthorized.js",
  "pages/demand-ideas/demandIdeaCapture.js",
  "pages/demand-ideas/demandIdeaRowActions.js",
  "pages/demand-ideas/demandIdeasList.js"
];

describe("one confirm implementation, product-wide", () => {
  it("opens no dialog outside the shared component, except the files named here", () => {
    /*
     * MUTATION GUARD: write `Swal.fire(` in any file not on the list — a new module, a new module's delete
     * handler — and this goes red with that file's path in the message. That is the whole point: a module
     * merged by another developer inherits this product's dialog design automatically IF it calls
     * `showConfirm`, and is stopped here if it does not.
     */
    const offenders = jsFiles()
      .filter((f) => RAW.test(read(f)) || (RAW.lastIndex = 0, count(read(f), RAW) > 0))
      .map(rel)
      .filter((f) => !KNOWN_RAW.includes(f))
      .sort();
    expect(offenders,
      "a dialog was opened without window.showConfirm — call the shared component, or add the file to KNOWN_RAW with a reason")
      .toEqual([]);
  });

  it("keeps the exception list honest — every named file still has a raw call", () => {
    // A stale exception is a hole: the file was fixed, the licence stayed, and the next raw call slips in free.
    const stale = KNOWN_RAW.filter((f) => {
      const full = path.join(JS_ROOT, f);
      return !fs.existsSync(full) || count(read(full), RAW) === 0;
    });
    expect(stale, "these files no longer open a raw dialog — remove them from KNOWN_RAW").toEqual([]);
  });

  it("counts the shared confirm's real surface, both call forms", () => {
    /*
     * ⚠ REPORTED, NOT PINNED. The number moves whenever a module is added, and that is fine — what must not
     * happen is the number being measured with a regex that cannot see `showConfirm?.(`, which is how "15"
     * survived a whole session while the truth was 74.
     */
    const callers = jsFiles().filter((f) => count(read(f), CALL) > 0);
    const total = callers.reduce((n, f) => n + count(read(f), CALL), 0);

    // Sanity floor: the shared confirm is used across the product, not by a handful of files.
    expect(callers.length, "the census collapsed — is the matcher seeing both call forms?").toBeGreaterThan(30);
    expect(total).toBeGreaterThan(callers.length);

    // The optional-chaining form is the MAJORITY here; a matcher blind to it undercounts by ~4x.
    const optional = jsFiles().reduce((n, f) => n + count(read(f), /showConfirm\?\.\(/g), 0);
    expect(optional, "the form that broke the last census disappeared — verify before trusting this")
      .toBeGreaterThan(total / 2);
  });

  it("declares the implementation in exactly one place, plus one named fallback", () => {
    /*
     * ⚠ `=` AND NOT `==`. The first version of this matched `window.showConfirm === 'function'` — the guard
     * clause six other files use — and reported five "second implementations" that were type checks.
     */
    const ASSIGN = /window\.showConfirm\s*=[^=]/;

    expect(ASSIGN.test(read(web("Views", "Shared", "_GlobalConfirmation.cshtml"))),
      "the shared confirm lost its declaration").toBe(true);

    /*
     * ⚠ ONE ASSIGNMENT IN JS IS ALLOWED, AND IT IS NOT A SECOND DESIGN: `backbone-shell.js` installs a native
     * `window.confirm` FALLBACK for the case where `_GlobalConfirmation.cshtml` never loaded. Without it a page
     * missing the partial would swallow the action silently instead of asking. It is legitimate precisely
     * BECAUSE it is gated — it must never overwrite a real implementation.
     */
    const assigners = jsFiles().filter((f) => ASSIGN.test(read(f))).map(rel).sort();
    expect(assigners, "a second confirm implementation appeared").toEqual(["backbone-shell.js"]);

    const shell = read(path.join(JS_ROOT, "backbone-shell.js"));
    expect(shell, "the fallback stopped being a fallback — it would now shadow the real dialog")
      .toContain("if (typeof window.showConfirm !== 'undefined') return;");
  });
});
