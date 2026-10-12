// WP-0173-LOC1 D5 — proves the LOCATION v1 content survives the v1.1 edit unchanged.
// Usage (repo root): node <this file>   → prints the report (base 01118cf0e vs the working file).
const fs = require('fs');
const crypto = require('crypto');
const { execSync } = require('child_process');
// Found by FILE NAME under the docs tree (the repo's DocsPathGuardTests forbid naming a docs folder outside the five).
const file = execSync('git ls-files -- "*location.openapi.yaml"', { encoding: 'utf8' }).trim();
const strip = t => t.split('\r').join('');
const v10 = strip(execSync(`git show 01118cf0e:${file}`, { encoding: 'utf8' })).split('\n');
const v11 = strip(fs.readFileSync(file, 'utf8')).split('\n');
const sha = t => crypto.createHash('sha256').update(t).digest('hex');

// 1. Every v1 line is still present, in order (except the two info lines the WP requires to change).
let j = 0;
const missing = [];
for (const line of v10) {
  const k = v11.indexOf(line, j);
  if (k < 0) missing.push(line); else j = k + 1;
}
console.log(`v1.0.0 lines: ${v10.length}, v1.1.0 lines: ${v11.length}; v1 lines not found in order: ${missing.length}`);
for (const m of missing) console.log(`  ${m}`);

// 2. The four frozen operations, block by block (from the verb line to the next sibling key).
function block(lines, op) {
  const i = lines.findIndex(l => l.includes(`operationId: ${op}`));
  let s = i;
  while (!/^    (get|post|patch):/.test(lines[s])) s--;
  let e = s + 1;
  while (e < lines.length && !/^ {0,4}\S/.test(lines[e])) e++;
  return lines.slice(s, e).join('\n');
}
console.log('\noperation           sha256(v1.0.0)    sha256(v1.1.0)    result');
for (const op of ['getLocation', 'listLocations', 'getLocationPath', 'validateLocations']) {
  const a = block(v10, op), b = block(v11, op);
  console.log(`${op.padEnd(20)}${sha(a).slice(0, 16)}  ${sha(b).slice(0, 16)}  ${a === b ? 'IDENTICAL' : 'DIFFERENT'} (${a.split('\n').length} lines)`);
}

// 3. The v1 components, line by line.
for (const key of ['    LocationType:', '    LocationRef:', '    Error:', '    UnknownLocation:', '  securitySchemes:']) {
  const a = block2(v10, key), b = block2(v11, key);
  console.log(`${key.trim().padEnd(20)}${sha(a).slice(0, 16)}  ${sha(b).slice(0, 16)}  ${a === b ? 'IDENTICAL' : 'DIFFERENT'}`);
}
function block2(lines, key) {
  const s = lines.findIndex(l => l.startsWith(key));
  const indent = key.length - key.trimStart().length;
  let e = s + 1;
  while (e < lines.length && (lines[e].length - lines[e].trimStart().length) > indent) e++;
  return lines.slice(s, e).join('\n');
}
