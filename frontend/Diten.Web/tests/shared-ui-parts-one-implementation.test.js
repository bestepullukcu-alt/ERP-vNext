const fs = require("fs");
const path = require("path");

/*
 * THE GUARD — WP-WC-SHARED-UI-01's OWN CLAIM, MEASURED AGAINST THE CODE, NOT THE COMMENT.
 *
 * This work package moved five pieces WorkCenterNext/Tasks/Meetings/RoleAssignments each carried their own copy
 * of into shared/diten-{dialog,person-picker,related-records}.js: the person/avatar row builder
 * (`personInitials`/`renderPersonOptions`), the searchable-select2 adapter (`buildSearchableDropdownAdapter`),
 * and the related/linked-record row (`relatedRecordRow`/`renderRelatedRows`). A file that still DECLARES the
 * algorithm — rather than calling the shared one — is the second copy this round exists to close, and nothing
 * short of reading every consumer's own source proves that did not happen.
 *
 * WorkCenterNext/app.js's `bindDialogSelect2`/`dialogLook` used to be the one named exception here — real
 * bodies of their own, because three of app.js's own tests pinned their literal source. WP-WC-SHARED-UI-01
 * M2b (2026-09-11) closed it: those three tests were retargeted to read the mechanism from
 * shared/diten-dialog.js instead (see that file's own top comment), and app.js's declarations became one-line
 * delegations like `dialogIcon`'s always were. Every name in DELEGATIONS below now has exactly the ONE real
 * declaration listed for it — no exceptions left.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const JS_ROOT = web("wwwroot", "assets", "js");

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
const rel = (f) => path.relative(JS_ROOT, f).split(path.sep).join("/");
const read = (f) => fs.readFileSync(f, "utf8");
// Comments may discuss a retired local copy — that is the record of why it moved. Code may not have one.
const code = (src) => src.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");

/** A declaration line whose right-hand side calls one of the shared modules is a DELEGATION, not a second body. */
const DELEGATES_TO_SHARED = /Diten(?:PersonPicker|Dialog|RelatedRecords)\s*[.?]/;

/**
 * Every file that declares `const <name> = ` (a function OR a value), split into real bodies vs. delegations to
 * a shared module. A delegation here is always a ONE-STATEMENT arrow (`(...) => shared.call(...)`, sometimes
 * wrapped over 2-3 lines by a guard ternary) — so the classifying window is the declaration line plus the next
 * few, not the whole (possibly much longer) block a real body can run to.
 */
const declarations = (name) => {
  const DECL = new RegExp(`const\\s+${name}\\s*=[^=]`);
  const LOOKAHEAD_LINES = 4;
  const real = [];
  const delegating = [];
  jsFiles().forEach((f) => {
    const stripped = code(read(f));
    const lines = stripped.split("\n");
    /*
     * EVERY declaration in the file, not the first one. Measured by the Control Tower (2026-09-11): with only the
     * first declaration examined, a file that already delegates could grow a SECOND, real body further down and
     * this guard stayed green — the exact drift it exists to catch. A file lands in `real` if ANY of its
     * declarations is a real body, in `delegating` if any delegates.
     */
    let hasReal = false;
    let hasDelegation = false;
    lines.forEach((line, index) => {
      if (!DECL.test(line)) { return; }
      const window = lines.slice(index, index + LOOKAHEAD_LINES).join("\n");
      if (DELEGATES_TO_SHARED.test(window)) { hasDelegation = true; } else { hasReal = true; }
    });
    if (hasReal) { real.push(rel(f)); }
    if (hasDelegation) { delegating.push(rel(f)); }
  });
  return { real: real.sort(), delegating: delegating.sort() };
};

