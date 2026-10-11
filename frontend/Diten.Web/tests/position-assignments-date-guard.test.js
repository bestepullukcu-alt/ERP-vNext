const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * BL-391 × WCN-FIX-02/03 — Organization / PositionAssignments' effective dates (GxP) through the REAL form.js.
 *
 * The form delegates the typed-date guard to the shared `DitenDateField` (XMC-001). Its own copy used to be
 * self-sufficient; a delegation depends on the view loading the shared script. So:
 *   - the real form, with the shared script loaded, clears "31.02.2026" + Enter and marks it invalid;
 *   - a refused OPTIONAL date (effective-to) stops the save — an empty effective-to is a valid "open-ended" payload,
 *     so saving it would turn "end on 31.02" into "never ends";
 *   - without the shared script the dates FAIL CLOSED: read-only, picked from the calendar only — and it is said;
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
      <select id="aPositionId"><option value="p-1" selected>P</option></select>
      <select id="aUserId"><option value="u-1" selected>U</option></select>
      <input type="text" id="aEffectiveFrom" class="form-control flatpickr-date">
      <input type="text" id="aEffectiveTo" class="form-control flatpickr-date">
      <button type="button" id="other">x</button>
      <button type="button" class="btn btn-primary" id="a-submit">Save</button>
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
    expect(fp.isOpen, "Enter did not close the calendar by itself").toBe(false);

    expect(el.value, "a date that does not exist was saved as another date").toBe("");
    expect(fp.altInput.classList.contains("is-invalid")).toBe(true);
  });

  test("without the shared script the dates FAIL CLOSED: read-only (picked from the calendar only), and it is said once", () => {
    const errors = vi.spyOn(console, "error").mockImplementation(() => {});
    page();
    loadScript("wwwroot/assets/js/Organization/PositionAssignments/form.js");

    ["aEffectiveFrom", "aEffectiveTo"].forEach((id) => {
      const fp = document.getElementById(id)._flatpickr;
      expect(fp.config.allowInput, `${id} can still be typed into`).toBe(false);
      expect(fp.altInput.hasAttribute("readonly"), `${id} box is not read-only`).toBe(true);
    });
    const guardErrors = errors.mock.calls.filter((call) => String(call[0]).includes("diten-datefield.js"));
    expect(guardErrors).toHaveLength(1);
    expect(String(guardErrors[0][0])).toContain("BL-391");
  });

  test("with the shared script the dates can be typed (the guard is there)", () => {
    loadScript("wwwroot/assets/js/shared/diten-datefield.js");
    page();
    loadScript("wwwroot/assets/js/Organization/PositionAssignments/form.js");
    const fp = document.getElementById("aEffectiveTo")._flatpickr;
    expect(fp.config.allowInput).toBe(true);
    expect(fp.altInput.hasAttribute("readonly")).toBe(false);
  });
});

describe("PositionAssignments form: a refused date stops the save (WCN-FIX-03)", () => {
  const posts = () => global.fetch.mock.calls.filter((call) => call[1] && call[1].method && call[1].method !== "GET");

  const fill = (id, text) => {
    const fp = document.getElementById(id)._flatpickr;
    fp.altInput.focus();
    fp.altInput.value = text;
    fp.altInput.dispatchEvent(new Event("input", { bubbles: true }));
    document.getElementById("other").focus(); // Tab: the closed-calendar blur judges it
    return fp;
  };

  const boot = () => {
    loadScript("wwwroot/assets/js/shared/diten-datefield.js");
    page();
    document.getElementById("a-form-page").insertAdjacentHTML("afterbegin",
      '<script type="application/json" id="assignment-form-l10n">{"DateFormat":"d.m.Y","DateNotRecognized":"DATE-REFUSED"}</script>');
    document.querySelectorAll("#assignment-form-l10n")[1].remove();
    loadScript("wwwroot/assets/js/Organization/PositionAssignments/form.js");
  };

  test("an end date typed as '31.02.2026' is refused, the save does not go out, and the reason is shown", async () => {
    boot();
    fill("aEffectiveFrom", "01.02.2026");
    const to = fill("aEffectiveTo", "31.02.2026");
    expect(to.altInput.classList.contains("is-invalid")).toBe(true);
    expect(document.getElementById("aEffectiveTo").value, "the refused end date is empty — an open-ended payload").toBe("");

    document.getElementById("a-submit").click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(posts(), "an open-ended assignment was saved instead of the refused end date").toEqual([]);
    const alert = document.getElementById("a-form-alert");
    expect(alert.classList.contains("d-none")).toBe(false);
    expect(alert.textContent).toContain("DATE-REFUSED");
  });

  test.each([["aEffectiveFrom", "aEffectiveTo"], ["aEffectiveTo", "aEffectiveFrom"]])(
    "WCN-FIX-06: a refused %s stops the save too (both dates are covered)", async (refused, other) => {
      boot();
      fill(other, "01.02.2026");
      fill(refused, "31.02.2026");
      document.getElementById("a-submit").click();
      await new Promise((resolve) => setTimeout(resolve, 0));
      expect(posts()).toEqual([]);
      expect(document.getElementById("a-form-alert").textContent).toContain("DATE-REFUSED");
    });

  test("the box still focused and the reader clicks Save directly: the guard judges first, nothing is written", async () => {
    boot();
    fill("aEffectiveFrom", "01.02.2026");
    const to = document.getElementById("aEffectiveTo")._flatpickr;
    to.altInput.focus();
    to.altInput.value = "31.02.2026";
    to.altInput.dispatchEvent(new Event("input", { bubbles: true }));
    const save = document.getElementById("a-submit");
    save.dispatchEvent(new MouseEvent("mousedown", { bubbles: true })); // the press: flatpickr closes, the guard judges
    save.focus();                                                        // the focus moves to the button
    save.click();
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(to.altInput.classList.contains("is-invalid")).toBe(true);
    expect(posts()).toEqual([]);
    expect(document.getElementById("a-form-alert").textContent).toContain("DATE-REFUSED");
  });

  test("control: the same form with a readable end date does save", async () => {
    boot();
    fill("aEffectiveFrom", "01.02.2026");
    fill("aEffectiveTo", "20.02.2026");
    const errors = vi.spyOn(console, "error").mockImplementation(() => {}); // jsdom: navigation after save
    document.getElementById("a-submit").click();
    await new Promise((resolve) => setTimeout(resolve, 0));
    errors.mockRestore();

    expect(posts()).toHaveLength(1);
    expect(JSON.parse(posts()[0][1].body).effectiveTo).toBe("2026-02-20T00:00:00Z");
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
