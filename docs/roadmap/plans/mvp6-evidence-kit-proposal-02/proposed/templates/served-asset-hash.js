// CANDIDATE — NOT ACTIVE. MVP6 evidence kit v1.1 template (G1): serve a test-route override AND record the exact bytes.
// Why: the A12 VER-02 negative control served the pre-A12 details.js through page.route() but recorded no hash of the
// bytes the browser executed; the independent review could only accept it "on behaviour" (check 4 PARTIAL).
// Use (Playwright, in the lane harness):
//   const { fulfillHashed } = require('<kit>/templates/served-asset-hash.js');
//   await page.route('**/assets/js/SupplyChain/Shipments/details.js*', (route) =>
//     fulfillHashed(route, BASELINE_FILE, { evidence: EVIDENCE_DIR, label: 'negctl-baseline-details-js',
//                                           expect: '<sha256 recorded with k09_binding.py record-served>' }));
// Every call appends one row to <evidence>/raw/served-overrides.tsv (source = route.fulfill) with the sha256 of the body
// actually passed to route.fulfill(). If `expect` is given and differs, the route is aborted instead of served, so a
// wrong file can never be executed silently. K09 fails when served rows do not match the pre-serve record.
const crypto = require('crypto');
const fs = require('fs');
const path = require('path');

const HDR = 'utc\tlabel\tsource\turl_pattern\tfile\tsha256\tbytes\texpected_sha256\tresult\n';

async function fulfillHashed(route, filePath, opts) {
  const body = fs.readFileSync(filePath);
  const sha256 = crypto.createHash('sha256').update(body).digest('hex');
  const expect = (opts && opts.expect) || '-';
  const ok = expect === '-' || expect === sha256;
  const out = path.join(opts.evidence, 'raw', 'served-overrides.tsv');
  if (!fs.existsSync(out)) fs.writeFileSync(out, HDR);
  const row = [new Date().toISOString().replace(/\.\d{3}Z$/, 'Z'), opts.label, 'route.fulfill', route.request().url(),
    filePath, sha256, String(body.length), expect, ok ? 'PASS' : 'FAIL'].map((v) => String(v).replace(/[\t\r\n]+/g, ' '));
  fs.appendFileSync(out, row.join('\t') + '\n');
  if (!ok) return route.abort('failed');
  return route.fulfill({ status: 200, contentType: opts.contentType || 'application/javascript', body });
}

module.exports = { fulfillHashed };
