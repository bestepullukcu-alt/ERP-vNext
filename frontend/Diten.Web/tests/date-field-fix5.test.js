const { loadScript } = require("./load-script");

/*
 * WCN-FIX-05 — the typed-date guard's fifth round, measured with the REAL vendored flatpickr (4.6.13) in jsdom.
 *
 *   1. Escape's restore is ANNOUNCED: a page that copies the field's `input` / `change` events into its own state
 *      (WorkCenterNext's subtask draft) follows the box back — it is not left on the misparse flatpickr committed on
 *      a window switch, nor on the raw typed text.
 *   2. A field the reader emptied on purpose has no old restore point: Escape after new typing leaves it empty.
 *   ·  The restore point is taken ONCE per typing run (M1) and dropped by a page write (M5).
 *   ·  A real window switch keeps document.activeElement on the box and turns document.hasFocus() false; the deferred
 *      judgment runs then too.
 *   ·  A page resetting an already-empty refused field with setDate('') is the page's write.
 *   ·  The component comparison counts the numbers (M6) and reads Arabic-Indic / full-width digits and Chinese units.
 *   ·  Limit, documented: a page write from inside an onChange hook during flatpickr's commit is judged with it.
 */

const nextTask = () => new Promise((resolve) => setTimeout(resolve, 0));
const PLAIN = { dateFormat: "Y-m-d", allowInput: true };
const ALT = { dateFormat: "Y-m-d", altInput: true, altFormat: "d.m.Y", allowInput: true };

beforeAll(() => {
  global.self = global.self || window;
  Object.defineProperty(window, "ontouchstart", { value: undefined, configurable: true, writable: true });
  loadScript("wwwroot/assets/vendor/libs/flatpickr/flatpickr.js");
  loadScript("wwwroot/assets/js/shared/diten-datefield.js");
});

afterEach(() => {
  vi.restoreAllMocks();
  document.querySelectorAll(".flatpickr-calendar").forEach((cal) => cal.remove());
  document.body.innerHTML = "";
});

const field = (opts, { key = "dateFormat" } = {}) => {
  document.body.innerHTML = '<input id="d"><button id="other" type="button">x</button>';
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
  const escapeInBox = () => box.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", keyCode: 27, bubbles: true }));
  const tab = () => document.getElementById("other").focus();
  return { el, fp, box, type, escapeInBox, tab };
};

// A page that keeps its own copy of the field, exactly as WorkCenterNext's subtask draft does (app.js onInput /
// onChange at document level read `event.target.value` of the hidden-or-plain value input).
const draftOf = (el) => {
  const draft = { value: el.value };
  const copy = (event) => { if (event.target === el) { draft.value = el.value; } };
  document.addEventListener("input", copy);
  document.addEventListener("change", copy);
  return draft;
};

// A browser window switch: the element keeps focus inside a window that lost it.
const windowSwitch = (box) => {
  vi.spyOn(document, "hasFocus").mockReturnValue(false);
  box.dispatchEvent(new FocusEvent("blur", { relatedTarget: null }));
};

describe("WCN-FIX-05 · 1: Escape's restore reaches the page's own copy of the field", () => {
  test("type a misparse → window switch (flatpickr commits 2026-03-03) → back → Escape: the page's copy is the old value again", async () => {
    const f = field(PLAIN);
    f.fp.setDate("2026-10-09", true);
    const draft = draftOf(f.el);
    f.box.focus();
    f.fp.open();
    f.type("2026-02-31");
    windowSwitch(f.box);
    expect(document.activeElement, "the switch must not move focus — that is jsdom's blur(), not a browser").toBe(f.box);
    await nextTask();
    vi.restoreAllMocks(); // the reader is back
    f.escapeInBox();

    expect(f.el.value).toBe("2026-10-09");
    expect(draft.value, "the page kept the misparse (or the emptied field) while the box shows the old value").toBe("2026-10-09");
    expect(f.box.classList.contains("is-invalid")).toBe(false);
  });

  test("without a window switch: type → Escape — the page's copy leaves the raw typed text", () => {
    const f = field(PLAIN);
    f.fp.setDate("2026-10-09", true);
    const draft = draftOf(f.el);
    f.box.focus();
    f.fp.open();
    f.type("2026-02-31");
    expect(draft.value).toBe("2026-02-31"); // the typing reached the page, as on the real screen
    f.escapeInBox();

    expect(draft.value).toBe("2026-10-09");
    expect(f.el.value).toBe("2026-10-09");
  });

  test("on a plain field the restore's own `input` echo is not mistaken for typing (nothing left to judge)", () => {
    const f = field(PLAIN);
    f.fp.setDate("2026-10-09", true);
    f.box.focus();
    f.fp.open();
    f.type("2026-02-31");
    f.escapeInBox();
    f.tab();
    expect(f.el.value).toBe("2026-10-09");
    expect(f.box.classList.contains("is-invalid")).toBe(false);
  });
});

