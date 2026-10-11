const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * BL-391 × WCN-FIX-03 (XMC-001, CT decision: YES) — the four other screens with the same `allowInput: true` + altInput
 * date pattern now use the SHARED typed-date guard, and fail closed without it. The three form screens' REAL scripts
 * are booted in jsdom against the ids they bind (the network is the only stub). The LegalEntities list binds its
 * filter dates only from inside its DataTable's init (jQuery + DataTables + saved views); there the production
 * `initDatePickers` block is lifted out of the real file and run against the real flatpickr — the measured code is
 * the shipped text, not a copy.
 *
 *   Organization / OrganizationUnits form · Organization / Positions form
 *   MasterData / LegalEntities list filter (created from / to) · MasterData / LegalEntities wizard
 */

const ROOT = path.resolve(__dirname, "..");
const read = (...parts) => fs.readFileSync(path.join(ROOT, ...parts), "utf8");

const SCREENS = [
  { name: "OrganizationUnits form", script: "wwwroot/assets/js/Organization/OrganizationUnits/form.js",
    view: ["Views", "Organization", "OrganizationUnits", "Form.cshtml"],
    root: '<div id="ou-form-page" data-ou-id="" data-ou-mode="create"></div>', dates: ["ouEffectiveFrom", "ouEffectiveTo"] },
  { name: "Positions form", script: "wwwroot/assets/js/Organization/Positions/form.js",
    view: ["Views", "Organization", "Positions", "Form.cshtml"],
    root: '<div id="p-form-page" data-p-id="" data-p-mode="create"></div>', dates: ["pEffectiveFrom", "pEffectiveTo"] },
  { name: "LegalEntities list filter", script: "wwwroot/assets/js/MasterData/LegalEntities/index.js",
    view: ["Views", "MasterData", "LegalEntities", "Index.cshtml"],
    root: "", dates: ["filterCreatedFrom", "filterCreatedTo"], lifted: true },
  { name: "LegalEntities wizard", script: "wwwroot/assets/js/MasterData/LegalEntities/wizard.js",
    view: ["Views", "MasterData", "LegalEntities", "Wizard.cshtml"],
    root: '<div id="le-wizard-page" data-le-id="" data-le-mode="create"></div>', dates: ["leIncorporationDate", "leDissolutionDate"] }
];

const tick = () => new Promise((resolve) => setTimeout(resolve, 0));

beforeAll(() => {
  global.self = global.self || window;
  Object.defineProperty(window, "ontouchstart", { value: undefined, configurable: true, writable: true });
  loadScript("wwwroot/assets/vendor/libs/flatpickr/flatpickr.js");
});

beforeEach(() => {
  global.fetch = vi.fn(() => Promise.resolve({ ok: true, status: 200, json: () => Promise.resolve({ data: [] }), text: () => Promise.resolve("") }));
});

afterEach(() => {
  document.querySelectorAll(".flatpickr-calendar").forEach((cal) => cal.remove());
  document.body.innerHTML = "";
  delete window.DitenDateField;
  delete window.Stepper;
  vi.restoreAllMocks();
});

const boot = async (screen, { withGuard }) => {
  if (withGuard) { loadScript("wwwroot/assets/js/shared/diten-datefield.js"); }
  document.body.innerHTML = `${screen.root}
    ${screen.dates.map((id) => `<input type="text" id="${id}" class="form-control flatpickr-date">`).join("")}
    <button type="button" id="other">x</button>`;
  vi.spyOn(console, "error").mockImplementation(() => {});
  if (screen.lifted) {
    const source = read(screen.script);
    const start = source.indexOf("    const dateGuard = () => {");
    const end = source.indexOf("    const dateVal = ", start);
    expect(start, "the list's date-guard block moved").toBeGreaterThan(-1);
    // eslint-disable-next-line no-new-func
    const initDatePickers = new Function("datePickers", "L", `${source.slice(start, end)}\nreturn initDatePickers;`)({}, {});
    initDatePickers();
  } else {
    loadScript(screen.script);
  }
  await tick();
  return screen.dates.map((id) => document.getElementById(id)._flatpickr);
};

