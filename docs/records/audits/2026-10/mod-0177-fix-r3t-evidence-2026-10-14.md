# MOD-0177 FIX-R3T — kanıt (WP-0177-FIX-R3T + CT kararı A) — 2026-10-14

> Ajan sonucu, **CT bekliyor**. Worktree `C:\Users\Ali\erp-vnext-wt-r3t`, dal `fix/mod-0177-r3-tenant`, taban `9362c0402` (R3 CT-ACCEPTED;
> dalda olduğu doğrulandı). Push yok, PR yok; taban bu arada `1f904aa83`'e ilerledi — CT kararı: güncelleme gerekmez, CT birleştirirken alır.
> Belge yeri `docs-organization.md` beş soruyla: "belirli bir tarihte olanı kaydediyor, değişmeyecek" → `records/audits/2026-10/`.

## 1. Commit'ler

| commit | içerik |
|---|---|
| `98b940b7f` | `Recall/RecallTenantBoundaryTests.cs` — (a) (b) (c), her birinde (d) kontrolü ve (e) kayıt yokluğu |
| `7c2885d94` | `RecallQuarantineSweeper.cs` istek başına yakalama + log; `Recall/RecallSweeperTenantTests.cs` — (f) (g) (g2); `RecallTestInfrastructure.cs` +4 satır (log yakalayıcı) |
| (bu commit) | bu kanıt + MOD-0177 pack §19.14 |

`RecallPostingGate.cs` üretim kodu değişmedi (yalnız sabotajda geçici; SHA `2ff6d132a14a…` başta ve sonda aynı).

## 2. CT bulgusu ve ne ölçüldü

CT: `RecallPostingGate.ByLotAsync` içindeki `f.Eq(x => x.TenantId, _tenant.TenantId) &` silindiğinde Inventory paketinin tamamı 1153/1153 yeşil
kaldı. Kod doğru, sınır testsizdi. Yeni testler GERÇEK gate'i, GERÇEK kayıt ucunu (`POST /api/inventory/movements`), `getFefoOrder`'ı ve
paketin kendi Mongo'sunu kullanır. T2'nin bloğu (aynı lot id / aynı SKU + lot numarası) test kurulumunda doğrudan `recall_lot_blocks`'a yazılır.

## 3. Yazılan testler

| test | dosya:satır | ölçtüğü |
|---|---|---|
| `a_ByLotAsync_another_tenants_block_on_the_same_lot_id_refuses_neither_the_pre_check_nor_the_re_check` | `Recall/RecallTenantBoundaryTests.cs:67` | T2 bloğu baştan var → ön kontrol + yeniden kontrol 201; yarış dikişi (R3 `AfterPassedPreCheck`) bloğu iki kontrol ARASINA koyar → yalnız işlem içi yeniden kontrol okur → 201; (e) T2 ve T1'de `ExitRefused` / `recall.exit.refused` = 0; (d) aynı blok T1'de → ön kontrol 422 ve yeniden kontrol 422 (denetimde `stage` pre-check / re-check), T2'de hâlâ satır yok |
| `b_ByTypedLotAsync_another_tenants_block_on_the_same_sku_and_lot_number_does_not_refuse_a_typed_lot` | `:103` | tipli lot metni (kayıtlı lot değil) + aynı SKU: T2 bloğu → ön kontrol 201, yarışta yeniden kontrol 201; (e) satır yok; (d) T1'de aynı SKU + numara → iki aşamada 422 |
| `c_UnderRecallAsync_another_tenants_block_does_not_exclude_the_lot_from_the_FEFO_order` | `:131` | T2 bloğu → lot `eligible`'da, `excluded`'da değil; (d) T1 bloğu → `excluded`, `RECALLED` |
| `f_one_sweep_sends_each_tenants_request_in_its_own_tenant_and_company_scope` | `Recall/RecallSweeperTenantTests.cs:45` | T1 ve T2'de birer Pending istek (onayda 0175 çağrısı "Before" çökmesiyle kaybedildi); TEK süpürme iki kiracıyı görür: ikisi de `Held`; her isteğin 0175 karantina kaydı (`quarantine_holds`) kendi isteğinin `TenantId` / `LegalEntityId`'sinde |
| `g_the_dispatchers_own_catch_keeps_a_request_whose_0175_call_throws_Pending_and_the_others_are_sent` | `:70` | üç Pending istek, birinin 0175 çağrısı yine çöker (mevcut dekoratör): o Pending kalır, ikisi `Held`. Kanıtladığı: **dispatcher'ın kendi yakalaması** (`DispatchAsync:109-117`) — süpürücünün yeni yakalaması değil (s5'te beklenen yeşil) |
| `g2_a_row_that_throws_in_the_sweep_loop_stays_Pending_is_logged_and_the_others_of_the_same_sweep_are_sent` | `:95` | CT kararı A: `LegalEntityId = null` Pending satır doğrudan yazılır (süpürücü döngüsünde `SetScope` fırlatır) + iki sağlam istek: ikisi `Held`, bozuk satır Pending ve `Attempts` değişmemiş; `Error` seviyesinde `recall.quarantine.sweep_request_failed` satırı istek id'si ve `tenant {T}` ile yazılmış |

