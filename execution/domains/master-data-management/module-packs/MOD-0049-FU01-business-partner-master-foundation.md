---
id: MOD-0049-FU01
name: Business Partner Master Foundation
domain: master-data-management
service: Diten.MdmService
service_port: 5059
shell: tenant
golden_reference: compact
data_mode: server
screens:
  - module: BusinessPartners
    data_mode: server
entity_base: EntityBase
status: ready-for-dev
approval_status: pending
revision: "r3 (2026-11-13) — sahip kararı C-5 (docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:28-30): rol ver / rol kaldır komutları, uçları ve mdm.business-partners.assign-role izni v1'den çıktı; rol oluşturmada verilir; son rol kuralı (Q-5) alan kuralı olarak kaldı (AC-26 birim vakası, S-26a/S-26b); ikinci rolle iki komut eklemeli gelir. Komut 6 → 4, uç 10 → 8, izin 7 → 6; MDM_BP_ROLE_ALREADY_ASSIGNED / MDM_BP_ROLE_NOT_ASSIGNED / MDM_BP_LAST_ROLE uç hata tablosundan çıktı; AC-5, AC-6, AC-7, AC-10, AC-11, AC-20, AC-25, AC-26 yeniden yazıldı (kimlik silinmedi). BUSINESS-PARTNER-READ ve MOD-0179 eşlemesi değişmedi. Künye ready-for-dev / pending değişmedi. r2 (2026-11-13, commit 7e89a2214) — sahip kararları işlendi (docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:14-21): Q-1 = BPT ve .antigravity kapısı kapandı (commit 63f046c92: RCS-001 §3 BPT satırı, RecordCodeTypeCatalogTests RuleAheadOfCatalogue BPT); Q-2 = A; Q-3 90 gün kuralı yok, kalıcı silme, zamanlanmış temizlik yok; Q-4 vergi no kiracıda tekil, listede maskeli, ayrıntıda yetkiliye açık; Q-5 Golden Compact sapmaları paketteki gibi, son rol kaldırılamaz (önce pasifleştirme) — AC-26 + S-26a/S-26b; Q-6 pasif kayıt salt okunur; Q-7 domain-config satırı CT yazar (metin önerisi §19.8). Kararlar §26'dan §19.7'ye taşındı. Künye ready-for-dev (çağıran CT talimatı, S-49); approval_status pending. r1 (2026-11-13, commit 22139776c) — ilk taslak. Girdiler: sahip kararları docs/records/decisions/2026-11/mod0179-3pl-owner-decisions-01.md (T-5 izni, Q-10 kimlik kararı, r1 inceleme F-5 notu); MOD-0179 paketi r3 (onaylı tüketici) §4.10, DL-4, AC-34 (b), AC-35…AC-38; MOD-0179 r1 (commit 241659850) §4.10 alan tasarımı ve X-1…X-3; MOD-0179 r1 Q-11 (kayıt kodu öneki) bu pakete taşındı."
status_note: "ready-for-dev r3 — approval_status pending; kod izni yok. Sahip kararları Q-1…Q-7 ve C-5 verildi (§19.7); .antigravity kapısı kapandı (63f046c92). Onaya kalan: domain-config satırı (X-11, CT yazar — metin §19.8) → bağımsız inceleme (CT-APR-02) → onay satırı (CT-APR-01, onaylayan Ali / GitHub GMG-ERP; yazan ≠ onaylayan). Ali bilgi notları A-1…A-5 ve CT soruları C-1…C-4 açık (§26). MOD-0179 DL-4 kapısı = bu paketin DL-1 kabulü + Gateway rotası (X-8) main'de + BUSINESS-PARTNER-READ 1.0.0 FROZEN."
owner: master-data-management / merkez (Beste) — geliştirme merkezde (T-5)
approver: Ali (GitHub GMG-ERP) — MDM sahibi
risk: "HIGH — (1) yeni kiracı ana verisi ve Mongo şeması (yeni koleksiyonlar, tekil index'ler); (2) yeni izin ad kaynağı mdm.business-partners.* ve manifest (RBAC); (3) kişisel veri (iletişim kişileri, şahıs şirketinde vergi no = kimlik no); (4) Gateway rotası (integration-agent); (5) Platform kayıt kodu kataloğu ve FROZEN RECORD-CODE-ALLOCATE sözleşmesine eklemeli sürüm + .antigravity RCS-001 satırı (korunan yol); (6) MOD-0179 için HARD bağımlılık (DL-4 kapısı) — sözleşme hatası tüketiciyi bozar. CT-APR-01: ilk onay + izin + kiracı + şema + gateway + korunan yol = yüksek risk."
branch: feature/sce/mod-0173-inventory-ledger-valuation
started: 2026-11-13
target: "sahip tarihi verilmedi"
form_field_count: 10
dependencies: [MOD-0049 (Blueprint ebeveyni — paket yok, GAP-039), MOD-0040-FU02 Record Code Service (BPT kodu — Group kapsamı), RECORD-CODE-ALLOCATE 1.0.0 (eklemeli 1.1.0 — X-7), MOD-0289 / MOD-0217 (MDM'de tek IRecordCodeAllocator portu — X-3), MOD-0018 (RBAC, manifest, plan senkronu), Gateway (integration-agent — X-8), Platform ülke listesi /api/lookups/reference/countries (yalnız ekran), DEV-0001 (Golden Compact); tüketici MOD-0179 (DL-4)]
---

# MOD-0049-FU01 — Business Partner Master Foundation (İş Ortağı Ana Verisi — Temel)

> **ready-for-dev (r3, 2026-11-13).** `status: ready-for-dev` (çağıran CT talimatı, S-49), `approval_status: pending`. **Paket onaylanmadan geliştirme
> başlamaz:** kod yalnız künye + bağımsız inceleme kaydı (CT-APR-02) + onay satırı (CT-APR-01, **onaylayan Ali / GitHub `GMG-ERP`**) ile yazılır (AGENTS.md
> Çekirdek madde 7, §10). Bu sürüm kod, commit, push, PR ya da onay izni **değildir**.
>
> **r3 revizyon notu.** Sahip kararı C-5 (`docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:28-30`): rol ver / rol kaldır komutları, uçları ve `mdm.business-partners.assign-role` izni
> **v1'den çıktı**; rol oluşturmada verilir; "son rol kaldırılamaz" (Q-5) alan kuralı olarak kalır (AC-26); ikinci rol eklenince iki komut eklemeli gelir.
> Sayılar: komut 6 → 4, uç 10 → 8, izin 7 → 6. Değişen yerler: §1, §2.1, §3, §4.3, §4.4, §4.5, §5, §7, §8.2, §8.5, §10, §11, §12, §13, §14, AC-5, AC-6, AC-7, AC-10,
> AC-11, AC-20, AC-25, AC-26, §17.1, §18, §19.2, §19.7, §21, §23, §25, §26. `BUSINESS-PARTNER-READ` (§8.4) ve MOD-0179 eşlemesi **değişmedi**.
>
> **r2 revizyon notu.** Sahip kararları Q-1…Q-7 verildi ve §19.7'ye yazıldı (kaynak `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:14-21`);
> `.antigravity` kapısı kapandı (commit `63f046c92`: `.antigravity/rules/record-coding-standard.md:76` `BPT` satırı ve
> `services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/RecordCodeTypeCatalogTests.cs:34` `["BPT"] = "MOD-0049-FU01 DL-1"`). Değişen yerler:
> §4.1, §4.3, §4.5, §4.6, §6, §8.2, §8.4, §8.5, §8.7, §10, §11, §12, §13, X-6, X-11, X-12, AC-6, AC-7, AC-8, AC-11, AC-12, AC-20, AC-22, AC-24, yeni AC-26, §18, §19.7,
> §19.8, §21, §22, §24, §26. **Onaydan önce kalan kapı:** domain-config satırı (X-11 — CT yazar; metin önerisi §19.8). Ali'ye giden maddeler (A-1…A-5)
> **bilgi** niteliğindedir; T-5 izni verilmiştir.

## 0. Kimlik ön kontrolü ve keşif

**DCP-002 ön kontrolü (fail-closed) — bu oturumda koşuldu (2026-11-13, depo HEAD `b3e9e6b7a`):**

| Komut | Sonuç |
|---|---|
| `PYTHONUTF8=1 python .antigravity/scripts/verify_module_id.py . --check-id MOD-0049-FU01 --name "Business Partner Master Foundation" --parent MOD-0049` | çıkış **0** — `OK  MOD-0049-FU01: proven against Blueprint/registry.` |
| `… --discover "business partner"` | çıkış 3 ("inceleme gerekli"); `ÖZET bulgular: blueprint 0 · sor 0 · registry 1 · paket 0 · gateway 0 · görev 0`; tek bulgu registry `MOD-0049-FU01` (bu paketin kimliği); rapor sha256 `edb15b18548813b9a1624926734f1962d20c9d4aebc8da396c21e5e562d981c9`; 572 ref |
| `… --discover "partner"` | çıkış 3; blueprint 1 · sor 2 · registry 3 (aşağıdaki karar tablosu); rapor sha256 `3a83ffb49677795f10ecc14a830fd032f080b47b7917f9426b0d3628b88c355f` |
| `… --discover "party master"`, `… --discover "customer master"` | çıkış 0; bulgu yok |

**Keşif bulgularının her birine karar:**

| Bulgu | Karar |
|---|---|
| Registry `MOD-0049-FU01` planned (`execution/registries/module-id-registry.md:161`) | **Bu paketin kimliği.** Ebeveyn satırı `:160` (MOD-0049, Blueprint 8.8 `Blueprint_Data` satır 50, SoR "golden entities (e.g., customer/product/supplier), match rules, survivorship policies") |
| Blueprint `MOD-0282 Partner & Alliance Management` (CRM-aligned Partner, Alliance, Referral) | **Mükerrer değil.** İş geliştirme ortaklığı (ittifak, yönlendirme) yeteneğidir; taraf ana verisi değildir. MOD-0282 ileride iş ortağını bu kayıttan okuyabilir (BL-665 kapsamında) |
| SoR `Partner profiles` → MOD-0036 EDI Hub | **Mükerrer değil.** EDI ortak profili (kanal / kimlik / mesaj seti) entegrasyon yapılandırmasıdır; taraf kaydı değildir. MOD-0179'un kanal kaydı da aynı ayrımla bu paketin dışındadır |
| SoR `co-mfg partner records` → MOD-0197 Co-Manufacturer Management | **Mükerrer değil, gelecekte rol adayı.** Fason üretici ilişki kaydı MOD-0197'nindir; taraf kimliği ileride bu kayıttan rol olarak gelebilir (v1 dışı, §4.5 genişleme noktası) |
| Registry `MOD-0021-FU-Partner` Partner Admin Audit Scope | **İlgisiz.** Platform "partner admin" aktör türünün denetim kapsamıdır |

**Kaynak karar (bağlayıcı):** `docs/records/decisions/2026-11/mod0179-3pl-owner-decisions-01.md` — T-5 (`:13`; izin `:32`: "MDM tarafındaki değişikliği merkez geliştirir
(XMC-001 izni MDM sahibinden alındı) … MDM'de yalnız eklemeli değişiklik"), Q-10 (`:48-54`: MOD-0049-FU01; kapsam dar: taraf kaydı ve roller, v1 rolü
`ThirdPartyLogistics`; eşleştirme / birleştirme, tedarikçi ve müşteri SoR'u kapsam dışı; MOD-0179 köprüdür, iş ortağını yalnız okur), F-5 (`:44-45`: kişisel veri
kuralı MDM iş ortağı `Contacts` için ayrıca değerlendirilir — §4.6). Aynı firmanın tedarikçi (`SUP-`), 3PL ve CRM müşterisi kayıtlarının bağlanması
`docs/roadmap/backlog/product-backlog.md:10965` BL-665 (ertelendi).

## 1. Module Summary

MDM'de **iş ortağı (taraf) ana verisinin** ilk, dar dilimi. Kiracı geneli bir `BusinessPartner` kaydı açar: değişmez kayıt kodu, ticari ad, kısa ad,
vergi / kimlik numarası, adres, iletişim kişileri, **roller** ve yaşam döngüsü (etkin / pasif). v1'in tek rolü **`ThirdPartyLogistics`** (3PL ortağı);
rol kümesi genişlemeye açık tasarlanır ama v1'de başka rol yoktur.

Tüketici: **MOD-0179 3PL Integration** (onaylı, r3). MOD-0179 iş ortağını **yalnız okur** — seçim listesi "3PL rollü etkin ortaklar", ayrıntıda ad /
adres / iletişim salt okunur (MOD-0179 `execution/domains/supply-chain-execution/module-packs/MOD-0179-3pl-integration.md:360-366`, AC-34 (b) `:841`, DL-4 `:844-848`).
Bu paketin **DL-1'i MOD-0179 DL-4'ün kapısıdır.**

Hedef kullanıcı: kiracının ana veri sorumlusu (oluşturur, düzenler, pasifleştirir) ve okuma izni olan her modül kullanıcısı (3PL operatörü).

Kapasite özeti: 4 yazma komutu, 4 sorgu, 8 servis ucu, 1 Compact ekran (liste + oluştur + düzenle + ayrıntı), 1 okuma sözleşmesi
(`BUSINESS-PARTNER-READ` 1.0.0), 1 kayıt kodu türü (`BPT` — Q-1), 6 izin anahtarı.

## 2. Ownership and Boundaries

### 2.1 Kapsam (v1)
- `BusinessPartner` kaydı: oluştur, güncelle, pasifleştir, etkinleştir; kod RCS-001 ortak servisinden (OTOMATİK, grup önekli).
- Roller: oluşturmada verilir; v1 rol kümesi `{ ThirdPartyLogistics }`; v1'de rol değiştiren komut / uç **yok** (C-5); son rol kuralı alan kuralıdır (AC-26).
- İletişim kişileri (0..10) ve adres — kişisel veri kuralıyla (§4.6).
- Okuma API'si: liste (rol + durum + arama süzgeci, sayfalı), tekil, geçmiş, sunucu dışa aktarımı.
- `BUSINESS-PARTNER-READ` 1.0.0 okuma sözleşmesi (taslak DL-1'in ilk adımı; DL-1 kabulünde CT dondurur).
- MDM kiracı ekranı `Master Data → İş Ortakları` (Golden Compact, 7 dil, `_LayoutTenantShell`).
- Merkezi denetim (yol b) + kaydın kendi yalnız-ekleme geçmişi.

### 2.2 Kapsam dışı (v1)
- **Eşleştirme / birleştirme, survivorship, "altın kayıt" kuralları** — Blueprint MOD-0049'un ileri FU'su (BL-665).
- **Tedarikçi SoR'u** (MOD-0140, `SUP-` kodu) ve **müşteri SoR'u** (CRM MOD-0149 Account) — bu kayıtlar burada açılmaz, buraya taşınmaz, bağlanmaz (BL-665).
- `ThirdPartyLogistics` dışında rol (tedarikçi, müşteri, fason üretici, taşıyıcı …) — her biri ayrı karar + paket (§4.5).
- Silme ve toplu silme (yalnız pasifleştirme; `IsDeleted` teknik alan olarak durur, v1 komutları onu set etmez — Brand emsali).
- Tüzel kişilik (LegalEntity) bazında görünürlük / kapsam (kayıt kiracı genelidir — §8.6); şirket bazlı ortak ilişkisi.
- Banka hesabı (MOD-0289'un `Supplier` sahipli hesap modeli ayrıdır), sözleşme (MOD-0217), kredi limiti, ödeme koşulu, vergi dairesi kodlaması.
- 3PL'e özgü her şey (kanal, eşleme, eşik, depo bağlantısı) — MOD-0179.
- İçe aktarma (import), toplu güncelleme, değişiklik olayı yayını (outbox / event) — takip adayları (§20).
- İş ortağı için yeni Platform lookup anahtarı (yok; §8.1 ülke listesi mevcut uçtan).

### 2.3 Sınır kuralları
| Konu | Sahibi | Bu paket |
|---|---|---|
| İş ortağı kimliği, kod, ad, adres, iletişim, vergi no, roller, durum | **MOD-0049-FU01 (MDM)** | yazar ve okutur |
| 3PL bağlantısı (ortak ↔ depo, kanal, eşik) | MOD-0179 (InventoryService) | yazmaz; MOD-0179 `BusinessPartnerId` + `PartnerCode` saklar, gerisini okur |
| Tedarikçi (`SUP-`), müşteri (CRM Account) | MOD-0140, MOD-0149 | dokunmaz |
| Kayıt kodu sayacı | MOD-0040-FU02 (Platform) | yalnız ister (`IRecordCodeAllocator`) |
| İzin değerlendirmesi, rol şablonu | MOD-0018 / Auth | manifest bildirir; şablona yazmaz (A-2) |
| Gateway rotası | integration-agent | ister (X-8) |

## 3. Owned Objects

- **Varlıklar:** `BusinessPartner` (kök), `BusinessPartnerContact` (gömülü değer nesnesi), `BusinessPartnerRoleAssignment` (gömülü), `BusinessPartnerHistoryEntry`
  (ayrı koleksiyon, yalnız ekleme).
- **Enum'lar:** `BusinessPartnerRole` (`ThirdPartyLogistics`), `BusinessPartnerStatus` (`Active`, `Inactive`), `BusinessPartnerContactType` (`Integration`,
  `Operations`, `Finance`, `Other`).
- **Depolar:** `IBusinessPartnerRepository` (okuma + koşullu yazma), `IBusinessPartnerHistoryRepository` (**yalnız** `AppendAsync` ve okuma; güncelleme / silme üyesi yok).
- **Komutlar (4 — hepsi yazma, hepsi §21'de):** `CreateBusinessPartnerCommand`, `UpdateBusinessPartnerCommand`, `DeactivateBusinessPartnerCommand`,
  `ActivateBusinessPartnerCommand`. **v1 dışı (C-5):** `AssignBusinessPartnerRoleCommand`, `RemoveBusinessPartnerRoleCommand` — ikinci rolle eklemeli gelir.
- **Alan kuralı:** `BusinessPartnerRoleRules` (Domain, saf) — `ValidateRoleSet` (≥1, tekrarsız, tanımlı; oluşturmada kullanılır) ve `CanRemove` (son rol →
  `LastRole` sonucu); v1'de `CanRemove`'u çağıran komut yoktur, kural birim testiyle korunur (AC-26).
- **Sorgular (4):** `GetBusinessPartnerListQuery`, `GetBusinessPartnerByIdQuery`, `GetBusinessPartnerHistoryQuery`, `GetBusinessPartnerExportQuery`.
- **DTO'lar (`BusinessPartnerModels.cs`):** `BusinessPartnerWriteRequest`, `BusinessPartnerListItemDto`, `BusinessPartnerDetailDto`, `BusinessPartnerContactDto`,
  `BusinessPartnerAddressDto`, `BusinessPartnerRoleDto`, `BusinessPartnerHistoryItemDto`, `BusinessPartnerStatusChangeRequest`,
  `BusinessPartnerAudit` (denetim kimliği sabitleri), `BusinessPartnerErrorCodes`.
- **API uçları (8)** — taban `/api/mdm/business-partners`:

| # | Yöntem + yol | İzin | Komut / sorgu |
|---|---|---|---|
| 1 | `GET /api/mdm/business-partners` | `mdm.business-partners.read` | `GetBusinessPartnerListQuery` |
| 2 | `GET /api/mdm/business-partners/{id}` | `mdm.business-partners.read` | `GetBusinessPartnerByIdQuery` |
| 3 | `GET /api/mdm/business-partners/{id}/history` | `mdm.business-partners.read` | `GetBusinessPartnerHistoryQuery` |
| 4 | `GET /api/mdm/business-partners/export?format=csv\|xlsx&columns=…` | `mdm.business-partners.export` | `GetBusinessPartnerExportQuery` |
| 5 | `POST /api/mdm/business-partners` | `mdm.business-partners.create` | `CreateBusinessPartnerCommand` |
| 6 | `PUT /api/mdm/business-partners/{id}` | `mdm.business-partners.update` | `UpdateBusinessPartnerCommand` |
| 7 | `POST /api/mdm/business-partners/{id}/deactivate` | `mdm.business-partners.deactivate` | `DeactivateBusinessPartnerCommand` |
| 8 | `POST /api/mdm/business-partners/{id}/activate` | `mdm.business-partners.activate` | `ActivateBusinessPartnerCommand` |

  v1 dışı (C-5): `…/{id}/roles` ve `…/{id}/roles/{role}/remove` uçları ve `mdm.business-partners.assign-role` izni yoktur.

  `DELETE` yöntemi **yoktur** — controller'da, komut katmanında ve Gateway'de (Brand emsali: `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/BrandsController.cs:22-24`
  sınıf düzeyinde yalnız `[Authorize]` + `[Route]`; izin her eylemde).
- **Frontend rotası:** `/MasterData/BusinessPartners` (Index), `/MasterData/BusinessPartners/Create`, `/MasterData/BusinessPartners/Edit/{id}`,
  `/MasterData/BusinessPartners/Details/{id}`; same-origin vekil `/MasterData/BusinessPartners/api/…`.
- **Manifest:** `BusinessPartnerMasterManifestProvider` — ModuleCode `business-partner-master`, sayfa `BUSINESSPARTNERS` (§14).
- **İzinler (7):** §14.
- **Sözleşme:** `BUSINESS-PARTNER-READ` 1.0.0 (§8.4).

## 4. Entity Fields

`BusinessPartner` MdmService `EntityBase`'inden türer: `Id`, `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt`, `Version` (int)
(`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/EntityBase.cs:3-12`). MDM `EntityBase` **`LegalEntityId` taşımaz** — iş ortağı **kiracı geneli**
kayıttır (MOD-0179 r1 ölçümü, `241659850:execution/domains/supply-chain-execution/module-packs/MOD-0179-3pl-integration.md:350-352`). `TenantId` sunucuda kiracı
bağlamından çözülür; istek gövdesinde **alan olarak yoktur** — gönderilse de bağlanmaz ve etkisizdir (MDM Brand emsali, `BrandsController.cs` "TenantId is absent from
BrandWriteRequest by design"). Zamanlar UTC. Ekranda GUID gösterilmez.

### 4.1 BusinessPartner
Koleksiyon `mdm_business_partners`.

| Alan | Tip | Zorunlu | Kural |
|---|---|---|---|
| `Code` | string | sistem | RCS-001 `{GRUP}-BPT-{000001}` (Q-1 = `BPT`); OTOMATİK; oluşturmada bir kez; **değişmez**; yazma isteğinde alan yok |
| `LegalName` | string | Evet | trim, 2..200 (ticari unvan) |
| `ShortName` | string | Evet | trim, 2..60; **kiracıda tekil** (büyük/küçük harf ve Türkçe karakter duyarsız normalleştirilmiş `ShortNameNormalized` ile); seçim listesinde görünen ad (`displayName`) |
| `ShortNameNormalized`, `LegalNameNormalized` | string | sistem | arama ve tekillik için (ASCII büyük harf, boşluk sıkıştırma) |
| `TaxNumber` | string \| null | Hayır | trim, 1..30, `[A-Za-z0-9-]`; **kişisel veri olabilir** (şahıs şirketinde kimlik no); `(TenantId, CountryCode, TaxNumberNormalized)` tekil; listede maskeli, ayrıntıda yetkiliye açık (Q-4) |
| `TaxNumberNormalized` | string \| null | sistem | boşluk / tire atılmış büyük harf |
| `Address` | nesne | Evet | `CountryCode` (ISO-3166 alpha-2, `^[A-Z]{2}$`, zorunlu), `City` (1..100, zorunlu), `District` (≤100), `AddressLine` (≤300), `PostalCode` (≤20) |
| `Contacts` | `BusinessPartnerContact[]` | Hayır | 0..10 (§4.2) |
| `Roles` | `BusinessPartnerRoleAssignment[]` | **her zaman ≥1** | §4.3; aynı rol iki kez yok; son rol kaldırılamaz — ortak pasifleştirilir (Q-5) |
| `Status` | `BusinessPartnerStatus` | sistem | `Active` (oluşturmada) \| `Inactive` (§8.2) |
| `StatusReason`, `StatusChangedAt`, `StatusChangedBy` | string / UTC / string | sistem | gerekçe 10..500 |
| `CreatedBy`, `UpdatedBy` | string | sistem | JWT `sub` — yalnız denetim kimliği, yetki girdisi değil (Brand emsali) |
| `Version` | int | sistem | her yazmada +1; tüm yazma istekleri `ExpectedVersion` taşır (oluşturma hariç) |

### 4.2 BusinessPartnerContact
Gömülü değer nesnesi.

| Alan | Tip | Kural |
|---|---|---|
| `ContactId` | uuid | sunucu verir; düzenlemede eşleştirme anahtarı |
| `Type` | `BusinessPartnerContactType` | `Integration` \| `Operations` \| `Finance` \| `Other` |
| `Name` | string | 2..120 |
| `Email` | string \| null | ≤256, e-posta biçimi, küçük harfe normalleştirilir |
| `Phone` | string \| null | ≤32, `^\+?[0-9 ()-]{5,32}$` |
| — | — | `Email` ya da `Phone`'dan en az biri dolu |

Kimlik numarası, doğum tarihi, kişisel adres, fotoğraf **alanı yoktur** (veri en aza indirme — §4.6).

### 4.3 BusinessPartnerRoleAssignment
Gömülü.

| Alan | Tip | Kural |
|---|---|---|
| `Role` | `BusinessPartnerRole` | v1 kümesi `{ ThirdPartyLogistics }`; string olarak saklanır |
| `AssignedAt`, `AssignedBy` | UTC / string | sunucu |

v1'de roller yalnız oluşturmada yazılır; güncelleme isteğinde `Roles` alanı yoktur (C-5). **Değişmez kural (Q-5, alan kuralı):** kayıtta her an en az bir rol
vardır; `BusinessPartnerRoleRules.CanRemove` son rol için `LastRole` döner — bir ortağın kullanımını bitirmenin yolu pasifleştirmedir (§8.2). Rol kaldırma komutu
ikinci rolle geldiğinde bu kuralı kullanır ve sonucu o zaman bir uç hata koduna eşler (o rolün paketi).

### 4.4 BusinessPartnerHistoryEntry
Koleksiyon `mdm_business_partner_history`, yalnız ekleme.
`BusinessPartnerId`, `EventType` (`created` \| `updated` \| `deactivated` \| `activated`; `role-assigned` / `role-removed` rol komutlarıyla eklemeli gelir — C-5), `Actor`, `OccurredAt`, `CorrelationId`,
`ChangedFields[]` (yalnız alan **adları**), `NonPersonalBefore` / `NonPersonalAfter` (yalnız kişisel olmayan alanların değerleri: `LegalName`, `ShortName`,
`CountryCode`, `City`, `Status`, `Role`), `Reason` (durum değişikliğinde). Varlıkla **aynı Mongo işleminde** yazılır (Brand `BrandHistoryEntry` emsali,
`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/BrandHistoryEntry.cs:7`). Depo arayüzünde güncelleme / silme üyesi yoktur.

### 4.5 Roller ve genişleme noktası
- v1 enum değeri yalnız `ThirdPartyLogistics`. Ekran etiketi "3PL ortağı" (MOD-0179 M-15 terimi) 7 dilde.
- **Yeni rol eklemek** = enum'a eklemeli değer + yeni karar + paket revizyonu (ya da yeni FU) + 7 dil etiketi + rol kümesi testinin (AC-7) aynı commit'te güncellenmesi
  + **rol ver / rol kaldır komutlarının eklemeli gelişi** (C-5; uçlar, `mdm.business-partners.assign-role` izni, denetim satırları, AC'leri o pakette).
  Yeni rol ayrıca rol başına alan gerektirirse (ör. tedarikçi ödeme koşulu) o alanlar bu kökte değil **rol sahibinin modülünde** tutulur (BL-665 ile tutarlı).
- Sorgu `role` parametresi bilinmeyen değerde **400** `MDM_BP_ROLE_UNKNOWN` döner (sessiz boş liste yok).
- Tüketiciler rolü string olarak okur; bilinmeyen rol değerini yok sayar (ileri uyum — §8.4 tüketici kuralı).
- **v1 (C-5 kararı):** her kayıt tam olarak `ThirdPartyLogistics` taşır ve rol oluşturmada verilir; rol değiştiren komut, uç ve izin yoktur. Ekranda Roller
  sekmesi salt okunurdur (§11). Son rol kuralı alan kuralı olarak durur (AC-26).

### 4.6 Kişisel veri kuralı
MOD-0179 r1 F-5 notunun devri.
Kararlar 01 `:44-45`: MOD-0179'un "ham mesajda kişisel veri 90 gün sonra kaldırılır" kuralı MDM iş ortağı `Contacts` için **ayrıca** değerlendirilecekti. **Karar (Q-3, Beste 2026-11-13):**
1. **90 gün kuralı uygulanmaz:** ham mesaj işlem kaydıdır; iletişim kişisi ise ortak etkin olduğu sürece işin yürümesi için gereken canlı ana veridir.
2. **En aza indirme:** yalnız iş iletişim alanları (§4.2); kimlik no / kişisel adres alanı yok.
3. **Silme hakkı:** kişi `UpdateBusinessPartnerCommand` ile listeden çıkarılınca kayıttan **kalıcı** gider; geçmişte ve merkezi denetimde değeri **yoktur**, yalnız
   `Contacts` alan adı yazılır (AUD-001 §3 `.antigravity/rules/audit-trail-standard.md:112-115`: "Bir alan değiştiyse adı yazılır, değeri değil").
4. **Yayılım sınırı:** liste uçları, dışa aktarım ve sözleşmenin liste öğesi kişi / adres satırı / posta kodu **taşımaz**; vergi no listede yalnız **maskeli**
   (madde 5), dışa aktarımda hiç yoktur; kişi ve adres yalnız tekil uçtadır.
   Tüketici (MOD-0179) bu alanları **saklamaz** (MOD-0179 §4.1: "Ad, adres, iletişim, vergi no alanı YOKTUR", ≤ 60 sn önbellek).
5. **Vergi no (Q-4 kararı):** kiracıda (ülke, vergi no) tekil; **listede her zaman maskeli** (`taxNumberMasked`, `6140•••••21` biçimi: ilk 4 + son 2; kimse için
   açık değil); **ayrıntıda yetkiliye açık** — tekil uç `TaxNumber`'ı yalnız `mdm.business-partners.update` sahibine açık, diğerlerine maskeli döner.
6. **Zamanlanmış temizlik yok (Q-3):** v1'de pasif ortakta kişi verisi için zamanlanmış silme / maskeleme işi **yoktur**; saklama süresi istenirse ayrı karar ve
   takip adayı (§20).
7. Okuma denetimi: kişi verisi KVKK özel nitelikli değil, toplu dışa aktarım kişi verisi taşımıyor → AUD-001 §7 okuma denetimi gerekmez (§21 okuma satırı).

### 4.7 Indexes
| Koleksiyon | Index | Amaç |
|---|---|---|
| `mdm_business_partners` | `(TenantId, Code)` tekil | kod tekilliği (arşivli / pasif dahil — RCS-001 §6) |
| `mdm_business_partners` | `(TenantId, ShortNameNormalized)` tekil kısmi (`IsDeleted = false`) | kısa ad tekilliği; eşzamanlı oluşturmada DB son söz |
| `mdm_business_partners` | `(TenantId, Address.CountryCode, TaxNumberNormalized)` tekil kısmi (`TaxNumberNormalized` var, `IsDeleted = false`) | aynı vergi no ile ikinci kayıt yok (Q-4) |
| `mdm_business_partners` | `(TenantId, Status, Roles.Role, ShortNameNormalized)` | MOD-0179 seçim listesi (rol + durum) ve varsayılan sıralama |
| `mdm_business_partners` | `(TenantId, LegalNameNormalized)` | arama |
| `mdm_business_partner_history` | `(TenantId, BusinessPartnerId, OccurredAt desc)` | geçmiş sekmesi |

Index'ler MdmService başlangıç index hazırlığı desenine eklenir (`services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/MongoIndexReadiness.cs` — X-2).

## 5. Repo Scope

`MDM` = `services/Diten.MdmService/src/Diten.MdmService.`, `MDMT` = `services/Diten.MdmService/tests/`. Hepsi `approved` / `ready-for-dev` + CT-APR-01'den sonra.

| Yol | Ne | Dilim |
|---|---|---|
| `docs/analysis/contracts/drafts/business-partner-read-v1.0-proposal.md` (yazılacak) | `BUSINESS-PARTNER-READ` 1.0.0 taslağı — **DL-1'in ilk adımı** (§8.4); dondurma DL-1 kabulünde CT adımı → `docs/analysis/contracts/business-partner-read.md` | DL-1 |
| `MDM/Domain/Entities/{BusinessPartner,BusinessPartnerContact,BusinessPartnerRoleAssignment,BusinessPartnerHistoryEntry}.cs` | varlıklar (yeni) | DL-1 |
| `MDM/Domain/Enums/{BusinessPartnerRole,BusinessPartnerStatus,BusinessPartnerContactType}.cs` | enum'lar (yeni) | DL-1 |
| `MDM/Domain/Rules/BusinessPartnerRoleRules.cs` | rol kümesi ve son rol alan kuralı (yeni, saf) | DL-1 |
| `MDM/Domain/Repositories/{IBusinessPartnerRepository,IBusinessPartnerHistoryRepository}.cs` | depo arayüzleri (yeni) | DL-1 |
| `MDM/Application/Features/BusinessPartner/**` | Golden Compact düzeni (§10) | DL-1 |
| `MDM/Persistence/Repositories/{BusinessPartnerRepository,BusinessPartnerHistoryRepository}.cs` | depolar (yeni) | DL-1 |
| `MDM/Api/Controllers/BusinessPartnersController.cs` | 8 uç (yeni) | DL-1 |
| `MDM/Api/Export/BusinessPartnerExportColumns.cs` | dışa aktarım sütun kümesi (Brand `BrandExportColumns` deseni; yeni) | DL-1 |
| `MDM/Api/ModuleRegistration/BusinessPartnerMasterManifestProvider.cs` | izin + sayfa manifesti (`BrandProductMasterManifestProvider.cs` deseni; yeni) | DL-1 |
| `MDM/Application/Contracts/RecordCodes/IRecordCodeAllocator.cs`, `MDM/Infrastructure/RecordCodes/**` | **yalnız henüz yoksa** (X-3) | DL-1 |
| `MDMT/Diten.MdmService.Application.Tests/BusinessPartners/**`, `MDMT/Diten.MdmService.Application.Tests/ModuleRegistration/BusinessPartnerMasterManifestProviderTests.cs`, `MDMT/Diten.MdmService.Api.Tests/BusinessPartnerHttpTests.cs` | birim, gerçek Mongo, HTTP, mimari, sözleşme testleri (yeni) | DL-1 |
| `frontend/Diten.Web/Views/MasterData/BusinessPartners/**` | Compact ekran seti (§11; yeni) | DL-2 |
| `frontend/Diten.Web/Controllers/MasterData/BusinessPartnersController.cs` | `MasterDataGatewayController`'dan türeyen same-origin vekil (yeni) | DL-2 |
| `frontend/Diten.Web/Models/MasterData/BusinessPartnerViewModels.cs` | vekil / görünüm modelleri (yeni) | DL-2 |
| `frontend/Diten.Web/wwwroot/assets/js/MasterData/BusinessPartners/{index.js,index.l10n.js,form.js}` | JS (yeni) | DL-2 |
| `frontend/Diten.Web/Resources/Views/MasterData/BusinessPartners/BusinessPartnersIndex.{en,tr,fr,es,zh,ar,ru}.resx` | 7 dil (yeni) | DL-2 |
| `frontend/Diten.Web.Tests/MasterData/BusinessPartnersViewTests.cs` | Layout / UAS-001 / L10n / yardım / dosya seti testleri (yeni) | DL-2 |
| `gateway/Diten.ApiGateway.Tests/Mod0049Fu01BusinessPartnerRouteTests.cs` (integration-agent) | rota kapsama testi (yeni) | DL-3 |

### 5.1 XMC-001 — başka modülün / ortak dosyaların dokunuşları
Her satır dokunmadan önce sahibine sorulur; **yalnız eklemeli**. Ölçüm HEAD'i `b3e9e6b7a`. Sayılar sabit taban değildir — dilim başında yeniden ölçülür.
MDM dosyaları (X-1, X-2, X-3) T-5 izniyle (Ali, 2026-11-13; kararlar 01 `:32`) merkez yazar; dosya listesi bu paketle Ali'ye bilgi olarak gider (A-1).

| # | Dosya | Bugün (ölçüm) | Gereken değişiklik | Sahip | Dilim |
|---|---|---|---|---|---|
| X-1 | `services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs:453-456` (manifest kayıtları) ve uygulama DI bölgesi | üç sağlayıcı: LegalEntity, ProductItemSkuMaster, BrandProductMaster | `BusinessPartnerMasterManifestProvider` kaydı + `BusinessPartner` depo / servis kayıtları; mevcut satırlar değişmez | MDM (Ali) — T-5 izni | DL-1 |
| X-2 | `services/Diten.MdmService/src/Diten.MdmService.Persistence/DependencyInjection.cs`, `services/Diten.MdmService/src/Diten.MdmService.Persistence/Configurations/MdmDocumentClassMaps.cs`, `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/MongoIndexReadiness.cs` | iş ortağı yok | depo kayıtları, sınıf eşlemesi, §4.7 index'leri; yalnız ekleme | MDM (Ali) — T-5 izni | DL-1 |
| X-3 | MDM kayıt kodu portu `IRecordCodeAllocator` + HTTP bağdaştırıcısı | **yok** (MdmService `src` altında `RecordCode` geçen dosya yalnız `ExternalReferenceReservation.cs`, `MasterDataAtomicWrite.cs` — ölçüldü); MOD-0289 Repo Scope'u "MOD-0217 ile **tek** port ve tek bağdaştırıcı: iki paketten DL-1'e önce giren açar" der | MOD-0289 / MOD-0217'den önce DL-1'e girilirse port **MOD-0289'un imzasıyla** açılır (`AllocateAsync(prefixScope, legalEntityId, recordType, idempotencyKey)`); sonra girilirse yalnız tüketilir. İkinci port, kendi sayaç, yedek üretici ya da ürün ailesinin `CodeReservation`'ı **yok** | MDM + Treasury CT (MOD-0289) | DL-1 |
| X-4 | `services/Diten.Platform/src/Diten.Platform.Domain/Features/RecordCodes/RecordCodeTypeCatalog.cs:37` bölgesi | `BNK`, `BAC`, `AGR` Mdm türleri; `BPT` yok | `new("BPT", "İş Ortağı", "OTOMATİK", "MOD-0049-FU01", [RecordCodePrefixScope.Group], Mdm, "mdm.business-partners.create")` | Platform (merkez / Beste — MOD-0040-FU02) | DL-1 |
| X-5 | `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedRecordCodeCredentialAuthenticator.cs:21` | Mdm tüketicisi `["BNK", "BAC", "AGR"]` | listeye `"BPT"` | Platform (merkez / Beste) | DL-1 |
| X-6 | `services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/RecordCodeTypeCatalogTests.cs:27-38` (`RuleAheadOfCatalogue`), `:34` (`BPT` satırı) | **kapıda yazıldı** (`63f046c92`): `["BPT"] = "MOD-0049-FU01 DL-1"` | **DL-1'de** X-4 ile aynı commit'te `BPT` satırı kalkar ve AC09'a `BPT` → `[Group]` iddiası eklenir | Platform (merkez) | DL-1 |
| X-7 | `docs/analysis/contracts/record-code-allocate.md` (1.0.0 FROZEN; `:5` "değişiklik yalnız yeni sürümle ve yalnız ekleme") | `:51` Mdm çağıran türleri, `:63` tür → izin tablosu, `:160-169` tür kataloğu | eklemeli **1.1.0**: `BPT` (yalnız Group, çağıran Mdm, izin `mdm.business-partners.create`) | CT (MOD-0040-FU02 sözleşme sahibi) | DL-1 kabulünden önce |
| X-8 | `gateway/Diten.ApiGateway/ocelot.json` — MDM rota bloğu (`/api/mdm/brands` `:3819`, `/{everything}` `:3836` emsali) | `/api/mdm/business-partners` yok; MDM her kaynağı ayrı rota | iki blok: `/api/mdm/business-partners` (`GET`, `POST`, `OPTIONS`) ve `/api/mdm/business-partners/{everything}` (`GET`, `POST`, `PUT`, `OPTIONS`) → `localhost:5059`; **`DELETE` yok** | **integration-agent** (T-6) | rota DL-1; test DL-3 |
| X-9 | `frontend/Diten.Web/Resources/SharedResource.{en,tr,fr,es,zh,ar,ru}.resx`, `frontend/Diten.Web/Resources/PageHelpResource.{7}.resx`, `frontend/Diten.Web/Services/Help/PageHelpCatalog.cs` | `Nav.Module.BRANDPRODUCTMASTER`, `Nav.Page.BRANDS` var; iş ortağı anahtarı yok | `Nav.Module.BUSINESSPARTNERMASTER`, `Nav.Page.BUSINESSPARTNERS`, `Perm.Entity.BUSINESSPARTNERS`, `Help.BUSINESSPARTNERS.*` 7 dilde; **yeni izin fiili yok** (§14 — rol köprüsü değişmez) | Web ortak (merkez) | DL-2 |
| X-10 | `services/Diten.AuthService/**` rol şablonları | — | 3PL operatör rolüne `mdm.business-partners.read` (MOD-0179 Q-13 ile birlikte); ana veri sorumlusu rolüne yazma anahtarları | Ali (Auth) | ayrı WP — A-2 |
| X-11 | `execution/domains/master-data-management/domain-config.md` — `:58` "Frontend / Gateway Scope Exception (MOD-0290-FU02 only)", `:71` "This exception applies to no other MDM module pack." | MOD-0049 / FU01 satırı yok | "In-Scope" satırı (MOD-0049-FU01, placement T-5 / Q-10) + frontend / gateway kapsam cümlesi (`MasterData/BusinessPartners`, iki rota) | CT yazar (bilgi Ali); bu paket domain-config'e dokunmaz — metin önerisi §19.8 | **onaydan önce** (Q-7 kararı) |
| X-12 | `tests/architecture/audit-ledger/Diten.MdmService.md` | `:12` `mdm-merkezi-iletim` yol b işaret izi kabul edilmiş | **değişiklik yok** (Q-2 = A kararı: işaret izi komutları tanır; yeni borç satırı yok) | — | — |

**MDM bekçi testleri değişmez (bağlayıcı):** MdmService test projelerindeki **mevcut** dosyalar hiçbir satırı değişmeden yeşil kalır; X-1/X-2'nin üretim satırları dışında
mevcut MDM üretim dosyası değişmez. Kanıt: dilim kabulünde `git diff --stat <dilim-başı>..<dilim-sonu> -- services/Diten.MdmService/tests` yalnız **yeni** dosyaları
gösterir (AC-14).

## 6. Protected Paths

- `.antigravity/**` — kullanıcı onayı ister. **Kapı KAPANDI (Beste "yaz", commit `63f046c92`):** RCS-001 §3 `BPT` satırı
  `.antigravity/rules/record-coding-standard.md:76` ve aynı adımda X-6 (`RecordCodeTypeCatalogTests.cs:34`); 17/17 yeşil, satır silinince AC16 kırmızı (karar kaydı
  `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:14-16`). Bu paket ve dilimleri `.antigravity/**`'a **yazmaz**.
  PKS-001 değişmez: `mdm.*` ad alanı `Diten.MdmService`'indir (`.antigravity/rules/permission-key-standard.md:97`); `mdm.business-partners` yeni kaynak, yeni ad alanı değil.
- `gateway/Diten.ApiGateway/**/ocelot.json` — yalnız integration-agent (X-8).
- `services/Diten.Platform/**` — yalnız X-4, X-5, X-6'nın adlı satırları (Platform sahibinin eliyle).
- `services/Diten.AuthService/**` (X-10 ayrı WP), `services/Diten.Building.Blocks/**`, `services/Diten.ProcurementService/**` (MOD-0140 tedarikçi), `services/Diten.CrmService/**`
  (MOD-0149), `services/Diten.InventoryService/**` (MOD-0179 — bu paket tüketiciye dokunmaz).
- `services/Diten.MdmService/**` içinde §5 / §5.1 dışında her şey: MOD-0220 LegalEntity, MOD-0290 ürün ailesi, Brand, denetim niyeti üreticileri, mevcut manifestler.
- `frontend/Diten.Web/Views/Archive/**`, `Views/Shared/_Layout.cshtml` (donuk), `_LayoutTenantShell.cshtml` (nav manifestten gelir; layout değişikliği yok).
- Donmuş sözleşmeler: `docs/analysis/contracts/record-code-allocate.md` (yalnız X-7 eklemeli sürümle, CT adımıyla), `docs/analysis/contracts/supplier.openapi.yaml`.
- Kayıtlar: `execution/registries/**` (bu paketle değişmez), MOD-0179 paketi (tüketici; değişiklik gerekmez — §19.4).

## 7. Dependencies

| Bağımlılık | Tür | Durum (ölçüm @`b3e9e6b7a`) | Kullanım | Değişiklik | Sahip |
|---|---|---|---|---|---|
| MOD-0040-FU02 Record Code Service | **HARD — DL-1 kabul kapısı** | Platform kodu var: `services/Diten.Platform/src/Diten.Platform.API/Controllers/Internal/InternalRecordCodesController.cs` (`POST /api/internal/record-codes/allocate`); `Group` kapsamı etkin (`docs/analysis/contracts/record-code-allocate.md:157`); `BPT` katalogda yok | `BPT` kodu | X-4…X-7 | merkez (Beste) |
| MDM `IRecordCodeAllocator` portu | HARD (DL-1) | yok (X-3) | kod isteği | X-3 | MDM + Treasury CT |
| Kiracı grup öneki hizalaması | HARD (işletim) | Platform ucu var (`services/Diten.Platform/src/Diten.Platform.API/Controllers/Admin/TenantRecordCodeGroupPrefixController.cs`); hizalanmamış kiracıda servis 409 `GROUP_PREFIX_MISSING` | grup öneki (bugün `GMG`) | yok — Platform yöneticisi işlemi | Platform yöneticisi |
| Platform `TrustedRecordCode` Mdm kimlik bilgisi | HARD (işletim) | `services/Diten.Platform/src/Diten.Platform.API/appsettings.json:207` `"Mdm"` bağlaması boş değerlerle | MDM → Platform çağrısı | yok — ortam sırları | işletim (A-5) |
| MDM merkezi denetim iletimi | HARD | `IPlatformAuditForwarder` kayıtlı (`services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs:462`); defter `tests/architecture/audit-ledger/Diten.MdmService.md:12` yol b, `:18` "En iyi çaba: iletim hatası yutulur" | §21 (Q-2 = A; başarısız komut `Outcome = Failed` ile iletilir — `services/Diten.MdmService/src/Diten.MdmService.Application/Behaviors/AuditForwardingBehavior.cs:60`) | yok (X-12) | MDM |
| BL-501 ortak iletici | SOFT | `services/Diten.Building.Blocks/Diten.BuildingBlocks.CentralAudit.Tests` var; MdmService kendi iletici kopyasını kullanır (BL-501'e geçmedi) | — | yok: FU01 MDM'nin mevcut işaretini kullanır; MDM'nin BL-501'e geçişi ayrı merkez işi, FU01 komutlarını değiştirmez | merkez |
| MOD-0018 RBAC + plan senkronu | HARD | `[HasPermission]` + manifest (Brand emsali) | 6 anahtar | X-10 (A-2), kiracı planına modül (A-3) | Ali / Platform yöneticisi |
| Gateway | HARD (DL-1 canlı kanıt, DL-2, MOD-0179 DL-4) | `/api/mdm/business-partners` yok | rota | X-8 | integration-agent |
| Ülke listesi | SOFT (yalnız ekran) | `GET /api/lookups/reference/countries` (`services/Diten.Platform/src/Diten.Platform.API/Controllers/TenantReferenceLookupsController.cs:29`; MOD-0220 sihirbazı emsali), mevcut `/api/lookups/{everything}` rotası | form seçim listesi | yok | Platform |
| MOD-0220 LegalEntity | — | iş ortağı tüzel kişiliğe bağlanmaz (kiracı geneli) | — | yok | MDM |
| MOD-0179 3PL Integration | **tüketici** | r3 `ready-for-dev`, DL-4 bu paketin DL-1'ine bağlı (`execution/domains/supply-chain-execution/module-packs/MOD-0179-3pl-integration.md:1163`) | okuma sözleşmesi | yok | supply-chain-execution |
| Port | — | MDM 5059 (AGENTS.md §3) | — | yeni port / servis yok | — |

## 8. Runtime Constraints

### 8.1 Servis ve kapsam
- Servis `Diten.MdmService`, port 5059; yeni servis ya da port yok. Tarayıcı ve `Diten.Web` yalnız Gateway (5000) üzerinden; tarayıcı JS 5059'a **gitmez**.
- Kiracı izolasyonu: her sorgu ve yazma `TenantId` yüklemli; başka kiracının kaydı **404** `MDM_BP_NOT_FOUND` (var olmayanla aynı gövde), yazma yok.
- Silme yok; `IsDeleted` teknik alan olarak durur, v1 komutları set etmez (Brand emsali). Liste ve tekil uç `IsDeleted = true` kaydı görmez.
- Ülke: `CountryCode` yalnız biçim olarak doğrulanır (`^[A-Z]{2}$`); ekranın seçim listesi `/api/lookups/reference/countries`'ten gelir, sabit yedek liste **yok**.
  Bu, MDM iş ülkesi SoR'u iddiası değildir (domain-config Country Boundary `execution/domains/master-data-management/domain-config.md:103-106`); iş ülkesi
  sahipliği kararı gelirse doğrulama sıkılaşır (§20).

### 8.2 Durum makinesi
`Active` →(deactivate, gerekçe 10..500)→ `Inactive` →(activate, gerekçe 10..500)→ `Active`. **Karar (Q-6):** `Inactive` kayıt **salt okunur** — güncelleme
409 `MDM_BP_INACTIVE`; yalnız `activate` çalışır. Aynı duruma geçiş 409 (`MDM_BP_ALREADY_ACTIVE` / `MDM_BP_ALREADY_INACTIVE`). Pasifleşme tüketicide
yeni bağlamayı kapatır, mevcut bağlantıyı bozmaz (MOD-0179 §4.10 `:365-366`); FU01 tüketiciyi bilmez ve onu denetlemez.

### 8.3 Kayıt kodu üretimi
- `CreateBusinessPartnerHandler`: (1) istek doğrulaması ve tekillik ön denetimi → (2) `IRecordCodeAllocator.AllocateAsync(prefixScope: Group, legalEntityId: null,
  recordType: "BPT", idempotencyKey: <yeni kaydın Id'si>)` → (3) varlık + geçmiş satırı **tek Mongo işleminde**. Kod alınıp işlem düşerse numara boşluk olarak kalır
  (RCS-001 §5.8); kod yeniden kullanılmaz.
- Hata eşlemesi (MOD-0289 D133 emsali): 403 `RECORD_CODE_PERMISSION_MISSING` → Create **403** (yeniden denenmez, kayıt yok); diğer 200 dışı her yanıt, `isSuccessful`
  false ya da boş `data`, zaman aşımı, port bağlı değil (ör. 409 `GROUP_PREFIX_MISSING`) → Create **503** `MDM_BP_RECORD_CODE_UNAVAILABLE`, kayıt yok. **Yedek kod
  üretilmez**, kodsuz kayıt yazılmaz.
- Kod `{GRUP}-BPT-{000001}`: grup öneki kiracı ayarından (`docs/analysis/contracts/record-code-allocate.md:157`, bugün `GMG`), sayaç (kiracı, grup öneki, tür) başına,
  ≥ 6 hane, sıfırlanmaz. Kiracı geneli kayıt → şirket öneki yerine grup öneki (`.antigravity/rules/record-coding-standard.md:103`, §5.3; `BNK` emsali `:68`).

### 8.4 Okuma sözleşmesi BUSINESS-PARTNER-READ
`BUSINESS-PARTNER-READ` 1.0.0 — taslak; dosyası DL-1'in ilk adımında yazılır.
Kimlik: kullanıcı token'ı (JWT) + Gateway kiracı çözümü; izin `mdm.business-partners.read`. Zarf `Response<T>`.

**Operasyon 1 — `listBusinessPartners`:** `GET /api/mdm/business-partners`
| Parametre | Tip | Kural |
|---|---|---|
| `role` | string? | `BusinessPartnerRole` adı (v1 `ThirdPartyLogistics`); bilinmeyen → 400 `MDM_BP_ROLE_UNKNOWN` |
| `status` | string? | `Active` \| `Inactive`; yoksa ikisi; bilinmeyen → 400 `MDM_BP_STATUS_UNKNOWN` |
| `search` | string? | ≤100; kod, kısa ad, ticari ad üzerinde önek / içerir (normalleştirilmiş) |
| `countryCode` | string? | `^[A-Z]{2}$` |
| `page`, `pageSize` | int | `page ≥ 1`; `pageSize` 1..100 (varsayılan 25) |
| `orderBy`, `orderDir` | string | beyaz liste: `code`, `shortName`, `legalName`, `status`, `updatedAt`; `asc` \| `desc`; başka değer 400 |

Yanıt `data`: `{ items: BusinessPartnerListItem[], totalCount, page, pageSize }`.
`BusinessPartnerListItem` = `{ id, code, legalName, shortName, displayName (= shortName), countryCode, city, taxNumberMasked (string | null, her zaman maskeli),
status, roles: string[], version, updatedAt }` — kişi, adres satırı, posta kodu **taşımaz**; vergi no yalnız maskeli (Q-4).

**Operasyon 2 — `getBusinessPartner`:** `GET /api/mdm/business-partners/{id}` → `BusinessPartnerDetail` = liste öğesi alanları + `taxNumber` (maskeli ya da açık —
§4.6 madde 5; `isTaxNumberMasked: bool`), `address { countryCode, city, district, addressLine, postalCode }`, `contacts: [{ contactId, type, name, email, phone }]`,
`roles: [{ role, assignedAt }]`, `statusReason`, `statusChangedAt`, `createdAt`. 404 `MDM_BP_NOT_FOUND` (yok, silinmiş ya da başka kiracı — aynı gövde).

**Tüketici kuralları (bağlayıcı):** (1) tüketici yalnız `id` ve `code`'u saklar; ad / adres / kişi / vergi no **saklanmaz** (önbellek ≤ 60 sn); (2) bilinmeyen
`roles` değeri ve bilinmeyen alan yok sayılır (ileri uyum); (3) 404 → "ana veride yok", 503 / zaman aşımı → "ana veriye ulaşılamadı", yeniden deneme tüketicinin
kuralıdır; (4) **yalnız GET** — tüketici MDM'ye yazmaz.

**MOD-0179 port eşlemesi** (`IBusinessPartnerDirectory`, MOD-0179 `:362-363`): `ListAsync(role: ThirdPartyLogistics, status: Active)` → operasyon 1
(`role=ThirdPartyLogistics&status=Active`); `GetAsync(id)` → operasyon 2; `{ id, code, displayName, status, roles }` doğrudan; `contactSummary` → tüketici
operasyon 2'nin `contacts`'ından türetir (saklamaz).

**Kullanıcısız okuma:** v1 yalnız kullanıcı token'ıyla okunur. MOD-0179'un kullanıcısız işçisi (giden mesaj öncesi ortak durumu, MOD-0179 AC-37) servis
kimliği ister (MOD-0033-FU02, MOD-0179 Q-12) — gelince sözleşmeye **eklemeli** 1.1.0 (A-4). Değişiklik olayı yayını v1'de yok (§20).

**Fikstürler (taslakta):** F-1 etkin 3PL ortağı; F-2 pasif 3PL ortağı; F-3 başka rollü ortak (ileri uyum; v1 API'si üretemez — test belgesi doğrudan Mongo'ya yazılır); F-4 başka kiracının kaydı (404); F-5 maskeli vergi no; F-6 bilinmeyen rol
sorgusu (400); F-7 MDM kapalı (Gateway 502/503).

### 8.5 Hata kodları
Sınıf `BusinessPartnerErrorCodes`.

| Kod | HTTP | Ne zaman |
|---|---|---|
| `MDM_BP_NOT_FOUND` | 404 | yok / silinmiş / başka kiracı |
| `MDM_BP_SHORT_NAME_DUPLICATE` | 409 | kısa ad kiracıda var |
| `MDM_BP_TAX_NUMBER_DUPLICATE` | 409 | aynı ülke + vergi no kiracıda var (Q-4) |
| `MDM_BP_EXPECTED_VERSION_REQUIRED` | 400 | yazma isteğinde `expectedVersion` yok / negatif |
| `MDM_BP_VERSION_CONFLICT` | 409 | sürüm uyuşmuyor; yazma yok |
| `MDM_BP_INACTIVE` | 409 | pasif kayıtta güncelleme (Q-6 kararı) |
| `MDM_BP_ALREADY_ACTIVE`, `MDM_BP_ALREADY_INACTIVE` | 409 | aynı duruma geçiş |
| `MDM_BP_REASON_REQUIRED` | 400 | gerekçe yok / 10..500 dışında |
| `MDM_BP_ROLE_UNKNOWN`, `MDM_BP_STATUS_UNKNOWN` | 400 | tanımsız rol / durum |
| `MDM_BP_ROLES_REQUIRED` | 400 | oluşturmada rol yok ya da tekrarlı rol |
| `MDM_BP_CONTACT_INVALID` | 400 | kişi kuralı (§4.2) ya da 10'dan fazla kişi |
| `MDM_BP_RECORD_CODE_UNAVAILABLE` | 503 | §8.3 |
| `RECORD_CODE_PERMISSION_MISSING` | 403 | §8.3 (kod servisinin kodu aynen) |

Kullanıcıya görünen ileti `Accept-Language`'a göre 7 dilde (MDM resx); kod dil bağımsızdır. Rol değiştirme hata kodları (zaten atanmış, atanmamış, son rol) v1 uç
tablosunda **yoktur** (C-5); son rol yalnız alan kuralının `LastRole` sonucudur (AC-26) ve rol komutları geldiğinde o paket uç koduna eşler.

### 8.6 Veri kapsamı (SEC-002)
Kayıt kiracı genelidir (`LegalEntityId` yok) → tüzel kişilik bazlı satır süzgeci **uygulanmaz**; okuma izni olan kiracı kullanıcısı kiracının bütün iş ortaklarını
görür. Gerekçe: 3PL ortağı grup düzeyinde sözleşilen dış hizmet sağlayıcıdır; MOD-0179 bağlantıyı kendi `LegalEntityId`'siyle şirket bazında tutar. Şirket bazlı
görünürlük gerekirse ayrı karar (§20).

### 8.7 Merkezi denetim ve yerel geçmiş
**Karar Q-2 = A (Beste 2026-11-13):** her komut `IAuditableCommand` + `IAuditMetadataProvider` (+ `IAuditDetailsProvider`) taşır → MDM `AuditForwardingBehavior` → `IPlatformAuditForwarder`
→ `POST /api/internal/audit/append` (yol **b**, iz `mdm-merkezi-iletim`, en iyi çaba). İletim kaybına karşı kaydın kendi geçmişi (§4.4) aynı işlemde yazılır
(Brand emsali: "audited twice over"). Merkezi kayıt: `Category = MasterData`, `EntityType = "BusinessPartner"`, `SourceModule = "business-partner-master"`,
`Operation` §21. **K2 sınıfı değildir** (kimlik / yetki / kiracı durumu / GxP / KVKK özel nitelikli veri değiştirmez) → kapalı-başarısız şartı yoktur.
Seçilmeyen seçenek B (güvenilir kaynak niyeti, `mdm-guvenilir-kaynak-niyeti`, MOD-0289 emsali) bu pakette kullanılmaz; X-12'de değişiklik yoktur.

## 9. Layout & Shell Contract
- `shell: tenant` → `Index.cshtml`, `Create.cshtml`, `Edit.cshtml`, `Details.cshtml` dosyalarının her birinde Razor bloğunda **açıkça** `Layout = "_LayoutTenantShell";`
  (emsal `frontend/Diten.Web/Views/MasterData/Brands/Index.cshtml:7`). `_ViewStart.cshtml` varsayılanına güvenilmez.
- Partial'larda (`_Filter`, `_DataTable`, `_Form`, `_IndexL10n`) `Layout =` **yoktur** (kabuğu iki kez çizer).
- View klasörü `frontend/Diten.Web/Views/MasterData/BusinessPartners/`; rota `/MasterData/BusinessPartners`.
- Nav: manifestten (`BUSINESSPARTNERS` sayfası, `IsNavigationVisible: true`); `_LayoutTenantShell.cshtml` değişmez. Nav anahtarları `Nav.Module.BUSINESSPARTNERMASTER`,
  `Nav.Page.BUSINESSPARTNERS` (`NavNameLocalizer.Normalize` biçimi).
- Yetkisiz kullanıcı (UAS-001): `mdm.business-partners.read` yoksa yalnız `_AccessDenied` partial'ı, HTTP 403; başlık / filtre / iskelet / düğme **yok**, yönlendirme
  **yok** (Brand emsali `frontend/Diten.Web/Views/MasterData/Brands/Index.cshtml:16-20`).

## 10. Backend File Convention

Golden Reference **Compact** (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceCompact/`) birebir; MDM özellik
klasörü tekil adla (`Features/Brand/`, `Features/LegalEntity/` emsali):

```text
services/Diten.MdmService/src/Diten.MdmService.Application/Features/BusinessPartner/
├── Commands/
│   ├── CreateBusinessPartnerCommand.cs          (sealed record, IRequest<Response<Guid>>, IAuditableCommand, IAuditMetadataProvider)
│   ├── UpdateBusinessPartnerCommand.cs          (IRequest<Response<NoContent>>)
│   ├── DeactivateBusinessPartnerCommand.cs
│   └── ActivateBusinessPartnerCommand.cs
├── Queries/
│   ├── GetBusinessPartnerListQuery.cs
│   ├── GetBusinessPartnerByIdQuery.cs
│   ├── GetBusinessPartnerHistoryQuery.cs
│   ├── GetBusinessPartnerExportQuery.cs
│   └── BusinessPartnerListSort.cs               (Golden Compact `GoldenReferenceCompactListSort.cs` deseni — beyaz liste)
├── Handlers/
│   ├── CommandHandlers/
│   │   ├── CreateBusinessPartnerHandler.cs
│   │   ├── UpdateBusinessPartnerHandler.cs
│   │   ├── DeactivateBusinessPartnerHandler.cs
│   │   └── ActivateBusinessPartnerHandler.cs
│   └── QueryHandlers/
│       ├── GetBusinessPartnerListHandler.cs
│       ├── GetBusinessPartnerByIdHandler.cs
│       ├── GetBusinessPartnerHistoryHandler.cs
│       └── GetBusinessPartnerExportHandler.cs
├── Validators/
│   ├── BusinessPartnerWriteRequestValidator.cs   (ortak alan kuralları — Brand `BrandWriteRequestValidator` emsali)
│   ├── CreateBusinessPartnerValidator.cs
│   ├── UpdateBusinessPartnerValidator.cs
│   ├── DeactivateBusinessPartnerValidator.cs
│   ├── ActivateBusinessPartnerValidator.cs
│   └── GetBusinessPartnerListValidator.cs
└── BusinessPartnerModels.cs                      (tek dosyada tüm DTO / istek / sabitler)
```

Adlandırma: Handler / Validator adlarında `Command` / `Query` soneki **yok**. Controller sınıf düzeyinde `[Authorize]` + `[ApiController]` + `[Route("api/mdm/business-partners")]`;
`[HasPermission]` **her eylemde**, sınıf düzeyinde yasak (AC-10).

**Golden Compact'tan bilinçli sapmalar (Q-5 kararı: paketteki gibi kabul):**
| Sapma | Gerekçe |
|---|---|
| `Delete` / `BulkDelete` komutu, handler'ı, ucu yok | ana veride silme yok, pasifleştirme var (Brand emsali `BrandsController.cs` "There is deliberately NO DELETE action") |
| Ek komutlar: Deactivate / Activate | yaşam döngüsü kapsamın kendisi (Q-10 kararı "taraf kaydı ve roller"); rol komutları v1 dışı (C-5) |
| Ek sorgular: History / Export | geçmiş sekmesi (§4.4) ve sunucu dışa aktarımı (BL-452) |
| Güncelleme `PUT` | MDM uç deseni (Brand `[HttpPut("{brandId:guid}")]`) |
| `ExpectedVersion` gövdede | MDM deseni (Brand `UpdateBrandHandler` `ExpectedVersion`) |

## 11. Frontend File Contract

Golden **Compact** (`frontend/Diten.Web/Views/DevEnablement/GoldenReferenceCompact/`) dosya seti:

```text
frontend/Diten.Web/Views/MasterData/BusinessPartners/
├── Index.cshtml                      (Layout = "_LayoutTenantShell" AÇIKÇA; UAS-001 kapısı)
├── Create.cshtml                     (Layout AÇIKÇA; kabuk + _Form)
├── Edit.cshtml                       (Layout AÇIKÇA; kabuk + _Form; Code salt okunur gösterilir)
├── Details.cshtml                    (Layout AÇIKÇA; sekmeler: Genel · İletişim kişileri · Roller · Geçmiş)
├── _Form.cshtml                      (Create/Edit ortak form; 10 alan — kişiler tekrarlı grup)
├── _Filter.cshtml                    (arama, rol, durum, ülke)
├── _DataTable.cshtml                 (data-dt-standard="v2", data-dt-data-mode="server", iskelet; HasSelection = false)
├── _IndexL10n.cshtml
└── BusinessPartnersIndex.cs          (marker sınıf)

frontend/Diten.Web/wwwroot/assets/js/MasterData/BusinessPartners/
├── index.js                          (const endpoint = '/MasterData/BusinessPartners/api'; export: { mode: 'server', url })
├── index.l10n.js
└── form.js

frontend/Diten.Web/Resources/Views/MasterData/BusinessPartners/
└── BusinessPartnersIndex.{en,tr,fr,es,zh,ar,ru}.resx   (kiracı modülü → 7 dil)
```

**Compact'ta YASAK:** `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, Index içinde oluştur / düzenle paneli.

Kurallar: (1) toplu seçim yok (silme yok) → `_BulkActionBar` yok; (2) satır eylemleri izinle çizilir: Görüntüle (`read`), Düzenle (`update`, yalnız `Active`),
Pasifleştir (`deactivate`), Etkinleştir (`activate`); araç çubuğu: Yeni (`create`), Dışa aktar (`export`); (3) Pasifleştir / Etkinleştir gerekçe ister —
Premium SweetAlert2 (MOD-0013), birincil düğme `data-dt-primary="chord"` (BL-586); (4) ülke listesi vekil üzerinden `/api/lookups/reference/countries`, sabit
yedek **yok**; (5) form `Code` girdisi içermez; (6) "Roller" sekmesi **salt okunur**: rolleri ve atanma zamanını listeler; rol ver / rol kaldır düğmesi, modalı ve vekil eylemi **yoktur** (C-5); oluşturma
formunda rol seçimi vardır (v1 tek seçenek: 3PL ortağı), düzenleme formunda yoktur; (7) ayrıntıda vergi no maskeli
gelirse "maskeli" rozeti; (8) "Geçmiş" sekmesi `GET …/{id}/history` (alan adları ve kişisel olmayan değerler). Liste ekranı dokunma protokolü:
`python3 .antigravity/scripts/verify_datatable_page.py . --area MasterData --module BusinessPartners --api-profile proxy --data-mode --format gaps`.

## 12. Validation Rules

| Field | Required | Format/Rule | DB-level | Pre-check |
|---|---|---|---|---|
| LegalName | Evet | trim, 2..200 | — | — |
| ShortName | Evet | trim, 2..60; normalleştirilmiş tekil | `(TenantId, ShortNameNormalized)` tekil kısmi | `ExistsByShortNameAsync` (kendisi hariç) |
| TaxNumber | Hayır | trim, 1..30, `[A-Za-z0-9-]` | `(TenantId, CountryCode, TaxNumberNormalized)` tekil kısmi | `ExistsByTaxNumberAsync` |
| Address.CountryCode | Evet | `^[A-Z]{2}$` | — | — (biçim; §8.1) |
| Address.City | Evet | trim, 1..100 | — | — |
| Address.District | Hayır | ≤100 | — | — |
| Address.AddressLine | Hayır | ≤300 | — | — |
| Address.PostalCode | Hayır | ≤20, `[A-Za-z0-9 -]` | — | — |
| Roles (oluşturma) | Evet | 1..n, her biri tanımlı `BusinessPartnerRole`, tekrarsız | — | — |
| Contacts | Hayır | 0..10; `Type` tanımlı; `Name` 2..120; `Email` biçim ≤256; `Phone` `^\+?[0-9 ()-]{5,32}$`; Email ∨ Phone | — | — |
| ExpectedVersion (güncelle / durum / rol) | Evet | ≥0 | koşullu yazım `Version == expected` | — |
| Reason (deactivate / activate) | Evet | trim, 10..500 | — | — |
| Roles (güncelleme) | — | güncelleme isteğinde alan **yok**; gönderilirse bağlanmaz (C-5) | — | — |
| Code, TenantId, Status, Version | — | istekte alan **yok**; gönderilirse bağlanmaz | — | — |

İlişkili kurallar: pasif kayıtta güncelleme 409 `MDM_BP_INACTIVE` (Q-6); rol kümesi her an ≥1 — alan kuralı `BusinessPartnerRoleRules` (Q-5, AC-26).

## 13. Failure Path to Verify

- **Kısa ad çakışması** — 409 `MDM_BP_SHORT_NAME_DUPLICATE` + formda alan hatası; kayıt oluşmaz; iki eşzamanlı oluşturmada biri 409 (gerçek Mongo, index).
- **Vergi no çakışması** — 409 `MDM_BP_TAX_NUMBER_DUPLICATE`; kayıt oluşmaz.
- **Zorunlu alan eksik** (ticari ad, kısa ad, ülke, şehir, rol) — 400 + doğrulayıcı iletisi (7 dil); kaydet engellenir.
- **Eşzamanlılık** — eski `expectedVersion` ile güncelleme / durum / rol → 409 `MDM_BP_VERSION_CONFLICT` + ekranda "veri değişti, yeniden yükleyin"; sessiz üzerine yazma yok.
- **Yetkisiz aktör** — her uç izinsiz 403; ekranda ilgili düğme çizilmez; okuma izni yoksa UAS-001 boş yüzey (403, yönlendirme yok).
- **Başka kiracının kaydı** — GET / PUT / POST eylemleri 404 `MDM_BP_NOT_FOUND`; yazma yok; liste hiç döndürmez.
- **Kod servisi yok / izin yok** — 503 `MDM_BP_RECORD_CODE_UNAVAILABLE` / 403; kayıt ve geçmiş satırı yazılmaz; yedek kod yok.
- **Pasif kayıtta düzenleme** — 409 `MDM_BP_INACTIVE`.
- **Gerekçesiz pasifleştirme** — 400 `MDM_BP_REASON_REQUIRED`; durum değişmez.
- **Bilinmeyen rol sorgusu** — 400 `MDM_BP_ROLE_UNKNOWN` (sessiz boş liste değil).
- **Silme denemesi** — `DELETE /api/mdm/business-partners/{id}` Gateway'de rota yok, serviste eylem yok (405 / 404); kayıt değişmez.
- **Merkezi denetim iletimi düşer** — komut başarılı, yerel geçmiş satırı var (yol b en iyi çaba; Q-2 kararı).
- **Güncellemeyle rol değiştirme denemesi** — gövdedeki `roles` bağlanmaz; kayıttaki roller değişmez (C-5; AC-7).
- **Rol ucu çağrısı** — `POST …/{id}/roles…` serviste eylem yok (404); kayıt değişmez (C-5).
- **Dışa aktarım çok büyük** — 413 `EXPORT_TOO_LARGE` (> 50 000 satır), kısmi dosya yok (Brand emsali).

## 14. Authorization Convention

```text
Policy:     [Authorize] (sınıf) — shell: tenant
Permission: [HasPermission("mdm.business-partners.{action}")] — her eylemde; sınıf düzeyinde YASAK
Actor type: tenant_user; platform_admin otomatik geçer (MOD-0018)
```

| Anahtar | Tier (PKS-001 §5) | Uçlar |
|---|---|---|
| `mdm.business-partners.read` | 1 | 1, 2, 3 |
| `mdm.business-partners.create` | 1 | 5 (+ Platform kod servisinin tür izni — X-4) |
| `mdm.business-partners.update` | 1 | 6 (+ tekil uçta açık vergi no — §4.6) |
| `mdm.business-partners.deactivate` | 2 | 7 |
| `mdm.business-partners.activate` | 2 | 8 |
| `mdm.business-partners.export` | 2 | 4 |

Ad alanı `mdm.*` MdmService'indir (`.antigravity/rules/permission-key-standard.md:97`) — yeni ad alanı yok, `.antigravity` değişmez. Bütün fiiller Rol İzinleri köprüsünde
**zaten vardır** (`read`, `create`, `update`, `deactivate`, `activate`, `export` — `frontend/Diten.Web/Views/Governance/RoleAssignments/_IndexL10n.cshtml`);
yalnız `Perm.Entity.BUSINESSPARTNERS` satırı 7 dilde eklenir (X-9).

**Manifest (`BusinessPartnerMasterManifestProvider`):** ModuleCode `business-partner-master`, ModuleName `BusinessPartnerMaster`, Domain `MASTER-DATA-MANAGEMENT`, Service
`DitenMdmService`, `IsTenantAssignable: true`, SortOrder dilim başında ölçülür (öneri Brand `335`'ten sonra); sayfa `BUSINESSPARTNERS` (`/MasterData/BusinessPartners`,
`RequiredPermission` read, `IsNavigationVisible: true`, `PageType: List`), eylemler `ADD_NEW` (create), `EXPORT` (export), `VIEW_DETAILS` (read), `EDIT` (update),
`DEACTIVATE` (deactivate, tehlikeli), `ACTIVATE` (activate); `ASSIGN_ROLE` eylemi ve `mdm.business-partners.assign-role` v1'de **yok** (C-5). Manifest yalnız `mdm.business-partners.*` bildirir; her anahtar bir
controller'ın uyguladığı anahtarla birebir (Brand manifest notu). Plan senkronu: modül kiracı planındaysa Admin = hepsi, Viewer = read (A-3).

**Rol önerisi (A-2, Auth'ta ayrı WP):** "Ana Veri Sorumlusu" = 6 anahtar; "3PL Operatörü" (MOD-0179) = `read`; "Görüntüleyici" = `read`.

## 15. Gateway / API Routing Decision

- **Karar: Gateway değişikliği gerekli** (X-8, integration-agent).
- Frontend Gateway 5000 üzerinden çağırır (`Diten.Web` vekili sunucu tarafında); tarayıcı 5059'a gitmez.
- Mevcut `ocelot.json`'da `/api/mdm/{everything}` catch-all **yok**; MDM her kaynağı ayrı rotadadır (`gateway/Diten.ApiGateway/ocelot.json:3819`, `:3836`).
- İstenen bloklar: `/api/mdm/business-partners` → `GET, POST, OPTIONS`; `/api/mdm/business-partners/{everything}` → `GET, POST, PUT, OPTIONS`; downstream `localhost:5059`,
  aynı yol. **`DELETE` yok.** Kiracı ara katmanı normal çalışır (muafiyet yok).
- Rota DL-1'de (canlı kanıt ve MOD-0179 DL-4 için); kapsama testi DL-3'te (`gateway/Diten.ApiGateway.Tests/Mod0049Fu01BusinessPartnerRouteTests.cs`).
- `gateway/Diten.ApiGateway/**/ocelot.json` korunan yoldur; bu paket doğrudan yazmaz.

## 16. Acceptance Criteria

**DL-1 — çekirdek: arka uç + okuma API'si + sözleşme (MOD-0179 DL-4 kapısı)**
- [ ] AC-1 (zorunlu) — `docs/analysis/contracts/drafts/business-partner-read-v1.0-proposal.md` DL-1'in ilk commit'inde yazılı (§8.4 iki operasyon, tüketici kuralları, F-1…F-7); sözleşme testi üretim DTO'larını (`BusinessPartnerListItemDto`, `BusinessPartnerDetailDto`) taslağın alan listesine karşı kapalı ölçer; DL-1 kabulünde CT `docs/analysis/contracts/business-partner-read.md` 1.0.0 FROZEN yazar. S-1: `BusinessPartnerListItemDto`'dan `roles`'u kaldır → sözleşme testi kırmızı.
- [ ] AC-2 (zorunlu) — `POST /api/mdm/business-partners`: `TenantId` kiracı bağlamından; gövdeye eklenen başka kiracı `tenantId`'si etkisiz; `Status = Active`; `Version` başlangıç değeri; geçmişe `created` satırı varlıkla **aynı işlemde** (işlem düşerse ikisi de yok — gerçek Mongo replica set). S-2a: handler'da `TenantId`'yi istekten oku → vaka kırmızı; S-2b: geçmiş yazımını işlem dışına al ve işlemi düşür → "ikisi de yok" vakası kırmızı.
- [ ] AC-3 (zorunlu) — Kod `IRecordCodeAllocator` ile `prefixScope = Group`, `recordType = "BPT"`; biçim `^[A-Z]{2,5}-BPT-\d{6,}$`; yazma isteklerinde `Code` alanı yok, hiçbir uç kodu değiştiremez; kod servisi 403 → 403, diğer her hata / zaman aşımı / bağlı olmayan port → 503 `MDM_BP_RECORD_CODE_UNAVAILABLE`, kayıt ve geçmiş yok, yedek kod yok (sahte allocator ile). S-3a: allocator hatasında yerel kod üret → vaka kırmızı; S-3b: `UpdateBusinessPartnerHandler`'a `Code` ataması ekle → değişmezlik vakası kırmızı; S-3c: `prefixScope`'u `LegalEntity` yap → istek iddiası kırmızı.
- [ ] AC-4 (zorunlu) — §12 doğrulamaları; kısa ad ve (ülke, vergi no) tekilliği hem ön denetimde hem index'te; iki eşzamanlı aynı kısa adlı oluşturmada tam biri başarılı, diğeri 409 (gerçek Mongo). S-4a: ön denetimi ve tekil index'i kaldır → eşzamanlı vaka kırmızı; S-4b: kişi kuralında "Email ∨ Phone" denetimini kaldır → vaka kırmızı.
- [ ] AC-5 (zorunlu) — Güncelle / pasifleştir / etkinleştir `expectedVersion` ister (yoksa 400); uyuşmazlıkta 409 `MDM_BP_VERSION_CONFLICT` ve hiçbir alan yazılmaz; başarılı her yazma `Version`'ı +1 artırır. S-5: koşullu yazımdan sürüm yüklemini kaldır → çakışma vakası kırmızı.
- [ ] AC-6 (zorunlu) — Pasifleştir / etkinleştir gerekçe 10..500 ister; pasif kayıtta güncelleme 409 `MDM_BP_INACTIVE` (Q-6 kararı; yalnız etkinleştir çalışır); aynı duruma geçiş 409; controller'larda, komut katmanında ve manifestte silme yok (yansıma testi: `BusinessPartnersController`'da `HttpDelete` yok, `Features/BusinessPartner` altında `Delete*` tipi yok). S-6a: gerekçe denetimini kaldır → vaka kırmızı; S-6b: controller'a `[HttpDelete]` eylem ekle → mimari test kırmızı; S-6c: pasif denetimini güncelleme handler'ından kaldır → vaka kırmızı.
- [ ] AC-7 (zorunlu) — `BusinessPartnerRole` değer kümesi tam olarak `{ ThirdPartyLogistics }` (küme testi; yeni rol aynı commit'te testi ve paketi günceller — §4.5); oluşturma ≥1, tekrarsız, tanımlı rol ister (yoksa / tekrarlıysa 400 `MDM_BP_ROLES_REQUIRED`, tanımsızsa 400 `MDM_BP_ROLE_UNKNOWN`); **C-5:** güncelleme isteği rolleri değiştiremez (gövdedeki `roles` bağlanmaz, kayıttaki roller aynı kalır) ve controller'da rol uçları yoktur (yansıma: `…/roles` rotalı eylem yok, `Assign*` / `Remove*Role*` komut tipi yok). S-7a: enum'a ikinci değer ekle → küme testi kırmızı; S-7b: `UpdateBusinessPartnerHandler`'da istekteki rolleri varlığa eşle → "güncelleme rolleri değiştirmez" vakası kırmızı.
- [ ] AC-8 (zorunlu) — Liste `role` + `status` + `search` + `countryCode` süzgeçleri, sayfa (`pageSize` ≤ 100), beyaz liste sıralama; `role=ThirdPartyLogistics&status=Active` yalnız etkin ve 3PL rollü kayıtları döndürür (F-1 döner, F-2 / F-3 dönmez; F-3 doğrudan Mongo'ya yazılmış başka rollü test belgesi); bilinmeyen rol / durum / sıralama 400; tekil uç ve geçmiş ucu çalışır. S-8a: rol süzgecini kaldır → F-3 vakası kırmızı; S-8b: durum süzgecini kaldır → F-2 vakası kırmızı; S-8c: sıralama beyaz listesini kaldır → `orderBy=tenantId` vakası kırmızı.
- [ ] AC-9 (zorunlu) — Kiracılar arası: başka kiracının kaydına GET / PUT / POST eylemleri 404 `MDM_BP_NOT_FOUND` (yok olanla aynı gövde), yazma yok; liste ve dışa aktarım başka kiracı satırı döndürmez (iki kiracılı gerçek Mongo testi). S-9: depo okuma süzgecinden `TenantId` yüklemini düşür → vaka kırmızı.
- [ ] AC-10 (zorunlu) — 8 eylemin her birinde §3'teki tam anahtarla `[HasPermission]`; sınıf düzeyinde `HasPermission` yok; izinsiz çağrı 403 (HTTP testi, her uç); manifest tam 6 anahtar bildirir, hepsi `mdm.business-partners.` önekli ve her biri bir eylemin anahtarı; `mdm.business-partners.assign-role` bildirilmez (C-5) (`BusinessPartnerMasterManifestProviderTests`). S-10a: bir eylemden `[HasPermission]`'ı kaldır → yansıma testi kırmızı; S-10b: manifestte anahtar yazımını boz → manifest testi kırmızı; S-10c: sınıfa `[HasPermission("mdm.business-partners.read")]` ekle → yansıma testi kırmızı.
- [ ] AC-11 (zorunlu) — Dört komutun her biri `IAuditableCommand` + `IAuditMetadataProvider` taşır; `Operation` §21'deki gibi; `EntityType = "BusinessPartner"`, `SourceModule = "business-partner-master"`; `AuditTrailStandardTests` defterde **yeni borç satırı olmadan** yeşil (Q-2 kararı: yol b + yerel geçmiş); ayrıntıda kişisel alanların (`TaxNumber`, `Address.AddressLine`, `Address.PostalCode`, `Address.District`, `Contacts`) yalnız **adı** geçer, değeri geçmez (merkezi ayrıntı ve yerel geçmiş için ayrı vaka). S-11a: `DeactivateBusinessPartnerCommand`'dan `IAuditableCommand`'ı kaldır → `AuditTrailStandardTests` kırmızı; S-11b: güncelleme ayrıntısına kişinin e-postasını yaz → kişisel veri vakası kırmızı.
- [ ] AC-12 (zorunlu) — Q-4 kararı: liste öğesi ve sözleşmenin liste öğesi kişi / adres satırı / posta kodu taşımaz, vergi no'yu yalnız `taxNumberMasked` (ilk 4 + son 2) olarak taşır — `update` sahibine de listede açık değer dönmez; dışa aktarım sütun kümesinde vergi no hiç yok (yansıma + yanıt gövdesi testi); tekil uç vergi no'yu `update` izni yoksa maskeli, varsa açık döner, `isTaxNumberMasked` buna uyar. S-12a: `BusinessPartnerListItemDto`'ya açık `TaxNumber` ekle → test kırmızı; S-12b: tekil uçta maskelemeyi kaldır → `read`-yalnız kullanıcı vakası kırmızı; S-12c: liste maskesini `update` sahibi için atla → `update` sahibinin liste vakası kırmızı.
- [ ] AC-13 (zorunlu) — Platform kayıt kodu kataloğu `BPT` (`Group`, çağıran Mdm, izin `mdm.business-partners.create`) — X-4, X-5; `RuleAheadOfCatalogue`'dan `BPT` satırı aynı commit'te kalkar ve `RecordCodeTypeCatalogTests` (AC16, AC09 `BPT` → `[Group]`) yeşil; RECORD-CODE-ALLOCATE 1.1.0 eklemeli sürümü (X-7) main'de. S-13: Mdm tüketici listesinden `BPT`'yi çıkar → Platform tüketici eşleme testi (`AC14_the_consumer_map…`) kırmızı.
- [ ] AC-14 (zorunlu) — MdmService'in **bütün** test projeleri filtresiz yeşil; `git diff --stat <dilim-başı>..<dilim-sonu> -- services/Diten.MdmService/tests` yalnız yeni dosyaları gösterir; mevcut MDM üretim dosyalarında yalnız X-1 / X-2 satırları değişir. S-14: mevcut bir Brand testinin bir satırını değiştir → diff denetimi kabul kaydında kırmızı.
- [ ] AC-15 (zorunlu) — Canlı kanıt (dev ortamı, Gateway 5000, gerçek Platform kod servisi; `scripts/run_record.py` kaydı): oluşturma `GMG-BPT-…` biçiminde kod alır; `role=ThirdPartyLogistics&status=Active` listesi; başka kiracı 404; Platform kod servisi durdurulunca oluşturma 503 ve kayıt yok. S-15: kod servisi kapalıyken oluşturma 201 dönerse kanıt kırmızı (negatif adım kayıtta zorunlu).
- [ ] AC-16 (zorunlu) — `GET …/export?format=csv|xlsx`: liste süzgeçleri ve aynı 400'ler, ≤ 50 000 satır (fazlası 413 `EXPORT_TOO_LARGE`, kısmi dosya yok), `Cache-Control: no-store`, sütunlar kişisel veri içermez (AC-12), en iyi çaba `DataExport` olayı (§21 okuma satırı). S-16: sütun kümesine `TaxNumber` ekle → dışa aktarım sözcük bekçisi (Brand `BrandExportWordsGuardTests` deseni) kırmızı.

- [ ] AC-26 (zorunlu) — Q-5 kararı, **alan kuralı düzeyinde** (C-5: v1'de uç yok): `BusinessPartnerRoleRules.CanRemove(roles, role)` tek rollü kümede `LastRole`, iki rollü kümede izin verir; `ValidateRoleSet` boş kümeyi reddeder; birim vakası rol kümesini bellek içinde kurar (enum değeri gerektirmez — kural rol değerinden bağımsız, sayıya bakar); kural handler'da değil Domain'dedir ve ikinci rolün rol kaldırma komutu bunu kullanır. S-26a: `CanRemove`'daki son rol denetimini kaldır → tek rollü vaka kırmızı; S-26b: denetimi `Count > 1` yerine `Count >= 1` yaz → tek rollü vaka kırmızı.

**DL-2 — MDM kiracı ekranı**
- [ ] AC-17 (zorunlu) — `Views/MasterData/BusinessPartners/{Index,Create,Edit,Details}.cshtml` dosyalarının her birinde `Layout = "_LayoutTenantShell";` açıkça yazılı; partial'larda `Layout =` yok. S-17: `Details.cshtml`'den Layout satırını sil → view testi kırmızı.
- [ ] AC-18 (zorunlu) — Dosya seti tam Compact (§11); `_CreateEditOffcanvas.cshtml` ve `_DetailsQuickView.cshtml` yok; `verify_datatable_page.py . --area MasterData --module BusinessPartners --api-profile proxy --data-mode` PASS (`data-dt-standard="v2"`, `data-dt-data-mode="server"`, sunucu dışa aktarımı) ve `quality-gate-datatable` PASS. S-18a: `_DetailsQuickView.cshtml` ekle → dosya seti testi kırmızı; S-18b: `data-dt-data-mode="client"` yap → doğrulayıcı kırmızı.
- [ ] AC-19 (zorunlu) — 7 dil (en, tr, fr, es, zh, ar, ru) resx paritesi; nav anahtarları, `Perm.Entity.BUSINESSPARTNERS`, rol etiketi ("3PL ortağı"), durumlar, kişi türleri, hata kodu iletileri; sabit kullanıcı metni yok; `scripts/check_pack_nav_keys.py` PASS; vitest rol etiketi defteri yeşil (yeni fiil yok). S-19: `BusinessPartnersIndex.zh.resx`'ten bir anahtar sil → parite testi kırmızı.
- [ ] AC-20 (zorunlu) — UAS-001: `read` yoksa Index / Details yalnız `_AccessDenied`, HTTP 403, filtre / başlık / iskelet / düğme yok, yönlendirme yok; Yeni / Düzenle / Pasifleştir / Etkinleştir / Dışa aktar düğmeleri yalnız ilgili izinle çizilir; Düzenle yalnız `Active` satırda; Roller sekmesi salt okunur — rol ver / rol kaldır düğmesi, modalı ve vekil eylemi yok (C-5; §11 kural 6). S-20a: izin denetimini filtre partial'ından sonraya taşı → view testi kırmızı; S-20b: Dışa aktar düğmesini koşulsuz çiz → view testi kırmızı; S-20c: Roller sekmesine "Rol ver" düğmesi ekle → view testi kırmızı.
- [ ] AC-21 (zorunlu) — Formda `Code` girdisi yok (Edit / Details'ta salt okunur gösterim); ülke seçim listesi vekil üzerinden `/api/lookups/reference/countries`, uç düşerse alan hata durumu gösterir, sabit yedek liste yok; ayrıntıda maskeli vergi no "maskeli" rozetiyle. S-21a: `form.js`'e sabit ülke dizisi ekle → JS taraması kırmızı; S-21b: `_Form.cshtml`'e `Code` girdisi ekle → view testi kırmızı.
- [ ] AC-22 (zorunlu) — Pasifleştir / Etkinleştir Premium SweetAlert2 modalı gerekçe ister (10..500, istemcide ve sunucuda), sunucu hata kodları 7 dilde gösterilir, birincil düğme `data-dt-primary="chord"` (vitest BL-586 yeşil); `window.confirm` yok. S-22: modal gerekçe zorunluluğunu kaldır → vitest vakası kırmızı.
- [ ] AC-23 (zorunlu) — Sayfa yardımı `Help.BUSINESSPARTNERS.Purpose`, `.Audience`, `.Step1…Step4` + `Step{n}Title` 7 dilde (§25); "tur yok". S-23: `PageHelpResource.tr.resx`'ten `Help.BUSINESSPARTNERS.Step1` sil → yardım testi kırmızı.
- [ ] AC-24 (zorunlu) — Tarayıcı 5059'a ve doğrudan Gateway'e gitmez (yalnız same-origin vekil); vekil her yazmada `[ValidateAntiForgeryToken]`, `Accept-Language` iletir; tarayıcı smoke (E3, kayıtlı): oluştur → ayrıntı → düzenle → pasifleştir (Düzenle düğmesi kaybolur) → etkinleştir, tr ve en. S-24: `index.js`'e `localhost:5059` adresi yaz → JS taraması kırmızı.

**DL-3 — Gateway kapsama testi (integration-agent)**
- [ ] AC-25 (zorunlu) — `gateway/Diten.ApiGateway.Tests/Mod0049Fu01BusinessPartnerRouteTests.cs`: §3'teki 8 uç (yöntem + yol) 5059'a aynı yolla yönlenir; `DELETE` hiçbir `/api/mdm/business-partners…` rotasında yok; `OPTIONS` iki rotada var; başka hiçbir rota bu öneki yakalamaz; Gateway test projesinin geri kalanı değişmeden yeşil. S-25a: `{everything}` rotasını kaldır → test kırmızı; S-25b: rotaya `DELETE` ekle → test kırmızı.

## 17. Test Expectations

- **Birim (Application):** doğrulayıcılar (§12 her satır), durum makinesi, rol kümesi, maskeleme, sıralama beyaz listesi, denetim ayrıntısı (kişisel alan adları).
- **Gerçek Mongo (replica set):** tekillik ve eşzamanlılık (AC-4), sürüm (AC-5), işlem bütünlüğü (AC-2), iki kiracı izolasyonu (AC-9), index hazırlığı.
  Yerel ortam: `DITEN_TEST_MONGOD` ile kendi mongod'u (MDM test emsali); bilinen ortam kırmızıları main ile karşılaştırılır.
- **HTTP (Api.Tests):** 10 ucun izin / 403 / 404 / 400 / 409 / 503 yolları; sahte allocator (`FakePlatform` deseni).
- **Mimari / yansıma:** silme yok, `HasPermission` her eylemde, geçmiş deposunda güncelleme / silme yok, liste DTO'sunda kişisel alan yok.
- **Sözleşme:** AC-1 (taslağa karşı); MOD-0179 DL-4 kendi tüketici testini FROZEN 1.0.0'a karşı koşar (MOD-0179 AC-35).
- **Denetim:** `AuditTrailStandardTests` (defter borcu büyümez).
- **Platform:** `RecordCodeTypeCatalogTests` (X-6), servise dokunulduğunda **bütün** Platform test paketi filtresiz.
- **Frontend:** `Diten.Web.Tests` view / L10n / yardım / dosya seti testleri; `verify_datatable_page.py` (proxy profili); vitest (BL-586, rol etiketi defteri,
  `dialog-one-implementation` — Windows'ta main ile karşılaştırılır); tarayıcı smoke (E3).
- **Gateway:** AC-25.
- **Build:** `Diten.MdmService.sln`, `Diten.Web`, Platform (X-4…X-6 diliminde), Gateway test projesi (DL-3) PASS.
- **RESX:** 7 dil parite PASS.

### 17.1 AC → test → sabotaj eşlemesi
| AC | Test | Sabotaj |
|---|---|---|
| AC-1 | sözleşme testi (Application.Tests) | S-1 |
| AC-2 | gerçek Mongo + HTTP | S-2a, S-2b |
| AC-3 | birim + HTTP (sahte allocator) | S-3a, S-3b, S-3c |
| AC-4 | gerçek Mongo eşzamanlı + birim | S-4a, S-4b |
| AC-5 | gerçek Mongo | S-5 |
| AC-6 | birim + mimari | S-6a, S-6b, S-6c |
| AC-7 | birim (küme) + HTTP + yansıma | S-7a, S-7b |
| AC-8 | gerçek Mongo + HTTP (F-1…F-3, F-6) | S-8a, S-8b, S-8c |
| AC-9 | iki kiracılı gerçek Mongo + HTTP | S-9 |
| AC-10 | yansıma + HTTP 403 + manifest testi | S-10a, S-10b, S-10c |
| AC-11 | `AuditTrailStandardTests` + ayrıntı vakası | S-11a, S-11b |
| AC-12 | yansıma + yanıt gövdesi | S-12a, S-12b, S-12c |
| AC-13 | Platform `RecordCodeTypeCatalogTests` | S-13 |
| AC-14 | tam MDM paketi + diff denetimi | S-14 |
| AC-15 | canlı kanıt kaydı (E4) | S-15 |
| AC-16 | HTTP + dışa aktarım sözcük bekçisi | S-16 |
| AC-17 | view testi | S-17 |
| AC-18 | dosya seti testi + `verify_datatable_page.py` | S-18a, S-18b |
| AC-19 | resx parite + nav anahtarı betiği + vitest | S-19 |
| AC-20 | view testi | S-20a, S-20b, S-20c |
| AC-21 | view testi + JS taraması | S-21a, S-21b |
| AC-22 | vitest | S-22 |
| AC-23 | yardım testi | S-23 |
| AC-24 | JS taraması + tarayıcı smoke (E3) | S-24 |
| AC-25 | Gateway rota testi | S-25a, S-25b |
| AC-26 | birim (alan kuralı) | S-26a, S-26b |

Her sabotaj aynı oturumda geri alınır; kabul kaydı "sabotaj → kırmızı → geri al → yeşil" sırasını test adıyla yazar.

## 18. Ready-for-dev Checklist

- [x] Golden Reference Compact okundu (backend `Features/GoldenReferenceCompact/` 17 dosya, frontend 9 view + 3 JS); sapmalar adıyla yazıldı (§10)
- [x] Frontmatter zorunlu alanlar dolu (service, shell, golden_reference, data_mode, entity_base, form_field_count)
- [x] Layout & Shell Contract'ta `Layout = "_LayoutTenantShell"` açıkça (§9) ve test edilebilir (AC-17)
- [x] Backend File Convention Golden Compact ile birebir; Handler / Validator soneksiz (§10)
- [x] Frontend File Contract Compact seti tam, yasak dosyalar adıyla (§11)
- [x] Validation Rules her alan için (§12)
- [x] Failure Path ≥ 4 senaryo (duplicate, missing, unauthorized, concurrency + 9 ek) (§13)
- [x] Authorization: 6 anahtar, policy, aktör, manifest (§14)
- [x] Gateway kararı: gerekli, integration-agent, `DELETE` yok (§15)
- [x] Lookup kararı: ülke listesi mevcut `/api/lookups/reference/countries`; yeni Platform lookup anahtarı yok; sabit yedek yok (§8.1)
- [x] AC'ler kimlikli, test edilebilir, her biri adlı sabotajlı (§16, §17.1)
- [x] Audited Events her yazma komutunu tam bir kez içeriyor (§21)
- [x] Sayfa yardımı ve tur kararı (§25)
- [x] Kayıt kodu sınıfı ve öneki gerekçeli (§22); `BPT` çakışma ölçümü (§19.2)
- [x] Beste kararları Q-1…Q-7 ve C-5 (§19.7)
- [x] `.antigravity` kapısı: RCS-001 §3 `BPT` satırı + aynı adımda X-6 (commit `63f046c92`)
- [ ] domain-config satırı ve frontend / gateway kapsam cümlesi (X-11 — CT yazar; metin §19.8) — onaydan önce
- [ ] Ali bilgilendirildi (A-1…A-5) — itiraz yok
- [ ] Bağımsız inceleme (CT-APR-02) — bu sürüm için
- [ ] Onay satırı (CT-APR-01, onaylayan Ali / `GMG-ERP`) — bu sürüm için
- [ ] DL-1 kapısı: X-3 port sahibi koordinasyonu (MOD-0289 / MOD-0217), X-4…X-6 Platform sahibi onayı, X-8 integration-agent onayı
- [ ] DL-1 kabul kapısı: X-7 RECORD-CODE-ALLOCATE 1.1.0 main'de; dev ortamında Mdm kod kimlik bilgisi ve grup öneki hizalı (A-5)

## 19. Implementation Notes

### 19.1 Okunan kaynaklar
- `AGENTS.md` Çekirdek, §6, §6.1, §7, §10, DCP-002 bölümü; `.antigravity/rules/module-pack-standard.md`; `.antigravity/rules/permission-key-standard.md` (§4 `:97`, §5);
  `.antigravity/rules/record-coding-standard.md` (§3 `:68`, `:75`; §5.3 `:103`; §5.5 `:115`; §6); `.antigravity/rules/audit-trail-standard.md` (§3 `:112`, §5 `:184`, §6, §7 `:243`).
- `execution/domains/master-data-management/domain-config.md` (`:58`, `:71`, `:103-106`); `execution/registries/module-id-registry.md:160-161`.
- `docs/records/decisions/2026-11/mod0179-3pl-owner-decisions-01.md` (`:13`, `:32`, `:44-45`, `:48-54`); `docs/roadmap/backlog/product-backlog.md:10965` (BL-665).
- MOD-0179 r3 (`execution/domains/supply-chain-execution/module-packs/MOD-0179-3pl-integration.md` `:360-366`, `:398`, `:841`, `:844-848`, `:1163`) ve r1
  (`241659850:execution/domains/supply-chain-execution/module-packs/MOD-0179-3pl-integration.md:349-369` §4.10, `:402-404` X-1…X-3).
- MOD-0289 paketi (Repo Scope, Dependencies — kayıt kodu portu ve D133 hata eşlemesi emsali); DEV-0001; MOD-0290-FU paketlerinin künyeleri (biçim).
- Kod: `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/{EntityBase,Brand,BrandHistoryEntry,LegalEntity}.cs`,
  `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/BrandsController.cs`, `services/Diten.MdmService/src/Diten.MdmService.Api/ModuleRegistration/BrandProductMasterManifestProvider.cs`,
  `services/Diten.MdmService/src/Diten.MdmService.Application/Features/Brand/Commands/CreateBrandCommand.cs`, `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Audit/AuditEnums.cs`,
  `services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs:453-462`, `tests/architecture/audit-ledger/Diten.MdmService.md`,
  `services/Diten.Platform/src/Diten.Platform.Domain/Features/RecordCodes/RecordCodeTypeCatalog.cs`, `services/Diten.Platform/src/Diten.Platform.API/Security/TrustedRecordCodeCredentialAuthenticator.cs`,
  `services/Diten.Platform/tests/Diten.Platform.Application.Tests/RecordCodes/RecordCodeTypeCatalogTests.cs`, `docs/analysis/contracts/record-code-allocate.md`,
  `services/Diten.Platform/src/Diten.Platform.API/Controllers/TenantReferenceLookupsController.cs`, `gateway/Diten.ApiGateway/ocelot.json` (MDM blokları),
  `frontend/Diten.Web/Views/MasterData/Brands/`, `frontend/Diten.Web/Controllers/MasterData/MasterDataGatewayController.cs`,
  `frontend/Diten.Web/Views/Governance/RoleAssignments/_IndexL10n.cshtml` (fiil köprüsü).

### 19.2 Ölçülen gerçekler (HEAD `b3e9e6b7a`)
- MdmService'te iş ortağı / partner varlığı **yok**; `Domain/Entities` altında yalnız ürün ailesi, marka, rakip ürün, tüzel kişilik ve denetim varlıkları.
- MDM `EntityBase` `LegalEntityId` taşımaz (`EntityBase.cs:3-12`) → kiracı geneli kayıt.
- MDM'de kayıt kodu portu yok; Platform kod servisi ve `Group` kapsamı var; `BPT` katalogda ve kodda yok (depo genelinde `BPT` araması boş); RCS-001 §3'te `BPT` yok;
  `SUP-` MOD-0140'ın kendi biçimidir (RCS-001 dışı), `BPT` ile çakışmaz.
- MDM Gateway rotaları kaynak başına (`ocelot.json:3819`, `:3836` …); `/api/mdm/business-partners` yok.
- Brand ekranı `Views/MasterData/Brands/` (`Layout = "_LayoutTenantShell"` `:7`, UAS-001 `:16-20`); vekil tabanı `MasterDataGatewayController`.
- Rol köprüsünde `read`, `create`, `update`, `activate`, `deactivate`, `export` fiilleri var → yeni fiil köprüsü gerekmez.
- MDM denetim işlemleri `Create`, `Update`, `Activate`, `Deactivate`, `Assign`, `Revoke`, `Export` tanımlı (`AuditEnums.cs:25-43`).

### 19.3 Çıkarımlar (ölçüm değil)
- Form alanı 10 (ticari ad, kısa ad, vergi no, ülke, şehir, ilçe, adres satırı, posta kodu, roller, kişiler) > 8 → **Compact**.
- MOD-0179 tüketici portu bu sözleşmeye değişiklik istemeden oturur (§8.4 eşleme); MOD-0179 paketi değişmez (§19.4).
- Kullanıcısız okuma (MOD-0179 işçisi) servis kimliği gelene kadar sözleşmede yoktur; tüketicinin açık işidir (A-4).

### 19.4 MOD-0179'dan alınanlar
| MOD-0179 kaynağı | Bu pakette |
|---|---|
| r1 §4.10 alan tasarımı (`Code`, `LegalName`, `ShortName`, `TaxNumber` maskeli, adres, `Contacts[]`, `Roles[]`, `Status`, `Version`) | §4.1…§4.3 (kişi türüne `Other` eklendi; kişi kuralı §4.2; adres nesneye toplandı) |
| r1 X-1 (MdmService kodu, `mdm.business-partners.*`, manifest, yol b) | §5, X-1, X-2, §14, §21 |
| r1 X-2 (MDM Gateway rotası) | X-8, §15, AC-25 |
| r1 X-3 (MDM ekranı, Golden Slim) | §9, §11 — **Compact**'a çevrildi (10 alan) |
| r1 Q-11 (MDM iş ortağı kayıt kodu öneki) | Q-1 = `BPT`, §22 (grup önekli) |
| r1 AC-35…AC-38 (MDM dilimi) | AC-2…AC-12, AC-25 (genişletildi) |
| r3 §4.10 okuma portu `ListAsync(role, status)`, `GetAsync(id)` | §8.4 operasyon 1 ve 2, port eşlemesi |
| r3 AC-34 (b) seçim listesi "3PL rollü etkin" | AC-8 (rol + durum süzgeci) |
| r3 DL-4 kapısı "FU01 API'si `main`'de" | §24: FU01 DL-1 kabulü + X-8 rotası + sözleşme FROZEN |
| Kararlar 01 F-5 notu (Contacts kişisel veri) | §4.6 (Q-3 kararı) |

MOD-0179 §4.10 "Kesin uç ve alan adları FU01 paketinin sözleşmesinden gelir (DL-4 başında)" dediği için MOD-0179 paketi bu paketle **değişmez**.

### 19.5 Plan hizalaması
Plan dosyası depoda değil ve bu modülün plan satırı bana verilmedi: dilim tablosunda `plan satırı yok` / `plan kimliği yok`. Bağlam: tüketici MOD-0179'un plan
MVP'si MVP-5 (MOD-0179 §23, çağıran CT aktarımı). Plan satırı açılırsa fark sahibe sorulur; paket kendiliğinden değişmez.

### 19.6 Dal
Künye `branch` alanı sahibin tek çalışma dalı kuralına göre `feature/sce/mod-0173-inventory-ledger-valuation` (MOD-0179 ile aynı). Standart biçim
`feature/mdm/mod-0049-fu01-business-partner-master-foundation` bu kural nedeniyle kullanılmaz.

### 19.7 Sahip kararları (r2)
Kaynak: `docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:14-21` (Beste; Q-1 ".antigravity kapısı — yaz", Q-2…Q-7 "hepsinde öneri gibi").

| # | Konu | Karar | Pakette |
|---|---|---|---|
| Q-1 | Kayıt kodu öneki (MOD-0179 r1 Q-11'in devri) | **`BPT`**, OTOMATİK, grup önekli (`GMG-BPT-000001`); `.antigravity` RCS-001 §3 satırı ve X-6 aynı adımda yazıldı (`63f046c92`) | §4.1, §6, §8.3, §22, X-6 |
| Q-2 | Denetim yolu | **A** — yol b `mdm-merkezi-iletim` (en iyi çaba) + aynı işlemde yerel geçmiş (Brand emsali); defter değişmez | §8.7, §21, AC-11, X-12 |
| Q-3 | İletişim kişileri kişisel veri kuralı (kararlar 01 F-5 notunun devri) | 90 gün kuralı **uygulanmaz**; en aza indirme; kişi silinince **kalıcı** silme (geçmişte / denetimde değer yok); v1'de zamanlanmış temizlik **yok** | §4.2, §4.6, AC-11 |
| Q-4 | Vergi numarası | kiracıda (ülke, vergi no) **tekil**; **listede maskeli**; **ayrıntıda yetkiliye** (`mdm.business-partners.update`) açık | §4.1, §4.6, §4.7, §8.4, AC-4, AC-12 |
| Q-5 | Golden Compact sapmaları ve son rol | sapmalar **paketteki gibi** (§10); **son rol kaldırılamaz** — önce pasifleştirme | §4.3, §4.5, §8.5, §10, §11, AC-7, AC-26 |
| Q-6 | Pasif kayıt | **salt okunur**; yalnız etkinleştir | §8.2, AC-6 |
| Q-7 | domain-config | MDM ekran / Gateway istisna satırı **eklenir; merkez (CT) yazar** — bu paket domain-config'e dokunmaz | X-11, §19.8 |
| C-5 | Rol ver / rol kaldır (`docs/records/decisions/2026-11/mvp5-wms-ct-owner-decisions-2026-11-13.md:28-30`, Beste "iki komutu v1'den çıkar") | komutlar, uçlar ve `mdm.business-partners.assign-role` izni **v1'den çıkar**; rol **oluşturmada** verilir; "son rol kaldırılamaz" (Q-5) **alan kuralı** olarak kalır; ikinci rol eklenince iki komut **eklemeli** gelir | §2.1, §3, §4.3, §4.5, §8.5, §10, §11, §12, §13, §14, §21, AC-5, AC-6, AC-7, AC-10, AC-11, AC-20, AC-25, AC-26 |

**r1 → r2 kapanış:** Q-1…Q-7 kapandı. **r2 → r3 kapanış:** C-5 kapandı (sahip kararı). Beste'ye açık soru kalmadı. Açık kalanlar yalnız Ali bilgi notları
A-1…A-5 ve CT soruları C-1…C-4 (§26).

**r2 → r3 sayılar:** komut 6 → 4; uç 10 → 8; izin 7 → 6; uç hata kodu 18 → 15 (`MDM_BP_ROLE_ALREADY_ASSIGNED`, `MDM_BP_ROLE_NOT_ASSIGNED`, `MDM_BP_LAST_ROLE`
çıktı); AC sayısı 26 (değişmedi; AC-5, AC-6, AC-7, AC-10, AC-11, AC-20, AC-25, AC-26 metni değişti); sabotaj: S-7b ve S-11a yeniden yazıldı, S-26a / S-26b alan
kuralına indi, S-20c eklendi (45 → 46). `BUSINESS-PARTNER-READ` 1.0.0 (§8.4) ve MOD-0179 port eşlemesi değişmedi — okuma uçları 1 ve 2 aynı.

### 19.8 domain-config satırı önerisi (Q-7 — CT yazar)
`execution/domains/master-data-management/domain-config.md`'ye eklenmesi önerilen metin (mevcut `## Frontend / Gateway Scope Exception (MOD-0290-FU02 only)`
bölümü ve `:71` cümlesi olduğu gibi kalır; yeni bölüm ayrı başlıktır):

```text
## In-Scope Reserved Module (owner decisions T-5 / Q-10, 2026-11-13)

- `MOD-0049-FU01 Business Partner Master Foundation` — child of Blueprint `MOD-0049` (registry rows MOD-0049 / MOD-0049-FU01);
  placement owner decision Q-10 (`docs/records/decisions/2026-11/mod0179-3pl-owner-decisions-01.md`), MDM owner permission T-5
  (Ali, 2026-11-13; central team develops); pack
  [MOD-0049-FU01](module-packs/MOD-0049-FU01-business-partner-master-foundation.md); `Diten.MdmService`, no new service or port.
  v1 scope: tenant-wide party record + roles (v1 role `ThirdPartyLogistics`); match / merge, survivorship and supplier / customer
  SoR are out of scope (BL-665). Consumer: MOD-0179 (read only, `BUSINESS-PARTNER-READ`).

## Frontend / Gateway Scope Exception (MOD-0049-FU01)

The "frontend and gateway remain outside the first implementation slice" restriction is narrowly lifted for MOD-0049-FU01, per the
module-pack-over-domain-config authority rule:

- `frontend/Diten.Web/**` — only the `MasterData/BusinessPartners` surface (views, same-origin proxy controller, JS, resx) plus
  additive shared keys (`Nav.Module.BUSINESSPARTNERMASTER`, `Nav.Page.BUSINESSPARTNERS`, `Perm.Entity.BUSINESSPARTNERS`,
  `Help.BUSINESSPARTNERS.*`); `_LayoutTenantShell.cshtml` is unchanged (navigation comes from the manifest). Exact scope: pack §5 / §11.
- `gateway/Diten.ApiGateway/ocelot.json` — only the two `/api/mdm/business-partners` route blocks, downstream `localhost:5059`,
  no `DELETE` method (pack §15). Ownership remains with `integration-agent`.

This exception applies to no other MDM module pack.
```

## 20. Follow-up Items

- **BL-665** — aynı firmanın tedarikçi (`SUP-`), 3PL ve CRM müşterisi kayıtlarının bağlanması; eşleştirme / birleştirme / survivorship (MOD-0049 ileri FU).
- Yeni roller (tedarikçi, müşteri, fason üretici MOD-0197, taşıyıcı) — her biri karar + paket (§4.5).
- Değişiklik olayı yayını (`BusinessPartnerChanged` outbox) — tüketicilerin kullanıcısız güncel kalması (A-4 ile birlikte).
- Servis kimliğiyle okuma (MOD-0033-FU02) → `BUSINESS-PARTNER-READ` 1.1.0 eklemeli.
- Pasif ortakta kişi verisinin saklama süresi ve zamanlanmış temizlik — v1'de yok (Q-3 kararı); istenirse ayrı karar.
- İş ülkesi SoR'u kararı gelince `CountryCode` doğrulamasının sıkılaşması (domain-config açık kararı).
- İçe aktarma (mevcut 3PL listelerinin toplu yüklenmesi) ve toplu güncelleme.
- Tüzel kişilik bazlı görünürlük (SEC-002) — ihtiyaç doğarsa.
- MDM'nin BL-501 ortak ileticisine geçişi (merkez; FU01 komutları değişmez).

## 21. Audited Events

Servisin denetim altyapısı **bugün var**: `mdm-merkezi-iletim` yol b, işaret izi (`tests/architecture/audit-ledger/Diten.MdmService.md:12`). Yol Q-2 kararıyla (A)
yazılmıştır: yol b + aynı işlemde yerel geçmiş; X-12'de değişiklik yok.

| Komut | Olay adı | Nesne (tür) | Yol | Önceki/sonraki ya da değişen alanlar | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|
| CreateBusinessPartnerCommand | `mdm.business-partner.created` (`Operation = Create`) | BusinessPartner | b | Sonraki: Code, LegalName, ShortName, CountryCode, City, Roles, Status; `TaxNumber`, `Contacts`, `Address.AddressLine`, `Address.PostalCode`, `Address.District` yalnız "dolu" bilgisiyle adı | — |
| UpdateBusinessPartnerCommand | `mdm.business-partner.updated` (`Operation = Update`) | BusinessPartner | b | değişen alan **adları**; değer yalnız LegalName, ShortName, CountryCode, City için önce / sonra | — |
| DeactivateBusinessPartnerCommand | `mdm.business-partner.deactivated` (`Operation = Deactivate`) | BusinessPartner | b | önceki / sonraki Status, gerekçe | — |
| ActivateBusinessPartnerCommand | `mdm.business-partner.activated` (`Operation = Activate`) | BusinessPartner | b | önceki / sonraki Status, gerekçe | — |

- **v1 dışı (C-5):** `AssignBusinessPartnerRoleCommand` (`mdm.business-partner.role-assigned`, `Operation = Assign`) ve `RemoveBusinessPartnerRoleCommand`
  (`mdm.business-partner.role-removed`, `Operation = Revoke`) bu tabloya ikinci rolün paketinde eklenir.
- **Okuma satırı:** `GetBusinessPartnerExportQuery` → `BusinessPartnerRegisterExport` (`Category = DataExport`, `Operation = Export`), en iyi çaba, satır sayısı yazılmaz
  (Brand dışa aktarım emsali). AUD-001 §7 zorunluluğu **yok** (dışa aktarım kişisel veri taşımaz — AC-12); kayıt yine yazılır.
- **Kayda yazılmayanlar:** kişi adı / e-posta / telefon, adres satırı, posta kodu, vergi no değeri (AUD-001 §3) — yalnız alan adı.
- **K2 sınıfı:** hiçbir komut K2 değildir (kimlik / yetki / kiracı durumu / GxP / KVKK özel nitelikli veri yok).
- **Yerel geçmiş:** her komut `BusinessPartnerHistoryEntry`'yi aynı işlemde yazar (§4.4); bu bir yol c izi olarak **beyan edilmez** (defterde kayıtlı iz değildir), ürün
  geçmişidir ve merkezi iletimin kaybına karşı yedektir.
- Yazma olmayan: liste, tekil, geçmiş okumaları (AUD-001 §7 kapsamı dışı).

## 22. Record Codes

| Kayıt türü | Sınıf | TÜR öneki | Üretici | Gerekçe |
|---|---|---|---|---|
| İş Ortağı (`BusinessPartner.Code`) | **OTOMATİK** | `BPT` (Q-1 kararı; `.antigravity/rules/record-coding-standard.md:76`) | MOD-0040-FU02 ortak servisi, `prefixScope = Group` | ana veri yüksek hacimli değildir ama kodu tüketiciler (MOD-0179 `PartnerCode`) **kopyalar** — değişmez olmalı; elle kod (İKİSİ DE) çapraz referansları kırar. Kiracı geneli kayıt → grup öneki (`GMG-BPT-000001`) |
| İletişim kişisi, rol ataması, geçmiş satırı | — | kod yok | — | kaydın parçası |

Önek ölçümü (r1, `b3e9e6b7a`): RCS-001 §3'te, Platform kataloğunda ve depo kodunda `BPT` yoktu; `SUP-` (MOD-0140 kanonik tedarikçi kodu, `SUP-xxxxxxxx`) ve
CRM `ACC-` ile çakışmaz. RCS-001 §3 satırı kapıda yazıldı (`63f046c92`, §6); katalog DL-1'de (X-4, X-5, X-7) ve aynı commit'te X-6 satırı kalkar.
RCS-001 §6 listesi: sınıf + önek (bu tablo), ortak servis (§8.3), OTOMATİK değişmezlik API'de (AC-3), kiracı geneli tekillik (`(TenantId, Code)`), testler (eşzamanlı iki
oluşturma farklı kod — Platform servisinin kendi testi; değişmezlik AC-3; ASCII biçim AC-3).

## 23. Teslim dilimleri

| dilim | ad | kapsadığı bölümler | gerekli kanıt seviyesi | plan yetenek kimliği | plan MVP |
|---|---|---|---|---|---|
| DL-1 | Çekirdek: BUSINESS-PARTNER-READ taslağı + BusinessPartner kaydı, kod (BPT), 4 komut, okuma API'si (liste rol + durum, tekil, geçmiş, dışa aktarım), izin + manifest, kiracı, denetim, Platform kod kataloğu, Gateway rotası (MOD-0179 DL-4 kapısı) | Owned Objects, Entity Fields, BusinessPartner, BusinessPartnerContact, BusinessPartnerRoleAssignment, BusinessPartnerHistoryEntry, Roller ve genişleme noktası, Kişisel veri kuralı, Indexes, Repo Scope, Protected Paths, Dependencies, Servis ve kapsam, Durum makinesi, Kayıt kodu üretimi, Okuma sözleşmesi BUSINESS-PARTNER-READ, Hata kodları, Veri kapsamı (SEC-002), Merkezi denetim ve yerel geçmiş, Backend File Convention, Validation Rules, Failure Path to Verify, Authorization Convention, Audited Events, Record Codes | E4 | plan kimliği yok | plan satırı yok |
| DL-2 | MDM kiracı ekranı: liste, oluştur, düzenle, ayrıntı (Genel · İletişim kişileri · Roller · Geçmiş); 7 dil; yardım | Layout & Shell Contract, Frontend File Contract, Page Help (UXH-001) | E4 | plan kimliği yok | plan satırı yok |
| DL-3 | Gateway kapsama testi (integration-agent) | Gateway / API Routing Decision | E3 | plan kimliği yok | plan satırı yok |

DL-1 çekirdek dilimdir; DL-2 ve DL-3 DL-1'e dayanır. Plan kaynağı: §19.5.

## 24. Dilim kapıları

Paket onaylanmadan (künye `approved` / `ready-for-dev` + CT-APR-02 + CT-APR-01, onaylayan Ali) hiçbir dilim başlamaz. **Kapanan kapılar:** sahip kararları
(§19.7) ve `.antigravity` RCS-001 §3 `BPT` + X-6 (`63f046c92`). **Onaydan önce kalan:** X-11 domain-config satırı (CT yazar, §19.8).

| Dilim | Başlamadan kapanması gereken | Seam'ler (XMC-001) | AC | Sabotajlar |
|---|---|---|---|---|
| DL-1 | ilk adım sözleşme taslağı; X-3 port koordinasyonu (MOD-0289 / MOD-0217 DL-1 durumu ölçülür); X-4…X-6 Platform onayı; X-8 integration-agent onayı. **Kabul kapısı:** X-7 1.1.0 main'de, dev ortamı kod kimlik bilgisi + grup öneki (A-5), sözleşme 1.0.0 FROZEN (CT) | X-1, X-2, X-3, X-4, X-5, X-6, X-7, X-8 (rota) | AC-1…AC-16, AC-26 | S-1, S-2a, S-2b, S-3a, S-3b, S-3c, S-4a, S-4b, S-5, S-6a, S-6b, S-6c, S-7a, S-7b, S-8a, S-8b, S-8c, S-9, S-10a, S-10b, S-10c, S-11a, S-11b, S-12a, S-12b, S-12c, S-13, S-14, S-15, S-16, S-26a, S-26b |
| DL-2 | DL-1 kabulü; X-9 Web ortak sahibi onayı; ekran brief / mockup (merkez kuralı — yeni modül ekranı) | X-9 | AC-17…AC-24 | S-17, S-18a, S-18b, S-19, S-20a, S-20b, S-20c, S-21a, S-21b, S-22, S-23, S-24 |
| DL-3 | DL-1 kabulü (rota main'de) | Gateway test projesi | AC-25 | S-25a, S-25b |

**MOD-0179 DL-4 kapısı** = bu paketin DL-1 kabulü (AC-1…AC-16 ve AC-26, AC-15 canlı kanıt dahil) + X-8 rotası main'de + `docs/analysis/contracts/business-partner-read.md` 1.0.0 FROZEN.
DL-2 ve DL-3 MOD-0179 DL-4'ü bekletmez. Sıra: DL-1 → DL-2 ve DL-3 (paralel).

## 25. Page Help (UXH-001)

- Sayfa kodu `BUSINESSPARTNERS` (Index; Create / Edit / Details aynı kodun alt sayfaları): `Help.BUSINESSPARTNERS.Purpose` ("Dış iş ortaklarının — v1'de 3PL
  ortaklarının — tek ana kaydı; kod, ad, adres, iletişim ve roller burada tutulur, diğer modüller buradan okur"), `Help.BUSINESSPARTNERS.Audience` (ana veri
  sorumlusu; okuma: 3PL operatörü), `Step1` (ortak ekle: ticari ad, kısa ad, ülke, şehir, rol), `Step2` (iletişim kişilerini ekle — yalnız iş iletişimi), `Step3`
  (roller — rol oluşturmada verilir, v1'de değiştirilmez; ortağın kullanımını bitirmek için pasifleştirilir), `Step4` (pasifleştir — gerekçe; pasif ortak yeni bağlantıda seçilemez) + `Step{n}Title`; `Help.BUSINESSPARTNERS.Immediate`
  ("Kod otomatik verilir ve değişmez"). Kiracı modülü → 7 dil.
- İlgili sayfa: MOD-0179 `TPLHEALTH` menüde olunca `PageHelpCatalog` + `Help.BUSINESSPARTNERS.Related.TPLHEALTH` (MOD-0179'un ekranı gelmeden link çizilmez).
- **Tur yok** (beş akıştan biri değil).

## 26. Açık sorular

Beste'ye açık soru yoktur — Q-1…Q-7 kararları §19.7'de.

**Ali'ye — bilgi (T-5 izni verildi; itiraz hakkı saklı):**
- **A-1** MDM'ye eklenenler: yeni `BusinessPartner` kökü ve iki koleksiyon, 8 uç, 6 izin anahtarı (`mdm.business-partners.*`), yeni manifest modülü
  `business-partner-master`, `Program.cs` ve Persistence DI'de yalnız eklemeli satırlar (X-1, X-2); mevcut MDM testleri değişmez (AC-14).
- **A-2** Rol şablonu (Auth, ayrı WP — X-10): "3PL Operatörü" rolüne `mdm.business-partners.read` (MOD-0179 Q-13 ile birlikte), ana veri sorumlusuna yazma anahtarları.
- **A-3** Kiracı planlarına `business-partner-master` modülünün eklenmesi (Platform yönetimi; plan senkronu anahtarları verir).
- **A-4** MOD-0179'un kullanıcısız işçisinin ortak durumunu okuması servis kimliği ister (MOD-0033-FU02, MOD-0179 Q-12); FU01 v1 yalnız kullanıcı token'ıyla
  okunur; servis kimliği gelince sözleşme 1.1.0 eklemeli.
- **A-5** Ortam: Platform `TrustedRecordCode` Mdm kimlik bilgisi (dev / prod sırları) ve kiracı grup öneki hizalaması — DL-1 canlı kanıtının önkoşulu.

**CT'ye:**
- **C-1** RECORD-CODE-ALLOCATE 1.1.0 eklemeli sürümü (X-7) — DL-1 kabulünden önce.
- **C-2** `BUSINESS-PARTNER-READ` 1.0.0 dondurma adımı — DL-1 kabulünde; MOD-0179 DL-4 kapısının parçası.
- **C-3** Plan satırı yok (§19.5) — MVP-5 bağlamı ve hedef tarih sahipten.
- **C-4** X-3: MdmService kayıt kodu portunu hangi paketin açacağı (MOD-0289 / MOD-0217 / bu paket) — DL-1 başında ölçülür.
