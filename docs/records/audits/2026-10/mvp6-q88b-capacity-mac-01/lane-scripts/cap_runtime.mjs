// Q88b browser harness — MOD-0192 Capacity UI draft overlay v3 (5c0a3b61…) on BASE-STACK v2, kit slot 5.
// Copied from the Q84b S&OP harness (login, helpers, hygiene) and the Q185 nav smoke (N1/N2), adapted to Capacity.
// Run ONLY by the lane supervisor (task harness); it supplies ACTOR_PW_<LABEL>; nothing here prints or writes a secret.
// Usage: node cap_runtime.mjs <phase> <webPort> <outDir> <stateDir> <runtimeDir> <locale> <fragmentDir>
//   login  real Web login per actor; storage state ONLY in <stateDir> (outside the repo, 0600).
//   happy  MOD-0192 happy path: entry page → create plan → details → create scenario → evaluate (Finite) → Refresh until
//          Completed → reload restores scenario + evaluation from the address. Also UI-PM-01. WRITES (plan, scenario, evaluation).
//   uipm   UI-PM-05, UI-PM-12, UI-PM-09, UI-PM-10 (REAL rejected submits). Zero write.
//   o3     O-3: 403 on create plan / create scenario / evaluate / page load (FAKED) + real 403s. Zero write.
//   q173   Q173 O-1 and O-2 (address / queued evaluationId). O-1 submits ONE real evaluation (write); the rest is read-only.
//   nav    sidebar + Ctrl+K in the platform system tenant (Q185 D-1). Read-only.
// FAKED = browser-side route.fulfill / delayed route.continue, used only where a real response cannot be produced on demand.
// PNGs only via page.screenshot({ path }). All browser URLs are on 127.0.0.1:<webPort>. Never deletes a file.
import fs from 'node:fs'; import path from 'node:path'; import crypto from 'node:crypto';
import { createRequire } from 'node:module';