describe.each(SCREENS)("$name", (screen) => {
  test("binds its dates with the shared guard: '31.02.2026' typed then Tab is cleared and marked", async () => {
    const pickers = await boot(screen, { withGuard: true });
    expect(pickers.every((fp) => fp && fp.altInput), "the screen did not bind its altInput dates").toBe(true);
    pickers.forEach((fp) => {
      expect(fp._ditenGuarded, "the shared guard was not called").toBe(true);
      fp.altInput.focus();
      fp.altInput.value = "31.02.2026";
      fp.altInput.dispatchEvent(new Event("input", { bubbles: true }));
      document.getElementById("other").focus();
      expect(fp.isOpen).toBe(false);
      expect(fp.input.value, "a date that does not exist was kept as another date").toBe("");
      expect(fp.altInput.classList.contains("is-invalid")).toBe(true);
    });
  });

  test("without the shared script its dates FAIL CLOSED (read-only) and it is said", async () => {
    const pickers = await boot(screen, { withGuard: false });
    pickers.forEach((fp) => {
      expect(fp.config.allowInput).toBe(false);
      expect(fp.altInput.hasAttribute("readonly")).toBe(true);
    });
    expect(console.error.mock.calls.some((call) => String(call[0]).includes("diten-datefield.js"))).toBe(true);
  });

  test("its view loads the shared guard before the screen's script", () => {
    const view = read(...screen.view);
    const shared = view.indexOf("assets/js/shared/diten-datefield.js");
    const own = view.indexOf(screen.script.replace("wwwroot/", ""));
    expect(shared, "the shared date-field script is not loaded by the view").toBeGreaterThan(-1);
    expect(own).toBeGreaterThan(shared);
  });
});

/*
 * WCN-FIX-04 (CT decision: YES) — a refused optional date stops the save on the three form screens (an empty optional
 * date is a valid "open-ended" payload, and the update is a full replace: saving would DELETE a stored end date), and
 * stops Apply on the list filter (the refused date would drop out of the query while Apply closed the panel that shows
 * its mark). The real scripts are booted; only the network is stubbed and every write is recorded.
 */
const writes = () => global.fetch.mock.calls.filter((call) => call[1] && call[1].method && call[1].method !== "GET");

const FORMS = [
  { name: "OrganizationUnits form", script: "wwwroot/assets/js/Organization/OrganizationUnits/form.js",
    root: '<div id="ou-form-page" data-ou-id="" data-ou-mode="create"></div>',
    l10nId: "ou-form-l10n", alert: "ou-form-alert", submit: "ou-submit",
    required: { ouCode: "OU-1", ouName: "Unit", ouLegalEntityId: "le-1" },
    from: "ouEffectiveFrom", to: "ouEffectiveTo" },
  { name: "Positions form", script: "wwwroot/assets/js/Organization/Positions/form.js",
    root: '<div id="p-form-page" data-p-id="" data-p-mode="create"></div>',
    l10nId: "position-form-l10n", alert: "p-form-alert", submit: "p-submit",
    required: { pCode: "P-1", pName: "Position", pOrgUnitId: "ou-1" },
    from: "pEffectiveFrom", to: "pEffectiveTo" },
  { name: "LegalEntities wizard", script: "wwwroot/assets/js/MasterData/LegalEntities/wizard.js",
    root: '<div id="le-wizard-page" data-le-id="" data-le-mode="create"></div>',
    l10nId: "le-wizard-l10n", alert: "le-wizard-alert", submit: "le-submit",
    required: { leCode: "LE-1", leLegalName: "Entity", leLegalFormCode: "LLC", leCountryCode: "TR", leBaseCurrencyCode: "TRY" },
    from: "leIncorporationDate", to: "leDissolutionDate" }
];

