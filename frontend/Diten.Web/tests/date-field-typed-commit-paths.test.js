const { loadScript } = require("./load-script");

/*
 * BL-391 × WCN-FIX-02 — a typed date that does not exist ("31.02") is cleared and marked invalid on EVERY path that
 * commits typed text, on every field shape the product uses. Measured with the REAL vendored flatpickr (4.6.13).
 *
 * Why three paths: flatpickr commits typed text through different code on each, and two of them run updateValue
 * (→ onValueUpdate) BEFORE onClose, where the guard judges:
 *   Enter          onKeyDown → setDate(value, true, fmt) → close
 *   Tab / blur     onBlur → setDate(value, true, fmt), then the focus move closes it
 *   click outside  documentClick → setDate(value, false, fmt) → (enableTime) updateTime → updateValue → close
 * Why three shapes: plain (the shared component's own default), altInput + altFormat (Organization /
 * PositionAssignments' effective dates — the GxP field BL-391 was reported on) and enableTime (meetings).
 *
 * jsdom defines `window.ontouchstart` (null), and flatpickr then binds its outside-click handler to `touchstart`
 * instead of `mousedown`. The desktop browser has no such property; it is removed here so `document.body` mousedown
 * reaches the same handler it reaches on a desktop.
 */

const SHAPES = {
  plain: { opts: { dateFormat: "Y-m-d", allowInput: true }, key: "dateFormat", typed: "2026-02-31", valid: "2026-02-20" },
  altInput: {
    opts: { dateFormat: "Y-m-d", altInput: true, altFormat: "d.m.Y", allowInput: true },
    key: "altFormat", typed: "31.02.2026", valid: "20.02.2026"
  },
  enableTime: {
    opts: { dateFormat: "Y-m-d H:i", enableTime: true, allowInput: true },
    key: "dateFormat", typed: "2026-02-31 10:00", valid: "2026-02-20 10:00"
  }
};

const PATHS = {
  Enter: (box) => {
    box.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", keyCode: 13, bubbles: true }));
  },
  "Tab / blur": (box) => {
    const other = document.getElementById("other");
    box.dispatchEvent(new FocusEvent("blur", { relatedTarget: other }));
    other.focus();
  },
  "click outside": () => {
    document.body.dispatchEvent(new MouseEvent("mousedown", { bubbles: true }));
  }
};

beforeAll(() => {
  global.self = global.self || window;
  Object.defineProperty(window, "ontouchstart", { value: undefined, configurable: true, writable: true });
  loadScript("wwwroot/assets/vendor/libs/flatpickr/flatpickr.js");
  loadScript("wwwroot/assets/js/shared/diten-datefield.js");
});

afterEach(() => {
  document.querySelectorAll(".flatpickr-calendar").forEach((cal) => cal.remove());
  document.body.innerHTML = "";
});

const typeAndCommit = (shapeName, pathName, text) => {
  const shape = SHAPES[shapeName];
  document.body.innerHTML = '<input id="d"><button id="other" type="button">x</button>';
  const el = document.getElementById("d");
  window.flatpickr(el, shape.opts);
  const fp = el._flatpickr;
  window.DitenDateField.guardAgainstSilentMisparse(fp, shape.key);

  const box = fp.altInput || fp.input;
  box.focus();
  fp.open();
  box.value = text;
  box.dispatchEvent(new Event("input", { bubbles: true }));
  PATHS[pathName](box);
  if (fp.isOpen) { fp.close(); }
  return { el, box };
};

describe("BL-391: a typed date that does not exist is never saved as another date", () => {
  for (const shapeName of Object.keys(SHAPES)) {
    for (const pathName of Object.keys(PATHS)) {
      test(`${shapeName} field · ${pathName}: "${SHAPES[shapeName].typed}" is cleared and marked invalid`, () => {
        const { el, box } = typeAndCommit(shapeName, pathName, SHAPES[shapeName].typed);

        expect(el.value, "the misparse was saved as a different date").toBe("");
        expect(box.classList.contains("is-invalid")).toBe(true);
      });
    }
  }
});

describe("BL-391: a typed date that exists is kept on every path (the guard does not over-reach)", () => {
  for (const shapeName of Object.keys(SHAPES)) {
    for (const pathName of Object.keys(PATHS)) {
      test(`${shapeName} field · ${pathName}: "${SHAPES[shapeName].valid}" is kept`, () => {
        const { el, box } = typeAndCommit(shapeName, pathName, SHAPES[shapeName].valid);

        expect(el.value).not.toBe("");
        expect(box.value).toBe(SHAPES[shapeName].valid);
        expect(box.classList.contains("is-invalid")).toBe(false);
      });
    }
  }
});
