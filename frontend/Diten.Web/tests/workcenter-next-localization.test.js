const fs = require("fs");
const path = require("path");

describe("WorkCenterNext localization resources", () => {
  const resourceRoot = path.resolve(__dirname, "../Resources/Views/WorkCenterNext");
  const locales = ["en", "fr", "es", "zh", "ar", "ru", "tr"];
  const names = (locale) => {
    const xml = fs.readFileSync(path.join(resourceRoot, `WorkCenterNextIndex.${locale}.resx`), "utf8");
    return [...xml.matchAll(/<data name="([^"]+)"/g)].map((match) => match[1]).sort();
  };

  it("keeps exact seven-language key parity", () => {
    const baseline = names("en");
    locales.slice(1).forEach((locale) => expect(names(locale)).toEqual(baseline));
  });

  it("contains the canonical resolver and trigger surface keys", () => {
    const baseline = names("en");
    [
      "FixtureInvalidTitle", "MigrationAdaptedNotice", "ProviderCommandRequired",
      "SourceProjectionRequested", "TriggerOnlyLabel", "TriggerResponsesLabel",
      "NoticeWaiting", "NoticeSnoozed", "ActionDisabledStaleProjection"
    ].forEach((key) => expect(baseline).toContain(key));
  });
  it("contains the person and plan-date keys the detail pane resolves", () => {
    const baseline = names("en");
    ["PersonSelf", "PersonNameUnavailable", "PlannedDateNone"].forEach((key) =>
      expect(baseline).toContain(key));
  });

  it("keeps the missing-plan text distinct from the no-SLA text", () => {
    // They answer different questions: "is there a deadline?" vs "have I planned the work?". Sharing one string
    // made a missing personal plan read as "SLA yok".
    const valueOf = (locale, key) => {
      const xml = fs.readFileSync(path.join(resourceRoot, `WorkCenterNextIndex.${locale}.resx`), "utf8");
      const match = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(xml);
      return match ? match[1].trim() : null;
    };

    locales.forEach((locale) => {
      expect(valueOf(locale, "PlannedDateNone")).not.toBe(valueOf(locale, "SlaNoSla"));
      expect(valueOf(locale, "PlannedDateNone")).toBeTruthy();
    });
  });

  it("translates the new keys rather than leaving English in place", () => {
    const valueOf = (locale, key) => {
      const xml = fs.readFileSync(path.join(resourceRoot, `WorkCenterNextIndex.${locale}.resx`), "utf8");
      const match = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(xml);
      return match ? match[1].trim() : null;
    };

    ["PersonNameUnavailable", "PlannedDateNone"].forEach((key) => {
      // Turkish shares no vocabulary with English here.
      expect(valueOf("tr", key)).not.toBe(valueOf("en", key));
    });
  });
  it("ships the READ-ONLY faces of the checklist row in all seven languages", () => {
    /*
     * A checklist row somebody else added now still states its level and its evidence flag — as a chip and a
     * mark rather than as buttons. Both needed their own words: `ChecklistLevelHint` is an instruction ("Change
     * the level: …") that would be a lie on a chip nobody here can change, and the paperclip button's label is
     * a verb where the mark has to be a statement.
     *
     * `t()` falls back to the KEY when a string is missing, so a gap here renders literal
     * "ChecklistLevelReadOnly" into a tooltip instead of failing. Only this gate catches that.
     */
    const valueOf = (locale, key) => {
      const xml = fs.readFileSync(path.join(resourceRoot, `WorkCenterNextIndex.${locale}.resx`), "utf8");
      const match = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(xml);
      return match ? match[1].trim() : null;
    };

    ["ChecklistLevelReadOnly", "ChecklistEvidenceMark"].forEach((key) => {
      locales.forEach((locale) => expect(valueOf(locale, key), `${locale}/${key}`).toBeTruthy());
      // Not English left in place under a translated file name.
      ["tr", "ru", "zh", "ar"].forEach((locale) =>
        expect(valueOf(locale, key), `${locale}/${key}`).not.toBe(valueOf("en", key)));
    });

    // And the read-only chip must not borrow the button's instruction: one tells you to do something you cannot.
    locales.forEach((locale) =>
      expect(valueOf(locale, "ChecklistLevelReadOnly")).not.toBe(valueOf(locale, "ChecklistLevelHint")));
  });

  it("ships the review action and its blocked reason in all seven languages", () => {
    // Faz 3b. A projected action carries a resource KEY, and t() falls back to the key itself when it is
    // missing — so a button whose label was never translated renders as "WorkAggregation_Action_SubmitReview"
    // rather than failing loudly. Only this gate catches that.
    const valueOf = (locale, key) => {
      const xml = fs.readFileSync(path.join(resourceRoot, `WorkCenterNextIndex.${locale}.resx`), "utf8");
      const match = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(xml);
      return match ? match[1].trim() : null;
    };

    ["WorkAggregation_Action_SubmitReview", "WorkAggregation_ActionDisabled_ReviewPending"].forEach((key) => {
      locales.forEach((locale) => expect(valueOf(locale, key)).toBeTruthy());
      // Not English left in place.
      expect(valueOf("tr", key)).not.toBe(valueOf("en", key));
      expect(valueOf("ru", key)).not.toBe(valueOf("en", key));
    });

    // The two reasons must not share a string: they are cleared by different people, and telling a holder
    // "waiting for approval" while a REVIEWER holds their work sends them to the wrong person.
    locales.forEach((locale) => {
      expect(valueOf(locale, "WorkAggregation_ActionDisabled_ReviewPending"))
        .not.toBe(valueOf(locale, "WorkAggregation_ActionDisabled_ApprovalPendingComplete"));
    });
  });

  it("keeps 'the review could not start' separate from 'the reviewer is holding it'", () => {
    /*
     * They are different facts and they send the reader to different places: one is a retry, the other is a
     * person to wait on. Live, the start failure reported itself as REVIEW_PENDING and pointed the user at a
     * reviewer who had never been asked. Approval already draws this line (ApprovalError_StartFailed vs
     * ActionDisabled_ApprovalPending); this holds review to it.
     */
    const valueOf = (locale, key) => {
      const xml = fs.readFileSync(path.join(resourceRoot, `WorkCenterNextIndex.${locale}.resx`), "utf8");
      const match = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(xml);
      return match ? match[1].trim() : null;
    };

    ["WorkAggregation_ReviewError_StartFailed", "WorkAggregation_ReviewError_ReviewerRequired"].forEach((key) => {
      locales.forEach((locale) => expect(valueOf(locale, key)).toBeTruthy());
      expect(valueOf("tr", key)).not.toBe(valueOf("en", key));
    });

    locales.forEach((locale) => {
      expect(valueOf(locale, "WorkAggregation_ReviewError_StartFailed"))
        .not.toBe(valueOf(locale, "WorkAggregation_ActionDisabled_ReviewPending"));
    });
  });

  it("keeps each date's OWN empty text at its new address", () => {
    /*
     * ⚠ THE TWO DATES SPLIT UP (BL-114). They shared a card called "Durum" whose entire content they were; the
     * source due date moved to the Summary and the personal plan to the Personal card.
     *
     * The rule this test exists for survived the move and is the reason it was worth writing: the two empties
     * are NOT interchangeable. "SLA yok" answers "is there a deadline?" and says nothing about whether the
     * holder has planned the work — one shared placeholder once made a missing plan read as "no SLA".
     *
     * So the pinning moved with them: the plan keeps `PlannedDateNone` where it now lives, and the due date
     * keeps the rule that an ABSENT value prints no row at all rather than borrowing the plan's words.
     */
    const app = fs.readFileSync(
      path.resolve(__dirname, "../wwwroot/assets/js/WorkCenterNext/app.js"), "utf8");
    /*
     * Sliced to the NEXT top-level declaration, not to a magic character count. The count version broke twice
     * as `renderSummary` grew (the plan field, then the parent field pushed `SourceDueLabel` past 7000 chars)
     * — and it broke by reporting the function does not contain a key it plainly contains, which is the worst
     * way for a guard to fail: it accuses the code of the defect the test itself has.
     */
    const fnAt = (name) => {
      const start = app.indexOf(`const ${name}`);
      const next = app.indexOf("\n    const ", start + 1);
      return app.slice(start, next === -1 ? app.length : next);
    };

    /*
     * ⚠ THE PLAN MOVED AGAIN (BL-141, 2026-08-14) — and this time it LOST its empty text on purpose.
     *
     * It went to the Summary because it is not personal: measured on `TaskItem`, the shared task row, read back
     * by the requester. The Summary's rule is that a row is printed for a fact that exists, and "Planla" is
     * already offered as an action on exactly the tasks that can be planned — measured live, twice on screen at
     * 900px (the actions card and the narrow-screen bar). A third invitation would be a third copy of one
     * button, so `PlannedDateNone` is retired rather than relocated.
     *
     * The RULE this test was written for is untouched and is asserted below: the two empties were never
     * interchangeable, so the due date must still not borrow the plan's words.
     */
    expect(fnAt("renderNote"), "the retired empty text came back").not.toContain("PlannedDateNone");
    expect(fnAt("renderSummary"), "the plan's empty text reappeared in the Summary")
      .not.toContain("PlannedDateNone");
    // The summary drops empty rows instead of printing a placeholder, so it must borrow neither word.
    const summary = fnAt("renderSummary");
    expect(summary).toContain("SourceDueLabel");
    expect(summary, "the summary borrowed the plan's empty text").not.toContain("PlannedDateNone");
    expect(summary, "the summary borrowed the SLA empty text").not.toContain("SlaNoSla");
  });

  /*
   * The "create in source module" dialog names CREATING, and nothing else.
   *
   * Its confirm button used to read 'NewOpenSource' ("Open in source") while the handler opened an arbitrary
   * EXISTING record from the chosen module. That open was removed as the wrong act, which left the label
   * promising something the dialog no longer did. The key is deleted rather than left unused, so it cannot be
   * picked up again by someone looking for a plausible-sounding label.
   */
  it("confirms with a CREATE label, and the retired open-label key is gone", () => {
    const app = fs.readFileSync(
      path.resolve(__dirname, "../wwwroot/assets/js/WorkCenterNext/app.js"), "utf8");
    const fn = app.slice(app.indexOf("const openCreateInSource"), app.indexOf("const openMeetingForm"));

    expect(fn, "the create-in-source dialog stopped naming CREATING").toContain("confirmText: t('NewCreateInSource')");
    // The LOOKUP, not the bare name: a comment in that function still explains the history on purpose.
    expect(app).not.toContain("t('NewOpenSource')");

    // And the dead key must not reappear in any language.
    locales.forEach((locale) => {
      expect(names(locale)).not.toContain("NewOpenSource");
      expect(names(locale)).toContain("NewCreateInSource");
    });
  });

  it("opens nothing from that dialog, so the CREATE label stays true", () => {
    // Non-vacuity for the label above: if the open were restored, "Create in source" would be a lie again.
    const app = fs.readFileSync(
      path.resolve(__dirname, "../wwwroot/assets/js/WorkCenterNext/app.js"), "utf8");
    const fn = app.slice(app.indexOf("const openCreateInSource"), app.indexOf("const openMeetingForm"));

    expect(fn).not.toContain("global.open");
  });

  /*
   * WC-1 — a sentence for every transition the engine can record, in all seven languages.
   *
   * DERIVED from the contract's vocabulary rather than from a list typed here, so a transition added to
   * MOD-0024 and mirrored into ACTIVITY_EVENT_CODES fails this test until its seven strings exist. A missing
   * one would not fail loudly on screen: t() answers with the key, so the row would read "AuditEventDelegated"
   * to a user, which is the failure mode this gate exists for.
   */
  it("names every transition the engine can record, in all seven languages", () => {
    const contract = fs.readFileSync(
      path.resolve(__dirname, "../wwwroot/assets/js/WorkCenterNext/fixture-contract.js"), "utf8");
    const declared = /const ACTIVITY_EVENT_CODES = \[([\s\S]*?)\]/.exec(contract);
    expect(declared).not.toBeNull();

    const codes = [...declared[1].matchAll(/'([a-zA-Z]+)'/g)].map((match) => match[1]);
    expect(codes.length).toBeGreaterThan(10);

    const valueOf = (locale, key) => {
      const xml = fs.readFileSync(path.join(resourceRoot, `WorkCenterNextIndex.${locale}.resx`), "utf8");
      const match = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(xml);
      return match ? match[1].trim() : null;
    };

    codes.forEach((code) => {
      const key = "AuditEvent" + code.charAt(0).toUpperCase() + code.slice(1);
      locales.forEach((locale) => {
        expect(valueOf(locale, key), `${key} is missing in ${locale}`).toBeTruthy();
      });
      // Not English left in place — the gap this project has repeatedly shipped.
      expect(valueOf("tr", key)).not.toBe(valueOf("en", key));
      expect(valueOf("ru", key)).not.toBe(valueOf("en", key));
    });
  });

  it("ships the activity filter and the history-gap notice in all seven languages", () => {
    const valueOf = (locale, key) => {
      const xml = fs.readFileSync(path.join(resourceRoot, `WorkCenterNextIndex.${locale}.resx`), "utf8");
      const match = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(xml);
      return match ? match[1].trim() : null;
    };

    ["ActivityFilterLabel", "ActivityFilterAll", "ActivityFilterCommentsOnly",
      "ActivityNoComments", "ActivityHistoryStartsHere",
      // The two signals that give the "Required" level a visible consequence, and the paperclip that has to
      // say what it is while nothing can attach a document yet.
      "ChecklistRequiredOpen", "ConfirmRequiredOpen", "ChecklistEvidenceHint"].forEach((key) => {
      locales.forEach((locale) => expect(valueOf(locale, key), `${key} in ${locale}`).toBeTruthy());
      expect(valueOf("tr", key)).not.toBe(valueOf("en", key));
    });

    // "No activity at all" and "no COMMENTS under this filter" are different facts, and sharing one string
    // would tell a reader their task has no history when it has twelve entries they just filtered out.
    locales.forEach((locale) => {
      expect(valueOf(locale, "ActivityNoComments")).not.toBe(valueOf(locale, "ActivityEmpty"));
    });
  });
});
