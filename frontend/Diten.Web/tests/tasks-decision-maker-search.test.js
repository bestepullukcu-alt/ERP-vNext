const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * BL-512 (WP-PLATFORM-DECISION-MAKERS-SEARCH-01) — the task form's reviewer and approval-manager pickers SEARCH the
 * server; the whole decision-makers list is never fetched. What this file measures, on the PRODUCTION form-page.js /
 * form.js / api.js with only fetch and jQuery stubbed:
 *  - the form asks no people list when it opens;
 *  - an edit turns the stored reviewer / approver back into names with ONE `?ids=` call, before writing the form;
 *  - an untouched save posts the stored approver back unchanged (never an empty one), also when it can no longer be
 *    resolved — which then reads as "person not found", never as a GUID;
 *  - the two pickers are bound as server-searched (≥ 2 characters) and their transport asks `?search=`.
 */

const FORM_VIEW = fs.readFileSync(path.resolve(__dirname, "..", "Views", "Tasks", "_Form.cshtml"), "utf8");

const TASK_ID = "11111111-2222-3333-4444-555555555555";
const APPROVER = "aaaaaaaa-1111-2222-3333-444444444444";
const REVIEWER = "bbbbbbbb-1111-2222-3333-444444444444";

const FORM_HTML = `
  <button type="submit" form="taskForm" id="taskSubmit">save</button>
  <form id="taskForm" data-task-mode="MODE" data-task-id="TASKID" data-task-version="3">
    <input id="taskTitle" />
    <select id="taskAssignmentTarget"><option value="SelfAssigned" selected>self</option></select>
    <select id="taskAssignee"></select>
    <select id="taskPoolPosition"></select>
    <input id="taskDueAt" type="date" />
    <input type="checkbox" id="taskReviewRequired" />
    <select class="select2" id="taskReviewer" data-people-search="1"><option value=""></option></select>
    <input type="checkbox" id="taskApprovalRequired" />
    <select class="select2" id="taskApprovalManager" data-people-search="1"><option value=""></option></select>
    <input type="checkbox" id="taskEmailNotifications" checked />
    <input type="checkbox" id="taskDelegationAllowed" />
    <div class="d-none" id="taskCustomFields"><div id="taskCustomFieldsRow"></div></div>
    <ul id="taskChecklistItems"></ul>
  </form>`;

