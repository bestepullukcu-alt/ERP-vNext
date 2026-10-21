// WP-MVP1-FIX-LIVE4 (FU-0176-BAND-EXEMPT, G1 live run: a COUNT_LOSS asked for a FEFO reason the service never reads, and the typed reason
// was lost) — run by Node against the SHIPPED frontend/Diten.Web/wwwroot/assets/js/Expiry/expiry-bands.js with a fake DOM and a FEFO order
// whose first lot is NOT the source row's: an exempt type gets no band and no reason field, a checked type (outbound from AVAILABLE, not
// exempt) gets the band; the reason travels only when the band asked for it; the band owns its field's requiredness and mirrors "filled"
// as a class (the shared required tracker recounts on class changes only — FU-0173-REQ-COUNTER). ExpiryBandExemptTests.cs runs it.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const source = readFileSync(path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js/Expiry/expiry-bands.js'), 'utf8');
const SOURCE_LOT = 'lot-late';

const element = (id) => {
    const set = new Set(id === 'ex-band-movement' || id === 'ex-band-reason-group' ? ['d-none'] : []);
    return {
        id, value: '', innerHTML: '', required: false, attrs: {}, listeners: {},
        get className() { return [...set].join(' '); },
        set className(v) { set.clear(); String(v).split(/s+/).filter(Boolean).forEach((c) => set.add(c)); },
        classList: { add: (c) => set.add(c), remove: (c) => set.delete(c), contains: (c) => set.has(c), toggle: (c, on) => ((on === undefined ? !set.has(c) : on) ? set.add(c) : set.delete(c)) },
        setAttribute(k, v) { this.attrs[k] = v; }, getAttribute(k) { return this.attrs[k]; },
        addEventListener(type, fn) { (this.listeners[type] ||= []).push(fn); },
        fire(type) { (this.listeners[type] || []).forEach((fn) => fn({})); },
        focus() {}
    };
};

const load = () => {
    const els = {};
    const byId = (id) => (els[id] ||= element(id));
    byId('expiry-band-l10n').textContent = JSON.stringify({ 'Expiry.Band.FefoFirst': 'Daha yakın tarihli lot var: {0} (SKT {1})', 'Expiry.Band.FefoReasonText': 'Gerekçe yazın' });
    const document = { getElementById: byId, querySelector: () => null, documentElement: { lang: 'tr' } };
    const order = { order: { eligible: [{ lotId: 'lot-early', lotNumber: 'AMX-2604', expiryDate: '2028-03-31' }, { lotId: SOURCE_LOT, lotNumber: 'AMX-2611', expiryDate: '2028-11-30' }] }, policy: { fefoOutboundMode: 'Reason' } };
    const fetch = async () => ({ ok: true, status: 200, json: async () => order });
    const window = {};
    const context = vm.createContext({ window, document, fetch, console, URLSearchParams, Date });
    vm.runInContext(source, context);
    return { bands: window.ExpiryBands, rules: window.ExpiryBandRules, byId };
};

const row = (movementType, fromStatus = 'AVAILABLE') => ({ direction: 'out', movementType, fromStatus, itemId: 'i1', sku: 'Lsku:s1', warehouseId: 'w1', lotId: SOURCE_LOT });

test('COUNT_LOSS (and every exempt type) gets NO band and NO reason field, even with an earlier lot in the FEFO order', async () => {
    for (const type of ['COUNT_LOSS', 'SCRAP', 'WRITE_OFF', 'WAREHOUSE_TRANSFER', 'LOCATION_TRANSFER', 'STATUS_TRANSFER', 'COUNT_GAIN', 'SUPPLIER_RETURN', 'REVERSAL']) {
        const { bands, byId } = load();
        await bands.movement.onSource(row(type));
        assert.ok(byId('ex-band-movement').classList.contains('d-none'), `${type}: band shown`);
        assert.ok(byId('ex-band-reason-group').classList.contains('d-none'), `${type}: reason field shown`);
        assert.equal(byId('ex-band-reason').required, false, `${type}: reason required`);
        assert.equal(bands.movement.reasonValid(), true);
    }
});

test('a checked type (an outbound from AVAILABLE, not exempt) gets the band and the required reason', async () => {
    const { bands, byId } = load();
    await bands.movement.onSource(row('GOODS_ISSUE_SALES'));
    assert.ok(!byId('ex-band-movement').classList.contains('d-none'));
    assert.match(byId('ex-band-movement').innerHTML, /AMX-2604/);
    assert.ok(!byId('ex-band-reason-group').classList.contains('d-none'));
    assert.equal(byId('ex-band-reason').required, true);
    assert.equal(byId('ex-band-reason').attrs['aria-required'], 'true');
    // from a held status the service checks nothing either
    const held = load();
    await held.bands.movement.onSource(row('GOODS_ISSUE_SALES', 'QUARANTINE'));
    assert.ok(held.byId('ex-band-movement').classList.contains('d-none'));
});

test('the reason travels only when the band asked for it; a band that closes clears and un-requires the field', async () => {
    const { bands, byId } = load();
    await bands.movement.onSource(row('GOODS_ISSUE_SALES'));
    byId('ex-band-reason').value = 'Müşteri talebiyle bu lot sevk ediliyor';
    assert.equal(bands.movement.extend({}).fefoBypassReason, 'Müşteri talebiyle bu lot sevk ediliyor');

    await bands.movement.onSource(row('COUNT_LOSS'));
    assert.equal(bands.movement.extend({}).fefoBypassReason, undefined);
    assert.equal(byId('ex-band-reason').value, '');
    assert.equal(byId('ex-band-reason').required, false);
});

test('typing the reason flips a class on the field, so the shared required tracker recounts (FU-0173-REQ-COUNTER)', async () => {
    const { bands, byId } = load();
    await bands.movement.onSource(row('GOODS_ISSUE_SALES'));
    const field = byId('ex-band-reason');
    assert.equal(field.classList.contains('ex-band-reason-filled'), false);
    field.value = 'Müşteri talebiyle bu lot sevk ediliyor';
    field.fire('input');
    assert.equal(field.classList.contains('ex-band-reason-filled'), true);
    field.value = ' ';
    field.fire('input');
    assert.equal(field.classList.contains('ex-band-reason-filled'), false);
});

test('the single Web list is the rule the band uses', () => {
    const { rules } = load();
    assert.deepEqual([...rules.FEFO_EXEMPT_TYPES].sort(), ['COUNT_GAIN', 'COUNT_LOSS', 'LOCATION_TRANSFER', 'REVERSAL', 'SCRAP', 'STATUS_TRANSFER', 'SUPPLIER_RETURN', 'WAREHOUSE_TRANSFER', 'WRITE_OFF']);
    assert.equal(rules.outboundChecked('COUNT_LOSS', 'AVAILABLE', 'l'), false);
    assert.equal(rules.outboundChecked('GOODS_ISSUE_PROD', 'AVAILABLE', 'l'), true);
    assert.equal(rules.outboundChecked('GOODS_ISSUE_PROD', 'AVAILABLE', ''), false);
});
