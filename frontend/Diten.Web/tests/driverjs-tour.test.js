const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * WP-UX-TOUR-DRIVERJS-01 — the tours run with the REAL vendored Driver.js 1.8.0 in jsdom.
 *
 * The scripts are the ones `_PageHelpPanel.cshtml` loads, read from the partial in its own order: removing the
 * library's <script> there turns this red. The Task Center tour (WORKCENTER, 5 steps) is driven through its steps;
 * Timesheet and Time approvals become available on their marks. Without the library: "no tour", no error.
 */

const WEB = path.resolve(__dirname, "..");
const PARTIAL = fs.readFileSync(path.join(WEB, "Views", "Shared", "_PageHelpPanel.cshtml"), "utf8");
const partialScripts = () => [...PARTIAL.matchAll(/<script src="~\/([^"]+\.js)"/g)].map((m) => m[1])
  .filter((src) => /driverjs|diten-tour/.test(src));

const MARKS = {
  WORKCENTER: ["wcn-tabs", "wcn-search", "wcn-list", "wcn-row-actions", "wcn-new"],
  MYTIMESHEET: ["te-week", "te-add-row", "te-grid", "te-submit"],
  TIMEAPPROVALS: ["ta-header", "ta-list"]
};
const STRINGS = { "Tour.Next": "Next", "Tour.Previous": "Back", "Tour.Done": "Done", "Tour.Progress": "{{current}} / {{total}}",
  "Tour.WORKCENTER.Step1.Title": "Tabs <b>", "Tour.WORKCENTER.Step1.Text": "Choose a list.",
  "Tour.WORKCENTER.Step2.Title": "Search", "Tour.WORKCENTER.Step2.Text": "Find a task." };

const page = (code) => {
  document.body.innerHTML = `<script id="diten-tour-l10n" type="application/json">${JSON.stringify(STRINGS)}</script>
    ${MARKS[code].map((mark) => `<div data-tour="${mark}">${mark}</div>`).join("")}`;
};

const boot = ({ withLibrary = true } = {}) => {
  delete window.DitenTour;
  delete window.DitenTourCatalog;
  delete window.driver;
  delete globalThis.driver;
  Object.defineProperty(window, "innerWidth", { value: 1280, configurable: true, writable: true });
  partialScripts().filter((src) => withLibrary || !/driverjs/.test(src)).forEach((src) => loadScript(`wwwroot/${src}`));
  // the IIFE assigns to the script's `this` — the same object the tour layer reads as `window`
  if (globalThis.driver && !window.driver) { window.driver = globalThis.driver; }
};

afterEach(() => {
  document.querySelectorAll(".driver-popover, .driver-overlay").forEach((node) => node.remove());
  document.body.innerHTML = "";
});

describe("the help partial loads the tour library before the tour layer", () => {
  test("the partial names the vendored library and the layer (guards against a vacuous load)", () => {
    expect(partialScripts()).toEqual(expect.arrayContaining([
      "assets/vendor/libs/driverjs/driver.js.iife.js",
      "assets/js/shared/diten-tour.js",
      "assets/js/shared/diten-tour-catalog.js"
    ]));
  });
});

describe("with the real Driver.js", () => {
  test("the Task Center tour is available and its five steps are driven, text escaped", () => {
    page("WORKCENTER");
    boot();
    expect(window.DitenTour.isAvailable("WORKCENTER")).toBe(true);
    expect(window.DitenTour.start("WORKCENTER")).toBe(true);

    const popover = () => document.querySelector(".driver-popover");
    expect(popover(), "no popover was drawn").not.toBeNull();
    expect(document.querySelector(".driver-popover-title").textContent).toBe("Tabs <b>"); // escaped, shown as text
    expect(document.querySelector(".driver-popover-title b")).toBeNull();
    expect(document.querySelector(".driver-active-element").getAttribute("data-tour")).toBe("wcn-tabs");

    // Each "Next" moves the popover to the next step: its title and its progress text are the step's. (In jsdom the
    // library's highlight class is only cleared at the end of its animation frame loop, so the popover — not the
    // `.driver-active-element` class — is what is read here.)
    const title = () => document.querySelector(".driver-popover-title").textContent;
    const progress = () => document.querySelector(".driver-popover-progress-text").textContent;
    const seen = [[title(), progress()]];
    for (let i = 1; i < 5; i += 1) {
      document.querySelector(".driver-popover-next-btn").click();
      seen.push([title(), progress()]);
    }
    expect(seen).toEqual([1, 2, 3, 4, 5].map((n) => [
      STRINGS[`Tour.WORKCENTER.Step${n}.Title`] || `Tour.WORKCENTER.Step${n}.Title`, `${n} / 5`]));
    expect(document.querySelector(".driver-popover-next-btn").textContent).toBe("Done");
  });

  test.each(["MYTIMESHEET", "TIMEAPPROVALS"])("the %s tour is available on its page and starts", (code) => {
    page(code);
    boot();
    expect(window.DitenTour.isAvailable(code)).toBe(true);
    expect(window.DitenTour.start(code)).toBe(true);
    expect(document.querySelector(".driver-active-element").getAttribute("data-tour")).toBe(MARKS[code][0]);
  });
});

describe("without the library", () => {
  test("no tour anywhere, and no error", () => {
    page("WORKCENTER");
    const error = vi.spyOn(console, "error");
    boot({ withLibrary: false });
    expect(window.DitenTour.isAvailable("WORKCENTER")).toBe(false);
    expect(window.DitenTour.start("WORKCENTER")).toBe(false);
    expect(document.querySelector(".driver-popover")).toBeNull();
    expect(error).not.toHaveBeenCalled();
  });
});
