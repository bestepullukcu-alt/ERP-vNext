const { loadScript } = require("./load-script");

/*
 * BL-391 × WCN-FIX-02/03 — a typed date that does not exist ("31.02") is cleared and marked invalid on EVERY path
 * that commits typed text, on every field shape the product uses, with the calendar OPEN or CLOSED. Measured with the
 * REAL vendored flatpickr (4.6.13).
 *
 * Open calendar — flatpickr commits typed text through different code on each path:
 *   Enter          onKeyDown → setDate(value, true, fmt) → close
 *   Tab            onBlur → setDate(value, true, fmt), then the focus landing elsewhere closes it (documentClick)
 *   click outside  documentClick → setDate(value, false, fmt) → (enableTime) updateTime → updateValue → close
 *   touch outside  the same handler, bound to `touchstart` on touch devices
 * Closed calendar (WCN-FIX-03) — the box keeps focus with the calendar closed after every pick and after Escape, and
 * flatpickr's onBlur still commits what is typed there, with NO close: the guard judges on blur.
 *
 * Shapes: plain (the shared component's default), altInput + altFormat (Organization effective dates — the GxP
 * field BL-391 was reported on) and enableTime (meetings).
 *
 * No path is helped to its end: every test asserts the calendar CLOSED BY THE PATH ITSELF (`fp.isOpen === false`),
 * never closes it by hand — a manual close would run the guard on a path that, in the browser, never closes.
 * Tab is one realistic move: focus lands on the next control (jsdom fires the box's blur and the document's focus).
 *
 * jsdom defines `window.ontouchstart` (null), and flatpickr then binds its outside-click handler to `touchstart`
 * instead of `mousedown`. The desktop has no such property; it is removed for the desktop tests and kept for the
 * touch variant.
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

const setTouch = (touch) => {
  Object.defineProperty(window, "ontouchstart", { value: touch ? null : undefined, configurable: true, writable: true });
};

beforeAll(() => {
  global.self = global.self || window;
  setTouch(false);
  loadScript("wwwroot/assets/vendor/libs/flatpickr/flatpickr.js");
  loadScript("wwwroot/assets/js/shared/diten-datefield.js");
});

afterEach(() => {
  setTouch(false);
  document.querySelectorAll(".flatpickr-calendar").forEach((cal) => cal.remove());
  document.body.innerHTML = "";
});

const field = (shapeName, { value = "", extra = {}, touch = false } = {}) => {
  setTouch(touch);
  const shape = SHAPES[shapeName];
  document.body.innerHTML = `<input id="d" value="${value}"><button id="other" type="button">x</button>`;
  const el = document.getElementById("d");
  window.flatpickr(el, Object.assign({}, shape.opts, extra));
  const fp = el._flatpickr;
  window.DitenDateField.guardAgainstSilentMisparse(fp, shape.key);
  const box = fp.altInput || fp.input;
  const other = document.getElementById("other");
  // What a reader's keystrokes (or a paste) leave: the text in the box and ONE `input` event.
  const type = (text) => {
    if (document.activeElement !== box) { box.focus(); }
    box.value = text;
    box.dispatchEvent(new Event("input", { bubbles: true }));
  };
  const dayOf = (y, m, d) => {
    fp.jumpToDate(`${y}-${String(m + 1).padStart(2, "0")}-01`);
    return [...fp.daysContainer.querySelectorAll(".flatpickr-day")]
      .find((n) => n.dateObj.getFullYear() === y && n.dateObj.getMonth() === m && n.dateObj.getDate() === d
        && !n.classList.contains("prevMonthDay") && !n.classList.contains("nextMonthDay"));
  };
  const pick = (y, m, d) => {
    box.focus();
    fp.open();
    dayOf(y, m, d).dispatchEvent(new MouseEvent("click", { bubbles: true }));
  };
  const tab = () => other.focus();
  const clickOutside = () => {
    document.body.dispatchEvent(new MouseEvent("mousedown", { bubbles: true }));
    box.blur();
  };
  return { el, fp, box, other, type, pick, dayOf, tab, clickOutside };
};

const OPEN_PATHS = {
  Enter: (f) => f.box.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", keyCode: 13, bubbles: true })),
  Tab: (f) => f.tab(),
  "click outside": (f) => document.body.dispatchEvent(new MouseEvent("mousedown", { bubbles: true }))
};

const refused = ({ el, box, fp }) => {
  expect(fp.isOpen, "the path did not close the calendar by itself").toBe(false);
  expect(el.value, "the misparse was saved as a different date").toBe("");
  expect(box.classList.contains("is-invalid")).toBe(true);
};

describe("BL-391, calendar OPEN: a typed date that does not exist is never saved as another date", () => {
  for (const shapeName of Object.keys(SHAPES)) {
    for (const pathName of Object.keys(OPEN_PATHS)) {
      test(`${shapeName} field · ${pathName}: "${SHAPES[shapeName].typed}" is cleared and marked invalid`, () => {
        const f = field(shapeName);
        f.box.focus();
        f.fp.open();
        f.type(SHAPES[shapeName].typed);
        OPEN_PATHS[pathName](f);
        refused(f);
      });
    }
    test(`${shapeName} field · touch outside (touchstart): "${SHAPES[shapeName].typed}" is cleared and marked invalid`, () => {
      const f = field(shapeName, { touch: true });
      f.box.focus();
      f.fp.open();
      f.type(SHAPES[shapeName].typed);
      document.body.dispatchEvent(new Event("touchstart", { bubbles: true }));
      refused(f);
    });
  }
});

describe("BL-391, calendar OPEN: a typed date that exists is kept on every path (the guard does not over-reach)", () => {
  for (const shapeName of Object.keys(SHAPES)) {
    for (const pathName of Object.keys(OPEN_PATHS)) {
      test(`${shapeName} field · ${pathName}: "${SHAPES[shapeName].valid}" is kept`, () => {
        const f = field(shapeName);
        f.box.focus();
        f.fp.open();
        f.type(SHAPES[shapeName].valid);
        OPEN_PATHS[pathName](f);

        expect(f.fp.isOpen).toBe(false);
        expect(f.el.value).not.toBe("");
        expect(f.box.value).toBe(SHAPES[shapeName].valid);
        expect(f.box.classList.contains("is-invalid")).toBe(false);
      });
    }
  }
});

/*
 * WCN-FIX-03 — the calendar is never opened by the test for the typing: it is CLOSED while the reader types.
 *   (a) a day was picked (selectDate → focusAndClose: box focused, calendar closed) → type → Tab;
 *   (b) Escape in the calendar (focusAndClose) → type → click outside;
 *   (c) a day was picked → type → click the (closed) box, which opens it → Tab.
 */
