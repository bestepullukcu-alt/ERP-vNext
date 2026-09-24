const fs = require("fs");
const path = require("path");

/*
 * ══ MOD-0288-FU04 — THREE RULES THAT ARE NOT ALLOWED TO BE COMMENTS ══════════════════════════════════════
 *
 * WHY THIS FILE EXISTS. In MOD-0288-FU03 the forbidden reporting-line fallback was pasted into the shipping
 * form.js by hand and the full suite ran 183 of 183 GREEN. The rule had been written carefully — as a comment,
 * directly above the line it protected. A comment stops nobody.
 *
 * So the three rules this pack turns on are measured here:
 *
 *   1. `Code` is read-only on edit. A user who can type into it does not have an immutable code.
 *   2. At 50 active definitions the CREATE PATH CLOSES — it does not blow up on save.
 *   3. `read` alone yields a fully read-only surface: no create, no row action that writes, no editable
 *      control.
 *
 * ⚠ AND THEY READ THE SHIPPING CODE. Rules 2 and 3 `require()` index.js and call the FUNCTIONS THE BROWSER
 * CALLS — `createGate`, `surfacePermissions`, `rowActionsFor` — rather than a copy of their logic. A test that
 * re-implements a rule proves only that the copy works, and this repository has been bitten twice by exactly
 * that. Rule 1 lives in Razor and a C# view model, which no JS test can execute; it is asserted by reading
 * those two production files, and the assertions are written to fail on the specific edit that would break it.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (f) => fs.readFileSync(f, "utf8");

const FORM_CSHTML = web("Views", "Organization", "FieldDefinitions", "_Form.cshtml");
const VIEW_MODEL = web("Models", "OrganizationFieldDefinitions", "OrganizationFieldDefinitionViewModels.cs");
const INDEX_JS = web("wwwroot", "assets", "js", "Organization", "FieldDefinitions", "index.js");
const FORM_JS = web("wwwroot", "assets", "js", "Organization", "FieldDefinitions", "form.js");

// The shipping module, loaded exactly as the browser loads it (its boot is guarded on the table element,
// which does not exist here, so requiring it starts nothing).
const rules = require(INDEX_JS);

const READ = "platform.organization-units.custom-fields.read";
const MANAGE = "platform.organization-units.custom-fields.manage";

/*
 * Comments are where these rules are EXPLAINED, so a naive scan finds the forbidden shapes inside the very
 * prose that forbids them. The Razor assertions below read code only.
 */
