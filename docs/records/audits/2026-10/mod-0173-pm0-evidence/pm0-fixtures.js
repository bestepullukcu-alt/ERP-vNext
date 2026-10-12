// WP-0173-PM0 — runtime fixtures of the PROBE chain only (copy of the WP-0173-UI2 fixtures). The ONE difference: the gateway
// copy gets NO /api/product-master route — the Web reaches PRODUCT-MASTER only through the bridge under test. Nothing here is shipped, nothing touches the user's
// fleet. Run by run-ui2-env.ps1:
//
//   node ui2-fixtures.js spec <lot|serial> <committed product-master yaml> <out yaml>
//       A COPY of the frozen PRODUCT-MASTER-BUNDLE spec whose EXAMPLES name one real MDM product (AKKORA,
//       GP-000000000002 — Model A selector data) and one probe GSKU, so the Prism mock (a static example server) answers
//       the same ids the UI's Model A picker shows. Variant "lot": lotControlled true; "serial": serialControlled true.
//       Units: ADET (base), KOLI → ADET 12, PALET → KOLI 40, and DESTE → PAKET 10 (no path to the base: the
//       UOM_CONVERSION_MISSING case). Schema and paths are untouched.
//   node ui2-fixtures.js gateway <copied ocelot.json> <evidence json>
//       The gateway COPY's routes: the four "Port": 5066 → the probe service (as LOC2), the Model A selector routes
//       /api/global-products, /api/gskus, /api/lskus (+ {everything}) → the probe mock (measured: dev MDM has 0 GSKU /
//       0 LSKU, and its 177 global products all belong to another tenant than the seed user's), and
//       /api/product-master/{everything} → the probe Prism (FU-PM-01: the committed gateway has NO product-master
//       route). Every change is listed in the evidence file.
//   node ui2-fixtures.js skumock <port>
//       A localhost-only stand-in for MDM's Model A selectors (GET /api/global-products/selector, /api/gskus,
//       /api/lskus — the MDM response shape), logging every request path so the evidence shows which paths the UI used.
'use strict';

const fs = require('fs');
const http = require('http');

const PRODUCT = { id: '006bfd79-7146-42eb-beb7-7977f271871a', code: 'GP-000000000002', name: 'AKKORA' };
const GSKU = { id: 'a0a0a0a0-0000-4000-8000-0000000000a1', code: 'GSKU-000000000002-01' };

function spec(variant, input, output) {
    let text = fs.readFileSync(input, 'utf8').replace(/\r\n/g, '\n');
    const replace = (from, to) => {
        if (!text.includes(from)) throw new Error(`spec: '${from}' not found`);
        text = text.split(from).join(to);
    };
    replace('b1f2c3d4-0000-0000-0000-000000000001', PRODUCT.id);
    replace('c3d4e5f6-0000-0000-0000-000000000002', GSKU.id);
    replace('canonicalCode: GP-000123', `canonicalCode: ${PRODUCT.code}`);
    replace('name: Amoksisilin 500mg Kapsül', `name: ${PRODUCT.name}`);
    replace('canonicalCode: LSKU-TR-000045', `canonicalCode: ${GSKU.code}`);
    replace('skuLevel: Lsku', 'skuLevel: Gsku');
    replace('baseUomId: EA', 'baseUomId: ADET');
    replace('packUomCode: BOX', 'packUomCode: KOLI');
    replace('packQuantity: "10"', 'packQuantity: "12"');
    replace('                  - { fromUom: BOX, toUom: EA, numerator: "10", denominator: "1" }\n                  - { fromUom: PALLET, toUom: BOX, numerator: "100", denominator: "1" }',
        '                  - { fromUom: KOLI, toUom: ADET, numerator: "12", denominator: "1" }\n                  - { fromUom: PALET, toUom: KOLI, numerator: "40", denominator: "1" }\n                  - { fromUom: DESTE, toUom: PAKET, numerator: "10", denominator: "1" }');
    if (variant === 'serial') {
        replace('lotControlled: true', 'lotControlled: false');
        replace('serialControlled: false', 'serialControlled: true');
    } else if (variant !== 'lot') {
        throw new Error('variant: lot | serial');
    }
    fs.writeFileSync(output, text, 'utf8');
    console.log(`spec ${variant} → ${output}`);
}

function gateway(file, evidence) {
    const raw = fs.readFileSync(file, 'utf8').replace(/^﻿/, '');
    const config = JSON.parse(raw);
    const changes = [];
    for (const route of config.Routes) {
        const up = route.UpstreamPathTemplate;
        for (const target of route.DownstreamHostAndPorts || []) {
            if (target.Port === 5066) {
                target.Port = 5067;
                changes.push(`${up}: port 5066 → 5067 (probe InventoryService)`);
            }
            if (/^\/api\/(gskus|lskus|global-products)(\/\{everything\})?$/.test(up)) {
                changes.push(`${up}: ${target.Host}:${target.Port} → 127.0.0.1:4012 (probe Model A mock; dev MDM: 0 GSKU / 0 LSKU, 0 products in the seed user's tenant)`);
                target.Host = '127.0.0.1';
                target.Port = 4012;
            }
        }
    }
    // PM0: no product-master route — the committed gateway has none and the bridge (Inventory__ProductMasterBaseUrl) is
    // what is measured.
    if (config.Routes.some((r) => r.UpstreamPathTemplate.startsWith('/api/product-master'))) throw new Error('product-master route present in the committed gateway');
    fs.writeFileSync(file, JSON.stringify(config, null, 2), 'utf8');
    const expected = 4 + 6;
    fs.writeFileSync(evidence, JSON.stringify({ changes, count: changes.length, expected }, null, 2), 'utf8');
    if (changes.length !== expected) throw new Error(`expected ${expected} changes, made ${changes.length}`);
    console.log(changes.join('\n'));
}

function skumock(port) {
    const page = (items, url) => JSON.stringify({
        data: { items, pageNumber: Number(url.searchParams.get('pageNumber') || 1), pageSize: Number(url.searchParams.get('pageSize') || 20), totalCount: items.length },
        isSuccessful: true
    });
    const handler = (req, res) => {
        const url = new URL(req.url, `http://localhost:${port}`);
        console.log(`${new Date().toISOString()} ${req.method} ${url.pathname}${url.search}`);
        if (req.method === 'GET' && url.pathname === '/api/global-products/selector') {
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(page([{ id: PRODUCT.id, canonicalCode: PRODUCT.code, globalProductName: PRODUCT.name }], url));
            return;
        }
        if (req.method === 'GET' && url.pathname === '/api/gskus') {
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(page([{
                id: GSKU.id, canonicalCode: GSKU.code, globalProductId: PRODUCT.id, globalProductCanonicalCode: PRODUCT.code,
                globalProductName: PRODUCT.name, packQuantity: 12, packUomCode: 'KOLI', lifecycleStatus: 'IdentityApproved', version: 1
            }], url));
            return;
        }
        if (req.method === 'GET' && url.pathname === '/api/lskus') {
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(page([], url));
            return;
        }
        res.writeHead(404, { 'Content-Type': 'application/json' });
        res.end('{}');
    };
    for (const host of ['127.0.0.1', '::1']) http.createServer(handler).listen(Number(port), host, () => console.log(`sku mock on ${host}:${port}`));
}

const [mode, a, b, c] = process.argv.slice(2);
if (mode === 'spec') spec(a, b, c);
else if (mode === 'gateway') gateway(a, b);
else if (mode === 'skumock') skumock(a);
else { console.error('usage: spec|gateway|skumock'); process.exit(2); }
