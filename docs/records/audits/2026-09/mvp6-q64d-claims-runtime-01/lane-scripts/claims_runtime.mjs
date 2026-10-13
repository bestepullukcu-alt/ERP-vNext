// Q64d browser/HTTP harness — MOD-0187 Claims UI DRAFT v2 (archive 1a57c879…) on the Q103 accepted base, kit slot 7.
// Derived from the Q64b (Q97 v2.1) harness; the Q64b phases keep their names. Playwright 1.59.1, Chromium, headless.
// Run ONLY by the lane supervisor (task harness): it supplies ACTOR_PW_<LABEL>; this script never prints or writes them.
// Usage: node claims_runtime.mjs <phase> <webPort> <evidenceDir> <fixtureJson> <stateDir> <runtimeDir> [extra]
//   login       real Web login per actor (Web → Gateway → Auth); storage state ONLY in <stateDir> (outside the repo, 0600).
//   vs1         CU-VS1 early vertical slice with network capture + root-leak assertion (CT F8).
//   spec        the v2 scenarios (claims-ui.spec.mjs, byte-identical to the v2 archive) with @playwright/test.
//   checks-neg  zero-write: 401/403 surfaces, cross-LE, cross-tenant, soft-deleted, unknown, CU-17 direct 422, CU-23, F8.
//   checks-mut  CU-10 carrier-linked create.
//   ui-flows    row-scoped UI flows on the NATIVE v2 index.js (no served replacement): D-01 reload, D-02 single shell,
//               CU-02, CU-31, CU-08, CU-21.
//   saveview    Save View / Reset (Apply → dirty, Reset → factory, Save → clean, reload restores the saved view).
//   crafted     browser-only crafted list/resolve responses (route.fulfill): CU-03, CU-04, CU-05, CU-13. Zero write.
//   cu12        presence ≠ nonempty: empty reasonCode + empty evidence string (UI), empty resolution/note (HTTP).
//   cu18        null root → 503 CLAIM_REFERENCE_INCOMPLETE, malformed root → 502 CLAIM_REFERENCE_INVALID (UI + HTTP).
//   cu19        idempotency: HTTP replay + 409, browser retry after a lost response (evidence-only fault proxy, DN-01 A).
//   cu20        approval amount boundaries (HTTP) + the Approved field/success message (UI).
//   cu22        Approved → Settled wording (label, confirmation) + 200.
//   l10n        CU-24/CU-25: 7 languages, RTL, widths 390/768/1024/1280/1440, overflow, keyboard focus/Escape.
//   cu26        premium dialogs only, no native dialog, no inline handler in the rendered DOM, no token in browser storage.
// Every page also records native dialogs (must stay 0), console errors and the MOD-0013 toast texts.
// PNGs only via page.screenshot({ path }) (owner decision MVP6-PNG-METHOD-OWNER-DECISION-01). All browser URLs are on
// 127.0.0.1:<webPort>. The harness never deletes a file.
import fs from 'node:fs'; import path from 'node:path'; import crypto from 'node:crypto'; import { spawnSync } from 'node:child_process';
import { createRequire } from 'node:module';

const [PHASE, WEBPORT, EVD, FXJSON, STATE, RUNTIME, EXTRA] = process.argv.slice(2);
// ESM ignores NODE_PATH: resolve Playwright from the lane runtime folder (~/mvp6-env), never from the repository.
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
const FX = JSON.parse(fs.readFileSync(FXJSON, 'utf8')).fixtures;
const ROOTS = Object.values(FX.shipments).map((s) => s.lifecycleCorrelationId).filter(Boolean).map((r) => r.toLowerCase());
const uniq = (base) => { let n = 1; while (fs.existsSync(path.join(OUTD, `${base}-a${n}.json`))) n++; return path.join(OUTD, `${base}-a${n}.json`); };
const result = { phase: PHASE, startedAt: new Date().toISOString(), web: WEB, playwright: PW_VERSION, cases: {}, png: [], console: [], toasts: [], dialogs: [], rootLeak: [] };
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
    if (!u.pathname.startsWith('/SupplyChain/Claims') && !u.pathname.startsWith('/api/personalization')) return;
    const rq = r.request(); let text = '';
    try { text = await r.text(); } catch (_) { }
    const hdrs = r.headers(); const blob = (text + JSON.stringify(hdrs)).toLowerCase();
    for (const root of ROOTS) if (blob.includes(root)) result.rootLeak.push({ tag, path: u.pathname + u.search, root });
    let body = null; try { body = JSON.parse(text); } catch (_) { }
    net.push({ at: iso(), method: rq.method(), path: u.pathname + u.search, status: r.status(), host: u.host,
      requestCorrelation: rq.headers()['x-correlation-id'] || null, idempotencyKey: rq.headers()['idempotency-key'] || null,
      requestBodyText: rq.method() === 'POST' || rq.method() === 'PUT' ? rq.postData() : null,
      responseCorrelation: hdrs['x-correlation-id'] || null, errorCode: body?.error?.code || null, errorCorrelation: body?.error?.correlationId || null,
      okKeys: body && !body.error && !Array.isArray(body) ? Object.keys(body).sort() : null, itemCount: Array.isArray(body?.items) ? body.items.length : null });
  });
  page.on('request', (r) => { const u = new URL(r.url()); if (u.host !== `127.0.0.1:${WEBPORT}` && !u.protocol.startsWith('data')) net.push({ foreignRequest: r.url() }); });
  return net;
}
async function ctx(browser, actor, opts = {}) {
  return browser.newContext({ storageState: actor ? statePath(actor) : undefined, timezoneId: 'UTC', locale: opts.locale || 'en-US',
    viewport: opts.viewport || { width: 1366, height: 900 } });
}
const save = () => { const f = uniq(PHASE); result.endedAt = iso(); fs.writeFileSync(f, JSON.stringify(result, null, 2) + '\n');
  console.log(JSON.stringify({ phase: PHASE, file: path.basename(f), verdicts: Object.fromEntries(Object.entries(result.cases).map(([k, v]) => [k, v.result])),
    rootLeak: result.rootLeak.length, dialogs: result.dialogs.length, error: result.error || null })); };
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
  await page.locator('#skeleton-loader').waitFor({ state: 'hidden' }); await page.locator('#dt-claims tbody tr').first().waitFor(); };
const rowOf = (page, id) => page.locator(`#dt-claims tbody tr:has(.js-quick-view[data-claim-id="${id}"])`);
const openAction = async (page, id, target) => { const row = rowOf(page, id); await row.locator('.dropdown-toggle').click();
  await row.locator(`.js-transition[data-target-status="${target}"]`).click(); await page.locator('#btnSubmitTransition').waitFor({ state: 'visible' }); };
