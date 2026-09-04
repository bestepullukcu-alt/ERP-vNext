const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * ══ "THERE IS NOBODY TO ASSIGN TO" — TRUE, AND A DEAD END ═════════════════════════════════════════════════
 *
 * MEASURED (2026-09-02, management demo): on /Tasks/Create the reviewer, approval-manager and watcher pickers
 * were all disabled, each holding one greyed line — "No one holds a position yet, so there is nobody to assign
 * to." The sentence is CORRECT (it is BL-327's downstream symptom: no organization unit or position in the
 * tenant is Active, so the assignable-people lookup answers with nothing) and it is where the reader stops.
 * It says what is missing; it does not say where the missing thing is made, and it offers no way to go there.
 *
 * ⚠ THE ROOT CAUSE IS NOT FIXED HERE. Why every unit and position is born Draft is BL-327 and is waiting on an
 * owner decision. What is fixed here is the second half of the defect and it is a defect in its own right:
 * even once BL-327 lands, a tenant on its first day will legitimately meet this state, and the screen still
 * has to tell that reader what to do.
 *
 * ── WHY THE LINK IS NOT INSIDE THE MESSAGE ───────────────────────────────────────────────────────────────
 * The empty state is an `<option>` — a disabled, selected line inside a `<select>`. An option renders text and
 * nothing else, so no anchor can live in it. The way out is drawn beside the control, as a `.form-text`, which
 * is the same carrier `taskAssigneeExcluded` already uses to explain a SHORT list (BL-072). One vocabulary for
 * "why this picker does not offer what you expected".
 *
 * ── WHY THE LINK IS NOT PERMISSION-GATED IN THE BROWSER ──────────────────────────────────────────────────
 * Measured: PositionsController carries `[Authorize]` and no `[HasPermission]` on its view routes, and the
 * backend is authoritative for everything under it. A client-side guess at the reader's rights would either
 * hide the way out from somebody who has it, or claim to know something only the server knows. The
 * destination is the authority; the link is an offer.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);

const LANGUAGES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const RESX = (lang) => web("Resources", "Views", "Tasks", `TasksIndex.${lang}.resx`);

/** One resource value, read from the real .resx the app ships. */
const resource = (lang, key) => {
  const xml = fs.readFileSync(RESX(lang), "utf8");
  const found = new RegExp(
    `<data name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`, "m").exec(xml);
  return found ? found[1] : null;
};

const LABELS = () => ({
  placeholder: "Kişi seçin…",
  empty: "Henüz kimsenin pozisyonu yok…",
  emptyActionLabel: "Pozisyonlar ekranını aç",
  emptyActionHref: "/Positions",
  nameUnavailable: "Ad yok"
});

const pickerMarkup = () => {
  document.body.innerHTML = `
    <div id="host">
      <div class="mb-3" data-task-field="assignee">
        <label class="form-label" for="taskAssignee">Atanan</label>
        <div class="diten-field">
          <i class="bx bx-user diten-field-icon" aria-hidden="true"></i>
          <select class="select2 form-select" id="taskAssignee"></select>
        </div>
        <div class="form-text">Only people who hold a position…</div>
      </div>
    </div>`;
  return document.getElementById("taskAssignee");
};

const person = () => ({
  userId: "dddddddd-dddd-dddd-dddd-dddddddddddd",
  displayName: "Selin Aras",
  positionId: "11111111-1111-1111-1111-111111111111",
  positionName: "QA Specialist",
  organizationUnitName: "Facility A"
});

const loadForm = () => {
  delete global.TaskForm;
  loadScript("wwwroot/assets/js/Tasks/form.js");
  return global.TaskForm;
};

const hint = () => document.querySelector("[data-task-empty-hint]");

// ══ 1. the way out is drawn ════════════════════════════════════════════════════════════════════════════

