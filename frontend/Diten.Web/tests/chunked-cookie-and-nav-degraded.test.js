const fs = require("fs");
const path = require("path");

/*
 * BL-294 — two defects that share one symptom: a screen that comes up empty and says nothing about it.
 *
 *   ① Six controllers read `Request.Cookies["access_token"]` directly. Once the access token outgrew a single
 *     cookie (it is past 3800 characters with today's claim set), that read stopped returning the token and
 *     started returning the literal chunk counter, `chunks-4`. Those six screens sent `Bearer chunks-4` to the
 *     gateway and got a 401 on every call. AuthTokenCookies.GetAccessToken reassembles the chunks; the other
 *     51 call sites already used it.
 *
 *   ② When the navigation endpoint fails, the sidebar and the Ctrl+K palette empty out in silence — the user
 *     sees two hand-written links and no explanation. The behaviour is now: retry ONCE, silently, and if that
 *     also fails, say so where the menu should have been.
 *
 * These are source assertions, and deliberately so. The C# side has the behavioural tests — NavigationRetryTests
 * counts the attempts, ChunkedAccessTokenTests proves a direct read yields the counter. What cannot be tested
 * from inside C# is the RULE: that no call site anywhere reads the cookie directly, and that the warning is
 * actually wired into the two views a user looks at. That is what these check.
 */
const root = path.resolve(__dirname, "..");
const read = (rel) => fs.readFileSync(path.resolve(root, rel), "utf8");
const CONTROLLERS = path.resolve(root, "Controllers");

