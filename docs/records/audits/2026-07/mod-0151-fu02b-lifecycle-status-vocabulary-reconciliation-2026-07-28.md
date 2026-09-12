# MOD-0151 FU02B — Lifecycle Status Vocabulary Reconciliation

> **Tarih:** 2026-07-28
> **Hedef tenant:** `97c59330-dbc4-4665-b29c-0c26dbb5cc93`
> **Değişiklik türü:** Governance (pack) + reference authoring + test hardening · **1 satırlık readiness descriptor kod düzeltmesi**
> **Karar:** **Seçenek A — Reference sözlüğünü lifecycle'a genişlet**
> **Verdict:** **PASS**

---

## 1. Preflight

### Files reviewed

| Kaynak | Ne için |
|---|---|
| [`mod-0151-fu02b-authenticated-gateway-live-smoke-closeout-retry-2026-07-25.md`](./mod-0151-fu02b-authenticated-gateway-live-smoke-closeout-retry-2026-07-25.md) | Canlı hata kanıtı (33/40 PASS, deactivate/archive 400) |
| [`mod-0151-fu02b-territory-lifecycle-activation-expiry-soft-delete-implementation-2026-07-25.md`](./mod-0151-fu02b-territory-lifecycle-activation-expiry-soft-delete-implementation-2026-07-25.md) | FU02B implementation kapsamı, 63/63 + 232/232 test iddiası |
| [`mod-0151-fu02b-pack-lifecycle-scope-update-2026-07-25.md`](./mod-0151-fu02b-pack-lifecycle-scope-update-2026-07-25.md) | FU02B'nin pack'e eklenme kararı, FU06 sınırı |
| MOD-0151 pack **§13.1 · §16 · §20 · §22.1** | Lifecycle state machine, reference proposal, FU06 boundary |
| `mod-0151-territory-reference-values.json` | Tenant'a publish edilen gerçek değer kaynağı (73 value) |
| `mod-0151-territory-required-reference-authoring-template.json` / `.md` | F1 authoring template |
| `mod-0151-territory-reference-publish-operator-runbook.md` · `mod-0151-territory-reference-operator-checklist.md` | Publish operator talimatı ve sayılar |
| `TerritoryLifecycleHandlers.cs` · `SoftDeleteDraftTerritoryNodeHandler.cs` · `TerritoryModelsController.cs` · `TerritoryReferenceSets.cs` · `TerritoryReferenceValidator.cs` | Lifecycle kod yolu ve readiness hesabı |
| `TerritoryLifecycleTests.cs` · `FakeTerritoryInfrastructure.cs` · `FakeReferenceSeams.cs` · `TerritoryContractTests.cs` | Test seam'i ve neden yakalamadığı |

### Scope confirmation

Yapılan: karar analizi · pack §16 hizalaması · reference authoring/publish-data güncellemesi · runbook/checklist sayı
güncellemesi · test seam hardening'i · readiness descriptor sayı düzeltmesi · bu rapor.

Yapılmayan: FU03 ve sonrası, assignment/resource/account/evidence/import-export, workflow approval, MOD-0023,
submit/approve/reject, Brand Scope, Product/Brand master, Account/Contact, hard delete, background scheduler,
RBAC seed/grant, Gateway route.

### No-smoke / no-publish confirmation

Bu task'ta **hiçbir reference value publish edilmedi**, **hiçbir canlı smoke çalıştırılmadı**, **Mongo'ya
dokunulmadı**, **tenant verisi elle düzeltilmedi**. Gateway veya `:5061` üzerinden **hiçbir business API çağrısı
yapılmadı**. Publish, ayrı bir operator prompt'u olarak §8'de önerilmiştir.

`git` durumu değişmedi: porcelain **421 → 421**, HEAD `094e3a86` sabit. (Not: `services/Diten.CrmService/` ve
`execution/domains/commercial-suite/` dizinleri repo'da henüz **untracked** olduğu için `git diff` bu dosyalar için
çıktı üretmez; değişiklikler dosya sisteminde gerçektir.)

---

## 2. Failure Summary