describe("WCN-FIX-05 · 2 and the restore point's life", () => {
  test("a field the reader EMPTIED and left keeps no old restore point: type → Escape leaves it empty", () => {
    const f = field(ALT, { key: "altFormat" });
    f.fp.setDate("2026-10-09", false);
    f.box.focus();
    f.fp.open();
    f.type("");      // the reader deletes the date…
    f.tab();         // …and leaves the field: an empty optional date is a valid choice
    expect(f.el.value).toBe("");

    f.box.focus();
    f.fp.open();
    f.type("3");
    f.escapeInBox();

    expect(f.el.value, "Escape brought back the date the reader deleted on purpose").toBe("");
    expect(f.box.value).toBe("");
  });

  test("M1: the restore point is taken ONCE per typing run — a commit between keystrokes does not become it", () => {
    const f = field(PLAIN);
    f.fp.setDate("2026-10-09", false);
    f.box.focus();
    f.fp.open();
    f.type("2026-02-31");
    f.box.dispatchEvent(new FocusEvent("blur", { relatedTarget: null })); // flatpickr's onBlur commits 2026-03-03
    expect(f.fp.selectedDates.length).toBe(1);
    f.type("2026-02-31 ");  // the reader goes on typing in the same run
    f.escapeInBox();

    expect(f.el.value, "Escape restored the commit made in the middle of the typing").toBe("2026-10-09");
  });

  test("M5: a page write ends the run's restore point — Escape after it comes back to the PAGE's value", () => {
    const f = field(PLAIN);
    f.fp.setDate("2026-10-09", false);
    f.box.focus();
    f.fp.open();
    f.type("3");
    f.fp.setDate("2026-11-11", false); // the page writes while the reader is typing
    f.type("4");
    f.escapeInBox();

    expect(f.el.value).toBe("2026-11-11");
  });

  test("a page resetting an already-EMPTY refused field with setDate('') is the page's write: the mark goes", () => {
    const f = field(PLAIN);
    f.box.focus();
    f.fp.open();
    f.type("2026-02-31");
    f.box.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", keyCode: 13, bubbles: true }));
    expect(f.box.classList.contains("is-invalid")).toBe(true);
    expect(f.box.value).toBe("");

    f.fp.setDate("", false);
    expect(f.box.classList.contains("is-invalid")).toBe(false);
  });
});

describe("WCN-FIX-05: the window switch, as a browser does it", () => {
  test("activeElement stays on the box and hasFocus() is false: the deferred judgment refuses the committed misparse", async () => {
    const f = field(ALT, { key: "altFormat" });
    f.box.focus();
    f.fp.open();
    f.type("31.02.2026");
    windowSwitch(f.box);
    await nextTask();

    expect(f.el.value).toBe("");
    expect(f.box.classList.contains("is-invalid")).toBe(true);
  });

  test("control: a blur that leaves the reader in the box with the window focused judges nothing yet", async () => {
    const f = field(ALT, { key: "altFormat" });
    f.box.focus();
    f.fp.open();
    f.type("31.02");
    vi.spyOn(document, "hasFocus").mockReturnValue(true);
    f.box.dispatchEvent(new FocusEvent("blur", { relatedTarget: null }));
    await nextTask();

    expect(f.box.classList.contains("is-invalid")).toBe(false);
  });
});

describe("WCN-FIX-05: the component comparison", () => {
  const reads = (text, date, fmt) => window.DitenDateField.typedReadsBack(text, date, fmt);
  const march5 = new Date(2026, 2, 5);

  test("M6: the count of numbers must match the format — '5/3/2026/7' is not 5 March 2026", () => {
    expect(reads("5/3/2026", march5, "j/n/Y")).toBe(true);
    expect(reads("5/3/2026/7", march5, "j/n/Y")).toBe(false);
  });

  test("M6 through the real field: '5/3/2026/7' typed and Tab is refused (flatpickr itself reads it as 5 March)", () => {
    const f = field({ dateFormat: "Y-m-d", altInput: true, altFormat: "j/n/Y", allowInput: true }, { key: "altFormat" });
    f.type("5/3/2026/7");
    f.tab();
    expect(f.el.value).toBe("");
    expect(f.box.classList.contains("is-invalid")).toBe(true);
  });

  test.each([
    ["Arabic-Indic digits", "٥/٣/٢٠٢٦", "j/n/Y"],
    ["extended Arabic-Indic digits", "۵/۳/۲۰۲۶", "j/n/Y"],
    ["full-width digits", "５/３/２０２６", "j/n/Y"],
    ["a right-to-left mark typed or pasted", "‏5/3/2026", "j/n/Y"],
    ["Chinese date units", "2026年3月5日", "Y/n/j"]
  ])("%s are read as the date they mean (5 March 2026)", (_label, typed, fmt) => {
    const f = field({ dateFormat: "Y-m-d", altInput: true, altFormat: fmt, allowInput: true }, { key: "altFormat" });
    f.type(typed);
    f.tab();
    expect(f.el.value).toBe("2026-03-05");
    expect(f.box.classList.contains("is-invalid")).toBe(false);
  });

  test("an overflow in Arabic-Indic digits is still refused", () => {
    const f = field({ dateFormat: "Y-m-d", altInput: true, altFormat: "j/n/Y", allowInput: true }, { key: "altFormat" });
    f.type("٣١/٢/٢٠٢٦");
    f.tab();
    expect(f.el.value).toBe("");
    expect(f.box.classList.contains("is-invalid")).toBe(true);
  });
});

describe("WCN-FIX-05: documented limit", () => {
  test("a page write from INSIDE an onChange hook during flatpickr's commit is judged with it (a clamping hook gets refused)", () => {
    const f = field(PLAIN);
    f.fp.config.onChange.unshift((dates) => {
      if (dates[0] && dates[0] < new Date(2026, 5, 1)) { f.fp.setDate("2026-06-01", false); }
    });
    f.box.focus();
    f.fp.open();
    f.type("2026-02-20");
    f.box.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", keyCode: 13, bubbles: true }));

    // The typed date was valid, but what the field holds after the hook is not what was typed: refused. The safe side
    // for a GxP date; no screen clamps in an onChange hook today (measured).
    expect(f.el.value).toBe("");
    expect(f.box.classList.contains("is-invalid")).toBe(true);
  });
});
