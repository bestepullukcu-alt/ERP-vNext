const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');

describe('MOD-0355 process-model catalog', () => {
  const controller = read('Controllers/ProcessModelingFrontendController.cs');
  const gateway = read('Services/ManagementGovernance/ProcessModeling/ProcessModelingFrontendGateway.cs');
  const index = read('Views/ManagementGovernance/ProcessModeling/Index.cshtml');
  const table = read('Views/ManagementGovernance/ProcessModeling/_DataTable.cshtml');
  const filter = read('Views/ManagementGovernance/ProcessModeling/_Filter.cshtml');
  const script = read('wwwroot/assets/js/pages/management-governance/process-modeling/index.js');

  it('owns only the exact tenant routes and explicit tenant shell', () => {
    expect(controller).toContain('[Route("management-governance/process-modeling")]');
    expect(controller).toContain('[HttpGet("models")]');
    expect(controller).toContain('[HttpGet("models/{id:guid}")]');
    expect(controller).toContain('[Authorize]');
    expect(controller).toContain('HasPermission(ProcessModelingFrontendPermissions.Read)');
    expect(controller).toContain('return StatusCode(StatusCodes.Status403Forbidden);');
    expect(controller).not.toContain('return Forbid();');
    expect(controller).toContain('process_modeling_permission_denied');
    expect(index).toContain('Layout = "_LayoutTenantShell"');
  });

  it('uses DataTables v2 and the same-origin read proxy', () => {
    expect(table).toContain('data-dt-standard="v2"');
    expect(table).toContain('id="dt-process-modeling"');
    expect(index).toContain('_Filter.cshtml');
    expect(index).toContain('_CreateEditOffcanvas.cshtml');
    expect(filter).toContain('id="filterForm" data-no-tracker');
    expect(script).toContain('new DataTable(tableElement');
    expect(script).toContain('window.DtDefaults.create');
    expect(script).toContain('serverSide: true');
    expect(script).toContain('stateSave: false');
    expect(script).toContain("colReorder: { columns: ':gt(0):not(:last-child)' }");
    expect(script).toContain("url: '/management-governance/process-modeling/api/models'");
  });

  it('never exposes direct-service, bearer, cookie or fabricated fallback data in browser code', () => {
    expect(script).not.toMatch(/localhost|:5017|document\.cookie|access_token|Authorization\s*:|X-Tenant-Id|fake|mock/i);
    expect(script).not.toMatch(/localStorage|sessionStorage/);
    expect(gateway).toContain('AuthTokenCookies.GetAccessToken(incoming)');
    expect(gateway).toContain('X-Tenant-Id');
    expect(gateway).toContain('X-Correlation-Id');
  });

  it('is default-off and performs zero HTTP reads before an approved route is configured', () => {
    expect(gateway).toContain('ManagementGovernance:ProcessModelingFrontend:Enabled');
    expect(gateway).toContain('ManagementGovernance:ProcessModelingFrontend:GatewayReadPath');
    expect(gateway).toContain('if (!IsReady)');
    expect(gateway.indexOf('if (!IsReady)')).toBeLessThan(gateway.indexOf('httpClient.SendAsync'));
    expect(gateway).toContain('process_modeling_frontend_gateway_not_ready');
    expect(controller).not.toMatch(/HttpPost|HttpPut|HttpDelete|AcceptVerbs/);
  });
});
