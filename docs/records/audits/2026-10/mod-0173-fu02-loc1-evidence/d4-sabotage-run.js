// WP-0173-LOC1 D4 — sabotage proofs. For each guard: break it in the PRODUCTION source → build → run its tests (must be
// RED) → restore with `git checkout -- <file>` → run again (must be GREEN). Finally `git diff --stat` must be empty.
// Guard texts are written with "\n"; they are matched against the file's own line ending.
// Usage (repo root): node <this file> <log file>
const fs = require('fs');
const { execSync } = require('child_process');
const log = process.argv[2];
const svc = 'services/Diten.InventoryService';
const src = `${svc}/src`;
const testProject = `${svc}/tests/Diten.InventoryService.Tests`;
const out = [];
const say = line => { out.push(line); console.log(line); };
const CR = String.fromCharCode(13);
const LF = String.fromCharCode(10);

const sabotages = [
  {
    id: 'K-01 level rule', file: `${src}/Diten.InventoryService.Domain/Rules/LocationHierarchyRules.cs`,
    from: ': AllowedParents[type].Contains(parentType.Value);', to: ': true;',
    filter: 'FullyQualifiedName~LocationHierarchyTests.AC01_every_type_parent_pair'
  },
  {
    id: 'K-02 code uniqueness (case-insensitive key)', file: `${src}/Diten.InventoryService.Domain/Rules/LocationHierarchyRules.cs`,
    from: 'public static string NormalizeCode(string code) => code.ToUpperInvariant();', to: 'public static string NormalizeCode(string code) => code;',
    filter: 'FullyQualifiedName~LocationCodeAndScopeTests.AC02_code_is_unique'
  },
  {
    id: 'K-03 company isolation (LE filter of every location query)', file: `${src}/Diten.InventoryService.Persistence/Repositories/LocationRepository.cs`,
    from: '        => Builders<Location>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId)\n           & Builders<Location>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)\n           & Builders<Location>.Filter.Eq(x => x.IsDeleted, false);',
    to: '        => Builders<Location>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId)\n           & Builders<Location>.Filter.Eq(x => x.IsDeleted, false);',
    filter: 'FullyQualifiedName~LocationCodeAndScopeTests.AC03_a_parent_of_another_company'
  },
  {
    id: 'K-05 deactivate / archive with stock', file: `${src}/Diten.InventoryService.Application/Features/Locations/Handlers/CommandHandlers/ChangeLocationStatusHandler.cs`,
    from: 'if ((await tx.StockPresenceAsync(node, ct)).Any)', to: 'if (false && (await tx.StockPresenceAsync(node, ct)).Any)',
    filter: 'FullyQualifiedName~LocationLifecycleTests.AC05_deactivate_or_archive_with_stock'
  },
  {
    id: 'K-06 parent must be Active to activate', file: `${src}/Diten.InventoryService.Application/Features/Locations/Handlers/CommandHandlers/ChangeLocationStatusHandler.cs`,
    from: 'if (parent is null || parent.Status != LocationStatus.Active)', to: 'if (false)',
    filter: 'FullyQualifiedName~LocationLifecycleTests.AC06'
  },
  {
    id: 'K-07 move with stock', file: `${src}/Diten.InventoryService.Application/Features/Locations/Handlers/CommandHandlers/UpdateLocationHandler.cs`,
    from: 'if ((await Presence()).Any) return HasStock("Stock is held at or below this location; it cannot be moved.");', to: '',
    filter: 'FullyQualifiedName~LocationLifecycleTests.AC07_a_move_with_stock'
  },
  {
    id: 'K-11 history row in the change transaction', file: `${src}/Diten.InventoryService.Application/Features/Locations/Handlers/CommandHandlers/UpdateLocationHandler.cs`,
    from: '                await tx.AppendHistoryAsync(history, ct);', to: '',
    filter: 'FullyQualifiedName~LocationHistoryBulkIdempotencyTests.AC11_create_and_every_changed_field'
  },
  {
    id: 'K-13 in-transaction location status check of the ledger', file: `${src}/Diten.InventoryService.Persistence/Repositories/InventoryLedgerRepository.cs`,
    from: '                         & f.Eq(x => x.Status, LocationStatus.Active)\n', to: '',
    filter: 'FullyQualifiedName~LocationLedgerTests.AC13_a_reversal_into_a_location_that_became_inactive'
  }
];

function run(cmd) {
  try { return { code: 0, text: execSync(cmd, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 64 * 1024 * 1024 }) }; }
  catch (e) { return { code: e.status ?? 1, text: (e.stdout || '') + (e.stderr || '') }; }
}

function testRun(filter) {
  const build = run(`dotnet build ${testProject} -nologo -v q`);
  if (build.code !== 0) return { exit: -1, summary: 'BUILD FAILED', failedNames: [] };
  const r = run(`dotnet test ${testProject} --no-build -nologo --filter "${filter}"`);
  const lines = r.text.split(LF).map(l => l.replace(CR, ''));
  const summary = lines.filter(l => /(Başarılı|Başarısız)!/.test(l)).join(' ').trim();
  const failedNames = lines.filter(l => /^\s+Başarısız /.test(l)).map(l => l.trim());
  const i = lines.findIndex(l => l.includes('Hata İletisi'));
  const message = i >= 0 ? lines[i + 1].trim().slice(0, 300) : undefined;
  return { exit: r.code, summary, failedNames, message };
}

say(`# WP-0173-LOC1 D4 sabotage run — HEAD ${run('git rev-parse --short HEAD').text.trim()} — ${new Date().toISOString()}`);
let all = true;
for (const s of sabotages) {
  const original = fs.readFileSync(s.file, 'utf8');
  const eol = original.includes(CR + LF) ? CR + LF : LF;
  const from = s.from.split(LF).join(eol);
  const to = s.to.split(LF).join(eol);
  const before = run(`git hash-object ${s.file}`).text.trim();
  if (!original.includes(from)) throw new Error(`${s.id}: guard text not found in ${s.file}`);
  fs.writeFileSync(s.file, original.replace(from, () => to));
  say(`\n## ${s.id}\nfile: ${s.file}\nremoved: ${JSON.stringify(s.from.trim())}\nreplaced by: ${JSON.stringify(s.to.trim())}\ntests: ${s.filter}`);
  const red = testRun(s.filter);
  say(`SABOTAGED → exit ${red.exit} ${red.summary}`);
  for (const n of red.failedNames) say(`  red: ${n}`);
  if (red.message) say(`  first failure: ${red.message}`);
  run(`git checkout -- ${s.file}`);
  const after = run(`git hash-object ${s.file}`).text.trim();
  const green = testRun(s.filter);
  say(`RESTORED (blob ${after === before ? 'identical' : 'DIFFERENT'} ${after.slice(0, 12)}) → exit ${green.exit} ${green.summary}`);
  const pass = red.exit > 0 && green.exit === 0 && after === before;
  all = all && pass;
  say(`VERDICT: ${pass ? 'PASS (red when broken, green when restored)' : 'FAIL'}`);
}
const diff = run('git diff --stat').text.trim();
say(`\nfinal git diff --stat: ${diff.length === 0 ? '(empty)' : diff}`);
say(`ALL: ${all && diff.length === 0 ? 'PASS' : 'FAIL'}`);
fs.writeFileSync(log, out.join(LF) + LF);
