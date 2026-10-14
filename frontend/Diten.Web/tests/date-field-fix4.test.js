const { loadScript } = require("./load-script");

/*
 * WCN-FIX-04 — the typed-date guard's fourth round, measured with the REAL vendored flatpickr (4.6.13) in jsdom.
 *
 *   1. Escape after flatpickr has ALREADY committed the misparse (alt-tab blur) brings back what the field held
 *      before the typing — never the committed 03.03.
 *   2. A value the PAGE writes (setDate / clear: quick-create's default, a saved view) ends a refusal and pending
 *      typing: it is kept, unmarked.
 *   ·  An empty field is not left red after the reader types and deletes.
 *   ·  Escape in the box cancels the typing too.
 *   ·  Open calendar + focus gone where nothing closes it (alt-tab; a mouse click on a touch-bound desktop): the
 *      committed misparse is judged one task later.
 *   ·  Typed text is compared by COMPONENT: unpadded formats ("5/3/2026" and "05/03/2026") and Arabic's format
 *      (its bidi marks are dropped server-side, FlatpickrDateFormat) are read correctly; 31/2 is still refused.
 */

const ALT = { dateFormat: "Y-m-d", altInput: true, altFormat: "d.m.Y", allowInput: true };

const setTouch = (touch) => {
  Object.defineProperty(window, "ontouchstart", { value: touch ? null : undefined, configurable: true, writable: true });
};
const nextTask = () => new Promise((resolve) => setTimeout(resolve, 0));

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

const field = (opts, { value = "", key = "altFormat", touch = false } = {}) => {
  setTouch(touch);
  document.body.innerHTML = `<input id="d" value="${value}"><button id="other" type="button">x</button>`;
  const el = document.getElementById("d");
  window.flatpickr(el, opts);
  const fp = el._flatpickr;
  window.DitenDateField.guardAgainstSilentMisparse(fp, key);
  const box = fp.altInput || fp.input;
  const type = (text) => {
    if (document.activeElement !== box) { box.focus(); }
    box.value = text;
    box.dispatchEvent(new Event("input", { bubbles: true }));
  };
  const tab = () => document.getElementById("other").focus();
  const escapeIn = (node) => node.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", keyCode: 27, bubbles: true }));
  return { el, fp, box, type, tab, escapeIn };
};

describe("WCN-FIX-04 · 1: Escape restores the value from BEFORE the typing, never a misparse flatpickr committed", () => {
  for (const waited of [false, true]) {
    test(`type "31.02.2026" → alt-tab (blur, no relatedTarget) → back, month arrow → Escape${waited ? " (after a task)" : ""}`, async () => {
      const f = field(ALT, { value: "2026-10-09" });
      f.box.focus();
      f.fp.open();
      f.type("31.02.2026");
      f.box.dispatchEvent(new FocusEvent("blur", { relatedTarget: null })); // flatpickr's onBlur commits 03.03
      f.box.blur();
      if (waited) { await nextTask(); }
      f.fp.nextMonthNav.dispatchEvent(new MouseEvent("click", { bubbles: true }));
      const day = f.fp.daysContainer.querySelector(".flatpickr-day");
      day.focus();
      f.escapeIn(day);

      expect(f.fp.isOpen).toBe(false);
      expect(f.el.value, "Escape brought back the misparse").not.toBe("2026-03-03");
      expect(f.el.value).toBe("2026-10-09");
      expect(f.box.value).toBe("09.10.2026");
      expect(f.box.classList.contains("is-invalid")).toBe(false);
    });
  }
});

