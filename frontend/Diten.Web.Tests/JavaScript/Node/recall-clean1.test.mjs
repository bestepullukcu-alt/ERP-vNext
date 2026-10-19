// WP-MVP1-FU-CLEAN1 — recall page rules, run by Node against the SHIPPED files (not copies):
//   frontend/Diten.Web/wwwroot/assets/js/Recall/case-view.js  (FU-0177-HEAD-NAMES: a person is a name or "unknown user", never the id)
// RecallClean1NodeTests (RecallClean1WebTests.cs) runs this file from `dotnet test`.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const js = (name) => readFileSync(path.join(repo, `frontend/Diten.Web/wwwroot/assets/js/Recall/${name}`), 'utf8');
const pure = vm.createContext({ window: {}, console, URLSearchParams });
const { RecallCaseView: view } = vm.runInContext(`${js('case-view.js')}\n;({ RecallCaseView })`, pure);
const t = (key) => ({ 'Recall.Case.UnknownUser': 'Bilinmeyen kullanıcı' }[key] ?? key);
const ID = '33333333-3333-4333-8333-333333333333';

test('FU-0177-HEAD-NAMES: the opener is the name the Web read, else "unknown user" — never the id as a name', () => {
    assert.equal(view.personName({ userId: ID, name: 'Ayşe Demir' }, t), 'Ayşe Demir');
    assert.equal(view.personName({ userId: ID }, t), 'Bilinmeyen kullanıcı');
    assert.equal(view.personName({ userId: ID, name: '' }, t), 'Bilinmeyen kullanıcı');
    assert.notEqual(view.personName({ userId: ID }, t), ID);
    assert.equal(view.personName(null, t), '');
    assert.equal(view.personName(undefined, t), '');
});

test('the case page draws every person of the head through that rule (opener, closer, canceller, timeline)', () => {
    const page = js('case-detail.js');
    assert.match(page, /const person = \(u\) => \(u\?\.userId && !u\?\.name \? `<span title="\$\{esc\(u\.userId\)\}">\$\{esc\(view\.personName\(u, t\)\)\}<\/span>` : esc\(view\.personName\(u, t\)\)\);/);
    assert.match(page, /byId\('rc-h-opened'\)\.innerHTML = `\$\{person\(c\.owner\)\}/);
    assert.match(page, /t\('Recall\.Case\.ClosedInfo', view\.personName\(c\.closure\.closedBy, t\)/);
    assert.match(page, /t\('Recall\.Case\.CancelledInfo', view\.personName\(c\.cancellation\.cancelledBy, t\)/);
    assert.doesNotMatch(page, /closedBy\?\.name \|\| c\.closure\.closedBy\?\.userId/);
});

// FU-0177-PLAN-OWNER — the plan owner picker (drill-view.js rules; drills.js wires them)
const { RecallDrillView: drill } = vm.runInContext(`${js('drill-view.js')}\n;({ RecallDrillView })`, vm.createContext({ window: {}, console, URLSearchParams }));
const ME = '11111111-1111-4111-8111-111111111111';
const OWNER = '22222222-2222-4222-8222-222222222222';

test('FU-0177-PLAN-OWNER: the search door names only the text (cut at 100), the options are the current owner then uuid + label people, the saved owner is the picked one', () => {
    assert.equal(drill.ownerSearch('  Ayşe '), '/Recall/lookups/users?q=Ay%C5%9Fe');
    assert.equal(drill.ownerSearch('x'.repeat(300)), `/Recall/lookups/users?q=${'x'.repeat(100)}`);
    const current = { value: OWNER, text: 'Mevcut Sorumlu' };
    const options = JSON.parse(JSON.stringify(drill.ownerOptions([{ value: ID, text: 'Ayşe Demir' }, { value: 'nope', text: 'X' }, { value: ID.toUpperCase(), text: 'Ayşe' },
        { value: OWNER, text: 'again' }, { value: ME, text: '' }], current)));
    assert.deepEqual(options, [current, { value: ID, text: 'Ayşe Demir' }]);
    assert.deepEqual(JSON.parse(JSON.stringify(drill.ownerOptions(null, current))), [current]);
    assert.equal(drill.ownerOf(ID, { owner: { userId: OWNER } }, ME), ID);
    assert.equal(drill.ownerOf('', { owner: { userId: OWNER } }, ME), OWNER);
    assert.equal(drill.ownerOf('not-a-uuid', null, ME), ME);
    const page = js('drills.js');
    assert.match(page, /ownerUserId: view\.ownerOf\(byId\('rc-plan-owner-select'\)\?\.value, state\.plan, me\)/);
    assert.match(page, /const reply = await ui\.get\(view\.ownerSearch\(byId\('rc-plan-owner-search'\)\.value\)\);/);
    assert.match(page, /if \(event\.key === 'Enter'\) \{ event\.preventDefault\(\); findOwner\(\); \}/);
});
