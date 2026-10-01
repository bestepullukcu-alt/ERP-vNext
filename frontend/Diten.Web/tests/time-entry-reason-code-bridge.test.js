const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * MOD-0280-FU01 T2a — the error-code bridge, the Password/Meetings pattern applied to Time Entry.
 *
 * THE CHAIN:
 *   TimeEntryReasonCodes (C#, Diten.Platform)  +  MOD-0023's WORKFLOW_REJECT_COMMENT_REQUIRED
 *     →  REASON_CODE_MESSAGE_KEYS (TimeEntry/core.js)
 *     →  the page's l10n payload (Views/TimeEntry/_IndexL10n.cshtml)
 *     →  resx, all seven languages
 *
 * Read from the C# source itself, never hand-copied: a code added to TimeEntryModels.cs is covered the moment it exists,
 * and fails here until it reaches the reader as a sentence in seven languages.
 */

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const webRoot = path.resolve(__dirname, "..");
const CORE_JS = path.join(webRoot, "wwwroot", "assets", "js", "TimeEntry", "core.js");
const INDEX_JS = path.join(webRoot, "wwwroot", "assets", "js", "TimeEntry", "index.js");
const CHIP_JS = path.join(webRoot, "wwwroot", "assets", "js", "shared", "timer-chip.js");
const PAYLOAD = path.join(webRoot, "Views", "TimeEntry", "_IndexL10n.cshtml");
const CHIP_PARTIAL = path.join(webRoot, "Views", "Shared", "_TimerChip.cshtml");
const RESX_DIR = path.join(webRoot, "Resources", "Views", "TimeEntry");
const MODELS = path.join(repoRoot, "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features", "TimeEntry", "TimeEntryModels.cs");
const WORKFLOW_MODELS = path.join(repoRoot, "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features", "Workflow", "WorkflowModels.cs");
const LANGUAGES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

const platformCodes = () => {
  const source = fs.readFileSync(MODELS, "utf8");
  const start = source.indexOf("class TimeEntryReasonCodes");
  expect(start, "TimeEntryReasonCodes is not declared in TimeEntryModels.cs").toBeGreaterThan(-1);
  const body = source.slice(start, source.indexOf("\n}", start));
  const codes = Array.from(body.matchAll(/public const string \w+ = "([A-Z_]+)";/g)).map(([, code]) => code);

  // MOD-0023's own refusal a timesheet reject meets (pack R5) — read from ITS source too.
  const workflow = fs.readFileSync(WORKFLOW_MODELS, "utf8");
  expect(workflow).toContain('"WORKFLOW_REJECT_COMMENT_REQUIRED"');
  return codes.concat(["WORKFLOW_REJECT_COMMENT_REQUIRED"]);
};

const clientMap = () => {
  const source = fs.readFileSync(CORE_JS, "utf8");
  const start = source.indexOf("var REASON_CODE_MESSAGE_KEYS");
  expect(start).toBeGreaterThan(-1);
  const text = source.slice(start, source.indexOf("};", start));
  return Array.from(text.matchAll(/\b([A-Z_]+):\s*'([A-Za-z0-9_]+)'/g)).map(([, code, key]) => ({ code, key }));
};

const resxKeys = (language) => {
  const xml = fs.readFileSync(path.join(RESX_DIR, `TimeEntryIndex.${language}.resx`), "utf8");
  return new Set(Array.from(xml.matchAll(/<data name="([^"]+)"/g)).map(([, name]) => name));
};

const resxValue = (language, key) => {
  const xml = fs.readFileSync(path.join(RESX_DIR, `TimeEntryIndex.${language}.resx`), "utf8");
  const match = new RegExp(`<data name="${key.replace(/\./g, "\\.")}"[^>]*><value>([\\s\\S]*?)</value>`).exec(xml);
  return match ? match[1] : null;
};

describe("every Time Entry reason code reaches the reader", () => {
  it("is not vacuous", () => {
    expect(platformCodes().length).toBeGreaterThan(40);
  });

  it("maps EVERY code Platform declares — none silently unmapped", () => {
    const client = clientMap().map((e) => e.code);
    const missing = platformCodes().filter((code) => !client.includes(code));
    expect(missing, `unmapped: ${missing.join(", ")}`).toEqual([]);
  });

  it("maps no code Platform does not declare, and none twice", () => {
    const platform = platformCodes();
    const codes = clientMap().map((e) => e.code);
    expect(codes.filter((c) => !platform.includes(c)), "stale/typo'd codes").toEqual([]);
    expect(codes.filter((c, i) => codes.indexOf(c) !== i), "mapped twice").toEqual([]);
  });

  it.each(clientMap())("$code → $key is in the page payload", ({ key }) => {
    expect(fs.readFileSync(PAYLOAD, "utf8")).toContain(`["${key}"] = Localizer["${key}"].Value`);
  });

  it.each(clientMap())("$code → $key is translated in all seven languages", ({ key }) => {
    const missing = LANGUAGES.filter((language) => !resxKeys(language).has(key) || !resxValue(language, key));
    expect(missing, `${key} missing in: ${missing.join(", ")}`).toEqual([]);
  });

  it("turns a code into its sentence through the real module", () => {
    delete window.TimeEntryCore;
    loadScript("wwwroot/assets/js/TimeEntry/core.js");
    const t = (key) => `<${key}>`;
    platformCodes().forEach((code) => {
      expect(window.TimeEntryCore.failureMessage({ status: 400, reasonCode: code }, t), code).not.toBe("<ErrGeneric>");
    });
  });
});

describe("the page's own words are complete in seven languages", () => {
  it("every resx carries the same key set, none empty", () => {
    const en = resxKeys("en");
    LANGUAGES.forEach((language) => {
      const keys = resxKeys(language);
      expect([...en].filter((k) => !keys.has(k)), `${language} lacks`).toEqual([]);
      expect([...keys].filter((k) => !en.has(k)), `${language} has extra`).toEqual([]);
      [...keys].forEach((k) => expect(resxValue(language, k), `${language}:${k}`).toBeTruthy());
    });
  });

  it("every key the page script asks for is in the payload and in the resx", () => {
    const source = fs.readFileSync(INDEX_JS, "utf8");
    const literal = Array.from(source.matchAll(/\bt\('([A-Za-z]+)'\s*[,)]/g)).map(([, k]) => k);
    const dynamic = ["StatusDraft", "StatusSubmitted", "StatusApproved", "StatusRejected", "StatusCorrection", "StatusSuperseded",
      "SourceManual", "SourceTimer", "SourceMeeting", "SourcePlan", "ErrForbidden", "ErrGeneric"];
    const payload = fs.readFileSync(PAYLOAD, "utf8");
    const en = resxKeys("en");
    // T2b — a key may come from SharedResource instead (the calendar screens' own "calendar not defined" sentence,
    // CalendarUnresolved): then the payload names it through SharedLocalizer and the shared resx carries it.
    const sharedEn = fs.readFileSync(path.join(webRoot, "Resources", "SharedResource.en.resx"), "utf8");
    [...new Set(literal.concat(dynamic))].forEach((key) => {
      if (payload.includes(`["${key}"] = SharedLocalizer["${key}"].Value`)) {
        expect(sharedEn, `SharedResource lacks ${key}`).toContain(`name="${key}"`);
        return;
      }
      expect(en.has(key), `resx lacks ${key}`).toBe(true);
      expect(payload, `payload lacks ${key}`).toContain(`["${key}"] = Localizer["${key}"].Value`);
    });
  });

  it("every key the timer chip asks for is in its partial and in the resx", () => {
    const source = fs.readFileSync(CHIP_JS, "utf8");
    const keys = [...new Set(Array.from(source.matchAll(/\bt\('([A-Za-z]+)'\s*[,)]/g)).map(([, k]) => k))];
    expect(keys.length).toBeGreaterThan(3);
    const partial = fs.readFileSync(CHIP_PARTIAL, "utf8");
    const en = resxKeys("en");
    keys.forEach((key) => {
      expect(en.has(key), `resx lacks ${key}`).toBe(true);
      expect(partial, `chip partial lacks ${key}`).toContain(`["${key}"] = Localizer["${key}"].Value`);
    });
  });

  it("keeps the placeholders of every sentence in every language", () => {
    const en = resxKeys("en");
    [...en].forEach((key) => {
      const expected = (resxValue("en", key).match(/\{\w+\}/g) || []).sort().join(",");
      LANGUAGES.forEach((language) => {
        expect((resxValue(language, key).match(/\{\w+\}/g) || []).sort().join(","), `${language}:${key}`).toBe(expected);
      });
    });
  });

  it("the Arabic page reads right to left in its own words (not English left in place)", () => {
    ["Title", "Disclaimer", "SubmitWeek", "UnreadableTask"].forEach((key) => {
      expect(resxValue("ar", key)).toMatch(/[؀-ۿ]/);
      expect(resxValue("ar", key)).not.toBe(resxValue("en", key));
    });
  });
});
