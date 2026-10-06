const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * WP-UI-SHORTCUTS-01 (BL-438) — WorkCenterNext's keys, after they moved onto the shared layer.
 *
 * MEASURED BEFORE THIS ROUND: no test pressed j, k, Enter, o, a, r or Escape on the list. The keys lived at the
 * tail of app.js's own keydown handler and nothing proved what they did, so "the behaviour does not change by a
 * bit" had nothing to be measured against. This file is that measurement, run against the real app.js booted
 * through the shared harness (wcn-boot), which loads the real layer the host views load.
 */
const ROOT = path.resolve(__dirname, "..");
const APP = fs.readFileSync(path.join(ROOT, "wwwroot/assets/js/WorkCenterNext/app.js"), "utf8");

const ID = (n) => `98d1f94e-1848-4539-8a99-77e72651b8a${n}`;
const item = (n) => ({
  fixtureKind: "workItem",
  id: ID(n),
  workIntent: "task",
  assignmentMode: "direct",
  ownershipState: "owned",
  admissionState: "admitted",
  normalizedStatus: "InProgress",
  taskLifecycle: "InProgress",
  executionState: "active",
  timerState: "notApplicable",
  systemState: "fresh",
  actionDepth: "inline",
  title: { kind: "display", text: `Görev ${n}`, locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: ID(n), deepLink: `/Tasks/${ID(n)}`
  },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution"],
  // `complete` is an ACCEPT-role action (mock-data's role map), which is what the `a` key reaches for.
  actions: [{
    code: "complete",
    label: { kind: "resource", key: "WorkAggregation_Action_Complete" },
    semanticType: "complete",
    enabled: true,
    source: "provider",
    disabledReasonCode: null,
    disabledReason: null,
    requiresConfirmation: false,
    requiresReason: false,
    requiresEvidence: false,
    supportsBulk: false,
    riskLevel: "normal"
  }],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  dueAt: null
});

const tick = () => new Promise((resolve) => setTimeout(resolve, 0));
const press = (key, target = document.body, extra = {}) => {
  const event = new window.KeyboardEvent("keydown", Object.assign({ key, bubbles: true, cancelable: true }, extra));
  target.dispatchEvent(event);
  return event;
};
const selectedRow = () => {
  const row = app().querySelector(".wcn-row.selected");
  return row ? row.getAttribute("data-wcn-row") : null;
};
const LIST_RETURN_KEY = "wcn:list-return-url";

const bootMine = async () => {
  await bootSurface({ items: [item(1), item(2), item(3)] });
  app().querySelector('[data-wcn-tab="islerim"]').click();
  await tick();
  // Non-vacuity: every key below is measured against three real rows.
  expect(app().querySelectorAll(".wcn-row[data-wcn-row]").length).toBe(3);
};

// jsdom has no layout, so no scrollIntoView; highlightRow calls it after marking the row.
beforeAll(() => { window.HTMLElement.prototype.scrollIntoView = window.HTMLElement.prototype.scrollIntoView || (() => {}); });

afterEach(() => {
  document.querySelectorAll(".modal, .offcanvas, .swal2-container").forEach((node) => node.remove());
  try { window.sessionStorage.removeItem(LIST_RETURN_KEY); } catch (error) { /* jsdom always has it */ }
});

describe("the keys do what they did", () => {
  beforeEach(bootMine);

  it("j selects the NEXT row and k the PREVIOUS one", () => {
    press("j");
    expect(selectedRow()).toBe(ID(1));
    press("j");
    expect(selectedRow()).toBe(ID(2));
    press("j");
    expect(selectedRow()).toBe(ID(3));
    press("k");
    expect(selectedRow()).toBe(ID(2));
  });

  it("Escape clears the selection", () => {
    press("j");
    expect(selectedRow()).toBe(ID(1));
    press("Escape");
    expect(selectedRow()).toBeNull();
  });

  it.each(["Enter", "o"])("%s opens the selected item, and nothing when nothing is selected", (key) => {
    expect(press(key).defaultPrevented).toBe(false);
    expect(window.sessionStorage.getItem(LIST_RETURN_KEY)).toBeNull();

    press("j");
    press("j");
    expect(press(key).defaultPrevented).toBe(true);
    // openDetailPage remembers the list URL on its way out — the observable half of the navigation in jsdom.
    expect(window.sessionStorage.getItem(LIST_RETURN_KEY)).not.toBeNull();
  });

  it("a runs the selected item's accept-role action; r does nothing when it has no reject-role action", async () => {
    // `complete` (accept role) asks for confirmation first, so the dialog opening IS the action starting.
    const dialogs = [];
    window.Swal = { fire: (config) => { dialogs.push(config); return Promise.resolve({ isConfirmed: false }); },
      isVisible: () => false };
    press("j");
    press("j");
    expect(press("r").defaultPrevented).toBe(false);
    await tick();
    expect(dialogs).toEqual([]);
    expect(press("a").defaultPrevented).toBe(true);
    await tick();
    expect(dialogs.length).toBe(1);
    delete window.Swal;
  });

  it("arrow keys on a focused tab move along its strip", async () => {
    const inbox = app().querySelector('[data-wcn-tab="inbox"]');
    const strip = inbox.closest('[role="tablist"]');
    expect(strip).not.toBeNull();
    const tabs = Array.from(strip.querySelectorAll('[role="tab"]'));
    const mine = tabs.findIndex((tab) => tab.getAttribute("data-wcn-tab") === "islerim");
    expect(press("ArrowRight", tabs[mine]).defaultPrevented).toBe(true);
    await tick();
    const next = tabs[mine + 1].getAttribute("data-wcn-tab");
    expect(app().querySelector(`[data-wcn-tab="${next}"]`).getAttribute("aria-selected")).toBe("true");
  });

  it("an arrow key that is NOT on a tab does nothing", () => {
    expect(press("ArrowRight").defaultPrevented).toBe(false);
  });
});