**Ölçümle düzeltilen varsayım (BLOCKED → CT kararı A):** WP "istek hata fırlatırsa turun geri kalanı gönderilmiyor" diyordu. 0175 çağrısındaki
hata için bu doğru değildi: `RecallQuarantineDispatcher.DispatchAsync` onu zaten yakalayıp isteği Pending bırakıyor. Dekoratörlü (g), yeni
try/catch kaldırılınca da yeşil kaldı (ölçüldü). Döngüyü gerçekten kıran hata dispatcher'ın dışındadır (kapsam kurulumu, kiracısı / şirketi
boş satır, yeniden okuma); (g2) bunu ölçer.

## 4. Süpürücü değişikliği (`RecallQuarantineSweeper.cs`)

- Döngüde her istek kendi `try`'ında; `catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))` →
  `_logger.LogError(ex, "recall.quarantine.sweep_request_failed {RequestId} tenant {TenantId} company {LegalEntityId}", …)`. Yakalamada hiçbir
  şey yazılmaz: istek Pending kalır, sonraki tur yeniden dener (`Attempts` değişmez — g2 ölçer). İptal (servis durdurma) yutulmaz.
- Kiracı listesi alan aşırı yükleme `SweepAsync(now, ct, IReadOnlyCollection<Guid>? onlyTenants)`; mevcut `SweepAsync(now, ct, Guid? onlyTenant = null)`
  imzası aynı ve ona yönlenir. Barındırılan servis değişmedi (filtre yok = bütün kiracılar). Sebep: testler tek veritabanını paylaşır; bütün
  kiracıları süpüren bir test başka test sınıflarının Pending isteklerine dokunabilirdi.
- `ILogger<RecallQuarantineSweeper>` yapıcıya eklendi (yalnız DI kuruyor — `AddSingleton<RecallQuarantineSweeper>()`; elle kuran test yok).

## 5. Sabotaj — her biri üretim kodunda, geri alındı, SHA başlangıçla aynı

