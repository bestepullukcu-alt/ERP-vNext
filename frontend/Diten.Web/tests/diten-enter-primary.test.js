const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * WP-UX-COMMON-01 (BL-586) — Enter runs a side panel's primary action, and only where that is what the reader means.
 *
 * Measured through the real shared script against Bootstrap-shaped markup (`.offcanvas.show`), plus a scan of EVERY
 * offcanvas in Views/ so a new panel that the rule cannot read is a red test, not a silent dead key.
 */

const VIEWS = path.resolve(__dirname, "..", "Views");

beforeAll(() => {
  loadScript("wwwroot/assets/js/shared/diten-enter-primary.js");
});

afterEach(() => {
  document.body.innerHTML = "";
});

const panel = (inner, { open = true } = {}) => {
  document.body.innerHTML = `<div class="offcanvas offcanvas-end${open ? " show" : ""}" id="p">
    <div class="offcanvas-body">${inner}</div></div>`;
  const clicks = [];
  document.querySelectorAll("button").forEach((b) => b.addEventListener("click", () => clicks.push(b.id)));
  return clicks;
};

const press = (target, init = {}) => {
  const event = new KeyboardEvent("keydown", Object.assign({ key: "Enter", bubbles: true, cancelable: true }, init));
  target.dispatchEvent(event);
  return event;
};

const SAVE = '<button type="button" id="save" class="btn btn-primary">Save</button>';
const CANCEL = '<button type="button" id="cancel" class="btn btn-label-secondary">Cancel</button>';

describe("BL-586: Enter in a side panel runs its primary action", () => {
  test("Enter in a single-line field clicks the panel's one .btn-primary", () => {
    const clicks = panel(`<input id="name" type="text">${CANCEL}${SAVE}`);
    const event = press(document.getElementById("name"));
    expect(clicks).toEqual(["save"]);
    expect(event.defaultPrevented).toBe(true);
  });

  test("a data-dt-primary mark wins over .btn-primary (End-relationship panels save with btn-warning)", () => {
    const clicks = panel(`<input id="d" type="date"><button type="button" id="end" class="btn btn-warning" data-dt-primary>End</button>
      <button type="button" id="other" class="btn btn-primary">Other</button>`);
    press(document.getElementById("d"));
    expect(clicks).toEqual(["end"]);
  });

  test("of two marked buttons that take turns, the one shown now is primary (VisitExecution submit / amend)", () => {
    const clicks = panel(`<input id="n" type="text">
      <button type="button" id="submit" class="btn btn-success d-none" data-dt-primary>Submit</button>
      <button type="button" id="amend" class="btn btn-outline-warning" data-dt-primary>Amend</button>`);
    press(document.getElementById("n"));
    expect(clicks).toEqual(["amend"]);
  });

  test("two unmarked .btn-primary: Enter is left alone (the panel needs a mark)", () => {
    const clicks = panel(`<input id="n" type="text">${SAVE}<button type="button" id="save2" class="btn btn-primary">Again</button>`);
    const event = press(document.getElementById("n"));
    expect(clicks).toEqual([]);
    expect(event.defaultPrevented).toBe(false);
  });

  test("a panel with no form fields has no Enter-primary (a quick view's single 'Edit' button is not a save)", () => {
    panel('<button type="button" id="edit" class="btn btn-primary">Edit</button>');
    expect(window.DitenEnterPrimary.primaryOf(document.getElementById("p"))).toBeNull();
  });

  test("a closed panel is not answered", () => {
    const clicks = panel(`<input id="n" type="text">${SAVE}`, { open: false });
    press(document.getElementById("n"));
    expect(clicks).toEqual([]);
  });
});

