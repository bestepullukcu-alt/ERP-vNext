// Q64b (Q97 v2.1) browser harness — MOD-0187 Claims UI draft on the lane stack (Playwright 1.59.1, Chromium 147, headless).
// Run ONLY by the lane supervisor (task harness): it supplies ACTOR_PW_<LABEL>; this script never prints or writes them.
// Usage: node claims_runtime.mjs <phase> <webPort> <evidenceDir> <fixtureJson> <stateDir> [<runtimeDir>]
//   login       real Web login page per actor (Web → Gateway → Auth), one browser context each; storage state written
//               ONLY to <stateDir> (~/mvp6-env, mode 0600, deleted by `cleanup`); evidence gets cookie NAMES only.
//   vs1         early vertical slice (CU-VS1) with network capture + root-leak assertion (CT F8).
//   spec        runs the Q64a scenarios (byte-identical copy of claims-ui.spec.mjs in <runtimeDir>) with @playwright/test.
//   checks-neg  zero-write checks: 401/403 surfaces, cross-LE, cross-tenant, soft-deleted, unknown, CU-17 direct 422, CU-23, F8.
//   checks-mut  CU-10 positive branch (carrier linked → checkbox enabled → create 201 with the carrier).
//   cleanup     delete the storage-state files.
// PNGs only via page.screenshot({ path }). All browser URLs are on 127.0.0.1:<webPort>.
import fs from 'node:fs'; import path from 'node:path'; import crypto from 'node:crypto'; import { spawnSync } from 'node:child_process';
import { createRequire } from 'node:module';

const [PHASE, WEBPORT, EVD, FXJSON, STATE, RUNTIME, MODE, FIXFILE, FIXSHA] = process.argv.slice(2);
// ESM ignores NODE_PATH: resolve Playwright from the lane runtime folder (~/mvp6-env), never from the repository.
const { chromium } = createRequire(path.join(RUNTIME, 'package.json'))('playwright');
const WEB = `http://127.0.0.1:${WEBPORT}`;
const PAGE = `${WEB}/SupplyChain/Claims`;
const T1 = '97c59330-dbc4-4665-b29c-0c26dbb5cc93', T2 = '00000000-0000-0000-0000-000000000001';
const ACT = { full: ['john.doe.t97@diten.com', T1], readonly: ['jane.smith.t97@diten.com', T1], noread: ['charlie.brown.t97@diten.com', T1],
  leb: ['bob.johnson.t97@diten.com', T1], t2user: ['john.doe.def@diten.com', T2] };
const OUTD = path.join(EVD, 'browser'); const PNG = path.join(EVD, 'png');
[OUTD, PNG].forEach((d) => fs.mkdirSync(d, { recursive: true }));
const FX = JSON.parse(fs.readFileSync(FXJSON, 'utf8')).fixtures;
const ROOTS = Object.values(FX.shipments).map((s) => s.lifecycleCorrelationId).filter(Boolean).map((r) => r.toLowerCase());
const uniq = (base) => { let n = 1; while (fs.existsSync(path.join(OUTD, `${base}-a${n}.json`))) n++; return path.join(OUTD, `${base}-a${n}.json`); };
const result = { phase: PHASE, startedAt: new Date().toISOString(), web: WEB, cases: {}, png: [], console: [], rootLeak: [] };
const sha = (b) => crypto.createHash('sha256').update(b).digest('hex');
const statePath = (a) => path.join(STATE, `${a}.json`);

