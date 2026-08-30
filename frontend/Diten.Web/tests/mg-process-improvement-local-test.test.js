const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

describe('MOD-0355 process improvement local-test surface', () => {
  const controller = read('Controllers/ProcessModelingFrontendController.cs');
  const models = read('Models/ManagementGovernance/ProcessModeling/PerformanceImprovement/PerformanceImprovementFrontendModels.cs');
  const index = read('Views/ManagementGovernance/ProcessModeling/PerformanceImprovement/Index.cshtml');
  const details = read('Views/ManagementGovernance/ProcessModeling/PerformanceImprovement/Details.cshtml');
  const filter = read('Views/ManagementGovernance/ProcessModeling/PerformanceImprovement/_Filter.cshtml');
  const table = read('Views/ManagementGovernance/ProcessModeling/PerformanceImprovement/_DataTable.cshtml');
  const l10n = read('Views/ManagementGovernance/ProcessModeling/PerformanceImprovement/_IndexL10n.cshtml');
  const script = read('wwwroot/assets/js/pages/management-governance/process-modeling/performance-improvement/index.js');
  const l10nScript = read('wwwroot/assets/js/pages/management-governance/process-modeling/performance-improvement/index.l10n.js');

  it('owns exact same-origin read routes and remains default-off', () => {
    expect(controller).toContain('[HttpGet("improvements")]');
    expect(controller).toContain('[HttpGet("improvements/{id:guid}")]');
    expect(controller).toContain('[HttpGet("api/improvements")]');
    expect(controller).toContain('[HttpGet("api/improvements/{id:guid}")]');
    expect(index).toContain('data-improvement-state="default-off"');
    expect(index).toContain('Layout = "_LayoutTenantShell"');
    expect(details).toContain('Layout = "_LayoutTenantShell"');
  });

  it('uses exact six owner permissions without wildcard alias or normalization', () => {
    const permissions = [...models.matchAll(/public const string \w+ = "([^"]+)";/g)].map(x => x[1]);
    expect(permissions).toHaveLength(6);
    expect(new Set(permissions).size).toBe(6);
    expect(permissions.every(x => x.startsWith('management-governance.process-modeling.improvements.') && x === x.trim() && !x.includes('*'))).toBe(true);
  });

  it('uses the v2 table, same-origin fetch and no direct service or token handling', () => {
    expect(index).toContain('_Filter.cshtml');
    expect(index).toContain('_DataTable.cshtml');
    expect(index).toContain('_IndexL10n.cshtml');
    expect(index).toContain('index.l10n.js');
    expect(filter).toContain('id="inlineFilterCollapse"');
    expect(table).toContain('id="skeleton-loader"');
    expect(table).toContain('data-dt-standard="v2"');
    expect(table).toContain('dt-checkboxes-select-all');
    expect(script).toContain("'/management-governance/process-modeling/api/improvements'");
    expect(script).toContain('window.DtDefaults.create');
    expect(script).toContain('new DataTable(tableElement, options)');
    expect(script).toContain('loadDefaultView');
    expect(script).toContain('saveView');
    expect(script).toContain('colReorder');
    expect(script).toContain("columns: ':gt(1):not(:last-child)'");
    expect(script).toContain('syncMultiSelectSummary');
    expect(script).toContain('factoryView');
    expect(l10n).toContain('SharedLocalizer["Loading"]');
    expect(l10nScript).toContain('PerformanceImprovementL10n');
    expect([index, details, filter, table, l10n, script, l10nScript].join('\n'))
      .not.toMatch(/localhost|:5017|Authorization|X-Tenant-Id|document\.cookie|localStorage|sessionStorage/i);
    expect(controller).not.toMatch(/localhost|:5017|X-Tenant-Id|document\.cookie|localStorage|sessionStorage/i);
  });

  it('keeps details zero-read and removes the permanent loading state while default-off', () => {
    expect(details).toContain('@if (Model.GatewayReady)');
    expect(details).toContain('data-improvement-state="default-off"');
    expect(script).toContain("if (!host || !gatewayReady) return;");
  });

  it('has exact locale key parity across seven cultures', () => {
    const base = 'Resources/Views/ManagementGovernance/ProcessModeling/PerformanceImprovement/PerformanceImprovementIndex';
    const keys = culture => [...read(`${base}.${culture}.resx`).matchAll(/<data name="([^"]+)"/g)].map(x => x[1]).sort();
    const expected = keys('en');
    expect(expected.length).toBeGreaterThan(20);
    for (const culture of ['fr', 'es', 'zh', 'ar', 'ru', 'tr']) expect(keys(culture)).toEqual(expected);
  });
});