| Action | Expected | Actual | Root Cause |
|---|---|---|---|
| `POST /territory-models/{id}/activate` | 200, model+node `active` | **200 — çalışıyor** | `active` her iki status set'inde de publish |
| `POST /territory-models/{id}/deactivate` | 200, model+node `inactive` | **400** `Lifecycle reference values are not published.` | `territory-model-status` içinde **`inactive` yok** |
| `POST /territory-models/{id}/archive` | 200, model+node `archived` | **400** aynı mesaj | `territory-node-status` içinde **`archived` yok** |
| `archive` (computed-expired model) | 200 | **400** aynı mesaj | Aynı — node-status `archived` eksik |
| `archive` (M1, deactivate başarısız olduğu için hâlâ active) | 200 | **409** | Ardıl etki: ön koşul `inactive` sağlanamadı |

Canlı sonuç: **40 kontrolden 33 PASS / 7 FAIL**; 7 FAIL'in tamamı yukarıdaki tek kök nedene iniyor.

---

## 3. Root Cause

### 3.1 Model status mismatch

`TerritoryLifecycleHandlers.cs` → `DeactivateTerritoryModelHandler`:

```csharp
if (!await PublishedAsync(TerritoryReferenceSets.TerritoryModelStatus, TerritoryLifecycle.Inactive, ct)
    || !await PublishedAsync(TerritoryReferenceSets.TerritoryNodeStatus, TerritoryLifecycle.Inactive, ct))
    return Response<bool>.Fail("Lifecycle reference values are not published.", 400);
```

| Set | Publish edilmiş (73/73 doğrulandı) | Gereken | Durum |
|---|---|---|---|
| `territory-model-status` (6) | draft · review · approved · active · superseded · archived | `inactive` | ❌ **YOK** |
| `territory-node-status` (4) | draft · active · inactive · ended | `inactive` | ✅ var |

### 3.2 Node status mismatch

`ArchiveTerritoryModelHandler` aynı deseni `archived` için uygular:

| Set | Publish edilmiş | Gereken | Durum |
|---|---|---|---|
| `territory-model-status` | … archived | `archived` | ✅ var |
| `territory-node-status` (4) | draft · active · inactive · ended | `archived` | ❌ **YOK** |

### 3.3 Governance kök nedeni — §13.1 / §22.1 drift

Authoring template'in kendi açıklaması bunu ele veriyor:

> `"description": "MOD-0151 TerritoryModel lifecycle status. Must match the pack lifecycle exactly (pack section 13.1)."`

Sözlük **yalnız §13.1 (FU06 approval lifecycle: draft → review → approved → active → superseded → archived)** baz
alınarak yazıldı ve **2026-07-23**'te publish edildi. **§22.1 (FU02B manual lifecycle: draft → active → inactive →
archived)** pack'e **2026-07-25**'te eklendi; sözlük güncellenmedi. Yani bu bir "operator publish'i unuttu" durumu
değil, **iki lifecycle'ın sözlük düzeyinde hiç uzlaştırılmamış olmasıdır**.

### 3.4 Why tests missed it