describe("an empty person picker offers a way out of the state it reports", () => {
  it("still says the empty sentence in the control — the report itself is not being replaced", () => {
    const TaskForm = loadForm();
    const select = pickerMarkup();

    TaskForm.renderPersonOptions(select, [], LABELS());

    expect(select.disabled).toBe(true);
    expect(select.options[0].textContent).toBe(LABELS().empty);
  });

  it("draws a link to where positions are actually made", () => {
    const TaskForm = loadForm();
    const select = pickerMarkup();

    TaskForm.renderPersonOptions(select, [], LABELS());

    const anchor = hint()?.querySelector("a");
    expect(anchor, "the reader is told what is missing and given nowhere to go").not.toBeNull();
    expect(anchor.getAttribute("href")).toBe("/Positions");
    expect(anchor.textContent).toBe("Pozisyonlar ekranını aç");
  });

  it("draws NOTHING extra when the list has people in it", () => {
    const TaskForm = loadForm();
    const select = pickerMarkup();

    TaskForm.renderPersonOptions(select, [person()], LABELS());

    expect(select.disabled).toBe(false);
    expect(hint(), "a working picker carries a 'go make positions' link").toBeNull();
  });

  it("clears the way out once the list fills — a stale link outlives the state it described", () => {
    const TaskForm = loadForm();
    const select = pickerMarkup();

    TaskForm.renderPersonOptions(select, [], LABELS());
    expect(hint()).not.toBeNull();

    TaskForm.renderPersonOptions(select, [person()], LABELS());
    expect(hint()).toBeNull();
  });

  it("does not stack a second link when the same empty picker is re-rendered", () => {
    const TaskForm = loadForm();
    const select = pickerMarkup();

    TaskForm.renderPersonOptions(select, [], LABELS());
    TaskForm.renderPersonOptions(select, [], LABELS());

    expect(document.querySelectorAll("[data-task-empty-hint]")).toHaveLength(1);
  });

  it("omits the link when no destination was supplied, rather than inventing one", () => {
    const TaskForm = loadForm();
    const select = pickerMarkup();
    const labels = LABELS();
    delete labels.emptyActionHref;

    TaskForm.renderPersonOptions(select, [], labels);

    expect(select.options[0].textContent).toBe(labels.empty);
    expect(hint()).toBeNull();
  });
});

// ══ 2. every surface that draws the picker hands it the way out ════════════════════════════════════════

describe("the two create surfaces both offer the way out", () => {
  const source = (file) => fs.readFileSync(web("wwwroot", "assets", "js", ...file), "utf8");

  it.each([
    ["the detailed form", ["Tasks", "form-page.js"]],
    ["the quick-create offcanvas", ["WorkCenterNext", "quick-create.js"]]
  ])("%s builds its person labels with a destination", (_name, file) => {
    const text = source(file);
    // Vacuity guard: prove the labels object this asserts about is really there.
    expect(text).toContain("assigneeEmpty");
    expect(
      text,
      "this surface renders the empty picker with no way out; the reader reads what is missing and stops"
    ).toContain("emptyActionHref");
  });
});

// ══ 3. seven languages, and the sentence names the screen ══════════════════════════════════════════════

describe("the message and its link are translated everywhere the tenant is", () => {
  it.each(LANGUAGES)("%s carries both keys, non-empty", (lang) => {
    for (const key of ["AssigneeEmpty", "AssigneeEmptyAction"]) {
      const value = resource(lang, key);
      expect(value, `${key} is missing from TasksIndex.${lang}.resx`).not.toBeNull();
      expect(value.trim().length, `${key} is blank in ${lang}`).toBeGreaterThan(0);
    }
  });

  it.each(LANGUAGES)("%s names the screen the reader has to go to, not just 'an administrator'", (lang) => {
    /*
     * Asserted against each language's OWN name for the Positions screen, read from that language's resx
     * rather than typed here — a hard-coded "Positions" would pass English and quietly demand English of the
     * other six. The old sentence said "ask an administrator to assign positions first", which names a person
     * and no place; this pins that the place survives translation.
     */
    const positionsScreen = resource(lang, "PositionsScreenName");
    expect(positionsScreen, `TasksIndex.${lang}.resx has no PositionsScreenName to compare against`).not.toBeNull();

    expect(
      resource(lang, "AssigneeEmpty"),
      `the ${lang} sentence does not mention the ${positionsScreen} screen, so it still ends in a dead end`
    ).toContain(positionsScreen);
  });

  it("the bridge actually ships the new key to the browser", () => {
    // A resource nothing serializes is a translation the page can never read.
    const bridge = fs.readFileSync(web("Views", "Tasks", "_IndexL10n.cshtml"), "utf8");
    expect(bridge).toContain("AssigneeEmptyAction");
    expect(bridge).toContain("PositionsScreenName");
  });
});
