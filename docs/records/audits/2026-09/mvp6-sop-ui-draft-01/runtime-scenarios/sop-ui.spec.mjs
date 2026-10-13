// MOD-0190 S&OP — Playwright runtime scenarios for the Mac lane (Q84b). DRAFT: written, never executed here.
// Run from the repo root on the Mac after the isolated environment is up (frontend + gateway + SupplyChain + Auth):
//   SOP_BASE_URL=http://localhost:5100 \
//   SOP_STATE_FULL=.auth/full.json SOP_STATE_READONLY=.auth/readonly.json SOP_STATE_NOREAD=.auth/noread.json \
//   SOP_STATE_LEB=.auth/le-b.json SOP_DEMAND_PLAN_ID=<fixture id> SOP_DEMAND_PLAN_VERSION=<fixture version> \
//   SOP_DEMAND_CHECKSUM=<fixture checksum> SOP_EVIDENCE_DIR=<abs dir for PNG> \
//   npx playwright test docs/records/audits/2026-09/mvp6-sop-ui-draft-01/runtime-scenarios/sop-ui.spec.mjs
// Storage states come from real-Auth logins (separate profiles — pack §23.12). No token is written by this script.
// The DEMAND fixture values are the exact scoped test fixture of the S&OP backend (F190-DEMAND). PNG capture uses
// Playwright's page.screenshot, recorded as evidence type only (SU-20 stays BLOCKED until PRES-183-04).
import { test, expect } from '@playwright/test';
import path from 'node:path';
import { randomUUID } from 'node:crypto';

const BASE = process.env.SOP_BASE_URL ?? 'http://localhost:5100';
const ENTRY = `${BASE}/SupplyChain/SandopPlans`;
const env = (name) => {
  const value = process.env[name];
  if (!value) throw new Error(`missing env ${name}`);
  return value;
};
const png = (name) => path.join(env('SOP_EVIDENCE_DIR'), `${name}.png`);
const watchForeign = (page) => {
  const foreign = [];
  page.on('request', (r) => { const u = new URL(r.url()); if (/:(5000|5061)$/.test(u.host)) foreign.push(r.url()); });
  return foreign;
};
const failOnNativeDialog = (page) => page.on('dialog', async (dialog) => {
  await dialog.dismiss();
  throw new Error(`native dialog shown: ${dialog.type()}`);
});
const uniqueName = () => `Q84b plan ${randomUUID().slice(0, 8)}`;

async function createPlan(page, { start = '2030-01-01', end = '2030-03-31' } = {}) {
  await page.goto(ENTRY);
  await page.locator('#btnOpenCreatePlan').click();
  await page.locator('#planName').fill(uniqueName());
  await page.locator('#planHorizonStart').fill(start);
  await page.locator('#planHorizonEnd').fill(end);
  await page.locator('#planDemandPlanId').fill(env('SOP_DEMAND_PLAN_ID'));
  await page.locator('#planDemandPlanVersion').fill(env('SOP_DEMAND_PLAN_VERSION'));
  const post = page.waitForRequest((r) => r.url() === `${BASE}/SupplyChain/SandopPlans/api` && r.method() === 'POST');
  await page.locator('#btnSubmitCreatePlan').click();
  const request = await post;
  await page.waitForURL(/\/SupplyChain\/SandopPlans\/Details\/[0-9a-f-]{36}$/i);
  return { request, planId: page.url().split('/').pop() };
}

