const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * BL-588 (WP-WCN-FIX-01) — "the first due-date pick does not reach the field; picking the same date a second time does".
 *
 * Measured with the REAL vendored flatpickr (4.6.13), not a double — the two older date-field tests use doubles that
 * only carry `onClose`, which is exactly why they could not see this. flatpickr's selectDate runs updateValue → close
 * (onClose: the BL-391 guard) → and only THEN onChange / `input`. The guard compared the pick with a copy of the box
 * taken when the field was enhanced (or left over from an earlier close) and CLEARED it as a "misparse": a field that
 * already held a value, or whose default was written without an event (quick-create's setDate(today, false)), lost
 * its first pick. The guard now judges only text TYPED in the current open session.
 *
 * Organization/PositionAssignments (altInput + altFormat) used its own copy of the guard with the same defect
 * (XMC-001); it now calls the shared one, measured here in its own shape.
 */

const day = (fp, y, m, d) => [...fp.daysContainer.querySelectorAll(".flatpickr-day")]
  .find((el) => el.dateObj.getFullYear() === y && el.dateObj.getMonth() === m && el.dateObj.getDate() === d
    && !el.classList.contains("prevMonthDay") && !el.classList.contains("nextMonthDay"));

const pick = (fp, y, m, d) => {
  fp.open();
  fp.jumpToDate(`${y}-${String(m + 1).padStart(2, "0")}-01`);
  day(fp, y, m, d).dispatchEvent(new MouseEvent("click", { bubbles: true }));
};

beforeAll(() => {
  global.self = global.self || window;
  loadScript("wwwroot/assets/vendor/libs/flatpickr/flatpickr.js");
  loadScript("wwwroot/assets/js/shared/diten-datefield.js");
});

afterEach(() => {
  document.querySelectorAll(".flatpickr-calendar").forEach((cal) => cal.remove());
  document.body.innerHTML = "";
});

const shared = (prefill) => {
  document.body.innerHTML = `<div id="host" class="diten-field"><i class="diten-field-icon"></i>
    <input class="form-control flatpickr-date" id="quickDueAt" ${prefill ? `value="${prefill}"` : ""}></div>`;
  window.DitenDateField.enhance(document.getElementById("host"));
  const input = document.getElementById("quickDueAt");
  return { input, fp: input._flatpickr };
};

describe("BL-588: the first calendar pick is written to the field", () => {
  test("the real flatpickr is loaded and bound (the test measures the library, not a double)", () => {
    const { fp } = shared();
    expect(typeof window.flatpickr).toBe("function");
    expect(fp && fp.daysContainer).toBeTruthy();
  });

  test("quick-create's path: default written without an event, picker opened and dismissed once, then a pick", () => {
    const { input, fp } = shared();
    fp.setDate("2026-10-09", false); // WcnQuickCreate.open()
    fp.open(); fp.close();

    pick(fp, 2026, 9, 20);

    expect(input.value).toBe("2026-10-20");
    expect(input.classList.contains("is-invalid")).toBe(false);
  });

  test("a field that already held a value when it was enhanced (subtask panels, Plan dialog, Task Edit)", () => {
    const { input, fp } = shared("2026-10-09");

    pick(fp, 2026, 9, 20);

    expect(input.value).toBe("2026-10-20");
    expect(input.classList.contains("is-invalid")).toBe(false);
  });

  test("two different picks in a row are both kept (no lost/kept alternation)", () => {
    const { input, fp } = shared();
    pick(fp, 2026, 9, 20);
    pick(fp, 2026, 9, 22);

    expect(input.value).toBe("2026-10-22");
    expect(input.classList.contains("is-invalid")).toBe(false);
  });

  test("a value cleared without an event (quick-create's resetDraft) does not poison the next pick", () => {
    const { input, fp } = shared();
    pick(fp, 2026, 9, 20);
    input.value = ""; // resetDraft()

    pick(fp, 2026, 9, 21);

    expect(input.value).toBe("2026-10-21");
  });

  test("text typed in the box and then a day picked FROM THE CALENDAR: the pick is kept (WCN-FIX-02)", () => {
    // The calendar click resets the typed text before flatpickr's selectDate → close runs the guard; without that
    // reset the half-typed text is judged against the pick and the pick is cleared.
    const { input, fp } = shared();
    fp.open();
    input.value = "2026-10-";
    input.dispatchEvent(new Event("input", { bubbles: true }));
    fp.jumpToDate("2026-10-01");
    day(fp, 2026, 9, 20).dispatchEvent(new MouseEvent("click", { bubbles: true }));

    expect(input.value).toBe("2026-10-20");
    expect(input.classList.contains("is-invalid")).toBe(false);
  });

  test("text typed in the box and then a day chosen with the KEYBOARD (Enter on the focused day): kept (WCN-FIX-02)", () => {
    const { input, fp } = shared();
    fp.open();
    input.value = "2026-10-";
    input.dispatchEvent(new Event("input", { bubbles: true }));
    fp.jumpToDate("2026-10-01");
    const target = day(fp, 2026, 9, 20);
    target.focus();
    target.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", keyCode: 13, bubbles: true }));

    expect(input.value).toBe("2026-10-20");
    expect(input.classList.contains("is-invalid")).toBe(false);
  });

  test("BL-391 still holds: text typed in the session that does not read back is cleared and marked invalid", () => {
    const { input, fp } = shared();
    fp.open();
    input.value = "2026-13-45x";
    input.dispatchEvent(new Event("input", { bubbles: true }));
    fp.close();

    expect(input.value).toBe("");
    expect(input.classList.contains("is-invalid")).toBe(true);
  });
});

describe("XMC-001: PositionAssignments' altInput shape through the SHARED guard", () => {
  const alt = (prefill) => {
    document.body.innerHTML = `<input id="assignmentEffectiveFrom" ${prefill ? `value="${prefill}"` : ""}>`;
    const el = document.getElementById("assignmentEffectiveFrom");
    window.flatpickr(el, { dateFormat: "Y-m-d", altInput: true, altFormat: "d.m.Y", allowInput: true });
    window.DitenDateField.guardAgainstSilentMisparse(el._flatpickr, "altFormat");
    return { el, fp: el._flatpickr };
  };

  test("the first pick into a pre-filled altInput date is kept, shown in the display format", () => {
    const { el, fp } = alt("2026-10-09");

    pick(fp, 2026, 9, 20);

    expect(el.value).toBe("2026-10-20");
    expect(fp.altInput.value).toBe("20.10.2026");
    expect(fp.altInput.classList.contains("is-invalid")).toBe(false);
  });

  test("text typed into the altInput that does not read back in altFormat is still cleared and marked invalid", () => {
    const { el, fp } = alt();
    fp.open();
    fp.altInput.value = "31.02.2026";
    fp.altInput.dispatchEvent(new Event("input", { bubbles: true }));
    fp.close();

    expect(el.value).toBe("");
    expect(fp.altInput.classList.contains("is-invalid")).toBe(true);
  });
});

describe("XMC-001: PositionAssignments uses the shared guard, not a copy of its own", () => {
  const repoRoot = path.resolve(__dirname, "..", "..", "..");
  const form = fs.readFileSync(path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "js",
    "Organization", "PositionAssignments", "form.js"), "utf8");
  const view = fs.readFileSync(path.join(repoRoot, "frontend", "Diten.Web", "Views", "Organization",
    "PositionAssignments", "Form.cshtml"), "utf8");

  test("form.js delegates to DitenDateField.guardAgainstSilentMisparse with the altFormat shape", () => {
    expect(form).toContain("window.DitenDateField && window.DitenDateField.guardAgainstSilentMisparse");
    expect(form).toContain("sharedGuard(instance, 'altFormat');");
    expect(form, "a private copy of the guard came back").not.toMatch(/lastTyped/);
    expect(form).toContain("guardDateAgainstSilentMisparse(el._flatpickr);");
  });

  test("the Form view loads the shared component after flatpickr and before form.js", () => {
    const flat = view.indexOf("assets/vendor/libs/flatpickr/flatpickr.js");
    const shared = view.indexOf("assets/js/shared/diten-datefield.js");
    const own = view.indexOf("assets/js/Organization/PositionAssignments/form.js");
    expect(flat).toBeGreaterThan(-1);
    expect(shared).toBeGreaterThan(flat);
    expect(own).toBeGreaterThan(shared);
  });
});
