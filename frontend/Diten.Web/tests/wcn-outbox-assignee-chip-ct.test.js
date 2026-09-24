const { bootSurface, app } = require("./wcn-boot");

/*
 * BAŞLATTIKLARIM NAMES THE HOLDER — owner finding 2026-09-24 (control round), CT guard.
 *
 * "Ali'ye görev atadım, Başlattıklarım'da hangisi Ali'de belli değil": every row drew the requester chip, and on
 * work the reader raised that chip only says "Ben". The row now names the HOLDER when the reader raised the work
 * and somebody else holds it, and keeps the requester chip everywhere else — inbox work (the holder is me, the
 * requester matters), self-assigned work (nothing to add), questions and approvals (untouched).
 */
const RAISED = "aaaaaaaa-0000-4000-8000-000000000001";
const RAISED2 = "aaaaaaaa-0000-4000-8000-000000000004";
const INBOX = "aaaaaaaa-0000-4000-8000-000000000002";
const SELF = "aaaaaaaa-0000-4000-8000-000000000003";
const ME = "11111111-1111-1111-1111-111111111111";
const ALI = "22222222-2222-2222-2222-222222222222";
const AYSE = "33333333-3333-3333-3333-333333333333";

const STRINGS = { DetailAssignee: "Atanan", PersonSelf: "Ben" };
const translator = {
  t: (key) => (key in STRINGS ? STRINGS[key] : key),
  tf: (key) => (key in STRINGS ? STRINGS[key] : key),
  tn: (key, args) => Object.keys(args || {}).reduce((text, name) => text.split(`{${name}}`).join(String(args[name])), key in STRINGS ? STRINGS[key] : key)
};

