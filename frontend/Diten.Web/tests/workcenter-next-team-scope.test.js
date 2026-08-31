const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * BL-023 PART A — the "Ekibim" scope option, on the browser side.
 *
 * ⚠ THIS SUITE WAS VACUOUS ONCE, AND THAT IS WHY IT IS WRITTEN THIS WAY.
 * The first version asserted that four resx keys existed and never looked at the render surface. The control
 * was never built, the strings were never printed, and the suite was green — a test that cannot fail is worse
 * than no test, because it is read as proof. Every assertion below now reads app.js, which is where the whole
 * page is rendered (`Index.cshtml` only ships the #wcnApp shell and the script tags), so deleting the control
 * turns this file red.
 *
 * The axis law is locked and this change must not bend it:
 *     tab = OWNERSHIP · segment = STATE (≤3) · chip = TYPE + SIGNAL
 * "Ekibim" is therefore NOT a fifth tab. It joins the scope dropdown that ALREADY answers "whose work am I
 * looking at" (mine / a delegator / all) — the SAP My Inbox shape.
 */
const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...parts) => path.join(repoRoot, "frontend", "Diten.Web", ...parts);
const read = (...parts) => fs.readFileSync(web(...parts), "utf8");

const APP_JS = () => read("wwwroot", "assets", "js", "WorkCenterNext", "app.js");
const CONTROLLER = () => read("Controllers", "WorkCenterNextController.cs");

const loadApi = () => {
  delete global.WorkCenterNextApi;
  loadScript("wwwroot/assets/js/WorkCenterNext/work-items-api.js");
  return global.WorkCenterNextApi;
};

// ── the control is ON THE SCREEN ────────────────────────────────────────────

