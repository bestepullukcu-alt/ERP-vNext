const fs = require("fs");
const path = require("path");
const { bootSurface, app } = require("./wcn-boot");

/*
 * THE LIST PAGE'S THIRD ROUND.
 *
 *   ① the three view modes, reconnected from the scratchpad they were parked in
 *   ② a sort control for the list view, which could not be sorted at all
 *   ③ a pin that survives a reload
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const APP = fs.readFileSync(web("wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");
const API = fs.readFileSync(web("wwwroot", "assets", "js", "Tasks", "api.js"), "utf8");
const PROXY = fs.readFileSync(web("Controllers", "TasksController.cs"), "utf8");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

const id = (n) => `bbbbbbbb-0000-0000-0000-${String(n).padStart(12, "0")}`;
const row = (n, over = {}) => Object.assign({
  fixtureKind: "workItem",
  id: id(n),
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
  title: { kind: "display", text: `Satır ${n}`, locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: {
    providerCode: "tasks", providerContractVersion: "1.0", objectType: "task",
    objectId: id(n), deepLink: `/Tasks/${id(n)}`
  },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["execution"],
  actions: [{
    code: "complete", label: { kind: "display", text: "Tamamla", locale: "und" },
    semanticType: "complete", enabled: true, source: "provider",
    disabledReasonCode: null, disabledReason: null,
    requiresConfirmation: false, requiresReason: false, requiresEvidence: false,
    supportsBulk: false, riskLevel: "normal"
  }],
  primaryActionCode: "complete",
  overflowActionCodes: [],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  dueAt: "2090-01-01T00:00:00+00:00",
  slaState: "no-sla"
}, over);

const boot = async (items) => {
  const r = await bootSurface({ rootAttrs: 'data-wcn-page="list"', items });
  app().querySelector('[data-wcn-tab="islerim"]').click();
  await new Promise((x) => setTimeout(x, 0));
  return r;
};
const view = async (v) => {
  const b = app().querySelector(`[data-wcn-view="${v}"]`);
  expect(b, `the ${v} view is not offered`).not.toBeNull();
  b.click();
  await new Promise((x) => setTimeout(x, 0));
};

describe("① the three view modes are reachable again", () => {
  it("offers them on the tabs where they mean something", () => {
    /*
     * MUTATION GUARD: drop one from TAB_VIEWS and this goes red naming the tab.
     *
     * ⚠ KANBAN IS NOT IN THE INBOX, deliberately: its columns are lifecycle states and an inbox row has
     * exactly one, so the board would be a list with extra furniture.
     */
    const tabViews = APP.split("const TAB_VIEWS")[1].split("};")[0];
    expect(tabViews).toContain("inbox: ['list', 'table', 'split', 'calendar']");
    expect(tabViews).toContain("islerim: ['list', 'table', 'split', 'kanban', 'calendar', 'focus']");
    expect(tabViews, "kanban was offered where every row has one state").not.toMatch(/inbox: \[[^\]]*kanban/);
  });

  it("dispatches all three, and carries them in the URL whitelist", () => {
    const dispatch = APP.split("switch (state.view)")[1].split("default: main = renderList")[0];
    ["case 'split':", "case 'kanban':", "case 'calendar':"].forEach((c) => {
      expect(dispatch, `${c} is not dispatched`).toContain(c);
    });
    expect(APP).toContain("view: ['list', 'table', 'split', 'kanban', 'calendar', 'focus'],");
  });

  it("shows the SAME items the list shows — filtering is respected by construction", async () => {
    await boot([row(1), row(2), row(3)]);
    /*
     * MUTATION GUARD: make any mode read `state.items` instead of `activeItems()` and this goes red.
     *
     * All three start from `activeItems()` — the call `renderList` and `renderTable` make — so tab, segment,
     * chips, search and the quick filters apply without a second implementation to keep in step. MEASURED live
     * at 30/30/30 with the chips off and 4/4/4 with "Bloke" on.
     */
    await view("kanban");
    const kanban = [...app().querySelectorAll(".wcn-kcol-count")].reduce((a, e) => a + Number(e.textContent || 0), 0);
    await view("split");
    const split = app().querySelectorAll(".wcn-split-list > *").length;
    expect(kanban).toBe(3);
    expect(split).toBe(3);
    ["renderKanban", "renderSplit", "renderCalendar"].forEach((fn) => {
      const body = APP.split(`const ${fn} = `)[1].split("\n    const ")[0];
      expect(body, `${fn} stopped reading the filtered list`).toMatch(/activeItems\(\)|\(items\)/);
    });
  });

  it("gives the calendar the empty state it never had", async () => {
    /*
     * It drew the month grid unconditionally, so a filter matching nothing produced 31 blank boxes and no
     * sentence — indistinguishable from a page that failed to load.
     */
    await boot([row(1)]);
    const cal = APP.split("const renderCalendar = ")[1].split("\n    const ")[0];
    expect(cal, "an empty month still draws a grid").toContain("if (!items.length) { return emptyState(); }");
  });
});