const l10nIdOf = (script) => {
  const match = /(?:getElementById|byId)\('([a-z0-9-]+-l10n)'\)/.exec(read(script));
  return match ? match[1] : null;
};

const bootForm = async (form, extra = "") => {
  loadScript("wwwroot/assets/js/shared/diten-datefield.js");
  const fields = Object.entries(form.required)
    .map(([id, value]) => `<select id="${id}"><option value="${value}" selected>${value}</option></select>`).join("");
  document.body.innerHTML = `${form.root}
    <script type="application/json" id="${l10nIdOf(form.script)}">{"DateFormat":"d.m.Y","DateNotRecognized":"DATE-REFUSED","RequiredField":"REQUIRED"}</script>
    <div id="${form.alert}" class="d-none"></div>
    ${fields}
    <input type="text" id="${form.from}" class="form-control flatpickr-date">
    <input type="text" id="${form.to}" class="form-control flatpickr-date">
    <button type="button" id="other">x</button>
    <button type="button" class="btn btn-primary" id="${form.submit}">Save</button>${extra}`;
  vi.spyOn(console, "error").mockImplementation(() => {});
  loadScript(form.script);
  await tick();
  // The wizard enables its submit only on its last step (bs-stepper is not loaded here); without this the click
  // would reach nothing and "no write" would pass for the wrong reason — the control test below proves it is reached.
  document.getElementById(form.submit).disabled = false;
};

const typeAndTab = (id, text) => {
  const fp = document.getElementById(id)._flatpickr;
  fp.altInput.focus();
  fp.altInput.value = text;
  fp.altInput.dispatchEvent(new Event("input", { bubbles: true }));
  document.getElementById("other").focus();
  return fp;
};

describe.each(FORMS)("$name: a refused date stops the save (WCN-FIX-04)", (form) => {
  test("the screen's l10n block id was found (guards against a vacuous boot)", () => {
    expect(l10nIdOf(form.script)).toBeTruthy();
  });

  test("an end date typed as '31.02.2026' is refused: no POST / PUT, and the reason is shown", async () => {
    await bootForm(form);
    typeAndTab(form.from, "01.02.2026");
    const to = typeAndTab(form.to, "31.02.2026");
    expect(to.altInput.classList.contains("is-invalid")).toBe(true);

    document.getElementById(form.submit).click();
    await tick();

    expect(writes(), "the refused end date was saved as 'no end date'").toEqual([]);
    expect(document.getElementById(form.alert).textContent).toContain("DATE-REFUSED");
  });

  test("control: the same screen with a readable end date DOES write (the save is reached)", async () => {
    await bootForm(form);
    typeAndTab(form.from, "01.02.2026");
    typeAndTab(form.to, "20.02.2026");
    document.getElementById(form.submit).click();
    await tick();
    expect(writes()).toHaveLength(1);
  });

  test("a stored end date and then a typo: nothing is written, so the stored end date is not deleted", async () => {
    await bootForm(form);
    document.getElementById(form.to)._flatpickr.setDate("2026-12-31", true); // what loadForEdit writes
    typeAndTab(form.to, "31.02.2027");

    document.getElementById(form.submit).click();
    await tick();

    expect(writes()).toEqual([]);
  });
});

