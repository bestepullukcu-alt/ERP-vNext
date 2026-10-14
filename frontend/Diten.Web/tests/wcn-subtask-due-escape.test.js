const { loadScript } = require("./load-script");
const { bootSurface, app } = require("./wcn-boot");

/*
 * WCN-FIX-05 · 1, END TO END on the real Task Center: the "new subtask" panel's due date (#wcnNewSubtaskDue,
 * data-wcn-newsubtask-field="dueAt") is copied into the panel's draft by app.js's document-level `input` / `change`
 * handlers, and Save sends `dueAt: draft.dueAt`. The field is the shared DitenDateField (real flatpickr + guard).
 *
 * The defect: the reader types a misparse, switches windows (flatpickr's blur commits 2026-03-03 and dispatches
 * change + input → the draft holds it), comes back and presses Escape. The box showed the old date again — the draft
 * did not, and Save sent the misparse. Escape's restore is now announced, so the draft follows the box.
 */

const TASK_ID = "98d1f94e-1848-4539-8a99-774e72651b8a";
const nextTask = () => new Promise((resolve) => setTimeout(resolve, 0));

const projectionItem = () => ({
  fixtureKind: "workItem",
  id: TASK_ID,
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
  title: { kind: "display", text: "Parent", locale: "und" },
  nativeStatus: { code: "InProgress", label: { kind: "resource", key: "WorkAggregation_TaskStatus_InProgress" } },
  source: { providerCode: "tasks", providerContractVersion: "1.0", objectType: "task", objectId: TASK_ID, deepLink: `/Tasks/${TASK_ID}` },
  assignee: { id: "dddddddd-dddd-dddd-dddd-dddddddddddd", isCurrentUser: true },
  requester: { id: "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee", isCurrentUser: false },
  lifecycleOwner: "tasks",
  workItemCapabilities: ["planning", "execution", "subtasks"],
  subtasks: { mode: "full", items: [] },
  actions: [],
  concurrency: { kind: "version", token: "1" },
  waitingContext: null,
  escalation: null,
  dueAt: "2026-07-30T00:00:00+00:00"
});

const until = async (predicate, { timeout = 2000, step = 5 } = {}) => {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    if (predicate()) { return; }
    await new Promise((resolve) => setTimeout(resolve, step));
  }
  throw new Error("until(): condition never became true");
};

beforeAll(() => {
  global.self = global.self || window;
  Object.defineProperty(window, "ontouchstart", { value: undefined, configurable: true, writable: true });
  loadScript("wwwroot/assets/vendor/libs/flatpickr/flatpickr.js");
  loadScript("wwwroot/assets/js/shared/diten-datefield.js");
});

afterEach(() => {
  vi.restoreAllMocks();
  document.querySelectorAll(".flatpickr-calendar").forEach((cal) => cal.remove());
});

const openPanelWithRealDateField = async () => {
  if (typeof global.__wcnTeardown === "function") { global.__wcnTeardown(); }
  global.bootstrap = { Offcanvas: { getOrCreateInstance: () => ({ show: () => {}, hide: () => {} }), getInstance: () => null } };
  const recorded = await bootSurface({
    rootAttrs: `data-wcn-page="detail" data-wcn-item-id="${TASK_ID}"`,
    items: [projectionItem()]
  });
  global.TasksApi.assignablePeople = () => Promise.resolve({ ok: true, data: [] });
  // The REAL date enhancer the host view wires (Tasks/form.js enhanceDates → DitenDateField.enhance).
  global.TaskForm = Object.assign({}, global.TaskForm, {
    enhanceSelects: () => {},
    enhanceDates: (root) => window.DitenDateField.enhance(root)
  });
  app().querySelector("[data-wcn-subtask-add-detailed]").click();
  await until(() => !!(document.getElementById("wcnNewSubtaskDue") && document.getElementById("wcnNewSubtaskDue")._flatpickr));
  const title = document.querySelector('[data-wcn-newsubtask-field="title"]');
  title.value = "Ekstre";
  title.dispatchEvent(new window.Event("input", { bubbles: true }));
  return recorded;
};

describe("Task Center new-subtask panel: Escape after a window switch never saves the misparse", () => {
  test("pick 2026-10-09 → type 2026-02-31 → window switch → back → Escape → Save sends 2026-10-09", async () => {
    const { created } = await openPanelWithRealDateField();
    const due = document.getElementById("wcnNewSubtaskDue");
    const fp = due._flatpickr;
    expect(fp._ditenGuarded, "the panel's date is not the guarded shared field").toBe(true);

    fp.setDate("2026-10-09", true); // what a calendar pick announces to the draft
    due.focus();
    fp.open();
    due.value = "2026-02-31";
    due.dispatchEvent(new window.Event("input", { bubbles: true }));
    vi.spyOn(document, "hasFocus").mockReturnValue(false);           // the window switch
    due.dispatchEvent(new window.FocusEvent("blur", { relatedTarget: null }));
    await nextTask();
    vi.restoreAllMocks();                                             // back in the window
    due.dispatchEvent(new window.KeyboardEvent("keydown", { key: "Escape", keyCode: 27, bubbles: true }));

    expect(due.value).toBe("2026-10-09");
    app().querySelector("[data-wcn-newsubtask-save]").click();
    await until(() => created.length > 0);
    expect(created[created.length - 1].dueAt, "Save sent what the box no longer shows").toBe("2026-10-09");
  });

  test("control: type a valid date and leave — Save sends the typed date (the draft is fed at all)", async () => {
    const { created } = await openPanelWithRealDateField();
    const due = document.getElementById("wcnNewSubtaskDue");
    due.focus();
    due._flatpickr.open();
    due.value = "2026-11-20";
    due.dispatchEvent(new window.Event("input", { bubbles: true }));
    document.querySelector('[data-wcn-newsubtask-field="title"]').focus();
    expect(due._flatpickr.isOpen).toBe(false);

    app().querySelector("[data-wcn-newsubtask-save]").click();
    await until(() => created.length > 0);
    expect(created[created.length - 1].dueAt).toBe("2026-11-20");
  });
});
