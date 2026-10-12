// MOD-0187 Claims — Playwright runtime scenarios for the Mac lane (Q64b). DRAFT: written, never executed here.
// Run from the repo root on the Mac after the isolated environment is up (frontend + gateway + SupplyChain + Auth):
//   CLAIMS_BASE_URL=http://localhost:5100 \
//   CLAIMS_STATE_FULL=.auth/full.json CLAIMS_STATE_READONLY=.auth/readonly.json CLAIMS_STATE_NOREAD=.auth/noread.json \
//   CLAIMS_STATE_LEB=.auth/le-b.json CLAIMS_SHIPMENT_DISPATCHED=<uuid> CLAIMS_SHIPMENT_DRAFT=<uuid> \
//   CLAIMS_SHIPMENT_CARRIER=<uuid-of-a-dispatched-shipment-with-carrier> CLAIMS_EVIDENCE_DIR=<abs dir for PNG> \
//   npx playwright test docs/records/audits/2026-09/mvp6-claims-ui-draft-01/runtime-scenarios/claims-ui.spec.mjs
// Storage states come from real-Auth logins (three identities, separate profiles — pack §32.12). No token is written
// by this script. PNG capture uses Playwright's page.screenshot, recorded as evidence type only (CU-30 stays BLOCKED
// until PRES-183-04 names the supported export).
import { test, expect } from '@playwright/test';
import path from 'node:path';

const BASE = process.env.CLAIMS_BASE_URL ?? 'http://localhost:5100';
const PAGE = `${BASE}/SupplyChain/Claims`;
const env = (name) => {
  const value = process.env[name];
  if (!value) throw new Error(`missing env ${name}`);
  return value;
};
const png = (name) => path.join(env('CLAIMS_EVIDENCE_DIR'), `${name}.png`);
const sameOriginOnly = (page) => {
  const foreign = [];
  page.on('request', (r) => { const u = new URL(r.url()); if (/:(5000|5061)$/.test(u.host)) foreign.push(r.url()); });
  return foreign;
};

test.describe('Claims list (CU-01…CU-05, CU-31)', () => {
  test.use({ storageState: process.env.CLAIMS_STATE_FULL });

  test('list loads through the same-origin adapter only; skeleton then table', async ({ page }) => {
    const foreign = sameOriginOnly(page);
    const listCall = page.waitForRequest((r) => r.url().startsWith(`${BASE}/SupplyChain/Claims/api`) && r.method() === 'GET');
    await page.goto(PAGE);
    const request = await listCall;
    expect(new URL(request.url()).search).toBe(''); // no query keys without filters
    expect(request.headers()['x-tenant-id']).toBeUndefined();
    await expect(page.locator('#dt-claims')).toBeVisible();
    await expect(page.locator('#skeleton-loader')).toBeHidden();
    expect(foreign).toEqual([]);
    await page.screenshot({ path: png('01-list-en'), fullPage: true });
  });

  test('filter sends only status and shipmentId', async ({ page }) => {
    await page.goto(PAGE);
    await page.locator('.dt-filter-btn').click();
    await page.locator('#filterShipmentId').fill(env('CLAIMS_SHIPMENT_DISPATCHED'));
    const filtered = page.waitForRequest((r) => r.url().includes('/SupplyChain/Claims/api?'));
    await page.locator('#btnFilterApply').click();
    const url = new URL((await filtered).url());
    expect([...url.searchParams.keys()].sort()).toEqual(['shipmentId']);
    await page.locator('#btnFilterReset').click();
  });

  test('QuickView makes no by-ID request (CU-31)', async ({ page }) => {
    await page.goto(PAGE);
    const firstQuick = page.locator('.js-quick-view').first();
    test.skip(!(await firstQuick.count()), 'no claim rows seeded');
    const calls = [];
    page.on('request', (r) => { if (r.url().includes('/SupplyChain/Claims/api/')) calls.push(r.url()); });
    await firstQuick.click();
    await expect(page.locator('#offcanvasDetailsPreview')).toBeVisible();
    await expect(page.locator('#oc-claim-id')).not.toBeEmpty();
    expect(calls).toEqual([]);
    await expect(page.locator('#offcanvasDetailsPreview')).not.toContainText(/Edit/);
  });
});