const CLOSED_SCENARIOS = {
  "picked a day, then typed, then Tab": (f, text) => {
    f.pick(2026, 0, 15);
    expect(f.fp.isOpen, "a non-time pick closes the calendar").toBe(SHAPES.enableTime === f.shape);
    f.type(text);
    f.tab();
  },
  "Escape, then typed, then click outside": (f, text) => {
    f.box.focus();
    f.fp.open();
    const day = f.fp.daysContainer.querySelector(".flatpickr-day");
    day.focus();
    day.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", keyCode: 27, bubbles: true }));
    expect(f.fp.isOpen, "Escape closes the calendar").toBe(false);
    f.type(text);
    f.clickOutside();
  },
  "picked a day, typed, clicked the closed box, then Tab": (f, text) => {
    f.pick(2026, 0, 15);
    f.type(text);
    f.box.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    if (!f.fp.isOpen) { f.fp.open(); } // the click on the box is what opens it in the browser
    f.tab();
  }
};

describe("BL-391, calendar CLOSED (WCN-FIX-03): typed text is judged when the box loses focus", () => {
  for (const scenario of Object.keys(CLOSED_SCENARIOS)) {
    for (const shapeName of Object.keys(SHAPES)) {
      test(`${shapeName} field · ${scenario}: "${SHAPES[shapeName].typed}" is refused`, () => {
        const f = field(shapeName);
        f.shape = SHAPES[shapeName];
        CLOSED_SCENARIOS[scenario](f, SHAPES[shapeName].typed);
        refused(f);
      });
      test(`${shapeName} field · ${scenario}: "${SHAPES[shapeName].valid}" is kept`, () => {
        const f = field(shapeName);
        f.shape = SHAPES[shapeName];
        CLOSED_SCENARIOS[scenario](f, SHAPES[shapeName].valid);
        expect(f.fp.isOpen).toBe(false);
        expect(f.box.value).toBe(SHAPES[shapeName].valid);
        expect(f.box.classList.contains("is-invalid")).toBe(false);
      });
    }
  }
});

