---
record_id: MOD-0217-DL2-HAZIRLIK-01
status: hazırlık — dağıtım izni değil, WP prompt'u değil
prepared_at_local: 2026-10-27
refreshed_at_local: 2026-10-28
prepared_by: Treasury Intake Control Tower
pack: execution/domains/master-data-management/module-packs/MOD-0217-contract-lifecycle-management-clm.md (status draft, approval_status not-approved, sha256 4d944a3dcdcdc177e839ddffd1a5ef3eac1b371032f0bedc42aaa8a9dab979ac (pack D80 / FU27 atıf revizyonu, 2026-10-29 — MOD-0018 Treasury Auth eki atıfları `MOD-0018-FU27-treasury-permission-onboarding.md`'ye çekildi (K-1 kapandı, eski geçici ad FU-TBD — KARAR D80/1); Auth paket kapısı maddesi şartlı teyide çekildi (Y-19 / KARAR D80/2: DL-1'i bekletmez; iki şart ekin DL-5 kodundan önce); revizyon notu satırı eklenmedi (satır numaraları korundu); kurallar, AC'ler ve dilim tablosu değişmedi; önceki sürüm `c689f8c2…e9b4` — pack D76 revizyonu, 2026-10-28 — O-16 KAPANDI — KARAR D76/4: ilk aday listesi onay denemesinde deneme başına saklanır (`ContractVersion.CandidatePrincipalIds`, `List<Guid>`, yeni Entity Fields alanı, form alanı değil); aynı anahtarla Submit tekrarı bu listeyi kullanır; AC-33 genişletildi (tekrarda Auth çağrı sayısı 0, giden liste ilk listeye eşit; sabotajlı); yeni AC yok, yeni hata kodu yok; önceki sürüm `aaf7eacd…68fb` — pack D75 revizyonu, 2026-10-28 — Auth aday sorgusu kimliği KARAR D74/1'e çekildi (ortak sır + iletilen kullanıcı token'ı, servis kimliği kullanılmaz; belge deposu servis kimliği D56/2 değişmedi); aynı anahtarla Submit tekrarı ilk aday listesini kullanır (D75/2 (b)); listenin saklandığı yer yeni açık karar O-16; yeni AC yok, AC genişletilmedi, yeni hata kodu yok; önceki sürüm `3bc065c5…de91` — pack D71 revizyonu, 2026-10-28 — D71 aday tabanı `mdm.contracts.versions.approve ∩ platform.workflow.tasks.approve` (anahtar başına ayrı DL-6 çağrısı), K-A4b kapandı, AC-33 genişletildi; önceki sürüm `7fb0eef9…` (pack D70 revizyonu, 2026-10-28 — D70/2 boş aday listesi 422 `MDM_CONTRACT_WORKFLOW_RESOLVER_NO_ELIGIBLE_APPROVER` (AC-33), D70/1 sözleşme onaylayıcı iş akışı izinleri rol şablonuyla (K-A4 kapandı, K-A4b açık), D70/3 Auth paket kapısı; önceki sürüm `de6f15f9…` (pack KARAR D69 revizyonu, 2026-10-28 — sözleşme sürümü onay anahtarı `mdm.contracts.versions.approve`, O-3 (b) kapandı; önceki sürüm `7e6b9520…` (ready-for-dev 1. tur eki D66 revizyonu — D66); daha önce `4c4bf518…` (ready-for-dev 1. tur revizyonu — Y-12, D62–D65); daha önce `f5795529…` (D58/D59 revizyonu); D56 sürümü `647a7679…`; bu kaydın ölçümleri ve satır atıfları önceki sürüm `76e502d6…` içindir)), ölçüm dal chore/ct/treasury-intake-ct-05 @ec063fee9 — çalışma ağacı temiz, dosya HEAD ile aynı; satır atıfları bu sürüme `:N@ec063fee9`)
source: /Users/natig/Projects/ERP-vNext @ da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d (salt okunur — `git ls-tree` / `git archive`; yalnız başvuru malzemesi — KOD ALMA YOK, D38/3)
predecessor: execution/domains/master-data-management/work-packs/MOD-0217-DL1-hazirlik.md
---

> **Güncel durum (2026-10-28, D59–D69 sonrası — pack tek doğruluk kaynağı).** Bu blok kaydın güncel durumudur; aşağıdaki
> tarihli bloklar ve `@904dfd5c2` / `@00d3f2b41` / `@ec063fee9` ölçüm atıfları kendi tarihlerinindir, değiştirilmedi. Pack
> sürümü başlıktaki sha256'dır. Ölçüm (2026-10-28): origin/main @ `027ca2e38` — ct-05 dalı #281 ile main'de (karar dosyası D45–D68,
> MOD-0018 Treasury Auth eki taslağı ve MOD-0217'nin D66 sürümü dahil; bu pack sürümü (D69) ve D69 main'de **değil** — yerel commit `db69732cb`).
> - **Auth paket metni** = MOD-0018 Treasury Auth eki (`execution/domains/platform-shared-services/module-packs/MOD-0018-FU27-treasury-permission-onboarding.md`; Treasury yazar, MOD-0018 sahibi
> Ali teyit eder — Y-15, Y-16; FU27 — K-1 kapandı, Y-19 / D80/1). Taslak main'de, `draft`; **2026-10-29: MOD-0018 sahibi D69–D78 sürümünü (main `4c08fc75a`) şartla teyit etti — KARAR D80/2, Y-19:** DL-1'i bekletmez; ekin DL-5 kodundan önce K-W5 defter kararı + rol atama adımlarının denetim borcu; D79'dan sonraki değişiklikler ayrıca teyide gider — pack'teki "Auth paket metni
> main'de" maddesi şartlı işaretli (D80 main'de, `317a48b13`; KARAR D82/1), paket kapısı şartlı (D53/1, D80/2). Bu paketin anahtarları, açık atamaları ve rolleri ekin **DL-3**'ü (`mdm.contracts.*` 18 anahtar, 4 rol, 1 açık atama — D69 dahil;
> koşullu: MOD-0217 onayından sonra); kayıt
> kodu DL-1 kabul kapısı (D53/2). D53 ve D56 aynen geçerli; Y-14 geçersiz (kullanılmadı).
> - **SEC-002:** ekin **DL-4**'ü (KARAR D56/1; Platform dosyaları için XMC-001 Beste — D63/2): paket metni paket kapısı, kodu
> DL-1 kabul kapısı. 2026-10-27 ölçümü: bugün çalışmaz (modül kodu iki tarafta `product-item-sku-master`'a sabit).
> - **MOD-0023 şablon kodu geldi:** `MDM-CONTRACT-VERSION-APPROVAL` (Y-12; W-5, W-6 — `docs/records/decisions/2026-10/mod0023-workflow-template-owner-decisions-01.md`, #277 ile
> main'de; MOD-0217 onayından sonra kullanılır) — tek aşama, tek onay adımı, atamasız, ayrı hukuk adımı yok. Şablonu kuran MOD-0023
> kodu main'de **değil** (O-3 (a)); örnek durum adları DL-3 başında (O-3 (c)) — DL-3 dilim kapısı.
> - **Onaylayıcı aday tabanı** = ekin **DL-6** aday sorgusu (D66/1; D67/1 — DL-6 paket metni DL-3 önkoşulu, kodu DL-3 kabul
> kapısı). **Sorgunun anahtarı `mdm.contracts.versions.approve` — KARAR D69** (O-3 (b) kapandı): yeni rol "Sözleşme onaylayıcı"
> (`mdm.contracts.read`, `mdm.contracts.documents.read`, `mdm.contracts.versions.approve`; açık atama değil); sürümü hazırlayan, şirket
> kapsamı dışı ve anahtarı olmayan kullanıcı aday olmaz (AC-33 genişledi); boş liste MOD-0023'te 400 → kesin ret (AC-53). Rolün
> MOD-0023 görev anahtarları D69'da yazılmadı — **D70/1 ile kapandı** (rol şablonuyla taşır; ek K-4 (b), K-A4 kapandı; elle atamada açık nokta K-A4b — **KARAR D71 ile kapandı**: aday listesi `platform.workflow.tasks.approve` ile de kesiştirilir). D69 main'de değil (D62/1: ekin DL-3 paket metninin D69
> kısmı onu bekler).
> - **Belge deposu:** MdmService servis kimliğiyle (KARAR D56/2 — D41/10'un MOD-0262 kısmının ve D46a'nın yerine); kullanıcılara
> `platform.document-repository.*` verilmez; MOD-0262-FU01'de servis kimliği desteği DL-2 dilim kapısı (Platform — Beste).
> - **BL-565 / RCS-001** DL-1 kabul kapısıdır (D59/1): DL-1 `IRecordCodeAllocator` sahtesiyle yazılır; gerçek servis kabulden
> önce main'de olmalı (v0 sözleşmesi Beste, 6 Kasım). Ölçüm: servis main'de yok (BL-565 `DURUM: AÇIK`).
> - **Rol matrisi** D69 ile 117 anahtar / 26 rol; MOD-0217 18 anahtar / 4 rol.
>
> **Durum (2026-10-27).** İlk DL-2 hazırlık kaydı. Pack sürümü DL-1 kaydının bağlandığı sürümle aynıdır (`76e502d6…`); bu sürüme
> CT-APR-02 satırı **var** (`execution/approvals/reviews.tsv:94` — UYGUN, 0 ENGEL), CT-APR-01 satırı **yok**. DL-2, DL-1'in
> **kabulünden** sonra başlar (pack "Teslim dilimleri" DL-2 Önkoşulu, `:1520@ec063fee9`); bu kayıt DL-1'in paket kapısını yeniden
> ölçmez, DL-2'nin kendi dilim kapısını ("— DL-2 için", D51/1) ve DL-2'ye özgü bağımlılığı (MOD-0262-FU01) ölçer.

> **Kod alma yok (D38/3).** Kaynak (`S@da4abb20`) yalnız okunur; DL-2'nin belge bağlama, değişiklik sürümü ve indirme denetimi
> Recovery'de sıfırdan yazılır. Kaynakta MOD-0262'ye belge bağlayan MDM kodu **yoktur** — belge yükleme kaynakta web katmanında yerel
> klasöre yazılıyordu (alma, madde 10). Bu kayıt §2'de **al-uyarla** kararı vermez.

# MOD-0217 DL-2 hazırlığı — sözleşme sürümleri ve belgeler (MOD-0262-FU01)

Bu kayıt, MOD-0217 DL-1 kabul edildiğinde DL-2 WP prompt'unun aynı gün yazılabilmesi için **neyin yeniden yazılacağını**, **kaynakta
hangi davranışın referans alınacağını**, **neyin alınmayacağını** ve DL-2'nin **dilim kapısını** önceden ölçer. Kod yazdırmaz; paketi,
kuyruğu, onay / inceleme kayıtlarını değiştirmez.

**DL-2 kapsamı** (pack "Teslim dilimleri", `:1516-1523@ec063fee9`): `AddContractDocumentCommand`, `WithdrawContractDocumentCommand`,
`ReconcileContractDocumentCommand`, `GetContractDocumentContentQuery` (indirme denetimi AC-62, D47/3), `CreateContractAmendmentCommand`
(değer kısmı AC-65, D49/2), `WithdrawContractVersionCommand`, `Infrastructure/DocumentRepository/**` (MOD-0262-FU01 istemcisi; MOD-0028
istemcisi **yok** — D40), belge uçları. Çağrı kimliği **MdmService servis kimliği**, `scope = Documents` (KARAR D56/2 — D41/10'un MOD-0262 kısmının yerine;
kullanıcılara `platform.document-repository.*` verilmez). Onay / imza
(MOD-0023) DL-3, ekran DL-4.

## 1. Başlama koşulları (ölçüm: çalışma ağacı @ec063fee9, 2026-10-27)

Kaynak: pack "Ready-for-dev Checklist" → "Dilim kapıları" (`:1309-1325@ec063fee9`), DL-2 Önkoşul metni (`:1520`), kapanış tablosu
MOD-0262-FU01 satırı (`:523`). "DL-2'ye etkisi": **bekletir** / **kabul** / **—** (MOD-0289 DL-2 kaydıyla aynı anlam).

| Koşul | Sahibi | Bugün | DL-2'ye etkisi |
|---|---|---|---|
| DL-1 kabul edildi (`:1550`) | DL-1 WP + CT | DL-1 başlamadı: MdmService'te `Contract` varlığı / `Features/Contract` yok (`ls`; yalnız `AuditIntentContract.cs`, `BrandProductContract`); DL-1 paket kapısı açık — CT-APR-01 yok, DCP-011 `draft` ve MOD-0217 yalnız **aday** (`DCP-011-finance-treasury-intake.md:7,17`), Auth paket metni (MOD-0018 Treasury Auth eki taslağı #281 ile main'de, teyitsiz [2026-10-29: şartla teyit edildi, DL-1'i bekletmez — KARAR D80/2, Y-19]; ölçüm anında MOD-0018'de `mdm.contracts` 0), BL-565 açık — paket kapısı değil, DL-1 kabul kapısı (D59/1), **SEC-002 sağlayıcısı** (aşağı) | bekletir (ilk ve en uzun engel; ayrıntı DL-1 kaydı §1) |
| SEC-002 sağlayıcısının `mdm.contracts.*` ile çalışması (paket kapısı, `:1292`) | MOD-0018 Treasury Auth eki DL-4 (KARAR D56/1): Treasury yazar, MOD-0018 sahibi teyit eder; XMC-001 Beste (Platform, D63/2) + MDM sahibi | **ölçüldü 2026-10-27: bugün ÇALIŞMAZ** — MDM istemcisi `ModuleCode = "product-item-sku-master"` sabit, başka modül kodu 400 (`services/Diten.MdmService/src/Diten.MdmService.Infrastructure/Authorization/PlatformTrustedLegalEntityScopeProviderClient.cs:18,60-62,163-171@ec063fee9`); Platform ucu yalnız bu modül + sabit `FirstConsumerPermissions` (`services/Diten.Platform/src/Diten.Platform.API/Security/TrustedLegalEntityScopeCredentialAuthenticator.cs:35-56,97-114`). DL-1 kaydındaki "kısmi inceleme" bu ölçümle kapanır, sonuç olumsuz | bekletir (DL-1 üzerinden: ek DL-4 paket metni paket kapısı, kodu DL-1 kabul kapısı — D56/1; DL-2'nin AC-5/AC-6 belge uçları kısmı da aynı sağlayıcıya bağlı) |
| MOD-0262-FU01 sözleşmesi kodda ölçüldü (`:1310-1315`) | — | **kapandı — bugün yeniden ölçüldü, aynı:** uçlar `[Route("api/v1/document-repository")]` + `POST objects` (`.manage`), `GET objects/{contentId}` ve `…/content` (`.read`), `POST objects/{contentId}/compensate` (`.manage`) (`services/Diten.Platform/src/Diten.Platform.API/Controllers/DocumentRepositoryController.cs:40,59-61,87-89,97-99,116-117@ec063fee9`); yükleme form alanları `file`, `owningItemId`, `owningVersionId`, `companyId`, `scope` (`:63-68`); izinler `platform.document-repository.read` / `.manage` (`services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/DocumentRepositoryModels.cs:17,20`); meta `RepositoryObjectModel` `OwningItemId`, `OwningVersionId` döner, `CompanyId` dönmez (`:53-64`); `ContentStorageScope.Documents = 0` (`…/Contracts/DocumentRepository/IContentStorageGateway.cs:65-67`); boyut varsayılanı 256 MB (`ContentStorageOptions.cs:25`), uzantı izin listesi var (`:27-28`), medya türü listesi boş (`:30`). Gateway rotası var (`gateway/Diten.ApiGateway/ocelot.json:1857-1885` — `/api/v1/document-repository` ve `/{everything}`) | — |
| O-13 çağrı kimliği (D41/10) — DL-2 başında ölçümle teyit (`:1317-1318`) | — | **2026-10-28: KARAR D56/2 ile değişti** — belge deposu MdmService servis kimliğiyle; kullanıcılara `platform.document-repository.*` verilmez; indirme yalnız MOD-0217 uçlarıyla (kapsam + AC-62). Ölçüm anında: **karar verildi; ölçüm tutarlı:** servis kimliği yok, yetki kullanıcı izinleriyle (`DocumentRepositoryAccessPolicy.cs:25-39`); `scope = Documents` mevcut. Ölçüm kararı çürütmüyor — ama genel anahtarların kiracı geneli etkisi var (§5 bulgu 2) | **bekletir** — yeni dilim kapısı: MOD-0262-FU01'de servis kimliği desteği main'de (Platform — Beste; XMC-001); pack maddesi işaretsiz |
| MOD-0262-FU01 canlı doğrulama kaydı (dev ortamında yükle / indir / başka kiracı 404) (`:1316`) | Treasury CT | **yok** — `docs/records/**`'te Treasury için MOD-0262-FU01 koşu kaydı bulunmadı (grep); FU01 pack'i `status: ready-for-dev`, `runtime_implementation: in-progress` (`execution/domains/platform-shared-services/module-packs/MOD-0262-FU01-document-binary-store.md:10,20`) | **bekletir** — CT'nin kapatabileceği DL-2 maddesi; D56/2 sonrası kayıt servis kimliğiyle alınır, önce servis kimliği desteği gerekir (dev ortamı + iki kiracı; `scripts/run_record.py`) |
| MOD-0262 meta yanıtında sahiplik alanları (Reconcile için, `:592`) | — | **ölçüldü: dönüyor** — `OwningItemId`, `OwningVersionId` (`DocumentRepositoryModels.cs:53-64`) | — |
| Auth: belge anahtarları `mdm.contracts.documents.*`, `amendments.create` (MOD-0018 Treasury Auth eki DL-3); `platform.document-repository.*` **hiçbir rolde yok** (KARAR D56/2 — D46a geri alındı) | Treasury yazar, MOD-0018 sahibi teyit eder (paket metni D53/1) | `services/**`'te `mdm.contracts` 0; Platform anahtarları kodda var (`DocumentRepositoryModels.cs:17,20`) — yeni Platform anahtarı gerekmez; kullanıcılara verilmez (D56/2) | — (paket kapısı / DL-1 kabulünde; DL-2 testleri gerçek izinle) |
| Gerçek replica set + hata enjeksiyonu altyapısı (AC-26, AC-31, AC-49, AC-50, AC-58) | — | **var:** `MdmTestMongo` tek düğümlü replica set + `enableTestCommands=1` (`services/Diten.MdmService/tests/Diten.MdmService.Application.Tests/MdmTestMongo.cs:13,156,494@ec063fee9`) → `failCommand` kullanılabilir (pack Test Expectations `:1223` "ölçülmedi" — bugün ölçüldü) | — |
| DL-2 kuyruk satırı | Treasury CT | **yok** — `CT-QUEUE.tsv:24` yalnız `WP-0217-DL1` | — (DL-1 kabulünden sonra CT açar) |

**DL-2'yi ilk bekleten:** (1) **DL-1 kabulü** — DL-1'in paket kapısında dış sahipli, tarihsiz maddeler (Auth paket metni
— MOD-0018 Treasury Auth eki, taslak main'de, MOD-0018 sahibinin teyidi bekleniyor; DCP-011 üyeliği + onayı — Beste) ve **SEC-002
sağlayıcısı** (KARAR D56/1: ekin DL-4'ü — paket metni paket kapısı, kodu DL-1 kabul kapısı; Platform dosyaları XMC-001 Beste). (2) DL-2'ye
özgü açık maddeler: **MOD-0262-FU01'de servis kimliği desteği** (KARAR D56/2; Platform — Beste) ve **MOD-0262-FU01 canlı doğrulama
kaydı** (servis kimliğiyle; Treasury CT'nin işi, dev ortamı gerekir).
**BL-565 / RCS-001 kod servisi** paket kapısında değil, DL-1 kabul kapısında (D59/1): DL-1 kodu sahte allocator ile
yazılabilir, kabulden önce gerçek servis main'de olmalı (Beste, 6 Kasım).

## 2. Kaynak dosyaların sınıflandırması (DL-2) — yalnız referans

Kaynak MOD-0217 kodu kaynak MDM servisindedir (`M/` = `services/DitenMdmService/src/`). Özet: `shasum -a 256` (ilk 16 hane), satır
sayısı `wc -l`, ikisi de @da4abb20, 2026-10-27'de `git -C /Users/natig/Projects/ERP-vNext archive da4abb20 <yol…>` ile salt okunur
ölçüldü (10 dosya; DL-1 kaydındaki 4'ü aynı sha ile yeniden ölçüldü, 6'sı yeni). Karar dili: **yalnız referans** = davranış / alan fikri
okunur, kod alınmaz; **alma** = pack "Taşınmayacaklar" (`:1382-1413@ec063fee9`, "madde N"); **sonraki dilim**. **al-uyarla yok
(D38/3).**

**Özet:**

| Recovery'de sıfırdan yazılacak (DL-2) | Kaynakta referans alınacak davranış | Alınmayacak |
|---|---|---|
| belge ekleme (MOD-0262'ye akışla), geri çekme, bağ mutabakatı (çit yolu), içerik indirme + indirme niyeti, değişiklik sürümü (`ContractVersion`, `Kind = Amendment`) + değer yazma izni, sürüm geri çekme; `IDocumentBinaryStorePort` + `DocumentBinaryStoreClient` | değişikliğin "Approved / Active sözleşmede yeni sürüm, `VersionNo + 1`, bayat sürüm 409" fikri (`ContractCommandHandlers.cs:1045-1110`) | ayrı `ContractAmendment` varlığı ve deposu (sürüme birleşir); kanıt geçidi (MOD-0031, v1'de yok — D41/6); web katmanında yerel klasöre yükleme (madde 10); kaynak rotalar / izinler / röle / geri çağrı (madde 1-9) |

### 2.1 Domain ve Persistence

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `M/Diten.MdmService.Domain/Entities/ContractVersion.cs` | `337dd2d7a6bdf9fc` · 64 | yalnız referans | `ContractVersion` (DL-1'de yazıldı) + DL-2 alanları `Documents: List<ContractDocumentRef>`, `AbandonedContentIds` (pack "Entity Fields" `:378-379`) | kaynakta belge işaretçisi ve çit alanı yok |
| `M/Diten.MdmService.Domain/Entities/ContractAmendment.cs` | `38e30644579fce8b` · 56 | yalnız referans → **birleşir** | `ContractVersion` (`Kind = Amendment`, `AmendmentReason`) | ayrı soy (`CreateLineage`) modeli alınmaz |
| `M/Diten.MdmService.Persistence/Repositories/ContractAmendmentRepository.cs` | `0dc201888ee9174d` · 38 | **alma** | — | ayrı koleksiyon yok |
| `M/Diten.MdmService.Persistence/Repositories/ContractVersionRepository.cs` | `0c3428acb8538389` · 80 | yalnız referans | `ContractVersionRepository` (DL-1) + koşullu işaretçi / çit yazımı, `OpenVersionNo` CAS | işlem iddiası alıntılanmaz (TRM-11) |

### 2.2 Application, Api, Infrastructure

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `M/…/Features/Mod0217/Contracts/ContractCommandHandlers.cs` | `4cca82fe84443ee2` · 2368 | yalnız referans (yalnız :1045-1110 `CreateContractAmendmentCommandHandler`) | `Features/Contract/{Commands/CreateContractAmendmentCommand.cs,Handlers/…,Validators/CreateContractAmendmentValidator.cs}` | kaynak: durum `ACTIVE` / `APPROVED` dizgisi (:1078-1081), yanlış durumda **400** — Recovery 409 `MDM_CONTRACT_STATE_INVALID` (pack Hata sınıflandırması); `ExpectedVersion` isteğe bağlı (:1090) → Recovery `If-Match` zorunlu + `OpenVersionNo` CAS + `ux_mdm_contract_version_no` (AC-31); değer yazma izni kaynakta yok → `value.write` (AC-65); serbest İngilizce ileti alma |
| `M/…/Features/Mod0217/Contracts/ContractCommands.cs` | `ae17bfb019477a93` · 219 | yalnız referans (amendment isteği alanları) | `ContractAmendmentRequest` (`ContractModels.cs`) | — |
| `M/Diten.MdmService.Api/Controllers/ContractsController.cs` | `ca91ee74fe86d436` · 377 | yalnız referans (:244-249 `CreateAmendment` uç şekli) | `ContractsController` (DL-1) + DL-2 aksiyonları | `Mod0217Permissions.Amend` (:244) alma → `mdm.contracts.amendments.create`; rota alma (madde 1); kaynakta belge uçları **yok** |
| `M/Diten.MdmService.Application/Interfaces/IMod0217Integration.cs` | `372a1b2424e6ac72` · 355 | yalnız referans (port sınır fikri) | `IDocumentBinaryStorePort` | kaynak entegrasyon arayüzleri iş akışı / kanıt / olay içindir; belge deposu portu yok |
| `M/Diten.MdmService.Infrastructure/Services/Mod0217ContractEvidenceGateway.cs` | `549fe73215f2fd1f` · 464 | **alma** | — | MOD-0031 kanıt değerlendirmesi (sabit `http://localhost:5000/…evidence-requirements:evaluate`, :16) — v1'de kanıt yok (O-9, D41/6) |
| `frontend/Diten.Web/Controllers/ContractsController.cs` | `b6fb0cf5c7da2c99` · 1820 | **alma** | `Infrastructure/DocumentRepository/DocumentBinaryStoreClient.cs` (MDM) + DL-4 vekili | madde 10 — `wizard/upload-document` `IFormFile` + 50 MB sınır + yerel klasör (:712-748); Recovery'de MdmService diske / geçici klasöre yazmaz (AC-24) |

### 2.3 Testler

Pack "Test Expectations" (`:1220-1242`): kaynak MOD-0217 işlem testleri kanıt değildir (TRM-11); DL-2 testlerinin hepsi Recovery'de
yazılır. Bu kayıt kaynak test dosyalarını ölçmedi.

### 2.6 Yetki, roller ve denetim — DL-2 (D41/1, D41/10, D46a, D47/3, D47/5, D49/2)

Pack "Authorization Convention" (`:861-905@ec063fee9`) ve işleyen tek matris "D47 rol matrisi (CT04a; D49 ile güncellendi)"
(`docs/records/decisions/2026-10/treasury-intake-owner-decisions-05.md:109-206@ec063fee9`).

- **DL-2 anahtarları:** `mdm.contracts.documents.read` (içerik indirme), `.documents.create` (ekleme + `reconcile`), `.documents.delete`
  (geri çekme), `mdm.contracts.amendments.create` (CreateAmendment), `mdm.contracts.update` (WithdrawContractVersion), `mdm.contracts.value.write`
  (değer taşıyan CreateAmendment — komut iznine **ek**, handler içi, D49/2), `mdm.contracts.value.read` (yalnız görme). **Bağımlı** belge deposu
  anahtarı yok — KARAR D56/2: MdmService MOD-0262-FU01'i kendi servis kimliğiyle çağırır; `platform.document-repository.read` /
  `.manage` hiçbir rolde yok (D46a geri alındı).
- **Roller (matris bölüm 3, `:199-201`):**

  | Rol | DL-2 anahtarları | DL-2 test kişisi olarak |
  |---|---|---|
  | Sözleşme görüntüleyici | `mdm.contracts.read`, `.documents.read` (belge deposu izni yok — D56/2) | indirme 200 + tek `document-content-read` niyeti (AC-62); kapsam dışı / başka kiracı 404, niyet yok, MOD-0262 çağrısı yok (AC-27) |
  | Sözleşme yöneticisi | `.documents.create`, `.documents.delete`, `.amendments.create`, `.update`, `.value.read`, `.value.write` | belge ekleme / geri çekme / reconcile, CreateAmendment (değerli → `value-written` niyeti), Withdraw sürüm |
  | Sözleşme imza kaydedici (**D47/5**) | `.documents.read`, `.documents.create` (imzalı kopya) | DL-2'de yalnız `SignedCopy` ekleme yolu (AC-28 — `Approved` sürüm fikstürle kurulur; imza kaydı DL-3) |
  | İzin listesiyle kurulan kişi (matriste hazır rol yok) | `amendments.create` var, `value.write` yok | AC-65 CreateAmendment kolu: değerli istek 403 `MDM_CONTRACT_VALUE_WRITE_REQUIRED`, sürüm / niyet yok; değersiz istek geçer |
  | İzin listesiyle kurulan kişi | `read` var, `documents.read` yok | AC-27 izinsiz 403, niyet yok |

- **D47/3:** belge indirme denetimi v1'de — içerik akışı başlamadan önce `mdm.contract-version.document-content-read` niyeti, yazılamazsa
  503 ve MOD-0262'ye çağrı yok (AC-62). **D47/5** imza kaydedici ayrılığı (AC-64) DL-3. **D50** MOD-0217'yi etkilemez.
- **Denetim:** `AuditCategory.MasterData` (D45/1); DL-2 olayları (Audited Events `:931-944`): `document-added`, `document-withdrawn`,
  `document-binding-abandoned` (çit yolu, aynı atomik yazımda), `contract-version.created` (`Kind = Amendment`), `value-written`,
  `contract-version.withdrawn`, `document-content-read`. Dosya adı ve içerik niyete yazılmaz. K2 değil (K2 = onay sonucu, imza, fesih — DL-3).

## 3. DL-2'de yeni yazılacaklar (hepsi Recovery'de sıfırdan)

Yollar pack "Backend File Convention" (`:708-742`), "Recovery ekleri" (`:270-291`), "Repo Scope" (`:437-474`) ve DL-2 satırı.

- `Features/Contract/Commands/{AddContractDocumentCommand,WithdrawContractDocumentCommand,ReconcileContractDocumentCommand,CreateContractAmendmentCommand,WithdrawContractVersionCommand}.cs`,
  `Queries/GetContractDocumentContentQuery.cs`, handler'ları, `Validators/CreateContractAmendmentValidator.cs`; `ContractModels.cs`'e
  `ContractDocumentDto`, `ContractAmendmentRequest` (DL-1 dosyasına ekleme).
- `Application` portu `IDocumentBinaryStorePort` (sakla, meta oku, akış oku, telafi) + `Infrastructure/DocumentRepository/DocumentBinaryStoreClient.cs`
  — `Gateway:BaseUrl`, kullanıcının `Authorization` + `X-Tenant-Id` başlıkları (`GatewayReferenceValueReader` deseni,
  `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/ReferenceData/GatewayReferenceValueReader.cs:30,34-49@ec063fee9`); yükleme
  **akışla** (istek bedeni ara belleğe tümüyle alınmaz, `:598`; §5 bulgu 4).
- Uzak çağrı sırası ve sınıflar (4a) kesin geri alma → telafi tam bir kez; (4b) sonuç bilinmiyor → aynı oturumla commit yeniden denemesi,
  çoğunluk okuması, çit yazımı (`Version = V+1`, `AbandonedContentIds += contentId` + niyet aynı işlemde); (4c) okuma yok → 503
  `MDM_CONTRACT_DOCUMENT_BINDING_UNKNOWN`, telafi yok (pack `:572-598`).
- `ContractsController` DL-2 aksiyonları: `POST …/{id}/versions/{versionNo}/documents` (multipart), `…/documents/{contentId}/withdraw`,
  `…/documents/{contentId}/reconcile`, `GET …/documents/{contentId}/content`, `POST …/{id}/amendments`, `POST …/{id}/versions/{versionNo}/withdraw`;
  her aksiyonda `[HasPermission]`.
- Denetim izi ekleri (yalnız ekleme): DL-2 `MasterDataAuditOperation` değerleri, defter satırları.
- Hata sözlüğü DL-2 alt kümesi (pack `:415-436`): `MDM_CONTRACT_DOCUMENT_NOT_FOUND`, `MDM_CONTRACT_DOCUMENT_STORE_UNAVAILABLE`,
  `MDM_CONTRACT_DOCUMENT_REJECTED` (400), `MDM_CONTRACT_DOCUMENT_TOO_LARGE` (413), `MDM_CONTRACT_DOCUMENT_IN_USE` (409; imza DL-3'te dolar),
  `MDM_CONTRACT_DOCUMENT_BINDING_UNKNOWN` (503), `MDM_CONTRACT_VALUE_WRITE_REQUIRED` (403), `MDM_CONTRACT_STATE_INVALID`,
  `MDM_CONTRACT_IMMUTABLE_FIELD`, `MDM_CONTRACT_AUDIT_UNAVAILABLE`; ileti resx anahtarı.

## 4. DL-2 iş paketi (WP) taslağının iskeleti

Bu bölüm prompt değildir. Biçim örneği `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL8-v1.0.md`. Hedef yol (öneri):
`execution/domains/master-data-management/work-packs/prompts/WP-0217-DL2-v1.0.md`. Kuyrukta `WP-0217-DL2` satırı **yok**.

**Başlık satırları**

| Alan | Değer (taslak) | DL-2 dağıtım günü doldurulacak |
|---|---|---|
| Ajan satırı | `@[.antigravity/agents/backend-architect.md]` — yeni servis yok (MdmService, D38/1) | — |
| WP / Prompt | `WP-0217-DL2` · `TRM-WP-0217-DL2-P v1.0` | kuyruk satırı |
| Repository · Branch · Expected HEAD · Worktree | `feature/mdm/wp-0217-dl2` — **öneri — D51/3 / D52/1 deseni, sahip teyidi gerekir** (D52/1 yalnız `wp-0217-dl1`'i adlandırdı) | Expected HEAD = DL-1 kabulünün main'deki commit'i (S-53); MOD-0289 DL-2 aynı dalgadaysa ortak MDM denetim dosyaları için sıra (DL-1 kaydı §2.5); worktree yolu |
| Risk | HIGH — §9.1: kiracı, veri kapsamı, dış bağımlılık (MOD-0262), denetim, telafi / veri kaybı, yetki | — |
| Görev sınıfı · Paket | ürün · pack yolu + `ready-for-dev` sürümünün sha256'sı (`pack_approval.py check` çıkış 0) | sha |
| Kod alma | **yok (D38/3)** — WP raporunda "kaynaktan dosya alınmadı" beyanı; okunan referanslar `path@da4abb20 + sha256` | — |
| Kapsanan dilim | **yalnız DL-2** (onay, imza kaydı, yenileme, fesih, ekran, Gateway **yok**) | — |

**DL-2 Bitiş AC'leri** (`MOD-0217-contract-lifecycle-management-clm.md:1798-1802` · paket @ `4d944a3d…79ac`): **AC-5 (belge ve değişiklik uçları kısmı)**, **AC-6 (belge ve değişiklik uçları
kısmı)**, **AC-7 (DL-2 yazmaları kısmı)**, **AC-10 (DL-2 komutları kısmı)**, **AC-11 (DL-2 yazmaları kısmı)**, **AC-12 (DL-2 defter
satırları kısmı)**, **AC-14 (DL-2 yazmaları kısmı)**, **AC-15 (DL-2 uçları kısmı)**, **AC-16 (DL-2 aksiyonları kısmı)**, **AC-17
(belge ve değişiklik alanları kısmı)**, **AC-21 (DL-2 yazmaları kısmı)**, **AC-22 (DL-2 yazmaları kısmı)**, AC-24, AC-25, AC-26,
AC-27, AC-28, AC-29, AC-30, AC-31, AC-39, **AC-42 (belge ve değişiklik eylemleri kısmı)**, AC-49, AC-50, AC-58, AC-62, **AC-65
(CreateAmendment kısmı)**. Kesişen AC kuralı (`:1769-1772`): "kısmı" işaretli AC'ler DL-3'te tam kapanır; **AC-65 DL-2'de tam
kapanır** (onu listeleyen son dilim). AC metinleri pack'ten tırnakla alıntılanır (AC-24 `:1251` … AC-65 `:1438`, paket @ `4d944a3d…79ac`).

**Dilim kabul kapısı — DL-2** (`:1829` · paket @ `4d944a3d…79ac`): Bitiş AC'leri (kısımlarıyla); AC-26, AC-31, AC-49, AC-50 ve AC-58 gerçek replica set'te (pack D56
revizyonu — §5 bulgu 3 kapandı); MOD-0262-FU01'e karşı dev ortamı kaydı (servis kimliğiyle — D56/2; yükle, indir, başka kiracı 404, kesin geri almada telafi); AC-29 mimari testi
(MOD-0028 çağrısı yok, sabotajlı — D40); build.

**İzinli yollar** (pack Repo Scope DL-2 satırları): `services/Diten.MdmService/src/Diten.MdmService.Application/Features/Contract/**`
(yalnız §3 dosyaları + `ContractModels.cs` eki), `Domain/Entities/ContractVersion.cs` (yalnız `Documents`, `AbandonedContentIds` eki —
§5 bulgu 5), `Persistence/Repositories/{Contract,ContractVersion}Repository.cs` ve `ContractAggregateStore.cs` (ekleme),
`Infrastructure/DocumentRepository/**`, `Api/Controllers/ContractsController.cs` (DL-2 aksiyonları), `ContractLifecycleManifestProvider.cs`
(belge / değişiklik anahtarları), denetim izi eklemeleri (yalnız ekleme), `services/Diten.MdmService/tests/**`, `tests/architecture/audit-ledger/Diten.MdmService.md`.

**Korunan yollar** (`:475-489`): `.antigravity/**`, `AGENTS.md`, `gateway/**`, `services/Diten.Platform/**` (MOD-0262-FU01 dahil — eklemeli
`ContentStorageScope` değeri gerekirse ayrı onay, D9 / XMC-001), `services/Diten.ProcurementService/**`, `services/Diten.AuthService/**`,
`services/Diten.Building.Blocks/**`; MdmService'te Repo Scope dışı her şey (MOD-0220 yalnız okunur, MOD-0290, MOD-0289 banka yolları,
`Infrastructure/Authorization/**`); `Infrastructure/Workflow/**` (DL-3); `Infrastructure/DocumentRecords/**` ve her MOD-0028 istemcisi
(yasak — AC-29); `frontend/**` (DL-4; özellikle MOD-0144'ün `Controllers/ContractsController.cs`); `docs/analysis/contracts/**`;
`execution/**`; kaynak depo.

**Önce oku:** pack (`ready-for-dev` sürümü), AGENTS.md, MDM domain-config, `.antigravity/rules/` multi-tenancy, security-jwt,
permission-key-standard, data-scope-enforcement, audit-trail-standard, mongo-indexing, handler-design, code-style, git-safety,
cross-module-change-consent; MOD-0262-FU01 pack; karar kayıtları D37, D40, D41 (özellikle /3, /9, /10), D45–D69 (özellikle D56/1–2, D59/1, D69); TRM-11 cevabı; DL-1
hazırlık kaydı ve DL-1 WP raporu; bu kayıt.

**Sözleşme yüzeyleri:** API (§3); tüketilen MOD-0262-FU01 `/api/v1/document-repository/objects` (sahte istemci: 201, 400, 413, 500, zaman
aşımı, telafi başarısız, meta sahiplik alanları var / yok — pack `:1224-1225`; canlı kanıt ayrı), SEC-002 (sahte). Sabotajlar: AC-26
(telafi çağrısı kaldırılınca), AC-27 (kapsam denetimi kaldırılınca), AC-29 (MOD-0028 çağrısı eklenince), AC-31 (`ux_mdm_contract_version_no`
/ `OpenVersionNo` CAS kaldırılınca), AC-49 ((4b) okuması atlanınca), AC-50 (Reconcile sahiplik / okuma denetimi kaldırılınca), AC-58
(çit atlanınca; niyet işlem dışına alınınca), AC-62 (niyet kaldırılınca / akıştan sonraya taşınınca), AC-65 (`value.write` kontrolü
kaldırılınca ya da `value.read`'e çevrilince; `value-written` niyeti kaldırılınca).

**Gerekli testler** (pack "Test Expectations" `:1206-1242`): birim (çit yazımı karar tablosu AC-58, MOD-0262 yanıt sınıflandırması,
doğrulayıcılar, durum makinesi — `Approved`'a yalnız `SignedCopy`, `Effective` / `Superseded` 409); **gerçek replica set** (kendi URI
değişkeni, sınıf başına veritabanı, `enableTestCommands` — `MdmTestMongo` deseni): AC-26, AC-31, AC-49, AC-50, AC-58 ayrı dikiş durumları
(yazımı uygulamadan düşürme / uygulayıp yanıtı düşürme / okumayı düşürme / gecikmiş commit / niyet dikişi); mimari: MdmService'te dosya
sistemi yazımı yok (AC-24), MOD-0028 portu / istemcisi yok (AC-29), `[HasPermission]`, `AuditTrailStandard`; build 0 hata; yerel kapıda
`scripts/` Python testleri.

**AGENT CONTRACT (SOP §17.4 + kart):**
- İşçi önce `Read`: `.antigravity/agents/backend-architect.md`, yukarıdaki `.antigravity/rules/` dosyaları.
- `backend-architect` alanları **doldurulmuş**: hedef servis + proje yolu; entity + DL-2 alanları (`ContractVersion.Documents`,
  `AbandonedContentIds`, `Kind = Amendment`); komut / sorgu listesi (§3); validator kuralları (pack "Validation Rules" `:775-810` belge ve
  değişiklik satırları); izin anahtarları (§2.6); `Response<T>` + `CustomBaseController`.
- Yazma kapsamı denetimi: araçlar İzinli yolları karşılamazsa `BLOCKED — agent/scope mismatch`; MOD-0262 / Platform değişikliği gerekirse
  dur ve bildir (XMC-001).

**Kart kuralları:** **S-63** — "push ve PR yok; commit sonrası dur ve bildir" (dal adı, HEAD sha, test sonucu); S-52, S-54, S-83 (HIGH),
S-53 (Expected HEAD DL-1 kabulünden sonra), dalga sonunda tek PR (S-76, S-77). Rapor biçimi SOP §22. Kodu kimin yazacağı (Ali — Y-2, TBD).

**Açık sorular (prompt'tan önce CT / sahip):** (1) genel `platform.document-repository.read` / `.manage` anahtarlarının kiracı geneli
etkisi (§5 bulgu 2) — kabul mü, kapsamlı anahtar mı; (2) MOD-0262'ye yükleme MdmService'te nasıl akışla iletilecek (`IFormFile`
kullanılmadan — §5 bulgu 4); (3) kabul kapısındaki replica set listesinin genişletilmesi (§5 bulgu 3). **Üçü de kapandı:** (1) KARAR D56/2 (servis
kimliği; kullanıcıya belge deposu izni yok); (2) pack AC-24 (`IFormFile` / `[FromForm]` yok, `MultipartReader`); (3) pack DL-2 kabul
kapısı AC-26, AC-50, AC-58'i sayıyor.

## 5. Bu kaydın sınırları ve pack'te bulunan tutarsızlıklar

- Kaynak depo yalnız okundu (`git ls-tree`, `git archive`); kaynaktan kod alınmaz (D38/3). Testler **çalıştırılmadı**; Recovery'de yalnız
  dosya okuma ve `grep` (@ec063fee9). MOD-0262-FU01 canlı davranışı ölçülmedi (kayıt yok).
- SEC-002 Platform tarafının çözümleme anlamı okunmadı (yalnız izin listesi) — **kısmi inceleme**.
- Bu kayıt WP prompt'u değildir; prompt kapısına girmez, dağıtım izni vermez.

**Pack'te bulunan tutarsızlıklar (pack değişmedi; `ready-for-dev` revizyonunda CT işi — düzeltme yapılmadı):**

1. **SEC-002 paket kapısı maddesi ölçümle kapanamaz:** checklist `:1292` ve Dependencies `:501` "ölçülmedi" diyor; ölçüm sağlayıcının
   iki tarafta `product-item-sku-master`'a sabitlendiğini gösteriyor (§1). Pack Platform değişikliğini, MDM istemci değişikliğini
   (`Infrastructure/Authorization/**` Repo Scope'ta yok) ve MOD-0217'nin **modül kodunu** yazmıyor. MOD-0289 DL-2 kaydı §5 bulgu 1 ile aynı. **Kapandı — KARAR D56/1** (MOD-0018 Treasury Auth
   eki DL-4).
2. **D46a'nın genel MOD-0262 anahtarları kapsam ve indirme denetimini atlatabilir:** genel `platform.document-repository.read` kiracıdaki
   **her** nesneyi (her `scope`), `.manage` her nesneyi yazma / telafi (silme) yetkisi verir (`services/Diten.Platform/src/Diten.Platform.Application/Features/DocumentRepository/Services/DocumentRepositoryAccessPolicy.cs:25-39@ec063fee9`);
   Gateway rotası açık (`ocelot.json:1857-1885`). Sözleşme görüntüleyici aynı token'la `GET /api/v1/document-repository/objects/{contentId}/content`'i
   doğrudan çağırırsa MOD-0217'nin kapsam denetimi (AC-27, SEC-002) ve indirme niyeti (AC-62, D47/3) devreye girmez; ayrıca başka modüllerin
   `Documents` nesneleri okunabilir. Pack (Authorization Convention `:894-895`, AC-27, AC-62) yalnız MDM yolunu tanımlıyor. Sahip kararı
   gerekir (ör. MOD-0262'de CRM visit-evidence deseninde kapsama bağlı anahtar — Platform değişikliği; ya da kabul edilmiş risk kaydı). **Kapandı — KARAR D56/2** (servis kimliği; kullanıcılara
   `platform.document-repository.*` verilmez).
3. **Kabul kapısının gerçek replica set listesi dar:** `:1551` yalnız AC-31 ve AC-49'u sayıyor; AC-58 metni "gerçek replica set'te"
   (`:1114`), AC-26 / AC-50 işlem ve dikiş davranışı ister; Test Expectations entegrasyon listesi (`:1213-1215`) AC-58'i saymıyor. **Kapandı** (pack D56 revizyonu).
4. **Akışla yükleme kuralının MDM tarafı testsiz:** pack "istek bedeni ara belleğe tümüyle alınmaz" (`:598`) ve AC-24 yalnız
   `Path.GetTempPath` / `File.Create` kullanımını mimari testle yasaklıyor; ASP.NET `IFormFile` bağlaması büyük dosyayı çerçeve içinde geçici
   dosyaya yazar ve bu testle yakalanmaz. Kural "MdmService'te `IFormFile` / `[FromForm]` dosya bağlaması yok, `MultipartReader` ile akış"
   gibi ölçülebilir bir AC koşuluna çevrilmeli (Platform ucu `IFormFile` kullanıyor — `DocumentRepositoryController.cs:63-64`; o tarafın işi). **Kapandı** (pack D56
   revizyonu: AC-24).
5. **Repo Scope dilim sütunu:** `Domain/Entities/{Contract,ContractVersion}.cs` ve depolar yalnız "(DL-1)" (`:441-446`); DL-2
   `ContractVersion.Documents` / `AbandonedContentIds` (`:378-379`) ve koşullu işaretçi / çit yazımı ister. Alanlar DL-1'de tanımlanacaksa
   prompt bunu söylemeli; değilse sütun "DL-1 / DL-2" olmalı. `ContractDocumentRef` tipinin dosyası adlandırılmamış. **Kapandı** (pack D56
   revizyonu: "DL-1 / DL-2" sütunu, `ContractDocumentRef.cs`).
6. **Test Expectations eski cümlesi:** `:1223` "Mongo `failCommand` … bu oturumda ölçülmedi" — MDM test kümesi `enableTestCommands=1`
   ile başlıyor (`MdmTestMongo.cs:156`). **Kapandı** (pack D56 revizyonu).
7. **MOD-0262-FU01 pack'inde iç çelişki (başka domain — yalnız not):** `status_note` "Runtime implementation not started" (`MOD-0262-FU01-document-binary-store.md:11`)
   ↔ `runtime_implementation: in-progress` (`:20`) ve kod mevcut. MOD-0217 pack Dependencies (`:496`) ikincisini anıyor.
8. **DL-1 kaydı eskidi (pack değil):** `MOD-0217-DL1-hazirlik.md` §1 CT-APR-02 satırı `78657a26…`'yı son inceleme sayıyor; bugün
   `reviews.tsv:94` güncel sha için UYGUN. Aynı kaydın SEC-002 "kısmi inceleme" satırı bu kayıttaki ölçümle olumsuz kapandı. **Kapandı**
   (DL-1 kaydı 2026-10-28'de tazelendi).
