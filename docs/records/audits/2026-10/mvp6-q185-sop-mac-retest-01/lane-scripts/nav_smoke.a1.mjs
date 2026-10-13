// Q185 nav-smoke harness — MOD-0190 S&OP UI draft overlay v3 (716e7c4c…) on BASE-STACK v2, kit slot 6.
// Copied from the Q84b harness (mvp6-q84b-sop-mac-01/lane-scripts/sop_runtime.mjs: login, helpers, hygiene) and adapted.
// Run ONLY by the lane supervisor (task harness); it supplies ACTOR_PW_<LABEL>; nothing here prints or writes a secret.
// Usage: node nav_smoke.mjs <phase> <webPort> <outDir> <stateDir> <runtimeDir> <locale> <fragmentDir>
//   login  real Web login per actor; storage state ONLY in <stateDir> (outside the repo, 0600).
//   nav    N1 sidebar shows the S&OP page entry with the v3 fragment text for the locale (not the raw key; in ar not the
//          English/server fallback); N2 Ctrl+K finds the page by its localized name and opens it; N3 the S&OP list (entry)
//          page loads with no page errors and no native dialogs. Read-only: no product write. All real responses, nothing faked.
// PNGs only via page.screenshot({ path }). All browser URLs are on 127.0.0.1:<webPort>. Never deletes a file.
import fs from 'node:fs'; import path from 'node:path'; import crypto from 'node:crypto';
import { createRequire } from 'node:module';

const [PHASE, WEBPORT, OUT, STATE, RUNTIME, LOCALE, FRAGDIR] = process.argv.slice(2);
const req = createRequire(path.join(RUNTIME, 'package.json'));
const { chromium } = req('playwright');
const PW_VERSION = req('playwright/package.json').version;
const WEB = `http://127.0.0.1:${WEBPORT}`;
const ENTRY = `${WEB}/SupplyChain/SandopPlans`;
const T1 = '97c59330-dbc4-4665-b29c-0c26dbb5cc93', T2 = '00000000-0000-0000-0000-000000000001';
const ACT = { full: ['john.doe.def@diten.com', T2], t1full: ['john.doe.t97@diten.com', T1] };
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


