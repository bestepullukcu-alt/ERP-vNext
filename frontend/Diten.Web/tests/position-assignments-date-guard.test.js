const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * BL-391 × WCN-FIX-02 — Organization / PositionAssignments' effective dates (GxP) through the REAL form.js.
 *
 * The form delegates the typed-date guard to the shared `DitenDateField` (XMC-001). Its own copy used to be
 * self-sufficient; a delegation depends on the view loading the shared script. So:
 *   - the real form, with the shared script loaded, clears "31.02.2026" + Enter and marks it invalid;
 *   - without the shared script it says so in the console — never a silent skip;
 *   - the view carries the script line, between flatpickr and form.js.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const VIEW = fs.readFileSync(path.join(repoRoot, "frontend", "Diten.Web", "Views", "Organization",
  "PositionAssignments", "Form.cshtml"), "utf8");

const page = () => {
  document.body.innerHTML = `
    <div id="a-form-page" data-a-id="" data-a-mode="create">
      <script type="application/json" id="assignment-form-l10n">{"DateFormat":"d.m.Y"}</script>
      <div id="a-form-alert" class="d-none"></div>
      <input type="text" id="aEffectiveFrom" class="form-control flatpickr-date">
      <input type="text" id="aEffectiveTo" class="form-control flatpickr-date">
      <button type="button" id="other">x</button>
    </div>`;
};

beforeAll(() => {
  global.self = global.self || window;
  Object.defineProperty(window, "ontouchstart", { value: undefined, configurable: true, writable: true });
  loadScript("wwwroot/assets/vendor/libs/flatpickr/flatpickr.js");
});

beforeEach(() => {
  global.fetch = vi.fn(() => Promise.resolve({ ok: true, json: () => Promise.resolve([]) }));
});

afterEach(() => {
  document.querySelectorAll(".flatpickr-calendar").forEach((cal) => cal.remove());
  document.body.innerHTML = "";
  delete window.DitenDateField;
  vi.restoreAllMocks();
});

describe("PositionAssignments form: the effective dates are guarded by the shared component", () => {
  test("with the shared script loaded, '31.02.2026' + Enter in the effective-from date is cleared and marked invalid", () => {
    loadScript("wwwroot/assets/js/shared/diten-datefield.js");
    page();
    loadScript("wwwroot/assets/js/Organization/PositionAssignments/form.js");

    const el = document.getElementById("aEffectiveFrom");
    const fp = el._flatpickr;
    expect(fp && fp.altInput, "form.js did not bind the altInput date").toBeTruthy();
    fp.altInput.focus();
    fp.open();
    fp.altInput.value = "31.02.2026";
    fp.altInput.dispatchEvent(new Event("input", { bubbles: true }));
    fp.altInput.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", keyCode: 13, bubbles: true }));
    if (fp.isOpen) { fp.close(); }

    expect(el.value, "a date that does not exist was saved as another date").toBe("");
    expect(fp.altInput.classList.contains("is-invalid")).toBe(true);
  });

  test("without the shared script the form says so in the console (once), instead of silently skipping the guard", () => {
    const errors = vi.spyOn(console, "error").mockImplementation(() => {});
    page();
    loadScript("wwwroot/assets/js/Organization/PositionAssignments/form.js");

    const guardErrors = errors.mock.calls.filter((call) => String(call[0]).includes("diten-datefield.js"));
    expect(guardErrors).toHaveLength(1);
    expect(String(guardErrors[0][0])).toContain("BL-391");
  });
});

describe("PositionAssignments Form view loads the shared date-field script (the GxP guard depends on this line)", () => {
  test("Form.cshtml has the diten-datefield.js script line after flatpickr and before form.js", () => {
    const scriptLine = /<script\s+src="~\/assets\/js\/shared\/diten-datefield\.js"[^>]*><\/script>/;
    expect(VIEW, "the shared date-field script line is missing from the view").toMatch(scriptLine);
    const flat = VIEW.indexOf("assets/vendor/libs/flatpickr/flatpickr.js");
    const shared = VIEW.search(scriptLine);
    const own = VIEW.indexOf("assets/js/Organization/PositionAssignments/form.js");
    expect(flat).toBeGreaterThan(-1);
    expect(shared).toBeGreaterThan(flat);
    expect(own).toBeGreaterThan(shared);
  });
});
