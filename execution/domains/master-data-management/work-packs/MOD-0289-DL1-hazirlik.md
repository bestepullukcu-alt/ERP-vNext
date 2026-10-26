---
record_id: MOD-0289-DL1-HAZIRLIK-01
status: hazırlık — dağıtım izni değil, WP prompt'u değil, kod alma izni değil
prepared_at_local: 2026-10-26
prepared_by: Treasury Intake Control Tower
pack: execution/domains/master-data-management/module-packs/MOD-0289-bank-master-bank-account-registry.md — revizyon 8 (status draft, approval_status not-approved, sha256 0746eeae…d213, yerel dal docs/ct/treasury-mod0289-d33 @bebe8fa73; henüz incelenmedi, main'de değil)
source: /Users/natig/Projects/ERP-vNext @ da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d (salt okunur; MOD-0289 için kod alma izni YOK — D27/5 kapsamaz)
---

# MOD-0289 DL-1 hazırlığı — banka ana verisi, denetim izi genişletmesi ve paket iskeleti

Bu kayıt, MOD-0289 `ready-for-dev` olduğunda DL-1 WP prompt'unun hızla yazılabilmesi için kaynağın **neyi gösterdiğini**,
**neyin alınmayacağını** ve DL-1'in **neyi yeniden yazacağını** önceden ölçer. Kod yazdırmaz; paketi değiştirmez. Prompt,
paketin `ready-for-dev` sürümüne ve o günkü Expected HEAD'e göre ayrıca yazılır, bağımsız incelenir (kart S-52…S-54) ve
prompt kapısından geçer.

> **Kod alma izni yok — bu kaydın her sınıflandırması buna bağlıdır.** Sahip kararı D27/5 kod alma iznini yalnız dört
> Treasury modülüne (MOD-0126/0119/0120/0124) verdi; MOD-0289'u kapsamaz (pack satır 85-88, 1562-1563, 1686). Pack
> künyesi: kod ancak `approved` / `ready-for-dev`, CT-APR-02 + CT-APR-01 ve sahibin MOD-0289 için **ayrıca** verdiği kod alma
> izninden sonra yazılır (pack satır 31-35). Bu yüzden aşağıda hiçbir satır **al-uyarla** değildir: kaynak dosyalar en çok
> **yalnız referans**tır (davranış okunur, kod alınmaz). Ayrı izin gelirse sınıflandırma yeniden yapılır ve her alınan dosya
> `path@da4abb20 + sha256` ile iz kaydına yazılır (pack satır 1607-1609).

## 1. Başlama koşulları (bugün açık olanlar)

DL-1 prompt'u ancak bunlar kapanınca dağıtılabilir (pack "Ready-for-dev Checklist" satır 1558-1588, "Teslim dilimleri" DL-1
önkoşulu satır 1808-1809):