| | sabotaj | dosya | sabote (kırmızı test) | geri alındı | SHA |
|---|---|---|---|---|---|
| s1 | `ByLotAsync` TenantId filtresi kaldırıldı | `RecallPostingGate.cs` | 1/3 kırmızı: `a_ByLotAsync_…` | 3/3 yeşil | `2ff6d132a14a` = başta |
| s2 | `ByTypedLotAsync` TenantId filtresi kaldırıldı | `RecallPostingGate.cs` | 1/3: `b_ByTypedLotAsync_…` | 3/3 | `2ff6d132a14a` |
| s3 | `UnderRecallAsync` TenantId filtresi kaldırıldı | `RecallPostingGate.cs` | 1/3: `c_UnderRecallAsync_…` | 3/3 | `2ff6d132a14a` |
| s4 | `SetScope` ilk isteğin (`due[0]`) kiracı / şirketiyle sabitlendi | `RecallQuarantineSweeper.cs` | 2/3: `f_…` (ve `g2_…`: `due[0]` bozuk satır olunca her istek fırlatır) | 3/3 | `de63b4afbe26` = başta |
| s5 | yeni istek başına try/catch kaldırıldı | `RecallQuarantineSweeper.cs` | 1/3: `g2_…`; `g_…` **beklenen yeşil (kapsam dışı — dispatcher'ın yakalaması)** | 3/3 | `de63b4afbe26` |

Koşucu sonunda `final: 4 changed paths` (o an commit edilmemiş 4 yol; sabotajlar geri alınmıştı). Süpürücünün WP öncesi SHA'sı
`ed55ff22dfa5…`; `de63b4afbe26…` düzeltilmiş halidir (sabotajların başlangıcı).

## 6. Sayılar — önce / sonra

| | önce (`9362c0402`, R3 kanıtı / CT ölçümü) | sonra |
|---|---|---|
| Inventory (`unset INVENTORY_TEST_MONGO`, kendi replica set) | 1153 + 4 atlanan | **1159 + 4 atlanan** — tam koşuda 1143 + 16 Prism başlangıç zaman aşımı; `PrismConsumedContractSmokeTests` tek başına **16 / 16**. +6 = a, b, c, f, g, g2 |
| Mimari | 69 / 77, 8 kırmızı | **69 / 77**, aynı 8 (DocsPathGuard × 5, MongoTestDatabaseGuard × 1, AuditTrailStandard × 2); denetim listesi "InventoryService: 58 yazma komutu" değişmedi |
| Derleme (`dotnet build services/Diten.InventoryService --no-incremental`) | 0 / 0 | **0 hata / 0 uyarı** |

## 7. Ortam

Testler yalnız kendi replica set'inde (27017'ye bağlanmadı). Filo pencerelerine (5000–5070, 4010) dokunulmadı, yeniden başlatılmadı; token
imzalanmadı; önizleme başlatılmadı. Sahibin Inventory penceresi süpürücüyü barındırıyor: değişiklik canlıya, CT birleştirip sahip yeniden
başlattığında geçer.

## 8. Açık kalanlar / FU önerileri

| | |
|---|---|
| O-R3T-1 | Süpürücü, kiracısı / şirketi boş satırı her turda yeniden dener ve her turda Error loglar (WP: "Pending kalsın"). Kalıcı bozuk satır için bir üst sınır ya da `Failed` + sebep FU olabilir → öneri **FU-0177-SWEEP-01**. |
| O-R3T-2 | `DispatchAsync`'in kendi yakalaması `LogWarning` ile yalnız istek id'si yazıyor (kiracı yok; OBS-001 "her log satırında TenantId"); bu WP'nin dosyası değil → öneri **FU-0177-SWEEP-02**. |
| O-R3T-3 | Kiracı listesi aşırı yüklemesini yalnız testler kullanıyor; barındırılan servis kullanmıyor. |

## Ek — sabotaj koşucusu (scratchpad'de koşuldu)