// ───────────────────────────── nav smoke (read-only) ─────────────────────────────
const unx = (v) => v.replace(/&amp;/g, '&').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"');
function fragment(l) {
  const xml = fs.readFileSync(path.join(FRAGDIR, `SharedResource.${l}.resx.fragment.xml`), 'utf8'); const out = {};
  for (const m of xml.matchAll(/<data name="([^"]+)"[^>]*><value>([\s\S]*?)<\/value>/g)) out[m[1]] = unx(m[2]);
  return out;
}
const norm = (t) => (t || '').replace(/\s+/g, ' ').trim();
async function sidebar(page) {
  return page.evaluate(() => { const links = Array.from(document.querySelectorAll('#layout-menu a[href]'));
    const a = links.find((x) => (x.getAttribute('href') || '').toLowerCase().endsWith('/supplychain/sandopplans'));
    const li = a ? a.closest('li.menu-item') : null; const parent = li ? li.parentElement.closest('li.menu-item') : null;
    return { linkCount: links.length, found: !!a, href: a ? a.getAttribute('href') : null, text: a ? a.innerText : null,
      parentText: parent ? (parent.querySelector(':scope > a')?.innerText || '') : null,
      menuTexts: links.map((x) => x.innerText.replace(/\s+/g, ' ').trim()).filter(Boolean).slice(0, 60) }; });
}
async function nav(browser) {
  const F = fragment(loc); const EN = fragment('en');
  const wantPage = F['Nav.Page.SANDOPPLANS']; const wantModule = F['Nav.Module.SOPWORKFLOWSIGNOFFS'];
  const c = await ctx(browser, 'full'); const page = await c.newPage(); watch(page, tag);
  const START = `${WEB}/SupplyChain/Shipments`;
  // N1 — sidebar, on a page that is NOT the S&OP page
  await page.goto(withCulture(START)); await settle(page, 800);
  const sh = await shell(page);
  // open the module group if the entry sits in a collapsed submenu
  let sb = await sidebar(page);
  if (sb.found) { await page.evaluate(() => { const a = Array.from(document.querySelectorAll('#layout-menu a[href]')).find((x) => (x.getAttribute('href') || '').toLowerCase().endsWith('/supplychain/sandopplans'));
      let li = a.closest('li.menu-item'); while (li) { li.classList.add('open'); li = li.parentElement.closest('li.menu-item'); } a.scrollIntoView({ block: 'center' }); }); await page.waitForTimeout(400); sb = await sidebar(page); }
  const p1 = await shot(page, 'n1-sidebar');
  const rawKey = /Nav\.(Page|Module)\.|SANDOP_PLANS|SANDOPPLANS/.test(sb.text || '');
  const n1 = sb.found && norm(sb.text) === wantPage && !rawKey && (loc !== 'ar' || (norm(sb.text) !== EN['Nav.Page.SANDOPPLANS'] && hasArabic(sb.text)));
  verdict('N1', n1, { expectedFromV3Fragment: wantPage, sidebarText: norm(sb.text), href: sb.href, rawKeyShown: rawKey, englishFallbackInAr: loc === 'ar' ? norm(sb.text) === EN['Nav.Page.SANDOPPLANS'] : 'n/a',
    moduleGroupText: norm(sb.parentText), expectedModuleFromV3Fragment: wantModule, moduleGroupMatches: norm(sb.parentText).includes(wantModule), sidebarLinkCount: sb.linkCount, htmlDir: sh.htmlDir, htmlLang: sh.htmlLang,
    note: loc === 'en' ? 'en: the fragment value equals the manifest DisplayName, so en cannot tell a key hit from the fallback; ar is the discriminating locale' : 'ar: text equals the ar fragment value and differs from the manifest/English name',
    evidence: [p1] });
  // N2 — Ctrl+K: search by the localized page name, open the hit
  const data = await page.request.get(`${WEB}/TenantSearch/data`, { headers: { 'X-Correlation-Id': crypto.randomUUID() } }); let dj = null; try { dj = await data.json(); } catch (_) { }
  const flat = JSON.stringify(dj || {}); const inData = flat.includes(JSON.stringify(wantPage).slice(1, -1));
  await page.keyboard.press('Control+k'); await page.locator('.aa-Input').waitFor({ state: 'visible', timeout: 10000 });
  await page.locator('.aa-Input').fill(wantPage); await page.waitForTimeout(900);
  const items = await page.locator('.aa-Item').evaluateAll((els) => els.map((e) => ({ text: e.innerText.replace(/\s+/g, ' ').trim(), href: e.querySelector('a')?.getAttribute('href') || null })));
  const p2 = await shot(page, 'n2-ctrlk-results');
  const hitIndex = items.findIndex((i) => i.text.includes(wantPage) && (i.href || '').toLowerCase().endsWith('/supplychain/sandopplans'));
  let opened = null;
  if (hitIndex >= 0) { await Promise.all([page.waitForURL((u) => u.pathname.toLowerCase() === '/supplychain/sandopplans', { timeout: 15000 }).catch(() => null), page.locator('.aa-Item').nth(hitIndex).locator('a').first().click()]);
    await settle(page, 800); opened = new URL(page.url()).pathname; }
  const p3 = await shot(page, 'n2-ctrlk-opened');
  verdict('N2', hitIndex >= 0 && (opened || '').toLowerCase() === '/supplychain/sandopplans', { query: wantPage, searchDataStatus: data.status(), localizedNameInSearchData: inData, results: items.slice(0, 8), hitIndex, openedPath: opened, startPage: '/SupplyChain/Shipments', evidence: [p2, p3] });
  // N3 — the S&OP list (entry) page loads clean (fresh navigation)
  const errsBefore = result.console.filter((m) => m.type === 'pageerror').length;
  const resp = await page.goto(withCulture(ENTRY)); await settle(page, 1000);
  const n3 = { status: resp ? resp.status() : null, title: norm(await page.locator('#sandopPlansTitle').innerText().catch(() => '')), entryMarkup: await page.locator('#sandop-plans-entry').count(),
    openForm: await page.locator('#formOpenPlan').count(), pageErrors: result.console.filter((m) => m.type === 'pageerror').length - errsBefore, consoleErrors: result.console.filter((m) => m.type === 'error').map((m) => m.text).slice(0, 5),
    nativeDialogs: result.dialogs.length, shell: await shell(page), activeMenuItem: await page.evaluate(() => document.querySelector('#layout-menu li.menu-item.active > a')?.innerText?.trim() || null) };
  const p4 = await shot(page, 'n3-sop-list-page');
  verdict('N3', n3.status === 200 && n3.entryMarkup === 1 && n3.title.length > 0 && n3.pageErrors === 0 && result.console.filter((m) => m.type === 'pageerror').length === 0 && n3.nativeDialogs === 0 && oneShell(n3.shell) && dirOk(n3.shell)
    && (loc !== 'ar' || hasArabic(n3.title)), { ...n3, evidence: [p4] });
  await c.close();
  // contrast (informational): tenant 97c5 has no module entitlement in a lane → no S&OP sidebar entry even with the S&OP keys
  { const c2 = await ctx(browser, 't1full'); const pg = await c2.newPage(); watch(pg, `${tag}-t1`);
    await pg.goto(withCulture(ENTRY)); await settle(pg, 800); const s2 = await sidebar(pg);
    result.info = { 'contrast tenant 97c5 (no entitlement; informational)': { sopEntryInSidebar: s2.found, sidebarLinkCount: s2.linkCount, pageTitle: norm(await pg.locator('#sandopPlansTitle').innerText().catch(() => '')) } };
    await shot(pg, 'info-tenant97c5-sidebar'); await c2.close(); }
}

const PH = { login, nav };
const browser = await chromium.launch({ headless: true });
try {
  if (PH[PHASE]) await PH[PHASE](browser); else throw new Error('unknown phase');
} catch (e) { result.error = String(e && e.message || e).slice(0, 1500); }
finally { await browser.close(); save(); }
process.exit(result.error || Object.values(result.cases).some((c) => c.result === 'FAIL') || result.dialogs.length || result.foreign.length ? 1 : 0);