| Koşul | Bugün | Kimden | Dayanak |
|---|---|---|---|
| **MOD-0289 için ayrı kod alma izni (D1)** | **verilmedi** (D27/5 kapsamaz) | sahip | pack satır 1562-1563, 1809 |
| Paket künyesi `ready-for-dev` + o sürüme CT-APR-02 ve CT-APR-01 (`high: evet`, yazmamış onaylayıcı) | `draft` / `not-approved`; revizyon 8 **incelenmedi** | CT + Beste | pack satır 19-20, 1559-1561; D34 (aşağıda) |
| DCP (`UNASSIGNED-finance-treasury-intake`) `approved`, TRM-S3 dilimi tanımlı | `UNASSIGNED`, taslak | Beste | pack satır 580, 1564 |
| MDM domain-config'e MOD-0289 satırı + frontend/gateway kapsam cümlesi | yok (domain-config'te `MOD-0289` geçmiyor — bu oturumda `grep -c` → 0) | MDM sahibi | pack satır 135-139, 1565 |
| RCS-001 ortak kod servisi (BL-565) — `BNK` kodu için | **yok**; sahibi belirlenmedi | sahip ataması | pack satır 569, 593 ("DL-1 önkoşulu"), 1571 |
| `BNK` / `BAC` önekleri RCS-001 §3'te | yazıldı | — | pack satır 542, 1571; `.antigravity/rules/record-coding-standard.md:68-69` (bu worktree) |
| Denetim defteri kararı (yeni K2 satırları, `mdm-guvenilir-kaynak-niyeti` belirteci, borç yok) | **verildi — D27/11**; CT-DECISIONS satırı kod aşamasında yazılır | — | pack satır 524-527, 1572 |
| Platform `AuditCategory`'de banka / finans değeri | yok; "MOD-0126 O-6 ile ortak karar — değer uydurulmaz" | Platform sahibi | pack satır 1175-1176, 1778 |
| MOD-0018 / Auth explicit-grant (`mdm.bank-accounts.read-unmasked`, `read-tenant-wide`) | açık | Beste (Auth sahibi, D9) | pack satır 1583 — **DL-2 için**, DL-1'i bekletmez |
| MOD-0048 para birimi tüketim yolu | ölçülmedi | Beste | pack satır 1584 — **DL-2 için** |
| `supplier.openapi.yaml` 1.3.0 main'de | yok (bugün 1.2.0) | Cihan (MVP-2) | pack satır 567, 1585 — **DL-3 için** |
| Güvenlik onayı (O-6) | **verildi — D33/1**; gerçek Mongo testleri ve AC-50 DL-2 kabul kapısında | — | pack satır 93-97, 1576-1578 — DL-1'e etkisi yok |

**Dayanak kararlar:** D33/1–4 pack revizyon 8'de uygulandı (pack satır 1693-1700; karar kaydı
`docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md` D33, main'e PR #235 ile girer — pack satır 96-97).
**D34** pack'te geçmiyor (bu oturumda `grep` → 0); MOD-0126 DL-1 hazırlık kaydındaki özetine göre (`…/finance-treasury/work-packs/MOD-0126-DL1-hazirlik.md`
satır 33-36): taslak paketler `ready-for-dev` olunca tek seferde onaya gelir, DCP numarası önce ve ayrıca istenir. MOD-0289'un
bu tura hangi sürümle gireceği pack'te yok.

**Üretici beklemeleri (DL-1 dışı, prompt'a "bu dilimde yok" olarak yazılır):** `supplier.openapi.yaml` 1.3.0 (Cihan) → DL-3;
MOD-0018 explicit-grant `mdm.bank-accounts.read-unmasked` (Beste) → DL-2; MOD-0048 (Beste) → DL-2. DL-1'i doğrudan bekleten
üretici yalnız **RCS-001 BL-565**'tir (sahibi belirlenmedi).

## 2. Kaynak dosyaların sınıflandırması (DL-1)

Kaynak MOD-0289 kodu `services/Diten.Platform/` altındadır (kısaltma: `P/` = `services/Diten.Platform/src/`); MDM'ye
taşınmaz, kurallar MDM düzenine yeniden yazılır (pack satır 1607-1609, 1619-1620). Özet: `shasum -a 256` (ilk 16 hane),
satır sayısı `wc -l`, ikisi de @da4abb20, bu oturumda `git archive` + `tar -xf` ile ölçüldü. Pack kendi notunda kaynak kodu
okumadığını söyler (pack satır 60, 1603); aşağıdaki satır atıfları bu oturumda **okunarak** doğrulandı (fark varsa Not
sütununda). Karar dili: **yalnız referans** (davranışı okunur, kod alınmaz — kod alma izni olmadığı için en üst sınıf budur),
**alma** (do-not-carry maddesiyle; "madde N" = pack "Taşınmayacaklar" satır 1704-1727), **sonraki dilim**.

### 2.1 Domain

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `P/Diten.Platform.Domain/Entities/Mod0289/BankMasterEntities.cs` | `3ee3c4f67a756ecf` · 721 | yalnız referans (yalnız `BankMasterDraft` :8-20, `BankBranchDraft` :23-38) | `Domain/Entities/{Bank,BankBranch}.cs : EntityBase` | pack satır 150-151; taban `TenantScopedEntity` **alma** (madde 4); Guid'in BSON string saklanması (:10, :25, :28 ve hesapta :52, :55; durum enum'u da string, :18) **alma** (madde 12); `BankCode` kullanıcı alanı **alma** (madde 11); hesap ve diğer 23 sınıf DL-2 / v1 dışı / alma (pack satır 152-165) |
| `P/Diten.Platform.Domain/Entities/Mod0289/BankMasterEnums.cs` | `51f1254aed62f22b` · 213 | yalnız referans (yalnız `BankMasterLifecycleStatus` :3-8) | `Domain/Enums/BankLifecycleStatus.cs` | değerler Draft=1/Active=2/Inactive=3 korunur (pack satır 218-219); diğer durum boyutları (:10-40) **alma** (madde 15) |
| `P/Diten.Platform.Domain/Repositories/Mod0289/IMod0289BankRepository.cs` | `5af68246cabf3a41` · 210 | yalnız referans (:108-117, :208) | `Domain/Repositories/I{Bank,BankBranch}Repository.cs` | `GetByBankCodeAsync` (:111) Recovery'de yok — kod RCS'den gelir; sürüm süzgeçli güncelleme yöntemleri yeni (kaynakta Update yok) |
| `services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/EntityVariations.cs` | `9a333d562385bce7` · 18 | **alma** | — | `TenantScopedEntity` (:8-11) — madde 4; pack atfı `EntityVariations.cs:10` doğrulandı |