```bash
#!/usr/bin/env bash
# WP-0177-FIX-R3T sabotage runner: one production edit → build → the guarding test (expect RED) → restore → SHA equal to the start → test again (GREEN).
set -u
REPO=/c/Users/Ali/erp-vnext-wt-r3t
OUT=/c/Users/Ali/AppData/Local/Temp/claude/C--Users-Ali-erp-vnext/b3a5c70a-e1ab-46e4-a73a-669e2be00e49/scratchpad/r3t/sabotage
mkdir -p "$OUT"; cd "$REPO"; unset INVENTORY_TEST_MONGO INVENTORY_PERF_TESTS
T=services/Diten.InventoryService/tests/Diten.InventoryService.Tests/Diten.InventoryService.Tests.csproj
GATE=services/Diten.InventoryService/src/Diten.InventoryService.Persistence/Recall/RecallPostingGate.cs
SWEEP=services/Diten.InventoryService/src/Diten.InventoryService.Persistence/Recall/RecallQuarantineSweeper.cs
SUMMARY="$OUT/summary.txt"; : > "$SUMMARY"
run() {
  dotnet build "$T" -c Debug > "$OUT/$1-build.log" 2>&1 || { echo "BUILD FAILED"; return; }
  dotnet test "$T" -c Debug --no-build --filter "$2" > "$OUT/$1.log" 2>&1
  local total; total=$(grep -E "Başarılı!|Başarısız!" "$OUT/$1.log" | tail -1 | sed -E 's/ +- +Diten.*//; s/, Atlanan.*//')
  local reds; reds=$(grep -E "^\s+Başarısız " "$OUT/$1.log" | sed -E 's/.*\.([A-Za-z0-9_]+) \[.*/\1/' | tr '\n' ' ')
  echo "$total [red: ${reds:-none}]"
}
sabotage() { # id desc filter file perl
  local id=$1 desc=$2 filter=$3 file=$4 expr=$5
  local sha0; sha0=$(sha256sum "$file" | cut -c1-64); cp "$file" "$OUT/$id.orig"
  perl -0pi -e "$expr" "$file"
  local sha1; sha1=$(sha256sum "$file" | cut -c1-64)
  if [ "$sha0" = "$sha1" ]; then printf '%s | %s | NOT APPLIED\n' "$id" "$desc" | tee -a "$SUMMARY"; return; fi
  local red; red=$(run "$id-red" "$filter")
  cp "$OUT/$id.orig" "$file"
  local sha2; sha2=$(sha256sum "$file" | cut -c1-64)
  local same; [ "$sha0" = "$sha2" ] && same="SHA equal ${sha0:0:12}" || same="SHA DIFFERS"
  local green; green=$(run "$id-green" "$filter")
  printf '%s | %s | sabotaged: %s | restored: %s | %s\n' "$id" "$desc" "$red" "$green" "$same" | tee -a "$SUMMARY"
}
sabotage s1 "ByLotAsync without its TenantId filter" "FullyQualifiedName~RecallTenantBoundaryTests" $GATE \
  's/(private async Task<IReadOnlyList<RecallLotBlock>> ByLotAsync.*?var filter = )f\.Eq\(x => x\.TenantId, _tenant\.TenantId\) & /$1/s'
sabotage s2 "ByTypedLotAsync without its TenantId filter" "FullyQualifiedName~RecallTenantBoundaryTests" $GATE \
  's/(private async Task<IReadOnlyList<RecallLotBlock>> ByTypedLotAsync.*?var filter = )f\.Eq\(x => x\.TenantId, _tenant\.TenantId\) & /$1/s'
sabotage s3 "UnderRecallAsync without its TenantId filter" "FullyQualifiedName~RecallTenantBoundaryTests" $GATE \
  's/(var rows = await _blocks\.Find\()f\.Eq\(x => x\.TenantId, _tenant\.TenantId\) & (f\.In\(x => x\.LotId, lotIds\))/$1$2/'
sabotage s4 "sweeper SetScope fixed to the FIRST request's tenant / company" "FullyQualifiedName~RecallSweeperTenantTests" $SWEEP \
  's/SetScope\(request\.TenantId!\.Value, request\.LegalEntityId!\.Value, request\.RequestedBy\)/SetScope(due[0].TenantId!.Value, due[0].LegalEntityId!.Value, request.RequestedBy)/'
sabotage s5 "sweeper without the per-request try / catch" "FullyQualifiedName~RecallSweeperTenantTests" $SWEEP \
  's/            try\r?\n            \{\r?\n(.*?)            \}\r?\n            catch \(Exception ex\) when.*?\r?\n            \{\r?\n.*?\r?\n.*?\r?\n.*?\r?\n            \}\r?\n/$1/s'
echo "final: $(git status --short | wc -l) changed paths"
```