describe("LegalEntities list filter: a refused date stops Apply (WCN-FIX-04)", () => {
  const source = read("wwwroot/assets/js/MasterData/LegalEntities/index.js");

  test("the filter dates are bound where the filters are set up (index.js setupFilters → initDatePickers)", () => {
    const setup = source.indexOf("const setupFilters = (api) => {");
    expect(setup).toBeGreaterThan(-1);
    expect(source.indexOf("initDatePickers();", setup)).toBeGreaterThan(setup);
    expect(source.indexOf("initDatePickers();", setup)).toBeLessThan(source.indexOf("btnFilterApply", setup));
  });

  test("Apply with a refused 'created to' date draws nothing, keeps the panel open, and says why", async () => {
    // The Apply handler is lifted from the real file with the closure it reads; the guard and flatpickr are real.
    const start = source.indexOf("        document.getElementById('btnFilterApply')?.addEventListener('click', () => {");
    const end = source.indexOf("        document.getElementById('btnFilterReset')", start);
    expect(start).toBeGreaterThan(-1);
    loadScript("wwwroot/assets/js/shared/diten-datefield.js");
    document.body.innerHTML = `<input id="filterCreatedFrom"><input id="filterCreatedTo"><button id="other">x</button>
      <button id="btnFilterApply">Apply</button><div id="inlineFilterCollapse" class="collapse show"></div>`;
    const datePickers = {};
    ["filterCreatedFrom", "filterCreatedTo"].forEach((id) => {
      datePickers[id] = window.flatpickr(document.getElementById(id), { dateFormat: "Y-m-d", altInput: true, altFormat: "d.m.Y", allowInput: true });
      window.DitenDateField.guardAgainstSilentMisparse(datePickers[id], "altFormat");
    });
    const api = { draw: vi.fn() };
    const toasts = [];
    window.showToast = (message, type) => toasts.push([message, type]);
    window.DtDefaults = { updateVisualState: vi.fn() };
    const hide = vi.fn();
    window.bootstrap = { Collapse: { getOrCreateInstance: () => ({ hide }) } };
    // eslint-disable-next-line no-new-func
    const bind = new Function("api", "datePickers", "L", "$", "dateVal", "getAppliedFilterCount", "saveFilterArmed",
      "setSaveFilterVisible", "isDirtyComparedToDefault", "filterCollapseId", "appliedFilters",
      `${source.slice(start, end)}`);
    bind(api, datePickers, { DateNotRecognized: "DATE-REFUSED" }, () => ({ val: () => [] }),
      (id) => document.getElementById(id).value, () => 0, false, () => {}, () => false, "inlineFilterCollapse", {});

    const to = datePickers.filterCreatedTo;
    to.altInput.focus();
    to.altInput.value = "31.02.2026";
    to.altInput.dispatchEvent(new Event("input", { bubbles: true }));
    document.getElementById("other").focus();
    expect(to.altInput.classList.contains("is-invalid")).toBe(true);

    document.getElementById("btnFilterApply").click();

    expect(api.draw).not.toHaveBeenCalled();
    expect(hide, "Apply closed the panel that shows the red mark").not.toHaveBeenCalled();
    expect(toasts).toEqual([["DATE-REFUSED", "error"]]);
    delete window.showToast;
    delete window.DtDefaults;
    delete window.bootstrap;
  });
});

/*
 * WCN-FIX-05 — the "both dates are covered" claim, bound to tests: {start date, end date} × {OrganizationUnits Save,
 * Positions Save, wizard Next, wizard Save (which also jumps back to the first invalid step), list filter Apply}.
 * The wizard runs with a stand-in for bs-stepper that dispatches the SAME events the library does
 * (show.bs-stepper, cancelable, then shown.bs-stepper, with detail.indexStep) — the wizard's own listeners do the rest.
 */
class FakeStepper {
  constructor(el) { this.el = el; this.index = 0; this.toCalls = []; FakeStepper.last = this; }
  to(step) {
    const show = new CustomEvent("show.bs-stepper", { detail: { indexStep: step - 1 }, cancelable: true });
    this.el.dispatchEvent(show);
    if (show.defaultPrevented) { return; }
    this.index = step - 1;
    this.toCalls.push(step);
    this.el.dispatchEvent(new CustomEvent("shown.bs-stepper", { detail: { indexStep: step - 1 } }));
  }
  next() { this.to(this.index + 2); }
  previous() { this.to(this.index); }
}

const WIZARD = FORMS.find((form) => form.name === "LegalEntities wizard");

