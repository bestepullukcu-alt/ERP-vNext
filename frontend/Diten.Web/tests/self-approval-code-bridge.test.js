const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * WP-WORKFLOW-APPROVAL-STATUS-01 (B2 follow-up) — the refusals and the disabled reason for "you started it, you cannot
 * decide it" reach the reader as a sentence in their own language. Every link measured on the production file:
 *   C# constant → Tasks/api.js REASON_CODE_MESSAGE_KEYS → Views/Tasks/_IndexL10n.cshtml → TasksIndex.{7}.resx
 *   and the Task Center's disabled reason → WorkCenterNextIndex.{7}.resx (that page emits its whole resx).
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const WEB = path.join(repoRoot, "frontend", "Diten.Web");
const FEATURES = path.join(repoRoot, "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features");
const LANGUAGES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const REFUSALS = ["TASK_APPROVAL_MANAGER_IS_SELF", "TASK_REVIEWER_IS_SUBMITTER"];

const resxValue = (file, key) => {
  const match = fs.readFileSync(file, "utf8").match(new RegExp(`<data name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`));
  return match ? match[1].trim() : null;
};

describe("B2: self-routing refusals ⇔ bridge ⇔ payload ⇔ resx", () => {
  beforeEach(() => {
    delete global.TasksApi;
    global.TasksL10n = { t: (key) => key };
    loadScript("wwwroot/assets/js/Tasks/api.js");
  });

  it.each(REFUSALS)("%s is a server constant, mapped, in the payload and translated ×7", (code) => {
    expect(fs.readFileSync(path.join(FEATURES, "Tasks", "TaskModels.cs"), "utf8"))
      .toMatch(new RegExp(`public const string \\w+ = "${code}";`));

    const key = global.TasksApi.REASON_CODE_MESSAGE_KEYS[code];
    expect(key, `${code} is not mapped in Tasks/api.js`).toBeTruthy();
    const name = key.charAt(0).toUpperCase() + key.slice(1);
    expect(fs.readFileSync(path.join(WEB, "Views", "Tasks", "_IndexL10n.cshtml"), "utf8"))
      .toContain(`${name} = Localizer["${name}"]`);

    const english = resxValue(path.join(WEB, "Resources", "Views", "Tasks", "TasksIndex.en.resx"), name);
    for (const language of LANGUAGES) {
      const value = resxValue(path.join(WEB, "Resources", "Views", "Tasks", `TasksIndex.${language}.resx`), name);
      expect(value, `${name} missing in ${language}`).toBeTruthy();
      expect(value).not.toBe(name);
      if (language !== "en") expect(value, `${name} in ${language} is still English`).not.toBe(english);
    }
  });

  it("SELF_APPROVAL_NOT_ALLOWED is a server constant and its disabled reason is translated ×7", () => {
    expect(fs.readFileSync(path.join(FEATURES, "WorkAggregation", "WorkAggregationModels.cs"), "utf8"))
      .toMatch(/public const string \w+ = "SELF_APPROVAL_NOT_ALLOWED";/);
    const key = "WorkAggregation_ActionDisabled_SelfApproval";
    expect(fs.readFileSync(path.join(FEATURES, "WorkAggregation", "Services", "WorkItemProjectionService.cs"), "utf8"))
      .toContain(`"${key}"`);
    const english = resxValue(path.join(WEB, "Resources", "Views", "WorkCenterNext", "WorkCenterNextIndex.en.resx"), key);
    for (const language of LANGUAGES) {
      const value = resxValue(path.join(WEB, "Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${language}.resx`), key);
      expect(value, `${key} missing in ${language}`).toBeTruthy();
      if (language !== "en") expect(value).not.toBe(english);
    }
  });
});
