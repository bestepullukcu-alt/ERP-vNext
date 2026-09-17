const fs = require("fs");
const path = require("path");

/*
 * MOD-0357 S12 — every `document.getElementById('mrXxx')` index.js calls must resolve against a REAL id in the
 * REAL views it ships with. A hand-built jsdom fixture in a *.test.js file can drift from the actual
 * `.cshtml` markup and still pass — MEASURED live while writing this slice: `_Filter.cshtml` first carried
 * `data-mr-scope`/`data-mr-status` attributes with no matching `id`, while index.js's `renderScope`/`setStatus`
 * already called `document.getElementById('mrScope')`/`getElementById('mrStatus')`. The screen-level test
 * (`meeting-report-screen.test.js`) used a fixture with the CORRECT ids and passed throughout, so it never
 * caught the gap — exactly the "a harness that mocked further in proves nothing about the code that ships"
 * trap this repo's own testing discipline warns about. This guard reads the REAL files instead.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);

const SCRIPT = fs.readFileSync(web("wwwroot", "assets", "js", "Meetings", "Report", "index.js"), "utf8");
const INDEX_VIEW = fs.readFileSync(web("Views", "Meetings", "Report", "Index.cshtml"), "utf8");
const FILTER_VIEW = fs.readFileSync(web("Views", "Meetings", "Report", "_Filter.cshtml"), "utf8");
const MARKUP = INDEX_VIEW + FILTER_VIEW;

describe("MOD-0357 S12: index.js's getElementById calls resolve against the REAL views", () => {
  // index.js calls the local `byId(id)` helper (itself `document.getElementById(id)`) almost everywhere, plus
  // two direct `document.getElementById('...')` calls in `bindEvents` — both literal-string shapes are swept.
  const ids = Array.from(SCRIPT.matchAll(/(?:byId|getElementById)\('([^']+)'\)/g)).map(([, id]) => id);

  it("the sweep actually found some ids to check, so the loop below is not vacuous", () => {
    expect(ids.length).toBeGreaterThan(10);
  });

  it.each(Array.from(new Set(ids)))("id=\"%s\" exists in Index.cshtml or _Filter.cshtml", (id) => {
    const pattern = new RegExp(`id="${id}"`);
    expect(pattern.test(MARKUP), `getElementById('${id}') has no matching id="${id}" in either view`).toBe(true);
  });
});