describe("BL-586: where Enter is NOT save", () => {
  test("textarea: Enter is a new line — nothing is clicked and the key is not taken", () => {
    const clicks = panel(`<textarea id="desc"></textarea>${SAVE}`);
    const event = press(document.getElementById("desc"));
    expect(clicks).toEqual([]);
    expect(event.defaultPrevented).toBe(false);
  });

  test("textarea: Ctrl+Enter and Cmd+Enter save", () => {
    let now = 1000000;
    vi.spyOn(Date, "now").mockImplementation(() => now);
    const clicks = panel(`<textarea id="desc"></textarea>${SAVE}`);
    press(document.getElementById("desc"), { ctrlKey: true });
    now += 5000; // past the one-press-one-action window
    press(document.getElementById("desc"), { metaKey: true });
    expect(clicks).toEqual(["save", "save"]);
    vi.restoreAllMocks();
  });

  test("a disabled primary button: nothing is clicked (no double submission), and no native submit either", () => {
    const clicks = panel(`<input id="n" type="text"><button type="button" id="save" class="btn btn-primary" disabled>Save</button>`);
    const event = press(document.getElementById("n"));
    expect(clicks).toEqual([]);
    expect(event.defaultPrevented).toBe(true);
  });

  test("an aria-disabled / .disabled primary is disabled too", () => {
    const clicks = panel(`<input id="n" type="text"><button type="button" id="save" class="btn btn-primary disabled" aria-disabled="true">Save</button>`);
    press(document.getElementById("n"));
    expect(clicks).toEqual([]);
  });

  test("IME composition: Enter confirms the composed text, never saves", () => {
    const clicks = panel(`<input id="n" type="text">${SAVE}`);
    press(document.getElementById("n"), { isComposing: true });
    press(document.getElementById("n"), { keyCode: 229 });
    expect(clicks).toEqual([]);
  });

  test("an open select2 / date picker / dropdown: Enter picks the entry, never saves", () => {
    ["select2-container select2-container--open", "flatpickr-calendar open", "dropdown-menu show"].forEach((cls) => {
      const clicks = panel(`<input id="n" type="text">${SAVE}<div class="${cls}"></div>`);
      press(document.getElementById("n"));
      expect(clicks, cls).toEqual([]);
    });
  });

  test("a field whose own list is expanded (aria-expanded) keeps Enter", () => {
    const clicks = panel(`<input id="n" type="text" aria-expanded="true">${SAVE}`);
    press(document.getElementById("n"));
    expect(clicks).toEqual([]);
  });

  test("a held key repeats nothing", () => {
    const clicks = panel(`<input id="n" type="text">${SAVE}`);
    press(document.getElementById("n"), { repeat: true });
    expect(clicks).toEqual([]);
  });

  test("checkbox, button and select keep their own Enter", () => {
    const clicks = panel(`<input id="c" type="checkbox"><select id="s"><option>1</option></select>${CANCEL}${SAVE}`);
    press(document.getElementById("c"));
    press(document.getElementById("s"));
    press(document.getElementById("cancel"));
    expect(clicks).toEqual([]);
  });

  test("a page that gives Enter its own meaning (handler on document calls preventDefault) is respected", () => {
    const clicks = panel(`<input id="sub" type="text" data-wcn-subtask-input>${SAVE}`);
    const own = (event) => { if (event.target.id === "sub") { event.preventDefault(); } };
    document.addEventListener("keydown", own);
    try {
      press(document.getElementById("sub"));
    } finally {
      document.removeEventListener("keydown", own);
    }
    expect(clicks).toEqual([]);
  });
});

describe("UX-COMMON-FIX1: consequential actions, one press one action, pickers read before their own handlers", () => {
  const CHORD = '<button type="button" id="publish" class="btn btn-primary" data-dt-primary="chord">Publish</button>';

  test("a chord-marked primary: bare Enter does nothing (and no native submit); Ctrl+Enter clicks it once", () => {
    const clicks = panel(`<input id="n" type="text">${CHORD}`);
    const bare = press(document.getElementById("n"));
    expect(clicks, "a bare Enter published").toEqual([]);
    expect(bare.defaultPrevented, "a bare Enter fell through to a native form submission").toBe(true);
    press(document.getElementById("n"), { ctrlKey: true });
    expect(clicks).toEqual(["publish"]);
  });

  test("a chord-marked primary: Cmd+Enter (macOS) clicks it too", () => {
    const clicks = panel(`<input id="n" type="text">${CHORD}`);
    press(document.getElementById("n"), { metaKey: true });
    expect(clicks).toEqual(["publish"]);
  });

  test("a field with a datalist keeps Enter (it may commit a suggestion)", () => {
    const clicks = panel(`<input id="n" type="text" list="opts"><datalist id="opts"><option value="a"></option></datalist>${SAVE}`);
    const event = press(document.getElementById("n"));
    expect(clicks).toEqual([]);
    expect(event.defaultPrevented).toBe(false);
  });

  test("two quick Enters are ONE click (no double POST where the page has no busy lock)", () => {
    const clicks = panel(`<input id="n" type="text">${SAVE}`);
    press(document.getElementById("n"));
    press(document.getElementById("n"));
    expect(clicks).toEqual(["save"]);
  });

  test("an open allowInput flatpickr: Enter commits the date and closes the calendar — the panel does NOT save", () => {
    global.self = global.self || window;
    loadScript("wwwroot/assets/vendor/libs/flatpickr/flatpickr.js");
    const clicks = panel(`<input id="due" type="text">${SAVE}`);
    const due = document.getElementById("due");
    const fp = window.flatpickr(due, { dateFormat: "Y-m-d", allowInput: true });
    due.focus();
    fp.open();
    expect(fp.isOpen).toBe(true);
    due.value = "2026-10-20";
    due.dispatchEvent(new Event("input", { bubbles: true }));
    press(due, { keyCode: 13 }); // flatpickr reads keyCode

    expect(fp.isOpen, "flatpickr did not handle its Enter").toBe(false);
    expect(due.value).toBe("2026-10-20");
    expect(clicks, "the panel saved while the reader was choosing a date").toEqual([]);
    fp.destroy();
  });

  test("an unmarked .btn-primary that is hidden right now (d-none, [hidden], an inactive tab) is not the primary", () => {
    const clicks = panel(`<input id="n" type="text">
      <div class="d-none"><button type="button" id="hiddenSave" class="btn btn-primary">Save</button></div>
      <div hidden><button type="button" id="hiddenSave2" class="btn btn-primary">Save</button></div>
      <button type="button" id="save" class="btn btn-primary">Save</button>`);
    press(document.getElementById("n"));
    expect(clicks).toEqual(["save"]);
  });

  test("the only .btn-primary hidden in an inactive step: nothing is clicked", () => {
    const clicks = panel(`<input id="n" type="text"><div class="d-none"><button type="button" id="save" class="btn btn-primary">Save</button></div>`);
    press(document.getElementById("n"));
    expect(clicks).toEqual([]);
  });

  test.each(["email", "number", "search", "tel", "url", "password", "date", "time"])(
    "U3: Enter in an <input type=%s> runs the primary", (type) => {
      const clicks = panel(`<input id="n" type="${type}">${SAVE}`);
      press(document.getElementById("n"));
      expect(clicks).toEqual(["save"]);
    });

  test("U4a: a primary disabled ONLY by the .disabled class is not clicked", () => {
    const clicks = panel(`<input id="n" type="text"><button type="button" id="save" class="btn btn-primary disabled">Save</button>`);
    press(document.getElementById("n"));
    expect(clicks).toEqual([]);
  });

  test("U4b: a primary disabled ONLY by aria-disabled is not clicked", () => {
    const clicks = panel(`<input id="n" type="text"><button type="button" id="save" class="btn btn-primary" aria-disabled="true">Save</button>`);
    press(document.getElementById("n"));
    expect(clicks).toEqual([]);
  });

  test.each([
    ["the autocomplete panel (.aa-Panel)", '<div class="aa-Panel"></div>'],
    ["a jQuery UI autocomplete list", '<ul class="ui-autocomplete"></ul>']
  ])("an open %s keeps Enter", (_label, markup) => {
    const clicks = panel(`<input id="n" type="text">${SAVE}${markup}`);
    press(document.getElementById("n"));
    expect(clicks).toEqual([]);
  });

  test("a closed jQuery UI autocomplete (display: none) does not hold Enter", () => {
    const clicks = panel(`<input id="n" type="text">${SAVE}<ul class="ui-autocomplete" style="display: none"></ul>`);
    press(document.getElementById("n"));
    expect(clicks).toEqual(["save"]);
  });

  test("a panel still sliding in (.offcanvas.showing) is answered", () => {
    document.body.innerHTML = `<div class="offcanvas offcanvas-end showing" id="p"><input id="n" type="text">${SAVE}</div>`;
    const clicks = [];
    document.getElementById("save").addEventListener("click", () => clicks.push("save"));
    press(document.getElementById("n"));
    expect(clicks).toEqual(["save"]);
  });
});

