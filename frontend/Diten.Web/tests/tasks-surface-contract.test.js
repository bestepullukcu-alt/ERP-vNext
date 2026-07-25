const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

const root = (...parts) => path.resolve(__dirname, "..", ...parts);
const read = (...parts) => fs.readFileSync(root(...parts), "utf8");

// MOD-0024 — the two decisions that are easy to undo by accident: the Task Center is the ONE task list and the
// ONE quick-create surface, and no Tasks dialog may be a bare SweetAlert2. Both are structural, so they are
// asserted against the source rather than a rendered page.
describe("MOD-0024 task surface contract", () => {
  describe("/Tasks renders no competing list", () => {
    const controller = read("Controllers", "TasksController.cs");

    it("redirects the index to the Task Center", () => {
      expect(controller).toMatch(/WorkCenterUrl\s*=\s*"\/WorkCenterNext"/);
      expect(controller).toMatch(/public IActionResult Index\(\)\s*=>\s*Redirect\(WorkCenterUrl\)/);
    });

    it("does not use a permanent redirect (browsers cache 301 forever)", () => {
      expect(controller).not.toMatch(/RedirectPermanent/);
    });

    it("no longer renders a list view", () => {
      expect(controller).not.toMatch(/Views\/Tasks\/Index\.cshtml/);
      expect(fs.existsSync(root("Views", "Tasks", "Index.cshtml"))).toBe(false);
      expect(fs.existsSync(root("Views", "Tasks", "_DataTable.cshtml"))).toBe(false);
      expect(fs.existsSync(root("Views", "Tasks", "_Filter.cshtml"))).toBe(false);
      expect(fs.existsSync(root("wwwroot", "assets", "js", "Tasks", "index.js"))).toBe(false);
    });

    it("keeps the detailed create/edit/details pages", () => {
      ["Create.cshtml", "Edit.cshtml", "Details.cshtml", "_Form.cshtml"].forEach((view) => {
        expect(fs.existsSync(root("Views", "Tasks", view))).toBe(true);
      });
      expect(controller).toMatch(/HttpGet\("Create"\)/);
      expect(controller).toMatch(/HttpGet\("\{id:guid\}"\)/);
      expect(controller).toMatch(/HttpGet\("\{id:guid\}\/Edit"\)/);
    });
  });

  describe("quick create exists in exactly one place", () => {
    it("is mounted on the Task Center", () => {
      const view = read("Views", "WorkCenterNext", "Index.cshtml");
      expect(view).toContain("~/Views/Tasks/_QuickCreateOffcanvas.cshtml");
      expect(view).toContain("WorkCenterNext/quick-create.js");
    });

    it("is not mounted anywhere else", () => {
      const views = fs.readdirSync(root("Views", "Tasks")).filter((f) => f.endsWith(".cshtml"));
      const mounting = views.filter((f) =>
        read("Views", "Tasks", f).includes("_QuickCreateOffcanvas.cshtml") &&
        !f.startsWith("_QuickCreateOffcanvas"));
      expect(mounting).toEqual([]);
    });
  });

  describe("saving returns to the Task Center", () => {
    it("sends the user to /WorkCenterNext after create or edit", () => {
      const formPage = read("wwwroot", "assets", "js", "Tasks", "form-page.js");
      expect(formPage).toMatch(/WORK_CENTER_URL\s*=\s*'\/WorkCenterNext'/);
      expect(formPage).toContain("global.location.href = WORK_CENTER_URL");
      // The old destination was the now-removed list.
      expect(formPage).not.toMatch(/location\.href = '\/Tasks'/);
    });
  });

  // MOD-0013: "standart, özelleştirilmemiş SweetAlert2 diyalogları kullanmak KESİNLİKLE YASAKTIR".
  describe("premium modal standard", () => {
    const surfaceFiles = [
      ...fs.readdirSync(root("wwwroot", "assets", "js", "Tasks"))
        .filter((f) => f.endsWith(".js"))
        .map((f) => ["wwwroot", "assets", "js", "Tasks", f]),
      ["wwwroot", "assets", "js", "WorkCenterNext", "quick-create.js"]
    ];

    it("has surface files to check (guards against a vacuous scan)", () => {
      expect(surfaceFiles.length).toBeGreaterThan(3);
    });

    it("calls no SweetAlert2 directly", () => {
      const offenders = surfaceFiles
        .filter((parts) => /\bSwal\b/.test(read(...parts)))
        .map((parts) => parts.at(-1));
      expect(offenders).toEqual([]);
    });

    it("uses no native alert() either", () => {
      const offenders = surfaceFiles
        .filter((parts) => /(^|[^.\w])alert\s*\(/.test(read(...parts)))
        .map((parts) => parts.at(-1));
      expect(offenders).toEqual([]);
    });

    it("routes dialogs through the shared helper", () => {
      const usesHelper = surfaceFiles.filter((parts) => read(...parts).includes("DitenModal."));
      expect(usesHelper.length).toBeGreaterThan(0);
    });
  });
});

describe("shared premium modal helper", () => {
  let fired;

  beforeEach(() => {
    delete global.DitenModal;
    fired = [];
    global.Swal = { fire: (config) => { fired.push(config); return Promise.resolve({ isConfirmed: true }); } };
    loadScript("wwwroot/assets/js/shared/premium-modal.js");
  });

  it("applies every chrome rule the standard requires", () => {
    global.DitenModal.error({ title: "Hata", message: "boom" });

    const config = fired[0];
    expect(config.padding).toBe("2.5rem 1.5rem 2rem");
    expect(config.buttonsStyling).toBe(false);
    expect(config.customClass.popup).toBe("rounded-4 shadow-lg");
    expect(config.customClass.confirmButton).toContain("btn btn-primary");
    // The default Swal icon animation must be replaced by the premium icon well.
    expect(config.iconHtml).toContain("swal-icon-circle");
    expect(config.icon).toBeUndefined();
  });

  it("uses class-based icon markup, never inline styles (FG-003)", () => {
    ["error", "success", "warning", "info"].forEach((type) => global.DitenModal[type]({ title: type }));

    fired.forEach((config) => {
      expect(config.iconHtml).toContain("swal-icon-circle");
      expect(config.iconHtml).not.toContain("style=");
    });
  });

  it("gives each type its own colour so the icon matches its meaning", () => {
    global.DitenModal.error({ title: "e" });
    global.DitenModal.success({ title: "s" });
    global.DitenModal.warning({ title: "w" });
    global.DitenModal.info({ title: "i" });

    expect(fired[0].iconHtml).toContain("text-danger");
    expect(fired[1].iconHtml).toContain("text-success");
    expect(fired[2].iconHtml).toContain("text-warning");
    expect(fired[3].iconHtml).toContain("text-primary");
  });

  it("escapes the message by default and trusts it only when asked", () => {
    global.DitenModal.error({ message: '<img src=x onerror="boom()">' });
    expect(fired[0].html).toContain("&lt;img");
    expect(fired[0].html).not.toContain("<img");

    global.DitenModal.info({ message: "<b>bold</b>", html: true });
    expect(fired[1].html).toContain("<b>bold</b>");
  });

  it("keeps the navbar from shifting when the popup opens", () => {
    global.DitenModal.info({ title: "x" });
    expect(fired[0].scrollbarPadding).toBe(false);
    expect(fired[0].heightAuto).toBe(false);
  });

  it("hides the button when used as a self-dismissing acknowledgement", () => {
    global.DitenModal.success({ title: "saved", timer: 1600 });
    expect(fired[0].timer).toBe(1600);
    expect(fired[0].showConfirmButton).toBe(false);
  });

  it("delegates confirmations to the one existing global instead of adding a second", () => {
    const seen = [];
    global.showConfirm = (title, cb, options) => { seen.push({ title, options }); cb?.(); };

    global.DitenModal.confirm("DeleteConfirmation", () => seen.push("confirmed"), { entityName: "X" });

    expect(seen[0].title).toBe("DeleteConfirmation");
    expect(seen).toContain("confirmed");
    // No SweetAlert of its own for confirmations.
    expect(fired).toEqual([]);
  });

  it("fails loudly rather than silently when SweetAlert2 is missing", async () => {
    delete global.Swal;
    const errors = [];
    const original = console.error;
    console.error = (...args) => errors.push(args.join(" "));

    const result = await global.DitenModal.error({ title: "t", message: "m" });

    console.error = original;
    expect(errors.join(" ")).toContain("DitenModal");
    expect(result.isConfirmed).toBe(false);
  });
});

// Fix for the silent degradation that cost hours of diagnosis: both fallbacks now announce themselves.
describe("MOD-0024 failures are never silent", () => {
  const read = (...parts) => fs.readFileSync(path.resolve(__dirname, "..", ...parts), "utf8");

  it("the quick-create fallback logs why it dropped to the full page", () => {
    const app = read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
    const openSelfTask = app.slice(app.indexOf("const openSelfTask"), app.indexOf("const createSelfTaskViaApi"));

    expect(openSelfTask).toContain("console.error");
    expect(openSelfTask).toContain("/Tasks/Create");
    // The message must say WHICH precondition failed, not just "failed".
    expect(openSelfTask).toContain("WcnQuickCreate is undefined");
    expect(openSelfTask).toContain("taskQuickCreate");
  });

  describe("a missing localization key is reported, not swallowed", () => {
    beforeEach(() => {
      delete global.TasksL10n;
      document.body.innerHTML =
        '<script id="tasks-l10n" type="application/json">{"errorOccurred":"Bir hata"}</script>';
      loadScript("wwwroot/assets/js/Tasks/index.l10n.js");
    });

    it("resolves a known key without complaining", () => {
      const errors = [];
      const original = console.error;
      console.error = (...args) => errors.push(args.join(" "));

      expect(global.TasksL10n.t("errorOccurred")).toBe("Bir hata");

      console.error = original;
      expect(errors).toEqual([]);
    });

    it("logs a miss and hints at the casing convention", () => {
      const errors = [];
      const original = console.error;
      console.error = (...args) => errors.push(args.join(" "));

      const value = global.TasksL10n.t("ErrorOccurred");

      console.error = original;
      // Still returns something renderable...
      expect(value).toBe("ErrorOccurred");
      // ...but the mismatch is now impossible to miss, with the fix spelled out.
      expect(errors.join(" ")).toContain("Missing localization key 'ErrorOccurred'");
      expect(errors.join(" ")).toContain("errorOccurred");
    });

    it("reports each missing key once so a re-render cannot flood the console", () => {
      const errors = [];
      const original = console.error;
      console.error = (...args) => errors.push(args.join(" "));

      global.TasksL10n.t("nope");
      global.TasksL10n.t("nope");
      global.TasksL10n.t("nope");

      console.error = original;
      expect(errors).toHaveLength(1);
    });
  });
});
