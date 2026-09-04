import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const root = new URL('../../', import.meta.url);
const js = await readFile(new URL('wwwroot/assets/js/PPM/Initiatives/index.js', root), 'utf8');
const view = await readFile(new URL('Views/PPM/Initiatives/Index.cshtml', root), 'utf8');
const form = await readFile(new URL('Views/PPM/Initiatives/_CreateEditOffcanvas.cshtml', root), 'utf8');
const table = await readFile(new URL('Views/PPM/Initiatives/_DataTable.cshtml', root), 'utf8');

assert.match(view, /Layout = "_LayoutTenantShell"/);
assert.match(table, /data-dt-standard="v2"/);
for (const id of ['initiativeCode', 'initiativeName', 'initiativeDescription', 'initiativePortfolio', 'initiativeType', 'initiativePriority', 'initiativeStart', 'initiativeEnd']) assert.match(form, new RegExp(`id="${id}"`));
assert.doesNotMatch(form, /LifecycleState|lifecycleState/);
assert.match(js, /lifecycle-contracts\/v2/);
assert.match(js, /contracts\/v2/);
assert.match(js, /row\.availableActions/);
assert.doesNotMatch(js, /Proposed:\s*\[/);
assert.doesNotMatch(js, /cancellationReasons\s*=\s*\[/);
assert.doesNotMatch(js, /localhost:5062|:5062\/api/);
assert.match(js, /classification.*disabled|setFormBlocked/s);
assert.match(js, /dependency-unavailable/);
assert.match(js, /expectedTerminalVersion/);
assert.match(js, /personalizationClient/);

// The browser must preserve the backend HTTP distinctions instead of flattening
// authoritative failures into one generic message.
for (const [status, key] of [
  [400, 'ValidationError'],
  [401, 'Unauthorized'],
  [403, 'Forbidden'],
  [404, 'NotFound'],
  [409, 'Conflict'],
  [503, 'DependencyUnavailable']
]) {
  assert.match(js, new RegExp(`${status}: L\\.${key}`));
}

// Lifecycle and closure vocabularies are server-owned. Keep the negative checks
// broad enough to catch either a direct array or an alternate local fallback.
for (const vocabulary of [
  'cancellationReasons',
  'holdReasons',
  'completionOutcomes',
  'closureReasons',
  'benefitDispositions'
]) {
  assert.doesNotMatch(js, new RegExp(`(?:const|let|var)\\s+${vocabulary}\\s*=`));
  assert.match(js, new RegExp(`state\\.lifecycle\\.${vocabulary}`));
}

assert.match(js, /row\.lifecycleState === 'Completed' \|\| row\.lifecycleState === 'Cancelled'/);
assert.match(js, /if \(!terminal\) actions\.push\(\{ className: 'js-initiative-edit'/);
assert.match(js, /if \(terminal\) actions\.push\(\{ className: 'js-initiative-successor'/);
assert.match(js, /if \(!terminal\) actions\.push\(\{ className: 'js-initiative-delete/);
assert.match(js, /\(row\.availableActions \|\| \[\]\)\.forEach/);
assert.match(js, /action\.availability === 'forbidden' \? 403/);
assert.match(js, /action\.availability === 'dependency-unavailable' \? 503/);
assert.match(js, /credentials: 'same-origin'/);
assert.match(js, /url\.protocol === 'http:' \|\| url\.protocol === 'https:'/);
assert.match(js, /anchor\.rel = 'noopener noreferrer'/);

console.log('ppm initiative frontend contract: PASS');
