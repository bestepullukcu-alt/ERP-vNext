#!/usr/bin/env node
/*
 * MOD-0048 — Publish Consent Required Reference Sets for MOD-0164-FU02
 * OPERATOR RUN SCRIPT. Not a seed. Not a Mongo write. Gateway MOD-0048/PSS-012 API only.
 *
 * Auth: this script does NOT log in. The OPERATOR obtains a tenant-scoped bearer token
 *       (tenant-auth login, password entered by the operator) and exports it:
 *
 *   Windows PowerShell:  $env:TOKEN="<jwt>"; $env:TENANT="97c59330-dbc4-4665-b29c-0c26dbb5cc93"; node publish-consent-sets.js
 *   bash:                TOKEN=<jwt> TENANT=97c59330-dbc4-4665-b29c-0c26dbb5cc93 node publish-consent-sets.js
 *
 * Flags:
 *   DRY_RUN=1   -> only baseline GET /sets + plan, no writes.
 *   NO_PUBLISH=1-> create/draft/values/validate but stop before submit/approve/publish (save-as-draft, SoD-safe).
 *
 * Idempotent / non-destructive: existing set is reused (no duplicate create); a fresh DRAFT version is created and
 * values REPLACED on that draft only; no hard delete; value codes are stable; historical published versions untouched.
 * SoD: submit and approve are distinct permissions — if the same identity cannot approve its own submission the script
 * reports the exact step that needs a second (checker) identity and leaves the version at that state.
 */
const http = require('http');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const GATEWAY = process.env.GATEWAY || 'http://localhost:5000';
const TOKEN = process.env.TOKEN || '';
const TENANT = process.env.TENANT || '97c59330-dbc4-4665-b29c-0c26dbb5cc93';
const DRY_RUN = process.env.DRY_RUN === '1';
const NO_PUBLISH = process.env.NO_PUBLISH === '1';

// Absolute default; override with TEMPLATE_PATH if the repo lives elsewhere.
const TEMPLATE = 'C:/Users/user/Desktop/ERP-vNext/docs/audits/mod-0048-crm-consent-campaign-knowledge-reference-set-authoring-template.json';
const templatePath = process.env.TEMPLATE_PATH || TEMPLATE;

const PUBLISH_SCOPE = [
  'consent-channel', 'consent-purpose', 'consent-legal-basis',
  'consent-status', 'preference-type', 'consent-source',
];
const EXCLUDED = ['preference-value']; // design-open, non-blocker — never published here

function req(method, url, body, extraHeaders) {
  return new Promise((resolve, reject) => {
    const u = new URL(url);
    const data = body ? JSON.stringify(body) : null;
    const headers = {
      'Accept': 'application/json',
      'Authorization': `Bearer ${TOKEN}`,
      'X-Tenant-Id': TENANT,
      ...(extraHeaders || {}),
    };
    if (data) { headers['Content-Type'] = 'application/json'; headers['Content-Length'] = Buffer.byteLength(data); }
    const r = http.request({ hostname: u.hostname, port: u.port, path: u.pathname + u.search, method, headers }, (res) => {
      let chunks = '';
      res.on('data', d => chunks += d);
      res.on('end', () => { let json; try { json = chunks ? JSON.parse(chunks) : null; } catch { json = chunks; } resolve({ status: res.statusCode, body: json }); });
    });
    r.on('error', reject);
    if (data) r.write(data);
    r.end();
  });
}
const idem = () => crypto.randomUUID();
const log = (...a) => console.log(...a);
// API responses are wrapped: { data: <payload>, statusCode, isSuccessful, errors, ... }
const unwrap = (b) => (b && typeof b === 'object' && !Array.isArray(b) && 'data' in b) ? b.data : b;

async function findSet(setCode) {
  const r = await req('GET', `${GATEWAY}/api/v1/reference-data/sets?pageSize=200`);
  if (r.status !== 200) throw new Error(`GET /sets -> ${r.status} ${JSON.stringify(r.body)}`);
  const d = unwrap(r.body);
  const arr = Array.isArray(d) ? d : (d.items || d.sets || d.results || []);
  return arr.find(s => (s.setCode || s.code || s.SetCode) === setCode) || null;
}

async function ensureSet(def) {
  let s = await findSet(def.setCode);
  if (s) { log(`  set exists: ${def.setCode} (id=${s.id || s.setId}, status=${s.status})`); return s; }
  if (DRY_RUN) { log(`  [dry-run] would CREATE set ${def.setCode}`); return null; }
  const r = await req('POST', `${GATEWAY}/api/v1/reference-data/sets`, {
    set_code: def.setCode, name: def.name, description: def.name, scope_type: def.scopeType || 'tenant',
  });
  if (r.status !== 201 && r.status !== 200) throw new Error(`CREATE set ${def.setCode} -> ${r.status} ${JSON.stringify(r.body)}`);
  const d = unwrap(r.body);
  log(`  created set: ${def.setCode} (id=${d.id || d.setId})`);
  return d;
}

