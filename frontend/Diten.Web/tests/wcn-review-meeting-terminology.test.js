const fs = require("fs");
const path = require("path");

/*
 * BL-389 — the Task Center's "Schedule a review meeting" action, its own success toast, and the notification
 * text it USED to draw its dialog title through (WorkAggregation_Action_ScheduleReviewMeeting — the action label
 * app.js's own actionLabel() actually renders, measured against app.js directly, not assumed) all said "review
 * meeting" already. ActReviewMeeting and ToastReviewMeeting were the ones out of step, calling it an "approval
 * meeting" in every language instead — a second name for the same thing, in the one place (the toast confirming
 * what was just scheduled) most likely to make a reader doubt what they just did.
 *
 * This guard reads the actual resx text, in all seven languages, and asserts the SAME term appears in all three
 * keys — never a hand-copied "expected" string that could drift from the resx the screen actually loads.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const RESX_DIR = path.join(repoRoot, "frontend", "Diten.Web", "Resources", "Views", "WorkCenterNext");
const APP_JS = path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "js", "WorkCenterNext", "app.js");

// The single term BL-389 asks for, one per language — the SAME word WorkAggregation_Action_ScheduleReviewMeeting
// (the label the screen actually renders) already used before this fix; ActReviewMeeting/ToastReviewMeeting are
// brought in line with it, not the other way around.
const REVIEW_MEETING_TERM = {
  en: "review meeting",
  tr: "İnceleme toplantısı",
  fr: "réunion de revue",
  es: "reunión de revisión",
  zh: "审查会议",
  ar: "اجتماع مراجعة",
  ru: "совещание по обзору"
};

const valueOf = (resxText, key) => {
  // Handles BOTH the single-line (<data name="X" ...><value>Y</value></data>) and multi-line resx shapes this
  // file mixes (see WorkCenterNextIndex.*.resx itself).
  const match = resxText.match(new RegExp(`name="${key}"[^]*?<value>([^<]*)</value>`));
  return match ? match[1] : null;
};

describe("BL-389: the review-meeting action, its toast, and its real screen label use ONE term per language", () => {
  it("actionLabel(action) is what the button/dialog title actually renders — the premise for measuring THIS key, not ActReviewMeeting", () => {
    const source = fs.readFileSync(APP_JS, "utf8");
    expect(source).toContain("const actionLabel = (action) => action?.displayLabel || (action?.labelKey ? t(action.labelKey) : '');");
    // ActReviewMeeting itself is dead — confirms fixing its wording is a terminology-consistency cleanup, not a
    // behaviour change to what the reader sees today.
    expect(source).not.toContain("t('ActReviewMeeting')");
    expect(source).not.toContain("tf('ActReviewMeeting'");
  });

  it.each(Object.entries(REVIEW_MEETING_TERM))(
    "%s: ActReviewMeeting, ToastReviewMeeting and WorkAggregation_Action_ScheduleReviewMeeting all carry the term",
    (language, term) => {
      const file = path.join(RESX_DIR, `WorkCenterNextIndex.${language}.resx`);
      const text = fs.readFileSync(file, "utf8");

      const act = valueOf(text, "ActReviewMeeting");
      const toast = valueOf(text, "ToastReviewMeeting");
      const real = valueOf(text, "WorkAggregation_Action_ScheduleReviewMeeting");

      expect(act, `ActReviewMeeting missing in ${language}`).not.toBeNull();
      expect(toast, `ToastReviewMeeting missing in ${language}`).not.toBeNull();
      expect(real, `WorkAggregation_Action_ScheduleReviewMeeting missing in ${language}`).not.toBeNull();

      // Case-insensitive: ToastReviewMeeting/WorkAggregation_Action_ScheduleReviewMeeting lead a sentence in
      // several languages (a capitalized first letter), which is a sentence-position artifact, not a different
      // term — BL-389 is about the WORD, not its casing.
      const lower = (value) => value.toLocaleLowerCase(language);
      expect(lower(act), `ActReviewMeeting (${language}): "${act}"`).toContain(lower(term));
      expect(lower(toast), `ToastReviewMeeting (${language}): "${toast}"`).toContain(lower(term));
      expect(lower(real), `WorkAggregation_Action_ScheduleReviewMeeting (${language}): "${real}"`).toContain(lower(term));
    }
  );
});
