// Q84b browser harness — MOD-0190 S&OP UI draft overlay v2 (7a9666c7…) on BASE-STACK v2, kit slot 8.
// Structure derived from the Q64d Claims harness (mvp6-q64d-claims-runtime-01/lane-scripts/claims_runtime.mjs).
// Playwright (Chromium, headless). Run ONLY by the lane supervisor (task harness): it supplies ACTOR_PW_<LABEL>;
// this script never prints or writes a password, token or cookie value.
// Usage: node sop_runtime.mjs <phase> <webPort> <outDir> <stateDir> <runtimeDir> <locale> <demandId> <demandVersion> <demandChecksum>
//   login   real Web login per actor (Web → Gateway → Auth); storage state ONLY in <stateDir> (outside the repo, 0600).
//   happy   MOD-0190 happy path: entry page → create plan → details → capture snapshot → sign-off via showConfirm → reload.
//           Also UI-PM-01 (one shell on entry + details). WRITES: 1 plan, 1 snapshot, 1 sign-off.
//   uipm    UI-PM-05 skeleton visible, UI-PM-12 failed/malformed section load + retry, UI-PM-09 stable-opener focus
//           return, UI-PM-10 focus inside the panel after a REAL rejected submit (422). Zero write.
//   o3      O-3: crafted 403 on create / capture / sign-off / page load → page title shown and focused, no raw error;
//           real 403s (read-only direct POST, no-read page = _AccessDenied only). Zero write.
// Crafted responses replace API responses in the browser only (route.fulfill), never assets (Q64d A-1).
// PNGs only via page.screenshot({ path }). All browser URLs are on 127.0.0.1:<webPort>. Never deletes a file.
import fs from 'node:fs'; import path from 'node:path'; import crypto from 'node:crypto';
import { createRequire } from 'node:module';

