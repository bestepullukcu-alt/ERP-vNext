// MOD-0192 Capacity — Playwright runtime scenarios for the Mac lane (Q88b). DRAFT: written, never executed here.
// Run from the repo root on the Mac after the isolated environment is up (frontend + gateway + SupplyChain + Auth + executor):
//   CAP_BASE_URL=http://localhost:5100 \
//   CAP_STATE_FULL=.auth/full.json CAP_STATE_READONLY=.auth/readonly.json CAP_STATE_NOREAD=.auth/noread.json \
//   CAP_DEMAND_PLAN_ID=<fixture id> CAP_DEMAND_PLAN_VERSION=<fixture version> CAP_DEMAND_CHECKSUM=<fixture checksum> \
//   CAP_SOURCE_CAPTURED_AT=<fixture UTC date-time> CAP_CONSTRAINT_ID=<fixture> CAP_CONSTRAINT_SOURCE=<fixture> \
//   CAP_CONSTRAINT_VERSION=<fixture> CAP_RESOURCE_REF=<fixture resource> CAP_PERIOD=<fixture period> CAP_UOM=<fixture uom> \
//   CAP_EVIDENCE_DIR=<abs dir for PNG> \
//   npx playwright test docs/records/audits/2026-09/mvp6-capacity-ui-draft-01/runtime-scenarios/capacity-ui.spec.mjs
// Storage states come from real-Auth logins (separate profiles — pack §23.12). No token is written by this script.
// Fixture values are the exact scoped fixtures of the accepted Capacity backend (F192-DEMAND; CAPACITY-EVAL-FIXTURE-192-01@1).
// PNG capture uses Playwright's page.screenshot, recorded as evidence type only (CP-20 stays BLOCKED until PRES-183-04).
import { test, expect } from '@playwright/test';
import path from 'node:path';
import { randomUUID } from 'node:crypto';

const BASE = process.env.CAP_BASE_URL ?? 'http://localhost:5100';
const ENTRY = `${BASE}/SupplyChain/CapacityPlans`;
const env = (name) => {
  const value = process.env[name];
  if (!value) throw new Error(`missing env ${name}`);
  return value;
};
const png = (name) => path.join(env('CAP_EVIDENCE_DIR'), `${name}.png`);
const watchForeign = (page) => {
  const foreign = [];
  page.on('request', (r) => { const u = new URL(r.url()); if (/:(5000|5061)$/.test(u.host)) foreign.push(r.url()); });
  return foreign;
};
const watchApi = (page) => {
  const calls = [];
  page.on('request', (r) => { if (r.url().includes('/SupplyChain/CapacityPlans/api')) calls.push(`${r.method()} ${r.url()}`); });
  return calls;
};
const failOnNativeDialog = (page) => page.on('dialog', async (dialog) => {
  await dialog.dismiss();
  throw new Error(`native dialog shown: ${dialog.type()}`);
});
const unique = (prefix) => `${prefix} ${randomUUID().slice(0, 8)}`;

// Each run uses a fresh horizon so CAPACITY_PLAN_ALREADY_EXISTS is not hit by accident.
const freshHorizon = () => {
  const year = 2040 + Math.floor(Math.random() * 50);
  return { start: `${year}-01-01`, end: `${year}-03-31` };
};

async function createPlan(page, horizon = freshHorizon()) {
  await page.goto(ENTRY);
  await page.locator('#btnOpenCreatePlan').click();
  await expect(page.locator('#createRequiredProgress')).toContainText('0/7');
  await page.locator('#planName').fill(unique('Q88b plan'));
  await page.locator('#planHorizonStart').fill(horizon.start);
  await page.locator('#planHorizonEnd').fill(horizon.end);
  await page.locator('#planDemandPlanId').fill(env('CAP_DEMAND_PLAN_ID'));
  await page.locator('#planDemandPlanVersion').fill(env('CAP_DEMAND_PLAN_VERSION'));
  await page.locator('#planSourceCapturedAt').fill(env('CAP_SOURCE_CAPTURED_AT'));
  await page.locator('#planSourceChecksum').fill(env('CAP_DEMAND_CHECKSUM'));
  await expect(page.locator('#createRequiredProgress')).toContainText('7/7');
  const post = page.waitForRequest((r) => r.url() === `${ENTRY}/api` && r.method() === 'POST');
  await page.locator('#btnSubmitCreatePlan').click();
  const request = await post;
  await page.waitForURL(/\/SupplyChain\/CapacityPlans\/Details\/[0-9a-f-]{36}$/i);
  return { request, planId: page.url().split('/').pop(), horizon };
}

