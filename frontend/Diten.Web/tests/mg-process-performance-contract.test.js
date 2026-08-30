const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

describe('MOD-0355 Process Performance frontend contract', () => {
  const controller = read('Controllers/ProcessModelingFrontendController.cs');
  const index = read('Views/ManagementGovernance/ProcessModeling/ProcessPerformance/Index.cshtml');
  const table = read('Views/ManagementGovernance/ProcessModeling/ProcessPerformance/_DataTable.cshtml');
  const script = read('wwwroot/assets/js/pages/management-governance/process-modeling/process-performance/index.js');
  test('uses tenant same-origin proxy and never direct service or bearer handling', () => {
    expect(index).toContain('Layout = "_LayoutTenantShell"');
    expect(script).toContain('/management-governance/process-modeling/api/kpi-bindings');
    expect(script).not.toContain('5017'); expect(script).not.toContain('Authorization'); expect(script).not.toContain('Bearer');
  });
  test('is DataTables v2 and renders unavailable controls fail closed', () => {
    expect(table).toContain('data-dt-standard="v2"'); expect(script).toContain('stateSave: false');
    expect(index).toContain('id="metricReference"'); expect(index).toContain('disabled aria-disabled="true"');
    expect(controller).toContain('kpi_reference_contract_unavailable');
  });
  test('has no free-text identity or mutation request', () => {
    expect(index).not.toMatch(/type="text"[^>]*metric/i); expect(script).not.toMatch(/fetch\([^)]*,\s*\{[^}]*method:\s*['"]POST/i);
  });
  test.each([401, 403, 404, 409, 503])('defines %s presentation', status => expect(script).toContain(`${status}: L.Error${status}`));
});