async function shot(page, name) {
  const p = path.join(PNG, `${name}.png`); await page.screenshot({ path: p, fullPage: true });
  result.png.push({ name: `${name}.png`, sha256: sha(fs.readFileSync(p)), url: page.url() });
}
function watch(page, tag) {
  const net = [];
  page.on('console', (m) => { if (['error', 'warning'].includes(m.type())) result.console.push({ tag, type: m.type(), text: m.text().slice(0, 300) }); });
  page.on('response', async (r) => {
    const u = new URL(r.url());
    if (!u.pathname.startsWith('/SupplyChain/Claims')) return;
    const req = r.request(); let text = '';
    try { text = await r.text(); } catch (_) { }
    const hdrs = r.headers(); const blob = (text + JSON.stringify(hdrs)).toLowerCase();
    for (const root of ROOTS) if (blob.includes(root)) result.rootLeak.push({ tag, path: u.pathname + u.search, root });
    let body = null; try { body = JSON.parse(text); } catch (_) { }
    net.push({ at: new Date().toISOString(), method: req.method(), path: u.pathname + u.search, status: r.status(), host: u.host,
      requestCorrelation: req.headers()['x-correlation-id'] || null, idempotencyKeyPresent: !!req.headers()['idempotency-key'],
      requestBodyText: req.method() === 'POST' ? req.postData() : null,
      responseCorrelation: hdrs['x-correlation-id'] || null, errorCode: body?.error?.code || null, errorCorrelation: body?.error?.correlationId || null,
      okKeys: body && !body.error ? Object.keys(body).sort() : null, itemCount: Array.isArray(body?.items) ? body.items.length : null,
      claimNumbers: Array.isArray(body?.items) ? body.items.map((i) => i.claimNumber) : null, status_: body?.status || null });
  });
  page.on('request', (r) => { const u = new URL(r.url()); if (u.host !== `127.0.0.1:${WEBPORT}` && !u.protocol.startsWith('data')) net.push({ foreignRequest: r.url() }); });
  return net;
}
async function ctx(browser, actor, opts = {}) {
  return browser.newContext({ storageState: actor ? statePath(actor) : undefined, timezoneId: 'UTC', locale: opts.locale || 'en-US', viewport: { width: 1366, height: 900 } });
}
const save = () => { const f = uniq(PHASE); result.endedAt = new Date().toISOString(); fs.writeFileSync(f, JSON.stringify(result, null, 2) + '\n'); console.log(JSON.stringify({ phase: PHASE, file: path.basename(f), verdicts: Object.fromEntries(Object.entries(result.cases).map(([k, v]) => [k, v.result])), rootLeak: result.rootLeak.length })); };
const verdict = (name, ok, detail) => { result.cases[name] = { result: ok ? 'PASS' : 'FAIL', ...detail }; };

async function antiforgery(page) { return page.locator('input[name="__RequestVerificationToken"]').first().inputValue().catch(() => null); }
async function adapter(page, method, url, body, extra = {}) {
  const tok = method === 'POST' ? await antiforgery(page) : null;
  const headers = { 'X-Correlation-Id': crypto.randomUUID(), ...(tok ? { RequestVerificationToken: tok } : {}), ...extra };
  if (method === 'POST') { headers['Content-Type'] = 'application/json'; headers['Idempotency-Key'] ??= crypto.randomUUID(); }
  const r = method === 'GET' ? await page.request.get(url, { headers, maxRedirects: 0 }) : await page.request.post(url, { headers, data: body, maxRedirects: 0 });
  let json = null; const text = await r.text(); try { json = JSON.parse(text); } catch (_) { }
  const leak = ROOTS.filter((x) => (text + JSON.stringify(r.headers())).toLowerCase().includes(x));
  return { status: r.status(), requestCorrelation: headers['X-Correlation-Id'], responseCorrelation: r.headers()['x-correlation-id'] || null,
    location: r.headers()['location'] || null, contentType: r.headers()['content-type'] || null, antiforgeryTokenUsed: !!tok,
    errorCode: json?.error?.code || null, errorCorrelation: json?.error?.correlationId || null, errorMessage: json?.error?.message || null,
    okKeys: json && !json.error ? Object.keys(json).sort() : null, items: Array.isArray(json?.items) ? json.items.map((i) => ({ claimId: i.claimId, shipmentId: i.shipmentId, status: i.status })) : null,
    rootLeak: leak };
}

async function login(browser) {
  fs.mkdirSync(STATE, { recursive: true, mode: 0o700 });
  for (const [actor, [email, tenant]] of Object.entries(ACT)) {
    const c = await ctx(browser, null); const page = await c.newPage(); const net = watch(page, `login-${actor}`);
    await page.goto(`${WEB}/account/login?tenantId=${tenant}&returnUrl=%2FSupplyChain%2FClaims`);
    await page.fill('#email', email); await page.fill('#password', process.env[`ACTOR_PW_${actor.toUpperCase()}`]);
    await Promise.all([page.waitForURL((u) => !u.pathname.startsWith('/account/login'), { timeout: 45000 }).catch(() => null), page.click('#loginForm button[type="submit"]')]);
    await page.waitForLoadState('networkidle').catch(() => null);
    const landed = new URL(page.url()).pathname;
    await c.storageState({ path: statePath(actor) }); fs.chmodSync(statePath(actor), 0o600);
    const cookies = (await c.cookies(WEB)).map((k) => k.name).sort();
    verdict(`login-${actor}`, !landed.startsWith('/account/login'), { landedOn: landed, cookieNames: cookies, stateFileWrittenTo: '<stateDir outside the repo, 0600>' });
    await c.close();
  }
}