const bootWizard = async () => {
  FakeStepper.last = null;
  window.Stepper = FakeStepper;
  await bootForm(WIZARD,
    '<div id="le-wizard-stepper"></div><button type="button" id="le-next">Next</button><button type="button" id="le-prev">Prev</button>');
  expect(FakeStepper.last && FakeStepper.last.el.id, "the wizard did not build its stepper").toBe("le-wizard-stepper");
};

describe.each(["from", "to"])("WCN-FIX-05: a refused %s date", (which) => {
  test.each(FORMS.filter((form) => form !== WIZARD))("$name: Save is stopped and says why", async (form) => {
    await bootForm(form);
    const other = which === "from" ? form.to : form.from;
    typeAndTab(other, "01.02.2026");
    typeAndTab(form[which], "31.02.2026");
    document.getElementById(form.submit).click();
    await tick();
    expect(writes()).toEqual([]);
    expect(document.getElementById(form.alert).textContent).toContain("DATE-REFUSED");
  });

  test("LegalEntities wizard: Next on the dates' step does not move on, and says why", async () => {
    await bootWizard();
    const stepper = FakeStepper.last;
    document.getElementById("le-next").click();          // step 1 → 2 (step 1 is filled)
    expect(stepper.index).toBe(1);
    typeAndTab(WIZARD[which], "31.02.2026");
    document.getElementById("le-next").click();          // step 2 → refused
    expect(stepper.index, "the wizard moved past a refused date").toBe(1);
    expect(document.getElementById(WIZARD.alert).textContent).toContain("DATE-REFUSED");
  });

  test("LegalEntities wizard: Save on the last step is stopped and jumps back to the dates' step", async () => {
    await bootWizard();
    const stepper = FakeStepper.last;
    for (let i = 0; i < 5; i += 1) { document.getElementById("le-next").click(); }
    expect(stepper.index, "the wizard did not reach its last step").toBe(5);
    typeAndTab(WIZARD[which], "31.02.2026");
    document.getElementById(WIZARD.submit).click();
    await tick();
    expect(writes()).toEqual([]);
    expect(stepper.index, "Save did not take the reader back to the first invalid step").toBe(1);
    expect(document.getElementById(WIZARD.alert).textContent).toContain("DATE-REFUSED");
  });
});

describe("WCN-FIX-06: the wizard's header click (non-linear stepper) cannot carry a refused date forward", () => {
  test.each(["from", "to"])("a refused %s date: clicking a later step's header keeps the reader on the dates' step", async (which) => {
    await bootWizard();
    const stepper = FakeStepper.last;
    for (let i = 0; i < 5; i += 1) { document.getElementById("le-next").click(); }
    stepper.to(2);                                       // back to the dates' step (always allowed)
    expect(stepper.index).toBe(1);
    typeAndTab(WIZARD[which], "31.02.2026");
    stepper.to(4);                                       // a header click on a step already reached
    expect(stepper.index, "the header click carried a refused date past its step").toBe(1);
    expect(document.getElementById(WIZARD.alert).textContent).toContain("DATE-REFUSED");
    stepper.to(1);                                       // going back stays free
    expect(stepper.index).toBe(0);
  });
});

