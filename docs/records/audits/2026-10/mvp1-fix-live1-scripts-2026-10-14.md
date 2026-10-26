# WP-MVP1-FIX-LIVE1 — yığın ve sabotaj betikleri (2026-10-14)

> Kanıt: `mvp1-fix-live1-evidence-2026-10-14.md` §4.2, §5. Tek seferlik kanıt betikleri, repo kodu değil.
>
> **Yığın.** FUB2 yığınından türetildi:
> 1. `mvp1-fub2-scripts-2026-10-10.md` → Q5 `Program.cs` + FUB2 `adapt.js` + `pm-block.cs.txt`.
> 2. Aşağıdaki `adapt.js`'yi uygulayın. Değişiklikleri:
>    - DB adları `itest_live1_*`;
>    - Gateway kopyasında 5067 → kendi Inventory'm;
>    - canlıdaki lokasyon zinciri (IST → D01 → D01-A → A-01-01-01);
>    - farklı SKT'li iki AVAILABLE lot;
>    - role `expiry.dashboard.read` + `expiry.fefo-order.read`;
>    - kendi AuthService'ime `DevSeeds__MockUsers=true`.
> 3. Projeleri Release derleyin.
> 4. `dotnet run -- <worktree> <çıktı> <pm-stub.js>` çalıştırın. `<çıktı>/stop` dosyası yalnız kendi PID'lerini kapatır.
>
> **Sabotaj.** `node sabotage.js <worktree> <çıktı>`.

## stack/adapt.js

```js
// turns the FUB2 stack (mvp1-fub2-scripts-2026-10-10.md: Q5 Program.cs + FUB2 adapt.js + pm-block) into the FIX-LIVE1 one
const fs = require('fs');
const p = __dirname + '/Program.cs';
let s = fs.readFileSync(p, 'utf8');
const rep = (a, b, all) => {
  const n = s.split(a).length - 1;
  if (all ? n < 1 : n !== 1) throw new Error(`anchor x${n}: ${a.slice(0, 80)}`);
  s = all ? s.split(a).join(b) : s.replace(a, () => b);
};
rep('itest_fub2_', 'itest_live1_', true);
rep('// WP-MVP1-FUB2 runtime stack', '// WP-MVP1-FIX-LIVE1 runtime stack (from the FUB2 one)');
rep('var testPassword = "Fb2!"', 'var testPassword = "Lv1!"');
// since F5 / R2 the inventory routes (/api/locations, /api/expiry, /api/trace, /api/quarantine, /api/inventory) are on 5067
rep('var map = new Dictionary<int, int> { [5057] = 15057, [5066] = 15176,', 'var map = new Dictionary<int, int> { [5057] = 15057, [5066] = 15176, [5067] = 15176,');
// wait for the MOD-0176 keys as well
rep('await perms.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("Key", "quarantine.settings.manage")) == 0',
    'await perms.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("Key", "quarantine.settings.manage")) == 0\n                               || await perms.CountDocumentsAsync(Builders<BsonDocument>.Filter.Eq("Key", "expiry.fefo-order.read")) == 0');
rep('        "auth.users.lookup" // the people names (display-label), granted explicitly to the probe role (FU-0175-ITEM-02 is BLOCKED)\n',
    '        "auth.users.lookup",\n        "expiry.dashboard.read", "expiry.fefo-order.read" // FEFO Sırası (the live finding)\n');
rep('var roleName = "fub2-qa-"', 'var roleName = "live1-qa-"');
// a location gets a name of its own (D01 · İstanbul Merkez Depo, like the live tenant)
rep('async Task<string> Location(string type, string? parent, string code)', 'async Task<string> Location(string type, string? parent, string code, string? name = null)');
rep('parent is null ? new { idempotencyKey = k, code, name = code, type } : (object)new { idempotencyKey = k, code, name = code, type, parentId = parent }, k);',
    'parent is null ? new { idempotencyKey = k, code, name = name ?? code, type } : (object)new { idempotencyKey = k, code, name = name ?? code, type, parentId = parent }, k);');
rep('    var wh = await Location("Warehouse", await Location("Site", null, "SITE-" + run), "D01-" + run);\n    var sl = await Location("StorageLocation", wh, "SL-" + run);\n    var bin = await Location("Bin", sl, "A-01-" + run);\n    var sample = await Location("Bin", sl, "NUMUNE-" + run);',
    '    // the live chain IST → D01 → D01-A → A-01-01-01 (codes as in the owner\'s tenant)\n    var wh = await Location("Warehouse", await Location("Site", null, "IST", "İstanbul"), "D01", "İstanbul Merkez Depo");\n    var sl = await Location("StorageLocation", wh, "D01-A", "D01 A Bölgesi");\n    var bin = await Location("Bin", sl, "A-01-01-01");\n    var sample = await Location("Bin", sl, "NUMUNE-01");');
// lots with their own expiry dates (FEFO order: the earlier expiry first)
rep('async Task<string> Lot(string number, string? retest, bool release)', 'async Task<string> Lot(string number, string? retest, bool release, string expiry = "2028-01-31T00:00:00Z")');
rep('["expiryDate"] = "2028-01-31T00:00:00Z"', '["expiryDate"] = expiry');
rep('    var holdLot = await Lot("FUB2-KRN-" + run, future, release: true);     // the hold of the note / lock probe (reject path)\n    await Receive(holdLot, "1200", "QUARANTINE");\n    var holdLot2 = await Lot("FUB2-KRN2-" + run, future, release: true);   // a second hold (approve path)\n    await Receive(holdLot2, "300", "QUARANTINE");\n',
    '    var holdLot = await Lot("L-2027-0331", null, release: true, expiry: "2027-03-31T00:00:00Z");   // FEFO rank 2\n    await Receive(holdLot, "100", "AVAILABLE");\n    var holdLot2 = await Lot("L-2026-1231", null, release: true, expiry: "2026-12-31T00:00:00Z");  // FEFO rank 1 (earlier expiry)\n    await Receive(holdLot2, "50", "AVAILABLE");\n');
// the dev mock users (john.doe / jane.smith / charlie.brown) are opt-in on this base (DataSeeder.MockUsersOptInConfigurationKey)
rep('    authEnv["PlatformService__BaseUrl"] = PlatformUrl;
', '    authEnv["PlatformService__BaseUrl"] = PlatformUrl;
    authEnv["DevSeeds__MockUsers"] = "true"; // the opt-in dev mock users (DataSeeder.MockUsersOptInConfigurationKey) — own stack only
');
fs.writeFileSync(p, s);
console.log('adapted');
```

