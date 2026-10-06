const fs = require("fs");
const path = require("path");

/*
 * WP-MG-MOD0357-S12-EXPORT-WORDING-01 — measured inconsistency (WP-CT-DECISION-BENCHMARK-01): the SCREEN's
 * own `ExportSnapshotNote` (MeetingReportIndex.*.resx) said "not a controlled document" while the exported
 * FILE's own disclaimer (Diten.Platform's MeetingReportExportDisclaimer.cs) said "not a controlled copy" —
 * same feature, two different terms, in all seven languages. ISO 9001 §7.5.3 cares about the term
 * "uncontrolled copy" specifically; "document" is the wrong word here.
 *
 * These files live in two different services (Diten.Web / Diten.Platform) with no shared runtime l10n
 * mechanism, so there is no single source file to import from either side. This test is the guard that
 * stands in for that: it reads BOTH real files as text (same discipline `meeting-report-markup-contract.test.js`
 * already uses — a fixture proves nothing about what ships) and pins the same term in both, in all seven
 * languages, never the word "document"/"belge" or its per-language equivalent.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const RESX_DIR = path.join(repoRoot, "frontend", "Diten.Web", "Resources", "Views", "Meetings", "Report");
const DISCLAIMER_CS = path.join(
  repoRoot,
  "services",
  "Diten.Platform",
  "src",
  "Diten.Platform.Application",
  "Features",
  "Meetings",
  "Services",
  "MeetingReportExportDisclaimer.cs"
);

const LANGUAGE_TERMS = {
  en: { required: "controlled copy", forbidden: "controlled document" },
  tr: { required: "kontrollü kopya", forbidden: "belge" },
  fr: { required: "copie contrôlée", forbidden: "document contrôlé" },
  es: { required: "copia controlada", forbidden: "documento controlado" },
  zh: { required: "受控副本", forbidden: "受控文档" },
  ar: { required: "نسخة خاضعة للرقابة", forbidden: "مستند" },
  ru: { required: "контролируемой копией", forbidden: "контролируемый документ" },
};

const LANGUAGES = Object.keys(LANGUAGE_TERMS);

function screenNote(language) {
  const text = fs.readFileSync(path.join(RESX_DIR, `MeetingReportIndex.${language}.resx`), "utf8");
  const match = text.match(/<data name="ExportSnapshotNote"[^>]*><value>([^<]*)<\/value>/);
  if (!match) {
    throw new Error(`ExportSnapshotNote not found in MeetingReportIndex.${language}.resx`);
  }
  return match[1];
}

function fileSentence(language) {
  const text = fs.readFileSync(DISCLAIMER_CS, "utf8");
  const match = text.match(new RegExp(`\\["${language}"\\]\\s*=\\s*"([^"]*)"`));
  if (!match) {
    throw new Error(`["${language}"] entry not found in MeetingReportExportDisclaimer.cs`);
  }
  return match[1];
}

describe("MOD-0357 S12: the export screen's own snapshot note says \"controlled copy\", in all seven languages", () => {
  it.each(LANGUAGES)("%s: screen note carries the required term and never the forbidden one", (language) => {
    const { required, forbidden } = LANGUAGE_TERMS[language];
    const note = screenNote(language);

    expect(note).toContain(required);
    expect(note).not.toContain(forbidden);
  });
});

describe("MOD-0357 S12: the exported file's own disclaimer says \"controlled copy\", in all seven languages", () => {
  it.each(LANGUAGES)("%s: file disclaimer carries the required term and never the forbidden one", (language) => {
    const { required, forbidden } = LANGUAGE_TERMS[language];
    const sentence = fileSentence(language);

    expect(sentence).toContain(required);
    expect(sentence).not.toContain(forbidden);
  });
});

describe("MOD-0357 S12: the screen and the exported file agree on the SAME term, per language", () => {
  it.each(LANGUAGES)("%s: screen note and file disclaimer use the same controlled-copy term", (language) => {
    const { required } = LANGUAGE_TERMS[language];

    expect(screenNote(language)).toContain(required);
    expect(fileSentence(language)).toContain(required);
  });
});
