// Q382 — the Shipment Details date round trip, run by Node against the SHIPPED details.js (not a copy).
//
// Run it at BOTH zones; ShipmentDetailsDateNodeTests.cs does that from `dotnet test`, which CI already runs:
//   TZ=Europe/Istanbul node --test frontend/Diten.Web.Tests/JavaScript/Node/shipment-details-datetime.test.mjs
//   TZ=UTC             node --test frontend/Diten.Web.Tests/JavaScript/Node/shipment-details-datetime.test.mjs
// (name the file: since Node 21 a bare directory argument is treated as a module path and fails.)
// UTC alone cannot catch the Q374 defect: a UTC wall clock IS the local wall clock there, so the old
// `toISOString().slice(0, 16)` prefill passes. Istanbul (UTC+03:00, no daylight saving since 2016) is where it fails.
//
// What this does NOT cover: the DOM — writing the field, the zone hint, the submit handlers. Those are proven only by
// live lane runs (owner decision 2026-10-04 §2; see ../../README.md).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const source = readFileSync(path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js/SupplyChain/Shipments/details.js'), 'utf8');

// The file runs as it does in the browser; with no #shipment-details host, init() returns before touching anything else.
const context = vm.createContext({ document: { getElementById: () => null, addEventListener: () => {} }, window: {}, Intl, crypto });
const { localInputValue, parseLocalInput } = vm.runInContext(`${source}\n;ShipmentDetails`, context);

// Expected values are facts about the zones, written out by hand — not computed with the code under test.
const zones = {
    'Europe/Istanbul': {
        offsetMinutes: -180,
        prefill: { '2026-10-03T20:38:00Z': '2026-10-03T23:38', '2026-01-15T23:30:00Z': '2026-01-16T02:30' },
        gapExists: false
    },
    UTC: {
        offsetMinutes: 0,
        prefill: { '2026-10-03T20:38:00Z': '2026-10-03T20:38', '2026-01-15T23:30:00Z': '2026-01-15T23:30' },
        gapExists: true
    }
};
const zone = process.env.TZ;
const expected = zones[zone];

test('runs in one of the two required zones, and the zone is really in effect', () => {
    assert.ok(expected, `TZ must be Europe/Istanbul or UTC, got ${JSON.stringify(zone)}`);
    // Guards against a TZ that Node silently ignored: the Istanbul run would otherwise be a second UTC run.
    assert.equal(new Date(Date.UTC(2026, 9, 3, 20, 38)).getTimezoneOffset(), expected.offsetMinutes);
});

test('a Date prefills to the local wall clock', () => {
    for (const [utc, wallClock] of Object.entries(expected.prefill)) {
        assert.equal(localInputValue(new Date(utc)), wallClock, `prefill of ${utc} in ${zone}`);
    }
});

test('the prefilled string parses back to the same instant', () => {
    for (const utc of [...Object.keys(expected.prefill), '2026-03-29T00:59:00Z', '2026-12-31T22:00:00Z']) {
        const parsed = parseLocalInput(localInputValue(new Date(utc)));
        assert.ok(parsed, `round trip of ${utc} parsed`);
        assert.equal(parsed.getTime(), new Date(utc).getTime(), `round trip of ${utc} in ${zone}`);
    }
});

test('a typed local wall clock, seconds included, is read as local time', () => {
    const [utc, wallClock] = Object.entries(expected.prefill)[0];
    assert.equal(parseLocalInput(`${wallClock}:15`).getTime(), new Date(utc).getTime() + 15000);
});

test('out-of-range and malformed values are rejected, not shifted', () => {
    for (const raw of ['2026-02-30T10:00', '2026-13-01T10:00', '2026-00-10T10:00', '2026-10-32T10:00',
        '2026-10-03T24:00', '2026-10-03T23:60', '2026-10-03T23:38:60', '', '2026-10-03 23:38', '2026-10-03T23:38Z', 'now']) {
        assert.equal(parseLocalInput(raw), null, `${JSON.stringify(raw)} in ${zone}`);
    }
});

test('a daylight-saving gap hour is rejected where the zone has one', () => {
    // Istanbul skipped 03:00–03:59 on 2016-03-27 (last spring-forward before permanent UTC+03:00). UTC has no gap.
    const raw = '2016-03-27T03:30';
    if (expected.gapExists) {
        assert.equal(parseLocalInput(raw)?.toISOString(), '2016-03-27T03:30:00.000Z');
    } else {
        assert.equal(parseLocalInput(raw), null, 'a skipped local hour must not be shifted to 04:30');
        assert.equal(parseLocalInput('2016-03-27T04:30')?.toISOString(), '2016-03-27T01:30:00.000Z');
    }
});