const task = (id, overrides) => Object.assign({
  fixtureKind: "workItem", id, workIntent: "task", assignmentMode: "direct", ownershipState: "owned",
  admissionState: "admitted", normalizedStatus: "InProgress", taskLifecycle: "InProgress", executionState: "active",
  timerState: "notApplicable", systemState: "fresh", actionDepth: "inline",
  title: { kind: "display", text: "Lot 42 sertifikası", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: { providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: id, deepLink: `/Tasks/${id}` },
  lifecycleOwner: "tasks", workItemCapabilities: ["planning", "execution"], actions: [],
  concurrency: { kind: "version", token: "1" }, waitingContext: null, escalation: null, dueAt: null
}, overrides);

const items = () => [
  // raised by me, held by Ali → Başlattıklarım
  task(RAISED, {
    viewerRelation: "initiator",
    requester: { id: ME, displayName: "Diten Admin", isCurrentUser: true },
    assignee: { id: ALI, displayName: "Ali Veli", isCurrentUser: false }
  }),
  // raised by me, held by Ayşe → Başlattıklarım (the filter has two holders to choose from)
  task(RAISED2, {
    viewerRelation: "initiator",
    title: { kind: "display", text: "Etiket kontrolü", locale: "und" },
    requester: { id: ME, displayName: "Diten Admin", isCurrentUser: true },
    assignee: { id: AYSE, displayName: "Ayşe Korkmaz", isCurrentUser: false }
  }),
  // raised by Ayşe, held by me → İşlerim
  task(INBOX, {
    requester: { id: AYSE, displayName: "Ayşe Korkmaz", isCurrentUser: false },
    assignee: { id: ME, displayName: "Diten Admin", isCurrentUser: true }
  }),
  // raised by me, held by me → İşlerim, nothing to add
  task(SELF, {
    requester: { id: ME, displayName: "Diten Admin", isCurrentUser: true },
    assignee: { id: ME, displayName: "Diten Admin", isCurrentUser: true }
  })
];

const tick = () => new Promise((resolve) => { setTimeout(resolve, 50); });

const chipsOf = (id) => [...app().querySelectorAll(`[data-wcn-row="${id}"] .wcn-chip-requester`)]
  .map((c) => ({ text: c.textContent.trim(), title: c.getAttribute("title") || "", icon: c.querySelector("i").className }));

describe("Başlattıklarım names the holder, everything else keeps the requester (CT)", () => {
  it("a task I raised and Ali holds shows Ali, titled Atanan — not 'Ben'", async () => {
    await bootSurface({ items: items(), wcn: translator });
    app().querySelector('[data-wcn-tab="baslattiklarim"]').click();
    await tick();
    const chips = chipsOf(RAISED);
    expect(chips.length, "exactly one person chip").toBe(1);
    expect(chips[0].text).toBe("Ali Veli");
    expect(chips[0].title).toBe("Atanan");
    expect(chips[0].icon).toContain("bx-user-check");
    expect(app().querySelector(`[data-wcn-row="${RAISED}"]`).textContent).not.toContain("Ben");
  });

  it("a task Ayşe raised and I hold still shows Ayşe as the requester", async () => {
    await bootSurface({ items: items(), wcn: translator });
    app().querySelector('[data-wcn-tab="islerim"]').click();
    await tick();
    const chips = chipsOf(INBOX);
    expect(chips.length).toBe(1);
    expect(chips[0].text).toBe("Ayşe Korkmaz");
    expect(chips[0].title).toBe("");
    expect(chips[0].icon).toContain("bx-user");
    expect(chips[0].icon).not.toContain("bx-user-check");
  });

  it("a task I raised for myself keeps the requester chip exactly as before (no holder, no 'Atanan')", async () => {
    await bootSurface({ items: items(), wcn: translator });
    app().querySelector('[data-wcn-tab="islerim"]').click();
    await tick();
    const chips = chipsOf(SELF);
    expect(chips.length).toBe(1);
    // toPresentation names the requester as it always has (the display name); the holder rule must not touch it.
    expect(chips[0].text).toBe("Diten Admin");
    expect(chips[0].title).toBe("");
    expect(chips[0].icon).not.toContain("bx-user-check");
  });
});

describe("Başlattıklarım can be narrowed to one holder (CT)", () => {
  const openFilters = () => { const toggle = app().querySelector("[data-wcn-filter-toggle]"); if (toggle) toggle.click(); };
  const assigneeSelect = () => app().querySelector('select[data-wcn-filter="assignee"]');

  it("offers a holder filter listing the holders present, only on Başlattıklarım", async () => {
    await bootSurface({ items: items(), wcn: translator });
    app().querySelector('[data-wcn-tab="baslattiklarim"]').click();
    await tick();
    openFilters();
    await tick();
    const select = assigneeSelect();
    expect(select, "the holder filter is drawn").not.toBeNull();
    expect(select.getAttribute("data-placeholder")).toBe("Atanan");
    expect([...select.options].map((o) => o.value)).toEqual(["Ali Veli", "Ayşe Korkmaz"]);

    app().querySelector('[data-wcn-tab="islerim"]').click();
    await tick();
    openFilters();
    await tick();
    expect(assigneeSelect(), "no holder filter where the holder is always me").toBeNull();
  });

  it("picking Ali leaves only Ali's work on screen, and resetting brings Ayşe's back", async () => {
    await bootSurface({ items: items(), wcn: translator });
    app().querySelector('[data-wcn-tab="baslattiklarim"]').click();
    await tick();
    openFilters();
    await tick();
    const select = assigneeSelect();
    [...select.options].forEach((o) => { o.selected = o.value === "Ali Veli"; });
    select.dispatchEvent(new Event("change", { bubbles: true }));
    await tick();
    const rows = () => [...app().querySelectorAll("[data-wcn-row]")].map((r) => r.getAttribute("data-wcn-row"));
    expect(rows()).toEqual([RAISED]);

    // Clearing the pick (the way select2's "×" does it) brings every holder back.
    [...assigneeSelect().options].forEach((o) => { o.selected = false; });
    assigneeSelect().dispatchEvent(new Event("change", { bubbles: true }));
    await tick();
    expect(rows().sort()).toEqual([RAISED, RAISED2].sort());
  });

  it("the search box finds work by its holder's name", async () => {
    await bootSurface({ items: items(), wcn: translator });
    app().querySelector('[data-wcn-tab="baslattiklarim"]').click();
    await tick();
    const search = app().querySelector("input[data-wcn-search]");
    expect(search, "the search box").not.toBeNull();
    search.value = "Ali";
    search.dispatchEvent(new Event("input", { bubbles: true }));
    // the box debounces before it filters
    await new Promise((resolve) => { setTimeout(resolve, 600); });
    const rows = [...app().querySelectorAll("[data-wcn-row]")].map((r) => r.getAttribute("data-wcn-row"));
    expect(rows).toEqual([RAISED]);
  });
});
