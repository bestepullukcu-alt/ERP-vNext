// Q129 browser/HTTP harness — independent VER of MOD-0187 Claims UI DRAFT v4 (archive 2b34741a…, Q64f) on BASE a8a236de + Q117.
// Copy of the Q122 harness (below, unchanged except this header block, the two new phases cu25/cu11 and their PH entries).
// Q129 phases: cu25 = CU-25 / Q122-D5 re-run in en and ar (create 400 → focus stays in the surface → Escape closes WITHOUT Tab →
// focus back on Add; PNG 03b-create-400-escape-<lang>); cu11 = the CU-11 part of Q122 `extra` (lexical 400/422 over HTTP + UI 400 keeps inputs),
// without the CU-10/CU-17 parts that need the matrix fixture.
// Q122 browser/HTTP harness — independent VER of MOD-0187 Claims UI DRAFT v3 (archive 66e72bf6…) on the Q103 accepted base
// + Q117 overlay, kit slot 9. Code derived from the Q64d harness (all §32.11 phases) and the Q64e harness (CU-05 skeleton/
// empty/error, CU-25 keyboard on both surfaces, D1/D2/D4 regression). No result of either lane is reused.
// Changes against Q64d (marked Q122): cu05 + cu25kbd + regression from Q64e; crafted keeps CU-03/CU-04/CU-13 only; cu18 per CT
// disposition (null root runtime; malformed root observed and recorded as F-0183-500); cu20 = Q64d cu20 + cu20b merged; cu10
// has two runs (nogrant expects 503 + zero write, grant expects 201); checks-neg also asserts .diten-access-denied.
// Run ONLY by the lane supervisor (task harness): it supplies ACTOR_PW_<LABEL>; this script never prints or writes them.
// Usage: node claims_runtime.mjs <phase> <webPort> <evidenceDir> <fixtureJson> <stateDir> <runtimeDir> [extra]
// PNGs only via page.screenshot({ path }) (owner decision MVP6-PNG-METHOD-OWNER-DECISION-01). All browser URLs are on
// 127.0.0.1:<webPort>. The harness never deletes a file.
import fs from 'node:fs'; import path from 'node:path'; import crypto from 'node:crypto'; import { spawnSync } from 'node:child_process';
import { createRequire } from 'node:module';

const [PHASE, WEBPORT, EVD, FXJSON, STATE, RUNTIME, EXTRA] = process.argv.slice(2);
const req = createRequire(path.join(RUNTIME, 'package.json'));
const { chromium } = req('playwright');
const PW_VERSION = req('playwright/package.json').version;
const WEB = `http://127.0.0.1:${WEBPORT}`;
const PAGE = `${WEB}/SupplyChain/Claims`;
const API = `${PAGE}/api`;
const T1 = '97c59330-dbc4-4665-b29c-0c26dbb5cc93', T2 = '00000000-0000-0000-0000-000000000001';
const ACT = { full: ['john.doe.t97@diten.com', T1], readonly: ['jane.smith.t97@diten.com', T1], noread: ['charlie.brown.t97@diten.com', T1],
  leb: ['bob.johnson.t97@diten.com', T1], t2user: ['john.doe.def@diten.com', T2] };
const OUTD = path.join(EVD, 'browser'); const PNG = path.join(EVD, 'png');
[OUTD, PNG].forEach((d) => fs.mkdirSync(d, { recursive: true }));
const FX = PHASE === 'login' && !fs.existsSync(FXJSON) ? { shipments: {}, claims: {} } : JSON.parse(fs.readFileSync(FXJSON, 'utf8')).fixtures;
const ROOTS = Object.values(FX.shipments).map((s) => s.lifecycleCorrelationId).filter(Boolean).map((r) => r.toLowerCase());
const TAG = EXTRA && ['login', 'checks-mut', 'matrix', 'matrixdom'].includes(PHASE) ? `${PHASE}-${EXTRA}` : PHASE;
const uniq = (base) => { let n = 1; while (fs.existsSync(path.join(OUTD, `${base}-a${n}.json`))) n++; return path.join(OUTD, `${base}-a${n}.json`); };
const result = { phase: PHASE, extra: EXTRA || null, startedAt: new Date().toISOString(), web: WEB, playwright: PW_VERSION, cases: {}, png: [], console: [], toasts: [], dialogs: [], rootLeak: [], foreign: [] };
const sha = (b) => crypto.createHash('sha256').update(b).digest('hex');
const statePath = (a) => path.join(STATE, `${a}.json`);
const iso = () => new Date().toISOString();

async function shot(page, name) {
  let n = name; let i = 2; while (fs.existsSync(path.join(PNG, `${n}.png`))) n = `${name}-${i++}`; // never overwrite a PNG
  const p = path.join(PNG, `${n}.png`); await page.screenshot({ path: p, fullPage: true });
  result.png.push({ name: `${n}.png`, sha256: sha(fs.readFileSync(p)), url: page.url(), capturedAt: iso(), phase: PHASE, tool: `playwright ${PW_VERSION} page.screenshot` });
}
function watch(page, tag) {
  const net = [];
  page.on('dialog', async (d) => { result.dialogs.push({ tag, type: d.type(), at: iso() }); await d.dismiss().catch(() => null); });
  page.on('console', (m) => {
    const text = m.text();
    const toast = /\[MOD-0013 showToast\] Triggered - Key: (.*) \| Type: (\w+)/.exec(text);
    if (toast) result.toasts.push({ tag, text: toast[1].slice(0, 400), type: toast[2], at: iso() });
    if (['error', 'warning'].includes(m.type())) result.console.push({ tag, type: m.type(), text: text.slice(0, 300) });
  });
  page.on('pageerror', (e) => result.console.push({ tag, type: 'pageerror', text: String(e.message || e).slice(0, 300) }));
  page.on('response', async (r) => {
    const u = new URL(r.url());
    if (u.host !== `127.0.0.1:${WEBPORT}`) return;
    const rq = r.request(); let text = '';
    const scan = u.pathname.startsWith('/SupplyChain/Claims') || u.pathname.startsWith('/api/personalization');
    try { text = await r.text(); } catch (_) { }
    const hdrs = r.headers(); const blob = (text + JSON.stringify(hdrs)).toLowerCase();
    // Q122: F8 root scan over EVERY same-origin response (page, assets, adapters), not only the Claims adapters
    for (const root of ROOTS) if (blob.includes(root)) result.rootLeak.push({ tag, path: u.pathname + u.search, root });
    if (!scan) return;
    let body = null; try { body = JSON.parse(text); } catch (_) { }
    net.push({ at: iso(), method: rq.method(), path: u.pathname + u.search, status: r.status(), host: u.host,
      requestCorrelation: rq.headers()['x-correlation-id'] || null, idempotencyKey: rq.headers()['idempotency-key'] || null,
      requestBodyText: rq.method() === 'POST' || rq.method() === 'PUT' ? rq.postData() : null,
      responseCorrelation: hdrs['x-correlation-id'] || null, errorCode: body?.error?.code || null, errorCorrelation: body?.error?.correlationId || null,
      okKeys: body && !body.error && !Array.isArray(body) ? Object.keys(body).sort() : null, itemCount: Array.isArray(body?.items) ? body.items.length : null });
  });
  page.on('request', (r) => { const u = new URL(r.url()); if (u.host !== `127.0.0.1:${WEBPORT}` && !u.protocol.startsWith('data')) { net.push({ foreignRequest: r.url() }); result.foreign.push({ tag, url: `${u.protocol}//${u.host}${u.pathname}` }); } });
  return net;
}
async function ctx(browser, actor, opts = {}) {
  return browser.newContext({ storageState: actor ? statePath(actor) : undefined, timezoneId: 'UTC', locale: opts.locale || 'en-US',
    viewport: opts.viewport || { width: 1366, height: 900 } });
}
const save = () => { const f = uniq(TAG); result.endedAt = iso(); fs.writeFileSync(f, JSON.stringify(result, null, 2) + '\n');
  console.log(JSON.stringify({ phase: PHASE, extra: EXTRA || null, file: path.basename(f), verdicts: Object.fromEntries(Object.entries(result.cases).map(([k, v]) => [k, v.result])),
    rootLeak: result.rootLeak.length, dialogs: result.dialogs.length, pageErrors: result.console.filter((m) => m.type === 'pageerror').length, error: result.error || null })); };
const verdict = (name, ok, detail) => { result.cases[name] = { result: ok ? 'PASS' : 'FAIL', ...detail }; };

async function antiforgery(page) { return page.locator('input[name="__RequestVerificationToken"]').first().inputValue().catch(() => null); }
async function adapter(page, method, url, body, extra = {}) {
  const tok = method === 'POST' ? await antiforgery(page) : null;
  const headers = { 'X-Correlation-Id': crypto.randomUUID(), ...(tok ? { RequestVerificationToken: tok } : {}), ...extra };
  if (method === 'POST') { headers['Content-Type'] = 'application/json'; headers['Idempotency-Key'] ??= crypto.randomUUID(); }
  const r = method === 'GET' ? await page.request.get(url, { headers, maxRedirects: 0 }) : await page.request.post(url, { headers, data: body, maxRedirects: 0 });
  let json = null; const text = await r.text(); try { json = JSON.parse(text); } catch (_) { }
  const leak = ROOTS.filter((x) => (text + JSON.stringify(r.headers())).toLowerCase().includes(x));
  return { status: r.status(), requestCorrelation: headers['X-Correlation-Id'], idempotencyKey: headers['Idempotency-Key'] || null,
    responseCorrelation: r.headers()['x-correlation-id'] || null, location: r.headers()['location'] || null, contentType: r.headers()['content-type'] || null,
    antiforgeryTokenUsed: !!tok, errorCode: json?.error?.code || null, errorCorrelation: json?.error?.correlationId || null, errorMessage: json?.error?.message || null,
    okKeys: json && !json.error ? Object.keys(json).sort() : null, claimId: json?.claimId || null, claimStatus: json?.status || null,
    approvedAmount: json?.approvedAmount ?? null, idempotentReplay: json?.idempotentReplay ?? null,
    items: Array.isArray(json?.items) ? json.items.map((i) => ({ claimId: i.claimId, shipmentId: i.shipmentId, status: i.status, claimedAmount: i.claimedAmount })) : null,
    rootLeak: leak };
}
const isList = (r) => new URL(r.url()).pathname === '/SupplyChain/Claims/api' && r.request().method() === 'GET';
const ready = async (page, url = PAGE) => { await page.goto(url); await page.locator('#dt-claims').waitFor({ state: 'visible' });
  await page.locator('#skeleton-loader').waitFor({ state: 'hidden' }); await page.locator('#dt-claims tbody tr').first().waitFor(); await page.waitForTimeout(300); };
const rowOf = (page, id) => page.locator(`#dt-claims tbody tr:has(.js-quick-view[data-claim-id="${id}"])`);
const openAction = async (page, id, target) => { const row = rowOf(page, id); await row.locator('.dropdown-toggle').click();
  await row.locator(`.js-transition[data-target-status="${target}"]`).click(); await page.locator('#btnSubmitTransition').waitFor({ state: 'visible' }); };
const confirm = async (page) => { await page.locator('.swal2-confirm').first().click(); };
const jsErrors = () => result.console.filter((m) => m.type === 'pageerror' || /xhr\.abort|TypeError|already been declared/.test(m.text)).length;
const filterById = async (page, shipmentId) => { await page.locator('.dt-filter-btn').click(); await page.locator('#filterShipmentId').fill(shipmentId);
  await Promise.all([page.waitForResponse(isList), page.locator('#btnFilterApply').click()]); await page.waitForTimeout(400); };
// Q122 (from Q64e)
const visibleBox = (loc) => loc.evaluate((e) => { const s = getComputedStyle(e); const r = e.getBoundingClientRect(); return s.display !== 'none' && s.visibility !== 'hidden' && r.height > 0; }).catch(() => false);
const focusIn = (page, sel) => page.evaluate((s) => !!document.activeElement?.closest(s), sel);
const focusId = (page) => page.evaluate(() => document.activeElement?.id || document.activeElement?.className || document.activeElement?.tagName || null);

// ───────────────────────────── login ─────────────────────────────
async function login(browser) {
  fs.mkdirSync(STATE, { recursive: true, mode: 0o700 });
  const only = EXTRA ? EXTRA.split(',') : Object.keys(ACT);
  for (const actor of only) {
    const [email, tenant] = ACT[actor];
    const c = await ctx(browser, null); const page = await c.newPage(); watch(page, `login-${actor}`);
    await page.goto(`${WEB}/account/login?tenantId=${tenant}&returnUrl=%2FSupplyChain%2FClaims`);
    await page.fill('#email', email); await page.fill('#password', process.env[`ACTOR_PW_${actor.toUpperCase()}`]);
    await Promise.all([page.waitForURL((u) => !u.pathname.startsWith('/account/login'), { timeout: 45000 }).catch(() => null), page.click('#loginForm button[type="submit"]')]);
    await page.waitForLoadState('networkidle').catch(() => null);
    const landed = new URL(page.url()).pathname;
    await c.storageState({ path: statePath(actor) }); fs.chmodSync(statePath(actor), 0o600);
    const cookies = (await c.cookies(WEB)).map((k) => ({ name: k.name, httpOnly: k.httpOnly })).sort((a, b) => a.name.localeCompare(b.name));
    verdict(`login-${actor}`, !landed.startsWith('/account/login'), { landedOn: landed, cookies, stateFileWrittenTo: '<stateDir outside the repo, 0600>' });
    await c.close();
  }
}

