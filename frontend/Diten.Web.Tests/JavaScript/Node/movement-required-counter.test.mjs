// WP-MVP1-FIX-LIVE4 (FU-0173-REQ-COUNTER, G1 live run: "Zorunlu: 8 / 9" stayed after the FEFO reason was written) — the SHARED required
// tracker (assets/js/custom/required-fields-tracker.js, unchanged) and the SHIPPED band (assets/js/Expiry/expiry-bands.js) on a real DOM
// (jsdom): the band's reason field is rendered by the real partial (Views/Shared/Expiry/_FefoBand.cshtml) inside the form's source block,
// hidden at load. Band open → the reason counts (8 / 9) and writing it reaches 9 / 9; band closed → the reason does not count (8 / 8).
// MovementRequiredCounterTests.cs runs it.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const web = (p) => readFileSync(path.join(repo, 'frontend/Diten.Web', p), 'utf8');
const { JSDOM } = createRequire(path.join(repo, 'frontend/Diten.Web/package.json'))('jsdom');

// the real band partial, Razor stripped (directives, comments, localizer calls → plain text)
const bandPartial = web('Views/Shared/Expiry/_FefoBand.cshtml')
    .replace(/@\*[\s\S]*?\*@/g, '').replace(/^@(using|inject)[^\n]*\n/gm, '').replace(/@Localizer\["[^"]+"\]/g, 'Metin');

const FIELDS = ['movementType', 'itemId', 'sku', 'source', 'quantity', 'uomId', 'transactionDate', 'postingDate'];
const html = `<!doctype html><html lang="tr"><body>
<script id="expiry-band-l10n" type="application/json">{"Expiry.Band.FefoFirst":"Daha yakın tarihli lot var: {0} (SKT {1})"}</script>
<form id="formStockMovement">
  ${FIELDS.filter((f) => f !== 'source').map((f) => `<label for="${f}">${f} <span class="text-danger">*</span></label><input id="${f}" required value="x">`).join('\n')}
  <div data-matrix="source"><label for="source">source <span class="text-danger">*</span></label><input id="source" required value="x">${bandPartial}</div>
  <div class="actions"><button type="submit" class="btn btn-primary">Kaydet</button></div>
</form></body></html>`;

const flush = () => new Promise((resolve) => setTimeout(resolve, 0));

const page = async () => {
    const dom = new JSDOM(html, { runScripts: 'outside-only', pretendToBeVisual: true });
    const { window } = dom;
    window.$ = () => ({ hasClass: () => false, on() {} });
    window.L10n = { RequiredProgressText: 'Zorunlu: {0} / {1}' };
    window.fetch = async () => ({ ok: true, status: 200, json: async () => ({ order: { eligible: [{ lotId: 'early', lotNumber: 'AMX-2604', expiryDate: '2028-03-31' }, { lotId: 'late', lotNumber: 'AMX-2611', expiryDate: '2028-11-30' }] }, policy: { fefoOutboundMode: 'Reason' } }) });
    // jsdom fires DOMContentLoaded itself after construction: the tracker registers first, exactly as on the page (no second init)
    window.eval(web('wwwroot/assets/js/custom/required-fields-tracker.js'));
    window.eval(web('wwwroot/assets/js/Expiry/expiry-bands.js'));
    await new Promise((resolve) => (window.document.readyState === 'complete' ? resolve() : window.addEventListener('load', resolve)));
    await flush();
    assert.equal(window.document.querySelectorAll('[data-required-tracker]').length, 1);
    const counter = () => window.document.querySelector('[data-required-tracker] .required-text').textContent;
    const open = async (movementType) => {
        await window.ExpiryBands.movement.onSource({ direction: 'out', movementType, fromStatus: 'AVAILABLE', itemId: 'i', sku: 'Lsku:s', warehouseId: 'w', lotId: 'late' });
        await flush();
    };
    return { window, counter, open };
};

test('band open: the reason counts, and writing it moves the counter to 9 / 9', async () => {
    const { window, counter, open } = await page();
    await flush();
    assert.equal(counter(), 'Zorunlu: 8 / 8');
    await open('GOODS_ISSUE_SALES');
    assert.equal(counter(), 'Zorunlu: 8 / 9');
    const reason = window.document.getElementById('ex-band-reason');
    reason.value = 'Müşteri talebiyle bu lot sevk ediliyor';
    reason.dispatchEvent(new window.Event('input', { bubbles: true }));
    await flush();
    assert.equal(counter(), 'Zorunlu: 9 / 9');
});

test('band closed (an exempt type): the reason is not counted', async () => {
    const { counter, open } = await page();
    await open('COUNT_LOSS');
    assert.equal(counter(), 'Zorunlu: 8 / 8');
});
