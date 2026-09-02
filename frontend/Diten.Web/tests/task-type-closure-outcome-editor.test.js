const fs = require("fs");
const path = require("path");

/*
 * The closure outcome dictionary editor — the MARKUP and the SCRIPT.
 *
 * Its sibling `Diten.Web.Tests/Controllers/TaskTypeClosureOutcomeEditorTests.cs` asserts what the controller
 * POSTS. That is the half that reaches the server, and it is not the whole story: the payload is only ever as
 * good as the form that filled it, and this slice's most breakable rules live in the row markup and in seventy
 * lines of vanilla JavaScript. A readonly attribute lost in a refactor produces a screen that lets somebody
 * rename a system outcome, and every assertion on the other side would still pass.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const ROW = () => read("Views", "Tasks", "TaskTypes", "_ClosureOutcomeRow.cshtml");
const FORM = () => read("Views", "Tasks", "TaskTypes", "_Form.cshtml");
const SCRIPT = () => read("wwwroot", "assets", "js", "Tasks", "task-type-closure-outcomes.js");
const LOCALES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

describe("(a) the dictionary cannot be deleted by accident", () => {
  it("posts the marker that tells the controller the editor really rendered", () => {
    /*
     * ⚠ THE DELETION GUARD, and the reason it is a hidden input rather than an inference.
     *
     * The API reads `closureOutcomes: null` as "leave it alone" and `[]` as "clear it". Without the marker, a
     * posted form with no rows is ambiguous between "I removed them all" and "this section never rendered" —
     * and guessing the second wrong wipes a tenant's configuration with a success message.
     */
    expect(FORM(), "the section no longer declares that it rendered")
      .toContain('asp-for="ClosureOutcomesSubmitted"');
  });

  it("keeps the row markup in ONE file, shared by the stored rows and the Add template", () => {
    /*
     * The template is what the Add button clones. If it were a second copy of the row, the copy would drift —
     * losing a `name` attribute is enough to make added rows bind to nothing while looking correct.
     */
    const form = FORM();
    const partial = /_ClosureOutcomeRow\.cshtml/g;
    expect((form.match(partial) || []).length, "the row markup was duplicated instead of shared").toBe(2);
    expect(form).toContain("<template data-closure-outcome-template>");
  });

  it("binds rows by a NAMED index, so removing one does not renumber the rest", () => {
    // A clone-and-append editor cannot renumber without re-rendering the list and discarding what was typed.
    expect(ROW()).toContain('name="ClosureOutcomes.Index"');
    expect(SCRIPT(), "the key generator was replaced by something that can collide")
      .not.toMatch(/rowsIn\([^)]*\)\.length[^;]*key/i);
  });
});

describe("(b) a system outcome's label comes from the catalogue and cannot be edited", () => {
  it("renders the system label read-only and gives it no name, so it never posts", () => {
    /*
     * Two controls, one visible. The system one is a DISPLAY: it carries no `name`, so the words never travel —
     * the resource key alone identifies the outcome and its seven translations already exist. Posting both a key
     * and text is refused by the server as ambiguous.
     */
    const row = ROW();
    const systemLabel = /<input[^>]*data-closure-outcome-system-label[^>]*\/>/s.exec(row);
    expect(systemLabel, "the system label control disappeared").toBeTruthy();
    expect(systemLabel[0], "the system label became editable").toContain("readonly");
    expect(systemLabel[0], "the system label started posting — the server will refuse it as ambiguous")
      .not.toMatch(/\sname=/);
  });

  it("reads the label from the WorkCenterNext resx instead of copying it into this screen's", () => {
    /*
     * `WorkAggregation_ClosureOutcome_*` already exists in seven languages, emitted by the projection and
     * resolved by the Task Center. Copying the five entries here would put one sentence in two files, and the
     * day one is corrected the two screens would disagree about what an outcome is called.
     */
    expect(ROW()).toContain("OutcomeLocalizer");
    expect(read("Resources", "Views", "Tasks", "TaskTypes", "TaskTypesIndex.en.resx"),
      "a system outcome label was duplicated into the task-type resx")
      .not.toContain("WorkAggregation_ClosureOutcome_");
  });

  it("makes the CODE read-only for a system row without disabling it", () => {
    /*
     * `readonly`, never `disabled`. A disabled input posts NOTHING, and this code is the value stored on every
     * task closed this way — disabling it would send an empty code and the server would refuse the save.
     */
    const code = /<input[^>]*data-closure-outcome-code[^>]*\/>/s.exec(ROW());
    expect(code).toBeTruthy();
    expect(code[0]).toContain('readonly="@isSystem"');
    expect(code[0], "the code control was disabled — it would stop posting").not.toContain("disabled");
    expect(SCRIPT()).toContain("code.readOnly = true;");
    expect(SCRIPT(), "the script disables the code instead of making it read-only")
      .not.toMatch(/code\.disabled\s*=/);
  });

  it("CLEARS the tenant text when a system outcome is chosen, rather than only hiding it", () => {
    /*
     * A hidden input still posts. Leaving words behind a chosen system outcome would send BOTH label halves and
     * turn Save into a 400 whose cause is invisible on screen.
     */
    const script = SCRIPT();
    const systemBranch = script.slice(script.indexOf("if (isSystem) {"), script.indexOf("// Back to a tenant"));
    expect(systemBranch, "the text box is hidden but not cleared").toContain("text.value = '';");
  });

  it("treats the catalogue's disposition and reason flag as DEFAULTS, not as locks", () => {
    /*
     * The server stores whatever the type says; it does not pin a system outcome's disposition. Locking them
     * here would be this screen enforcing a rule the engine does not have, and the two would disagree the first
     * time the catalogue changed. Only the LABEL is the catalogue's to keep.
     */
    const row = ROW();
    const disposition = /<select[^>]*data-closure-outcome-disposition[^>]*>/s.exec(row);
    expect(disposition).toBeTruthy();
    expect(disposition[0]).not.toContain("disabled");
    expect(disposition[0]).not.toContain("readonly");
  });
});

