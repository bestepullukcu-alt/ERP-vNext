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
    expect(script).toContain("colReorder: { columns: ':gt(1):not(:last-child)' }");
    expect(script).toContain("url: '/management-governance/process-modeling/api/models'");
  });

  it('never exposes direct-service, bearer, cookie or fabricated fallback data in browser code', () => {
    expect(script).not.toMatch(/localhost|:5017|document\.cookie|access_token|Authorization\s*:|X-Tenant-Id|fake|mock/i);
    expect(script).not.toMatch(/localStorage|sessionStorage/);
    expect(gateway).toContain('AuthTokenCookies.GetAccessToken(incoming)');
    expect(gateway).toContain('X-Tenant-Id');
    expect(gateway).toContain('X-Correlation-Id');
  });

  it('fails closed unless the exact loopback local-test transport is enabled', () => {
    expect(gateway).toContain('ManagementGovernance:ProcessModelingFrontend:LocalTestEnabled');
    expect(gateway).toContain('ManagementGovernance:ProcessModelingFrontend:LocalTestOrigin');
    expect(gateway).toContain('new("http://127.0.0.1:5017/")');
    expect(gateway).toContain('if (!IsReady || !TryGetOrigin');
    expect(gateway.indexOf('if (!IsReady || !TryGetOrigin')).toBeLessThan(gateway.indexOf('httpClient.SendAsync'));
    expect(gateway).toContain('process_modeling_frontend_gateway_not_ready');
  });

  it('owns the exact model write routes with antiforgery and exact permissions', () => {
    const routes = [
      ['HttpPost("api/models")', 'ProcessModelingFrontendPermissions.Create'],
      ['HttpPut("api/models/{id:guid}")', 'ProcessModelingFrontendPermissions.Update'],
      ['HttpPut("api/model-versions/{id:guid}/draft-content")', 'ProcessModelingFrontendPermissions.Update'],
      ['HttpPost("api/model-versions/{id:guid}/request-review")', 'ProcessModelingFrontendPermissions.RequestReview'],
      ['HttpPost("api/model-versions/{id:guid}/return-to-draft")', 'ProcessModelingFrontendPermissions.ReturnToDraft'],
      ['HttpPost("api/model-versions/{id:guid}/publish")', 'ProcessModelingFrontendPermissions.Publish'],
      ['HttpPost("api/model-versions/{id:guid}/retire")', 'ProcessModelingFrontendPermissions.Retire'],
      ['HttpPost("api/models/{id:guid}/revisions")', 'ProcessModelingFrontendPermissions.CreateRevision']
    ];
    for (const [route, permission] of routes) {
      expect(controller).toContain(`[${route}, ValidateAntiForgeryToken]`);
      expect(controller).toContain(permission);
    }
    expect(controller).not.toMatch(/HttpDelete|AcceptVerbs/);
  });

  it('requires same-origin antiforgery, idempotency and expected versions without browser authority fabrication', () => {
    expect(script).toContain("credentials: 'same-origin'");
    expect(script).toContain('RequestVerificationToken: antiforgery');
    expect(script).toContain("'Idempotency-Key': crypto.randomUUID()");
    expect(script).toContain('expectedVersion: Number(document.getElementById(\'ProcessModelExpectedVersion\').value)');
    expect(gateway).toContain('Idempotency-Key');
    expect(gateway).toContain('idempotency_key_required');
    expect(script).not.toMatch(/localhost|:5017|document\.cookie|access_token|Authorization\s*:|X-Tenant-Id/i);
  });

  it('keeps 400, 403, 404, 409, 503 and offline write outcomes distinct', () => {
    for (const status of [400, 401, 403, 404, 409]) expect(script).toContain(`code === ${status}`);
    expect(script).toContain('L.Error503');
    expect(script).toContain("{ offline: true }");
    expect(script).toContain("error.offline ? 'offline' : `error-${error.status}`");
  });
});
