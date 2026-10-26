// WP-MVP1-FIX-LIVE3 (live finding 2) — the list words of the MVP-1 list pages reach window.L10n, run by Node against the SHIPPED
// frontend/Diten.Web/wwwroot/assets/js/Inventory/mvp1-list-l10n.js: a key the page did not set (absent or empty) is filled from the
// payload; a key the page set keeps the page's word. MvpOneListL10nTests.cs runs it.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const source = readFileSync(path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js/Inventory/mvp1-list-l10n.js'), 'utf8');

const run = (l10n, words) => {
    const window = { L10n: l10n, document: { getElementById: (id) => (id === 'mvp1-list-l10n' ? { textContent: JSON.stringify(words) } : null) } };
    window.window = window;
    vm.runInContext(source, vm.createContext({ window, console: { warn: () => {}, error: () => {} } }));
    return window.L10n;
};

test('a missing or empty list word is filled from the payload (the shell bridge carries no Search)', () => {
    const l10n = run({ Action: 'Eylem', Search: '' }, { Search: 'Ara', Filter: 'Filtrele', ShowAll: 'Tümünü göster' });
    assert.equal(l10n.Search, 'Ara');
    assert.equal(l10n.Filter, 'Filtrele');
    assert.equal(l10n.Action, 'Eylem');
});

test('a word the page set itself is kept', () => {
    const l10n = run({ Search: 'Lot ara…', ColumnVisibility: 'Sütunlar' }, { Search: 'Ara', ColumnVisibility: 'Sütun görünürlüğü' });
    assert.equal(l10n.Search, 'Lot ara…');
    assert.equal(l10n.ColumnVisibility, 'Sütunlar');
});
