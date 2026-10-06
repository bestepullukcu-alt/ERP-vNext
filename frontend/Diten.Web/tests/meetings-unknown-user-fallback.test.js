const fs = require("fs");
const path = require("path");

/*
 * BL-390 — the Meetings list's Düzenleyen (organizer) column showed a raw GUID
 * (22222222-2222-2222-2222-222222222222) for an organizer whose account the eligible-people lookup no longer
 * resolves (deleted, or a test identity). "No GUID on screen" is a product-wide rule; every place in the
 * Meetings module that resolves a userId to a display name had the SAME `name || id` fallback shape, so the
 * fix — and this guard — covers all of them: the list's filter dropdown and DataTable column (index.js), the
 * Details page's organizer field and attendee list (form.js, BL-390's own "Detay sayfasında" instruction), and
 * the minutes/tutanak attendance list plus its publish byline (minutes-editor.js, "tutanak katılım listesinde").
 *
 * Each site is read straight from the source and asserted against the localized fallback (`L.UnknownUser` /
 * `t('unknownUser')` / `tShared('unknownUser')`, per which l10n bridge that file already uses) — and asserted
 * to NO LONGER contain the raw-id fallback that produced the dev repro.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const js = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", "wwwroot", "assets", "js", "Meetings", ...parts);
const INDEX_JS = js("index.js");
const FORM_JS = js("form.js");
const MINUTES_JS = js("minutes-editor.js");
const INDEX_L10N_CSHTML = path.join(repoRoot, "frontend", "Diten.Web", "Views", "Meetings", "_IndexL10n.cshtml");
const RESX_DIR = path.join(repoRoot, "frontend", "Diten.Web", "Resources", "Views", "Meetings");
const LANGUAGES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

const read = (file) => fs.readFileSync(file, "utf8");

describe("BL-390: an unresolved organizer/attendee never renders as a raw GUID", () => {
  test("index.js: the organizer filter option AND the DataTable column both fall back to L.UnknownUser", () => {
    const source = read(INDEX_JS);
    expect(source).toContain("organizerNamesById[id] || L.UnknownUser");
    expect(source).toContain("organizerNamesById[data] || L.UnknownUser");
    expect(source).toContain("p.displayName || L.UnknownUser");
    // The exact shape BL-390's dev repro measured — must be gone, not just supplemented.
    expect(source).not.toContain("organizerNamesById[id] || id");
    expect(source).not.toContain("organizerNamesById[data] || data");
    expect(source).not.toContain("p.displayName || p.userId");
  });

  test("index.js: L.UnknownUser is itself sourced from the resx bridge, not a hardcoded string", () => {
    const source = read(INDEX_JS);
    expect(source).toContain("UnknownUser: t('unknownUser')");
  });

  test("form.js (Details page): the organizer field and the attendee list both fall back to t('unknownUser')", () => {
    const source = read(FORM_JS);
    expect(source).toContain("eligiblePeopleById[meeting.organizerUserId] || t('unknownUser')");
    expect(source).toContain("eligiblePeopleById[a.userId] || t('unknownUser')");
    expect(source).toContain("eligiblePeopleById[p.userId] = p.displayName || t('unknownUser')");
    expect(source).not.toContain("eligiblePeopleById[meeting.organizerUserId] || meeting.organizerUserId");
    expect(source).not.toContain("eligiblePeopleById[a.userId] || a.userId");
  });

  test("minutes-editor.js (tutanak katılım listesi): attendance names and the publish byline both fall back to the shared label", () => {
    const source = read(MINUTES_JS);
    expect(source).toContain("eligiblePeopleById[userId] || tShared('unknownUser')");
    expect(source).toContain("eligiblePeopleById[p.userId] = p.displayName || tShared('unknownUser')");
    expect(source).toContain("v.publishedByDisplayName || tShared('unknownUser')");
    expect(source).not.toContain("eligiblePeopleById[userId] || userId");
    expect(source).not.toContain("v.publishedByDisplayName || v.publishedByUserId");
  });

  test("the shared l10n payload exposes UnknownUser to every page that loads it (Index/Create/Edit/Details)", () => {
    const payload = read(INDEX_L10N_CSHTML);
    expect(payload).toContain(`UnknownUser = Localizer["UnknownUser"].Value`);
  });

  test.each(LANGUAGES)("UnknownUser is translated in %s", (language) => {
    const file = path.join(RESX_DIR, `MeetingsIndex.${language}.resx`);
    expect(read(file)).toContain(`name="UnknownUser"`);
  });
});