describe("WCN-FIX-04 · 2: a value the page writes is the page's — kept, unmarked", () => {
  test("refusal → setDate(today, false) (quick-create's default) → open and close: the value stays, no mark", () => {
    const f = field({ dateFormat: "Y-m-d", allowInput: true }, { key: "dateFormat" });
    f.box.focus();
    f.fp.open();
    f.type("2026-02-31");
    f.box.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", keyCode: 13, bubbles: true }));
    expect(f.box.classList.contains("is-invalid")).toBe(true);

    f.fp.setDate("2026-10-09", false);
    f.fp.open();
    document.body.dispatchEvent(new MouseEvent("mousedown", { bubbles: true }));

    expect(f.fp.isOpen).toBe(false);
    expect(f.el.value).toBe("2026-10-09");
    expect(f.box.classList.contains("is-invalid")).toBe(false);
  });

  test("refusal → the page clears the field (a reset) → the empty field is not left red", () => {
    const f = field({ dateFormat: "Y-m-d", allowInput: true }, { key: "dateFormat" });
    f.box.focus();
    f.fp.open();
    f.type("2026-02-31");
    f.box.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", keyCode: 13, bubbles: true }));
    f.fp.clear();
    f.fp.open();
    document.body.dispatchEvent(new MouseEvent("mousedown", { bubbles: true }));

    expect(f.el.value).toBe("");
    expect(f.box.classList.contains("is-invalid")).toBe(false);
  });

  test("a saved view writes a filter date while the reader's text is pending: the written value is kept", () => {
    const f = field(ALT);
    f.box.focus();
    f.fp.open();
    f.type("31.0");                     // the reader has started typing…
    f.fp.setDate("2026-10-09", false);  // …and the page applies a saved view
    f.tab();

    expect(f.fp.isOpen).toBe(false);
    expect(f.el.value).toBe("2026-10-09");
    expect(f.box.classList.contains("is-invalid")).toBe(false);
  });

  test("flatpickr's OWN commit of the typed text is not a page write: '31.02.2026' + Enter is still refused", () => {
    const f = field(ALT);
    f.box.focus();
    f.fp.open();
    f.type("31.02.2026");
    f.box.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", keyCode: 13, bubbles: true }));
    expect(f.el.value).toBe("");
    expect(f.box.classList.contains("is-invalid")).toBe(true);
  });
});

describe("WCN-FIX-04: smaller edges", () => {
  test("after a refusal, typing and deleting leaves an EMPTY optional field unmarked (it no longer blocks Save)", () => {
    const f = field(ALT);
    f.box.focus();
    f.fp.open();
    f.type("31.02.2026");
    f.box.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", keyCode: 13, bubbles: true }));
    expect(f.box.classList.contains("is-invalid")).toBe(true);

    f.type("3");
    f.type("");
    f.tab();
    expect(f.el.value).toBe("");
    expect(f.box.classList.contains("is-invalid")).toBe(false);
  });

  test("a mark the PAGE set (its own required check) is not removed by the guard", () => {
    const f = field(ALT);
    f.box.classList.add("is-invalid");
    f.box.focus();
    f.tab();
    expect(f.box.classList.contains("is-invalid")).toBe(true);
  });

  test("Escape in the BOX (open calendar) cancels the typing and keeps the previous value", () => {
    const f = field(ALT, { value: "2026-10-09" });
    f.box.focus();
    f.fp.open();
    f.type("31.02.2026");
    f.escapeIn(f.box);

    expect(f.fp.isOpen).toBe(false);
    expect(f.el.value).toBe("2026-10-09");
    expect(f.box.value).toBe("09.10.2026");
    expect(f.box.classList.contains("is-invalid")).toBe(false);
  });

  test("Escape in the BOX (closed calendar, after a pick) cancels the typing and keeps the picked value", () => {
    const f = field(ALT);
    f.box.focus();
    f.fp.open();
    f.fp.jumpToDate("2026-01-01");
    [...f.fp.daysContainer.querySelectorAll(".flatpickr-day")]
      .find((n) => n.dateObj.getMonth() === 0 && n.dateObj.getDate() === 15)
      .dispatchEvent(new MouseEvent("click", { bubbles: true }));
    expect(f.fp.isOpen).toBe(false);
    f.type("31.02.2026");
    f.escapeIn(f.box);
    f.tab();

    expect(f.el.value).toBe("2026-01-15");
    expect(f.box.value).toBe("15.01.2026");
    expect(f.box.classList.contains("is-invalid")).toBe(false);
  });

  test("touch-capable desktop: a mouse click on something that takes no focus closes nothing — the misparse is judged one task later", async () => {
    const f = field(ALT, { touch: true });
    f.box.focus();
    f.fp.open();
    f.type("31.02.2026");
    document.body.dispatchEvent(new MouseEvent("mousedown", { bubbles: true })); // flatpickr listens to touchstart here
    f.box.blur();                                                                 // focus goes nowhere
    expect(f.fp.isOpen, "the click closed the calendar — this is not the path under test").toBe(true);
    await nextTask();

    expect(f.el.value, "flatpickr's onBlur commit stood unjudged").toBe("");
    expect(f.box.classList.contains("is-invalid")).toBe(true);
  });

  test("open calendar, focus moving INTO the calendar is not leaving the field: nothing is judged yet", async () => {
    const f = field(ALT);
    f.box.focus();
    f.fp.open();
    f.type("31.02.2026");
    const day = f.fp.daysContainer.querySelector(".flatpickr-day");
    f.box.dispatchEvent(new FocusEvent("blur", { relatedTarget: day }));
    day.focus();
    await nextTask();
    expect(f.box.classList.contains("is-invalid")).toBe(false);
    expect(f.box.value).toBe("31.02.2026");
  });
});

