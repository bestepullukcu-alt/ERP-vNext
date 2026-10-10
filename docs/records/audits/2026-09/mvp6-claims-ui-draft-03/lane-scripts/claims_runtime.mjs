// Q64e browser harness — MOD-0187 Claims UI DRAFT v3 on the Q103 accepted base, kit slot 8. Derived from the Q64d harness.
// Scope (WP Q64e): re-run ONLY CU-05 and CU-25, plus a D1/D2/D4 regression pass. Run ONLY by the lane supervisor (task
// harness): it supplies ACTOR_PW_<LABEL>; this script never prints or writes them. Never deletes a file.
// Usage: node claims_runtime.mjs <phase> <webPort> <evidenceDir> <fixtureJson> <stateDir> <runtimeDir> [mode] [v2File] [v2Sha]
//   login       real Web login per actor; storage state ONLY in <stateDir> (outside the repo, 0600).
//   cu05        skeleton (delayed first load) / empty / 503 error states.            mode v3 (native) | v2 (served v2 index.js)
//   cu25        responsive (v3 only) + keyboard: create (Enter/Space, Escape, focus return) and transition surface
//               (focus inside + Escape, immediately and after the slide-in).          mode v3 | v2
//   regression  v3 only: single shell (D-02), create → reload (D-01), Open → Investigating through the transition surface,
//               filter Apply/Reset + Save View visibility, QuickView — 0 JS errors, 0 native dialogs, 0 root leaks.
// Mode v2 serves the v2 index.js through the kit's fulfillHashed (served bytes hashed; K09 binds them to the pre-serve
// record) so the v2 → v3 difference is measured in the same lane, on the same data, with the same browser.
import fs from 'node:fs'; import path from 'node:path'; import crypto from 'node:crypto';
import { createRequire } from 'node:module';

const [PHASE, WEBPORT, EVD, FXJSON, STATE, RUNTIME, MODE = 'v3', V2FILE, V2SHA] = process.argv.slice(2);
const req = createRequire(path.join(RUNTIME, 'package.json'));
const { chromium } = req('playwright');
const PW_VERSION = req('playwright/package.json').version;
const { fulfillHashed } = req('/Users/natig/Projects/ERP-vNext-recovery/scripts/evidence-kit/templates/served-asset-hash.js');
const WEB = `http://127.0.0.1:${WEBPORT}`; const PAGE = `${WEB}/SupplyChain/Claims`; const API = `${PAGE}/api`;
const T1 = '97c59330-dbc4-4665-b29c-0c26dbb5cc93', T2 = '00000000-0000-0000-0000-000000000001';
const ACT = { full: ['john.doe.t97@diten.com', T1], readonly: ['jane.smith.t97@diten.com', T1], noread: ['charlie.brown.t97@diten.com', T1],
  leb: ['bob.johnson.t97@diten.com', T1], t2user: ['john.doe.def@diten.com', T2] };
const OUTD = path.join(EVD, 'browser'); const PNG = path.join(EVD, 'png');
[OUTD, PNG].forEach((d) => fs.mkdirSync(d, { recursive: true }));
const FX = JSON.parse(fs.readFileSync(FXJSON, 'utf8')).fixtures;
const ROOTS = Object.values(FX.shipments).map((s) => s.lifecycleCorrelationId).filter(Boolean).map((r) => r.toLowerCase());
const TAG = PHASE === 'login' || PHASE === 'regression' ? PHASE : `${PHASE}-${MODE}`;
const uniq = (base) => { let n = 1; while (fs.existsSync(path.join(OUTD, `${base}-a${n}.json`))) n++; return path.join(OUTD, `${base}-a${n}.json`); };
const result = { phase: PHASE, mode: MODE, startedAt: new Date().toISOString(), web: WEB, playwright: PW_VERSION, cases: {}, png: [], console: [], toasts: [], dialogs: [], rootLeak: [] };
const sha = (b) => crypto.createHash('sha256').update(b).digest('hex');
const statePath = (a) => path.join(STATE, `${a}.json`);
const iso = () => new Date().toISOString();
const verdict = (name, ok, detail) => { result.cases[name] = { result: ok ? 'PASS' : 'FAIL', ...detail }; };

