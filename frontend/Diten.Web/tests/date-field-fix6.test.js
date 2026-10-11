const { loadScript } = require("./load-script");

/*
 * WCN-FIX-06 — the typed-date guard's sixth round, with the REAL vendored flatpickr (4.6.13) and, for the side panel,
 * the REAL vendored Bootstrap (Offcanvas) — not a double.
 *
 *   1. Normalising Arabic-Indic / full-width digits keeps the caret where the reader put it, and never rewrites the box
 *      in the middle of an IME composition (it waits for compositionend).
 *   2. A field EMPTIED with Backspace, then a window switch: the deferred judgment ends the typing run, so typing "3"
 *      and Escape later cannot bring the deleted date back. And one Escape rule: a run (typed OR emptied) is cancelled.
 *   3. An Escape that cancelled the typing stops there: the side panel around the field stays open. A second Escape,
 *      with nothing left to cancel, closes the panel.
 *   ·  The restore announces `change` only when the dates really change; `input` either way.
 *   ·  The bidi marks the normaliser drops are the whole set (U+200E/200F, U+061C, U+202A–202E, U+2066–2069).
 *   ·  The component comparison reads Arabic-Indic digits itself (A5).
 */

const nextTask = () => new Promise((resolve) => setTimeout(resolve, 0));
const settle = (ms = 400) => new Promise((resolve) => setTimeout(resolve, ms));
const ALT = { dateFormat: "Y-m-d", altInput: true, altFormat: "j/n/Y", allowInput: true };
const PLAIN = { dateFormat: "Y-m-d", allowInput: true };

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

const field = (opts, { key = "altFormat", host = null, value = "" } = {}) => {
  if (!host) { document.body.innerHTML = `<input id="d" value="${value}"><button id="other" type="button">x</button>`; }
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
  const escape = () => {
    const event = new KeyboardEvent("keydown", { key: "Escape", keyCode: 27, bubbles: true, cancelable: true });
    box.dispatchEvent(event);
    return event;
  };
  return { el, fp, box, type, escape };
};

describe("WCN-FIX-06 · 1: normalising the digits keeps the caret and leaves an IME composition alone", () => {
  test("a mid-text correction with an Arabic-Indic digit keeps the caret right after it", () => {
    const f = field(ALT);
    f.box.focus();
    f.box.value = "5/٣/2026";
    f.box.setSelectionRange(3, 3); // the reader just typed ٣ in the middle
    f.box.dispatchEvent(new InputEvent("input", { bubbles: true }));

    expect(f.box.value).toBe("5/3/2026");
    expect(f.box.selectionStart, "the caret jumped to the end").toBe(3);
  });

  test("a bidi mark removed before the caret moves the caret back by exactly that mark", () => {
    const f = field(ALT);
    f.box.focus();
    f.box.value = "\u200F5/3";
    f.box.setSelectionRange(4, 4);
    f.box.dispatchEvent(new InputEvent("input", { bubbles: true }));
    expect(f.box.value).toBe("5/3");
    expect(f.box.selectionStart).toBe(3);
  });

  test("during an IME composition the box is not rewritten; at compositionend it is", () => {
    const f = field(ALT);
    f.box.focus();
    f.type("５", { isComposing: true });
    expect(f.box.value, "the box was rewritten in the middle of a composition").toBe("５");
    f.box.dispatchEvent(new CompositionEvent("compositionend", { bubbles: true, data: "５" }));
    expect(f.box.value).toBe("5");
  });
});

describe("WCN-FIX-06 · 2: a field emptied with Backspace", () => {
  test("emptied → window switch → back → type '3' → Escape: the deleted date does NOT come back", async () => {
    const f = field(ALT, { value: "2026-10-09" });
    f.box.focus();
    f.fp.open();
    f.type("");                                                    // Backspace to empty
    vi.spyOn(document, "hasFocus").mockReturnValue(false);         // the window switch
    f.box.dispatchEvent(new FocusEvent("blur", { relatedTarget: null }));
    await nextTask();
    vi.restoreAllMocks();
    f.type("3");
    f.escape();

    expect(f.el.value, "Escape brought back the date the reader deleted").toBe("");
  });

  test("one Escape rule: emptied with Backspace and nothing else → Escape restores the date (the typing run is cancelled)", () => {
    const f = field(ALT, { value: "2026-10-09" });
    f.box.focus();
    f.fp.open();
    f.type("");
    f.escape();
    expect(f.el.value).toBe("2026-10-09");
    expect(f.box.value).toBe("9/10/2026");
  });
});