describe("nothing reads the access-token cookie directly", () => {
  it("has NO direct Request.Cookies[\"access_token\"] read left in any controller", () => {
    /*
     * The general rule, not a list of six files. A seventh controller written next month with the same
     * copy-paste `var token = Request.Cookies["access_token"];` fails here instead of shipping a screen that
     * 401s for every user with a normal-sized token.
     */
    // Comments are stripped first: the repaired call sites carry a comment NAMING the forbidden read, and a
    // naive scan would flag the very explanation that keeps it from coming back.
    const withoutComments = (source) => source.replace(/\/\/[^\n]*/g, "").replace(/\/\*[\s\S]*?\*\//g, "");

    const offenders = fs
      .readdirSync(CONTROLLERS)
      .filter((name) => name.endsWith(".cs"))
      .filter((name) =>
        /Cookies\s*\[\s*"access_token"\s*\]/.test(withoutComments(read(path.join("Controllers", name)))));

    expect(offenders, `these read the chunk counter, not the token: ${offenders.join(", ")}`).toEqual([]);
  });

  it("routes the six repaired call sites through AuthTokenCookies.GetAccessToken", () => {
    // Non-vacuity for the rule above: deleting the read altogether would also make it pass.
    [
      "UsersController.cs",
      "RolesController.cs",
      "RoleAssignmentsController.cs",
      "UserRoleAssignmentsController.cs",
      "PermissionsController.cs",
      "GoldenReferenceSlimController.cs"
    ].forEach((file) => {
      expect(read(path.join("Controllers", file)), `${file} no longer resolves an access token at all`)
        .toContain("AuthTokenCookies.GetAccessToken(Request)");
    });
  });

  it("keeps the chunk marker and the reassembly in ONE place", () => {
    /*
     * The `chunks-N` marker is written and read by the same file. If a second file ever learns to write or
     * parse it, the two will drift and the drift will look exactly like this defect did — a valid session
     * rejected by the gateway.
     */
    const helper = read("Services/Auth/AuthTokenCookies.cs");
    expect(helper).toContain('"chunks-"');
    expect(helper).toContain("chunks-{chunkCount}");
  });
});

describe("the navigation endpoint gets exactly one silent retry", () => {
  const retry = () => read("Services/Http/NavigationRetry.cs");

  it("exists as a single shared helper rather than three copies", () => {
    // Three consumers fall over together; three hand-rolled retries would drift apart the moment one is tuned.
    ["ViewComponents/DynamicModuleMenuViewComponent.cs", "Controllers/TenantSearchController.cs"].forEach((file) => {
      expect(read(file), `${file} does not go through the shared retry`).toContain("SendOnceMoreOnTransientAsync");
    });
  });

  it("sends at most twice — the retry is not a loop", () => {
    /*
     * The mutation this exists for is "make the retry infinite". A `while`/`for` around the send, or a retry
     * count above one, would let a struggling gateway be hammered by every page render. NavigationRetryTests
     * counts the attempts for real; this catches the shape before it gets that far.
     */
    const body = retry();

    expect(body).not.toMatch(/\b(while|for)\s*\(/);
    expect((body.match(/client\.SendAsync\(/g) || []).length,
      "more than two send sites means more than two attempts").toBe(2);
  });

  it("does not sleep — the retry must not slow the page down", () => {
    // A backoff delay inside a page render adds its wait to every genuine outage the user hits.
    expect(retry()).not.toMatch(/Task\.Delay|Thread\.Sleep/);
  });

  it("retries transient faults only, never a decided answer", () => {
    const body = retry();

    expect(body).toContain("RequestTimeout");
    expect(body).toContain("TooManyRequests");
    expect(body).toContain(">= 500");
    // A 401 is the server deciding, not failing: the same token gets the same answer a millisecond later.
    expect(body).not.toContain("Unauthorized");
  });
});

describe("a navigation that stays down is never silent", () => {
  it("gives the sidebar a load-failure state distinct from being legitimately empty", () => {
    /*
     * A tenant with no entitled module and a tenant whose menu failed to load both render zero groups. Only
     * the second is a defect the user can act on, so only the second may show a warning — otherwise every
     * unentitled tenant is nagged about a problem that does not exist.
     */
    const component = read("ViewComponents/DynamicModuleMenuViewComponent.cs");

    expect(component).toContain("FailedToLoad");
    expect(component).toContain("EmptyPayloadReason");
    // The empty-payload path must still resolve to the SILENT model.
    expect(component).toMatch(/EmptyPayloadReason\s*\n?\s*\?\s*DynamicModuleMenuViewModel\.Empty/);
  });

  it("renders the warning in the menu's own place, localized", () => {
    const view = read("Views/Shared/Components/DynamicModuleMenu/Default.cshtml");

    expect(view).toContain("Model.LoadFailed");
    // A distinct marker from the layout's data-nav-load-failed (which carries the TEXT for main.js), so a
    // querySelector for the sidebar notice cannot silently match the <html> element instead.
    expect(view).toContain("data-nav-load-failed-notice");
    expect(view).toContain('SharedLocalizer["NavigationLoadFailed"]');
    // MEASURED: text-body-secondary put the hint at 2.29:1 (light) / 3.49:1 (dark) at 13px — both under AA.
    expect(view, 'the hint is back on a sub-AA muted colour').not.toMatch(/nav-load-failed-hint[^"]*text-body-secondary/);
    // No hardcoded English: the string must come from the resx like every other tenant-side label.
    expect(view).not.toMatch(/could not be loaded|Refresh the page/);
  });

  it("tells the Ctrl+K palette that its emptiness is a failure, not an answer", () => {
    const controller = read("Controllers/TenantSearchController.cs");

    expect(controller).toContain("degraded");
    expect(controller).toContain("NavigationLoadFailed");
    // A 30-second cache on a transient outage keeps showing the warning after the endpoint recovered.
    expect(controller).toContain('result.Degraded ? "no-store"');
  });

  it("renders that warning in the palette instead of an empty box", () => {
    const main = read("wwwroot/assets/js/main.js");

    expect(main).toContain("renderSearchDegradedNotice");
    expect(main).toContain("searchData.degraded");
    // The text is server- or layout-supplied (7 languages); main.js must not invent an English fallback.
    expect(main).toContain("navLoadFailed");
    expect(main).not.toMatch(/'Menu could not be loaded'|"Menu could not be loaded"/);
  });

  it("hands the localized text to that static asset from the shell layout", () => {
    const layout = read("Views/Shared/_LayoutTenantShell.cshtml");

    expect(layout).toContain("data-nav-load-failed");
    expect(layout).toContain('SharedLocalizer["NavigationLoadFailedHint"]');
  });
});

describe("the warning exists in all seven tenant languages", () => {
  // Tenant-side strings are seven-language or they are not done. A missing culture silently falls back to the
  // neutral resource, which is how an English sentence ends up in an Arabic sidebar.
  const LANGS = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

  LANGS.forEach((lang) => {
    it(`${lang} carries both keys with real translations`, () => {
      const resx = read(`Resources/SharedResource.${lang}.resx`);

      ["NavigationLoadFailed", "NavigationLoadFailedHint"].forEach((key) => {
        const match = resx.match(new RegExp(`<data name="${key}"[^>]*>\\s*<value>([^<]*)</value>`));
        expect(match, `${lang} is missing ${key}`).not.toBeNull();
        expect(match[1].trim().length, `${lang}/${key} is empty`).toBeGreaterThan(0);
      });
    });
  });

  it("does not ship the English text under a non-English culture", () => {
    // The cheapest way to fake seven languages is to paste the English value into all seven files.
    const english = read("Resources/SharedResource.en.resx").match(
      /<data name="NavigationLoadFailed"[^>]*>\s*<value>([^<]*)<\/value>/
    )[1];

    ["tr", "fr", "es", "zh", "ar", "ru"].forEach((lang) => {
      const value = read(`Resources/SharedResource.${lang}.resx`).match(
        /<data name="NavigationLoadFailed"[^>]*>\s*<value>([^<]*)<\/value>/
      )[1];
      expect(value, `${lang} is still the English string`).not.toBe(english);
    });
  });
});