// ───────────────────────────── CU-VS1 / CU-01 / CU-09 ─────────────────────────────
async function vs1(browser) {
  const s = FX.shipments.DISPATCHED; const c = await ctx(browser, 'full'); const page = await c.newPage(); const net = watch(page, 'vs1');
  const listResp = page.waitForResponse(isList);
  const pageResp = await page.goto(PAGE); const lr = await listResp;
  await page.locator('#dt-claims').waitFor({ state: 'visible' });
  await page.locator('.add-new').click();
  await page.locator('#claimShipmentId').fill(s.shipmentId);
  const resolved = page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/'));
  await page.locator('#btnResolveShipment').click(); const rr = await resolved; const resolveBody = await rr.json();
  await page.locator('#claimReasonCode').fill('DAMAGE'); await page.locator('#claimClaimedAmount').fill('250.00'); await page.locator('#claimCurrency').fill('EUR');
  const linkDisabled = await page.locator('#claimLinkCarrier').isDisabled();
  const post = page.waitForRequest((r) => r.url() === API && r.method() === 'POST');
  const created = page.waitForResponse((r) => r.url() === API && r.request().method() === 'POST');
  const reload = page.waitForResponse((r) => isList(r) && r.request().timing().startTime > 0, { timeout: 30000 });
  await page.locator('#btnSaveClaim').click();
  const sent = await post; const cr = await created; const cb = await cr.json();
  const reloaded = await reload.then(() => true).catch(() => false);
  await page.waitForTimeout(800);
  const rowText = await page.locator('#dt-claims').innerText();
  await shot(page, 'vs1-after-create-en');
  const adapterCalls = net.filter((n) => n.path);
  verdict('CU-VS1', pageResp.status() === 200 && lr.status() === 200 && rr.status() === 200 && cr.status() === 201 && cb.status === 'Open' && /^CLM-/.test(cb.claimNumber || '')
    && sent.postData() === `{"shipmentId":"${s.shipmentId}","reasonCode":"DAMAGE","claimedAmount":"250.00","currency":"EUR"}` && rowText.includes('250.00') && linkDisabled && reloaded,
  { page: pageResp.status(), list: lr.status(), resolve: rr.status(), resolveKeys: Object.keys(resolveBody).sort(), carrierCheckboxDisabled: linkDisabled,
    createStatus: cr.status(), createBody: cb, sentBody: sent.postData(), idempotencyKeyPresent: !!sent.headers()['idempotency-key'],
    browserRequestCorrelation: sent.headers()['x-correlation-id'], browserResponseCorrelation: cr.headers()['x-correlation-id'],
    responseCorrelationIsRoot: (cr.headers()['x-correlation-id'] || '').toLowerCase() === (s.lifecycleCorrelationId || '').toLowerCase(),
    listReloadedAfterCreate: reloaded, reloadShows25000: rowText.includes('250.00') });
  // Q122 CU-01: same-origin chain — every browser call on the Web origin, none on 5900/5961 or any other host except the shell's fonts
  verdict('CU-01-same-origin', adapterCalls.every((n) => n.host === `127.0.0.1:${WEBPORT}`) && result.foreign.every((f) => /fonts\.(googleapis|gstatic)\.com/.test(f.url)),
    { adapterCallHosts: [...new Set(adapterCalls.map((n) => n.host))], foreignHosts: [...new Set(result.foreign.map((f) => f.url.split('/').slice(0, 3).join('/')))],
      noAuthorizationHeaderFromBrowser: true, note: 'Authorization is added server-side by the MVC adapter; the browser holds no bearer token (see CU-26 storage scan)' });
  // Q122 CU-09: body parity — exact body text, key order, no scope headers
  verdict('CU-09-create-body-parity', sent.postData() === `{"shipmentId":"${s.shipmentId}","reasonCode":"DAMAGE","claimedAmount":"250.00","currency":"EUR"}`
    && !sent.headers()['x-tenant-id'] && !sent.headers()['x-legal-entity-id'] && !!sent.headers()['idempotency-key'],
    { sentBody: sent.postData(), scopeHeaders: { tenant: sent.headers()['x-tenant-id'] || null, legalEntity: sent.headers()['x-legal-entity-id'] || null } });
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
  const cli = path.join(path.dirname(req.resolve('@playwright/test')), 'cli.js');
  const r = spawnSync(process.execPath, [cli, 'test', '-c', path.join(RUNTIME, 'playwright.config.mjs')], { cwd: RUNTIME, env, encoding: 'utf8', timeout: 900000 });
  result.cases.spec = { result: r.status === 0 ? 'PASS' : 'FAIL', exit: r.status, specSha256: sha(fs.readFileSync(specSrc)), stdoutTail: (r.stdout || '').slice(-6000), stderrTail: (r.stderr || '').slice(-2000) };
  for (const f of fs.readdirSync(env.CLAIMS_EVIDENCE_DIR)) result.png.push({ name: `spec/${f}`, sha256: sha(fs.readFileSync(path.join(env.CLAIMS_EVIDENCE_DIR, f))), phase: 'spec', tool: `@playwright/test ${PW_VERSION} page.screenshot` });
}

// ───────────────────────────── zero-write negatives, isolation, denied ─────────────────────────────
async function checksNeg(browser) {
  const S = FX.shipments, C = FX.claims; const unknown = '00000000-0000-4000-8000-000000000001';
  { const c = await ctx(browser, null); const page = await c.newPage(); await page.goto(`${WEB}/account/login?tenantId=${T1}`);
    const api = await adapter(page, 'GET', API); const pg = await page.request.get(PAGE, { maxRedirects: 0 });
    verdict('401-json-adapter', api.status === 401 && !api.location && (api.contentType || '').includes('json'),
      { api, bodyCodeIsUnauthenticated: api.errorCode === 'UNAUTHENTICATED', pageStatus: pg.status(), pageLocationPath: pg.headers()['location'] ? new URL(pg.headers()['location'], WEB).pathname : null });
    await c.close(); }
  { const c = await ctx(browser, 'noread'); const page = await c.newPage(); const net = watch(page, 'noread');
    const r = await page.goto(PAGE); await page.waitForLoadState('networkidle').catch(() => null);
    const dom = { accessDenied: await page.locator('.diten-access-denied').count(), table: await page.locator('#dt-claims').count(), skeleton: await page.locator('#skeleton-loader').count(),
      filter: await page.locator('#inlineFilterHost').count(), addNew: await page.locator('.add-new').count(), shell: await page.locator('.layout-wrapper').count(),
      text: (await page.locator('.diten-access-denied').innerText().catch(() => '')).slice(0, 300),
      technicalText: /\b403\b|Forbidden|Permission denied/i.test(await page.locator('body').innerText()) };
    await shot(page, 'uas001-noread-access-denied-en');
    const list = await adapter(page, 'GET', API); const res = await adapter(page, 'GET', `${API}/shipments/${S.DISPATCHED.shipmentId}`);
    const create = await adapter(page, 'POST', API, `{"shipmentId":"${S.DISPATCHED.shipmentId}","reasonCode":"X","claimedAmount":"1.00","currency":"EUR"}`);
    verdict('CU-06-noread', r.status() === 200 && !new URL(page.url()).pathname.startsWith('/account') && dom.accessDenied === 1 && dom.shell === 1 && dom.table === 0 && dom.skeleton === 0
      && dom.filter === 0 && dom.addNew === 0 && !dom.technicalText && list.status === 403 && list.errorCode === 'FORBIDDEN' && res.status === 403 && create.status === 403
      && net.filter((n) => n.path && n.path.startsWith('/SupplyChain/Claims/api')).length === 0,
    { pageStatus: r.status(), finalPath: new URL(page.url()).pathname, dom, listAdapter: list, resolveAdapter: res, createAdapter: create,
      browserAdapterCalls: net.filter((n) => n.path && n.path.startsWith('/SupplyChain/Claims/api')).length });
    await c.close(); }
  { const c = await ctx(browser, 'readonly'); const page = await c.newPage(); watch(page, 'readonly'); await ready(page); await page.waitForTimeout(500);
    const dom = { addNew: await page.locator('.add-new').count(), actions: await page.locator('.js-transition').count(), rows: await page.locator('#dt-claims tbody tr').count() };
    const create = await adapter(page, 'POST', API, `{"shipmentId":"${S.DISPATCHED.shipmentId}","reasonCode":"X","claimedAmount":"1.00","currency":"EUR"}`);
    const trans = await adapter(page, 'POST', `${API}/${C.OPEN1.claimId}/transition?shipmentId=${S.SEED.shipmentId}`, `{"targetStatus":"Investigating","occurredAt":"2026-09-27T12:00:00+00:00"}`);
    const resolve = await adapter(page, 'GET', `${API}/shipments/${S.DISPATCHED.shipmentId}`);
    await shot(page, 'readonly-list-no-cta-en');
    verdict('CU-07-readonly', dom.rows > 0 && dom.addNew === 0 && dom.actions === 0 && create.status === 403 && trans.status === 403 && resolve.status === 403,
      { dom, directCreate: create, directTransition: trans, directResolve: resolve });
    await c.close(); }
  const full = await ctx(browser, 'full'); const fp = await full.newPage(); await ready(fp);
  const leb = await ctx(browser, 'leb'); const lp = await leb.newPage(); await lp.goto(PAGE); await lp.locator('#dt-claims').waitFor({ state: 'visible' });
  const t2 = await ctx(browser, 't2user'); const tp = await t2.newPage(); await tp.goto(PAGE); await tp.locator('#dt-claims').waitFor({ state: 'visible' });
  await shot(lp, 'cu16-leb-list-en');
  const fullList = await adapter(fp, 'GET', API); const lebList = await adapter(lp, 'GET', API); const t2List = await adapter(tp, 'GET', API);
  const lebFiltered = await adapter(lp, 'GET', `${API}?shipmentId=${S.SEED.shipmentId}`);
  const t1ids = new Set(Object.values(C).filter((x) => x.shipmentId !== S.T2SHIP.shipmentId).map((x) => x.claimId));
  verdict('CU-16-cross-LE-list', lebList.status === 200 && lebList.items.every((i) => !t1ids.has(i.claimId)) && lebFiltered.status === 200 && lebFiltered.items.length === 0,
    { lebListCount: lebList.items?.length, lebFilteredBySeedShipment: lebFiltered.items?.length, fullListCount: fullList.items?.length });
  verdict('isolation-cross-tenant-list', t2List.status === 200 && t2List.items.length === 1 && t2List.items[0].claimId === C.T2CLAIM.claimId && fullList.items.every((i) => i.claimId !== C.T2CLAIM.claimId),
    { t2ListClaimIds: t2List.items?.map((i) => i.claimId), fullListContainsT2Claim: fullList.items?.some((i) => i.claimId === C.T2CLAIM.claimId), fullListCount: fullList.items?.length });
  verdict('soft-deleted-claim-not-listed', fullList.items.every((i) => i.claimId !== C.SOFTDEL1.claimId), { softDeletedClaimId: C.SOFTDEL1.claimId });
  const nf = { unknown: await adapter(fp, 'GET', `${API}/shipments/${unknown}`), foreignTenant: await adapter(fp, 'GET', `${API}/shipments/${S.T2SHIP.shipmentId}`),
    softDeleted: await adapter(fp, 'GET', `${API}/shipments/${S.SOFTDEL.shipmentId}`), foreignLeFromLeb: await adapter(lp, 'GET', `${API}/shipments/${S.DISPATCHED.shipmentId}`),
    foreignTenantFromT2: await adapter(tp, 'GET', `${API}/shipments/${S.DISPATCHED.shipmentId}`) };
  const shape = (x) => JSON.stringify([x.status, x.errorCode, x.errorMessage, x.okKeys]);
  verdict('CU-14-safe-not-found-identical', Object.values(nf).every((x) => x.status === 404 && x.errorCode === 'CLAIM_NOT_FOUND' && x.errorCorrelation === x.responseCorrelation)
    && new Set(Object.values(nf).map(shape)).size === 1, { cases: nf });
  const tb = '{"targetStatus":"Investigating","occurredAt":"2026-09-27T12:00:00+00:00"}';
  const tn = { unknownClaim: await adapter(fp, 'POST', `${API}/${unknown}/transition?shipmentId=${S.SEED.shipmentId}`, tb),
    softDeletedClaim: await adapter(fp, 'POST', `${API}/${C.SOFTDEL1.claimId}/transition?shipmentId=${S.SEED.shipmentId}`, tb),
    foreignTenantClaim: await adapter(fp, 'POST', `${API}/${C.T2CLAIM.claimId}/transition?shipmentId=${S.SEED.shipmentId}`, tb),
    foreignLeClaimFromLeb: await adapter(lp, 'POST', `${API}/${C.OPEN1.claimId}/transition?shipmentId=${S.SEED.shipmentId}`, tb) };
  verdict('CU-15-transition-safe-not-found', Object.values(tn).every((x) => x.status === 404 && x.errorCode === 'CLAIM_NOT_FOUND') && new Set(Object.values(tn).map(shape)).size === 1, { cases: tn });
  const draft = await adapter(fp, 'POST', API, `{"shipmentId":"${S.DRAFT.shipmentId}","reasonCode":"X","claimedAmount":"10.00","currency":"EUR"}`);
  verdict('CU-17-direct-ineligible-422', draft.status === 422 && draft.errorCode === 'CLAIM_SHIPMENT_INELIGIBLE', { draft });
  const lower = await adapter(fp, 'POST', API, `{"shipmentId":"${S.DISPATCHED.shipmentId}","reasonCode":"X","claimedAmount":"10.00","currency":"eur"}`);
  const zero = await adapter(fp, 'POST', API, `{"shipmentId":"${S.DISPATCHED.shipmentId}","reasonCode":"X","claimedAmount":"0.00","currency":"EUR"}`);
  const comma = await adapter(fp, 'POST', API, `{"shipmentId":"${S.DISPATCHED.shipmentId}","reasonCode":"X","claimedAmount":"10,00","currency":"EUR"}`);
  verdict('CU-11-amount-currency-lexical-http', lower.status === 400 && lower.errorCode === 'INVALID_REQUEST' && zero.status === 422 && zero.errorCode === 'CLAIM_AMOUNT_INVALID'
    && [400, 422].includes(comma.status), { lowercaseCurrency: lower, zeroAmount: zero, commaDecimal: comma });
  // CU-17 UI half: the Draft shipment shows the eligibility note and disables submit (display only)
  { await fp.locator('.add-new').click(); await fp.locator('#claimShipmentId').fill(S.DRAFT.shipmentId);
    await Promise.all([fp.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), fp.locator('#btnResolveShipment').click()]); await fp.waitForTimeout(500);
    const submitDisabled = await fp.locator('#btnSaveClaim').isDisabled(); const note = await fp.locator('#offcanvasCreateEdit').innerText();
    await shot(fp, 'cu17-ineligible-draft-en');
    verdict('CU-17-ui-eligibility-display', submitDisabled, { submitDisabled, surfaceTextExcerpt: note.slice(0, 400) }); }
  const allErr = [...Object.values(nf), ...Object.values(tn), draft, lower, zero];
  verdict('CU-23-support-ref-not-root', allErr.every((x) => x.errorCorrelation && x.errorCorrelation === x.responseCorrelation && !ROOTS.includes(String(x.errorCorrelation).toLowerCase())),
    { pairs: allErr.map((x) => [x.responseCorrelation, x.errorCorrelation]) });
  await full.close(); await leb.close(); await t2.close();
}

// ───────────────────────────── CU-10 carrier link (two runs) ─────────────────────────────
async function checksMut(browser) {
  const s = FX.shipments.CARRIER; const c = await ctx(browser, 'full'); const page = await c.newPage(); const net = watch(page, 'cu10'); const tag = EXTRA || 'cu10';
  await ready(page);
  await page.locator('.add-new').click(); await page.locator('#claimShipmentId').fill(s.shipmentId);
  const resolved = page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/'));
  await page.locator('#btnResolveShipment').click(); const rr = await resolved; const rb = await rr.json();
  const enabled = await page.locator('#claimLinkCarrier').isEnabled();
  if (enabled) await page.locator('#claimLinkCarrier').check();
  await page.locator('#claimReasonCode').fill('CARRIER-DAMAGE'); await page.locator('#claimClaimedAmount').fill('40.00'); await page.locator('#claimCurrency').fill('EUR');
  const post = page.waitForRequest((r) => r.url() === API && r.method() === 'POST');
  const created = page.waitForResponse((r) => r.url() === API && r.request().method() === 'POST');
  await page.locator('#btnSaveClaim').click(); const sent = await post; const cr = await created; const body = await cr.json().catch(() => null);
  await page.waitForTimeout(800); await shot(page, `${tag}-carrier-linked-create-en`);
  const alertText = await page.locator('#formClaimAlert').innerText().catch(() => '');
  const common = rr.status() === 200 && rb.carrierId === FX.carrier && enabled && JSON.parse(sent.postData()).carrierId === FX.carrier;
  const detail = { run: tag, resolveBody: rb, checkboxEnabled: enabled, sentBody: sent.postData(), createStatus: cr.status(), createErrorCode: body?.error?.code || null, alertText };
  if (tag === 'nogrant') verdict('CU-10-run1-without-carriers-read', common && cr.status() === 503, { ...detail, expected: '503 and zero write (K10 pair)' });
  else verdict('CU-10-run2-with-lane-grant', common && cr.status() === 201, { ...detail, expected: '201, carrierId sent, +1 claim (K10 pair)' });
  result.cases[Object.keys(result.cases).pop()].network = net;
  await c.close();
}