### 2.2 Application

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `P/…Application/Features/Mod0289/Services/Mod0289BankService.cs` | `44617d01fb258eab` · **5353** | yalnız referans (yalnız :152-322 banka / şube, :3644-3750 doğrulayıcılar) | `Features/BankMaster/{Commands,Queries,Handlers,Validators}/**` (pack satır 927-936) | kurallar handler + domain'e bölünerek **yeniden yazılır** (pack satır 970-971). Kaynak farkları (pack kazanır): BIC 6..11 alfanümerik (:3675-3681) ↔ pack ISO 9362 8/11 + ülke eşleşmesi (pack satır 1031); şube `City` ≤ 120 (:3746-3747) ↔ pack ≤ 100 (pack satır 281); ülke kodu MOD-0048 referans kümesine karşı (`EnsureActiveReferenceValueAsync("COUNTRY_CODE")` :186, :274) ↔ pack yalnız ISO biçimi (D24/5, pack satır 261, 1030) — **alma**; banka ve şube oluşturmada `TenantId = Guid.Empty` (:209, :293) **alma** (pack satır 607-609, madde 1); liste N+1 şube sayımı (:164-169) alınmaz; serbest İngilizce iletiler alınmaz (resx anahtarı, pack satır 502) |
| aynı dosya, diğer bölümler | — | sonraki dilim / alma | — | hesap, token, iş akışı, istisna, içe aktarma, outbox (:2446-2470, `TenantId = Guid.Empty` :2450 doğrulandı) — DL-2 / v1 dışı / madde 1, 7, 8 |
| `P/…Features/Mod0289/Models/Mod0289BankModels.cs` | `f189b970caed2d0b` · 888 | yalnız referans (:5-78 banka / şube modelleri) | `Features/BankMaster/BankModels.cs` (tek dosya; pack satır 204-205) | DTO'larda `TenantId` yok; `CreatedBy` string (:41, :78) **alma** (madde 12) → Guid `sub` |
| `P/…Features/Mod0289/Mod0289Permissions.cs` | `7731c1e0f1869cbc` · 43 | **alma** | `mdm.banks.*` (pack satır 1130-1134) | `MOD0289_BANK_*` / `MOD0289_BRANCH_*` literalleri (:5-8) — madde 10 |
| `P/…Features/Mod0289/{Commands,Contracts,Handlers,Queries,Validators}/**` (F02 hesap oluşturma işlemi, 7 dosya), `Models/Mod0289AccountCreatePilotOptions.cs`, `Services/Mod0289AccountCreate{Fingerprint,Participant}.cs` | ölçüldü, tabloya alınmadı (DL-1 dışı) | **alma** | — | F02 / PAIR — madde 3 |
| `P/…Features/Mod0289/Services/Mod0289PaymentReferenceHandoffService.cs` | `8fe015e359f0e688` · 445 | **alma** | — | token / ödeme referansı aktarma — madde 7 |
| `P/…Features/Mod0289/Services/Mod0289WorkflowGateway.cs` | `27647ef035142372` · 440 | sonraki dilim değil — v1 dışı | — | MOD-0023 akışı v1'de yok (D23/2, pack satır 1774) |