async function ensureDraftVersion(setId, existing) {
  const draftId = existing && (existing.activeDraftVersionId || existing.ActiveDraftVersionId);
  if (draftId) { log(`    reuse draft version ${draftId}`); return draftId; }
  if (DRY_RUN) { log('    [dry-run] would CREATE draft version'); return null; }
  const r = await req('POST', `${GATEWAY}/api/v1/reference-data/sets/${setId}/versions`, {});
  if (r.status !== 201 && r.status !== 200) throw new Error(`CREATE version -> ${r.status} ${JSON.stringify(r.body)}`);
  const d = unwrap(r.body);
  const vid = d.id || d.versionId || d.activeDraftVersionId;
  log(`    created draft version ${vid}`);
  return vid;
}

async function putValues(versionId, def) {
  const values = def.values.map(v => ({
    code: v.valueCode, label: v.displayName, description: v.attributes && v.attributes.description,
    is_active: !v.isDeprecated, sort_order: v.sortOrder, parent_value_code: v.parentValueCode || null,
    attributes: v.attributes || {},
  }));
  if (DRY_RUN) { log(`    [dry-run] would PUT ${values.length} values`); return; }
  const r = await req('PUT', `${GATEWAY}/api/v1/reference-data/versions/${versionId}/values`, { values });
  if (r.status !== 200 && r.status !== 201) throw new Error(`PUT values -> ${r.status} ${JSON.stringify(r.body)}`);
  log(`    replaced ${values.length} values`);
}

async function lifecycle(versionId, setCode) {
  if (DRY_RUN || NO_PUBLISH) { log(`    stop before publish (${DRY_RUN ? 'dry-run' : 'no-publish'})`); return 'draft'; }
  let r = await req('POST', `${GATEWAY}/api/v1/reference-data/versions/${versionId}/validate`, {});
  log(`    validate -> ${r.status}`);
  if (r.status >= 400) throw new Error(`validate ${setCode} -> ${r.status} ${JSON.stringify(r.body)}`);
  r = await req('POST', `${GATEWAY}/api/v1/reference-data/versions/${versionId}/submit`, {}, { 'Idempotency-Key': idem() });
  log(`    submit -> ${r.status}`);
  if (r.status >= 400) { log(`    !! submit blocked (permission/SoD?): ${JSON.stringify(r.body)}`); return 'submit-blocked'; }
  r = await req('POST', `${GATEWAY}/api/v1/reference-data/versions/${versionId}/approve`, { decision: 'approve' }, { 'Idempotency-Key': idem() });
  log(`    approve -> ${r.status}`);
  if (r.status >= 400) { log(`    !! approve blocked — needs a CHECKER identity (SoD): ${JSON.stringify(r.body)}`); return 'approve-blocked-needs-checker'; }
  r = await req('POST', `${GATEWAY}/api/v1/reference-data/versions/${versionId}/publish`, {}, { 'Idempotency-Key': idem() });
  log(`    publish -> ${r.status}`);
  if (r.status >= 400) throw new Error(`publish ${setCode} -> ${r.status} ${JSON.stringify(r.body)}`);
  return 'published';
}

(async () => {
  if (!TOKEN && !DRY_RUN) { console.error('ERROR: TOKEN env var is empty. Operator must export a tenant-scoped bearer token.'); process.exit(2); }
  const tpl = JSON.parse(fs.readFileSync(templatePath, 'utf8'));
  const byCode = Object.fromEntries(tpl.sets.map(s => [s.setCode, s]));
  for (const ex of EXCLUDED) if (PUBLISH_SCOPE.includes(ex)) throw new Error(`excluded set ${ex} in scope`);
  log(`MOD-0048 consent publish — gateway=${GATEWAY} tenant=${TENANT} dryRun=${DRY_RUN} noPublish=${NO_PUBLISH}`);
  log(`scope (${PUBLISH_SCOPE.length}): ${PUBLISH_SCOPE.join(', ')}`);
  log(`excluded: ${EXCLUDED.join(', ')}\n`);
  const results = {};
  for (const code of PUBLISH_SCOPE) {
    const def = byCode[code];
    if (!def) throw new Error(`template missing set ${code}`);
    log(`# ${code} (${def.values.length} values, requiredLevel=${def.requiredLevel})`);
    const existing = await ensureSet(def);
    if (DRY_RUN) { results[code] = 'dry-run'; log(''); continue; }
    const setId = existing ? (existing.id || existing.setId) : (await findSet(code)).id;
    const vid = await ensureDraftVersion(setId, existing || await findSet(code));
    await putValues(vid, def);
    results[code] = await lifecycle(vid, code);
    log('');
  }
  log('== read-back ==');
  for (const code of PUBLISH_SCOPE) {
    const r = await req('GET', `${GATEWAY}/api/v1/reference-data/sets/${code}/published-values`);
    const d = unwrap(r.body);
    const arr = Array.isArray(d) ? d : (d && (d.values || d.items) || []);
    const codes = arr.map(v => v.code || v.valueCode || v.Code);
    log(`  ${code}: HTTP ${r.status}, count=${codes.length}, lifecycle=${results[code]} [${codes.join(', ')}]`);
  }
  log('\nDONE. Verify every set shows status=published with the expected value count.');
})().catch(e => { console.error('FATAL:', e.message); process.exit(1); });