describe("the page obeys the shared silence rule", () => {
  beforeEach(bootMine);

  it("j does nothing while the quick-create offcanvas is open, and works again once it closes", () => {
    const panel = document.createElement("div");
    panel.className = "offcanvas show";
    document.body.appendChild(panel);
    press("j");
    expect(selectedRow()).toBeNull();
    panel.classList.remove("show");
    press("j");
    expect(selectedRow()).toBe(ID(1));
  });

  it("j typed into the search box is a character, not a move", () => {
    const search = app().querySelector("[data-wcn-search]") || (() => {
      const input = document.createElement("input");
      app().appendChild(input);
      return input;
    })();
    expect(press("j", search).defaultPrevented).toBe(false);
    expect(selectedRow()).toBeNull();
  });
});

describe("the list is the one the layer generates", () => {
  beforeEach(bootMine);

  it("registers every WorkCenterNext key with the layer", () => {
    const scope = window.DitenShortcuts.list().find((group) => group.scope === "workcenter");
    expect(scope.entries.map((entry) => [entry.actionKey, entry.keys.join(",")])).toEqual([
      ["Wcn.Tabs", "arrowleft,arrowright,home,end"],
      ["Wcn.Next", "j"],
      ["Wcn.Previous", "k"],
      ["Wcn.Open", "enter,o"],
      ["Wcn.OpenFocused", " "],
      ["Wcn.Accept", "a"],
      ["Wcn.Reject", "r"],
      ["Wcn.ClearSelection", "escape"]
    ]);
  });

  it("the toolbar's keyboard button shows at every width and opens that list", async () => {
    const button = app().querySelector("[data-wcn-shortcuts]");
    expect(button).not.toBeNull();
    expect(button.closest(".d-none")).toBeNull();
    expect(button.getAttribute("aria-label")).toBe("KeyboardHint");
    expect(app().querySelector(".wcn-keyboard-menu")).toBeNull();
    const open = vi.spyOn(window.DitenShortcuts, "open").mockImplementation(() => null);
    button.click();
    await tick();   // app.js's click handler is async
    expect(open).toHaveBeenCalledTimes(1);
  });

  it("a second boot replaces the scope rather than stacking a second copy", async () => {
    await bootMine();
    const scopes = window.DitenShortcuts.list().filter((group) => group.scope === "workcenter");
    expect(scopes.length).toBe(1);
    press("j");
    press("j");
    expect(selectedRow()).toBe(ID(2));
  });
});

describe("app.js keeps no shortcut listener of its own", () => {
  it("its one keydown listener is the in-field commit handler, and it holds no shortcut", () => {
    const listeners = APP.match(/addEventListener\('keydown'/g) || [];
    expect(listeners.length).toBe(1);
    expect(APP).toContain("document.addEventListener('keydown', onFieldKeydown)");
    const start = APP.indexOf("const onFieldKeydown = async (event) => {");
    const end = APP.indexOf("const SHORTCUT_SCOPE = 'workcenter';");
    expect(start).toBeGreaterThan(0);
    expect(end).toBeGreaterThan(start);
    const body = APP.slice(start, end);
    ["moveSelection", "key === 'j'", "key === 'k'", "openDetailPage", "actionByRole", "arrowleft"]
      .forEach((needle) => expect(body, `${needle} is back in the field handler`).not.toContain(needle));
  });
});