async function vs1(browser) {
  const s = FX.shipments.DISPATCHED; const c = await ctx(browser, 'full'); const page = await c.newPage(); const net = watch(page, 'vs1');
  const listResp = page.waitForResponse((r) => new URL(r.url()).pathname === '/SupplyChain/Claims/api' && r.request().method() === 'GET');
  const pageResp = await page.goto(PAGE); const lr = await listResp;
  await page.locator('#dt-claims').waitFor({ state: 'visible' });
  await page.locator('.add-new').click();
  await page.locator('#claimShipmentId').fill(s.shipmentId);
  const resolved = page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/'));
  await page.locator('#btnResolveShipment').click(); const rr = await resolved; const resolveBody = await rr.json();
  await page.locator('#claimReasonCode').fill('DAMAGE'); await page.locator('#claimClaimedAmount').fill('250.00'); await page.locator('#claimCurrency').fill('EUR');
  const linkDisabled = await page.locator('#claimLinkCarrier').isDisabled();
  const post = page.waitForRequest((r) => r.url() === `${PAGE}/api` && r.method() === 'POST');
  const created = page.waitForResponse((r) => r.url() === `${PAGE}/api` && r.request().method() === 'POST');
  await page.locator('#btnSaveClaim').click();
  const sent = await post; const cr = await created; const cb = await cr.json();
  const reload = page.waitForResponse((r) => new URL(r.url()).pathname === '/SupplyChain/Claims/api' && r.request().method() === 'GET', { timeout: 30000 });
  await reload.catch(() => null);
  await page.waitForTimeout(800);
  const rowText = await page.locator('#dt-claims').innerText();
  await shot(page, 'vs1-after-create-en');
  verdict('CU-VS1', pageResp.status() === 200 && lr.status() === 200 && rr.status() === 200 && cr.status() === 201 && cb.status === 'Open' && /^CLM-/.test(cb.claimNumber || '')
    && sent.postData() === `{"shipmentId":"${s.shipmentId}","reasonCode":"DAMAGE","claimedAmount":"250.00","currency":"EUR"}` && rowText.includes('250.00') && linkDisabled,
  { page: pageResp.status(), list: lr.status(), resolve: rr.status(), resolveKeys: Object.keys(resolveBody).sort(), carrierCheckboxDisabled: linkDisabled,
    createStatus: cr.status(), createBody: cb, sentBody: sent.postData(), idempotencyKeyPresent: !!sent.headers()['idempotency-key'],
    browserRequestCorrelation: sent.headers()['x-correlation-id'], browserResponseCorrelation: cr.headers()['x-correlation-id'],
    responseCorrelationIsRoot: (cr.headers()['x-correlation-id'] || '').toLowerCase() === (s.lifecycleCorrelationId || '').toLowerCase(),
    reloadShows25000: rowText.includes('250.00') });
  result.cases['CU-VS1'].network = net; result.createdClaimId = cb.claimId;
  await c.close();
}

function spec() {
  const specSrc = path.join(RUNTIME, 'claims-ui.spec.mjs');
  const env = { PATH: process.env.PATH, HOME: process.env.HOME, TMPDIR: process.env.TMPDIR, NODE_PATH: process.env.NODE_PATH || '',
    CLAIMS_BASE_URL: WEB, CLAIMS_STATE_FULL: statePath('full'), CLAIMS_STATE_READONLY: statePath('readonly'), CLAIMS_STATE_NOREAD: statePath('noread'),
    CLAIMS_STATE_LEB: statePath('leb'), CLAIMS_SHIPMENT_DISPATCHED: FX.shipments.DISPATCHED.shipmentId, CLAIMS_SHIPMENT_DRAFT: FX.shipments.DRAFT.shipmentId,
    CLAIMS_SHIPMENT_CARRIER: FX.shipments.CARRIER.shipmentId, CLAIMS_EVIDENCE_DIR: path.join(PNG, 'spec'), PW_JSON_OUT: path.join(OUTD, 'spec-report.json') };
  fs.mkdirSync(env.CLAIMS_EVIDENCE_DIR, { recursive: true });
  const cli = path.join(RUNTIME, '..', 'node_modules/@playwright/test/cli.js');
  const r = spawnSync(process.execPath, [cli, 'test', '-c', path.join(RUNTIME, 'playwright.config.mjs')], { cwd: RUNTIME, env, encoding: 'utf8', timeout: 900000 });
  result.cases.spec = { result: r.status === 0 ? 'PASS' : 'FAIL', exit: r.status, specSha256: sha(fs.readFileSync(specSrc)), stdoutTail: (r.stdout || '').slice(-6000), stderrTail: (r.stderr || '').slice(-2000) };
  for (const f of fs.readdirSync(env.CLAIMS_EVIDENCE_DIR)) result.png.push({ name: `spec/${f}`, sha256: sha(fs.readFileSync(path.join(env.CLAIMS_EVIDENCE_DIR, f))) });
}