const [PHASE, WEBPORT, OUT, STATE, RUNTIME, LOCALE, DEMAND_ID, DEMAND_VER, DEMAND_SUM] = process.argv.slice(2);
const req = createRequire(path.join(RUNTIME, 'package.json'));
const { chromium } = req('playwright');
const PW_VERSION = req('playwright/package.json').version;
const WEB = `http://127.0.0.1:${WEBPORT}`;
const ENTRY = `${WEB}/SupplyChain/SandopPlans`;
const T1 = '97c59330-dbc4-4665-b29c-0c26dbb5cc93';
const ACT = { full: ['john.doe.t97@diten.com', T1], readonly: ['jane.smith.t97@diten.com', T1], noread: ['charlie.brown.t97@diten.com', T1] };
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
const RAW = /\bFORBIDDEN\b|\bForbidden\b|\b403\b|Exception|stack trace|"error"|\{"|DEPENDENCY_UNAVAILABLE|INVALID_DEMAND_REFERENCE/;

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
    const u = new URL(r.url()); if (!u.pathname.startsWith('/SupplyChain/SandopPlans/api')) return;
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
const active = (page) => page.evaluate(() => { const a = document.activeElement; return { id: a?.id || null, tag: a?.tagName || null,
  inCreate: !!document.getElementById('offcanvasCreatePlan')?.contains(a), inCapture: !!document.getElementById('offcanvasCaptureSnapshot')?.contains(a),
  inSignOff: !!document.getElementById('offcanvasRecordSignOff')?.contains(a) }; });
const shell = (page) => page.evaluate(() => ({ layoutWrappers: document.querySelectorAll('.layout-wrapper').length, layoutMenus: document.querySelectorAll('#layout-menu').length,
  navbars: document.querySelectorAll('#layout-navbar').length, footers: document.querySelectorAll('footer.content-footer').length,
  mainJs: document.querySelectorAll('script[src*="/assets/js/main.js"]').length, htmlDir: document.documentElement.getAttribute('dir'), htmlLang: document.documentElement.getAttribute('lang') }));
const oneShell = (s) => s.layoutWrappers === 1 && s.layoutMenus === 1 && s.navbars === 1 && s.footers === 1 && s.mainJs === 1;
const dirOk = (s) => (loc === 'ar' ? s.htmlDir === 'rtl' : s.htmlDir !== 'rtl');
const visibleText = (page) => page.evaluate(() => document.body.innerText);
const hasArabic = (s) => /[؀-ۿ]/.test(s);
const shown = (page, id) => page.locator(`#${id}.show`).waitFor({ state: 'visible', timeout: 15000 });
const hidden = async (page, id) => { await page.waitForFunction((i) => { const e = document.getElementById(i); return !e || (!e.classList.contains('show') && !e.classList.contains('hiding') && !e.classList.contains('showing')); }, id, { timeout: 15000 }); await page.waitForTimeout(300); };
const crafted = (status, code) => ({ status, contentType: 'application/json', headers: { 'X-Correlation-Id': crypto.randomUUID() },
  body: JSON.stringify({ error: { code, message: code === 'FORBIDDEN' ? 'Permission denied' : 'Dependency unavailable', correlationId: crypto.randomUUID() } }) });
const readPlan = () => JSON.parse(fs.readFileSync(planFile, 'utf8'));
const settle = async (page, ms = 600) => { await page.waitForLoadState('networkidle').catch(() => null); await page.waitForTimeout(ms); };

// ───────────────────────────── login ─────────────────────────────
async function login(browser) {
  fs.mkdirSync(STATE, { recursive: true, mode: 0o700 });
  for (const [actor, [email, tenant]] of Object.entries(ACT)) {
    const c = await ctx(browser, null); const page = await c.newPage(); watch(page, `login-${actor}`);
    await page.goto(`${WEB}/account/login?tenantId=${tenant}&returnUrl=%2FSupplyChain%2FSandopPlans`);
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

// ───────────────────────────── happy path (writes) ─────────────────────────────
async function happy(browser) {
  const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, tag);
  const listGets = []; page.on('request', (r) => { const u = new URL(r.url()); if (u.pathname === '/SupplyChain/SandopPlans/api' && r.method() === 'GET') listGets.push(u.pathname); });
  await page.goto(withCulture(ENTRY)); await settle(page);
  const entryShell = await shell(page);
  const entry = { title: (await page.locator('#sandopPlansTitle').innerText()).trim(), cta: await page.locator('#btnOpenCreatePlan').count(), tables: await page.locator('table').count() };
  const pEntry = await shot(page, 'happy-01-entry');
  // create
  await page.locator('#btnOpenCreatePlan').click(); await shown(page, 'offcanvasCreatePlan');
  const name = `Q84b ${loc} plan ${crypto.randomUUID().slice(0, 8)}`;
  // unique horizon per locale run so the active-horizon unique index never collides across runs
  const start = loc === 'ar' ? '2031-01-01' : '2030-01-01'; const end = loc === 'ar' ? '2031-03-31' : '2030-03-31';
  await page.fill('#planName', name); await page.fill('#planHorizonStart', start); await page.fill('#planHorizonEnd', end);
  await page.fill('#planDemandPlanId', DEMAND_ID); await page.fill('#planDemandPlanVersion', DEMAND_VER);
  const pCreate = await shot(page, 'happy-02-create-offcanvas');
  const createResp = page.waitForResponse((r) => new URL(r.url()).pathname === '/SupplyChain/SandopPlans/api' && r.request().method() === 'POST');
  await page.locator('#btnSubmitCreatePlan').click();
  const cr = await createResp; const createStatus = cr.status(); const createBodyKeys = Object.keys(JSON.parse(cr.request().postData() || '{}'));
  await page.waitForURL(/\/SupplyChain\/SandopPlans\/Details\/[0-9a-f-]{36}$/i, { timeout: 20000 });
  const planId = page.url().split('/').pop();
  await page.locator('#summary-content').waitFor({ state: 'visible' }); await settle(page);
  const detailsShell = await shell(page);
  const draft = { title: (await page.locator('#planTitle').innerText()).trim(), status: (await page.locator('#summaryStatus').innerText()).trim(),
    captureVisible: await page.locator('#btnOpenCaptureSnapshot').isVisible(), signOffVisible: await page.locator('#btnOpenRecordSignOff').isVisible() };
  const pDraft = await shot(page, 'happy-03-details-draft');
  verdict('MOD0190-happy-create', createStatus === 201 && draft.title === name && draft.captureVisible && !draft.signOffVisible && listGets.length === 0,
    { planId, createStatus, createBodyKeys, entry, draft, listRequestsOnEntry: listGets.length, evidence: [pEntry, pCreate, pDraft] });
  // capture
  await page.locator('#btnOpenCaptureSnapshot').click(); await shown(page, 'offcanvasCaptureSnapshot');
  const prefill = { demandPlanId: await page.inputValue('#captureDemandPlanId'), demandPlanVersion: await page.inputValue('#captureDemandPlanVersion'),
    capturedAtSet: (await page.inputValue('#captureSourceCapturedAt')).length > 0 };
  await page.fill('#captureSourceChecksum', DEMAND_SUM);
  const pCap = await shot(page, 'happy-04-capture-offcanvas');
  const capResp = page.waitForResponse((r) => r.url().endsWith(`/api/${planId}/snapshots`) && r.request().method() === 'POST');
  await page.locator('#btnSubmitCaptureSnapshot').click();
  const capStatus = (await capResp).status();
  await hidden(page, 'offcanvasCaptureSnapshot');
  await page.locator('#dt-sandop-snapshots tbody tr').first().waitFor(); await settle(page, 1000);
  const afterCap = { snapshotRows: await page.locator('#dt-sandop-snapshots tbody tr:not(:has(.dt-empty))').count(), status: (await page.locator('#summaryStatus').innerText()).trim(),
    signOffVisible: await page.locator('#btnOpenRecordSignOff').isVisible() };
  const pAfterCap = await shot(page, 'happy-05-details-after-capture');
  verdict('MOD0190-happy-capture', capStatus === 201 && afterCap.snapshotRows === 1 && afterCap.signOffVisible && prefill.demandPlanId === DEMAND_ID,
    { capStatus, prefill, afterCap, evidence: [pCap, pAfterCap] });
  // sign-off through showConfirm
  await page.locator('#btnOpenRecordSignOff').click(); await shown(page, 'offcanvasRecordSignOff');
  const snapOptions = await page.locator('#signOffSnapshotId option').count();
  await page.selectOption('#signOffRole', 'Finance'); await page.selectOption('#signOffDecision', 'Approved');
  await page.fill('#signOffComment', `Q84b ${loc} sign-off`);
  const pSo = await shot(page, 'happy-06-signoff-offcanvas');
  await page.locator('#btnSubmitRecordSignOff').click();
  await page.locator('.swal2-confirm').waitFor({ state: 'visible', timeout: 10000 });
  const pConfirm = await shot(page, 'happy-07-signoff-confirm');
  const soResp = page.waitForResponse((r) => r.url().endsWith(`/api/${planId}/sign-offs`) && r.request().method() === 'POST');
  await page.locator('.swal2-confirm').click();
  const soStatus = (await soResp).status();
  await hidden(page, 'offcanvasRecordSignOff'); await settle(page, 1000);
  const afterSo = { signOffRows: await page.locator('#dt-sandop-signoffs tbody tr:not(:has(.dt-empty))').count() };
  await page.reload(); await page.locator('#summary-content').waitFor({ state: 'visible' }); await page.locator('#dt-sandop-signoffs tbody tr').first().waitFor(); await settle(page, 1000);
  const afterReload = { signOffRows: await page.locator('#dt-sandop-signoffs tbody tr:not(:has(.dt-empty))').count(),
    snapshotRows: await page.locator('#dt-sandop-snapshots tbody tr:not(:has(.dt-empty))').count(), statusBadgeClass: await page.locator('#summaryStatus .badge').getAttribute('class').catch(() => null),
    status: (await page.locator('#summaryStatus').innerText()).trim(), text: (await page.locator('#dt-sandop-signoffs tbody').innerText()).slice(0, 300) };
  const pFinal = await shot(page, 'happy-08-details-after-signoff-reload');
  verdict('MOD0190-happy-signoff', soStatus === 201 && snapOptions >= 1 && afterSo.signOffRows === 1 && afterReload.signOffRows === 1 && afterReload.snapshotRows === 1
    && /warning/.test(afterReload.statusBadgeClass || ''), { soStatus, snapshotOptions: snapOptions, afterSo, afterReload, evidence: [pSo, pConfirm, pFinal] });
  verdict('UI-PM-01', oneShell(entryShell) && oneShell(detailsShell) && dirOk(entryShell) && dirOk(detailsShell),
    { entryShell, detailsShell, note: 'entry and details render with every partial (create/capture/sign-off offcanvas, L10n); exactly one shell each', evidence: [pEntry, pFinal] });
  fs.writeFileSync(planFile, JSON.stringify({ planId, name }) + '\n', { mode: 0o600 });
  await c.close();
}

// ───────────────────────────── UI-PM-05 / 12 / 09 / 10 (zero write) ─────────────────────────────
async function uipm(browser) {
  const { planId } = readPlan(); const DET = `${ENTRY}/Details/${planId}`; const API = `/SupplyChain/SandopPlans/api/${planId}`;
  // UI-PM-05: skeleton visible while the three section loads are pending
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, `${tag}-pm05`);
    await page.route((u) => u.pathname.startsWith(API), async (route) => { if (route.request().method() === 'GET') { await new Promise((r) => setTimeout(r, 3000)); } await route.continue().catch(() => null); });
    await page.goto(withCulture(DET), { waitUntil: 'domcontentloaded' });
    await page.waitForFunction(() => !!document.getElementById('summary-skeleton') && document.getElementById('summary-skeleton').style.display === 'block', null, { timeout: 8000 }).catch(() => null);
    const during = await page.evaluate(() => ['summary', 'snapshots', 'signoffs'].map((s) => { const e = document.getElementById(`${s}-skeleton`);
      const cs = getComputedStyle(e); return { section: s, display: cs.display, height: e.offsetHeight, dNone: e.classList.contains('d-none'), contentHidden: document.getElementById(s === 'summary' ? 'summary-content' : `${s}-table-host`).classList.contains('d-none') }; }));
    const pDuring = await shot(page, 'uipm05-skeleton-visible');
    await page.locator('#summary-content').waitFor({ state: 'visible', timeout: 20000 }); await settle(page, 800);
    const after = await page.evaluate(() => ['summary', 'snapshots', 'signoffs'].map((s) => ({ section: s, display: getComputedStyle(document.getElementById(`${s}-skeleton`)).display })));
    const pAfter = await shot(page, 'uipm05-content-loaded');
    verdict('UI-PM-05', during.every((d) => d.display === 'block' && d.height > 0 && !d.dNone && d.contentHidden) && after.every((a) => a.display === 'none'),
      { during, after, method: 'GET section responses delayed 3 s in the browser (route.continue); real responses', evidence: [pDuring, pAfter] });
    await c.close(); }
  // UI-PM-12: failed (500) and malformed (200 without items) section loads → error state; retry reloads
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, `${tag}-pm12`);
    const mode = { snapshots: 'fail', signoffs: 'malformed', summary: 'fail' };
    await page.route((u) => u.pathname.startsWith(API), async (route) => {
      const p = new URL(route.request().url()).pathname; const m = route.request().method();
      if (m === 'GET' && p === `${API}/snapshots` && mode.snapshots === 'fail') return route.fulfill(crafted(500, 'DEPENDENCY_UNAVAILABLE'));
      if (m === 'GET' && p === `${API}/sign-offs` && mode.signoffs === 'malformed') return route.fulfill({ status: 200, contentType: 'application/json', headers: { 'X-Correlation-Id': crypto.randomUUID() }, body: '{"unexpected":true}' });
      if (m === 'GET' && p === API && mode.summary === 'fail') return route.fulfill(crafted(503, 'DEPENDENCY_UNAVAILABLE'));
      return route.continue();
    });
    await page.goto(withCulture(DET)); await settle(page, 1000);
    const state = await page.evaluate(() => ['summary', 'snapshots', 'signoffs'].map((s) => { const err = document.getElementById(`${s}-error`);
      return { section: s, errorVisible: !err.classList.contains('d-none') && err.offsetHeight > 0, skeletonDisplay: getComputedStyle(document.getElementById(`${s}-skeleton`)).display,
        message: err.querySelector('.js-section-error-message')?.innerText || '', referenceShown: !err.querySelector('.js-section-error-reference').classList.contains('d-none'),
        retry: !!err.querySelector('.js-retry') }; }));
    const pErr = await shot(page, 'uipm12-section-errors');
    const text = await visibleText(page);
    await c.close();
    // Q84b a2: every .js-retry calls loadAll() (details.js:712-714), so one click reloads all sections. Each section's retry is
    // therefore checked on its own page load with ONLY that section failing; the retry must re-request it and show content.
    const retried = {};
    for (const [s, kind] of [['summary', 'fail'], ['snapshots', 'fail'], ['signoffs', 'malformed']]) {
      const c2 = await ctx(browser, 'full'); const pg = await c2.newPage(); watch(pg, `${tag}-pm12-${s}`);
      const wantPath = s === 'summary' ? API : `${API}/${s === 'signoffs' ? 'sign-offs' : 'snapshots'}`; let failing = true;
      await pg.route((u) => u.pathname === wantPath, async (route) => {
        if (!failing || route.request().method() !== 'GET') return route.continue();
        if (kind === 'malformed') return route.fulfill({ status: 200, contentType: 'application/json', headers: { 'X-Correlation-Id': crypto.randomUUID() }, body: '{"unexpected":true}' });
        return route.fulfill(crafted(s === 'summary' ? 503 : 500, 'DEPENDENCY_UNAVAILABLE'));
      });
      await pg.goto(withCulture(DET)); await pg.locator(`#${s}-error`).waitFor({ state: 'visible', timeout: 15000 }); await settle(pg, 500);
      const before = { errorVisible: await pg.locator(`#${s}-error`).isVisible(), others: await pg.evaluate((x) => ['summary', 'snapshots', 'signoffs'].filter((y) => y !== x)
        .map((y) => document.getElementById(y === 'summary' ? 'summary-content' : `${y}-table-host`).classList.contains('d-none') ? `${y}:hidden` : `${y}:content`), s) };
      failing = false;
      const resp = pg.waitForResponse((r) => new URL(r.url()).pathname === wantPath && r.request().method() === 'GET', { timeout: 15000 }).catch(() => null);
      await pg.locator(`#${s}-error .js-retry`).click();
      const r = await resp; await settle(pg, 800);
      retried[s] = { kind, before, reloadRequest: !!r, status: r ? r.status() : null, contentVisible: await pg.locator(s === 'summary' ? '#summary-content' : `#${s}-table-host`).isVisible(),
        errorHidden: !(await pg.locator(`#${s}-error`).isVisible()) };
      if (s === 'snapshots') await shot(pg, 'uipm12-snapshots-after-retry');
      await c2.close();
    }
    { const c3 = await ctx(browser, 'full'); const page = await c3.newPage(); await page.goto(withCulture(DET)); await page.locator('#summary-content').waitFor({ state: 'visible' }); await settle(page, 600);
    const pRetry = await shot(page, 'uipm12-after-retry');
    const localized = state.every((s) => s.message.trim().length > 0 && !RAW.test(s.message) && (loc !== 'ar' || hasArabic(s.message)));
    verdict('UI-PM-12', state.every((s) => s.errorVisible && s.skeletonDisplay === 'none' && s.retry) && localized && state.find((s) => s.section === 'snapshots').referenceShown
      && Object.values(retried).every((r) => r.reloadRequest && r.status === 200 && r.contentVisible && r.errorHidden) && !RAW.test(text),
      { state, retried, rawErrorInPage: RAW.test(text), method: 'crafted in browser: summary 503, snapshots 500, sign-offs 200 malformed (all at once for the error states); then per section, only that section failing, retry against the real service', evidence: [pErr, pRetry, 'png/uipm12-snapshots-after-retry-' + loc + '.png'] });
    await c3.close(); } }
  // UI-PM-09: stable openers get focus back on hidden (Escape and the close button)
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, `${tag}-pm09`);
    const out = {};
    await page.goto(withCulture(ENTRY)); await settle(page);
    for (const how of ['escape', 'close-button']) {
      await page.locator('#btnOpenCreatePlan').focus(); await page.keyboard.press('Enter'); await shown(page, 'offcanvasCreatePlan');
      const inside = await active(page);
      if (how === 'escape') await page.keyboard.press('Escape'); else await page.locator('#offcanvasCreatePlan [data-bs-dismiss="offcanvas"]').first().click();
      await hidden(page, 'offcanvasCreatePlan');
      out[`create-${how}`] = { focusInsideOnShown: inside.inCreate, afterHidden: await active(page) };
    }
    const pEntry = await shot(page, 'uipm09-entry-focus-returned');
    await page.goto(withCulture(DET)); await page.locator('#summary-content').waitFor({ state: 'visible' }); await settle(page, 800);
    for (const [btn, panel, key] of [['btnOpenCaptureSnapshot', 'offcanvasCaptureSnapshot', 'inCapture'], ['btnOpenRecordSignOff', 'offcanvasRecordSignOff', 'inSignOff']]) {
      if (!(await page.locator(`#${btn}`).isVisible())) { out[panel] = { skipped: 'opener not visible for this plan state' }; continue; }
      await page.locator(`#${btn}`).focus(); await page.keyboard.press('Enter'); await shown(page, panel);
      const inside = await active(page); await page.keyboard.press('Escape'); await hidden(page, panel);
      out[panel] = { focusInsideOnShown: inside[key], afterHidden: await active(page) };
    }
    const pDet = await shot(page, 'uipm09-details-focus-returned');
    const ok = out['create-escape'].afterHidden.id === 'btnOpenCreatePlan' && out['create-close-button'].afterHidden.id === 'btnOpenCreatePlan'
      && out.offcanvasCaptureSnapshot?.afterHidden?.id === 'btnOpenCaptureSnapshot' && out.offcanvasRecordSignOff?.afterHidden?.id === 'btnOpenRecordSignOff';
    verdict('UI-PM-09', ok, { out, scope: 'r2 / OD-F-Q145-1: stable toolbar/page openers only (Create plan, Capture snapshot, Record sign-off); no row-menu surface exists', evidence: [pEntry, pDet] });
    await c.close(); }
  // UI-PM-10: REAL rejected direct submits (422) → focus inside the still-open panel; Escape still closes it
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, `${tag}-pm10`);
    const out = {};
    await page.goto(withCulture(ENTRY)); await settle(page);
    await page.locator('#btnOpenCreatePlan').click(); await shown(page, 'offcanvasCreatePlan');
    await page.fill('#planName', `Q84b ${loc} rejected ${crypto.randomUUID().slice(0, 6)}`); await page.fill('#planHorizonStart', '2032-01-01'); await page.fill('#planHorizonEnd', '2032-02-01');
    await page.fill('#planDemandPlanId', 'DP-Q84B-UNKNOWN'); await page.fill('#planDemandPlanVersion', DEMAND_VER);
    let resp = page.waitForResponse((r) => new URL(r.url()).pathname === '/SupplyChain/SandopPlans/api' && r.request().method() === 'POST');
    await page.locator('#btnSubmitCreatePlan').click(); let r = await resp; await page.waitForTimeout(700);
    out.create = { status: r.status(), errorCode: (await r.json().catch(() => ({})))?.error?.code || null, panelOpen: await page.locator('#offcanvasCreatePlan.show').count() === 1,
      focus: await active(page), alert: (await page.locator('#formCreatePlanAlert').innerText().catch(() => '')).trim().slice(0, 200), inputsKept: (await page.inputValue('#planDemandPlanId')) === 'DP-Q84B-UNKNOWN' };
    const pCreate = await shot(page, 'uipm10-create-422-focus-inside');
    await page.keyboard.press('Escape'); await hidden(page, 'offcanvasCreatePlan'); out.create.escapeClosed = await page.locator('#offcanvasCreatePlan.show').count() === 0; out.create.afterEscape = await active(page);
    await page.goto(withCulture(DET)); await page.locator('#summary-content').waitFor({ state: 'visible' }); await settle(page, 800);
    await page.locator('#btnOpenCaptureSnapshot').click(); await shown(page, 'offcanvasCaptureSnapshot');
    await page.fill('#captureSourceChecksum', 'sha256:q84b-wrong-checksum');
    resp = page.waitForResponse((x) => x.url().endsWith(`${API}/snapshots`) && x.request().method() === 'POST');
    await page.locator('#btnSubmitCaptureSnapshot').click(); r = await resp; await page.waitForTimeout(700);
    out.capture = { status: r.status(), errorCode: (await r.json().catch(() => ({})))?.error?.code || null, panelOpen: await page.locator('#offcanvasCaptureSnapshot.show').count() === 1,
      focus: await active(page), alert: (await page.locator('#formCaptureSnapshotAlert').innerText().catch(() => '')).trim().slice(0, 200) };
    const pCap = await shot(page, 'uipm10-capture-422-focus-inside');
    await page.keyboard.press('Escape'); await hidden(page, 'offcanvasCaptureSnapshot'); out.capture.escapeClosed = await page.locator('#offcanvasCaptureSnapshot.show').count() === 0;
    const okc = out.create.status === 422 && out.create.panelOpen && out.create.focus.inCreate && out.create.escapeClosed && out.create.inputsKept && !RAW.test(out.create.alert) && out.create.alert.length > 0;
    const okp = out.capture.status === 422 && out.capture.panelOpen && out.capture.focus.inCapture && out.capture.escapeClosed && !RAW.test(out.capture.alert) && out.capture.alert.length > 0;
    verdict('UI-PM-10', okc && okp, { out, exempt: 'record sign-off submit is reached through window.showConfirm (OD-F-Q145-1); not asserted', evidence: [pCreate, pCap] });
    await c.close(); }
}