async function shot(page, name) {
  let n = name; let i = 2; while (fs.existsSync(path.join(PNG, `${n}.png`))) n = `${name}-${i++}`;
  const p = path.join(PNG, `${n}.png`); await page.screenshot({ path: p, fullPage: true });
  result.png.push({ name: `${n}.png`, sha256: sha(fs.readFileSync(p)), url: page.url(), capturedAt: iso(), phase: PHASE, mode: MODE, tool: `playwright ${PW_VERSION} page.screenshot` });
}
function watch(page, tag) {
  page.on('dialog', async (d) => { result.dialogs.push({ tag, type: d.type(), at: iso() }); await d.dismiss().catch(() => null); });
  page.on('console', (m) => { const text = m.text(); const toast = /\[MOD-0013 showToast\] Triggered - Key: (.*) \| Type: (\w+)/.exec(text);
    if (toast) result.toasts.push({ tag, text: toast[1].slice(0, 300), type: toast[2] });
    if (['error', 'warning'].includes(m.type())) result.console.push({ tag, type: m.type(), text: text.slice(0, 300) }); });
  page.on('pageerror', (e) => result.console.push({ tag, type: 'pageerror', text: String(e.message || e).slice(0, 300) }));
  page.on('response', async (r) => { const u = new URL(r.url()); if (!u.pathname.startsWith('/SupplyChain/Claims')) return;
    let text = ''; try { text = await r.text(); } catch (_) { }
    const blob = (text + JSON.stringify(r.headers())).toLowerCase(); for (const root of ROOTS) if (blob.includes(root)) result.rootLeak.push({ tag, path: u.pathname, root }); });
}
async function ctx(browser, actor, opts = {}) {
  const c = await browser.newContext({ storageState: actor ? statePath(actor) : undefined, timezoneId: 'UTC', locale: opts.locale || 'en-US', viewport: opts.viewport || { width: 1366, height: 900 } });
  if (MODE === 'v2' && PHASE !== 'login') await c.route('**/assets/js/SupplyChain/Claims/index.js*', (route) => fulfillHashed(route, V2FILE, { evidence: EVD, label: 'q64e-v2-index-js-baseline', expect: V2SHA }));
  return c;
}
const isList = (r) => new URL(r.url()).pathname === '/SupplyChain/Claims/api' && r.request().method() === 'GET';
const ready = async (page, url = PAGE) => { await page.goto(url); await page.locator('#dt-claims').waitFor({ state: 'visible' });
  await page.locator('#dt-claims tbody tr').first().waitFor(); await page.waitForTimeout(400); };
const rowOf = (page, id) => page.locator(`#dt-claims tbody tr:has(.js-quick-view[data-claim-id="${id}"])`);
const jsErrors = () => result.console.filter((m) => m.type === 'pageerror' || /xhr\.abort|TypeError|already been declared/.test(m.text)).length;
const visibleBox = (loc) => loc.evaluate((e) => { const s = getComputedStyle(e); const r = e.getBoundingClientRect(); return s.display !== 'none' && s.visibility !== 'hidden' && r.height > 0; }).catch(() => false);
const focusIn = (page, sel) => page.evaluate((s) => !!document.activeElement?.closest(s), sel);
const focusId = (page) => page.evaluate(() => document.activeElement?.id || document.activeElement?.className || document.activeElement?.tagName || null);
const filterById = async (page, shipmentId) => { await page.locator('.dt-filter-btn').click(); await page.locator('#filterShipmentId').fill(shipmentId);
  await Promise.all([page.waitForResponse(isList), page.locator('#btnFilterApply').click()]); await page.waitForTimeout(400); };

async function login(browser) {
  fs.mkdirSync(STATE, { recursive: true, mode: 0o700 });
  for (const [actor, [email, tenant]] of Object.entries(ACT)) {
    const c = await browser.newContext({ timezoneId: 'UTC' }); const page = await c.newPage(); watch(page, `login-${actor}`);
    await page.goto(`${WEB}/account/login?tenantId=${tenant}&returnUrl=%2FSupplyChain%2FClaims`);
    await page.fill('#email', email); await page.fill('#password', process.env[`ACTOR_PW_${actor.toUpperCase()}`]);
    await Promise.all([page.waitForURL((u) => !u.pathname.startsWith('/account/login'), { timeout: 45000 }).catch(() => null), page.click('#loginForm button[type="submit"]')]);
    await page.waitForLoadState('networkidle').catch(() => null);
    const landed = new URL(page.url()).pathname;
    await c.storageState({ path: statePath(actor) }); fs.chmodSync(statePath(actor), 0o600);
    verdict(`login-${actor}`, !landed.startsWith('/account/login'), { landedOn: landed, cookies: (await c.cookies(WEB)).map((k) => ({ name: k.name, httpOnly: k.httpOnly })) });
    await c.close();
  }
}

