// WP-MVP1-NAMES1 (FU-0173-UI-USER-01) — the "Kaydeden" rule of the movement history, run by Node against the SHIPPED file
// frontend/Diten.Web/wwwroot/assets/js/Inventory/StockMovements/people.js; StockMovementNamesTests (InventoryRecordedByTests.cs) runs it.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const js = (rel) => readFileSync(path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js/Inventory/StockMovements', rel), 'utf8');
const { StockMovementPeople: people } = vm.runInContext(`${js('people.js')}\n;({ StockMovementPeople })`, vm.createContext({ window: {}, console }));
const ID = '0d9c8b7a-6f5e-4d3c-8b1a-0f9e8d7c6b5a';
const UNKNOWN = 'Bilinmeyen kullanıcı';

test('the recorded name is shown; without one the cell says unknown user and the id is only the title — never the id as a name', () => {
    assert.equal(people.text({ createdById: ID, createdByName: 'Ayşe Demir' }, UNKNOWN), 'Ayşe Demir');
    assert.equal(people.html({ createdById: ID, createdByName: 'Ayşe Demir' }, UNKNOWN), 'Ayşe Demir');
    assert.equal(people.text({ createdById: ID }, UNKNOWN), UNKNOWN);
    assert.equal(people.html({ createdById: ID }, UNKNOWN), `<span title="${ID}">${UNKNOWN}</span>`);
    assert.notEqual(people.text({ createdById: ID }, UNKNOWN), ID);
    assert.equal(people.text({}, UNKNOWN), '');                                  // a row without a poster says nothing
    assert.equal(people.html({ createdById: ID, createdByName: '<b>x</b>' }, UNKNOWN), '&lt;b&gt;x&lt;/b&gt;'); // text, escaped
});

test('the list draws the cell through that rule, in its own column after Source', () => {
    const index = js('index.js');
    assert.match(index, /\{ data: 'sourceModule', name: 'source' \},\s*\{ data: 'createdByName', name: 'recordedBy' \},/);
    assert.match(index, /const recordedBy = \(full\) => window\.StockMovementPeople\.html\(full, L\(\)\.UnknownUser\);/);
    assert.match(index, /\{ targets: 11, render: \(data, type, full\) => recordedBy\(full\) \},/);
    assert.match(index, /\{ targets: \[12, 13\], render: \(data\) => esc\(formatDate\(data\)\) \},/);
});