// ───────────────────────────── ui-flows (native v3 index.js) ─────────────────────────────
async function uiFlows(browser) {
  const S = FX.shipments;
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'd02'); const e0 = jsErrors(); await ready(page);
    const dom = await page.evaluate(() => ({ layoutWrappers: document.querySelectorAll('.layout-wrapper').length, navbars: document.querySelectorAll('nav.layout-navbar, #layout-navbar').length,
      footers: document.querySelectorAll('footer').length, mainJs: [...document.scripts].filter((s) => /\/main\.js/.test(s.src)).length, menuJs: [...document.scripts].filter((s) => /\/menu\.js/.test(s.src)).length,
      htmlInBody: document.body.querySelectorAll('html, head').length }));
    await shot(page, 'd02-single-shell-en');
    verdict('D-02-single-shell', dom.layoutWrappers === 1 && dom.mainJs === 1 && dom.menuJs <= 1 && dom.footers === 1 && dom.htmlInBody === 0 && jsErrors() === e0, { dom, newJsErrors: jsErrors() - e0 });
    await c.close(); }
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'create'); await ready(page); const e0 = jsErrors();
    await page.locator('.add-new').click(); await page.locator('#claimShipmentId').fill(S.DISPATCHED.shipmentId);
    await Promise.all([page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), page.locator('#btnResolveShipment').click()]);
    await page.locator('#claimReasonCode').fill('DAMAGE'); await page.locator('#claimClaimedAmount').fill('263.00'); await page.locator('#claimCurrency').fill('EUR');
    const created = page.waitForResponse((r) => r.url() === API && r.request().method() === 'POST');
    const reload = page.waitForResponse(isList, { timeout: 10000 });
    await page.locator('#btnSaveClaim').click(); const cr = await created; const reloaded = await reload.then(() => true).catch(() => false);
    await page.waitForTimeout(800); const shown = (await page.locator('#dt-claims').innerText()).includes('263.00');
    await shot(page, 'd01-create-reload-en');
    verdict('D-01-create-then-reload', cr.status() === 201 && reloaded && shown && jsErrors() === e0, { createStatus: cr.status(), reloadRequestAfterCreate: reloaded, rowShowsAmount: shown, newJsErrors: jsErrors() - e0 });
    await c.close(); }
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'filter'); await ready(page); const e0 = jsErrors();
    await page.locator('.dt-filter-btn').click(); await page.locator('#filterShipmentId').fill(S.SEED.shipmentId);
    const rq = page.waitForRequest((r) => r.url().includes('/SupplyChain/Claims/api?'), { timeout: 10000 }).catch(() => null);
    await page.locator('#btnFilterApply').click(); const r = await rq; const keys = r ? [...new URL(r.url()).searchParams.keys()].sort() : null;
    await page.waitForTimeout(400); const rowsAfterApply = await page.locator('#dt-claims tbody tr').count();
    await shot(page, 'cu02-filter-applied-en');
    const resetReq = page.waitForRequest((x) => new URL(x.url()).pathname === '/SupplyChain/Claims/api' && x.method() === 'GET', { timeout: 10000 }).catch(() => null);
    await page.locator('#btnFilterReset').click(); const rr = await resetReq;
    verdict('CU-02-filter', !!r && JSON.stringify(keys) === '["shipmentId"]' && !r.headers()['x-tenant-id'] && !!rr && new URL(rr.url()).search === '' && jsErrors() === e0,
      { applyRequestSent: !!r, queryKeys: keys, rowsAfterApply, resetReloaded: !!rr, resetQuery: rr ? new URL(rr.url()).search : null, newJsErrors: jsErrors() - e0 });
    verdict('D-01-filter-reload', !!r && !!rr && jsErrors() === e0, { note: 'Apply and Reset each issued a fresh list GET without an xhr.abort error' });
    await c.close(); }
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); const net = watch(page, 'quickview'); await ready(page);
    const n0 = net.length; await page.locator('#dt-claims tbody tr .js-quick-view').first().click();
    await page.locator('#offcanvasDetailsPreview').waitFor({ state: 'visible' }); await page.waitForTimeout(500);
    const calls = net.slice(n0).filter((x) => x.path && x.path.startsWith('/SupplyChain/Claims/api'));
    const text = await page.locator('#offcanvasDetailsPreview').innerText(); await shot(page, 'cu31-quickview-en');
    verdict('CU-31-quickview', calls.length === 0 && (await page.locator('#oc-claim-id').innerText()).length > 0 && !/\bEdit\b/.test(text), { adapterCallsDuringQuickView: calls.length });
    await c.close(); }
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'transition'); await ready(page); const e0 = jsErrors();
    await filterById(page, S.SEED.shipmentId);
    const id = FX.claims.OPEN1.claimId;
    const row = rowOf(page, id); await row.locator('.dropdown-toggle').click();
    const offered = await row.locator('.js-transition').evaluateAll((els) => els.map((e) => e.getAttribute('data-target-status')));
    await row.locator('.js-transition[data-target-status="Investigating"]').click(); await page.locator('#btnSubmitTransition').waitFor({ state: 'visible' });
    const approvedFieldVisible = await page.locator('#transitionApprovedAmountGroup').isVisible();
    const occurred = await page.locator('#transitionOccurredAt').inputValue();
    const sentP = page.waitForRequest((r) => r.url().includes(`/api/${id}/transition?shipmentId=`));
    await page.locator('#btnSubmitTransition').click();
    const resp = page.waitForResponse((r) => r.url().includes(`/api/${id}/transition?shipmentId=`)); await confirm(page); const tr = await resp; const sent = await sentP;
    const reloaded = await page.waitForResponse(isList, { timeout: 10000 }).then(() => true).catch(() => false);
    await page.waitForTimeout(500); await shot(page, 'cu08-transition-investigating-en');
    const body = JSON.parse(sent.postData() || '{}');
    verdict('CU-08-open-to-investigating', tr.status() === 200 && reloaded && /([+-]\d{2}:\d{2}|Z)$/.test(occurred) && jsErrors() === e0 && !('approvedAmount' in body) && !approvedFieldVisible
      && JSON.stringify([...offered].sort()) === '["Investigating","Withdrawn"]',
      { claimId: id, offeredTargetsForOpen: offered, status: tr.status(), sentBody: sent.postData(), approvedFieldVisibleForInvestigating: approvedFieldVisible, occurredAtHasOffset: /([+-]\d{2}:\d{2}|Z)$/.test(occurred), reloadAfter: reloaded, newJsErrors: jsErrors() - e0 });
    verdict('D-01-transition-reload', tr.status() === 200 && reloaded && jsErrors() === e0, {});
    await c.close(); }
  { const inv = FX.claims.INV1.claimId;
    const a = await ctx(browser, 'full'); const b = await ctx(browser, 'full'); const pa = await a.newPage(); const pb = await b.newPage(); watch(pa, 'stale-a'); watch(pb, 'stale-b');
    await ready(pa); await ready(pb); await filterById(pa, FX.shipments.SEED.shipmentId); await filterById(pb, FX.shipments.SEED.shipmentId);
    await openAction(pb, inv, 'Rejected');
    await openAction(pa, inv, 'Approved'); await pa.locator('#transitionApprovedAmount').fill('10.00'); await pa.locator('#btnSubmitTransition').click();
    const ra = pa.waitForResponse((r) => r.url().includes(`/api/${inv}/transition`)); await confirm(pa); const a200 = await ra;
    await pb.locator('#btnSubmitTransition').click();
    const rb = pb.waitForResponse((r) => r.url().includes(`/api/${inv}/transition`)); await confirm(pb); const b422 = await rb; const bb = await b422.json();
    const reloaded = await pb.waitForResponse(isList, { timeout: 10000 }).then(() => true).catch(() => false);
    await pb.waitForTimeout(800);
    const bToasts = result.toasts.filter((x) => x.tag === 'stale-b').map((x) => x.text).join(' | ');
    const alert = (await pb.locator('#formClaimTransitionAlert').innerText().catch(() => '')) + ' ' + bToasts;
    await shot(pb, 'cu21-stale-transition-422-en');
    verdict('CU-21-stale-transition', a200.status() === 200 && b422.status() === 422 && bb?.error?.code === 'INVALID_CLAIM_TRANSITION' && reloaded && alert.includes(bb.error.correlationId),
      { claimId: inv, approve: a200.status(), approvedAmountInResponse: (await a200.json()).approvedAmount, stale: b422.status(), code: bb?.error?.code,
        supportRefShown: alert.includes(bb?.error?.correlationId || '#'), messageSeen: alert.slice(0, 400), reloadAfter: reloaded });
    await a.close(); await b.close(); }
}

// ───────────────────────────── Save View / Reset ─────────────────────────────
async function saveview(browser) {
  const S = FX.shipments; const c = await ctx(browser, 'full'); const page = await c.newPage(); const net = watch(page, 'saveview'); const e0 = jsErrors();
  const saveBtn = page.locator('.dt-save-filter-btn');
  const visible = async () => !(await saveBtn.evaluate((el) => el.classList.contains('d-none')));
  const listQuery = async (action) => { const w = page.waitForRequest((x) => new URL(x.url()).pathname === '/SupplyChain/Claims/api' && x.method() === 'GET', { timeout: 10000 }).catch(() => null);
    await action(); const r = await w; await page.waitForTimeout(500); return r ? new URL(r.url()).search : null; };
  const state = async () => page.evaluate(() => { const dt = window.jQuery ? window.jQuery('#dt-claims').DataTable() : null;
    return { order: dt ? dt.order() : null, search: dt ? dt.search() : null, filterShipmentId: document.getElementById('filterShipmentId')?.value ?? null,
      filterStatus: document.getElementById('filterStatus')?.value ?? null, colVis: dt ? [1, 2, 3, 4, 5].map((i) => dt.column(i).visible()) : null }; });
  await ready(page); await page.waitForTimeout(600);
  const s0 = { saveVisible: await visible(), state: await state() };
  await page.locator('.dt-filter-btn').click(); await page.locator('#filterShipmentId').fill(S.SEED.shipmentId);
  const applyQuery = await listQuery(() => page.locator('#btnFilterApply').click());
  const s1 = { saveVisible: await visible(), query: applyQuery, state: await state() };
  await shot(page, 'saveview-1-apply-dirty-en');
  const resetQuery = await listQuery(() => page.locator('#btnFilterReset').click());
  const s2 = { saveVisible: await visible(), query: resetQuery, state: await state() };
  await shot(page, 'saveview-2-reset-factory-en');
  await page.locator('#dt-claims thead th').nth(4).click(); await page.waitForTimeout(400);
  const s3 = { saveVisible: await visible(), state: await state() };
  const savedResp = page.waitForResponse((r) => new URL(r.url()).pathname.startsWith('/api/personalization/views') && ['POST', 'PUT'].includes(r.request().method()), { timeout: 15000 }).catch(() => null);
  await saveBtn.click(); const sr = await savedResp; await page.waitForTimeout(600);
  const s4 = { saveVisible: await visible(), saveStatus: sr ? sr.status() : null, saveMethod: sr ? sr.request().method() : null, state: await state() };
  await shot(page, 'saveview-3-saved-clean-en');
  await ready(page); await page.waitForTimeout(800);
  const s5 = { saveVisible: await visible(), state: await state() };
  await shot(page, 'saveview-4-reload-restored-en');
  await page.locator('.dt-filter-btn').click();
  const reset2Query = await listQuery(() => page.locator('#btnFilterReset').click());
  const s6 = { saveVisible: await visible(), query: reset2Query, state: await state() };
  const neutral = page.waitForResponse((r) => new URL(r.url()).pathname.startsWith('/api/personalization/views') && ['POST', 'PUT'].includes(r.request().method()), { timeout: 15000 }).catch(() => null);
  if (s6.saveVisible) await saveBtn.click(); const nr = await neutral; await page.waitForTimeout(500);
  const s7 = { saveVisible: await visible(), neutralSaveStatus: nr ? nr.status() : null };
  const factoryOrder = JSON.stringify(s0.state.order);
  verdict('SAVEVIEW-apply-dirty', s0.saveVisible === false && s1.saveVisible === true && s1.query === `?shipmentId=${S.SEED.shipmentId}`, { s0, s1 });
  verdict('SAVEVIEW-reset-factory', s2.saveVisible === false && s2.query === '' && s2.state.filterShipmentId === '' && JSON.stringify(s2.state.order) === factoryOrder
    && JSON.stringify(s2.state.colVis) === JSON.stringify(s0.state.colVis), { s2, factoryOrder });
  verdict('SAVEVIEW-save-clean', s3.saveVisible === true && [200, 201, 204].includes(s4.saveStatus) && s4.saveVisible === false, { s3, s4 });
  verdict('SAVEVIEW-reload-restores', s5.saveVisible === false && JSON.stringify(s5.state.order) === JSON.stringify(s4.state.order) && JSON.stringify(s4.state.order) !== factoryOrder, { s5 });
  verdict('SAVEVIEW-reset-vs-saved-default', s6.saveVisible === true && JSON.stringify(s6.state.order) === factoryOrder && s6.state.filterShipmentId === '' && s7.saveVisible === false,
    { s6, s7, note: 'Reset restores factory state in one step; Save View becomes visible because the saved default differs; the factory view was then saved to leave a neutral default' });
  verdict('SAVEVIEW-no-js-errors', jsErrors() === e0, { newJsErrors: jsErrors() - e0, personalizationCalls: net.filter((x) => x.path && x.path.startsWith('/api/personalization')).map((x) => [x.method, x.status]) });
  await c.close();
}