async function createScenario(page, planId, { delta = '10.5', withRows = true } = {}) {
  await page.locator('#btnOpenCreateScenario').click();
  await page.locator('#scenarioName').fill(unique('Q88b scenario'));
  if (withRows) {
    await page.locator('#btnAddConstraintRef').click();
    await page.locator('#scenarioConstraintRefRows .js-constraint-id').fill(env('CAP_CONSTRAINT_ID'));
    await page.locator('#scenarioConstraintRefRows .js-constraint-source').fill(env('CAP_CONSTRAINT_SOURCE'));
    await page.locator('#scenarioConstraintRefRows .js-constraint-version').fill(env('CAP_CONSTRAINT_VERSION'));
    await page.locator('#btnAddAdjustment').click();
    await page.locator('#scenarioAdjustmentRows .js-resource-ref').fill(env('CAP_RESOURCE_REF'));
    await page.locator('#scenarioAdjustmentRows .js-period').fill(env('CAP_PERIOD'));
    await page.locator('#scenarioAdjustmentRows .js-delta').fill(delta);
    await page.locator('#scenarioAdjustmentRows .js-uom').fill(env('CAP_UOM'));
  }
  const post = page.waitForRequest((r) => r.url().endsWith(`/api/${planId}/scenarios`) && r.method() === 'POST');
  await page.locator('#btnSubmitCreateScenario').click();
  const request = await post;
  await expect(page.locator('#scenario-content')).toBeVisible();
  return request;
}

test.describe('Entry page (CP-01, CP-02)', () => {
  test.use({ storageState: process.env.CAP_STATE_FULL });

  test('no list request; malformed and nil UUID blocked client-side; same-origin only', async ({ page }) => {
    const foreign = watchForeign(page);
    const apiCalls = watchApi(page);
    await page.goto(ENTRY);
    await expect(page.locator('#openPlanId')).toBeVisible();
    await expect(page.locator('table')).toHaveCount(0);
    for (const bad of ['not-a-uuid', '00000000-0000-0000-0000-000000000000']) {
      await page.locator('#openPlanId').fill(bad);
      await page.locator('#btnOpenPlan').click();
      await expect(page.locator('#openPlanIdError')).toBeVisible();
      expect(page.url()).toBe(ENTRY);
    }
    expect(apiCalls).toEqual([]);
    expect(foreign).toEqual([]);
  });

  test('unknown plan ID → safe-not-found, workspace removed (CP-13)', async ({ page }) => {
    await page.goto(`${ENTRY}/Details/${randomUUID()}`);
    await expect(page.locator('#planAlert')).toBeVisible();
    await expect(page.locator('#planWorkspace')).toBeHidden();
    await expect(page.locator('#btnOpenCreateScenario')).toHaveCount(0);
  });
});

