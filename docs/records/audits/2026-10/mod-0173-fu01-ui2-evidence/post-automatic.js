// WP-0173-UI2 — one AUTOMATIC movement (GOODS_RECEIPT_PO, sourceModule MOD-0142) through the PROBE gateway
// (:5090 → InventoryService :5067), so the history has a row the UI must NOT offer "Correct" / an active Reversal on
// (pack §13 "reversed in the source module"). The UI cannot post it (manual types only); a source module would.
// The test JWT is minted in memory with the local dev signing settings and is never printed; the evidence keeps the
// request with Authorization masked and the gateway's answer. Copy of the LOC2 post-stock.js.
const fs = require('fs');
const crypto = require('crypto');

const [tenant, company, warehouseId, locationId, out] = process.argv.slice(2);
const settings = JSON.parse(fs.readFileSync('C:/Users/Ali/erp-vnext-wt-ui2/frontend/Diten.Web/appsettings.Development.json', 'utf8').replace(/^\uFEFF/, '')).JwtSettings;
const b64 = (b) => Buffer.from(b).toString('base64').replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
const now = Math.floor(Date.now() / 1000);
const header = b64(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
const payload = b64(JSON.stringify({
    sub: '1c0c2000-0000-4000-8000-0000000000c4', tenant_id: tenant, legal_entity_id: company, actor_type: 'tenant_user',
    permission: ['inventory.movements.create', 'inventory.stock.read'], iss: settings.Issuer, aud: settings.Audience,
    nbf: now - 30, iat: now - 30, exp: now + 600
}));
const token = `${header}.${payload}.${b64(crypto.createHmac('sha256', Buffer.from(settings.Secret, 'utf8')).update(`${header}.${payload}`).digest())}`;

const body = {
    idempotencyKey: 'ui2-grpo-' + crypto.randomUUID(),
    movementType: 'GOODS_RECEIPT_PO',
    itemId: '006bfd79-7146-42eb-beb7-7977f271871a',
    skuId: 'a0a0a0a0-0000-4000-8000-0000000000a1',
    skuLevel: 'Gsku',
    toWarehouseId: warehouseId,
    toLocationId: locationId,
    quantity: '10',
    uomId: 'ADET',
    lotId: 'LOT-UI2-PO',
    toStockStatus: 'AVAILABLE',
    sourceModule: 'MOD-0142',
    sourceType: 'GOODS_RECEIPT',
    sourceDocumentId: 'GRN-UI2-1',
    sourceLineId: '1',
    transactionDate: new Date().toISOString(),
    postingDate: new Date().toISOString()
};

(async () => {
    const res = await fetch('http://localhost:5090/api/inventory/movements', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}`, 'X-Tenant-Id': tenant, 'Idempotency-Key': body.idempotencyKey },
        body: JSON.stringify(body)
    });
    const text = await res.text();
    let parsed; try { parsed = JSON.parse(text); } catch { parsed = text; }
    const record = {
        step: 'UI2 — one automatic movement (GOODS_RECEIPT_PO, MOD-0142) so S-2 shows a row without Correct',
        request: { method: 'POST', url: 'http://localhost:5090/api/inventory/movements', headers: { Authorization: 'Bearer ***(masked, test JWT: inventory.movements.create + inventory.stock.read, legal_entity_id claim = the selected company)', 'X-Tenant-Id': tenant, 'Idempotency-Key': body.idempotencyKey }, body },
        response: { status: res.status, xCorrelationId: res.headers.get('x-correlation-id'), body: parsed }
    };
    fs.writeFileSync(out, JSON.stringify(record, null, 2));
    console.log(res.status, typeof parsed === 'object' ? (parsed.transactionNumber || parsed.error?.code) : text.slice(0, 200));
})();