// CU-05: skeleton (first load delayed 2.5 s) / empty ([]) / error (503) are three distinct, visible states.
async function cu05(browser) {
  const fulfillList = (c, body, status) => c.route((u) => u.pathname === '/SupplyChain/Claims/api', (route) => route.request().method() === 'GET'
    ? route.fulfill({ status, contentType: 'application/json', headers: { 'X-Correlation-Id': '11111111-2222-4333-8444-555555555555' }, body: JSON.stringify(body) }) : route.continue());
  // skeleton
  const c1 = await ctx(browser, 'full');
  await c1.route((u) => u.pathname === '/SupplyChain/Claims/api', async (route) => { await new Promise((r) => setTimeout(r, 2500)); await route.continue(); });
  const p1 = await c1.newPage(); watch(p1, 'cu05-skeleton'); await p1.goto(PAGE); await p1.waitForTimeout(700);
  const during = { skeletonVisible: await visibleBox(p1.locator('#skeleton-loader')), tableVisible: await visibleBox(p1.locator('#claims-table-host')),
    errorVisible: await visibleBox(p1.locator('#claims-error-state')), skeletonDisplay: await p1.locator('#skeleton-loader').evaluate((e) => getComputedStyle(e).display) };
  await shot(p1, `cu05-skeleton-${MODE}`); await p1.waitForTimeout(3200);
  const after = { skeletonVisible: await visibleBox(p1.locator('#skeleton-loader')), tableVisible: await visibleBox(p1.locator('#claims-table-host')) };
  await shot(p1, `cu05-after-load-${MODE}`); await c1.close();
  // empty
  const c2 = await ctx(browser, 'full'); await fulfillList(c2, { items: [] }, 200); const p2 = await c2.newPage(); watch(p2, 'cu05-empty'); await p2.goto(PAGE); await p2.waitForTimeout(1500);
  const empty = { tableVisible: await visibleBox(p2.locator('#claims-table-host')), skeletonVisible: await visibleBox(p2.locator('#skeleton-loader')), errorVisible: await visibleBox(p2.locator('#claims-error-state')),
    text: (await p2.locator('#dt-claims tbody').innerText()).trim() };
  await shot(p2, `cu05-empty-${MODE}`); await c2.close();
  // error 503
  const c3 = await ctx(browser, 'full'); await fulfillList(c3, { error: { code: 'CLAIM_STORAGE_UNAVAILABLE', message: 'x', correlationId: '11111111-2222-4333-8444-555555555555' } }, 503);
  const p3 = await c3.newPage(); watch(p3, 'cu05-error'); await p3.goto(PAGE); await p3.waitForTimeout(1500);
  const error = { errorVisible: await visibleBox(p3.locator('#claims-error-state')), tableVisible: await visibleBox(p3.locator('#claims-table-host')), skeletonVisible: await visibleBox(p3.locator('#skeleton-loader')),
    message: await p3.locator('#claims-error-message').innerText(), reference: await p3.locator('#claims-error-reference-value').innerText().catch(() => ''), retryVisible: await visibleBox(p3.locator('#btnClaimsRetry')) };
  await shot(p3, `cu05-error-503-${MODE}`); await c3.close();
  const sharedEmpty = empty.text.length > 0; // shared localized DtEmptyTable (Q64a F13) — by design
  verdict('CU-05-skeleton-empty-error', during.skeletonVisible && !during.tableVisible && !during.errorVisible && !after.skeletonVisible && after.tableVisible
    && empty.tableVisible && !empty.skeletonVisible && !empty.errorVisible && sharedEmpty
    && error.errorVisible && !error.tableVisible && !error.skeletonVisible && error.message.length > 0 && error.retryVisible,
    { skeletonDuringLoad: during, afterLoad: after, empty, error503: error });
}