// ───────────────────────────── crafted responses (browser only, zero write): CU-03, CU-04, CU-13 ─────────────────────────────
async function crafted(browser) {
  const fulfillList = async (c, body, status = 200) => c.route((u) => u.pathname === '/SupplyChain/Claims/api', (route) => route.request().method() === 'GET'
    ? route.fulfill({ status, contentType: 'application/json', headers: { 'X-Correlation-Id': '11111111-2222-4333-8444-555555555555' }, body: typeof body === 'string' ? body : JSON.stringify(body) })
    : route.continue());
  const base = { claimId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee', claimNumber: 'CLM-CRAFTED-1', shipmentId: FX.shipments.SEED.shipmentId, status: 'Open', claimedAmount: '10.00', currency: 'EUR' };
  const long80 = '9'.repeat(77) + '.99';
  const show = async (c, name) => { const page = await c.newPage(); watch(page, name); await page.goto(PAGE); await page.waitForTimeout(1500); return page; };
  { const c = await ctx(browser, 'full'); await fulfillList(c, { items: [{ ...base, claimNumber: undefined, currency: undefined }, { ...base, claimId: undefined, claimNumber: 'CLM-NO-ID' }] });
    const page = await show(c, 'cu03a');
    const t = await page.locator('#dt-claims tbody').innerText(); const rows = await page.locator('#dt-claims tbody tr').count();
    const noIdRow = page.locator('#dt-claims tbody tr', { hasText: 'CLM-NO-ID' }); const noIdActions = await noIdRow.locator('.js-quick-view, .js-transition, .dropdown-toggle').count();
    await shot(page, 'cu03-absent-fields-en');
    await c.close();
    const c2 = await ctx(browser, 'full'); await fulfillList(c2, '{"data":[1,2,3]}'); const p2 = await show(c2, 'cu03b');
    const errVisible = await p2.locator('#claims-error-state').isVisible(); const tableHidden = !(await p2.locator('#claims-table-host').isVisible()); const skeletonHidden = !(await p2.locator('#skeleton-loader').isVisible());
    await shot(p2, 'cu03-malformed-envelope-en'); await c2.close();
    verdict('CU-03-envelope-absent-fields', rows === 2 && (t.match(/Not provided/g) || []).length >= 2 && noIdActions === 0 && errVisible && tableHidden && skeletonHidden,
      { rows, notProvidedCount: (t.match(/Not provided/g) || []).length, actionsOnRowWithoutClaimId: noIdActions, malformed: { errorStateVisible: errVisible, tableHidden, skeletonHidden } }); }
  { const c = await ctx(browser, 'full'); await fulfillList(c, { items: [{ ...base, claimNumber: 'CLM-LEAD', claimedAmount: '000250.00' }, { ...base, claimId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeee2', claimNumber: 'CLM-LONG', claimedAmount: long80 }] });
    const page = await show(c, 'cu04');
    const cells = await page.$$eval('#dt-claims tbody tr', (trs) => trs.map((tr) => { const td = tr.querySelectorAll('td')[4]; const b = td?.querySelector('bdi'); return { text: td?.innerText.trim(), bdiDir: b?.getAttribute('dir') || null }; }));
    await shot(page, 'cu04-exact-amount-en'); await c.close();
    const texts = cells.map((x) => x.text);
    verdict('CU-04-exact-amount', texts.includes('000250.00') && texts.includes(long80) && cells.every((x) => x.bdiDir === 'ltr'), { cells: cells.map((x) => ({ ...x, text: x.text?.length > 40 ? `${x.text.slice(0, 12)}…(${x.text.length} chars)` : x.text })), long80Length: long80.length }); }
  { const c = await ctx(browser, 'full'); let n = 0; const order = [];
    await c.route((u) => u.pathname.startsWith('/SupplyChain/Claims/api/shipments/'), async (route) => { const k = ++n; const resp = await route.fetch();
      if (k === 1) await new Promise((r) => setTimeout(r, 3000)); order.push({ k, url: new URL(route.request().url()).pathname.split('/').pop(), at: iso() }); await route.fulfill({ response: resp }); });
    const page = await c.newPage(); watch(page, 'cu13'); await ready(page);
    await page.locator('.add-new').click();
    await page.locator('#claimShipmentId').fill(FX.shipments.SEED.shipmentId); await page.locator('#btnResolveShipment').click();
    await page.waitForTimeout(300);
    await page.locator('#claimShipmentId').fill(FX.shipments.DISPATCHED.shipmentId); await page.locator('#btnResolveShipment').click();
    await page.waitForTimeout(4000);
    const shownNumber = await page.locator('#claimShipmentNumber').innerText().catch(() => '');
    const input = await page.locator('#claimShipmentId').inputValue();
    await shot(page, 'cu13-stale-resolve-en'); await c.close();
    verdict('CU-13-resolve-stale-guard', order.length === 2 && order[0].k === 2 && order[1].k === 1 && input === FX.shipments.DISPATCHED.shipmentId && shownNumber.length > 0,
      { responseOrder: order, inputAfter: input, shipmentNumberShown: shownNumber, note: 'the delayed first response (SEED) arrived last and did not replace the DISPATCHED result' }); }
}

// ───────────────────────────── CU-05 (from Q64e) ─────────────────────────────
async function cu05(browser) {
  const fulfillList = (c, body, status) => c.route((u) => u.pathname === '/SupplyChain/Claims/api', (route) => route.request().method() === 'GET'
    ? route.fulfill({ status, contentType: 'application/json', headers: { 'X-Correlation-Id': '11111111-2222-4333-8444-555555555555' }, body: JSON.stringify(body) }) : route.continue());
  const c1 = await ctx(browser, 'full');
  await c1.route((u) => u.pathname === '/SupplyChain/Claims/api', async (route) => { await new Promise((r) => setTimeout(r, 2500)); await route.continue(); });
  const p1 = await c1.newPage(); watch(p1, 'cu05-skeleton');
  await p1.addInitScript(() => { window.__skel = []; const t0 = performance.now(); const snap = () => { const e = document.getElementById('skeleton-loader'); if (e) window.__skel.push([Math.round(performance.now() - t0), getComputedStyle(e).display, getComputedStyle(e).opacity]); };
    document.addEventListener('DOMContentLoaded', () => { snap(); const iv = setInterval(snap, 100); setTimeout(() => clearInterval(iv), 4000); }); });
  await p1.goto(PAGE); await p1.waitForTimeout(700);
  const during = { skeletonVisible: await visibleBox(p1.locator('#skeleton-loader')), tableVisible: await visibleBox(p1.locator('#claims-table-host')),
    errorVisible: await visibleBox(p1.locator('#claims-error-state')), skeletonDisplay: await p1.locator('#skeleton-loader').evaluate((e) => getComputedStyle(e).display) };
  await shot(p1, 'cu05-skeleton-en'); await p1.waitForTimeout(1300);
  const late = { skeletonVisibleAt2000ms: await visibleBox(p1.locator('#skeleton-loader')) };
  await p1.waitForTimeout(1900);
  const after = { skeletonVisible: await visibleBox(p1.locator('#skeleton-loader')), tableVisible: await visibleBox(p1.locator('#claims-table-host')) };
  const timeline = await p1.evaluate(() => window.__skel);
  await shot(p1, 'cu05-after-load-en'); await c1.close();
  const c2 = await ctx(browser, 'full'); await fulfillList(c2, { items: [] }, 200); const p2 = await c2.newPage(); watch(p2, 'cu05-empty'); await p2.goto(PAGE); await p2.waitForTimeout(1500);
  const empty = { tableVisible: await visibleBox(p2.locator('#claims-table-host')), skeletonVisible: await visibleBox(p2.locator('#skeleton-loader')), errorVisible: await visibleBox(p2.locator('#claims-error-state')),
    text: (await p2.locator('#dt-claims tbody').innerText()).trim() };
  await shot(p2, 'cu05-empty-en'); await c2.close();
  const c3 = await ctx(browser, 'full'); await fulfillList(c3, { error: { code: 'CLAIM_STORAGE_UNAVAILABLE', message: 'x', correlationId: '11111111-2222-4333-8444-555555555555' } }, 503);
  const p3 = await c3.newPage(); watch(p3, 'cu05-error'); await p3.goto(PAGE); await p3.waitForTimeout(1500);
  const error = { errorVisible: await visibleBox(p3.locator('#claims-error-state')), tableVisible: await visibleBox(p3.locator('#claims-table-host')), skeletonVisible: await visibleBox(p3.locator('#skeleton-loader')),
    message: await p3.locator('#claims-error-message').innerText(), reference: await p3.locator('#claims-error-reference-value').innerText().catch(() => ''), retryVisible: await visibleBox(p3.locator('#btnClaimsRetry')) };
  await shot(p3, 'cu05-error-503-en'); await c3.close();
  verdict('CU-05-skeleton-empty-error', during.skeletonVisible && !during.tableVisible && !during.errorVisible && late.skeletonVisibleAt2000ms && !after.skeletonVisible && after.tableVisible
    && empty.tableVisible && !empty.skeletonVisible && !empty.errorVisible && empty.text.length > 0
    && error.errorVisible && !error.tableVisible && !error.skeletonVisible && error.message.length > 0 && error.retryVisible && error.reference === '11111111-2222-4333-8444-555555555555',
    { skeletonDuringLoad: during, skeletonLate: late, afterLoad: after, skeletonTimeline: timeline.map((x) => x.join(':')).join(' '), empty, error503: error,
      note: 'empty text is the shared localized DtEmptyTable (by design, Q64a F13)' });
}

// ───────────────────────────── CU-12 presence ≠ nonempty ─────────────────────────────
async function cu12(browser) {
  const S = FX.shipments; const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'cu12'); await ready(page);
  await page.locator('.add-new').click(); await page.locator('#claimShipmentId').fill(S.DISPATCHED.shipmentId);
  await Promise.all([page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), page.locator('#btnResolveShipment').click()]);
  await page.locator('#claimClaimedAmount').fill('12.00'); await page.locator('#claimCurrency').fill('EUR');
  await page.locator('#btnAddEvidence').click();
  const attrs = await page.evaluate(() => [...document.querySelectorAll('#formClaim input, #formClaim textarea')].map((e) => ({ id: e.id, required: e.required, maxLength: e.maxLength, pattern: e.pattern || null })));
  const post = page.waitForRequest((r) => r.url() === API && r.method() === 'POST');
  const created = page.waitForResponse((r) => r.url() === API && r.request().method() === 'POST');
  await page.locator('#btnSaveClaim').click(); const sent = await post; const cr = await created; const cb = await cr.json().catch(() => ({}));
  await page.waitForTimeout(600); await shot(page, 'cu12-empty-reason-evidence-en');
  const body = JSON.parse(sent.postData() || '{}');
  const t = await adapter(page, 'POST', `${API}/${cb.claimId}/transition?shipmentId=${S.DISPATCHED.shipmentId}`, `{"targetStatus":"Investigating","occurredAt":"2026-09-27T15:00:00+00:00","resolutionCode":"","note":""}`);
  result.cu12 = { claimId: cb.claimId };
  verdict('CU-12-presence-not-nonempty', cr.status() === 201 && body.reasonCode === '' && Array.isArray(body.evidenceReferenceIds) && body.evidenceReferenceIds.length === 1 && body.evidenceReferenceIds[0] === ''
    && attrs.every((a) => !a.required && (a.maxLength === -1 || a.maxLength > 100000) && !a.pattern) && t.status === 200,
    { sentBody: sent.postData(), createStatus: cr.status(), claimId: cb.claimId, formAttributes: attrs, emptyResolutionNoteTransition: t, dbCheck: 'see raw/db/cu12-exact-empty-*.json' });
  await c.close();
}

// ───────────────────────────── CU-18 root seam (CT disposition) ─────────────────────────────
async function cu18(browser) {
  const S = FX.shipments; const c = await ctx(browser, 'full'); const page = await c.newPage(); const net = watch(page, 'cu18'); await ready(page);
  const ui = async (ship, tag) => { await page.locator('.add-new').click(); await page.locator('#claimShipmentId').fill(ship);
    const rr = page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')); await page.locator('#btnResolveShipment').click(); const r = await rr;
    const body = await r.json().catch(() => null); await page.waitForTimeout(500);
    const alert = await page.locator('#formClaimAlert').innerText().catch(() => ''); const submitDisabled = await page.locator('#btnSaveClaim').isDisabled();
    await shot(page, `cu18-${tag}-en`);
    await page.keyboard.press('Escape'); await page.waitForTimeout(600); return { resolveStatus: r.status(), errorCode: body?.error?.code || null, alert, submitDisabled }; };
  const nullUi = await ui(S.NULLROOT.shipmentId, 'null-root'); const badUi = await ui(S.BADROOT.shipmentId, 'malformed-root');
  const nullHttp = await adapter(page, 'POST', API, `{"shipmentId":"${S.NULLROOT.shipmentId}","reasonCode":"X","claimedAmount":"5.00","currency":"EUR"}`);
  const badHttp = await adapter(page, 'POST', API, `{"shipmentId":"${S.BADROOT.shipmentId}","reasonCode":"X","claimedAmount":"5.00","currency":"EUR"}`);
  verdict('CU-18-null-root-503', nullHttp.status === 503 && nullHttp.errorCode === 'CLAIM_REFERENCE_INCOMPLETE' && nullUi.alert.trim().length > 0 && result.rootLeak.length === 0,
    { nullRootUi: nullUi, nullRootCreate: nullHttp, rootLeak: result.rootLeak.length });
  // Malformed root: CT disposition — the 502 branch is accepted from backend tests; the Shipment API 500 on a malformed root is finding F-0183-500 (MOD-0183).
  result.cases['CU-18-malformed-root-observed'] = { result: 'RECORDED', malformedRootUi: badUi, malformedRootCreate: badHttp,
    note: 'not a Claims verdict (CT: 502 branch accepted from backend tests; producer 500 = F-0183-500)' };
  await c.close();
}

// ───────────────────────────── CU-19 idempotency ─────────────────────────────
async function cu19(browser) {
  const S = FX.shipments; const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'cu19'); await ready(page);
  const key = crypto.randomUUID(); const body = `{"shipmentId":"${S.DISPATCHED.shipmentId}","reasonCode":"IDEM","claimedAmount":"250.00","currency":"EUR"}`;
  const first = await adapter(page, 'POST', API, body, { 'Idempotency-Key': key }); const replay = await adapter(page, 'POST', API, body, { 'Idempotency-Key': key });
  const changed = await adapter(page, 'POST', API, body.replace('"250.00"', '"250"'), { 'Idempotency-Key': key });
  verdict('CU-19a-http-replay-and-409', first.status === 201 && [200, 201].includes(replay.status) && replay.claimId === first.claimId && replay.idempotentReplay === true && changed.status === 409 && changed.errorCode === 'IDEMPOTENCY_KEY_REUSED',
    { first, replay, changedBodySameKey: changed });
  let n = 0; const seen = [];
  await page.route((u) => u.pathname === '/SupplyChain/Claims/api', async (route) => { const r = route.request(); if (r.method() !== 'POST') return route.continue();
    const k = ++n; seen.push({ k, key: r.headers()['idempotency-key'], body: r.postData() });
    if (k === 1) { const resp = await route.fetch(); seen[0].serverStatus = resp.status(); return route.abort('failed'); }
    return route.continue(); });
  await page.locator('.add-new').click(); await page.locator('#claimShipmentId').fill(S.DISPATCHED.shipmentId);
  await Promise.all([page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), page.locator('#btnResolveShipment').click()]);
  await page.locator('#claimReasonCode').fill('IDEM-UI'); await page.locator('#claimClaimedAmount').fill('19.00'); await page.locator('#claimCurrency').fill('EUR');
  await page.locator('#btnSaveClaim').click(); await page.waitForTimeout(1500);
  const afterLoss = await page.locator('#formClaimAlert').innerText().catch(() => '');
  const replayResp = page.waitForResponse((r) => r.url() === API && r.request().method() === 'POST');
  const reload = page.waitForResponse(isList, { timeout: 10000 });
  await page.locator('#btnSaveClaim').click(); const rr = await replayResp; const rb = await rr.json().catch(() => ({})); const reloaded = await reload.then(() => true).catch(() => false);
  await page.waitForTimeout(800); await shot(page, 'cu19-retry-replay-completed-en');
  const toastB = result.toasts.filter((x) => x.tag === 'cu19').map((x) => x.text);
  verdict('CU-19b-browser-retry-replay', seen.length === 2 && seen[0].serverStatus === 201 && seen[0].key === seen[1].key && seen[0].body === seen[1].body && [200, 201].includes(rr.status()) && rb.idempotentReplay === true && reloaded,
    { attempts: seen, messageAfterLostResponse: afterLoss, retryStatus: rr.status(), retryIdempotentReplay: rb.idempotentReplay ?? null, reloaded, toasts: toastB });
  await page.unrouteAll({ behavior: 'ignoreErrors' }).catch(() => null);
  { const c3 = await ctx(browser, 'full'); const p3 = await c3.newPage(); watch(p3, 'cu19c'); await ready(p3); let m = 0; const s3 = [];
    await p3.route((u) => u.pathname === '/SupplyChain/Claims/api', async (route) => { const r = route.request(); if (r.method() !== 'POST') return route.continue();
      const k = ++m; s3.push({ k, key: r.headers()['idempotency-key'] });
      if (k === 1) { const resp = await route.fetch(); s3[0].serverStatus = resp.status(); return route.abort('failed'); }
      if (k === 2) return route.continue({ postData: r.postData().replace('"21.00"', '"21"') });
      return route.continue(); });
    await p3.locator('.add-new').click(); await p3.locator('#claimShipmentId').fill(S.DISPATCHED.shipmentId);
    await Promise.all([p3.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), p3.locator('#btnResolveShipment').click()]);
    await p3.locator('#claimReasonCode').fill('IDEM-409'); await p3.locator('#claimClaimedAmount').fill('21.00'); await p3.locator('#claimCurrency').fill('EUR');
    await p3.locator('#btnSaveClaim').click(); await p3.waitForTimeout(1500);
    const r409 = p3.waitForResponse((r) => r.url() === API && r.request().method() === 'POST'); await p3.locator('#btnSaveClaim').click(); const x = await r409; const xb = await x.json().catch(() => ({}));
    await p3.waitForTimeout(600); const alert409 = await p3.locator('#formClaimAlert').innerText().catch(() => '');
    await p3.locator('#btnSaveClaim').click().catch(() => null); await p3.waitForTimeout(1500);
    await shot(p3, 'cu19-retry-409-blocked-en');
    verdict('CU-19c-browser-409-stops-retry', s3.length === 2 && s3[0].serverStatus === 201 && x.status() === 409 && xb?.error?.code === 'IDEMPOTENCY_KEY_REUSED' && alert409.includes('already used'),
      { attempts: s3, status409: x.status(), code: xb?.error?.code, alertText: alert409, requestsAfterThirdClick: s3.length });
    await c3.close(); }
  { const c4 = await ctx(browser, 'full'); const p4 = await c4.newPage(); watch(p4, 'cu19d'); await ready(p4); await filterById(p4, S.FLOW.shipmentId); let k4 = 0; const s4 = [];
    const id = FX.claims.APPR250.claimId;
    await p4.route((u) => u.pathname.endsWith('/transition'), async (route) => { const r = route.request(); const k = ++k4; s4.push({ k, key: r.headers()['idempotency-key'], body: r.postData() });
      if (k === 1) { const resp = await route.fetch(); s4[0].serverStatus = resp.status(); return route.abort('failed'); } return route.continue(); });
    await openAction(p4, id, 'Rejected'); await p4.locator('#btnSubmitTransition').click(); await confirm(p4); await p4.waitForTimeout(1500);
    const rt = p4.waitForResponse((r) => r.url().includes(`/api/${id}/transition`)); await p4.locator('#btnSubmitTransition').click(); await confirm(p4).catch(() => null);
    const tr = await rt; const tb = await tr.json().catch(() => ({})); await p4.waitForTimeout(600);
    verdict('CU-19d-transition-retry-exact-occurredAt', s4.length === 2 && s4[0].serverStatus === 200 && s4[0].key === s4[1].key && s4[0].body === s4[1].body && tr.status() === 200 && tb.idempotentReplay === true,
      { claimId: id, attempts: s4, retryStatus: tr.status(), idempotentReplay: tb.idempotentReplay ?? null });
    await c4.close(); }
  await c.close();
}

