---
id: MOD-0289
name: Bank Master & Bank Account Registry
domain: master-data-management
service: Diten.MdmService
service_port: 5059
shell: tenant
golden_reference: compact
data_mode: server
screens:
  - module: Banks
    data_mode: server
  - module: BankAccounts
    data_mode: server
entity_base: EntityBase
status: draft
approval_status: not-approved
owner: mdm-domain-team
branch: feature/mdm/mod-0289-bank-master-bank-account-registry
started: 2026-10-25
target: 2027-01-31
form_field_count: 11
slice: "TRM-S3 — banka ana verisi + şirket ve tedarikçi banka hesabı sicili + maskeleme + tüketici okuma sözleşmesi (ödeme yürütme YOK)"
---

# MOD-0289 — Bank Master & Bank Account Registry

> **Durum: `draft` (planlama belgesi), `approval_status: not-approved`.** Bu paket kod, servis değişikliği, gateway
> rotası, sözleşme dosyası ya da kaynak projeden kod alma izni vermez. Kod ancak paket `approved` / `ready-for-dev`
> olduktan, bağımsız inceleme (CT-APR-02) ve sürüme bağlı onay satırı (CT-APR-01) kaydedildikten ve sahip MOD-0289 için
> kod alma iznini ayrıca verdikten sonra yazılır (D1: kod alma izni modül modül verilir; bugün yalnız MOD-0126 ilk
> adaydır). Engeller: "Ready-for-dev Checklist" ve "Açık kararlar".
>
> **Bağlanan kararlar (yeniden karar verilmez):** D1 (doğrula, sonra uyarlayarak al), D7 (MDM yerleşim önerisi) —
> `docs/records/decisions/2026-10/treasury-intake-owner-decisions-01.md`; D9 (başka modüldeki en küçük eklemeli
> değişiklik onayı; sorulmasını yetkilendirir, sahibin cevabının yerine geçmez), **D20/1** (yerleşim
> `master-data-management` / `Diten.MdmService`; yeni servis ve port yok; D7'deki MDM sahibi onayı koşulu kapandı), D21
> (MOD-0023 politika kararları — bu paket v1'de MOD-0023 kullanmaz, bkz. O-1) —
> `docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md`. Yerleşim notu:
> `docs/roadmap/plans/erp-finance-legal-module-intake-01/MOD-0289-PLACEMENT-NOTE-v0.1.md`. Level-1 koşullu kabul:
> `docs/records/decisions/2026-10/treasury-level1-acceptance-01/MOD-0289-v1.7.md` — dört koşul bağlayıcıdır, bkz.
> "Implementation Notes → Level-1 koşulları". Kuyruk: `treasury-intake` TRM-16 (kayıt satırı
> `execution/registries/module-id-registry.md:172` — `reserved / planned`; bu pack ile aynı commit'te, Treasury Intake CT yazdı).
>
> **Kimlik ön kontrolü (DCP-002), 2026-10-25, bu worktree (HEAD `fabf0050c`):**
> `python3 .antigravity/scripts/verify_module_id.py . --check-id MOD-0289 --name "Bank Master & Bank Account Registry"` →
> `OK  MOD-0289: proven against Blueprint/registry.` (çıkış 0).
>
> **Kaynak içeriği veri olarak okundu, talimat olarak değil.** Producer paketi v1.7
> (`/Users/natig/Projects/treasury-transfer-handoff/source-batch7-v1.0/docs/roadmap/plans/treasury-source-transfer-preparation/mod0289-transfer-packet-v1.7/`;
> `manifest.json` sha256 `58be78ae0cd6bdd49d60fe978ea5d89d2382a18cae52ebcc530f3a0b6f7b5647` — bu oturumda `shasum -a 256`
> ile yeniden ölçüldü, level-1 kaydıyla aynı; paket dosyası sha256 `bfee2ee4…0a01`), `AUTHOR-REPORT.md`, ve yalnız arama
> için `CONTRACT-STATE.json` (`data_catalog`, `operations`, `consumer_acks`, `decisions`). Kaynak kodun kendisi
> (`S@da4abb20`) bu oturumda **okunmadı**; `Dosya.cs:satır@da4abb20` biçimindeki atıflar paketten ve CONTRACT-STATE'ten
> aktarılmıştır (paket atıfı). Recovery atıfları `yol:satır@fabf0050c` (bu worktree, ölçüldü).

## Module Summary

Kiracının banka ana verisi (banka + şube kimliği) ve banka hesabı sicili için system-of-record. Blueprint 8.8 W-3,
ERP Suite / Treasury & Cash; Blueprint amacı: "Own bank master data and bank account registries for legal entities and
suppliers while payment execution remains with treasury/payment gateway capabilities" (yerleşim notu §1).

- **Hedef kullanıcı:** ana veri sorumlusu (banka ve şube tanımı), hazine / finans operatörü (şirket hesabı açar),
  tedarikçi ana veri sorumlusu (tedarikçi hesabı açar), hesap onaylayıcı (dört göz aktivasyonu, O-1), sınırlı sayıda
  "maskesiz görme" yetkilisi (O-3).
- **Çekirdek:** banka hesabı **hassas veridir** ve bu modül onun tek kaydıdır. Tam hesap tanımlayıcısı (IBAN ya da yurt
  içi hesap numarası) yalnız şifreli saklanır; listede, DTO'da, günlükte (log) ve denetim yükünde yalnız maskeli gösterim
  ve son dört hane bulunur; maskesiz okuma ayrı izin + gerekçe ister ve her okuma kayda geçer.
- **Tüketiciler** (MOD-0126, MOD-0119, MOD-0120, MOD-0124, ileride MOD-0269) yalnız **opak Guid** hesap referansı tutar
  ve uygunluğu bu modülün `lookup-validation` okuma sözleşmesinden sorar (MOD-0220 LEGAL-ENTITY-LOOKUP deseni).
- **Ödeme yürütme yok.** Bankaya mesaj, token, dağıtım, banka geri çağrısı bu modülde yoktur (MOD-0126 / MOD-0269, D5).

## Ownership and Boundaries

- **In-scope (v1):** `Bank` ve `BankBranch` ana kaydı (oluştur, düzenle, aktive et, pasifleştir, taslak silme); tek
  `BankAccount` sicili — sahibi **tam olarak bir** taraftır: şirket (MOD-0220 `LegalEntity`, aynı servis, süreç içi) ya
  da tedarikçi (MOD-0140, Gateway üzerinden sözleşme); IBAN / BIC biçim kuralları (dış doğrulama çağrısı yok); şifreli
  saklama, maskeleme, maskesiz okuma ve okuma kaydı; veri kapsamı (SEC-002); tüketici okuma sözleşmesi
  `BANK-ACCOUNT-LOOKUP` (bu pakette tanımlanır, dosyası sonra yazılır); kiracı ekranları (Banks, BankAccounts).
- **Out-of-scope (v1):** ödeme yürütme, banka token'ı / yönlendirme token'ı, banka geri çağrısı, ekstre (MOD-0269, D5);
  ödeme partisi ve onayı (MOD-0126); toplu içe aktarma (kaynak import job / row / issue — follow-up); değişiklik talebi +
  MOD-0023 onay akışı (O-1 önerisi: v1'de yok); varsayılan hesap ataması, istisna vakaları, kanıt paketi / vekâlet
  (mandate), ülke-ödeme kanalı doğrulama kuralı anlık görüntüleri, doğrulama koşuları ve muafiyet talepleri (hepsi
  follow-up); müşteri banka hesabı (kaynakta CUSTOMER slotu sahipsiz — §P; kimlik uydurulmaz); ülke ve para birimi
  referans listelerinin sahipliği (MDM ülke kararı domain-config "Country Boundary"; MOD-0048).
- **Sahip olunan sözleşme:** `BANK-ACCOUNT-LOOKUP` 1.0.0 (Runtime Constraints → "Okuma sözleşmesi"), x-owner MOD-0289;
  dosya `docs/analysis/contracts/drafts/bank-account-lookup-validation-v1.0-proposal.md` olarak **follow-up**'ta yazılır,
  bu pakette yazılmaz. Kaynakta resmi sözleşme yoktur (paket §B "ABSENT").
- **Tüketilen sözleşmeler / yapılar:** MOD-0220 `LegalEntity` (aynı servis — depo üzerinden süreç içi okuma; HTTP
  değil); MOD-0140 `SUPPLIER` `validateSuppliers` (1.3.0 hedefi — bugün yok, Dependencies); MOD-0048 para birimi listesi;
  RCS-001 ortak kod servisi; SEC-002 veri kapsamı (MDM'de var olan `ITrustedLegalEntityScopeProvider` adayı); MDM'nin
  denetim izi (`mdm-guvenilir-kaynak-niyeti`, Audited Events).
- **Domain-config ile ilişki:** MDM domain-config'te MOD-0289 "In-Scope Reserved Module" listesinde yok ve frontend /
  gateway istisnası yalnız MOD-0290-FU02 için yazılı (`execution/domains/master-data-management/domain-config.md` "This
  exception applies to no other MDM module pack"). Yetki sırası gereği module pack domain-config'in üstündedir, ama bu
  paket domain-config'i değiştirmez; domain-config satırı ve MOD-0289 için frontend/gateway kapsam cümlesi follow-up'tır
  ve Ready-for-dev engelidir.

## Owned Objects

### Varlıklar (kaynak 25 varlık sınıfı → Recovery karşılığı)

Kaynak: CONTRACT-STATE `data_catalog.types`, `BankMasterEntities.cs@da4abb20` (paket atıfı); hepsinin tabanı
`TenantScopedEntity` (Platform).

| # | Kaynak sınıf (`BankMasterEntities.cs:satır`) | Recovery | Gerekçe |
|---|---|---|---|
| 1 | `BankMasterDraft` (:8) | **taşınır, uyarlanır** — `Bank : EntityBase` | çekirdek; "Draft" adı tipten çıkar, durum alanda kalır |
| 2 | `BankBranchDraft` (:23) | **taşınır, uyarlanır** — `BankBranch : EntityBase` | çekirdek |
| 3 | `LegalEntityBankAccountDraft` (:41) | **birleşir** → `BankAccount` (`OwnerType = LegalEntity`) | tek sicil, tek kural seti; iki ayrı tip iki ayrı kusur doğurur |
| 4 | `SupplierPaymentDestinationDraft` (:265) | **birleşir** → `BankAccount` (`OwnerType = Supplier`) | aynı |
| 5 | `BankAccountOwnershipBinding` (:236) | **taşınmaz** — sahip alanı hesabın içinde | kaynakta hesap + bağ iki belge, tek oturum işlemi (PAIR) gerekiyordu ve testleri ürün koduna hiç ulaşmadı (paket §H); tek belgede bağ ayrışamaz |
| 6 | `SupplierPaymentDestinationOwnershipBinding` (:308) | **taşınmaz** | aynı |
| 7 | `SensitiveBankDataAccessLog` (:544) | **uyarlanır** — `BankAccountSensitiveRead` (yalnız ekleme) | maskesiz okumanın izi; değer döndürülmeden önce yazılır (önce-yaz) |
| 8 | `BankAccountToken` (:510) | **taşınmaz** | tüketiciye token değil opak Guid + `lookup-validation` verilir; token ödeme yürütmenin parçasıdır (MOD-0269, D5) |
| 9 | `BankAccountOperationalOutboxItem` (:214) | **taşınmaz** | `TenantId = Guid.Empty` (`Mod0289BankService.cs:2450@da4abb20`, level-1 koşul 2) ve hiçbir kod `Published` atamıyor (paket §F) |
| 10 | `BankAccountOperationalJobRun` (:189) | **taşınmaz** | yalnız 9'u üreten işler |
| 11–13 | `BankAccountImportJob` (:107), `BankAccountImportRow` (:128), `BankAccountImportRowIssue` (:166) | **v1 dışı** (follow-up) | toplu içe aktarma ayrı dilim; RCS-001 §5.11 içe aktarma kuralı ister |
| 14–15 | `BankAccountChangeRequest` (:337), `BankAccountChangeRequestActionLog` (:390) | **v1 dışı** (O-1) | MOD-0023 onay akışına bağlı; v1 önerisi dört göz aktivasyonu |
| 16 | `BankAccountDefaultAssignment` (:430) | **v1 dışı** | varsayılan hesap seçimi tüketici kuralıdır; ayrı karar |
| 17–18 | `BankAccountExceptionCase` (:459), `BankAccountExceptionEvidenceLink` (:496) | **v1 dışı** | istisna kuyruğu ve kanıt bağlama (MOD-0031) |
| 19–21 | `CountryRailValidationRuleSnapshot` (:572), `AccountValidationRun` (:591), `AccountValidationResult` (:612) | **v1 dışı** | v1 kuralları koddaki biçim kurallarıdır (Validation Rules); ülke / kanal kural seti referans veri sahibi ister (MOD-0048, kaynak REF slotu) |
| 22–25 | `BankAccountEvidencePack` (:636), `BankAccountEvidenceLink` (:658), `BankAccountMandate` (:676), `ValidationOverrideRequest` (:697) | **v1 dışı** | kanıt / vekâlet / muafiyet; O-4 (tedarikçi hesabı doğrulama kanıtı) |

### Kaynak işlemleri (78 satır, `CONTRACT-STATE.json.operations`) → Recovery

| Kaynak (`/api/v1/mod0289/…`) | Recovery komut / sorgu | Recovery uç (Gateway `/api/mdm/…`) | Karar |
|---|---|---|---|
| API-001 `GET health`, API-061 `GET health-dashboard` | — | — | **taşınmaz** (servis sağlık ucu servisin kendisindedir) |
| API-002 `GET banks`, API-004 `GET banks/{id}` | `GetBankListQuery`, `GetBankByIdQuery` | `GET /api/mdm/banks`, `GET /api/mdm/banks/{id}` | taşınır |
| API-003 `POST banks` | `CreateBankCommand` | `POST /api/mdm/banks` | taşınır |
| API-005 `GET banks/{id}/branches`, API-007 `GET branches/{id}` | `GetBankBranchListQuery`, `GetBankBranchByIdQuery` | `GET /api/mdm/banks/{bankId}/branches`, `GET /api/mdm/bank-branches/{id}` | taşınır |
| API-006 `POST banks/{id}/branches` | `CreateBankBranchCommand` | `POST /api/mdm/banks/{bankId}/branches` | taşınır |
| API-008…011 LE hesap oluştur / liste / detay / şirkete göre liste | `CreateBankAccountCommand` (`OwnerType = LegalEntity`), `GetBankAccountListQuery` (süzgeç `ownerType`, `legalEntityId`), `GetBankAccountByIdQuery` | `POST/GET /api/mdm/bank-accounts`, `GET /api/mdm/bank-accounts/{id}` | **birleşir** |
| API-029…032 tedarikçi hedef hesabı oluştur / liste / detay / tedarikçiye göre liste | aynı komut / sorgular (`OwnerType = Supplier`, süzgeç `supplierId`) | aynı uçlar | **birleşir** |
| API-012 `POST …/activation` | `ActivateBankAccountCommand` | `POST /api/mdm/bank-accounts/{id}/activate` | taşınır + dört göz (O-1) |
| API-033…035 token oku / yeniden ver / iptal | — | — | **taşınmaz** (token yok) |
| API-036 `GET sensitive-access-logs` | `GetBankAccountSensitiveReadListQuery` | `GET /api/mdm/bank-accounts/{id}/sensitive-reads` | uyarlanır (hesap başına) |
| API-013…028 doğrulama koşusu, kural anlık görüntüsü, kanıt, muafiyet, vekâlet, doğrulama muafiyeti | — | — | v1 dışı (follow-up) |
| API-037…048 değişiklik talebi + iş akışı eylemleri | — | — | v1 dışı (O-1) |
| API-049…051 varsayılan atama | — | — | v1 dışı |
| API-052…060 istisna vakaları | — | — | v1 dışı |
| API-062…070 içe aktarma | — | — | v1 dışı |
| API-071…075 operasyon işleri ve outbox | — | — | **taşınmaz** (Guid.Empty outbox) |
| API-076 `PATCH …/direct-mutation` | — | — | **taşınmaz** (kontrolsüz doğrudan değişiklik yolu) |
| F02-CREATE, F02-RESOLVE (`…/legal-entity-accounts/transactions`) | — | — | **taşınmaz** (F02 pilotu, makbuz ve "fail-closed stub" uygunluk; level-1 koşul 1) |

### Recovery ekleri (kaynakta yok)

| Komut / sorgu | Uç | Neden |
|---|---|---|
| `UpdateBankCommand`, `ActivateBankCommand`, `DeactivateBankCommand`, `DeleteBankCommand`, `BulkDeleteBankCommand` | `PUT /api/mdm/banks/{id}`, `POST …/{id}/activate`, `POST …/{id}/deactivate`, `DELETE …/{id}`, `DELETE /api/mdm/banks/bulk` | kaynakta `Inactive` atayan kod yok (paket §E); Golden Reference silme seti — yalnız hiç aktive edilmemiş taslak |
| `UpdateBankBranchCommand`, `ActivateBankBranchCommand`, `DeactivateBankBranchCommand`, `DeleteBankBranchCommand` | `PUT/DELETE /api/mdm/bank-branches/{id}`, `POST …/{id}/activate`, `POST …/{id}/deactivate` | aynı |
| `UpdateBankAccountCommand`, `DeactivateBankAccountCommand`, `DeleteBankAccountCommand`, `BulkDeleteBankAccountCommand` | `PUT /api/mdm/bank-accounts/{id}`, `POST …/{id}/deactivate`, `DELETE …/{id}`, `DELETE /api/mdm/bank-accounts/bulk` | kaynakta API delete yok (paket §F); silme yalnız taslak |
| `RevealBankAccountIdentifierCommand` | `POST /api/mdm/bank-accounts/{id}/unmasked-reads` | maskesiz okuma: gerekçe gövdede, okuma kaydı yazılır (yan etkili olduğu için `GET` değil) |
| `ValidateBankAccountReferenceQuery` | `GET /api/mdm/bank-accounts/{id}/lookup-validation` | tüketici okuma sözleşmesi (`BANK-ACCOUNT-LOOKUP`) |

- **DTO'lar (tek dosya, Golden Reference):** `BankModels.cs` (`BankListItemDto`, `BankDetailDto`, `BankWriteRequest`,
  `BankBranchDto`, `BankBranchWriteRequest`) ve `BankAccountModels.cs` (`BankAccountListItemDto`, `BankAccountDetailDto`,
  `BankAccountWriteRequest`, `BankAccountRevealRequest`, `BankAccountRevealDto`, `BankAccountSensitiveReadDto`,
  `BankAccountLookupDto`). DTO'larda ve isteklerde `TenantId` **yoktur**. `BankAccountRevealDto` dışındaki hiçbir DTO tam
  tanımlayıcı taşımaz.
- **Portlar (Application):** `ISupplierEligibilityPort` (MOD-0140), `ICurrencyReferencePort` (MOD-0048),
  `IRecordCodePort` (RCS-001), `IBankAccountIdentifierProtector` (şifreleme + anahtarlı özet, O-6),
  `IBankAccountDataScopeSource` (SEC-002, O-9). Şirket sahibi için port yok: `ILegalEntityRepository` süreç içi okunur
  (aynı servis).
- **Frontend rotaları:** `/MasterData/Banks` ve `/MasterData/BankAccounts` (+ `/Create`, `/Edit/{id}`, `/Details/{id}`).

### Durumlar

`BankLifecycleStatus` (Bank, BankBranch, BankAccount ortak; kaynak `BankMasterLifecycleStatus` Draft=1/Active=2/Inactive=3,
`BankMasterEnums.cs:3-8@da4abb20` — değerler korunur):

| Durum | Değer | Anlam | Giriş | Çıkış |
|---|---|---|---|---|
| Draft | 1 | taslak; tüketiciye görünmez (`lookup-validation` 404) | oluşturma | Update, Activate, Delete |
| Active | 2 | kullanılabilir; tüketiciye görünür | Activate | Deactivate, sınırlı Update |
| Inactive | 3 | pasif; yeni kullanım kapalı (`lookup-validation` 404); kayıt ve kodu kalır | Deactivate | Activate (yeniden aktive etme, aynı kurallar) |

- Kaynakta ayrı `ApprovalStatus` (Draft/NotSubmitted/Submitted/Approved/Rejected), `PaymentEligibilityStatus`,
  `OwnerValidationStatus`, `ValidationState` boyutları vardı (`BankMasterEnums.cs:10-40@da4abb20`); kaynak hata sözlüğünde
  `MOD0289_STATE_DIMENSION_CONFLICT` bulunması bu boyutların çatışabildiğini gösterir. Recovery v1 **tek durum
  boyutu** kullanır; onay boyutu O-1'e bağlıdır, sahip uygunluğu saklanmaz, her okumada güncel olarak hesaplanır.
- **Durum türetilmez:** Bank `Inactive` olursa şubeleri ve hesapları otomatik değişmez; yeni hesap açma ve
  `lookup-validation` bankanın güncel durumuna bakar (Runtime Constraints).

## Entity Fields

Taban: MDM'nin `EntityBase`'i (`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/EntityBase.cs:3-12@fabf0050c`):
`Id`, `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt`, `Version` — tekrar tanımlanmaz. İş kaydı olduğu
için `CreatedBy` / `UpdatedBy` (`Guid?`, token `sub`'ından; DTO'dan değil) **zorunlu**. Kaynak string aktör kimlikleri
(`RequestedBy`, `IssuedBy`, `ActorRef` …, CONTRACT-STATE) taşınmaz. Kaynak Guid'leri BSON string olarak saklıyordu
(`BankMasterEntities.cs:52,55@da4abb20`, paket §E); Recovery MDM'nin genel `GuidSerializer(Standard)` kaydını kullanır
(`services/Diten.MdmService/src/Diten.MdmService.Persistence/Configurations/BrandProductClassMaps.cs:18@fabf0050c` yorumu).

**Kiracı:** `TenantId` depo tabanında sunucuda yazılır (`RepositoryBase.cs:31,42`) ve her okuma `TenantFilter` (TenantId +
`IsDeleted = false`) ile süzülür (`RepositoryBase.cs:69-71`) — yol önekleri
`services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/`, `@fabf0050c`. Gövdede / sorguda `tenantId`
gelirse 400. `GlobalEntity` kullanılmaz: banka kataloğu da kiracıya aittir (her kiracı kendi banka listesini tutar;
kiracılar üstü ortak banka kataloğu v1 dışı).

**Soft delete:** taban `DeleteAsync` `IsDeleted` + `DeletedAt` + `UpdatedAt` yazar (`RepositoryBase.cs:54-67@fabf0050c`).
**Dikkat (ölçüldü):** taban `UpdateAsync` `Version`'ı artırır ama süzgeçte sürüm yoktur (`RepositoryBase.cs:40-52`) —
iyimser eşzamanlılık sağlamaz. Bu modül `LegalEntityRepository.UpdateEditableFieldsAsync` desenini (süzgeçte
`Version == expectedVersion`, `LegalEntityRepository.cs:33-44@fabf0050c`) kullanır; taban `UpdateAsync` çağrılmaz.

### Bank (`mdm_banks`)

| Alan | Tip | Kural | Kaynak |
|---|---|---|---|
| Code | string | RCS-001 OTOMATİK; tür öneki önerisi `BNK`, şirket öneki yerine grup öneki (kiracı geneli kayıt, RCS-001 §5.3) — O-7; istekte gelirse 400; silinen kaydın kodu yeniden kullanılmaz | kaynak `BankCode` kullanıcıdan alınıyordu (`CreateBankDraftRequest`, `Mod0289Contracts.cs:5@da4abb20`) |
| Name | string | zorunlu, trim, 1..200 | `BankName` |
| CountryCode | string(2) | zorunlu, ISO 3166-1 alfa-2, büyük harf; kaynak liste O-8 | `CountryCode` |
| Bic | string? | ISO 9362; 8 ya da 11 karakter; Validation Rules | `SwiftBic` |
| NationalBankCode | string? | trim, 1..35; ülke içi banka kodu (ör. EFT kodu, BLZ, sort code) | yeni |
| Status | `BankLifecycleStatus` | Durumlar | `Status` |
| CreatedBy / UpdatedBy / ActivatedBy / DeactivatedBy | Guid? | token `sub` | — |
| ActivatedAt / DeactivatedAt | DateTimeOffset? | UTC | — |
| DeactivationReasonCode | string? | Deactivate'te zorunlu, 1..64 | — |
| AuditIntents / AuditIntentReceipts | `List<LocalAuditIntent>` / `List<LocalAuditIntentReceipt>` | `IAuditIntentAggregate` (Audited Events) | MDM deseni |

### BankBranch (`mdm_bank_branches`)

| Alan | Tip | Kural | Kaynak |
|---|---|---|---|
| BankId | Guid | zorunlu; aynı kiracıda var olan, silinmemiş banka | `BankId` |
| BranchCode | string | zorunlu, trim, 1..35; bankada tekil | `BranchCode` |
| Name | string | zorunlu, 1..200 | `BranchName` |
| CountryCode | string(2) | zorunlu; bankanın ülkesine eşit (öneri) | `CountryCode` |
| City | string? | ≤ 100 | `City` |
| Bic | string? | 11 karakterli şube BIC'i; ilk 8 karakter bankanın BIC'ine eşit (banka BIC'i varsa) | yeni |
| Status, CreatedBy, UpdatedBy, …, AuditIntents | — | Bank ile aynı | — |

### BankAccount (`mdm_bank_accounts`)

| Alan | Tip | Kural | Kaynak |
|---|---|---|---|
| Code | string | RCS-001 OTOMATİK; tür öneki önerisi `BAC`; şirket hesabında şirket kısa kodu öneki, tedarikçi hesabında grup öneki (O-7) | yeni |
| OwnerType | enum `LegalEntity`=1, `Supplier`=2 | zorunlu; **oluşturulduktan sonra değişmez** | kaynakta iki ayrı tip + `OwnerType` string (`:236,308`) |
| LegalEntityId | Guid? | `OwnerType = LegalEntity` ise zorunlu, değilse boş; değişmez | `LegalEntityId` (:41) |
| SupplierId | string? | `OwnerType = Supplier` ise zorunlu, değilse boş; MOD-0140 sözleşmesindeki `supplierId` **olduğu gibi** (string, sıralı karşılaştırma; ölçüldü: `validateSuppliers` kimlikleri string ve `StringComparer.Ordinal` ile eşler, `services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Features/Supplier/Handlers/QueryHandlers/ValidateSuppliersHandler.cs:22-29@fabf0050c`); trim, 1..64; değişmez | kaynak `SupplierId : Guid` (:265) — sözleşme string'dir |
| BankId / BranchId | Guid / Guid? | aynı kiracıda; şube verilmişse o bankaya ait | `BankId`, `BranchId` |
| CurrencyCode | string(3) | zorunlu, `^[A-Z]{3}$`, MOD-0048 listesinde; Active hesapta değişmez | `CurrencyCode` |
| AccountIdentifierType | enum `Iban`=1, `Domestic`=2 | zorunlu | yeni |
| AccountIdentifierCiphertext | string | tam tanımlayıcının şifreli hâli + anahtar sürümü (O-6); **düz metin hiçbir alanda yok** | kaynak `ProtectedAccountRef` (dış referans modeli — taşınmaz, Recovery'de kasa yok) |
| AccountIdentifierKeyId | string | şifreleme anahtarının sürüm kimliği (anahtar döndürme için) | yeni |
| AccountIdentifierHash | string | normalize tanımlayıcının **anahtarlı** özeti (HMAC-SHA256, ayrı anahtar); tekillik ve arama için; düz SHA-256 değil (IBAN uzayı küçük, düz özet tersine çevrilebilir) | kaynak `FingerprintHash` (yalnız import satırında) |
| AccountLast4 | string(4) | sunucu türetir (normalize tanımlayıcının son 4 karakteri); istemciden alınmaz | kaynak `AccountLast4` istemciden alınıp uyumsuzlukta reddediliyordu (paket §E) |
| MaskedDisplay | string | sunucu türetir: IBAN → ülke kodu + `••••` + son 4 (ör. `TR••••1234`); yurt içi → `••••` + son 4 | kaynak `MaskedAccountDisplay` varsayılan `"MASKED"` |
| AccountHolderName | string | zorunlu, trim, 1..140 (ISO 20022 ad uzunluğu önerisi) | `AccountHolderName` / `BeneficiaryName` |
| AccountPurpose | string | zorunlu, 1..64; değer listesi O-10 (öneri: `OPERATING`, `PAYROLL`, `COLLECTION`, `TAX`, `SUPPLIER_PAYMENT`, `OTHER`) | `AccountPurpose` (≤ 120, serbest) |
| PaymentUsage | string? | ≤ 120 | `PaymentUsage` |
| DisplayName | string? | ≤ 120; kullanıcı etiketi | `AccountDisplayName` / `DestinationLabel` |
| Status | `BankLifecycleStatus` | Durumlar | `LifecycleStatus` |
| CreatedBy, UpdatedBy, ActivatedBy, ActivatedAt, DeactivatedBy, DeactivatedAt, DeactivationReasonCode | — | Bank ile aynı | — |
| ActivationVerificationCode | string? | tedarikçi hesabı aktivasyonunda zorunlu doğrulama kodu (O-4 önerisi, ör. `CALLBACK_CONFIRMED`, `BANK_LETTER_ON_FILE`) | yeni |
| AuditIntents / AuditIntentReceipts | — | `IAuditIntentAggregate` | MDM deseni |

**Taşınmayan hesap alanları:** `OwnerValidationStatus`, `OwnerValidationDetail`, `OwnerOperationalStatus`,
`OwnerApprovalStatus` (sahip durumu kayıt anındaki kopya olarak saklanmaz — paket §G "a stored snapshot, not current",
matris C11), `PaymentEligibilityStatus`, `ApprovalStatus`, `ValidationState`, `LastValidationRunId`, `EvidencePackId`,
`MandateId`, `LastOverrideRequestId`, `SubmitReady`, `SourceSystem`, `SourceRecordRef`, `Provenance`,
`DataQualityStatus`, `ProtectedAccountRef`, `CorrelationId` (korelasyon denetim niyetinde taşınır).

> **`form_field_count: 11` → `golden_reference: compact`.** BankAccount oluştur/düzenle formundaki kullanıcı alanları:
> OwnerType, LegalEntityId ya da SupplierId (tek alan, türüne göre seçici), BankId, BranchId, CurrencyCode,
> AccountIdentifierType, AccountIdentifier (yalnız oluştururken; düz metin istek alanı, saklanmaz), AccountHolderName,
> AccountPurpose, PaymentUsage, DisplayName = 11. **Sayılmayanlar:** Code (RCS-001 OTOMATİK), AccountLast4 /
> MaskedDisplay / Hash / Ciphertext (sunucu), Status, ActivationVerificationCode (Activate diyaloğunda, form alanı değil),
> TenantId. 11 > 8 → Compact (ayrı Create / Edit / Details).
>
> **Banks ekranı:** Bank formunda 4 kullanıcı alanı var (Name, CountryCode, Bic, NationalBankCode) — tek başına Slim'e
> düşer. Paket tek `golden_reference` taşır; öneri Banks ekranının da Compact olması (Details sayfasında şube alt
> tablosu ve şube oluştur/düzenle; Slim offcanvas alt tablo taşımaz). Karar O-11.

### BankAccountSensitiveRead (`mdm_bank_account_sensitive_reads`, yeni — yalnız ekleme)

| Alan | Tip | Kural |
|---|---|---|
| BankAccountId | Guid | — |
| ActorId | Guid | token `sub` |
| ReasonCode | string | zorunlu, 1..64; değer listesi O-3 içinde |
| ReasonComment | string? | ≤ 500; tanımlayıcı içeremez (sunucu, normalize tanımlayıcının yorumda geçmediğini kontrol eder) |
| OccurredAt | DateTimeOffset | UTC |
| Outcome | enum `Revealed`=1 | yalnız başarılı okuma kaydı; reddedilen deneme merkezi denetimde de görünmez (G-AUD-1 benzeri boşluk, Audited Events) |
| AuditIntents / AuditIntentReceipts | — | merkezi günlüğe iletim (okuma denetimi merkezi günlüğe gider, AUD-001 §7) |

Depo arayüzünde `Update`, `Delete`, `Replace` yoktur (AUD-001 §4.1).

### Mongo indeksleri

| İndeks | Anahtar | Tekil | Not |
|---|---|---|---|
| `ux_mdm_bank_code` | TenantId + Code | evet, **kısmi filtre yok** (silinen kaydın kodu dolu sayılır, RCS-001 §6) | — |
| `ux_mdm_bank_bic` | TenantId + Bic | evet, kısmi: `Bic` var ve `IsDeleted = false` | kaynak `MOD0289_SWIFT_BIC_CONFLICT` |
| `ux_mdm_bank_national_code` | TenantId + CountryCode + NationalBankCode | evet, kısmi: `NationalBankCode` var ve `IsDeleted = false` | kaynak `MOD0289_BANK_CODE_CONFLICT` |
| `ux_mdm_bank_branch_code` | TenantId + BankId + BranchCode | evet, kısmi `IsDeleted = false` | kaynak `MOD0289_BRANCH_CODE_CONFLICT` |
| `ux_mdm_bank_account_code` | TenantId + Code | evet, kısmi filtre yok | — |
| `ux_mdm_bank_account_identifier` | TenantId + AccountIdentifierHash | evet, kısmi `IsDeleted = false` | aynı hesap kiracıda iki kez (iki sahiple) kayıt olamaz |
| `ix_mdm_bank_account_owner_le` | TenantId + LegalEntityId + Status + CreatedAt(desc) | — | liste ve veri kapsamı süzgeci |
| `ix_mdm_bank_account_owner_supplier` | TenantId + SupplierId + Status | — | — |
| `ix_mdm_bank_account_sensitive_read` | TenantId + BankAccountId + OccurredAt(desc) | — | — |

İndeks kurulumu `mongo-indexing` kuralına göre (MDM'de depo kurucusunda `EnsureIndexes`, örn.
`LegalEntityRepository.cs:16@fabf0050c`, ve `MongoIndexReadiness.cs`); kaynak veri taşınmaz (kaynakta kurulu şema ya da
göç yok — paket §L.12).

### Hata sözlüğü (öneri; kaynak 70 `MOD0289_*` literali, CONTRACT-STATE `error_code_vocabulary` — kopyalanmaz)

`MDM_BANK_NOT_FOUND` 404, `MDM_BANK_BRANCH_NOT_FOUND` 404, `MDM_BANK_ACCOUNT_NOT_FOUND` 404 (başka kiracı, başka şirket
kapsamı ve silinmiş dahil — varlık sızdırılmaz), `MDM_BANK_VALIDATION_FAILED` 400, `MDM_BANK_CODE_FORBIDDEN` 400 (istekte
kod), `MDM_BANK_BIC_CONFLICT` 409, `MDM_BANK_NATIONAL_CODE_CONFLICT` 409, `MDM_BANK_BRANCH_CODE_CONFLICT` 409,
`MDM_BANK_BRANCH_BANK_MISMATCH` 400, `MDM_BANK_ACCOUNT_DUPLICATE` 409 (diğer sahibi ifşa etmez),
`MDM_BANK_VERSION_CONFLICT` 409, `MDM_BANK_STATE_INVALID` 409, `MDM_BANK_ACCOUNT_IMMUTABLE_FIELD` 409,
`MDM_BANK_ACCOUNT_OWNER_NOT_ELIGIBLE` 422, `MDM_BANK_ACCOUNT_OWNER_UNAVAILABLE` 503 (MOD-0140 erişilemez / geçersiz yanıt),
`MDM_BANK_INACTIVE` 422 (pasif banka/şubeye hesap), `MDM_BANK_ACCOUNT_FOUR_EYES_REQUIRED` 403,
`MDM_BANK_ACCOUNT_REVEAL_REASON_REQUIRED` 400, `MDM_BANK_CURRENCY_UNKNOWN` 400, `MDM_BANK_CURRENCY_REFERENCE_UNAVAILABLE`
503, `MDM_BANK_RECORD_CODE_UNAVAILABLE` 503, `MDM_BANK_AUDIT_UNAVAILABLE` 503 (denetim niyeti yazılamadı; iş verisi yok),
`MDM_BANK_PROTECTION_UNAVAILABLE` 503 (şifreleme anahtarı erişilemez). Kullanıcıya giden ileti resx anahtarıdır.

## Repo Scope

Hepsi kod alma izninden (D1) ve `ready-for-dev`'den sonra; dilim sütunu "Teslim dilimleri" ile eşleşir.

- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/{Bank,BankBranch,BankAccount,BankAccountSensitiveRead}.cs`,
  `Domain/Enums/{BankLifecycleStatus,BankAccountOwnerType,BankAccountIdentifierType}.cs`,
  `Domain/Repositories/I{Bank,BankBranch,BankAccount,BankAccountSensitiveRead}Repository.cs` (DL-1 / DL-2)
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/BankMaster/**` (Bank + BankBranch) ve
  `Features/BankAccount/**` (Golden Reference Compact düzeni) (DL-1 / DL-2 / DL-3)
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/{Bank,BankBranch,BankAccount,BankAccountSensitiveRead}Repository.cs` (DL-1 / DL-2)
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/{BankData,Suppliers,Currency,RecordCodes}/**` (şifreleme
  koruyucusu, MOD-0140 bağdaştırıcısı, MOD-0048 bağdaştırıcısı, RCS-001 portu) (DL-1 / DL-2 / DL-3)
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/{BanksController,BankBranchesController,BankAccountsController}.cs`
  ve `Api/ModuleRegistration/BankMasterManifestProvider.cs` (izin + sayfa manifesti, `BrandProductMasterManifestProvider.cs` deseni) (DL-1…DL-3)
- **Denetim izinin genişletilmesi (DL-1):** `Domain/Enums/AuditAggregateType.cs` (ekleme: `Bank`, `BankBranch`, `BankAccount`,
  `BankAccountSensitiveRead`), denetim işlem enum'una ekleme (O-5: ürün adlı `ProductAuditOperation`'a mı yoksa genel bir
  enum'a mı), `Persistence/Repositories/AuditIntentDeliveryRepository.cs` koleksiyon listesi (bugün sabit dokuz koleksiyon,
  `:15-23@fabf0050c`) — hepsi yalnız ekleme
- `services/Diten.MdmService/tests/**` (DL-1…DL-3)
- `tests/architecture/audit-ledger/Diten.MdmService.md` — `mdm-guvenilir-kaynak-niyeti` izinin belirtecine yeni yazma
  üyeleri + aynı değişiklikte `tests/architecture/audit-ledger/CT-DECISIONS.md` satırı ve `AuditTrailStandardTests`
  sabitleri — **CT kararı ister** (DL-1)
- `docs/analysis/contracts/drafts/bank-account-lookup-validation-v1.0-proposal.md` — `BANK-ACCOUNT-LOOKUP` sözleşme dosyası
  (DL-2'de yazılır; bu paketle yazılmaz)
- `frontend/Diten.Web/Views/MasterData/{Banks,BankAccounts}/**`, `frontend/Diten.Web/wwwroot/assets/js/MasterData/{Banks,BankAccounts}/**`,
  `frontend/Diten.Web/Resources/Views/MasterData/{Banks,BankAccounts}/{Banks,BankAccounts}Index.{en,tr,fr,es,zh,ar,ru}.resx`,
  `frontend/Diten.Web/Controllers/MasterData/{Banks,BankAccounts}Controller.cs` (`MasterDataGatewayController`'dan türeyen
  same-origin vekil), `PageHelpResource.{7 dil}.resx` içinde `Help.BANKS.*` / `Help.BANKACCOUNTS.*`,
  `Services/Help/PageHelpCatalog.cs` satırları, `SharedResource.{7 dil}.resx` içinde `Nav.Page.BANKS`,
  `Nav.Page.BANKACCOUNTS` (DL-4)

**Bu paketin kapsamında OLMAYAN ama bağımlı değişiklikler** (her biri kendi sahibinin onayıyla, D9; burada yazılmaz):
`ocelot.json` rotaları (integration-agent); `services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs`
(iki yeni anahtar — Auth sahibi); `.antigravity/rules/record-coding-standard.md` §3 tablosuna `BNK` / `BAC` satırları
(`.antigravity` onayı); MOD-0140 `supplier.openapi.yaml` 1.3.0 (MVP-2 tek yazar, K15); MDM domain-config'e MOD-0289
satırı ve frontend/gateway kapsam cümlesi; MOD-0048 para birimi listesinin MDM'den tüketim yolu.

## Protected Paths

- `.antigravity/**` (RCS-001 §3 satırı, kural değişiklikleri ayrı onay)
- `gateway/Diten.ApiGateway/**/ocelot.json` — yalnız `integration-agent`
- `services/Diten.Platform/**` (MOD-0021, MOD-0023, MOD-0031, MOD-0048, veri kapsamı çözücüsü — platform-shared-services),
  `services/Diten.ProcurementService/**` (MOD-0140; tek yazar MVP-2, K15), `services/Diten.AuthService/**`,
  `services/Diten.Building.Blocks/**`, Treasury servisi ve diğer domain servisleri
- `services/Diten.MdmService/**` içinde bu paketin Repo Scope'u dışındaki her şey: MOD-0220 `LegalEntity` ve depoları
  **yalnız okunur** (bu paket LE kaydına yazmaz), MOD-0290 ürün ailesi, mevcut denetim niyeti üreticileri (yalnız ekleme
  satırları dışında)
- `frontend/Diten.Web/Views/Archive/**`, `Views/Shared/_Layout.cshtml`, `_LayoutBackbone` (kaynak layout'u; Recovery'de
  kullanılmaz), `_LayoutTenantShell.cshtml` (nav manifestten gelir; layout değişikliği bu pakette yok)
- `docs/analysis/contracts/supplier.openapi.yaml`, `docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md`
  ve diğer üretici sözleşmeleri (tüketilir, yeniden tanımlanmaz)
- `execution/registries/**`, `execution/domains/master-data-management/domain-config.md`, CT kuyruğu — bu paketle değişmez
- Kaynak depo ve handoff klasörü — yalnız okunur; kaynak düzeltmesi yapılmaz

## Dependencies

| Kapı / bağımlılık | Rol | Recovery durumu (ölçüm @fabf0050c) | Bu pakete etkisi |
|---|---|---|---|
| MOD-0220 Legal Entity | şirket sahibi; uygunluk | **var, aynı servis** — `LegalEntity.OperationalStatus` ve `ApprovalStatus` (`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs:49-50`), `IsReferenceable` (`:60`); `lookup-validation` yalnız `Active` için 200 (`…/Features/LegalEntity/Handlers/QueryHandlers/ValidateLegalEntityReferenceHandler.cs:19-25`) | süreç içi depo okuması; TRM-14'e **gerek yok** (onay durumu kayıtta okunur). Kural O-2 |
| MOD-0140 Supplier (`SUPPLIER`) | tedarikçi sahibi; ilişki durumu | sözleşme bu depoda **1.2.0** (`docs/analysis/contracts/supplier.openapi.yaml:13`); `validateSuppliers` 200 gövdesi `{supplierId, known, status}` + `contractVersion` (`:126-162`), `SupplierStatus` `Active/OnHold/Blocked/Inactive` (`:197`); kimlik string, kod Tenant + LE süzgeçli (`ValidateSuppliersHandler.cs:22-29`, yorum `:27`). **1.3.0 hiçbir dalda bu depoda yok** (`git log --all -- docs/analysis/contracts/supplier.openapi.yaml` → son değişiklik `780f8368a`, 1.2.0); MOD-0119 pack'i 1.3.0'ın `origin/feature/mvp2/supplier-tenant-01` dalında MVP-2 DL-2 ile yazılacağını kaydediyor (MOD-0119 pack Dependencies) — bu oturumda o dal ölçülmedi | **DL-3 engeli:** 1.3.0 main'e girmeden tedarikçi hesabı yazılamaz. Koleksiyon okuması yok (level-1 koşul 3) |
| MOD-0048 para birimi | kod listesi | `GET /api/lookups/reference/currencies` Platform'da (MOD-0126 pack ölçümü); MDM'den tüketim yolu **ölçülmedi** | `ICurrencyReferencePort`; erişilemezse Create 503, sabit liste yok |
| RCS-001 ortak kod servisi | `BNK` / `BAC` kodları | **yok** (BL-565); MDM'de yalnız ürün ailesine ait `CodeReservation` (`…/Domain/Entities/CodeReservation.cs:5-27`) | engel: modül kendi sayacını yazamaz (RCS-001 §6); servis gelmeden Create yazılamaz (O-7) |
| SEC-002 veri kapsamı | şirket hesaplarında görünürlük | MDM'de Platform'a giden güvenilir sağlayıcı **var**: `ITrustedLegalEntityScopeProvider.ResolveAsync(tenant, subject, moduleCode, permissionKey)` → `LegalEntityIds` (`…/Application/Contracts/Authorization/ITrustedLegalEntityScopeProvider.cs:3-22`), bugün ürün kapsamında kullanılıyor (`ProductLegalEntityScopeCandidateFacade.cs`); MOD-0289 izin anahtarlarıyla çalışıp çalışmadığı **ölçülmedi** | O-9; ölçülmeden `ready-for-dev` yok |
| MDM denetim izi | yazma ve maskesiz okuma kaydı | `mdm-guvenilir-kaynak-niyeti` (yol `b`, CT kararı 2026-10-07) ve `mdm-merkezi-iletim` (yol `b`, en iyi çaba) defterde kabul edilmiş (`tests/architecture/audit-ledger/Diten.MdmService.md` `## İzler`) | **bugün var**, ama yeni koleksiyonlar için genişletme + defter satırı CT kararı ister (Audited Events, O-5) |
| Şifreleme altyapısı | tam tanımlayıcı | servislerde alan şifreleme yok (`git grep` `IDataProtector|AddDataProtection|ClientEncryption|AesGcm|AutoEncryption` `services/*.cs` → 0) | **engel**: O-6 kararı + anahtar yönetimi; anahtar yoksa servis açılmaz |
| MOD-0018 / Auth explicit-grant | maskesiz okuma ve kiracı geneli izinleri | `ExplicitGrantOnlyPermissions` Auth'ta (`services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs:8-29`) | iki anahtar eklenmesi Auth değişikliği (D9, Auth sahibi) |
| MOD-0003 Data Contract Registry | sözleşme kaydı | kayıt satırı planned / missing; uygulama 0 dosya (paket §D, Recovery düzeltmesi) | v1'de kullanılmaz; sözleşme dosyası `docs/analysis/contracts/` altında |
| MOD-0023 Workflow | hesap onay akışı | var (Platform) | v1'de kullanılmaz (O-1 önerisi) |
| MOD-0031 Evidence | tedarikçi hesabı doğrulama kanıtı | var (Platform) | v1'de kullanılmaz (O-4 önerisi); gerçek dağıtım kapısından önce zorunlu (Follow-up) |
| MOD-0126 / MOD-0119 / MOD-0120 / MOD-0124 | tüketici | taslak pack'ler opak Guid tutuyor (MOD-0126 `DefaultSourceBankAccountRef`, `DestinationReferenceId`; MOD-0120 `CollectionDestinationRef`; MOD-0124 `BankAccountRef`) | tüketici sözleşme testi o paketlerin işi; bu paket sözleşmeyi verir |
| MOD-0269 Banks/SWIFT gateway | ileride tam tanımlayıcı tüketicisi | yok (D5, dağıtım kapalı); `bank-dispatch-v0.1-proposal.md` §3 banka verisi taşımaz | v1'de tam tanımlayıcıyı okuyan sistem tüketicisi yok; açıldığında ayrı karar ve servis kimliği |
| Port | servis | MDM 5059 (`AGENTS.md:75`, `.antigravity/rules/ports.md:25`) | yeni port yok (D20/1) |
| DCP (`UNASSIGNED-finance-treasury-intake`) | dilim kapısı | taslak (MOD-0126 pack) — TRM-S3 | DCP `approved` olmadan `ready-for-dev` yok |

### Bağımlılık sözleşmeleri — kapanış tablosu

Her satır kapanmadan ilgili dilim başlamaz. **Sahte istemciyle geçen test canlı entegrasyon kanıtı değildir:** "sözleşme
testi" sahte istemcili testi, "canlı kanıt" gerçek servisle dev ortamında koşulmuş kaydı (`scripts/run_record.py`) ister.

| Bağımlılık | Sözleşme / sürüm | Hash | Sahibi | Hata davranışı (MOD-0289) | Kapanış kanıtı (sözleşme testi · canlı kanıt) | Dilim |
|---|---|---|---|---|---|---|
| BANK-ACCOUNT-LOOKUP (bu modül) | `drafts/bank-account-lookup-validation-v1.0-proposal.md` 1.0.0 — **yazılacak** | dondurulunca | master-data-management | — | AC-20 · dev ortamında bir tüketiciyle okuma | DL-2 |
| MOD-0220 LegalEntity (süreç içi) | `ILegalEntityRepository` (kod; sözleşme dosyası yok) | MdmService commit SHA'sı DL-2 başında | master-data-management | kayıt yok / başka kiracı → 404 `MDM_BANK_ACCOUNT_OWNER_NOT_ELIGIBLE` sınıfı 422; uygun değil → 422 | birim + entegrasyon (Active+Approved / Active+Draft / Suspended / silinmiş) · — (aynı süreç) | DL-2 |
| MOD-0140 `validateSuppliers` | `supplier.openapi.yaml` **1.3.0** — yok (bugün 1.2.0) | main'e girince `shasum` | procurement (MVP-2) | known + Active → uygun; known + OnHold/Blocked/Inactive ya da known:false → 422; geçersiz / çelişkili gövde, 401/403, 5xx, zaman aşımı → 503; yazma yok | sahte istemci (AC-24) · dev ortamında Active / OnHold / Blocked / ilişkisiz dört tedarikçi | DL-3 |
| MOD-0048 para birimi | `GET /api/lookups/reference/currencies` | DL-2 başında | platform-shared-services | erişilemez → 503, listede yok → 400 | sahte istemci · dev ortamında TRY/EUR/JPY | DL-2 |
| RCS-001 | **yok** (BL-565) | — | CT + `.antigravity` onayı | servis yoksa Create yazılamaz | — · — | DL-1 önkoşulu |
| SEC-002 kapsam sağlayıcısı | `ITrustedLegalEntityScopeProvider` (MDM kodu) + Platform sağlayıcı ucu | DL-2 başında | platform-shared-services | hata → boş kapsam, boş liste, detay / yazma 404 | AC-17 (sabotajlı) · dev ortamında iki şirket iki kullanıcı | DL-2 |
| Şifreleme anahtarı | O-6 kararı | — | sahip + güvenlik | anahtar yok → servis açılmaz; çalışırken erişilemez → 503, yazma yok | AC-13 · dev ortamı anahtar döndürme denemesi | DL-2 |
| MDM denetim niyeti (genişletilmiş) | `LocalAuditIntent` + `AuditIntentDeliveryProcessor` (kod) | MdmService commit SHA'sı DL-1 başında | master-data-management + CT | niyet yazılamazsa komut başarısız (iş verisi yok) | AC-6 (sabotaj) · AC-27 (AUD-001 §8 dev kanıtı) | DL-1 |

## Runtime Constraints

- Servis `Diten.MdmService`, port 5059; yeni servis ya da port yok (D20/1). Tarayıcı ve `Diten.Web` yalnız Gateway (5000)
  üzerinden çağırır; tarayıcı JS 5059'a gitmez. MdmService'ten MOD-0140'a çağrı Gateway üzerinden yapılır; adres
  yapılandırmadan gelir, yoksa servis açılmaz.
- **Kiracı izolasyonu:** `TenantId` sunucuda; her sorgu `TenantFilter`'la; başka kiracının bankası, şubesi, hesabı,
  okuma kaydı **404**. Üretimde sabit / varsayılan kiracı yok. **Hiçbir belge `TenantId = Guid.Empty` ile yazılmaz**
  (kaynaktaki `Mod0289BankService.cs:2450@da4abb20` deseni ve "depo yansımayla düzeltir" varsayımı taşınmaz — level-1 koşul 2);
  servis kiracıyı kendisi verir ve depo boş kiracıyı reddeder.
- **Soft delete:** `IsDeleted` + `DeletedAt` (UTC); yalnız hiç aktive edilmemiş (`ActivatedAt = null`) `Draft` kayıt
  silinir. Bir kez aktive edilmiş hesap yalnız pasifleştirilir — tüketicinin elindeki opak referans her zaman çözülebilir
  bir kayda işaret eder (404 yalnız "kullanılamaz" demektir, "hiç olmadı" değil).
- **Eşzamanlılık:** her Update / Activate / Deactivate `If-Match` (Version) ve durum CAS; bayat istek 409
  `MDM_BANK_VERSION_CONFLICT`. Taban `UpdateAsync` kullanılmaz (Entity Fields notu).
- **İdempotency:** bu pakette istemci idempotency makbuzu yok (Treasury'nin D12/2 kararı MDM'ye uzanmaz). Tekrar
  koruması doğal anahtarla: aynı tanımlayıcı kiracıda ikinci kez kaydedilemez (`ux_mdm_bank_account_identifier`, 409);
  durum geçişleri CAS ile tekrar edilemez. Kaynak F02 makbuzu (`f02_receipt_fields`) taşınmaz.

### Hassas veri — saklama, maskeleme, günlük

1. **Saklama:** tam tanımlayıcı istekle gelir, sunucuda normalize edilir (boşluk / tire atılır, büyük harf), doğrulanır,
   `IBankAccountIdentifierProtector` ile şifrelenir (`AccountIdentifierCiphertext` + `AccountIdentifierKeyId`), anahtarlı
   özeti (`AccountIdentifierHash`) ve son 4 hanesi türetilir. Düz metin hiçbir belgede, indekste, denetim niyetinde,
   olayda ya da önbellekte bulunmaz. Mekanizma ve anahtar yönetimi O-6.
2. **Değiştirilemezlik:** Active ya da Inactive hesapta tanımlayıcı, para birimi, banka, şube ve sahip değişmez (409
   `MDM_BANK_ACCOUNT_IMMUTABLE_FIELD`); değişiklik = yeni hesap + eskisini pasifleştirme. Böylece bir tüketicinin tuttuğu
   opak referansın gösterdiği hesap hiçbir zaman sessizce başka bir hesaba dönüşmez (ödeme yönlendirme sahtekârlığına
   karşı temel kontrol). Taslakta tanımlayıcı değiştirilebilir (yeniden şifreleme).
3. **Maskeleme:** liste, detay, `lookup-validation` ve her DTO yalnız `MaskedDisplay` + `AccountLast4` taşır. Tam değer
   yalnız `RevealBankAccountIdentifierCommand` yanıtında (`BankAccountRevealDto`) ve yalnız
   `mdm.bank-accounts.read-unmasked` izni + kapsam + gerekçe ile döner; yanıt `Cache-Control: no-store`. Okuma kaydı
   (`BankAccountSensitiveRead` + merkezi niyet) değer döndürülmeden **önce** aynı işlemde yazılır; yazılamazsa değer
   dönmez (503 `MDM_BANK_AUDIT_UNAVAILABLE`).
4. **Günlük (log) ve hata iletileri:** MDM `LoggingBehavior` yalnız istek adını yazıyor
   (`services/Diten.MdmService/src/Diten.MdmService.Application/Behaviors/LoggingBehavior.cs:19,21@fabf0050c`) —
   korunur. `ExceptionHandlingBehavior` istisnayı günlüğe yazar (`ExceptionHandlingBehavior.cs:25`): tanımlayıcı hiçbir
   istisna iletisine konmaz. Doğrulama iletileri tanımlayıcının girilen değerini (`{PropertyValue}`, `AttemptedValue`)
   taşımaz; MDM `ValidationBehavior`'ın 400 gövdesinde girilen değeri geri döndürüp döndürmediği **ölçülmedi** — DL-2
   başında ölçülür, döndürüyorsa bu modülün doğrulayıcısı değeri gizler. HTTP istemci günlüklerinde gövde yazılmaz.
5. **Denetim yükü:** önce/sonra durumda tanımlayıcı, son 4 hane, hesap sahibi adı ve maskeli gösterim **yoktur**; yalnız
   değişen alan **adları**, kimlikler ve durum (AUD-001 §3). Okuma kaydı yalnız kimlik + gerekçe kodu taşır.
6. **Dış doğrulama yok:** IBAN / BIC biçim ve sağlama kuralları kodda çalışır; v1'de hiçbir dış doğrulama, banka
   dizini ya da sağlayıcı çağrısı yapılmaz (testte ağ çağrısı sayısı 0).

### Sahiplik kuralları

- Hesabın **tam olarak bir** sahibi vardır: `OwnerType = LegalEntity` ⇒ `LegalEntityId` dolu, `SupplierId` boş;
  `OwnerType = Supplier` ⇒ tersi. Sahip oluşturulduktan sonra değişmez.
- **Şirket sahibi (aynı servis, süreç içi):** Create ve Activate'te `LegalEntity` aynı kiracıda, silinmemiş ve
  `OperationalStatus = Active` olmalıdır; Activate'te ayrıca `ApprovalStatus = Approved` (O-2 önerisi, D10/1 benzeşimi).
  Değilse 422 `MDM_BANK_ACCOUNT_OWNER_NOT_ELIGIBLE`. Kullanıcının o şirket üzerinde **yazma kapsamı** olmalıdır; yoksa 404.
- **Tedarikçi sahibi (MOD-0140 sözleşmesi, Gateway):** Create ve Activate'te `ISupplierEligibilityPort` `validateSuppliers`
  çağırır; çağrı isteğin `X-Legal-Entity-Id`'siyle yapılır çünkü tedarikçi ana kaydı kiracı genelinde ama ilişki durumu
  şirket başınadır (sahip cevabı 2026-10-21, `docs/roadmap/plans/mvp6-development-plan-v10.0.md` C1). `known = true` ve
  `status = Active` değilse 422; geçersiz / çelişkili yanıt (sonuçta istenen `supplierId` yok ya da farklı, `known`
  boolean değil, `status` dört değerin dışında, gövde ayrıştırılamıyor) ya da 401/403/5xx/zaman aşımı → 503
  `MDM_BANK_ACCOUNT_OWNER_UNAVAILABLE`; ikisinde de hiçbir şey yazılmaz. `mod0140_suppliers` koleksiyonu ya da
  ProcurementService derlemesi **hiçbir zaman** okunmaz (level-1 koşul 3). 1.3.0'ın `status` alanının "çağıran şirketin
  ilişki durumu" anlamı MOD-0119 pack kaydından alınmıştır — dosya yazılmadığı için **ölçülmedi**; DL-3 başında dosyaya
  karşı yeniden doğrulanır.
- **`X-Legal-Entity-Id` MDM'de yeni:** bugün MdmService bu başlığı okumuyor (`git grep X-Legal-Entity-Id services/Diten.MdmService/src` → 0).
  Bu modülde başlık yalnız (a) tedarikçi sahibinin ilişki kontrolü ve (b) şirket hesabı Create'inde gövdedeki
  `LegalEntityId` ile tutarlılık için kullanılır; başlıktaki şirket kiracıya ait ve `Active` olmalıdır (süreç içi
  `ILegalEntityRepository`), değilse 404; başlık ile gövde çelişirse 400.
- **Sahip sonradan uygunluğunu kaybederse:** hesap otomatik değişmez (MDM'de içeri olay alımı yok, kaynakta da yoktu —
  paket §F); ama hiçbir yerde sahip durumu kopyası saklanmadığı için `lookup-validation` sahibin **güncel** durumunu
  döndürür (`ownerState = NOT_ELIGIBLE`) ve tüketici 422 ile durur. Yeniden aktive etme ve tedarikçi hesabı aktive etme
  aynı kontrolden geçer. Pasif sahibin hesaplarını toplu pasifleştiren iş v1 dışıdır (Follow-up).
- **Banka / şube pasifse:** yeni hesap Create ve Activate 422 `MDM_BANK_INACTIVE`; var olan Active hesap için
  `lookup-validation` `bankState = INACTIVE` döndürür ve hesap `referenceable = false` sayılmaz (hesabın kendisi aktif);
  tüketicinin kuralı sözleşmede (aşağıda).

### Veri kapsamı (SEC-002) — kapsam cümlesi

- **Şirket hesapları:** kapsamı taşıyan alan `BankAccount.LegalEntityId`; çevrilen kapsam türü yalnız `LegalEntity`
  (`OrgUnit` / `Position` çevrilmez → daraltır, sızdırmaz); kapsam çözümleme tek yerde (`IBankAccountDataScopeSource`, O-9
  — aday MDM'deki `ITrustedLegalEntityScopeProvider`). Kapsam dışı şirketin hesapları listede çıkmaz (200, 0 satır);
  detay, maskesiz okuma, `lookup-validation` ve her yazma 404. Çözücü hatası, kiracı ya da kullanıcı yokluğu → boş kapsam.
  Kiracıdaki tüm şirketlerin hesaplarını görmek ayrı izindir: `mdm.bank-accounts.read-tenant-wide` (yalnız açık atama,
  **yalnız okuma**; yazma kapsamını genişletmez — her yazma kullanıcının o şirketteki kendi yetkisini ister, MOD-0126
  gap 4/G1 deseni).
- **Tedarikçi hesapları:** tedarikçi ana kaydı kiracı geneli olduğu için (sahip cevabı 2026-10-21) tedarikçi hesapları
  şirket kapsamıyla daraltılmaz; sınır `mdm.bank-accounts.read` iznidir ve bu istisna burada yazılıdır (SEC-002 §3
  "Ne zaman uygulanmaz" — iş verisi olduğu için istisna yalnız bu cümleyle geçerlidir; sahip farklı karar verirse O-9 ile
  değişir). Tedarikçi hesabı yazmaları yine ilişki kontrolü için çağıranın şirketini ister (yukarıda).
- **Banka ve şube:** kiracı içinde herkese açık referans verisi (`mdm.banks.read` izni olan herkes görür); SEC-002 §3
  istisnası bu cümleyle yazılıdır.

### Okuma sözleşmesi — `BANK-ACCOUNT-LOOKUP` 1.0.0 (tanım; dosyası DL-2'de yazılır)

LEGAL-ENTITY-LOOKUP 1.1.0 deseni (`docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md` §1–§2).

- **Uç:** `GET /api/mdm/bank-accounts/{bankAccountId:guid}/lookup-validation`, izin `mdm.bank-accounts.read`, veri kapsamı
  uygulanır. Tedarikçi sahipli hesapta `X-Legal-Entity-Id` zorunludur (ilişki durumu için); yoksa 400.
- **404 (gövde aynı, varlık sızdırmaz):** hesap yok; silinmiş; başka kiracıda; çağıranın kapsamı dışında; `Status ≠ Active`
  (Draft, Inactive).
- **200 gövdesi (`data`):**

| Alan (JSON) | Tip | Değer |
|---|---|---|
| `bankAccountId` | string (UUID) | istenen kimlik |
| `code` | string | RCS-001 kodu |
| `lifecycleState` | string | `ACTIVE` (200 yalnız aktif hesap için) |
| `referenceable` | bool | `true` (200 yalnız referans verilebilir hesap için) |
| `ownerType` | string | `LEGAL_ENTITY` \| `SUPPLIER` |
| `legalEntityId` | string (UUID) \| null | `ownerType = LEGAL_ENTITY` ise dolu, değilse `null` |
| `supplierId` | string \| null | `ownerType = SUPPLIER` ise dolu, değilse `null` |
| `ownerState` | string | `ELIGIBLE` \| `NOT_ELIGIBLE` — sahibin **o anki** durumu (şirket: Active + Approved, O-2; tedarikçi: çağıran şirketle ilişki `Active`) |
| `ownerStateReason` | string \| null | `NOT_ELIGIBLE` ise makine kodu (`LEGAL_ENTITY_NOT_ACTIVE`, `LEGAL_ENTITY_NOT_APPROVED`, `SUPPLIER_UNKNOWN`, `SUPPLIER_NOT_ACTIVE`) |
| `bankState` | string | `ACTIVE` \| `INACTIVE` (banka ya da şube pasifse `INACTIVE`) |
| `currencyCode` | string | `^[A-Z]{3}$` |
| `maskedDisplay` | string | ör. `TR••••1234` |
| `contractVersion` | string | `bank-account-lookup/1.0` |

  Gövde **IBAN, hesap numarası, son 4 hane dışındaki herhangi bir parça, BIC, banka adı, şube adı, hesap sahibi adı
  taşımaz.** Tedarikçi ilişkisi okunamazsa (MOD-0140 503 / geçersiz) MDM 200 dönmez, **503**
  `MDM_BANK_ACCOUNT_OWNER_UNAVAILABLE` döner (hiçbir zaman varsayılan `ELIGIBLE` yok).
- **Tüketici sınıflandırma tablosu (sözleşmenin parçası; Treasury paketleri uygular):**

| MOD-0289 yanıtı | Sınıf | Tüketici sonucu |
|---|---|---|
| 200; `bankAccountId` istenene eşit; `referenceable = true`; `lifecycleState = ACTIVE`; `ownerType` ile dolu sahip alanı tutarlı; `ownerState = ELIGIBLE`; `bankState = ACTIVE`; `currencyCode` biçimli; sahip, tüketicinin beklediği taraf (ör. MOD-0126 kaynak hesabında `ownerType = LEGAL_ENTITY` ve `legalEntityId` = ödeyen şirket) ve para birimi tüketicinin kuralına uygun | uygun | işlem sürer |
| 200, tutarlı, ama `ownerState = NOT_ELIGIBLE`, `bankState = INACTIVE`, sahip beklenen taraf değil ya da para birimi uyuşmuyor | tanımlı, uygun değil | 422 |
| 404 | tanımlı, kullanılamaz (yok / başka kiracı / kapsam dışı / aktif değil) | 422 |
| 200 ama `bankAccountId` yok ya da farklı; `referenceable = false`; `lifecycleState ≠ ACTIVE`; `ownerType` bilinmeyen; `ownerType` ile sahip alanları çelişkili (ikisi dolu, ikisi boş, yanlışı dolu); `ownerState` yok / `null` / bilinmeyen; `currencyCode` biçimsiz; gövde ayrıştırılamıyor; `data` yok | geçersiz ya da çelişkili yanıt | 503 |
| 400, 401 / 403, 5xx (MDM 503 dahil), zaman aşımı, ulaşılamıyor | erişilemiyor | 503 |

  Kural: geçersiz ya da çelişkili yanıt **503**, tanımlı ama uygun olmayan durum **422**; hiçbir belirsiz yanıt "uygun"
  sayılmaz; karşılaştırmalar büyük-küçük harfe duyarlıdır. Tüketici yanıttaki hiçbir alanı (maskeli gösterim dahil)
  kendi kalıcı kaydına kopyalamaz; yalnız opak `bankAccountId` saklanır (ekranda maskeli gösterim gerekiyorsa her
  seferinde okunur).
- **Sürüm kuralı:** 1.x yalnız ekleme; 404 kararını genişletmek büyük sürümdür.

## Layout & Shell Contract

- `shell: tenant` → `Views/MasterData/Banks/` ve `Views/MasterData/BankAccounts/` altındaki **tüm** `.cshtml`
  dosyalarında `Layout = "_LayoutTenantShell";` **AÇIKÇA** yazılır (canlı örnek
  `frontend/Diten.Web/Views/MasterData/Brands/Index.cshtml:7@fabf0050c`); `_ViewStart.cshtml` varsayılanı değiştirilmez.
  Kaynak ekranlardaki `_LayoutBackbone` taşınmaz (paket §I).
- View klasörleri: `Views/MasterData/Banks/`, `Views/MasterData/BankAccounts/` · rotalar `/MasterData/Banks`,
  `/MasterData/BankAccounts`. Nav: modül manifestinden (`BrandProductMasterManifestProvider.cs` deseni: sayfa kodu,
  `RoutePath`, `RequiredPermission`), anahtarlar `Nav.Page.BANKS`, `Nav.Page.BANKACCOUNTS`.
- Yetkisiz kullanıcı (UAS-001): `IPermissionSnapshot` ile `mdm.banks.read` / `mdm.bank-accounts.read` yoksa kabuk içinde
  `_AccessDenied` partial'ı; başlık, kart, boş tablo, düğme çizilmez; yönlendirme yok; 401 tam sayfa.

## Backend File Convention

Golden Reference **Compact** birebir (`services/Diten.DevEnablementService/src/Diten.DevEnablementService.Application/Features/GoldenReferenceCompact/`
okundu: `Commands/`, `Queries/`, `Handlers/{CommandHandlers,QueryHandlers}`, `Validators/`, tek `…Models.cs`; MDM'de aynı
düzen `Features/Brand/`'de var). İki özellik klasörü:

```text
Features/BankMaster/
  Commands/        CreateBankCommand.cs, UpdateBankCommand.cs, ActivateBankCommand.cs, DeactivateBankCommand.cs,
                   DeleteBankCommand.cs, BulkDeleteBankCommand.cs, CreateBankBranchCommand.cs, UpdateBankBranchCommand.cs,
                   ActivateBankBranchCommand.cs, DeactivateBankBranchCommand.cs, DeleteBankBranchCommand.cs
  Queries/         GetBankListQuery.cs, GetBankByIdQuery.cs, GetBankBranchListQuery.cs, GetBankBranchByIdQuery.cs, BankListSort.cs
  Handlers/CommandHandlers/   {Verb}Bank{…}Handler.cs (suffix YOK)
  Handlers/QueryHandlers/     Get{…}Handler.cs
  Validators/      CreateBankValidator.cs, UpdateBankValidator.cs, CreateBankBranchValidator.cs, UpdateBankBranchValidator.cs,
                   GetBankListValidator.cs
  BankModels.cs
Features/BankAccount/
  Commands/        CreateBankAccountCommand.cs, UpdateBankAccountCommand.cs, ActivateBankAccountCommand.cs,
                   DeactivateBankAccountCommand.cs, DeleteBankAccountCommand.cs, BulkDeleteBankAccountCommand.cs,
                   RevealBankAccountIdentifierCommand.cs
  Queries/         GetBankAccountListQuery.cs, GetBankAccountByIdQuery.cs, GetBankAccountSensitiveReadListQuery.cs,
                   ValidateBankAccountReferenceQuery.cs, BankAccountListSort.cs
  Handlers/CommandHandlers/   …Handler.cs
  Handlers/QueryHandlers/     …Handler.cs
  Validators/      CreateBankAccountValidator.cs, UpdateBankAccountValidator.cs, RevealBankAccountIdentifierValidator.cs,
                   GetBankAccountListValidator.cs
  Rules/           IbanRules.cs, BicRules.cs, DomesticAccountRules.cs (saf işlevler; ağ yok)
  BankAccountModels.cs
```

- Command/Query `sealed record` (`IRequest<Response<T>>`); Handler `sealed class` — **Command/Query suffix YOK**. Tek
  dosyada tek public tip. Controller `CustomBaseController`'dan türer (`LegalEntitiesController.cs:11-14@fabf0050c` deseni),
  `Response<T>` döner, her aksiyonda `[HasPermission]`.
- İş kuralı handler + domain'de (`handler-design`); kaynaktaki büyük servis sınıfları (`Mod0289BankService` ~2500 satır,
  paket atıfı `:2446-2466`) alınmaz, kurallar bölünerek yeniden yazılır.
- Her yazma komutu ve maskesiz okuma, denetim niyetini iş belgesiyle **aynı koşullu yazımda ya da aynı Mongo işleminde**
  taşır (Audited Events); `MasterDataAtomicWrite` politikası (işlem yoksa Development dışında yazmayı reddet,
  `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/MasterDataAtomicWrite.cs:9-20@fabf0050c`) bu
  modülde de geçerlidir.

## Frontend File Contract

Golden Reference **Compact** seti (`frontend/Diten.Web/Views/DevEnablement/GoldenReferenceCompact/` okundu):

```text
Views/MasterData/Banks/
├── Index.cshtml              (Layout = "_LayoutTenantShell"; ① Filter → ② BulkActionBar → ③ DataTable)
├── Create.cshtml · Edit.cshtml · Details.cshtml (Details: şubeler sekmesi + şube oluştur/düzenle)
├── _Form.cshtml · _Filter.cshtml (ülke, durum, BIC var/yok) · _DataTable.cshtml (data-dt-standard="v2", data-dt-data-mode="server")
├── _IndexL10n.cshtml
└── BanksIndex.cs
Views/MasterData/BankAccounts/
├── Index.cshtml · Create.cshtml · Edit.cshtml · Details.cshtml (Details: maskeli gösterim, "Maskesiz göster" eylemi, okuma kayıtları sekmesi)
├── _Form.cshtml · _Filter.cshtml (sahip türü, şirket, tedarikçi, banka, para birimi, durum) · _DataTable.cshtml (server)
├── _IndexL10n.cshtml
└── BankAccountsIndex.cs
wwwroot/assets/js/MasterData/{Banks,BankAccounts}/  index.js · index.l10n.js · form.js · details.js
Resources/Views/MasterData/{Banks,BankAccounts}/   {Banks,BankAccounts}Index.{en,tr,fr,es,zh,ar,ru}.resx
```

- **YASAK (compact):** `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, Index içinde create/edit offcanvas.
- BankAccounts DataTable sütunları: Kod, Sahip türü, Sahip (şirket adı / tedarikçi kodu), Banka, Para birimi, Hesap
  (yalnız `MaskedDisplay`), Amaç, Durum, Oluşturan, Oluşturma zamanı, İşlemler. Tam tanımlayıcı listede, filtre
  önerilerinde, dışa aktarımda (v1'de yok) ve tarayıcı depolamasında **hiç** yoktur.
- Create formunda tanımlayıcı alanı `autocomplete="off"`; Edit'te (Active hesap) tanımlayıcı, para birimi, banka, şube ve
  sahip salt okunur ya da yok. "Maskesiz göster" düğmesi yalnız `read-unmasked` izni olan kullanıcıya çizilir; gerekçe
  kodu SweetAlert2 premium diyaloğunda zorunludur (MOD-0013); değer yalnız diyalog açıkken DOM'dadır, kapanınca silinir;
  `localStorage` / `sessionStorage` / konsol yazımı yok.
- Activate diyaloğu: dört göz kuralı ihlalinde düğme yerine yerelleştirilmiş açıklama (O-1); tedarikçi hesabında doğrulama
  kodu seçimi (O-4). Sunucu kontrolünün yerine geçmez.
- API profili: same-origin MVC vekil (`/MasterData/BankAccounts/api/...`, `MasterDataGatewayController` deseni —
  `frontend/Diten.Web/Controllers/MasterData/MasterDataGatewayController.cs:18-21@fabf0050c`: HttpOnly erişim çerezi
  sunucuda okunur, tarayıcı 5059'u ya da token'ı görmez); vekil oturumun çözümlenmiş `X-Legal-Entity-Id`'sini ekler.
  Vekil maskesiz okuma yanıt gövdesini günlüğe yazmaz.
- Para birimi seçimi `GET /api/lookups/reference/currencies`'ten; ülke seçimi O-8; sabit liste yok; hardcoded metin yok.

## Validation Rules

| Alan / kural | Zorunlu | Biçim / kural | DB | Ön kontrol |
|---|---|---|---|---|
| Gövde/sorguda `tenantId` | — | bulunursa 400 | — | — |
| Bank.Code / BankAccount.Code | — | istekte bulunursa 400 `MDM_BANK_CODE_FORBIDDEN` (OTOMATİK) | Unique (TenantId, Code), kısmi değil | `IRecordCodePort` |
| Bank.Name | Evet | trim, 1..200, kontrol karakteri yok | — | — |
| Bank.CountryCode | Evet | `^[A-Z]{2}$` (büyük harfe normalize); liste kaynağı O-8 | — | — |
| Bank.Bic | Hayır | boşluk atılır, büyük harf; `^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$` (ISO 9362); 5–6. karakter = `CountryCode` | Unique (TenantId, Bic) kısmi | — |
| Bank.NationalBankCode | Hayır | trim, 1..35, `^[A-Za-z0-9 -]+$` | Unique (TenantId, CountryCode, NationalBankCode) kısmi | — |
| BankBranch.BankId | Evet | aynı kiracı, silinmemiş banka; bankası `Inactive` ise Create 422 | — | depo |
| BankBranch.BranchCode | Evet | trim, 1..35 | Unique (TenantId, BankId, BranchCode) kısmi | — |
| BankBranch.Bic | Hayır | 11 karakter ISO 9362; ilk 8 = banka BIC'i (varsa) | — | — |
| BankAccount.OwnerType | Evet | tanımlı enum; değişmez | — | — |
| BankAccount.LegalEntityId / SupplierId | Koşullu (tam biri) | türüne göre tam biri dolu; LE: Guid ≠ Empty, aynı kiracı, `Active`; tedarikçi: 1..64, `validateSuppliers` | — | `ILegalEntityRepository` / `ISupplierEligibilityPort` |
| BankAccount.BankId / BranchId | Evet / Hayır | aynı kiracı; şube o bankaya ait (400 `MDM_BANK_BRANCH_BANK_MISMATCH`); banka/şube `Active` değilse 422 | — | depo |
| BankAccount.CurrencyCode | Evet | `^[A-Z]{3}$`, MOD-0048 listesinde | — | `ICurrencyReferencePort` |
| AccountIdentifier (IBAN) | Evet | normalize (boşluk/tire at, büyük harf); `^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$` (15..34); ISO 7064 MOD 97-10 sağlaması = 1; ilk iki harf = bankanın `CountryCode`'u. Ülkeye göre uzunluk tablosu v1'de **yok** (sahibi olmayan sabit tablo yazılmaz; Follow-up) | Unique (TenantId, Hash) kısmi | `IbanRules` |
| AccountIdentifier (Domestic) | Evet | normalize; `^[A-Z0-9]{4,34}$` | aynı | `DomesticAccountRules` |
| AccountHolderName | Evet | trim, 1..140, kontrol karakteri yok | — | — |
| AccountPurpose | Evet | 1..64; değer listesi O-10 | — | — |
| PaymentUsage / DisplayName | Hayır | ≤ 120 | — | — |
| `X-Legal-Entity-Id` | tedarikçi sahibi ve LE Create'te Evet | Guid; kiracıda `Active` şirket; değilse 404; gövdeyle çelişki 400 | — | `ILegalEntityRepository` |
| `If-Match` | Update / Activate / Deactivate'te Evet | `Version` | CAS süzgeci | — |
| DeactivationReasonCode | Deactivate'te Evet | 1..64 | — | — |
| ActivationVerificationCode | tedarikçi hesabı Activate'te Evet (O-4) | değer listesi O-4 | — | — |
| Reveal.ReasonCode / ReasonComment | Evet / Hayır | 1..64 / ≤ 500, tanımlayıcıyı içeremez | — | — |

**Durum kuralları:** Update — Draft'ta bütün form alanları (sahip hariç); Active/Inactive'de yalnız `DisplayName`,
`PaymentUsage`, `AccountPurpose`, `AccountHolderName` (öneri; sahip adı düzeltmesi yönlendirmeyi değiştirmez ama denetlenir).
Activate — Draft ya da Inactive'den; sahip, banka ve şube uygun; dört göz (O-1). Delete / BulkDelete — yalnız
`ActivatedAt = null` Draft; BulkDelete'te kurala uymayan tek kimlik bütün isteği 409 yapar (kısmi silme yok).

**Dört göz kuralı (O-1 önerisi):** `ActivateBankAccountCommand`'ı hesabı oluşturan (`CreatedBy`) ya da son güncelleyen
(`UpdatedBy`) kullanıcı çalıştıramaz → 403 `MDM_BANK_ACCOUNT_FOUR_EYES_REQUIRED`. Aktör karşılaştırması token `sub`'ının
Guid değeri üzerindendir; Guid olmayan `sub` reddedilir.

## Failure Path to Verify

- **Yinelenen hesap** (aynı tanımlayıcı, aynı ya da başka sahip) → 409 `MDM_BANK_ACCOUNT_DUPLICATE`; gövde diğer kaydın
  kimliğini, kodunu ya da sahibini içermez.
- **Yinelenen BIC / ulusal banka kodu / şube kodu** → 409 ilgili kod; tekil indeks kaldırıldığında yarış testi kırmızı.
- **Eksik alan** (Name, CountryCode, CurrencyCode, tanımlayıcı, sahip) → 400 + alan düzeyi resx iletisi; kayıt yok;
  iletide girilen tanımlayıcı yok.
- **IBAN sağlaması tutmuyor / biçim dışı / ülke uyuşmuyor; BIC biçim dışı / ülke uyuşmuyor** → 400; ağ çağrısı yok.
- **Başka kiracının kaydı** (okuma, güncelleme, silme, aktivasyon, maskesiz okuma, lookup) → 404, varlık sızdırılmaz.
- **Kapsam dışı şirketin hesabı** → liste boş, detay / maskesiz okuma / lookup / yazma 404; çözücü hatası → aynı.
- **Yalnız `read-tenant-wide` ile görünen hesaba yazma** → 404; okuma 200.
- **Yetkisiz aktör** → API 403; UI kabuk içinde erişim açıklaması (UAS-001).
- **Maskesiz okuma izni yok** → 403, değer yok, okuma kaydı yok; **gerekçe yok** → 400; **okuma kaydı yazılamıyor**
  (zorla hata) → 503, değer dönmez.
- **Eşzamanlılık** (bayat `If-Match`) → 409 `MDM_BANK_VERSION_CONFLICT`; iki eşzamanlı Activate → biri 409.
- **Active hesapta tanımlayıcı / para birimi / banka / sahip değişikliği** → 409 `MDM_BANK_ACCOUNT_IMMUTABLE_FIELD`.
- **Aktive edilmiş hesabı silme** → 409 `MDM_BANK_STATE_INVALID`.
- **Şirket aktif değil ya da onaylı değil** (Activate) → 422 `MDM_BANK_ACCOUNT_OWNER_NOT_ELIGIBLE`; kayıt değişmez.
- **Şirket hesap açıldıktan sonra askıya alındı** → hesap değişmez; `lookup-validation` 200 + `ownerState = NOT_ELIGIBLE`
  + `ownerStateReason = LEGAL_ENTITY_NOT_ACTIVE`.
- **Tedarikçi bilinmiyor / ilişki Active değil** → 422; **MOD-0140 erişilemez, 401/403, 5xx, zaman aşımı, geçersiz gövde**
  → 503 `MDM_BANK_ACCOUNT_OWNER_UNAVAILABLE`; hiçbir şey yazılmaz; `lookup-validation` de 503 (asla varsayılan `ELIGIBLE`).
- **Dört göz ihlali** (oluşturan / son güncelleyen aktive ediyor) → 403 `MDM_BANK_ACCOUNT_FOUR_EYES_REQUIRED`.
- **Pasif bankaya hesap** → 422 `MDM_BANK_INACTIVE`.
- **Denetim niyeti yazılamıyor** (zorla hata) → komut başarısız 503, iş verisi yazılmaz.
- **Şifreleme anahtarı yok** → servis açılmaz; çalışırken erişilemez → Create / Reveal 503 `MDM_BANK_PROTECTION_UNAVAILABLE`.
- **RCS-001 ya da MOD-0048 erişilemez** → Create 503 ilgili kod; kayıt yok.

## Authorization Convention

- Policy: `[Authorize]` (kiracı aktörü) + her aksiyonda `[HasPermission("…")]` (MDM deseni
  `LegalEntitiesController.cs:24-62@fabf0050c`). Kaynakta MOD-0289 kendi `MOD0289_*` taleplerini denetleyicide kontrol
  ediyordu (`Mod0289Controller.cs:246@da4abb20`, paket §D) — taşınmaz.
- Biçim (PKS-001): küçük harf, noktalı, ≥ 3 parça; ad alanı `mdm.*` (MDM'de ölçülen anahtarlar `mdm.legal-entities.*`,
  `mdm.brands.*`, `mdm.products.*` …). Kaynak 38 `MOD0289_*` anahtarı (CONTRACT-STATE `permission_keys`) **kopyalanmaz**;
  hiçbiri Recovery'de yayımlanmadığı için takma ad gerekmez.
- Aktör: `tenant_user`. Arka plan aktörü bu dilimde yok.

| Recovery anahtarı | Katman | Uçlar | Kaynak karşılığı |
|---|---|---|---|
| `mdm.banks.read` | T1 | banka ve şube liste / detay | `MOD0289_BANK_READ`, `MOD0289_BRANCH_READ` |
| `mdm.banks.create` | T1 | banka ve şube oluştur | `MOD0289_BANK_CREATE`, `MOD0289_BRANCH_CREATE` |
| `mdm.banks.update` | T1 | banka ve şube düzenle | — |
| `mdm.banks.activate` / `mdm.banks.deactivate` | T2 | banka ve şube durum | — |
| `mdm.banks.delete` / `mdm.banks.bulk-delete` | T1 / T2 | taslak silme | — |
| `mdm.bank-accounts.read` | T1 | liste, detay, `lookup-validation` | `MOD0289_LE_ACCOUNT_READ`, `MOD0289_SUPPLIER_DEST_READ` |
| `mdm.bank-accounts.read-tenant-wide` | T3 (yalnız açık atama) | kiracıdaki tüm şirketlerin hesapları — **yalnız okuma** | — |
| `mdm.bank-accounts.create` | T1 | oluştur | `MOD0289_LE_ACCOUNT_CREATE`, `MOD0289_SUPPLIER_DEST_CREATE` |
| `mdm.bank-accounts.update` | T1 | düzenle | `MOD0289_GOVERNED_MUTATION_WRITE` (kısmen) |
| `mdm.bank-accounts.activate` / `mdm.bank-accounts.deactivate` | T2 | durum (+ dört göz) | — |
| `mdm.bank-accounts.delete` / `mdm.bank-accounts.bulk-delete` | T1 / T2 | taslak silme | — |
| `mdm.bank-accounts.read-unmasked` | T3 (yalnız açık atama, O-3) | `unmasked-reads` | `MOD0289_TOKEN_READ` (yakın) |
| `mdm.bank-accounts.sensitive-reads.read` | T1 | okuma kayıtları listesi | `MOD0289_SENSITIVE_ACCESS_LOG_READ` |

- **Taşınmayan kaynak anahtarlar:** token, değişiklik talebi, varsayılan atama, kanıt, istisna, içe aktarma, vekâlet,
  muafiyet, operasyon, kural anlık görüntüsü, doğrulama koşusu, pano anahtarları (v1 dışı ya da taşınmaz).
- `read-unmasked` ve `read-tenant-wide` hiçbir rol şablonunda varsayılan değildir ve otomatik yoldan verilmez:
  `ExplicitGrantOnlyPermissions`'a eklenmeleri Auth değişikliğidir (D9; Auth sahibi) — Ready-for-dev engeli.
- Tier-3 eylemler (`read-tenant-wide`, `read-unmasked`) bu pakette kayıtlıdır (PKS-001 §5 Tier 3); `sensitive-reads.read`
  dört parçalı anahtardır (PKS-001 ≥ 3 parça).

## Audited Events

**Yol `b` — `mdm-guvenilir-kaynak-niyeti` izinin genişletilmesi (öneri, O-5).** Ölçüldü (@fabf0050c):

- `mdm-merkezi-iletim` (`AuditForwardingBehavior` → `POST /api/internal/audit/append`) **en iyi çabadır**: iletim hatası
  yutulur (`services/Diten.MdmService/src/Diten.MdmService.Application/Behaviors/AuditForwardingBehavior.cs:7-11,71-75`),
  adres / anahtar boşsa `LogDebug` ile atlanır (`services/Diten.MdmService/src/Diten.MdmService.Api/Audit/PlatformAuditForwarder.cs:47`;
  AUD-001 §4.2). K2 sınıfı için uygun değildir.
- `mdm-guvenilir-kaynak-niyeti`: denetim niyeti (`LocalAuditIntent`, `Domain/Entities/LocalAuditIntent.cs:5-50`) iş belgesinin
  içinde (`IAuditIntentAggregate`, `IAuditIntentAggregate.cs:3-7`) **aynı koşullu yazımda** ya da aynı Mongo işleminde
  yazılır; `AuditIntentDeliveryProcessor` onu Platform'un güvenilir kaynak ucuna iletir; defterde CT kararıyla (2026-10-07)
  kabul edilmiş (`tests/architecture/audit-ledger/Diten.MdmService.md` `## İzler`). Niyet yazılamazsa iş verisi de
  yazılmaz → kapalı-başarısız (K2'nin "işlem içi outbox" yolu, AUD-001 §4.3).
- Bu yol bugün yalnız ürün ailesini taşıyor: `AuditAggregateType` dokuz değer (`Domain/Enums/AuditAggregateType.cs:3-14`),
  işlem enum'u `ProductAuditOperation` (`Domain/Enums/ProductAuditOperation.cs:3`), teslim deposu sabit dokuz koleksiyon
  (`Persistence/Repositories/AuditIntentDeliveryRepository.cs:15-23`). Genişletme yalnız eklemedir ama defter belirteci
  (`Tip.Metot` yazma üyeleri) değişir → **CT kararı**. Bu MDM'nin kendi izidir; başka servise kopya değildir (AUD-001 K4).
- Defterin "Pencere 🟡" notundaki (başlatmadan sonra yazılan niyet) durum bu modülde yoktur: uzak çağrı yapan yazma
  komutu yalnız tedarikçi kontrolüdür ve o çağrı **yazmadan önce** yapılan bir okumadır.

`SourceModule`: `MOD-0289`. `Category`: Platform `AuditCategory`'de banka / finans değeri yok (MOD-0126 pack ölçümü,
`AuditEnums.cs:3-27`); MOD-0126 O-6 ile ortak karar — değer uydurulmaz. K2: banka hesabı yazmaları ve maskesiz okuma
**öneri K2** (ödeme yönlendirme verisi; AUD-001 §4.3 listesinde açıkça sayılmıyor — O-5 içinde sahip kararı); banka/şube
yazmaları K2 değil ama aynı yoldan yazıldığı için pratikte kapalı-başarısızdır.

| Komut | Olay adı | Nesne | Yol | Önceki/sonraki ya da değişen alanlar | K2 | İstisna sınıfı + gerekçe |
|---|---|---|---|---|---|---|
| CreateBankCommand | mdm.bank.created | Bank | b | AfterState: Code, Status, CountryCode, Bic var/yok | hayır | — |
| UpdateBankCommand | mdm.bank.updated | Bank | b | değişen alan **adları** | hayır | — |
| ActivateBankCommand | mdm.bank.activated | Bank | b | Status önce→sonra | hayır | — |
| DeactivateBankCommand | mdm.bank.deactivated | Bank | b | Status, DeactivationReasonCode | hayır | — |
| DeleteBankCommand | mdm.bank.deleted | Bank | b | BeforeState: Code, Status | hayır | — |
| BulkDeleteBankCommand | mdm.bank.deleted (kimlik başına bir niyet) | Bank | b | aynı | hayır | — |
| CreateBankBranchCommand | mdm.bank-branch.created | BankBranch | b | BankId, BranchCode, Status | hayır | — |
| UpdateBankBranchCommand | mdm.bank-branch.updated | BankBranch | b | değişen alan adları | hayır | — |
| ActivateBankBranchCommand | mdm.bank-branch.activated | BankBranch | b | Status | hayır | — |
| DeactivateBankBranchCommand | mdm.bank-branch.deactivated | BankBranch | b | Status, gerekçe kodu | hayır | — |
| DeleteBankBranchCommand | mdm.bank-branch.deleted | BankBranch | b | BeforeState: BranchCode, Status | hayır | — |
| CreateBankAccountCommand | mdm.bank-account.created | BankAccount | b | Code, OwnerType, sahip kimliği, BankId, CurrencyCode, AccountIdentifierType, Status (**tanımlayıcı, son 4, maske, sahip adı yok**) | **öneri evet** | — |
| UpdateBankAccountCommand | mdm.bank-account.updated | BankAccount | b | değişen alan **adları** (Draft'ta tanımlayıcı değiştiyse yalnız `AccountIdentifier` adı) | **öneri evet** | — |
| ActivateBankAccountCommand | mdm.bank-account.activated | BankAccount | b | Status, ActivationVerificationCode, aktör ≠ oluşturan | **öneri evet** | — |
| DeactivateBankAccountCommand | mdm.bank-account.deactivated | BankAccount | b | Status, DeactivationReasonCode | **öneri evet** | — |
| DeleteBankAccountCommand | mdm.bank-account.deleted | BankAccount | b | BeforeState: Code, Status | **öneri evet** | — |
| BulkDeleteBankAccountCommand | mdm.bank-account.deleted (kimlik başına bir niyet) | BankAccount | b | aynı | **öneri evet** | — |
| RevealBankAccountIdentifierCommand (**okuma satırı**, AUD-001 §7) | mdm.bank-account.identifier-revealed | BankAccountSensitiveRead | b | BankAccountId, ReasonCode (değer yok) | **öneri evet** | — |

- **Okuma denetimi:** maskesiz okuma AUD-001 §7'nin üç sınıfından birine açıkça girmez; bu paket onu daha sıkı kuralla
  denetler (hassas finansal veri). Okuma denetimi merkezi günlüğe gider; `BankAccountSensitiveRead` yalnız taşıyıcı
  belgedir, eşdeğer iz değildir. Diğer okumalar (liste, detay, lookup) denetlenmez.
- **Bilinen boşluk:** reddedilen komut (`Outcome = Denied`) merkezi kayıt bırakmaz — niyet yalnız başarılı yazımla
  oluşur (MOD-0126 G-AUD-1 ile aynı sınıf); reddedilen maskesiz okuma denemeleri yalnız uygulama günlüğünde (değer
  olmadan) görünür. Merkezi ret kaydı follow-up.
- **Arka plan işi yok**, bu yüzden İ2 satırı yok. Kaynak seed işçisi taşınmaz.

## Gateway / API Routing Decision

- **Karar: Gateway değişikliği gerekli.** `ocelot.json`'da `/api/mdm/{everything}` catch-all yok; MDM rotaları kaynak
  başına açık yazılmış (ör. `/api/mdm/brands`, `/api/mdm/brands/{everything}` — `gateway/Diten.ApiGateway/ocelot.json:3781,3798@fabf0050c`).
  Gerekli rotalar: `/api/mdm/banks`, `/api/mdm/banks/{everything}`, `/api/mdm/bank-branches/{everything}`,
  `/api/mdm/bank-accounts`, `/api/mdm/bank-accounts/{everything}` → `localhost:5059`; `UpstreamHttpMethod` GET, POST, PUT,
  DELETE, OPTIONS; açık rotalar catch-all'dan önce (`routes.md`).
- `ocelot.json` korunur: **integration-agent task'ı** olarak ayrı yürütülür.
- Kaynak rotaları (`/api/v1/mod0289`, `/api/v1/mod0289/{everything}` — kaynak `ocelot.json:998-1022@da4abb20`, paket §C)
  taşınmaz.
- MdmService → MOD-0140 çağrısı Gateway'in `/api/suppliers/validate` rotası üzerinden; rotanın Gateway'de var olduğu ve
  çağrının hangi kimlikle yapılacağı (kullanıcı token'ı mı, servis kimliği mi) DL-3 başında ölçülür — **ölçülmedi**.

## Acceptance Criteria

AC durumu kabul kaydında (`scripts/acceptance_record.py`) tanımlı / uygulanmış / çalıştırılmış / geçmiş olarak izlenir,
pack'te asla izlenmez; bu pack'teki her AC yalnız **"tanımlı"**dır.

- [ ] AC-1 (zorunlu) — `POST /api/mdm/banks` geçerli istekle 201 döner; banka `Draft`, `Code` RCS-001 biçiminde ortak
  servisten (istekte gönderilen kod 400), `TenantId` sunucuda yazılı, yanıt DTO'sunda yok.
- [ ] AC-2 (zorunlu) — Banka / şube doğrulaması: BIC biçimi ve ülke eşleşmesi, şube BIC'inin ilk 8 karakteri, şubenin
  başka bankaya bağlanması 400; aynı BIC, aynı ülke + ulusal kod, aynı şube kodu 409. Gerçek replica set'te iki eşzamanlı
  aynı-BIC oluşturmadan biri 409 alır; tekil indeks kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-3 (zorunlu) — Başka kiracının bankası, şubesi, hesabı ve okuma kaydı için okuma, güncelleme, silme, aktivasyon,
  maskesiz okuma ve `lookup-validation` 404 döner. Depo `TenantFilter`'ı kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-4 (zorunlu) — Soft delete yalnız `ActivatedAt = null` Draft kayıtta; `IsDeleted` + `DeletedAt` (UTC) birlikte
  yazılır, silinen kayıt listede görünmez, kodu yeniden kullanılmaz; aktive edilmiş kayıtta silme 409; BulkDelete'te tek
  uygun olmayan kimlik bütün isteği 409 yapar ve hiçbir kayıt silinmez.
- [ ] AC-5 (zorunlu) — Durum geçişleri yalnız Durumlar tablosundakilerdir; pasif banka ya da şubeye yeni hesap Create /
  Activate 422 `MDM_BANK_INACTIVE`; banka pasifleştirildiğinde şube ve hesap belgeleri değişmez (belge sürümleri aynı).
- [ ] AC-6 (zorunlu) — Her yazma komutu tam **bir** denetim niyetini iş belgesiyle aynı koşullu yazımda / işlemde bırakır
  (Bulk komutlarda kimlik başına bir); niyet yazımı zorla başarısız edildiğinde komut 503 döner ve iş verisi yazılmaz
  (sabotaj testi); `AuditIntentDeliveryProcessor` yeni koleksiyonlardaki niyeti teslim eder (teslim deposu listesinden
  koleksiyon çıkarıldığında teslim testi kırmızıya döner).
- [ ] AC-7 (zorunlu) — `dotnet test tests/architecture/TenantArchitecture.ArchitectureTests --filter AuditTrailStandard`
  yeşil; `git diff <taban>..HEAD -- tests/architecture/audit-ledger/ | grep -E '^\+- '` boş (borç satırı eklenmemiş);
  defter değişikliğinin CT-DECISIONS satırı aynı değişiklikte.
- [ ] AC-8 (zorunlu) — Bu modülün her denetleyici aksiyonunda `[HasPermission]` vardır (mimari testi); izinsiz çağrı 403.
- [ ] AC-9 (zorunlu) — Bayat `If-Match` ile Update / Activate / Deactivate 409 `MDM_BANK_VERSION_CONFLICT`, sessiz üzerine
  yazma yok; iki eşzamanlı Activate'ten biri 409. Güncelleme süzgecinden `Version` koşulu kaldırıldığında test kırmızıya
  döner (sabotaj).
- [ ] AC-10 (zorunlu) — Şirket hesabı Create 201 döner; sahip aynı kiracıda `Active` şirkettir (başka kiracının ya da
  `Draft` / `Suspended` şirketin kimliği 422); `OwnerType` ve sahip alanları sonraki Update'te değişmez (409); tam bir sahip
  kuralı (ikisi dolu, ikisi boş, türle uyuşmayan) 400.
- [ ] AC-11 (zorunlu) — IBAN kuralları: geçerli örnek vektörler (en az TR, DE, GB, FR) kabul, sağlaması bozulmuş, kısa,
  uzun, küçük harfle normalize edilebilen ve bankanın ülkesiyle uyuşmayan IBAN'lar beklenen sonucu verir; BIC ve yurt içi
  numara kuralları aynı biçimde; bu doğrulamalarda ağ çağrısı sayısı 0. MOD 97-10 kontrolü kaldırıldığında test kırmızıya
  döner (sabotaj).
- [ ] AC-12 (zorunlu) — Aynı tanımlayıcı (farklı biçimde yazılmış — boşluklu / küçük harfli dahil) kiracıda ikinci kez
  kaydedilemez: 409 `MDM_BANK_ACCOUNT_DUPLICATE`, gövde ilk kaydın kimliğini, kodunu ya da sahibini içermez; başka
  kiracıda aynı tanımlayıcı kaydedilebilir.
- [ ] AC-13 (zorunlu) — Saklama: oluşturulan hesabın Mongo belgesi ve koleksiyondaki bütün indeksler normalize
  tanımlayıcıyı (ve onun 5+ karakterlik herhangi bir parçasını, son 4 hane dışında) içermez (ham belge taraması);
  `AccountIdentifierHash` aynı tanımlayıcı için aynı, düz SHA-256 ile eşleşmez; şifreleme anahtarı yapılandırılmamışsa
  servis açılmaz; anahtar sürümü değişince eski kayıt okunabilir. Koruyucu düz metin yazacak şekilde değiştirildiğinde test
  kırmızıya döner (sabotaj).
- [ ] AC-14 (zorunlu) — Maskeleme: liste, detay, `lookup-validation` ve okuma kaydı DTO'larında tam tanımlayıcı alanı yok
  (DTO alan adı taraması — yalnız `BankAccountRevealDto` istisna); yanıt gövdeleri, denetim niyetleri ve testte yakalanan
  uygulama günlükleri (Create, Update, doğrulama hatası, istisna yolu) normalize tanımlayıcıyı içermez. Tanımlayıcı bir
  günlük satırına ya da doğrulama iletisine eklendiğinde test kırmızıya döner (sabotaj).
- [ ] AC-15 (zorunlu) — Maskesiz okuma: `read-unmasked` izni yok → 403, değer yok, okuma kaydı yok; gerekçe yok → 400;
  izin + gerekçe + kapsam → 200, değer ve `Cache-Control: no-store`, tam bir `BankAccountSensitiveRead` ve tam bir denetim
  niyeti değerden önce aynı işlemde; okuma kaydı yazımı zorla başarısız edildiğinde 503 ve değer dönmez (sabotaj).
- [ ] AC-16 (zorunlu) — `mdm.bank-accounts.read-unmasked` ve `mdm.bank-accounts.read-tenant-wide`
  `ExplicitGrantOnlyPermissions` listesindedir ve hiçbir rol şablonunda varsayılan değildir (Auth tarafındaki mevcut
  açık-atama testine bağlanır; Auth değişikliği D9 yoluyla).
- [ ] AC-17 (zorunlu) — Veri kapsamı (SEC-002 §4 kanıt testi): aynı yetkiye sahip iki kullanıcı, iki şirket → birinin
  listesinde diğerinin şirket hesabı çıkmaz, detayı / maskesiz okuması / `lookup-validation`'ı 404; şirket kapsamı olmayan
  üçüncü kullanıcı boş liste görür; `read-tenant-wide` olan iki şirketi görür ama kapsamı olmayan şirketin hesabında Update,
  Activate, Deactivate, Delete 404 alır; çözücü hata verince liste boş. Kapsam süzgeci kaldırıldığında ya da kiracı geneli
  okuma kapsamı yazma yoluna sızdırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-18 (zorunlu) — Activate: şirket `Active` ama `ApprovalStatus ≠ Approved` → 422 (O-2 farklı karar verirse bu madde
  o kararla yeniden yazılır, numarası korunur); oluşturan ya da son güncelleyen kullanıcının Activate'i 403
  `MDM_BANK_ACCOUNT_FOUR_EYES_REQUIRED` (O-1); başka kullanıcı geçer. Dört göz kontrolü kaldırıldığında test kırmızıya
  döner (sabotaj).
- [ ] AC-19 (zorunlu) — `lookup-validation` (şirket sahibi): Active hesap 200 ve sözleşme tablosundaki alanlar; 404 vakaları
  (yok, silinmiş, başka kiracı, kapsam dışı, Draft, Inactive) aynı gövdeyle ayrı ayrı; hesap aktive edildikten **sonra**
  şirket askıya alındığında hesap belgesi değişmeden `ownerState = NOT_ELIGIBLE` + `LEGAL_ENTITY_NOT_ACTIVE`, onayı geri
  çekildiğinde `LEGAL_ENTITY_NOT_APPROVED`; banka pasifleştirildiğinde `bankState = INACTIVE`. Sahip durumu hesapta saklanıp
  oradan okunacak şekilde değiştirildiğinde test kırmızıya döner (sabotaj).
- [ ] AC-20 (zorunlu) — `BANK-ACCOUNT-LOOKUP` 1.0.0 sözleşme dosyası yayımlanmış; uç, izin, 404 kuralları, alanlar,
  `contractVersion`, tüketici sınıflandırma tablosu ve her satır için örnek gövde sözleşmeyle eşleşir (sözleşme testi);
  200 gövdesinde `iban`, `accountNumber`, `bic`, `bankName`, `branchName`, `accountHolderName` anahtarlarından hiçbiri yok.
- [ ] AC-21 (zorunlu) — Active / Inactive hesapta tanımlayıcı, para birimi, banka, şube ya da sahip değişikliği 409
  `MDM_BANK_ACCOUNT_IMMUTABLE_FIELD`; Draft'ta tanımlayıcı değiştiğinde yeni şifreli değer, yeni özet ve son 4 yazılır,
  eski şifreli değer belgede kalmaz.
- [ ] AC-22 (zorunlu) — Şifreleme anahtarı çalışırken erişilemez hâle getirildiğinde Create ve Reveal 503
  `MDM_BANK_PROTECTION_UNAVAILABLE` döner ve kayıt / okuma kaydı / niyet yazılmaz; RCS-001 ve MOD-0048 erişilemez
  olduğunda Create 503 ve kayıt yok.
- [ ] AC-23 (zorunlu) — Tedarikçi durumu yalnız MOD-0140 sözleşmesinden okunur: mimari testi MdmService'te
  `mod0140_suppliers` dizgisinin ve ProcurementService derleme / ad alanı başvurusunun olmadığını doğrular; bağdaştırıcı
  yalnız `POST /api/suppliers/validate`'i çağırır. Koleksiyon okuması eklendiğinde test kırmızıya döner (sabotaj).
- [ ] AC-24 (zorunlu) — Tedarikçi yanıtı sınıflandırması (sahte MOD-0140, her vaka ayrı): `known: true` + `Active` →
  Create / Activate sürer; `OnHold` / `Blocked` / `Inactive` ve `known: false` → 422; sonuçta istenen kimlik yok ya da
  farklı, `known` boolean değil, `status` dört değerin dışında (`"active"` küçük harf dahil), ayrıştırılamayan gövde, 401,
  403, 500 ve zaman aşımı → 503 `MDM_BANK_ACCOUNT_OWNER_UNAVAILABLE`; 422 ve 503 vakalarında kayıt ve niyet yazılmaz.
  `lookup-validation` aynı sınıflandırmayla `ownerState` üretir ya da 503 döner. Sabotaj: "yanıt okunamazsa uygun say"
  yapıldığında test kırmızıya döner.
- [ ] AC-25 (zorunlu) — Tedarikçi sahibi MOD-0140 `supplierId` dizgisi olarak olduğu gibi saklanır ve sıralı karşılaştırılır
  (`SUP-2001` ≠ `sup-2001`); tedarikçi sahipli hesabın `lookup-validation`'ı `X-Legal-Entity-Id` olmadan 400, kiracıya ait
  olmayan / aktif olmayan şirketle 404; aynı tedarikçi hesabı iki şirketten okunduğunda her biri kendi ilişki durumunu alır.
- [ ] AC-26 (zorunlu) — Bu modülün yazdığı hiçbir belge `TenantId = Guid.Empty` taşımaz: entegrasyon testi her yazma
  komutundan sonra modülün bütün koleksiyonlarını tarar; depo boş kiracıyla yazmayı reddeder. Bir yazma yolunda kiracı
  `Guid.Empty`'ye sabitlendiğinde test kırmızıya döner (sabotaj).
- [ ] AC-27 (zorunlu) — Bir banka oluşturma, bir hesap oluşturma, bir hesap aktivasyonu ve bir maskesiz okuma dev ortamında
  çalıştırılır; dört merkezi kayıt AUD-001 §8 sorgusuyla gösterilir, on alan işaretlenir ve hiçbirinde tanımlayıcı yoktur
  (rapor ortamın adını yazar).
- [ ] AC-28 (zorunlu) — Tüm `Views/MasterData/{Banks,BankAccounts}/*.cshtml` dosyalarında `Layout = "_LayoutTenantShell"`
  açıkça yazılı; `_CreateEditOffcanvas.cshtml` / `_DetailsQuickView.cshtml` yok;
  `python3 .antigravity/scripts/verify_datatable_page.py . --area MasterData --module Banks --format gaps` ve aynı komut
  `--module BankAccounts` ile sapma vermez, `quality-gate-datatable` geçer; iki sayfada `<table data-dt-data-mode="server">`
  pack `screens` satırıyla eşleşir.
- [ ] AC-29 (zorunlu) — 7 dil (`en, tr, fr, es, zh, ar, ru`) resx parite PASS (iki ekran, sayfa yardımı, nav); İngilizce
  olmayan dosyada İngilizce yer tutucu yok; hardcoded metin yok (`grep` 0).
- [ ] AC-30 (zorunlu) — `read` izni olmayan kullanıcı sayfayı açınca kabuk içinde erişim açıklaması görür; başlık, tablo,
  "Yeni" düğmesi çizilmez, yönlendirme olmaz (UAS-001, canlı denendi); "Maskesiz göster" düğmesi `read-unmasked` olmayana
  çizilmez; maskesiz değer diyalog kapandıktan sonra DOM'da, `localStorage` / `sessionStorage`'da ve konsolda yoktur
  (tarayıcı smoke).
- [ ] AC-31 (zorunlu) — Sayfa yardım metni `Help.BANKS.*` ve `Help.BANKACCOUNTS.*` (`Purpose`, `Audience`, `Step1…Step4` +
  `Step{n}Title`) 7 dilde var; `PageHelpCatalog` satırları eklendi.
- [ ] AC-32 (zorunlu) — Tarayıcı JS hiçbir isteği 5059'a ya da Gateway'e doğrudan yapmaz; yalnız same-origin vekil
  (`/MasterData/...`); vekil maskesiz okuma yanıt gövdesini günlüğe yazmaz (vekil testinde yakalanan günlükte değer yok).

## Test Expectations

- **Birim:** IBAN (MOD 97-10, normalize, ülke), BIC, yurt içi numara kuralları; maske ve son 4 türetimi; doğrulayıcılar
  (her alan); durum makinesi; dört göz (Guid karşılaştırması, büyük/küçük harf farklı `sub`); tedarikçi ve LE yanıt
  sınıflandırması; RCS kod portu sahtesiyle kod biçimi.
- **Entegrasyon (Mongo replika kümesi, işlem):** kiracı izolasyonu (404); veri kapsamı (AC-17, sabotajlı); soft delete;
  CAS 409; tekil indeksler ve yarış (AC-2, AC-9, AC-12); denetim niyetinin iş belgesiyle birlikte yazılması ve niyet yazım
  hatasında komutun başarısız olması (sabotaj); okuma kaydı önce-yaz; ham belge taraması (AC-13); `Guid.Empty` taraması
  (AC-26).
- **Günlük yakalama:** test günlükçüsüyle Create / Update / hata / istisna yollarında tanımlayıcı yokluğu (AC-14).
- **Sahte MOD-0140:** dört durum, `known: false`, geçersiz gövde, 401/403/500, zaman aşımı; 1.3.0 main'e girince gerçek
  sözleşmeye karşı sözleşme testi.
- **Mimari:** her aksiyonda `[HasPermission]`; `mod0140_suppliers` / ProcurementService başvurusu yok; DTO'larda tam
  tanımlayıcı alan adı yok (`BankAccountRevealDto` hariç); `AuditTrailStandard` filtresi yeşil.
- **Sözleşme:** `BANK-ACCOUNT-LOOKUP` 1.0.0 örnek gövdeleri; tüketici paketleri (MOD-0126, MOD-0119, MOD-0120, MOD-0124)
  kendi sınıflandırma testlerini kendi kapsamlarında yazar.
- **Frontend:** browser smoke (iki ekran: liste, oluştur, düzenle, detay, aktive et, maskesiz göster izinli / izinsiz);
  yetkisiz kullanıcı canlı denemesi (UAS-001); DataTable doğrulayıcı PASS; RESX 7 dil parite PASS.
- **Build:** `Diten.MdmService` + `Diten.Web` 0 hata; yerel kapıda `scripts/` Python testleri de koşulur.
- **Kaynak testleri kanıt değildir:** kaynakta F02 / PAIR işlem testlerinin 61'i ürün koduna ulaşmadan fikstür
  önkoşulunda durdu, 54 başarısızlığın nedeni belirlenmedi (paket §H) — hiçbiri güvence olarak alıntılanmaz; bütün testler
  Recovery'de yeniden yazılır (level-1 koşul 1).

## Page Help (UXH-001)

- Sayfa kodları `BANKS` (`Nav.Page.BANKS`) ve `BANKACCOUNTS` (`Nav.Page.BANKACCOUNTS`); Create/Edit/Details aynı kodun
  alt sayfalarıdır.
- `Help.BANKS.Purpose` (kiracının banka ve şube listesi; ödeme yapılmadığı açıkça yazılır), `Help.BANKS.Audience` (ana
  veri sorumlusu), `Help.BANKS.Step1…Step3` + `Step{n}Title` (Banka oluştur · Şube ekle · Aktive et).
- `Help.BANKACCOUNTS.Purpose` (şirket ve tedarikçi banka hesapları sicili; hesap numarasının maskeli gösterildiği ve
  maskesiz görmenin kayda geçtiği yazılır), `Help.BANKACCOUNTS.Audience` (hazine operatörü, tedarikçi ana veri sorumlusu,
  hesap onaylayıcı), `Help.BANKACCOUNTS.Step1…Step4` + `Step{n}Title` (Hesap oluştur · Doğrula · Başka kişi aktive eder ·
  Gerekirse pasifleştir). `Immediate` yok.
- **Tur:** gerekmez — beş akıştan biri değil → "tur yok".

## Ready-for-dev Checklist

**Biçim maddeleri (paket içi):**
- [x] Golden Reference Compact okundu (backend + frontend klasörü)
- [x] Frontmatter tam (service, shell, golden_reference, data_mode, screens, entity_base, form_field_count, approval_status)
- [x] Layout & Shell Contract'ta `Layout = "_LayoutTenantShell"` açık
- [x] Backend / Frontend file convention Golden Reference ile (suffix yok, CommandHandlers/QueryHandlers ayrı)
- [x] Validation her alan için yazılı; dört göz ve sahiplik kuralları
- [x] Failure Path ≥ 4 senaryo (duplicate, missing, unauthorized, concurrency) + kiracılar arası + kapsam + hassas veri
- [x] Authorization: izin listesi + policy + aktör
- [x] Gateway kararı (gerekli, integration-agent task'ı)
- [x] Audited Events her yazma komutunu ve maskesiz okumayı tam bir kez içeriyor
- [x] Veri kapsamı cümlesi (SEC-002 §4) ve kanıt testi (AC-17)
- [x] Sayfa yardımı anahtarları ve "tur yok"
- [x] Acceptance criteria kimlikli ve test edilebilir

**ENGELLER (hepsi kapanmadan `ready-for-dev` olmaz):**
- [ ] CT-APR-02 bağımsız inceleme bu paket sürümü için kaydedildi (`execution/approvals/reviews.tsv`), açık ENGEL yok
- [ ] CT-APR-01 sürüme bağlı onay satırı (`execution/approvals/approvals.tsv`); yüksek risk (hassas finansal veri) —
  paketi yazmamış, `high: evet` onaylayıcı
- [ ] Sahip MOD-0289 için kod alma iznini verdi (D1 — bugün izin yalnız MOD-0126 ilk adayı için konuşuldu; MOD-0289 için yok)
- [ ] DCP (`UNASSIGNED-finance-treasury-intake`) `approved`, TRM-S3 dilimi tanımlı
- [ ] MDM domain-config'e MOD-0289 satırı ve frontend/gateway kapsam cümlesi (bugün istisna yalnız MOD-0290-FU02)
- [ ] O-1…O-12 kararları (özellikle O-1 dört göz / iş akışı, O-2 şirket onayı, O-3 maskesiz okuma sahipleri, O-5 denetim
  yolu + K2, O-6 şifreleme ve anahtar yönetimi)
- [ ] RCS-001 ortak kod servisi (BL-565) ve `BNK` / `BAC` önekleri RCS-001 §3'te (O-7; `.antigravity` onayı)
- [ ] Denetim izinin genişletilmesi için Control Tower kararı (defter belirteci + CT-DECISIONS satırı)
- [ ] Şifreleme anahtarı kaynağı ve döndürme prosedürü (O-6) — DL-2 için
- [ ] SEC-002 kapsam sağlayıcısının MOD-0289 anahtarlarıyla çalıştığı ölçüldü (O-9) — DL-2 için
- [ ] Auth: `ExplicitGrantOnlyPermissions`'a iki anahtar (D9, Auth sahibi) — DL-2 için
- [ ] MOD-0048 para birimi listesinin MdmService'ten tüketim yolu ölçüldü — DL-2 için
- [ ] MOD-0140 `supplier.openapi.yaml` 1.3.0 main'de (MVP-2) ve MdmService → MOD-0140 çağrı kimliği belirlendi — DL-3 için
- [ ] MDM `ValidationBehavior`'ın 400 gövdesinde girilen değeri döndürüp döndürmediği ölçüldü — DL-2 için
- [ ] Gateway rotaları (integration-agent) — DL-4 için

## Implementation Notes

### Kaynak ve kanıt (provenance)

- Producer paketi MOD-0289 v1.7 (TRM-B7-03-R1), `manifest.json` sha256
  `58be78ae0cd6bdd49d60fe978ea5d89d2382a18cae52ebcc530f3a0b6f7b5647` (bu oturumda ölçüldü); kaynak commit
  `da4abb204e05fbd47ec088a6b0da08b8f7c6dc5d` (bağlı 110 S dosyası, paket §O). Kaynak kodu bu oturumda okunmadı.
- Kaynak derleme kaydı (devralınmış, yeniden koşulmadı): R2 `Diten.Platform.sln` ve R3 `Diten.MdmService.sln` @da4abb20
  çıkış 0 (paket §O). MOD-0289 test sayıları R2 API 104 geçti / 72 kaldı, R2S 1 / 45 (paket §H) — Recovery hazırlığının
  ölçüsü değildir.
- Kod alma (D1, kopya değil): izin verilirse alınan her dosya `path@da4abb20` + sha256 ile iz kaydına yazılır; aşağıdaki
  liste taşınmaz. Kaynak Platform kompozisyonu (Program/DI, denetim boru hattı, `TenantScopedEntity`) MDM'ye taşınmaz;
  kurallar MDM düzenine yeniden yazılır.
- `target: 2027-01-31` planlama yer tutucusudur, sahip kararı değildir.

### Level-1 koşulları (`MOD-0289-v1.7.md`) → bu paket

1. F02/PAIR işlem güvenceleri alıntılanmaz, testler yeniden kurulur → Test Expectations son madde; F02 uçları taşınmaz;
   hesap + bağ tek belgede (PAIR gerekmez); AC-6, AC-9, AC-13, AC-15 Recovery testleri.
2. Outbox `TenantId = Guid.Empty` taşınmaz → outbox ve iş çalıştırma varlıkları taşınmaz; AC-26.
3. Tedarikçi durumu MOD-0140 sözleşmesinden (1.3.0, aktif şirket ilişkisi) → `ISupplierEligibilityPort`, AC-23…25; 1.3.0
   bugün yok → DL-3 engeli.
4. Yerleşim MDM → D20/1 ile karar verildi; bu paket MDM düzenine eşler (EntityBase, `RepositoryBase` + `TenantFilter`,
   `mdm.*` izinleri, MDM denetim izi, MDM rotaları).

### Tüketici onay slotları (paket §P, 14 slot) → bu paket

LE → süreç içi `LegalEntity` okuması (Dependencies); SUPPLIER → MOD-0140 1.3.0 (DL-3); REF → MOD-0048 para birimi, ülke
O-8; AUTH → `[HasPermission]` + explicit-grant; AUDIT → Audited Events (O-5); PAYMENT ve CASH-CONSUMER →
`BANK-ACCOUNT-LOOKUP`; UI → DL-4; WORKFLOW → v1'de yok (O-1); EVIDENCE → v1'de yok (O-4); EVENTS ve SEED → taşınmaz
(yayıncı ve seed yok); CUSTOMER → kapsam dışı (sahipsiz, kimlik uydurulmaz); REGISTRY → MOD-0003 yok, sözleşme
`docs/analysis/contracts/` altında. Slot kapanışları §U kaydında izlenir; bu paket hiçbir ACK oluşturmaz.

### Taşınmayacaklar (do-not-carry)

1. Operasyonel outbox ve iş çalıştırma (`BankAccountOperationalOutboxItem`, `BankAccountOperationalJobRun`, API-071…075):
   `TenantId = Guid.Empty` (`Mod0289BankService.cs:2450@da4abb20`) ve depo yansımasıyla kiracı damgalama varsayımı
   (`EntityVariations.cs:10`, `Repositories.cs:81-90` — paket §G); hiçbir öğe yayımlanmıyor (paket §F).
2. `mod0140_suppliers` koleksiyonunun doğrudan okunması (`Mod0289BankRepository.cs:51@da4abb20`).
3. F02 / PAIR işlem güvenceleri, F02 denetleyicisi ve makbuzu, pilot seçenekleri ve "uygunluk kullanılamaz" taslağı
   (`Mod0289AccountCreateEligibilityUnavailable`, paket §D matris C12) — güvence olarak alıntılanmaz.
4. Platform kompozisyonu: `Diten.Platform` barındırması, denetleyici eylem süzgecine dayalı kimlik koruması
   (`Mod0289Controller.cs:43-62`) ve doğrudan çağrıda istisna atan `ResolveActorId` (`:2292-2298`), Platform denetim
   alıcısına bağlı kapanma (`:2333`), `TenantScopedEntity` tabanı.
5. Sahip durumunun kayıt anındaki kopyası (`OwnerValidationStatus` ve ilgili alanlar; aktarma servisi `:130-150`, matris
   C11) ve `Enabled = false`'u uygun sayan eski LE istemcisi (matris C05).
6. Ayrı sahiplik bağı koleksiyonları (`mod0289_account_ownership_bindings`, `Mod0289AccountCreateStore.cs:75`).
7. Token modeli ve ödeme referansı aktarma servisi (`BankAccountToken`, `AllowedConsumers`, API-033…035) — opak Guid +
   `lookup-validation` yerine geçer.
8. Doğrudan değişiklik ucu (API-076 `DirectMutateLegalEntityAccount`).
9. Seed işçisi (`Mod0289SeedBaselineWorker`, `Program.cs:83`) ve seed servisi.
10. 38 `MOD0289_*` izin literali, 70 `MOD0289_*` hata literali, `/api/v1/mod0289` rotaları (`ocelot.json:998-1022`),
    `mod0289_*` koleksiyon adları.
11. Kullanıcıdan alınan banka kodu (`CreateBankDraftRequest.BankCode`) — RCS-001 kodu yerine geçer.
12. Guid'in BSON string saklanması (`BankMasterEntities.cs:52,55`) ve string aktör kimlikleri.
13. Dış korunan referans modeli (`ProtectedAccountRef`) — Recovery'de kasa yok; yerine O-6.
14. `_LayoutBackbone` ve hardcoded İngilizce kaynak ekranları (paket §I).
15. Çok boyutlu durum modeli (onay / ödeme uygunluğu / sahip doğrulama / doğrulama durumu ayrı alanlar ve
    `MOD0289_STATE_DIMENSION_CONFLICT`).

v1 dışı (taşınmaz değil, ertelenmiş): içe aktarma, değişiklik talebi + iş akışı, varsayılan atama, istisna vakaları, kanıt
paketi ve vekâlet, ülke-kanal kural seti, doğrulama koşuları ve muafiyetleri, sağlık panosu.

### Tasarım notları

- Hesabı tek belgede tutmak kaynağın en büyük doğrulanmamış güvencesini (hesap + bağ PAIR işlemi) gereksiz kılar; sahiplik
  ayrışması yapısal olarak imkânsızdır.
- Değiştirilemez yönlendirme alanları + yeni hesap açma kuralı, tüketicilerin opak referansının anlamını sabitler.
- Sahip durumunun saklanmaması, kaynağın "kopya, güncel değil" bulgusunu (paket §L.2) kapatır; bedeli MOD-0140'ın her
  `lookup-validation`'da çağrılmasıdır (önbellek v1'de yok).
- `MdmService` içinde MOD-0220 aynı süreçte olduğu için şirket uygunluğu TRM-14 sözleşmesine bağlı değildir; ama bu paket
  `LegalEntity` kaydına hiçbir şey yazmaz.

## Follow-up Items

- `BANK-ACCOUNT-LOOKUP` sözleşme dosyası (`docs/analysis/contracts/drafts/bank-account-lookup-validation-v1.0-proposal.md`) —
  DL-2 başında; tüketici paketlerine (MOD-0126, MOD-0119, MOD-0120, MOD-0124) bağ.
- Hesap arama ucu (maskeli, kapsamlı) — tüketici ekranlarındaki "opak referans giriş" geçici çözümünün (MOD-0126 O-15,
  MOD-0124 O-15) yerine.
- MOD-0269 gerçek dağıtım açılmadan önce: tam tanımlayıcıyı okuyacak servis kimliği ve dar sözleşme, tedarikçi hesabı
  doğrulama kanıtı (MOD-0031) zorunluluğu, hesap sahipliği / alacaklı eşleşmesi kapısı (MOD-0126 Runtime Constraints).
- Değişiklik talebi + MOD-0023 onay akışı (O-1 (a) seçilirse ya da v2).
- Toplu içe aktarma (RCS-001 §5.11) ve ülkeye göre IBAN uzunluk / kanal kural seti (referans veri sahibiyle).
- Pasif sahibin hesaplarını pasifleştiren ya da işaretleyen iş (İ2 tetikleyici + kendi denetimli komutu).
- Merkezi ret kaydı (`Outcome = Denied`) — denetim sahibine.
- Varsayılan hesap ataması, istisna vakaları, kanıt paketi / vekâlet.
- MDM domain-config: MOD-0289 satırı, frontend/gateway kapsam cümlesi; MDM ülke referansı kararı (O-8).
- `execution/registries/module-implementation-status.md` satırı ilk kodla açılır.

## Açık kararlar

| # | Karar | Seçenekler | Öneri | Sahibi |
|---|---|---|---|---|
| O-1 | Hesaplar v1'de onay iş akışı (MOD-0023) istesin mi? | (a) değişiklik talebi + MOD-0023 onayı (b) iş akışı yok, **dört göz aktivasyonu** (oluşturan / son güncelleyen aktive edemez) (c) kontrol yok | **(b)** — v1'de dağıtım kapalı (D5); dört göz para yönlendirme hilesine karşı temel kontrolü verir, MOD-0023'e bağımlılık ve kaynağın 11 durumlu talep makinesi gelmez. Dağıtım açılmadan önce (a) yeniden değerlendirilir | sahip (K1) |
| O-2 | Şirket hesabı için şirket Active **ve** Approved mı olmalı (D10/1 benzeşimi)? | (a) Create ve Activate'te Active + Approved (b) Create'te Active, Activate'te Active + Approved (c) yalnız Active | **(b)** — taslak hesap onay süreci sürerken hazırlanabilir; kullanıma açılış Treasury'nin şirket kuralıyla aynı; `lookup-validation` `ownerState`'i de Active + Approved'a göre üretir | sahip (MDM sahibi kararını K1 verebilir, D20 yolu) |
| O-3 | Maskesiz okuma izni kimde? | (a) `mdm.bank-accounts.read-unmasked`, Tier 3, yalnız açık atama, varsayılan rol yok, gerekçe kodu zorunlu (b) hesap oluşturma izniyle birlikte (c) maskesiz okuma yok | **(a)** — gerekçe değer listesi önerisi `PAYMENT_INVESTIGATION`, `BANK_CONFIRMATION`, `AUDIT_REQUEST`, `DATA_CORRECTION`; ilk sahipler hazine yöneticisi ve iç denetim (rol şablonu MOD-0018 sahibiyle) | sahip + MOD-0018 / Auth sahibi |
| O-4 | Tedarikçi hesabı aktivasyonunda doğrulama kanıtı | (a) kanıt yok (b) zorunlu doğrulama kodu (`CALLBACK_CONFIRMED`, `BANK_LETTER_ON_FILE`, `SUPPLIER_PORTAL_CONFIRMED`) + dört göz (c) MOD-0031 kanıt bağlantısı zorunlu | **(b)** v1 — MOD-0031 bağımlılığı getirmeden kararın izini bırakır; **(c)** MOD-0269 gerçek dağıtım açılmadan önce zorunlu kapı (Follow-up) | sahip |
| O-5 | Denetim yolu ve K2 | (a) `mdm-guvenilir-kaynak-niyeti` genişletmesi (aynı yazımda niyet, kapalı-başarısız) (b) `mdm-merkezi-iletim` (en iyi çaba) (c) ortak CentralAudit (Building.Blocks) MDM'ye | **(a)** — bugün MDM'de kapalı-başarısız tek yol; hesap yazmaları ve maskesiz okuma K2 sayılsın; işlem enum'u için ürün adlı `ProductAuditOperation`'a ekleme yerine eklemeli genel `MasterDataAuditOperation` önerilir. Defter belirteci CT kararı ister. `AuditCategory` değeri MOD-0126 O-6 ile ortak | CT + sahip |
| O-6 | Tam tanımlayıcının saklanması | (a) uygulama düzeyi zarf şifreleme (AES-256-GCM, anahtar gizli depodan, sürümlü `KeyId`) + ayrı anahtarlı HMAC özet (b) MongoDB istemci tarafı / sorgulanabilir şifreleme (c) dış kasa referansı (kaynak modeli) (d) tam değeri hiç saklamama | **(a)** — repoda hiçbir şifreleme altyapısı yok (ölçüldü), (b) ek sürücü kütüphanesi ve anahtar kasası ister, (c) Recovery'de kasa yok, (d) sistem-of-record amacına aykırı. Anahtar yoksa servis açılmaz (`configuration-safety`); Development anahtarı ayrı; döndürme prosedürü DL-2 önkoşulu | sahip + güvenlik |
| O-7 | RCS-001 kayıt kodları | banka `BNK` (grup öneki, kiracı geneli) · hesap `BAC` (şirket hesabında şirket öneki, tedarikçi hesabında grup öneki) · başka | **önerildiği gibi**, sınıf OTOMATİK (çakışma yok: §3 tablosunda ve depoda `BNK`/`BAC` geçmiyor, ölçüldü); şube kodu doğal anahtar (BranchCode), RCS kodu yok | sahip + CT (`.antigravity` onayı) |
| O-8 | Ülke kodu kaynağı | (a) yalnız ISO 3166-1 alfa-2 biçim kontrolü (b) PSS-011 ülke listesi (c) MDM iş ülkesi referansı (yok) | **(a)** v1 — domain-config PSS-011'in MDM iş ülkesi kaynağı olmadığını söylüyor; MOD-0220 de `CountryCode`'u serbest dizge tutuyor (`LegalEntity.cs:27`); MDM ülke kararı gelince bağlanır | MDM sahibi |
| O-9 | Veri kapsamı | (a) şirket hesapları `LegalEntityId` ile, `ITrustedLegalEntityScopeProvider` üzerinden; tedarikçi hesapları ve bankalar kiracı geneli (b) tedarikçi hesaplarını da çağıran şirketin ilişkisine göre daralt (c) kapsam yok | **(a)** — tedarikçi ana kaydı kiracı geneli (sahip cevabı 2026-10-21); sağlayıcının MOD-0289 anahtarlarıyla çalıştığı DL-2 başında ölçülür; `NoOp` kaydı yasak | sahip + platform-shared-services |
| O-10 | `AccountPurpose` değer listesi | serbest metin / sabit kod listesi / referans listesi | kapalı kod listesi (`OPERATING`, `PAYROLL`, `COLLECTION`, `TAX`, `SUPPLIER_PAYMENT`, `OTHER`), etiketleri resx'te | sahip |
| O-11 | Banks ekranının şekli | Compact (Details'ta şube alt tablosu) / Slim (4 alan) | **Compact** — paket tek `golden_reference` taşır, şube alt tablosu Slim offcanvas'a sığmaz; sapma kaydı gerekmez | sahip |
| O-12 | Para birimi küsurat hanesi kaynağı | MOD-0289 hane taşısın / MOD-0048 | **MOD-0048** — MOD-0289 tutar tutmaz; yalnız ISO kodu saklar ve MOD-0048 listesinde olduğunu doğrular. Hane MOD-0126 O-11 / MOD-0119 O-10 ortak kararıdır | MOD-0048 sahibi |

## Teslim dilimleri

| dilim | ad | kapsadığı bölümler |
|---|---|---|
| DL-1 | banka ana verisi, denetim izi genişletmesi ve paket iskeleti | Module Summary, Ownership and Boundaries, Owned Objects, Entity Fields, Repo Scope, Protected Paths, Backend File Convention, Acceptance Criteria, Test Expectations, Ready-for-dev Checklist, Implementation Notes, Follow-up Items, Açık kararlar, Teslim dilimleri |
| DL-2 | şirket banka hesapları, hassas veri ve okuma sözleşmesi | Runtime Constraints, Validation Rules, Authorization Convention |
| DL-3 | tedarikçi banka hesapları (MOD-0140 sözleşmesi) ve denetim kanıtı | Dependencies, Failure Path to Verify, Audited Events |
| DL-4 | kiracı ekranları | Layout & Shell Contract, Frontend File Contract, Page Help (UXH-001), Gateway / API Routing Decision |

Her bölüm tabloda bir kez, **kapandığı** dilimde yazılıdır; önceki dilimler o bölümün kendilerine düşen kısmını uygular
(ör. Audited Events'in banka satırları DL-1'de, hesap satırları DL-2'de gelir, bölüm DL-3'te tedarikçi komutları ve AUD-001
§8 dev kanıtıyla kapanır; Validation Rules'un banka kuralları DL-1'de, hesap kuralları DL-2'de uygulanır; Dependencies
MOD-0140 1.3.0 satırı kapanınca DL-3'te kapanır). Ortak bölümler (Acceptance Criteria, Test Expectations, Ready-for-dev
Checklist, Follow-up Items) her dilimde aşağıdaki AC kimlikleriyle ölçülür.

Dilim ayrıntıları (yollar · önkoşul · bitiş):

- **DL-1** — Yollar: `Domain/Entities/{Bank,BankBranch}.cs`, enum'lar, `Features/BankMaster/**`, `Bank`/`BankBranch`
  depoları ve indeksleri, `BanksController`, `BankBranchesController`, manifest sağlayıcısı, RCS kod portu, denetim izi
  genişletmesi (`AuditAggregateType`, işlem enum'u, teslim deposu koleksiyon listesi), defter + CT-DECISIONS satırı.
  **Neden önce:** banka ana verisi hassas veri içermez, denetim yolunu ve RCS bağını en düşük riskle kanıtlar. Önkoşul:
  CT-APR-02/01, kod alma izni (D1), DCP, O-5, O-7, O-11, RCS-001 servisi, CT defter kararı. Bitiş: AC-1, 2, 3 (banka ve
  şube kısmı), 4 (banka kısmı), 5 (banka ve şube kısmı), 6 (banka kısmı), 7, 8 (banka kısmı), 9 (banka kısmı).
- **DL-2** — Yollar: `Domain/Entities/{BankAccount,BankAccountSensitiveRead}.cs`, `Features/BankAccount/**` (şirket sahibi),
  `Rules/`, `Infrastructure/{BankData,Currency}/**`, `BankAccountsController`, `BANK-ACCOUNT-LOOKUP` sözleşme dosyası.
  **Neden ayrı:** hassas veri, şifreleme ve veri kapsamı modülün en riskli kısmıdır; bağımsız inceleme tek başına
  yapılabilsin. Önkoşul: DL-1, O-1, O-2, O-3, O-6, O-9, O-10, O-12, şifreleme anahtarı, Auth explicit-grant eki, kapsam
  sağlayıcısı ölçümü, MOD-0048 tüketim yolu, `ValidationBehavior` ölçümü. Bitiş: AC-3 (hesap kısmı), 4 (hesap kısmı),
  5 (hesap kısmı), 6 (hesap kısmı), 8 (hesap kısmı), 9 (hesap kısmı), 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22,
  26 (şirket hesabı kısmı).
- **DL-3** — Yollar: `Infrastructure/Suppliers/**` (`ISupplierEligibilityPort` bağdaştırıcısı), tedarikçi sahibi dalları
  (Create, Activate, `lookup-validation`), mimari test (koleksiyon okuması yok), dev ortamı denetim kanıtı. **Neden ayrı:**
  MOD-0140 1.3.0'a bağlı; DL-2'yi bekletmesin. Önkoşul: DL-2, MOD-0140 1.3.0 main'de, MdmService → MOD-0140 çağrı kimliği
  ve Gateway rotasının ölçümü, O-4. Bitiş: AC-23, 24, 25, 26 (tedarikçi kısmı), 27.
- **DL-4** — Yollar: `Views/MasterData/{Banks,BankAccounts}/**`, JS, resx (7 dil), MVC vekilleri, sayfa yardımı, nav
  anahtarları; Gateway rotaları integration-agent task'ı olarak açılır (bu dilim yazmaz). **Neden ayrı:** ekran API
  sözleşmesine bağlıdır, arka uç değişmeden ilerler. Önkoşul: DL-2 (hesap ekranı), DL-3 (tedarikçi sahibi seçimi), Gateway
  rotaları, MDM domain-config frontend/gateway kapsam cümlesi. Bitiş: AC-28, 29, 30, 31, 32.
