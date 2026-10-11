// Q65b browser harness — MOD-0186 Returns UI draft overlay v2 (8862b46e…) on BASE-STACK v2, kit slot 7.
// Copied from the Q84b S&OP harness (mvp6-q84b-sop-mac-01/lane-scripts/sop_runtime.mjs: login, helpers, hygiene) and adapted.
// Playwright (Chromium, headless). Run ONLY by the lane supervisor (task harness): it supplies ACTOR_PW_<LABEL>;
// this script never prints or writes a password, token or cookie value.
// Usage: node ret_runtime.mjs <phase> <webPort> <outDir> <stateDir> <runtimeDir> <locale> <fixtureJson>
//   login   real Web login per actor; storage state ONLY in <stateDir> (outside the repo, 0600).
//   happy   MOD-0186 RU-VS1 happy path: list → create (resolve a Delivered shipment, one line) → QuickView details (no by-ID
//           request, RU-29) → next workflow step Requested → Authorized (row menu → transition panel → showConfirm) → reload.
//           Also UI-PM-01. WRITES: 1 return (+1 entitlement) then 1 transition.
//   uipm    UI-PM-05 skeleton, UI-PM-12 failed/malformed list + retry, UI-PM-09 Add focus return, UI-PM-10 REAL 422 create. Zero write.
//   o3      O-3 (403 on create → page title focus?, no raw error) + Q174 O-1 (Add disabled after 403, informational); real 403s. Zero write.
// Crafted responses (route.fulfill / delayed route.continue) only where a real one cannot be produced; each is marked "faked".
// PNGs only via page.screenshot({ path }). All browser URLs are on 127.0.0.1:<webPort>. Never deletes a file.
import fs from 'node:fs'; import path from 'node:path'; import crypto from 'node:crypto';
import { createRequire } from 'node:module';

const [PHASE, WEBPORT, OUT, STATE, RUNTIME, LOCALE, FXJSON] = process.argv.slice(2);
const req = createRequire(path.join(RUNTIME, 'package.json'));
const { chromium } = req('playwright');
const PW_VERSION = req('playwright/package.json').version;
const WEB = `http://127.0.0.1:${WEBPORT}`;
const ENTRY = `${WEB}/SupplyChain/Returns`;
const API = '/SupplyChain/Returns/api';
const FX = FXJSON ? JSON.parse(fs.readFileSync(FXJSON, 'utf8')).fixtures : null;
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
const planFile = path.join(STATE, `return-${loc}.json`);
const iso = () => new Date().toISOString();
const RAW = /\bFORBIDDEN\b|\bForbidden\b|Permission denied|\b403\b|Exception|stack trace|"error"|\{"|DEPENDENCY_UNAVAILABLE|RETURN_QUANTITY_EXCEEDED|INVALID_REQUEST/;

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
    const u = new URL(r.url()); if (!u.pathname.startsWith('/SupplyChain/Returns/api')) return;
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
  inCreate: !!document.getElementById('offcanvasCreateEdit')?.contains(a), inTransition: !!document.getElementById('offcanvasReturnTransition')?.contains(a),
  classes: a?.className?.toString().slice(0, 80) || '', text: (a?.innerText || '').trim().slice(0, 60) }; });
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
    await page.goto(`${WEB}/account/login?tenantId=${tenant}&returnUrl=%2FSupplyChain%2FReturns`);
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

// ───────────────────────────── Returns helpers ─────────────────────────────
const isList = (r) => new URL(r.url()).pathname === API && r.request().method() === 'GET';
const ready = async (page) => { const lr = page.waitForResponse(isList, { timeout: 30000 }).catch(() => null); await page.goto(withCulture(ENTRY)); const r = await lr;
  await page.locator('#skeleton-loader').waitFor({ state: 'hidden', timeout: 20000 }).catch(() => null); await settle(page, 700); return r ? r.status() : null; };