describe("UX-COMMON-FIX2 (R1): no native form submission behind the rule's back", () => {
  // The tenant subscription panel's shape: the primary is a `type="submit" form=` button whose handler is the form's
  // submit event; Enter on its "cancel at period end" checkbox used to fall to the browser's implicit submission.
  const SUBMIT_CHORD = '<button type="submit" id="act" class="btn btn-primary" data-dt-primary="chord">Save</button>';
  const SUBMIT_ENTER = '<button type="submit" id="act" class="btn btn-primary" data-dt-primary="enter">Save</button>';

  test.each([
    ["a checkbox", '<input id="t" type="checkbox">'],
    ["a closed select", '<select id="t"><option>1</option></select>'],
    ["a datalist field", '<input id="t" type="text" list="o"><datalist id="o"><option value="a"></option></datalist>']
  ])("chord panel: Enter on %s is defaultPrevented and clicks nothing", (_label, field) => {
    const clicks = panel(`${field}${SUBMIT_CHORD}`);
    const event = press(document.getElementById("t"));
    expect(event.defaultPrevented, "the browser would submit the form natively").toBe(true);
    expect(clicks).toEqual([]);
  });

  test("chord panel: a held Enter and Alt+Enter are defaultPrevented and click nothing", () => {
    const clicks = panel(`<input id="t" type="text">${SUBMIT_CHORD}`);
    expect(press(document.getElementById("t"), { repeat: true }).defaultPrevented).toBe(true);
    expect(press(document.getElementById("t"), { altKey: true }).defaultPrevented).toBe(true);
    expect(clicks).toEqual([]);
  });

  test("a plain submit panel: a held Enter is ONE action (the repeats are not native submissions)", () => {
    const clicks = panel(`<input id="t" type="text">${SUBMIT_ENTER}`);
    const first = press(document.getElementById("t"));
    const repeats = [1, 2, 3].map(() => press(document.getElementById("t"), { repeat: true }));
    expect(first.defaultPrevented).toBe(true);
    expect(repeats.every((e) => e.defaultPrevented), "a repeat fell through to a native submission").toBe(true);
    expect(clicks).toEqual(["act"]);
  });

  test("a <button> with no type IS a submit button (its implicit submission is held too)", () => {
    const clicks = panel('<input id="t" type="checkbox"><button id="act" class="btn btn-primary" data-dt-primary="enter">Save</button>');
    expect(press(document.getElementById("t")).defaultPrevented).toBe(true);
    expect(clicks).toEqual([]);
  });

  test("IME composition and a textarea are never taken, even in a chord panel", () => {
    panel(`<input id="t" type="text"><textarea id="x"></textarea>${SUBMIT_CHORD}`);
    expect(press(document.getElementById("t"), { isComposing: true }).defaultPrevented).toBe(false);
    expect(press(document.getElementById("x")).defaultPrevented).toBe(false);
  });

  test("a plain type=button panel: a checkbox's Enter is left alone (no implicit submission to stop)", () => {
    const clicks = panel(`<input id="t" type="checkbox">${SAVE}`);
    expect(press(document.getElementById("t")).defaultPrevented).toBe(false);
    expect(clicks).toEqual([]);
  });
});