`FakeTerritoryReferenceValidator.ValidateValueAsync` (test seam'i) **her değeri `Valid` döndürüyordu**:

```csharp
if (MissingSets.Contains(setCode)) return SetMissing;
return ReferenceValidationStatus.Valid;   // ← value hiç kontrol edilmiyordu
```

Set'i tamamen "missing" işaretlemek dışında hiçbir test, **bir set'in publish olup istenen value'yu içermemesi**
senaryosunu modelleyemiyordu. Bu yüzden `Deactivate_Active_Updates_Nodes` ve `Archive_Active_Fails_But_Inactive_Succeeds`
testleri yeşilken canlı ortamda aynı aksiyonlar 400 dönüyordu. **63/63 + 232/232 PASS iddiası doğruydu ama bu
sınıftaki hatayı yakalayamıyordu.**

---

## 4. Option Analysis

| Option | Change | Pros | Cons | Recommendation |
|---|---|---|---|---|
| **A — Sözlüğü lifecycle'a genişlet** | `territory-model-status` + `inactive`; `territory-node-status` + `archived`; pack §16, authoring template, runbook, checklist, test seam güncellenir | Pack §22.1'de **zaten onaylanmış** lifecycle'ı birebir karşılar · runtime lifecycle kodu, UI, RESX değişmez · mevcut value'lar silinmez/yeniden adlandırılmaz (yalnız 2 ekleme) · FU06 sözlüğü bozulmaz · semantik olarak dürüst | MOD-0048 re-publish gerekir (2 set) · required value 62 → 64 | ✅ **SEÇİLDİ** |
| **B — Kodu mevcut sözlüğe hizala** | `inactive` → `superseded`; `archived` (node) → `ended`; kod + UI + RESX + test değişir | Re-publish gerekmez | **Semantik olarak yanlış:** `superseded` `isTerminal=true`, oysa §22.1 `inactive → active` geri dönüşünü zorunlu kılar → ya geri dönüş kırılır ya da terminal semantiği bozulur · `ended` FU06/atama katmanının tarihsel sonlandırma işaretidir; node arşivine bağlanırsa FU05/FU06'da `ended` kullanılamaz hâle gelir · archived/read-only ayrımı kaybolur · runtime + UI + RESX + test yüzeyi çok daha geniş | ❌ Reddedildi |
| **C — Lifecycle + sözlüğü birlikte yeniden normalize et** | §22.1 state machine'i yeniden tasarla, sözlüğü ona göre kur | En geniş governance temizliği | §22.1 **zaten onaylanmış** ve implement edilmiş; yeniden açmak FU02B'yi baştan yazmak demek · canlı defect'i çözmek için gereksiz · FU06 sınırını da yeniden müzakereye açar | ❌ Gereksiz (A zaten C'nin dar ve doğru hâli) |

---

## 5. Selected Decision

### **Seçenek A**

**Gerekçe:**

1. **Pack §22.1 zaten otoritedir.** Lifecycle `active → inactive` ve `inactive/computed-expired → archived` olarak
   **onaylanmış ve implement edilmiştir**. Uyumsuz olan taraf sözlüktür, kod değil.
2. **§16, §22.1'den önce yazılmıştır.** Authoring template'in kendi açıklaması "pack section 13.1" der; §22.1 iki gün
   sonra eklenmiştir. Bu net bir dokümantasyon drift'idir.
3. **`inactive` ≠ `superseded`.** `superseded` `isTerminal=true` FU06 ikame durumudur. §22.1 `inactive → active`
   geri dönüşünü şart koşar; terminal bir değere bağlamak lifecycle'ı kırar.
4. **`archived` ≠ `ended`.** `ended` node/assignment tarihsel sonlandırmasıdır (FU05/FU06); `archived` model
   lifecycle'ının node'lara yansıttığı read-only arşivdir. Birleştirmek ileri FU'ları sakatlar.
5. **En küçük ve en güvenli değişiklik.** Mevcut hiçbir value silinmez, yeniden kodlanmaz, sortOrder'ı değişmez;
   yalnız iki yeni value eklenir. Runtime lifecycle kodu, UI ve RESX'e dokunulmaz.

FU02B ve FU06 sözlükleri artık **açıkça sahiplendirilmiştir**: FU02B yalnız `draft/active/inactive/archived` yazar;
`review/approved/superseded` FU06'ya aittir ve FU02B bunları asla yazmaz.

---

## 6. Pack / Reference Changes

