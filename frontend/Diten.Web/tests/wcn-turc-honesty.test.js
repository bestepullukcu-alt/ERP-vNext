const fs = require("fs");
const path = require("path");

/*
 * TUR C (2026-08-24) — the last round before delivery. A readout that lied is gone, the module can be booted
 * twice without two instances fighting over one document, and the fixture rule that cost three attempts is
 * written down.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
const CSS = fs.readFileSync(web("wwwroot", "assets", "css", "backbone-custom.css"), "utf8");
const CONTRACT = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "fixture-contract.js"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resx = (lang) => fs.readFileSync(
  web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");
const value = (xml, key) => {
  const at = xml.indexOf(`name="${key}"`);
  return at < 0 ? null : xml.slice(xml.indexOf("<value>", at) + 7, xml.indexOf("</value>", at));
};
const code = (src) => src.replace(/\/\*[\s\S]*?\*\//g, "").replace(/(^|[^:])\/\/.*$/gm, "$1");

describe("the timer readout no longer claims what it cannot know", () => {
  it("draws no ticking value", () => {
    /*
     * MUTATION GUARD #1: put the live readout back and this goes red.
     *
     * MEASURED: it showed 37:29, the page was refreshed, and it returned at 37:15 — starting over, not
     * continuing. The mapper invents `startedAt` on every load and `TaskItem` has no timer-start field at all,
     * so the number could never be right, on a fixture or on a real task.
     */
    const stripped = code(APP);
    expect(stripped, "the ticking readout came back").not.toContain("wcnTimerValue");
    expect(stripped, "the ticking readout came back").not.toContain("wcn-ts-live");
    expect(stripped, "the one-second interval came back").not.toContain("setInterval(paint");
  });

  it("keeps everything that IS recorded", () => {
    // The total survives a refresh because it comes from stored `loggedMinutes`; the state comes from the
    // projection; "Süre gir" is the only path that writes anything durable.
    const card = APP.slice(APP.indexOf("const renderTimesheet"), APP.indexOf("const renderTimesheet") + 5200);
    expect(card).toContain("formatMinutes(ts.loggedMinutes)");
    expect(card).toContain("TimerStateRunning");
    expect(card).toContain("wcn-ts-log");
  });

  it("says plainly that elapsed time is not recorded, in all seven languages", () => {
    LANGS.forEach((lang) => {
      const v = String(value(resx(lang), "TimerFollowsStatusHint") || "").trim();
      expect(v, `${lang} has no hint`).not.toBe("");
      // The old sentence pointed at start/pause controls; the new one states the limitation.
      expect(v.toLowerCase(), `${lang} still points at controls that record nothing`)
        .not.toMatch(/duraklatma aksiyonlar|pause it from the actions/);
    });
  });

  it("adds no entity field and no migration", () => {
    // The honest fix belongs to MOD-0280. This round stops lying; it does not build the feature.
    const models = fs.readFileSync(path.join(repoRoot, "services", "Diten.Platform", "src",
      "Diten.Platform.Application", "Features", "WorkAggregation", "WorkAggregationModels.cs"), "utf8");
    expect(models, "a timer anchor was smuggled into the DTO").not.toMatch(/TimerStartedAt|TimerStartAt/);
  });
});

describe("one instance owns the document", () => {
  it("tears down the previous boot's listeners", () => {
    /*
     * BL-189. Every handler is on `document`, so a second boot STACKS rather than replaces: one click runs
     * `onClick` twice against two different `state` objects. It bites the test harness first, but any page
     * that loads the bundle twice hits the same thing — this is production behaviour, not a test fixture.
     */
    expect(APP).toContain("global.__wcnTeardown");
    expect(APP).toContain("document.removeEventListener('click', onClickWrapped)");
    ["change", "input", "keydown"].forEach((evt) =>
      expect(APP, `${evt} is added but never removed`).toContain(`document.removeEventListener('${evt}'`));
    // Every document-level listener added at boot must have a matching removal.
    const added = (APP.match(/document\.addEventListener\('(click|change|input|keydown)'/g) || []).length;
    const removed = (APP.match(/document\.removeEventListener\('(click|change|input|keydown)'/g) || []).length;
    expect(removed, "a boot listener has no teardown").toBe(added);
  });
});

describe("the fixture's visibility rule is written down", () => {
  it("states the minimum visible row in one place", () => {
    /*
     * MUTATION GUARD #2: delete the rule and this goes red.
     *
     * BL-222: the rule lived only inside `inTab` in app.js and cost three attempts to rediscover — each
     * attempt produced a row the list silently dropped, so the test would have been proving something about
     * the fixture rather than about the feature.
     */
    /*
     * ⚠ ASSERTED ON THE VALUE, NOT ON THE PROSE. The first version of this test checked
     * `CONTRACT.toContain("MINIMUM_VISIBLE_ROW")` and the four condition names — and it PASSED with the
     * object deleted, because the explaining comment and the export line still carried every string it
     * looked for. A rule that a comment can satisfy is a rule nothing enforces.
     */
    const declaration = CONTRACT.slice(CONTRACT.indexOf("const MINIMUM_VISIBLE_ROW"),
      CONTRACT.indexOf("const MINIMUM_VISIBLE_ROW") + 500);
    expect(CONTRACT.indexOf("const MINIMUM_VISIBLE_ROW"), "the rule object is gone").toBeGreaterThan(-1);
    expect(declaration).toContain("tab: 'inbox'");
    expect(declaration).toContain("requires:");
    expect(declaration).toContain("forbids:");
    // The four conditions `inTab` actually applies must be named in the surrounding documentation.
    ["catalogVisible", "dismissed", "itemInScope", "isTerminal"].forEach((cond) =>
      expect(CONTRACT, `the rule omits ${cond}`).toContain(cond));
    // …and it must be exported, not just declared.
    expect(CONTRACT).toMatch(/MINIMUM_VISIBLE_ROW,\s*\n\s*enums:/);
  });

  it("stays a description, with the implementation still in one place", () => {
    // `inTab` is where the rule RUNS. A second implementation would be two answers to one question.
    expect((code(APP).match(/const inTab = /g) || []).length).toBe(1);
    expect(code(CONTRACT), "the contract started deciding visibility itself").not.toContain("const inTab");
  });
});

describe("the one contrast pairing we made worse", () => {
  it("takes the body colour, like the row title it sits beside", () => {
    /*
     * MEASURED across every `--bs-secondary-color` text in the module: the token's own floor is 2.29 (light)
     * and 3.49 (dark) on the card surface, and twenty texts sit at it — a design-system fact, recorded rather
     * than changed. `wcn-subtask-status` was the ONE that fell BELOW that floor (2.09 / 3.25), because it sits
     * on the completed row's tinted background that THIS SESSION introduced.
     */
    const rule = CSS.slice(CSS.indexOf("\n.wcn-subtask-status {"), CSS.indexOf("}", CSS.indexOf("\n.wcn-subtask-status {")));
    expect(rule).toContain("color: var(--bs-body-color)");
    expect(rule, "the chip fell back under the token's own floor").not.toContain("var(--bs-secondary-color)");
  });

  it("leaves the token alone", () => {
    // Changing `--bs-secondary-color` is a design-system decision and repaints every screen in the product.
    expect(CSS, "the shared token was redefined").not.toMatch(/--bs-secondary-color:\s*#/);
  });
});
