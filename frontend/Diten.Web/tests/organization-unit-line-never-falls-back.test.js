const fs = require("fs");
const path = require("path");

/*
 * ══ TWO RULES THAT ONLY EXISTED AS COMMENTS ═══════════════════════════════════════════════════════════════
 *
 * MEASURED (2026-09-07, Control Tower verification of MOD-0288-FU03): the forbidden fallback was added by hand
 * to form.js —
 *
 *     setSelect('ouAdministrativeParentId', d.administrativeParentOrganizationUnitId || d.parentOrganizationUnitId)
 *
 * — and the full suite ran 183 of 183 GREEN with that line in the file. Nothing measured it. The rule was
 * written, carefully, as a comment directly above the line it protects, and a comment stops nobody.
 *
 * ── WHY THE RULE MATTERS MORE THAN IT LOOKS ──────────────────────────────────────────────────────────────
 * MOD-0288-FU02 decision 2: when a unit has no administrative parent, it HAS no administrative manager. It does
 * not inherit the functional one. A fallback collapses two reporting lines into one and destroys the exact
 * distinction the whole feature exists to record — and it does so invisibly, because the screen then shows a
 * plausible name in a field that should be empty. The next developer meets an empty select, thinks it looks
 * unfinished, and "fixes" it. This file is what argues back.
 *
 * ── THE SECOND RULE, FROM A BUG THAT SHIPPED SILENTLY ────────────────────────────────────────────────────
 * A `titleCase()` helper resolved stored enum values for selects. It worked for one-word members and broke on
 * the first two-word one: `GroupFunction` → "Groupfunction" matched no option, the select fell back to empty,
 * and the next Save wrote `Department`. Opening a Group-function unit and pressing Save silently changed its
 * type. `HQ` had already needed a hard-coded exception for the same reason; the second exception was the
 * signal. The fix asks the select what it actually offers instead of guessing at the string.
 *
 * ── WHAT THESE TESTS ARE, AND WHAT THEY ARE NOT ──────────────────────────────────────────────────────────
 * They read the production source and assert on its shape. That is weaker than driving the behaviour, and it
 * is deliberate: `optionValueFor` and `populate` live inside an IIFE, so exercising them means either
 * refactoring the module for the test's convenience or re-implementing the logic here. The second is how this
 * repository has been bitten twice — a test that measures its own copy of a rule proves only that the copy
 * works. Reading the real file is honest about being a shape check while still failing on the exact edit that
 * went unnoticed.
 *
 * If form.js is ever refactored to export these functions, replace this file with behavioural tests. Until
 * then, this is the guard that would have gone red.
 */

const FORM_JS = path.resolve(
  __dirname,
  "..",
  "wwwroot/assets/js/Organization/OrganizationUnits/form.js"
);

const source = () => fs.readFileSync(FORM_JS, "utf8");

/*
 * Comments are where these rules are EXPLAINED, so a naive scan finds the forbidden shapes in the very prose
 * that forbids them. Both assertions below read code only.
 */
const codeOnly = () =>
  source()
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .split("\n")
    .filter((line) => !line.trim().startsWith("//"))
    .join("\n");

describe("organization unit reporting lines", () => {
  test("the file under test exists and is not empty", () => {
    // A shape check that reads nothing passes vacuously; this is the floor.
    const text = source();
    expect(text.length).toBeGreaterThan(500);
    expect(text).toContain("ouAdministrativeParentId");
  });

  test("the administrative line never falls back to the functional parent", () => {
    const text = source();

    /*
     * The rule is narrow: the administrative value must never be replaced BY THE FUNCTIONAL ONE.
     * `admin || ''` is not a fallback — it is a null-to-empty-string normalisation, and forbidding it would
     * make this guard cry wolf until someone deletes it.
     */
    const fallback =
      /administrativeParentOrganizationUnitId\s*(\|\||\?\?)\s*[A-Za-z_$.]*\bparentOrganizationUnitId\b|ouAdministrativeParentId'?\s*,[^)]*\bd\.parentOrganizationUnitId\b/;

    const offending = codeOnly()
      .split("\n")
      .map((line, i) => ({ line: line.trim(), no: i + 1 }))
      .filter(({ line }) => !line.startsWith("*") && !line.startsWith("//"))
      .filter(({ line }) => fallback.test(line));

    expect(
      offending.map(({ no, line }) => `${no}: ${line}`).join("\n")
    ).toBe("");
  });

  test("stored enum values are matched against the options the select offers, not rebuilt from the string", () => {
    const text = source();

    // titleCase() is what broke on GroupFunction. Its absence FROM THE CODE is the rule — the comment above
    // the fix names it on purpose, and that mention must not fail this test.
    expect(codeOnly()).not.toMatch(/\btitleCase\s*\(/);

    // And the replacement actually reads the option list.
    expect(text).toMatch(/el\.options/);
    expect(text).toMatch(/toLowerCase\(\)\s*===\s*value\.toLowerCase\(\)/);
  });

  test("no hard-coded per-member exception remains for enum resolution", () => {
    const text = source();

    // HQ needed one under titleCase(). If a second special case appears, the string is being guessed at again.
    const specialCase = /if\s*\([^)]*===\s*['"](HQ|GroupFunction)['"]/;
    expect(specialCase.test(codeOnly())).toBe(false);
  });
});