### 2.3 Persistence

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `P/…Infrastructure/Persistence/Repositories/Mod0289/Mod0289BankRepository.cs` | `9e61cbcd4b3c5a97` · 2228 | yalnız referans (:75-196 banka / şube, :1927-1934) | `Persistence/Repositories/{Bank,BankBranch}Repository.cs` | MDM `RepositoryBase` + `TenantFilter` deseni (pack satır 244-250); taban `UpdateAsync` değil, `Version == expected` süzgeçli koşullu güncelleme (pack satır 251-253, 613-614); her yazma niyetiyle aynı koşullu yazım / işlem, `MasterDataAtomicWrite` (pack satır 972-975). Kaynakta **indeks kurulumu yok** (bu oturumda `grep CreateOne\|Indexes` → 0) — BIC / kod tekilliği yalnız ön sorgu + `DuplicateKey` yakalama (servis :189-220); arama süzgeci kullanıcı girdisini kaçışsız regex yapar (:83, :149) — Recovery kaçışlı ya da sabit önekli arama yazar (pack'te kural yok — prompt'ta açık soru). `mod0140_suppliers` okuması (:51) **alma** (madde 2); `mod0048_reference_data_*` doğrudan okuması (:71-72) **alma** (pack'te madde yok; gerekçe D24/5 + başka modül koleksiyonu); `mod0289_*` koleksiyon adları **alma** (madde 10) → `mdm_banks`, `mdm_bank_branches` |
| `services/Diten.Platform.Common/src/Diten.Platform.Common/Persistence/Repositories.cs` | `9d35765f0be11430` · 110 | **alma** | — | yansımayla kiracı damgalama (:81-90, pack atfı doğrulandı) — madde 1 |
| `P/…Repositories/Mod0289/Mod0289AccountCreateStore.cs` | `68cde5a947e43897` · 89 | **alma** | — | ayrı sahiplik bağı / F02 — madde 3, 6. Pack atfı `:75` bu oturumda açılmadı (ölçülmedi) |
| — | — | **yeni** | MDM indeks kurulumu (depo kurucusunda `EnsureIndexes`, `MongoIndexReadiness.cs` — pack satır 471-473) | kaynakta karşılığı yok; aşağıdaki DL-1 tekil indeksleri |

**DL-1 veritabanı tekil indeksleri (pack "Mongo indeksleri" satır 454-459; Validation Rules satır 1028-1035) — açıkça:**

| İndeks | Anahtar | Tekil · kısmi süzgeç | Test + sabotaj |
|---|---|---|---|
| `ux_mdm_bank_code` | TenantId + Code | **Unique, kısmi filtre YOK** — silinen bankanın kodu da dolu sayılır (RCS-001 §6; pack satır 456, 1028) | aynı kiracıda aynı kodla ikinci kayıt — silinmiş bankanın koduyla dahil — reddedilir; sabotaj: indekse `IsDeleted=false` süzgeci eklenince silinmiş-kod vakası kırmızıya döner. Kodun RCS'den gelmesi DB tekilliğini kaldırmaz; ikisi birlikte |
| `ux_mdm_bank_bic` | TenantId + Bic | Unique, kısmi: `Bic` var **ve** `IsDeleted = false` (pack satır 457) | AC-2: iki eşzamanlı aynı-BIC oluşturmadan biri 409 `MDM_BANK_BIC_CONFLICT`; indeks kaldırılınca kırmızı (pack satır 1241-1243) |
| `ux_mdm_bank_national_code` | TenantId + CountryCode + NationalBankCode | Unique, kısmi: `NationalBankCode` var **ve** `IsDeleted = false` (pack satır 458) | aynı ülke + ulusal kod 409 `MDM_BANK_NATIONAL_CODE_CONFLICT` (AC-2) |
| `ux_mdm_bank_branch_code` | TenantId + BankId + BranchCode | Unique, kısmi `IsDeleted = false` (pack satır 459) | aynı şube kodu 409 `MDM_BANK_BRANCH_CODE_CONFLICT` (AC-2) |

Hesap, amaç, çit ve okuma kaydı indeksleri (pack satır 460-469) DL-2'dedir. Kaynağın kod çakışması kontrolü (servis :189-195,
`IsDeleted=false` süzgeçli ön sorgu, repo :118-127) **alınmaz**: kod kullanıcıdan gelmez ve tekillik silinmişleri de kapsar.