// CU-25: responsive + keyboard (create and transition surfaces).
async function cu25(browser) {
  if (MODE === 'v3') {
    const langs = ['en', 'tr', 'fr', 'es', 'zh', 'ar', 'ru']; const overflow = {};
    for (const lang of langs) {
      const c = await ctx(browser, 'full', { locale: lang, viewport: { width: 1280, height: 900 } }); const page = await c.newPage(); watch(page, `l10n-${lang}`);
      await ready(page, `${PAGE}?culture=${lang}&ui-culture=${lang}`); overflow[lang] = {};
      for (const w of (lang === 'en' || lang === 'ar') ? [390, 768, 1024, 1280, 1440] : [1280]) {
        await page.setViewportSize({ width: w, height: 900 }); await page.waitForTimeout(400);
        overflow[lang][w] = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1);
        await shot(page, `cu25-${lang}-${w}`);
      }
      if (lang === 'ar') overflow.arDir = await page.evaluate(() => document.documentElement.getAttribute('dir'));
      await c.close();
    }
    verdict('CU-25a-responsive', langs.every((l) => Object.values(overflow[l]).every((v) => v === false)) && overflow.arDir === 'rtl', { overflow });
  }
  // create surface
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, `kbd-create-${MODE}`); await ready(page);
    const add = page.locator('.add-new').first(); const open = () => visibleBox(page.locator('#offcanvasCreateEdit'));
    await add.focus(); await page.keyboard.press('Enter'); await page.waitForTimeout(1200); const byEnter = await open();
    if (!byEnter) { await add.focus(); await page.keyboard.press('Space'); await page.waitForTimeout(1200); }
    const opened = await open(); const focusInside = await focusIn(page, '#offcanvasCreateEdit');
    await page.locator('#btnAddEvidence').click(); await page.locator('#btnAddEvidence').click();
    const ids = await page.$$eval('#claimEvidenceList [id]', (els) => els.map((e) => e.id));
    await shot(page, `cu25-create-open-${MODE}`);
    await page.locator('#claimShipmentId').focus(); await page.keyboard.press('Escape'); await page.waitForTimeout(900);
    const closed = !(await open()); const inert = await page.locator('#offcanvasCreateEdit').evaluate((e) => e.inert === true);
    const focusReturned = await focusIn(page, '.add-new'); const focusedAfterClose = await focusId(page);
    verdict('CU-25b-create-keyboard', opened && focusInside && closed && inert && focusReturned && new Set(ids).size === ids.length,
      { openedByEnter: byEnter, openedBySpace: !byEnter && opened, focusInsideAfterOpen: focusInside, closedByEscape: closed, inertAfterClose: inert,
        focusReturnedToAdd: focusReturned, focusedAfterClose, evidenceIds: ids, note: 'Enter on Add = Q64d-D3 (template owner; not changed here)' });
    await c.close(); }
  // transition surface: immediate Escape (inside the ~300 ms slide-in) and settled Escape (after 900 ms)
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, `kbd-transition-${MODE}`); await ready(page); await filterById(page, FX.shipments.SEED.shipmentId);
    const id = FX.claims.OPEN2.claimId; const surf = page.locator('#offcanvasClaimTransition');
    const openIt = async () => { const row = rowOf(page, id); await row.locator('.dropdown-toggle').click(); await row.locator('.js-transition[data-target-status="Investigating"]').click(); };
    const settleClosed = async () => { await page.waitForTimeout(900); const closed = !(await visibleBox(surf));
      if (!closed) { await page.locator('#offcanvasClaimTransition .btn-close').click(); await surf.waitFor({ state: 'hidden' }); await page.waitForTimeout(400); } return closed; };
    await openIt(); await page.waitForTimeout(50);
    const immediate = { focusInside: await focusIn(page, '#offcanvasClaimTransition'), focused: await focusId(page) };
    await page.keyboard.press('Escape'); immediate.closedByEscape = await settleClosed();
    await openIt(); await page.waitForTimeout(900);
    const settled = { focusInside: await focusIn(page, '#offcanvasClaimTransition'), focused: await focusId(page) };
    await shot(page, `cu25-transition-open-${MODE}`);
    await page.keyboard.press('Escape'); settled.closedByEscape = await settleClosed();
    verdict('CU-25c-transition-keyboard', immediate.focusInside && immediate.closedByEscape && settled.focusInside && settled.closedByEscape && settled.focused === 'transitionTarget',
      { claimId: id, immediate50ms: immediate, settled900ms: settled });
    await c.close(); }
}