describe("UX-COMMON-FIX2: the cases a sabotage must turn red (E1–E4, .hiding)", () => {
  test("E1: a CLOSED date picker without allowInput — Enter opens its calendar, the panel does NOT save", () => {
    global.self = global.self || window;
    loadScript("wwwroot/assets/vendor/libs/flatpickr/flatpickr.js");
    const clicks = panel(`<input id="due" type="text">${SAVE}`);
    const due = document.getElementById("due");
    const fp = window.flatpickr(due, { dateFormat: "Y-m-d" });
    due.focus(); // focusing opens it (clickOpens)…
    fp.close();  // …so close it: the case is Enter on a CLOSED picker
    expect(fp.isOpen).toBe(false);
    press(due, { keyCode: 13 });
    expect(fp.isOpen, "flatpickr did not open on Enter").toBe(true);
    expect(clicks, "the panel saved while the calendar opened").toEqual([]);
    fp.destroy();
  });

  test("E2: Alt+Enter in a single-line field is not save", () => {
    const clicks = panel(`<input id="n" type="text">${SAVE}`);
    press(document.getElementById("n"), { altKey: true });
    expect(clicks).toEqual([]);
  });

  test.each(["datetime-local", "month", "week"])("E3: Enter in an <input type=%s> runs the primary", (type) => {
    const clicks = panel(`<input id="n" type="${type}">${SAVE}`);
    press(document.getElementById("n"));
    expect(clicks).toEqual(["save"]);
  });

  test("E4: one press one action holds for 800 ms, measured on a mocked clock", () => {
    let now = 5000000;
    vi.spyOn(Date, "now").mockImplementation(() => now);
    try {
      const clicks = panel(`<input id="n" type="text">${SAVE}`);
      press(document.getElementById("n"));
      now += 500;
      press(document.getElementById("n"));
      expect(clicks, "a second Enter 500 ms later was a second save").toEqual(["save"]);
      now += 400; // 900 ms after the first
      press(document.getElementById("n"));
      expect(clicks).toEqual(["save", "save"]);
    } finally {
      vi.restoreAllMocks();
    }
  });

  test("a panel sliding OUT (.offcanvas.show.hiding) is not answered", () => {
    document.body.innerHTML = `<div class="offcanvas offcanvas-end show hiding" id="p"><input id="n" type="text">${SAVE}</div>`;
    const clicks = [];
    document.getElementById("save").addEventListener("click", () => clicks.push("save"));
    press(document.getElementById("n"));
    expect(clicks).toEqual([]);
  });
});

describe("UX-COMMON-FIX2 (R3): a chord button tells the reader", () => {
  test("on panel open, a chord button gets aria-keyshortcuts and the help panel's localized title", () => {
    document.body.innerHTML = `<div id="diten-help-panel" data-diten-chord-title="Ctrl+Entrée"></div>
      <div class="offcanvas offcanvas-end" id="p"><input type="text">
        <button type="button" id="go" class="btn btn-primary" data-dt-primary="chord">Publish</button>
        <button type="button" id="own" class="btn btn-secondary" data-dt-primary="chord" title="Mine">Other</button></div>`;
    document.getElementById("p").dispatchEvent(new Event("show.bs.offcanvas", { bubbles: true }));
    const go = document.getElementById("go");
    expect(go.getAttribute("aria-keyshortcuts")).toBe("Control+Enter Meta+Enter");
    expect(go.getAttribute("title")).toBe("Ctrl+Entrée");
    expect(document.getElementById("own").getAttribute("title"), "a title the page wrote was replaced").toBe("Mine");
  });

  test("a mark set by a page script before the panel opens is decorated too (Tenants' per-action mark)", () => {
    document.body.innerHTML = `<div class="offcanvas offcanvas-end" id="p"><input type="text">
      <button type="submit" id="act" class="btn btn-primary" data-dt-primary="enter">Save</button></div>`;
    document.getElementById("act").setAttribute("data-dt-primary", "chord");
    document.getElementById("p").dispatchEvent(new Event("show.bs.offcanvas", { bubbles: true }));
    expect(document.getElementById("act").getAttribute("aria-keyshortcuts")).toBe("Control+Enter Meta+Enter");
  });
});

