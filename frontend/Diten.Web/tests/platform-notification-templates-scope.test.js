// WP-PLATFORM-SCOPE-SMALL-01 (A) — the template screens address a tenant's override UNDER ITS TENANT. The Platform's
// id-only routes now reach platform defaults only; a screen that kept calling them for a tenant's template would get
// 404 (that is the fix working, not a bug in the screen).
const fs = require("fs");
const path = require("path");

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");

describe("notification templates — a tenant's override is read and archived under its tenant", () => {
  const js = (name) => read("wwwroot", "assets", "js", "Platform", "NotificationTemplates", name);
  const proxy = read("Controllers", "Platform", "NotificationTemplatesController.cs");

  test("the details page reads and archives through its scope", () => {
    const details = js("details.js");
    expect(details).toMatch(/const templateUrl = \(\) => \(scopeTenantId\s*\? `\$\{apiBase\}\/tenant\/\$\{scopeTenantId\}\/templates\/\$\{templateId\}`\s*: `\$\{apiBase\}\/templates\/\$\{templateId\}`\);/);
    expect(details).toMatch(/fetch\(templateUrl\(\), \{ credentials: 'same-origin' \}\)/);
    expect(details).toMatch(/fetch\(`\$\{templateUrl\(\)\}\/archive`/);
    expect(details).not.toMatch(/\$\{apiBase\}\/templates\/\$\{templateId\}\/archive/);
  });

  test("the edit form loads a tenant's template under its tenant", () => {
    expect(js("form.js")).toMatch(/fetch\(scopeTenantId\s*\? `\$\{apiBase\}\/tenant\/\$\{scopeTenantId\}\/templates\/\$\{templateId\}`/);
  });

  test("the list archives a tenant's row under its tenant", () => {
    expect(js("index.js")).toMatch(/fetch\(row\.tenantId\s*\? `\$\{apiBase\}\/tenant\/\$\{row\.tenantId\}\/templates\/\$\{row\.id\}\/archive`/);
  });

  test("the Web proxy forwards the tenant routes to the Platform's tenant-scoped endpoints", () => {
    expect(proxy).toContain('[HttpGet("api/tenant/{tenantId:guid}/templates/{id:guid}")]');
    expect(proxy).toContain("/api/platform/notifications/tenant-settings/{tenantId}/templates/by-id/{id}");
    expect(proxy).toContain('[HttpPost("api/tenant/{tenantId:guid}/templates/{id:guid}/archive")]');
    expect(proxy).toContain("/api/platform/notifications/tenant-settings/{tenantId}/templates/{id}/archive");
  });
});
