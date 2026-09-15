const fs = require("fs");
const path = require("path");

/*
 * WP-DM-DCP005-KURAL4-UI-01 (Kural 4 v2, sahip 2026-09-15) — the /active toggle's own client-side reason-code →
 * sentence mapping (index.js never goes through TaskTypesController's server-side ReasonCodeMessages; it is a
 * raw fetch to the same-origin proxy), plus the 7-language completeness of the five sentences this WP adds.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);

const INDEX_JS = fs.readFileSync(
  web("wwwroot", "assets", "js", "Tasks", "TaskTypes", "index.js"),
  "utf8"
);
const BRIDGE = fs.readFileSync(
  web("Views", "Tasks", "TaskTypes", "_IndexL10n.cshtml"),
  "utf8"
);
const L10N_LOADER = fs.readFileSync(
  web("wwwroot", "assets", "js", "Tasks", "TaskTypes", "index.l10n.js"),
  "utf8"
);

const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
const resxPath = (lang) =>
  web("Resources", "Views", "Tasks", "TaskTypes", `TaskTypesIndex.${lang}.resx`);

const readResx = (lang) => {
  const xml = fs.readFileSync(resxPath(lang), "utf8");
  const entries = {};
  const re = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>\s*<\/data>/g;
  let m;
  while ((m = re.exec(xml))) {
    entries[m[1]] = m[2]
      .replace(/&lt;/g, "<")
      .replace(/&gt;/g, ">")
      .replace(/&amp;/g, "&");
  }
  return entries;
};

/*
 * buildToggleActiveErrorMessage is a top-level, dependency-free function declared BEFORE the DataTable IIFE —
 * deliberately, so it (and its module.exports guard) can be evaluated on their own, without booting the whole
 * screen (jQuery DataTable, personalization client, DOM queries the IIFE runs eagerly at file-load time).
 */
const loadModule = () => {
  const iifeStart = INDEX_JS.indexOf("const TaskFieldDefinitionList = (function () {");
  const prefix = iifeStart === -1 ? INDEX_JS : INDEX_JS.slice(0, iifeStart);
  const module = { exports: {} };
  // eslint-disable-next-line no-new-func
  new Function("module", `${prefix}\n`)(module);
  return module.exports;
};

describe("TaskTypes /active toggle — Kural 4 v2 client-side messages", () => {
  const { buildToggleActiveErrorMessage } = loadModule();
  const labels = readResxAsLabels();

  function readResxAsLabels() {
    const en = readResx("en");
    return {
      ErrorTaskTypeEnableBlockedDocuments: en.ErrorTaskTypeEnableBlockedDocuments,
      ErrorTaskTypeEnableRegisterUnavailable: en.ErrorTaskTypeEnableRegisterUnavailable,
      ErrorOccurred: "Something went wrong.",
    };
  }

  it("is exported for testing", () => {
    expect(typeof buildToggleActiveErrorMessage).toBe("function");
  });

  it("maps task_type_enable_blocked_documents to the activation sentence, WITH the document list", () => {
    const message = buildToggleActiveErrorMessage(
      "task_type_enable_blocked_documents",
      ["UID-0000104: Blocked (Draft)", "UID-0000118: Unresolved"],
      labels
    );
    expect(message).toContain(labels.ErrorTaskTypeEnableBlockedDocuments);
    expect(message).toContain("UID-0000104: Blocked (Draft)");
    expect(message).toContain("UID-0000118: Unresolved");
  });

  it("maps task_type_enable_blocked_documents with NO errors list to just the sentence", () => {
    const message = buildToggleActiveErrorMessage("task_type_enable_blocked_documents", undefined, labels);
    expect(message.trim()).toBe(labels.ErrorTaskTypeEnableBlockedDocuments);
  });

  it("maps task_type_enable_register_unavailable to the unavailable sentence", () => {
    const message = buildToggleActiveErrorMessage("task_type_enable_register_unavailable", null, labels);
    expect(message).toBe(labels.ErrorTaskTypeEnableRegisterUnavailable);
  });

  it("falls back to the generic error for an unknown or missing reason code", () => {
    expect(buildToggleActiveErrorMessage(undefined, undefined, labels)).toBe(labels.ErrorOccurred);
    expect(buildToggleActiveErrorMessage("SOME_OTHER_CODE", undefined, labels)).toBe(labels.ErrorOccurred);
  });

  it("never throws when labels are missing", () => {
    expect(() => buildToggleActiveErrorMessage("task_type_enable_blocked_documents", ["x"], undefined)).not.toThrow();
    expect(() => buildToggleActiveErrorMessage("task_type_enable_blocked_documents", ["x"], {})).not.toThrow();
  });
});