describe("WP-WC-SHARED-UI-01 — the person picker, dialog adapter and related-record row have one body each", () => {
  it("personInitials is a real declaration only in shared/diten-person-picker.js", () => {
    const { real } = declarations("personInitials");
    expect(real, "personInitials grew a second real body outside the shared module")
      .toEqual(["shared/diten-person-picker.js"]);
  });

  it("Tasks/form.js and WorkCenterNext/app.js call the shared personInitials rather than redeclaring it", () => {
    const { delegating } = declarations("personInitials");
    expect(delegating).toEqual(["Tasks/form.js", "WorkCenterNext/app.js"].sort());
  });

  it("renderPersonOptions is a real declaration only in shared/diten-person-picker.js", () => {
    const { real } = declarations("renderPersonOptions");
    expect(real).toEqual(["shared/diten-person-picker.js"]);
  });

  it("buildSearchableDropdownAdapter is a real declaration only in shared/diten-person-picker.js", () => {
    const { real } = declarations("buildSearchableDropdownAdapter");
    expect(real, "Meetings/form.js or RoleAssignments/index.js rebuilt the select2 adapter locally instead of calling the shared one")
      .toEqual(["shared/diten-person-picker.js"]);
  });

  it("Meetings/form.js and Governance/RoleAssignments/index.js call the shared adapter rather than redeclaring it", () => {
    const { delegating } = declarations("buildSearchableDropdownAdapter");
    expect(delegating).toEqual(["Governance/RoleAssignments/index.js", "Meetings/form.js"].sort());
  });

  it("relatedRecordRow / renderRelatedRows are real declarations only in shared/diten-related-records.js", () => {
    // `renderRelatedRows` also names an unrelated local function in DocumentManagement/QmsBaselines/details.js
    // (a baseline's OWN "related records" table, nothing to do with WC-1/Meetings) — coincidental, pre-existing,
    // not a second copy of this WP's component. relatedRecordRow has no such collision.
    expect(declarations("relatedRecordRow").real).toEqual(["shared/diten-related-records.js"]);
    expect(declarations("renderRelatedRows").real.filter((f) => f !== "DocumentManagement/QmsBaselines/details.js"))
      .toEqual(["shared/diten-related-records.js"]);
  });

  it("WorkCenterNext/app.js's renderRelated and Meetings/form.js's renderLinkedTasks call the shared row renderer", () => {
    const app = code(read(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js")));
    const renderRelatedBody = app.slice(app.indexOf("const renderRelated ="), app.indexOf("const renderCompliance ="));
    expect(renderRelatedBody).toMatch(/DitenRelatedRecords\.renderRelatedRows\(/);
    // A second, local row template (the old inline `<a class="wcn-related-row">…` string) never came back.
    expect(renderRelatedBody).not.toMatch(/<a class="wcn-related-row"/);

    const meetingsForm = code(read(web("wwwroot", "assets", "js", "Meetings", "form.js")));
    const renderLinkedTasksBody = meetingsForm.slice(
      meetingsForm.indexOf("const renderLinkedTasks ="),
      meetingsForm.indexOf("const reloadMeeting =")
    );
    expect(renderLinkedTasksBody).toMatch(/DitenRelatedRecords\.renderRelatedRows\(/);
  });

  it("bindDialogSelect2 and dialogLook are real declarations only in shared/diten-dialog.js", () => {
    // WP-WC-SHARED-UI-01 M2b (2026-09-11) — WorkCenterNext/app.js used to keep a real body of each (the former
    // named exception, see this file's top comment); both are one-line delegations now.
    expect(declarations("bindDialogSelect2").real, "a real body reappeared outside the shared module")
      .toEqual(["shared/diten-dialog.js"]);
    expect(declarations("dialogLook").real, "a real body reappeared outside the shared module")
      .toEqual(["shared/diten-dialog.js"]);
  });

  it("WorkCenterNext/app.js calls the shared bindDialogSelect2/dialogLook rather than redeclaring them", () => {
    expect(declarations("bindDialogSelect2").delegating).toContain("WorkCenterNext/app.js");
    expect(declarations("dialogLook").delegating).toContain("WorkCenterNext/app.js");
  });

  it("dialogIcon and dialogDescriptionClass are NOT similarly duplicated — only their call sites are pinned", () => {
    const { real } = declarations("dialogIcon");
    expect(real, "dialogIcon rebuilt its body outside the shared module — only its call sites are pinned by app.js's own tests")
      .toEqual(["shared/diten-dialog.js"]);
  });

  it("M1/M2 — no hand-rolled Bootstrap modal remains under Views/Meetings", () => {
    /*
     * The cancel-meeting and reassign-organizer actions used to open a `class="modal fade"` Bootstrap dialog
     * each; both now open through window.showConfirm (Meetings/form.js), so a `.modal` reappearing here means
     * one of the two regressed back to its own hand-rolled dialog.
     */
    const offenders = [];
    const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).forEach((e) => {
      const p = path.join(dir, e.name);
      if (e.isDirectory()) { walk(p); }
      else if (e.name.endsWith(".cshtml") && /class="[^"]*\bmodal\b/.test(read(p))) { offenders.push(p); }
    });
    walk(web("Views", "Meetings"));
    expect(offenders.map((f) => path.relative(web("Views", "Meetings"), f)))
      .toEqual([]);
  });
});