### 2.4 Api, Infrastructure ve frontend

| Kaynak | Özet · satır | Karar | Recovery hedefi | Not |
|---|---|---|---|---|
| `P/Diten.Platform.API/Controllers/Mod0289Controller.cs` | `7ba4e18ed40c2d45` · 2451 | yalnız referans (:72-238 banka / şube uçları) | `Api/Controllers/{BanksController,BankBranchesController}.cs` | `CustomBaseController`, `Response<T>`, her aksiyonda `[HasPermission]` (pack satır 967-969, 1116-1118); eylem süzgeciyle kimlik koruması (:43-62), denetleyicide izin kontrolü (:246 vb.), istisna atan `ResolveActorId` (:2292-2298), Platform denetim alıcısına bağlı ileti (:2333) **alma** (madde 4) — atıflar doğrulandı; şube detayında `BranchRead` **veya** `BankRead` (:225) davranışı Recovery'de tek anahtar `mdm.banks.read`; rota `/api/v1/mod0289` (:21) **alma** (madde 10) → `/api/mdm/banks`, `/api/mdm/bank-branches` (pack satır 172-175, 194-195) |
| `P/Diten.Platform.API/Models/Mod0289/Mod0289Contracts.cs` | `f440007a804b6ad5` · 467 | yalnız referans (:5-33) | `BankWriteRequest`, `BankBranchWriteRequest` (`BankModels.cs`) | `CreateBankDraftRequest.BankCode` (:7-8) **alma** (madde 11) — istekte kod 400 `MDM_BANK_CODE_FORBIDDEN` |
| `P/Diten.Platform.API/Controllers/Mod0289AccountTransactionsController.cs` | `9bd2ec1eb631d604` · 88 | **alma** | — | F02 — madde 3 |
| `P/Diten.Platform.API/Services/Mod0289/Mod0289RequestIdentity.cs`, `Mod0289AccountCreateIdentityGuard.cs` | `cb9b88d93cbe2461` · 128; `73a6ad7d3f4d94bb` · 47 | **alma** | MDM JWT / `sub` | madde 4 (Platform kimlik koruması) |
| `P/Diten.Platform.API/Services/Mod0289/Mod0289SeedBaselineWorker.cs`, `P/…Infrastructure/Services/Mod0289/Mod0289SeedBaselineService.cs` | `5a30d5d64d7f540d` · 69; `e45cb2daabbb6dba` · 172 | **alma** | — | madde 9 |
| `P/…Infrastructure/Services/Mod0289/Mod0289AccountCreateEligibilityUnavailable.cs` | `b71e970d3f1ed9ea` · 14 | **alma** | — | madde 3 |
| `frontend/Diten.Web/Controllers/Platform/BankMasterRegistryController.cs`, `Views/Platform/BankMasterRegistry/Index.cshtml` | `d1c1ad00381e85c1` · 58; `7ee9b57eb133d81c` · 313 | sonraki dilim (DL-4) | `Views/MasterData/Banks/**` | `_LayoutBackbone` / hardcoded metin **alma** (madde 14); bu oturumda içerik okunmadı |

### 2.5 Testler (yalnız referans — davranış listesi olarak okunur, Recovery testleri yeniden yazılır)