describe("TaskTypes l10n bridge — the two client-visible Kural 4 v2 keys", () => {
  it("_IndexL10n.cshtml serializes both client-facing keys", () => {
    expect(BRIDGE).toMatch(/ErrorTaskTypeEnableBlockedDocuments\s*=\s*Localizer\["ErrorTaskTypeEnableBlockedDocuments"\]/);
    expect(BRIDGE).toMatch(/ErrorTaskTypeEnableRegisterUnavailable\s*=\s*Localizer\["ErrorTaskTypeEnableRegisterUnavailable"\]/);
  });

  it("index.l10n.js's own required-keys guard lists both", () => {
    expect(L10N_LOADER).toContain("'ErrorTaskTypeEnableBlockedDocuments'");
    expect(L10N_LOADER).toContain("'ErrorTaskTypeEnableRegisterUnavailable'");
  });
});

describe("TaskTypesIndex resx — Kural 4 v2's five sentences, all seven languages", () => {
  const KEYS = [
    "InfoTaskTypeSavedInactiveBlockedDocuments",
    "InfoTaskTypeSavedInactiveUnverified",
    "ErrorTaskTypeEnableBlockedDocuments",
    "ErrorTaskTypeEnableRegisterUnavailable",
    "ErrorTaskTypeActiveDocumentBlocked",
  ];
  const byLang = Object.fromEntries(LANGS.map((lang) => [lang, readResx(lang)]));

  it.each(KEYS)("%s exists with a REAL (non-English-duplicate) value in every language", (key) => {
    const english = byLang.en[key];
    expect(english, `en:${key} missing`).toBeTruthy();
    for (const lang of LANGS) {
      const value = byLang[lang][key];
      expect(value, `${lang}:${key} missing`).toBeTruthy();
      expect(value.trim().length, `${lang}:${key} empty`).toBeGreaterThan(0);
      if (lang !== "en") {
        expect(value, `${lang}:${key} is still the English text`).not.toBe(english);
      }
    }
  });

  it("the Turkish sentences match the prompt's own pinned wording", () => {
    expect(byLang.tr.InfoTaskTypeSavedInactiveBlockedDocuments).toBe(
      "Görev türü pasif olarak kaydedildi: bağlı belgelerin hepsi yürürlükte değil."
    );
    expect(byLang.tr.InfoTaskTypeSavedInactiveUnverified).toBe(
      "Belge durumu doğrulanamadı; tür pasif kaydedildi. Daha sonra aktif etmeyi deneyin."
    );
    expect(byLang.tr.ErrorTaskTypeEnableBlockedDocuments).toBe(
      "Şu belgeler yürürlükte olmadığı için tür aktif edilemez:"
    );
    expect(byLang.tr.ErrorTaskTypeEnableRegisterUnavailable).toBe(
      "Belge durumu doğrulanamadı, daha sonra tekrar deneyin."
    );
    expect(byLang.tr.ErrorTaskTypeActiveDocumentBlocked).toBe(
      "Aktif bir türe yürürlükte olmayan belge bağlanamaz. Önce türü pasife alın ya da belgenin yürürlüğe girmesini bekleyin."
    );
  });
});