test.describe('Early vertical slice (CP-VS1, CP-06…CP-10)', () => {
  test.use({ storageState: process.env.CAP_STATE_FULL });

  test('create plan → scenario → Finite evaluation → manual Refresh until Completed; decimals exact', async ({ page }) => {
    test.setTimeout(180_000);
    failOnNativeDialog(page);
    const foreign = watchForeign(page);
    const { request, planId } = await createPlan(page);
    const planBody = JSON.parse(request.postData());
    expect(Object.keys(planBody)).toEqual(['name', 'horizonStart', 'horizonEnd', 'demandPlanId', 'demandPlanVersion', 'sourceCapturedAt', 'sourceChecksum']);
    expect(request.headers()['idempotency-key']).toBeTruthy();
    expect(request.headers()['x-tenant-id']).toBeUndefined();
    await expect(page.locator('#summaryStatus .badge')).toBeVisible();

    const scenarioRequest = await createScenario(page, planId);
    const scenarioBody = JSON.parse(scenarioRequest.postData());
    expect(scenarioBody.constraintRefs).toHaveLength(1);
    expect(scenarioBody.adjustments[0].availableCapacityDelta).toBe('10.5'); // a JSON string, never a number
    await expect(page.locator('#scenarioAdjustments tbody')).toContainText('10.5');

    await page.locator('#btnOpenEvaluate').click();
    await page.locator('#evaluationMode').selectOption('Finite');
    await page.locator('#evaluateResourceRefRows .js-resource-ref').first().fill(env('CAP_RESOURCE_REF'));
    const evaluate = page.waitForResponse((r) => r.url().includes('/evaluations') && r.request().method() === 'POST');
    await page.locator('#btnSubmitEvaluate').click();
    expect((await evaluate).status()).toBe(202);
    await expect(page.locator('#evaluation-content')).toBeVisible();
    await expect(page.locator('#btnOpenEvaluate')).toBeHidden(); // Accepted/Running: no second submit

    // No automatic polling: with no click, no evaluation GET happens for 10 s (CP-10, F192-POLL).
    const gets = [];
    page.on('request', (r) => { if (r.method() === 'GET' && r.url().includes('/evaluations/')) gets.push(r.url()); });
    await page.waitForTimeout(10_000);
    expect(gets).toHaveLength(0);

    // Each Refresh click = exactly one getCapacityEvaluation.
    for (let click = 1; click <= 30 && await page.locator('#btnRefreshEvaluation').isVisible(); click += 1) {
      const before = gets.length;
      const response = page.waitForResponse((r) => r.url().includes('/evaluations/') && r.request().method() === 'GET');
      await page.locator('#btnRefreshEvaluation').click();
      await response;
      expect(gets.length).toBe(before + 1);
      await page.waitForTimeout(2_000);
    }
    await expect(page.locator('#btnRefreshEvaluation')).toBeHidden(); // Completed or Failed
    await expect(page.locator('#bottlenecks-host')).toBeVisible(); // fixture completes (Completed row)
    const cells = await page.locator('#dt-capacity-bottlenecks tbody td.text-end').allTextContents();
    cells.forEach((text) => expect(text.trim()).toMatch(/^-?\d+(\.\d+)?$/)); // literal wire strings, no grouping
    await page.screenshot({ path: png('cp-vs1-workspace-en'), fullPage: true });
    expect(foreign).toEqual([]);
  });

  test('second evaluate while active → 409 EVALUATION_ALREADY_ACTIVE (CP-11)', async ({ page }) => {
    const { planId } = await createPlan(page);
    await createScenario(page, planId, { withRows: false });
    const scenarioId = await page.locator('#scenarioIdValue .js-copy-value').getAttribute('data-copy-value');
    const token = await page.locator('#formEvaluate input[name="__RequestVerificationToken"]').inputValue();
    const post = () => page.request.post(`${ENTRY}/api/${planId}/scenarios/${scenarioId}/evaluations`, {
      headers: { 'Content-Type': 'application/json', 'Idempotency-Key': randomUUID(), 'X-Correlation-Id': randomUUID(), RequestVerificationToken: token },
      data: JSON.stringify({ evaluationMode: 'Finite', resourceRefs: [env('CAP_RESOURCE_REF')] })
    });
    expect((await post()).status()).toBe(202);
    const second = await post();
    expect(second.status()).toBe(409);
    expect((await second.json()).error.code).toBe('EVALUATION_ALREADY_ACTIVE');
  });

  test('invalid decimal is blocked client-side with no request (CP-09)', async ({ page }) => {
    const { planId } = await createPlan(page);
    const calls = watchApi(page);
    await page.locator('#btnOpenCreateScenario').click();
    await page.locator('#scenarioName').fill(unique('Q88b scenario'));
    await page.locator('#btnAddAdjustment').click();
    await page.locator('#scenarioAdjustmentRows .js-resource-ref').fill('R1');
    await page.locator('#scenarioAdjustmentRows .js-period').fill('P1');
    await page.locator('#scenarioAdjustmentRows .js-delta').fill('1,5');
    await page.locator('#scenarioAdjustmentRows .js-uom').fill('H');
    await page.locator('#btnSubmitCreateScenario').click();
    await expect(page.locator('#formCreateScenarioAlert')).toBeVisible();
    expect(calls.filter((c) => c.startsWith('POST') && c.endsWith(`/api/${planId}/scenarios`))).toEqual([]);
  });

  test('same horizon and demand version → 409 CAPACITY_PLAN_ALREADY_EXISTS with inputs kept (CP-12)', async ({ page }) => {
    const { horizon } = await createPlan(page);
    await page.goto(ENTRY);
    await page.locator('#btnOpenCreatePlan').click();
    await page.locator('#planName').fill(unique('Q88b duplicate'));
    await page.locator('#planHorizonStart').fill(horizon.start);
    await page.locator('#planHorizonEnd').fill(horizon.end);
    await page.locator('#planDemandPlanId').fill(env('CAP_DEMAND_PLAN_ID'));
    await page.locator('#planDemandPlanVersion').fill(env('CAP_DEMAND_PLAN_VERSION'));
    await page.locator('#planSourceCapturedAt').fill(env('CAP_SOURCE_CAPTURED_AT'));
    await page.locator('#planSourceChecksum').fill(env('CAP_DEMAND_CHECKSUM'));
    await page.locator('#btnSubmitCreatePlan').click();
    await expect(page.locator('#formCreatePlanAlert')).toBeVisible();
    await expect(page.locator('#planHorizonStart')).toHaveValue(horizon.start);
  });

  test('unknown scenario ID → scenario safe-not-found only; plan stays (CP-02, CP-13)', async ({ page }) => {
    await createPlan(page);
    await page.locator('#openScenarioId').fill('not-a-uuid');
    await page.locator('#btnOpenScenario').click();
    await expect(page.locator('#openScenarioIdError')).toBeVisible();
    await page.locator('#openScenarioId').fill(randomUUID());
    await page.locator('#btnOpenScenario').click();
    await expect(page.locator('#scenario-error')).toBeVisible();
    await expect(page.locator('#scenario-content')).toBeHidden();
    await expect(page.locator('#summary-content')).toBeVisible();
  });
});

