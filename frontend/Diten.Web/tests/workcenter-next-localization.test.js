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
  it("wires each date cell to its OWN empty text", () => {
    // Having the strings is not enough — the detail pane must actually use the plan-specific one. The bug was a
    // single shared placeholder in renderPlanDates, so this pins the call sites.
    const app = fs.readFileSync(
      path.resolve(__dirname, "../wwwroot/assets/js/WorkCenterNext/app.js"), "utf8");
    const fn = app.slice(app.indexOf("const renderPlanDates"), app.indexOf("const renderPlanDates") + 1200);

    expect(fn).toContain("cell('SourceDueLabel', item.dueAt, 'SlaNoSla'");
    expect(fn).toContain("cell('PlannedDateLabel', item.plannedDate, 'PlannedDateNone'");
    // The old shape hardcoded the fallback inside cell() instead of taking it per call.
    expect(fn).not.toMatch(/value \|\| t\('SlaNoSla'\)/);
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

    expect(fn).toContain("confirmButtonText: t('NewCreateInSource')");
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
});