const confirm = async (page) => { await page.locator('.swal2-confirm').first().click(); };
const jsErrors = () => result.console.filter((m) => m.type === 'pageerror' || /xhr\.abort|TypeError|already been declared/.test(m.text)).length;
const filterById = async (page, shipmentId) => { await page.locator('.dt-filter-btn').click(); await page.locator('#filterShipmentId').fill(shipmentId);
  await Promise.all([page.waitForResponse(isList), page.locator('#btnFilterApply').click()]); await page.waitForTimeout(400); };

// ───────────────────────────── Q64b phases (unchanged logic, v2 selectors) ─────────────────────────────
async function login(browser) {
  fs.mkdirSync(STATE, { recursive: true, mode: 0o700 });
  for (const [actor, [email, tenant]] of Object.entries(ACT)) {
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
  verdict('CU-VS1', pageResp.status() === 200 && lr.status() === 200 && rr.status() === 200 && cr.status() === 201 && cb.status === 'Open' && /^CLM-/.test(cb.claimNumber || '')
    && sent.postData() === `{"shipmentId":"${s.shipmentId}","reasonCode":"DAMAGE","claimedAmount":"250.00","currency":"EUR"}` && rowText.includes('250.00') && linkDisabled && reloaded,
  { page: pageResp.status(), list: lr.status(), resolve: rr.status(), resolveKeys: Object.keys(resolveBody).sort(), carrierCheckboxDisabled: linkDisabled,
    createStatus: cr.status(), createBody: cb, sentBody: sent.postData(), idempotencyKeyPresent: !!sent.headers()['idempotency-key'],
    browserRequestCorrelation: sent.headers()['x-correlation-id'], browserResponseCorrelation: cr.headers()['x-correlation-id'],
    responseCorrelationIsRoot: (cr.headers()['x-correlation-id'] || '').toLowerCase() === (s.lifecycleCorrelationId || '').toLowerCase(),
    listReloadedAfterCreate: reloaded, reloadShows25000: rowText.includes('250.00') });
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
  // '@playwright/test/cli.js' is not an exported subpath (spec-a1 failed on it); resolve the package root through its main entry.
  const cli = path.join(path.dirname(req.resolve('@playwright/test')), 'cli.js');
  const r = spawnSync(process.execPath, [cli, 'test', '-c', path.join(RUNTIME, 'playwright.config.mjs')], { cwd: RUNTIME, env, encoding: 'utf8', timeout: 900000 });
  result.cases.spec = { result: r.status === 0 ? 'PASS' : 'FAIL', exit: r.status, specSha256: sha(fs.readFileSync(specSrc)), stdoutTail: (r.stdout || '').slice(-6000), stderrTail: (r.stderr || '').slice(-2000) };
  for (const f of fs.readdirSync(env.CLAIMS_EVIDENCE_DIR)) result.png.push({ name: `spec/${f}`, sha256: sha(fs.readFileSync(path.join(env.CLAIMS_EVIDENCE_DIR, f))), phase: 'spec', tool: `@playwright/test ${PW_VERSION} page.screenshot` });
}

async function checksNeg(browser) {
  const S = FX.shipments, C = FX.claims; const unknown = '00000000-0000-4000-8000-000000000001';
  { const c = await ctx(browser, null); const page = await c.newPage(); await page.goto(`${WEB}/account/login?tenantId=${T1}`);
    const api = await adapter(page, 'GET', API); const pg = await page.request.get(PAGE, { maxRedirects: 0 });
    verdict('401-json-adapter', api.status === 401 && !api.location && (api.contentType || '').includes('json'),
      { api, bodyCodeIsUnauthenticated: api.errorCode === 'UNAUTHENTICATED', pageStatus: pg.status(), pageLocationPath: pg.headers()['location'] ? new URL(pg.headers()['location'], WEB).pathname : null });
    await c.close(); }
  { const c = await ctx(browser, 'noread'); const page = await c.newPage(); const net = watch(page, 'noread');
    const r = await page.goto(PAGE); await page.waitForLoadState('networkidle').catch(() => null);
    const dom = { table: await page.locator('#dt-claims').count(), skeleton: await page.locator('#skeleton-loader').count(), filter: await page.locator('#inlineFilterHost').count(),
      addNew: await page.locator('.add-new').count(), title: await page.locator('h4, h5').allInnerTexts() };
    await shot(page, 'uas001-noread-access-denied-en');
    const list = await adapter(page, 'GET', API); const res = await adapter(page, 'GET', `${API}/shipments/${S.DISPATCHED.shipmentId}`);
    verdict('CU-06-noread', r.status() === 200 && !new URL(page.url()).pathname.startsWith('/account') && dom.table === 0 && dom.skeleton === 0 && dom.filter === 0 && dom.addNew === 0
      && list.status === 403 && list.errorCode === 'FORBIDDEN' && res.status === 403 && net.filter((n) => n.path && n.path.startsWith('/SupplyChain/Claims/api')).length === 0,
    { pageStatus: r.status(), finalPath: new URL(page.url()).pathname, dom, listAdapter: list, resolveAdapter: res, browserAdapterCalls: net.filter((n) => n.path && n.path.startsWith('/SupplyChain/Claims/api')).length });
    await c.close(); }
  { const c = await ctx(browser, 'readonly'); const page = await c.newPage(); watch(page, 'readonly'); await ready(page); await page.waitForTimeout(500);
    const dom = { addNew: await page.locator('.add-new').count(), actions: await page.locator('.js-transition').count(), rows: await page.locator('#dt-claims tbody tr').count() };
    const create = await adapter(page, 'POST', API, `{"shipmentId":"${S.DISPATCHED.shipmentId}","reasonCode":"X","claimedAmount":"1.00","currency":"EUR"}`);
    const trans = await adapter(page, 'POST', `${API}/${C.OPEN1.claimId}/transition?shipmentId=${S.SEED.shipmentId}`, `{"targetStatus":"Investigating","occurredAt":"2026-09-26T12:00:00+00:00"}`);
    await shot(page, 'readonly-list-no-cta-en');
    verdict('CU-07-readonly', dom.addNew === 0 && dom.actions === 0 && create.status === 403 && trans.status === 403, { dom, directCreate: create, directTransition: trans });
    await c.close(); }
  const full = await ctx(browser, 'full'); const fp = await full.newPage(); await ready(fp);
  const leb = await ctx(browser, 'leb'); const lp = await leb.newPage(); await lp.goto(PAGE); await lp.locator('#dt-claims').waitFor({ state: 'visible' });
  const t2 = await ctx(browser, 't2user'); const tp = await t2.newPage(); await tp.goto(PAGE); await tp.locator('#dt-claims').waitFor({ state: 'visible' });
  const fullList = await adapter(fp, 'GET', API); const lebList = await adapter(lp, 'GET', API); const t2List = await adapter(tp, 'GET', API);
  const lebFiltered = await adapter(lp, 'GET', `${API}?shipmentId=${S.SEED.shipmentId}`);
  const t1ids = new Set(Object.values(C).filter((x) => x.shipmentId !== S.T2SHIP.shipmentId).map((x) => x.claimId));
  verdict('CU-16-cross-LE-list', lebList.status === 200 && lebList.items.every((i) => !t1ids.has(i.claimId)) && lebFiltered.status === 200 && lebFiltered.items.length === 0,
    { lebListCount: lebList.items?.length, lebFilteredBySeedShipment: lebFiltered.items?.length });
  verdict('isolation-cross-tenant-list', t2List.status === 200 && t2List.items.length === 1 && t2List.items[0].claimId === C.T2CLAIM.claimId && fullList.items.every((i) => i.claimId !== C.T2CLAIM.claimId),
    { t2ListClaimIds: t2List.items?.map((i) => i.claimId), fullListContainsT2Claim: fullList.items?.some((i) => i.claimId === C.T2CLAIM.claimId), fullListCount: fullList.items?.length });
  verdict('soft-deleted-claim-not-listed', fullList.items.every((i) => i.claimId !== C.SOFTDEL1.claimId), { softDeletedClaimId: C.SOFTDEL1.claimId });
  const nf = { unknown: await adapter(fp, 'GET', `${API}/shipments/${unknown}`), foreignTenant: await adapter(fp, 'GET', `${API}/shipments/${S.T2SHIP.shipmentId}`),
    softDeleted: await adapter(fp, 'GET', `${API}/shipments/${S.SOFTDEL.shipmentId}`), foreignLeFromLeb: await adapter(lp, 'GET', `${API}/shipments/${S.DISPATCHED.shipmentId}`),
    foreignTenantFromT2: await adapter(tp, 'GET', `${API}/shipments/${S.DISPATCHED.shipmentId}`) };
  const shape = (x) => JSON.stringify([x.status, x.errorCode, x.errorMessage, x.okKeys]);
  verdict('CU-14-safe-not-found-identical', Object.values(nf).every((x) => x.status === 404 && x.errorCode === 'CLAIM_NOT_FOUND' && x.errorCorrelation === x.responseCorrelation)
    && new Set(Object.values(nf).map(shape)).size === 1, { cases: nf });
  const tb = '{"targetStatus":"Investigating","occurredAt":"2026-09-26T12:00:00+00:00"}';
  const tn = { softDeletedClaim: await adapter(fp, 'POST', `${API}/${C.SOFTDEL1.claimId}/transition?shipmentId=${S.SEED.shipmentId}`, tb),
    foreignTenantClaim: await adapter(fp, 'POST', `${API}/${C.T2CLAIM.claimId}/transition?shipmentId=${S.SEED.shipmentId}`, tb),
    foreignLeClaimFromLeb: await adapter(lp, 'POST', `${API}/${C.OPEN1.claimId}/transition?shipmentId=${S.SEED.shipmentId}`, tb) };
  verdict('CU-15-transition-safe-not-found', Object.values(tn).every((x) => x.status === 404 && x.errorCode === 'CLAIM_NOT_FOUND'), { cases: tn });
  const draft = await adapter(fp, 'POST', API, `{"shipmentId":"${S.DRAFT.shipmentId}","reasonCode":"X","claimedAmount":"10.00","currency":"EUR"}`);
  verdict('CU-17-direct-ineligible-422', draft.status === 422 && draft.errorCode === 'CLAIM_SHIPMENT_INELIGIBLE', { draft });
  const lower = await adapter(fp, 'POST', API, `{"shipmentId":"${S.DISPATCHED.shipmentId}","reasonCode":"X","claimedAmount":"10.00","currency":"eur"}`);
  const zero = await adapter(fp, 'POST', API, `{"shipmentId":"${S.DISPATCHED.shipmentId}","reasonCode":"X","claimedAmount":"0.00","currency":"EUR"}`);
  verdict('CU-11-amount-currency-lexical-http', lower.status === 400 && lower.errorCode === 'INVALID_REQUEST' && zero.status === 422 && zero.errorCode === 'CLAIM_AMOUNT_INVALID', { lowercaseCurrency: lower, zeroAmount: zero });
  const allErr = [...Object.values(nf), ...Object.values(tn), draft, lower, zero];
  verdict('CU-23-support-ref-not-root', allErr.every((x) => x.errorCorrelation && x.errorCorrelation === x.responseCorrelation && !ROOTS.includes(String(x.errorCorrelation).toLowerCase())),
    { pairs: allErr.map((x) => [x.responseCorrelation, x.errorCorrelation]) });
  await full.close(); await leb.close(); await t2.close();
}

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
  verdict('CU-10-carrier-linked', rr.status() === 200 && rb.carrierId === FX.carrier && enabled && cr.status() === 201 && JSON.parse(sent.postData()).carrierId === FX.carrier,
    { run: tag, resolveBody: rb, checkboxEnabled: enabled, sentBody: sent.postData(), createStatus: cr.status(), createErrorCode: body?.error?.code || null, alertText: await page.locator('#formClaimAlert').innerText().catch(() => '') });
  result.cases['CU-10-carrier-linked'].network = net;
  await c.close();
}

