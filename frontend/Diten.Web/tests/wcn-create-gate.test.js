const fs = require("fs");
const path = require("path");

/*
 * THE THIRD CREATE GATE (2026-08-24, Tur A). Three gates exist and they collect different numbers of fields:
 *     inline box + Enter      1 field   (due date / priority / assignee INHERITED from the parent)
 *     the subtask panel       5 fields
 *     /Tasks/Create          20 fields  — and the ONLY one that renders `#taskCustomFields`
 *
 * That last clause is the whole reason for this work. The custom-fields section fills at runtime from
 * `TaskFieldDefinition`, which carries `IsRequired`. The day a tenant defines a required custom field, the two
 * shortcuts CANNOT collect it. A shortcut that silently cannot satisfy the tenant's own rule needs a door.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
const FORM_PAGE = fs.readFileSync(web("wwwroot", "assets", "js", "Tasks", "form-page.js"), "utf8");
const CONTROLLER = fs.readFileSync(web("Controllers", "TasksController.cs"), "utf8");
const CREATE_VIEW = fs.readFileSync(web("Views", "Tasks", "Create.cshtml"), "utf8");
const FORM = fs.readFileSync(web("Views", "Tasks", "_Form.cshtml"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) => fs.readFileSync(
  web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");

describe("the panel offers a door to the full form", () => {
  it("adds a third gate without touching the other two", () => {
    expect(APP).toContain("data-wcn-newsubtask-full");
    expect(APP).toContain("t('SubtaskCreateAllFields')");
    // The 5-field panel keeps its own submit, and the 1-field inline add keeps its handler.
    expect(APP).toContain("data-wcn-newsubtask-save");
    expect(APP).toContain("const addSubtask");
  });

  it("mirrors the pattern the edit panel already uses", () => {
    // `SubtaskOpenFullDetail` is the same secondary button with the same external-link glyph. A second visual
    // language for "leave here and continue in the full surface" would be one too many.
    expect(APP).toContain("SubtaskOpenFullDetail");
    const at = APP.indexOf("data-wcn-newsubtask-full");
    const gate = APP.slice(at - 200, at + 300);
    expect(gate).toContain("btn-label-secondary");
    expect(gate).toContain("bx-link-external");
  });

  it("names the button in all seven languages", () => {
    LANGS.forEach((lang) => {
      const xml = resx(lang);
      const at = xml.indexOf('name="SubtaskCreateAllFields"');
      expect(at, `${lang} is missing the label`).toBeGreaterThan(-1);
      const v = xml.slice(xml.indexOf("<value>", at) + 7, xml.indexOf("</value>", at));
      expect(v.trim(), `${lang} label is empty`).not.toBe("");
    });
  });
});

describe("the full form is the only gate that can collect a tenant's required field", () => {
  it("is the one that renders the custom-fields section", () => {
    // Non-vacuity for the whole feature: if this section moved, the third gate stops being necessary.
    expect(FORM).toContain("taskCustomFields");
  });
});

describe("returnUrl is not an open redirect", () => {
  it("gates it on Url.IsLocalUrl, server-side", () => {
    /*
     * MUTATION GUARD: drop `Url.IsLocalUrl` and this goes red. The parameter is reachable from a link a user
     * can be handed, so an unchecked value is an open redirect — `//evil.example` is a valid protocol-relative
     * URL that a naive check for a leading slash would accept.
     */
    const action = CONTROLLER.slice(CONTROLLER.indexOf('[HttpGet("Create")]'),
      CONTROLLER.indexOf('[HttpGet("Create")]') + 700);
    expect(action).toContain("Url.IsLocalUrl(returnUrl)");
    expect(action).toContain('ViewData["ReturnUrl"]');
    // An off-site value must become null, not pass through.
    expect(action).toMatch(/\?\s*returnUrl\s*:\s*null/);
  });

  it("lets nothing downstream widen the gate", () => {
    // The view only prints what the controller already vetted…
    expect(CREATE_VIEW).toContain('ViewData["ReturnUrl"]');
    // …and the page reads it from the DOM rather than from the query string it could have parsed itself.
    expect(FORM_PAGE).toContain("data-return-url");
    expect(FORM_PAGE, "the page started reading the URL itself, bypassing the server check")
      .not.toMatch(/URLSearchParams|location\.search/);
  });

  it("carries the parent on the field the rest of the module already uses", () => {
    // `parentTaskItemId` is the name `saveNewSubtask` posts; a second name for one thing is how two names drift.
    expect(FORM_PAGE).toContain("parentTaskItemId");
    expect(APP).toContain("payload.parentTaskItemId = parentId");
  });
});