| File | Change | Notes |
|---|---|---|
| `execution/domains/commercial-suite/module-packs/MOD-0151-territory-management.md` | §16 tablosunda `territory-model-status` → `inactive`, `territory-node-status` → `archived` eklendi; **yeni §16.1 "Lifecycle status sözlüğü — FU02B / FU06 sahiplik ayrımı"** bölümü eklendi | §16.1, her value'nun sahip FU'sunu, terminal olup olmadığını ve geri dönülebilirliğini tabloya bağlar; `inactive ≠ superseded` ve `archived ≠ ended` gerekçeleri yazılıdır. §22.1 **değiştirilmedi** (zaten doğruydu) |
| `execution/domains/commercial-suite/reference-data/mod-0151-territory-reference-values.json` | `territory-model-status` + `inactive` (sortOrder **45**, lifecycleOrder 45, isTerminal false, isEditable false, requiresApproval false) · `territory-node-status` + `archived` (sortOrder **50**, isHistorical true, isActiveLike false, isEditable false) | Publish DATA dosyası. sortOrder 45/50 seçildi ki **mevcut değerlerin sortOrder'ı hiç değişmesin**. Tüm attribute'lar string (MOD-0048 sözleşmesi). JSON geçerliliği doğrulandı. Toplam value **73 → 75** |
| `…/mod-0151-territory-required-reference-authoring-template.json` | Aynı iki value eklendi (metadata native tip — bu dosyanın konvansiyonu) + iki set'in `description` alanı §22.1/§13.1 ayrımını açıklayacak şekilde güncellendi | Required value **62 → 64** (doğrulandı) |
| `…/mod-0151-territory-required-reference-authoring-template.md` | §5 tablosu, §6.2 (6→**7 value**, `inactive` satırı + sahip FU sütunu), §6.3 (4→**5 value**, `archived`), toplam sayılar | §6.2/§6.3'e "birbirinin yerine kullanılamaz" uyarıları eklendi |
| `…/mod-0151-territory-reference-publish-operator-runbook.md` | **Yeni §0 "RE-PUBLISH GEREKSİNİMİ"** bölümü; set sayıları 6→7 / 4→5; toplam 62→**64** (tüm geçtiği yerlerde) | §0, hangi 2 set'in re-publish edileceğini, hangi 10 set'e dokunulmayacağını, SoD kuralını ve beklenen 64/75 sayılarını verir |
| `…/mod-0151-territory-reference-operator-checklist.md` | Set sayıları, required toplam 62→**64**, §5.7 doğrulama satırı, "yasak işlemler" notu | Yasak listesi artık "value **silmek**" ile "reconciliation'da eklenen 2 value" arasını ayırt eder |
| `services/…/Features/Territory/TerritoryReferenceSets.cs` | `TerritoryModelStatus` descriptor `ExpectedValueCount` 6 → **7**; `TerritoryNodeStatus` 4 → **5** (+ gerekçe yorumu) | **Tek runtime dosyası değişikliği.** Zorunlu çünkü bu sayılar contract endpoint'inin `expectedValueCount` alanını besler; re-publish sonrası actual 7/5 olurken expected 6/4 kalsaydı contract yanıltıcı bir readiness descriptor'ı yayınlardı. **Lifecycle mantığı, guard'lar, endpoint'ler, UI ve RESX değişmedi** |

---

## 7. Test Changes

| Test Area | Change | Notes |
|---|---|---|
| `FakeTerritoryInfrastructure.cs` → `FakeTerritoryReferenceValidator` | **Vocabulary-aware yapıldı.** Yeni `Vocabulary` sözlüğü iki lifecycle-gating set'in publish edilmiş value listesini (authoring template'in aynısı) tutar; `ValidateValueAsync` artık publish edilmemiş value için `InvalidValue` döner. Yeni `Unpublish(setCode, value)` yardımcısı "kısmi/bayat publish" senaryosunu modeller | Bu **kök test boşluğunun kapatılmasıdır**. Lifecycle-gating olmayan set'ler eski permissive davranışı korur |
| `TerritoryLifecycleTests.Full_Lifecycle_Chain_Runs_On_Canonical_Published_Vocabulary` | **YENİ** — canonical sözlükle `draft → active → inactive → archived` zincirinin tamamını tek testte koşar; model ve node status'larını her adımda doğrular | Canlıda kırılan tam zincir |
| `TerritoryLifecycleTests.Deactivate_Fails_Closed_When_ModelStatus_Does_Not_Publish_Inactive` | **YENİ** — `inactive` unpublish edilince 400 + model/node status'un **değişmediği** | Canlı hatanın birebir regresyon testi |
| `TerritoryLifecycleTests.Archive_Fails_Closed_When_NodeStatus_Does_Not_Publish_Archived` | **YENİ** — `archived` unpublish edilince 400 + status değişmez | Canlı hatanın birebir regresyon testi |
| `TerritoryLifecycleTests.Every_Status_The_Lifecycle_Writes_Is_In_The_Published_Vocabulary` | **YENİ** (Theory ×4: draft/active/inactive/archived) — FU02B'nin yazdığı her status'un **her iki** set'in sözlüğünde bulunduğunu doğrular | Sözlük ile kod arasındaki sözleşmeyi kalıcı olarak pinler |
| `TerritoryLifecycleTests.Readiness_Descriptors_Match_The_Authoring_Template_Vocabulary_Size` | **YENİ** — `TerritoryReferenceSets.Required` içindeki `ExpectedValueCount` (7/5) ile sözlük boyutunun eşleştiğini doğrular | Descriptor sayısının sessizce kaymasını engeller |
| Mevcut `Deactivate_Active_Updates_Nodes` · `Archive_Active_Fails_But_Inactive_Succeeds` | Kod değişmedi ama **artık gerçek kapsam sağlıyorlar** | Eskiden permissive seam yüzünden boştular |