describe("UX-COMMON-FIX2 (R5): the tenant subscription panel's Enter is decided per action", () => {
  beforeAll(() => {
    loadScript("wwwroot/assets/js/Platform/Tenants/details.js");
  });

  test.each([
    ["cancel", "chord"], ["suspend", "chord"],
    ["assign", "enter"], ["activate", "enter"], ["renew", "enter"]
  ])("%s → %s", (action, mark) => {
    const details = require("vm").runInThisContext("TenantDetails");
    expect(details.subscriptionActionPrimary(action)).toBe(mark);
  });

  test("opening the panel sets that mark on its primary button (and the view's static mark is the safe chord)", () => {
    const source = fs.readFileSync(path.resolve(__dirname, "..", "wwwroot/assets/js/Platform/Tenants/details.js"), "utf8");
    expect(source).toMatch(/getElementById\('btnSubmitSubscriptionAction'\)\?\.setAttribute\('data-dt-primary', subscriptionActionPrimary\(action\)\)/);
    const view = fs.readFileSync(path.join(VIEWS, "Platform/Tenants/Details.cshtml"), "utf8");
    expect(view).toMatch(/id="btnSubmitSubscriptionAction" data-dt-primary="chord"/);
  });
});

/*
 * UX-COMMON-FIX2 (R4, CT decision) — every offcanvas in Views/ that carries form fields MARKS its primary explicitly:
 * `data-dt-primary="enter"` or `data-dt-primary="chord"`. An unmarked panel is red; the exceptions are named here with
 * their reason, and the list only shrinks.
 */
const UNMARKED_PANELS = {
  // Filter drawers on the FROZEN _Layout: no save ("Apply" is `btn-filter-apply`); the Enter module is not loaded there.
  "DeliveryExecutionManagement/Initiatives.cshtml": "filter drawer, no save, FROZEN _Layout",
  "EnterpriseStrategyBusinessPerformance/Initiatives.cshtml": "filter drawer, no save, FROZEN _Layout",
  "EnterpriseStrategyBusinessPerformance/Objectives.cshtml": "filter drawer, no save, FROZEN _Layout",
  // PPM is outside WP-UX-COMMON-01 (CT: "PPM'ye dokunma"): its two panels keep the FIX1 rule (one unmarked .btn-primary).
  "PPM/Initiatives/_CreateEditOffcanvas.cshtml": "PPM, not touched by this WP",
  "PPM/Shared/_CreateEditOffcanvas.cshtml": "PPM, not touched by this WP"
};

/*
 * WP-LANE-UX-GUARDS-01 (CT decision, ONE-TIME) — screens that arrived with the main-sync of 2026-10-12, owned by other
 * teams: XMC-001, their views are not ours to mark. Named with the owner and the reason; the mark is the owner's work
 * and is sent to them. This is not "the list grows": the latch test below pins every list's size, so the next unmarked
 * panel is red again, whoever adds it and wherever.
 */
const SCE = "SCE / MVP-6 (gmg-10)";
const CRM = "CRM";
const MAIN_SYNC_WHY = "main-sync (2026-10-12) ile geldi; sahibi başka ekip; işaret sahibin işi";
const MAIN_SYNC_UNMARKED = {
  "CRM/CyclePeriods/_PeriodPanel.cshtml": { owner: CRM, why: MAIN_SYNC_WHY },
  "CRM/KnowledgePaths/Workspace.cshtml": { owner: CRM, why: MAIN_SYNC_WHY },
  "CRM/VisitPlanning/_DoctorPanel.cshtml": { owner: CRM, why: MAIN_SYNC_WHY },
  "CRM/VisitPlanning/_NewPlanDrawer.cshtml": { owner: CRM, why: MAIN_SYNC_WHY },
  "SupplyChain/Carriers/_ChangeStatusOffcanvas.cshtml": { owner: SCE, why: MAIN_SYNC_WHY },
  "SupplyChain/Carriers/_CreateEditOffcanvas.cshtml": { owner: SCE, why: MAIN_SYNC_WHY },
  "SupplyChain/Claims/Index.cshtml": { owner: SCE, why: MAIN_SYNC_WHY },
  "SupplyChain/Claims/_CreateEditOffcanvas.cshtml": { owner: SCE, why: MAIN_SYNC_WHY },
  "SupplyChain/Loads/_CreateEditOffcanvas.cshtml": { owner: SCE, why: MAIN_SYNC_WHY },
  "SupplyChain/Returns/Index.cshtml": { owner: SCE, why: MAIN_SYNC_WHY },
  "SupplyChain/Returns/_CreateEditOffcanvas.cshtml": { owner: SCE, why: MAIN_SYNC_WHY },
  "SupplyChain/Shipments/_PodOffcanvas.cshtml": { owner: SCE, why: MAIN_SYNC_WHY },
  "SupplyChain/Shipments/_TransitionOffcanvas.cshtml": { owner: SCE, why: MAIN_SYNC_WHY }
};
// The same main-sync screens: a consequential label that is not chord-marked — "chord olmalı, sahibine iletilecek".
// Keyed by file + label (no line number, so an unrelated edit above the button does not move the key).
const MAIN_SYNC_CONSEQUENTIAL = {
  'SupplyChain/Carriers/_ChangeStatusOffcanvas.cshtml @Localizer["Confirm Status Change"]': { owner: SCE, why: `${MAIN_SYNC_WHY}; chord olmalı, sahibine iletilecek` },
  'SupplyChain/Claims/Index.cshtml @Localizer["Submit Transition"]': { owner: SCE, why: `${MAIN_SYNC_WHY}; chord olmalı, sahibine iletilecek` },
  'SupplyChain/Returns/Index.cshtml @Localizer["Submit Transition"]': { owner: SCE, why: `${MAIN_SYNC_WHY}; chord olmalı, sahibine iletilecek` },
  'SupplyChain/Returns/_CreateEditOffcanvas.cshtml @Localizer["Create Return"]': { owner: SCE, why: `${MAIN_SYNC_WHY}; chord olmalı, sahibine iletilecek` },
  'SupplyChain/Shipments/_PodOffcanvas.cshtml @Localizer["Confirm Action"]': { owner: SCE, why: `${MAIN_SYNC_WHY}; chord olmalı, sahibine iletilecek` },
  'SupplyChain/Shipments/_TransitionOffcanvas.cshtml @Localizer["Confirm Action"]': { owner: SCE, why: `${MAIN_SYNC_WHY}; chord olmalı, sahibine iletilecek` }
};
// The latch: every list's size, pinned. A row added to any of them — named or not — is red here.
const LIST_SIZES = { UNMARKED_PANELS: 5, NOT_CONSEQUENTIAL: 2, MAIN_SYNC_UNMARKED: 13, MAIN_SYNC_CONSEQUENTIAL: 6 };
// "file:line label </button>" → "file label"
const findingKey = (finding) => finding.replace(/^([^:]+):\d+ /, "$1 ").replace(/\s*<\/button>\s*$/, "").trim();

