# WP-MVP1-SEC-ESIGN1 — kanıt (2026-10-14, CT bekliyor)

> Dal `fix/mvp1-sec-esign1`, taban `acff1f7be`, worktree `C:\Users\Ali\erp-vnext-wt-sec-esign1`.
> Belge yeri: `docs-organization` beş sorusundan 2. soru ("belirli bir tarihte olanı kaydediyor, değişmeyecek") → `records/audits/2026-10/`.
> Sabotaj betiği: scratchpad `sabotage.js`. Metni aşağıda §5'te.

## 0. Özet

| madde | durum | commit |
|---|---|---|
| 1 — istek listesi sıralamasına kalıcı test | **kapandı** | `fd88f8832` |
| 4 — gövde içi yetki kontrolü → `[HasAnyPermission]` | **kapandı** | `8687da0a2` |
| 5 — `InternalESignActionsController.Register` iç uç izin listesinde | **kapandı** (koruma bu dalda ve main'de var, yerinde eklendi) | `a013a621e` |
| 6 — 7 e-imza sayfasına "?" yardım metni | **BLOCKED** (§4) | — |

## 1. Madde 1 — `ESignatureRequestListOrderMongoTests`

Test, üretimdeki gerçek `ESignRequestRepository` ile ve teste ait tek düğümlü bir replica set üzerinde koşuyor (`DisposableMongoReplicaSet`, `DITEN_TEST_MONGOD`; asla 27017).

| test | doğruladığı |
|---|---|
| `The_signer_list_is_due_date_ascending_then_the_newer_request_first_and_stays_in_its_tenant` | Dört istek: a +3 gün, b +1 gün, c1 +2 gün (eski), c2 +2 gün (yeni). Dizi **bütün olarak** `["b","c2","c1","a"]`. Aynı imzacının başka kiracıdaki (son tarihi en yakın) isteği listede yok; total 4, waiting 4. |
| `The_overdue_list_is_waiting_requests_past_due_ascending_and_stays_in_its_tenant` | Son tarihi geçmiş üç Waiting istek artan sırada: `["late-1","late-2","late-3"]`. Henüz süresi dolmamış istek, imzalanmış istek ve başka kiracının isteği girmiyor. Diğer kiracının listesi yalnız kendi isteğini veriyor. |

Üretim kodu değişmedi (`ESignatureRepositories.cs:242`, `:267`). Sıra zaten doğruydu; eksik olan onu kanıtlayan testti.

## 2. Madde 4 — `[HasAnyPermission]`

### 2.1 Ölçüm

- `HasPermissionAttribute`, `AllowMultiple = true` ve `IAsyncAuthorizationFilter` (`Security/HasPermissionAttribute.cs:13–14`). Aynı aksiyondaki iki etiket iki ayrı filtredir; ilk sonuç üreten zinciri keser, yani **iki anahtarın ikisi de** gerekir (VE).
  - Kodla doğrulandı: `HasPermission_allows_multiple_and_the_two_are_separate_filters`.
  - Testle doğrulandı: `Two_HasPermission_attributes_are_AND_one_key_is_not_enough` (tek anahtar → 403, ikisi birden → geçer).
- A1 otomatik kaydı (`HasPermissionReflector.CollectPermissionKeys`, `PlatformPermissionAutoRegistrationWorker.cs:56`) yalnız `HasPermissionAttribute.Permission`'ı okuyordu. Gövde içindeki iki kontrol bu yüzden görünmüyordu.
- `LoginOnlyEndpointGuardTests` da bunları görmüyordu. Tabandaki kırmızı: "These actions carry no permission key … `ESignatureSamplesController.Image, ESignatureSettingsController.ListMeanings`".
- platform_admin / partner_admin yolu `HasPermissionAttribute` içinde: e-posta → `IPlatformAdministratorRepository` → aktif mi, davet kabul edilmiş mi; geçerse anahtar aranmaz.

### 2.2 Değişiklik

- **`Security/HasAnyPermissionAttribute.cs` (yeni):** `params string[]`; boş ya da boşluklu anahtar kabul edilmez.
  - Kural yeniden yazılmadı: her anahtar gerçek bir `HasPermissionAttribute` ile sırayla değerlendirilir ve ilk geçen anahtar yeter.
  - Anonim istek ilk yanıtta durur (401). Hiçbir anahtar geçmezse son ret (403) kalır.
  - platform-aktör kuralı aynen işler: testle doğrulandı.
  - `HasPermissionAttribute`'a dokunulmadı.
- **`HasPermissionReflector.cs`:** sınıf ve aksiyon düzeyindeki `[HasAnyPermission]`'ın her anahtarı A1'e eklenir (yalnız satır eklendi).
- **`ESignatureSamplesController.Image`:**
  - `[Authorize]` yerine `[HasAnyPermission(MySamplesManage, SamplesApprove, SignaturesRead)]`; gövdedeki 403 dalı kaldırıldı.
  - Çağıranın hangi anahtarları taşıdığı handler'a hâlâ gidiyor (`canApprove`, `canRead`). Bu artık kapı değil; hangi örneği görebileceğinin verisi.
- **`ESignatureControllers.cs` `ListMeanings`:** `[HasAnyPermission(MeaningsRead, SignaturesSign)]`; gövde kontrolü kaldırıldı.
- **Korumalar:**
  - `LoginOnlyEndpointGuardTests.IsKeyGated`: `HasAnyPermission` de sayılıyor (bir satır eklendi).
  - `ESignatureArchitectureTests`: iki aksiyonda etiket zorunlu ve her anahtarı manifestte aranıyor.

### 2.3 Testler

- `HasAnyPermissionAttributeTests` — 11 test:
  - VE ölçümü;
  - her iki anahtar ayrı ayrı geçer;
  - anahtarsız 403, anonim 401;
  - platform_admin ve partner_admin geçer;
  - boş anahtar reddedilir;
  - A1 beş anahtarı topluyor.
- `ESignatureAnyPermissionHttpTests` — gerçek host üzerinde:
  - (b) `Image`, her anahtar ayrı ayrı 200 döner:
    - sahip;
    - bekleyen örneğin onaycısı;
    - örneği gösteren imzanın okuyucusu.
  - (b) `ListMeanings`, iki anahtar ayrı ayrı 200 döner.
  - (a) Anahtarsız kullanıcı (yalnız `esign.policy.read`) → 403, iki aksiyonda da.
  - (c) Anonim → 401, iki aksiyonda da. İstek Gateway'deki gibi `X-Tenant-Id` ile gönderildi. Kiracısız anonim istekte `TenantResolutionMiddleware` yetki filtresinden önce 400 "Missing Tenant" döner; bu belgelenmiş sıradır.

## 3. Madde 5 — iç uç izin listesi

**Ölçüm** (`git fetch origin`, origin/main = `19497c934`, round-2 = `ebcb33a62`):

| ref | koruma | `InternalESignActionsController` |
|---|---|---|
| bu dal (`acff1f7be`) | `LoginOnlyEndpointGuardTests.AnonymousInternalControllers` (13 kayıt) + `Every_anonymous_action_is_an_allow_listed_internal_api_key_route` | yok → tabanda **kırmızı** |
| origin/main | aynı dosya, birebir (`git diff HEAD origin/main -- <dosya>` boş) | yok; e-imza denetleyicisi main'de de var |
| origin/chore/ct-round-2 | genişletilmiş sürüm (17 kayıt; güvenilir servis şeması paragrafı, BL-454, ürün-beş devralma) + `EmailShellDispatchTests` | yok |

Sonuç: koruma main'de olduğu için kural gereği **yerinde eklendi**. Eklenen satır (`LoginOnlyEndpointGuardTests.cs`, liste içi):

```csharp
        // MOD-0022 CT-D12 / WP-MVP1-SEC-ESIGN1 — POST api/internal/esign/actions/register (ESIGN-BUNDLE §4.23). Caller: a consumer
        // module registering its signable actions at start-up (e.g. InventoryService MOD-0175 quarantine actions), service to service.
        // Gate: X-Internal-Api-Key compared in fixed time with AuthService:InternalApiKey, fail closed when unset or absent — checked
        // before the body is read (InternalESignActionsRegisterAuthTests). Not routed by the Gateway: no browser or tenant token reaches it.
        "InternalESignActionsController",
```

- Round-2 sürümü main'e indiğinde aynı satır oraya da girmeli. Kendi listesinde bu kayıt yok; CT round-2 sahibine iletir.
- `InternalESignActionsRegisterAuthTests`:
  - anahtarsız, yanlış anahtarlı ve kiracı token'lı istek → 401 ve katalogda kayıt yok;
  - doğru anahtar kapıyı geçer.
- Taban kırmızısı `Every_anonymous_action_is_an_allow_listed_internal_api_key_route` artık yeşil.

## 4. Madde 6 — BLOCKED: yeniden kullanılacak bir "?" yardım bileşeni yok

Prompt diyor ki: "Mevcut yardım ("?") bileşenini ve resx desenini başka bir modülden ölç ve aynısını kullan. Yeni bileşen icat etme." Ölçüm:

- Bu dalda, origin/main'de ve round-2'de `frontend/Diten.Web/Views/**` ile `wwwroot/assets/js/**` altında sayfa düzeyinde bir yardım ("?") bileşeni yok.
  - Aranan desenler: `PageHelp`, `_Help.cshtml`, `page-help`, `HelpButton`, `help-trigger`.
  - Ayrıca `git log --all -G` ile tüm dalların geçmişi de tarandı.
- `bx-help-circle` yalnız iki yerde içerik ikonu olarak geçiyor (CRM Knowledge "faq" türü, Segments bölüm başlığı) ve `_GlobalConfirmation` onay ikonunda. Yardım bileşeni değil.
- `wwwroot/assets/js/ui-popover.js` (Bootstrap popover başlatıcı) dosyası var, ama hiçbir view onu yüklemiyor.
- En yaygın yerleşik "açıklama" deseni bir satır içi not: `<p class="text-muted small"><i class="bx bx-info-circle me-1"></i>@Localizer["…"]</p>` (CRM'de yaygın). Bu bir "?" değil.
- 7 e-imza sayfası bulundu:
  - `Views/ESignature/`: `MySignature`, `Requests`, `Verification`, `AuditExport`;
  - `Views/ESignature/Settings/`: `Policy`, `Meanings`, `SampleApprovals`.

Seçenekler (CT kararı):
- **(A)** CT yeni ortak bileşeni onaylar. Önerilen biçim:
  - sayfa başlığının yanında bir "?" düğmesi;
  - Bootstrap popover ya da offcanvas;
  - var olan `ui-popover.js` kullanılır;
  - anahtarlar `ESignature.<Sayfa>.Help.Title` / `.Body`, en + tr;
  - parite testi ve tr-sil sabotajı.

  Bileşen tek kez yazılır, diğer modüller de kullanır.
- **(B)** Yerleşik satır içi `bx-info-circle` not deseni ile her sayfaya bir "Bu sayfa ne işe yarar" paragrafı eklenir. Yeni bileşen olmaz, ama "?" de olmaz.

## 5. Sabotajlar (`sabotage.js`)

Her sabotajda aynı akış izlendi:
1. Değişiklik yapıldı, adı verilen testler koşuldu: kırmızı.
2. Özgün baytlar geri yazıldı; `git checkout` kullanılmadı (git-safety §6).
3. Aynı testler yeniden koşuldu: yeşil.
4. `git status --porcelain` boş.

| | değişiklik | kırmızıya dönen test | sonuç | geri alındı |
|---|---|---|---|---|
| 1a | `ListForSignerAsync` `ThenByDescending(RequestedAtUtc)` → `ThenBy` | `The_signer_list_…` (beklenen `["b","c2","c1","a"]`, konum 1'de fark) | **1 kırmızı** / 2 | 2/2 yeşil, temiz |
| 1b | `ListForSignerAsync` `SortBy(DueAtUtc)` → `SortByDescending` | `The_signer_list_…` | **1 kırmızı** / 2 | 2/2 yeşil, temiz |
| 1c (ek) | `ListOverdueAsync` `SortBy` → `SortByDescending` | `The_overdue_list_…` | **1 kırmızı** / 2 | 2/2 yeşil, temiz |
| 4a | `Image`'dan `[HasAnyPermission]` kaldırıldı | (a) `Image_without_any_of_the_three_keys_is_403_and_anonymous_is_401` | **1 kırmızı** / 1 | yeşil, temiz |
| 4b | any-of → all-of (geçen anahtar artık yetmez) | (b) `Image_the_owner/approver/signature_read…` ×3, `ListMeanings_either_key…` ×2, `HasAnyPermission_lets_any_one_declared_key_through` ×2 | **7 kırmızı** / 18 | 18/18 yeşil, temiz |
| 5a (ek) | izin listesinden `InternalESignActionsController` silindi | `Every_anonymous_action_is_an_allow_listed_internal_api_key_route` | **1 kırmızı** / 6 | 6/6 yeşil, temiz |
| 5b (ek) | `Register` anahtar kontrolü kapatıldı | `Register_without_the_internal_key_is_401…` ×2 | **2 kırmızı** / 3 | 3/3 yeşil, temiz |

## 6. Test sayıları

Ortam: `DITEN_TEST_MONGOD="C:\Program Files\MongoDB\Server\9.0\bin\mongod.exe"`, `DITEN_PLATFORM_TEST_MONGO_URI` tanımsız. Aynı ortam önce ve sonra kullanıldı.

| suite | önce | sonra |
|---|---|---|
| Platform Application, filtre `~ESign` | 163 / 163 | **176 / 176** |
| Platform Application, tümü | 5779 geçti / 262 kırmızı (6041) | **5804 geçti / 260 kırmızı (6064)** |
| Platform BackgroundJobs | 21 / 1 kırmızı (22) | 21 / 1 kırmızı (22), aynı test |
| Platform CompositionGuard | 0 / 2 kırmızı (2) | 0 / 2 kırmızı (2), aynı testler |
| Platform Eventing | 65 geçti, 3 atlandı (68) | 65 geçti, 3 atlandı (68) |
| tests/architecture (`TenantArchitecture.ArchitectureTests`) | 69 / 8 kırmızı (77) | 69 / 8 kırmızı (77), aynı testler |
| Web (`Diten.Web.Tests`) | 1458 / 1458 | 1458 / 1458 |

- Önce ve sonra kırmızı kümeleri ad ad karşılaştırıldı: **yeni kırmızı yok.** Düzelen iki taban kırmızısı:
  - `LoginOnlyEndpointGuardTests.Every_action_has_a_key_…` (madde 4);
  - `…Every_anonymous_action_is_an_allow_listed_internal_api_key_route` (madde 5).
- Yeni testler: +23 (2 sıralama, 11 filtre, 7 HTTP any-of, 3 Register). `~ESign` filtresi +13: sıralama 2, HTTP any-of 7, Register 3 ve adında "esign" geçen A1 testi.
- Değiştirilen ve eklenen dosyalarda derleme uyarısı yok.

**Tabanda kırmızı olanlar** (ölçüldü, bu WP'nin değil):
- Platform App:
  - 252 test `DITEN_PLATFORM_TEST_MONGO_URI is not set` ile kapalı kalarak düşüyor (önceden var olan, 27017 dışı bir Mongo URI istiyor);
  - `VerifiedGsku/VerifiedMarketOperationalEligibilityTests` ×5;
  - `PlatformMongoTestLockTests` ×2;
  - `PpmAuditRetentionPolicySeedMongoTests` ("test-owned mongod executable unavailable");
  - `DateTimeOffsetSortGuardTests`;
  - `BusinessReferenceDataTenantAssignmentMongoTests`.
- BackgroundJobs: `PlatformContainerValidationTests`.
- CompositionGuard: 2 test.
- Mimari:
  - `AuditTrailStandardTests` ×2. İçinde AuthService'in e-imza komutları `ConsumeESignTicketCommand` ve `IssueESignWindowTicketCommand` "YENİ KOMUT DENETİMSİZ" diye geçiyor → FU önerisi §7;
  - `DocsPathGuardTests` ×5;
  - `MongoTestDatabaseGuardTests` ×1.

`AccountKindAcceptanceHost.Tests` koşulmadı. 27017'ye bağlanan test yok; Platform harness'i 27017–27021'i reddediyor.

## 7. Açıklar ve FU önerileri

- **O-ESIGN1-1 = §4** — madde 6 BLOCKED, seçenek A / B.
- **O-ESIGN1-2:** round-2'nin `LoginOnlyEndpointGuardTests` sürümü main'e indiğinde `InternalESignActionsController` kaydı oraya da taşınmalı. Satır §3'te.
- **FU-0022-AUD-01 (öneri):** AuthService `ConsumeESignTicketCommand` ve `IssueESignWindowTicketCommand` AUD-001 mimari testinde "denetimsiz yeni komut" olarak kırmızı. Ya denetim yolu eklenir ya da gerekçeli istisna yazılır.
- **O-ESIGN1-3:** `HasAnyPermission` platform-aktör reddinde her anahtar için yönetici kaydını yeniden okur. Tutarlı sonuç verir, yalnız tekrar okur. Önerilen iyileştirme `HasPermission`'ın kuralını paylaşılan bir iç yardımcıya çıkarmak; bu bir `HasPermission` değişikliği olduğu için bu WP'de yapılmadı.