async function checksNeg(browser) {
  const S = FX.shipments, C = FX.claims; const unknown = '00000000-0000-4000-8000-000000000001';
  // 401: unauthenticated JSON adapter (no redirect) and page (standard login redirect)
  { const c = await ctx(browser, null); const page = await c.newPage(); await page.goto(`${WEB}/account/login?tenantId=${T1}`);
    const api = await adapter(page, 'GET', `${PAGE}/api`); const pg = await page.request.get(PAGE, { maxRedirects: 0 });
    verdict('401-json-adapter', api.status === 401 && !api.location && (api.contentType || '').includes('json'), { api, pageStatus: pg.status(), pageLocationPath: pg.headers()['location'] ? new URL(pg.headers()['location'], WEB).pathname : null });
    await c.close(); }
  // noread: page = _AccessDenied only; list adapter 403 JSON; resolve 403
  { const c = await ctx(browser, 'noread'); const page = await c.newPage(); const net = watch(page, 'noread');
    const r = await page.goto(PAGE); await page.waitForLoadState('networkidle').catch(() => null);
    const dom = { table: await page.locator('#dt-claims').count(), skeleton: await page.locator('#skeleton-loader').count(), filter: await page.locator('#inlineFilterHost').count(),
      addNew: await page.locator('.add-new').count(), title: await page.locator('h4, h5').allInnerTexts() };
    await shot(page, 'uas001-noread-access-denied-en');
    const list = await adapter(page, 'GET', `${PAGE}/api`); const res = await adapter(page, 'GET', `${PAGE}/api/shipments/${S.DISPATCHED.shipmentId}`);
    verdict('CU-06-noread', r.status() === 200 && !new URL(page.url()).pathname.startsWith('/account') && dom.table === 0 && dom.skeleton === 0 && dom.filter === 0 && dom.addNew === 0
      && list.status === 403 && list.errorCode === 'FORBIDDEN' && res.status === 403 && net.filter((n) => n.path && n.path.startsWith('/SupplyChain/Claims/api')).length === 0,
    { pageStatus: r.status(), finalPath: new URL(page.url()).pathname, dom, listAdapter: list, resolveAdapter: res, browserAdapterCalls: net.filter((n) => n.path && n.path.startsWith('/SupplyChain/Claims/api')).length });
    await c.close(); }
  // readonly: list OK, no CTA/actions; direct POST create and transition → 403 (antiforgery token taken from its own page if rendered)
  { const c = await ctx(browser, 'readonly'); const page = await c.newPage(); await page.goto(PAGE); await page.locator('#dt-claims').waitFor({ state: 'visible' }); await page.waitForTimeout(800);
    const dom = { addNew: await page.locator('.add-new').count(), actions: await page.locator('.js-transition').count(), rows: await page.locator('#dt-claims tbody tr').count() };
    const create = await adapter(page, 'POST', `${PAGE}/api`, `{"shipmentId":"${S.DISPATCHED.shipmentId}","reasonCode":"X","claimedAmount":"1.00","currency":"EUR"}`);
    const trans = await adapter(page, 'POST', `${PAGE}/api/${C.OPEN1.claimId}/transition?shipmentId=${S.SEED.shipmentId}`, `{"targetStatus":"Investigating","occurredAt":"2026-09-26T12:00:00+00:00"}`);
    await shot(page, 'readonly-list-no-cta-en');
    verdict('CU-07-readonly', dom.addNew === 0 && dom.actions === 0 && create.status === 403 && trans.status === 403, { dom, directCreate: create, directTransition: trans });
    await c.close(); }
  // cross-LE (leb) and cross-tenant (t2user); soft-deleted and unknown; identical 404 shapes (CU-14/CU-15/CU-16)
  const full = await ctx(browser, 'full'); const fp = await full.newPage(); await fp.goto(PAGE); await fp.locator('#dt-claims').waitFor({ state: 'visible' });
  const leb = await ctx(browser, 'leb'); const lp = await leb.newPage(); await lp.goto(PAGE); await lp.locator('#dt-claims').waitFor({ state: 'visible' });
  const t2 = await ctx(browser, 't2user'); const tp = await t2.newPage(); await tp.goto(PAGE); await tp.locator('#dt-claims').waitFor({ state: 'visible' });
  const fullList = await adapter(fp, 'GET', `${PAGE}/api`); const lebList = await adapter(lp, 'GET', `${PAGE}/api`); const t2List = await adapter(tp, 'GET', `${PAGE}/api`);
  const lebFiltered = await adapter(lp, 'GET', `${PAGE}/api?shipmentId=${S.SEED.shipmentId}`);
  const t1ids = new Set(Object.values(C).filter((x) => x.shipmentId !== S.T2SHIP.shipmentId).map((x) => x.claimId));
  verdict('CU-16-cross-LE-list', lebList.status === 200 && lebList.items.every((i) => !t1ids.has(i.claimId)) && lebFiltered.status === 200 && lebFiltered.items.length === 0,
    { lebListCount: lebList.items?.length, lebFilteredBySeedShipment: lebFiltered.items?.length });
  verdict('isolation-cross-tenant-list', t2List.status === 200 && t2List.items.length === 1 && t2List.items[0].claimId === C.T2CLAIM.claimId && fullList.items.every((i) => i.claimId !== C.T2CLAIM.claimId),
    { t2ListClaimIds: t2List.items?.map((i) => i.claimId), fullListContainsT2Claim: fullList.items?.some((i) => i.claimId === C.T2CLAIM.claimId), fullListCount: fullList.items?.length,
      fullListContainsSoftDeletedClaim: fullList.items?.some((i) => i.claimId === C.SOFTDEL1.claimId) });
  verdict('soft-deleted-claim-not-listed', fullList.items.every((i) => i.claimId !== C.SOFTDEL1.claimId), { softDeletedClaimId: C.SOFTDEL1.claimId });
  const nf = { unknown: await adapter(fp, 'GET', `${PAGE}/api/shipments/${unknown}`), foreignTenant: await adapter(fp, 'GET', `${PAGE}/api/shipments/${S.T2SHIP.shipmentId}`),
    softDeleted: await adapter(fp, 'GET', `${PAGE}/api/shipments/${S.SOFTDEL.shipmentId}`), foreignLeFromLeb: await adapter(lp, 'GET', `${PAGE}/api/shipments/${S.DISPATCHED.shipmentId}`),
    foreignTenantFromT2: await adapter(tp, 'GET', `${PAGE}/api/shipments/${S.DISPATCHED.shipmentId}`) };
  const shape = (x) => JSON.stringify([x.status, x.errorCode, x.errorMessage, x.okKeys]);
  verdict('CU-14-safe-not-found-identical', Object.values(nf).every((x) => x.status === 404 && x.errorCode === 'CLAIM_NOT_FOUND' && x.errorCorrelation === x.responseCorrelation)
    && new Set(Object.values(nf).map(shape)).size === 1, { cases: nf });
  const tn = { softDeletedClaim: await adapter(fp, 'POST', `${PAGE}/api/${C.SOFTDEL1.claimId}/transition?shipmentId=${S.SEED.shipmentId}`, `{"targetStatus":"Investigating","occurredAt":"2026-09-26T12:00:00+00:00"}`),
    foreignTenantClaim: await adapter(fp, 'POST', `${PAGE}/api/${C.T2CLAIM.claimId}/transition?shipmentId=${S.SEED.shipmentId}`, `{"targetStatus":"Investigating","occurredAt":"2026-09-26T12:00:00+00:00"}`),
    foreignLeClaimFromLeb: await adapter(lp, 'POST', `${PAGE}/api/${C.OPEN1.claimId}/transition?shipmentId=${S.SEED.shipmentId}`, `{"targetStatus":"Investigating","occurredAt":"2026-09-26T12:00:00+00:00"}`) };
  verdict('CU-15-transition-safe-not-found', Object.values(tn).every((x) => x.status === 404 && x.errorCode === 'CLAIM_NOT_FOUND'), { cases: tn });
  // CU-17 direct POST on a Draft shipment → 422; CU-23 header = error.correlationId, not the root
  const draft = await adapter(fp, 'POST', `${PAGE}/api`, `{"shipmentId":"${S.DRAFT.shipmentId}","reasonCode":"X","claimedAmount":"10.00","currency":"EUR"}`);
  verdict('CU-17-direct-ineligible-422', draft.status === 422 && draft.errorCode === 'CLAIM_SHIPMENT_INELIGIBLE', { draft });
  const allErr = [...Object.values(nf), ...Object.values(tn), draft];
  verdict('CU-23-support-ref-not-root', allErr.every((x) => x.errorCorrelation && x.errorCorrelation === x.responseCorrelation && !ROOTS.includes(String(x.errorCorrelation).toLowerCase())),
    { pairs: allErr.map((x) => [x.responseCorrelation, x.errorCorrelation]) });
  await full.close(); await leb.close(); await t2.close();
}

