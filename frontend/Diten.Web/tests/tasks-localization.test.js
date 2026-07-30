const fs = require("fs");
const path = require("path");

// MOD-0024 — the 7-language gate. A tenant surface must ship every string in all seven languages, and a
// non-English file must not silently carry the English value.
const LOCALES = ["en", "fr", "es", "zh", "ar", "ru", "tr"];
const RESX_DIR = path.resolve(__dirname, "..", "Resources", "Views", "Tasks");

const readEntries = (locale) => {
  const file = path.join(RESX_DIR, `TasksIndex.${locale}.resx`);
  const xml = fs.readFileSync(file, "utf8");
  const entries = {};
  const pattern = /<data name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)<\/value>/g;
  let match;
  while ((match = pattern.exec(xml)) !== null) {
    entries[match[1]] = match[2];
  }
  return entries;
};

describe("MOD-0024 task localization", () => {
  const byLocale = {};
  beforeAll(() => {
    LOCALES.forEach((locale) => { byLocale[locale] = readEntries(locale); });
  });

  it("ships a resx for every supported tenant language", () => {
    LOCALES.forEach((locale) => {
      expect(fs.existsSync(path.join(RESX_DIR, `TasksIndex.${locale}.resx`))).toBe(true);
    });
  });

  it("says what the review switch DOES, and keeps no key for what it used to promise", () => {
    /*
     * Faz 3b. The switch was disabled with a "coming soon" hint; review is now implemented, so the hint had to
     * change and its key had to GO. A leftover key is not harmless here — it is a translated sentence promising
     * a later release, one edit away from being rendered again beside a feature that already shipped.
     */
    LOCALES.forEach((locale) => {
      expect(byLocale[locale].ReviewRequiredHint).toBeTruthy();
      expect(byLocale[locale].ReviewComingSoonHint).toBeUndefined();
    });

    expect(byLocale.tr.ReviewRequiredHint).not.toBe(byLocale.en.ReviewRequiredHint);
  });

  it("ships the reviewer field's label and hint in all seven languages", () => {
    // The field is mandatory whenever the review switch is on, so an untranslated label is a required field the
    // user cannot read.
    LOCALES.forEach((locale) => {
      expect(byLocale[locale].FieldReviewer).toBeTruthy();
      expect(byLocale[locale].ReviewerHint).toBeTruthy();
    });

    expect(byLocale.tr.FieldReviewer).not.toBe(byLocale.en.FieldReviewer);
    expect(byLocale.ru.ReviewerHint).not.toBe(byLocale.en.ReviewerHint);
  });

  it("leaves the review switch enabled on the form", () => {
    // The string alone proves nothing: the hint could be correct while the control stays disabled, which is the
    // state this slice replaced.
    const form = fs.readFileSync(
      path.resolve(__dirname, "..", "Views", "Tasks", "_Form.cshtml"), "utf8");
    const block = form.slice(form.indexOf('id="taskReviewRequired"'), form.indexOf('id="taskApprovalRequired"'));

    expect(block).not.toContain("disabled");
    expect(block).toContain("ReviewRequiredHint");
    expect(form).not.toContain("ReviewComingSoonHint");
  });

  it("has an identical key set across all seven languages", () => {
    const baseKeys = Object.keys(byLocale.en).sort();
    expect(baseKeys.length).toBeGreaterThan(0);

    LOCALES.filter((l) => l !== "en").forEach((locale) => {
      expect(Object.keys(byLocale[locale]).sort()).toEqual(baseKeys);
    });
  });

  it("has no empty values", () => {
    LOCALES.forEach((locale) => {
      Object.entries(byLocale[locale]).forEach(([key, value]) => {
        expect(value.trim().length, `${locale}/${key} is empty`).toBeGreaterThan(0);
      });
    });
  });

  it("contains the keys the form and list actually resolve", () => {
    [
      "TasksTitle", "PageDescription", "AddNewTask", "QuickCreateTitle",
      "FieldTitle", "FieldAssignmentTarget", "TargetSelf", "TargetPerson", "TargetPool",
      "FieldAssignee", "FieldPoolPosition", "PoolPositionHint", "FieldOrganizationUnit",
      "FieldDueAt", "FieldEstimateHours", "FieldSpentHours", "SpentHoursReadOnlyHint",
      "FieldApprovalRequired", "ApprovalHint", "FieldWatchers", "WatchersHint",
      "LifecycleOpen", "LifecycleInProgress", "LifecycleDone", "LifecycleCancelled",
      "ActionClaim", "ErrorAlreadyClaimed", "ErrorNoAccess", "ErrorUnavailable",
      "EmptyTitle", "EmptyDescription"
    ].forEach((key) => {
      expect(byLocale.en, `missing key ${key}`).toHaveProperty(key);
    });
  });

  /*
   * The gap that let TWO bugs through in one day: the tests never crossed the serialization boundary.
   *
   * The bridge is a C# anonymous object rendered by Json.Serialize, and MVC's JSON options apply the camelCase
   * naming policy — so a property written `ErrorOccurred` reaches the browser as `errorOccurred`. The earlier
   * version of this suite compared the C# property names to the JS keys in their raw form, so PascalCase on both
   * sides "matched" while every lookup failed at runtime and t() silently returned the key itself.
   *
   * These tests therefore compare the JS keys against the SERIALIZED names, applying the same policy the runtime
   * does. A case mismatch now fails here.
   */
  describe("JS keys match the SERIALIZED bridge payload, not the C# property names", () => {
    const JS_FILES = [
      path.resolve(__dirname, "..", "wwwroot", "assets", "js", "Tasks"),
      path.resolve(__dirname, "..", "wwwroot", "assets", "js", "WorkCenterNext", "quick-create.js")
    ];
    const BRIDGE = path.resolve(__dirname, "..", "Views", "Tasks", "_IndexL10n.cshtml");

    // System.Text.Json's JsonNamingPolicy.CamelCase lowercases the first character.
    const toSerializedName = (propertyName) => propertyName[0].toLowerCase() + propertyName.slice(1);

    // Comments are stripped first: they legitimately quote wrong keys while explaining the convention, and a
    // scanner that reads prose as code reports them as real call sites.
    const stripComments = (source) => source
      .replace(/\/\*[\s\S]*?\*\//g, "")
      .replace(/(^|[^:])\/\/.*$/gm, "$1");

    const readJs = () => JS_FILES.flatMap((entry) => (
      fs.statSync(entry).isDirectory()
        ? fs.readdirSync(entry).filter((f) => f.endsWith(".js")).map((f) => path.join(entry, f))
        : [entry]
    )).map((file) => stripComments(fs.readFileSync(file, "utf8"))).join("\n");

    // Keys built by template literal — t(`lifecycle${x}`) — cannot be read statically, so the families are
    // enumerated from the values the server can actually send. The SUFFIX stays PascalCase because it is
    // concatenated onto the camelCase prefix (lifecycle + Open = lifecycleOpen).
    const DYNAMIC_FAMILIES = {
      lifecycle: ["Open", "Planned", "InProgress", "Waiting", "PendingReview", "Done", "Cancelled"],
      priority: ["Low", "Medium", "High"],
      target: ["Self", "Person", "Pool"]
    };

    const usedKeys = () => {
      const source = readJs();
      const keys = new Set();
      // Matches t('key') and the branches of t(cond ? 'a' : 'b').
      const quoted = /\bt(?:f)?\(\s*(?:[^)'"]*\?\s*)?'([A-Za-z][A-Za-z0-9_]*)'(?:\s*:\s*'([A-Za-z][A-Za-z0-9_]*)')?/g;
      let match;
      while ((match = quoted.exec(source)) !== null) {
        keys.add(match[1]);
        if (match[2]) { keys.add(match[2]); }
      }

      Object.entries(DYNAMIC_FAMILIES).forEach(([prefix, suffixes]) => {
        if (new RegExp("t\\(`" + prefix + "\\$\\{").test(source)) {
          suffixes.forEach((suffix) => keys.add(prefix + suffix));
        }
      });

      return [...keys];
    };

    const serializedPayloadKeys = () => {
      const bridge = fs.readFileSync(BRIDGE, "utf8");
      const keys = new Set();
      const assignment = /^\s*([A-Za-z][A-Za-z0-9_]*)\s*=\s*(?:Shared)?Localizer\[/gm;
      let match;
      while ((match = assignment.exec(bridge)) !== null) {
        keys.add(toSerializedName(match[1]));
      }
      return keys;
    };

    it("finds keys and payload entries to compare (guards against a vacuous scan)", () => {
      expect(usedKeys().length).toBeGreaterThan(15);
      expect(serializedPayloadKeys().size).toBeGreaterThan(50);
    });

    it("asks for every key in the case the browser will actually receive", () => {
      const published = serializedPayloadKeys();
      const missing = usedKeys().filter((key) => !published.has(key));
      expect(missing).toEqual([]);
    });

    it("uses no PascalCase key — the payload contains none", () => {
      // Direct encoding of the convention: a capitalised first letter can never resolve.
      const pascal = usedKeys().filter((key) => /^[A-Z]/.test(key));
      expect(pascal).toEqual([]);
    });

    it("maps each used key back to a resx entry in all 7 languages", () => {
      // The resx keys stay PascalCase; only the wire form is camelCase. Map back before checking.
      const resxNames = new Map(Object.keys(byLocale.en).map((k) => [toSerializedName(k), k]));
      const missing = [];

      usedKeys().forEach((key) => {
        const resxKey = resxNames.get(key);
        if (!resxKey) { missing.push(`resx/<none>/${key}`); return; }
        LOCALES.forEach((locale) => {
          if (!Object.prototype.hasOwnProperty.call(byLocale[locale], resxKey)) {
            missing.push(`${locale}/${resxKey}`);
          }
        });
      });

      expect(missing).toEqual([]);
    });
  });

  it("does not leave English text in the Turkish file", () => {
    // Turkish shares no vocabulary with English here, so an identical longer value means a forgotten translation.
    const suspicious = Object.entries(byLocale.tr)
      .filter(([key, value]) => value === byLocale.en[key] && value.trim().length > 3);
    expect(suspicious.map(([k]) => k)).toEqual([]);
  });

  it("keeps every language's non-cognate values distinct from English", () => {
    // Cognates are legitimate (French "Description"/"Actions"); a whole file matching English is not.
    LOCALES.filter((l) => l !== "en").forEach((locale) => {
      const identical = Object.entries(byLocale[locale])
        .filter(([key, value]) => value === byLocale.en[key] && value.trim().length > 3);
      const ratio = identical.length / Object.keys(byLocale.en).length;
      expect(ratio, `${locale} looks untranslated (${identical.length} identical values)`).toBeLessThan(0.1);
    });
  });
});