## sabotage.js

```js
// WP-MVP1-FIX-LIVE1 sabotages: each one edit, the named test must turn red, then `git checkout -- <file>` restores it and
// `git status --porcelain` must be empty. usage: node sabotage.js <worktree> <out>
const fs = require('fs');
const { execFileSync, spawnSync } = require('child_process');
const [W, out] = process.argv.slice(2);
const WEB = 'frontend/Diten.Web.Tests';
const T = 'FullyQualifiedName~Diten.Web.Tests.Contracts.LiveLookupContractTests.';
const cases = [
  { id: 's1', what: 'Expiry lookups/warehouses reads "id" again (the pre-fix Options())', file: 'frontend/Diten.Web/Controllers/ExpiryController.cs',
    a: '(LocationOptions.Parse(reply.Body) ?? []).Select(o => new { value = o.Value, text = o.Text })',
    b: '(JsonNode.Parse(reply.Body)?["items"] as JsonArray ?? new JsonArray()).OfType<JsonObject>().Select(o => new { value = o["id"]?.ToString(), text = string.Join(" · ", new[] { o["code"]?.ToString(), o["name"]?.ToString() }.Where(x => !string.IsNullOrWhiteSpace(x))) })',
    filter: T + 'A_the_FEFO_warehouse_picker_values_are_the_contract_locationId' },
  { id: 's2', what: 'the Quarantine location fix reverted (Options(body, "id", "code", "name"))', file: 'frontend/Diten.Web/Controllers/QuarantineController.cs',
    a: '(LocationOptions.Parse(reply.Body) ?? []).Select(o => new { value = o.Value, text = o.Text })',
    b: 'Options(reply.Body, "id", "code", "name")',
    filter: T + 'B_the_quarantine_sample_warehouse_and_location_options_carry_the_contract_locationId' },
  { id: 's3', what: 'the fake Gateway contract check switched off', file: 'frontend/Diten.Web.Tests/Contracts/ContractGateway.cs',
    a: 'if (errors.Count > 0) throw new ContractViolationException', b: 'if (false && errors.Count > 0) throw new ContractViolationException',
    filter: T + 'D_a_fake_location_row_with_id_instead_of_locationId_is_refused_by_the_contract' },
  { id: 's4', what: 'the ledger ignores the resolved lot numbers', file: 'frontend/Diten.Web/Controllers/StockLedgerController.cs',
    a: 'InventoryListAdapter.MapBalanceRows(rows, labels, lotNumbers)', b: 'InventoryListAdapter.MapBalanceRows(rows, labels)',
    filter: T + 'C_the_ledger_shows_lot_numbers_with_one_TRACE_read_per_SKU_and_an_unresolved_lot_keeps_its_id_with_a_hint' }
];
const git = (...a) => execFileSync('git', ['-C', W, ...a], { encoding: 'utf8' });
const results = [];
for (const c of cases) {
  const path = `${W}/${c.file}`;
  const text = fs.readFileSync(path, 'utf8');
  if (text.split(c.a).length !== 2) throw new Error(`${c.id}: anchor`);
  fs.writeFileSync(path, text.replace(c.a, () => c.b));
  const run = spawnSync('dotnet', ['test', `${W}/${WEB}`, '-nologo', '--filter', c.filter], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  const log = `${run.stdout}\n${run.stderr}`;
  fs.writeFileSync(`${out}/sabotage-${c.id}.txt`, log);
  const summary = (log.match(/(Başarısız!|Başarılı!|Failed!|Passed!)[^\n]*/) || ['(no summary)'])[0].trim();
  git('checkout', '--', c.file);
  const porcelain = git('status', '--porcelain').trim();
  const red = /Başarısız!|Failed!/.test(summary) && !/error CS/.test(log);
  results.push({ id: c.id, what: c.what, test: c.filter.split('.').pop(), summary, red, restoredClean: porcelain === '' });
  console.log(`${c.id}: ${red ? 'RED' : 'NOT RED'} — ${summary} — restored ${porcelain === '' ? 'clean' : 'DIRTY: ' + porcelain}`);
}
fs.writeFileSync(`${out}/sabotage-results.json`, JSON.stringify(results, null, 2));
```
