const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * WP-WCN-TASK-REQUEST-01 (merge into the engine takeover) — the submit handler now answers an open review with
 * TaskReasonCodes.ReviewPending in PendingReview too (TaskSubmitReviewPendingTests pins the handler). This measures the
 * other half on the production files: the code the SERVER sends — read from TaskModels.cs, not typed here — reaches the
 * reader as "this task already has an open review" on submitReview, in all seven languages, through the bridge.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const WEB = path.join(repoRoot, "frontend", "Diten.Web");
const MODELS = path.join(repoRoot, "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features", "Tasks", "TaskModels.cs");
const LANGUAGES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

const serverCode = () => {
  const match = fs.readFileSync(MODELS, "utf8").match(/public const string ReviewPending = "([^"]+)";/);
  expect(match, "TaskReasonCodes.ReviewPending not found").toBeTruthy();
  return match[1];
};

const resxValue = (language, key) => {
  const file = path.join(WEB, "Resources", "Views", "Tasks", `TasksIndex.${language}.resx`);
  const match = fs.readFileSync(file, "utf8").match(new RegExp(`<data name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`));
  return match ? match[1].trim() : null;
};

describe("submitting while a review is open reads 'a review is already open'", () => {
  beforeEach(() => {
    delete global.TasksApi;
    global.TasksL10n = { t: (key) => `t:${key}` };
    loadScript("wwwroot/assets/js/Tasks/api.js");
  });

  it("the server's own REVIEW_PENDING, refused on submitReview, is the open-review sentence", () => {
    const refused = { ok: false, status: 409, reasonCode: serverCode() };
    expect(global.TasksApi.failureMessage(refused, global.TasksApi.reasonOverridesFor("submitReview")))
      .toBe("t:errorReviewAlreadyOpen");
    expect(global.TasksApi.isTransitionBlocked(refused)).toBe(true);
  });

  it("the sentence exists ×7 and through the bridge", () => {
    expect(fs.readFileSync(path.join(WEB, "Views", "Tasks", "_IndexL10n.cshtml"), "utf8"))
      .toContain('ErrorReviewAlreadyOpen = Localizer["ErrorReviewAlreadyOpen"]');
    const english = resxValue("en", "ErrorReviewAlreadyOpen");
    for (const language of LANGUAGES) {
      const value = resxValue(language, "ErrorReviewAlreadyOpen");
      expect(value, `missing in ${language}`).toBeTruthy();
      if (language !== "en") expect(value, `${language} is still English`).not.toBe(english);
    }
  });
});