| Kaynak | Özet · satır | DL-1'de karşılığı |
|---|---|---|
| `services/Diten.Platform/tests/Diten.Platform.API.Tests/Mod0289ContractBootstrapTests.cs` | `324b62a7563857d6` · 4075 (79 `public async Task` test) | yalnız :27-367 banka / şube vakaları (oluştur-oku, şube bilinmeyen bankaya 404, zorunlu alan 400, kod / BIC / şube kodu çakışması) → AC-1, 2, 3, 4, 5 (banka ve şube kısmı); kod-çakışması testi (:318) RCS'li modelde **anlamını yitirir** |
| `…/Mod0289RequestIdentityTests.cs`, `…/Mod0289RequestIdentityHttpTests.cs` | `57e513bba79dbfc4` · 269; `2fa7357c457f2b7b` · 215 | alma (madde 4) — Recovery'de AC-8 mimari testi |
| `…/Mod0289SeedBaselineServiceTests.cs` | `b13daf37d56d6d7f` · 83 | alma (madde 9) |
| `…/Mod0289AccountCreateTransactionHttpTests.cs`, `…Infrastructure.Tests/Mod0289AccountCreateTransactionMongoTests.cs`, `…/Mod0289LegalEntityAccountAggregateMongoTests.cs` | `5bdba821d3ecf134` · 151; `559c192224667f97` · 379; `a747245a73092dc9` · 111 | alma (madde 3) — güvence olarak alıntılanmaz (pack satır 1517-1519) |
| `…/Mod0289PaymentReferenceHandoffServiceTests.cs` | `b80fd06622039e14` · 433 | alma (madde 7) |
| `frontend/Diten.Web/tests/mod0289-bank-master-registry-ui.test.js` | `2576e9228d51fd0b` · 427 | DL-4 |

Devralınmış kaynak kanıtı (R2 API 104 geçti / 72 kaldı, R2S 1 / 45 — pack satır 1604-1606) Recovery hazırlığının ölçüsü
değildir. Kaynak testler bu oturumda **çalıştırılmadı**.

## 3. Kaynakta olmayan, DL-1'de yeni yazılacaklar

Yollar pack "Teslim dilimleri" DL-1 satırı (pack satır 1805-1807) ve "Repo Scope" (pack satır 508-527); hepsi
`services/Diten.MdmService/` altında, yeni servis ve port yok (MDM 5059, D20/1 — pack satır 579, 603).

- `Domain/Entities/{Bank,BankBranch}.cs` (`EntityBase`, `IAuditIntentAggregate`; alanlar pack satır 259-283 — `Code` RCS,
  `NationalBankCode`, `DomesticKeyFenceVersion`, `DomesticKeyIncludesBranch`, `Bic`, `CreatedBy`/`UpdatedBy` Guid?),
  `Domain/Enums/BankLifecycleStatus.cs`; `Domain/Repositories/I{Bank,BankBranch}Repository.cs`.
- `Features/BankMaster/**` Compact düzeni: 11 komut, 4 sorgu + `BankListSort`, 5 doğrulayıcı, `BankModels.cs` (pack satır
  927-936). **Kaynakta karşılığı olmayan** komutlar: Update / Activate / Deactivate / Delete / BulkDelete (banka) ve Update /
  Activate / Deactivate / Delete (şube) — pack satır 194-195 (kaynakta `Inactive` atayan kod yok).
- `Persistence/Repositories/{Bank,BankBranch}Repository.cs` + §2.3'teki dört tekil indeks; `If-Match` / `Version` CAS
  (pack satır 613-614, AC-9).
