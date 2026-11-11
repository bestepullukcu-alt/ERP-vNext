// WP-MVP1-FU-CLEAN2 (D-KAR-1, owner decision A 2026-10-19) — the Quarantine pages show a person by the ONE rule of MVP-1, run by Node against
// the SHIPPED files: quarantine-ui.js (QuarantineUi.person / personHtml) on top of the shared people.js (StockMovementPeople) — the name, else
// "Bilinmeyen kullanıcı", the id ONLY as the hover title; the FU-0175-Q5-02 short-id display is gone. QuarantinePeopleTests.cs runs it.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const web = (rel) => readFileSync(path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js', rel), 'utf8');
const UNKNOWN = 'Bilinmeyen kullanıcı';
const window = { QuarantineL10n: { 'Quarantine.Common.UnknownUser': UNKNOWN } };
const document = { getElementById: () => null, documentElement: { lang: 'tr' } };
const context = vm.createContext({ window, document, console, Intl });
vm.runInContext(web('Inventory/StockMovements/people.js'), context);
vm.runInContext(web('Quarantine/quarantine-ui.js'), context);
const ui = window.QuarantineUi;
const ID = '0d9c8b7a-6f5e-4d3c-8b1a-0f9e8d7c6b5a';

test('a named person shows the name; an unnamed one says unknown user with the id only as the title', () => {
    assert.equal(ui.person({ userId: ID, name: 'Zehra Yıldız' }), 'Zehra Yıldız');
    assert.equal(ui.personHtml({ userId: ID, name: 'Zehra Yıldız' }), 'Zehra Yıldız');
    assert.equal(ui.person({ userId: ID }), UNKNOWN);
    assert.equal(ui.personHtml({ userId: ID }), `<span title="${ID}">${UNKNOWN}</span>`);
    assert.equal(ui.person(null), '');
    assert.equal(ui.personHtml({ userId: ID, name: '<b>x</b>' }), '&lt;b&gt;x&lt;/b&gt;');
});

test('the id never appears as text — not whole, not the 8-character short id of FU-0175-Q5-02', () => {
    const text = ui.personHtml({ userId: ID }).replace(/<[^>]*>/g, '');
    assert.equal(text, UNKNOWN);
    assert.ok(!text.includes(ID.slice(0, 8)), 'the short id is gone (D-KAR-1)');
    assert.equal(ui.unknownPerson, undefined, 'the short-id helper is no longer exported');
});

test('the rule is the shared one (no copy) and every Quarantine person goes through it', () => {
    const qui = web('Quarantine/quarantine-ui.js');
    assert.match(qui, /window\.StockMovementPeople\.text\(personRow\(ref\), t\('Quarantine\.Common\.UnknownUser'\)\)/);
    assert.match(qui, /window\.StockMovementPeople\.html\(personRow\(ref\), t\('Quarantine\.Common\.UnknownUser'\)\)/);
    assert.doesNotMatch(qui, /slice\(0, 8\)\} · /);
    const details = web('Quarantine/details.js');
    for (const call of ['ui.personHtml(h.by)', 'ui.personHtml(decision.decider)', 'ui.personHtml(wf.approver)', 'ui.personHtml(h.createdBy)'])
        assert.ok(details.includes(call), call);
    assert.doesNotMatch(details, /ui\.person\(/);
    const settings = web('Quarantine/settings.js');
    assert.ok(settings.includes("ui.personHtml({ userId: id, name: labels.users[id] })"));
    assert.doesNotMatch(settings, /unknownPerson/);
});
