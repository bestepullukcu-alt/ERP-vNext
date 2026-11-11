// WP-MVP1-NAMES1 (RECALL 1.8.0, O-CLEAN1-1) — every Recall page draws a person through ONE rule (RecallCaseView.personName — FU-CLEAN1 c), run by
// Node against the SHIPPED files; RecallNames1NodeTests (RecallNames1WebTests.cs) runs it.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const dir = path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js/Recall');
const js = (name) => readFileSync(path.join(dir, name), 'utf8');
const views = vm.runInContext(`${js('case-view.js')}\n${js('cases-view.js')}\n;({ RecallCaseView, RecallCasesView })`, vm.createContext({ window: {}, console, URLSearchParams }));
const t = (key, ...a) => ({ 'Recall.Case.UnknownUser': 'Bilinmeyen kullanıcı', 'Recall.Type.Real': 'Gerçek', 'Recall.Type.Drill': 'Tatbikat' }[key] ?? key);
const ID = '33333333-3333-4333-8333-333333333333';
const fmt = { t, qty: String, date: (v) => String(v), duration: String, person: (u) => views.RecallCaseView.personName(u, t) };
const plain = (x) => JSON.parse(JSON.stringify(x));

test('Vakalar: the owner is the name the service sent, else "unknown user" with the id only as the title — never the id as a name', () => {
    const named = plain(views.RecallCasesView.cells({ rowKind: 'Case', caseId: 'c', status: 'Active', owner: { userId: ID, name: 'Ayşe Demir' } }, fmt));
    assert.deepEqual([named.owner, named.ownerTitle], ['Ayşe Demir', '']);
    const unnamed = plain(views.RecallCasesView.cells({ rowKind: 'Case', caseId: 'c', status: 'Active', owner: { userId: ID } }, fmt));
    assert.deepEqual([unnamed.owner, unnamed.ownerTitle], ['Bilinmeyen kullanıcı', ID]);
    const drill = plain(views.RecallCasesView.cells({ rowKind: 'Drill', status: 'Completed', owner: { userId: ID } }, fmt));
    assert.deepEqual([drill.owner, drill.ownerTitle], ['Bilinmeyen kullanıcı', ID]);
    assert.match(js('cases.js'), /person: \(u\) => window\.RecallCaseView\.personName\(u, t\)/);
});

test('Tatbikat / Kanıt Paketi / Ayarlar draw people through the same rule; no recall script falls back to the id as a name', () => {
    assert.match(js('drills.js'), /const person = \(u\) => window\.RecallCaseView\.personName\(u, t\);/);
    assert.match(js('evidence.js'), /esc\(window\.RecallCaseView\.personName\(x\.exportedBy, t\)\)/);
    assert.match(js('settings.js'), /window\.RecallCaseView\.personName\(settings\.updatedBy, t\)/);
    assert.match(js('settings.js'), /esc\(window\.RecallCaseView\.personName\(h\.changedBy, t\)\)/);
    for (const file of readdirSync(dir).filter((f) => f.endsWith('.js'))) {
        assert.doesNotMatch(js(file), /\?\.name \|\| [\w.?]*\?\.userId/, `${file}: a person shown by the id`);
    }
});

test('the rule is loaded on every recall page (the shared vocabulary partial), once', () => {
    const partial = readFileSync(path.join(repo, 'frontend/Diten.Web/Views/Recall/_RecallL10n.cshtml'), 'utf8');
    assert.ok(partial.indexOf('Recall/recall-ui.js') < partial.indexOf('Recall/case-view.js'), 'case-view.js after recall-ui.js in the partial');
    assert.doesNotMatch(readFileSync(path.join(repo, 'frontend/Diten.Web/Views/Recall/CaseDetails.cshtml'), 'utf8'), /Recall\/case-view\.js/);
});