describe("WCN-FIX-03: the edges of the typed-text rule", () => {
  test("a paste (one input event with the whole text) is judged like typing", () => {
    const f = field("altInput");
    f.box.focus();
    f.fp.open();
    f.box.value = "31.02.2026";
    f.box.dispatchEvent(new InputEvent("input", { bubbles: true, inputType: "insertFromPaste" }));
    f.tab();
    refused(f);
  });

  test("V1: a click on a DISABLED day selects nothing, so it does not wipe the typed text — Tab still refuses it", () => {
    const f = field("altInput", { extra: { disable: ["2026-02-10"] } });
    f.box.focus();
    f.fp.open();
    f.type("31.02.2026");
    const disabled = f.dayOf(2026, 1, 10);
    expect(disabled.classList.contains("flatpickr-disabled")).toBe(true);
    disabled.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    f.tab();
    refused(f);
  });

  test("week-number cells carry `flatpickr-day` and nothing else (measured), so only cells in daysContainer are days", () => {
    const f = field("plain", { extra: { weekNumbers: true } });
    const weekCell = [...f.fp.calendarContainer.querySelectorAll(".flatpickr-day")].find((n) => !f.fp.daysContainer.contains(n));
    expect(weekCell).toBeTruthy();
    expect(weekCell.className).toBe("flatpickr-day");
    f.box.focus();
    f.fp.open();
    f.type("2026-02-31");
    weekCell.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    f.tab();
    refused(f);
  });

  test("V2: altInput — a pick, then a paste of '31.02.2026' in the SAME task, then Tab: refused (no echo mark on the typed box)", () => {
    const f = field("altInput");
    f.pick(2026, 0, 15);
    f.type("31.02.2026");
    f.tab();
    refused(f);
  });

  test("V2: altInput — a pick, then a paste after the task ends, then Tab: refused", async () => {
    const f = field("altInput");
    f.pick(2026, 0, 15);
    await Promise.resolve();
    f.type("31.02.2026");
    f.tab();
    refused(f);
  });

  test("V2: the echo mark EXPIRES — a page onChange hook that throws stops the echo; the reader's next typing is still judged", async () => {
    const f = field("plain");
    f.fp.config.onChange.push(() => { throw new Error("a page hook that fails"); });
    const errors = vi.spyOn(console, "error").mockImplementation(() => {});
    /*
     * WP-LANE-GUARDS-02 — the hook throws INSIDE an event listener (flatpickr's click / blur), so the try / catch
     * below never sees it: jsdom reports it on `window`, and with no listener there vitest counts it as an unhandled
     * error and fails the run. The expected failure is taken here, by its message, and counted; anything else passes.
     */
    const hookFailures = [];
    const takeHookFailure = (event) => {
      if (event.error && event.error.message === "a page hook that fails") { hookFailures.push(event.error); event.preventDefault(); }
    };
    window.addEventListener("error", takeHookFailure);
    try {
      try {
        f.pick(2026, 0, 15);
      } catch (error) { /* the page hook's own failure */ }
      await Promise.resolve(); // the reader's typing is always a later task than the click
      f.type("2026-02-31");
      try { f.tab(); } catch (error) { /* the page hook fails again on the commit */ }
    } finally {
      window.removeEventListener("error", takeHookFailure);
      errors.mockRestore();
    }
    expect(hookFailures.length, "the page hook never failed — the case is not exercised").toBeGreaterThan(0);
    expect(f.el.value).toBe("");
    expect(f.box.classList.contains("is-invalid")).toBe(true);
  });

  test("V3: onOpen forgets typed text the box no longer shows (code rewrote the box without an event)", () => {
    const f = field("plain");
    f.pick(2026, 0, 15);
    f.type("2026-10-");                 // the reader starts typing in the closed box…
    f.fp.setDate("2026-10-09", false);  // …and code writes a value without an event (quick-create's default)
    f.box.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    if (!f.fp.isOpen) { f.fp.open(); }
    document.body.dispatchEvent(new MouseEvent("mousedown", { bubbles: true }));

    expect(f.fp.isOpen).toBe(false);
    expect(f.el.value).toBe("2026-10-09");
    expect(f.box.classList.contains("is-invalid")).toBe(false);
  });

  test("Escape in the open calendar cancels the typing and keeps the previous valid value", () => {
    const f = field("altInput", { value: "2026-10-09" });
    f.box.focus();
    f.fp.open();
    f.type("31.02.2026");
    const day = f.fp.daysContainer.querySelector(".flatpickr-day");
    day.focus();
    day.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", keyCode: 27, bubbles: true }));
    f.tab();

    expect(f.fp.isOpen).toBe(false);
    expect(f.el.value).toBe("2026-10-09");
    expect(f.box.value).toBe("09.10.2026");
    expect(f.box.classList.contains("is-invalid")).toBe(false);
  });

  test("enableTime: a valid typed date followed by an hour edit keeps the edited value (no false refusal)", () => {
    const f = field("enableTime");
    f.box.focus();
    f.fp.open();
    f.type("2026-02-20 10:00");
    f.fp.hourElement.focus();
    f.fp.hourElement.value = "11";
    f.fp.hourElement.dispatchEvent(new Event("input", { bubbles: true }));
    f.fp.hourElement.dispatchEvent(new Event("blur"));
    document.body.dispatchEvent(new MouseEvent("mousedown", { bubbles: true }));

    expect(f.fp.isOpen).toBe(false);
    expect(f.el.value).toBe("2026-02-20 11:00");
    expect(f.box.classList.contains("is-invalid")).toBe(false);
  });

  test("enableTime: a typo followed by an hour edit is refused, and the hour edit does not invent 'today'", () => {
    const f = field("enableTime");
    f.box.focus();
    f.fp.open();
    f.type("2026-02-31 10:00");
    f.fp.hourElement.focus();
    f.fp.hourElement.value = "11";
    f.fp.hourElement.dispatchEvent(new Event("input", { bubbles: true }));
    f.fp.hourElement.dispatchEvent(new Event("blur"));
    document.body.dispatchEvent(new MouseEvent("mousedown", { bubbles: true }));
    refused(f);
  });

  test("guarding the same instance twice binds nothing twice (one judgment, one clear)", () => {
    const f = field("plain");
    expect(window.DitenDateField.guardAgainstSilentMisparse(f.fp, "dateFormat")).toBe(true);
    const clear = vi.spyOn(f.fp, "clear");
    f.box.focus();
    f.fp.open();
    f.type("2026-02-31");
    f.box.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", keyCode: 13, bubbles: true }));
    refused(f);
    expect(clear).toHaveBeenCalledTimes(1);
  });
});
