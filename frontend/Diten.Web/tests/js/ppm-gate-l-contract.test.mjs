import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const read = path => readFileSync(new URL(`../../${path}`, import.meta.url), 'utf8');

test('Gate L configs retain exact lifecycle and ownership boundaries', () => {
  const investment = read('wwwroot/assets/js/PPM/InvestmentCases/index.js');
  const benefit = read('wwwroot/assets/js/PPM/BenefitCommitments/index.js');
  assert.match(investment, /Draft: \['UnderAnalysis', 'Withdrawn'\]/);
  assert.match(investment, /UnderAnalysis: \['Closed', 'Withdrawn'\]/);
  assert.match(benefit, /Draft: \['Planned', 'Cancelled'\]/);
  assert.match(benefit, /Active: \['Closed', 'Cancelled'\]/);
  assert.doesNotMatch(benefit, /PortfolioId|portfolioId/);
  assert.match(read('wwwroot/assets/js/PPM/InvestmentCases/index.l10n.js'), /ppm-l10n/);
  assert.match(read('wwwroot/assets/js/PPM/BenefitCommitments/index.l10n.js'), /ppm-l10n/);
});

test('shared CRUD is same-origin, immutable-parent aware and approval-free', () => {
  const source = read('wwwroot/assets/js/PPM/ppm-crud.js');
  assert.match(source, /credentials: 'same-origin'/);
  assert.match(source, /config\.immutableParent/);
  assert.match(source, /window\.showConfirm/);
  assert.doesNotMatch(source, /5061|approve/i);
});

test('MVC proxy forwards tenant and correlation context without browser token access', () => {
  const controller = read('Controllers/PPM/PpmController.cs');
  const crud = read('wwwroot/assets/js/PPM/ppm-crud.js');
  assert.match(controller, /TenantHeaderName = "X-Tenant-Id"/);
  assert.match(controller, /CorrelationHeaderName = "X-Correlation-Id"/);
  assert.match(controller, /TryAddWithoutValidation\(TenantHeaderName, tenantId\.Value\.ToString\("D"\)\)/);
  assert.match(controller, /TryAddWithoutValidation\(CorrelationHeaderName, ResolveCorrelationId\(\)\)/);
  assert.match(controller, /StringComparison\.Ordinal\)/);
  assert.doesNotMatch(controller, /permission\.Contains|StartsWith\(required/i);
  assert.match(crud, /credentials: 'same-origin'/);
  assert.doesNotMatch(crud, /document\.cookie|localStorage|Authorization|Bearer/);
});

test('parent rendering is Code — Title and missing lookups never expose raw identifiers', () => {
  const source = read('wwwroot/assets/js/PPM/ppm-crud.js');
  const investment = read('wwwroot/assets/js/PPM/InvestmentCases/index.js');
  assert.match(source, /parent \? `\$\{parent\.code\} — \$\{titleOf\(parent\)\}`/);
  assert.match(source, /config\.hideRawParentId \? \(L\.NotAvailable \|\| '-'\)/);
  assert.match(source, /row\.investmentCaseId\);\s*return parent \? `\$\{parent\.code\} — \$\{titleOf\(parent\)\}` : \(L\.NotAvailable \|\| '-'\)/);
  assert.match(investment, /hideRawParentId: true/);
});

test('required parent lookups fail closed and never expose server payloads', () => {
  const source = read('wwwroot/assets/js/PPM/ppm-crud.js');
  assert.match(source, /lookupBlocked: false/);
  assert.match(source, /state\.lookupBlocked \|\| !form\.checkValidity\(\)/);
  assert.match(source, /error\.status === 401/);
  assert.match(source, /error\.status === 403/);
  assert.match(source, /error\.status === 503/);
  assert.match(source, /Invalid server response/);
  assert.match(source, /\.prop\('disabled', state\.lookupBlocked\)/);
});

test('successful parent lookup clears the temporary loading lock without a stale error', () => {
    const source = read('wwwroot/assets/js/PPM/ppm-crud.js');
    assert.match(source, /setLookupBlocked\(new Error\([^\n]+Loading[^\n]+\), false\)/);
    assert.match(source, /if \(!error\) \{\s*formAlert\.replaceChildren\(\);\s*formAlert\.classList\.add\('d-none'\);/s);
    assert.match(source, /setLookupBlocked\(null\);/);
});
