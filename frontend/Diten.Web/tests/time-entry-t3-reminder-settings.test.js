const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");
const { installNetwork, ok, refused, flush, readSource } = require("./time-entry-harness");

/*
 * MOD-0280-FU01 T3-08 — the weekly reminder switch on the Settings page (pack §21.3 N3). The page's real script; only the
 * network is fake. The switch rides the SAME versioned settings PUT as the pool: it must carry the pool as it stands
 * (never clear it), the row's version, and put itself back when the server refuses.
 */

const POOL = "33333333-0000-0000-0000-0000000000aa";

async function bootSettings(settings, extra = []) {
  delete window.TimeEntryCore;
  loadScript("wwwroot/assets/js/TimeEntry/core.js");
  document.body.innerHTML = `<div id="timeEntrySettings"><div id="tsNotice"></div>
    <select id="tsPoolPosition"><option value="">none</option></select><button id="tsSavePool"></button>
    <input type="checkbox" id="tsWeeklyReminder" disabled />
    <p id="tsLoading"></p><div id="tsSwitches"></div></div>
    <script id="timesettings-l10n" type="application/json">{"ReminderSavedOn":"on!","ReminderSavedOff":"off!","ReminderSaveFailed":"failed!"}</script>`;
  window.showToast = vi.fn();
  window.__timeSettingsNoAutoInit = true;
  const calls = installNetwork(extra.concat([
    ["GET", "/TimeEntry/api/settings/legal-entities", () => ok([])],
    ["GET", "/TimeEntry/api/settings", () => ok(settings)],
    ["GET", "/TimeEntry/Settings/lookup/positions", () => ok([{ id: POOL, code: "HR-ADM", name: "Time admin" }])],
    ["GET", "/TimeEntry/Settings/lookup/legal-entities", () => ok([])]
  ]));
  delete window.TimeEntrySettings;
  loadScript("wwwroot/assets/js/TimeEntry/Settings/index.js");
  await window.TimeEntrySettings.init();
  await flush();
  return calls;
}

describe("settings — the weekly reminder switch", () => {
  it("shows the stored value and is off for a tenant that never saved settings", async () => {
    await bootSettings({ timeAdminPoolPositionId: null, version: 0, weeklyReminderEnabled: false });
    const box = document.getElementById("tsWeeklyReminder");
    expect(box.checked).toBe(false);
    expect(box.disabled).toBe(false);

    await bootSettings({ timeAdminPoolPositionId: POOL, version: 4, weeklyReminderEnabled: true });
    expect(document.getElementById("tsWeeklyReminder").checked).toBe(true);
  });

  it("turning it on sends the version, the pool as it stands and the new value — one PUT", async () => {
    const calls = await bootSettings({ timeAdminPoolPositionId: POOL, version: 4, weeklyReminderEnabled: false },
      [["PUT", "/TimeEntry/api/settings", (body) => ok({ timeAdminPoolPositionId: body.timeAdminPoolPositionId, version: 5, weeklyReminderEnabled: body.weeklyReminderEnabled })]]);
    const box = document.getElementById("tsWeeklyReminder");
    box.checked = true;
    box.dispatchEvent(new Event("change"));
    await flush();

    const puts = calls.filter((c) => c.method === "PUT");
    expect(puts).toHaveLength(1);
    expect(puts[0].body).toEqual({ expectedVersion: 4, timeAdminPoolPositionId: POOL, weeklyReminderEnabled: true });
    expect(window.TimeEntrySettings.state().settings.version).toBe(5);
    expect(document.getElementById("tsNotice").textContent).toBe("on!");
    expect(window.showToast).toHaveBeenCalledWith("on!", "success");
    expect(box.checked).toBe(true);
  });

  it("a refused save puts the switch back where the server has it", async () => {
    await bootSettings({ timeAdminPoolPositionId: null, version: 2, weeklyReminderEnabled: false },
      [["PUT", "/TimeEntry/api/settings", () => refused(409, "TIME_ENTRY_SETTINGS_CONCURRENCY_CONFLICT")]]);
    const box = document.getElementById("tsWeeklyReminder");
    box.checked = true;
    box.dispatchEvent(new Event("change"));
    await flush();

    expect(box.checked).toBe(false);
    expect(window.TimeEntrySettings.state().settings.version).toBe(2);
    expect(window.showToast).not.toHaveBeenCalled();
  });

  it("the pool save leaves the reminder out, so the server keeps it", async () => {
    const calls = await bootSettings({ timeAdminPoolPositionId: null, version: 1, weeklyReminderEnabled: true },
      [["PUT", "/TimeEntry/api/settings", (body) => ok({ timeAdminPoolPositionId: body.timeAdminPoolPositionId, version: 2, weeklyReminderEnabled: true })]]);
    await window.TimeEntrySettings.savePool();
    expect(Object.keys(calls.find((c) => c.method === "PUT").body)).not.toContain("weeklyReminderEnabled");
  });

  it("the page carries the switch with its label and help, and the l10n bridge carries every reminder word", () => {
    const view = readSource("Views/TimeEntry/Settings/Index.cshtml");
    expect(view).toContain('id="tsWeeklyReminder"');
    expect(view).toContain('@Localizer["ReminderLabel"]');
    expect(view).toContain('@Localizer["ReminderHelp"]');
    const bridge = readSource("Views/TimeEntry/Settings/_IndexL10n.cshtml");
    ["ReminderTitle", "ReminderHelp", "ReminderLabel", "ReminderSavedOn", "ReminderSavedOff", "ReminderSaveFailed"]
      .forEach((key) => expect(bridge).toContain(`["${key}"] = Localizer["${key}"].Value`));
  });

  it("every reminder key is present and translated in all seven languages", () => {
    const dir = path.join(__dirname, "..", "Resources", "Views", "TimeEntry", "Settings");
    const value = (lang, key) => {
      const text = fs.readFileSync(path.join(dir, `SettingsIndex.${lang}.resx`), "utf8");
      const match = text.match(new RegExp(`<data name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`));
      return match ? match[1] : null;
    };
    const keys = ["ReminderTitle", "ReminderHelp", "ReminderLabel", "ReminderSavedOn", "ReminderSavedOff", "ReminderSaveFailed"];
    ["en", "tr", "fr", "es", "zh", "ar", "ru"].forEach((lang) => keys.forEach((key) => {
      expect(value(lang, key), `${lang} ${key}`).toBeTruthy();
      if (lang !== "en") { expect(value(lang, key), `${lang} ${key} is the English text`).not.toBe(value("en", key)); }
    }));
  });
});