// ───────────────────────────── ui-flows (native v2 index.js) ─────────────────────────────
async function uiFlows(browser) {
  const S = FX.shipments;
  // D-02: exactly one tenant shell (one <html>/<body> child layout, one main.js/menu.js), no duplicate-declaration error.
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'd02'); const e0 = jsErrors(); await ready(page);
    const dom = await page.evaluate(() => ({ layoutWrappers: document.querySelectorAll('.layout-wrapper').length, navbars: document.querySelectorAll('nav.layout-navbar, #layout-navbar').length,
      footers: document.querySelectorAll('footer').length, mainJs: [...document.scripts].filter((s) => /\/main\.js/.test(s.src)).length, menuJs: [...document.scripts].filter((s) => /\/menu\.js/.test(s.src)).length,
      htmlInBody: document.body.querySelectorAll('html, head').length }));
    await shot(page, 'd02-single-shell-en');
    verdict('D-02-single-shell', dom.layoutWrappers === 1 && dom.mainJs === 1 && dom.menuJs <= 1 && dom.htmlInBody === 0 && jsErrors() === e0, { dom, newJsErrors: jsErrors() - e0 });
    await c.close(); }
  // D-01 / CU-VS1 browser half: create → reload shows the amount, no xhr.abort TypeError.
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
  // CU-02 filter Apply/Reset
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'filter'); await ready(page); const e0 = jsErrors();
    await page.locator('.dt-filter-btn').click(); await page.locator('#filterShipmentId').fill(S.SEED.shipmentId);
    const rq = page.waitForRequest((r) => r.url().includes('/SupplyChain/Claims/api?'), { timeout: 10000 }).catch(() => null);
    await page.locator('#btnFilterApply').click(); const r = await rq; const keys = r ? [...new URL(r.url()).searchParams.keys()].sort() : null;
    await page.waitForTimeout(400);
    const resetReq = page.waitForRequest((x) => new URL(x.url()).pathname === '/SupplyChain/Claims/api' && x.method() === 'GET', { timeout: 10000 }).catch(() => null);
    await page.locator('#btnFilterReset').click(); const rr = await resetReq;
    verdict('CU-02-filter', !!r && JSON.stringify(keys) === '["shipmentId"]' && !r.headers()['x-tenant-id'] && !!rr && new URL(rr.url()).search === '' && jsErrors() === e0,
      { applyRequestSent: !!r, queryKeys: keys, resetReloaded: !!rr, resetQuery: rr ? new URL(rr.url()).search : null, newJsErrors: jsErrors() - e0 });
    await c.close(); }
  // CU-31 QuickView
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); const net = watch(page, 'quickview'); await ready(page);
    const n0 = net.length; await page.locator('#dt-claims tbody tr .js-quick-view').first().click();
    await page.locator('#offcanvasDetailsPreview').waitFor({ state: 'visible' }); await page.waitForTimeout(500);
    const calls = net.slice(n0).filter((x) => x.path && x.path.startsWith('/SupplyChain/Claims/api'));
    const text = await page.locator('#offcanvasDetailsPreview').innerText(); await shot(page, 'cu31-quickview-en');
    verdict('CU-31-quickview', calls.length === 0 && (await page.locator('#oc-claim-id').innerText()).length > 0 && !/\bEdit\b/.test(text), { adapterCallsDuringQuickView: calls.length });
    await c.close(); }
  // CU-08 Open → Investigating (and approvedAmount never sent on other targets)
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'transition'); await ready(page); const e0 = jsErrors();
    await filterById(page, S.SEED.shipmentId);
    const id = FX.claims.OPEN1.claimId;
    await openAction(page, id, 'Investigating');
    const approvedFieldVisible = await page.locator('#transitionApprovedAmountGroup').isVisible();
    const occurred = await page.locator('#transitionOccurredAt').inputValue();
    const sentP = page.waitForRequest((r) => r.url().includes(`/api/${id}/transition?shipmentId=`));
    await page.locator('#btnSubmitTransition').click();
    const resp = page.waitForResponse((r) => r.url().includes(`/api/${id}/transition?shipmentId=`)); await confirm(page); const tr = await resp; const sent = await sentP;
    const reloaded = await page.waitForResponse(isList, { timeout: 10000 }).then(() => true).catch(() => false);
    await page.waitForTimeout(500); await shot(page, 'cu08-transition-investigating-en');
    const body = JSON.parse(sent.postData() || '{}');
    verdict('CU-08-open-to-investigating', tr.status() === 200 && reloaded && /([+-]\d{2}:\d{2}|Z)$/.test(occurred) && jsErrors() === e0 && !('approvedAmount' in body) && !approvedFieldVisible,
      { claimId: id, status: tr.status(), sentBody: sent.postData(), approvedFieldVisibleForInvestigating: approvedFieldVisible, occurredAtHasOffset: /([+-]\d{2}:\d{2}|Z)$/.test(occurred), reloadAfter: reloaded, newJsErrors: jsErrors() - e0 });
    await c.close(); }
  // CU-21 stale transition (two profiles)
  { const inv = FX.claims.INV1.claimId;
    const a = await ctx(browser, 'full'); const b = await ctx(browser, 'full'); const pa = await a.newPage(); const pb = await b.newPage(); watch(pa, 'stale-a'); watch(pb, 'stale-b');
    await ready(pa); await ready(pb); await filterById(pa, S.SEED.shipmentId); await filterById(pb, S.SEED.shipmentId);
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
  // make the view dirty by sort (order change), then Save → clean
  await page.locator('#dt-claims thead th').nth(4).click(); await page.waitForTimeout(400);
  const s3 = { saveVisible: await visible(), state: await state() };
  const savedResp = page.waitForResponse((r) => new URL(r.url()).pathname.startsWith('/api/personalization/views') && ['POST', 'PUT'].includes(r.request().method()), { timeout: 15000 }).catch(() => null);
  await saveBtn.click(); const sr = await savedResp; await page.waitForTimeout(600);
  const s4 = { saveVisible: await visible(), saveStatus: sr ? sr.status() : null, saveMethod: sr ? sr.request().method() : null, state: await state() };
  await shot(page, 'saveview-3-saved-clean-en');
  // reload: the saved view is applied, button hidden
  await ready(page); await page.waitForTimeout(800);
  const s5 = { saveVisible: await visible(), state: await state() };
  // Reset → factory (order back to base) → dirty against the saved default
  await page.locator('.dt-filter-btn').click();
  const reset2Query = await listQuery(() => page.locator('#btnFilterReset').click());
  const s6 = { saveVisible: await visible(), query: reset2Query, state: await state() };
  // neutralise: save the factory view so later phases start from factory state
  const neutral = page.waitForResponse((r) => new URL(r.url()).pathname.startsWith('/api/personalization/views') && ['POST', 'PUT'].includes(r.request().method()), { timeout: 15000 }).catch(() => null);
  if (s6.saveVisible) await saveBtn.click(); const nr = await neutral; await page.waitForTimeout(500);
  const s7 = { saveVisible: await visible(), neutralSaveStatus: nr ? nr.status() : null };
  const factoryOrder = JSON.stringify(s0.state.order);
  verdict('SAVEVIEW-apply-dirty', s0.saveVisible === false && s1.saveVisible === true && s1.query === `?shipmentId=${S.SEED.shipmentId}`, { s0, s1 });
  verdict('SAVEVIEW-reset-factory', s2.saveVisible === false && s2.query === '' && s2.state.filterShipmentId === '' && JSON.stringify(s2.state.order) === factoryOrder
    && JSON.stringify(s2.state.colVis) === JSON.stringify(s0.state.colVis), { s2, factoryOrder });
  verdict('SAVEVIEW-save-clean', s3.saveVisible === true && [200, 201, 204].includes(s4.saveStatus) && s4.saveVisible === false, { s3, s4 });
  verdict('SAVEVIEW-reload-restores', s5.saveVisible === false && JSON.stringify(s5.state.order) === JSON.stringify(s4.state.order), { s5 });
  verdict('SAVEVIEW-reset-vs-saved-default', s6.saveVisible === true && JSON.stringify(s6.state.order) === factoryOrder && s6.state.filterShipmentId === '' && s7.saveVisible === false,
    { s6, s7, note: 'Reset restores factory state in one step; Save View becomes visible because the saved default differs; the factory view was then saved to leave a neutral default' });
  verdict('SAVEVIEW-no-js-errors', jsErrors() === e0, { newJsErrors: jsErrors() - e0, personalizationCalls: net.filter((x) => x.path && x.path.startsWith('/api/personalization')).map((x) => [x.method, x.status]) });
  await c.close();
}

