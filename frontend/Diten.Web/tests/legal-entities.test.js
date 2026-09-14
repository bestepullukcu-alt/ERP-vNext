const fs = require('fs');
const path = require('path');
const assert = require('node:assert/strict');
const { describe, it } = require('node:test');

const root = path.resolve(__dirname, '..');
const read = relative => fs.readFileSync(path.join(root, relative), 'utf8');
const wizard = read('wwwroot/assets/js/MasterData/LegalEntities/wizard.js');
const controller = read('Controllers/LegalEntitiesController.cs');

describe('MOD-0220 Legal Entity editable-version contract', () => {
  it('captures the detail version while hydrating an edit', () => {
    assert.match(wizard, /expectedVersion\s*=\s*(?:d|detail)\.version/);
  });

  it('sends the captured expectedVersion in the update JSON payload', () => {
    const payload = wizard.slice(wizard.indexOf('const collectPayload'), wizard.indexOf('// ─── Save'));
    assert.match(payload, /expectedVersion/);
    assert.match(wizard, /body: JSON\.stringify\(payload\)/);
  });

  it('keeps the MVC update proxy as a raw request-body pass-through to Gateway', () => {
    const update = controller.slice(
      controller.indexOf('public async Task<IActionResult> UpdateProxy'),
      controller.indexOf('[HttpPatch', controller.indexOf('public async Task<IActionResult> UpdateProxy')));
    assert.match(update, /Request\.Body/);
    assert.match(update, /ReadToEndAsync/);
    assert.match(update, /ProxyGatewayAsync\(HttpMethod\.Put/);
    assert.match(update, /, body\)/);
  });
});