describe("BL-512 — the task form's approver and reviewer pickers search, never list", () => {
  let requests;
  let sent;
  let select2Settings;
  let resolvable;
  let idsFailure;

  const stubFetch = () => {
    global.fetch = async (url, init) => {
      requests.push({ url, method: init?.method || "GET" });
      const ok = (data, status = 200) => ({ ok: true, status, json: async () => ({ data }) });
      if (url === "/Tasks/api/field-definitions") { return ok([]); }
      if (url === "/Tasks/api/assignable-positions") { return ok([]); }
      if (url === "/Tasks/api/assignable-people") { return ok({ people: [], excluded: null }); }
      if (url.startsWith("/Tasks/api/decision-makers?ids=")) {
        if (idsFailure) { return { ok: false, status: idsFailure, json: async () => ({ reasonCode: null }) }; }
        const ids = decodeURIComponent(url.split("?ids=")[1]).split(",");
        return ok({ people: resolvable.filter((p) => ids.includes(p.userId)) });
      }
      if (url.startsWith("/Tasks/api/decision-makers?search=")) {
        return ok({ people: [{ userId: APPROVER, displayName: "Ayşe Kaya", positionName: "Kalite Müdürü", organizationUnitName: "Kalite" }] });
      }
      if (url === `/Tasks/api/${TASK_ID}` && (init?.method || "GET") === "GET") {
        return ok({
          id: TASK_ID, title: "Var olan görev", version: 3, assignmentTarget: "SelfAssigned",
          approvalRequired: true, approvalManagerUserId: APPROVER,
          reviewRequired: true, reviewerCandidateUserId: REVIEWER,
          dueAt: "2026-12-01T00:00:00Z", fieldValues: []
        });
      }
      if (url === `/Tasks/api/${TASK_ID}` && init?.method === "PUT") {
        sent = JSON.parse(init.body);
        return ok(null);
      }
      if (url.startsWith("/Tasks/api/assignment-direction")) { return ok({ isUpward: false }); }
      return ok(null);
    };
  };

  const stubJQuery = () => {
    const jq = (selectorOrNode) => {
      const nodes = typeof selectorOrNode === "string"
        ? Array.from(document.querySelectorAll(selectorOrNode))
        : [selectorOrNode];
      const api = {
        length: nodes.length,
        each(cb) { nodes.forEach((n, i) => cb.call(n, i, n)); return api; },
        wrap() { return api; }, parent() { return api; },
        hasClass() { return false; }, on() { return api; },
        select2(settings) { nodes.forEach((n) => { select2Settings[n.id] = settings; }); return api; }
      };
      return api;
    };
    global.$ = jq;
    global.jQuery = jq;
  };

  const flush = async () => { for (let i = 0; i < 8; i += 1) { await new Promise((r) => setTimeout(r, 0)); } };

  const boot = async (mode) => {
    document.body.innerHTML = FORM_HTML.replace("MODE", mode).replace("TASKID", mode === "edit" ? TASK_ID : "");
    stubJQuery();
    delete global.TaskForm;
    delete global.TasksApi;
    loadScript("wwwroot/assets/js/backbone-shell.js");
    loadScript("wwwroot/assets/js/shared/diten-checkitem.js");
    loadScript("wwwroot/assets/js/shared/diten-person-picker.js");
    loadScript("wwwroot/assets/js/shared/diten-people-search.js");
    loadScript("wwwroot/assets/js/Tasks/form.js");
    loadScript("wwwroot/assets/js/Tasks/api.js");
    loadScript("wwwroot/assets/js/Tasks/form-page.js");
    await flush();
  };

  beforeEach(() => {
    requests = [];
    sent = null;
    select2Settings = {};
    resolvable = [{ userId: APPROVER, displayName: "Ayşe Kaya", positionName: "Kalite Müdürü", organizationUnitName: "Kalite" }];
    idsFailure = null;
    window.sessionStorage.clear();
    stubFetch();
    window.HTMLElement.prototype.scrollIntoView = function scrollIntoView() {};
    global.TasksL10n = { t: (key) => key };
    global.DitenModal = { success: async () => {}, error: async () => {}, warning: async () => {} };
    global.showToast = () => {};
    global.location = { get href() { return ""; }, set href(value) { void value; } };
  });

  it("asks no people list when the form opens", async () => {
    await boot("create");

    expect(requests.filter((r) => r.url.includes("decision-makers")), "the decision-makers list was fetched on open").toEqual([]);
  });

  it("binds both pickers as server-searched, two characters at least, asking ?search=", async () => {
    await boot("create");

    ["taskReviewer", "taskApprovalManager"].forEach((id) => {
      const settings = select2Settings[id];
      expect(settings, `${id} was not enhanced`).toBeTruthy();
      expect(settings.minimumInputLength).toBe(2);
      expect(typeof settings.ajax?.transport).toBe("function");
      expect(settings.language.inputTooShort()).toBe("peopleSearchMinimumLength");
      expect(settings.language.noResults()).toBe("peopleSearchNoResults");
    });

    const results = await new Promise((resolve, reject) =>
      select2Settings.taskApprovalManager.ajax.transport({ data: { term: "ay" } }, resolve, reject));
    expect(requests.some((r) => r.url === "/Tasks/api/decision-makers?search=ay")).toBe(true);
    expect(results.results).toEqual([{ id: APPROVER, text: "Ayşe Kaya — Kalite Müdürü — Kalite" }]);
  });

  it("an edit resolves the stored reviewer and approver with ONE ids call and shows the name", async () => {
    await boot("edit");

    const idsCalls = requests.filter((r) => r.url.startsWith("/Tasks/api/decision-makers?ids="));
    expect(idsCalls).toHaveLength(1);
    expect(requests.some((r) => r.url.includes("decision-makers?search="))).toBe(false);
    const approver = document.getElementById("taskApprovalManager");
    expect(approver.value).toBe(APPROVER);
    expect(approver.selectedOptions[0].textContent).toBe("Ayşe Kaya — Kalite Müdürü — Kalite");
  });

  it("a person that no longer resolves keeps its value and reads 'person not found', never the id", async () => {
    await boot("edit");

    const reviewer = document.getElementById("taskReviewer");
    expect(reviewer.value).toBe(REVIEWER);
    expect(reviewer.selectedOptions[0].textContent).toBe("decisionMakerUnavailable");
    expect(reviewer.selectedOptions[0].textContent).not.toContain(REVIEWER);
  });

  it("an untouched save posts the stored approver and reviewer back unchanged", async () => {
    await boot("edit");
    document.getElementById("taskSubmit").click();
    await flush();

    expect(sent, "the edit was not saved").toBeTruthy();
    expect(sent.approvalManagerUserId).toBe(APPROVER);
    expect(sent.reviewerCandidateUserId).toBe(REVIEWER);
  });

  // BL-512 FIX1 — a READ that failed is not "this person no longer exists": both keep their value, and say why.
  it.each([429, 403, 503])("when the stored people cannot be loaded (%i) they keep their value and say so", async (status) => {
    idsFailure = status;
    await boot("edit");

    ["taskApprovalManager", "taskReviewer"].forEach((id) => {
      const select = document.getElementById(id);
      expect(select.selectedOptions[0].textContent, id).toBe("decisionMakerLoadFailed");
    });
    expect(document.getElementById("taskApprovalManager").value).toBe(APPROVER);

    document.getElementById("taskSubmit").click();
    await flush();
    expect(sent.approvalManagerUserId).toBe(APPROVER);
    expect(sent.reviewerCandidateUserId).toBe(REVIEWER);
  });

  // BL-512 FIX1 — the quick-create draft path: a draft that carries an approver is named the same way an edit is.
  it("a quick-create draft's approver is resolved with ONE ids call and shown by name", async () => {
    window.sessionStorage.setItem("tasks.create-draft", JSON.stringify({
      title: "Taslak", assignmentTarget: "SelfAssigned", approvalRequired: true, approvalManagerUserId: APPROVER
    }));
    await boot("create");

    expect(requests.filter((r) => r.url.startsWith("/Tasks/api/decision-makers?ids="))).toHaveLength(1);
    const approver = document.getElementById("taskApprovalManager");
    expect(approver.value).toBe(APPROVER);
    expect(approver.selectedOptions[0].textContent).toBe("Ayşe Kaya — Kalite Müdürü — Kalite");
  });

  it("the markup marks both pickers as server-searched with the search hint", () => {
    expect(FORM_VIEW).toMatch(/id="taskReviewer"[^>]*\n?[^>]*data-people-search="1" data-placeholder="@Localizer\["PeopleSearchHint"\]"/);
    expect(FORM_VIEW).toMatch(/name="approvalManagerUserId"\s*\n\s*data-people-search="1" data-placeholder="@Localizer\["PeopleSearchHint"\]"/);
  });
});

