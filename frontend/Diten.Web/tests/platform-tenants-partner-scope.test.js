// BL-521 — the tenant list for a partner administrator: the server filters the rows (Platform API); the screen only
// stops offering what the server refuses. A partner administrator may not create a tenant (403), so it gets no
// "add tenant" button — not a button that ends in an error.
const fs = require("fs");
const path = require("path");

const repoRoot = path.resolve(__dirname, "..", "..", "..");
const web = (...p) => path.join(repoRoot, "frontend", "Diten.Web", ...p);
const read = (...p) => fs.readFileSync(web(...p), "utf8");

describe("BL-521 — the tenant list offers creation to platform administrators only", () => {
  const view = read("Views", "Platform", "Tenants", "Index.cshtml");
  const script = read("wwwroot", "assets", "js", "Platform", "Tenants", "index.js");
  const defaults = read("wwwroot", "assets", "js", "dt-defaults.js");

  test("the page says 'may create' only for a platform_admin token", () => {
    expect(view).toMatch(/id="tenantsCanCreate" value="@\(string\.Equals\(User\.FindFirst\("actor_type"\)\?\.Value, "platform_admin"/);
    expect(view).not.toMatch(/tenantsCanCreate[^\n]*partner_admin/);
  });

  test("the list asks for the add button only when the page says so", () => {
    expect(script).toMatch(/const canCreateTenant = \(\) => document\.getElementById\('tenantsCanCreate'\)\?\.value === 'true';/);
    expect(script).toMatch(/exportButtons\(\s*\/\/[^\n]*\n\s*canCreateTenant\(\) \? L\.AddNewTenants : null,/);
  });

  test("the table helper draws no add button without a label", () => {
    expect(defaults).toMatch(/if \(addNewText\) \{/);
  });
});
