// WP-MVP1-FU-CLEAN1 (FU-QUI1-ESC-TEST, FU-QUI1-LOTSTATUS) — guards over the SHIPPED page scripts (frontend/Diten.Web/wwwroot/assets/js):
//   1. every page script parses — classic scripts through node:vm, the few ES modules through `node --input-type=module --check`
//      (Node's own parser; no package);
//   2. in the MVP-1 modules (Quarantine, Inventory, Traceability, Expiry, Recall) the global confirm's TEXT options (subtext, entityName) are
//      never escaped by the caller (the dialog escapes them — a second escape shows `&amp;`) and never carry markup (outerHTML / innerHTML /
//      a literal tag): a form body goes through subtextHtml.
// WebScriptsGuardNodeTests.cs runs this file from `dotnet test`.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import vm from 'node:vm';

const repo = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../..');
const root = path.join(repo, 'frontend/Diten.Web/wwwroot/assets/js');
const walk = (dir) => readdirSync(dir, { withFileTypes: true }).flatMap((e) => e.isDirectory() ? walk(path.join(dir, e.name))
    : /\.m?js$/.test(e.name) ? [path.join(dir, e.name)] : []);
const rel = (file) => path.relative(root, file).replace(/\\/g, '/');
const all = walk(root);
const MODULES = ['Quarantine', 'Inventory', 'Traceability', 'Expiry', 'Recall'];
const owned = all.filter((f) => MODULES.includes(rel(f).split('/')[0]));

// a value of a TEXT option is "bad" when it is escaped by the caller or carries markup
const ESCAPED = /\b(esc\w*|escape\w*|DitenText)\s*\(/;
const MARKUP = /\.(outer|inner)HTML\b|['"`][^'"`\n]*<[a-zA-Z]/;
export const textViolations = (source) => {
    const found = [];
    const line = (index) => source.slice(0, index).split('\n').length;
    // the value up to the next option (a comma, a closing brace or the line end)
    for (const m of source.matchAll(/\b(subtext|entityName)\s*:\s*([^,\n}]+)/g)) {
        if (ESCAPED.test(m[2]) || MARKUP.test(m[2])) found.push(`${line(m.index)}: ${m[0].trim().slice(0, 100)}`);
    }
    // the shorthand `subtext,` takes a local variable: check what it was given
    if (/[{,]\s*(subtext|entityName)\s*[,}]/.test(source)) {
        for (const m of source.matchAll(/\b(?:const|let|var)\s+(subtext|entityName)\s*=\s*([\s\S]{0,2000}?);\s*\n/g)) {
            if (ESCAPED.test(m[2]) || MARKUP.test(m[2])) found.push(`${line(m.index)}: shorthand ${m[1]} = ${m[2].slice(0, 60).replace(/\s+/g, ' ')}`);
        }
    }
    return found;
};

test('every page script parses (classic through node:vm, ES modules through node --check)', () => {
    assert.ok(all.length > 700, `only ${all.length} scripts found`);
    const failures = [];
    let modules = 0;
    for (const file of all) {
        const source = readFileSync(file, 'utf8');
        try {
            new vm.Script(source, { filename: file });
        } catch (error) {
            if (!(error instanceof SyntaxError) || !/\b(import|export)\b/.test(error.message)) {
                failures.push(`${rel(file)}: ${error.message.split('\n')[0]}`);
                continue;
            }
            modules++;
            const check = spawnSync(process.execPath, ['--input-type=module', '--check'], { input: source, encoding: 'utf8' });
            if (check.status !== 0) failures.push(`${rel(file)} (module): ${(check.stderr || '').split('\n').find((l) => /Error/.test(l)) || 'parse error'}`);
        }
    }
    assert.deepEqual(failures, []);
    assert.ok(modules >= 1, 'the ES-module path was never exercised');
});

test('MVP-1 modules: the confirm TEXT options are neither escaped by the caller nor markup', () => {
    assert.ok(owned.length > 50, `only ${owned.length} MVP-1 scripts found`);
    const violations = owned.flatMap((file) => textViolations(readFileSync(file, 'utf8')).map((v) => `${rel(file)}:${v}`));
    assert.deepEqual(violations, []);
});

test('the guard itself catches each bad form (escape, outerHTML, literal tag, shorthand)', () => {
    assert.equal(textViolations("showConfirm(t, null, { subtext: esc(x) })").length, 1);
    assert.equal(textViolations("showConfirm(t, null, { subtext: ui.esc(x) })").length, 1);
    assert.equal(textViolations("showConfirm(t, null, { subtext: body.outerHTML })").length, 1);
    assert.equal(textViolations("showConfirm(t, null, { entityName: `<b>${x}</b>` })").length, 1);
    assert.equal(textViolations("const subtext = `<p>${a}</p>`;\nshowConfirm(t, null, { type: 'x', subtext, ok: 1 });").length, 1);
    assert.equal(textViolations("showConfirm(t, null, { subtext: t('A'), subtextHtml: body.outerHTML })").length, 0);
});

test('the form bodies of the lot status and lot migration dialogs go through subtextHtml (FU-QUI1-LOTSTATUS)', () => {
    const status = readFileSync(path.join(root, 'Traceability/Lots/status.js'), 'utf8');
    assert.match(status, /subtextHtml: body\.outerHTML/);
    assert.match(readFileSync(path.join(root, 'Traceability/LotMigration/index.js'), 'utf8'), /subtextHtml: bodyHtml/);
    assert.match(readFileSync(path.join(root, 'Quarantine/blocked.js'), 'utf8'), /subtextHtml: body\.outerHTML/);
});