const [PHASE, WEBPORT, OUT, STATE, RUNTIME, LOCALE, FRAGDIR] = process.argv.slice(2);
const DEMAND_ID = 'dp-2027', DEMAND_VER = '3', DEMAND_SUM = 'sha256:ee56d4f9a3c8'; // the backend's bounded fixture (DemandFixtureReader.cs)
const req = createRequire(path.join(RUNTIME, 'package.json'));
const { chromium } = req('playwright');
const PW_VERSION = req('playwright/package.json').version;
const WEB = `http://127.0.0.1:${WEBPORT}`;
const ENTRY = `${WEB}/SupplyChain/CapacityPlans`;
const APIROOT = '/SupplyChain/CapacityPlans/api';
const TF = '19200000-0000-4000-8000-000000000001', T2 = '00000000-0000-0000-0000-000000000001';
const ACT = { capfull: ['cap.full.q88b@diten.com', TF], capro: ['cap.readonly.q88b@diten.com', TF], capnoread: ['cap.noread.q88b@diten.com', TF], navfull: ['john.doe.def@diten.com', T2] };
const OUTD = path.join(OUT, 'browser'); const PNG = path.join(OUT, 'png');
[OUTD, PNG].forEach((d) => fs.mkdirSync(d, { recursive: true }));
const loc = LOCALE || 'en';
const tag = `${PHASE}-${loc}`;
const uniq = (base) => { let n = 1; while (fs.existsSync(path.join(OUTD, `${base}-a${n}.json`))) n++; return path.join(OUTD, `${base}-a${n}.json`); };
const result = { phase: PHASE, locale: loc, startedAt: new Date().toISOString(), web: WEB, playwright: PW_VERSION, cases: {}, png: [], console: [], toasts: [], dialogs: [], foreign: [], net: [] };
const sha = (b) => crypto.createHash('sha256').update(b).digest('hex');
const statePath = (a) => path.join(STATE, `${a}.json`);
const planFile = path.join(STATE, `plan-${loc}.json`);
const iso = () => new Date().toISOString();
const RAW = /\bFORBIDDEN\b|\bForbidden\b|Permission denied|\b403\b|Exception|stack trace|"error"|\{"|DEPENDENCY_UNAVAILABLE|INVALID_DEMAND_REFERENCE|INVALID_CONSTRAINT_REFERENCE|UNKNOWN_CAPACITY/;

async function shot(page, name) {
  const base = `${name}-${loc}`; let n = base; let i = 2; while (fs.existsSync(path.join(PNG, `${n}.png`))) n = `${base}-${i++}`;
  const p = path.join(PNG, `${n}.png`); await page.screenshot({ path: p, fullPage: true });
  result.png.push({ name: `${n}.png`, sha256: sha(fs.readFileSync(p)), url: new URL(page.url()).pathname, capturedAt: iso(), tool: `playwright ${PW_VERSION} page.screenshot` });
  return `png/${n}.png`;
}
function watch(page, t) {
  page.on('dialog', async (d) => { result.dialogs.push({ tag: t, type: d.type(), at: iso() }); await d.dismiss().catch(() => null); });
  page.on('console', (m) => {
    const text = m.text();
    const toast = /\[MOD-0013 showToast\] Triggered - Key: (.*) \| Type: (\w+)/.exec(text);
    if (toast) result.toasts.push({ tag: t, text: toast[1].slice(0, 300), type: toast[2], at: iso() });
    if (['error', 'warning'].includes(m.type())) result.console.push({ tag: t, type: m.type(), text: text.slice(0, 300) });
  });
  page.on('pageerror', (e) => result.console.push({ tag: t, type: 'pageerror', text: String(e.message || e).slice(0, 300) }));
  page.on('request', (r) => { const u = new URL(r.url()); if (u.host === `127.0.0.1:${WEBPORT}` || u.protocol.startsWith('data')) return;
    if (/^fonts\.(googleapis|gstatic)\.com$/.test(u.host)) { result.shellFonts = (result.shellFonts || 0) + 1; return; } // shell Google Fonts (Q64d: the only foreign requests)
    result.foreign.push({ tag: t, host: u.host }); });
  page.on('response', async (r) => {
    const u = new URL(r.url()); if (!u.pathname.startsWith('/SupplyChain/CapacityPlans/api')) return;
    let code = null; try { code = (await r.json())?.error?.code || null; } catch (_) { }
    result.net.push({ tag: t, at: iso(), method: r.request().method(), path: u.pathname, status: r.status(), errorCode: code,
      idempotencyKey: !!r.request().headers()['idempotency-key'], fromRoute: r.request().headers()['x-q84b-crafted'] ? true : undefined });
  });
}
async function ctx(browser, actor, viewport) {
  return browser.newContext({ storageState: actor ? statePath(actor) : undefined, timezoneId: 'UTC', locale: loc === 'ar' ? 'ar' : 'en-US',
    viewport: viewport || { width: 1366, height: 900 } });
}
const verdict = (name, ok, detail) => { result.cases[name] = { result: ok ? 'PASS' : 'FAIL', ...detail }; };
const save = () => { const f = uniq(tag); result.endedAt = iso(); fs.writeFileSync(f, JSON.stringify(result, null, 2) + '\n');
  console.log(JSON.stringify({ phase: PHASE, locale: loc, file: path.basename(f), verdicts: Object.fromEntries(Object.entries(result.cases).map(([k, v]) => [k, v.result])),
    dialogs: result.dialogs.length, foreign: result.foreign.length, error: result.error || null })); };
const withCulture = (u) => `${u}${u.includes('?') ? '&' : '?'}culture=${loc}&ui-culture=${loc}`;
const active = (page) => page.evaluate(() => { const a = document.activeElement; const inP = (id) => !!document.getElementById(id)?.contains(a);
  return { id: a?.id || null, tag: a?.tagName || null, inCreatePlan: inP('offcanvasCreatePlan'), inScenario: inP('offcanvasCreateScenario'), inEvaluate: inP('offcanvasEvaluate') }; });
const shell = (page) => page.evaluate(() => ({ layoutWrappers: document.querySelectorAll('.layout-wrapper').length, layoutMenus: document.querySelectorAll('#layout-menu').length,
  navbars: document.querySelectorAll('#layout-navbar').length, footers: document.querySelectorAll('footer.content-footer').length,
  mainJs: document.querySelectorAll('script[src*="/assets/js/main.js"]').length, htmlDir: document.documentElement.getAttribute('dir'), htmlLang: document.documentElement.getAttribute('lang') }));
const oneShell = (s) => s.layoutWrappers === 1 && s.layoutMenus === 1 && s.navbars === 1 && s.footers === 1 && s.mainJs === 1;
const dirOk = (s) => (loc === 'ar' ? s.htmlDir === 'rtl' : s.htmlDir !== 'rtl');
const visibleText = (page) => page.evaluate(() => document.body.innerText);
const hasArabic = (s) => /[؀-ۿ]/.test(s);
const shown = (page, id) => page.locator(`#${id}.show`).waitFor({ state: 'visible', timeout: 15000 });
const hidden = async (page, id) => { await page.waitForFunction((i) => { const e = document.getElementById(i); return !e || (!e.classList.contains('show') && !e.classList.contains('hiding') && !e.classList.contains('showing')); }, id, { timeout: 15000 }); await page.waitForTimeout(300); };
const craftedOld = (status, code) => ({ status, contentType: 'application/json', headers: { 'X-Correlation-Id': crypto.randomUUID() },
  body: JSON.stringify({ error: { code, message: code === 'FORBIDDEN' ? 'Permission denied' : 'Dependency unavailable', correlationId: crypto.randomUUID() } }) });
const readPlan = () => JSON.parse(fs.readFileSync(planFile, 'utf8'));
const settle = async (page, ms = 600) => { await page.waitForLoadState('networkidle').catch(() => null); await page.waitForTimeout(ms); };

// ───────────────────────────── login ─────────────────────────────
async function login(browser) {
  fs.mkdirSync(STATE, { recursive: true, mode: 0o700 });
  for (const [actor, [email, tenant]] of Object.entries(ACT)) {
    const c = await ctx(browser, null); const page = await c.newPage(); watch(page, `login-${actor}`);
    await page.goto(`${WEB}/account/login?tenantId=${tenant}&returnUrl=%2FSupplyChain%2FCapacityPlans`);
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

const crafted = (status, code) => ({ status, contentType: 'application/json', headers: { 'X-Correlation-Id': crypto.randomUUID() },
  body: JSON.stringify({ error: { code, message: code, correlationId: crypto.randomUUID() } }) });
const norm = (t) => (t || '').replace(/\s+/g, ' ').trim();
// ───────────────────────────── Capacity helpers ─────────────────────────────
const stFile = path.join(STATE, `cap-${loc}.json`);
const readSt = () => JSON.parse(fs.readFileSync(stFile, 'utf8'));
const writeSt = (o) => fs.writeFileSync(stFile, JSON.stringify(o) + '\n', { mode: 0o600 });
const det = (planId, q = '') => withCulture(`${ENTRY}/Details/${planId}${q}`);
const apiOf = (planId) => `${APIROOT}/${planId}`;
const waitPlan = async (page) => { await page.locator('#summary-content').waitFor({ state: 'visible', timeout: 20000 }); await settle(page, 600); };
const qs = (page) => page.evaluate(() => { const u = new URL(window.location.href); return { scenarioId: u.searchParams.get('scenarioId'), evaluationId: u.searchParams.get('evaluationId') }; });
const panelState = (page, name) => page.evaluate((n) => { const g = (s) => { const e = document.getElementById(`${n}-${s}`); return !!e && !e.classList.contains('d-none') && getComputedStyle(e).display !== 'none'; };
  return { empty: g('empty'), skeleton: g('skeleton'), error: g('error'), content: g('content') }; }, name);
async function fillPlan(page, o) {
  await page.fill('#planName', o.name); await page.fill('#planHorizonStart', o.start); await page.fill('#planHorizonEnd', o.end);
  await page.fill('#planDemandPlanId', o.demandId ?? DEMAND_ID); await page.fill('#planDemandPlanVersion', DEMAND_VER);
  await page.fill('#planSourceCapturedAt', '2027-01-04T08:00:00Z'); await page.fill('#planSourceChecksum', DEMAND_SUM);
}
async function fillScenario(page, name, constraintId = 'line-4-hours') {
  await page.fill('#scenarioName', name);
  if (await page.locator('#scenarioConstraintRefRows .js-constraint-id').count() === 0) await page.locator('#btnAddConstraintRef').click();
  if (await page.locator('#scenarioAdjustmentRows .js-resource-ref').count() === 0) await page.locator('#btnAddAdjustment').click();
  const c = page.locator('#scenarioConstraintRefRows'); await c.locator('.js-constraint-id').first().fill(constraintId); await c.locator('.js-constraint-source').first().fill('SUPPLY-CONSTRAINTS'); await c.locator('.js-constraint-version').first().fill('8');
  const a = page.locator('#scenarioAdjustmentRows'); await a.locator('.js-resource-ref').first().fill('line-4'); await a.locator('.js-period').first().fill('2027-W03'); await a.locator('.js-delta').first().fill('80.000'); await a.locator('.js-uom').first().fill('HOUR');
}
async function fillEvaluate(page, resource = 'line-4') {
  await page.selectOption('#evaluationMode', 'Finite');
  if (await page.locator('#evaluateResourceRefRows .js-resource-ref').count() === 0) await page.locator('#btnAddResourceRef').click();
  await page.locator('#evaluateResourceRefRows .js-resource-ref').first().fill(resource);
}
const postTo = (page, pathname) => page.waitForResponse((r) => new URL(r.url()).pathname === pathname && r.request().method() === 'POST', { timeout: 30000 });
async function refreshUntilCompleted(page, planId, max = 8) {
  const seen = [];
  for (let i = 0; i < max; i++) {
    const s = norm(await page.locator('#evaluationStatusValue').innerText().catch(() => '')); seen.push(s);
    const rows = await page.locator('#dt-capacity-bottlenecks tbody tr:not(:has(.dt-empty))').count().catch(() => 0);
    if (rows > 0 || (await page.locator('#evaluationCompletedAtValue').innerText().catch(() => '')).match(/\d{4}-\d{2}-\d{2}/)) return { completed: true, statuses: seen, refreshClicks: i };
    await page.waitForTimeout(4000);
    const g = page.waitForResponse((r) => /\/evaluations\/[0-9a-f-]{36}$/i.test(new URL(r.url()).pathname) && r.request().method() === 'GET', { timeout: 15000 }).catch(() => null);
    await page.locator('#btnRefreshEvaluation').click(); await g; await settle(page, 500);
  }
  return { completed: false, statuses: seen, refreshClicks: max };
}

// ───────────────────────────── happy path (writes) ─────────────────────────────
async function happy(browser) {
  const c = await ctx(browser, 'capfull'); const page = await c.newPage(); watch(page, tag);
  const apiGets = []; page.on('request', (r) => { const u = new URL(r.url()); if (u.pathname.startsWith(APIROOT) && r.method() === 'GET') apiGets.push(u.pathname); });
  await page.goto(withCulture(ENTRY)); await settle(page);
  const entryShell = await shell(page);
  const entry = { markup: await page.locator('#capacity-plans-entry').count(), cta: await page.locator('#btnOpenCreatePlan').count(), tables: await page.locator('table').count(), apiGetsOnEntry: apiGets.length,
    title: norm(await page.locator('#capacity-plans-entry h5, #capacity-plans-entry h4').first().innerText().catch(() => '')) };
  const p1 = await shot(page, 'happy-01-entry');
  await page.locator('#btnOpenCreatePlan').click(); await shown(page, 'offcanvasCreatePlan');
  const name = `Q88b ${loc} plan ${crypto.randomUUID().slice(0, 8)}`;
  await fillPlan(page, { name, start: loc === 'ar' ? '2027-04-01' : '2027-01-01', end: loc === 'ar' ? '2027-06-30' : '2027-03-31' });
  const p2 = await shot(page, 'happy-02-create-plan');
  const cr = postTo(page, APIROOT); await page.locator('#btnSubmitCreatePlan').click(); const createResp = await cr;
  let cb = null; try { cb = await createResp.json(); } catch (_) { }
  await page.waitForURL(/\/SupplyChain\/CapacityPlans\/Details\/[0-9a-f-]{36}/i, { timeout: 20000 }).catch(() => null);
  const planId = (page.url().match(/Details\/([0-9a-f-]{36})/i) || [])[1] || null;
  if (planId) await waitPlan(page);
  const detailsShell = await shell(page);
  const sum = { title: norm(await page.locator('#planTitle').innerText().catch(() => '')), status: norm(await page.locator('#summaryStatus').innerText().catch(() => '')),
    scenarioCta: await page.locator('#btnOpenCreateScenario').isVisible().catch(() => false), evaluateCta: await page.locator('#btnOpenEvaluate').isVisible().catch(() => false) };
  const p3 = await shot(page, 'happy-03-details');
  verdict('MOD0192-happy-create-plan', createResp.status() === 201 && !!planId && sum.title === name && entry.apiGetsOnEntry === 0,
    { planId, createStatus: createResp.status(), createErrorCode: cb?.error?.code || null, createBodyKeys: Object.keys(JSON.parse(createResp.request().postData() || '{}')), entry, summary: sum, evidence: [p1, p2, p3] });
  if (!planId) { await c.close(); return; }
  // scenario
  await page.locator('#btnOpenCreateScenario').click(); await shown(page, 'offcanvasCreateScenario');
  await fillScenario(page, `Q88b ${loc} scenario A`);
  const p4 = await shot(page, 'happy-04-create-scenario');
  const sr = postTo(page, `${apiOf(planId)}/scenarios`); await page.locator('#btnSubmitCreateScenario').click(); const scResp = await sr;
  let sb = null; try { sb = await scResp.json(); } catch (_) { }
  await hidden(page, 'offcanvasCreateScenario'); await page.locator('#scenario-content').waitFor({ state: 'visible', timeout: 15000 }).catch(() => null); await settle(page, 600);
  const scenarioId = sb?.scenarioId || null;
  const sc = { panel: await panelState(page, 'scenario'), idShown: norm(await page.locator('#scenarioIdValue').innerText().catch(() => '')), nameShown: norm(await page.locator('#scenarioNameValue').innerText().catch(() => '')),
    address: await qs(page), evaluateCta: await page.locator('#btnOpenEvaluate').isVisible().catch(() => false) };
  const p5 = await shot(page, 'happy-05-scenario');
  verdict('MOD0192-happy-create-scenario', scResp.status() === 201 && !!scenarioId && sc.panel.content && sc.idShown.includes(scenarioId) && sc.address.scenarioId === scenarioId && sc.evaluateCta,
    { scenarioId, status: scResp.status(), errorCode: sb?.error?.code || null, scenario: sc, requestBody: JSON.parse(scResp.request().postData() || '{}'), evidence: [p4, p5] });
  if (!scenarioId) { writeSt({ planId, name }); await c.close(); return; }
  // evaluate
  await page.locator('#btnOpenEvaluate').click(); await shown(page, 'offcanvasEvaluate');
  await fillEvaluate(page);
  const p6 = await shot(page, 'happy-06-evaluate');
  const er = postTo(page, `${apiOf(planId)}/scenarios/${scenarioId}/evaluations`); await page.locator('#btnSubmitEvaluate').click(); const evResp = await er;
  let eb = null; try { eb = await evResp.json(); } catch (_) { }
  await hidden(page, 'offcanvasEvaluate'); await page.locator('#evaluation-content').waitFor({ state: 'visible', timeout: 15000 }).catch(() => null); await settle(page, 600);
  const evaluationId = eb?.evaluationId || null;
  const firstStatus = norm(await page.locator('#evaluationStatusValue').innerText().catch(() => ''));
  const done = await refreshUntilCompleted(page, planId);
  const ev = { panel: await panelState(page, 'evaluation'), idShown: norm(await page.locator('#evaluationIdValue').innerText().catch(() => '')), scenarioShown: norm(await page.locator('#evaluationScenarioIdValue').innerText().catch(() => '')),
    status: norm(await page.locator('#evaluationStatusValue').innerText().catch(() => '')), bottleneckRows: await page.locator('#dt-capacity-bottlenecks tbody tr:not(:has(.dt-empty))').count().catch(() => 0),
    bottleneckText: norm(await page.locator('#dt-capacity-bottlenecks tbody').innerText().catch(() => '')).slice(0, 200), address: await qs(page) };
  const p7 = await shot(page, 'happy-07-evaluation-completed');
  // reload: the address restores scenario + evaluation
  await page.reload(); await waitPlan(page); await page.locator('#evaluation-content').waitFor({ state: 'visible', timeout: 15000 }).catch(() => null); await settle(page, 800);
  const re = { scenario: await panelState(page, 'scenario'), evaluation: await panelState(page, 'evaluation'), address: await qs(page), evaluationIdShown: norm(await page.locator('#evaluationIdValue').innerText().catch(() => '')) };
  const p8 = await shot(page, 'happy-08-after-reload');
  verdict('MOD0192-happy-evaluate', evResp.status() === 202 && !!evaluationId && done.completed && ev.panel.content && ev.idShown.includes(evaluationId) && ev.scenarioShown.includes(scenarioId) && ev.bottleneckRows === 1
    && ev.address.evaluationId === evaluationId && re.scenario.content && re.evaluation.content && re.evaluationIdShown.includes(evaluationId),
    { evaluationId, status: evResp.status(), errorCode: eb?.error?.code || null, firstStatus, refresh: done, evaluation: ev, afterReload: re, evidence: [p6, p7, p8] });
  verdict('UI-PM-01', oneShell(entryShell) && oneShell(detailsShell) && dirOk(entryShell) && dirOk(detailsShell), { entryShell, detailsShell, note: 'entry and details with every partial (create plan / create scenario / evaluate offcanvas, L10n)', evidence: [p1, p8] });
  writeSt({ planId, name, scenarioId, evaluationId });
  await c.close();
}

// ───────────────────────────── UI-PM-05 / 12 / 09 / 10 (zero write) ─────────────────────────────
async function uipm(browser) {
  const { planId, scenarioId, evaluationId } = readSt(); const API = apiOf(planId);
  // UI-PM-05 — FAKED delay only (real responses)
  { const c = await ctx(browser, 'capfull'); const page = await c.newPage(); watch(page, `${tag}-pm05`);
    await page.route((u) => u.pathname.startsWith(API), async (route) => { if (route.request().method() === 'GET') await new Promise((r) => setTimeout(r, 3000)); await route.continue().catch(() => null); });
    await page.goto(det(planId, `?scenarioId=${scenarioId}&evaluationId=${evaluationId}`), { waitUntil: 'domcontentloaded' });
    await page.waitForFunction(() => { const e = document.getElementById('summary-skeleton'); return e && getComputedStyle(e).display !== 'none'; }, null, { timeout: 8000 }).catch(() => null);
    await page.waitForTimeout(500);
    const sk = (names) => page.evaluate((ns) => ns.map((n) => { const e = document.getElementById(`${n}-skeleton`); return { section: n, display: getComputedStyle(e).display, height: e.offsetHeight }; }), names);
    const during = await sk(['summary', 'scenario', 'evaluation']);
    const pD = await shot(page, 'uipm05-skeleton-1');
    await page.locator('#scenario-content').waitFor({ state: 'visible', timeout: 20000 }).catch(() => null); await page.waitForTimeout(700);
    const duringEval = await sk(['evaluation']);
    const pE = await shot(page, 'uipm05-skeleton-2-evaluation');
    await page.locator('#evaluation-content').waitFor({ state: 'visible', timeout: 20000 }).catch(() => null); await settle(page, 600);
    const after = await sk(['summary', 'scenario', 'evaluation']);
    const pA = await shot(page, 'uipm05-loaded');
    const vis = (x) => x.display !== 'none' && x.height > 0;
    verdict('UI-PM-05', vis(during[0]) && vis(during[1]) && vis(duringEval[0]) && after.every((a) => a.display === 'none'),
      { during, duringEvaluationLoad: duringEval, after, faked: 'delay only: GETs delayed 3 s in the browser, real responses', evidence: [pD, pE, pA] });
    await c.close(); }
  // UI-PM-12 — FAKED failures, one panel per page load; Retry hits the real service
  { const out = {}; const ev = [];
    const cases = [['summary', API, 'fail', 503], ['scenario', `${API}/scenarios/${scenarioId}`, 'fail', 500], ['scenario-malformed', `${API}/scenarios/${scenarioId}`, 'malformed', 200], ['evaluation', `${API}/evaluations/${evaluationId}`, 'fail', 500]];
    for (const [label, pathname, kind, status] of cases) {
      const sec = label.split('-')[0];
      const c = await ctx(browser, 'capfull'); const page = await c.newPage(); watch(page, `${tag}-pm12-${label}`); let failing = true;
      await page.route((u) => u.pathname === pathname, (route) => { if (!failing || route.request().method() !== 'GET') return route.continue();
        return kind === 'malformed' ? route.fulfill({ status: 200, contentType: 'application/json', headers: { 'X-Correlation-Id': crypto.randomUUID() }, body: '{"unexpected":true}' }) : route.fulfill(crafted(status, 'DEPENDENCY_UNAVAILABLE')); });
      await page.goto(det(planId, `?scenarioId=${scenarioId}&evaluationId=${evaluationId}`)); await page.locator(`#${sec}-error`).waitFor({ state: 'visible', timeout: 15000 }).catch(() => null); await settle(page, 600);
      const st = await page.evaluate((s) => { const err = document.getElementById(`${s}-error`); return { errorVisible: !!err && !err.classList.contains('d-none') && err.offsetHeight > 0,
        skeletonDisplay: getComputedStyle(document.getElementById(`${s}-skeleton`)).display, message: err?.querySelector('.js-section-error-message')?.innerText || err?.innerText || '',
        retryVisible: !!err?.querySelector('.js-retry') && !err.querySelector('.js-retry').classList.contains('d-none') }; }, sec);
      const text = await visibleText(page); ev.push(await shot(page, `uipm12-${label}-error`));
      failing = false;
      const rr = page.waitForResponse((r) => new URL(r.url()).pathname === pathname && r.request().method() === 'GET', { timeout: 15000 }).catch(() => null);
      await page.locator(`#${sec}-error .js-retry`).click().catch(() => null); const r = await rr; await settle(page, 900);
      out[label] = { ...st, rawErrorInPage: RAW.test(text), retryRequest: !!r, retryStatus: r ? r.status() : null, after: await panelState(page, sec) };
      ev.push(await shot(page, `uipm12-${label}-after-retry`)); await c.close();
    }
    const ok = Object.values(out).every((o) => o.errorVisible && o.skeletonDisplay === 'none' && norm(o.message).length > 0 && !RAW.test(o.message) && (loc !== 'ar' || hasArabic(o.message)) && !o.rawErrorInPage
      && o.retryVisible && o.retryRequest && o.retryStatus === 200 && o.after.content && !o.after.error);
    verdict('UI-PM-12', ok, { out, faked: 'plan GET 503, scenario GET 500, scenario GET 200 malformed, evaluation GET 500 via route.fulfill; Retry hits the real service', evidence: ev }); }
  // UI-PM-09 — stable openers return focus
  { const c = await ctx(browser, 'capfull'); const page = await c.newPage(); watch(page, `${tag}-pm09`); const out = {};
    await page.goto(withCulture(ENTRY)); await settle(page);
    for (const how of ['escape', 'close-button']) {
      await page.locator('#btnOpenCreatePlan').focus(); await page.keyboard.press('Enter'); await shown(page, 'offcanvasCreatePlan'); const inside = await active(page);
      if (how === 'escape') await page.keyboard.press('Escape'); else await page.locator('#offcanvasCreatePlan [data-bs-dismiss="offcanvas"]').first().click();
      await hidden(page, 'offcanvasCreatePlan'); out[`createPlan-${how}`] = { focusInsideOnShown: inside.inCreatePlan, afterHidden: await active(page), want: 'btnOpenCreatePlan' };
    }
    const pE = await shot(page, 'uipm09-entry');
    await page.goto(det(planId, `?scenarioId=${scenarioId}`)); await waitPlan(page); await page.locator('#scenario-content').waitFor({ state: 'visible', timeout: 15000 }).catch(() => null); await settle(page, 500);
    for (const [btn, panel, key] of [['btnOpenCreateScenario', 'offcanvasCreateScenario', 'inScenario'], ['btnOpenEvaluate', 'offcanvasEvaluate', 'inEvaluate']]) {
      if (!(await page.locator(`#${btn}`).isVisible().catch(() => false))) { out[panel] = { skipped: 'opener not visible', afterHidden: { id: null }, want: btn }; continue; }
      await page.locator(`#${btn}`).focus(); await page.keyboard.press('Enter'); await shown(page, panel); const inside = await active(page);
      await page.keyboard.press('Escape'); await hidden(page, panel); out[panel] = { focusInsideOnShown: inside[key], afterHidden: await active(page), want: btn };
    }
    const pD = await shot(page, 'uipm09-details');
    verdict('UI-PM-09', Object.values(out).every((o) => o.afterHidden.id === o.want), { out, scope: 'r2 / OD-F-Q145-1: stable toolbar/page openers (Create plan, Create scenario, Evaluate); no row-menu surface', evidence: [pE, pD] });
    await c.close(); }
  // UI-PM-10 — REAL rejected submits (422): focus inside the open panel; Escape closes
  { const c = await ctx(browser, 'capfull'); const page = await c.newPage(); watch(page, `${tag}-pm10`); const out = {};
    const check = async (panelId, alertId, key, r) => { await page.waitForTimeout(800);
      const o = { status: r.status(), errorCode: (await r.json().catch(() => ({})))?.error?.code || null, panelOpen: await page.locator(`#${panelId}.show`).count() === 1, focus: await active(page),
        alert: norm(await page.locator(`#${alertId}`).innerText().catch(() => '')).slice(0, 200) };
      o.pngKey = key; return o; };
    await page.goto(withCulture(ENTRY)); await settle(page);
    await page.locator('#btnOpenCreatePlan').click(); await shown(page, 'offcanvasCreatePlan');
    await fillPlan(page, { name: `Q88b ${loc} rejected`, start: '2028-01-01', end: '2028-02-01', demandId: 'dp-q88b-unknown' });
    let pr = postTo(page, APIROOT); await page.locator('#btnSubmitCreatePlan').click(); out.createPlan = await check('offcanvasCreatePlan', 'formCreatePlanAlert', 'inCreatePlan', await pr);
    const p1 = await shot(page, 'uipm10-create-plan-422'); await page.keyboard.press('Escape'); await hidden(page, 'offcanvasCreatePlan'); out.createPlan.escapeClosed = await page.locator('#offcanvasCreatePlan.show').count() === 0;
    await page.goto(det(planId, `?scenarioId=${scenarioId}`)); await waitPlan(page); await page.locator('#scenario-content').waitFor({ state: 'visible', timeout: 15000 }).catch(() => null); await settle(page, 500);
    await page.locator('#btnOpenCreateScenario').click(); await shown(page, 'offcanvasCreateScenario'); await fillScenario(page, `Q88b ${loc} rejected scenario`, 'line-9-unknown');
    pr = postTo(page, `${API}/scenarios`); await page.locator('#btnSubmitCreateScenario').click(); out.scenario = await check('offcanvasCreateScenario', 'formCreateScenarioAlert', 'inScenario', await pr);
    const p2 = await shot(page, 'uipm10-scenario-422'); await page.keyboard.press('Escape'); await hidden(page, 'offcanvasCreateScenario'); out.scenario.escapeClosed = await page.locator('#offcanvasCreateScenario.show').count() === 0;
    await page.locator('#btnOpenEvaluate').click(); await shown(page, 'offcanvasEvaluate'); await fillEvaluate(page, 'line-9-unknown');
    pr = postTo(page, `${API}/scenarios/${scenarioId}/evaluations`); await page.locator('#btnSubmitEvaluate').click(); out.evaluate = await check('offcanvasEvaluate', 'formEvaluateAlert', 'inEvaluate', await pr);
    const p3 = await shot(page, 'uipm10-evaluate-422'); await page.keyboard.press('Escape'); await hidden(page, 'offcanvasEvaluate'); out.evaluate.escapeClosed = await page.locator('#offcanvasEvaluate.show').count() === 0;
    const ok = Object.values(out).every((o) => o.status >= 400 && o.status < 500 && o.status !== 403 && o.panelOpen && o.focus[o.pngKey] && o.escapeClosed && o.alert.length > 0 && !RAW.test(o.alert) && (loc !== 'ar' || hasArabic(o.alert)));
    verdict('UI-PM-10', ok, { out, note: 'no submit goes through window.showConfirm in this module, so all three direct submits are asserted', evidence: [p1, p2, p3] });
    await c.close(); }
}

// ───────────────────────────── O-3 (zero write) ─────────────────────────────
async function o3(browser) {
  const { planId, scenarioId, evaluationId } = readSt(); const API = apiOf(planId); const out = {}; const ev = [];
  const pageTitleInfo = (page) => page.evaluate(() => { const a = document.activeElement; const heads = Array.from(document.querySelectorAll('h1,h2,h3,h4,h5,h6')).filter((h) => !h.closest('.offcanvas') && h.offsetParent !== null);
    const first = heads[0] || null; return { activeId: a?.id || null, activeTag: a?.tagName || null, activeText: (a?.innerText || '').trim().slice(0, 50), activeIsFirstPageHeading: !!first && a === first,
      firstHeading: first ? { tag: first.tagName, id: first.id || null, tabindex: first.getAttribute('tabindex'), text: first.innerText.trim().slice(0, 50) } : null }; });
  // (a) create plan → FAKED 403
  { const c = await ctx(browser, 'capfull'); const page = await c.newPage(); watch(page, `${tag}-plan403`);
    await page.route((u) => u.pathname === APIROOT, (route) => route.request().method() === 'POST' ? route.fulfill(crafted(403, 'FORBIDDEN')) : route.continue());
    await page.goto(withCulture(ENTRY)); await settle(page);
    await page.locator('#btnOpenCreatePlan').click(); await shown(page, 'offcanvasCreatePlan'); await fillPlan(page, { name: 'Q88b o3', start: '2029-01-01', end: '2029-02-01' });
    await page.locator('#btnSubmitCreatePlan').click(); await hidden(page, 'offcanvasCreatePlan'); await page.waitForTimeout(800);
    out.createPlan = { ...(await pageTitleInfo(page)), ctaCount: await page.locator('#btnOpenCreatePlan').count(), rawErrorInPage: RAW.test(await visibleText(page)), toasts: result.toasts.filter((t) => t.tag === `${tag}-plan403`).map((t) => t.text) };
    ev.push(await shot(page, 'o3-create-plan-403')); await c.close(); }
  // (b) create scenario / evaluate → FAKED 403
  for (const [key, btn, panel, pathname, fill] of [['scenario', 'btnOpenCreateScenario', 'offcanvasCreateScenario', `${API}/scenarios`, (p) => fillScenario(p, 'Q88b o3 scenario')],
    ['evaluate', 'btnOpenEvaluate', 'offcanvasEvaluate', `${API}/scenarios/${scenarioId}/evaluations`, (p) => fillEvaluate(p)]]) {
    const c = await ctx(browser, 'capfull'); const page = await c.newPage(); watch(page, `${tag}-${key}403`);
    await page.route((u) => u.pathname === pathname, (route) => route.request().method() === 'POST' ? route.fulfill(crafted(403, 'FORBIDDEN')) : route.continue());
    await page.goto(det(planId, `?scenarioId=${scenarioId}`)); await waitPlan(page); await page.locator('#scenario-content').waitFor({ state: 'visible', timeout: 15000 }).catch(() => null); await settle(page, 500);
    await page.locator(`#${btn}`).click(); await shown(page, panel); await fill(page);
    await page.locator(panel === 'offcanvasCreateScenario' ? '#btnSubmitCreateScenario' : '#btnSubmitEvaluate').click(); await hidden(page, panel); await page.waitForTimeout(800);
    out[key] = { ...(await pageTitleInfo(page)), openerCount: await page.locator(`#${btn}`).count(), planTitleVisible: await page.locator('#planTitle').isVisible(), workspaceVisible: await page.locator('#planWorkspace').isVisible(),
      rawErrorInPage: RAW.test(await visibleText(page)), toasts: result.toasts.filter((t) => t.tag === `${tag}-${key}403`).map((t) => t.text) };
    ev.push(await shot(page, `o3-${key}-403`)); await c.close();
  }
  // (c) page load → FAKED 403 on GET plan
  { const c = await ctx(browser, 'capfull'); const page = await c.newPage(); watch(page, `${tag}-page403`);
    await page.route((u) => u.pathname === API, (route) => route.request().method() === 'GET' ? route.fulfill(crafted(403, 'FORBIDDEN')) : route.continue());
    await page.goto(det(planId)); await page.locator('#planAlert').waitFor({ state: 'visible', timeout: 15000 }).catch(() => null); await settle(page, 600);
    out.page = { ...(await pageTitleInfo(page)), alert: norm(await page.locator('#planAlert').innerText().catch(() => '')).slice(0, 200), planTitleVisible: await page.locator('#planTitle').isVisible(), planTitle: norm(await page.locator('#planTitle').innerText()),
      workspaceHidden: !(await page.locator('#planWorkspace').isVisible()), rawErrorInPage: RAW.test(await visibleText(page)) };
    ev.push(await shot(page, 'o3-page-403')); await c.close(); }
  // (d) real 403s
  { const c = await ctx(browser, 'capro'); const page = await c.newPage(); watch(page, `${tag}-readonly`);
    await page.goto(withCulture(ENTRY)); await settle(page);
    const tok = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue().catch(() => null);
    const r = await page.request.post(`${WEB}${APIROOT}`, { headers: { 'Content-Type': 'application/json', 'X-Correlation-Id': crypto.randomUUID(), 'Idempotency-Key': crypto.randomUUID(), ...(tok ? { RequestVerificationToken: tok } : {}) },
      data: JSON.stringify({ name: 'Q88b ro', horizonStart: '2029-03-01', horizonEnd: '2029-04-01', demandPlanId: DEMAND_ID, demandPlanVersion: DEMAND_VER, sourceCapturedAt: '2027-01-04T08:00:00Z', sourceChecksum: DEMAND_SUM }), maxRedirects: 0 });
    let j = null; try { j = await r.json(); } catch (_) { }
    await page.goto(det(planId, `?scenarioId=${scenarioId}`)); await waitPlan(page).catch(() => null);
    out.readonly = { entryCta: 0, directPostStatus: r.status(), directPostCode: j?.error?.code || null, detailsLoads: await page.locator('#summary-content').isVisible().catch(() => false),
      scenarioCta: await page.locator('#btnOpenCreateScenario').count(), evaluateCta: await page.locator('#btnOpenEvaluate').count() };
    await page.goto(withCulture(ENTRY)); await settle(page); out.readonly.entryCta = await page.locator('#btnOpenCreatePlan').count();
    ev.push(await shot(page, 'o3-readonly-entry')); await c.close(); }
  { const c = await ctx(browser, 'capnoread'); const page = await c.newPage(); watch(page, `${tag}-noread`); const res = {};
    for (const [k, u] of [['entry', withCulture(ENTRY)], ['details', det(planId)]]) { await page.goto(u); await settle(page);
      res[k] = { markup: await page.locator('#capacity-plans-entry, #capacity-plan-details').count(), skeletons: await page.locator('.backbone-skeleton').count(), forms: await page.locator('form#formOpenPlan, form#formOpenScenario').count(),
        rawErrorInPage: RAW.test(await visibleText(page)), url: new URL(page.url()).pathname }; ev.push(await shot(page, `o3-noread-${k}`)); }
    const g = await page.request.get(`${WEB}${API}`, { headers: { 'X-Correlation-Id': crypto.randomUUID() }, maxRedirects: 0 }); res.directGet = g.status(); out.noread = res; await c.close(); }
  const onTitle = (o, id) => o.activeIsFirstPageHeading || (id && o.activeId === id);
  const parts = { createPlanFocusOnPageTitle: onTitle(out.createPlan), scenarioFocusOnPageTitle: onTitle(out.scenario, 'planTitle'), evaluateFocusOnPageTitle: onTitle(out.evaluate, 'planTitle'),
    notBody: [out.createPlan, out.scenario, out.evaluate].every((o) => o.activeTag !== 'BODY'), noRawError: [out.createPlan, out.scenario, out.evaluate, out.page].every((o) => !o.rawErrorInPage),
    pageLevel: out.page.planTitleVisible && out.page.workspaceHidden && out.page.alert.length > 0 && !RAW.test(out.page.alert) && (loc !== 'ar' || hasArabic(out.page.alert)),
    real403: out.readonly.entryCta === 0 && out.readonly.directPostStatus === 403 && out.readonly.scenarioCta === 0 && out.readonly.evaluateCta === 0 && out.noread.entry.markup === 0 && out.noread.details.markup === 0
      && out.noread.entry.skeletons === 0 && out.noread.directGet === 403 && out.noread.entry.url === '/SupplyChain/CapacityPlans' };
  verdict('O-3', parts.createPlanFocusOnPageTitle && parts.scenarioFocusOnPageTitle && parts.evaluateFocusOnPageTitle && parts.noRawError && parts.pageLevel && parts.real403,
    { parts, out, faked: '403 on create plan / create scenario / evaluate / plan GET via route.fulfill; read-only and no-read 403s are real', evidence: ev });
}

// ───────────────────────────── Q173 O-1 / O-2 ─────────────────────────────
async function q173(browser) {
  const st = readSt(); const { planId, scenarioId: S1, evaluationId: E1 } = st; const API = apiOf(planId);
  const c = await ctx(browser, 'capfull'); const page = await c.newPage(); watch(page, tag);
  const evalGets = []; page.on('request', (r) => { const m = new URL(r.url()).pathname.match(/\/evaluations\/([0-9a-f-]{36})$/i); if (m && r.method() === 'GET') evalGets.push(m[1].toLowerCase()); });
  // setup (write): a second scenario S2 in the same plan, through the UI
  await page.goto(det(planId)); await waitPlan(page);
  await page.locator('#btnOpenCreateScenario').click(); await shown(page, 'offcanvasCreateScenario'); await fillScenario(page, `Q88b ${loc} scenario B`);
  const sr = postTo(page, `${API}/scenarios`); await page.locator('#btnSubmitCreateScenario').click(); const scResp = await sr; let sb = null; try { sb = await scResp.json(); } catch (_) { }
  await hidden(page, 'offcanvasCreateScenario'); await settle(page, 600);
  const S2 = sb?.scenarioId || null;
  if (!S2) { verdict('Q173-setup', false, { status: scResp.status(), errorCode: sb?.error?.code || null }); await c.close(); return; }
  writeSt({ ...st, scenario2Id: S2 });
  // O-2 (a): queued evaluationId after a FAILED scenario load (real 404: unknown scenario id), then the user opens S2 by ID
  const unknown = crypto.randomUUID(); evalGets.length = 0;
  await page.goto(det(planId, `?scenarioId=${unknown}&evaluationId=${E1}`)); await waitPlan(page); await page.locator('#scenario-error').waitFor({ state: 'visible', timeout: 15000 }).catch(() => null); await settle(page, 600);
  const a0 = { scenario: await panelState(page, 'scenario'), evaluation: await panelState(page, 'evaluation'), address: await qs(page), evalGets: [...evalGets] };
  const pA0 = await shot(page, 'q173-o2a-1-unknown-scenario');
  await page.fill('#openScenarioId', S2); await page.locator('#btnOpenScenario').click(); await page.locator('#scenario-content').waitFor({ state: 'visible', timeout: 15000 }).catch(() => null); await settle(page, 1500);
  const a1 = { scenario: await panelState(page, 'scenario'), scenarioIdShown: norm(await page.locator('#scenarioIdValue').innerText().catch(() => '')), evaluation: await panelState(page, 'evaluation'),
    evaluationIdShown: norm(await page.locator('#evaluationIdValue').innerText().catch(() => '')), evaluationScenarioShown: norm(await page.locator('#evaluationScenarioIdValue').innerText().catch(() => '')), address: await qs(page), evalGets: [...evalGets] };
  const pA1 = await shot(page, 'q173-o2a-2-after-opening-S2');
  const o2a = a1.scenario.content && a1.scenarioIdShown.includes(S2) && !a1.evaluation.content && !a1.evalGets.includes(E1.toLowerCase()) && a1.address.evaluationId !== E1;
  // O-2 (b): cross-scenario evaluationId in the address (S2 + E1, E1 belongs to S1)
  evalGets.length = 0;
  await page.goto(det(planId, `?scenarioId=${S2}&evaluationId=${E1}`)); await waitPlan(page); await page.locator('#scenario-content').waitFor({ state: 'visible', timeout: 15000 }).catch(() => null); await settle(page, 1500);
  const b1 = { scenarioIdShown: norm(await page.locator('#scenarioIdValue').innerText().catch(() => '')), evaluation: await panelState(page, 'evaluation'), evaluationIdShown: norm(await page.locator('#evaluationIdValue').innerText().catch(() => '')),
    evaluationScenarioShown: norm(await page.locator('#evaluationScenarioIdValue').innerText().catch(() => '')), address: await qs(page), evalGets: [...evalGets] };
  const pB = await shot(page, 'q173-o2b-cross-scenario-address');
  const shownUnderOther = b1.evaluation.content && b1.evaluationIdShown.includes(E1) && b1.scenarioIdShown.includes(S2);
  const o2b = !shownUnderOther;
  verdict('Q173-O-2', o2a && o2b, { parts: { a_queueNotAppliedAfterFailedScenarioLoad: o2a, b_evaluationNotShownUnderForeignScenario: o2b }, S1, S2, E1, unknownScenarioId: unknown,
    a: { afterUnknownScenario: a0, afterOpeningS2ById: a1 }, b: b1, note: 'all responses real (unknown scenario id → real safe-not-found; S2 and E1 are real records of the same plan)', evidence: [pA0, pA1, pB] });
  // O-1: evaluationId is written to the address only after a successful read-back. FAKED: the read-back GET returns 503 once.
  let failing = true; evalGets.length = 0;
  await page.route((u) => /\/evaluations\/[0-9a-f-]{36}$/i.test(u.pathname), (route) => (failing && route.request().method() === 'GET') ? route.fulfill(crafted(503, 'DEPENDENCY_UNAVAILABLE')) : route.continue());
  await page.goto(det(planId, `?scenarioId=${S2}`)); await waitPlan(page); await page.locator('#scenario-content').waitFor({ state: 'visible', timeout: 15000 }).catch(() => null); await settle(page, 600);
  const before = await qs(page);
  await page.locator('#btnOpenEvaluate').click(); await shown(page, 'offcanvasEvaluate'); await fillEvaluate(page);
  const er = postTo(page, `${API}/scenarios/${S2}/evaluations`); await page.locator('#btnSubmitEvaluate').click(); const evResp = await er; let eb = null; try { eb = await evResp.json(); } catch (_) { }
  await hidden(page, 'offcanvasEvaluate'); await page.locator('#evaluation-error').waitFor({ state: 'visible', timeout: 15000 }).catch(() => null); await settle(page, 600);
  const E2 = eb?.evaluationId || null;
  const afterFail = { address: await qs(page), evaluation: await panelState(page, 'evaluation'), readBackGets: [...evalGets] };
  const p1 = await shot(page, 'q173-o1-1-readback-failed');
  failing = false;
  const rr = page.waitForResponse((r) => /\/evaluations\/[0-9a-f-]{36}$/i.test(new URL(r.url()).pathname) && r.request().method() === 'GET', { timeout: 15000 }).catch(() => null);
  const retry = page.locator('#evaluation-error .js-retry'); if (await retry.isVisible().catch(() => false)) await retry.click(); else await page.locator('#btnRefreshEvaluation').click().catch(() => null);
  const r2 = await rr; await page.locator('#evaluation-content').waitFor({ state: 'visible', timeout: 15000 }).catch(() => null); await settle(page, 600);
  const afterOk = { address: await qs(page), evaluation: await panelState(page, 'evaluation'), retryStatus: r2 ? r2.status() : null, evaluationIdShown: norm(await page.locator('#evaluationIdValue').innerText().catch(() => '')) };
  const done = await refreshUntilCompleted(page, planId); // let the executor finish so the DB pair is stable
  const p2 = await shot(page, 'q173-o1-2-readback-ok');
  verdict('Q173-O-1', evResp.status() === 202 && !!E2 && before.evaluationId === null && afterFail.address.evaluationId === null && afterFail.evaluation.error && afterOk.retryStatus === 200 && afterOk.address.evaluationId === E2 && afterOk.evaluation.content,
    { submitStatus: evResp.status(), E2, addressBeforeSubmit: before, afterFailedReadBack: afterFail, afterSuccessfulReadBack: afterOk, completed: done, faked: 'the evaluation read-back GET is answered 503 in the browser until the retry; the POST (202) and the later GET are real', evidence: [p1, p2] });
  writeSt({ ...readSt(), evaluation2Id: E2 });
  await c.close();
}

// ───────────────────────────── nav (system tenant, read-only) ─────────────────────────────
const unx = (v) => v.replace(/&amp;/g, '&').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"');
function fragment(l) { const xml = fs.readFileSync(path.join(FRAGDIR, `SharedResource.${l}.resx.fragment.xml`), 'utf8'); const o = {};
  for (const m of xml.matchAll(/<data name="([^"]+)"[^>]*><value>([\s\S]*?)<\/value>/g)) o[m[1]] = unx(m[2]); return o; }
async function nav(browser) {
  const F = fragment(loc); const EN = fragment('en'); const wantPage = F['Nav.Page.CAPACITYPLANS']; const wantModule = F['Nav.Module.CAPACITYPLANNING'];
  const c = await ctx(browser, 'navfull'); const page = await c.newPage(); watch(page, tag);
  await page.goto(withCulture(`${WEB}/SupplyChain/Shipments`)); await settle(page, 800);
  const sb = await page.evaluate(() => { const links = Array.from(document.querySelectorAll('#layout-menu a[href]')); const a = links.find((x) => (x.getAttribute('href') || '').toLowerCase().endsWith('/supplychain/capacityplans'));
    if (a) a.scrollIntoView({ block: 'center' }); return { linkCount: links.length, found: !!a, href: a ? a.getAttribute('href') : null, text: a ? a.innerText : null }; });
  const p1 = await shot(page, 'nav-sidebar');
  const rawKey = /Nav\.(Page|Module)\.|CAPACITY_PLANS|CAPACITYPLANS|CAPACITYPLANNING/.test(sb.text || '');
  verdict('NAV-sidebar', sb.found && norm(sb.text) === wantModule && !rawKey && (loc !== 'ar' || (norm(sb.text) !== EN['Nav.Module.CAPACITYPLANNING'] && hasArabic(sb.text))),
    { sidebarEntryHref: sb.href, sidebarText: norm(sb.text), expectedFromV3Fragment: wantModule, fragmentKeyShown: 'Nav.Module.CAPACITYPLANNING (single-page module: module name shown, page name hidden — product design, Q185 D-2)',
      rawKeyShown: rawKey, sidebarLinkCount: sb.linkCount, tenant: 'platform system tenant (Q185 D-1)', evidence: [p1] });
  const data = await page.request.get(`${WEB}/TenantSearch/data`, { headers: { 'X-Correlation-Id': crypto.randomUUID() } }); let dj = null; try { dj = await data.json(); } catch (_) { }
  const inData = JSON.stringify(dj || {}).includes(JSON.stringify(wantPage).slice(1, -1));
  await page.keyboard.press('Control+k'); await page.locator('.aa-Input').waitFor({ state: 'visible', timeout: 10000 }); await page.locator('.aa-Input').fill(wantPage); await page.waitForTimeout(900);
  const items = await page.locator('.aa-Item').evaluateAll((els) => els.map((e) => ({ text: e.innerText.replace(/\s+/g, ' ').trim(), href: e.querySelector('a')?.getAttribute('href') || null })));
  const p2 = await shot(page, 'nav-ctrlk-results');
  const hit = items.findIndex((i) => (i.href || '').toLowerCase().endsWith('/supplychain/capacityplans')); let opened = null;
  if (hit >= 0) { await Promise.all([page.waitForURL((u) => u.pathname.toLowerCase() === '/supplychain/capacityplans', { timeout: 15000 }).catch(() => null), page.locator('.aa-Item').nth(hit).locator('a').first().click()]); await settle(page, 800); opened = new URL(page.url()).pathname; }
  const p3 = await shot(page, 'nav-ctrlk-opened');
  verdict('NAV-ctrlk', hit >= 0 && inData && (opened || '').toLowerCase() === '/supplychain/capacityplans' && await page.locator('#capacity-plans-entry').count() === 1 && (loc !== 'ar' || hasArabic(items[hit].text)),
    { query: wantPage, queryKey: 'Nav.Page.CAPACITYPLANS (v3 fragment value for the locale)', hitText: hit >= 0 ? items[hit].text : null, results: items.slice(0, 8), localizedNameInSearchData: inData, openedPath: opened, evidence: [p2, p3] });
  await c.close();
}

const PH = { login, happy, uipm, o3, q173, nav };
const browser = await chromium.launch({ headless: true });
try {
  if (PH[PHASE]) await PH[PHASE](browser); else throw new Error('unknown phase');
} catch (e) { result.error = String(e && e.message || e).slice(0, 1500); }
finally { await browser.close(); save(); }
process.exit(result.error || Object.values(result.cases).some((c) => c.result === 'FAIL') || result.dialogs.length || result.foreign.length ? 1 : 0);