### Test sonuçları

| Suite | Sonuç |
|---|---|
| Territory testleri | **71/71 PASS** (63 → 71; 8 yeni) |
| Tüm CrmService Application suite | **240/240 PASS** (232 → 240) |

> İlk koşuda ilgisiz `ContactLocationPiiHardeningTests.PiiMasking_…` testi 1 kez kırmızı verdi; izole koşuda ve
> temiz tekrar koşuda **yeşil**. Bu, FU01 raporunda da belgelenmiş **mevcut flake**'tir; territory ile ilgisizdir ve
> bu task'ta değiştirilen hiçbir dosyaya dokunmaz.

### Mutasyon doğrulaması (testlerin gerçekten yakaladığının kanıtı)

Test seam'inin sözlüğü **geçici olarak düzeltme öncesi hâline** çevrildi (`inactive` ve `archived` çıkarıldı) ve
territory suite yeniden koşuldu:

```
Başarısız: 6, Başarılı: 65, Toplam: 71
  ✗ Full_Lifecycle_Chain_Runs_On_Canonical_Published_Vocabulary
  ✗ Every_Status_The_Lifecycle_Writes_Is_In_The_Published_Vocabulary(status: "inactive")
  ✗ Every_Status_The_Lifecycle_Writes_Is_In_The_Published_Vocabulary(status: "archived")
  ✗ Readiness_Descriptors_Match_The_Authoring_Template_Vocabulary_Size
  ✗ Deactivate_Active_Updates_Nodes            ← eskiden yeşildi
  ✗ Archive_Active_Fails_But_Inactive_Succeeds ← eskiden yeşildi
```

Dosya hemen geri alındı ve suite yeniden **71/71 PASS**. Bu, canlıdaki vocabulary mismatch'inin artık test
seviyesinde yakalandığının doğrudan kanıtıdır.

---

## 8. Publish Requirement

> ⚠️ **Bu task publish YAPMADI.** Aşağıdaki adım ayrı bir MOD-0048 operator prompt'u olarak yürütülmelidir.