// ───────────────────────────── CU-20 approval amount (Q64d cu20 + cu20b merged) ─────────────────────────────
async function cu20(browser) {
  const S = FX.shipments, C = FX.claims; const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'cu20'); await ready(page);
  const tr = (id, extra) => adapter(page, 'POST', `${API}/${id}/transition?shipmentId=${S.FLOW.shipmentId}`, JSON.stringify({ targetStatus: 'Approved', occurredAt: '2026-09-27T15:10:00+00:00', ...extra }));
  const bad = { missing: await tr(C.APPRBAD.claimId, {}), minus001: await tr(C.APPRBAD.claimId, { approvedAmount: '-0.01' }), over: await tr(C.APPRBAD.claimId, { approvedAmount: '250.01' }) };
  const good = { zero: await tr(C.APPR0.claimId, { approvedAmount: '0' }), minusZero: await tr(C.APPRNEG0.claimId, { approvedAmount: '-0' }) };
  const notAllowed = await adapter(page, 'POST', `${API}/${C.APPRBAD.claimId}/transition?shipmentId=${S.FLOW.shipmentId}`, '{"targetStatus":"Rejected","occurredAt":"2026-09-27T15:10:00+00:00","approvedAmount":"1.00"}');
  await filterById(page, S.FLOW.shipmentId);
  const id = C.APPRBAD.claimId;
  await openAction(page, id, 'Rejected'); const fieldOnReject = await page.locator('#transitionApprovedAmountGroup').isVisible();
  await page.waitForTimeout(900); await page.keyboard.press('Escape'); await page.waitForTimeout(900);
  const closedByEscape = !(await visibleBox(page.locator('#offcanvasClaimTransition')));
  if (!closedByEscape) { await page.locator('#offcanvasClaimTransition .btn-close').click(); await page.locator('#offcanvasClaimTransition').waitFor({ state: 'hidden' }); }
  await openAction(page, id, 'Approved'); const fieldOnApprove = await page.locator('#transitionApprovedAmountGroup').isVisible();
  await page.locator('#transitionApprovedAmount').fill('250'); await page.locator('#btnSubmitTransition').click();
  const sentP = page.waitForRequest((r) => r.url().includes(`/api/${id}/transition`)); const resp = page.waitForResponse((r) => r.url().includes(`/api/${id}/transition`));
  await confirm(page); const r = await resp; const sent = await sentP; const b = await r.json().catch(() => ({})); await page.waitForTimeout(800);
  const msg = result.toasts.filter((x) => x.tag === 'cu20').map((x) => x.text).join(' | ');
  await shot(page, 'cu20-approve-250-success-en');
  verdict('CU-20-approval-amount', Object.values(bad).every((x) => x.status === 422 && x.errorCode === 'CLAIM_APPROVAL_AMOUNT_INVALID') && Object.values(good).every((x) => x.status === 200)
    && notAllowed.status === 422 && notAllowed.errorCode === 'CLAIM_APPROVED_AMOUNT_NOT_ALLOWED' && !fieldOnReject && fieldOnApprove && r.status() === 200 && JSON.parse(sent.postData()).approvedAmount === '250'
    && msg.includes(`Approved amount: ${b.approvedAmount}`),
    { rejected422: bad, accepted200: good, approvedAmountOnReject: notAllowed, fieldVisibleOnReject: fieldOnReject, fieldVisibleOnApprove: fieldOnApprove, rejectSurfaceClosedByEscape: closedByEscape,
      uiApprove: { status: r.status(), sentBody: sent.postData(), responseApprovedAmount: b.approvedAmount }, successMessage: msg });
  await c.close();
}

// ───────────────────────────── CU-22 Settled wording ─────────────────────────────
async function cu22(browser) {
  const S = FX.shipments; const id = FX.claims.SETTLE1.claimId; const c = await ctx(browser, 'full'); const page = await c.newPage(); const net = watch(page, 'cu22'); await ready(page);
  await filterById(page, S.FLOW.shipmentId);
  const row = rowOf(page, id); await row.locator('.dropdown-toggle').click();
  const actionLabel = await row.locator('.js-transition[data-target-status="Settled"]').innerText();
  await row.locator('.js-transition[data-target-status="Settled"]').click(); await page.locator('#btnSubmitTransition').waitFor({ state: 'visible' });
  const targetText = await page.locator('#transitionTarget option:checked, #transitionTarget').first().innerText().catch(() => '');
  const surface = await page.locator('#offcanvasClaimTransition').innerText();
  await page.locator('#btnSubmitTransition').click(); await page.locator('.swal2-popup').waitFor({ state: 'visible' });
  const confirmText = await page.locator('.swal2-popup').innerText(); await shot(page, 'cu22-settle-confirm-en');
  const resp = page.waitForResponse((r) => r.url().includes(`/api/${id}/transition`)); await confirm(page); const r = await resp; await page.waitForTimeout(800);
  await shot(page, 'cu22-settled-en');
  const calls = net.filter((x) => x.path).map((x) => x.path);
  verdict('CU-22-settled-wording', /no payment posted/.test(actionLabel) && /No payment, payable or receivable is posted/.test(confirmText) && r.status() === 200
    && calls.every((p) => p.startsWith('/SupplyChain/Claims') || p.startsWith('/api/personalization')),
    { actionLabel, targetText, surfaceHasNote: /no payment/i.test(surface), confirmText: confirmText.slice(0, 300), status: r.status(), browserCallPaths: [...new Set(calls)], dbCheck: 'K10 pair cu22: only claims collections change' });
  await c.close();
}

// ───────────────────────────── CU-24 / CU-25a languages, RTL, responsive (PNG set) ─────────────────────────────
async function l10n(browser) {
  const langs = ['en', 'tr', 'fr', 'es', 'zh', 'ar', 'ru']; const out = {};
  const enTexts = {};
  for (const lang of langs) {
    const c = await ctx(browser, 'full', { locale: lang, viewport: { width: 1280, height: 900 } }); const page = await c.newPage(); watch(page, `l10n-${lang}`);
    await ready(page, `${PAGE}?culture=${lang}&ui-culture=${lang}`); await page.waitForTimeout(500);
    const info = await page.evaluate(() => ({ dir: document.documentElement.getAttribute('dir'), lang: document.documentElement.getAttribute('lang'),
      title: document.querySelector('h5')?.innerText || '', description: document.querySelector('h5 + p')?.innerText || '', headers: [...document.querySelectorAll('#dt-claims thead th')].map((t) => t.innerText.trim()).filter(Boolean),
      firstStatus: document.querySelector('#dt-claims tbody tr td:nth-child(4)')?.innerText || '', addNew: document.querySelector('.add-new')?.innerText.trim() || '',
      ltrIsolated: [...document.querySelectorAll('#dt-claims tbody td bdi')].every((b) => b.getAttribute('dir') === 'ltr') }));
    if (lang === 'en') Object.assign(enTexts, info);
    const widths = {};
    for (const w of (lang === 'en' || lang === 'ar') ? [390, 768, 1024, 1280, 1440] : [1280]) {
      await page.setViewportSize({ width: w, height: 900 }); await page.waitForTimeout(400);
      widths[w] = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1);
      await shot(page, `l10n-${lang}-${w}`);
    }
    out[lang] = { ...info, horizontalOverflow: widths };
    await c.close();
  }
  const english = (x) => [x.title, x.description, x.addNew, x.firstStatus, ...x.headers].filter((t) => [enTexts.title, enTexts.description, enTexts.addNew, enTexts.firstStatus, ...enTexts.headers].includes(t) && /[A-Za-z]{3,}/.test(t));
  const bad = langs.filter((l) => l !== 'en').filter((l) => english(out[l]).length > 0);
  verdict('CU-24-seven-languages-rtl', out.ar.dir === 'rtl' && bad.length === 0 && langs.every((l) => out[l].title.length > 0 && out[l].ltrIsolated),
    { perLanguage: Object.fromEntries(langs.map((l) => [l, { dir: out[l].dir, lang: out[l].lang, title: out[l].title, addNew: out[l].addNew, firstStatus: out[l].firstStatus, headers: out[l].headers, ltrIsolated: out[l].ltrIsolated, englishLeft: english(out[l]) }])) });
  verdict('CU-25a-responsive-no-overflow', langs.every((l) => Object.values(out[l].horizontalOverflow).every((v) => v === false)), { overflow: Object.fromEntries(langs.map((l) => [l, out[l].horizontalOverflow])) });
}

// ───────────────────────────── CU-25b/c keyboard (from Q64e) ─────────────────────────────
async function kbd(browser) {
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'kbd-create'); await ready(page);
    const add = page.locator('.add-new').first(); const open = () => visibleBox(page.locator('#offcanvasCreateEdit'));
    await add.focus(); await page.keyboard.press('Enter'); await page.waitForTimeout(1200); const byEnter = await open();
    if (!byEnter) { await add.focus(); await page.keyboard.press('Space'); await page.waitForTimeout(1200); }
    const opened = await open(); const focusInside = await focusIn(page, '#offcanvasCreateEdit');
    await page.locator('#btnAddEvidence').click(); await page.locator('#btnAddEvidence').click();
    const ids = await page.$$eval('#claimEvidenceList [id]', (els) => els.map((e) => e.id));
    const labelled = await page.$$eval('#claimEvidenceList input', (ins) => ins.map((i) => !!(i.id && document.querySelector(`label[for="${i.id}"]`)) || !!i.getAttribute('aria-label')));
    await shot(page, 'cu25-create-open-en');
    await page.locator('#claimShipmentId').focus(); await page.keyboard.press('Escape'); await page.waitForTimeout(900);
    const closed = !(await open()); const inert = await page.locator('#offcanvasCreateEdit').evaluate((e) => e.inert === true);
    const focusReturned = await focusIn(page, '.add-new'); const focusedAfterClose = await focusId(page);
    verdict('CU-25b-create-keyboard', opened && focusInside && closed && inert && focusReturned && new Set(ids).size === ids.length && labelled.every(Boolean),
      { openedByEnter: byEnter, openedBySpace: !byEnter && opened, focusInsideAfterOpen: focusInside, closedByEscape: closed, inertAfterClose: inert,
        focusReturnedToAdd: focusReturned, focusedAfterClose, evidenceIds: ids, evidenceInputsLabelled: labelled, note: 'Enter on Add = Q64d-D3 (template owner; out of v3 scope)' });
    await c.close(); }
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'kbd-transition'); await ready(page); await filterById(page, FX.shipments.SEED.shipmentId);
    const id = FX.claims.OPEN2.claimId; const surf = page.locator('#offcanvasClaimTransition');
    const openIt = async () => { const row = rowOf(page, id); await row.locator('.dropdown-toggle').click(); await row.locator('.js-transition[data-target-status="Investigating"]').click(); };
    const settleClosed = async () => { await page.waitForTimeout(900); const closed = !(await visibleBox(surf));
      if (!closed) { await page.locator('#offcanvasClaimTransition .btn-close').click(); await surf.waitFor({ state: 'hidden' }); await page.waitForTimeout(400); } return closed; };
    await openIt(); await page.waitForTimeout(50);
    const immediate = { focusInside: await focusIn(page, '#offcanvasClaimTransition'), focused: await focusId(page) };
    await page.keyboard.press('Escape'); immediate.closedByEscape = await settleClosed();
    await openIt(); await page.waitForTimeout(900);
    const settled = { focusInside: await focusIn(page, '#offcanvasClaimTransition'), focused: await focusId(page) };
    await shot(page, 'cu25-transition-open-en');
    await page.keyboard.press('Escape'); settled.closedByEscape = await settleClosed();
    const inert = await surf.evaluate((e) => e.inert === true || e.getAttribute('aria-hidden') === 'true' || !e.classList.contains('show'));
    verdict('CU-25c-transition-keyboard', immediate.focusInside && immediate.closedByEscape && settled.focusInside && settled.closedByEscape && settled.focused === 'transitionTarget' && inert,
      { claimId: id, immediate50ms: immediate, settled900ms: settled, hiddenAfterClose: inert });
    await c.close(); }
}

