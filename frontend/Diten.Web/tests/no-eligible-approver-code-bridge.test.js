const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * WP-WF-ENGINE-CANDIDATES-01 D2-FIX1 — three refusals reach the reader as a sentence in their own language, and as a
 * RULE (blocking), not "somebody changed it first". Every link measured on the production files:
 *   C# constant → Tasks/api.js REASON_CODE_MESSAGE_KEYS + BLOCKING_REASON_CODES → Views/Tasks/_IndexL10n.cshtml →
 *   TasksIndex.{7}.resx
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const WEB = path.join(repoRoot, "frontend", "Diten.Web");
const FEATURES = path.join(repoRoot, "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features");
const LANGUAGES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const CODES = [
  ["WORKFLOW_NO_ELIGIBLE_APPROVER", path.join(FEATURES, "Workflow", "WorkflowModels.cs")],
  ["TASK_APPROVAL_CANCEL_FAILED", path.join(FEATURES, "Tasks", "TaskModels.cs")],
  ["TASK_REVIEW_CANCEL_FAILED", path.join(FEATURES, "Tasks", "TaskModels.cs")]
];

const resxValue = (file, key) => {
  const match = fs.readFileSync(file, "utf8").match(new RegExp(`<data name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`));
  return match ? match[1].trim() : null;
};

describe("D2-FIX1: no eligible approver / withdraw failed ⇔ bridge ⇔ payload ⇔ resx", () => {
  beforeEach(() => {
    delete global.TasksApi;
    global.TasksL10n = { t: (key) => key };
    loadScript("wwwroot/assets/js/Tasks/api.js");
  });

  it.each(CODES)("%s is a server constant, mapped, blocking, in the payload and translated ×7", (code, source) => {
    expect(fs.readFileSync(source, "utf8")).toMatch(new RegExp(`public const string \\w+ = "${code}";`));

    const key = global.TasksApi.REASON_CODE_MESSAGE_KEYS[code];
    expect(key, `${code} is not mapped in Tasks/api.js`).toBeTruthy();
    expect(global.TasksApi.isTransitionBlocked({ status: 409, reasonCode: code }), `${code} is not blocking`).toBe(true);
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
});
