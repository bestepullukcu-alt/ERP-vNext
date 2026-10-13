'use strict';
// A12 runtime VER-02 browser harness (Playwright 1.59.1, Chromium 147, headless, native macOS).
// Run by lane_supervisor.py `run` (ACTOR_PW_* in env; never logged). Usage: node harness.js <W> <E> <phase>
//   phases: vs | a12 | list | late | negctl | a08 | a09dup | a09state  (list split out of a12 after a harness page-size defect)
// One persistent browser profile per actor (W/browser/<actor>); each actor logs in through the real Web login page
// (Web -> Gateway -> Auth); no cookie is copied between profiles. PNGs are written only with page.screenshot({ path }).
const { chromium } = require('playwright');
const fs = require('fs'); const path = require('path'); const crypto = require('crypto'); const { execFileSync } = require('child_process');

const [W, E, PHASE] = process.argv.slice(2);
const WEB = 'http://127.0.0.1:5401';
const TENANT = '97c59330-dbc4-4665-b29c-0c26dbb5cc93';
const FX = JSON.parse(fs.readFileSync(path.join(E, 'raw/fixture/shipment-fixtures.json'), 'utf8')).fixtures;
const ACT = { A: { label: 'actor-a', email: 'john.doe.t97@diten.com', pw: process.env.ACTOR_PW_A },
  B: { label: 'actor-b', email: 'jane.smith.t97@diten.com', pw: process.env.ACTOR_PW_B },
  LEB: { label: 'actor-le-b', email: 'bob.johnson.t97@diten.com', pw: process.env.ACTOR_PW_LEB } };
const RAW = path.join(E, 'raw/browser'); const DBD = path.join(E, 'raw/db'); const PNG = path.join(E, 'png');
[RAW, DBD, PNG].forEach((d) => fs.mkdirSync(d, { recursive: true }));
const SNAPJS = path.join(E, 'lane-scripts/db_snapshot.js');
const BASELINE_JS = path.join(W, 'baseline/frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/details.js');
const result = { phase: PHASE, startedAt: new Date().toISOString(), cases: {}, db: [], png: [], console: [] };
const sha = (buf) => crypto.createHash('sha256').update(buf).digest('hex');

function snap(label, shipmentId) {
  const out = execFileSync('mongosh', ['--quiet', 'mongodb://127.0.0.1:34994/?directConnection=true', '--file', SNAPJS],
    { env: { ...process.env, SNAP: JSON.stringify({ label, shipmentId }) } }).toString().trim();
  fs.writeFileSync(path.join(DBD, `${label}.json`), out + '\n');
  return JSON.parse(out);
}
function assertDb(name, before, after, expect) {
  // expect: 'zero' (entire SupplyChain DB and the shipment unchanged) or {counts:{coll:+N}, state:{k:v}}
  const eq = (x, y) => JSON.stringify(x) === JSON.stringify(y);
  let pass; let detail;
  if (expect === 'zero') {
    pass = eq(before.counts, after.counts) && eq(before.dbTotals, after.dbTotals) && eq(before.state, after.state);
    detail = pass ? 'entire SupplyChain DB fingerprint, shipment counts and state unchanged' : `before=${JSON.stringify(before.dbTotals)} after=${JSON.stringify(after.dbTotals)}`;
  } else {
    const bad = [];
    for (const [k, d] of Object.entries(expect.counts || {})) if ((after.counts[k] - before.counts[k]) !== d) bad.push(`${k} delta ${after.counts[k] - before.counts[k]} != ${d}`);
    for (const [k, v] of Object.entries(expect.state || {})) if (JSON.stringify(after.state?.[k]) !== JSON.stringify(v)) bad.push(`state.${k}=${JSON.stringify(after.state?.[k])} != ${JSON.stringify(v)}`);
    pass = bad.length === 0; detail = pass ? JSON.stringify(expect) : bad.join('; ');
  }
  const row = { name, before: before.label, after: after.label, expect: typeof expect === 'string' ? expect : JSON.stringify(expect), result: pass ? 'PASS' : 'FAIL', detail };
  result.db.push(row);
  const f = path.join(DBD, 'db-assertions.tsv');
  if (!fs.existsSync(f)) fs.writeFileSync(f, 'phase\tname\tbefore\tafter\texpect\tresult\tdetail\n');
  fs.appendFileSync(f, `${PHASE}\t${name}\t${row.before}\t${row.after}\t${row.expect}\t${row.result}\t${detail}\n`);
  return pass;
}