const razorCodeOnly = (text) =>
  text
    .replace(/@\*[\s\S]*?\*@/g, "")
    .replace(/\/\*[\s\S]*?\*\//g, "");

const csharpCodeOnly = (text) =>
  text
    .replace(/\/\*[\s\S]*?\*\//g, "")
    .split("\n")
    .filter((line) => !line.trim().startsWith("//") && !line.trim().startsWith("///"))
    .join("\n");

// ── RULE 1 — the code cannot be typed into on edit ────────────────────────────────────────────────────────
describe("rule 1: Code is immutable after creation", () => {
  /*
   * ⚠ THIS TEST USED TO DEMAND `disabled`, AND THAT DEMAND WAS THE DEFECT (owner report, 2026-09-21).
   *
   * Its reasoning was that a readonly input "is still submitted" — which is true, and is exactly what an
   * immutable field NEEDS to be. A disabled control is not successful: the browser sends nothing for it. So
   * every edit reached the controller with an empty `Code`, its `[Required]` failed, `ModelState.IsValid` was
   * false, and the page came back with the code box red. The form could not save a single change from the day
   * it was written, and this test held that shut.
   *
   * The other half of the old reasoning — that readonly "reads as editable" — was a real observation about an
   * unpainted box, and it is answered rather than ignored: `bg-label-secondary` is the product's existing
   * immutable-field look, the one the Users and Roles screens already paint for the same reason (Bootstrap 5
   * dropped the [readonly] background rule).
   *
   * What the rule actually is, and what is asserted below: the box cannot be typed into, it POSTS what it
   * shows, it looks unlike an editable box, and no script quietly turns it back into one. The server-side half
   * of the guard is unchanged and still tested two tests down.
   *
   * The end-to-end proof that a disabled box breaks the save lives in
   * `frontend/Diten.Web.Tests/Forms/OrganizationFieldDefinitionEditPostsCodeTests.cs`, which renders this view
   * for real and validates the model the way the controller does.
   */
  test("the edit branch of _Form.cshtml renders Code uneditable but still posting", () => {
    const source = razorCodeOnly(read(FORM_CSHTML));

    // The `isEdit` branch, up to its `else`. Reading the branch rather than the whole file is what makes this
    // fail when somebody moves the editable input into it.
    const branch = source.match(/@if\s*\(isEdit\)\s*\{([\s\S]*?)\}\s*else/);
    expect(branch).not.toBeNull();

    const codeInput = branch[1].match(/<input[^>]*asp-for="Code"[^>]*>/);
    expect(codeInput).not.toBeNull();
    expect(codeInput[0]).toMatch(/\breadonly\b/);
    expect(codeInput[0], "a disabled control posts nothing, so every save fails [Required]")
      .not.toMatch(/\bdisabled\b/);
    expect(codeInput[0], "an unpainted readonly box reads as editable — the owner clicked into one and nothing happened")
      .toMatch(/bg-label-secondary/);
  });

  test("no script turns the immutable code input back into an editable one", () => {
    // The attribute is one `removeAttribute` away from being decoration — whichever attribute it is.
    [INDEX_JS, FORM_JS].forEach((file) => {
      const source = read(file).replace(/\/\*[\s\S]*?\*\//g, "");
      expect(source).not.toMatch(/data-code-immutable[\s\S]{0,200}(disabled\s*=\s*false|readOnly\s*=\s*false|removeAttribute\(\s*['"](disabled|readonly))/i);
    });
  });

  test("the view model refuses a posted code that differs from the stored one", () => {
    const source = csharpCodeOnly(read(VIEW_MODEL));

    /*
     * A disabled input posts nothing, so a crafted POST reaches the model with any code at all. The guard
     * compares against the code that was read, which is why OriginalCode travels on the form.
     *
     * ⚠ ASSERTED INSIDE Validate(), NOT ANYWHERE IN THE FILE. Measured: an earlier version of this test
     * matched /IsEdit[\s\S]*OriginalCode[\s\S]*ValidationResult/ across the whole file and stayed GREEN when
     * the entire guard block was deleted — the property declaration alone satisfied it. A guard that survives
     * the deletion of the thing it guards is the defect it was written to prevent.
     */
    const validateBody = source.match(/IEnumerable<ValidationResult> Validate\(ValidationContext[^)]*\)\s*\{([\s\S]*)\n\}/);
    expect(validateBody).not.toBeNull();
    expect(validateBody[1]).toMatch(/if\s*\(IsEdit/);
    expect(validateBody[1]).toMatch(/OriginalCode/);
    expect(validateBody[1]).toMatch(/!string\.Equals\(\s*Code[\s\S]{0,80}OriginalCode/);
    // ⚠ The update payload must not carry a code. FU02's update request has no member for one, so sending it
    // would be discarded in silence while the screen reported a saved change.
    const controller = read(web("Controllers", "OrganizationFieldDefinitionsController.cs"));
    const updatePayload = controller.match(/ToUpdatePayload\(OrganizationFieldDefinitionEditViewModel model\)\s*=>\s*new\s*\{([\s\S]*?)\};/);
    expect(updatePayload).not.toBeNull();
    expect(updatePayload[1]).not.toMatch(/\bcode\s*=/);
    expect(updatePayload[1]).not.toMatch(/\bisActive\s*=/);
  });
});

// ── RULE 2 — the create path closes at the limit ──────────────────────────────────────────────────────────
describe("rule 2: at 50 active definitions the create path is closed, not failed on save", () => {
  const manage = [READ, MANAGE];
  const definitions = (activeCount, inactiveCount = 0) => [
    ...Array.from({ length: activeCount }, (_, i) => ({ id: `a${i}`, isActive: true })),
    ...Array.from({ length: inactiveCount }, (_, i) => ({ id: `i${i}`, isActive: false }))
  ];

  test("the limit is FU02's fifty, not a number this screen chose", () => {
    expect(rules.MAX_ACTIVE_DEFINITIONS).toBe(50);
  });

  test("below the limit the gate is open and reports what is left", () => {
    const gate = rules.createGate(definitions(49), manage);
    expect(gate.allowed).toBe(true);
    expect(gate.remaining).toBe(1);
  });

  test("AT the limit the gate is closed and says why", () => {
    const gate = rules.createGate(definitions(50), manage);
    expect(gate.allowed).toBe(false);
    expect(gate.reason).toBe("limit");
    expect(gate.remaining).toBe(0);
  });

  test("only ACTIVE definitions count toward the limit", () => {
    // Deactivated definitions keep their stored values readable; they do not occupy capacity, and counting
    // them would lock a tenant out of creating fields it is entitled to.
    const gate = rules.createGate(definitions(10, 40), manage);
    expect(gate.allowed).toBe(true);
    expect(gate.remaining).toBe(40);
  });

  test("the gate is what the button asks, and it is asked again at click time", () => {
    const source = read(INDEX_JS).replace(/\/\*[\s\S]*?\*\//g, "");
    // Rendering-time gating alone would leave a stale button clickable after a reload.
    expect(source).toMatch(/applyCreateGate\s*=/);
    expect(source).toMatch(/bindAddNew[\s\S]*createGate\(\s*definitionsData\s*,\s*permissionKeys\(\)\s*\)\.allowed/);
  });
});

// ── RULE 3 — `read` alone is a read-only surface ──────────────────────────────────────────────────────────
describe("rule 3: read without manage yields no write surface", () => {
  const readerOnly = [READ];
  const manager = [READ, MANAGE];

  test("a reader is recognised as a reader", () => {
    expect(rules.surfacePermissions(readerOnly)).toEqual({ canRead: true, canManage: false });
    expect(rules.surfacePermissions(manager)).toEqual({ canRead: true, canManage: true });
    expect(rules.surfacePermissions([])).toEqual({ canRead: false, canManage: false });
  });

  test("a reader gets no create path — the reason is the permission, not the limit", () => {
    const gate = rules.createGate([{ id: "a", isActive: true }], readerOnly);
    expect(gate.allowed).toBe(false);
    expect(gate.reason).toBe("permission");
  });

  test("a reader's only row action is the read one", () => {
    const row = { id: "a", isActive: true };
    expect(rules.rowActionsFor(row, readerOnly)).toEqual(["details"]);
    // ⚠ Not "edit, but disabled". A disabled write control still tells the reader the door is theirs.
    expect(rules.rowActionsFor(row, readerOnly)).not.toContain("edit");
    expect(rules.rowActionsFor(row, readerOnly)).not.toContain("deactivate");
  });

  test("a manager gets the write actions, and none that the backend cannot serve", () => {
    expect(rules.rowActionsFor({ id: "a", isActive: true }, manager)).toEqual(["details", "edit", "deactivate"]);
    // FU02 has no re-activate route, so an inactive row is offered no lifecycle action at all.
    expect(rules.rowActionsFor({ id: "a", isActive: false }, manager)).toEqual(["details", "edit"]);
  });

  test("the bulk write path refuses without manage", () => {
    const source = read(INDEX_JS).replace(/\/\*[\s\S]*?\*\//g, "");
    expect(source).toMatch(/bulkDeactivate[\s\S]{0,200}surfacePermissions\(\s*permissionKeys\(\)\s*\)\.canManage/);
    expect(source).toMatch(/const deactivate[\s\S]{0,400}surfacePermissions\(\s*permissionKeys\(\)\s*\)\.canManage/);
  });

  test("the server refuses too — the screen is not the only gate", () => {
    const controller = read(web("Controllers", "OrganizationFieldDefinitionsController.cs"));
    // Every write action asks CanManage before it does anything.
    const writeActions = controller.match(/public\s+(?:async\s+)?Task<IActionResult>\s+(Create|Edit|DeactivateProxy|BulkProxy)\b[\s\S]*?\n    \}/g) || [];
    expect(writeActions.length).toBeGreaterThanOrEqual(4);
    writeActions.forEach((action) => expect(action).toMatch(/!CanManage/));
  });
});

// ── The FU03 bug, in the shape it would take here ─────────────────────────────────────────────────────────
describe("stored enum values are resolved against the options, never re-capitalised", () => {
  test("labelForEnum matches case-insensitively for the two-word types", () => {
    const map = { MultilineText: "Çok satırlı metin", SingleSelect: "Tek seçim" };
    expect(rules.labelForEnum(map, "MultilineText")).toBe("Çok satırlı metin");
    // The exact shape FU03 broke on: a server casing that is not the option's casing.
    expect(rules.labelForEnum(map, "multilinetext")).toBe("Çok satırlı metin");
    expect(rules.labelForEnum(map, "SINGLESELECT")).toBe("Tek seçim");
    // An unknown value is shown as it arrived, never silently mapped to the first entry.
    expect(rules.labelForEnum(map, "Whatever")).toBe("Whatever");
    expect(rules.labelForEnum(map, "")).toBe("");
  });

  test("form.js resolves the type select through its own options, case-insensitively", () => {
    const source = read(FORM_JS).replace(/\/\*[\s\S]*?\*\//g, "");
    expect(source).toMatch(/optionValueFor\s*=\s*\(select,\s*raw\)/);
    expect(source).toMatch(/o\.value\.toLowerCase\(\)\s*===\s*value\.toLowerCase\(\)/);
    // ⚠ The precedent's exact comparison is the thing NOT copied; a `===` on option values here is the
    // regression, and so is any re-capitalising helper.
    expect(source).not.toMatch(/o\.value\s*===\s*previous/);
    expect(source).not.toMatch(/titleCase/);
  });
});
