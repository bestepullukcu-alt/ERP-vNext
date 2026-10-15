const { loadScript } = require("./load-script");

/*
 * WCN-FIX-07 — the typed-date guard's seventh round, with the REAL vendored flatpickr (4.6.13) and, for the side panel,
 * the REAL vendored Bootstrap (Offcanvas). Three mutants the FIX6 tests let live, and four small rules:
 *
 *   · the caret is placed by the length of the NORMALISED text before it — a mark in the middle, before the caret,
 *     moves it back by one (a mark at the start, with the caret at the end, could not tell: the box clamps the caret);
 *   · on a PLAIN field the restore's own `input` echo is marked as an echo: a second Escape, with nothing left to
 *     cancel, reaches the panel and closes it;
 *   · an Escape that cancels with the calendar CLOSED (a field refused on Enter) also stops at the field;
 *   · the caret is placed only in the box the reader is in; a composition that ends with the text the run already has
 *     is not new typing; an Escape inside an IME composition is the IME's, not a cancel.
 */

const settle = (ms = 400) => new Promise((resolve) => setTimeout(resolve, ms));
const ALT = { dateFormat: "Y-m-d", altInput: true, altFormat: "j/n/Y", allowInput: true };
const PLAIN = { dateFormat: "Y-m-d", allowInput: true };

beforeAll(() => {
  global.self = global.self || window;
  Object.defineProperty(window, "ontouchstart", { value: undefined, configurable: true, writable: true });
  loadScript("wwwroot/assets/vendor/libs/flatpickr/flatpickr.js");
  loadScript("wwwroot/assets/js/shared/diten-datefield.js");
  loadScript("wwwroot/assets/vendor/libs/popper/popper.js");
  loadScript("wwwroot/assets/vendor/js/bootstrap.js");
});

afterEach(() => {
  vi.restoreAllMocks();
  document.querySelectorAll(".flatpickr-calendar").forEach((cal) => cal.remove());
  document.body.innerHTML = "";
});

const bind = (opts, key) => {
  const el = document.getElementById("d");
  window.flatpickr(el, opts);
  const fp = el._flatpickr;
  window.DitenDateField.guardAgainstSilentMisparse(fp, key);
  const box = fp.altInput || fp.input;
  const type = (text, init = {}) => {
    if (document.activeElement !== box) { box.focus(); }
    box.value = text;
    box.dispatchEvent(new InputEvent("input", Object.assign({ bubbles: true }, init)));
  };
  const key_ = (k, init = {}) => {
    const event = new KeyboardEvent("keydown",
      Object.assign({ key: k, keyCode: k === "Escape" ? 27 : 13, bubbles: true, cancelable: true }, init));
    box.dispatchEvent(event);
    return event;
  };
  return { el, fp, box, type, escape: (init) => key_("Escape", init), enter: () => key_("Enter") };
};

const field = (opts, { key = "altFormat", value = "" } = {}) => {
  document.body.innerHTML = `<input id="d" value="${value}"><button id="other" type="button">x</button>`;
  return bind(opts, key);
};

const panelWithField = async (opts, key) => {
  document.body.innerHTML = `<div class="offcanvas offcanvas-end" id="panel" tabindex="-1">
    <input id="d" value="2026-10-09"><button id="other" type="button">x</button></div>`;
  const panel = document.getElementById("panel");
  window.bootstrap.Offcanvas.getOrCreateInstance(panel).show();
  await settle();
  expect(panel.classList.contains("show"), "the real Offcanvas did not open").toBe(true);
  return { panel, ...bind(opts, key) };
};

describe("WCN-FIX-07 · the caret counts the NORMALISED text before it", () => {
  test("a bidi mark in the middle, before the caret: '5‹RLM›/|3/2026' → caret 2", () => {
    const f = field(ALT);
    f.box.focus();
    f.box.value = "5\u200F/3/2026";
    f.box.setSelectionRange(3, 3);
    f.box.dispatchEvent(new InputEvent("input", { bubbles: true }));

    expect(f.box.value).toBe("5/3/2026");
    expect(f.box.selectionStart, "the caret kept its raw position").toBe(2);
  });

  test("the caret is placed only in the box the reader is in", () => {
    const f = field(ALT);
    document.getElementById("other").focus();
    const spy = vi.spyOn(f.box, "setSelectionRange");
    f.box.value = "٥/3/2026";
    f.box.dispatchEvent(new InputEvent("input", { bubbles: true }));

    expect(f.box.value).toBe("5/3/2026");
    expect(spy).not.toHaveBeenCalled();
  });
});

describe("WCN-FIX-07 · a PLAIN field: the restore's echo is not typing", () => {
  test("type a misparse → Escape (panel stays) → Escape again: nothing left to cancel, the panel closes", async () => {
    const f = await panelWithField(PLAIN, "dateFormat");
    f.box.focus();
    f.type("2026-02-31");
    f.escape();
    await settle();
    expect(f.panel.classList.contains("show")).toBe(true);
    expect(f.el.value).toBe("2026-10-09");

    f.escape();
    await settle();
    expect(f.panel.classList.contains("show"), "the restore's own echo was taken for typing and ate the 2nd Escape")
      .toBe(false);
  });
});

describe("WCN-FIX-07 · an Escape that cancels with the calendar CLOSED stops at the field too", () => {
  test("type a misparse → Enter (refused, calendar closed) → Escape: the date comes back and the panel stays open", async () => {
    const f = await panelWithField(ALT, "altFormat");
    f.box.focus();
    f.type("31/2/2026");
    f.enter();
    expect(f.box.classList.contains("is-invalid")).toBe(true);
    expect(f.fp.isOpen, "the calendar is open; this test is about a closed one").toBe(false);

    f.escape();
    await settle();
    expect(f.el.value).toBe("2026-10-09");
    expect(f.panel.classList.contains("show"), "the Escape that undid the refusal also closed the panel").toBe(true);
  });
});

describe("WCN-FIX-07 · IME", () => {
  test("a composition that ends with the text the run already holds opens no typing run (Escape passes on)", () => {
    const f = field(ALT, { value: "2026-10-09" });
    f.box.focus();
    const passed = [];
    document.addEventListener("keydown", (event) => { if (event.key === "Escape") { passed.push(event); } });
    f.box.dispatchEvent(new CompositionEvent("compositionend", { bubbles: true, data: "" }));
    f.escape();

    expect(passed.length, "an unchanged composition opened a typing run that swallowed Escape").toBe(1);
    expect(f.el.value).toBe("2026-10-09");
  });

  test("Escape INSIDE a composition is the IME's: the typing is not cancelled and the key is not stopped", () => {
    const f = field(ALT, { value: "2026-10-09" });
    f.box.focus();
    f.type("31/2", { isComposing: true });
    const passed = [];
    document.addEventListener("keydown", (event) => { if (event.key === "Escape") { passed.push(event); } });
    f.escape({ isComposing: true });

    expect(passed.length).toBe(1);
    expect(f.box.value, "the IME's Escape cancelled the reader's typing").toBe("31/2");
  });
});