| Alan | Değer |
|---|---|
| **Tenant** | `97c59330-dbc4-4665-b29c-0c26dbb5cc93` |
| **Scope type** | `tenant` |
| **Re-publish edilecek set** | `territory-model-status` · `territory-node-status` (**yalnız bu 2 set**) |
| **Dokunulmayacak** | Diğer 8 required set + 2 optional set |
| **Eklenen değerler** | `territory-model-status` → `inactive` (sortOrder 45) · `territory-node-status` → `archived` (sortOrder 50) |
| **Silinecek/yeniden adlandırılacak** | **Hiçbiri** |
| **Beklenen sonrası: required** | **64/64** (önce 62) |
| **Beklenen sonrası: toplam** | **75/75** (64 required + 11 optional) |
| **Beklenen set value sayıları** | `territory-model-status` **7** · `territory-node-status` **5** |
| **Akış** | Maker-checker; SoD (`sod_submitter_cannot_approve`) geçerli; `publishoverride` ile SoD bypass **yasak**; publish `Idempotency-Key` ister; attribute'lar **string** olarak gönderilir |
| **Veri kaynağı** | `mod-0151-territory-reference-values.json` (güncellendi) · sürücü: `publish-mod-0151-territory-reference.ps1` |
| **Publish sonrası doğrulama** | `smoke-mod-0151-territory-publishedvalues.ps1` → 75/75 · contract `expectedValueCount == actualValueCount` (7/7 ve 5/5) |
| **Operator talimatı** | Runbook **§0** (yeni bölüm) |

---

## 9. Locked Smoke Model Cleanup Plan

| Alan | Değer |
|---|---|
| **Model code** | `SMOKE-MOD0151-LIFE-20260728074528` |
| **Model id** | `33fd7e03-37c1-4777-be17-eae0834bce3c` |
| **Current state** | **ACTIVE**, 3 adet `active` node ile (country / zone / microzone) |
| **Neden kilitli** | `deactivate` ve `archive` defect nedeniyle 400 dönüyor; `delete-draft` yalnız draft'ta çalışır → API ile hiçbir çıkış yolu yok |
| **Operasyonel etki** | Overlap guard doğru çalıştığı için, `countryScope=tr` + 2027-01-01…2027-12-31 penceresiyle çakışan **başka hiçbir modelin aktifleştirilmesine izin vermez** |