async function context(actor) {
  const dir = path.join(W, 'browser', ACT[actor].label); fs.mkdirSync(dir, { recursive: true });
  const c = await chromium.launchPersistentContext(dir, { headless: true, timezoneId: 'UTC', locale: 'en-US', viewport: { width: 1366, height: 900 } });
  c.setDefaultTimeout(20000);
  return c;
}
function watch(page, actor, tag) {
  const net = [];
  page.on('dialog', (d) => d.accept());
  page.on('console', (m) => { if (['error', 'warning'].includes(m.type())) result.console.push({ actor: ACT[actor].label, tag, type: m.type(), text: m.text().slice(0, 300) }); });
  page.on('response', async (r) => {
    const u = new URL(r.url());
    if (!u.pathname.startsWith('/SupplyChain/Shipments/api') && !u.pathname.startsWith('/account/login')) return;
    const req = r.request(); let body = null;
    if (u.pathname.startsWith('/SupplyChain/Shipments/api')) { try { body = await r.json(); } catch (_) { } }
    net.push({ at: new Date().toISOString(), method: req.method(), path: u.pathname + u.search, status: r.status(),
      requestCorrelation: req.headers()['x-correlation-id'] || null, idempotencyKey: req.headers()['idempotency-key'] || null,
      responseCorrelation: r.headers()['x-correlation-id'] || null,
      errorCode: body?.error?.code || null, bodyCorrelation: body?.error?.correlationId || null,
      okShape: body && !body.error ? { shipmentId: body.shipmentId, status: body.status, lifecycleCorrelationId: body.lifecycleCorrelationId, idempotentReplay: body.idempotentReplay ?? null } : null });
  });
  return net;
}
async function ensureLogin(c, actor) {
  const page = await c.newPage(); const net = watch(page, actor, 'login');
  await page.goto(`${WEB}/SupplyChain/Shipments`);
  let loggedInNow = false;
  if (page.url().includes('/account/login')) {
    await page.goto(`${WEB}/account/login?tenantId=${TENANT}&returnUrl=%2FSupplyChain%2FShipments`);
    await page.fill('#email', ACT[actor].email); await page.fill('#password', ACT[actor].pw);
    await Promise.all([page.waitForURL((u) => !u.pathname.startsWith('/account/login'), { timeout: 30000 }), page.click('#loginForm button[type="submit"]')]);
    loggedInNow = true;
  }
  const cookies = (await c.cookies(WEB)).map((k) => k.name).sort();
  result.cases[`login-${ACT[actor].label}`] = { loggedInNow, landedOn: new URL(page.url()).pathname, loginResponses: net.map((n) => ({ status: n.status, path: n.path })), cookieNames: cookies };
  await page.close();
}