describe("BL-512 — the search sentences exist in all seven languages, and the retired refusal is gone", () => {
  const LOCALES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
  const KEYS = ["PeopleSearchHint", "PeopleSearchMinimumLength", "PeopleSearchNoResults", "PeopleSearching",
    "DecisionMakerUnavailable", "ErrorPeopleSearchRateLimited"];
  const resx = (folder, name, locale) =>
    fs.readFileSync(path.resolve(__dirname, "..", "Resources", "Views", folder, `${name}.${locale}.resx`), "utf8");
  const value = (xml, key) => {
    const match = new RegExp(`<data name="${key}"[^>]*>\\s*<value>([^<]*)</value>`).exec(xml);
    return match ? match[1].trim() : null;
  };

  it("every new key has a value in every language, and no language copies the English one", () => {
    const english = resx("Tasks", "TasksIndex", "en");
    LOCALES.forEach((locale) => {
      const xml = resx("Tasks", "TasksIndex", locale);
      KEYS.forEach((key) => {
        expect(value(xml, key), `${key} is missing in ${locale}`).toBeTruthy();
        if (locale !== "en") {
          expect(value(xml, key), `${key} in ${locale} is the English sentence`).not.toBe(value(english, key));
        }
      });
    });
  });

  it("the Tasks bridge hands every new key to the page", () => {
    const bridge = fs.readFileSync(path.resolve(__dirname, "..", "Views", "Tasks", "_IndexL10n.cshtml"), "utf8");
    KEYS.forEach((key) => expect(bridge, `${key} is not in the bridge`).toContain(`Localizer["${key}"]`));
  });

  it("DelegateNoEligiblePeople left all seven languages and the code", () => {
    LOCALES.forEach((locale) =>
      expect(resx("WorkCenterNext", "WorkCenterNextIndex", locale), locale).not.toContain('name="DelegateNoEligiblePeople"'));
    const app = fs.readFileSync(path.resolve(__dirname, "..", "wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
    expect(app).not.toContain("DelegateNoEligiblePeople");
  });
});

describe("BL-512 FIX1 — the lookup's refusals each have their own sentence, in seven languages", () => {
  const LOCALES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
  const CODES = {
    PEOPLE_SEARCH_RATE_LIMITED: "errorPeopleSearchRateLimited",
    PEOPLE_LOOKUP_SEARCH_AND_IDS: "errorPeopleLookupSearchAndIds",
    PEOPLE_LOOKUP_TOO_MANY_IDS: "errorPeopleLookupTooManyIds",
    PEOPLE_LOOKUP_IDS_INVALID: "errorPeopleLookupIdsInvalid",
    PEOPLE_DIRECTORY_UNAVAILABLE: "errorPeopleDirectoryUnavailable"
  };

  it.each(Object.entries(CODES))("TasksApi says %s with its own key", (reasonCode, key) => {
    delete global.TasksApi;
    global.TasksL10n = { t: (k) => k };
    loadScript("wwwroot/assets/js/Tasks/api.js");
    expect(global.TasksApi.failureMessage({ ok: false, status: 400, reasonCode })).toBe(key);
  });

  it("every one of those keys (and the two new picker sentences) is in all seven languages and on the bridge", () => {
    const bridge = fs.readFileSync(path.resolve(__dirname, "..", "Views", "Tasks", "_IndexL10n.cshtml"), "utf8");
    const keys = [...Object.values(CODES), "searchFailed", "decisionMakerLoadFailed"].map((k) => k[0].toUpperCase() + k.slice(1));
    const valueOf = (xml, key) => (new RegExp(`<data name="${key}"[^>]*>\\s*<value>([^<]+)</value>`).exec(xml) || [])[1];
    const english = fs.readFileSync(path.resolve(__dirname, "..", "Resources", "Views", "Tasks", "TasksIndex.en.resx"), "utf8");
    LOCALES.forEach((locale) => {
      const xml = fs.readFileSync(path.resolve(__dirname, "..", "Resources", "Views", "Tasks", `TasksIndex.${locale}.resx`), "utf8");
      keys.forEach((key) => {
        expect(valueOf(xml, key), `${key} missing in ${locale}`).toBeTruthy();
        // ATT-FIX1 E6 — a copy of the English is not a translation.
        if (locale !== "en") { expect(valueOf(xml, key), `${key} in ${locale} is the English sentence`).not.toBe(valueOf(english, key)); }
      });
    });
    keys.forEach((key) => expect(bridge, `${key} is not on the bridge`).toContain(`Localizer["${key}"]`));
  });
});

describe("BL-512 FIX1 — the task form's two server-searched kinds on the REAL select2 4.0.13", () => {
  const words = { minimumLength: "min", noResults: "nobody found", searching: "searching", unknown: "unknown",
    rateLimited: "too many searches", failed: "could not search" };

  const boot = (markup, options) => {
    document.body.innerHTML = `<form id="f">${markup}</form>`;
    delete global.$;
    delete global.jQuery;
    delete global.TaskForm;
    loadScript("wwwroot/assets/vendor/libs/jquery/jquery.js");
    loadScript("wwwroot/assets/vendor/libs/select2/select2.js");
    loadScript("wwwroot/assets/js/shared/diten-people-search.js");
    loadScript("wwwroot/assets/js/shared/diten-person-picker.js");
    loadScript("wwwroot/assets/js/Tasks/form.js");
    global.TaskForm.enhanceSelects(document.getElementById("f"), options);
  };

  const typeInto = async (id, text) => {
    global.jQuery(`#${id}`).select2("open");
    const box = document.querySelector(".select2-search__field");
    box.value = text;
    global.jQuery(box).trigger("input");
    await new Promise((resolve) => setTimeout(resolve, 400));
    for (let i = 0; i < 4; i += 1) { await new Promise((resolve) => setTimeout(resolve, 0)); }
    const message = document.querySelector(".select2-results__message");
    return message ? message.textContent : null;
  };

  it.each([[429, "too many searches"], [403, "could not search"], [503, "could not search"]])(
    "the approver picker says a failed search (%i)", async (status, sentence) => {
      boot('<select class="select2" id="taskApprovalManager" data-people-search="1"><option value=""></option></select>',
        { searchPeople: async () => ({ ok: false, status }), peopleSearchText: words });
      expect(await typeInto("taskApprovalManager", "ay")).toBe(sentence);
    });

  it("a record-backed field says a failed search, never 'no results'", async () => {
    boot('<select class="select2" id="rec" data-custom-field="DEPT" data-custom-field-record="1"><option value=""></option></select>',
      { searchRecords: async () => ({ ok: false, status: 503 }), recordSearchText: { rateLimited: "too many searches", failed: "could not search" } });
    expect(await typeInto("rec", "ka")).toBe("could not search");
  });
});

describe("BL-512 FIX1 — every page that loads the task form loads the shared search transport first", () => {
  const VIEWS = path.resolve(__dirname, "..", "Views");
  const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true })
    .flatMap((e) => (e.isDirectory() ? walk(path.join(dir, e.name)) : e.name.endsWith(".cshtml") ? [path.join(dir, e.name)] : []));

  it("no view loads Tasks/form.js without shared/diten-people-search.js before it", () => {
    const pages = walk(VIEWS).filter((file) => fs.readFileSync(file, "utf8").includes('src="~/assets/js/Tasks/form.js"'));
    expect(pages.length, "no page loads the task form — the scan is broken").toBeGreaterThan(3);
    pages.forEach((file) => {
      const view = fs.readFileSync(file, "utf8");
      const shared = view.indexOf('src="~/assets/js/shared/diten-people-search.js"');
      expect(shared, `${path.relative(VIEWS, file)} does not load the shared transport`).toBeGreaterThan(-1);
      expect(shared, `${path.relative(VIEWS, file)} loads it after form.js`).toBeLessThan(view.indexOf('src="~/assets/js/Tasks/form.js"'));
    });
  });
});
