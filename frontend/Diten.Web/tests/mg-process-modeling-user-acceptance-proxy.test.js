const fs = require('fs');
const path = require('path');
const root = path.resolve(__dirname, '..');
const read = file => fs.readFileSync(path.join(root, file), 'utf8');

describe('MOD-0355 normal-user same-origin proxy', () => {
  const controller = read('Controllers/ProcessModelingFrontendController.cs');
  const gateway = read('Services/ManagementGovernance/ProcessModeling/ProcessModelingFrontendGateway.cs');
  const settings = read('appsettings.Development.json');
  const browser = [
    read('wwwroot/assets/js/pages/management-governance/process-modeling/index.js'),
    read('wwwroot/assets/js/pages/management-governance/process-modeling/editor.js'),
    read('wwwroot/assets/js/pages/management-governance/process-modeling/performance-improvement/index.js')
  ].join('\n');

  it('owns exact model and improvement mutation routes', () => {
    for (const route of [
      '[HttpPost("api/models")]', '[HttpPut("api/models/{id:guid}")]',
      '[HttpPost("api/improvements")]', '[HttpPut("api/improvements/{id:guid}")]',
      '[HttpPost("api/improvements/{id:guid}/qualify")]',
      '[HttpPost("api/improvements/{id:guid}/outcomes")]',
      '[HttpPost("api/improvements/{id:guid}/close")]'
    ]) expect(controller).toContain(route);
    expect(controller).toContain('request-review|return-to-draft|publish|retire|create-revision');
  });

  it('uses ordinal permission gates for every forwarded mutation', () => {
    expect(controller).toContain('StringComparison.Ordinal');
    for (const permission of ['Create', 'Update', 'RequestReview', 'ReturnToDraft', 'Publish', 'Retire', 'CreateRevision'])
      expect(controller).toContain(`ProcessModelingFrontendPermissions.${permission}`);
    for (const permission of ['Create', 'Update', 'Qualify', 'RecordOutcome', 'Close'])
      expect(controller).toContain(`PerformanceImprovementFrontendPermissions.${permission}`);
  });

  it('derives authority server-side and forwards only safe operation headers', () => {
    expect(gateway).toContain('AuthTokenCookies.GetAccessToken(incoming)');
    expect(gateway).toContain('new AuthenticationHeaderValue("Bearer", token)');
    expect(gateway).toContain('"X-Tenant-Id", tenantId.ToString("D")');
    for (const name of ['Idempotency-Key', 'If-Match', 'X-Expected-Version'])
      expect(gateway).toContain(`ForwardSafeHeader(incoming, request, "${name}")`);
    expect(gateway).not.toMatch(/ForwardSafeHeader\(incoming, request, "(?:Authorization|X-Tenant-Id|permission)"/i);
  });

  it('targets Gateway only and browser code never handles tokens or 5017', () => {
    expect(settings).toContain('"GatewayUrl": "http://localhost:5000"');
    expect(settings).toContain('"GatewayReadPath": "/api/management-governance/process-modeling"');
    expect(browser).not.toMatch(/5017|document\.cookie|access_token|Authorization|X-Tenant-Id/i);
  });
});