async function checksMut(browser) {
  const s = FX.shipments.CARRIER; const c = await ctx(browser, 'full'); const page = await c.newPage(); const net = watch(page, 'cu10');
  await page.goto(PAGE); await page.locator('#dt-claims').waitFor({ state: 'visible' });
  await page.locator('.add-new').click(); await page.locator('#claimShipmentId').fill(s.shipmentId);
  const resolved = page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/'));
  await page.locator('#btnResolveShipment').click(); const rr = await resolved; const rb = await rr.json();
  const enabled = await page.locator('#claimLinkCarrier').isEnabled();
  if (enabled) await page.locator('#claimLinkCarrier').check();
  await page.locator('#claimReasonCode').fill('CARRIER-DAMAGE'); await page.locator('#claimClaimedAmount').fill('40.00'); await page.locator('#claimCurrency').fill('EUR');
  const post = page.waitForRequest((r) => r.url() === `${PAGE}/api` && r.method() === 'POST');
  const created = page.waitForResponse((r) => r.url() === `${PAGE}/api` && r.request().method() === 'POST');
  await page.locator('#btnSaveClaim').click(); const sent = await post; const cr = await created;
  await page.waitForTimeout(800); await shot(page, 'cu10-carrier-linked-create-en');
  verdict('CU-10-carrier-linked', rr.status() === 200 && rb.carrierId === FX.carrier && enabled && cr.status() === 201 && JSON.parse(sent.postData()).carrierId === FX.carrier,
    { resolveBody: rb, checkboxEnabled: enabled, sentBody: sent.postData(), createStatus: cr.status(), createBody: await cr.json() });
  result.cases['CU-10-carrier-linked'].network = net;
  await c.close();
}