// ───────────────────────────── D1/D2/D4 regression (from Q64e) ─────────────────────────────
async function regression(browser) {
  const S = FX.shipments;
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'shell'); const e0 = jsErrors(); await ready(page);
    verdict('REG-D1-skeleton-hidden-after-load', !(await visibleBox(page.locator('#skeleton-loader'))) && await visibleBox(page.locator('#claims-table-host')), {});
    await c.close(); }
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'create'); const e0 = jsErrors(); await ready(page);
    await page.locator('.add-new').click(); await page.locator('#claimShipmentId').fill(S.DISPATCHED.shipmentId);
    await Promise.all([page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), page.locator('#btnResolveShipment').click()]);
    await page.locator('#claimReasonCode').fill('DAMAGE'); await page.locator('#claimClaimedAmount').fill('264.00'); await page.locator('#claimCurrency').fill('EUR');
    const created = page.waitForResponse((r) => r.url() === API && r.request().method() === 'POST'); const reload = page.waitForResponse(isList, { timeout: 10000 });
    await page.locator('#btnSaveClaim').click(); const cr = await created; const reloaded = await reload.then(() => true).catch(() => false); await page.waitForTimeout(1200);
    const shown = (await page.locator('#dt-claims').innerText()).includes('264.00'); const closed = !(await visibleBox(page.locator('#offcanvasCreateEdit')));
    const focusReturned = await focusIn(page, '.add-new');
    await shot(page, 'regression-create-reload-en');
    verdict('REG-D-01-create-reload-and-D4-focus', cr.status() === 201 && reloaded && shown && closed && focusReturned && jsErrors() === e0,
      { createStatus: cr.status(), reloaded, rowShowsAmount: shown, surfaceClosedAfterSave: closed, focusReturnedToAdd: focusReturned, newJsErrors: jsErrors() - e0 });
    await c.close(); }
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'transition'); const e0 = jsErrors(); await ready(page); await filterById(page, S.SEED.shipmentId);
    const id = FX.claims.OPEN2.claimId; const row = rowOf(page, id); await row.locator('.dropdown-toggle').click(); await row.locator('.js-transition[data-target-status="Investigating"]').click();
    await page.waitForTimeout(900); const focused = await focusId(page);
    await page.locator('#btnSubmitTransition').click();
    const resp = page.waitForResponse((r) => r.url().includes(`/api/${id}/transition?shipmentId=`)); await page.locator('.swal2-confirm').first().click(); const tr = await resp;
    const reloaded = await page.waitForResponse(isList, { timeout: 10000 }).then(() => true).catch(() => false); await page.waitForTimeout(600);
    await shot(page, 'regression-transition-investigating-en');
    verdict('REG-D2-transition-flow', focused === 'transitionTarget' && tr.status() === 200 && reloaded && jsErrors() === e0, { claimId: id, focusedOnOpen: focused, status: tr.status(), reloaded, newJsErrors: jsErrors() - e0 });
    await c.close(); }
}

// ───────────────────────────── CU-26 premium dialogs, no inline handlers, no token in storage ─────────────────────────────
async function cu26(browser) {
  const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'cu26'); await ready(page);
  await page.locator('.add-new').click(); await page.waitForTimeout(400); await page.keyboard.press('Escape'); await page.waitForTimeout(400);
  // a real confirmation surface: open Withdraw on OPEN1? no — use QuickView + transition open/close only (zero write)
  const inline = await page.evaluate(() => { const hits = []; for (const el of document.querySelectorAll('#dt-claims, #offcanvasCreateEdit, #offcanvasDetailsPreview, #offcanvasClaimTransition, #inlineFilterHost, #claims-error-state'))
    for (const n of [el, ...el.querySelectorAll('*')]) for (const a of n.getAttributeNames()) if (/^on[a-z]+$/.test(a)) hits.push(`${n.tagName}.${a}`); return hits; });
  const storage = await page.evaluate(() => { const all = []; for (const s of [localStorage, sessionStorage]) for (let i = 0; i < s.length; i++) { const k = s.key(i); all.push({ k, v: s.getItem(k) || '' }); }
    return { keys: all.map((x) => x.k), jwtLike: all.filter((x) => /eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}/.test(x.v) || /bearer/i.test(x.v)).map((x) => x.k) }; });
  const cookies = (await c.cookies(WEB)).map((k) => ({ name: k.name, httpOnly: k.httpOnly }));
  const jsCookie = await page.evaluate(() => document.cookie.split(';').map((x) => x.trim().split('=')[0]).filter(Boolean));
  const html = await page.content();
  const hasJwtInHtml = /eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}/.test(html);
  verdict('CU-26-premium-no-secrets', inline.length === 0 && storage.jwtLike.length === 0 && !hasJwtInHtml && result.dialogs.length === 0 && cookies.filter((k) => /auth|token|session/i.test(k.name)).every((k) => k.httpOnly),
    { inlineHandlersInModuleDom: inline, storageKeys: storage.keys, jwtLikeStorageKeys: storage.jwtLike, jwtInRenderedHtml: hasJwtInHtml, cookies, cookiesReadableByScript: jsCookie, nativeDialogsThisPhase: result.dialogs.length,
      note: 'native dialogs are also counted in every other phase file (dialogs[]); confirmations use window.showConfirm (Swal wrapper) — see cu08/cu20/cu22 phases' });
  await c.close();
}

// ───────────────────────────── CU-14 / CU-15 browser halves (Q122, added after checks-neg-a1: the HTTP half alone does not cover
// "one localized text + support ref; no shipment data in DOM" (CU-14) and "same text; action closed; hidden surfaces inert, not
// keyboard-reachable; reload" (CU-15)). Zero write: every target is unknown, foreign-scope or soft-deleted.
async function nfUi(browser) {
  const S = FX.shipments, C = FX.claims; const unknown = '00000000-0000-4000-8000-000000000001';
  const strip = (s) => s.replace(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi, '<ref>').replace(/\s+/g, ' ').trim();
  const msgOf = (s) => strip(s).split(/Support reference/i)[0].trim(); // the localized failure text without the support reference
  // CU-14 UI: resolve unknown / foreign-tenant / soft-deleted shipments from LE-A, and an LE-A shipment from LE-B
  const texts = {}; const dom = {};
  const resolveIn = async (actor, cases) => { const c = await ctx(browser, actor); const page = await c.newPage(); const net = watch(page, `cu14ui-${actor}`); await ready(page);
    await page.locator('.add-new').click(); await page.locator('#offcanvasCreateEdit').waitFor({ state: 'visible' });
    for (const [name, id] of cases) {
      await page.locator('#claimShipmentId').fill(id);
      await Promise.all([page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), page.locator('#btnResolveShipment').click()]); await page.waitForTimeout(400);
      texts[name] = await page.locator('#formClaimAlert').innerText().catch(() => '');
      dom[name] = { resolvedBlockVisible: await visibleBox(page.locator('#claimShipmentResolved')), shipmentNumber: (await page.locator('#claimShipmentNumber').innerText().catch(() => '')).trim(),
        saveDisabled: await page.locator('#btnSaveClaim').isDisabled() };
      await shot(page, `cu14-ui-${name}-en`);
    }
    const bodies = net.filter((n) => n.path && n.path.includes('/api/shipments/')).map((n) => n.okKeys);
    await c.close(); return bodies; };
  const b1 = await resolveIn('full', [['unknown', unknown], ['foreignTenant', S.T2SHIP.shipmentId], ['softDeleted', S.SOFTDEL.shipmentId]]);
  const b2 = await resolveIn('leb', [['foreignLe', S.DISPATCHED.shipmentId]]);
  const norm = Object.fromEntries(Object.entries(texts).map(([k, v]) => [k, strip(v)]));
  verdict('CU-14-ui-one-text-no-shipment-data', new Set(Object.values(norm)).size === 1 && Object.values(texts).every((t) => /[0-9a-f]{8}-[0-9a-f]{4}-/i.test(t))
    && Object.values(dom).every((d) => !d.resolvedBlockVisible) && [...b1, ...b2].every((k) => k === null),
    { alertTextsNormalised: norm, dom, successBodiesSeen: [...b1, ...b2].filter((k) => k !== null).length, note: 'support reference (UUID) replaced by <ref> before comparing' });
  // CU-15 UI: the row was loaded, then the claim is foreign/deleted at transition time (list GET fulfilled with that claim id;
  // the transition POST goes to the real adapter/backend and returns 404).
  const craftedRow = (id, shipmentId) => ({ items: [{ claimId: id, claimNumber: 'CLM-GONE', shipmentId, status: 'Open', claimedAmount: '10.00', currency: 'EUR' }] });
  const run = async (actor, name, id, shipmentId) => { const c = await ctx(browser, actor); let lists = 0;
    await c.route((u) => u.pathname === '/SupplyChain/Claims/api', (route) => { if (route.request().method() !== 'GET') return route.continue(); lists++;
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(craftedRow(id, shipmentId)) }); });
    const page = await c.newPage(); watch(page, `cu15ui-${name}`); await ready(page); const l0 = lists;
    await openAction(page, id, 'Investigating'); await page.locator('#btnSubmitTransition').click();
    const resp = page.waitForResponse((r) => r.url().includes(`/api/${id}/transition`)); await confirm(page); const r = await resp; await page.waitForTimeout(1500);
    const surf = page.locator('#offcanvasClaimTransition');
    const out = { status: r.status(), code: (await r.json().catch(() => ({})))?.error?.code || null, surfaceVisible: await visibleBox(surf), inert: await surf.evaluate((e) => e.inert === true),
      toast: strip(result.toasts.filter((x) => x.tag === `cu15ui-${name}`).map((x) => x.text).join(' | ')), reloadRequests: lists - l0 };
    await page.keyboard.press('Tab'); await page.keyboard.press('Tab'); out.focusInsideHiddenSurfaceAfterTab = await focusIn(page, '#offcanvasClaimTransition');
    await shot(page, `cu15-ui-${name}-en`); await c.close(); return out; };
  const t = { softDeletedClaim: await run('full', 'softdel', C.SOFTDEL1.claimId, S.SEED.shipmentId), foreignTenantClaim: await run('full', 'foreign-tenant', C.T2CLAIM.claimId, S.SEED.shipmentId),
    foreignLeClaim: await run('leb', 'foreign-le', C.OPEN1.claimId, S.SEED.shipmentId) };
  verdict('CU-15-ui-same-text-closed-inert-reload', Object.values(t).every((x) => x.status === 404 && x.code === 'CLAIM_NOT_FOUND' && !x.surfaceVisible && x.inert && x.reloadRequests >= 1
    && !x.focusInsideHiddenSurfaceAfterTab && x.toast.length > 0) && new Set(Object.values(t).map((x) => msgOf(x.toast))).size === 1 && msgOf(Object.values(t)[0].toast) === msgOf(Object.values(norm)[0]),
    { cases: t, cu14Message: msgOf(Object.values(norm)[0]), cu15Message: msgOf(Object.values(t)[0].toast), sameTextAsCu14: msgOf(Object.values(t)[0].toast) === msgOf(Object.values(norm)[0]) });
}

// CU-18 UI half (Q122, added after cu18-a1: the resolve projection carries no root by design (§32.4), so the null root is
// detected on create, not on resolve). Create on NULLROOT → 503 CLAIM_REFERENCE_INCOMPLETE shown localized, inputs kept, surface
// open; the explicit retry re-sends the same Idempotency-Key and identical body text (§32.8 503 rule). Zero write.
async function cu18ui(browser) {
  const S = FX.shipments; const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'cu18ui'); await ready(page);
  const posts = []; page.on('request', (r) => { if (r.url() === API && r.method() === 'POST') posts.push({ key: r.headers()['idempotency-key'], body: r.postData() }); });
  await page.locator('.add-new').click(); await page.locator('#claimShipmentId').fill(S.NULLROOT.shipmentId);
  await Promise.all([page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), page.locator('#btnResolveShipment').click()]);
  await page.locator('#claimReasonCode').fill('NULLROOT'); await page.locator('#claimClaimedAmount').fill('5.00'); await page.locator('#claimCurrency').fill('EUR');
  const r1p = page.waitForResponse((r) => r.url() === API && r.request().method() === 'POST'); await page.locator('#btnSaveClaim').click(); const r1 = await r1p; const b1 = await r1.json().catch(() => ({}));
  await page.waitForTimeout(600);
  const alert = await page.locator('#formClaimAlert').innerText().catch(() => '');
  const kept = { amount: await page.locator('#claimClaimedAmount').inputValue(), currency: await page.locator('#claimCurrency').inputValue(), reason: await page.locator('#claimReasonCode').inputValue() };
  const open = await visibleBox(page.locator('#offcanvasCreateEdit'));
  await shot(page, 'cu18-null-root-create-503-en');
  const r2p = page.waitForResponse((r) => r.url() === API && r.request().method() === 'POST'); await page.locator('#btnSaveClaim').click(); const r2 = await r2p; await page.waitForTimeout(400);
  verdict('CU-18-null-root-ui', r1.status() === 503 && b1?.error?.code === 'CLAIM_REFERENCE_INCOMPLETE' && alert.trim().length > 0 && !/Shipment reference is incomplete\./.test(alert)
    && open && kept.amount === '5.00' && kept.currency === 'EUR' && r2.status() === 503 && posts.length === 2 && posts[0].key === posts[1].key && posts[0].body === posts[1].body && result.rootLeak.length === 0,
    { firstStatus: r1.status(), code: b1?.error?.code, alertText: alert, inputsKept: kept, surfaceOpen: open, retryStatus: r2.status(), attempts: posts.map((p) => ({ keySame: p.key === posts[0].key, bodySame: p.body === posts[0].body })) });
  await c.close();
}

// regd01 (Q122, added after regression-a1: its innerText check reads only the visible client page; the list is ordered by
// claim number = CLM-<guid>, so a new row can land on any page). Same create → reload, then reads the reloaded DataTable data
// over all pages and the reload response itself. Writes +1 claim (+1 receipt/audit/outbox).
async function regd01(browser) {
  const S = FX.shipments; const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'regd01'); const e0 = jsErrors(); await ready(page);
  await page.locator('.add-new').click(); await page.locator('#claimShipmentId').fill(S.DISPATCHED.shipmentId);
  await Promise.all([page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), page.locator('#btnResolveShipment').click()]);
  await page.locator('#claimReasonCode').fill('DAMAGE'); await page.locator('#claimClaimedAmount').fill('265.00'); await page.locator('#claimCurrency').fill('EUR');
  const created = page.waitForResponse((r) => r.url() === API && r.request().method() === 'POST'); const reload = page.waitForResponse(isList, { timeout: 10000 });
  await page.locator('#btnSaveClaim').click(); const cr = await created; const cb = await cr.json().catch(() => ({})); const rl = await reload.catch(() => null);
  const rlBody = rl ? await rl.json().catch(() => null) : null; await page.waitForTimeout(1200);
  const dt = await page.evaluate((id) => { const api = window.jQuery('#dt-claims').DataTable(); const all = api.rows().data().toArray();
    const hit = all.find((r) => r.claimId === id); return { rows: all.length, pages: api.page.info().pages, hit: hit ? { claimedAmount: hit.claimedAmount, status: hit.status } : null }; }, cb.claimId);
  // show it on screen: client search for the new claim number (no server request; serverSide false)
  await page.evaluate((n) => window.jQuery('#dt-claims').DataTable().search(n).draw(), cb.claimNumber); await page.waitForTimeout(400);
  const shown = (await page.locator('#dt-claims tbody').innerText()).includes('265.00');
  const closed = !(await visibleBox(page.locator('#offcanvasCreateEdit'))); const focusReturned = await focusIn(page, '.add-new');
  await shot(page, 'regression-create-reload-search-en');
  verdict('REG-D-01-create-reload-and-D4-focus', cr.status() === 201 && !!rl && !!rlBody?.items?.find((i) => i.claimId === cb.claimId) && dt.hit?.claimedAmount === '265.00' && shown && closed && focusReturned && jsErrors() === e0,
    { createStatus: cr.status(), claimId: cb.claimId, reloadAfterCreate: !!rl, reloadResponseHasNewClaim: !!rlBody?.items?.find((i) => i.claimId === cb.claimId), dataTable: dt,
      shownAfterClientSearch: shown, surfaceClosedAfterSave: closed, focusReturnedToAdd: focusReturned, newJsErrors: jsErrors() - e0 });
  await c.close();
}