const rowOf = (page, id) => page.locator(`#dt-returns tbody tr:has(.js-quick-view[data-return-id="${id}"])`);
async function resolveAndFill(page, shipmentId, qty, reason) {
  await page.fill('#returnShipmentId', shipmentId);
  const rr = page.waitForResponse((r) => new URL(r.url()).pathname === `${API}/shipments/${shipmentId}`, { timeout: 20000 });
  await page.locator('#btnResolveShipment').click(); const res = await rr;
  await page.locator('#returnShipmentResolved').waitFor({ state: 'visible', timeout: 15000 });
  const row = page.locator('#returnLinesBody tr').first(); await row.waitFor();
  await row.locator('.js-line-select').check(); await row.locator('.js-line-quantity').fill(qty);
  await page.fill('#returnReasonCode', reason);
  return { resolveStatus: res.status(), shipmentStatus: (await page.locator('#returnShipmentStatus').innerText()).trim(), lines: await page.locator('#returnLinesBody tr').count(),
    saveEnabled: await page.locator('#btnSaveReturn').isEnabled() };
}
const addBtn = (page) => page.locator('.add-new').first();

// ───────────────────────────── happy path (writes) ─────────────────────────────
async function happy(browser) {
  const ship = FX.shipments[loc === 'ar' ? 'DELIV-AR' : 'DELIV-EN'];
  const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, tag);
  const byId = []; page.on('request', (r) => { const u = new URL(r.url()); if (/^\/SupplyChain\/Returns\/api\/[0-9a-f-]{36}$/i.test(u.pathname) && r.method() === 'GET') byId.push(u.pathname); });
  const listStatus = await ready(page);
  const shellList = await shell(page);
  const before = { rows: await page.locator('#dt-returns tbody tr:not(:has(.dt-empty))').count(), add: await page.locator('.add-new').count(), title: (await page.locator('h5.mb-0').first().innerText()).trim() };
  const p1 = await shot(page, 'happy-01-list');
  await addBtn(page).click(); await shown(page, 'offcanvasCreateEdit');
  const res = await resolveAndFill(page, ship.shipmentId, '1', `Q65B-${loc.toUpperCase()}-DAMAGED`);
  const p2 = await shot(page, 'happy-02-create-resolved');
  const cr = page.waitForResponse((r) => new URL(r.url()).pathname === API && r.request().method() === 'POST');
  await page.locator('#btnSaveReturn').click(); const createResp = await cr;
  let created = null; try { created = await createResp.json(); } catch (_) { }
  const body = JSON.parse(createResp.request().postData() || '{}');
  await hidden(page, 'offcanvasCreateEdit'); await settle(page, 1200);
  const returnId = created?.returnId;
  await rowOf(page, returnId).waitFor({ timeout: 15000 }).catch(() => null);
  const afterCreate = { rowPresent: await rowOf(page, returnId).count() === 1, rowText: (await rowOf(page, returnId).innerText().catch(() => '')).replace(/\s+/g, ' ').slice(0, 200) };
  const p3 = await shot(page, 'happy-03-list-after-create');
  verdict('MOD0186-happy-list-create', listStatus === 200 && createResp.status() === 201 && created?.status === 'Requested' && afterCreate.rowPresent && res.saveEnabled
    && Object.keys(body).join(',') === 'shipmentId,reasonCode,lines' && body.lines.length === 1,
    { returnId, rmaNumber: created?.rmaNumber, createStatus: createResp.status(), responseStatus: created?.status, createBodyKeys: Object.keys(body), resolve: res, listStatus, before, afterCreate, evidence: [p1, p2, p3] });
  // details = QuickView (the pack's detail surface; RU-29 no by-ID request)
  await rowOf(page, returnId).locator('.js-quick-view').click(); await shown(page, 'offcanvasDetailsPreview');
  const qv = { rma: (await page.locator('#oc-rma-number').innerText()).trim(), status: (await page.locator('#oc-status').innerText()).trim(), returnIdShown: (await page.locator('#oc-return-id').innerText()).trim() };
  const p4 = await shot(page, 'happy-04-quickview');
  await page.keyboard.press('Escape'); await hidden(page, 'offcanvasDetailsPreview');
  verdict('MOD0186-happy-details', qv.rma === created?.rmaNumber && qv.returnIdShown.includes(returnId) && byId.length === 0, { qv, byIdRequests: byId.length, evidence: [p4] });
  // next workflow step: Requested → Authorized
  await rowOf(page, returnId).locator('.dropdown-toggle').click();
  await rowOf(page, returnId).locator('.js-transition[data-target-status="Authorized"]').click(); await shown(page, 'offcanvasReturnTransition');
  const pm07 = await active(page);
  const tp = { target: await page.inputValue('#transitionTarget'), occurredAtSet: (await page.inputValue('#transitionOccurredAt')).length > 0 };
  const p5 = await shot(page, 'happy-05-transition-panel');
  await page.locator('#btnSubmitTransition').click(); await page.locator('.swal2-confirm').waitFor({ state: 'visible', timeout: 10000 });
  const p6 = await shot(page, 'happy-06-transition-confirm');
  const tr = page.waitForResponse((r) => new URL(r.url()).pathname === `${API}/${returnId}/transition` && r.request().method() === 'POST');
  await page.locator('.swal2-confirm').click(); const trResp = await tr; let trBody = null; try { trBody = await trResp.json(); } catch (_) { }
  await hidden(page, 'offcanvasReturnTransition'); await settle(page, 1200);
  await ready(page);
  const afterReload = { rowPresent: await rowOf(page, returnId).count() === 1, rowText: (await rowOf(page, returnId).innerText().catch(() => '')).replace(/\s+/g, ' ').slice(0, 200) };
  await rowOf(page, returnId).locator('.js-quick-view').click(); await shown(page, 'offcanvasDetailsPreview');
  const qv2 = (await page.locator('#oc-status').innerText()).trim();
  const p7 = await shot(page, 'happy-07-after-authorize-reload');
  verdict('MOD0186-happy-authorize', trResp.status() === 200 && trBody?.status === 'Authorized' && tp.target === 'Authorized' && afterReload.rowPresent && qv2 !== qv.status,
    { transitionStatus: trResp.status(), responseStatus: trBody?.status, panel: tp, quickViewStatusBefore: qv.status, quickViewStatusAfter: qv2, afterReload, evidence: [p5, p6, p7] });
  verdict('UI-PM-01', oneShell(shellList) && dirOk(shellList), { listShell: shellList, note: 'list page with every partial (filter, table, QuickView, create, transition surface, L10n)', evidence: [p1] });
  result.info = { 'UI-PM-07 (row-menu transition panel focus, informational)': pm07 };
  fs.writeFileSync(planFile, JSON.stringify({ returnId, rmaNumber: created?.rmaNumber }) + '\n', { mode: 0o600 });
  await c.close();
}