describe("WCN-FIX-06: onOpen drops the run when the PAGE reset the box without an event (A2)", () => {
  test("pick → type '3' (closed) → the page clears the box text (resetDraft, no event) → open → Escape: the reset stays", () => {
    const f = field(PLAIN, { key: "dateFormat" });
    f.box.focus();
    f.fp.open();
    f.fp.jumpToDate("2026-10-01");
    [...f.fp.daysContainer.querySelectorAll(".flatpickr-day")]
      .find((n) => n.dateObj.getMonth() === 9 && n.dateObj.getDate() === 9)
      .dispatchEvent(new MouseEvent("click", { bubbles: true }));
    expect(f.fp.isOpen).toBe(false);
    f.type("3");
    f.box.value = "";                     // the page's reset: no event, no setDate
    f.box.dispatchEvent(new MouseEvent("click", { bubbles: true }));
    if (!f.fp.isOpen) { f.fp.open(); }
    f.escape();

    expect(f.box.value, "Escape restored a date over the page's own reset").toBe("");
  });
});

describe("WCN-FIX-06 · 3: an Escape that cancelled the typing does not close the side panel (real Bootstrap Offcanvas)", () => {
  beforeAll(() => {
    loadScript("wwwroot/assets/vendor/libs/popper/popper.js");
    loadScript("wwwroot/assets/vendor/js/bootstrap.js");
  });

  const panelWithField = async () => {
    document.body.innerHTML = `<div class="offcanvas offcanvas-end" id="panel" tabindex="-1">
      <input id="d" value="2026-10-09"><button id="other" type="button">x</button></div>`;
    const panel = document.getElementById("panel");
    const offcanvas = window.bootstrap.Offcanvas.getOrCreateInstance(panel);
    offcanvas.show();
    await settle();
    expect(panel.classList.contains("show"), "the real Offcanvas did not open").toBe(true);
    return { panel, ...field(ALT, { host: panel }) };
  };

  test("type a misparse → Escape: the typing is cancelled and the panel stays open; a second Escape closes it", async () => {
    const f = await panelWithField();
    f.box.focus();
    f.fp.open();
    f.type("31/2/2026");
    f.escape();
    await settle();

    expect(f.panel.classList.contains("show"), "the Escape that undid the typing also closed the panel").toBe(true);
    expect(f.el.value).toBe("2026-10-09");

    f.escape();
    await settle();
    expect(f.panel.classList.contains("show"), "with nothing to cancel, Escape should reach the panel").toBe(false);
  });

  test("control: with nothing typed, Escape closes the panel at once", async () => {
    const f = await panelWithField();
    f.box.focus();
    f.escape();
    await settle();
    expect(f.panel.classList.contains("show")).toBe(false);
  });
});

describe("WCN-FIX-06: what the restore announces", () => {
  test("empty → type → Escape (no date changes): `input` reaches the page, `change` does not", () => {
    const f = field(PLAIN, { key: "dateFormat" });
    const seen = [];
    ["input", "change"].forEach((type) => document.addEventListener(type, (e) => { if (e.target === f.el) { seen.push(type); } }));
    f.box.focus();
    f.fp.open();
    f.type("2026-02-3");
    seen.length = 0;
    f.escape();

    expect(seen).toContain("input");
    expect(seen).not.toContain("change");
    expect(f.el.value).toBe("");
    // and the echo is not mistaken for typing: nothing is left to judge
    document.getElementById("other").focus();
    expect(f.box.classList.contains("is-invalid")).toBe(false);
  });

  test("a restore that DOES change the dates announces `change` too", async () => {
    const f = field(PLAIN, { key: "dateFormat", value: "2026-10-09" });
    const seen = [];
    document.addEventListener("change", (e) => { if (e.target === f.el) { seen.push("change"); } });
    f.box.focus();
    f.fp.open();
    f.type("2026-02-31");
    f.box.dispatchEvent(new FocusEvent("blur", { relatedTarget: null })); // flatpickr commits 2026-03-03
    seen.length = 0;
    f.escape();
    expect(f.el.value).toBe("2026-10-09");
    expect(seen).toContain("change");
  });
});

describe("WCN-FIX-06: the normaliser's mark set and the comparison's own normalising", () => {
  test.each([["U+200E", "\u200E"], ["U+200F", "\u200F"], ["U+061C", "\u061C"], ["U+202A", "\u202A"], ["U+202B", "\u202B"],
    ["U+202E", "\u202E"], ["U+2066", "\u2066"], ["U+2069", "\u2069"]])("%s is removed", (_label, mark) => {
    expect(window.DitenDateField.normalizeTyped(`5${mark}/3/2026`)).toBe("5/3/2026");
  });

  test("A5: typedReadsBack reads Arabic-Indic digits itself (not only after the box was normalised)", () => {
    expect(window.DitenDateField.typedReadsBack("٥/٣/٢٠٢٦", new Date(2026, 2, 5), "j/n/Y")).toBe(true);
    expect(window.DitenDateField.typedReadsBack("٣١/٢/٢٠٢٦", new Date(2026, 2, 3), "j/n/Y")).toBe(false);
  });
});