// ───────────────────────────── O-3 (zero write) ─────────────────────────────
async function o3(browser) {
  const { planId } = readPlan(); const DET = `${ENTRY}/Details/${planId}`; const API = `/SupplyChain/SandopPlans/api/${planId}`;
  const out = {}; const ev = [];
  // (a) create → crafted 403
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, `${tag}-create403`);
    await page.route((u) => u.pathname === '/SupplyChain/SandopPlans/api', (route) => route.request().method() === 'POST' ? route.fulfill(crafted(403, 'FORBIDDEN')) : route.continue());
    await page.goto(withCulture(ENTRY)); await settle(page);
    await page.locator('#btnOpenCreatePlan').click(); await shown(page, 'offcanvasCreatePlan');
    await page.fill('#planName', 'Q84b o3'); await page.fill('#planHorizonStart', '2033-01-01'); await page.fill('#planHorizonEnd', '2033-02-01');
    await page.fill('#planDemandPlanId', DEMAND_ID); await page.fill('#planDemandPlanVersion', DEMAND_VER);
    await page.locator('#btnSubmitCreatePlan').click(); await hidden(page, 'offcanvasCreatePlan'); await page.waitForTimeout(500);
    const text = await visibleText(page);
    out.create = { focus: await active(page), ctaCount: await page.locator('#btnOpenCreatePlan').count(), titleVisible: await page.locator('#sandopPlansTitle').isVisible(),
      title: (await page.locator('#sandopPlansTitle').innerText()).trim(), rawErrorInPage: RAW.test(text), toasts: result.toasts.filter((t) => t.tag === `${tag}-create403`).map((t) => t.text) };
    ev.push(await shot(page, 'o3-create-403'));
    await c.close(); }
  // (b) capture → crafted 403
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, `${tag}-capture403`);
    await page.route((u) => u.pathname === `${API}/snapshots`, (route) => route.request().method() === 'POST' ? route.fulfill(crafted(403, 'FORBIDDEN')) : route.continue());
    await page.goto(withCulture(DET)); await page.locator('#summary-content').waitFor({ state: 'visible' }); await settle(page, 800);
    await page.locator('#btnOpenCaptureSnapshot').click(); await shown(page, 'offcanvasCaptureSnapshot');
    await page.fill('#captureSourceChecksum', DEMAND_SUM);
    await page.locator('#btnSubmitCaptureSnapshot').click(); await hidden(page, 'offcanvasCaptureSnapshot'); await page.waitForTimeout(500);
    const text = await visibleText(page);
    out.capture = { focus: await active(page), openerCount: await page.locator('#btnOpenCaptureSnapshot').count(), titleVisible: await page.locator('#planTitle').isVisible(),
      workspaceVisible: await page.locator('#planWorkspace').isVisible(), rawErrorInPage: RAW.test(text), toasts: result.toasts.filter((t) => t.tag === `${tag}-capture403`).map((t) => t.text) };
    ev.push(await shot(page, 'o3-capture-403'));
    await c.close(); }
  // (c) sign-off (through showConfirm) → crafted 403
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, `${tag}-signoff403`);
    await page.route((u) => u.pathname === `${API}/sign-offs`, (route) => route.request().method() === 'POST' ? route.fulfill(crafted(403, 'FORBIDDEN')) : route.continue());
    await page.goto(withCulture(DET)); await page.locator('#summary-content').waitFor({ state: 'visible' }); await settle(page, 800);
    await page.locator('#btnOpenRecordSignOff').click(); await shown(page, 'offcanvasRecordSignOff');
    await page.selectOption('#signOffRole', 'Executive'); await page.selectOption('#signOffDecision', 'Rejected');
    await page.locator('#btnSubmitRecordSignOff').click(); await page.locator('.swal2-confirm').waitFor({ state: 'visible' }); await page.locator('.swal2-confirm').click();
    await hidden(page, 'offcanvasRecordSignOff'); await page.waitForTimeout(800);
    const text = await visibleText(page);
    out.signOff = { focus: await active(page), openerCount: await page.locator('#btnOpenRecordSignOff').count(), titleVisible: await page.locator('#planTitle').isVisible(), rawErrorInPage: RAW.test(text) };
    ev.push(await shot(page, 'o3-signoff-403'));
    await c.close(); }
  // (d) page load → crafted 403 on GET plan (page-level denial)
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, `${tag}-page403`);
    await page.route((u) => u.pathname === API, (route) => route.request().method() === 'GET' ? route.fulfill(crafted(403, 'FORBIDDEN')) : route.continue());
    await page.goto(withCulture(DET)); await page.locator('#planAlert').waitFor({ state: 'visible', timeout: 15000 }); await settle(page, 600);
    const text = await visibleText(page);
    out.page = { focus: await active(page), alert: (await page.locator('#planAlert').innerText()).trim().slice(0, 200), titleVisible: await page.locator('#planTitle').isVisible(),
      title: (await page.locator('#planTitle').innerText()).trim(), workspaceHidden: !(await page.locator('#planWorkspace').isVisible()), rawErrorInPage: RAW.test(text) };
    ev.push(await shot(page, 'o3-page-403'));
    await c.close(); }
  // (e) real 403s: read-only direct create POST (Web adapter gate), no-read pages (UAS-001)
  { const c = await ctx(browser, 'readonly'); const page = await c.newPage(); watch(page, `${tag}-readonly`);
    await page.goto(withCulture(ENTRY)); await settle(page);
    const tok = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue().catch(() => null);
    const r = await page.request.post(`${ENTRY}/api`, { headers: { 'Content-Type': 'application/json', 'X-Correlation-Id': crypto.randomUUID(), 'Idempotency-Key': crypto.randomUUID(), ...(tok ? { RequestVerificationToken: tok } : {}) },
      data: JSON.stringify({ name: 'Q84b ro', horizonStart: '2034-01-01', horizonEnd: '2034-02-01', demandPlanId: DEMAND_ID, demandPlanVersion: DEMAND_VER }), maxRedirects: 0 });
    let j = null; try { j = await r.json(); } catch (_) { }
    out.readonly = { ctaCount: await page.locator('#btnOpenCreatePlan').count(), titleVisible: await page.locator('#sandopPlansTitle').isVisible(), directPostStatus: r.status(), directPostCode: j?.error?.code || null };
    ev.push(await shot(page, 'o3-readonly-entry'));
    await c.close(); }
  { const c = await ctx(browser, 'noread'); const page = await c.newPage(); watch(page, `${tag}-noread`);
    const res = {};
    for (const [k, u] of [['entry', ENTRY], ['details', DET]]) {
      await page.goto(withCulture(u)); await settle(page);
      res[k] = { entryMarkup: await page.locator('#sandop-plans-entry, #sandop-plan-details').count(), skeletons: await page.locator('.backbone-skeleton').count(),
        forms: await page.locator('#formOpenPlan, #formCreatePlan').count(), rawErrorInPage: RAW.test(await visibleText(page)), url: new URL(page.url()).pathname };
      ev.push(await shot(page, `o3-noread-${k}`));
    }
    const g = await page.request.get(`${WEB}${API}`, { headers: { 'X-Correlation-Id': crypto.randomUUID() }, maxRedirects: 0 });
    res.directGet = g.status();
    out.noread = res;
    await c.close(); }
  const okCreate = out.create.focus.id === 'sandopPlansTitle' && out.create.ctaCount === 0 && out.create.titleVisible && !out.create.rawErrorInPage;
  const okCapture = out.capture.focus.id === 'planTitle' && out.capture.openerCount === 0 && out.capture.titleVisible && out.capture.workspaceVisible && !out.capture.rawErrorInPage;
  const okPage = out.page.titleVisible && out.page.workspaceHidden && out.page.alert.length > 0 && !RAW.test(out.page.alert) && !out.page.rawErrorInPage && (loc !== 'ar' || hasArabic(out.page.alert));
  const okReal = out.readonly.ctaCount === 0 && out.readonly.directPostStatus === 403 && out.noread.entry.entryMarkup === 0 && out.noread.details.entryMarkup === 0
    && out.noread.entry.skeletons === 0 && out.noread.details.skeletons === 0 && out.noread.directGet === 403 && out.noread.entry.url === '/SupplyChain/SandopPlans';
  const okSignOff = out.signOff.openerCount === 0 && out.signOff.focus.tag !== 'BODY' && out.signOff.titleVisible && !out.signOff.rawErrorInPage;
  verdict('O-3', okCreate && okCapture && okPage && okReal, { out, parts: { create: okCreate, capture: okCapture, pageLevel: okPage, real403: okReal },
    info: { signOffViaShowConfirm: okSignOff, note: 'sign-off 403 recorded as information (showConfirm-reached submit, OD-F-Q145-1 exemption for focus)' }, evidence: ev });
}

const PH = { login, happy, uipm, o3 };
const browser = await chromium.launch({ headless: true });
try {
  if (PH[PHASE]) await PH[PHASE](browser); else throw new Error('unknown phase');
} catch (e) { result.error = String(e && e.message || e).slice(0, 1500); }
finally { await browser.close(); save(); }
process.exit(result.error || Object.values(result.cases).some((c) => c.result === 'FAIL') || result.dialogs.length || result.foreign.length ? 1 : 0);