test.describe('Create (CU-VS1, CU-09…CU-14, CU-17)', () => {
  test.use({ storageState: process.env.CLAIMS_STATE_FULL });

  test('create positive: 250.00 EUR on a Dispatched shipment; body is exact text', async ({ page }) => {
    await page.goto(PAGE);
    await page.locator('.add-new').click();
    await page.locator('#claimShipmentId').fill(env('CLAIMS_SHIPMENT_DISPATCHED'));
    const resolved = page.waitForResponse((r) => r.url().includes('/SupplyChain/Claims/api/shipments/'));
    await page.locator('#btnResolveShipment').click();
    const resolveBody = await (await resolved).json();
    expect(Object.keys(resolveBody).sort()).toEqual(['carrierId', 'shipmentNumber', 'status']); // root never reaches the browser
    await page.locator('#claimReasonCode').fill('DAMAGE');
    await page.locator('#claimClaimedAmount').fill('250.00');
    await page.locator('#claimCurrency').fill('EUR');
    const post = page.waitForRequest((r) => r.url() === `${BASE}/SupplyChain/Claims/api` && r.method() === 'POST');
    const created = page.waitForResponse((r) => r.url() === `${BASE}/SupplyChain/Claims/api` && r.request().method() === 'POST');
    await page.locator('#btnSaveClaim').click();
    const sent = await post;
    expect(sent.postData()).toBe(`{"shipmentId":"${env('CLAIMS_SHIPMENT_DISPATCHED')}","reasonCode":"DAMAGE","claimedAmount":"250.00","currency":"EUR"}`);
    expect(sent.headers()['idempotency-key']).toMatch(/^[0-9a-f-]{36}$/);
    expect((await created).status()).toBe(201);
    await expect(page.locator('#dt-claims')).toContainText('250.00');
    await page.screenshot({ path: png('02-create-201'), fullPage: true });
  });

  test('create negative: lowercase currency → localized 400, inputs kept (CU-11)', async ({ page }) => {
    await page.goto(PAGE);
    await page.locator('.add-new').click();
    await page.locator('#claimShipmentId').fill(env('CLAIMS_SHIPMENT_DISPATCHED'));
    await page.locator('#btnResolveShipment').click();
    await expect(page.locator('#claimShipmentResolved')).toBeVisible();
    await page.locator('#claimReasonCode').fill('');
    await page.locator('#claimClaimedAmount').fill('10');
    await page.locator('#claimCurrency').fill('usd');
    const res = page.waitForResponse((r) => r.url() === `${BASE}/SupplyChain/Claims/api` && r.request().method() === 'POST');
    await page.locator('#btnSaveClaim').click();
    expect((await res).status()).toBe(400);
    await expect(page.locator('#formClaimAlert')).toBeVisible();
    await expect(page.locator('#claimCurrency')).toHaveValue('usd');
    await page.screenshot({ path: png('03-create-400') });
  });

  test('ineligible shipment: note + disabled submit (CU-17)', async ({ page }) => {
    await page.goto(PAGE);
    await page.locator('.add-new').click();
    await page.locator('#claimShipmentId').fill(env('CLAIMS_SHIPMENT_DRAFT'));
    await page.locator('#btnResolveShipment').click();
    await expect(page.locator('#claimIneligibleNote')).toBeVisible();
    await expect(page.locator('#btnSaveClaim')).toBeDisabled();
  });

  test('carrier checkbox enabled only when the shipment has a carrier (CU-10)', async ({ page }) => {
    await page.goto(PAGE);
    await page.locator('.add-new').click();
    await page.locator('#claimShipmentId').fill(env('CLAIMS_SHIPMENT_DISPATCHED'));
    await page.locator('#btnResolveShipment').click();
    await expect(page.locator('#claimLinkCarrier')).toBeDisabled();
    await page.locator('#claimShipmentId').fill(env('CLAIMS_SHIPMENT_CARRIER'));
    await page.locator('#btnResolveShipment').click();
    await expect(page.locator('#claimLinkCarrier')).toBeEnabled();
  });

  test('unknown shipment → one safe not-found text, no shipment data (CU-14)', async ({ page }) => {
    await page.goto(PAGE);
    await page.locator('.add-new').click();
    await page.locator('#claimShipmentId').fill('00000000-0000-4000-8000-000000000001');
    const res = page.waitForResponse((r) => r.url().includes('/api/shipments/'));
    await page.locator('#btnResolveShipment').click();
    expect((await res).status()).toBe(404);
    await expect(page.locator('#formClaimAlert')).toBeVisible();
    await expect(page.locator('#claimShipmentResolved')).toBeHidden();
  });
});

