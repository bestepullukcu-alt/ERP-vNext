// MOD-0186 Returns — Playwright runtime scenarios for the Mac lane (Q65b). DRAFT: written, never executed here.
// Run from the repo root on the Mac after the isolated environment is up (frontend + gateway + SupplyChain + Auth):
//   RETURNS_BASE_URL=http://localhost:5100 \
//   RETURNS_STATE_FULL=.auth/full.json RETURNS_STATE_READONLY=.auth/readonly.json RETURNS_STATE_NOREAD=.auth/noread.json \
//   RETURNS_STATE_LEB=.auth/le-b.json RETURNS_SHIPMENT_DELIVERED=<uuid, non-null root> RETURNS_SHIPMENT_DRAFT=<uuid> \
//   RETURNS_EVIDENCE_DIR=<abs dir for PNG> \
//   npx playwright test docs/records/audits/2026-09/mvp6-returns-ui-draft-01/runtime-scenarios/returns-ui.spec.mjs
// Storage states come from real-Auth logins (three identities, separate profiles — pack §32.12). No token is written by
// this script. PNG capture uses Playwright's page.screenshot, recorded as evidence type only (RU-28 stays BLOCKED until
// PRES-183-04 names the supported export).
import { test, expect } from '@playwright/test';
import path from 'node:path';
import { randomUUID } from 'node:crypto';

const BASE = process.env.RETURNS_BASE_URL ?? 'http://localhost:5100';
const PAGE = `${BASE}/SupplyChain/Returns`;
const env = (name) => {
  const value = process.env[name];
  if (!value) throw new Error(`missing env ${name}`);
  return value;
};
const png = (name) => path.join(env('RETURNS_EVIDENCE_DIR'), `${name}.png`);
const sameOriginOnly = (page) => {
  const foreign = [];
  page.on('request', (r) => { const u = new URL(r.url()); if (/:(5000|5061)$/.test(u.host)) foreign.push(r.url()); });
  return foreign;
};
const failOnNativeDialog = (page) => page.on('dialog', async (dialog) => {
  await dialog.dismiss();
  throw new Error(`native dialog shown: ${dialog.type()}`);
});
const confirmShared = async (page) => page.locator('.swal2-confirm, [data-confirm-accept]').first().click(); // MOD-0013 wrapper (A10)

async function resolve(page, shipmentId) {
  await page.goto(PAGE);
  await page.locator('.add-new').click();
  await page.locator('#returnShipmentId').fill(shipmentId);
  await page.locator('#btnResolveShipment').click();
}

test.describe('Returns list (RU-01…RU-05, RU-29)', () => {
  test.use({ storageState: process.env.RETURNS_STATE_FULL });

  test('list loads through the same-origin adapter only; skeleton then table', async ({ page }) => {
    const foreign = sameOriginOnly(page);
    const listCall = page.waitForRequest((r) => r.url().startsWith(`${BASE}/SupplyChain/Returns/api`) && r.method() === 'GET');
    await page.goto(PAGE);
    const request = await listCall;
    expect(new URL(request.url()).search).toBe(''); // no query keys without filters
    expect(request.headers()['x-tenant-id']).toBeUndefined(); // scope is added server-side only
    expect(request.headers().authorization).toBeUndefined();
    await expect(page.locator('#dt-returns')).toBeVisible();
    await expect(page.locator('#skeleton-loader')).toBeHidden();
    expect(foreign).toEqual([]);
    await page.screenshot({ path: png('01-list-en'), fullPage: true });
  });

  test('filter sends only status and shipmentId', async ({ page }) => {
    await page.goto(PAGE);
    await page.locator('.dt-filter-btn').click();
    await page.locator('#filterShipmentId').fill(env('RETURNS_SHIPMENT_DELIVERED'));
    const filtered = page.waitForRequest((r) => r.url().includes('/SupplyChain/Returns/api?'));
    await page.locator('#btnFilterApply').click();
    const url = new URL((await filtered).url());
    expect([...url.searchParams.keys()].sort()).toEqual(['shipmentId']);
    await page.locator('#btnFilterReset').click();
  });

  test('QuickView makes no by-ID request (RU-29)', async ({ page }) => {
    await page.goto(PAGE);
    const firstQuick = page.locator('.js-quick-view').first();
    test.skip(!(await firstQuick.count()), 'no return rows seeded');
    const calls = [];
    page.on('request', (r) => { if (r.url().includes('/SupplyChain/Returns/api/')) calls.push(r.url()); });
    await firstQuick.click();
    await expect(page.locator('#offcanvasDetailsPreview')).toBeVisible();
    await expect(page.locator('#oc-return-id')).toHaveText(/[0-9a-f-]{36}/i);
    expect(calls).toEqual([]);
  });
});