// ───────────────────────────── crafted responses (browser only, zero write) ─────────────────────────────
async function crafted(browser) {
  const fulfillList = async (c, body, status = 200) => c.route((u) => u.pathname === '/SupplyChain/Claims/api', (route) => route.request().method() === 'GET'
    ? route.fulfill({ status, contentType: 'application/json', headers: { 'X-Correlation-Id': '11111111-2222-4333-8444-555555555555' }, body: typeof body === 'string' ? body : JSON.stringify(body) })
    : route.continue());
  const base = { claimId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee', claimNumber: 'CLM-CRAFTED-1', shipmentId: FX.shipments.SEED.shipmentId, status: 'Open', claimedAmount: '10.00', currency: 'EUR' };
  const long80 = '9'.repeat(77) + '.99';
  const show = async (c, name) => { const page = await c.newPage(); watch(page, name); await page.goto(PAGE); await page.waitForTimeout(1500); return page; };
  // CU-03 absent fields / no claimId / malformed envelope
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
  // CU-04 exact amount text
  { const c = await ctx(browser, 'full'); await fulfillList(c, { items: [{ ...base, claimNumber: 'CLM-LEAD', claimedAmount: '000250.00' }, { ...base, claimId: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeee2', claimNumber: 'CLM-LONG', claimedAmount: long80 }] });
    const page = await show(c, 'cu04');
    const cells = await page.$$eval('#dt-claims tbody tr', (trs) => trs.map((tr) => { const td = tr.querySelectorAll('td')[4]; const b = td?.querySelector('bdi'); return { text: td?.innerText.trim(), bdiDir: b?.getAttribute('dir') || null }; }));
    await shot(page, 'cu04-exact-amount-en'); await c.close();
    const texts = cells.map((x) => x.text);
    verdict('CU-04-exact-amount', texts.includes('000250.00') && texts.includes(long80) && cells.every((x) => x.bdiDir === 'ltr'), { cells: cells.map((x) => ({ ...x, text: x.text?.length > 40 ? `${x.text.slice(0, 12)}…(${x.text.length} chars)` : x.text })), long80Length: long80.length }); }
  // CU-05 skeleton / empty / error distinct
  { const c = await ctx(browser, 'full'); await fulfillList(c, { items: [] }); const page = await show(c, 'cu05a');
    const empty = { tableVisible: await page.locator('#claims-table-host').isVisible(), errorVisible: await page.locator('#claims-error-state').isVisible(), skeletonVisible: await page.locator('#skeleton-loader').isVisible(), text: (await page.locator('#dt-claims tbody').innerText()).trim() };
    await shot(page, 'cu05-empty-en'); await c.close();
    const c2 = await ctx(browser, 'full'); await fulfillList(c2, { error: { code: 'CLAIM_STORAGE_UNAVAILABLE', message: 'x', correlationId: '11111111-2222-4333-8444-555555555555' } }, 503);
    const p2 = await show(c2, 'cu05b');
    const err = { tableVisible: await p2.locator('#claims-table-host').isVisible(), errorVisible: await p2.locator('#claims-error-state').isVisible(), skeletonVisible: await p2.locator('#skeleton-loader').isVisible(),
      message: await p2.locator('#claims-error-message').innerText(), reference: await p2.locator('#claims-error-reference-value').innerText().catch(() => '') , retry: await p2.locator('#btnClaimsRetry').isVisible() };
    await shot(p2, 'cu05-error-503-en'); await c2.close();
    const c3 = await ctx(browser, 'full'); await c3.route((u) => u.pathname === '/SupplyChain/Claims/api', async (route) => { await new Promise((r) => setTimeout(r, 2500)); await route.continue(); });
    const p3 = await c3.newPage(); watch(p3, 'cu05c'); await p3.goto(PAGE); await p3.waitForTimeout(700);
    const sk = { skeletonVisibleWhileLoading: await p3.locator('#skeleton-loader').isVisible(), tableVisibleWhileLoading: await p3.locator('#claims-table-host').isVisible() };
    await shot(p3, 'cu05-skeleton-en'); await p3.waitForTimeout(3000); sk.skeletonHiddenAfter = !(await p3.locator('#skeleton-loader').isVisible()); await c3.close();
    verdict('CU-05-skeleton-empty-error', empty.tableVisible && !empty.errorVisible && !empty.skeletonVisible && empty.text === 'No claims match the current filter.'
      && err.errorVisible && !err.tableVisible && !err.skeletonVisible && err.message.length > 0 && err.retry && sk.skeletonVisibleWhileLoading && !sk.tableVisibleWhileLoading && sk.skeletonHiddenAfter,
      { empty, error503: err, skeleton: sk }); }
  // CU-13 resolve stale-response guard: first resolve delayed, second resolve wins; the late first response never populates.
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
  // transition with empty optional texts (HTTP through the adapter)
  const t = await adapter(page, 'POST', `${API}/${cb.claimId}/transition?shipmentId=${S.DISPATCHED.shipmentId}`, `{"targetStatus":"Investigating","occurredAt":"2026-09-27T01:00:00+00:00","resolutionCode":"","note":""}`);
  result.cu12 = { claimId: cb.claimId };
  verdict('CU-12-presence-not-nonempty', cr.status() === 201 && body.reasonCode === '' && Array.isArray(body.evidenceReferenceIds) && body.evidenceReferenceIds.length === 1 && body.evidenceReferenceIds[0] === ''
    && attrs.every((a) => !a.required && (a.maxLength === -1 || a.maxLength > 100000) && !a.pattern) && t.status === 200,
    { sentBody: sent.postData(), createStatus: cr.status(), claimId: cb.claimId, formAttributes: attrs, emptyResolutionNoteTransition: t, dbCheck: 'see raw/db/cu12-exact-empty.json' });
  await c.close();
}

// ───────────────────────────── CU-18 root seam ─────────────────────────────
async function cu18(browser) {
  const S = FX.shipments; const c = await ctx(browser, 'full'); const page = await c.newPage(); const net = watch(page, 'cu18'); await ready(page);
  const ui = async (ship, tag) => { await page.locator('.add-new').click(); await page.locator('#claimShipmentId').fill(ship);
    const rr = page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')); await page.locator('#btnResolveShipment').click(); const r = await rr;
    const body = await r.json().catch(() => null); await page.waitForTimeout(500);
    const alert = await page.locator('#formClaimAlert').innerText().catch(() => ''); await shot(page, `cu18-${tag}-en`);
    await page.keyboard.press('Escape'); await page.waitForTimeout(400); return { resolveStatus: r.status(), errorCode: body?.error?.code || null, alert }; };
  const nullUi = await ui(S.NULLROOT.shipmentId, 'null-root'); const badUi = await ui(S.BADROOT.shipmentId, 'malformed-root');
  const nullHttp = await adapter(page, 'POST', API, `{"shipmentId":"${S.NULLROOT.shipmentId}","reasonCode":"X","claimedAmount":"5.00","currency":"EUR"}`);
  const badHttp = await adapter(page, 'POST', API, `{"shipmentId":"${S.BADROOT.shipmentId}","reasonCode":"X","claimedAmount":"5.00","currency":"EUR"}`);
  verdict('CU-18-root-seam', nullHttp.status === 503 && nullHttp.errorCode === 'CLAIM_REFERENCE_INCOMPLETE' && badHttp.status === 502 && badHttp.errorCode === 'CLAIM_REFERENCE_INVALID'
    && nullUi.alert.includes('incomplete') && badUi.alert.includes('invalid') && result.rootLeak.length === 0,
    { nullRootUi: nullUi, malformedRootUi: badUi, nullRootCreate: nullHttp, malformedRootCreate: badHttp, rootLeak: result.rootLeak.length });
  await c.close();
}

// ───────────────────────────── CU-19 idempotency ─────────────────────────────
async function cu19(browser) {
  const S = FX.shipments; const c = await ctx(browser, 'full'); const page = await c.newPage(); const net = watch(page, 'cu19'); await ready(page);
  // (a) HTTP: same key + identical text → replay; same key + "250" vs "250.00" → 409
  const key = crypto.randomUUID(); const body = `{"shipmentId":"${S.DISPATCHED.shipmentId}","reasonCode":"IDEM","claimedAmount":"250.00","currency":"EUR"}`;
  const first = await adapter(page, 'POST', API, body, { 'Idempotency-Key': key }); const replay = await adapter(page, 'POST', API, body, { 'Idempotency-Key': key });
  const changed = await adapter(page, 'POST', API, body.replace('"250.00"', '"250"'), { 'Idempotency-Key': key });
  verdict('CU-19a-http-replay-and-409', first.status === 201 && [200, 201].includes(replay.status) && replay.claimId === first.claimId && changed.status === 409 && changed.errorCode === 'IDEMPOTENCY_KEY_REUSED',
    { first, replay, changedBodySameKey: changed });
  // (b) browser: the create response is lost after the server processed it (evidence-only fault proxy: route.fetch then abort);
  //     the retry re-sends the same key with identical body text → replay → completed + reload.
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
  await page.unroute('**/*').catch(() => null); await page.unrouteAll({ behavior: 'ignoreErrors' }).catch(() => null);
  // (c) browser: a retry whose body was changed in transit (same key) → 409; the UI blocks the intent (no further request).
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
  // (d) transition retry keeps the exact occurredAt text and key
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

// ───────────────────────────── CU-20 approval amount ─────────────────────────────
async function cu20(browser) {
  const S = FX.shipments, C = FX.claims; const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'cu20'); await ready(page);
  const tr = (id, extra) => adapter(page, 'POST', `${API}/${id}/transition?shipmentId=${S.FLOW.shipmentId}`, JSON.stringify({ targetStatus: 'Approved', occurredAt: '2026-09-27T01:10:00+00:00', ...extra }));
  const bad = { missing: await tr(C.APPRBAD.claimId, {}), minus001: await tr(C.APPRBAD.claimId, { approvedAmount: '-0.01' }), over: await tr(C.APPRBAD.claimId, { approvedAmount: '250.01' }) };
  const good = { zero: await tr(C.APPR0.claimId, { approvedAmount: '0' }), minusZero: await tr(C.APPRNEG0.claimId, { approvedAmount: '-0' }) };
  const notAllowed = await adapter(page, 'POST', `${API}/${C.APPRBAD.claimId}/transition?shipmentId=${S.FLOW.shipmentId}`, '{"targetStatus":"Rejected","occurredAt":"2026-09-27T01:10:00+00:00","approvedAmount":"1.00"}');
  // UI: field only on Approved; 250 → 200; success message carries the response approvedAmount
  await filterById(page, S.FLOW.shipmentId);
  const id = C.APPRBAD.claimId; // still Investigating after the three 422s and the 422 not-allowed
  await openAction(page, id, 'Rejected'); const fieldOnReject = await page.locator('#transitionApprovedAmountGroup').isVisible(); await page.keyboard.press('Escape'); await page.waitForTimeout(400);
  await openAction(page, id, 'Approved'); const fieldOnApprove = await page.locator('#transitionApprovedAmountGroup').isVisible();
  await page.locator('#transitionApprovedAmount').fill('250'); await page.locator('#btnSubmitTransition').click();
  const sentP = page.waitForRequest((r) => r.url().includes(`/api/${id}/transition`)); const resp = page.waitForResponse((r) => r.url().includes(`/api/${id}/transition`));
  await confirm(page); const r = await resp; const sent = await sentP; const b = await r.json().catch(() => ({})); await page.waitForTimeout(800);
  const msg = result.toasts.filter((x) => x.tag === 'cu20').map((x) => x.text).join(' | ');
  await shot(page, 'cu20-approve-250-success-en');
  verdict('CU-20-approval-amount', Object.values(bad).every((x) => x.status === 422 && x.errorCode === 'CLAIM_APPROVAL_AMOUNT_INVALID') && Object.values(good).every((x) => x.status === 200)
    && notAllowed.status === 422 && notAllowed.errorCode === 'CLAIM_APPROVED_AMOUNT_NOT_ALLOWED' && !fieldOnReject && fieldOnApprove && r.status() === 200 && JSON.parse(sent.postData()).approvedAmount === '250'
    && msg.includes(`Approved amount: ${b.approvedAmount}`),
    { rejected422: bad, accepted200: good, approvedAmountOnReject: notAllowed, fieldVisibleOnReject: fieldOnReject, fieldVisibleOnApprove: fieldOnApprove, uiApprove: { status: r.status(), sentBody: sent.postData(), responseApprovedAmount: b.approvedAmount }, successMessage: msg });
  await c.close();
}

// cu20b (added after cu20-a1 stopped on a harness step: Escape did not close the transition offcanvas, so the next row
// click was intercepted; APPR0/APPRNEG0 had already returned 200 — proven by K10 pair cu20 (+2) and raw/db/cu20-approved-a1.json).
// Re-runs the zero-write 422 cases, records the Escape behaviour of the transition surface, closes it with its close button,
// and does the UI approval on APPRBAD.
async function cu20b(browser) {
  const S = FX.shipments, C = FX.claims; const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'cu20b'); await ready(page);
  const tr = (id, extra) => adapter(page, 'POST', `${API}/${id}/transition?shipmentId=${S.FLOW.shipmentId}`, JSON.stringify({ targetStatus: 'Approved', occurredAt: '2026-09-27T01:10:00+00:00', ...extra }));
  const bad = { missing: await tr(C.APPRBAD.claimId, {}), minus001: await tr(C.APPRBAD.claimId, { approvedAmount: '-0.01' }), over: await tr(C.APPRBAD.claimId, { approvedAmount: '250.01' }) };
  const notAllowed = await adapter(page, 'POST', `${API}/${C.APPRBAD.claimId}/transition?shipmentId=${S.FLOW.shipmentId}`, '{"targetStatus":"Rejected","occurredAt":"2026-09-27T01:10:00+00:00","approvedAmount":"1.00"}');
  await filterById(page, S.FLOW.shipmentId);
  const id = C.APPRBAD.claimId;
  await openAction(page, id, 'Rejected'); const fieldOnReject = await page.locator('#transitionApprovedAmountGroup').isVisible();
  const focusInside = await page.evaluate(() => !!document.activeElement?.closest('#offcanvasClaimTransition'));
  await page.keyboard.press('Escape'); await page.waitForTimeout(700);
  const closedByEscape = !(await page.locator('#offcanvasClaimTransition').isVisible());
  if (!closedByEscape) { await page.locator('#offcanvasClaimTransition .btn-close').click(); await page.locator('#offcanvasClaimTransition').waitFor({ state: 'hidden' }); }
  await shot(page, 'cu20b-after-close-en');
  await openAction(page, id, 'Approved'); const fieldOnApprove = await page.locator('#transitionApprovedAmountGroup').isVisible();
  await page.locator('#transitionApprovedAmount').fill('250'); await page.locator('#btnSubmitTransition').click();
  const sentP = page.waitForRequest((r) => r.url().includes(`/api/${id}/transition`)); const resp = page.waitForResponse((r) => r.url().includes(`/api/${id}/transition`));
  await confirm(page); const r = await resp; const sent = await sentP; const b = await r.json().catch(() => ({})); await page.waitForTimeout(800);
  const msg = result.toasts.filter((x) => x.tag === 'cu20b').map((x) => x.text).join(' | ');
  await shot(page, 'cu20-approve-250-success-en');
  verdict('CU-20-approval-amount', Object.values(bad).every((x) => x.status === 422 && x.errorCode === 'CLAIM_APPROVAL_AMOUNT_INVALID')
    && notAllowed.status === 422 && notAllowed.errorCode === 'CLAIM_APPROVED_AMOUNT_NOT_ALLOWED' && !fieldOnReject && fieldOnApprove && r.status() === 200 && JSON.parse(sent.postData()).approvedAmount === '250'
    && msg.includes(`Approved amount: ${b.approvedAmount}`),
    { rejected422: bad, approvedAmountOnReject: notAllowed, fieldVisibleOnReject: fieldOnReject, fieldVisibleOnApprove: fieldOnApprove,
      uiApprove: { status: r.status(), sentBody: sent.postData(), responseApprovedAmount: b.approvedAmount }, successMessage: msg,
      accepted200: 'APPR0 "0" and APPRNEG0 "-0" → 200 in cu20-a1 (K10 +2; raw/db/cu20-approved-a1.json)' });
  verdict('CU-25c-transition-escape', closedByEscape, { focusInsideTransitionSurfaceAfterOpen: focusInside, closedByEscape,
    note: 'transition offcanvas opened from a row dropdown item; Escape pressed once' });
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

// ───────────────────────────── CU-24 / CU-25 languages, RTL, responsive, keyboard ─────────────────────────────
async function l10n(browser) {
  const langs = ['en', 'tr', 'fr', 'es', 'zh', 'ar', 'ru']; const out = {};
  const enTexts = {};
  for (const lang of langs) {
    const c = await ctx(browser, 'full', { locale: lang, viewport: { width: 1280, height: 900 } }); const page = await c.newPage(); watch(page, `l10n-${lang}`);
    await ready(page, `${PAGE}?culture=${lang}&ui-culture=${lang}`); await page.waitForTimeout(500);
    const info = await page.evaluate(() => ({ dir: document.documentElement.getAttribute('dir'), lang: document.documentElement.getAttribute('lang'),
      title: document.querySelector('h5')?.innerText || '', description: document.querySelector('h5 + p')?.innerText || '', headers: [...document.querySelectorAll('#dt-claims thead th')].map((t) => t.innerText.trim()).filter(Boolean),
      emptyOrFirstStatus: document.querySelector('#dt-claims tbody tr td:nth-child(4)')?.innerText || '' }));
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
  const english = (x) => [x.title, x.description, ...x.headers].filter((t) => [enTexts.title, enTexts.description, ...enTexts.headers].includes(t) && /[A-Za-z]{3,}/.test(t));
  const bad = langs.filter((l) => l !== 'en').filter((l) => english(out[l]).length > 0);
  verdict('CU-24-seven-languages-rtl', out.ar.dir === 'rtl' && bad.length === 0 && langs.every((l) => out[l].title.length > 0),
    { perLanguage: Object.fromEntries(langs.map((l) => [l, { dir: out[l].dir, lang: out[l].lang, title: out[l].title, headers: out[l].headers, englishLeft: english(out[l]) }])) });
  verdict('CU-25a-responsive-no-overflow', langs.every((l) => Object.values(out[l].horizontalOverflow).every((v) => v === false)), { overflow: Object.fromEntries(langs.map((l) => [l, out[l].horizontalOverflow])) });
  // keyboard: Tab reaches Add, Enter opens create, focus inside, Escape closes, focus returns; evidence editor ids unique
  const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'keyboard'); await ready(page);
  await page.locator('.add-new').focus(); await page.keyboard.press('Enter'); await page.locator('#offcanvasCreateEdit').waitFor({ state: 'visible' }); await page.waitForTimeout(500);
  const focusInside = await page.evaluate(() => !!document.activeElement && !!document.activeElement.closest('#offcanvasCreateEdit'));
  await page.locator('#btnAddEvidence').click(); await page.locator('#btnAddEvidence').click();
  const ids = await page.$$eval('#claimEvidenceList [id]', (els) => els.map((e) => e.id)); const labelsOk = await page.$$eval('#claimEvidenceList input', (ins) => ins.every((i) => !!i.id && !!document.querySelector(`label[for="${i.id}"]`) || !!i.getAttribute('aria-label')));
  await shot(page, 'cu25-keyboard-create-open-en');
  await page.keyboard.press('Escape'); await page.waitForTimeout(600);
  const closed = !(await page.locator('#offcanvasCreateEdit').isVisible()); const inert = await page.locator('#offcanvasCreateEdit').evaluate((e) => e.inert === true);
  const focusBack = await page.evaluate(() => document.activeElement?.classList.contains('add-new') || !!document.activeElement?.closest('.add-new'));
  verdict('CU-25b-keyboard', focusInside && closed && inert && new Set(ids).size === ids.length && labelsOk, { focusInsideAfterOpen: focusInside, closedByEscape: closed, inertAfterClose: inert, focusReturnedToAdd: focusBack, evidenceIds: ids, evidenceInputsLabelled: labelsOk });
  await c.close();
}

// kbd (added after l10n-a1 stopped: Enter on the focused Add button did not open the create offcanvas within 30 s).
// Records Enter and Space on the Add button, Escape on the create offcanvas opened by click, and evidence-editor ids.
async function kbd(browser) {
  const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'kbd'); await ready(page);
  const open = () => page.locator('#offcanvasCreateEdit').isVisible();
  const addInfo = await page.locator('.add-new').first().evaluate((e) => ({ tag: e.tagName, type: e.getAttribute('type'), tabindex: e.getAttribute('tabindex'), role: e.getAttribute('role'), classes: e.className }));
  await page.locator('.add-new').first().focus(); const focused = await page.evaluate(() => !!document.activeElement?.closest('.add-new'));
  await page.keyboard.press('Enter'); await page.waitForTimeout(1200); const byEnter = await open();
  let bySpace = null;
  if (!byEnter) { await page.locator('.add-new').first().focus(); await page.keyboard.press('Space'); await page.waitForTimeout(1200); bySpace = await open(); }
  if (!(byEnter || bySpace)) { await page.locator('.add-new').first().click(); await page.locator('#offcanvasCreateEdit').waitFor({ state: 'visible' }); await page.waitForTimeout(500); }
  const focusInside = await page.evaluate(() => !!document.activeElement?.closest('#offcanvasCreateEdit'));
  await page.locator('#btnAddEvidence').click(); await page.locator('#btnAddEvidence').click();
  const ids = await page.$$eval('#claimEvidenceList [id]', (els) => els.map((e) => e.id));
  const labelled = await page.$$eval('#claimEvidenceList input', (ins) => ins.map((i) => !!(i.id && document.querySelector(`label[for="${i.id}"]`)) || !!i.getAttribute('aria-label')));
  await shot(page, 'cu25-keyboard-create-open-en');
  await page.locator('#claimShipmentId').focus(); await page.keyboard.press('Escape'); await page.waitForTimeout(800);
  const closedByEscape = !(await open()); const inert = await page.locator('#offcanvasCreateEdit').evaluate((e) => e.inert === true);
  const focusBack = await page.evaluate(() => !!document.activeElement?.closest('.add-new'));
  verdict('CU-25b-keyboard-create', (byEnter || bySpace === true) && closedByEscape && inert && new Set(ids).size === ids.length && labelled.every(Boolean),
    { addButton: addInfo, addFocusable: focused, openedByEnter: byEnter, openedBySpace: bySpace, focusInsideAfterOpen: focusInside, closedByEscapeFromField: closedByEscape,
      inertAfterClose: inert, focusReturnedToAdd: focusBack, evidenceIds: ids, evidenceInputsLabelled: labelled });
  await c.close();
}

// ───────────────────────────── CU-26 premium dialogs, no inline handlers, no token in storage ─────────────────────────────
async function cu26(browser) {
  const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'cu26'); await ready(page);
  await page.locator('.add-new').click(); await page.waitForTimeout(400); await page.keyboard.press('Escape'); await page.waitForTimeout(400);
  const inline = await page.evaluate(() => { const hits = []; for (const el of document.querySelectorAll('#dt-claims, #offcanvasCreateEdit, #offcanvasDetailsPreview, #offcanvasClaimTransition, #inlineFilterHost, #claims-error-state'))
    for (const n of [el, ...el.querySelectorAll('*')]) for (const a of n.getAttributeNames()) if (/^on[a-z]+$/.test(a)) hits.push(`${n.tagName}.${a}`); return hits; });
  const storage = await page.evaluate(() => { const all = []; for (const s of [localStorage, sessionStorage]) for (let i = 0; i < s.length; i++) { const k = s.key(i); all.push({ k, v: s.getItem(k) || '' }); }
    return { keys: all.map((x) => x.k), jwtLike: all.filter((x) => /eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}/.test(x.v) || /bearer/i.test(x.v)).map((x) => x.k) }; });
  const cookies = (await c.cookies(WEB)).map((k) => ({ name: k.name, httpOnly: k.httpOnly }));
  const jsCookie = await page.evaluate(() => document.cookie.split(';').map((x) => x.trim().split('=')[0]).filter(Boolean));
  verdict('CU-26-premium-no-secrets', inline.length === 0 && storage.jwtLike.length === 0 && result.dialogs.length === 0,
    { inlineHandlersInModuleDom: inline, storageKeys: storage.keys, jwtLikeStorageKeys: storage.jwtLike, cookies, cookiesReadableByScript: jsCookie, nativeDialogsThisPhase: result.dialogs.length,
      note: 'native dialogs are also counted in every other phase file (dialogs[])' });
  await c.close();
}

const PH = { login, vs1, 'checks-neg': checksNeg, 'checks-mut': checksMut, 'ui-flows': uiFlows, saveview, crafted, cu12, cu18, cu19, cu20, cu20b, cu22, l10n, kbd, cu26 };
const browser = PHASE === 'spec' ? null : await chromium.launch({ headless: true });
try {
  if (PHASE === 'spec') spec();
  else if (PH[PHASE]) await PH[PHASE](browser);
  else throw new Error('unknown phase');
} catch (e) { result.error = String(e && e.message || e).slice(0, 1500); }
finally { if (browser) await browser.close(); save(); }
process.exit(result.error || Object.values(result.cases).some((c) => c.result === 'FAIL') || result.rootLeak.length || result.dialogs.length ? 1 : 0);