describe("(c) the reason flag belongs to the row", () => {
  it("names the checkbox per row and posts a false beside it", () => {
    /*
     * ⭐ Per row, never one switch above the list — see the row's own comment for what a global flag does to the
     * data. The hidden `false` is what makes UNCHECKING post at all: an unchecked checkbox sends nothing, so
     * without it a flag could be set and never cleared.
     */
    const row = ROW();
    expect(row).toContain('name="ClosureOutcomes[@key].RequiresReason" value="false"');
    expect(row).toMatch(/type="checkbox"[^>]*name="ClosureOutcomes\[@key\]\.RequiresReason"/s);
  });

  it("has no dictionary-wide reason switch anywhere on the form", () => {
    // The design this slice refuses, asserted as an absence so it cannot creep back as a "convenience".
    expect(FORM()).not.toMatch(/RequiresReasonForAll|AllOutcomesRequireReason|GlobalRequiresReason/);
  });
});

describe("the editor obeys the house rules", () => {
  it("styles through classes only — no inline style anywhere (FG-003)", () => {
    [["row", ROW()], ["form", FORM()], ["script", SCRIPT()]].forEach(([name, source]) => {
      expect(source, `${name} uses a style attribute`).not.toMatch(/style="/);
      expect(source, `${name} writes element.style`).not.toMatch(/\.style\./);
    });
  });

  it("toggles visibility with a CSS class rather than display", () => {
    expect(SCRIPT()).toContain("classList");
    expect(SCRIPT()).not.toMatch(/display\s*=/);
  });

  it("defines every editor label in all seven languages", () => {
    const keys = [
      "ClosureOutcomesSection", "ClosureOutcomesHint", "ClosureOutcomesEmpty", "ClosureOutcomeAdd",
      "ClosureOutcomeRemove", "ClosureOutcomeSource", "ClosureOutcomeSourceCustom", "ClosureOutcomeCode",
      "ClosureOutcomeLabel", "ClosureOutcomeDisposition", "ClosureOutcomeSortOrder",
      "ClosureOutcomeRequiresReason", "ClosureDispositionCompleted", "ClosureDispositionCancelled"
    ];

    LOCALES.forEach((locale) => {
      const resx = read("Resources", "Views", "Tasks", "TaskTypes", `TaskTypesIndex.${locale}.resx`);
      keys.forEach((key) => {
        expect(resx.includes(`name="${key}"`), `${key} missing in ${locale}`).toBe(true);
      });
    });
  });

  it("translates them rather than leaving English in every language", () => {
    // A resx that "has the key" and holds the English sentence is the failure this repository keeps paying for.
    const value = (locale, key) => {
      const resx = read("Resources", "Views", "Tasks", "TaskTypes", `TaskTypesIndex.${locale}.resx`);
      const m = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(resx);
      return m ? m[1].trim() : null;
    };

    ["ClosureOutcomesSection", "ClosureOutcomeAdd", "ClosureOutcomeRequiresReason"].forEach((key) => {
      const english = value("en", key);
      expect(english).toBeTruthy();
      LOCALES.filter((l) => l !== "en").forEach((locale) => {
        expect(value(locale, key), `${key}/${locale} is still the English text`).not.toBe(english);
      });
    });
  });
});
