import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { JSDOM } from 'jsdom';

const read = path => readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8');

const loadSurface = scriptPath => {
  const dom = new JSDOM('<!doctype html><div id="inlineFilterHost"></div>', {
    runScripts: 'outside-only',
    url: 'http://localhost/ppm'
  });
  let config;
  dom.window.PpmCrud = { mount: value => { config = value; } };
  dom.window.eval(read(scriptPath));
  dom.window.document.dispatchEvent(new dom.window.Event('DOMContentLoaded'));
  assert.ok(config, `${scriptPath} must mount its executable PPM configuration.`);
  return { config, window: dom.window };
};

test('Investment Case Gate-L surface executes only the approved lifecycle', () => {
  const { config, window } = loadSurface('wwwroot/assets/js/PPM/InvestmentCases/index.js');

  assert.equal(config.endpoint, '/PPM/InvestmentCases/api');
  assert.equal(config.defaultLifecycle, 'Draft');
  assert.equal(config.immutableParent, true);
  assert.equal(config.hideRawParentId, true);
  assert.deepEqual(Array.from(config.transitions.Draft), ['UnderAnalysis', 'Withdrawn']);
  assert.deepEqual(Array.from(config.transitions.UnderAnalysis), ['Closed', 'Withdrawn']);
  assert.deepEqual(Array.from(config.transitions.Closed), []);
  assert.deepEqual(Array.from(config.transitions.Withdrawn), []);
  assert.equal(window.document.getElementById('inlineFilterHost').classList.contains('px-3'), true);
});

test('Benefit Commitment Gate-L surface executes ownership without a Portfolio field', () => {
  const { config } = loadSurface('wwwroot/assets/js/PPM/BenefitCommitments/index.js');

  assert.equal(config.endpoint, '/PPM/BenefitCommitments/api');
  assert.equal(config.defaultLifecycle, 'Draft');
  assert.equal(config.hasInvestmentCaseParent, true);
  assert.equal(config.hasPortfolio, undefined);
  assert.equal(config.immutableParent, true);
  assert.deepEqual(Array.from(config.transitions.Draft), ['Planned', 'Cancelled']);
  assert.deepEqual(Array.from(config.transitions.Planned), ['Active', 'Cancelled']);
  assert.deepEqual(Array.from(config.transitions.Active), ['Closed', 'Cancelled']);
  assert.deepEqual(Array.from(config.transitions.Closed), []);
  assert.deepEqual(Array.from(config.transitions.Cancelled), []);
});

test('all Gate-L browser endpoints remain same-origin MVC proxy paths', () => {
  const investment = loadSurface('wwwroot/assets/js/PPM/InvestmentCases/index.js').config;
  const benefit = loadSurface('wwwroot/assets/js/PPM/BenefitCommitments/index.js').config;

  for (const config of [investment, benefit]) {
    assert.match(config.endpoint, /^\/PPM\//);
    assert.doesNotMatch(config.endpoint, /:\d+|\/api\/v1\/ppm/i);
    assert.equal(config.headers['X-Requested-With'], 'XMLHttpRequest');
  }
});

test('shared CRUD script is executable in a browser context', () => {
  const dom = new JSDOM('<!doctype html>', { runScripts: 'outside-only' });
  dom.window.eval(read('wwwroot/assets/js/PPM/ppm-crud.js'));
  assert.equal(typeof dom.window.PpmCrud.mount, 'function');
});