test.describe('Permissions (CP-04, CP-05)', () => {
  test('no read → _AccessDenied only', async ({ browser }) => {
    const context = await browser.newContext({ storageState: env('CAP_STATE_NOREAD') });
    const page = await context.newPage();
    await page.goto(ENTRY);
    await expect(page.locator('#openPlanId')).toHaveCount(0);
    await expect(page.locator('#btnOpenCreatePlan')).toHaveCount(0);
    await context.close();
  });

  test('read-only → no create, scenario or evaluate control', async ({ browser }) => {
    const context = await browser.newContext({ storageState: env('CAP_STATE_READONLY') });
    const page = await context.newPage();
    await page.goto(ENTRY);
    await expect(page.locator('#btnOpenCreatePlan')).toHaveCount(0);
    await page.goto(`${ENTRY}/Details/${randomUUID()}`);
    await expect(page.locator('#btnOpenCreateScenario')).toHaveCount(0);
    await expect(page.locator('#btnOpenEvaluate')).toHaveCount(0);
    await context.close();
  });
});

test.describe('Localization and layout (CP-17)', () => {
  test.use({ storageState: process.env.CAP_STATE_FULL });
  for (const culture of ['tr', 'ar']) {
    for (const width of [390, 768, 1024, 1440]) {
      test(`${culture} at ${width}px has no horizontal overflow`, async ({ page }) => {
        await page.setViewportSize({ width, height: 900 });
        await page.goto(`${ENTRY}?culture=${culture}&ui-culture=${culture}`);
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth);
        expect(overflow).toBe(false);
        if (culture === 'ar') expect(await page.locator('html').getAttribute('dir')).toBe('rtl');
        await page.screenshot({ path: png(`cp-17-entry-${culture}-${width}`), fullPage: true });
      });
    }
  }
});