test.describe('Transition (CU-08, CU-20…CU-22)', () => {
  test.use({ storageState: process.env.CLAIMS_STATE_FULL });

  test('allowed transition Open → Investigating returns 200 and reloads', async ({ page }) => {
    await page.goto(PAGE);
    const action = page.locator('.js-transition[data-target-status="Investigating"]').first();
    test.skip(!(await action.count()), 'no Open claim seeded');
    await page.locator('.dropdown-toggle').first().click();
    await action.click();
    await expect(page.locator('#transitionOccurredAt')).toHaveValue(/[+-]\d{2}:\d{2}$|Z$/);
    await page.locator('#btnSubmitTransition').click();
    const res = page.waitForResponse((r) => r.url().includes('/transition?shipmentId='));
    await page.locator('.swal2-confirm, [data-confirm="yes"]').first().click(); // shared premium wrapper
    expect((await res).status()).toBe(200);
  });

  test('stale transition → 422 INVALID_CLAIM_TRANSITION shows conflict + reload (CU-21)', async ({ browser }) => {
    // Two profiles: actor A approves, actor B (opened earlier) rejects the same claim.
    const a = await browser.newContext({ storageState: env('CLAIMS_STATE_FULL') });
    const b = await browser.newContext({ storageState: env('CLAIMS_STATE_FULL') });
    const pa = await a.newPage(); const pb = await b.newPage();
    await pa.goto(PAGE); await pb.goto(PAGE);
    const approve = pa.locator('.js-transition[data-target-status="Approved"]').first();
    test.skip(!(await approve.count()), 'no Investigating claim seeded');
    await pb.locator('.dropdown-toggle').first().click();
    await pb.locator('.js-transition[data-target-status="Rejected"]').first().click();
    await pa.locator('.dropdown-toggle').first().click();
    await approve.click();
    await pa.locator('#transitionApprovedAmount').fill('0');
    await pa.locator('#btnSubmitTransition').click();
    await pa.locator('.swal2-confirm, [data-confirm="yes"]').first().click();
    const res = pb.waitForResponse((r) => r.url().includes('/transition?shipmentId='));
    await pb.locator('#btnSubmitTransition').click();
    await pb.locator('.swal2-confirm, [data-confirm="yes"]').first().click();
    const response = await res;
    expect(response.status()).toBe(422);
    expect((await response.json()).error.code).toBe('INVALID_CLAIM_TRANSITION');
    await pb.screenshot({ path: png('04-transition-422') });
    await a.close(); await b.close();
  });

  test('reused key with a changed body → 409 IDEMPOTENCY_KEY_REUSED, retry stops (CU-19)', async ({ request }) => {
    // HTTP-level probe through the same-origin adapter with the full identity's cookies.
    test.skip(true, 'needs a seeded Open claim id + antiforgery token capture; kept as the Q64b probe outline');
  });
});

test.describe('Scope and permissions (CU-06, CU-07, CU-15, CU-16)', () => {
  test('without read: only _AccessDenied inside the shell', async ({ browser }) => {
    const context = await browser.newContext({ storageState: env('CLAIMS_STATE_NOREAD') });
    const page = await context.newPage();
    const calls = [];
    page.on('request', (r) => { if (r.url().includes('/SupplyChain/Claims/api')) calls.push(r.url()); });
    await page.goto(PAGE);
    await expect(page.locator('#dt-claims')).toHaveCount(0);
    await expect(page.locator('#skeleton-loader')).toHaveCount(0);
    await expect(page.locator('#inlineFilterHost')).toHaveCount(0);
    expect(calls).toEqual([]);
    await page.screenshot({ path: png('05-access-denied') });
    await context.close();
  });

  test('read-only: no create CTA and no row actions', async ({ browser }) => {
    const context = await browser.newContext({ storageState: env('CLAIMS_STATE_READONLY') });
    const page = await context.newPage();
    await page.goto(PAGE);
    await expect(page.locator('#dt-claims')).toBeVisible();
    await expect(page.locator('.add-new')).toHaveCount(0);
    await expect(page.locator('.js-transition')).toHaveCount(0);
    await context.close();
  });

  test('cross-LE: LE-B list excludes LE-A claims; ?shipmentId=<LE-A> → empty', async ({ browser }) => {
    const context = await browser.newContext({ storageState: env('CLAIMS_STATE_LEB') });
    const page = await context.newPage();
    const res = await page.request.get(`${PAGE}/api?shipmentId=${env('CLAIMS_SHIPMENT_DISPATCHED')}`,
      { headers: { 'X-Correlation-Id': crypto.randomUUID() } });
    expect(res.status()).toBe(200);
    expect((await res.json()).items).toEqual([]);
    await context.close();
  });

  test('cross-LE: resolve of an LE-A shipment from LE-B → 404 safe-not-found', async ({ browser }) => {
    const context = await browser.newContext({ storageState: env('CLAIMS_STATE_LEB') });
    const page = await context.newPage();
    const res = await page.request.get(`${PAGE}/api/shipments/${env('CLAIMS_SHIPMENT_DISPATCHED')}`,
      { headers: { 'X-Correlation-Id': crypto.randomUUID() } });
    expect(res.status()).toBe(404);
    expect((await res.json()).error.code).toBe('CLAIM_NOT_FOUND');
    await context.close();
  });
});

test.describe('Languages (CU-24, CU-25)', () => {
  for (const culture of ['tr', 'ar']) {
    test(`screen in ${culture}${culture === 'ar' ? ' (RTL)' : ''}`, async ({ browser }) => {
      const context = await browser.newContext({ storageState: env('CLAIMS_STATE_FULL'), locale: culture });
      const page = await context.newPage();
      await page.goto(`${PAGE}?culture=${culture}&ui-culture=${culture}`);
      await expect(page.locator('#dt-claims')).toBeVisible();
      if (culture === 'ar') await expect(page.locator('html')).toHaveAttribute('dir', 'rtl');
      const text = await page.locator('main, body').first().innerText();
      expect(text).not.toMatch(/\bClaims Management\b|\bAdd Claim\b|\bClaim Number\b/);
      for (const width of [390, 768, 1024, 1440]) {
        await page.setViewportSize({ width, height: 900 });
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth);
        expect(overflow, `${culture} @${width}`).toBe(false);
        await page.screenshot({ path: png(`06-${culture}-${width}`), fullPage: true });
      }
      await context.close();
    });
  }
});