// ───────────────────────────── UI-PM-05 / 12 / 09 / 10 (zero write) ─────────────────────────────
async function uipm(browser) {
  // UI-PM-05 — faked: the list GET is delayed 3 s in the browser (route.continue after a delay; the real response follows)
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, `${tag}-pm05`);
    await page.route((u) => u.pathname === API, async (route) => { if (route.request().method() === 'GET') await new Promise((r) => setTimeout(r, 3000)); await route.continue().catch(() => null); });
    await page.goto(withCulture(ENTRY), { waitUntil: 'domcontentloaded' });
    await page.waitForFunction(() => document.getElementById('skeleton-loader')?.style.display === 'block', null, { timeout: 8000 }).catch(() => null);
    await page.waitForTimeout(600);
    const during = await page.evaluate(() => { const e = document.getElementById('skeleton-loader'); return { display: getComputedStyle(e).display, height: e.offsetHeight, opacity: getComputedStyle(e).opacity,
      tableHidden: document.getElementById('returns-table-host').classList.contains('d-none') }; });
    const pD = await shot(page, 'uipm05-skeleton-visible');
    await page.locator('#returns-table-host').waitFor({ state: 'visible', timeout: 20000 }); await settle(page, 600);
    const after = await page.evaluate(() => getComputedStyle(document.getElementById('skeleton-loader')).display);
    const pA = await shot(page, 'uipm05-list-loaded');
    verdict('UI-PM-05', during.display === 'block' && during.height > 0 && Number(during.opacity) > 0 && during.tableHidden && after === 'none',
      { during, after, faked: 'delay only (real response)', evidence: [pD, pA] });
    await c.close(); }
  // UI-PM-12 — faked: list 500 and list 200-malformed (a real failing list cannot be produced on demand); retry against the real service
  { const out = {}; const ev = [];
    for (const kind of ['fail500', 'malformed']) {
      const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, `${tag}-pm12-${kind}`); let failing = true;
      await page.route((u) => u.pathname === API, (route) => {
        if (!failing || route.request().method() !== 'GET') return route.continue();
        if (kind === 'malformed') return route.fulfill({ status: 200, contentType: 'application/json', headers: { 'X-Correlation-Id': crypto.randomUUID() }, body: '{"unexpected":true}' });
        return route.fulfill(crafted(500, 'DEPENDENCY_UNAVAILABLE'));
      });
      await page.goto(withCulture(ENTRY)); await page.locator('#returns-error-state').waitFor({ state: 'visible', timeout: 15000 }).catch(() => null); await settle(page, 600);
      const st = await page.evaluate(() => ({ errorVisible: !document.getElementById('returns-error-state').classList.contains('d-none'),
        skeletonDisplay: getComputedStyle(document.getElementById('skeleton-loader')).display, tableHidden: document.getElementById('returns-table-host').classList.contains('d-none'),
        message: document.getElementById('returns-error-message')?.innerText || '', referenceShown: !document.getElementById('returns-error-reference').classList.contains('d-none') }));
      const text = await visibleText(page);
      ev.push(await shot(page, `uipm12-${kind}-error`));
      failing = false;
      const rr = page.waitForResponse(isList, { timeout: 15000 }).catch(() => null);
      await page.locator('#btnReturnsRetry').click(); const r = await rr; await settle(page, 800);
      out[kind] = { ...st, rawErrorInPage: RAW.test(text), retryRequest: !!r, retryStatus: r ? r.status() : null,
        tableVisibleAfterRetry: await page.locator('#returns-table-host').isVisible(), errorHiddenAfterRetry: !(await page.locator('#returns-error-state').isVisible()) };
      ev.push(await shot(page, `uipm12-${kind}-after-retry`));
      await c.close();
    }
    const ok = Object.values(out).every((o) => o.errorVisible && o.skeletonDisplay === 'none' && o.tableHidden && o.message.trim().length > 0 && !RAW.test(o.message)
      && (loc !== 'ar' || hasArabic(o.message)) && !o.rawErrorInPage && o.retryRequest && o.retryStatus === 200 && o.tableVisibleAfterRetry && o.errorHiddenAfterRetry) && out.fail500.referenceShown;
    verdict('UI-PM-12', ok, { out, faked: 'list 500 and list 200 {"unexpected":true} via route.fulfill; retry hits the real service', evidence: ev }); }
  // UI-PM-09 — stable opener Add (OD-Q164-09): Escape and close button return focus to Add
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, `${tag}-pm09`); const out = {};
    await ready(page);
    for (const how of ['escape', 'close-button']) {
      // a2: Enter on the focused DataTables Add button does not open the panel (Q64d-D3, shared template pattern); record it and
      // open with Space, so focus is still on Add when the panel opens (UI-PM-09 is about the focus return, not the key).
      await addBtn(page).focus(); await page.keyboard.press('Enter');
      const enterOpens = await page.locator('#offcanvasCreateEdit.show').waitFor({ state: 'visible', timeout: 2500 }).then(() => true).catch(() => false);
      if (!enterOpens) { await addBtn(page).focus(); await page.keyboard.press('Space'); }
      await shown(page, 'offcanvasCreateEdit');
      const inside = await active(page); inside.enterOpens = enterOpens;
      if (how === 'escape') await page.keyboard.press('Escape'); else await page.locator('#offcanvasCreateEdit [data-bs-dismiss="offcanvas"]').first().click();
      await hidden(page, 'offcanvasCreateEdit');
      out[how] = { enterOpensPanel: inside.enterOpens, focusInsideOnShown: inside.inCreate, afterHidden: await active(page) };
    }
    const p = await shot(page, 'uipm09-add-focus-returned');
    verdict('UI-PM-09', Object.values(out).every((o) => /add-new/.test(o.afterHidden.classes)), { out, scope: 'r2 / OD-F-Q145-1 / OD-Q164-09: stable opener = Add; row-menu surfaces exempt', evidence: [p] });
    await c.close(); }
  // UI-PM-10 — REAL 422 RETURN_QUANTITY_EXCEEDED on create (quantity 999 > shipped 2): focus inside the open panel, Escape closes
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, `${tag}-pm10`);
    await ready(page);
    await addBtn(page).click(); await shown(page, 'offcanvasCreateEdit');
    const res = await resolveAndFill(page, FX.shipments['DELIV-NEG'].shipmentId, '999', 'Q65B-OVER');
    const cr = page.waitForResponse((r) => new URL(r.url()).pathname === API && r.request().method() === 'POST');
    await page.locator('#btnSaveReturn').click(); const r = await cr; await page.waitForTimeout(800);
    const o = { status: r.status(), errorCode: (await r.json().catch(() => ({})))?.error?.code || null, panelOpen: await page.locator('#offcanvasCreateEdit.show').count() === 1,
      focus: await active(page), alert: (await page.locator('#formReturnAlert').innerText().catch(() => '')).trim().slice(0, 200), resolve: res };
    const p = await shot(page, 'uipm10-create-422-focus-inside');
    await page.keyboard.press('Escape'); await hidden(page, 'offcanvasCreateEdit'); o.escapeClosed = await page.locator('#offcanvasCreateEdit.show').count() === 0; o.afterEscape = await active(page);
    verdict('UI-PM-10', o.status === 422 && o.panelOpen && o.focus.inCreate && o.escapeClosed && o.alert.length > 0 && !RAW.test(o.alert) && (loc !== 'ar' || hasArabic(o.alert)),
      { out: o, exempt: 'transition submit is reached through window.showConfirm (OD-F-Q145-1); not asserted', evidence: [p] });
    await c.close(); }
}