describe("WCN-FIX-05: the list filter's Apply stops for either date", () => {
  const source = read("wwwroot/assets/js/MasterData/LegalEntities/index.js");
  test.each(["filterCreatedFrom", "filterCreatedTo"])("a refused %s stops Apply", (id) => {
    const start = source.indexOf("        document.getElementById('btnFilterApply')?.addEventListener('click', () => {");
    const end = source.indexOf("        document.getElementById('btnFilterReset')", start);
    loadScript("wwwroot/assets/js/shared/diten-datefield.js");
    document.body.innerHTML = `<input id="filterCreatedFrom"><input id="filterCreatedTo"><button id="other">x</button>
      <button id="btnFilterApply">Apply</button>`;
    const datePickers = {};
    ["filterCreatedFrom", "filterCreatedTo"].forEach((key) => {
      datePickers[key] = window.flatpickr(document.getElementById(key), { dateFormat: "Y-m-d", altInput: true, altFormat: "d.m.Y", allowInput: true });
      window.DitenDateField.guardAgainstSilentMisparse(datePickers[key], "altFormat");
    });
    const api = { draw: vi.fn() };
    window.showToast = vi.fn();
    window.DtDefaults = { updateVisualState: vi.fn() };
    window.bootstrap = { Collapse: { getOrCreateInstance: () => ({ hide: vi.fn() }) } };
    // eslint-disable-next-line no-new-func
    new Function("api", "datePickers", "L", "$", "dateVal", "getAppliedFilterCount", "saveFilterArmed",
      "setSaveFilterVisible", "isDirtyComparedToDefault", "filterCollapseId", "appliedFilters", source.slice(start, end))(
      api, datePickers, { DateNotRecognized: "DATE-REFUSED" }, () => ({ val: () => [] }),
      (key) => document.getElementById(key).value, () => 0, false, () => {}, () => false, "x", {});
    const fp = datePickers[id];
    fp.altInput.focus();
    fp.altInput.value = "31.02.2026";
    fp.altInput.dispatchEvent(new Event("input", { bubbles: true }));
    document.getElementById("other").focus();
    document.getElementById("btnFilterApply").click();

    expect(api.draw).not.toHaveBeenCalled();
    expect(window.showToast).toHaveBeenCalledWith("DATE-REFUSED", "error");
    delete window.showToast;
    delete window.DtDefaults;
    delete window.bootstrap;
  });
});

/*
 * WCN-FIX-05 · 3 — the screens' l10n blocks carry the refusal sentence, and SharedResource has it in all 7 languages.
 * The JS tests above inject their own JSON; without this a deleted line would leave every language on the script's
 * English fallback and nothing would notice.
 */
describe("DateNotRecognized reaches every screen that shows it", () => {
  const BLOCKS = [
    "Views/Organization/PositionAssignments/_FormL10n.cshtml",
    "Views/Organization/OrganizationUnits/_FormL10n.cshtml",
    "Views/Organization/Positions/_FormL10n.cshtml",
    "Views/MasterData/LegalEntities/_WizardL10n.cshtml",
    "Views/MasterData/LegalEntities/_IndexL10n.cshtml"
  ];
  test.each(BLOCKS)("%s emits DateNotRecognized from SharedResource", (block) => {
    expect(read(block)).toMatch(/DateNotRecognized\s*=\s*SharedLocalizer\["DateNotRecognized"\]\.Value/);
  });

  const sentence = (lang) => {
    const match = /<data name="DateNotRecognized"[^>]*>\s*<value>([\s\S]*?)<\/value>/.exec(read(`Resources/SharedResource.${lang}.resx`));
    return match ? match[1].trim() : "";
  };
  const comparable = (text) => text.replace(/[\u200B-\u200F\u202A-\u202E\u2066-\u2069\uFEFF]/g, "")
    .replace(/[\s.!?…:;,。！？،؛]+$/u, "").trim().toLocaleLowerCase("en");

  test.each(["en", "tr", "fr", "es", "zh", "ar", "ru"])("SharedResource.%s carries a non-empty DateNotRecognized", (lang) => {
    expect(sentence(lang).length).toBeGreaterThan(10);
  });

  test.each(["tr", "fr", "es", "zh", "ar", "ru"])("SharedResource.%s DateNotRecognized is a translation, not the English sentence", (lang) => {
    expect(comparable(sentence(lang))).not.toBe(comparable(sentence("en")));
  });

  test("the scripts read the key the blocks emit", () => {
    ["wwwroot/assets/js/Organization/PositionAssignments/form.js", "wwwroot/assets/js/Organization/OrganizationUnits/form.js",
      "wwwroot/assets/js/Organization/Positions/form.js", "wwwroot/assets/js/MasterData/LegalEntities/wizard.js",
      "wwwroot/assets/js/MasterData/LegalEntities/index.js"]
      .forEach((script) => expect(read(script), script).toContain("L.DateNotRecognized"));
  });
});
