const fs = require("fs");
const path = require("path");

/*
 * The tenant's dynamic sidebar rendered ZERO groups for a whole session, and nothing said so.
 *
 * DynamicModuleMenuViewComponent.ResolveAsync had FOUR ways to return an empty menu — no token, a non-2xx
 * response, an empty payload, and a caught exception — and not one of them logged above Debug. The only way the
 * outage was found at all was by reading the HttpClient trace underneath LogDebug and noticing 401s. Meanwhile
 * six hand-written links in _LayoutTenantShell.cshtml kept the sidebar looking populated, so the failure read as
 * "some modules are missing" when the truth was "the dynamic menu produced nothing".
 *
 * Two rules follow, and both are checked here because both are invisible from inside the file that breaks them:
 *   1. No path may return an empty menu without saying why, at Warning, with a distinguishable reason.
 *   2. Hand-written nav links must be marked as such, so they can never again disguise an empty dynamic menu.
 *
 * These are source assertions. The component needs an HttpContext, an HttpClient, a token cookie and a gateway to
 * exercise for real, and none of that would make the rule any truer than reading the four exits does.
 */
const root = path.resolve(__dirname, "..");
const read = (rel) => fs.readFileSync(path.resolve(root, rel), "utf8");

const COMPONENT = "ViewComponents/DynamicModuleMenuViewComponent.cs";
const LAYOUT = "Views/Shared/_LayoutTenantShell.cshtml";

describe("an empty dynamic menu always says why", () => {
  /** The body of ResolveAsync plus its EmptyBecause helper — everything that can yield an empty menu. */
  const resolveBody = () => {
    const source = read(COMPONENT);
    const start = source.indexOf("private DynamicModuleMenuViewModel EmptyBecause");
    expect(start).toBeGreaterThan(-1);
    const end = source.indexOf("// Flat descriptors", start);
    expect(end).toBeGreaterThan(start);
    return source.slice(start, end);
  };

  it("names all four reasons, so the log points at the right team", () => {
    /*
     * The reasons are not decoration. `no_token` is a session problem, `http_401` is authentication, and
     * `empty_payload` is entitlement or catalogue data — three different owners. Collapsing them into one
     * message would log the outage without diagnosing it, which is barely better than the silence.
     */
    const body = resolveBody();

    ["no_token", "http_", "empty_payload", "Reason=exception"].forEach((reason) => {
      expect(body, `no distinguishable reason for ${reason}`).toContain(reason);
    });
  });

  it("logs at WARNING, never Debug — a vanished menu is not a debug event", () => {
    const body = resolveBody();

    expect(body).toContain("_logger.LogWarning");
    // The original non-2xx path used LogDebug, which is invisible at the default Information level and is
    // precisely why this took a session to find.
    expect(body).not.toContain("_logger.LogDebug");
  });

  it("carries the tenant and a correlation id, so one tenant's outage can be told from another's", () => {
    const body = resolveBody();

    expect(body).toContain("TenantId=");
    expect(body).toContain("CorrelationId=");
  });

  it("has NO path that returns an empty menu without logging", () => {
    /*
     * The general rule rather than four specific assertions: every `return DynamicModuleMenuViewModel.Empty` in
     * ResolveAsync must be inside the logging helper. A fifth exit added later — and there will be one — fails
     * here instead of silently reopening the defect.
     */
    const body = resolveBody();
    const directReturns = (body.match(/return DynamicModuleMenuViewModel\.Empty;/g) || []).length;

    // Exactly one: the single `return` inside EmptyBecause itself. Everything else must call the helper.
    expect(directReturns, "an exit returns Empty without going through EmptyBecause").toBeLessThanOrEqual(2);
    expect(body).toContain("EmptyBecause(");
  });
});

describe("hand-written nav links cannot disguise themselves as catalogue links", () => {
  it("is fenced by an explicit LEGACY-NAV marker", () => {
    /*
     * Without this, an empty dynamic menu looks like a partially-populated one — which is exactly how the outage
     * was misread. The marker is what makes "the sidebar has links" stop meaning "the menu works".
     */
    const layout = read(LAYOUT);

    expect(layout).toContain("LEGACY-NAV");
    expect(layout).toContain("LEGACY-NAV-END");
    expect(layout.indexOf("══ LEGACY-NAV-END"))
      .toBeGreaterThan(layout.indexOf("LEGACY-NAV — HAND-WRITTEN"));
  });

  it("records which links are already catalogue duplicates", () => {
    /*
     * Measured against platform_module_page_descriptors: four of the six routes are ALSO nav-visible Active
     * catalogue pages, so when the dynamic menu recovers they render twice. That fact belongs next to the code,
     * because the fix is a deletion of those four — not a move — and whoever does it needs to know which.
     */
    const layout = read(LAYOUT);

    ["CONTROLLED_DOCUMENTS", "TEMPLATE_MASTERS", "TEMPLATE_VARIANTS", "ACCESS_MATRIX"].forEach((pageCode) => {
      expect(layout, `the marker does not record ${pageCode} as a duplicate`).toContain(pageCode);
    });
  });

  it("records the two that CANNOT be moved, and why the block was marked rather than deleted", () => {
    // /DocumentManagementReconciliation has no nav-visible catalogue page; HCM has no module row at all. Deleting
    // the block today would leave the tenant with no sidebar while the menu is still returning 401.
    const layout = read(LAYOUT);

    expect(layout).toContain("/DocumentManagementReconciliation");
    expect(layout).toMatch(/HCM has NO module row/i);
  });

  it("keeps every hand-written link inside the fence", () => {
    /*
     * Non-vacuity for the marker: it is worth nothing if a seventh hand-written link is added outside it. The
     * fence must contain every one of the six routes that are not catalogue-driven.
     */
    const layout = read(LAYOUT);
    // The END fence is searched AFTER the header, because the header's own prose names it ("...to the
    // LEGACY-NAV-END marker...") and a naive indexOf would slice the block down to the comment alone.
    const headerAt = layout.indexOf("LEGACY-NAV — HAND-WRITTEN");
    expect(headerAt).toBeGreaterThan(-1);
    const endAt = layout.indexOf("══ LEGACY-NAV-END", headerAt);
    expect(endAt).toBeGreaterThan(headerAt);
    const fenced = layout.slice(headerAt, endAt);

    [
      "/DocumentManagementControlledDocuments",
      "/DocumentManagementTemplateMasters",
      "/DocumentManagementTemplateVariants",
      "/DocumentManagementAccessMatrix",
      "/DocumentManagementReconciliation",
      "/HCM/Employees/Create"
    ].forEach((route) => {
      expect(fenced, `${route} is a hand-written link outside the LEGACY-NAV fence`).toContain(route);
    });
  });
});
