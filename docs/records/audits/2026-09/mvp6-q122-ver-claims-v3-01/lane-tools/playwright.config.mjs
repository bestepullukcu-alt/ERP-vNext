// Q122 lane runtime config for the v3 scenarios (spec byte-identical to the v3 archive). Headless Chromium 1217, one worker (the scenarios share
// seeded data and a two-profile stale-transition case), JSON report to the evidence path given by the harness.
export default {
  testDir: '.',
  testMatch: ['claims-ui.spec.mjs'],
  workers: 1,
  fullyParallel: false,
  retries: 0,
  timeout: 60000,
  expect: { timeout: 15000 },
  reporter: [['list'], ['json', { outputFile: process.env.PW_JSON_OUT }]],
  use: { headless: true, timezoneId: 'UTC', viewport: { width: 1366, height: 900 }, trace: 'off', screenshot: 'off', video: 'off' },
  outputDir: '/Users/natig/mvp6-env/q122/runtime/test-output',
};
