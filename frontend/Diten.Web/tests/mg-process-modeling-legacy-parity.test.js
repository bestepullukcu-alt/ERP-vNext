const fs = require('fs');
const path = require('path');
const repo = path.resolve(__dirname, '../../..');
const read = relative => fs.readFileSync(path.join(repo, relative), 'utf8');

describe('MOD-0355 legacy parity gate', () => {
  const evidence = read('execution/domains/management-governance/evidence/MOD-0355-legacy-parity.md');
  const legacyController = read('frontend/Diten.Web/Controllers/ManagementGovernanceController.cs');
  const legacyRegistry = read('frontend/Diten.Web/Config/ManagementGovernanceRegistry.cs');

  it('records every mandatory comparison dimension', () => {
    for (const heading of [
      'Old URL', 'Old entities', 'Fields', 'Commands/actions', 'Lifecycle', 'Permissions',
      'Integrations', 'New owner/module', 'New URL', 'Field mapping', 'Disposition',
      'Migration requirement', 'Browser evidence'
    ]) expect(evidence).toContain(heading);
  });

  it('preserves and quarantines generic placeholders instead of claiming parity', () => {
    expect(legacyController).toContain('[HttpGet("modules/{moduleId}")]');
    expect(legacyRegistry).toContain('mod-process-architecture');
    expect(legacyRegistry).toContain('mod-process-design');
    expect(evidence).toContain('QUARANTINE');
    expect(evidence).toContain('Missing');
    expect(evidence).not.toContain('Full parity complete');
    expect(evidence).not.toContain('Retirement approved');
  });

  it('does not mutate protected legacy, WorkCenter or shared shell paths', () => {
    const changed = require('child_process').execFileSync('git', ['status', '--porcelain=v1', '-uall'], { cwd: repo, encoding: 'utf8' });
    expect(changed).not.toMatch(/Controllers\/ManagementGovernanceController\.cs/);
    expect(changed).not.toMatch(/Config\/ManagementGovernanceRegistry\.cs/);
    expect(changed).not.toMatch(/WorkCenter|WorkCenterNext|_Layout\.cshtml|_LayoutTenantShell\.cshtml|ocelot\.json/);
  });
});