**Cleanup — publish sonrası, yalnız normal API ile (RETRY-2 task'ında):**

1. `POST /api/crm/territory-models/33fd7e03-…/deactivate` → 200 bekleniyor (model + 3 node `inactive`)
2. `POST /api/crm/territory-models/33fd7e03-…/archive` → 200 bekleniyor (model + 3 node `archived`, read-only)
3. Diğer smoke kayıtları (`SMOKE-MOD0151-OVL-…`, `-NODE-…`, `-EXP-…`) draft veya archived kalır; hard delete yoktur.

**Yasak:** Mongo hand-edit · status'u elle değiştirme · hard delete · kaydı DB'den silme. Bu task'ta bunların
**hiçbiri yapılmadı**; modele hiç dokunulmadı.

---

## 10. Guard Checks

| Check | Result |
|---|---|
| FU03 başlatıldı mı? | **No** |
| Assignment / resource / evidence / import-export açıldı mı? | **No** |
| Workflow approval eklendi mi? | **No** |
| MOD-0023 entegrasyonu eklendi mi? | **No** |
| Submit/approve/reject endpoint eklendi mi? | **No** |
| Brand Scope eklendi mi? | **No** |
| Product/Brand master touched? | **No** |
| Account/Contact touched? | **No** |
| Hard delete eklendi mi? | **No** |
| Mongo hand-edit yapıldı mı? | **No** |
| Tenant verisi manuel düzeltildi mi? | **No** |
| Existing reference codes silindi mi? | **No** (yalnız 2 ekleme; hiçbir kod/sortOrder değişmedi) |
| Existing status semantics kırıldı mı? | **No** (`superseded`/`ended` anlamları korundu ve §16.1'de netleştirildi) |
| Pack §16 ve §22.1 uyumlu hale geldi mi? | **Yes** (§16 tablosu + yeni §16.1 sahiplik matrisi) |
| Lifecycle-required status values authoring dosyasında var mı? | **Yes** (model-status 7/7, node-status 5/5) |
| Tests canlıdaki vocabulary mismatch'i yakalayacak hale geldi mi? | **Yes** — mutasyon testiyle kanıtlandı (6 test kırmızıya döndü) |
| Runtime lifecycle code değişti mi? | **No** — handler/guard/endpoint/UI/RESX'e dokunulmadı |
| Runtime code değişti mi? | **Yes, sınırlı:** yalnız `TerritoryReferenceSets.cs` içindeki 2 `ExpectedValueCount` sabiti (6→7, 4→5). Gerekçe §6'da; bu değerler contract'ın readiness descriptor'ını besler, davranışsal guard değildir |
| MOD-0048 publish yapıldı mı? | **No** (bilinçli — §8 operator prompt'u) |
| Canlı smoke çalıştırıldı mı? | **No** |
| Locked active smoke model API dışı değiştirildi mi? | **No** (hiç dokunulmadı) |
| `crm.territory.delete` / `crm.micro-zone.manage` eklendi mi? | **No** |
| Direct `:5061` business API çağrısı yapıldı mı? | **No** |
| TenantId payload gönderildi mi? | **No** (hiç API çağrısı yapılmadı) |
| Gateway route / RBAC seed değişti mi? | **No** |
| Testler geçti mi? | **Yes** — Territory 71/71, tüm suite 240/240 |

---

## 11. Final Verdict

### **PASS**

- Kök neden **doğrulandı** ve tek bir governance drift'ine indirgendi (§16, §22.1'den önce yazılmış; §13.1 baz alınmış).
- Karar **Seçenek A** olarak gerekçelendirildi; B ve C reddedilme nedenleriyle birlikte kayda geçti.
- **Pack / reference / authoring template / runbook / checklist hizalaması tamamlandı**; lifecycle-required status
  değerleri artık eksiksiz.
- **Test boşluğu kapatıldı** ve kapandığı **mutasyon testiyle kanıtlandı** — bu sınıftaki hata bir daha canlıya
  kadar gizlenemez.
- Runtime dışı hiçbir scope açılmadı; lifecycle kodu, UI ve RESX değişmedi; tek kod değişikliği iki sayı sabiti.
- **Publish gereksinimi** tenant, set listesi ve beklenen sayılarla net raporlandı (§8).
- **Kilitli model cleanup planı** yalnız normal API adımlarıyla net (§9).

---

## 12. Next Recommended Prompt

**1. Sıradaki (zorunlu):**

`@orchestrator MOD-0048 — MOD-0151 Lifecycle Status Reference Publish`

Tenant `97c59330-dbc4-4665-b29c-0c26dbb5cc93` için **yalnız** `territory-model-status` (+`inactive`) ve
`territory-node-status` (+`archived`) set'lerini maker-checker akışıyla re-publish et. Mevcut value'ları silme,
yeniden adlandırma veya sortOrder'larını değiştirme. Kaynak: `mod-0151-territory-reference-values.json`,
talimat: runbook **§0**. Publish sonrası beklenen: required **64/64**, toplam **75/75**, contract
`expectedValueCount == actualValueCount` (7/7 ve 5/5).

**2. Ardından:**

`@orchestrator MOD-0151 FU02B — Authenticated Gateway Live Smoke Closeout RETRY-2`

Aynı zinciri yeniden koş (bu kez `deactivate` ve `archive` 200 beklenir) **ve** §9'daki kilitli
`SMOKE-MOD0151-LIFE-20260728074528` modelini normal API ile deactivate → archive ederek temizle.

**3. RETRY-2 PASS olduktan sonra:**

`@orchestrator MOD-0151 FU03 — Assignment Rules + Preview`

### Ayrı, bloklamayan follow-up'lar

| # | Prompt | Neden ayrı |
|---|---|---|
| G | `MOD-0151 FU02B — Lifecycle Audit Log Sink Hardening` | CrmService structured lifecycle audit event'leri hiçbir log dosyasına düşmüyor (`logs/Crm-watch-*.log` boş/bayat), bu yüzden RETRY smoke'unda audit kanıtı üretilemedi. **Vocabulary defect'inin sebebi değildir** — gözlemlenebilirlik eksiğidir |
| H | `MOD-0048 — Business Unit Reference Set Publish for MOD-0151 FU02A` | `business-unit` value seti publish olmadığı için Alpha/Beta seçimi ve BU-scope'lu overlap senaryosu canlıda doğrulanamıyor. **Lifecycle status defect'i ile ilgisi yoktur**; ayrı FU02A follow-up'ıdır |
