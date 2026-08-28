const fs = require("fs");
const path = require("path");
const { loadScript } = require("./load-script");

/*
 * ══ WC-D2 (DCP-004 §2 D2) — A BUTTON REACHES A BACKEND, WHOEVER OWNS THE ITEM ═════════════════════════════
 *
 * THE MEASURED DEFECT. The projection publishes an authoritative actions[] — code, label, enabled — and names no
 * endpoint. So the shell held its own address book, with one entry:
 *
 *     item.provenance !== 'fixture' && item.source?.providerCode === 'tasks'
 *
 * Anything else fell through to a browser-side "transition" that moved the row, fired a toast, and told the
 * console "no backend owns it". MOD-0023's approval items have had four live endpoints behind them since WC-1.
 *
 * These tests are the guards the round was scoped around. The load-bearing one is the second group: an action on
 * an item from a provider that is NOT `tasks` leaving the browser at all.
 */
const APP = fs.readFileSync(
  path.resolve(__dirname, "..", "wwwroot", "assets", "js", "WorkCenterNext", "app.js"), "utf8");

describe("the routing decision no longer names a provider", () => {
  it("has no `providerCode === 'tasks'` comparison anywhere in the write path", () => {
    /*
     * Scoped to the write path deliberately, and the scope is stated rather than assumed: renderSourceCard and
     * the detail header still ask `providerCode !== 'tasks'` to decide whether to draw a SOURCE-MODULE chip.
     * That is a presentational fact about this surface's own home module ("is this row foreign to the Task
     * Center?"), not an address book — removing it would hide a chip, not fix a route.
     */
    const writePath = APP.slice(APP.indexOf("const isFixtureShowcase"), APP.indexOf("const applyPlan"));

    expect(writePath).not.toMatch(/providerCode\s*===\s*['"]tasks['"]/);
    expect(writePath).not.toMatch(/providerCode\s*!==\s*['"]tasks['"]/);
  });

  it("keeps the fixture/real distinction, which is a different question", () => {
    // A showcase fixture has no record on any server: writing one would 404 on an id that was never stored.
    expect(APP).toContain("const isFixtureShowcase");
    expect(APP).toContain("const isDispatchableItem");
    expect(APP).toMatch(/isFixtureShowcase\s*=\s*\(item\)\s*=>\s*!item \|\| item\.provenance === 'fixture'/);
  });
});

describe("every provider's action leaves the browser through ONE address", () => {
  let calls;

  const item = (providerCode) => ({
    id: "ac65ce1d-7d3a-46c3-a3e9-fd8c54f6b2b0",
    provenance: "api",
    source: { providerCode: providerCode },
    concurrency: { kind: "version", token: "3" }
  });

  beforeEach(() => {
    calls = [];
    global.fetch = async (url, options) => {
      calls.push({ url, options, body: JSON.parse(options.body) });
      return {
        ok: true,
        status: 200,
        json: async () => ({ data: null, reason_code: null })
      };
    };
    loadScript("wwwroot/assets/js/WorkCenterNext/work-items-api.js");
  });

  it.each(["tasks", "workflow", "crm"])(
    "posts a %s item's action to the single work-item dispatch endpoint", async (providerCode) => {
      const subject = item(providerCode);

      await global.WorkCenterNextApi.dispatchAction(subject.id, "approve", providerCode, { expectedVersion: 3 });

      expect(calls).toHaveLength(1);
      // The SAME url regardless of provider — that is the whole change.
      expect(calls[0].url).toBe(`/WorkCenterNext/api/work-items/${subject.id}/actions/approve`);
      expect(calls[0].options.method).toBe("POST");
      expect(calls[0].body.providerCode).toBe(providerCode);
      expect(calls[0].body.payload).toEqual({ expectedVersion: 3 });
    });

  it("never puts a module's own route in the browser", () => {
    const api = fs.readFileSync(
      path.resolve(__dirname, "..", "wwwroot", "assets", "js", "WorkCenterNext", "work-items-api.js"), "utf8");
    // /Tasks/api and /api/v1/... are addresses the shell must not know; the proxy and the gateway do.
    expect(api).not.toContain("/Tasks/api");
    expect(api).not.toContain("/api/v1/");
  });

  it("carries the server's reason code back so the message stays in the reader's language", async () => {
    global.fetch = async () => ({
      ok: false,
      status: 409,
      json: async () => ({ reason_code: "TASK_CONCURRENCY_CONFLICT", errors: ["stale"] })
    });

    const result = await global.WorkCenterNextApi.dispatchAction("id", "start", "tasks", {});

    expect(result.ok).toBe(false);
    expect(result.status).toBe(409);
    // TasksApi.failureMessage / isConcurrencyConflict read exactly this field.
    expect(result.reasonCode).toBe("TASK_CONCURRENCY_CONFLICT");
  });

  it("reports a network failure as UNAVAILABLE rather than a silent success", async () => {
    global.fetch = async () => { throw new Error("offline"); };

    const result = await global.WorkCenterNextApi.dispatchAction("id", "start", "tasks", {});

    expect(result.ok).toBe(false);
    expect(result.reasonCode).toBe("UNAVAILABLE");
  });
});

describe("every refusal the endpoint can answer has a sentence in seven languages", () => {
  const LOCALES = ["en", "tr", "fr", "es", "zh", "ar", "ru"];
  const CODES = [
    "WORK_ITEM_PROVIDER_UNKNOWN",
    "WORK_ITEM_PROVIDER_NOT_DISPATCHABLE",
    "WORK_ITEM_ACTION_UNKNOWN",
    "WORK_ITEM_ACTION_FORBIDDEN",
    "WORK_ITEM_ACTION_PAYLOAD_INVALID"
  ];

  const repoRoot = path.resolve(__dirname, "..", "..", "..");

  /** The C# constants, read from source: the wire codes are not restated here. */
  const serverCodes = () => {
    const source = fs.readFileSync(path.join(
      repoRoot, "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features",
      "WorkAggregation", "Dispatch", "WorkItemActionDispatchModels.cs"), "utf8");
    const block = source.slice(source.indexOf("class WorkItemActionReasonCodes"));
    return [...block.matchAll(/=\s*"([A-Z_]+)"/g)].map((m) => m[1]);
  };

  const apiJs = fs.readFileSync(
    path.resolve(__dirname, "..", "wwwroot", "assets", "js", "Tasks", "api.js"), "utf8");

  it("maps every code the server can answer with — an unmapped one reads as the generic error", () => {
    // The bridge's own comment says it three times: an unmapped code IS "an error occurred".
    serverCodes().forEach((code) => {
      expect(apiJs, `${code} is unmapped in REASON_CODE_MESSAGE_KEYS`).toContain(`${code}:`);
    });
    expect(new Set(serverCodes())).toEqual(new Set(CODES));
  });

  const messageKey = (code) => {
    const match = new RegExp(`${code}:\\s*'([A-Za-z]+)'`).exec(apiJs);
    expect(match, `${code} has no message key`).toBeTruthy();
    return match[1].charAt(0).toUpperCase() + match[1].slice(1);
  };

  const resxValue = (locale, key) => {
    const source = fs.readFileSync(
      path.join(repoRoot, "frontend", "Diten.Web", "Resources", "Views", "Tasks", `TasksIndex.${locale}.resx`),
      "utf8");
    const match = new RegExp(`name="${key}"[^>]*>\\s*<value>([\\s\\S]*?)</value>`).exec(source);
    return match ? match[1].trim() : null;
  };

  it.each(LOCALES)("%s carries all five", (locale) => {
    CODES.forEach((code) => {
      const key = messageKey(code);
      expect(resxValue(locale, key), `${key} missing in ${locale}`).toBeTruthy();
    });
  });

  it("translates them rather than leaving English in place", () => {
    CODES.forEach((code) => {
      const key = messageKey(code);
      const english = resxValue("en", key);
      LOCALES.filter((l) => l !== "en").forEach((locale) => {
        expect(resxValue(locale, key), `${key}/${locale} is still English`).not.toBe(english);
      });
    });
  });

  it("is listed in the l10n bridge, which does NOT auto-enumerate its resx", () => {
    const bridge = fs.readFileSync(
      path.resolve(__dirname, "..", "Views", "Tasks", "_IndexL10n.cshtml"), "utf8");
    CODES.forEach((code) => {
      const key = messageKey(code);
      expect(bridge, `${key} is not published to the browser`).toContain(`${key} = Localizer["${key}"]`);
    });
  });
});

describe("the gateway opens the write path and only the write path", () => {
  const ocelot = JSON.parse(fs.readFileSync(
    path.resolve(__dirname, "..", "..", "..", "gateway", "Diten.ApiGateway", "ocelot.json"), "utf8"));

  const workItemRoutes = () =>
    ocelot.Routes.filter((route) => route.UpstreamPathTemplate.startsWith("/api/v1/work-items"));

  it("routes POST to the action path", () => {
    const write = workItemRoutes()
      .find((route) => route.UpstreamPathTemplate === "/api/v1/work-items/{itemId}/actions/{actionCode}");

    expect(write, "the dispatch route is not in the gateway — every click would 404").toBeTruthy();
    expect(write.UpstreamHttpMethod).toContain("POST");
  });

  it("leaves the READ catch-all read-only — widening it would open the whole surface to writes", () => {
    const read = workItemRoutes()
      .find((route) => route.UpstreamPathTemplate === "/api/v1/work-items/{everything}");

    expect(read.UpstreamHttpMethod.sort()).toEqual(["GET", "OPTIONS"]);
  });
});