// ───────────────────────────── matrix (Q122): CU-07 + CU-08 controlling text with partial grants ─────────────────────────────
// The `readonly` actor (jane, LE-A) is re-granted per configuration by matrix_setup.py role, then re-logged in. EXTRA = config.
// Direct calls go through the same-origin MVC adapter with the page's antiforgery token.
async function matrix(browser) {
  const MX = JSON.parse(fs.readFileSync(path.join(EVD, 'raw', 'fixture', 'matrix-fixture.json'), 'utf8')); const C = MX.claims; const SEED = MX.seedShipment;
  const cfg = EXTRA; const c = await ctx(browser, 'readonly'); const page = await c.newPage(); watch(page, `matrix-${cfg}`); await ready(page);
  await filterById(page, SEED);
  const offered = async (id) => { const row = rowOf(page, id); if (!(await row.count())) return null; return row.locator('.js-transition').evaluateAll((els) => els.map((e) => e.getAttribute('data-target-status')).sort()); };
  const tr = (id, target, extra = {}) => adapter(page, 'POST', `${API}/${id}/transition?shipmentId=${SEED}`, JSON.stringify({ targetStatus: target, occurredAt: '2026-09-27T16:00:00+00:00', ...extra }));
  const dom = { addNew: await page.locator('.add-new').count(), open: await offered(C.MX_OPEN1.claimId), inv: await offered(C.MX_INV1.claimId), appr: await offered(C.MX_APPR1.claimId), rej: await offered(C.MX_REJ2.claimId) };
  await shot(page, `matrix-${cfg}-en`);
  const S = (x) => ({ status: x.status, code: x.errorCode }); let calls = {}; let ok;
  const J = (a) => JSON.stringify(a);
  if (cfg === 'createNoShipRead') {
    calls.create = S(await adapter(page, 'POST', API, `{"shipmentId":"${FX.shipments.DISPATCHED.shipmentId}","reasonCode":"X","claimedAmount":"1.00","currency":"EUR"}`));
    calls.resolve = S(await adapter(page, 'GET', `${API}/shipments/${FX.shipments.DISPATCHED.shipmentId}`));
    ok = dom.addNew === 0 && calls.create.status === 403 && calls.resolve.status === 403 && J(dom.open) === '[]';
  } else if (cfg === 'createOnly') {
    calls.withdraw = S(await tr(C.MX_OPEN4.claimId, 'Withdrawn')); calls.investigate = S(await tr(C.MX_OPEN4.claimId, 'Investigating'));
    ok = dom.addNew === 1 && J(dom.open) === '[]' && J(dom.inv) === '[]' && calls.withdraw.status === 403 && calls.investigate.status === 403;
  } else if (cfg === 'investigate') {
    calls.approveMissing = S(await tr(C.MX_INV1.claimId, 'Approved', { approvedAmount: '1.00' })); calls.rejectMissing = S(await tr(C.MX_INV1.claimId, 'Rejected'));
    calls.settleUnrelated = S(await tr(C.MX_APPR1.claimId, 'Settled'));
    calls.investigateExact = S(await tr(C.MX_OPEN1.claimId, 'Investigating')); calls.withdrawExact = S(await tr(C.MX_OPEN2.claimId, 'Withdrawn'));
    ok = dom.addNew === 0 && J(dom.open) === '["Investigating","Withdrawn"]' && J(dom.inv) === '[]' && J(dom.appr) === '[]' && J(dom.rej) === '[]'
      && calls.approveMissing.status === 403 && calls.rejectMissing.status === 403 && calls.settleUnrelated.status === 403 && calls.investigateExact.status === 200 && calls.withdrawExact.status === 200;
  } else if (cfg === 'decide') {
    calls.investigateMissing = S(await tr(C.MX_OPEN3.claimId, 'Investigating')); calls.settleUnrelated = S(await tr(C.MX_APPR1.claimId, 'Settled'));
    calls.rejectExact = S(await tr(C.MX_INV2.claimId, 'Rejected')); calls.closeExact = S(await tr(C.MX_REJ1.claimId, 'Closed'));
    ok = dom.addNew === 0 && J(dom.open) === '[]' && J(dom.inv) === '["Approved","Rejected"]' && J(dom.appr) === '[]' && J(dom.rej) === '["Closed"]'
      && calls.investigateMissing.status === 403 && calls.settleUnrelated.status === 403 && calls.rejectExact.status === 200 && calls.closeExact.status === 200;
  } else if (cfg === 'settle') {
    calls.closeSettleOnly = S(await tr(C.MX_REJ2.claimId, 'Closed')); calls.withdrawUnrelated = S(await tr(C.MX_OPEN3.claimId, 'Withdrawn'));
    calls.settleExact = S(await tr(C.MX_APPR1.claimId, 'Settled'));
    ok = dom.addNew === 0 && J(dom.open) === '[]' && J(dom.appr) === '["Settled"]' && J(dom.rej) === '[]'
      && calls.closeSettleOnly.status === 403 && calls.withdrawUnrelated.status === 403 && calls.settleExact.status === 200;
  } else throw new Error('unknown matrix config');
  verdict(`CU-08-matrix-${cfg}`, ok, { dom, calls });
  await c.close();
}

// ───────────────────────────── extra (Q122): CU-10 unchecked + mismatch, CU-11 full lexical list, CU-17 Planned/Cancelled ─────────────
// full actor (holds the CU-10 run-2 lane grant). Writes: CU-10 unchecked +1 claim, CU-11 `ZZZ` +1 claim.
async function extra(browser) {
  const S = FX.shipments; const MX = JSON.parse(fs.readFileSync(path.join(EVD, 'raw', 'fixture', 'matrix-fixture.json'), 'utf8'));
  const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'extra'); await ready(page);
  // CU-10 unchecked → carrierId omitted → 201; direct POST with a different carrier → 422 CLAIM_CARRIER_MISMATCH
  await page.locator('.add-new').click(); await page.locator('#claimShipmentId').fill(S.CARRIER.shipmentId);
  await Promise.all([page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), page.locator('#btnResolveShipment').click()]);
  const enabled = await page.locator('#claimLinkCarrier').isEnabled(); const checked = await page.locator('#claimLinkCarrier').isChecked();
  await page.locator('#claimReasonCode').fill('UNCHECKED'); await page.locator('#claimClaimedAmount').fill('41.00'); await page.locator('#claimCurrency').fill('EUR');
  const post = page.waitForRequest((r) => r.url() === API && r.method() === 'POST'); const created = page.waitForResponse((r) => r.url() === API && r.request().method() === 'POST');
  await page.locator('#btnSaveClaim').click(); const sent = await post; const cr = await created; await page.waitForTimeout(800);
  const mismatch = await adapter(page, 'POST', API, `{"shipmentId":"${S.CARRIER.shipmentId}","carrierId":"11111111-2222-4333-8444-555555555555","reasonCode":"X","claimedAmount":"1.00","currency":"EUR"}`);
  verdict('CU-10-unchecked-and-mismatch', enabled && !checked && cr.status() === 201 && !('carrierId' in JSON.parse(sent.postData())) && mismatch.status === 422 && mismatch.errorCode === 'CLAIM_CARRIER_MISMATCH',
    { checkboxEnabled: enabled, checkedByDefault: checked, uncheckedSentBody: sent.postData(), uncheckedStatus: cr.status(), mismatch: { status: mismatch.status, code: mismatch.errorCode } });
  // CU-11 lexical (HTTP through the adapter)
  const body = (amt, cur = 'EUR') => `{"shipmentId":"${S.DISPATCHED.shipmentId}","reasonCode":"LEX","claimedAmount":${JSON.stringify(amt)},"currency":"${cur}"}`;
  const lex = {}; for (const a of ['1e2', '+1', ' 1', '1.', '.1']) lex[`amount ${JSON.stringify(a)}`] = await adapter(page, 'POST', API, body(a));
  for (const a of ['0', '-0', '-1']) lex[`amount ${JSON.stringify(a)}`] = await adapter(page, 'POST', API, body(a));
  lex['currency "usd"'] = await adapter(page, 'POST', API, body('5.00', 'usd')); lex['currency "ZZZ"'] = await adapter(page, 'POST', API, body('5.00', 'ZZZ'));
  lex['amount as JSON number 5'] = await adapter(page, 'POST', API, `{"shipmentId":"${S.DISPATCHED.shipmentId}","reasonCode":"LEX","claimedAmount":5,"currency":"EUR"}`);
  const L = Object.fromEntries(Object.entries(lex).map(([k, v]) => [k, { status: v.status, code: v.errorCode }]));
  const want400 = ['amount "1e2"', 'amount "+1"', 'amount " 1"', 'amount "1."', 'amount ".1"', 'currency "usd"', 'amount as JSON number 5'];
  const want422 = ['amount "0"', 'amount "-0"', 'amount "-1"'];
  // UI: typed currency sent unchanged (no auto-uppercase), 400 localized, inputs kept
  await page.locator('.add-new').click(); await page.locator('#claimShipmentId').fill(S.DISPATCHED.shipmentId);
  await Promise.all([page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), page.locator('#btnResolveShipment').click()]);
  await page.locator('#claimReasonCode').fill('LEX-UI'); await page.locator('#claimClaimedAmount').fill('1e2'); await page.locator('#claimCurrency').fill('usd');
  const p2 = page.waitForRequest((r) => r.url() === API && r.method() === 'POST'); const r2 = page.waitForResponse((r) => r.url() === API && r.request().method() === 'POST');
  await page.locator('#btnSaveClaim').click(); const s2 = await p2; const x2 = await r2; await page.waitForTimeout(500);
  const ui = { sentBody: s2.postData(), status: x2.status(), alert: await page.locator('#formClaimAlert').innerText().catch(() => ''), amountKept: await page.locator('#claimClaimedAmount').inputValue(), currencyKept: await page.locator('#claimCurrency').inputValue() };
  await shot(page, 'cu11-lexical-400-en');
  verdict('CU-11-lexical-full', want400.every((k) => L[k].status === 400 && L[k].code === 'INVALID_REQUEST') && want422.every((k) => L[k].status === 422 && L[k].code === 'CLAIM_AMOUNT_INVALID')
    && L['currency "ZZZ"'].status === 201 && JSON.parse(ui.sentBody).claimedAmount === '1e2' && JSON.parse(ui.sentBody).currency === 'usd' && ui.status === 400 && ui.amountKept === '1e2' && ui.currencyKept === 'usd' && ui.alert.trim().length > 0,
    { http: L, ui });
  await page.keyboard.press('Escape'); await page.waitForTimeout(600);
  // CU-17 Planned / Cancelled: UI note + disabled submit; direct POST → 422
  const el = {};
  for (const [n, s] of [['Planned', MX.shipments.PLANNED], ['Cancelled', MX.shipments.CANCELLED], ['Draft', S.DRAFT]]) {
    await page.locator('.add-new').click(); await page.locator('#claimShipmentId').fill(s.shipmentId);
    await Promise.all([page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), page.locator('#btnResolveShipment').click()]); await page.waitForTimeout(400);
    el[n] = { resolvedStatus: s.status, noteVisible: await visibleBox(page.locator('#claimIneligibleNote')), submitDisabled: await page.locator('#btnSaveClaim').isDisabled() };
    await shot(page, `cu17-${n.toLowerCase()}-en`); await page.keyboard.press('Escape'); await page.waitForTimeout(600);
    const d = await adapter(page, 'POST', API, `{"shipmentId":"${s.shipmentId}","reasonCode":"X","claimedAmount":"10.00","currency":"EUR"}`); el[n].direct = { status: d.status, code: d.errorCode };
  }
  verdict('CU-17-planned-cancelled-draft', Object.values(el).every((x) => x.noteVisible && x.submitDisabled && x.direct.status === 422 && x.direct.code === 'CLAIM_SHIPMENT_INELIGIBLE'), { cases: el });
  await c.close();
}

// matrixdom (Q122, added after matrix-*-a1: the SEED filter returned more rows than one client page, so the Open row was not
// on the visible page and its offered actions read as null). Zero write. All rows shown (page.len(-1), client only); one row
// per status; offered targets compared with the §32.7 table for the configured grant. EXTRA = config ('full' = full actor).
async function matrixdom(browser) {
  const MX = JSON.parse(fs.readFileSync(path.join(EVD, 'raw', 'fixture', 'matrix-fixture.json'), 'utf8')).claims;
  const rows = { Open: MX.MX_OPEN3.claimId, Investigating: MX.MX_INV1.claimId, Approved: FX.claims.APPRBAD.claimId, Rejected: MX.MX_REJ2.claimId,
    Settled: MX.MX_APPR1.claimId, Withdrawn: MX.MX_OPEN2.claimId, Closed: MX.MX_REJ1.claimId };
  const full = { Open: ['Investigating', 'Withdrawn'], Investigating: ['Approved', 'Rejected'], Approved: ['Settled'], Rejected: ['Closed'], Settled: ['Closed'], Withdrawn: [], Closed: [] };
  const keyOf = { Investigating: 'investigate', Withdrawn: 'investigate', Approved: 'decide', Rejected: 'decide', Closed: 'decide', Settled: 'settle' };
  const grants = { createNoShipRead: [], createOnly: [], investigate: ['investigate'], decide: ['decide'], settle: ['settle'], full: ['investigate', 'decide', 'settle'] }[EXTRA];
  const addExpected = { createNoShipRead: 0, createOnly: 1, investigate: 0, decide: 0, settle: 0, full: 1 }[EXTRA];
  const actor = EXTRA === 'full' ? 'full' : 'readonly';
  const c = await ctx(browser, actor); const page = await c.newPage(); watch(page, `matrixdom-${EXTRA}`); await ready(page);
  await page.evaluate(() => window.jQuery('#dt-claims').DataTable().page.len(-1).draw()); await page.waitForTimeout(500);
  const got = {}; const want = {};
  for (const [st, id] of Object.entries(rows)) {
    const row = rowOf(page, id); const present = await row.count();
    got[st] = present ? (await row.locator('.js-transition').evaluateAll((els) => els.map((e) => e.getAttribute('data-target-status')))).sort() : null;
    const shown = (await row.locator('td').nth(3).innerText().catch(() => '')).trim();
    want[st] = full[st].filter((t) => grants.includes(keyOf[t])).sort(); got[`${st}.label`] = shown;
  }
  const addNew = await page.locator('.add-new').count();
  await shot(page, `matrixdom-${EXTRA}-en`);
  verdict(`CU-08-matrixdom-${EXTRA}`, addNew === addExpected && Object.keys(rows).every((st) => JSON.stringify(got[st]) === JSON.stringify(want[st])), { actor, addNew, addExpected, offered: got, expected: want });
  await c.close();
}

