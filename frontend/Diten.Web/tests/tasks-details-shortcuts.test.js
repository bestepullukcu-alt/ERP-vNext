const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * WP-UI-SHORTCUTS-01 (BL-438) — the task detail page's keys: `e` presses the header's Edit link, `Esc` its Back
 * link. Both are links the Razor header already draws; the key presses THEM, so it can never reach somewhere a
 * click could not. The page is booted from its real script against the markup Views/Tasks/Details.cshtml emits.
 */
const ROOT = path.resolve(__dirname, "..");
const VIEW = fs.readFileSync(path.join(ROOT, "Views/Tasks/Details.cshtml"), "utf8");
const FORM_VIEWS = ["Create", "Edit"].map((name) => [name, fs.readFileSync(path.join(ROOT, `Views/Tasks/${name}.cshtml`), "utf8")]);
const TASK_ID = "98d1f94e-1848-4539-8a99-774e72651b8a";

const tick = () => new Promise((resolve) => setTimeout(resolve, 0));
const press = (key, target = document.body) => {
  const event = new window.KeyboardEvent("keydown", { key, bubbles: true, cancelable: true });
  target.dispatchEvent(event);
  return event;
};

const boot = async ({ withEdit = true } = {}) => {
  if (window.DitenShortcuts) { window.DitenShortcuts.__uninstall(); }
  delete window.DitenShortcuts;
  document.body.innerHTML = `
    <a href="/Tasks" class="btn btn-label-secondary" data-task-back>Back</a>
    ${withEdit ? `<a href="/Tasks/${TASK_ID}/Edit" class="btn btn-primary" data-task-edit>Edit</a>` : ""}
    <div id="taskDetails" data-task-id="${TASK_ID}"><section class="card backbone-preview-section p-4"></section></div>`;
  window.TasksApi = { get: () => Promise.resolve({ ok: true, status: 200, data: { title: "T", lifecycle: "Open" } }) };
  loadScript("wwwroot/assets/js/shared/diten-shortcuts.js");
  loadScript("wwwroot/assets/js/Tasks/details-page.js");
  await tick();
  const clicks = [];
  document.querySelectorAll("a").forEach((link) => link.addEventListener("click", (event) => {
    event.preventDefault();   // jsdom cannot navigate; the click reaching the link is the measurement
    clicks.push(link.hasAttribute("data-task-edit") ? "edit" : "back");
  }));
  return clicks;
};

afterEach(() => {
  if (window.DitenShortcuts) { window.DitenShortcuts.__uninstall(); }
  delete window.DitenShortcuts;
  delete window.TasksApi;
});

describe("the task detail page's shortcuts", () => {
  it("e presses Edit and Escape presses Back", async () => {
    const clicks = await boot();
    expect(press("e").defaultPrevented).toBe(true);
    expect(press("Escape").defaultPrevented).toBe(true);
    expect(clicks).toEqual(["edit", "back"]);
  });

  it("does nothing while a dialog is open or while typing", async () => {
    const clicks = await boot();
    document.body.insertAdjacentHTML("beforeend", "<div class=\"swal2-container\"></div>");
    press("e");
    document.querySelector(".swal2-container").remove();
    document.body.insertAdjacentHTML("beforeend", "<input id=\"f\">");
    press("e", document.getElementById("f"));
    expect(clicks).toEqual([]);
  });

  it("with no Edit link on the page, e reaches nothing", async () => {
    const clicks = await boot({ withEdit: false });
    expect(press("e").defaultPrevented).toBe(false);
    expect(clicks).toEqual([]);
  });

  it("lists exactly those two, under the page's own scope", async () => {
    await boot();
    const scope = window.DitenShortcuts.list().find((group) => group.scope === "task-details");
    expect(scope.entries.map((entry) => [entry.actionKey, entry.keys.join(",")]))
      .toEqual([["Task.Edit", "e"], ["Task.Back", "escape"]]);
  });

  it("the Razor header carries the hooks the script presses", () => {
    expect(VIEW).toMatch(/href="\/Tasks" class="btn btn-label-secondary" data-task-back/);
    expect(VIEW).toMatch(/href="\/Tasks\/@taskId\/Edit" class="btn btn-primary" data-task-edit/);
    expect(VIEW).toContain('<partial name="~/Views/Shared/_DitenShortcuts.cshtml" />');
  });
});

describe("the task form pages get the list and nothing else", () => {
  /*
   * Create/Edit bind NO page key on purpose. Their only actions are Save — a letter would fire it from a
   * half-filled form — and Cancel, which leaves the page and discards what was typed; the page has no dirty
   * tracking to guard it. `?` still opens the list there, as everywhere.
   */
  it.each(FORM_VIEWS)("%s loads the shared layer", (_name, view) => {
    expect(view).toContain('<partial name="~/Views/Shared/_DitenShortcuts.cshtml" />');
  });

  it("form-page.js registers no shortcut", () => {
    const src = fs.readFileSync(path.join(ROOT, "wwwroot/assets/js/Tasks/form-page.js"), "utf8");
    expect(src).not.toContain("DitenShortcuts.register");
  });
});
