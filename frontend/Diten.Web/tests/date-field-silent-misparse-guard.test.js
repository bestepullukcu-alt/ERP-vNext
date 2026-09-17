const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * BL-391 — "2026-09-13" typed into a gg.aa.yyyy date field silently saved as "2026-06-20"; no warning, no
 * rejection. `allowInput: true` hands flatpickr free text on close, and flatpickr's own parser is lenient enough
 * to resolve text that does not match the field's format into SOME date instead of refusing it.
 *
 * `DitenDateField.guardAgainstSilentMisparse` is exercised directly against a minimal flatpickr-shaped double —
 * everything the function actually reads (input/altInput, config[formatKey], config.onClose, formatDate, clear)
 * — the same choice date-field-never-dies-silently.test.js already made for this component instead of loading
 * the real vendor bundle into jsdom.
 *
 * Organization/PositionAssignments/form.js's OWN copy (`guardDateAgainstSilentMisparse`, altInput/altFormat
 * shape — that file cannot depend on this component being loaded, since its host view never loads it) is
 * checked below by structural equivalence: it must implement the identical close-time contract and actually be
 * wired to the flatpickr instance it creates.
 */

const DATEFIELD = "wwwroot/assets/js/shared/diten-datefield.js";
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const POSITION_ASSIGNMENTS_FORM = path.join(
  repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "js", "Organization", "PositionAssignments", "form.js"
);

const fakeInstance = ({ altInput = false } = {}) => {
  const input = document.createElement("input");
  const alt = altInput ? document.createElement("input") : null;
  const instance = {
    input,
    altInput: alt,
    config: { dateFormat: "Y-m-d", altFormat: "d.m.Y", onClose: [] },
    selectedDates: [],
    clear() {
      instance.input.value = "";
      if (instance.altInput) { instance.altInput.value = ""; }
      instance.selectedDates = [];
    },
    // A tiny, deterministic stand-in for flatpickr's own formatDate — only the two formats this module uses.
    formatDate(date, format) {
      const pad = (n) => String(n).padStart(2, "0");
      const y = date.getFullYear();
      const m = pad(date.getMonth() + 1);
      const d = pad(date.getDate());
      return format === "d.m.Y" ? `${d}.${m}.${y}` : `${y}-${m}-${d}`;
    }
  };
  return instance;
};

const typeInto = (el, value) => {
  el.value = value;
  el.dispatchEvent(new Event("input"));
};

const close = (instance, resolvedDate) => {
  instance.selectedDates = resolvedDate ? [resolvedDate] : [];
  instance.config.onClose.forEach((fn) => fn(instance.selectedDates, "", instance));
};

describe("BL-391: DitenDateField.guardAgainstSilentMisparse never lets a misread date through quietly", () => {
  beforeEach(() => { loadScript(DATEFIELD); });

  test("text that reformats back to exactly what was typed is accepted", () => {
    const instance = fakeInstance();
    global.DitenDateField.guardAgainstSilentMisparse(instance, "dateFormat");
    typeInto(instance.input, "2026-09-13");

    close(instance, new Date(2026, 8, 13));

    expect(instance.input.value).toBe("2026-09-13");
    expect(instance.input.classList.contains("is-invalid")).toBe(false);
  });

  test("BL-391's own repro: flatpickr resolving typed text to a DIFFERENT date is cleared and marked invalid", () => {
    const instance = fakeInstance({ altInput: true });
    global.DitenDateField.guardAgainstSilentMisparse(instance, "altFormat");
    typeInto(instance.altInput, "13.09.2026");

    // flatpickr silently resolved the typed text to a DIFFERENT date — the reported failure mode.
    close(instance, new Date(2026, 5, 20));

    expect(instance.altInput.value).toBe("");
    expect(instance.altInput.classList.contains("is-invalid")).toBe(true);
  });

  test("text flatpickr could not resolve at all is cleared and marked invalid", () => {
    const instance = fakeInstance();
    global.DitenDateField.guardAgainstSilentMisparse(instance, "dateFormat");
    typeInto(instance.input, "not a date");

    close(instance, null);

    expect(instance.input.value).toBe("");
    expect(instance.input.classList.contains("is-invalid")).toBe(true);
  });

  test("an empty field on close is left alone — nothing typed is not something to reject", () => {
    const instance = fakeInstance();
    global.DitenDateField.guardAgainstSilentMisparse(instance, "dateFormat");

    close(instance, null);

    expect(instance.input.value).toBe("");
    expect(instance.input.classList.contains("is-invalid")).toBe(false);
  });

  test("a later VALID entry clears the invalid mark", () => {
    const instance = fakeInstance();
    global.DitenDateField.guardAgainstSilentMisparse(instance, "dateFormat");
    typeInto(instance.input, "garbage");
    close(instance, null);
    expect(instance.input.classList.contains("is-invalid")).toBe(true);

    typeInto(instance.input, "2026-09-13");
    close(instance, new Date(2026, 8, 13));

    expect(instance.input.classList.contains("is-invalid")).toBe(false);
  });
});

describe("BL-391: PositionAssignments/form.js's OWN altInput guard matches the same close-time contract", () => {
  const source = fs.readFileSync(POSITION_ASSIGNMENTS_FORM, "utf8");

  test("guardDateAgainstSilentMisparse re-renders through the SAME altFormat the field displays, not dateFormat", () => {
    expect(source).toContain("instance.formatDate(resolved, instance.config.altFormat)");
  });

  test("a mismatch or an unresolved date clears the field and marks the VISIBLE (altInput) element invalid", () => {
    expect(source).toContain("if (!resolved || reformatted !== raw) { instance.clear(); target.classList.add('is-invalid'); }");
    expect(source).toContain("const target = instance.altInput || instance.input;");
  });

  test("the raw typed text is captured on 'input', before flatpickr's own close-time reformat can overwrite it", () => {
    expect(source).toContain("target.addEventListener('input', () => { lastTyped = target.value; });");
  });

  test("the guard is actually wired to the flatpickr instance initDatePickers() just created", () => {
    const initFn = source.slice(source.indexOf("const initDatePickers"), source.indexOf("const dateOrNull"));
    expect(initFn).toContain("window.flatpickr(el, { dateFormat: 'Y-m-d', altInput: true, altFormat: L.DateFormat || 'Y-m-d', allowInput: true });");
    expect(initFn).toContain("guardDateAgainstSilentMisparse(el._flatpickr);");
  });
});