/*
 * The seven tenant languages' display formats, as FlatpickrDateFormat produces them from .NET's ShortDatePattern
 * (measured; pinned by Diten.Web.Tests FlatpickrDateFormatTests). Arabic's right-to-left marks are dropped there.
 */
const CULTURE_FORMATS = {
  en: { fmt: "n/j/Y", ok: ["3/5/2026", "03/05/2026"], refused: ["2/31/2026"] },
  tr: { fmt: "j.m.Y", ok: ["5.3.2026", "05.03.2026"], refused: ["31.2.2026"] },
  fr: { fmt: "d/m/Y", ok: ["5/3/2026", "05/03/2026"], refused: ["31/02/2026"] },
  es: { fmt: "j/n/Y", ok: ["5/3/2026", "05/03/2026"], refused: ["31/2/2026"] },
  zh: { fmt: "Y/n/j", ok: ["2026/3/5", "2026/03/05"], refused: ["2026/2/31"] },
  ar: { fmt: "j/n/Y", ok: ["5/3/2026", "05/03/2026"], refused: ["31/2/2026"] },
  ru: { fmt: "d.m.Y", ok: ["5.3.2026", "05.03.2026"], refused: ["31.02.2026"] }
};

describe("WCN-FIX-04: typed text is compared by COMPONENT, so unpadded and padded spellings both read back", () => {
  for (const [lang, { fmt, ok, refused }] of Object.entries(CULTURE_FORMATS)) {
    for (const typed of ok) {
      test(`${lang} (${fmt}): "${typed}" is accepted as 5 March 2026`, () => {
        const f = field({ dateFormat: "Y-m-d", altInput: true, altFormat: fmt, allowInput: true });
        f.type(typed);
        f.tab();
        expect(f.el.value).toBe("2026-03-05");
        expect(f.box.classList.contains("is-invalid")).toBe(false);
      });
    }
    for (const typed of refused) {
      test(`${lang} (${fmt}): "${typed}" (an overflow) is refused`, () => {
        const f = field({ dateFormat: "Y-m-d", altInput: true, altFormat: fmt, allowInput: true });
        f.type(typed);
        f.tab();
        expect(f.el.value).toBe("");
        expect(f.box.classList.contains("is-invalid")).toBe(true);
      });
    }
  }

  test("the comparison itself: numbers in format order against the resolved date; a 4-digit year takes 4 digits", () => {
    const reads = window.DitenDateField.typedReadsBack;
    const march5 = new Date(2026, 2, 5, 10, 30);
    expect(reads("5/3/2026", march5, "j/n/Y")).toBe(true);
    expect(reads("05/03/2026", march5, "j/n/Y")).toBe(true);
    expect(reads("5/3/26", march5, "j/n/Y")).toBe(false);
    expect(reads("5/3/2026x", march5, "j/n/Y")).toBe(false);
    expect(reads("5/3", march5, "j/n/Y")).toBe(false);
    expect(reads("2026-03-05 10:30", march5, "Y-m-d H:i")).toBe(true);
    expect(reads("2026-03-05 10:31", march5, "Y-m-d H:i")).toBe(false);
    expect(reads("5/3/26", march5, "j/n/y")).toBe(true);
  });
});