async function openDetail(c, actor, shipmentId, opts = {}) {
  const page = await c.newPage(); const net = watch(page, actor, opts.tag || 'detail');
  if (opts.baseline) {
    await page.route('**/assets/js/SupplyChain/Shipments/details.js*', (route) => route.fulfill({ status: 200, contentType: 'application/javascript', body: fs.readFileSync(BASELINE_JS) }));
  }
  if (opts.beforeGoto) await opts.beforeGoto(page);
  const culture = opts.culture ? `?culture=${opts.culture}` : '';
  const shell = await c.request.get(`${WEB}/SupplyChain/Shipments/Details/${shipmentId}${culture}`);
  const html = await shell.text();
  const loadingText = (html.match(/<h4 class="mb-1" id="shipmentNumber">([^<]*)<\/h4>/) || [])[1] || null;
  const apiWait = page.waitForResponse((r) => new URL(r.url()).pathname === `/SupplyChain/Shipments/api/${shipmentId}`, { timeout: 30000 }).catch(() => null);
  await page.goto(`${WEB}/SupplyChain/Shipments/Details/${shipmentId}${culture}`);
  if (!opts.noWait) {
    await apiWait;
    await page.waitForFunction((t) => { const e = document.getElementById('shipmentNumber'); return e && e.textContent.trim() !== t; }, decodeHtml(loadingText), { timeout: 15000 }).catch(() => null);
    await page.waitForTimeout(400);
  }
  return { page, net, loadingText: decodeHtml(loadingText), shellStatus: shell.status() };
}
function decodeHtml(s) { return s == null ? s : s.replace(/&#x([0-9a-f]+);/gi, (_, h) => String.fromCodePoint(parseInt(h, 16))).replace(/&#(\d+);/g, (_, d) => String.fromCodePoint(+d)).replace(/&amp;/g, '&'); }

async function probe(page, loadingText) {
  return page.evaluate((loading) => {
    const q = (s) => document.querySelector(s);
    const vis = (el) => !!el && !el.hidden && getComputedStyle(el).display !== 'none' && getComputedStyle(el).visibility !== 'hidden' && el.getClientRects().length > 0;
    const FOC = 'a[href],button,input,select,textarea,[tabindex]:not([tabindex="-1"])';
    const surf = (el) => el ? { visible: vis(el), hidden: el.hidden, inert: el.hasAttribute('inert'), ariaHidden: el.getAttribute('aria-hidden'), focusables: el.querySelectorAll(FOC).length } : null;
    const L = window.L10n || {}; const alert = q('#detailsAlert'); const host = q('#shipment-details');
    const visibleTextOf = (el) => el ? el.innerText : '';
    return { lang: document.documentElement.lang, dir: document.documentElement.dir || null, shipmentNumber: q('#shipmentNumber')?.textContent.trim(),
      alertVisible: vis(alert), alertText: alert?.textContent.trim(), row: surf(q('#shipment-details .row.g-6')),
      actions: Object.assign(surf(q('#shipmentActions')) || {}, { buttons: [...(q('#shipmentActions')?.querySelectorAll('button') || [])].map((b) => b.textContent.trim()) }),
      offTransition: surf(q('#offcanvasTransition')), offPod: surf(q('#offcanvasPod')),
      detail: { status: q('#detailStatus')?.textContent, source: q('#detailSource')?.textContent, warehouse: q('#detailWarehouse')?.textContent, root: q('#detailRoot')?.textContent,
        lineRows: q('#detailLines')?.rows.length ?? null, podEmptyVisible: vis(q('#podEmpty')), podDetailsVisible: vis(q('#podDetails')),
        podRecipient: q('#podRecipient')?.textContent, podEvidence: q('#podEvidence')?.textContent },
      l10n: { notFound: L.notFound, supportReference: L.supportReference },
      loadingVisibleAnywhere: loading ? document.body.innerText.includes(loading) : null,
      visibleDetailText: visibleTextOf(host) };
  }, loadingText);
}
async function tabWalk(page, n = 45) {
  await page.evaluate(() => { document.activeElement?.blur?.(); window.scrollTo(0, 0); });
  const seq = [];
  for (let i = 0; i < n; i += 1) {
    await page.keyboard.press('Tab');
    seq.push(await page.evaluate(() => { const a = document.activeElement;
      return { tag: a?.tagName, id: a?.id || null, text: (a?.innerText || a?.value || '').trim().slice(0, 30),
        inDetailSurface: !!a?.closest?.('#shipment-details .row.g-6, #shipmentActions, #offcanvasTransition, #offcanvasPod') }; }));
  }
  const programmatic = await page.evaluate(() => { const b = document.querySelector('#offcanvasTransition button[type="submit"], #offcanvasPod button[type="submit"]');
    if (!b) return null; b.focus(); return { attempted: b.closest('.offcanvas').id, focusMoved: document.activeElement === b }; });
  return { presses: n, reachedDetailSurface: seq.filter((s) => s.inDetailSurface).length, sequence: seq, programmaticFocusIntoHidden: programmatic };
}
async function shot(page, name) {
  const file = path.join(PNG, `${name}.png`);
  await page.screenshot({ path: file, fullPage: true });
  const buf = fs.readFileSync(file);
  result.png.push({ file: `png/${name}.png`, sha256: sha(buf), bytes: buf.length, url: page.url(), capturedAt: new Date().toISOString(), method: 'page.screenshot({path, fullPage:true})' });
}
function safeNotFoundVerdict(p, net, shipmentId, loadingText, pick) {
  const calls = net.filter((n) => n.path === `/SupplyChain/Shipments/api/${shipmentId}` && n.method === 'GET');
  const call = pick ? calls.filter(pick).pop() : calls.pop();
  const corr = call?.bodyCorrelation || call?.responseCorrelation;
  const expectedAlert = `${p.l10n.notFound}${corr ? ` ${p.l10n.supportReference}: ${corr}` : ''}`;
  const checks = {
    http404: call?.status === 404, code: call?.errorCode === 'SHIPMENT_NOT_FOUND',
    correlationEchoed: !!call && call.requestCorrelation === call.responseCorrelation && call.responseCorrelation === call.bodyCorrelation,
    headingIsLocalizedNotFound: p.shipmentNumber === p.l10n.notFound && !!p.l10n.notFound,
    alertExactSupportReference: p.alertVisible && p.alertText === expectedAlert,
    noLoadingText: p.loadingVisibleAnywhere === false,
    summaryLinesPodHidden: p.row && !p.row.visible && p.row.hidden && p.row.inert && p.row.ariaHidden === 'true',
    actionsHiddenEmpty: !p.actions.visible && p.actions.hidden && p.actions.inert && p.actions.buttons.length === 0,
    offcanvasesHiddenInert: [p.offTransition, p.offPod].every((o) => o && !o.visible && o.hidden && o.inert && o.ariaHidden === 'true'),
    noShipmentValues: !p.detail.status && !p.detail.source && !p.detail.root && p.detail.lineRows === 0,
  };
  return { pass: Object.values(checks).every(Boolean), checks, call, expectedAlert };
}

async function caseSafe404(name, actor, c, shipmentId, dbShipmentId, opts = {}) {
  const before = snap(`${PHASE}-${name}-before`, dbShipmentId);
  const { page, net, loadingText, shellStatus } = await openDetail(c, actor, shipmentId, opts);
  const p = await probe(page, loadingText);
  const verdict = safeNotFoundVerdict(p, net, shipmentId, loadingText);
  const keyboard = await tabWalk(page);
  verdict.checks.keyboardNeverReachesHiddenSurfaces = keyboard.reachedDetailSurface === 0 && (!keyboard.programmaticFocusIntoHidden || keyboard.programmaticFocusIntoHidden.focusMoved === false);
  verdict.checks.hiddenSurfacesHaveFocusables = (p.offTransition?.focusables || 0) + (p.offPod?.focusables || 0) > 0;
  verdict.pass = Object.values(verdict.checks).every(Boolean);
  await shot(page, `${PHASE}-${name}`);
  const after = snap(`${PHASE}-${name}-after`, dbShipmentId);
  const dbPass = assertDb(name, before, after, 'zero');
  result.cases[name] = { actor: ACT[actor].label, shipmentId, culture: opts.culture || 'en', shellStatus, loadingText, verdict: verdict.pass && dbPass ? 'PASS' : 'FAIL',
    checks: verdict.checks, expectedAlert: verdict.expectedAlert, probe: p, keyboard, network: net, dbZeroWrite: dbPass };
  await page.close();
  return result.cases[name];
}
async function caseNormal(name, actor, c, key, expect) {
  const f = FX[key]; const before = snap(`${PHASE}-${name}-before`, f.shipmentId);
  const { page, net, loadingText, shellStatus } = await openDetail(c, actor, f.shipmentId);
  const p = await probe(page, loadingText); const keyboard = await tabWalk(page, 30);
  const call = net.filter((n) => n.path === `/SupplyChain/Shipments/api/${f.shipmentId}` && n.method === 'GET').pop();
  const checks = { http200: call?.status === 200, correlationEchoed: call?.requestCorrelation === call?.responseCorrelation,
    heading: !!p.shipmentNumber && p.shipmentNumber !== loadingText && p.shipmentNumber !== p.l10n.notFound,
    summary: p.detail.status === expect.status && p.detail.source === f.sourceDocumentId && p.detail.root === f.root,
    lines: p.detail.lineRows === 2, pod: expect.pod ? (p.detail.podDetailsVisible && !p.detail.podEmptyVisible && p.detail.podRecipient === expect.pod) : (p.detail.podEmptyVisible && !p.detail.podDetailsVisible),
    actions: JSON.stringify(p.actions.buttons) === JSON.stringify(expect.actions), surfacesVisible: p.row.visible && !p.row.inert && p.actions.visible !== false,
    alertHidden: !p.alertVisible, keyboardReachesActions: expect.actions.length === 0 || keyboard.reachedDetailSurface > 0 };
  await shot(page, `${PHASE}-${name}`);
  const after = snap(`${PHASE}-${name}-after`, f.shipmentId); const dbPass = assertDb(name, before, after, 'zero');
  result.cases[name] = { actor: ACT[actor].label, shipmentId: f.shipmentId, shellStatus, verdict: Object.values(checks).every(Boolean) && dbPass ? 'PASS' : 'FAIL', checks, probe: p,
    keyboard: { reachedDetailSurface: keyboard.reachedDetailSurface }, network: net, dbZeroWrite: dbPass };
  await page.close();
  return result.cases[name];
}
function mongoEval(js) { return execFileSync('mongosh', ['--quiet', 'mongodb://127.0.0.1:34994/?directConnection=true', '--eval', js]).toString().trim(); }
function softDelete(shipmentId) {
  // lane-DB fixture step (no delete endpoint exists); DeletedAt uses the same BSON type as UpdatedAt/CreatedAt.
  return mongoEval(`if (db.serverCmdLineOpts().parsed.net.port!==34994) throw new Error('port');
    const c=db.getSiblingDB('DitenSupplyChain_ShipmentA12RtVer02').sce_shipments; const s=c.findOne({_id:'${shipmentId}'});
    const when = (s.CreatedAt instanceof Date) ? new Date() : s.CreatedAt;
    const r=c.updateOne({_id:'${shipmentId}', IsDeleted:false},{$set:{IsDeleted:true, DeletedAt: when}});
    print(JSON.stringify({matched:r.matchedCount, modified:r.modifiedCount, createdAtType: (s.CreatedAt instanceof Date)?'Date':typeof s.CreatedAt}));`);
}
async function confirmDialog(page) {
  const btn = page.locator('.swal2-confirm');
  try { await btn.waitFor({ state: 'visible', timeout: 4000 }); await btn.click(); } catch (_) { /* window.confirm fallback is auto-accepted */ }
}
const future = (iso, minutes) => new Date(new Date(iso).getTime() + minutes * 60000).toISOString().slice(0, 16);
async function waitApi(page, id, kind) { return page.waitForResponse((r) => new URL(r.url()).pathname === `/SupplyChain/Shipments/api/${id}/${kind}` && r.request().method() === 'POST', { timeout: 30000 }); }
async function waitReload(page, id) { return page.waitForResponse((r) => new URL(r.url()).pathname === `/SupplyChain/Shipments/api/${id}` && r.request().method() === 'GET', { timeout: 30000 }); }

// ---------------------------------------------------------------------------------------------------- phases
async function main() {
  const ctx = {};
  const need = { vs: ['A'], a12: ['A', 'LEB'], late: ['A'], negctl: ['LEB', 'A'], list: ['A', 'LEB'], a08: ['A', 'B'], a09dup: ['A', 'B'], a09state: ['A', 'B'] }[PHASE];
  for (const a of need) { ctx[a] = await context(a); await ensureLogin(ctx[a], a); }
  try {
    if (PHASE === 'vs') {
      await caseNormal('vertical-slice-normal-detail', 'A', ctx.A, 'VS', { status: 'Draft', actions: ['Change Status'], pod: null });
    }
    if (PHASE === 'a12') {
      await caseNormal('normal-detail-draft', 'A', ctx.A, 'NORMAL', { status: 'Draft', actions: ['Change Status'], pod: null });
      await caseNormal('normal-detail-delivered-pod', 'A', ctx.A, 'PODVIEW', { status: 'Delivered', actions: ['Change Status'], pod: 'Recipient PODVIEW' });
      for (const cul of ['en', 'tr', 'ar']) await caseSafe404(`cross-le-${cul}`, 'LEB', ctx.LEB, FX.NORMAL.shipmentId, FX.NORMAL.shipmentId, { culture: cul === 'en' ? null : cul });
      const unknown = crypto.randomUUID();
      await caseSafe404('unknown-actor-le-b', 'LEB', ctx.LEB, unknown, null);
      await caseSafe404('unknown-actor-a', 'A', ctx.A, crypto.randomUUID(), null);
      // soft-deleted: first prove actor-a (owner LE) sees it, then soft-delete in the lane DB, then detail must be safe 404
      await caseNormal('deleted-fixture-visible-before-delete', 'A', ctx.A, 'DELETED', { status: 'Draft', actions: ['Change Status'], pod: null });
      const bDel = snap('a12-softdelete-before', FX.DELETED.shipmentId);
      result.cases['soft-delete-fixture-step'] = { lane_db_update: JSON.parse(softDelete(FX.DELETED.shipmentId)) };
      const aDel = snap('a12-softdelete-after', FX.DELETED.shipmentId);
      assertDb('soft-delete-fixture-step', bDel, aDel, { counts: {}, state: { isDeleted: true } });
      await caseSafe404('soft-deleted-actor-a', 'A', ctx.A, FX.DELETED.shipmentId, FX.DELETED.shipmentId);
      // data/API isolation: actor-le-b same-origin adapter mutations on the LE-A shipment -> 404, zero write
      const before = snap('a12-leb-mutation-before', FX.NORMAL.shipmentId);
      const { page } = await openDetail(ctx.LEB, 'LEB', FX.NORMAL.shipmentId, { tag: 'leb-mutation' });
      const mut = await page.evaluate(async ({ id, root }) => {
        const tok = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const post = async (kind, body, key) => { const r = await fetch(`/SupplyChain/Shipments/api/${id}/${kind}`, { method: 'POST', credentials: 'same-origin',
          headers: { 'Content-Type': 'application/json', RequestVerificationToken: tok, 'Idempotency-Key': key, 'X-Correlation-Id': root }, body: JSON.stringify(body) });
          let b = null; try { b = await r.json(); } catch (_) { } return { status: r.status, code: b?.error?.code || null, bodyCorrelation: b?.error?.correlationId || null, headerCorrelation: r.headers.get('X-Correlation-Id'), key }; };
        return { antiForgeryPresent: !!tok,
          transition: await post('transition', { targetStatus: 'Planned', occurredAt: new Date().toISOString(), reasonCode: null, note: 'A12 VER-02 cross-LE probe' }, 'c1000000-0000-4000-8000-0000000a1202'),
          pod: await post('pod', { recipientName: 'Cross LE Probe', receivedAt: new Date().toISOString(), evidenceReferenceIds: ['A12V02-CROSS-LE'], note: null }, 'c2000000-0000-4000-8000-0000000a1202') };
      }, { id: FX.NORMAL.shipmentId, root: FX.NORMAL.root });
      await page.close();
      const after = snap('a12-leb-mutation-after', FX.NORMAL.shipmentId);
      const dbOk = assertDb('leb-cross-le-mutations', before, after, 'zero');
      const ok = mut.transition.status === 404 && mut.transition.code === 'SHIPMENT_NOT_FOUND' && mut.pod.status === 404 && mut.pod.code === 'SHIPMENT_NOT_FOUND'
        && mut.transition.bodyCorrelation === FX.NORMAL.root && mut.pod.bodyCorrelation === FX.NORMAL.root;
      result.cases['cross-le-mutation-isolation'] = { actor: 'actor-le-b', verdict: ok && dbOk ? 'PASS' : 'FAIL', mutations: mut, dbZeroWrite: dbOk };
    }
    if (PHASE === 'list') {
      // list isolation: actor-le-b list must not contain LE-A fixtures; actor-a list must
      const listFor = async (actor, c) => { const p2 = await c.newPage(); await p2.goto(`${WEB}/SupplyChain/Shipments`);
        const r = await p2.evaluate(async () => { const x = await fetch('/SupplyChain/Shipments/api?page=1&pageSize=50', { credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest', 'X-Correlation-Id': crypto.randomUUID() } }); let b = null; try { b = await x.json(); } catch (_) { } return { status: x.status, errorCode: b?.error?.code || null, shape: b ? Object.keys(b) : null, items: (b?.items || b?.data || []).map((i) => i.sourceDocumentId) }; });
        await p2.close(); return r; };
      const la = await listFor('A', ctx.A); const lb = await listFor('LEB', ctx.LEB);
      const leaSources = Object.values(FX).map((f) => f.sourceDocumentId);
      result.cases['list-isolation'] = { verdict: la.status === 200 && lb.status === 200 && leaSources.filter((s) => s !== FX.DELETED.sourceDocumentId).every((s) => la.items.includes(s)) && lb.items.filter((s) => leaSources.includes(s)).length === 0 ? 'PASS' : 'FAIL',
        actorA: { status: la.status, count: la.items.length, containsAllLiveFixtures: leaSources.filter((s) => s !== FX.DELETED.sourceDocumentId).every((s) => la.items.includes(s)), containsDeleted: la.items.includes(FX.DELETED.sourceDocumentId) },
        actorLeB: { status: lb.status, count: lb.items.length, leAFixturesVisible: lb.items.filter((s) => leaSources.includes(s)).length } };
    }
    if (PHASE === 'late') {
      // Late async: hold the FIRST detail GET (a real 200 captured while the shipment still exists), trigger a second load
      // after the shipment is soft-deleted (real 404), then release the stale 200. The load-version guard must drop it.
      const f = process.env.LATE_FIXTURE ? JSON.parse(fs.readFileSync(path.join(E, 'raw/fixture', process.env.LATE_FIXTURE), 'utf8')).fixtures.LATE : FX.LATE; let held = null;
      result.lateFixtureSource = process.env.LATE_FIXTURE || 'shipment-fixtures.json'; let release; const gate = new Promise((r) => { release = r; });
      const before = snap('late-before', f.shipmentId);
      const { page, net, loadingText } = await openDetail(ctx.A, 'A', f.shipmentId, { noWait: true, tag: 'late',
        beforeGoto: async (pg) => { await pg.route(`**/SupplyChain/Shipments/api/${f.shipmentId}`, async (route) => {
          if (!held) { const resp = await route.fetch(); held = { status: resp.status(), at: new Date().toISOString() }; await gate; await route.fulfill({ response: resp }); held.releasedAt = new Date().toISOString(); }
          else { await route.continue(); } }); } });
      for (let i = 0; i < 50 && !held; i += 1) await page.waitForTimeout(100);
      const stage1 = await probe(page, loadingText);
      result.cases['late-soft-delete-step'] = { lane_db_update: JSON.parse(softDelete(f.shipmentId)) };
      const mid = snap('late-after-softdelete', f.shipmentId);
      const second = waitReload(page, f.shipmentId);
      await page.evaluate(() => ShipmentDetails.init());
      await second; await page.waitForTimeout(500);
      const stage2 = await probe(page, loadingText);
      release(); await page.waitForTimeout(1500);
      const stage3 = await probe(page, loadingText);
      const v = safeNotFoundVerdict(stage3, net, f.shipmentId, loadingText, (n) => n.status === 404); // the 404 that rendered; the stale 200 arrives later by design
      const firstCall = net.find((n) => n.path === `/SupplyChain/Shipments/api/${f.shipmentId}`);
      const checks = { firstResponseWas200Held: held?.status === 200, secondResponse404: v.checks.http404 && v.checks.code, finalIsSafeNotFound: v.pass,
        staleDataNotRendered: stage3.detail.status === '' && stage3.detail.source === '' && stage3.detail.lineRows === 0, stage2AlreadySafe: stage2.shipmentNumber === stage2.l10n.notFound };
      await shot(page, 'late-final-after-stale-200-released');
      const after = snap('late-after', f.shipmentId);
      assertDb('late-softdelete-fixture', before, mid, { counts: {}, state: { isDeleted: true } });
      const dbOk = assertDb('late-browser-zero-write', mid, after, 'zero');
      result.cases['late-async-stale-response'] = { actor: 'actor-a', verdict: Object.values(checks).every(Boolean) && dbOk ? 'PASS' : 'FAIL', checks, held, safe404Checks: v.checks, firstCall,
        stages: { beforeRelease_initial: { shipmentNumber: stage1.shipmentNumber }, afterSecond404: { shipmentNumber: stage2.shipmentNumber, rowVisible: stage2.row.visible }, afterStale200Released: stage3 }, network: net };
      await page.close();
    }
    if (PHASE === 'negctl') {
      // Sabotage control: the SAME detector against the pre-A12 details.js (baseline 1f7b36fc…, served by a test route only)
      // must reproduce the original FAIL; the A12 successor must pass (a12 phase). Zero write either way.
      const r = await caseSafe404('sabotage-baseline-detailsjs-cross-le', 'LEB', ctx.LEB, FX.NORMAL.shipmentId, FX.NORMAL.shipmentId, { baseline: true });
      r.expectation = 'detector must FAIL on the baseline (original A12 defect reproduced)';
      r.controlResult = r.verdict === 'FAIL' && (!r.checks.summaryLinesPodHidden || !r.checks.noLoadingText) ? 'PASS (defect reproduced, detector sensitive)' : 'FAIL (detector blind)';
      const late = FX.VS; // late-async sabotage: baseline has no version guard -> stale 200 re-exposes surfaces
      let held = null; let release; const gate = new Promise((res) => { release = res; });
      const unknownId = late.shipmentId;
      const { page, net, loadingText } = await openDetail(ctx.A, 'A', unknownId, { noWait: true, baseline: true, tag: 'late-sabotage',
        beforeGoto: async (pg) => { await pg.route(`**/SupplyChain/Shipments/api/${unknownId}`, async (route) => {
          if (!held) { const resp = await route.fetch(); held = { status: resp.status() }; await gate; await route.fulfill({ response: resp }); }
          else { await route.fulfill({ status: 404, contentType: 'application/json', headers: { 'X-Correlation-Id': '00000000-0000-4000-8000-00000000c0de' }, body: JSON.stringify({ error: { code: 'SHIPMENT_NOT_FOUND', message: 'SHIPMENT_NOT_FOUND', correlationId: '00000000-0000-4000-8000-00000000c0de' }, contractVersion: 'v1' }) }); } }); } });
      for (let i = 0; i < 50 && !held; i += 1) await page.waitForTimeout(100);
      const second = waitReload(page, unknownId).catch(() => null);
      await page.evaluate(() => ShipmentDetails.init()); await second; await page.waitForTimeout(400);
      release(); await page.waitForTimeout(1500);
      const s = await probe(page, loadingText);
      result.cases['sabotage-baseline-late-async'] = { note: 'baseline details.js + synthetic 404 for the second load (test route only; VS shipment untouched)', held,
        staleSurfaceReExposed: s.row.visible && s.detail.status !== '', probe: { shipmentNumber: s.shipmentNumber, rowVisible: s.row.visible, status: s.detail.status },
        controlResult: s.row.visible && s.detail.status !== '' ? 'PASS (baseline re-exposes stale data; detector sensitive)' : 'INCONCLUSIVE' };
      await page.close();
    }
    if (PHASE === 'a08') {
      const f = FX.A08; const A = await openDetail(ctx.A, 'A', f.shipmentId, { tag: 'a08-a' }); const B = await openDetail(ctx.B, 'B', f.shipmentId, { tag: 'a08-b' });
      const init = { a: (await probe(A.page, A.loadingText)).detail.status, b: (await probe(B.page, B.loadingText)).detail.status };
      await B.page.click('#shipmentActions button'); await B.page.waitForSelector('#offcanvasTransition.show');
      await B.page.selectOption('#targetStatus', 'Planned'); await B.page.fill('#occurredAt', future(f.lastEventAt, 10));
      const s0 = snap('a08-before-actor-a', f.shipmentId);
      await A.page.click('#shipmentActions button'); await A.page.waitForSelector('#offcanvasTransition.show');
      await A.page.selectOption('#targetStatus', 'Planned'); await A.page.fill('#occurredAt', future(f.lastEventAt, 10));
      let wa = waitApi(A.page, f.shipmentId, 'transition'); let ra = waitReload(A.page, f.shipmentId);
      await A.page.click('#formTransition button[type="submit"]'); await confirmDialog(A.page); const ar = await wa; await ra; await A.page.waitForTimeout(500);
      const aAfter = await probe(A.page, A.loadingText);
      const s1 = snap('a08-after-actor-a', f.shipmentId);
      const wb = waitApi(B.page, f.shipmentId, 'transition'); const rb = waitReload(B.page, f.shipmentId);
      await B.page.click('#formTransition button[type="submit"]'); await confirmDialog(B.page); const br = await wb; await rb; await B.page.waitForTimeout(700);
      const bAfter = await probe(B.page, B.loadingText);
      const bAlert = await B.page.locator('#transitionAlert').innerText().catch(() => null);
      await shot(B.page, 'a08-actor-b-after-stale-transition');
      const s2 = snap('a08-after-actor-b', f.shipmentId);
      const dbA = assertDb('a08-actor-a-commit', s0, s1, { counts: { sce_shipment_history: 1, sce_shipment_receipts: 1, sce_shipment_audit: 1, sce_shipment_outbox: 1 }, state: { status: 'Planned' } });
      const dbB = assertDb('a08-actor-b-stale-zero-write', s1, s2, 'zero');
      const bNet = B.net.filter((n) => n.path.endsWith('/transition')).pop();
      const checks = { bothRenderedDraft: init.a === 'Draft' && init.b === 'Draft', actorACommitted: ar.status() === 200 && aAfter.detail.status === 'Planned',
        actorBGot422: bNet?.status === 422 && bNet?.errorCode === 'INVALID_SHIPMENT_TRANSITION', actorBRootReference: bNet?.bodyCorrelation === f.root && (bAlert || '').includes(f.root),
        actorBRefreshedToPlanned: bAfter.detail.status === 'Planned' && bAfter.row.visible, actorBActionsAfterRefresh: JSON.stringify(bAfter.actions.buttons) === JSON.stringify(['Change Status']), db: dbA && dbB };
      result.cases['A08-stale-transition'] = { verdict: Object.values(checks).every(Boolean) ? 'PASS' : 'FAIL', checks, initial: init, actorBAlert: bAlert, actorBRequest: bNet, actorARequest: A.net.filter((n) => n.path.endsWith('/transition')).pop() };
      await A.page.close(); await B.page.close();
    }
    if (PHASE === 'a09dup' || PHASE === 'a09state') {
      const f = PHASE === 'a09dup' ? FX.A09DUP : FX.A09STATE;
      const A = await openDetail(ctx.A, 'A', f.shipmentId, { tag: `${PHASE}-a` }); const B = await openDetail(ctx.B, 'B', f.shipmentId, { tag: `${PHASE}-b` });
      const init = { a: (await probe(A.page, A.loadingText)).detail.status, b: (await probe(B.page, B.loadingText)).detail.status };
      await B.page.locator('#shipmentActions button', { hasText: 'Capture' }).click(); await B.page.waitForSelector('#offcanvasPod.show');
      await B.page.fill('#recipientName', 'Recipient B (stale)'); await B.page.fill('#receivedAt', future(f.lastEventAt, 20)); await B.page.fill('#evidenceReferenceIds', `EVID-${PHASE}-B`);
      const s0 = snap(`${PHASE}-before-actor-a`, f.shipmentId);
      let ar;
      if (PHASE === 'a09dup') {
        await A.page.locator('#shipmentActions button', { hasText: 'Capture' }).click(); await A.page.waitForSelector('#offcanvasPod.show');
        await A.page.fill('#recipientName', 'Recipient A'); await A.page.fill('#receivedAt', future(f.lastEventAt, 10)); await A.page.fill('#evidenceReferenceIds', `EVID-${PHASE}-A`);
        const wa = waitApi(A.page, f.shipmentId, 'pod'); const ra = waitReload(A.page, f.shipmentId);
        await A.page.click('#formPod button[type="submit"]'); await confirmDialog(A.page); ar = await wa; await ra;
      } else {
        await A.page.locator('#shipmentActions button', { hasText: 'Change' }).click(); await A.page.waitForSelector('#offcanvasTransition.show');
        await A.page.selectOption('#targetStatus', 'Exception'); await A.page.fill('#occurredAt', future(f.lastEventAt, 10));
        const wa = waitApi(A.page, f.shipmentId, 'transition'); const ra = waitReload(A.page, f.shipmentId);
        await A.page.click('#formTransition button[type="submit"]'); await confirmDialog(A.page); ar = await wa; await ra;
      }
      await A.page.waitForTimeout(500); const aAfter = await probe(A.page, A.loadingText);
      const s1 = snap(`${PHASE}-after-actor-a`, f.shipmentId);
      const wb = waitApi(B.page, f.shipmentId, 'pod'); const rb = waitReload(B.page, f.shipmentId);
      await B.page.click('#formPod button[type="submit"]'); await confirmDialog(B.page); await wb; await rb; await B.page.waitForTimeout(700);
      const bAfter = await probe(B.page, B.loadingText); const bAlert = await B.page.locator('#podAlert').innerText().catch(() => null);
      await shot(B.page, `${PHASE}-actor-b-after-stale-pod`);
      const s2 = snap(`${PHASE}-after-actor-b`, f.shipmentId);
      const bNet = B.net.filter((n) => n.path.endsWith('/pod')).pop();
      const dup = PHASE === 'a09dup';
      const dbA = assertDb(`${PHASE}-actor-a-commit`, s0, s1, dup
        ? { counts: { sce_shipment_history: 1, sce_shipment_receipts: 1, sce_shipment_audit: 1 }, state: { status: 'Delivered' } }
        : { counts: { sce_shipment_history: 1, sce_shipment_receipts: 1, sce_shipment_audit: 1, sce_shipment_outbox: 1 }, state: { status: 'Exception' } });
      const dbB = assertDb(`${PHASE}-actor-b-stale-zero-write`, s1, s2, 'zero');
      const checks = { bothRenderedDispatched: init.a === 'Dispatched' && init.b === 'Dispatched', actorACommitted: [200, 201].includes(ar.status()) && aAfter.detail.status === (dup ? 'Delivered' : 'Exception'),
        actorBExactError: dup ? (bNet?.status === 409 && bNet?.errorCode === 'POD_ALREADY_CAPTURED') : (bNet?.status === 422 && bNet?.errorCode === 'INVALID_SHIPMENT_TRANSITION'),
        actorBRootReference: bNet?.bodyCorrelation === f.root && (bAlert || '').includes(f.root),
        actorBRefreshed: bAfter.detail.status === (dup ? 'Delivered' : 'Exception') && bAfter.row.visible,
        actorBPodActionGone: !bAfter.actions.buttons.some((t) => /Capture/i.test(t)),
        authoritativePod: dup ? (bAfter.detail.podDetailsVisible && bAfter.detail.podRecipient === 'Recipient A') : (bAfter.detail.podEmptyVisible), db: dbA && dbB };
      result.cases[dup ? 'A09-stale-POD-409' : 'A09-stale-eligibility-422'] = { verdict: Object.values(checks).every(Boolean) ? 'PASS' : 'FAIL', checks, initial: init, actorBAlert: bAlert, actorBRequest: bNet,
        actorAStatus: ar.status(), actorBActionsAfter: bAfter.actions.buttons, db: { s0: s0.state, s1: s1.state, s2: s2.state } };
      await A.page.close(); await B.page.close();
    }
  } finally {
    for (const c of Object.values(ctx)) await c.close();
    result.finishedAt = new Date().toISOString();
    fs.writeFileSync(path.join(RAW, `${PHASE}.json`), JSON.stringify(result, null, 2) + '\n');
    const summary = Object.fromEntries(Object.entries(result.cases).map(([k, v]) => [k, v.verdict || v.controlResult || (v.lane_db_update ? 'fixture-step' : 'info')]));
    console.log(JSON.stringify({ phase: PHASE, summary, db: result.db.map((d) => `${d.name}:${d.result}`), png: result.png.length, consoleErrors: result.console.length }));
  }
}
main().catch((e) => { console.error('HARNESS ERROR', e.message); process.exit(1); });
