/**
 * The middle checklist level must not claim to be mandatory.
 *
 * Three levels ship and the model is consistent: Optional does nothing, the middle one ASKS on completion, and
 * Blocking stops completion. The middle one was called "Required" — and in every language we ship, that word
 * means mandatory. The screen therefore said two contradictory things a few pixels apart: "this list does not
 * block completion", and directly beneath it "4 required items open". Readers believed the word.
 *
 * The defence used to live in the STYLESHEET — the notice was styled not to look like the blocking notice. A
 * style cannot outvote a word. So the word changed, in all seven languages, on both screens that show it.
 *
 * What did NOT change, and what this file also pins: the key name, the wire value and the enum. This was a
 * vocabulary defect; paying for it with a contract migration would have been a far larger job for no gain.
 */
import { describe, it, expect } from "vitest";
import fs from "node:fs";
import path from "node:path";

const ROOT = path.resolve(__dirname, "..", "Resources", "Views");
const LOCALES = ["tr", "en", "fr", "es", "zh", "ar", "ru"];

// The two screens that render the level: the detail page and the create form. A word that changes on one and
// not the other gives a single level two names, which is worse than the name we started with.
const FAMILIES = {
  detail: (l) => path.join(ROOT, "WorkCenterNext", `WorkCenterNextIndex.${l}.resx`),
  create: (l) => path.join(ROOT, "Tasks", `TasksIndex.${l}.resx`),
};

const value = (file, key) => {
  const xml = fs.readFileSync(file, "utf8");
  const m = xml.match(new RegExp(`<data name="${key}"[^>]*>\\s*(?:<value>)?([^<]*)`));
  return m ? m[1].trim() : null;
};

/*
 * Per language: the word now used, and the words that would mean "mandatory" and so must never come back.
 * `blocking` is listed so the test can prove the two levels stay tellable apart — a middle word that reads as
 * the strict one is the same defect wearing different letters.
 */
const EXPECTED = {
  tr: { word: "Beklenen", forbidden: ["Zorunlu", "Mecburi"], blocking: "Engelleyici" },
  en: { word: "Expected", forbidden: ["Required", "Mandatory"], blocking: "Blocking" },
  fr: { word: "Attendu", forbidden: ["Obligatoire", "Requis"], blocking: "Bloquant" },
  es: { word: "Esperado", forbidden: ["Obligatorio", "Requerido"], blocking: "Bloqueante" },
  zh: { word: "应完成", forbidden: ["必填", "必须"], blocking: "阻断" },
  ar: { word: "متوقَّع", forbidden: ["إلزامي", "مطلوب"], blocking: "مانع" },
  ru: { word: "Ожидается", forbidden: ["Обязательно", "Обязательный"], blocking: "Блокирующий" },
};

describe("the middle checklist level does not call itself mandatory", () => {
  it.each(LOCALES)("[%s] says 'expected', not 'required', on BOTH screens", (locale) => {
    const { word, forbidden } = EXPECTED[locale];
    for (const [screen, file] of Object.entries(FAMILIES)) {
      const got = value(file(locale), "ChecklistLevelRequired");
      expect(got, `${screen}/${locale}`).toBe(word);
      forbidden.forEach((bad) => expect(got, `${screen}/${locale}`).not.toBe(bad));
    }
  });

  it.each(LOCALES)("[%s] keeps the middle level distinguishable from the blocking level", (locale) => {
    const { word, blocking } = EXPECTED[locale];
    for (const file of Object.values(FAMILIES)) {
      expect(value(file(locale), "ChecklistLevelBlocking")).toBe(blocking);
      expect(word).not.toBe(blocking);
    }
  });

  it.each(LOCALES)("[%s] carries the same word into the cycle hint and the open-count sentence", (locale) => {
    const { word, forbidden } = EXPECTED[locale];
    // The hint lists all three levels in order; if it still names the old word, the chip and its own tooltip
    // disagree about what the level is called.
    for (const file of Object.values(FAMILIES)) {
      const hint = value(file(locale), "ChecklistLevelHint");
      expect(hint).toContain(word);
      forbidden.forEach((bad) => expect(hint).not.toContain(bad));
    }
    // "{0} expected item(s) still open" — the sentence that sits under the "does not block" line.
    const count = value(FAMILIES.detail(locale), "ChecklistRequiredOpen");
    expect(count).toContain("{0}");
    forbidden.forEach((bad) => expect(count.toLowerCase()).not.toContain(bad.toLowerCase()));
  });

  it("changes the DISPLAY only — key name, wire value and cycle order are untouched", () => {
    // The key stays `ChecklistLevelRequired` in all fourteen files: renaming it would be a migration, and the
    // defect was never in the wire.
    LOCALES.forEach((locale) => {
      Object.values(FAMILIES).forEach((file) => {
        expect(fs.readFileSync(file(locale), "utf8")).toContain('name="ChecklistLevelRequired"');
      });
    });
    const app = fs.readFileSync(
      path.resolve(__dirname, "..", "wwwroot/assets/js/WorkCenterNext/app.js"), "utf8");
    expect(app).toContain("const order = ['Optional', 'Required', 'Blocking'];");
  });
});
