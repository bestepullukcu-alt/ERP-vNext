# MVP-1 FIX-WEBGATES v2 — kanıt (WP-MVP1-FIX-WEBGATES, prompt v2) — 2026-10-14

> **Durum: ajan sonucu, CT onayı bekliyor.** Taban ölçüldü: `cd3680d7c` (prompt'un beklediği). Dal `fix/mvp1-webgates-v2`, yalıtılmış worktree
> `C:\Users\Ali\erp-vnext-wt-webgates2`. Push yok, PR yok; main'e, MVP-1 dalına, ana checkout'a ve filoya (5000–5070, 4010) dokunulmadı;
> `AccountKindAcceptanceHost.Tests` koşulmadı. Üretim kodu (`frontend/Diten.Web/**`), servisler ve sözleşmeler değişmedi.
> Yer (docs-organization, beş soru): tek bir WP'nin tarihli, bitmiş ölçüm kaydı → 2. soru → `docs/records/audits/2026-10/`.

## 1. Commit'ler

| # | Commit | Adım |
|---|---|---|
| 1 | `cc86d069d` | `test(web)` — yeni dosya `frontend/Diten.Web.Tests/Expiry/ExpiryDataGateTests.cs` (52 test) |
| 2 | (bu commit) | bu kanıt + MOD-0176 pack §19 kısa notu |

## 2. Bulgu (CT, F5 kabulü) ve ne yapıldı

`ExpiryController`'da `list/exceptions` → `ExceptionsRead`, politika okuma → `PoliciesRead`, `data/evidence/preview` → `EvidenceExport` kapıları
tek tek kaldırıldığında bütün Expiry Web testleri yeşil kalıyordu. Açık değil (Web çağıranın token'ını iletir, InventoryService anahtarı yeniden
kontrol eder), ama koruma kanıtsızdı. Artık her veri kapısı üç yönden üretim koduna bağlı:

1. **Keşif (yansıtma):** `ExpiryController`'ın `[HttpGet/Post/Put]` taşıyan her public action'ı üç sınıftan birine düşer — **sayfa**
   (senkron `IActionResult`, gövdesi `=> View(…)`), **catch-all proxy** (şablonda `{**`), **veri kapısı** (geri kalan her şey). Elle liste yok:
   keşfedilen kapılar = tablodaki kapılar (tamlık testi).
2. **Kaynak:** tablodaki anahtarlar = action'ın kendi `if (…) return Refuse(403, …)` ifadesindeki `_permissions.Has(…)` anahtarları
   (sabit değerleri yansıtmayla çözülür); bu ifadeden önce `SendAsync` / `Gateway(` yok; kapı içindeki diğer her `_permissions.Has` bir
   "iç kapı" satırı olmak zorunda.
3. **Davranış:** anahtar(lar) YOKKEN (Web'in bildiği diğer bütün anahtarlar VAR) → **403** ve FakeGateway'e **hiç** istek yok; anahtarlardan
   herhangi BİRİ VARKEN → istek gateway'e gider ve cevap 403 değildir (pozitif kontrol).

## 3. Kapı ↔ anahtar tablosu (`cd3680d7c`, `frontend/Diten.Web/Controllers/ExpiryController.cs`)

| Action | Rota | 403 satırı | Açan anahtar(lar) — herhangi biri |
|---|---|---|---|
| `ListApproaching` | `GET list/approaching` | :109 | `expiry.dashboard.read` |
| `ListExpired` | `GET list/expired` | :118 | `expiry.dashboard.read` |
| `DashboardData` | `GET data/dashboard` | :131 | `expiry.dashboard.read` |
| `FefoOrderData` | `GET data/fefo-order` | :172 | `expiry.fefo-order.read` |
| `Export` | `GET export/{list}` | :187 | `expiry.dashboard.read` |
| `Lookups` | `GET lookups/{kind}` | :231 | `expiry.dashboard.read` · `expiry.fefo-order.read` |
| `ListExceptions` | `GET list/exceptions` | :275 | `expiry.exceptions.read` |
| `ExceptionData` | `GET data/exception/{id}` | :308 | `expiry.exceptions.read` |
| `PolicyData` | `GET data/policy` | :326 | `expiry.policies.read` |
| `PolicyProducts` | `GET data/policy/products` | :345 | `expiry.policies.read` |
| `EvidencePreview` | `GET data/evidence/preview` | :382 | `expiry.evidence.export` |
| `EvidenceExports` | `GET data/evidence/exports` | :405 | `expiry.evidence.export` |
| `EvidenceFile` | `GET evidence/file/{exportId}` | :418 | `expiry.evidence.export` |
| `BandContext` | `GET band/context` | :434–436 | `inventory.movements.create` · `trace.lots.create` · `expiry.dashboard.read` |

**İç kapılar** (açılmış bir kapının içinde, ek gateway çağrısı yalnız anahtarla):

| Kapı | Satır | Anahtar | Ek çağrı |
|---|---|---|---|
| `DashboardData` | :141 | `expiry.policies.read` | `GET /api/expiry/policy` (panel ufukları) |
| `DashboardData` | :148 | `expiry.exceptions.read` | `GET /api/expiry/exceptions?type=BELOW_MIN_REMAINING…` (son 30 gün sayacı) |

**İstisna sınıfı — "Web anahtarı yok, yetki serviste":** `ProxyRead` (`GET api/{**everything}`), `ProxyWrite` (`POST`), `ProxyReplace` (`PUT`).
Ölçülen: token YOKKEN **401** ve gateway'e istek yok; token VAR ve hiçbir Web anahtarı YOKKEN istek iletilir (yetki servisin `[HasPermission]`'ı).
Yol sınırı SEC-PROXY1'in (`Diten.Web.Tests/Security/**`), burada tekrar test edilmedi.

**Sayfalar (UAS-001) — dokunulmadı, ölçüldü:** 8 sayfa action'ı, her biri yalnız `View(…)` döndürür ve her biri mevcut bir UAS-001 testinde
render edilir: `Index`, `Approaching`, `Expired`, `FefoOrder` → `ExpiryWebTests.UAS_001_without_the_page_key_only_the_access_explanation_is_drawn`;
`Exceptions`, `ExceptionDetails`, `Policy`, `Evidence` → `ExpiryF5WebTests.UAS_001_the_new_pages_draw_only_the_explanation_without_their_key`.

## 4. Testler (`ExpiryDataGateTests`, 52)

| Test | Vaka | Ne ölçer |
|---|---|---|
| `Every_routed_action_of_ExpiryController_is_in_exactly_one_class_and_every_door_is_in_the_gate_table` | 1 | keşif = tablo (14 kapı), proxy = 3, sayfa = 8 |
| `A_page_action_only_returns_its_view` | 1 | 8 sayfanın gövdesi `=> View(` |
| `The_table_keys_are_the_keys_of_the_doors_own_403_statement` | 14 | tablo = kaynaktaki 403 ifadesi; ondan önce gateway çağrısı yok; iç kapılar tabloda |
| `Without_its_key_a_door_answers_403_and_nothing_reaches_the_gateway` | 14 | anahtar yok → 403, 0 istek |
| `With_one_of_its_keys_a_door_reaches_the_gateway` | 17 | 14 kapı × açan her anahtar (Lookups 2, BandContext 3) → istek gider |
| `An_inner_call_happens_only_with_its_key` | 2 | iç kapı anahtarı yokken ek çağrı yok, varken var |
| `The_catch_all_proxy_without_a_token_answers_401_and_sends_nothing` | 3 | token yok → 401, 0 istek; token var + Web anahtarı yok → iletilir |

Bir action'a örnek parametre gerekir (`list`, `kind`, `id`, `exportId`, `itemId`, `skuId`, `skuLevel`, `warehouseId`, `everything`); bilinmeyen
bir parametre adıyla gelen yeni kapı testte gürültülü biçimde kırılır ("no sample for parameter …").

## 5. Önce / sonra

| | Taban `cd3680d7c` | Sonra |
|---|---|---|
| `Diten.Web.Tests` | **2199 / 2199** | **2251 / 2251** (2199 + 52) |
| `dotnet build Diten.Web.csproj --no-incremental` | 0 hata, 18 uyarı | 0 hata, 18 uyarı |
| `dotnet build Diten.Web.Tests.csproj --no-incremental` | 0 hata, 24 uyarı | 0 hata, **24** uyarı — **0 yeni**; yeni dosyadan uyarı 0 (hepsi önceden var olan CRM / WorkCenter `DevScenarios.cshtml`) |

## 6. Sabotajlar (araç §8; filtre `FullyQualifiedName~Diten.Web.Tests.Expiry`, 76 test)

Uygula → build → filtre (kırmızı) → **özgün baytları geri yaz** (mtime damgalı) → build → filtre (yeşil). Döngü sonunda `ExpiryController.cs`'in
SHA-256'sı döngü öncesiyle aynı, `git diff` döngü öncesiyle bayt bayt aynı, `git diff -- frontend/Diten.Web` boş.

| # | Sabotaj | Kırmızı | Geri |
|---|---|---|---|
| s1 | `ListExceptions` (`list/exceptions`) — :275 `ExceptionsRead` kapısı silindi | 2 — `The_table_keys_…(door: "ListExceptions")`, `Without_its_key_…(door: "ListExceptions")` | 76 / 76 |
| s2 | `PolicyData` (`data/policy`) — :326 `PoliciesRead` kapısı silindi | 2 — `The_table_keys_…(door: "PolicyData")`, `Without_its_key_…(door: "PolicyData")` | 76 / 76 |
| s2b | `DashboardData` iç politika okuması — :141 `if (_permissions.Has(ExpiryKeys.PoliciesRead))` → `if (true)` (CT'nin "politika okuma"sının panel içindeki hali) | 2 — `The_table_keys_…(door: "DashboardData")`, `An_inner_call_happens_only_with_its_key(… "expiry.policies.read" …)` | 76 / 76 |
| s3 | `EvidencePreview` (`data/evidence/preview`) — :382 `EvidenceExport` kapısı silindi | 2 — `The_table_keys_…(door: "EvidencePreview")`, `Without_its_key_…(door: "EvidencePreview")` | 76 / 76 |
| s4 | `DashboardData` (`data/dashboard`, F4) — :131 `DashboardRead` kapısı silindi | 2 — `The_table_keys_…(door: "DashboardData")`, `Without_its_key_…(door: "DashboardData")` | 76 / 76 |
| s5 | geçici yeni kapı `[HttpGet("data/sabotage")] SabotageDoor()`, tabloya eklenmedi | 1 — `Every_routed_action_…_is_in_the_gate_table` | 76 / 76 |

Her kapı sabotajında **davranış testi kaynak testinden bağımsız** kırmızı: kaynak denetimi kaldırılsa bile 403 / "gateway'e istek yok" ölçümü düşer.

## 7. Diff-stat `cd3680d7c..HEAD`

```text
 .../Diten.Web.Tests/Expiry/ExpiryDataGateTests.cs  | 317 +++++++++++++++++++++
 1 file changed, 317 insertions(+)
```

Bu commit yalnız bu dosyayı ve MOD-0176 pack §19 notunu ekler (yalnız ekleme). Ortak test dosyalarına dokunulmadı.

## 8. Sabotaj aracı (`sabotage-webgates.js`, worktree kökünden `node sabotage-webgates.js`)

```js
// WP-MVP1-FIX-WEBGATES v2 sabotage tool: apply → build → run the Expiry Web tests (red expected) → restore the ORIGINAL BYTES (mtime
// stamped) → build → green. Run from the worktree root: node sabotage-webgates.js
const fs = require('fs');
const crypto = require('crypto');
const { execSync } = require('child_process');

const file = 'frontend/Diten.Web/Controllers/ExpiryController.cs';
const gate = (line, key, door) => ({ line, text: `        if (!_permissions.Has(ExpiryKeys.${key})) return Refuse(403, null);`, door });
const sabotages = [
  { id: 's1', what: '`list/exceptions` (ListExceptions) — ExceptionsRead gate removed', ...gate(275, 'ExceptionsRead', 'ListExceptions') },
  { id: 's2', what: '`data/policy` (PolicyData) — PoliciesRead gate removed', ...gate(326, 'PoliciesRead', 'PolicyData') },
  { id: 's3', what: '`data/evidence/preview` (EvidencePreview) — EvidenceExport gate removed', ...gate(382, 'EvidenceExport', 'EvidencePreview') },
  { id: 's4', what: '`data/dashboard` (DashboardData, F4) — DashboardRead gate removed', ...gate(131, 'DashboardRead', 'DashboardData') },
  { id: 's2b', what: '`data/dashboard` inner policy read — `if (_permissions.Has(ExpiryKeys.PoliciesRead))` → `if (true)`', line: 141,
    text: '        if (_permissions.Has(ExpiryKeys.PoliciesRead))', replaceWith: '        if (true)' },
  { id: 's5', what: 'a temporary new door `data/sabotage` (not in the table)', insertBefore: '    // ── plumbing',
    insert: '    [HttpGet("data/sabotage")]\r\n    public Task<IActionResult> SabotageDoor() => Task.FromResult<IActionResult>(Ok());\r\n\r\n' },
];

const sh = (cmd) => { try { return execSync(cmd, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 1 << 28 }); } catch (e) { return (e.stdout || '') + (e.stderr || ''); } };
const sha = () => crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const build = () => { const o = sh('dotnet build frontend/Diten.Web.Tests/Diten.Web.Tests.csproj -nologo -v q'); if (!/\s0 Hata/.test(o)) throw new Error('build failed\n' + o.slice(-3000)); };
const run = () => {
  const o = sh('dotnet test frontend/Diten.Web.Tests/Diten.Web.Tests.csproj --no-build -nologo --filter "FullyQualifiedName~Diten.Web.Tests.Expiry"');
  const sum = (o.match(/(Başarılı!|Başarısız!).*?Toplam:\s+\d+/) || ['?'])[0];
  const failed = [...o.matchAll(/^\s+Başarısız (Diten\.Web\.Tests\.Expiry\.[^\[\r\n]+?) \[/gm)].map(m => m[1].replace('Diten.Web.Tests.Expiry.', ''));
  return { sum, failed };
};

const original = fs.readFileSync(file); // bytes
const diffBefore = sh('git diff');
const shaBefore = sha();
for (const s of sabotages) {
  const text = original.toString('utf8');
  let changed;
  if (s.line) {
    const lines = text.split('\r\n');
    if (lines[s.line - 1] !== s.text) throw new Error(`${s.id}: line ${s.line} is not the expected gate: ${lines[s.line - 1]}`);
    if (s.replaceWith !== undefined) lines[s.line - 1] = s.replaceWith; else lines.splice(s.line - 1, 1);
    changed = lines.join('\r\n');
  } else {
    if (text.split(s.insertBefore).length !== 2) throw new Error(`${s.id}: anchor not found once`);
    changed = text.replace(s.insertBefore, () => s.insert + s.insertBefore);
  }
  fs.writeFileSync(file, changed, 'utf8');
  let red;
  try { build(); red = run(); }
  finally {
    fs.writeFileSync(file, original); // the original bytes
    const now = new Date(); fs.utimesSync(file, now, now);
  }
  build();
  const green = run();
  console.log(`\n### ${s.id} — ${s.what}`);
  console.log(`red:      ${red.sum}`);
  for (const f of red.failed) console.log(`   ${f}`);
  console.log(`restored: ${green.sum}`);
}
console.log(`\nSHA-256 of ExpiryController.cs unchanged: ${sha() === shaBefore}`);
console.log(`git diff identical before/after the loop: ${sh('git diff') === diffBefore}`);
console.log(`git diff -- frontend/Diten.Web empty: ${sh('git diff -- frontend/Diten.Web').trim() === ''}`);
```

Çıktı:

```text

### s1 — `list/exceptions` (ListExceptions) — ExceptionsRead gate removed
red:      Başarısız! - Başarısız:     2, Başarılı:    74, Atlanan:     0, Toplam:    76
   ExpiryDataGateTests.The_table_keys_are_the_keys_of_the_doors_own_403_statement(door: "ListExceptions")
   ExpiryDataGateTests.Without_its_key_a_door_answers_403_and_nothing_reaches_the_gateway(door: "ListExceptions")
restored: Başarılı!  - Başarısız:     0, Başarılı:    76, Atlanan:     0, Toplam:    76

### s2 — `data/policy` (PolicyData) — PoliciesRead gate removed
red:      Başarısız! - Başarısız:     2, Başarılı:    74, Atlanan:     0, Toplam:    76
   ExpiryDataGateTests.The_table_keys_are_the_keys_of_the_doors_own_403_statement(door: "PolicyData")
   ExpiryDataGateTests.Without_its_key_a_door_answers_403_and_nothing_reaches_the_gateway(door: "PolicyData")
restored: Başarılı!  - Başarısız:     0, Başarılı:    76, Atlanan:     0, Toplam:    76

### s3 — `data/evidence/preview` (EvidencePreview) — EvidenceExport gate removed
red:      Başarısız! - Başarısız:     2, Başarılı:    74, Atlanan:     0, Toplam:    76
   ExpiryDataGateTests.The_table_keys_are_the_keys_of_the_doors_own_403_statement(door: "EvidencePreview")
   ExpiryDataGateTests.Without_its_key_a_door_answers_403_and_nothing_reaches_the_gateway(door: "EvidencePreview")
restored: Başarılı!  - Başarısız:     0, Başarılı:    76, Atlanan:     0, Toplam:    76

### s4 — `data/dashboard` (DashboardData, F4) — DashboardRead gate removed
red:      Başarısız! - Başarısız:     2, Başarılı:    74, Atlanan:     0, Toplam:    76
   ExpiryDataGateTests.The_table_keys_are_the_keys_of_the_doors_own_403_statement(door: "DashboardData")
   ExpiryDataGateTests.Without_its_key_a_door_answers_403_and_nothing_reaches_the_gateway(door: "DashboardData")
restored: Başarılı!  - Başarısız:     0, Başarılı:    76, Atlanan:     0, Toplam:    76

### s2b — `data/dashboard` inner policy read — `if (_permissions.Has(ExpiryKeys.PoliciesRead))` → `if (true)`
red:      Başarısız! - Başarısız:     2, Başarılı:    74, Atlanan:     0, Toplam:    76
   ExpiryDataGateTests.The_table_keys_are_the_keys_of_the_doors_own_403_statement(door: "DashboardData")
   ExpiryDataGateTests.An_inner_call_happens_only_with_its_key(door: "DashboardData", key: "expiry.policies.read", call: "/api/expiry/policy")
restored: Başarılı!  - Başarısız:     0, Başarılı:    76, Atlanan:     0, Toplam:    76

### s5 — a temporary new door `data/sabotage` (not in the table)
red:      Başarısız! - Başarısız:     1, Başarılı:    75, Atlanan:     0, Toplam:    76
   ExpiryDataGateTests.Every_routed_action_of_ExpiryController_is_in_exactly_one_class_and_every_door_is_in_the_gate_table
restored: Başarılı!  - Başarısız:     0, Başarılı:    76, Atlanan:     0, Toplam:    76

SHA-256 of ExpiryController.cs unchanged: true
git diff identical before/after the loop: true
git diff -- frontend/Diten.Web empty: true
```