// A FROZEN exception must really be on the FROZEN _Layout: the view itself, or EVERY view that renders the partial.
const FROZEN_EXCEPTIONS = [
  "DeliveryExecutionManagement/Initiatives.cshtml",
  "EnterpriseStrategyBusinessPerformance/Initiatives.cshtml",
  "EnterpriseStrategyBusinessPerformance/Objectives.cshtml",
  "EnterpriseStrategyBusinessPerformance/Components/Planning/_PlanningCycleOffcanvas.cshtml",
  "EnterpriseStrategyBusinessPerformance/Components/Planning/_StrategyPeriodOffcanvas.cshtml"
];

const cshtmlFiles = (dir) => fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
  const full = path.join(dir, entry.name);
  if (entry.isDirectory()) { return cshtmlFiles(full); }
  return entry.name.endsWith(".cshtml") ? [full] : [];
});

const relative = (file) => path.relative(VIEWS, file).split(path.sep).join("/");

// The same FROZEN derivation as page-help-ledger.test.js: a non-partial view that falls to the frozen _Layout.
const frozenViews = (() => {
  return cshtmlFiles(VIEWS).filter((file) => !path.basename(file).startsWith("_")).filter((file) => {
    const source = fs.readFileSync(file, "utf8");
    const layout = /Layout\s*=\s*(null|"([^"]+)")/.exec(source);
    if (layout) { return layout[2] === "_Layout" || layout[2] === "~/Views/Shared/_Layout.cshtml"; }
    return !file.startsWith(path.join(VIEWS, "Platform") + path.sep);
  }).map(relative);
})();

// Each `<div class="… offcanvas …">` block, closed by balancing its <div>s.
const offcanvasBlocks = () => cshtmlFiles(VIEWS).flatMap((file) => {
  const source = fs.readFileSync(file, "utf8");
  const open = /<div\b[^>]*\bclass="([^"]*)"[^>]*>/g;
  const blocks = [];
  let match;
  while ((match = open.exec(source)) !== null) {
    if (!/(^|\s)offcanvas(\s|$)/.test(match[1])) { continue; }
    const tag = /<div\b|<\/div>/g;
    tag.lastIndex = match.index;
    let depth = 0;
    let end = source.length;
    let t;
    while ((t = tag.exec(source)) !== null) {
      depth += t[0] === "</div>" ? -1 : 1;
      if (depth === 0) { end = tag.lastIndex; break; }
    }
    blocks.push({ file: relative(file), line: source.slice(0, match.index).split("\n").length, body: source.slice(match.index, end) });
  }
  return blocks;
});

/*
 * The second check: a primary whose label says it cannot be taken back must not be `enter`-marked. The words were
 * measured from the panels' primary labels (resource key and English text), widened in FIX2. Generic verbs that also
 * name reversible work (Run, Apply, Upload, Request, Record, Post) stay out — those panels carry `chord` explicitly.
 */