test.describe('Entry page (SU-01, SU-02)', () => {
  test.use({ storageState: process.env.SOP_STATE_FULL });

  test('no list request; malformed UUID blocked client-side; same-origin only', async ({ page }) => {
    const foreign = watchForeign(page);
    const apiCalls = [];
    page.on('request', (r) => { if (r.url().includes('/SupplyChain/SandopPlans/api')) apiCalls.push(`${r.method()} ${r.url()}`); });
    await page.goto(ENTRY);
    await expect(page.locator('#openPlanId')).toBeVisible();
    await expect(page.locator('table')).toHaveCount(0);
    await page.locator('#openPlanId').fill('not-a-uuid');
    await page.locator('#btnOpenPlan').click();
    await expect(page.locator('#openPlanIdError')).toBeVisible();
    expect(page.url()).toBe(ENTRY);
    expect(apiCalls).toEqual([]);
    expect(foreign).toEqual([]);
  });

  test('unknown ID → safe-not-found, workspace removed (SU-13)', async ({ page }) => {
    await page.goto(`${ENTRY}/Details/${randomUUID()}`);
    await expect(page.locator('#planAlert')).toBeVisible();
    await expect(page.locator('#planWorkspace')).toBeHidden();
    await expect(page.locator('#btnOpenCaptureSnapshot')).toHaveCount(0);
  });
});

test.describe('Early vertical slice (SU-VS1, SU-06…SU-09, SU-12)', () => {
  test.use({ storageState: process.env.SOP_STATE_FULL });

  test('create → capture → Approved sign-off after showConfirm; status stays InReview', async ({ page }) => {
    failOnNativeDialog(page);
    const foreign = watchForeign(page);
    const { request, planId } = await createPlan(page);
    const body = JSON.parse(request.postData());
    expect(Object.keys(body)).toEqual(['name', 'horizonStart', 'horizonEnd', 'demandPlanId', 'demandPlanVersion']);
    expect(request.headers()['idempotency-key']).toBeTruthy();
    expect(request.headers()['x-tenant-id']).toBeUndefined();
    await expect(page.locator('#summaryStatus')).toContainText(/\S/);
    await expect(page.locator('#btnOpenRecordSignOff')).toBeHidden(); // Draft, no snapshot yet

    await page.locator('#btnOpenCaptureSnapshot').click();
    await page.locator('#captureSourceChecksum').fill(env('SOP_DEMAND_CHECKSUM'));
    const capture = page.waitForRequest((r) => r.url().endsWith(`/api/${planId}/snapshots`) && r.method() === 'POST');
    await page.locator('#btnSubmitCaptureSnapshot').click();
    expect(JSON.parse((await capture).postData()).supplyInputRefs).toEqual([]);
    await expect(page.locator('#dt-sandop-snapshots tbody tr')).toHaveCount(1);
    await expect(page.locator('#dt-sandop-snapshots .badge')).toBeVisible(); // current marker

    await page.locator('#btnOpenRecordSignOff').click();
    await page.locator('#signOffRole').selectOption('Finance');
    await page.locator('#signOffDecision').selectOption('Approved');
    await page.locator('#btnSubmitRecordSignOff').click();
    await page.locator('.swal2-confirm, [data-confirm-accept]').first().click(); // shared showConfirm dialog (MOD-0013)
    await expect(page.locator('#dt-sandop-signoffs tbody tr')).toHaveCount(1);
    await page.reload();
    await expect(page.locator('#dt-sandop-signoffs tbody tr')).toHaveCount(1);
    await expect(page.locator('#summaryStatus .badge')).toHaveClass(/bg-label-warning/); // InReview, not advanced
    await page.screenshot({ path: png('su-vs1-workspace-en'), fullPage: true });
    expect(foreign).toEqual([]);
  });

  test('duplicate role/snapshot → SIGN_OFF_ALREADY_RECORDED; first decision kept (SU-10)', async ({ page }) => {
    const { planId } = await createPlan(page);
    await page.locator('#btnOpenCaptureSnapshot').click();
    await page.locator('#captureSourceChecksum').fill(env('SOP_DEMAND_CHECKSUM'));
    await page.locator('#btnSubmitCaptureSnapshot').click();
    await expect(page.locator('#dt-sandop-snapshots tbody tr')).toHaveCount(1);
    for (const decision of ['Approved', 'Rejected']) {
      await page.locator('#btnOpenRecordSignOff').click();
      await page.locator('#signOffRole').selectOption('Executive');
      await page.locator('#signOffDecision').selectOption(decision);
      await page.locator('#btnSubmitRecordSignOff').click();
      await page.locator('.swal2-confirm, [data-confirm-accept]').first().click();
    }
    await expect(page.locator('#dt-sandop-signoffs tbody tr')).toHaveCount(1);
    await expect(page.locator('#dt-sandop-signoffs tbody')).toContainText(/\S/);
    expect(planId).toMatch(/^[0-9a-f-]{36}$/i);
  });

  test('sign-off in Draft by direct POST → 409 SANDOP_SIGN_OFF_STATE_CONFLICT (SU-11)', async ({ page }) => {
    const { planId } = await createPlan(page);
    const token = await page.locator('#formCaptureSnapshot input[name="__RequestVerificationToken"]').inputValue();
    const response = await page.request.post(`${ENTRY}/api/${planId}/sign-offs`, {
      headers: { 'Content-Type': 'application/json', 'Idempotency-Key': randomUUID(), 'X-Correlation-Id': randomUUID(), RequestVerificationToken: token },
      data: JSON.stringify({ snapshotId: randomUUID(), role: 'Finance', decision: 'Approved' })
    });
    expect(response.status()).toBe(409);
    expect((await response.json()).error.code).toBe('SANDOP_SIGN_OFF_STATE_CONFLICT');
  });

  test('unknown demand reference → 422 with inputs kept (SU-15)', async ({ page }) => {
    await page.goto(ENTRY);
    await page.locator('#btnOpenCreatePlan').click();
    await page.locator('#planName').fill(uniqueName());
    await page.locator('#planHorizonStart').fill('2030-01-01');
    await page.locator('#planHorizonEnd').fill('2030-01-31');
    await page.locator('#planDemandPlanId').fill('unknown-demand-plan');
    await page.locator('#planDemandPlanVersion').fill('v0');
    await page.locator('#btnSubmitCreatePlan').click();
    await expect(page.locator('#formCreatePlanAlert')).toBeVisible();
    await expect(page.locator('#planDemandPlanId')).toHaveValue('unknown-demand-plan');
  });
});