- `Api/Controllers/{BanksController,BankBranchesController}.cs`, `Api/ModuleRegistration/BankMasterManifestProvider.cs`
  (`BrandProductMasterManifestProvider.cs` deseni — bu worktree'de var, ölçüldü) (pack satır 517-518).
- **RCS kod portu** (`IRecordCodePort`, `Infrastructure/RecordCodes/**` — pack satır 209-212, 514-516): `BNK`, grup öneki;
  servis erişilemezse 503 `MDM_BANK_RECORD_CODE_UNAVAILABLE` (pack satır 485). Ortak servis (BL-565) yokken port hedefsizdir —
  başlama koşulu §1.
- **Denetim izi genişletmesi (yalnız ekleme):** `Domain/Enums/AuditAggregateType.cs` (`Bank`, `BankBranch` — DL-1'in
  ihtiyacı; pack dört değeri birlikte sayar), eklemeli genel `MasterDataAuditOperation` (D24/2), `AuditIntentDeliveryRepository.cs`
  koleksiyon listesi (bugün sabit dokuz koleksiyon) (pack satır 519-522, 1167-1170). Olaylar: banka ve şube satırları
  `mdm.bank.*`, `mdm.bank-branch.*` (pack satır 1183-1193); K2 değil ama aynı kapalı-başarısız yoldan (pack satır 1178-1179).
- `tests/architecture/audit-ledger/Diten.MdmService.md` yeni K2 satırları + aynı değişiklikte `CT-DECISIONS.md` satırı ve
  `AuditTrailStandardTests` sabitleri, borç satırı yok (D27/11; pack satır 524-527; AC-7).
- İzin anahtarları `mdm.banks.read|create|update|activate|deactivate|delete|bulk-delete` (pack satır 1130-1134) — manifest
  sağlayıcısında; MOD-0018 rol şablonu ayrımı pack'te DL-1 için yazılı değil (ölçülmedi).
- Hata sözlüğü DL-1 alt kümesi (pack satır 477-486, 490-491): `MDM_BANK_NOT_FOUND`, `MDM_BANK_BRANCH_NOT_FOUND`,
  `MDM_BANK_VALIDATION_FAILED`, `MDM_BANK_CODE_FORBIDDEN`, `MDM_BANK_BIC_CONFLICT`, `MDM_BANK_NATIONAL_CODE_CONFLICT`,
  `MDM_BANK_BRANCH_CODE_CONFLICT`, `MDM_BANK_VERSION_CONFLICT`, `MDM_BANK_STATE_INVALID`, `MDM_BANK_INACTIVE`,
  `MDM_BANK_RECORD_CODE_UNAVAILABLE`, `MDM_BANK_AUDIT_UNAVAILABLE`, `MDM_BANK_DOMESTIC_KEY_LOCKED`; ileti resx anahtarı.
- **DL-1 bitiş AC'leri** (pack satır 1809-1810): AC-1, 2, 3 (banka ve şube kısmı), 4 (banka kısmı), 5 (banka ve şube kısmı),
  6 (banka kısmı), 7, 8 (banka kısmı), 9 (banka kısmı) — metinler pack satır 1239-1261. **Dilim kabul kapısı** (pack satır
  1836): bu AC'ler + AC-2 ve AC-9'un gerçek replica set yarış testleri + `AuditTrailStandard` (AC-7) + `Diten.MdmService`
  build 0 hata + yerel kapıda `scripts/` Python testleri.

## 4. WP prompt iskeleti (doldurulacak alanlar)

Biçim: main'deki örnek `execution/domains/pharmacovigilance/work-packs/prompts/WP-0230-DL2B-v1.3.md` (bu worktree'de var).
Hazır olunca `execution/domains/master-data-management/work-packs/prompts/WP-0289-DL1-v1.0.md` olarak yazılır (yol öneridir;
pack'te yok).

| Alan | Değer (taslak) | Açık |
|---|---|---|
| Ajan satırı | `@[.antigravity/agents/backend-architect.md]` (+ §17.4 giriş alanları) | — (yeni servis yok; `/add-module` gerekmez) |
| WP / Prompt | `WP-0289-DL1` · prompt kimliği | WP / prompt kimliği ve kuyruk satırı pack'te yok — TRM-16 kuyruğundan (pack satır 48-49) atanır |
| Repository / Branch / Expected HEAD / Worktree | `feature/mdm/mod-0289-bank-master-bank-account-registry` (pack frontmatter satır 22) · Expected HEAD = paketin `ready-for-dev` onayının main'deki commit'i | dağıtım günü; "MdmService commit SHA'sı DL-1 başında" (pack satır 599) |
| Risk | HIGH — sinyaller: kiracı, denetim, kayıt kodu, sözleşme (pack CT-APR-01 "yüksek risk", satır 1560-1561) | — |
| Görev sınıfı · Paket | ürün · MOD-0289 pack (`ready-for-dev` sürümü) | — |
| Kod alma | **yok** — kaynak yalnız referans; ayrı izin gelirse §2 yeniden sınıflandırılır | sahip kararı |
| İzinli yollar | pack "Repo Scope" DL-1 satırları (§3) | `Infrastructure/RecordCodes/**` BL-565'in biçimine bağlı |
| Korunan yollar | pack "Protected Paths" (satır 545-560) + `.antigravity/**`, `ocelot.json`, MOD-0220 `LegalEntity` (yalnız okunur), Platform / Auth / Procurement servisleri | — |
| Kapsanan dilim | DL-1 (hesap, hassas veri, amaçlar, çit, ekranlar, gateway **yok**) | — |
| Önce oku | pack, AGENTS.md, MDM domain-config, multi-tenancy, security-jwt, permission-key-standard, git-safety, cross-module-change-consent, code-style, audit-trail-standard, record-coding-standard, mongo-indexing, handler-design, D1/D20/D23/D24/D27/D33 karar kayıtları, bu hazırlık kaydı | — |
| Sözleşme yüzeyleri | API (`/api/mdm/banks`, `/api/mdm/bank-branches` — sözleşme dosyası pack'te DL-1 için yok), İzin (`mdm.banks.*`), Test (AC-1…AC-9 banka/şube kısmı), Sabotaj (AC-2 indeks, AC-3 `TenantFilter`, AC-6 niyet yazımı + teslim listesi, AC-9 `Version` süzgeci, `ux_mdm_bank_code` kısmi süzgeç) | AC metinleri pack satır numaralarıyla alıntılanır |
| Keşif | `verify_module_id.py --discover "bank master"` raporu, yol@commit + sha256 | dağıtım günü koşulur |
| Uzunluk | ~10 bin token'ı aşarsa işçiye dosya yolu + SHA-256 ile verilir (S-52) | — |

## 5. Bu kaydın sınırları

- Kaynak depo yalnız okundu (`git ls-tree`, `git archive`, `tar -xf` kendi geçici klasörüne); kaynak düzeltmesi yok.
- Sınıflandırma okuma ve paket metniyle yapıldı; kaynak testler ve Recovery testleri **çalıştırılmadı**; Recovery'de hiçbir
  ölçüm koşulmadı (yalnız dosya varlığı ve `grep`).
- Paket revizyon 8 **incelenmemiştir** (CT-APR-02 yok); revizyon değişirse satır atıfları yeniden doğrulanır.
- Bu kayıt WP prompt'u değildir; prompt kapısına girmez, dağıtım ya da **kod alma** izni vermez.

### Pack'te bulunan çelişkiler (CT'ye; pack değişikliği yeni inceleme ister)

1. **Kaynak servis boyu:** pack satır 970 `Mod0289BankService` "~2500 satır" der; ölçüm **5353** satır (@da4abb20).
2. **`Guid.Empty` kapsamı:** pack yalnız outbox satırını (`:2450`) atfeder (satır 158, 608, 1705); ölçüm aynı dosyada 34
   `TenantId = Guid.Empty` ataması, banka (:209) ve şube (:293) oluşturma dahil. Kural (hiçbir belge `Guid.Empty` ile yazılmaz,
   satır 607-609) zaten genel; atıf eksik.
3. **AC-27 dilimi:** bağımlılık kapanış tablosu MDM denetim niyeti satırında AC-27'yi kanıt sayıp dilimi **DL-1** yazar (satır
  599); AC-27 DL-3 bitiş ve kabul listesindedir (satır 1824, 1838) ve hesap + maskesiz okuma gerektirir (satır 1325-1327).
4. **RCS §3 satırı:** Repo Scope (satır 542) ve checklist (satır 1571) "yazıldı, D30" der; O-7 satırı (satır 1780) ve Protected
  Paths (satır 547) hâlâ "ayrı onay" der. `.antigravity/rules/record-coding-standard.md:68-69` satırları kaynak olarak "D29/2"
   gösterir — atıf uyumu ölçülmedi.
5. **Gateway rotaları:** Gateway kararı (satır 1225-1227) `bank-account-purposes` rotasını saymaz; amaç uçları (satır 199) ve
   denetleyici (satır 538) DL-2'dedir (DL-1'e etkisi yok, DL-2/DL-4 için not).