// D1/D2/D4 regression pass on v3 (writes: +1 claim, +2 receipts/audit/outbox).
async function regression(browser) {
  const S = FX.shipments;
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'shell'); const e0 = jsErrors(); await ready(page);
    const dom = await page.evaluate(() => ({ layoutWrappers: document.querySelectorAll('.layout-wrapper').length, mainJs: [...document.scripts].filter((s) => /\/main\.js/.test(s.src)).length, footers: document.querySelectorAll('footer').length }));
    verdict('REG-D-02-single-shell', dom.layoutWrappers === 1 && dom.mainJs === 1 && dom.footers === 1 && jsErrors() === e0, { dom });
    // skeleton hidden once the table is shown (D1 must not leave it visible)
    verdict('REG-D1-skeleton-hidden-after-load', !(await visibleBox(page.locator('#skeleton-loader'))) && await visibleBox(page.locator('#claims-table-host')), {});
    // filter Apply/Reset + Save View visibility (Q64c C-04 still intact)
    const saveVisible = () => page.locator('.dt-save-filter-btn').evaluate((e) => !e.classList.contains('d-none'));
    const s0 = await saveVisible(); await page.locator('.dt-filter-btn').click(); await page.locator('#filterShipmentId').fill(S.SEED.shipmentId);
    const ap = page.waitForRequest((r) => r.url().includes('/SupplyChain/Claims/api?')); await page.locator('#btnFilterApply').click(); const ar = await ap; await page.waitForTimeout(400); const s1 = await saveVisible();
    const rp = page.waitForRequest((r) => new URL(r.url()).pathname === '/SupplyChain/Claims/api' && r.method() === 'GET'); await page.locator('#btnFilterReset').click(); const rr = await rp; await page.waitForTimeout(400); const s2 = await saveVisible();
    verdict('REG-filter-saveview', s0 === false && s1 === true && s2 === false && new URL(ar.url()).searchParams.get('shipmentId') === S.SEED.shipmentId && new URL(rr.url()).search === '' && jsErrors() === e0, { s0, s1, s2 });
    // QuickView (no by-ID request)
    const calls = []; page.on('request', (r) => { if (new URL(r.url()).pathname.startsWith('/SupplyChain/Claims/api/')) calls.push(r.url()); });
    await page.locator('#dt-claims tbody tr .js-quick-view').first().click(); await page.locator('#offcanvasDetailsPreview').waitFor({ state: 'visible' }); await page.waitForTimeout(400);
    verdict('REG-quickview', calls.length === 0, { adapterCalls: calls.length });
    await c.close(); }
  // D4 + D-01: create by keyboard-free click, close returns focus; create → list reload shows the amount
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'create'); const e0 = jsErrors(); await ready(page);
    await page.locator('.add-new').click(); await page.locator('#claimShipmentId').fill(S.DISPATCHED.shipmentId);
    await Promise.all([page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/')), page.locator('#btnResolveShipment').click()]);
    await page.locator('#claimReasonCode').fill('DAMAGE'); await page.locator('#claimClaimedAmount').fill('264.00'); await page.locator('#claimCurrency').fill('EUR');
    const created = page.waitForResponse((r) => r.url() === API && r.request().method() === 'POST'); const reload = page.waitForResponse(isList, { timeout: 10000 });
    await page.locator('#btnSaveClaim').click(); const cr = await created; const reloaded = await reload.then(() => true).catch(() => false); await page.waitForTimeout(1200);
    const shown = (await page.locator('#dt-claims').innerText()).includes('264.00'); const closed = !(await visibleBox(page.locator('#offcanvasCreateEdit')));
    const focusReturned = await focusIn(page, '.add-new');
    await shot(page, 'regression-create-reload');
    verdict('REG-D-01-create-reload-and-D4-focus', cr.status() === 201 && reloaded && shown && closed && focusReturned && jsErrors() === e0,
      { createStatus: cr.status(), reloaded, rowShowsAmount: shown, surfaceClosedAfterSave: closed, focusReturnedToAdd: focusReturned, newJsErrors: jsErrors() - e0 });
    await c.close(); }
  // D2 path end to end: Open → Investigating through the transition surface (first field focused), confirm, 200, reload
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, 'transition'); const e0 = jsErrors(); await ready(page); await filterById(page, S.SEED.shipmentId);
    const id = FX.claims.OPEN1.claimId; const row = rowOf(page, id); await row.locator('.dropdown-toggle').click(); await row.locator('.js-transition[data-target-status="Investigating"]').click();
    await page.waitForTimeout(900); const focused = await focusId(page);
    await page.locator('#btnSubmitTransition').click();
    const resp = page.waitForResponse((r) => r.url().includes(`/api/${id}/transition?shipmentId=`)); await page.locator('.swal2-confirm').first().click(); const tr = await resp;
    const reloaded = await page.waitForResponse(isList, { timeout: 10000 }).then(() => true).catch(() => false); await page.waitForTimeout(600);
    await shot(page, 'regression-transition-investigating');
    verdict('REG-D2-transition-flow', focused === 'transitionTarget' && tr.status() === 200 && reloaded && jsErrors() === e0, { claimId: id, focusedOnOpen: focused, status: tr.status(), reloaded, newJsErrors: jsErrors() - e0 });
    await c.close(); }
}