test.describe('Permissions (SU-04, SU-05)', () => {
  test('no read → _AccessDenied only', async ({ browser }) => {
    const context = await browser.newContext({ storageState: env('SOP_STATE_NOREAD') });
    const page = await context.newPage();
    await page.goto(ENTRY);
    await expect(page.locator('#openPlanId')).toHaveCount(0);
    await expect(page.locator('#btnOpenCreatePlan')).toHaveCount(0);
    await context.close();
  });

  test('read-only → no create, capture or sign-off control', async ({ browser }) => {
    const context = await browser.newContext({ storageState: env('SOP_STATE_READONLY') });
    const page = await context.newPage();
    await page.goto(ENTRY);
    await expect(page.locator('#btnOpenCreatePlan')).toHaveCount(0);
    await page.goto(`${ENTRY}/Details/${randomUUID()}`);
    await expect(page.locator('#btnOpenCaptureSnapshot')).toHaveCount(0);
    await expect(page.locator('#btnOpenRecordSignOff')).toHaveCount(0);
    await context.close();
  });
});

test.describe('Localization and layout (SU-17)', () => {
  test.use({ storageState: process.env.SOP_STATE_FULL });
  for (const culture of ['tr', 'ar']) {
    for (const width of [390, 768, 1024, 1440]) {
      test(`${culture} at ${width}px has no horizontal overflow`, async ({ page }) => {
        await page.setViewportSize({ width, height: 900 });
        await page.goto(`${ENTRY}?culture=${culture}&ui-culture=${culture}`);
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
        expect(overflow).toBe(false);
        if (culture === 'ar') expect(await page.locator('html').getAttribute('dir')).toBe('rtl');
        await page.screenshot({ path: png(`su-17-entry-${culture}-${width}`), fullPage: true });
      });
    }
  }
});
