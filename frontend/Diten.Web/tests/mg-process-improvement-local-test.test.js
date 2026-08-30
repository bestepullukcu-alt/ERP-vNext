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

  const messages = {
    Loading: 'loading', Empty: 'empty', Error401: '401', Error403: '403', Error404: 'not-found',
    Error409: '409', Error503: '503', ErrorOffline: 'offline'
  };

  async function executeDetails(fetchResult) {
    document.body.innerHTML = '<div id="performanceImprovementStatus" class="alert d-none" data-improvement-state="idle"></div>'
      + '<div id="performanceImprovementDetail"></div>'
      + '<div id="performanceImprovementContext" data-mode="details" data-ready="true" data-id="11111111-1111-4111-8111-111111111111"></div>';
    window.PerformanceImprovementL10n = messages;
    window.fetch = vi.fn(fetchResult);
    window.eval(script);
    await new Promise(resolve => setTimeout(resolve, 0));
    return document.getElementById('performanceImprovementStatus');
  }

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

  it('executes exact zero browser reads and writes while default-off', async () => {
    document.body.innerHTML = '<div id="performanceImprovementStatus" class="alert d-none" data-improvement-state="idle"></div>'
      + '<div id="performanceImprovementContext" data-mode="index" data-ready="false" data-read="true"></div>';
    window.PerformanceImprovementL10n = messages;
    window.fetch = vi.fn();
    window.eval(script);
    await new Promise(resolve => setTimeout(resolve, 0));
    expect(window.fetch).toHaveBeenCalledTimes(0);
    expect(script).not.toMatch(/method\s*:\s*['"](?:POST|PUT|PATCH|DELETE)['"]/i);
  });

  it.each([
    [403, 'error-403'],
    [404, 'error-404'],
    [409, 'error-409'],
    [503, 'error-503']
  ])('renders HTTP %s as its own non-disclosing state', async (status, expectedState) => {
    const host = await executeDetails(() => Promise.resolve({ ok: false, status }));
    expect(host.dataset.improvementState).toBe(expectedState);
    expect(host.textContent).toBe(messages[`Error${status}`]);
    expect(host.textContent).not.toMatch(/tenant|foreign|başka kiracı/i);
  });

  it('renders transport failure as offline rather than authoritative 503', async () => {
    const host = await executeDetails(() => Promise.reject(new TypeError('network')));
    expect(host.dataset.improvementState).toBe('offline');
    expect(host.textContent).toBe(messages.ErrorOffline);
  });

  it('has exact locale key parity across seven cultures', () => {
    const base = 'Resources/Views/ManagementGovernance/ProcessModeling/PerformanceImprovement/PerformanceImprovementIndex';
    const keys = culture => [...read(`${base}.${culture}.resx`).matchAll(/<data name="([^"]+)"/g)].map(x => x[1]).sort();
    const expected = keys('en');
    expect(expected.length).toBeGreaterThan(20);
    expect(expected).toContain('ErrorOffline');
    for (const culture of ['fr', 'es', 'zh', 'ar', 'ru', 'tr']) expect(keys(culture)).toEqual(expected);
  });
});