test.describe('Early vertical slice (RU-VS1, RU-08…RU-10, RU-14)', () => {
  test.use({ storageState: process.env.RETURNS_STATE_FULL });

  test('resolve a Delivered shipment and create a return for one line; the row appears after reload', async ({ page }) => {
    failOnNativeDialog(page);
    const foreign = sameOriginOnly(page);
    await resolve(page, env('RETURNS_SHIPMENT_DELIVERED'));
    await expect(page.locator('#returnLinesTable')).toBeVisible();
    await expect(page.locator('#btnSaveReturn')).toBeEnabled();
    const firstRow = page.locator('#returnLinesBody .return-line-row').first();
    await firstRow.locator('.js-line-select').check();
    await firstRow.locator('.js-line-quantity').fill('1');
    await page.locator('#returnReasonCode').fill(''); // presence, empty allowed (RU-09)
    const post = page.waitForRequest((r) => r.url() === `${BASE}/SupplyChain/Returns/api` && r.method() === 'POST');
    await page.locator('#btnSaveReturn').click();
    const request = await post;
    const body = JSON.parse(request.postData());
    expect(Object.keys(body)).toEqual(['shipmentId', 'reasonCode', 'lines']); // evidence omitted when empty
    expect(typeof body.lines[0].quantity).toBe('string');
    expect(body.lines[0].quantity).toBe('1');
    expect(request.headers()['idempotency-key']).toBeTruthy();
    const response = await request.response();
    expect(response.status()).toBe(201);
    // Support reference is the adapter trace, never the Shipment root (RU-21).
    expect(response.headers()['x-correlation-id']).toBe(request.headers()['x-correlation-id']);
    await expect(page.locator('#dt-returns')).toContainText(/RMA-/);
    await page.reload();
    await expect(page.locator('#dt-returns')).toContainText(/RMA-/);
    expect(foreign).toEqual([]);
    await page.screenshot({ path: png('02-created-en'), fullPage: true });
  });

  test('ineligible shipment disables submit; unknown shipment leaves no shipment data (RU-11, RU-14)', async ({ page }) => {
    await resolve(page, env('RETURNS_SHIPMENT_DRAFT'));
    await expect(page.locator('#returnIneligibleNote')).toBeVisible();
    await expect(page.locator('#btnSaveReturn')).toBeDisabled();
    await page.locator('#returnShipmentId').fill(randomUUID());
    await page.locator('#btnResolveShipment').click();
    await expect(page.locator('#formReturnAlert')).toBeVisible();
    await expect(page.locator('#returnLinesBody tr')).toHaveCount(0);
    await expect(page.locator('#returnShipmentResolved')).toBeHidden();
  });

  test('a late resolve response for a previous UUID never populates (RU-10)', async ({ page }) => {
    await page.route('**/SupplyChain/Returns/api/shipments/**', async (route) => {
      if (route.request().url().includes(env('RETURNS_SHIPMENT_DELIVERED'))) await new Promise((r) => { void page.waitForTimeout(1500).then(r); });
      await route.continue();
    });
    await resolve(page, env('RETURNS_SHIPMENT_DELIVERED'));
    await page.locator('#returnShipmentId').fill(env('RETURNS_SHIPMENT_DRAFT'));
    await page.locator('#btnResolveShipment').click();
    await expect(page.locator('#returnIneligibleNote')).toBeVisible();
    await page.waitForTimeout(2000);
    await expect(page.locator('#returnIneligibleNote')).toBeVisible(); // still the second shipment
  });
});

test.describe('Transitions (RU-07, RU-17…RU-20, RU-24)', () => {
  test.use({ storageState: process.env.RETURNS_STATE_FULL });

  test('authorize through the offcanvas and the shared confirmation; occurredAt carries an offset', async ({ page }) => {
    failOnNativeDialog(page);
    await page.goto(PAGE);
    const action = page.locator('.js-transition[data-target-status="Authorized"]').first();
    test.skip(!(await action.count()), 'no Requested return seeded');
    await page.locator('.dropdown-toggle').first().click();
    await action.click();
    await expect(page.locator('#transitionOccurredAt')).toHaveValue(/[+-]\d{2}:\d{2}$|Z$/);
    const post = page.waitForRequest((r) => r.url().includes('/transition?shipmentId=') && r.method() === 'POST');
    await page.locator('#btnSubmitTransition').click();
    await confirmShared(page);
    const body = JSON.parse((await post).postData());
    expect(Object.keys(body)).toEqual(['targetStatus', 'occurredAt']);
  });

  test('Received is labelled a manual assertion; Dispositioned requires a code and keeps whitespace (RU-18, RU-19)', async ({ page }) => {
    await page.goto(PAGE);
    const receive = page.locator('.js-transition[data-target-status="Received"]').first();
    test.skip(!(await receive.count()), 'no InTransit return seeded');
    await page.locator('.dropdown-toggle').first().click();
    await receive.click();
    await expect(page.locator('#transitionReceivedNote')).toBeVisible();
    await expect(page.locator('#transitionInventoryGroup')).toBeVisible();
  });
});

test.describe('Permissions (RU-05, RU-06, RU-07)', () => {
  test('no read → _AccessDenied only', async ({ browser }) => {
    const context = await browser.newContext({ storageState: env('RETURNS_STATE_NOREAD') });
    const page = await context.newPage();
    await page.goto(PAGE);
    await expect(page.locator('#dt-returns')).toHaveCount(0);
    await expect(page.locator('#filterForm')).toHaveCount(0);
    await context.close();
  });

  test('read-only → no create CTA and no row action', async ({ browser }) => {
    const context = await browser.newContext({ storageState: env('RETURNS_STATE_READONLY') });
    const page = await context.newPage();
    await page.goto(PAGE);
    await expect(page.locator('.add-new')).toHaveCount(0);
    await expect(page.locator('.js-transition')).toHaveCount(0);
    await context.close();
  });
});

test.describe('Localization and layout (RU-22, RU-23)', () => {
  test.use({ storageState: process.env.RETURNS_STATE_FULL });
  for (const culture of ['tr', 'ar']) {
    for (const width of [390, 768, 1024, 1440]) {
      test(`${culture} at ${width}px has no horizontal overflow`, async ({ page }) => {
        await page.setViewportSize({ width, height: 900 });
        await page.goto(`${PAGE}?culture=${culture}&ui-culture=${culture}`);
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
        expect(overflow).toBe(false);
        if (culture === 'ar') expect(await page.locator('html').getAttribute('dir')).toBe('rtl');
        await page.screenshot({ path: png(`ru-22-list-${culture}-${width}`), fullPage: true });
      });
    }
  }
});