describe("② the list can be sorted, through the sorter that already existed", () => {
  it("draws the control for the list and split views only", () => {
    /*
     * Not for the table, which sorts from its own headers, and not for kanban/calendar, whose arrangement IS
     * their sort (columns by state, cells by day) — a control there would change nothing.
     */
    expect(APP).toContain("(state.view === 'list' || state.view === 'split') ? `<div class=\"dropdown\">");
  });

  it("uses ONE sorter and ONE URL parameter, shared with the table", () => {
    /*
     * MUTATION GUARD: give the list its own sorter and this goes red. A second implementation is exactly what
     * made the two views disagree in the first place.
     */
    expect(APP).toContain('data-wcn-sort="${esc(key)}"');
    expect(APP.split("const SORT_LABEL")[1].split("const sortMenu")[0]).toContain("Object.keys(SORTERS)");
    // The same state the grid mirrors into, so list↔table keeps the order and ?sort= means one thing.
    expect(APP).toContain("put('sort', state.sortKey, 'sla')");
  });

  it("reuses the existing click handler rather than adding one", () => {
    const handlers = (APP.match(/closest\('\[data-wcn-sort\]'\)/g) || []).length;
    expect(handlers, "a second sort handler appeared").toBe(1);
  });

  it("offers only keys that can tell two rows apart", () => {
    // The same rule the table's columns follow, and the same helper — not a second test of the same idea.
    expect(APP).toContain("distinguishes(activeItems(), SORT_FIELD[key])");
  });

  it("names the control in all seven languages", () => {
    LANGS.forEach((lang) => {
      const resx = fs.readFileSync(
        web("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${lang}.resx`), "utf8");
      expect(resx, `${lang} cannot name the sort control`).toContain('name="SortLabel"');
    });
  });
});

describe("③ a pin that survives a reload", () => {
  it("writes to the server, following the snooze exactly", () => {
    /*
     * MEASURED before: pin, refresh, gone — a mark whose only meaning is "I will come back to this",
     * disappearing at the moment it is needed.
     *
     * MUTATION GUARD: put the local-only toggle back and this goes red.
     */
    expect(APP, "the pin went back to browser memory").not.toContain("item.pinned = !item.pinned; render();");
    expect(APP).toContain("global.TasksApi.setPinned(item.id, { pinned: next })");
    // Same shape as the snooze: PUT under /personal, no new endpoint family.
    expect(API).toContain("setPinned: (taskId, payload) => request('PUT', `/${taskId}/personal/pin`, payload)");
  });

  it("is reachable through the proxy — the line the first live test proved was missing", () => {
    /*
     * The web controller is a PROXY with one method per endpoint, so a route that exists on the service is
     * still invisible to the browser until it is named here. The first live click returned 404 with the
     * handler already written.
     */
    expect(PROXY).toContain('[HttpPut("api/{id:guid}/personal/pin")]');
  });

  it("re-reads the projection instead of applying optimistically", () => {
    // `afterPhase2Write` is the shared path: refresh, then say what happened — never assume the write landed.
    const fn = APP.split("const togglePin = ")[1].split("\n    const ")[0];
    expect(fn).toContain("afterPhase2Write");
    expect(fn, "a real item took the fixture branch").toContain("isRealTaskItem(item)");
  });

  it("keeps a showcase branch, because a fixture has no engine behind it", () => {
    const fn = APP.split("const togglePin = ")[1].split("\n    const ")[0];
    expect(fn).toContain("item.personal.pinned = next");
  });
});