// ui-flows <mode delivered|fixes> [fixed index.js] [expected sha256]: row-scoped UI flows the Q64a spec could not reach
// (spec selector/race defects S-01/S-02). In `fixes` mode the Claims index.js is served from the FIXES file through the
// kit's fulfillHashed (served bytes hashed and bound by K09); nothing else changes.
async function uiFlows(browser) {
  const { fulfillHashed } = createRequire(path.join(RUNTIME, 'package.json'))('/Users/natig/Projects/ERP-vNext-recovery/scripts/evidence-kit/templates/served-asset-hash.js');
  const S = FX.shipments; const tag = MODE; result.mode = MODE;
  const mk = async () => { const c = await ctx(browser, 'full');
    if (MODE === 'fixes') await c.route('**/assets/js/SupplyChain/Claims/index.js*', (route) => fulfillHashed(route, FIXFILE, { evidence: EVD, label: 'fix02-claims-index-js', expect: FIXSHA }));
    return c; };
  const ready = async (page) => { await page.goto(PAGE); await page.locator('#dt-claims').waitFor({ state: 'visible' }); await page.locator('#dt-claims tbody tr .js-quick-view').first().waitFor({ state: 'visible', timeout: 20000 }); };
  const jsErrors = () => result.console.filter((m) => /xhr\.abort|TypeError/.test(m.text)).length;
  const rowOf = (page, id) => page.locator(`#dt-claims tbody tr:has(.js-quick-view[data-claim-id="${id}"])`);
  const openAction = async (page, id, target) => { const row = rowOf(page, id); await row.locator('.dropdown-toggle').click(); await row.locator(`.js-transition[data-target-status="${target}"]`).click(); await page.locator('#btnSubmitTransition').waitFor({ state: 'visible' }); };
  const confirm = async (page) => { await page.locator('.swal2-confirm').first().click(); };
  const isList = (r) => new URL(r.url()).pathname === '/SupplyChain/Claims/api' && r.request().method() === 'GET';
  // 1 create → reload shows the amount (CU-VS1 browser half, pack §32.8 reload)
  { const c = await mk(); const page = await c.newPage(); watch(page, `${tag}-create`); await ready(page);
    const amount = MODE === 'fixes' ? '262.00' : '261.00'; const e0 = jsErrors();
    await page.locator('.add-new').click(); await page.locator('#claimShipmentId').fill(S.DISPATCHED.shipmentId);
    await Promise.all([page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), page.locator('#btnResolveShipment').click()]);
    await page.locator('#claimReasonCode').fill('DAMAGE'); await page.locator('#claimClaimedAmount').fill(amount); await page.locator('#claimCurrency').fill('EUR');
    const created = page.waitForResponse((r) => r.url() === `${PAGE}/api` && r.request().method() === 'POST');
    await page.locator('#btnSaveClaim').click(); const cr = await created;
    const reload = await page.waitForResponse(isList, { timeout: 10000 }).then(() => true).catch(() => false);
    await page.waitForTimeout(800); const shown = (await page.locator('#dt-claims').innerText()).includes(amount);
    await shot(page, `${tag}-create-reload-en`);
    verdict('create-then-reload', cr.status() === 201 && reload && shown && jsErrors() === e0, { createStatus: cr.status(), reloadRequestAfterCreate: reload, rowShowsAmount: shown, amount, newJsErrors: jsErrors() - e0 });
    await c.close(); }
  // 2 filter Apply/Reset (CU-02)
  { const c = await mk(); const page = await c.newPage(); watch(page, `${tag}-filter`); await ready(page);
    await page.locator('.dt-filter-btn').click(); await page.locator('#filterShipmentId').fill(S.SEED.shipmentId);
    const req = page.waitForRequest((r) => r.url().includes('/SupplyChain/Claims/api?'), { timeout: 10000 }).catch(() => null);
    await page.locator('#btnFilterApply').click(); const r = await req; const keys = r ? [...new URL(r.url()).searchParams.keys()].sort() : null;
    const resetReq = page.waitForRequest((x) => isList({ url: () => x.url(), request: () => x }) , { timeout: 10000 }).then(() => true).catch(() => false);
    await page.locator('#btnFilterReset').click(); const reset = await resetReq;
    verdict('CU-02-filter', !!r && JSON.stringify(keys) === '["shipmentId"]' && !r.headers()['x-tenant-id'] && reset, { applyRequestSent: !!r, queryKeys: keys, resetReloaded: reset });
    await c.close(); }
  // 3 QuickView without a by-ID request (CU-31)
  { const c = await mk(); const page = await c.newPage(); const net = watch(page, `${tag}-quickview`); await ready(page);
    const n0 = net.length; await page.locator('#dt-claims tbody tr .js-quick-view').first().click();
    await page.locator('#offcanvasDetailsPreview').waitFor({ state: 'visible' }); await page.waitForTimeout(500);
    const calls = net.slice(n0).filter((x) => x.path && x.path.startsWith('/SupplyChain/Claims/api'));
    const text = await page.locator('#offcanvasDetailsPreview').innerText(); await shot(page, `${tag}-quickview-en`);
    verdict('CU-31-quickview', calls.length === 0 && (await page.locator('#oc-claim-id').innerText()).length > 0 && !/\bEdit\b/.test(text), { adapterCallsDuringQuickView: calls.length });
    await c.close(); }
  // pick targets from the live list
  const probe = await mk(); const pp = await probe.newPage(); await ready(pp); const list = await adapter(pp, 'GET', `${PAGE}/api?shipmentId=${S.SEED.shipmentId}`); await probe.close();
  const open = (list.items || []).find((i) => i.status === 'Open'); const inv = (list.items || []).find((i) => i.status === 'Investigating');
  result.targets = { open: open?.claimId || null, investigating: inv?.claimId || null };
  // 4 Open → Investigating via row action + premium confirm (CU-08)
  if (open) { const c = await mk(); const page = await c.newPage(); watch(page, `${tag}-transition`); await ready(page); const e0 = jsErrors();
    await openAction(page, open.claimId, 'Investigating');
    const occurred = await page.locator('#transitionOccurredAt').inputValue();
    await page.locator('#btnSubmitTransition').click();
    const resp = page.waitForResponse((r) => r.url().includes(`/api/${open.claimId}/transition?shipmentId=`)); await confirm(page); const tr = await resp;
    const reload = await page.waitForResponse(isList, { timeout: 10000 }).then(() => true).catch(() => false);
    await page.waitForTimeout(500); await shot(page, `${tag}-transition-investigating-en`);
    verdict('CU-08-open-to-investigating', tr.status() === 200 && reload && /([+-]\d{2}:\d{2}|Z)$/.test(occurred) && jsErrors() === e0, { claimId: open.claimId, status: tr.status(), occurredAtHasOffset: /([+-]\d{2}:\d{2}|Z)$/.test(occurred), reloadAfter: reload, newJsErrors: jsErrors() - e0 });
    await c.close(); } else verdict('CU-08-open-to-investigating', false, { reason: 'no Open claim on SEED' });
  // 5 two profiles: A approves, B's already-open Reject → 422 INVALID_CLAIM_TRANSITION + support ref + reload (CU-21)
  if (inv) { const a = await mk(); const b = await mk(); const pa = await a.newPage(); const pb = await b.newPage(); watch(pa, `${tag}-stale-a`); watch(pb, `${tag}-stale-b`);
    await ready(pa); await ready(pb);
    await openAction(pb, inv.claimId, 'Rejected');
    await openAction(pa, inv.claimId, 'Approved'); await pa.locator('#transitionApprovedAmount').fill('10.00'); await pa.locator('#btnSubmitTransition').click();
    const ra = pa.waitForResponse((r) => r.url().includes(`/api/${inv.claimId}/transition`)); await confirm(pa); const a200 = await ra;
    await pb.locator('#btnSubmitTransition').click();
    const rb = pb.waitForResponse((r) => r.url().includes(`/api/${inv.claimId}/transition`)); await confirm(pb); const b422 = await rb; const bb = await b422.json();
    const reload = await pb.waitForResponse(isList, { timeout: 10000 }).then(() => true).catch(() => false);
    await pb.waitForTimeout(600); const alert = await pb.locator('#formClaimTransitionAlert, .swal2-popup, .toast').allInnerTexts();
    await shot(pb, `${tag}-stale-transition-422-en`);
    verdict('CU-21-stale-transition', a200.status() === 200 && b422.status() === 422 && bb?.error?.code === 'INVALID_CLAIM_TRANSITION' && reload
      && alert.join(' ').includes(bb.error.correlationId), { claimId: inv.claimId, approve: a200.status(), approvedAmountInResponse: (await a200.json()).approvedAmount, stale: b422.status(), code: bb?.error?.code,
      supportRefShown: alert.join(' ').includes(bb?.error?.correlationId || '#'), reloadAfter: reload });
    await a.close(); await b.close(); } else verdict('CU-21-stale-transition', false, { reason: 'no Investigating claim on SEED' });
}

const browser = (PHASE === 'spec' || PHASE === 'cleanup') ? null : await chromium.launch({ headless: true });
try {
  if (PHASE === 'login') await login(browser);
  else if (PHASE === 'vs1') await vs1(browser);
  else if (PHASE === 'spec') spec();
  else if (PHASE === 'checks-neg') await checksNeg(browser);
  else if (PHASE === 'checks-mut') await checksMut(browser);
  else if (PHASE === 'ui-flows') await uiFlows(browser);
  else if (PHASE === 'cleanup') { for (const a of Object.keys(ACT)) { try { fs.rmSync(statePath(a)); } catch (_) { } } result.cases.cleanup = { result: Object.keys(ACT).every((a) => !fs.existsSync(statePath(a))) ? 'PASS' : 'FAIL' }; }
  else throw new Error('unknown phase');
} catch (e) { result.error = String(e && e.message || e).slice(0, 1500); }
finally { if (browser) await browser.close(); save(); }
process.exit(result.error || Object.values(result.cases).some((c) => c.result === 'FAIL') || result.rootLeak.length ? 1 : 0);
