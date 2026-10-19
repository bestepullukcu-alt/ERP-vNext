const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * WP-WEB-TASK-UPDATE-HINT-01 (BL-341 step 1) — the "Update Task" chip (platform.tasks.update) grants far more than
 * editing fields: every TasksController endpoint guarded by TaskPermissions.Update rides on it (accept, decline,
 * withdraw, plan, unplan, start, submit for review, inquire, return, the checklist, attachments, dependencies). The
 * chip keeps its verb and tone; its tooltip lists what the key also grants, in seven languages.
 *
 * The hint is a hand-written list, so it can fall behind the controller. The guard counts the endpoints in the
 * PRODUCTION source and pins the count: a new or removed Update endpoint turns this red until the hint is reviewed.
 */
const WEB = path.resolve(__dirname, "..");
const REPO = path.resolve(WEB, "..", "..");
const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const KEY = "platform.tasks.update";
const HINT = "KeyHint_PlatformTasksUpdate";

const controller = fs.readFileSync(path.join(REPO, "services", "Diten.Platform", "src", "Diten.Platform.API", "Controllers",
  "TasksController.cs"), "utf8");
const bridge = fs.readFileSync(path.join(WEB, "Views", "Governance", "RoleAssignments", "_IndexL10n.cshtml"), "utf8");
const resx = (lang) => {
  const xml = fs.readFileSync(path.join(WEB, "Resources", "Views", "Governance", "RoleAssignments", `RoleAssignmentsIndex.${lang}.resx`), "utf8");
  return new Map([...xml.matchAll(/<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g)].map((m) => [m[1], m[2].trim()]));
};
const load = () => {
  delete window.PermLabel;
  loadScript("wwwroot/assets/js/Governance/RoleAssignments/perm-label.js");
  return window.PermLabel;
};

describe("the Update Task chip says everything it grants (BL-341 step 1)", () => {
  test("TasksController still guards exactly the 19 endpoints the hint lists", () => {
    const guarded = (controller.match(/HasPermission\(TaskPermissions\.Update\)/g) || []).length;
    expect(guarded, `TasksController now guards ${guarded} endpoints with TaskPermissions.Update, the hint lists 19 — `
      + "ipucunu gözden geçir (KeyHint_PlatformTasksUpdate, 7 dil), then update this count").toBe(19);
  });

  test("the hint is bridged for the key and written in all seven languages, eleven items each", () => {
    expect(bridge).toMatch(/\["platform\.tasks\.update"\]\s*=\s*Localizer\["KeyHint_PlatformTasksUpdate"\]\.Value/);
    LANGS.forEach((lang) => {
      const hint = resx(lang).get(HINT) || "";
      expect(hint.length, `${lang}/${HINT}`).toBeGreaterThan(100);
      expect((hint.match(/•/g) || []).length, `${lang}/${HINT} items`).toBe(11);
    });
    // translated, not copied
    const english = resx("en").get(HINT);
    expect(LANGS.filter((lang) => lang !== "en" && resx(lang).get(HINT) === english)).toEqual([]);
  });

  test("the chip keeps its verb and tone and carries the hint", () => {
    const PL = load();
    const values = resx("tr");
    const L = {
      ActionVerbs: { update: values.get("ActionVerb_Update") },
      KeyHints: { [KEY]: values.get(HINT) }
    };
    const chip = PL.split({ key: KEY, module: "tasks", resource: "tasks", action: "update" }, L);
    const plain = PL.split({ key: "platform.tasks.other.update", module: "tasks", resource: "tasks", action: "update" }, L);

    expect(chip.action.label).toBe("Güncelle");
    expect(chip.action.code).toBe("update");
    expect(chip.action.tone).toBe(plain.action.tone);
    expect(chip.action.icon).toBe(plain.action.icon);
    expect(chip.action.hint).toBe(values.get(HINT));
    expect(chip.action.hint.startsWith("Bu izin şunları da verir:")).toBe(true);
    expect(chip.scope).toBeNull();
  });
});