// diag (added after cu05-v3-a1 still showed display:none): polls the skeleton every 100 ms during a delayed first load
// and records whether the served index.js carries the D1 fix. Zero write.
async function diag(browser) {
  const c = await ctx(browser, 'full');
  await c.route((u) => u.pathname === '/SupplyChain/Claims/api', async (route) => { await new Promise((r) => setTimeout(r, 2500)); await route.continue(); });
  const page = await c.newPage(); watch(page, `diag-${MODE}`);
  await page.addInitScript(() => {
    window.__skel = []; const t0 = performance.now();
    const snap = (why) => { const e = document.getElementById('skeleton-loader'); if (!e) return;
      window.__skel.push({ t: Math.round(performance.now() - t0), why, inline: e.getAttribute('style'), cls: e.className, computed: getComputedStyle(e).display }); };
    document.addEventListener('DOMContentLoaded', () => {
      snap('DOMContentLoaded'); const e = document.getElementById('skeleton-loader');
      if (e) new MutationObserver(() => snap('mutation')).observe(e, { attributes: true, attributeFilter: ['style', 'class'] });
      const iv = setInterval(() => snap('poll'), 100); setTimeout(() => clearInterval(iv), 4000);
    });
  });
  const served = await page.request.get(`${WEB}/assets/js/SupplyChain/Claims/index.js`).then((r) => r.text());
  await page.goto(PAGE); await page.waitForTimeout(4200);
  const timeline = await page.evaluate(() => window.__skel);
  const changes = timeline.filter((x, i) => i === 0 || x.why === 'mutation' || x.computed !== timeline[i - 1].computed);
  verdict('DIAG-skeleton-timeline', true, { servedIndexHasD1Fix: served.includes("skeleton.style.display = state === 'skeleton' ? 'block' : 'none';"), servedSha256: sha(Buffer.from(served)),
    changes, pollSamples: timeline.filter((x) => x.why === 'poll').map((x) => `${x.t}:${x.computed}`).join(' ') });
  await c.close();
}

const PH = { login, cu05, cu25, regression, diag };
const browser = await chromium.launch({ headless: true });
try { if (!PH[PHASE]) throw new Error('unknown phase'); if (MODE === 'v2' && !(V2FILE && V2SHA)) throw new Error('v2 mode needs file + sha'); await PH[PHASE](browser); }
catch (e) { result.error = String(e && e.message || e).slice(0, 1500); }
finally { await browser.close(); const f = uniq(TAG); result.endedAt = iso(); fs.writeFileSync(f, JSON.stringify(result, null, 2) + '\n');
  console.log(JSON.stringify({ phase: PHASE, mode: MODE, file: path.basename(f), verdicts: Object.fromEntries(Object.entries(result.cases).map(([k, v]) => [k, v.result])), rootLeak: result.rootLeak.length, dialogs: result.dialogs.length, error: result.error || null })); }
process.exit(result.error || Object.values(result.cases).some((c) => c.result === 'FAIL') || result.rootLeak.length || result.dialogs.length ? 1 : 0);