const CONSEQUENTIAL = /\b(Publish|Approve|Reject|Submit|Send|Confirm|End|Terminate|Retire|Deactivate|Delete|Delegate|Withdraw|Cancel|Suspend|Amend|Escalat\w*|Transition|Evidence|Reserve|Snapshot|Revoke|Reset|Invite|Assign|Disable|Remove|Archive|Release|Close|Finalize|Sign|Decline|Return|Activate)/;
const splitWords = (text) => text.replace(/([a-z])([A-Z])/g, "$1 $2");
// A consequential-sounding label on a reversible save, by name and reason. The list only shrinks.
const NOT_CONSEQUENTIAL = {
  "EnterpriseStrategyBusinessPerformance/Components/Planning/_PlanningCycleOffcanvas.cshtml":
    "hard-coded English 'Submit' on a plain create / update save of a planning cycle; FROZEN _Layout (no Enter rule there)",
  "EnterpriseStrategyBusinessPerformance/Components/Planning/_StrategyPeriodOffcanvas.cshtml":
    "hard-coded English 'Submit' on a plain create / update save of a strategy period; FROZEN _Layout"
};
const primaryButtons = (body) => {
  const marked = [...body.matchAll(/<button\b[^>]*data-dt-primary[^>]*>([\s\S]*?)<\/button>/g)];
  if (marked.length) { return marked.map((m) => m[0]); }
  const primaries = [...body.matchAll(/<button\b[^>]*class="[^"]*\bbtn-primary\b[^"]*"[^>]*>([\s\S]*?)<\/button>/g)];
  return primaries.length === 1 ? [primaries[0][0]] : [];
};
// The finding for one panel: an `enter`-marked or unmarked primary whose label is consequential.
const consequentialUnchorded = (block) => primaryButtons(block.body).flatMap((button) => {
  const label = splitWords(button.replace(/<button\b[^>]*>/, ""));
  return CONSEQUENTIAL.test(label) && !/data-dt-primary="chord"/.test(button)
    ? [`${block.file}:${block.line} ${label.replace(/\s+/g, " ").trim()}`] : [];
});