describe("the scope option is actually rendered", () => {
  test("the header's scope dropdown offers a TEAM entry", () => {
    /*
     * The assertion that would have caught the miss: the option has to be BUILT by buildHeader, not merely
     * translated in a resx. `scopeItem('team', …)` is the existing menu's own row builder.
     */
    const app = APP_JS();
    const header = app.slice(app.indexOf("const buildHeader"), app.indexOf("buildDelegationBanner"));

    expect(header, "buildHeader renders no team option").toMatch(/scopeItem\(\s*'team'/);
    expect(header, "the team option has no label").toMatch(/ScopeTeam/);
  });

  test("the team option is DISABLED with a reason when nobody reports to the user", () => {
    /*
     * DECISION (recorded in BL-023): disabled + a sentence, never hidden. A hidden control cannot explain its
     * own absence, so a manager who expects a team reads the feature as missing rather than their org chart as
     * empty. This is the same "no silent empty state" rule the project has now applied six times.
     */
    const app = APP_JS();
    const header = app.slice(app.indexOf("const buildHeader"), app.indexOf("buildDelegationBanner"));

    expect(header, "the option is never disabled").toMatch(/disabled/);
    expect(header, "the reason is never shown").toMatch(/ScopeTeamEmpty/);
  });

  test("choosing the team scope refetches with scope=team", () => {
    // The dropdown must actually change the DATA, not just the label.
    const app = APP_JS();
    expect(app, "nothing reloads when the scope changes").toMatch(/scope:\s*['"]team['"]|SCOPE\.TEAM/);
  });

  test("the app asks whether the user HAS a team, rather than guessing from an empty list", () => {
    const app = APP_JS();
    expect(app).toMatch(/team-availability|fetchTeamAvailability/);
  });
});

// ── the axis law survives ───────────────────────────────────────────────────

describe("the axis law survives", () => {
  test("the tab strip is unchanged — counted from app.js, not from the resx", () => {
    /*
     * The earlier version counted resx keys, which is why it passed while nothing existed. The tab strip is
     * built from two arrays in app.js; those are what a fifth tab would have to appear in.
     *
     * ⚠ AND UNTIL BL-016 THOSE TWO ARRAYS WERE DEAD. The strip was rendered from a hand-typed literal and the
     * URL whitelist from a second copy, so this guard was reading constants the running page never touched — a
     * new tab added to the literal alone would have left it green. app.js now renders from `TABS`, which is
     * these two arrays concatenated, so what is parsed here is what is drawn.
     *
     * ⚠ THE EXPECTED SET IS FIVE, NOT FOUR, SINCE BL-016 — and that is this law being obeyed rather than
     * bent. "What did I start that somebody else is carrying" is an OWNERSHIP question, so it takes a tab.
     * What this test exists to refuse is a tab that is NOT an ownership question, and `team` is the case in
     * point: "my team's work" is the same ownership question asked about somebody else, so it belongs in the
     * scope dropdown beside delegation. That assertion is unchanged and is the load-bearing one.
     */
    const app = APP_JS();
    const primary = /const TABS_PRIMARY = \[([^\]]*)\]/.exec(app);
    const secondary = /const TABS_SECONDARY = \[([^\]]*)\]/.exec(app);

    expect(primary, "TABS_PRIMARY moved — the tab strip changed shape").toBeTruthy();
    expect(secondary, "TABS_SECONDARY moved").toBeTruthy();

    const tabs = [...primary[1].matchAll(/'([^']+)'/g), ...secondary[1].matchAll(/'([^']+)'/g)]
      .map((m) => m[1]);

    expect(tabs, "the tab set changed").toEqual(["inbox", "islerim", "havuz", "baslattiklarim", "history"]);
    expect(tabs, "'team' became a TAB — that breaks the axis law").not.toContain("team");
  });

  test("the strip the reader sees is built from those arrays, not from a second hand-typed copy", () => {
    /*
     * The guard above is only worth its runtime if the arrays it parses are the ones that render. They were not
     * until BL-016 — so this pins the wiring rather than trusting it, and a literal creeping back into
     * buildTabs fails HERE instead of silently blinding the test above.
     */
    const app = APP_JS();
    const buildTabs = app.slice(app.indexOf("const buildTabs"), app.indexOf("const buildSegments"));

    expect(buildTabs, "buildTabs renders a hand-typed tab list again").toMatch(/TABS\.map\(tab\)/);
    expect(buildTabs, "a literal tab list is back in buildTabs").not.toMatch(/\['inbox',\s*'islerim'/);
  });
});

// ── the scope reaches the server ────────────────────────────────────────────

describe("the scope reaches the server", () => {
  test("the client can ask for a scope, and defaults to the caller's own work", async () => {
    const api = loadApi();
    const asked = [];
    global.fetch = async (url) => {
      asked.push(String(url));
      return { ok: true, status: 200, json: async () => ({ data: [] }) };
    };

    await api.fetchWorkItems();
    await api.fetchWorkItems({ scope: "team" });

    // Default call unchanged — the regression that matters most, because every existing caller passes nothing.
    expect(asked[0]).toBe("/WorkCenterNext/api/work-items");
    expect(asked[1], "the team scope never reaches the server").toContain("scope=team");
  });

  test("an unknown scope value collapses to self rather than being forwarded", () => {
    const api = loadApi();
    expect(api.SCOPE).toEqual({ SELF: "self", TEAM: "team" });
    expect(api.endpointFor("nonsense")).toBe(api.ENDPOINT);
  });

  test("the availability call has its own client function", async () => {
    const api = loadApi();
    const asked = [];
    global.fetch = async (url) => {
      asked.push(String(url));
      return { ok: true, status: 200, json: async () => ({ data: { hasTeam: true, memberCount: 2 } }) };
    };

    const result = await api.fetchTeamAvailability();

    expect(asked[0]).toContain("team-availability");
    expect(result.hasTeam).toBe(true);
    expect(result.memberCount).toBe(2);
  });

  test("an unreachable availability call fails CLOSED — no team rather than a broken option", async () => {
    // Offering a scope that will error is worse than not offering it; the reason string still explains itself.
    const api = loadApi();
    global.fetch = async () => { throw new Error("network"); };

    expect((await api.fetchTeamAvailability()).hasTeam).toBe(false);
  });

  test("the proxy forwards both routes — an unproxied path 404s inside the web tier", () => {
    const controller = CONTROLLER();
    expect(controller, "the scope is never appended to the upstream URL")
      .toMatch(/work-items\/mine\?scope=team/);
    expect(controller, "the availability route is not proxied").toContain("team-availability");
  });
});

// ── l10n ────────────────────────────────────────────────────────────────────

describe("the new strings ship in all seven languages", () => {
  const LOCALES = ["en", "fr", "es", "zh", "ar", "ru", "tr"];
  const KEYS = ["ScopeTeam", "ScopeTeamEmpty"];

  test("every key is present, once, in every language", () => {
    LOCALES.forEach((locale) => {
      const xml = read("Resources", "Views", "WorkCenterNext", `WorkCenterNextIndex.${locale}.resx`);
      KEYS.forEach((key) => {
        const hits = [...xml.matchAll(new RegExp(`name="${key}"`, "g"))].length;
        expect(hits, `${locale} has ${hits} copies of ${key}`).toBe(1);
      });
      // A duplicate key silently shadows the original — this round introduced one and it went unnoticed
      // because the parity check compared two equally-duplicated files.
      const names = [...xml.matchAll(/<data name="([^"]+)"/g)].map((m) => m[1]);
      expect(new Set(names).size, `${locale} contains duplicate keys`).toBe(names.length);
    });
  });

  test("key sets are identical across the seven files", () => {
    const keysOf = (locale) => [...read("Resources", "Views", "WorkCenterNext",
      `WorkCenterNextIndex.${locale}.resx`).matchAll(/<data name="([^"]+)"/g)].map((m) => m[1]).sort();

    const base = keysOf("en");
    LOCALES.forEach((locale) => expect(keysOf(locale), `${locale} drifted from en`).toEqual(base));
  });
});