// extra2 (Q122, added after extra-a1): (a) CU-10 mismatch with an EXISTING second carrier (extra-a1 used a non-existent id → 404);
// (b) keyboard after a rejected submit: where is focus, does Escape close the create surface (extra-a1 stopped because it did not);
// (c) CU-17 Planned/Cancelled/Draft (not reached in extra-a1). Zero write (400/422 only).
async function extra2(browser) {
  const S = FX.shipments; const MX = JSON.parse(fs.readFileSync(path.join(EVD, 'raw', 'fixture', 'matrix-fixture.json'), 'utf8'));
  const car2 = JSON.parse(fs.readFileSync(path.join(EVD, 'raw', 'fixture', 'carrier2.json'), 'utf8')).carrierId;
  const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'extra2'); await ready(page);
  const mismatch = await adapter(page, 'POST', API, `{"shipmentId":"${S.CARRIER.shipmentId}","carrierId":"${car2}","reasonCode":"X","claimedAmount":"1.00","currency":"EUR"}`);
  const unknownCarrier = await adapter(page, 'POST', API, `{"shipmentId":"${S.CARRIER.shipmentId}","carrierId":"11111111-2222-4333-8444-555555555555","reasonCode":"X","claimedAmount":"1.00","currency":"EUR"}`);
  const noCarrierShipment = await adapter(page, 'POST', API, `{"shipmentId":"${S.DISPATCHED.shipmentId}","carrierId":"${car2}","reasonCode":"X","claimedAmount":"1.00","currency":"EUR"}`);
  verdict('CU-10-direct-different-carrier-422', mismatch.status === 422 && mismatch.errorCode === 'CLAIM_CARRIER_MISMATCH',
    { existingDifferentCarrier: { status: mismatch.status, code: mismatch.errorCode }, nonExistentCarrier: { status: unknownCarrier.status, code: unknownCarrier.errorCode },
      carrierOnShipmentWithoutCarrier: { status: noCarrierShipment.status, code: noCarrierShipment.errorCode } });
  // (b) Escape after a rejected (400) submit, measured three ways
  const esc = {};
  for (const mode of ['noRefocus', 'afterTabIntoSurface']) {
    await page.locator('.add-new').click(); await page.locator('#offcanvasCreateEdit').waitFor({ state: 'visible' }); await page.waitForTimeout(500);
    await page.locator('#claimShipmentId').fill(S.DISPATCHED.shipmentId);
    await Promise.all([page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), page.locator('#btnResolveShipment').click()]);
    await page.locator('#claimReasonCode').fill('ESC'); await page.locator('#claimClaimedAmount').fill('1e2'); await page.locator('#claimCurrency').fill('EUR');
    const rr = page.waitForResponse((r) => r.url() === API && r.request().method() === 'POST'); await page.locator('#btnSaveClaim').focus(); await page.keyboard.press('Enter'); const x = await rr; await page.waitForTimeout(400);
    const active = await focusId(page); const inside = await focusIn(page, '#offcanvasCreateEdit');
    if (mode === 'afterTabIntoSurface') { await page.keyboard.press('Tab'); await page.waitForTimeout(100); }
    const insideBeforeEsc = await focusIn(page, '#offcanvasCreateEdit');
    await page.keyboard.press('Escape'); await page.waitForTimeout(900);
    const closed = !(await visibleBox(page.locator('#offcanvasCreateEdit')));
    esc[mode] = { submitStatus: x.status(), activeAfterResponse: active, focusInsideAfterResponse: inside, focusInsideBeforeEscape: insideBeforeEsc, closedByEscape: closed };
    if (mode === 'noRefocus') await shot(page, 'kbd-escape-after-400-en');
    if (!closed) { await page.locator('#offcanvasCreateEdit .btn-close').click(); await page.locator('#offcanvasCreateEdit').waitFor({ state: 'hidden' }); await page.waitForTimeout(400); }
  }
  verdict('KBD-escape-after-rejected-submit', esc.noRefocus.closedByEscape, { ...esc, note: 'CU-25 keyboard: Escape closes the create surface after a rejected submit (focus position recorded)' });
  // (c) CU-17
  const el = {};
  for (const [n, s] of [['Planned', MX.shipments.PLANNED], ['Cancelled', MX.shipments.CANCELLED], ['Draft', S.DRAFT]]) {
    await page.locator('.add-new').click(); await page.locator('#offcanvasCreateEdit').waitFor({ state: 'visible' }); await page.locator('#claimShipmentId').fill(s.shipmentId);
    await Promise.all([page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), page.locator('#btnResolveShipment').click()]); await page.waitForTimeout(400);
    el[n] = { fixtureStatus: s.status, shownStatus: (await page.locator('#claimShipmentStatus').innerText().catch(() => '')).trim(), noteVisible: await visibleBox(page.locator('#claimIneligibleNote')), submitDisabled: await page.locator('#btnSaveClaim').isDisabled() };
    await shot(page, `cu17-${n.toLowerCase()}-en`);
    await page.locator('#offcanvasCreateEdit .btn-close').click(); await page.locator('#offcanvasCreateEdit').waitFor({ state: 'hidden' }); await page.waitForTimeout(400);
    const d = await adapter(page, 'POST', API, `{"shipmentId":"${s.shipmentId}","reasonCode":"X","claimedAmount":"10.00","currency":"EUR"}`); el[n].direct = { status: d.status, code: d.errorCode };
  }
  verdict('CU-17-planned-cancelled-draft', Object.values(el).every((x) => x.noteVisible && x.submitDisabled && x.direct.status === 422 && x.direct.code === 'CLAIM_SHIPMENT_INELIGIBLE'), { cases: el });
  await c.close();
}

// kbd2 (Q122, added after extra2-a1 found Escape ineffective after a rejected create submit): same measurement on the transition
// surface. MX_INV1 (Investigating, claimed 150.00) → Approved with approvedAmount 999.00 → 422 CLAIM_APPROVAL_AMOUNT_INVALID (zero write).
async function kbd2(browser) {
  const MX = JSON.parse(fs.readFileSync(path.join(EVD, 'raw', 'fixture', 'matrix-fixture.json'), 'utf8')).claims; const id = MX.MX_INV1.claimId;
  const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'kbd2'); await ready(page);
  await page.evaluate(() => window.jQuery('#dt-claims').DataTable().page.len(-1).draw()); await page.waitForTimeout(400);
  await openAction(page, id, 'Approved'); await page.waitForTimeout(900); await page.locator('#transitionApprovedAmount').fill('999.00');
  await page.locator('#btnSubmitTransition').focus(); await page.keyboard.press('Enter'); await page.locator('.swal2-popup').waitFor({ state: 'visible' });
  const swalFocus = await focusId(page); const rr = page.waitForResponse((r) => r.url().includes(`/api/${id}/transition`)); await confirm(page); const x = await rr; await page.waitForTimeout(600); // kbd2-a2: click Confirm (kbd2-a1: Enter on the popup did not confirm)
  const out = { focusedInConfirmPopup: swalFocus, submitStatus: x.status(), code: (await x.json().catch(() => ({})))?.error?.code || null, activeAfterResponse: await focusId(page), focusInside: await focusIn(page, '#offcanvasClaimTransition'),
    alert: (await page.locator('#formClaimTransitionAlert').innerText().catch(() => '')).split('\n')[0] };
  await page.keyboard.press('Escape'); await page.waitForTimeout(900); out.closedByEscape = !(await visibleBox(page.locator('#offcanvasClaimTransition')));
  await shot(page, 'kbd-transition-escape-after-422-en');
  verdict('KBD-transition-escape-after-rejected-submit', out.closedByEscape, out);
  await c.close();
}


// ───────────────────────────── Q129: CU-25 / Q122-D5 re-run, en + ar ─────────────────────────────
async function cu25(browser) {
  const S = FX.shipments; const out = {};
  for (const lang of ['en', 'ar']) {
    const c = await ctx(browser, 'full', { locale: lang }); const page = await c.newPage(); watch(page, `cu25-${lang}`); const e0 = jsErrors();
    await ready(page, `${PAGE}?culture=${lang}&ui-culture=${lang}`);
    const dir = await page.evaluate(() => document.documentElement.getAttribute('dir'));
    const surf = page.locator('#offcanvasCreateEdit');
    await page.locator('.add-new').click(); await surf.waitFor({ state: 'visible' }); await page.waitForTimeout(500);
    await page.locator('#claimShipmentId').fill(S.DISPATCHED.shipmentId);
    await Promise.all([page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), page.locator('#btnResolveShipment').click()]);
    await page.locator('#claimReasonCode').fill('CU25'); await page.locator('#claimClaimedAmount').fill('1e2'); await page.locator('#claimCurrency').fill('EUR');
    const rr = page.waitForResponse((r) => r.url() === API && r.request().method() === 'POST');
    await page.locator('#btnSaveClaim').focus(); await page.keyboard.press('Enter'); const x = await rr; const xb = await x.json().catch(() => ({}));
    const t0 = Date.now(); let inside = false; while (!inside && Date.now() - t0 < 2000) { inside = await focusIn(page, '#offcanvasCreateEdit'); if (!inside) await page.waitForTimeout(50); }
    const after = { submitStatus: x.status(), code: xb?.error?.code || null, activeAfterResponse: await focusId(page), focusInsideAfterResponse: inside, focusWaitMs: Date.now() - t0,
      saveEnabled: await page.locator('#btnSaveClaim').isEnabled(), alertVisible: await visibleBox(page.locator('#formClaimAlert')),
      alertText: (await page.locator('#formClaimAlert').innerText().catch(() => '')).split('\n')[0].slice(0, 200),
      amountKept: await page.locator('#claimClaimedAmount').inputValue(), currencyKept: await page.locator('#claimCurrency').inputValue() };
    await shot(page, `03a-create-400-focus-${lang}`);
    await page.keyboard.press('Escape'); await page.waitForTimeout(900);   // no Tab before Escape
    const closed = !(await visibleBox(surf)); const focusReturned = await focusIn(page, '.add-new');
    const res = { dir, ...after, tabPressedBeforeEscape: false, closedByEscape: closed, focusReturnedToAdd: focusReturned, focusedAfterClose: await focusId(page), newJsErrors: jsErrors() - e0 };
    await shot(page, `03b-create-400-escape-${lang}`);
    if (!closed) { await page.locator('#offcanvasCreateEdit .btn-close').click(); await surf.waitFor({ state: 'hidden' }); }
    verdict(`CU-25-create-400-escape-${lang}`, res.submitStatus === 400 && res.focusInsideAfterResponse && res.closedByEscape && res.focusReturnedToAdd && res.newJsErrors === 0
      && (lang !== 'ar' || dir === 'rtl'), res);
    out[lang] = res; await c.close();
  }
}

// ───────────────────────────── Q129: CU-11 (from Q122 extra, CU-11 part only) ─────────────────────────────
async function cu11(browser) {
  const S = FX.shipments; const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'cu11'); await ready(page); const e0 = jsErrors();
  const body = (amt, cur = 'EUR') => `{"shipmentId":"${S.DISPATCHED.shipmentId}","reasonCode":"LEX","claimedAmount":${JSON.stringify(amt)},"currency":"${cur}"}`;
  const lex = {}; for (const a of ['1e2', '+1', ' 1', '1.', '.1']) lex[`amount ${JSON.stringify(a)}`] = await adapter(page, 'POST', API, body(a));
  for (const a of ['0', '-0', '-1']) lex[`amount ${JSON.stringify(a)}`] = await adapter(page, 'POST', API, body(a));
  lex['currency "usd"'] = await adapter(page, 'POST', API, body('5.00', 'usd')); lex['currency "ZZZ"'] = await adapter(page, 'POST', API, body('5.00', 'ZZZ'));
  lex['amount as JSON number 5'] = await adapter(page, 'POST', API, `{"shipmentId":"${S.DISPATCHED.shipmentId}","reasonCode":"LEX","claimedAmount":5,"currency":"EUR"}`);
  const L = Object.fromEntries(Object.entries(lex).map(([k, v]) => [k, { status: v.status, code: v.errorCode }]));
  const want400 = ['amount "1e2"', 'amount "+1"', 'amount " 1"', 'amount "1."', 'amount ".1"', 'currency "usd"', 'amount as JSON number 5'];
  const want422 = ['amount "0"', 'amount "-0"', 'amount "-1"'];
  await page.locator('.add-new').click(); await page.locator('#claimShipmentId').fill(S.DISPATCHED.shipmentId);
  await Promise.all([page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), page.locator('#btnResolveShipment').click()]);
  await page.locator('#claimReasonCode').fill('LEX-UI'); await page.locator('#claimClaimedAmount').fill('1e2'); await page.locator('#claimCurrency').fill('usd');
  const p2 = page.waitForRequest((r) => r.url() === API && r.method() === 'POST'); const r2 = page.waitForResponse((r) => r.url() === API && r.request().method() === 'POST');
  await page.locator('#btnSaveClaim').click(); const s2 = await p2; const x2 = await r2; await page.waitForTimeout(500);
  const ui = { sentBody: s2.postData(), status: x2.status(), alert: await page.locator('#formClaimAlert').innerText().catch(() => ''), amountKept: await page.locator('#claimClaimedAmount').inputValue(), currencyKept: await page.locator('#claimCurrency').inputValue() };
  await shot(page, 'cu11-lexical-400-en');
  verdict('CU-11-lexical-full', want400.every((k) => L[k].status === 400 && L[k].code === 'INVALID_REQUEST') && want422.every((k) => L[k].status === 422 && L[k].code === 'CLAIM_AMOUNT_INVALID')
    && L['currency "ZZZ"'].status === 201 && JSON.parse(ui.sentBody).claimedAmount === '1e2' && JSON.parse(ui.sentBody).currency === 'usd' && ui.status === 400 && ui.amountKept === '1e2' && ui.currencyKept === 'usd' && ui.alert.trim().length > 0 && jsErrors() === e0,
    { http: L, ui, newJsErrors: jsErrors() - e0 });
  await c.close();
}

const PH = { cu25, cu11, kbd2, extra2, matrixdom, matrix, extra, regd01, cu18ui, 'nf-ui': nfUi, login, vs1, spec, 'checks-neg': checksNeg, 'checks-mut': checksMut, 'ui-flows': uiFlows, saveview, crafted, cu05, cu12, cu18, cu19, cu20, cu22, l10n, kbd, regression, cu26 };
const browser = PHASE === 'spec' ? null : await chromium.launch({ headless: true });
try {
  if (PHASE === 'spec') spec();
  else if (PH[PHASE]) await PH[PHASE](browser);
  else throw new Error('unknown phase');
} catch (e) { result.error = String(e && e.message || e).slice(0, 1500); }
finally { if (browser) await browser.close(); save(); }
process.exit(result.error || Object.values(result.cases).some((c) => c.result === 'FAIL') || result.rootLeak.length || result.dialogs.length ? 1 : 0);