describe("BL-586 / UX-COMMON-FIX2: every side panel with fields marks its primary action", () => {
  const blocks = offcanvasBlocks();
  const withFields = blocks.filter((b) => /<input\b(?![^>]*type="hidden")|<textarea\b|<select\b/.test(b.body));
  const marked = (body) => /data-dt-primary="(enter|chord)"/.test(body);

  test("the scan reaches the panels (guards against a vacuous pass)", () => {
    expect(withFields.length).toBeGreaterThan(50);
    expect(withFields.map((b) => b.file)).toEqual(expect.arrayContaining([
      "Governance/Users/_CreateEditOffcanvas.cshtml",
      "Governance/Roles/_CreateEditOffcanvas.cshtml",
      "CRM/Accounts/_RelationshipEndOffcanvas.cshtml"
    ]));
  });

  test("R4: a panel with no explicit enter / chord mark is listed with its reason; the list only shrinks", () => {
    const unmarked = [...new Set(withFields.filter((b) => !marked(b.body)).map((b) => b.file))].sort();
    expect(unmarked).toEqual([...Object.keys(UNMARKED_PANELS), ...Object.keys(MAIN_SYNC_UNMARKED)].sort());
  });

  test("WP-LANE-UX-GUARDS-01: the latch — no exception list grows, and every main-sync row names its owner and reason", () => {
    const lists = { UNMARKED_PANELS, NOT_CONSEQUENTIAL, MAIN_SYNC_UNMARKED, MAIN_SYNC_CONSEQUENTIAL };
    Object.entries(LIST_SIZES).forEach(([name, size]) => expect(Object.keys(lists[name]).length, name).toBe(size));
    const unnamed = Object.entries({ ...MAIN_SYNC_UNMARKED, ...MAIN_SYNC_CONSEQUENTIAL })
      .filter(([, row]) => ![SCE, CRM].includes(row && row.owner) || !/^main-sync \(2026-10-12\)/.test((row && row.why) || ""))
      .map(([key]) => key);
    expect(unnamed, "a main-sync row without its owner or reason").toEqual([]);
  });

  test("every mark is one of the two values", () => {
    const odd = withFields.flatMap((b) => [...b.body.matchAll(/data-dt-primary(?:="([^"]*)")?/g)]
      .filter((m) => !["enter", "chord"].includes(m[1])).map((m) => `${b.file}:${b.line} ${m[0]}`));
    expect(odd).toEqual([]);
  });

  test("a FROZEN exception is really on the FROZEN _Layout (the view, or every view rendering the partial)", () => {
    const notFrozen = FROZEN_EXCEPTIONS.filter((file) => {
      if (!path.basename(file).startsWith("_")) { return !frozenViews.includes(file); }
      const name = path.basename(file, ".cshtml");
      const hosts = cshtmlFiles(VIEWS).map(relative).filter((view) => view !== file
        && new RegExp(`["/]${name}["]`).test(fs.readFileSync(path.join(VIEWS, view), "utf8")));
      return hosts.length === 0 || hosts.some((view) => !frozenViews.includes(view));
    });
    expect(notFrozen).toEqual([]);
    // Every FROZEN-reasoned exception above is in that list (a stale or new one cannot hide behind "FROZEN").
    const claimed = [...Object.entries(UNMARKED_PANELS), ...Object.entries(NOT_CONSEQUENTIAL)]
      .filter(([, reason]) => /FROZEN/.test(reason)).map(([file]) => file);
    expect(claimed.filter((file) => !FROZEN_EXCEPTIONS.includes(file))).toEqual([]);
  });

  test("a primary whose label is consequential is chord-marked, never enter (or named as an exception)", () => {
    const findings = withFields.filter((b) => !NOT_CONSEQUENTIAL[b.file]).flatMap(consequentialUnchorded);
    expect(findings.filter((finding) => !MAIN_SYNC_CONSEQUENTIAL[findingKey(finding)])).toEqual([]);
    // …and a main-sync row its owner fixed leaves the list (the list only shrinks).
    expect(Object.keys(MAIN_SYNC_CONSEQUENTIAL).filter((key) => !findings.map(findingKey).includes(key))).toEqual([]);
  });

  test("positive control: the word check flags an enter-marked consequential label (every listed word)", () => {
    const words = CONSEQUENTIAL.source.replace(/^\\b\(|\)$/g, "").split("|").map((w) => w.replace("\\w*", "ion"));
    words.forEach((word) => {
      const block = { file: "control.cshtml", line: 1, body: `<button type="button" class="btn btn-primary" data-dt-primary="enter">${word}</button>` };
      expect(consequentialUnchorded(block), word).toHaveLength(1);
    });
    ["Publish", "Revoke", "Invite", "Archive"].forEach((word) => expect(words).toContain(word));
  });

  test("the measured consequential panels carry the chord mark (incl. those whose label is just 'Save')", () => {
    const chord = (file, id) => withFields.filter((b) => b.file === file)
      .some((b) => new RegExp(`id="${id}"[^>]*data-dt-primary="chord"|data-dt-primary="chord"[^>]*id="${id}"`).test(b.body));
    [
      ["Platform/Workflow/Index.cshtml", "wf-publish-submit"],
      ["Platform/Workflow/Tasks.cshtml", "wf-taskaction-submit"],
      ["Platform/Workflow/Escalations.cshtml", "wf-esc-run"],
      ["CRM/VisitExecution/Index.cshtml", "ve-submit-report"],
      ["CRM/VisitExecution/Index.cshtml", "ve-amend-report"],
      ["CRM/Accounts/_ContactLinkEndOffcanvas.cshtml", "btnConfirmEndContactLink"],
      ["CRM/Accounts/_RelationshipEndOffcanvas.cshtml", "btnConfirmEndRelationship"],
      ["CRM/Campaigns/_SnapshotPanel.cshtml", "submitSnapshot"],
      ["CRM/TerritoryManagement/_AccountAssignments.cshtml", "apply-selected-button"],
      ["DocumentManagement/ControlledDocuments/VersionHistory.cshtml", "btnSubmitVersion"],
      ["DocumentManagement/MasterRegister/Details.cshtml", "lifecycleModalSubmit"],
      ["DocumentManagement/MasterRegister/Details.cshtml", "approvalEvidenceSubmit"],
      ["MDM/ProductAbbreviationRegister/_CreateEditOffcanvas.cshtml", "btnRequestAbbreviation"],
      ["Platform/Tenants/Details.cshtml", "btnSubmitSubscriptionAction"],
      // FIX2, CT decisions: an invitation e-mail leaves; a privileged account and its invitation.
      ["Governance/Users/_CreateEditOffcanvas.cshtml", "btnSaveUser"],
      ["Platform/Administrators/_CreateEditOffcanvas.cshtml", "btnSaveAdministrator"]
    ].forEach(([file, id]) => expect(chord(file, id), `${file} #${id}`).toBe(true));
  });

  test("WP-LANE-UX-GUARDS-01: the three Stock behaviour panels save with a bare Enter (the save always asks to confirm)", () => {
    ["FinishedGoods", "Gskus", "Lskus"].forEach((screen) => {
      const body = withFields.find((b) => b.file === `MasterDataManagement/${screen}/_StockBehaviourOffcanvas.cshtml`).body;
      expect(body, screen).toMatch(/id="btnSaveStockBehaviour"[^>]*data-dt-primary="enter"/);
    });
    // Why enter, not chord: add-only lists cannot be undone, but every save passes through this confirmation first.
    const script = fs.readFileSync(path.resolve(__dirname, "..", "wwwroot/assets/js/MasterDataManagement/StockBehaviour/stock-behaviour.js"), "utf8");
    expect(script).toMatch(/window\.showConfirm\(words\.SaveConfirmation, run,/);
  });

  test("FIX2, CT decision: Gskus saves with a bare Enter (it asks for confirmation in every mode)", () => {
    const gsku = withFields.find((b) => b.file === "MasterDataManagement/Gskus/_CreateEditOffcanvas.cshtml").body;
    expect(gsku).toMatch(/id="btnSaveGsku"[^>]*data-dt-primary="enter"/);
  });

  test("the panels that save with a non-primary colour carry the mark", () => {
    const byFile = (file) => withFields.find((b) => b.file === file).body;
    expect(byFile("CRM/Accounts/_ContactLinkEndOffcanvas.cshtml")).toMatch(/id="btnConfirmEndContactLink"[^>]*data-dt-primary/);
    expect(byFile("CRM/Accounts/_RelationshipEndOffcanvas.cshtml")).toMatch(/id="btnConfirmEndRelationship"[^>]*data-dt-primary/);
    const visit = withFields.filter((b) => b.file === "CRM/VisitExecution/Index.cshtml").map((b) => b.body).join("\n");
    expect(visit).toMatch(/id="ve-submit-report"[^>]*data-dt-primary/);
    expect(visit).toMatch(/id="ve-amend-report"[^>]*data-dt-primary/);
  });
});
