const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * BL-391 × WCN-FIX-03 (XMC-001, CT decision: YES) — the four other screens with the same `allowInput: true` + altInput
 * date pattern now use the SHARED typed-date guard, and fail closed without it. The three form screens' REAL scripts
 * are booted in jsdom against the ids they bind (the network is the only stub). The LegalEntities list binds its
 * filter dates only from inside its DataTable's init (jQuery + DataTables + saved views); there the production
 * `initDatePickers` block is lifted out of the real file and run against the real flatpickr — the measured code is
 * the shipped text, not a copy.
 *
 *   Organization / OrganizationUnits form · Organization / Positions form
 *   MasterData / LegalEntities list filter (created from / to) · MasterData / LegalEntities wizard
 */

const ROOT = path.resolve(__dirname, "..");
const read = (...parts) => fs.readFileSync(path.join(ROOT, ...parts), "utf8");

const SCREENS = [
  { name: "OrganizationUnits form", script: "wwwroot/assets/js/Organization/OrganizationUnits/form.js",
    view: ["Views", "Organization", "OrganizationUnits", "Form.cshtml"],
    root: '<div id="ou-form-page" data-ou-id="" data-ou-mode="create"></div>', dates: ["ouEffectiveFrom", "ouEffectiveTo"] },
  { name: "Positions form", script: "wwwroot/assets/js/Organization/Positions/form.js",
    view: ["Views", "Organization", "Positions", "Form.cshtml"],
    root: '<div id="p-form-page" data-p-id="" data-p-mode="create"></div>', dates: ["pEffectiveFrom", "pEffectiveTo"] },
  { name: "LegalEntities list filter", script: "wwwroot/assets/js/MasterData/LegalEntities/index.js",
    view: ["Views", "MasterData", "LegalEntities", "Index.cshtml"],
    root: "", dates: ["filterCreatedFrom", "filterCreatedTo"], lifted: true },
  { name: "LegalEntities wizard", script: "wwwroot/assets/js/MasterData/LegalEntities/wizard.js",
    view: ["Views", "MasterData", "LegalEntities", "Wizard.cshtml"],
    root: '<div id="le-wizard-page" data-le-id="" data-le-mode="create"></div>', dates: ["leIncorporationDate", "leDissolutionDate"] }
];

const tick = () => new Promise((resolve) => setTimeout(resolve, 0));

beforeAll(() => {
  global.self = global.self || window;
  Object.defineProperty(window, "ontouchstart", { value: undefined, configurable: true, writable: true });
  loadScript("wwwroot/assets/vendor/libs/flatpickr/flatpickr.js");
});

beforeEach(() => {
  global.fetch = vi.fn(() => Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve({ data: [] }), text: () => Promise.resolve("") }));
});

afterEach(() => {
  document.querySelectorAll(".flatpickr-calendar").forEach((cal) => cal.remove());
  document.body.innerHTML = "";
  delete window.DitenDateField;
  vi.restoreAllMocks();
});

const boot = async (screen, { withGuard }) => {
  if (withGuard) { loadScript("wwwroot/assets/js/shared/diten-datefield.js"); }
  document.body.innerHTML = `${screen.root}
    ${screen.dates.map((id) => `<input type="text" id="${id}" class="form-control flatpickr-date">`).join("")}
    <button type="button" id="other">x</button>`;
  vi.spyOn(console, "error").mockImplementation(() => {});
  if (screen.lifted) {
    const source = read(screen.script);
    const start = source.indexOf("    const dateGuard = () => {");
    const end = source.indexOf("    const dateVal = ", start);
    expect(start, "the list's date-guard block moved").toBeGreaterThan(-1);
    // eslint-disable-next-line no-new-func
    const initDatePickers = new Function("datePickers", "L", `${source.slice(start, end)}\nreturn initDatePickers;`)({}, {});
    initDatePickers();
  } else {
    loadScript(screen.script);
  }
  await tick();
  return screen.dates.map((id) => document.getElementById(id)._flatpickr);
};

describe.each(SCREENS)("$name", (screen) => {
  test("binds its dates with the shared guard: '31.02.2026' typed then Tab is cleared and marked", async () => {
    const pickers = await boot(screen, { withGuard: true });
    expect(pickers.every((fp) => fp && fp.altInput), "the screen did not bind its altInput dates").toBe(true);
    pickers.forEach((fp) => {
      expect(fp._ditenGuarded, "the shared guard was not called").toBe(true);
      fp.altInput.focus();
      fp.altInput.value = "31.02.2026";
      fp.altInput.dispatchEvent(new Event("input", { bubbles: true }));
      document.getElementById("other").focus();
      expect(fp.isOpen).toBe(false);
      expect(fp.input.value, "a date that does not exist was kept as another date").toBe("");
      expect(fp.altInput.classList.contains("is-invalid")).toBe(true);
    });
  });

  test("without the shared script its dates FAIL CLOSED (read-only) and it is said", async () => {
    const pickers = await boot(screen, { withGuard: false });
    pickers.forEach((fp) => {
      expect(fp.config.allowInput).toBe(false);
      expect(fp.altInput.hasAttribute("readonly")).toBe(true);
    });
    expect(console.error.mock.calls.some((call) => String(call[0]).includes("diten-datefield.js"))).toBe(true);
  });

  test("its view loads the shared guard before the screen's script", () => {
    const view = read(...screen.view);
    const shared = view.indexOf("assets/js/shared/diten-datefield.js");
    const own = view.indexOf(screen.script.replace("wwwroot/", ""));
    expect(shared, "the shared date-field script is not loaded by the view").toBeGreaterThan(-1);
    expect(own).toBeGreaterThan(shared);
  });
});