// ───────────────────────────── O-3 + Q174 O-1 (zero write) ─────────────────────────────
async function o3(browser) {
  const out = {}; const ev = [];
  // (a) create → 403 — faked (the full actor holds create; a real 403 on this path cannot be produced for an actor who sees Add)
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, `${tag}-create403`);
    await page.route((u) => u.pathname === API, (route) => route.request().method() === 'POST' ? route.fulfill(crafted(403, 'FORBIDDEN')) : route.continue());
    await ready(page);
    await addBtn(page).click(); await shown(page, 'offcanvasCreateEdit');
    await resolveAndFill(page, FX.shipments['DELIV-NEG'].shipmentId, '1', 'Q65B-O3');
    await page.locator('#btnSaveReturn').click(); await hidden(page, 'offcanvasCreateEdit'); await page.waitForTimeout(800);
    const text = await visibleText(page);
    out.create = { focus: await active(page), titleVisible: await page.locator('h5.mb-0').first().isVisible(), title: (await page.locator('h5.mb-0').first().innerText()).trim(),
      titleFocusable: await page.evaluate(() => { const h = document.querySelector('h5.mb-0'); return !!h && (h.id !== '' || h.hasAttribute('tabindex')); }),
      addCount: await page.locator('.add-new').count(), addDisabled: await addBtn(page).isDisabled().catch(() => null), rawErrorInPage: RAW.test(text),
      toasts: result.toasts.filter((t) => t.tag === `${tag}-create403`).map((t) => t.text) };
    ev.push(await shot(page, 'o3-create-403'));
    // Q174 O-1 (informational): does Add stay disabled after the 403? (still disabled after 3 s and after a list reload via Retry-free filter apply)
    await page.waitForTimeout(3000);
    out.o1 = { addDisabledAfter3s: await addBtn(page).isDisabled().catch(() => null), addHasDisabledClass: await addBtn(page).evaluate((b) => b.classList.contains('disabled')).catch(() => null) };
    await c.close(); }
  // (b) transition → 403 — faked
  { const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, `${tag}-transition403`);
    const { returnId } = readPlan();
    await page.route((u) => /\/transition$/.test(u.pathname), (route) => route.fulfill(crafted(403, 'FORBIDDEN')));
    await ready(page);
    const row = rowOf(page, returnId); await row.locator('.dropdown-toggle').click();
    const target = await row.locator('.js-transition').first().getAttribute('data-target-status');
    await row.locator('.js-transition').first().click(); await shown(page, 'offcanvasReturnTransition');
    await page.locator('#btnSubmitTransition').click(); await page.locator('.swal2-confirm').waitFor({ state: 'visible' }); await page.locator('.swal2-confirm').click();
    await hidden(page, 'offcanvasReturnTransition'); await page.waitForTimeout(1000);
    out.transition = { target, focus: await active(page), rawErrorInPage: RAW.test(await visibleText(page)), titleVisible: await page.locator('h5.mb-0').first().isVisible() };
    ev.push(await shot(page, 'o3-transition-403'));
    await c.close(); }
  // (c) real 403s: read-only (no Add; direct POST → 403), no-read (_AccessDenied only; direct GET → 403)
  { const c = await ctx(browser, 'readonly'); const page = await c.newPage(); watch(page, `${tag}-readonly`);
    await ready(page);
    const tok = await page.locator('input[name="__RequestVerificationToken"]').first().inputValue().catch(() => null);
    const r = await page.request.post(`${WEB}${API}`, { headers: { 'Content-Type': 'application/json', 'X-Correlation-Id': crypto.randomUUID(), 'Idempotency-Key': crypto.randomUUID(), ...(tok ? { RequestVerificationToken: tok } : {}) },
      data: JSON.stringify({ shipmentId: FX.shipments['DELIV-NEG'].shipmentId, reasonCode: 'Q65B-RO', lines: [{ shipmentLineNumber: '1', quantity: '1', uomId: 'EA' }] }), maxRedirects: 0 });
    let j = null; try { j = await r.json(); } catch (_) { }
    out.readonly = { addCount: await page.locator('.add-new').count(), rows: await page.locator('#dt-returns tbody tr').count(), directPostStatus: r.status(), directPostCode: j?.error?.code || null };
    ev.push(await shot(page, 'o3-readonly-list'));
    await c.close(); }
  { const c = await ctx(browser, 'noread'); const page = await c.newPage(); watch(page, `${tag}-noread`);
    await page.goto(withCulture(ENTRY)); await settle(page);
    out.noread = { table: await page.locator('#dt-returns').count(), skeletons: await page.locator('.backbone-skeleton').count(), filter: await page.locator('#filterForm').count(),
      url: new URL(page.url()).pathname, rawErrorInPage: RAW.test(await visibleText(page)) };
    const g = await page.request.get(`${WEB}${API}`, { headers: { 'X-Correlation-Id': crypto.randomUUID() }, maxRedirects: 0 });
    out.noread.directGet = g.status();
    ev.push(await shot(page, 'o3-noread-list'));
    await c.close(); }
  const titleFocus = out.create.focus.tag === 'H5' || out.create.focus.tag === 'H4';
  const okCreate = titleFocus && out.create.titleVisible && !out.create.rawErrorInPage;
  const okReal = out.readonly.addCount === 0 && out.readonly.directPostStatus === 403 && out.noread.table === 0 && out.noread.skeletons === 0 && out.noread.directGet === 403 && out.noread.url === '/SupplyChain/Returns';
  verdict('O-3', okCreate && okReal && !out.transition.rawErrorInPage, { out, parts: { createFocusOnPageTitle: titleFocus, noRawError: !out.create.rawErrorInPage && !out.transition.rawErrorInPage, real403: okReal },
    faked: 'create 403 and transition 403 via route.fulfill; real 403s for read-only and no-read', evidence: ev });
  verdict('Q174-O-1 (informational)', true, { observed: { addDisabledRightAfter403: out.create.addDisabled, ...out.o1 }, note: 'informational only: records whether Add stays disabled after a 403 on create' });
}

const PH = { login, happy, uipm, o3 };
const browser = await chromium.launch({ headless: true });
try {
  if (PH[PHASE]) await PH[PHASE](browser); else throw new Error('unknown phase');
} catch (e) { result.error = String(e && e.message || e).slice(0, 1500); }
finally { await browser.close(); save(); }
process.exit(result.error || Object.values(result.cases).some((c) => c.result === 'FAIL') || result.dialogs.length || result.foreign.length ? 1 : 0);
