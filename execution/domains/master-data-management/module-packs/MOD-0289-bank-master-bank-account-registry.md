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
  - module: BankAccountPurposes
    data_mode: client
    data_mode_max_rows: 200
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
> (MOD-0023 politika kararları — bu paket v1'de MOD-0023 kullanmaz, D23/2), **D23/1–4** (MOD-0289 pack kararları,
> 2026-10-25: tedarikçi hesabı kapsamı "ilişkili şirketler", v1'de MOD-0023 yok + dört göz, şirket `Active` /
> etkinleştirmede ve lookup'ta `Active` + `Approved`, maskesiz okuma ayrı izin + gerekçe + değerden önce denetim) —
> `docs/records/decisions/2026-10/treasury-intake-owner-decisions-02.md` (D23; bu pack revizyonuyla aynı commit'te).
> Yerleşim notu:
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
> aktarılmıştır (paket atıfı). Recovery atıfları `yol:satır@158e0dc93` (bu worktree, ölçüldü; ilk ölçüm `fabf0050c`'de
> yapıldı ve `git diff --stat fabf0050c 158e0dc93 -- services frontend gateway tests docs/analysis AGENTS.md .antigravity`
> boş — iki commit arasında yalnız bu pack ve kayıt satırı değişti, atıflar aynı satırlara işaret eder).
>
> **Revizyon 2 (2026-10-25):** sahip kararları D23/1–4 uygulandı; sahip gap listesi 13 (CONDITIONAL PASS) kapatıldı —
> kapanış tablosu "Implementation Notes → Gap 13 kapanışı".
>
> **Revizyon 3 (2026-10-25):** sahip gap listesi 14 (CONDITIONAL PASS) kapatıldı — "Implementation Notes → Gap 14
> kapanışı". Kod atıfları yeniden ölçülmedi: `git diff --stat 158e0dc93 c37f2188d -- services frontend gateway tests
> docs/analysis AGENTS.md .antigravity` boş (ölçüldü), bu yüzden `@158e0dc93` atıfları `c37f2188d`'de aynı satırlara işaret
> eder.
>
> **Revizyon 4 (2026-10-25):** sahip gap listesi 15 (CONDITIONAL PASS) kapatıldı — "Implementation Notes → Gap 15
> kapanışı". Bu revizyonda yeniden ölçülen iki satır `@373513765` ile atıflanır; diğer atıflar değişmedi.
>
> **Revizyon 5 (2026-10-25):** sahip gap listesi 16 (CONDITIONAL PASS) kapatıldı — "Implementation Notes → Gap 15b /
> Gap 16 kapanışı". Kapılar ikiye ayrıldı: tasarım / onay kapısı = `ready-for-dev`; dilim kabul kapısı = "Teslim
> dilimleri → Dilim kabul kapısı". Bu revizyonda kod yeniden ölçülmedi.
>
> **Revizyon 6 (2026-10-25):** sahip kararları **D24/1–9** uygulandı (O-4, O-5, O-6 sahip onayı, O-7, O-8, O-10, O-11,
> O-12 ve kiracısız hedef kaydının denetimi) — `treasury-intake-owner-decisions-02.md` D24 (bu pack revizyonuyla aynı commit'te). **O-6'da güvenlik onayı ve gerçek Mongo testleri
> hâlâ açıktır** (D24/3 onları kapatmaz). Kapanış tablosu "Implementation Notes → D24 uygulaması". Yeniden ölçülen iki satır
> `@54b605877` ile atıflanır.

## Module Summary

Kiracının banka ana verisi (banka + şube kimliği) ve banka hesabı sicili için system-of-record. Blueprint 8.8 W-3,
ERP Suite / Treasury & Cash; Blueprint amacı: "Own bank master data and bank account registries for legal entities and
suppliers while payment execution remains with treasury/payment gateway capabilities" (yerleşim notu §1).

- **Hedef kullanıcı:** ana veri sorumlusu (banka ve şube tanımı), hazine / finans operatörü (şirket hesabı açar),
  tedarikçi ana veri sorumlusu (tedarikçi hesabı açar), hesap onaylayıcı (dört göz aktivasyonu, D23/2), sınırlı sayıda
  "maskesiz görme" yetkilisi (D23/4).
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
  MOD-0023 onay akışı (D23/2: v1'de yok); varsayılan hesap ataması, istisna vakaları, kanıt paketi / vekâlet
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
| 14–15 | `BankAccountChangeRequest` (:337), `BankAccountChangeRequestActionLog` (:390) | **v1 dışı** (D23/2) | MOD-0023 onay akışına bağlı; v1'de dört göz aktivasyonu (D23/2) |
| 16 | `BankAccountDefaultAssignment` (:430) | **v1 dışı** | varsayılan hesap seçimi tüketici kuralıdır; ayrı karar |
| 17–18 | `BankAccountExceptionCase` (:459), `BankAccountExceptionEvidenceLink` (:496) | **v1 dışı** | istisna kuyruğu ve kanıt bağlama (MOD-0031) |
| 19–21 | `CountryRailValidationRuleSnapshot` (:572), `AccountValidationRun` (:591), `AccountValidationResult` (:612) | **v1 dışı** | v1 kuralları koddaki biçim kurallarıdır (Validation Rules); ülke / kanal kural seti referans veri sahibi ister (MOD-0048, kaynak REF slotu) |
| 22–25 | `BankAccountEvidencePack` (:636), `BankAccountEvidenceLink` (:658), `BankAccountMandate` (:676), `ValidationOverrideRequest` (:697) | **v1 dışı** | kanıt / vekâlet / muafiyet; tedarikçi hesabı doğrulama kanıtı D24/1 (v1'de doğrulama kodu; MOD-0031 MOD-0269 öncesi) |

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
| API-012 `POST …/activation` | `ActivateBankAccountCommand` | `POST /api/mdm/bank-accounts/{id}/activate` | taşınır + dört göz (D23/2) |
| API-033…035 token oku / yeniden ver / iptal | — | — | **taşınmaz** (token yok) |
| API-036 `GET sensitive-access-logs` | `GetBankAccountSensitiveReadListQuery` | `GET /api/mdm/bank-accounts/{id}/sensitive-reads` | uyarlanır (hesap başına) |
| API-013…028 doğrulama koşusu, kural anlık görüntüsü, kanıt, muafiyet, vekâlet, doğrulama muafiyeti | — | — | v1 dışı (follow-up) |
| API-037…048 değişiklik talebi + iş akışı eylemleri | — | — | v1 dışı (D23/2) |
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
| `CreateBankAccountPurposeCommand`, `UpdateBankAccountPurposeCommand` (yalnız etiketler), `ActivateBankAccountPurposeCommand`, `DeactivateBankAccountPurposeCommand`, `DeleteBankAccountPurposeCommand`; `GetBankAccountPurposeListQuery`, `GetBankAccountPurposeByIdQuery` | `GET/POST /api/mdm/bank-account-purposes`, `GET/PUT/DELETE /api/mdm/bank-account-purposes/{id}`, `POST …/{id}/activate`, `POST …/{id}/deactivate` | KARAR D24/6: çekirdek kodlar + kiracı ek amaçları; liste çekirdek kodları da `isCore = true` ile döner, çekirdek koda yazma uçları yoktur (`{id}` yalnız kiracı amacıdır) |
| `ApproveBankDataKeySetTargetCommand` | uç yok — yalnız döndürme işi, onaylı yapılandırmadan gönderir | servis düzeyi onaylı anahtar kümesi hedefini yazar (gap 15/2) |
| `AdvanceBankDataKeyFenceCommand` | uç yok — yalnız döndürme işi gönderir | kiracının anahtar kümesi çitini onaylı hedefe ilerletir (gap 14/1, 15/2) |
| `RotateBankAccountProtectionCommand` | uç yok — yalnız `BankAccountProtectionRotationWorker` (barındırılan iş, varsayılan kapalı, sistem aktörü) gönderir | anahtar döndürme: hesap başına yeniden şifreleme + eksik özet sürümlerinin yazılması + emekli özet sürümünün silinmesi (gap 13 P1-b, iyileştirme 2) |

- **DTO'lar (tek dosya, Golden Reference):** `BankModels.cs` (`BankListItemDto`, `BankDetailDto`, `BankWriteRequest`,
  `BankBranchDto`, `BankBranchWriteRequest`) ve `BankAccountModels.cs` (`BankAccountListItemDto`, `BankAccountDetailDto`,
  `BankAccountWriteRequest`, `BankAccountRevealRequest`, `BankAccountRevealDto`, `BankAccountSensitiveReadDto`,
  `BankAccountLookupDto`). DTO'larda ve isteklerde `TenantId` **yoktur**. `BankAccountRevealDto` dışındaki hiçbir DTO tam
  tanımlayıcı taşımaz.
- **Portlar (Application):** `ISupplierEligibilityPort` (MOD-0140), `ICurrencyReferencePort` (MOD-0048),
  `IRecordCodePort` (RCS-001), `IBankAccountIdentifierProtector` (şifreleme + anahtarlı özet, D24/3),
  `IBankAccountUniquenessKeyBuilder` (türe göre tekillik anahtarı, gap 13 P1-a), `IIbanCountryFormatSource` (ülke biçimi
  kapısı, gap 13 P2 — v1'de bağlı kaynak yok), `IBankAccountDataScopeSource` (SEC-002, D23/1). Şirket sahibi için port yok: `ILegalEntityRepository` süreç içi okunur
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
  boyutu** kullanır; onay boyutu yoktur (D23/2: v1'de MOD-0023 yok, dört göz aktivasyonu), sahip uygunluğu saklanmaz, her
  okumada güncel olarak hesaplanır.
- **Durum türetilmez:** Bank `Inactive` olursa şubeleri ve hesapları otomatik değişmez; yeni hesap açma ve
  `lookup-validation` bankanın güncel durumuna bakar (Runtime Constraints).

## Entity Fields

Taban: MDM'nin `EntityBase`'i (`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/EntityBase.cs:3-12@158e0dc93`):
`Id`, `TenantId`, `IsDeleted`, `DeletedAt`, `CreatedAt`, `UpdatedAt`, `Version` — tekrar tanımlanmaz. İş kaydı olduğu
için `CreatedBy` / `UpdatedBy` (`Guid?`, token `sub`'ından; DTO'dan değil) **zorunlu**. Kaynak string aktör kimlikleri
(`RequestedBy`, `IssuedBy`, `ActorRef` …, CONTRACT-STATE) taşınmaz. Kaynak Guid'leri BSON string olarak saklıyordu
(`BankMasterEntities.cs:52,55@da4abb20`, paket §E); Recovery MDM'nin genel `GuidSerializer(Standard)` kaydını kullanır
(`services/Diten.MdmService/src/Diten.MdmService.Persistence/Configurations/BrandProductClassMaps.cs:18@158e0dc93` yorumu).

**Kiracı:** `TenantId` depo tabanında sunucuda yazılır (`RepositoryBase.cs:31,42`) ve her okuma `TenantFilter` (TenantId +
`IsDeleted = false`) ile süzülür (`RepositoryBase.cs:69-71`) — yol önekleri
`services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/`, `@158e0dc93`. Gövdede / sorguda `tenantId`
gelirse 400. `GlobalEntity` kullanılmaz: banka kataloğu da kiracıya aittir (her kiracı kendi banka listesini tutar;
kiracılar üstü ortak banka kataloğu v1 dışı).

**Soft delete:** taban `DeleteAsync` `IsDeleted` + `DeletedAt` + `UpdatedAt` yazar (`RepositoryBase.cs:54-67@158e0dc93`).
**Dikkat (ölçüldü):** taban `UpdateAsync` `Version`'ı artırır ama süzgeçte sürüm yoktur (`RepositoryBase.cs:40-52`) —
iyimser eşzamanlılık sağlamaz. Bu modül `LegalEntityRepository.UpdateEditableFieldsAsync` desenini (süzgeçte
`Version == expectedVersion`, `LegalEntityRepository.cs:33-44@158e0dc93`) kullanır; taban `UpdateAsync` çağrılmaz.

### Bank (`mdm_banks`)

| Alan | Tip | Kural | Kaynak |
|---|---|---|---|
| Code | string | RCS-001 OTOMATİK; tür öneki `BNK` (KARAR D24/4), şirket öneki yerine grup öneki (kiracı geneli kayıt, RCS-001 §5.3); istekte gelirse 400; silinen kaydın kodu yeniden kullanılmaz | kaynak `BankCode` kullanıcıdan alınıyordu (`CreateBankDraftRequest`, `Mod0289Contracts.cs:5@da4abb20`) |
| Name | string | zorunlu, trim, 1..200 | `BankName` |
| CountryCode | string(2) | zorunlu, ISO 3166-1 alfa-2 biçimi, büyük harf (KARAR D24/5: yalnız biçim; MDM iş ülkesi kaynağı gelince bağlanır) | `CountryCode` |
| Bic | string? | ISO 9362; 8 ya da 11 karakter; Validation Rules | `SwiftBic` |
| NationalBankCode | string? | normalize: trim, boşluk ve tire atılır, büyük harf; 1..35, `^[A-Z0-9]+$`; ülke içi banka kodu (ör. EFT kodu, BLZ, sort code). Bankada `Domestic` hesap açılacaksa **zorunlu** (gap 13 P1-a) | yeni |
| DomesticKeyFenceVersion | int | 0'dan başlar; yurt içi anahtar çiti (gap 14/2): yurt içi hesap oluşturan / anahtarını değiştiren her işlem ve bankanın anahtar alanlarını değiştiren her işlem bu alanı **aynı Mongo işleminde** koşullu artırır (CAS) — iki eşzamanlı işlemden biri yazma çakışmasıyla düşer | yeni |
| DomesticKeyIncludesBranch | bool | varsayılan `false`; `true` ise bu bankadaki `Domestic` hesaplar şube ister ve tekillik anahtarına şube kodu girer (ülkenin hesap numarası şube içinde tekilse). Ülke tablosu yazılmaz; ana veri sorumlusu banka başına ayarlar. Bankada en az bir silinmemiş `Domestic` hesap varken değiştirilemez (409 — anahtarlar değişirdi) | yeni (gap 13 P1-a) |
| Status | `BankLifecycleStatus` | Durumlar | `Status` |
| CreatedBy / UpdatedBy / ActivatedBy / DeactivatedBy | Guid? | token `sub` | — |
| ActivatedAt / DeactivatedAt | DateTimeOffset? | UTC | — |
| DeactivationReasonCode | string? | Deactivate'te zorunlu, 1..64 | — |
| AuditIntents / AuditIntentReceipts | `List<LocalAuditIntent>` / `List<LocalAuditIntentReceipt>` | `IAuditIntentAggregate` (Audited Events) | MDM deseni |

### BankBranch (`mdm_bank_branches`)

| Alan | Tip | Kural | Kaynak |
|---|---|---|---|
| BankId | Guid | zorunlu; aynı kiracıda var olan, silinmemiş banka | `BankId` |
| BranchCode | string | zorunlu; normalize NationalBankCode ile aynı (trim, boşluk/tire at, büyük harf), 1..35; bankada tekil; şubede silinmemiş `Domestic` hesap varken değiştirilemez (409) | `BranchCode` |
| DomesticKeyFenceVersion | int | Bank'taki aynı alan, şube düzeyinde (şube kodu anahtara girdiğinde) | yeni (gap 14/2) |
| Name | string | zorunlu, 1..200 | `BranchName` |
| CountryCode | string(2) | zorunlu; bankanın ülkesine eşit (öneri) | `CountryCode` |
| City | string? | ≤ 100 | `City` |
| Bic | string? | 11 karakterli şube BIC'i; ilk 8 karakter bankanın BIC'ine eşit (banka BIC'i varsa) | yeni |
| Status, CreatedBy, UpdatedBy, …, AuditIntents | — | Bank ile aynı | — |

### BankAccount (`mdm_bank_accounts`)

| Alan | Tip | Kural | Kaynak |
|---|---|---|---|
| Code | string | RCS-001 OTOMATİK; tür öneki `BAC` (KARAR D24/4); şirket hesabında şirket kısa kodu öneki, tedarikçi hesabında grup öneki | yeni |
| OwnerType | enum `LegalEntity`=1, `Supplier`=2 | zorunlu; **oluşturulduktan sonra değişmez** | kaynakta iki ayrı tip + `OwnerType` string (`:236,308`) |
| LegalEntityId | Guid? | `OwnerType = LegalEntity` ise zorunlu, değilse boş; değişmez | `LegalEntityId` (:41) |
| SupplierId | string? | `OwnerType = Supplier` ise zorunlu, değilse boş; MOD-0140 sözleşmesindeki `supplierId` **olduğu gibi** (string, sıralı karşılaştırma; ölçüldü: `validateSuppliers` kimlikleri string ve `StringComparer.Ordinal` ile eşler, `services/Diten.ProcurementService/src/Diten.ProcurementService.Application/Features/Supplier/Handlers/QueryHandlers/ValidateSuppliersHandler.cs:22-29@158e0dc93`); trim, 1..64; değişmez | kaynak `SupplierId : Guid` (:265) — sözleşme string'dir |
| BankId / BranchId | Guid / Guid? | aynı kiracıda; şube verilmişse o bankaya ait; `Domestic` hesapta bankanın `DomesticKeyIncludesBranch = true` ise şube zorunlu | `BankId`, `BranchId` |
| CurrencyCode | string(3) | zorunlu, `^[A-Z]{3}$`, MOD-0048 listesinde; Active hesapta değişmez | `CurrencyCode` |
| AccountIdentifierType | enum `Iban`=1, `Domestic`=2 | zorunlu | yeni |
| AccountIdentifierEnvelope | string (base64) | AES-256-GCM zarfı: biçim baytı `0x01` ‖ 12 bayt nonce ‖ şifreli metin ‖ 16 bayt etiket (Runtime Constraints → "Şifreleme", KARAR D24/3 — sahip onayı; güvenlik onayı açık); **düz metin hiçbir alanda yok** | kaynak `ProtectedAccountRef` (dış referans modeli — taşınmaz, Recovery'de kasa yok) |
| EncryptionKeyVersion | string | zarfı üreten anahtar sürümü (ör. `k2026a`); ek doğrulanmış veriye (AAD) de girer | yeni |
| UniquenessKeyDigests | string[] | tekillik anahtarının **her etkin özet anahtarı sürümüyle** HMAC-SHA256'sı, `"{hashKeyVersion}:{hex}"` biçiminde (döndürme sırasında birden çok öğe); düz SHA-256 değil (IBAN uzayı küçük, düz özet tersine çevrilebilir) | kaynak `FingerprintHash` (yalnız import satırında) |
| HashKeyVersions | string[] | `UniquenessKeyDigests`'teki sürümler (sorgu ve başlangıç kontrolü için) | yeni (gap 13 P1-b) |
| WrittenWithKeySetId | string | kaydı son yazan örneğin anahtar kümesi kimliği (gap 14/1 çiti; yalnız tanılama ve döndürme sayımı — doğruluk çitin kendisindedir) | yeni (gap 14/1) |
| IdentifierCheckLevel | enum `FormatAndChecksum`=1, `CountryFormat`=2, `DomesticFormat`=3, `DomesticVerified`=4 | sunucu yazar. IBAN: v1'de `FormatAndChecksum`; ülke biçimi kapısı açık ve kaynak bağlıyken geçen IBAN `CountryFormat`. Yurt içi: v1'de `DomesticFormat` (genel biçim); yurt içi doğrulama kapısı açık ve kaynaklar bağlıyken geçen hesap `DomesticVerified` (gap 14/4) | yeni (gap 13 P2, gap 14/4) |
| AccountLast4 | string(4) | sunucu türetir (normalize tanımlayıcının son 4 karakteri); istemciden alınmaz | kaynak `AccountLast4` istemciden alınıp uyumsuzlukta reddediliyordu (paket §E) |
| MaskedDisplay | string | sunucu türetir: IBAN → ülke kodu + `••••` + son 4 (ör. `TR••••1234`); yurt içi → `••••` + son 4 | kaynak `MaskedAccountDisplay` varsayılan `"MASKED"` |
| AccountHolderName | string | zorunlu, trim, 1..140 (ISO 20022 ad uzunluğu önerisi) | `AccountHolderName` / `BeneficiaryName` |
| AccountPurpose | string | zorunlu; **KARAR D24/6:** sabit çekirdek kodlardan biri (`OPERATING`, `PAYROLL`, `COLLECTION`, `TAX`, `SUPPLIER_PAYMENT`, `OTHER`) ya da aynı kiracının **aktif** `BankAccountPurpose` kodu; büyük harf, sıralı karşılaştırma | `AccountPurpose` (≤ 120, serbest) |
| PaymentUsage | string? | ≤ 120 | `PaymentUsage` |
| DisplayName | string? | ≤ 120; kullanıcı etiketi | `AccountDisplayName` / `DestinationLabel` |
| Status | `BankLifecycleStatus` | Durumlar | `LifecycleStatus` |
| CreatedBy, UpdatedBy, ActivatedBy, ActivatedAt, DeactivatedBy, DeactivatedAt, DeactivationReasonCode | — | Bank ile aynı | — |
| ActivationVerificationCode | string? | tedarikçi hesabı aktivasyonunda zorunlu doğrulama kodu (KARAR D24/1: `CALLBACK_CONFIRMED` \| `BANK_LETTER_ON_FILE` \| `SUPPLIER_PORTAL_CONFIRMED`) | yeni |
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
> MaskedDisplay / UniquenessKeyDigests / AccountIdentifierEnvelope / IdentifierCheckLevel (sunucu), Status, ActivationVerificationCode (Activate diyaloğunda, form alanı değil),
> TenantId. 11 > 8 → Compact (ayrı Create / Edit / Details).
>
> **Banks ekranı:** Bank formunda 5 kullanıcı alanı var (Name, CountryCode, Bic, NationalBankCode, DomesticKeyIncludesBranch) — tek başına Slim'e
> düşer. **KARAR D24/7:** Banks ekranı Compact (Details sayfasında şube alt tablosu ve şube oluştur/düzenle; Slim offcanvas
> alt tablo taşımaz).

### Hesap amaçları — çekirdek kodlar + BankAccountPurpose (`mdm_bank_account_purposes`, yeni — KARAR D24/6)

- **Çekirdek kodlar** (`OPERATING`, `PAYROLL`, `COLLECTION`, `TAX`, `SUPPLIER_PAYMENT`, `OTHER`) **kodda sabittir**
  (`BankAccountPurposeCatalog.Core`), veritabanında satırları yoktur; bu yüzden düzenlenemez, pasifleştirilemez, silinemez.
  Etiketleri resx'tedir: `BankAccountPurpose.Core.{KOD}` anahtarı 7 dilde (`BankAccountsIndex.{en,tr,fr,es,zh,ar,ru}.resx`).
- **Kiracı amaçları** kiracının ekleyebildiği referans verisidir:

| Alan | Tip | Kural |
|---|---|---|
| (taban) | `EntityBase` | `TenantId` sunucuda; soft delete |
| Code | string | zorunlu; büyük harfe normalize; `^[A-Z][A-Z0-9_]{1,31}$`; kiracıda tekil (silinmişler dahil — kod yeniden kullanılmaz); çekirdek kodlarla çakışamaz (409 `MDM_BANK_ACCOUNT_PURPOSE_CODE_RESERVED`); oluşturulduktan sonra **değişmez** (hesaplar koda başvurur) |
| Labels | sözlük `kültür → metin` | **7 dilin hepsi zorunlu** (`en, tr, fr, es, zh, ar, ru`); her biri trim, 1..80, kontrol karakteri yok; eksik ya da fazladan kültür anahtarı 400 |
| IsActive | bool | varsayılan `true` |
| UsageFenceVersion | int | hesap yazımı ile pasifleştirme / silme yarışını kapatan çit (aşağıda) |
| CreatedBy / UpdatedBy / DeactivatedBy / DeactivatedAt | — | token `sub`, UTC |
| AuditIntents / AuditIntentReceipts | — | `IAuditIntentAggregate` |

- **Kod sınıfı (RCS-001):** amaç kodu bir **sözlük anahtarıdır**, kayıt kodu değildir — MOD-0220'deki `LegalFormCode` gibi
  doğal, anlamlı, kiracının seçtiği bir referans değeridir (ölçüldü:
  `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs:14@54b605877`). RCS-001 §6 "kullanıcıya
  görünen kodu olan her yeni kayıt türü" der (`.antigravity/rules/record-coding-standard.md:142@54b605877`); sözlük
  kodlarının bu kapsamda olup olmadığı kuralda açıkça yazılı değil — bu paket doğal anahtarı seçer ve sınıflandırmanın CT
  tarafından teyidini Ready-for-dev maddesi yapar.
- **Pasif amaç:** var olan hesaplar değişmez (belge sürümü aynı, `lookup-validation` etkilenmez); yeni Create ya da amacı bu
  koda değiştiren Update 422 `MDM_BANK_ACCOUNT_PURPOSE_INACTIVE`. Yeniden aktive edilebilir.
- **Silme:** yalnız hiçbir silinmemiş hesap kullanmıyorsa (soft delete); kullanılıyorsa 409
  `MDM_BANK_ACCOUNT_PURPOSE_IN_USE` (pasifleştirme önerilir).
- **Yarış çiti:** kiracı amacıyla hesap yazan işlem aynı Mongo işleminde amaç belgesini `{Id, TenantId, IsActive = true,
  IsDeleted = false, UsageFenceVersion = okunan}` süzgeciyle `$inc UsageFenceVersion` yapar; pasifleştirme ve silme aynı
  alanı artırır (silme ayrıca işlem içinde kullanım sayar) — eşzamanlı iki işlemden biri yazma çakışmasıyla düşer
  (gap 14/2 deseni).
- **Gösterim (sabit metin yok):** API amaç listesini `{code, isCore, isActive, labels?}` olarak döner; çekirdek kodda
  `labels` yoktur ve ekran resx anahtarını o anki UI kültürüyle çözer; kiracı amacında ekran `labels[CultureInfo.CurrentUICulture
  .TwoLetterISOLanguageName]` değerini gösterir — 7 dil zorunlu olduğu için eksik dil durumu oluşmaz; yine de beklenmeyen
  eksiklikte ekran **kodu** gösterir (veri, sabit metin değil).
- **Sınır:** kiracı başına en çok 200 ek amaç (doğrulama; `data_mode: client`, `data_mode_max_rows: 200`).

### BankAccountSensitiveRead (`mdm_bank_account_sensitive_reads`, yeni — yalnız ekleme)

| Alan | Tip | Kural |
|---|---|---|
| BankAccountId | Guid | — |
| ActorId | Guid | token `sub` |
| ReasonCode | string | zorunlu, 1..64 (D23/4); değer listesi O-3 satırında (değer listesi sahip kararı değil — rol şablonuyla birlikte yazılır) |
| ReasonComment | string? | ≤ 500; tanımlayıcı içeremez (sunucu, normalize tanımlayıcının yorumda geçmediğini kontrol eder) |
| OccurredAt | DateTimeOffset | UTC |
| Outcome | enum `Revealed`=1 | yalnız başarılı okuma kaydı; reddedilen deneme merkezi denetimde de görünmez (G-AUD-1 benzeri boşluk, Audited Events) |
| AuditIntents / AuditIntentReceipts | — | merkezi günlüğe iletim (okuma denetimi merkezi günlüğe gider, AUD-001 §7) |

Depo arayüzünde `Update`, `Delete`, `Replace` yoktur (AUD-001 §4.1).

### BankDataKeyFence (`mdm_bank_data_key_fences`, yeni — gap 14/1)

| Alan | Tip | Kural |
|---|---|---|
| TenantId | Guid | kiracı başına bir belge (tekil); `EntityBase` |
| ActiveKeySetId | string | bu kiracıda yazmaya izinli **tek** anahtar kümesi kimliği |
| PreviousKeySetId | string? | bir önceki küme (tanılama) |
| Epoch | int | her ilerletmede +1 |
| AppliedTargetRevision | long | çitin uyguladığı hedef revizyonu; ilerletme yalnız `target.ApprovalRevision > AppliedTargetRevision` iken ve hedefe yapılır — eski iş bir çiti geri alamaz (gap 16/1) |
| WriteSeq | long | bu kiracıdaki her banka hesabı yazımı işlem içinde +1 (çakışmayı zorlamak için) |
| AdvancedAt / AdvancedBy | DateTimeOffset / string | son ilerletme (sistem aktörü) |
| AuditIntents / AuditIntentReceipts | — | ilerletme denetlenir (Audited Events) |

**Anahtar kümesi kimliği (`KeySetId`) — kanonik serileştirme (gap 15/1):** örneğin yapılandırmasından türetilir ve
**güncel anahtar seçimini de içerir**; aynı anahtar listesine sahip ama farklı "güncel" sürüm seçen iki örnek farklı
kimlik üretir. Girdi, aşağıdaki satırların bu sırayla UTF-8 olarak, her biri tek `\n` (LF) ile biten birleşimidir:

```text
mdm-bank-keyset/2
enc.current=<CurrentEncryptionKeyVersion>
hash.current=<CurrentHashKeyVersion>
<tür>|<sürüm>|<durum>|<kcv>        (her anahtar için bir satır)
```

- Anahtar satırları `tür` (`enc` < `hash`), sonra `sürüm` sıralı (ordinal) dizge karşılaştırmasıyla sıralanır; `durum`
  `Active` \| `Retiring` \| `DecryptOnly` (yapılandırmadaki yazımla, büyük-küçük harf duyarlı); `kcv` =
  `HMAC-SHA256(anahtar, "mdm-bank-kcv")`'nin ilk 8 baytı, küçük harf 16 onaltılık karakter (anahtarı ifşa etmez; aynı adlı
  ama farklı baytlı anahtarı ayırır).
- Sürüm adı `^[a-z0-9-]{1,32}$` olmalıdır (ayırıcı `|` ya da satır sonu içeremez); değilse ya da güncel sürüm listede
  `Active` değilse servis açılmaz.
- `KeySetId` = `ks2-` + girdinin SHA-256'sının küçük harf onaltılık yazımı. Önceki tanım (`ks` ön ekli, güncel seçimsiz)
  hiç yayımlanmadığı için geçiş gerekmez.

### BankDataKeySetTarget (`mdm_bank_data_key_set_targets`, yeni — gap 15/2)

Servis düzeyinde **tek** belge (`Id = "bank-data"`): yazmaya izinli anahtar kümesinin merkezi olarak onaylanmış hedefi.
Kiracıya ait değildir; `EntityBase` / `GlobalEntity` değil, MDM'deki servis düzeyi durum belgesi desenidir (ölçüldü:
`AuditIntentTemporalMigrationState` `EntityBase`'ten türemez —
`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/AuditIntentTemporalMigrationState.cs:3@373513765`).

| Alan | Tip | Kural |
|---|---|---|
| Id | string | sabit `bank-data` |
| ApprovedKeySetId | string | yeni kiracı çitinin **tek** kaynağı ve her çit ilerletmesinin hedefi |
| Version | int | her onayda +1 (CAS) |
| ApprovalRevision | long | onaylı dağıtım revizyonu; **kesin artan** — yeni onay yalnız `revizyon > ApprovalRevision` iken yazılır (gap 16/1) |
| BootstrapSeq | long | her kiracı çiti oluşturulmasında işlem içinde +1 (hedef değişikliğiyle çakışmayı zorlamak için) |
| ApprovalRef | string | onay kaynağı (değişiklik kaydı / onay kimliği); zorunlu |
| History | liste | yalnız ekleme: `{Version, ApprovalRevision, ExpectedPreviousKeySetId, FromKeySetId, ToKeySetId, ApprovalRef, ApprovedAt, Actor}` |

- **Yalnız** `ApproveBankDataKeySetTargetCommand` yazar. Komutu döndürme işi gönderir: onaylı yapılandırma dört değeri
  birlikte taşır — `Mdm:BankData:Rotation:ApprovedTargetKeySetId`, `…:ApprovalRef`, `…:ApprovalRevision` (long, ≥ 1) ve
  `…:ExpectedPreviousKeySetId` (ilk kurulumda boş). Hedef **tek koşullu güncellemeyle** yazılır ve yalnız şunların hepsi
  doğruysa: (i) işi çalıştıran örneğin `KeySetId`'si `ApprovedTargetKeySetId`'ye eşit; (ii) `ApprovalRevision` >
  `target.ApprovalRevision` (kesin artan); (iii) `target.ApprovedKeySetId == ExpectedPreviousKeySetId` (ilk kurulumda hedef
  yok ve beklenen boş — eşzamanlı iki ilk kurulumu birincil anahtar çözer); (iv) kayıtlarda geçen her şifreleme ve özet
  sürümü bu örneğin kümesinde var (`ix_mdm_bank_account_key_versions` üzerinde kiracılar arası `distinct`; yoksa
  okunamayacak kayıt bırakan kümeye geçilmez). Süzgeç ve `History` `$push`'u aynı tek belge güncellemesindedir (atomik).
  **Ret:** revizyon eski ya da eşit → 409 `MDM_BANK_KEYSET_TARGET_REVISION_STALE`; beklenen önceki küme tutmuyor → 409
  `MDM_BANK_KEYSET_TARGET_PREVIOUS_MISMATCH`; (iv) tutmuyor → 409 `MDM_BANK_KEYSET_TARGET_INCOMPLETE`; her retde hedef
  **değişmez** ve iş aynı revizyonu yeniden denemez (sınıflandırma, ret sonrası okunan hedefe göre yapılır; karar
  süzgecindir). Böylece eski yapılandırmayla kalmış bir iş, K2'den sonra K1'i yeniden onaylayamaz.
- **Geri dönüş yalnız yeni ve açık bir revizyondur:** K2'den K1'e dönmek için daha yüksek revizyon, `ExpectedPreviousKeySetId
  = K2` ve kendi `ApprovalRef`'i olan yeni bir onaylı yapılandırma dağıtılır; eski revizyonun tekrar oynatılması geri dönüş
  sayılmaz. Onayın insan
  tarafı yapılandırma değişikliğinin onaylı dağıtımıdır (`ApprovalRef` ona işaret eder); hangi onay kaydının kullanılacağı
  güvenlik onayının parçasıdır (O-6: sahip onayı D24/3 var, güvenlik onayı açık).
- Hedef yoksa hiçbir kiracı çiti oluşturulamaz → banka hesabı yazmaları 503 `MDM_BANK_KEYSET_TARGET_UNAVAILABLE`
  (kapalı-güvenli).

### Mongo indeksleri

| İndeks | Anahtar | Tekil | Not |
|---|---|---|---|
| `ux_mdm_bank_code` | TenantId + Code | evet, **kısmi filtre yok** (silinen kaydın kodu dolu sayılır, RCS-001 §6) | — |
| `ux_mdm_bank_bic` | TenantId + Bic | evet, kısmi: `Bic` var ve `IsDeleted = false` | kaynak `MOD0289_SWIFT_BIC_CONFLICT` |
| `ux_mdm_bank_national_code` | TenantId + CountryCode + NationalBankCode | evet, kısmi: `NationalBankCode` var ve `IsDeleted = false` | kaynak `MOD0289_BANK_CODE_CONFLICT` |
| `ux_mdm_bank_branch_code` | TenantId + BankId + BranchCode | evet, kısmi `IsDeleted = false` | kaynak `MOD0289_BRANCH_CODE_CONFLICT` |
| `ux_mdm_bank_account_code` | TenantId + Code | evet, kısmi filtre yok | — |
| `ux_mdm_bank_account_identifier` | TenantId + UniquenessKeyDigests (çok anahtarlı — dizi öğesi başına) | evet, kısmi `IsDeleted = false` | aynı hesap kiracıda iki kez (iki sahiple) kayıt olamaz; döndürme sırasında ortak etkin sürümün özeti çakışır (gap 13 P1-b). Çok anahtarlı tekil indeksin kısmi süzgeçle bu sürücüde beklendiği gibi çalıştığı DL-2 başında gerçek replica set'te **ölçülür** (bu oturumda ölçülmedi) |
| `ux_mdm_bank_data_key_fence_tenant` | TenantId | evet | kiracı başına tek çit belgesi (gap 14/1) |
| (`mdm_bank_data_key_set_targets` `_id`) | Id | evet (birincil anahtar) | tek hedef belgesi (gap 15/2) |
| `ix_mdm_bank_account_key_versions` | EncryptionKeyVersion + HashKeyVersions | — | kiracılar arası başlangıç kontrolü ve döndürme işi (kiracı süzgeçsiz tek yönetim sorgusu; yalnız sürüm adlarını okur) |
| `ix_mdm_bank_account_owner_le` | TenantId + LegalEntityId + Status + CreatedAt(desc) | — | liste ve veri kapsamı süzgeci |
| `ix_mdm_bank_account_owner_supplier` | TenantId + SupplierId + Status | — | — |
| `ux_mdm_bank_account_purpose_code` | TenantId + Code | evet, **kısmi filtre yok** (silinen amacın kodu yeniden kullanılmaz) | D24/6 |
| `ix_mdm_bank_account_purpose` | TenantId + AccountPurpose (`mdm_bank_accounts`) | — | amaç kullanım sayımı (silme kontrolü) |
| `ix_mdm_bank_account_sensitive_read` | TenantId + BankAccountId + OccurredAt(desc) | — | — |

İndeks kurulumu `mongo-indexing` kuralına göre (MDM'de depo kurucusunda `EnsureIndexes`, örn.
`LegalEntityRepository.cs:16@158e0dc93`, ve `MongoIndexReadiness.cs`); kaynak veri taşınmaz (kaynakta kurulu şema ya da
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
`MDM_BANK_PROTECTION_UNAVAILABLE` 503 (şifreleme anahtarı ya da kayıttaki anahtar sürümü erişilemez),
`MDM_BANK_PROTECTION_INTEGRITY_FAILED` 500 (zarf doğrulanamadı: kurcalanmış metin / etiket / nonce, yanlış anahtar ya da
başka kayda taşınmış zarf; değer dönmez), `MDM_BANK_ACCOUNT_RELATIONSHIP_REQUIRED` 403 (tedarikçi hesabını kiracı geneli
izniyle maskeli gören ama ilişkisi olmayan kullanıcının maskesiz okuması, D23/1), `MDM_BANK_DOMESTIC_BANK_CODE_REQUIRED` 400
(ulusal banka kodu olmayan bankada yurt içi hesap), `MDM_BANK_DOMESTIC_BRANCH_REQUIRED` 400, `MDM_BANK_DOMESTIC_KEY_LOCKED` 409
(yurt içi hesabı olan bankada `DomesticKeyIncludesBranch` ya da kod değişikliği), `MDM_BANK_IBAN_COUNTRY_FORMAT_INVALID` 400
ve `MDM_BANK_IBAN_COUNTRY_FORMAT_UNAVAILABLE` 503 (ülke biçimi kapısı açıkken, gap 13 P2), `MDM_BANK_KEYSET_STALE` 503
(yazan örneğin anahtar kümesi kiracının çitindeki ya da onaylı hedefteki küme değil; yazma yok, gap 14/1, 15/2),
`MDM_BANK_KEYSET_TARGET_UNAVAILABLE` 503 (onaylı hedef yok; yeni kiracı çiti kurulamaz, gap 15/2),
`MDM_BANK_KEYSET_TARGET_REVISION_STALE` 409, `MDM_BANK_KEYSET_TARGET_PREVIOUS_MISMATCH` 409,
`MDM_BANK_KEYSET_TARGET_INCOMPLETE` 409 (onay reddi; hedef değişmez, gap 16/1),
`MDM_BANK_ACCOUNT_PURPOSE_NOT_FOUND` 404, `MDM_BANK_ACCOUNT_PURPOSE_CODE_RESERVED` 409, `MDM_BANK_ACCOUNT_PURPOSE_CODE_CONFLICT`
409, `MDM_BANK_ACCOUNT_PURPOSE_IN_USE` 409, `MDM_BANK_ACCOUNT_PURPOSE_INACTIVE` 422, `MDM_BANK_ACCOUNT_PURPOSE_CORE_IMMUTABLE`
409 (D24/6), `MDM_BANK_DOMESTIC_KEY_CHANGED` 409
(yurt içi hesap oluşturulurken banka / şube anahtar alanı eşzamanlı değişti, gap 14/2),
`MDM_BANK_DOMESTIC_VERIFICATION_FAILED` 400 ve `MDM_BANK_DOMESTIC_VERIFICATION_UNAVAILABLE` 503 (yurt içi doğrulama kapısı
açıkken, gap 14/4). Kullanıcıya giden ileti resx anahtarıdır.

## Repo Scope

Hepsi kod alma izninden (D1) ve `ready-for-dev`'den sonra; dilim sütunu "Teslim dilimleri" ile eşleşir.

- `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/{Bank,BankBranch,BankAccount,BankAccountSensitiveRead}.cs`,
  `Domain/Enums/{BankLifecycleStatus,BankAccountOwnerType,BankAccountIdentifierType}.cs`,
  `Domain/Repositories/I{Bank,BankBranch,BankAccount,BankAccountSensitiveRead}Repository.cs` (DL-1 / DL-2)
- `services/Diten.MdmService/src/Diten.MdmService.Application/Features/BankMaster/**` (Bank + BankBranch) ve
  `Features/BankAccount/**` (Golden Reference Compact düzeni) (DL-1 / DL-2 / DL-3)
- `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/{Bank,BankBranch,BankAccount,BankAccountSensitiveRead}Repository.cs` (DL-1 / DL-2)
- `services/Diten.MdmService/src/Diten.MdmService.Infrastructure/{BankData,Suppliers,Currency,RecordCodes}/**` (şifreleme
  koruyucusu ve anahtar seçenekleri + başlangıç doğrulaması, MOD-0140 bağdaştırıcısı, MOD-0048 bağdaştırıcısı, RCS-001
  portu) ve `Api/Services/BankData/BankAccountProtectionRotationWorker.cs` (döndürme işi, varsayılan kapalı) (DL-1 / DL-2 / DL-3)
- `services/Diten.MdmService/src/Diten.MdmService.Api/Controllers/{BanksController,BankBranchesController,BankAccountsController}.cs`
  ve `Api/ModuleRegistration/BankMasterManifestProvider.cs` (izin + sayfa manifesti, `BrandProductMasterManifestProvider.cs` deseni) (DL-1…DL-3)
- **Denetim izinin genişletilmesi (DL-1):** `Domain/Enums/AuditAggregateType.cs` (ekleme: `Bank`, `BankBranch`, `BankAccount`,
  `BankAccountSensitiveRead`), denetim işlem enum'una ekleme (KARAR D24/2: ürün adlı `ProductAuditOperation`'a değil, eklemeli genel
  `MasterDataAuditOperation`), `Persistence/Repositories/AuditIntentDeliveryRepository.cs` koleksiyon listesi (bugün sabit dokuz koleksiyon,
  `:15-23@158e0dc93`) — hepsi yalnız ekleme
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
  `Nav.Page.BANKACCOUNTS`, `Nav.Page.BANKACCOUNTPURPOSES`; `Views/MasterData/BankAccountPurposes/**`, ilgili JS ve
  `BankAccountPurposesIndex.{7 dil}.resx`, `Help.BANKACCOUNTPURPOSES.*` (DL-4). Arka uç: `Features/BankAccountPurpose/**`,
  `Domain/Entities/BankAccountPurpose.cs`, `Persistence/Repositories/BankAccountPurposeRepository.cs`,
  `Api/Controllers/BankAccountPurposesController.cs` (DL-2)

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

| Kapı / bağımlılık | Rol | Recovery durumu (ölçüm @158e0dc93) | Bu pakete etkisi |
|---|---|---|---|
| MOD-0220 Legal Entity | şirket sahibi; uygunluk | **var, aynı servis** — `LegalEntity.OperationalStatus` ve `ApprovalStatus` (`services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/LegalEntity.cs:49-50`), `IsReferenceable` (`:60`); `lookup-validation` yalnız `Active` için 200 (`…/Features/LegalEntity/Handlers/QueryHandlers/ValidateLegalEntityReferenceHandler.cs:19-25`) | süreç içi depo okuması; TRM-14'e **gerek yok** (onay durumu kayıtta okunur). Kural **KARAR D23/3** |
| MOD-0140 Supplier (`SUPPLIER`) | tedarikçi sahibi; ilişki durumu | sözleşme bu depoda **1.2.0** (`docs/analysis/contracts/supplier.openapi.yaml:13`); `validateSuppliers` 200 gövdesi `{supplierId, known, status}` + `contractVersion` (`:126-162`), `SupplierStatus` `Active/OnHold/Blocked/Inactive` (`:197`); kimlik string, kod Tenant + LE süzgeçli (`ValidateSuppliersHandler.cs:22-29`, yorum `:27`). **1.3.0 hiçbir dalda bu depoda yok** (`git log --all -- docs/analysis/contracts/supplier.openapi.yaml` → son değişiklik `780f8368a`, 1.2.0); MOD-0119 pack'i 1.3.0'ın `origin/feature/mvp2/supplier-tenant-01` dalında MVP-2 DL-2 ile yazılacağını kaydediyor (MOD-0119 pack Dependencies) — bu oturumda o dal ölçülmedi | **DL-3 engeli:** 1.3.0 main'e girmeden tedarikçi hesabı yazılamaz. Koleksiyon okuması yok (level-1 koşul 3) |
| MOD-0048 para birimi | kod listesi | `GET /api/lookups/reference/currencies` Platform'da (MOD-0126 pack ölçümü); MDM'den tüketim yolu **ölçülmedi** | `ICurrencyReferencePort`; erişilemezse Create 503, sabit liste yok |
| RCS-001 ortak kod servisi | `BNK` / `BAC` kodları | **yok** (BL-565); MDM'de yalnız ürün ailesine ait `CodeReservation` (`…/Domain/Entities/CodeReservation.cs:5-27`) | engel: modül kendi sayacını yazamaz (RCS-001 §6); servis gelmeden Create yazılamaz (O-7) |
| SEC-002 veri kapsamı | şirket hesaplarında görünürlük | MDM'de Platform'a giden güvenilir sağlayıcı **var**: `ITrustedLegalEntityScopeProvider.ResolveAsync(tenant, subject, moduleCode, permissionKey)` → `LegalEntityIds` (`…/Application/Contracts/Authorization/ITrustedLegalEntityScopeProvider.cs:3-22`), bugün ürün kapsamında kullanılıyor (`ProductLegalEntityScopeCandidateFacade.cs`); MOD-0289 izin anahtarlarıyla çalışıp çalışmadığı **ölçülmedi** | kapsam kuralı **KARAR D23/1**; sağlayıcı ölçülmeden `ready-for-dev` yok |
| MDM denetim izi | yazma ve maskesiz okuma kaydı | `mdm-guvenilir-kaynak-niyeti` (yol `b`, CT kararı 2026-10-07) ve `mdm-merkezi-iletim` (yol `b`, en iyi çaba) defterde kabul edilmiş (`tests/architecture/audit-ledger/Diten.MdmService.md` `## İzler`) | **bugün var**; yol KARAR D24/2 ile seçildi; yeni koleksiyonlar için genişletme + defter belirteci ayrı CT kararı ister (Audited Events) |
| Şifreleme altyapısı | tam tanımlayıcı | servislerde alan şifreleme yok (`git grep` `IDataProtector|AddDataProtection|ClientEncryption|AesGcm|AutoEncryption` `services/*.cs` → 0) | **engel**: O-6 — sahip onayı D24/3 var, **güvenlik onayı ve gerçek Mongo testleri açık** + anahtar yönetimi; anahtar yoksa servis açılmaz. `System.Security.Cryptography.AesGcm` ve `RandomNumberGenerator` .NET'in parçasıdır; MdmService'in hedef çerçevesinde kullanılabilirliği **ölçülmedi** (DL-2 başında `dotnet --version` + csproj hedefi) |
| MOD-0018 / Auth explicit-grant | maskesiz okuma ve kiracı geneli izinleri | `ExplicitGrantOnlyPermissions` Auth'ta (`services/Diten.AuthService/src/Diten.AuthService.Domain/Authorization/ExplicitGrantOnlyPermissions.cs:8-29`) | iki anahtar eklenmesi Auth değişikliği (D9, Auth sahibi) |
| MOD-0003 Data Contract Registry | sözleşme kaydı | kayıt satırı planned / missing; uygulama 0 dosya (paket §D, Recovery düzeltmesi) | v1'de kullanılmaz; sözleşme dosyası `docs/analysis/contracts/` altında |
| MOD-0023 Workflow | hesap onay akışı | var (Platform) | v1'de kullanılmaz (**KARAR D23/2**) |
| MOD-0031 Evidence | tedarikçi hesabı doğrulama kanıtı | var (Platform) | v1'de kullanılmaz (KARAR D24/1); MOD-0269 gerçek dağıtımından önce zorunlu kapı (Follow-up) |
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
| MOD-0140 `validateSuppliers` + ilişkili tedarikçi listesi | `supplier.openapi.yaml` **1.3.0** — yok (bugün 1.2.0); liste görünümü için "çağıran şirketle Active ilişkili tedarikçi kimlikleri" okuması gerekir — 1.3.0'da hangi işlemle verileceği **ölçülmedi** (1.2.0'da `listSuppliers` durum süzgeçli, `supplier.openapi.yaml:26-54`) | main'e girince `shasum` | procurement (MVP-2) | known + Active → ilişkili; known + OnHold/Blocked/Inactive ya da known:false → ilişkisiz (D23/1: okumada 404, yazmada 422); geçersiz / çelişkili gövde, 401/403, 5xx, zaman aşımı → 503; yazma yok, hesap gösterilmez | sahte istemci (AC-24, AC-41) · dev ortamında Active / OnHold / Blocked / ilişkisiz dört tedarikçi | DL-3 |
| MOD-0048 para birimi | `GET /api/lookups/reference/currencies` | DL-2 başında | platform-shared-services | erişilemez → 503, listede yok → 400 | sahte istemci · dev ortamında TRY/EUR/JPY | DL-2 |
| RCS-001 | **yok** (BL-565) | — | CT + `.antigravity` onayı | servis yoksa Create yazılamaz | — · — | DL-1 önkoşulu |
| SEC-002 kapsam sağlayıcısı | `ITrustedLegalEntityScopeProvider` (MDM kodu) + Platform sağlayıcı ucu | DL-2 başında | platform-shared-services | hata → boş kapsam, boş liste, detay / yazma 404 | AC-17 (sabotajlı) · dev ortamında iki şirket iki kullanıcı | DL-2 |
| Şifreleme ve özet anahtarları | O-6 (D24/3 sahip onayı; güvenlik onayı açık) — `Mdm:BankData:EncryptionKeys` / `Mdm:BankData:HashKeys` (sürümlü); üretim gizli deposu **ölçülmedi** | — | sahip + güvenlik | eksik / hatalı anahtar → servis açılmaz; kayıttaki sürüm yok → 503; doğrulama hatası → 500, değer yok | AC-13, AC-35, AC-37…AC-39 · dev ortamı anahtar döndürme denemesi | DL-2 |
| IBAN ülke biçimi kaynağı (ISO 13616 kayıt verisi) | **yok**; sahibi referans veri (MOD-0048 / REF slotu) | — | MOD-0048 sahibi | kapı kapalı (v1) → `FormatAndChecksum`; kapı açık + kaynak yok / erişilemez → 503 | AC-40 (sahte kaynak) · — | DL-2 (mekanizma); gerçek veri MOD-0269 öncesi |
| Banka dizini + yurt içi hesap kuralı kaynakları (`IBankDirectorySource`, `IDomesticAccountRuleSource`) | **yok**; sahibi adlandırılmadı (referans veri / bankacılık sahibi) | — | TBD (sahip) | kapı kapalı (v1) → `DomesticFormat`; kapı açık + kaynak yok / erişilemez → 503 | AC-47 (sahte kaynaklar) · — | DL-2 (mekanizma); gerçek veri MOD-0269 öncesi |
| Anahtar kümesi çiti + onaylı hedef (`mdm_bank_data_key_fences`, `mdm_bank_data_key_set_targets`) | MDM kodu (bu paket) | — | master-data-management | eski küme / onaysız küme → 503, yazma yok; hedef yok → 503 | AC-43, AC-44, AC-48, AC-49 (gerçek replica set) · AC-50 yük testi · dev ortamı döndürme provası | DL-2 |
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

1. **Saklama:** tam tanımlayıcı istekle gelir, sunucuda **türüne göre** normalize edilir (aşağıda "Tekillik anahtarı"),
   doğrulanır, `IBankAccountIdentifierProtector` ile şifrelenir (`AccountIdentifierEnvelope` + `EncryptionKeyVersion`,
   aşağıda "Şifreleme"), tekillik özetleri (`UniquenessKeyDigests`) ve son 4 hanesi türetilir. Düz metin hiçbir belgede,
   indekste, denetim niyetinde, olayda ya da önbellekte bulunmaz. Mekanizma O-6 (KARAR D24/3 — sahip onayı; **güvenlik onayı
   ve gerçek Mongo testleri açık**).
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
   (`services/Diten.MdmService/src/Diten.MdmService.Application/Behaviors/LoggingBehavior.cs:19,21@158e0dc93`) —
   korunur. `ExceptionHandlingBehavior` istisnayı günlüğe yazar (`ExceptionHandlingBehavior.cs:25`): tanımlayıcı hiçbir
   istisna iletisine konmaz. Doğrulama iletileri tanımlayıcının girilen değerini (`{PropertyValue}`, `AttemptedValue`)
   taşımaz; MDM `ValidationBehavior`'ın 400 gövdesinde girilen değeri geri döndürüp döndürmediği **ölçülmedi** — DL-2
   başında ölçülür, döndürüyorsa bu modülün doğrulayıcısı değeri gizler. HTTP istemci günlüklerinde gövde yazılmaz.
5. **Denetim yükü:** önce/sonra durumda tanımlayıcı, son 4 hane, hesap sahibi adı ve maskeli gösterim **yoktur**; yalnız
   değişen alan **adları**, kimlikler ve durum (AUD-001 §3). Okuma kaydı yalnız kimlik + gerekçe kodu taşır.
6. **Dış doğrulama yok:** IBAN / BIC biçim ve sağlama kuralları kodda çalışır; v1'de hiçbir dış doğrulama, banka
   dizini ya da sağlayıcı çağrısı yapılmaz (testte ağ çağrısı sayısı 0).

### Tekillik anahtarı (gap 13 P1-a)

Tekillik anahtarı, hesabın **bankacılıkta kimliğini** veren değerlerdir; yalnız hesap numarası değildir (aynı yurt içi
numara iki farklı bankada iki ayrı hesaptır). Türe göre ayrı normalize edilir ve sürümlü bir ön ek taşır:

| Tür | Normalizasyon | Tekillik anahtarı (HMAC girdisi) |
|---|---|---|
| `Iban` | boşluklar atılır (yalnız boşluk kabul edilir; tire, nokta ve diğer ayırıcılar 400), büyük harf; kanonik elektronik biçim | `bak/1\|IBAN\|{kanonik IBAN}` |
| `Domestic` | hesap numarası: boşluk ve tire atılır, büyük harf, **baştaki sıfırlar korunur** (`00123` ≠ `123`); `CountryCode` bankanın ülkesi; `NationalBankCode` ve `BranchCode` bankadaki normalize değerler | `bak/1\|DOM\|{CountryCode}\|{NationalBankCode}\|{BranchCode ya da boş}\|{hesap no}` — `BranchCode` yalnız bankanın `DomesticKeyIncludesBranch = true` olduğu durumda girer; aksi hâlde şube verilmiş olsa bile anahtarda boştur |

- Ulusal banka kodu olmayan bankada `Domestic` hesap açılamaz (400 `MDM_BANK_DOMESTIC_BANK_CODE_REQUIRED`); bankanın
  `DomesticKeyIncludesBranch = true` ise şube zorunludur (400 `MDM_BANK_DOMESTIC_BRANCH_REQUIRED`). Hangi ülkenin şube
  gerektirdiğini gösteren sabit tablo yazılmaz; ayar banka başınadır ve ana veri sorumlusunca verilir.
- Anahtarı oluşturan banka alanları (`CountryCode`, `NationalBankCode`, `DomesticKeyIncludesBranch`, şubenin `BranchCode`'u)
  bankada / şubede silinmemiş `Domestic` hesap varken değiştirilemez (409 `MDM_BANK_DOMESTIC_KEY_LOCKED`) — yoksa var olan
  kayıtların anahtarı sessizce eskirdi.
- **Kilit sorgu ile değil çitle korunur (gap 14/2):** "hesap var mı" sorgusu tek başına, eşzamanlı bir hesap oluşturmayla
  yarışabilir. Bu yüzden iki taraf da aynı belgeye yazar:
  - **Yurt içi hesap Create / Draft'ta yurt içi anahtarı değiştiren Update:** işlem içinde bankayı `{Id, TenantId,
    DomesticKeyFenceVersion = okunan, CountryCode = okunan, NationalBankCode = okunan, DomesticKeyIncludesBranch = okunan,
    Status = Active}` süzgeciyle `$inc DomesticKeyFenceVersion` yapar (şube anahtara giriyorsa şube belgesinde de aynısı,
    `BranchCode = okunan`), sonra hesabı yazar. Süzgeç tutmazsa (alan arada değişti) 409 `MDM_BANK_DOMESTIC_KEY_CHANGED`,
    hesap yazılmaz.
  - **Banka / şube anahtar alanı Update:** aynı işlemde `DomesticKeyFenceVersion`'ı koşullu artırır **ve** silinmemiş
    yurt içi hesap sayımını işlem içinde yapar; sayım > 0 ise 409 `MDM_BANK_DOMESTIC_KEY_LOCKED`.
  - İki işlem aynı belgeyi değiştirdiği için Mongo'da yazma çakışması oluşur; biri geçici hatayla geri alınır, yeniden
    denendiğinde ya hesabı görür (409 kilit) ya da değişmiş alanı görür (409 değişti). İkisinin birlikte başarılı olması
    yapısal olarak engellenir. `MasterDataAtomicWrite` politikası geçerlidir (işlem yoksa Development dışında yazma yok).
  - Hesap silme (Draft) çite dokunmaz: silme ile banka değişikliği yarışırsa en kötü durumda banka değişikliği 409 alır
    (güvenli yön).
- IBAN ile yurt içi biçim **ayrı anahtar uzaylarıdır**: aynı hesabın bir kez IBAN, bir kez yurt içi numarayla kaydı v1'de
  yakalanmaz (IBAN'dan BBAN çözümlemesi ülke biçimi verisi ister). Bu sınır kabul edilmiş bir boşluktur; ülke biçimi
  kapısıyla (gap 13 P2) birlikte kapanır (Follow-up).

### Özet anahtarı döndürme (gap 13 P1-b)

- Özet anahtarları sürümlüdür: `Mdm:BankData:HashKeys` = `{version → 32 bayt anahtar, state: Active | Retiring}` ve tek
  `CurrentHashKeyVersion`. Her yazma (Create, Draft'ta tanımlayıcı değişikliği, döndürme) tekillik anahtarının özetini
  **her `Active` sürümle** hesaplar ve hepsini `UniquenessKeyDigests`'e yazar (çift yazma); `HashKeyVersions` bu sürümleri
  listeler.
- **Yinelenen kontrolü** tekil çok anahtarlı indeksle yapılır (`ux_mdm_bank_account_identifier`): yeni kayıt, var olan
  kayıtla en az bir ortak sürümün özetini paylaşırsa indeks çakışır → 409. Uygulama ayrıca yazmadan önce her `Active`
  sürümün özetiyle arar (kullanıcıya erken ve açık hata için); karar yine indeksindir (yarış güvenliği).
- **Yazar çiti (gap 14/1):** "yeni özeti olmayan kayıt sayısı 0" tek başına yetmez — eski yapılandırmayla çalışmaya devam
  eden bir örnek, sayım bittikten sonra yine yalnız eski özeti yazabilir. Bu yüzden **her banka hesabı yazımı** (Create,
  Update, Activate, Deactivate, Delete, döndürme) aynı Mongo işleminde kiracının `BankDataKeyFence` belgesini
  `{TenantId, ActiveKeySetId = örneğin KeySetId}` süzgeciyle `$inc WriteSeq` yapar; süzgeç tutmazsa 503
  `MDM_BANK_KEYSET_STALE`, hiçbir şey yazılmaz. Çitin ilerletilmesi aynı belgeyi değiştirdiği için yolda olan eski küme
  yazımıyla yazma çakışması oluşur; ilerletme commit olduktan sonra eski kümeli hiçbir yazım commit olamaz. Hesap yazma
  hacmi düşük (ana veri) olduğu için kiracı başına tek sıcak belge kabul edilir; gerçek yük altında çakışma ve yeniden deneme
  oranı **ölçülmedi** — yük testi AC-50, DL-2'nin **dilim kabul kapısıdır** (eşiğin yazılması `ready-for-dev` maddesidir).
- **Yeni kiracı çitinin kurulması ilk yazana bırakılmaz (gap 15/2):** kiracının çit belgesi yoksa, yazma işlemi aynı Mongo
  işleminde (1) hedef belgesini `{Id = "bank-data", ApprovedKeySetId = örneğin KeySetId}` süzgeciyle `$inc BootstrapSeq`
  yapar — süzgeç tutmazsa (örneğin kümesi onaylı hedef değil ya da hedef yok) 503 `MDM_BANK_KEYSET_STALE` /
  `MDM_BANK_KEYSET_TARGET_UNAVAILABLE`, çit **oluşturulmaz** ve hiçbir şey yazılmaz; (2) çiti `ActiveKeySetId =
  ApprovedKeySetId` ile ekler (`ux_mdm_bank_data_key_fence_tenant` iki eşzamanlı eklemeden birini düşürür; kaybeden yeniden
  denediğinde var olan çiti normal kuralla karşılaştırır); (3) iş kaydını yazar. Hedef değişikliği de aynı hedef belgesini
  yazdığı için, hedef değiştikten sonra eski kümeyle çit kuran bir işlem commit olamaz (yazma çakışması). Çit hiçbir zaman
  yazanın kendi kümesiyle kurulmaz; yalnız onaylı hedefle.
- **Döndürme prosedürü (sıra bağlayıcı):** (1) yeni sürüm `Active` olarak **bütün örneklere** dağıtılır, eski sürüm `Active`
  kalır (yeni küme K2: iki sürümle çift yazma); (2) hedef K2 olarak onaylanır (`ApproveBankDataKeySetTargetCommand`) ve
  çit her kiracıda K1'den onaylı hedefe ilerletilir (`AdvanceBankDataKeyFenceCommand`, yalnız örneğin kendi kümesi onaylı
  hedefe eşitse ve hedef revizyonu çitin `AppliedTargetRevision`'ından büyükse; K1 ile çalışan örnekler bundan sonra 503
  alır — dağıtım bitmeden ilerletmek kullanılabilirliği düşürür, güvenliği değil);
  (3) `BankAccountProtectionRotationWorker` her kiracıda 200'lük sayfalarla eksik adımları yapar (aşağıda "atlama
  koşulu"); (4) çit K2'deyken `HashKeyVersions` içinde yeni sürüm olmayan kayıt sayısı (kiracılar arası,
  `ix_mdm_bank_account_key_versions`) 0 ise bu sayım artık kesindir, çünkü eski sürümle yazabilecek bir örnek çit
  tarafından reddedilir; (5) yeni sürüm güncel, eski sürüm `Retiring` olan küme K3 bütün örneklere dağıtılır ve çit K3'e
  ilerletilir (önce hedef K3 onaylanır; K2 örnekleri artık 503); (6) iş eski sürümün özetlerini siler; (7) eski sürüm
  yapılandırmadan çıkarılır (yeni küme K4, aynı onay + ilerletme). Her çit ilerletmesi yalnız onaylı hedefe yapılır. Her ilerletme denetlenir. Operasyon kolaylığı: örnekler `KeySetId`'lerini sağlık ucunda
  bildirir (doğruluk buna dayanmaz; çite dayanır).
- **Atlama koşulu (gap 14/3):** iş bir kaydı **yalnız** şu üçü birlikte doğruysa atlar: (a) `EncryptionKeyVersion` =
  `CurrentEncryptionKeyVersion`; (b) her `Active` özet sürümünün özeti `UniquenessKeyDigests`'te var; (c) `Retiring` ya da
  yapılandırmada olmayan sürümün özeti yok. Aksi hâlde yalnız eksik adımları yapar — yeniden şifreleme yalnız (a) tutmuyorsa
  (yeni nonce ile), özet ekleme yalnız (b) için, temizlik yalnız (c) için — hepsi tek CAS yazımında, çit kontrolüyle ve
  tek denetim niyetiyle. Her adım idempotenttir: aynı kayda ikinci geçiş belgeyi değiştirmez.
- **Başlangıç kontrolü:** en az bir `Active` sürüm ve `CurrentHashKeyVersion` yoksa, anahtar 32 bayt değilse ya da bir özet
  anahtarı bir şifreleme anahtarıyla aynıysa servis açılmaz; ayrıca **hiçbir `Active` sürümü taşımayan** silinmemiş kayıt
  varsa (`HashKeyVersions` ∩ Active = ∅; tek `limit 1` sorgusu) servis açılmaz — yoksa o kaydın yinelenmesi görünmez olurdu.

### Şifreleme (iyileştirme 2; KARAR D24/3 — sahip onayı var, **güvenlik onayı ve gerçek Mongo testleri açık**)

- **Algoritma:** AES-256-GCM (`System.Security.Cryptography.AesGcm`), 128 bit etiket. **Nonce:** her şifrelemede (Create,
  Draft'ta değişiklik, döndürme) `RandomNumberGenerator` ile üretilen yeni 96 bit (12 bayt) değer; hiçbir zaman sayaçtan,
  sabitten ya da önceki zarftan türetilmez ve yeniden kullanılmaz (aynı anahtarla nonce yinelenmesi GCM'de gizliliği ve
  bütünlüğü bozar). Anahtar başına şifreleme sayısı rastgele nonce sınırının (2³²) çok altında tutulur; sayı ölçülür ve
  günlüğe yalnız sayaç olarak yazılır.
- **Ek doğrulanmış veri (AAD):** UTF-8 `mdm.bank-account.identifier|1|{TenantId:N}|{BankAccountId:N}|{EncryptionKeyVersion}`.
  Zarf başka bir hesaba ya da kiracıya kopyalanırsa ya da sürüm alanı değiştirilirse çözme başarısız olur.
- **Saklanan:** `AccountIdentifierEnvelope` = `0x01` ‖ nonce ‖ şifreli metin ‖ etiket (base64) ve `EncryptionKeyVersion`.
  Anahtar kaydın içinde yoktur.
- **Anahtarlar:** `Mdm:BankData:EncryptionKeys` = `{version → 32 bayt, state: Active | DecryptOnly}` ve tek
  `CurrentEncryptionKeyVersion`; Development anahtarı üretimden ayrı. Üretimde gizli depodan gelir (hangi depo —
  **ölçülmedi**; `configuration-safety`).
- **Davranış:** (a) başlangıçta güncel anahtar yok, 32 bayt değil, ya da kayıtlarda geçen bir `EncryptionKeyVersion`
  yapılandırmada yok (kiracılar arası `distinct`, `ix_mdm_bank_account_key_versions`) → servis açılmaz; (b) çalışırken
  kayıttaki sürüm yok / anahtar erişilemez → 503 `MDM_BANK_PROTECTION_UNAVAILABLE`, değer ve kayıt yok; (c) etiket
  doğrulanamaz (kurcalanmış metin, etiket ya da nonce; yanlış anahtar baytı; AAD uyuşmazlığı) → 500
  `MDM_BANK_PROTECTION_INTEGRITY_FAILED`, değer ya da kısmi değer dönmez, günlüğe yalnız hesap kimliği ve hata sınıfı yazılır;
  maskesiz okuma kaydı bu durumda yazılmaz çünkü kayıt ve çözme aynı işlemdedir ve işlem geri alınır.
- **Döndürme ve kurtarma:** yeni sürüm `CurrentEncryptionKeyVersion` olur, eski sürüm `DecryptOnly` kalır — karışık sürümlü
  kayıtlar okunabilir. Döndürme işi eski sürümlü her kaydı çözer, **yeni nonce** ile yeni sürümle şifreler, CAS ile yazar
  (aynı `RotateBankAccountProtectionCommand`; özet adımıyla aynı yazım). Çökme sonrası yeniden çalıştırmak kalan kayıtları
  işler; atlama koşulu yukarıdaki üç koşuldur (gap 14/3). **Aynı açık şifrelemede de vardı ve aynı çitle kapanır (gap
  14/1):** eski kümeyle çalışan örnek, eski sürümü güncel sanarak sayım bittikten sonra eski anahtarla zarf yazabilirdi.
  Şifreleme sürümleri de `KeySetId`'ye girdiği için yeni güncel sürüme geçiş bir çit ilerletmesidir; eski sürüm
  yapılandırmadan **yalnız** çit güncel sürümü içeren kümedeyken ve o sürümü taşıyan kayıt sayısı 0 iken çıkarılır; erken
  çıkarılırsa başlangıç kontrolü (a) servisi açmaz.
- **IBAN denetim düzeyi (gap 13 P2):** v1 iki düzeyi ayırır — **"biçim + sağlama geçerli"** (`FormatAndChecksum`: genel
  IBAN deseni + ISO 7064 MOD 97-10) ve **"ülke biçimi geçerli"** (`CountryFormat`: ülkeye göre uzunluk ve BBAN yapısı,
  ISO 13616 kayıt verisi). v1 yalnız ilkini yapar; kayıt `IdentifierCheckLevel = FormatAndChecksum` ile saklanır ve bu
  düzey DTO'da (`identifierCheckLevel`), listede (rozet "ülke biçimi denetlenmedi", resx) ve `lookup-validation`'da
  döner. Ülke biçimi kapısı `Mdm:BankData:IbanCountryFormatGate` (`Off` varsayılan / `On`) ve `IIbanCountryFormatSource`
  ile çalışır: `On` iken Create ve Draft değişikliği ülke biçimini denetler — geçmezse 400
  `MDM_BANK_IBAN_COUNTRY_FORMAT_INVALID`, kaynak yok / erişilemez ise 503 `MDM_BANK_IBAN_COUNTRY_FORMAT_UNAVAILABLE` (sessizce
  düşük düzeye inilmez); geçerse `CountryFormat`. Var olan `FormatAndChecksum` kayıtlar kendiliğinden yükselmez (yeniden
  denetim işi Follow-up).
- **Yurt içi doğrulama düzeyi (gap 14/4):** IBAN kuralı yurt içi hesaba uygulanmaz. Yurt içi hesap v1'de
  `DomesticFormat` ("genel biçim geçerli": `^[A-Z0-9]{4,34}$` + bankada ulusal banka kodu var) olarak saklanır.
  `DomesticVerified` ("banka ve ülke kuralı doğrulandı") iki şeyin birlikte doğrulanmasını ister: (1) **banka varlığı** —
  `CountryCode` + `NationalBankCode` (+ şube kodu, banka anahtara katıyorsa) bir banka dizini kaynağında
  (`IBankDirectorySource`, ör. ulusal takas dizini) var ve aktif; (2) **hesap numarası kuralı** — o ülkenin numara biçimi ve
  varsa kontrol hanesi kuralı (`IDomesticAccountRuleSource`). Kapı `Mdm:BankData:DomesticVerificationGate` (`Off` varsayılan /
  `On`): `On` iken Create ve Draft değişikliği iki kontrolü yapar — geçmezse 400 `MDM_BANK_DOMESTIC_VERIFICATION_FAILED`,
  kaynaklardan biri yok / erişilemez ise 503 `MDM_BANK_DOMESTIC_VERIFICATION_UNAVAILABLE` (düşük düzeye inilmez); geçerse
  `DomesticVerified`. İki kaynağın sahibi ve verisi yoktur (Dependencies); sabit ülke tablosu yazılmaz.
- **MOD-0269 gerçek dağıtımından önce iki zorunlu kapı (ikisi de):** (a) IBAN hesapları için ülke biçimi kapısı `On`,
  kaynak bağlı ve dağıtılacak her IBAN hesabı `CountryFormat`; (b) yurt içi hesaplar için yurt içi doğrulama kapısı `On`,
  iki kaynak bağlı ve dağıtılacak her yurt içi hesap `DomesticVerified`. Tüketici bunu `lookup-validation`'daki
  `identifierType` + `identifierCheckLevel` çiftinden okur; birinin kapısı kapalıyken o türdeki hesaplar gerçek dağıtıma
  giremez.

### Sahiplik kuralları

- Hesabın **tam olarak bir** sahibi vardır: `OwnerType = LegalEntity` ⇒ `LegalEntityId` dolu, `SupplierId` boş;
  `OwnerType = Supplier` ⇒ tersi. Sahip oluşturulduktan sonra değişmez.
- **Şirket sahibi (aynı servis, süreç içi):** Create ve Activate'te `LegalEntity` aynı kiracıda, silinmemiş ve
  `OperationalStatus = Active` olmalıdır; Activate'te ayrıca `ApprovalStatus = Approved` (**KARAR D23/3**, D10/1 ile aynı çizgi).
  Değilse 422 `MDM_BANK_ACCOUNT_OWNER_NOT_ELIGIBLE`. Kullanıcının o şirket üzerinde **yazma kapsamı** olmalıdır; yoksa 404.
- **Tedarikçi sahibi (MOD-0140 sözleşmesi, Gateway):** Create, Update, Activate, Deactivate ve Delete'te (ve D23/1 gereği her
  okumada, aşağıda) `ISupplierEligibilityPort` `validateSuppliers`
  çağırır; çağrı isteğin `X-Legal-Entity-Id`'siyle yapılır çünkü tedarikçi ana kaydı kiracı genelinde ama ilişki durumu
  şirket başınadır (sahip cevabı 2026-10-21, `docs/roadmap/plans/mvp6-development-plan-v10.0.md` C1). `known = true` ve
  `status = Active` değilse yazma 422 (Create, Activate) ya da 404 (var olan hesapta Update / Deactivate / Delete — ilişkisiz
  kullanıcı hesabı görmez, D23/1); geçersiz / çelişkili yanıt (sonuçta istenen `supplierId` yok ya da farklı, `known`
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
  döndürür (şirket sahibinde `ownerState = NOT_ELIGIBLE`; tedarikçi sahibinde ilişki `Active` değilse D23/1 gereği 404) ve
  tüketici 422 ile durur. Yeniden aktive etme ve tedarikçi hesabı aktive etme
  aynı kontrolden geçer. Pasif sahibin hesaplarını toplu pasifleştiren iş v1 dışıdır (Follow-up).
- **Banka / şube pasifse:** yeni hesap Create ve Activate 422 `MDM_BANK_INACTIVE`; var olan Active hesap için
  `lookup-validation` `bankState = INACTIVE` döndürür ve hesap `referenceable = false` sayılmaz (hesabın kendisi aktif);
  tüketicinin kuralı sözleşmede (aşağıda).

### Veri kapsamı (SEC-002) — kapsam cümlesi

- **Şirket hesapları:** kapsamı taşıyan alan `BankAccount.LegalEntityId`; çevrilen kapsam türü yalnız `LegalEntity`
  (`OrgUnit` / `Position` çevrilmez → daraltır, sızdırmaz); kapsam çözümleme tek yerde (`IBankAccountDataScopeSource` — aday
  MDM'deki `ITrustedLegalEntityScopeProvider`). Kapsam dışı şirketin hesapları listede çıkmaz (200, 0 satır);
  detay, maskesiz okuma, `lookup-validation` ve her yazma 404. Çözücü hatası, kiracı ya da kullanıcı yokluğu → boş kapsam.
  Kiracıdaki tüm şirketlerin hesaplarını görmek ayrı izindir: `mdm.bank-accounts.read-tenant-wide` (yalnız açık atama,
  **yalnız okuma**; yazma kapsamını genişletmez — her yazma kullanıcının o şirketteki kendi yetkisini ister, MOD-0126
  gap 4/G1 deseni).
- **Tedarikçi hesapları — KARAR D23/1 ("ilişkili şirketler"):** kapsamı taşıyan alan `BankAccount.SupplierId` + isteğin
  şirketi (`X-Legal-Entity-Id`); çevrilen kapsam: kullanıcının veri kapsamındaki o şirketin MOD-0140'ta tedarikçiyle
  **`Active`** ilişkisi. Kurallar:
  - **İlişkili şirket kullanıcısı** (`mdm.bank-accounts.read` + şirket kullanıcının kapsamında + ilişki `Active`): liste,
    detay ve `lookup-validation` görür (maskeli); maskesiz okuma ayrıca `read-unmasked` + gerekçe ister (D23/4).
  - **İlişkisiz kullanıcı** (ilişki yok, `OnHold` / `Blocked` / `Inactive`, `known: false`, başlıkta şirket yok ya da şirket
    kullanıcının kapsamında değil): hesap listede çıkmaz; detay, `lookup-validation`, maskesiz okuma ve her yazma **404**.
  - **`mdm.bank-accounts.read-tenant-wide`** (yalnız açık atama): bütün tedarikçi hesaplarını liste ve detayda **maskeli**
    görür; ilişkisi yoksa maskesiz okuma **403** `MDM_BANK_ACCOUNT_RELATIONSHIP_REQUIRED` (hesap görünür olduğu için 404
    değil), yazma ve `lookup-validation` 404 (lookup bir şirketin kullanım uygunluğudur).
  - Liste: ilişkili tedarikçi kimlikleri MOD-0140'tan okunur ve süzgece konur; MOD-0140 erişilemez / geçersiz yanıt →
    tedarikçi hesabı içeren liste isteği 503 (hesaplar "ilişki bilinmiyor" diye gösterilmez); kiracı geneli izinle
    maskeli liste MOD-0140'a gitmez.
  - **D23/1 sonucu (not):** kiracı genelinde tedarikçi hesaplarını **yazan** bir ana veri sorumlusu rolü yoktur; yazma her
    zaman ilişkili bir şirket üzerinden yapılır.
- **Banka ve şube:** kiracı içinde herkese açık referans verisi (`mdm.banks.read` izni olan herkes görür); SEC-002 §3
  istisnası bu cümleyle yazılıdır.

### Okuma sözleşmesi — `BANK-ACCOUNT-LOOKUP` 1.0.0 (tanım; dosyası DL-2'de yazılır)

LEGAL-ENTITY-LOOKUP 1.1.0 deseni (`docs/analysis/contracts/drafts/legal-entity-lookup-validation-v1.1-proposal.md` §1–§2).

- **Uç:** `GET /api/mdm/bank-accounts/{bankAccountId:guid}/lookup-validation`, izin `mdm.bank-accounts.read`, veri kapsamı
  uygulanır. Tedarikçi sahipli hesapta `X-Legal-Entity-Id` zorunludur (ilişki durumu için); yoksa 400.
- **404 (gövde aynı, varlık sızdırmaz):** hesap yok; silinmiş; başka kiracıda; çağıranın kapsamı dışında; `Status ≠ Active`
  (Draft, Inactive); tedarikçi sahipli hesapta isteğin şirketiyle `Active` ilişki yok (**KARAR D23/1**).
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
| `ownerState` | string | `ELIGIBLE` \| `NOT_ELIGIBLE` — sahibin **o anki** durumu (şirket: `Active` + `Approved`, **KARAR D23/3**; tedarikçi: 200 yalnız `Active` ilişkide döndüğü için her zaman `ELIGIBLE`, D23/1) |
| `ownerStateReason` | string \| null | `NOT_ELIGIBLE` ise makine kodu (`LEGAL_ENTITY_NOT_ACTIVE`, `LEGAL_ENTITY_NOT_APPROVED`) |
| `bankState` | string | `ACTIVE` \| `INACTIVE` (banka ya da şube pasifse `INACTIVE`) |
| `currencyCode` | string | `^[A-Z]{3}$` |
| `identifierType` | string | `IBAN` \| `DOMESTIC` |
| `identifierCheckLevel` | string | IBAN: `FORMAT_AND_CHECKSUM` \| `COUNTRY_FORMAT`; yurt içi: `DOMESTIC_FORMAT` \| `DOMESTIC_VERIFIED` (gap 13 P2, gap 14/4); türle uyuşmayan değer çelişkili yanıttır (503) |
| `maskedDisplay` | string | ör. `TR••••1234` |
| `contractVersion` | string | `bank-account-lookup/1.0` |

  Gövde **IBAN, hesap numarası, son 4 hane dışındaki herhangi bir parça, BIC, banka adı, şube adı, hesap sahibi adı
  taşımaz.** Tedarikçi ilişkisi okunamazsa (MOD-0140 503 / geçersiz) MDM 200 dönmez, **503**
  `MDM_BANK_ACCOUNT_OWNER_UNAVAILABLE` döner (hiçbir zaman varsayılan `ELIGIBLE` yok).
- **`referenceable` tek başına yeterli değildir (iyileştirme 1).** `referenceable = true` yalnız **hesap kaydının** aktif ve
  silinmemiş olduğunu söyler; banka / şube durumunu, sahibin uygunluğunu, para birimini ya da denetim düzeyini söylemez
  (ör. pasif bankadaki aktif hesap `referenceable = true` + `bankState = INACTIVE` döner). Tüketici aşağıdaki **uygunluk
  alanlarının hepsini** kontrol etmeden işleme devam etmez:

  | # | Uygunluk alanı | Tüketici kuralı |
  |---|---|---|
  | E1 | `bankAccountId` | istenen kimliğe eşit |
  | E2 | `lifecycleState` | `ACTIVE` |
  | E3 | `referenceable` | `true` |
  | E4 | `bankState` | `ACTIVE` |
  | E5 | `ownerType` + `legalEntityId` / `supplierId` | tutarlı ve tüketicinin beklediği taraf (ör. MOD-0126 kaynak hesabı: `LEGAL_ENTITY` ve `legalEntityId` = ödeyen şirket) |
  | E6 | `ownerState` | `ELIGIBLE` |
  | E7 | `currencyCode` | biçimli ve tüketicinin para birimi kuralına uygun (ör. parti para birimine eşit) |
  | E8 | `identifierType` + `identifierCheckLevel` | tüketicinin istediği en az düzey, türe göre (v1 tüketicileri için IBAN `FORMAT_AND_CHECKSUM`, yurt içi `DOMESTIC_FORMAT` yeterli; MOD-0269 gerçek dağıtımı IBAN'da `COUNTRY_FORMAT`, yurt içinde `DOMESTIC_VERIFIED` ister) |
  | E9 | `contractVersion` | `bank-account-lookup/1.` ile başlar |

- **Tüketici sınıflandırma tablosu (sözleşmenin parçası; Treasury paketleri uygular):**

| MOD-0289 yanıtı | Sınıf | Tüketici sonucu |
|---|---|---|
| 200 ve E1…E9'un hepsi sağlanıyor | uygun | işlem sürer |
| 200, tutarlı (E1–E3, E9 sağlanıyor, alanlar tanımlı değerlerde), ama E4–E8'den biri tüketicinin kuralını sağlamıyor (`bankState = INACTIVE`, sahip beklenen taraf değil, `ownerState = NOT_ELIGIBLE`, para birimi uyuşmuyor, denetim düzeyi yetersiz) | tanımlı, uygun değil | 422 |
| 404 | tanımlı, kullanılamaz (yok / başka kiracı / kapsam dışı / aktif değil) | 422 |
| 200 ama `bankAccountId` yok ya da farklı; `referenceable = false`; `lifecycleState ≠ ACTIVE`; `ownerType` bilinmeyen; `ownerType` ile sahip alanları çelişkili (ikisi dolu, ikisi boş, yanlışı dolu); `ownerState` yok / `null` / bilinmeyen; `bankState` ya da `identifierCheckLevel` yok / bilinmeyen; `contractVersion` yok ya da `bank-account-lookup/1.` değil; `currencyCode` biçimsiz; gövde ayrıştırılamıyor; `data` yok | geçersiz ya da çelişkili yanıt | 503 |
| 400, 401 / 403, 5xx (MDM 503 dahil), zaman aşımı, ulaşılamıyor | erişilemiyor | 503 |

  Kural: geçersiz ya da çelişkili yanıt **503**, tanımlı ama uygun olmayan durum **422**; hiçbir belirsiz yanıt "uygun"
  sayılmaz; karşılaştırmalar büyük-küçük harfe duyarlıdır. Tüketici yanıttaki hiçbir alanı (maskeli gösterim dahil)
  kendi kalıcı kaydına kopyalamaz; yalnız opak `bankAccountId` saklanır (ekranda maskeli gösterim gerekiyorsa her
  seferinde okunur).
- **Uyum fikstürleri (sözleşme dosyasının parçası):** sözleşme dosyası her uygunluk alanı için bir **negatif fikstür**
  taşır — öteki bütün alanlar uygunken yalnız o alan bozuk: `referenceable = true` + `bankState = INACTIVE`; `referenceable =
  true` + `ownerState = NOT_ELIGIBLE`; yanlış sahip türü / başka şirket; başka para birimi; `FORMAT_AND_CHECKSUM` ile
  `COUNTRY_FORMAT` isteyen tüketici; ve her 503 satırı için bir fikstür. MdmService testlerinde bir **başvuru
  sınıflandırıcısı** bu fikstürleri koşar; her tüketici paketi (MOD-0126, MOD-0119, MOD-0120, MOD-0124) aynı fikstürleri
  kendi sınıflandırıcısına karşı koşar (o paketlerin işi, Follow-up tablosu).
- **Sürüm kuralı:** 1.x yalnız ekleme; 404 kararını genişletmek büyük sürümdür. (Bu sürüm D23/1 ile tedarikçi ilişkisi
  yokluğunu 404'e dahil ederek yayımlanır; dosya henüz yazılmadığı için bu bir değişiklik değildir.)

## Layout & Shell Contract

- `shell: tenant` → `Views/MasterData/Banks/` ve `Views/MasterData/BankAccounts/` altındaki **tüm** `.cshtml`
  dosyalarında `Layout = "_LayoutTenantShell";` **AÇIKÇA** yazılır (canlı örnek
  `frontend/Diten.Web/Views/MasterData/Brands/Index.cshtml:7@158e0dc93`); `_ViewStart.cshtml` varsayılanı değiştirilmez.
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
                   RevealBankAccountIdentifierCommand.cs, RotateBankAccountProtectionCommand.cs,
                   AdvanceBankDataKeyFenceCommand.cs, ApproveBankDataKeySetTargetCommand.cs (üçünü yalnız iş gönderir)
  Queries/         GetBankAccountListQuery.cs, GetBankAccountByIdQuery.cs, GetBankAccountSensitiveReadListQuery.cs,
                   ValidateBankAccountReferenceQuery.cs, BankAccountListSort.cs
  Handlers/CommandHandlers/   …Handler.cs
  Handlers/QueryHandlers/     …Handler.cs
  Validators/      CreateBankAccountValidator.cs, UpdateBankAccountValidator.cs, RevealBankAccountIdentifierValidator.cs,
                   GetBankAccountListValidator.cs
  Rules/           IbanRules.cs, BicRules.cs, DomesticAccountRules.cs, BankAccountUniquenessKey.cs (türe göre normalize +
                   anahtar; saf işlevler; ağ yok)
  BankAccountModels.cs
Features/BankAccountPurpose/
  Commands/        CreateBankAccountPurposeCommand.cs, UpdateBankAccountPurposeCommand.cs, ActivateBankAccountPurposeCommand.cs,
                   DeactivateBankAccountPurposeCommand.cs, DeleteBankAccountPurposeCommand.cs
  Queries/         GetBankAccountPurposeListQuery.cs, GetBankAccountPurposeByIdQuery.cs
  Handlers/CommandHandlers/ · Handlers/QueryHandlers/
  Validators/      CreateBankAccountPurposeValidator.cs, UpdateBankAccountPurposeValidator.cs
  BankAccountPurposeCatalog.cs (çekirdek kodlar, sabit) · BankAccountPurposeModels.cs
Infrastructure/BankData/
                   AesGcmBankAccountIdentifierProtector.cs, HmacUniquenessDigester.cs, BankDataKeyOptions.cs,
                   BankDataKeyStartupValidator.cs (anahtar biçimi + kayıtlardaki sürümlerin yapılandırmada olması),
                   BankDataKeySetId.cs (kanonik serileştirme + örneğin anahtar kümesi kimliği), BankDataKeyFenceRepository.cs,
                   BankDataKeySetTargetRepository.cs (Persistence)
Api/Services/BankData/
                   BankAccountProtectionRotationWorker.cs (barındırılan iş; `Mdm:BankData:Rotation:Enabled` varsayılan false)
```

- Command/Query `sealed record` (`IRequest<Response<T>>`); Handler `sealed class` — **Command/Query suffix YOK**. Tek
  dosyada tek public tip. Controller `CustomBaseController`'dan türer (`LegalEntitiesController.cs:11-14@158e0dc93` deseni),
  `Response<T>` döner, her aksiyonda `[HasPermission]`.
- İş kuralı handler + domain'de (`handler-design`); kaynaktaki büyük servis sınıfları (`Mod0289BankService` ~2500 satır,
  paket atıfı `:2446-2466`) alınmaz, kurallar bölünerek yeniden yazılır.
- Her yazma komutu ve maskesiz okuma, denetim niyetini iş belgesiyle **aynı koşullu yazımda ya da aynı Mongo işleminde**
  taşır (Audited Events); `MasterDataAtomicWrite` politikası (işlem yoksa Development dışında yazmayı reddet,
  `services/Diten.MdmService/src/Diten.MdmService.Persistence/Repositories/MasterDataAtomicWrite.cs:9-20@158e0dc93`) bu
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
Views/MasterData/BankAccountPurposes/
├── Index.cshtml · Create.cshtml · Edit.cshtml (yalnız etiketler; kod salt okunur) · Details.cshtml (kullanan hesap sayısı)
├── _Form.cshtml (Code + 7 dil etiketi) · _Filter.cshtml (çekirdek / kiracı, durum) · _DataTable.cshtml (data-dt-data-mode="client")
├── _IndexL10n.cshtml
└── BankAccountPurposesIndex.cs
Resources/Views/MasterData/{Banks,BankAccounts,BankAccountPurposes}/   {…}Index.{en,tr,fr,es,zh,ar,ru}.resx
```

- **YASAK (compact):** `_CreateEditOffcanvas.cshtml`, `_DetailsQuickView.cshtml`, Index içinde create/edit offcanvas.
- BankAccounts DataTable sütunları: Kod, Sahip türü, Sahip (şirket adı / tedarikçi kodu), Banka, Para birimi, Hesap
  (yalnız `MaskedDisplay`), Amaç, Durum, Oluşturan, Oluşturma zamanı, İşlemler. Tam tanımlayıcı listede, filtre
  önerilerinde, dışa aktarımda (v1'de yok) ve tarayıcı depolamasında **hiç** yoktur.
- Create formunda tanımlayıcı alanı `autocomplete="off"`; Edit'te (Active hesap) tanımlayıcı, para birimi, banka, şube ve
  sahip salt okunur ya da yok. "Maskesiz göster" düğmesi yalnız `read-unmasked` izni olan kullanıcıya çizilir; gerekçe
  kodu SweetAlert2 premium diyaloğunda zorunludur (MOD-0013); değer yalnız diyalog açıkken DOM'dadır, kapanınca silinir;
  `localStorage` / `sessionStorage` / konsol yazımı yok.
- Activate diyaloğu: dört göz kuralı ihlalinde düğme yerine yerelleştirilmiş açıklama (D23/2); tedarikçi hesabında doğrulama
  kodu seçimi (D24/1). Sunucu kontrolünün yerine geçmez.
- API profili: same-origin MVC vekil (`/MasterData/BankAccounts/api/...`, `MasterDataGatewayController` deseni —
  `frontend/Diten.Web/Controllers/MasterData/MasterDataGatewayController.cs:18-21@158e0dc93`: HttpOnly erişim çerezi
  sunucuda okunur, tarayıcı 5059'u ya da token'ı görmez); vekil oturumun çözümlenmiş `X-Legal-Entity-Id`'sini ekler.
  Vekil maskesiz okuma yanıt gövdesini günlüğe yazmaz.
- IBAN hesaplarında `identifierCheckLevel = FORMAT_AND_CHECKSUM` liste ve detayda rozetle gösterilir ("ülke biçimi
  denetlenmedi", resx anahtarı); Banks formunda `DomesticKeyIncludesBranch` anahtarı, bankada yurt içi hesap varken salt
  okunur. Kiracı geneli izinle görülen ilişkisiz tedarikçi hesabında "Maskesiz göster" çizilmez (D23/1).
- Para birimi seçimi `GET /api/lookups/reference/currencies`'ten; ülke alanı ISO alfa-2 metin girişi (D24/5); sabit liste yok;
  hardcoded metin yok. Hesap amacı seçici çekirdek kodları resx etiketiyle, kiracı amaçlarını kaydın o kültürdeki etiketiyle
  gösterir (aşağıda "Hesap amaçları").

## Validation Rules

| Alan / kural | Zorunlu | Biçim / kural | DB | Ön kontrol |
|---|---|---|---|---|
| Gövde/sorguda `tenantId` | — | bulunursa 400 | — | — |
| Bank.Code / BankAccount.Code | — | istekte bulunursa 400 `MDM_BANK_CODE_FORBIDDEN` (OTOMATİK) | Unique (TenantId, Code), kısmi değil | `IRecordCodePort` |
| Bank.Name | Evet | trim, 1..200, kontrol karakteri yok | — | — |
| Bank.CountryCode | Evet | `^[A-Z]{2}$` (büyük harfe normalize); yalnız biçim (D24/5) | — | — |
| Bank.Bic | Hayır | boşluk atılır, büyük harf; `^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$` (ISO 9362); 5–6. karakter = `CountryCode` | Unique (TenantId, Bic) kısmi | — |
| Bank.NationalBankCode | Koşullu (bankada `Domestic` hesap varsa Evet) | normalize (trim, boşluk/tire at, büyük harf), 1..35, `^[A-Z0-9]+$`; yurt içi hesap varken değişmez (409) | Unique (TenantId, CountryCode, NationalBankCode) kısmi | — |
| Bank.DomesticKeyIncludesBranch | Hayır | bool; yurt içi hesap varken değişmez (409 `MDM_BANK_DOMESTIC_KEY_LOCKED`) | — | depo sayımı |
| BankBranch.BankId | Evet | aynı kiracı, silinmemiş banka; bankası `Inactive` ise Create 422 | — | depo |
| BankBranch.BranchCode | Evet | normalize (trim, boşluk/tire at, büyük harf), 1..35; şubede yurt içi hesap varken değişmez (409) | Unique (TenantId, BankId, BranchCode) kısmi | — |
| BankBranch.Bic | Hayır | 11 karakter ISO 9362; ilk 8 = banka BIC'i (varsa) | — | — |
| BankAccount.OwnerType | Evet | tanımlı enum; değişmez | — | — |
| BankAccount.LegalEntityId / SupplierId | Koşullu (tam biri) | türüne göre tam biri dolu; LE: Guid ≠ Empty, aynı kiracı, `Active`; tedarikçi: 1..64, `validateSuppliers` | — | `ILegalEntityRepository` / `ISupplierEligibilityPort` |
| BankAccount.BankId / BranchId | Evet / Hayır | aynı kiracı; şube o bankaya ait (400 `MDM_BANK_BRANCH_BANK_MISMATCH`); banka/şube `Active` değilse 422 | — | depo |
| BankAccount.CurrencyCode | Evet | `^[A-Z]{3}$`, MOD-0048 listesinde | — | `ICurrencyReferencePort` |
| AccountIdentifier (IBAN) | Evet | normalize: yalnız boşluk atılır (başka ayırıcı 400), büyük harf; `^[A-Z]{2}[0-9]{2}[A-Z0-9]{11,30}$` (15..34); ISO 7064 MOD 97-10 sağlaması = 1; ilk iki harf = bankanın `CountryCode`'u → **"biçim + sağlama geçerli"** (`FormatAndChecksum`). **"Ülke biçimi geçerli"** (ülke uzunluğu + BBAN yapısı) v1'de yapılmaz; kapı `On` iken `IIbanCountryFormatSource` ile (Runtime Constraints, gap 13 P2) | Unique (TenantId, UniquenessKeyDigests) kısmi | `IbanRules` |
| AccountIdentifier (Domestic) | Evet | normalize: boşluk ve tire atılır, büyük harf, baştaki sıfırlar korunur; `^[A-Z0-9]{4,34}$`; bankada `NationalBankCode` zorunlu (400 `MDM_BANK_DOMESTIC_BANK_CODE_REQUIRED`); bankanın `DomesticKeyIncludesBranch = true` ise şube zorunlu (400 `MDM_BANK_DOMESTIC_BRANCH_REQUIRED`) | aynı (anahtar ülke + banka kodu [+ şube] + numara) | `DomesticAccountRules`, `BankAccountUniquenessKey` |
| AccountHolderName | Evet | trim, 1..140, kontrol karakteri yok | — | — |
| AccountPurpose | Evet | büyük harfe normalize; çekirdek kod ya da aynı kiracıda silinmemiş **aktif** `BankAccountPurpose.Code` (D24/6); pasif amaç yeni kullanımda 422 `MDM_BANK_ACCOUNT_PURPOSE_INACTIVE`; bilinmeyen 400; amacı değiştirmeyen Update pasif amaçla da geçer | — | çekirdek liste + `IBankAccountPurposeRepository` (işlem içi CAS, aşağıda) |
| PaymentUsage / DisplayName | Hayır | ≤ 120 | — | — |
| `X-Legal-Entity-Id` | tedarikçi sahibi ve LE Create'te Evet | Guid; kiracıda `Active` şirket; değilse 404; gövdeyle çelişki 400 | — | `ILegalEntityRepository` |
| `If-Match` | Update / Activate / Deactivate'te Evet | `Version` | CAS süzgeci | — |
| DeactivationReasonCode | Deactivate'te Evet | 1..64 | — | — |
| ActivationVerificationCode | tedarikçi hesabı Activate'te Evet (D24/1) | `CALLBACK_CONFIRMED` \| `BANK_LETTER_ON_FILE` \| `SUPPLIER_PORTAL_CONFIRMED` | — | — |
| Reveal.ReasonCode / ReasonComment | Evet / Hayır | 1..64 / ≤ 500, tanımlayıcıyı içeremez | — | — |

**Durum kuralları:** Update — Draft'ta bütün form alanları (sahip hariç); Active/Inactive'de yalnız `DisplayName`,
`PaymentUsage`, `AccountPurpose`, `AccountHolderName` (öneri; sahip adı düzeltmesi yönlendirmeyi değiştirmez ama denetlenir).
Activate — Draft ya da Inactive'den; sahip, banka ve şube uygun; dört göz (D23/2). Delete / BulkDelete — yalnız
`ActivatedAt = null` Draft; BulkDelete'te kurala uymayan tek kimlik bütün isteği 409 yapar (kısmi silme yok).

**Dört göz kuralı (KARAR D23/2):** `ActivateBankAccountCommand`'ı hesabı oluşturan (`CreatedBy`) ya da son güncelleyen
(`UpdatedBy`) kullanıcı çalıştıramaz → 403 `MDM_BANK_ACCOUNT_FOUR_EYES_REQUIRED`. Aktör karşılaştırması token `sub`'ının
Guid değeri üzerindendir; Guid olmayan `sub` reddedilir.

## Failure Path to Verify

- **Yinelenen hesap** (aynı tekillik anahtarı — aynı IBAN; ya da aynı ülke + banka kodu [+ şube] + numara — aynı ya da başka
  sahip, farklı özet anahtarı sürümleri altında da) → 409 `MDM_BANK_ACCOUNT_DUPLICATE`; gövde diğer kaydın kimliğini, kodunu
  ya da sahibini içermez. **Aynı yurt içi numara başka bankada** → kabul (iki ayrı hesap).
- **Yurt içi hesap, ulusal banka kodu olmayan banka / şube gerektiren bankada şubesiz** → 400; **yurt içi hesabı olan
  bankada anahtar alanı değişikliği** → 409 `MDM_BANK_DOMESTIC_KEY_LOCKED`.
- **Özet anahtarı sürümü taşımayan kayıt varken açılış** → servis açılmaz.
- **Eski anahtar kümesiyle çalışan örnek çit ilerletildikten sonra yazıyor** → 503 `MDM_BANK_KEYSET_STALE`, hiçbir şey
  yazılmaz; ilerletmeyle eşzamanlı eski küme yazımı → biri geri alınır, yeniden denemede 503.
- **Aynı anahtar listesi, farklı güncel sürüm seçimi** → farklı `KeySetId`, 503 `MDM_BANK_KEYSET_STALE`.
- **Eski iş eski revizyonu yeniden onaylıyor** (K2 rev n'den sonra K1 rev n-1) → 409 `MDM_BANK_KEYSET_TARGET_REVISION_STALE`,
  hedef K2 kalır; beklenen önceki küme tutmuyor → 409 `MDM_BANK_KEYSET_TARGET_PREVIOUS_MISMATCH`.
- **Yeni kiracı, onaylı hedefle uyuşmayan yazar** → çit kurulmaz, 503, kayıt yok; **onaylı hedef yok** → 503
  `MDM_BANK_KEYSET_TARGET_UNAVAILABLE`.
- **Yurt içi hesap oluşturma ile bankanın anahtar alanı değişikliği eşzamanlı** → ikisinden biri 409
  (`MDM_BANK_DOMESTIC_KEY_CHANGED` ya da `MDM_BANK_DOMESTIC_KEY_LOCKED`); ikisi birlikte başarılı olmaz.
- **Yurt içi doğrulama kapısı açıkken banka dizininde olmayan banka kodu / ülke kuralına uymayan numara** → 400; kaynak yok
  → 503.
- **Zarf kurcalanmış / yanlış anahtar / başka kayda taşınmış** → maskesiz okuma 500 `MDM_BANK_PROTECTION_INTEGRITY_FAILED`,
  değer yok; **kayıttaki şifreleme sürümü yapılandırmada yok** → açılışta servis açılmaz, çalışırken 503.
- **Sağlaması geçerli ama ülke uzunluğu yanlış IBAN** → v1'de (kapı `Off`) kabul, `FormatAndChecksum` işaretli; kapı `On`
  → 400 `MDM_BANK_IBAN_COUNTRY_FORMAT_INVALID`; kapı `On` + kaynak yok → 503.
- **İlişkisiz şirket kullanıcısı tedarikçi hesabına** (D23/1) → liste dışı, detay / lookup / maskesiz okuma / yazma 404;
  kiracı geneli izinle maskeli görülen ilişkisiz hesapta maskesiz okuma 403 `MDM_BANK_ACCOUNT_RELATIONSHIP_REQUIRED`.
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
- **Tedarikçi bilinmiyor / ilişki Active değil** → Create / Activate 422, var olan hesapta okuma ve diğer yazmalar 404
  (D23/1); **MOD-0140 erişilemez, 401/403, 5xx, zaman aşımı, geçersiz gövde** → 503 `MDM_BANK_ACCOUNT_OWNER_UNAVAILABLE`;
  hiçbir şey yazılmaz; `lookup-validation` de 503 (asla varsayılan `ELIGIBLE`).
- **Dört göz ihlali** (oluşturan / son güncelleyen aktive ediyor) → 403 `MDM_BANK_ACCOUNT_FOUR_EYES_REQUIRED`.
- **Pasif bankaya hesap** → 422 `MDM_BANK_INACTIVE`.
- **Hesap amacı:** çekirdek kodla çakışan kiracı kodu → 409 `MDM_BANK_ACCOUNT_PURPOSE_CODE_RESERVED`; aynı kiracı kodu ikinci
  kez → 409 `MDM_BANK_ACCOUNT_PURPOSE_CODE_CONFLICT`; çekirdek koda yazma → 409 `MDM_BANK_ACCOUNT_PURPOSE_CORE_IMMUTABLE`;
  pasif amaçla yeni hesap → 422; kullanılan amacı silme → 409; 7 dilden biri eksik → 400; başka kiracının amacı → 404.
- **Denetim niyeti yazılamıyor** (zorla hata) → komut başarısız 503, iş verisi yazılmaz.
- **Şifreleme anahtarı yok** → servis açılmaz; çalışırken erişilemez → Create / Reveal 503 `MDM_BANK_PROTECTION_UNAVAILABLE`.
- **RCS-001 ya da MOD-0048 erişilemez** → Create 503 ilgili kod; kayıt yok.

## Authorization Convention

- Policy: `[Authorize]` (kiracı aktörü) + her aksiyonda `[HasPermission("…")]` (MDM deseni
  `LegalEntitiesController.cs:24-62@158e0dc93`). Kaynakta MOD-0289 kendi `MOD0289_*` taleplerini denetleyicide kontrol
  ediyordu (`Mod0289Controller.cs:246@da4abb20`, paket §D) — taşınmaz.
- Biçim (PKS-001): küçük harf, noktalı, ≥ 3 parça; ad alanı `mdm.*` (MDM'de ölçülen anahtarlar `mdm.legal-entities.*`,
  `mdm.brands.*`, `mdm.products.*` …). Kaynak 38 `MOD0289_*` anahtarı (CONTRACT-STATE `permission_keys`) **kopyalanmaz**;
  hiçbiri Recovery'de yayımlanmadığı için takma ad gerekmez.
- Aktör: `tenant_user`. Tek arka plan aktörü `BankAccountProtectionRotationWorker`'dır (sistem aktörü; MDM'de
  `IAuditSystemActor` işareti var — `services/Diten.MdmService/src/Diten.MdmService.Application/Contracts/Audit/IPlatformAuditForwarder.cs:52@158e0dc93`
  — ama merkezi iletim yoluna aittir; niyet yolunda (`LocalAuditIntent.ActorId`) sistem aktörünün nasıl yazıldığı
  **ölçülmedi**, DL-2 başında ölçülür; HTTP ucu yoktur, bu yüzden `[HasPermission]` taşıyan aksiyonu da yoktur; yalnız
  yapılandırmayla açılır). Bu iş tam tanımlayıcıyı çözer ve hiçbir yanıt, günlük ya da olayda taşımaz.

| Recovery anahtarı | Katman | Uçlar | Kaynak karşılığı |
|---|---|---|---|
| `mdm.banks.read` | T1 | banka ve şube liste / detay | `MOD0289_BANK_READ`, `MOD0289_BRANCH_READ` |
| `mdm.banks.create` | T1 | banka ve şube oluştur | `MOD0289_BANK_CREATE`, `MOD0289_BRANCH_CREATE` |
| `mdm.banks.update` | T1 | banka ve şube düzenle | — |
| `mdm.banks.activate` / `mdm.banks.deactivate` | T2 | banka ve şube durum | — |
| `mdm.banks.delete` / `mdm.banks.bulk-delete` | T1 / T2 | taslak silme | — |
| `mdm.bank-accounts.read` | T1 | liste, detay, `lookup-validation` (şirket hesabında veri kapsamı; tedarikçi hesabında `Active` ilişki, D23/1) | `MOD0289_LE_ACCOUNT_READ`, `MOD0289_SUPPLIER_DEST_READ` |
| `mdm.bank-accounts.read-tenant-wide` | T3 (yalnız açık atama) | kiracıdaki tüm şirketlerin hesapları ve **tüm tedarikçi hesapları maskeli** (D23/1) — **yalnız okuma**; maskesiz okumayı ve lookup'ı açmaz | — |
| `mdm.bank-accounts.create` | T1 | oluştur | `MOD0289_LE_ACCOUNT_CREATE`, `MOD0289_SUPPLIER_DEST_CREATE` |
| `mdm.bank-accounts.update` | T1 | düzenle | `MOD0289_GOVERNED_MUTATION_WRITE` (kısmen) |
| `mdm.bank-accounts.activate` / `mdm.bank-accounts.deactivate` | T2 | durum (+ dört göz) | — |
| `mdm.bank-accounts.delete` / `mdm.bank-accounts.bulk-delete` | T1 / T2 | taslak silme | — |
| `mdm.bank-accounts.read-unmasked` | T3 (yalnız açık atama, **KARAR D23/4**) | `unmasked-reads` (+ gerekçe kodu + kapsam / ilişki; değerden önce denetim) | `MOD0289_TOKEN_READ` (yakın) |
| `mdm.bank-accounts.sensitive-reads.read` | T1 | okuma kayıtları listesi | `MOD0289_SENSITIVE_ACCESS_LOG_READ` |
| `mdm.bank-account-purposes.read` | T1 | amaç liste / detay (hesap formundaki seçici `mdm.bank-accounts.create` / `.update` sahibine de amaç listesini okur — seçici çağrısı bu izni ister) | — |
| `mdm.bank-account-purposes.create` / `.update` / `.delete` | T1 | kiracı amacı oluştur / etiket düzenle / sil | — |
| `mdm.bank-account-purposes.activate` / `.deactivate` | T2 | kiracı amacı durum | — |

- **Taşınmayan kaynak anahtarlar:** token, değişiklik talebi, varsayılan atama, kanıt, istisna, içe aktarma, vekâlet,
  muafiyet, operasyon, kural anlık görüntüsü, doğrulama koşusu, pano anahtarları (v1 dışı ya da taşınmaz).
- `read-unmasked` ve `read-tenant-wide` hiçbir rol şablonunda varsayılan değildir ve otomatik yoldan verilmez:
  `ExplicitGrantOnlyPermissions`'a eklenmeleri Auth değişikliğidir (D9; Auth sahibi) — Ready-for-dev engeli.
- Tier-3 eylemler (`read-tenant-wide`, `read-unmasked`) bu pakette kayıtlıdır (PKS-001 §5 Tier 3); `sensitive-reads.read`
  dört parçalı anahtardır (PKS-001 ≥ 3 parça).

## Audited Events

**Yol `b` — `mdm-guvenilir-kaynak-niyeti` izinin genişletilmesi (KARAR D24/2).** Defter belirteci ayrı CT kararıdır. Ölçüldü (@158e0dc93):

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
**K2 (KARAR D24/2)** (ödeme yönlendirme verisi; AUD-001 §4.3 listesinde açıkça sayılmıyor, sahip bu modül için K2 saydı);
işlem enum'u eklemeli genel `MasterDataAuditOperation`. Banka / şube / hesap amacı yazmaları K2 değil ama aynı yoldan
yazıldığı için pratikte kapalı-başarısızdır.

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
| CreateBankAccountCommand | mdm.bank-account.created | BankAccount | b | Code, OwnerType, sahip kimliği, BankId, CurrencyCode, AccountIdentifierType, Status (**tanımlayıcı, son 4, maske, sahip adı yok**) | **evet (K2, D24/2)** | — |
| UpdateBankAccountCommand | mdm.bank-account.updated | BankAccount | b | değişen alan **adları** (Draft'ta tanımlayıcı değiştiyse yalnız `AccountIdentifier` adı) | **evet (K2, D24/2)** | — |
| ActivateBankAccountCommand | mdm.bank-account.activated | BankAccount | b | Status, ActivationVerificationCode, aktör ≠ oluşturan | **evet (K2, D24/2)** | — |
| DeactivateBankAccountCommand | mdm.bank-account.deactivated | BankAccount | b | Status, DeactivationReasonCode | **evet (K2, D24/2)** | — |
| DeleteBankAccountCommand | mdm.bank-account.deleted | BankAccount | b | BeforeState: Code, Status | **evet (K2, D24/2)** | — |
| BulkDeleteBankAccountCommand | mdm.bank-account.deleted (kimlik başına bir niyet) | BankAccount | b | aynı | **evet (K2, D24/2)** | — |
| RevealBankAccountIdentifierCommand (**okuma satırı**, AUD-001 §7) | mdm.bank-account.identifier-revealed | BankAccountSensitiveRead | b | BankAccountId, ReasonCode (değer yok) — değerden **önce** (KARAR D23/4) | **evet (K2, D24/2)** | — |
| CreateBankAccountPurposeCommand | mdm.bank-account-purpose.created | BankAccountPurpose | b | Code, IsActive, etiket **kültür adları** (etiket metni yok) | hayır | — |
| UpdateBankAccountPurposeCommand | mdm.bank-account-purpose.updated | BankAccountPurpose | b | değişen etiket kültür adları | hayır | — |
| ActivateBankAccountPurposeCommand | mdm.bank-account-purpose.activated | BankAccountPurpose | b | IsActive önce→sonra | hayır | — |
| DeactivateBankAccountPurposeCommand | mdm.bank-account-purpose.deactivated | BankAccountPurpose | b | IsActive önce→sonra | hayır | — |
| DeleteBankAccountPurposeCommand | mdm.bank-account-purpose.deleted | BankAccountPurpose | b | BeforeState: Code | hayır | — |
| ApproveBankDataKeySetTargetCommand (sistem aktörü) | mdm.bank-data.key-set-target-approved | BankDataKeySetTarget | **KARAR D24/9: yerel geçmiş + kiracı çit kaydı** — kiracısız servis kaydıdır; MDM niyet yolu kiracısız niyeti reddeder (`AuditIntentContract.BuildCentralIdempotencyKey` boş kiracıda istisna atar, `services/Diten.MdmService/src/Diten.MdmService.Domain/Entities/AuditIntentContract.cs:12@373513765`). Kayıt: hedef belgesinin yalnız-ekleme `History`'si + her kiracıdaki çit kurulumu / ilerletmesi niyetinde hedef `Version`, `ApprovalRevision` ve `ApprovalRef`; başka modül değişmez; merkezi kiracısız kayıt Follow-up | Version, ApprovalRevision, ExpectedPreviousKeySetId, ApprovedKeySetId önce→sonra, ApprovalRef (anahtar yok); ret (409) merkezi kayıt bırakmaz — bilinen boşluk | **evet (K2, D24/2)** | — |
| AdvanceBankDataKeyFenceCommand (sistem aktörü) | mdm.bank-data.key-fence-advanced (kurulumda `…key-fence-created`) | BankDataKeyFence | b | Epoch önce→sonra, ActiveKeySetId önce→sonra, hedef `Version` + `ApprovalRef` (kimlik özetleri; anahtar ya da denetim değeri yok) | **evet (K2, D24/2)** | — |
| RotateBankAccountProtectionCommand (sistem aktörü) | mdm.bank-account.protection-rotated | BankAccount | b | EncryptionKeyVersion önce→sonra, eklenen / silinen özet sürüm **adları** (özet değerleri, zarf ve tanımlayıcı yok) | **evet (K2, D24/2)** | — (İ4 kullanılmaz: zarf değiştiği için kayıt değişir ve denetlenir) |

- **Okuma denetimi:** maskesiz okuma AUD-001 §7'nin üç sınıfından birine açıkça girmez; bu paket onu daha sıkı kuralla
  denetler (hassas finansal veri). Okuma denetimi merkezi günlüğe gider; `BankAccountSensitiveRead` yalnız taşıyıcı
  belgedir, eşdeğer iz değildir. Diğer okumalar (liste, detay, lookup) denetlenmez.
- **Bilinen boşluk:** reddedilen komut (`Outcome = Denied`) merkezi kayıt bırakmaz — niyet yalnız başarılı yazımla
  oluşur (MOD-0126 G-AUD-1 ile aynı sınıf); reddedilen maskesiz okuma denemeleri yalnız uygulama günlüğünde (değer
  olmadan) görünür. Merkezi ret kaydı follow-up.
- **Arka plan işi:** yalnız `BankAccountProtectionRotationWorker`; komut değildir, gönderdiği her
  `RotateBankAccountProtectionCommand` kendi niyetiyle denetlenir — işin kendisi için İ2 satırı gerekip gerekmediği defter
  testinin barındırılan işleri nasıl saydığına bağlıdır, **ölçülmedi** (DL-2 başında `AuditTrailStandardTests` okunur).
  Kaynak seed işçisi taşınmaz.

## Gateway / API Routing Decision

- **Karar: Gateway değişikliği gerekli.** `ocelot.json`'da `/api/mdm/{everything}` catch-all yok; MDM rotaları kaynak
  başına açık yazılmış (ör. `/api/mdm/brands`, `/api/mdm/brands/{everything}` — `gateway/Diten.ApiGateway/ocelot.json:3781,3798@158e0dc93`).
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
- [ ] AC-12 (zorunlu) — Aynı IBAN (farklı biçimde yazılmış — boşluklu / küçük harfli dahil) kiracıda ikinci kez
  kaydedilemez: 409 `MDM_BANK_ACCOUNT_DUPLICATE`, gövde ilk kaydın kimliğini, kodunu ya da sahibini içermez; başka
  kiracıda aynı IBAN kaydedilebilir. IBAN'da boşluk dışı ayırıcı (tire, nokta) 400.
- [ ] AC-13 (zorunlu) — Saklama: oluşturulan hesabın Mongo belgesi ve koleksiyondaki bütün indeksler normalize
  tanımlayıcıyı (ve onun 5+ karakterlik herhangi bir parçasını, son 4 hane dışında) içermez (ham belge taraması);
  `UniquenessKeyDigests` aynı tekillik anahtarı ve aynı özet sürümü için aynı, düz SHA-256 ile eşleşmez. Koruyucu düz metin yazacak şekilde değiştirildiğinde test
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
- [ ] AC-18 (zorunlu) — Create: şirket `Active` + `ApprovalStatus = Draft` → 201 (KARAR D23/3); Activate: şirket `Active`
  ama `ApprovalStatus ≠ Approved` → 422 (D23/3); oluşturan ya da son güncelleyen kullanıcının Activate'i 403
  `MDM_BANK_ACCOUNT_FOUR_EYES_REQUIRED` (KARAR D23/2); başka kullanıcı geçer; MOD-0023'e hiçbir çağrı yapılmaz (D23/2). Dört göz kontrolü kaldırıldığında test kırmızıya
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
  Create / Activate sürer; `OnHold` / `Blocked` / `Inactive` ve `known: false` → Create / Activate 422, var olan hesapta
  okuma ve diğer yazmalar 404 (D23/1); sonuçta istenen kimlik yok ya da
  farklı, `known` boolean değil, `status` dört değerin dışında (`"active"` küçük harf dahil), ayrıştırılamayan gövde, 401,
  403, 500 ve zaman aşımı → 503 `MDM_BANK_ACCOUNT_OWNER_UNAVAILABLE`; 422 ve 503 vakalarında kayıt ve niyet yazılmaz.
  `lookup-validation` ilişki `Active` ise 200 + `ownerState = ELIGIBLE`, değilse 404, geçersiz / erişilemez ise 503 döner. Sabotaj: "yanıt okunamazsa uygun say"
  yapıldığında test kırmızıya döner.
- [ ] AC-25 (zorunlu) — Tedarikçi sahibi MOD-0140 `supplierId` dizgisi olarak olduğu gibi saklanır ve sıralı karşılaştırılır
  (`SUP-2001` ≠ `sup-2001`); tedarikçi sahipli hesabın `lookup-validation`'ı `X-Legal-Entity-Id` olmadan 400, kiracıya ait
  olmayan / aktif olmayan şirketle 404; aynı tedarikçi hesabı `Active` ilişkili şirketten 200, ilişkisiz şirketten 404 döner.
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
- [ ] AC-33 (zorunlu) — Yurt içi tekillik anahtarı (gap 13 P1-a): aynı yurt içi numara **iki farklı bankada** (farklı
  ulusal banka kodu) kabul edilir (iki hesap); **aynı bankada** ikinci kez 409 `MDM_BANK_ACCOUNT_DUPLICATE`; bankanın
  `DomesticKeyIncludesBranch = true` iken aynı numara iki farklı şubede kabul, aynı şubede 409; `false` iken aynı numara iki
  şubede 409 (şube anahtara girmez); `001234` ile `1234` iki ayrı hesaptır; `12-34 56` ile `123456` aynı hesaptır; aynı
  bankada aynı numaranın IBAN ve yurt içi kaydı v1'de ayrı kabul edilir (bilinen sınır, Runtime Constraints). Anahtar
  yalnız hesap numarasına indirildiğinde "iki bankada kabul" vakası kırmızıya döner (sabotaj).
- [ ] AC-34 (zorunlu) — Yurt içi anahtar kuralları: ulusal banka kodu olmayan bankada yurt içi hesap 400
  `MDM_BANK_DOMESTIC_BANK_CODE_REQUIRED`; `DomesticKeyIncludesBranch = true` bankada şubesiz yurt içi hesap 400
  `MDM_BANK_DOMESTIC_BRANCH_REQUIRED`; silinmemiş yurt içi hesabı olan bankada `CountryCode`, `NationalBankCode`,
  `DomesticKeyIncludesBranch` ve şubede `BranchCode` değişikliği 409 `MDM_BANK_DOMESTIC_KEY_LOCKED`; hesap yokken izinli.
  Kilit kontrolü kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-35 (zorunlu) — Özet anahtarı döndürmesinde yinelenen tespiti (gap 13 P1-b): hesap A yalnız özet sürümü `h1`
  etkinken açılır; `h2` eklenip güncel yapılır (`h1` hâlâ `Active`, A henüz yeniden özetlenmemiş) → aynı hesabın ikinci
  kaydı 409 (h1 özetinden); döndürme işi A'ya `h2` özetini ekler ve `h1` emekliye ayrılıp özetleri silinir → aynı hesabın
  kaydı yine 409 (h2 özetinden); iki örnek döndürme sırasında aynı hesabı eşzamanlı açarsa biri 409 (gerçek replica set).
  Yinelenen kontrolü yalnız güncel sürümle yapılacak ya da yeni kayıt yalnız güncel sürümün özetini yazacak şekilde
  değiştirildiğinde ilk vaka kırmızıya döner (sabotaj).
- [ ] AC-36 (zorunlu) — Döndürme işi ve başlangıç kontrolü: iş kesintiye uğrayıp yeniden başlatıldığında kalan kayıtları
  işler, işlenmiş kaydı tekrar değiştirmez (belge sürümü aynı), her değişiklik için tam bir `mdm.bank-account.protection-rotated`
  niyeti yazar ve niyette zarf, özet ya da tanımlayıcı yoktur; hiçbir `Active` özet sürümü taşımayan kayıt varken ya da
  kayıtlarda geçen bir şifreleme sürümü yapılandırmada yokken servis açılmaz. Başlangıç kontrolü kaldırıldığında "erken
  emekli edilen sürüm" vakası kırmızıya döner (sabotaj).
- [ ] AC-37 (zorunlu) — AES-GCM bütünlüğü (iyileştirme 2): aynı tanımlayıcının 10 000 şifrelemesinde nonce'lar tekil ve 12
  bayt, zarflar birbirinden farklı; zarf başka bir hesaba ya da başka kiracıya kopyalandığında, `EncryptionKeyVersion`
  değiştirildiğinde, şifreli metinde / etikette / nonce'ta tek bayt değiştirildiğinde ve sürüm doğru ama anahtar baytı
  farklı olduğunda maskesiz okuma 500 `MDM_BANK_PROTECTION_INTEGRITY_FAILED` döner, değer ya da kısmi değer yoktur, okuma
  kaydı ve niyet kalmaz, günlükte tanımlayıcı yoktur. AAD bağlaması kaldırıldığında kopyalama vakası, nonce sabitlendiğinde
  tekillik vakası kırmızıya döner (sabotaj).
- [ ] AC-38 (zorunlu) — Anahtar yapılandırması: güncel şifreleme anahtarı yok, güncel özet sürümü yok, anahtar 32 bayt
  değil ya da bir özet anahtarı bir şifreleme anahtarıyla aynıysa servis açılmaz (başlangıç doğrulama testi); çalışırken
  kayıttaki sürümün anahtarı erişilemezse 503 `MDM_BANK_PROTECTION_UNAVAILABLE` ve değer yok.
- [ ] AC-39 (zorunlu) — Yeniden şifreleme ve karışık sürümler: `k1` ve `k2` sürümlü kayıtlar aynı anda okunabilir; iş `k1`
  kaydını yeni nonce ile `k2`'ye taşır, maskesiz değer önce ve sonra aynıdır, eski zarf belgede kalmaz; iş yarıda
  durdurulup yeniden çalıştırıldığında hepsi `k2` olur; `k1` yapılandırmadan `k1` kaydı kalmışken çıkarılırsa servis
  açılmaz (AC-36). İş eski nonce'u yeniden kullanacak şekilde değiştirildiğinde test kırmızıya döner (sabotaj).
- [ ] AC-40 (zorunlu) — IBAN denetim düzeyi (gap 13 P2): test, ülke uzunluğu yanlış bir BBAN için sağlama hanelerini
  kendisi hesaplayarak **sağlaması geçerli ama ülke biçimi geçersiz** bir IBAN üretir; kapı `Off` iken (v1) bu IBAN kabul
  edilir ve `IdentifierCheckLevel = FormatAndChecksum` belgede, DTO'da (`identifierCheckLevel`), liste rozet verisinde ve
  `lookup-validation`'da görünür; kapı `On` + sahte `IIbanCountryFormatSource` iken aynı IBAN 400
  `MDM_BANK_IBAN_COUNTRY_FORMAT_INVALID`, geçerli IBAN `CountryFormat`; kapı `On` + kaynak yok / hata → 503
  `MDM_BANK_IBAN_COUNTRY_FORMAT_UNAVAILABLE` (düşük düzeye inilmez). Kapı açıkken kaynak hatası "biçim yeter" sayılacak
  şekilde değiştirildiğinde test kırmızıya döner (sabotaj).
- [ ] AC-41 (zorunlu) — Tedarikçi hesabı kapsamı (KARAR D23/1, gap 13 P1-c; sahte MOD-0140): `Active` ilişkili şirketin
  kullanıcısı tedarikçi hesabını listede görür, detay 200 (maskeli), `lookup-validation` 200; `read-unmasked` + gerekçe ile
  maskesiz okuma 200. **İlişkisiz** şirketin kullanıcısı (ilişki yok, `OnHold`, `Blocked`, `Inactive`, `known: false`,
  şirket başlığı yok ya da şirket kullanıcının kapsamında değil): liste dışı, detay 404, `lookup-validation` 404, maskesiz
  okuma 404 (`read-unmasked` olsa da), Update / Activate / Deactivate / Delete 404. **`read-tenant-wide`** olan ama ilişkisi
  olmayan kullanıcı: bütün tedarikçi hesaplarını listede ve detayda **maskeli** görür (yanıtta tam tanımlayıcı yok),
  maskesiz okuma 403 `MDM_BANK_ACCOUNT_RELATIONSHIP_REQUIRED` ve okuma kaydı yok, yazmalar ve lookup 404; MOD-0140
  erişilemezken ilişki isteyen her okuma 503 (hesap gösterilmez). İlişki kontrolü kaldırıldığında ilişkisiz kullanıcı
  vakaları kırmızıya döner (sabotaj).
- [ ] AC-42 (zorunlu) — Lookup uygunluk alanları (iyileştirme 1): üretici testleri — pasif bankadaki aktif hesap 200 +
  `referenceable = true` + `bankState = INACTIVE`; onayı geri çekilmiş şirketin hesabı 200 + `referenceable = true` +
  `ownerState = NOT_ELIGIBLE`; her 200 gövdesi E1…E9 alanlarının hepsini taşır. Sözleşme dosyası her uygunluk alanı için
  bir negatif fikstür (öteki alanlar uygun, `referenceable = true`) ve her 503 satırı için bir fikstür taşır; MdmService
  testlerindeki başvuru sınıflandırıcısı negatif fikstürlerin her birinde 422, 503 fikstürlerinde 503, uygun fikstürde
  "sürer" verir. Sınıflandırıcı yalnız `referenceable`'a bakacak şekilde değiştirildiğinde pasif banka ve onaysız sahip
  fikstürleri kırmızıya döner (sabotaj). Tüketici paketlerinin aynı fikstürleri kendi sınıflandırıcılarına karşı koşması o
  paketlerin işidir (Follow-up tablosu).
- [ ] AC-43 (zorunlu) — Anahtar kümesi çiti, özet döndürmesi (gap 14/1; gerçek replica set, iki örnek): örnek A eski küme
  K1 (yalnız `h1`), örnek B yeni küme K2 (`h1` + `h2`) ile çalışır ve ikisi döndürme boyunca eşzamanlı hesap yazar; çit K2'ye
  ilerletildikten sonra A'nın her yazımı 503 `MDM_BANK_KEYSET_STALE` alır ve hiçbir belge yazılmaz; ilerletmeyle eşzamanlı
  A yazımları ya ilerletmeden önce commit olur ya da reddedilir; iş bittikten ve `h1` emekliye ayrıldıktan sonra **`h2`
  özeti olmayan hiçbir kayıt yoktur** ve aynı hesabın yeniden kaydı 409'dur. Çit kontrolü (`ActiveKeySetId` süzgeci ya da
  `WriteSeq` artırımı) kaldırıldığında "emeklilikten sonra yalnız `h1` özetli kayıt" vakası kırmızıya döner (sabotaj).
- [ ] AC-44 (zorunlu) — Anahtar kümesi çiti, şifreleme anahtarı emekliliği (gap 14/1): eski güncel şifreleme sürümü `k1`
  ile çalışan örnek, çit `k2`'yi güncel yapan kümeye ilerletildikten sonra zarf yazamaz (503); iş bittikten sonra `k1`
  zarflı kayıt sayısı 0'dır ve `k1` çıkarıldıktan sonra bütün kayıtlar okunabilir; çit ilerletilmeden `k1`'i çıkarma adımı
  reddedilir (iş "çit hedef kümede değil" der). Çit kaldırıldığında eski örneğin emeklilik sonrası `k1` zarfı yazdığı vaka
  kırmızıya döner (sabotaj).
- [ ] AC-45 (zorunlu) — Yurt içi anahtar kilidi yarışı (gap 14/2; gerçek replica set, paralel istek): bir istek bankada
  ilk yurt içi hesabı açarken diğeri bankanın `NationalBankCode`'unu (ayrı denemede `DomesticKeyIncludesBranch`'ı ve şubenin
  `BranchCode`'unu) değiştirir → tam biri başarılı, diğeri 409 (`MDM_BANK_DOMESTIC_KEY_CHANGED` ya da
  `MDM_BANK_DOMESTIC_KEY_LOCKED`); son durumda hesabın tekillik anahtarı bankanın güncel alanlarıyla tutarlıdır. Testi 100
  tekrar koşar. `DomesticKeyFenceVersion` artırımı iki taraftan kaldırıldığında ikisinin birlikte başarılı olduğu bir tekrar
  bulunur ve test kırmızıya döner (sabotaj).
- [ ] AC-46 (zorunlu) — Döndürme işinin atlama koşulu (gap 14/3): şifrelemesi güncel ama yeni `Active` özeti eksik kayıt
  atlanmaz — yalnız özet eklenir, zarf baytları (nonce dahil) değişmez; özeti tam ama şifrelemesi eski kayıt yalnız yeniden
  şifrelenir; ikisi de tam ama `Retiring` özeti duran kayıt yalnız temizlenir; üçü de tamsa belge değişmez; her durumda tek
  niyet ve aynı kayda ikinci geçişte değişiklik yok. Atlama koşulu yalnız şifreleme sürümüne bakacak şekilde
  değiştirildiğinde ilk vaka kırmızıya döner (sabotaj).
- [ ] AC-47 (zorunlu) — Yurt içi doğrulama kapısı (gap 14/4): kapı `Off` (v1) iken yurt içi hesap `DomesticFormat` olarak
  kaydedilir ve lookup'ta `identifierType = DOMESTIC` + `identifierCheckLevel = DOMESTIC_FORMAT` döner; kapı `On` + sahte
  kaynaklar iken dizinde olmayan banka kodu ve ülke kuralına uymayan numara ayrı ayrı 400
  `MDM_BANK_DOMESTIC_VERIFICATION_FAILED`, ikisi de geçerse `DomesticVerified`; kaynaklardan biri yok / hata → 503; IBAN hesabı
  bu kapıdan etkilenmez, yurt içi hesap IBAN ülke kapısından etkilenmez. Başvuru sınıflandırıcısı (AC-42) "gerçek dağıtım"
  profilinde `DOMESTIC_FORMAT` ve `FORMAT_AND_CHECKSUM` için 422, `DOMESTIC_VERIFIED` / `COUNTRY_FORMAT` için "sürer",
  türle uyuşmayan düzey için 503 verir. Kapı açıkken kaynak hatası "biçim yeter" sayılacak şekilde değiştirildiğinde test
  kırmızıya döner (sabotaj).
- [ ] AC-48 (zorunlu) — `KeySetId` güncel seçimi içerir (gap 15/1): aynı anahtar listesi ve aynı durumlarla çalışan iki
  örnekten biri `CurrentHashKeyVersion` (ayrı denemede `CurrentEncryptionKeyVersion`) için farklı sürüm seçer; çit diğerinin
  kümesindeyken bu örneğin her yazımı 503 `MDM_BANK_KEYSET_STALE` alır ve hiçbir belge yazılmaz; kanonik serileştirme bir
  sabit örnek girdi için belgelenen `ks2-…` değerini üretir (altın değer testi), satır sırası ya da sürüm sırası değişince
  değer değişmez (sıralama), `|` içeren sürüm adı servisi açmaz. Güncel alanlar karma girdisinden çıkarıldığında farklı
  seçimli örneğin yazabildiği görülür ve test kırmızıya döner (sabotaj).
- [ ] AC-49 (zorunlu) — Yeni kiracı çiti onaylı hedefle kurulur (gap 15/2; gerçek replica set): onaylı hedef K2 iken, çit
  belgesi olmayan yeni kiracıda K1 (eski) ve K2 (yeni) örnekleri aynı anda ilk banka hesabını yazar (50 tekrar) → çit
  her tekrarda `ActiveKeySetId = K2` ile kurulur, K1 yazımları 503 alır, K1 kümesiyle (`WrittenWithKeySetId = K1`) yazılmış
  hiçbir kayıt ve K1 ile kurulmuş hiçbir çit yoktur; hedef yokken her yazım 503 `MDM_BANK_KEYSET_TARGET_UNAVAILABLE` ve çit
  kurulmaz; hedef K1'den K2'ye onaylanırken eşzamanlı K1 çit kurulumu ya hedef değişikliğinden önce commit olur (sonra
  normal ilerletme kuralı) ya da reddedilir. "İlk yazan kendi kümesiyle kurar" davranışına dönüldüğünde K1'in kazandığı bir
  tekrar bulunur ve test kırmızıya döner (sabotaj).
- [ ] AC-50 (zorunlu) — Çit yük testi (gap 14/1 ölçülmeyen kalemi; DL-2 dilim kabul kapısında geçmeli): gerçek replica
  set'te tek kiracıda en az 8 paralel yazar 10 dakika banka hesabı Create / Update yazar ve aynı sürede bir çit ilerletmesi
  yapılır; her istek ya başarılı ya tanımlı sonuçla (409 / 503) biter, kayıp ya da çift yazım yoktur, ilerletmeden sonra eski
  kümeli commit yoktur; çakışma ve yeniden deneme oranı, p95 / p99 gecikme `scripts/run_record.py` ile kaydedilir. Kabul
  eşiği (ör. yeniden deneme oranı ve p95 üst sınırı) sahip + güvenlik tarafından **`ready-for-dev`'den önce** yazılır (tasarım
  kapısı maddesi); eşik yazılmadan bu AC geçmiş sayılamaz.
- [ ] AC-51 (zorunlu) — Onaylı hedef geri alınamaz (gap 16/1): hedef K2 (revizyon n) iken, eski yapılandırmayla kalmış iş K1'i
  revizyon n-1 ile yeniden onaylamaya çalışır → 409 `MDM_BANK_KEYSET_TARGET_REVISION_STALE`, hedef K2, `ApprovalRevision` n,
  `History` değişmez; aynı revizyon n'nin tekrarı da 409; revizyon n+1 ama `ExpectedPreviousKeySetId = K1` → 409
  `MDM_BANK_KEYSET_TARGET_PREVIOUS_MISMATCH`; açık geri dönüş — revizyon n+1, `ExpectedPreviousKeySetId = K2`, yeni
  `ApprovalRef`, K1 kayıtlardaki bütün sürümleri içeriyor → kabul, hedef K1, `History`'de revizyon n+1 satırı; K1 kayıtlarda
  geçen bir sürümü içermiyorsa → 409 `MDM_BANK_KEYSET_TARGET_INCOMPLETE`. Revizyon kontrolü süzgeçten çıkarıldığında eski
  K1 tekrarının kazandığı görülür ve test kırmızıya döner (sabotaj).
- [ ] AC-52 (zorunlu) — Onay yarışı ve çit revizyonu (gap 16/1; gerçek replica set, 50 tekrar): yeni iş K2'yi revizyon n ile
  onaylarken eski iş K1'i revizyon n-1 ile eşzamanlı onaylamaya çalışır → her tekrarda son hedef K2'dir (K1 ya hiç yazılmaz
  ya da önce yazılıp K2 tarafından geçilir; K2'den sonra K1 yazımı yoktur); eski iş bir kiracı çitini düşük revizyonlu hedefe
  ilerletmeye çalışırsa ilerletme reddedilir ve çitin `AppliedTargetRevision`'ı hiç azalmaz. Çit ilerletmesinden revizyon
  koşulu kaldırıldığında çitin K1'e döndüğü bir tekrar bulunur ve test kırmızıya döner (sabotaj).
- [ ] AC-53 (zorunlu) — Çekirdek hesap amaçları değişmez (D24/6): amaç listesi her kiracıda altı çekirdek kodu `isCore = true`
  ile aynı sırayla döner; çekirdek kod için düzenleme, pasifleştirme ve silme yolu yoktur (çekirdek kodu hedefleyen her yazma
  isteği 409 `MDM_BANK_ACCOUNT_PURPOSE_CORE_IMMUTABLE` ya da kimliği olmadığından 404) ve hiçbir çekirdek kod veritabanına
  yazılmaz; altı kodun her biriyle hesap açılabilir. Çekirdek liste veritabanından okunacak şekilde değiştirildiğinde
  "çekirdek kod silinemez" vakası kırmızıya döner (sabotaj).
- [ ] AC-54 (zorunlu) — Kod çakışması ve değişmezlik: kiracı kodu `OPERATING` (ve `operating`, ` Payroll ` normalize
  biçimleri) 409 `MDM_BANK_ACCOUNT_PURPOSE_CODE_RESERVED`; aynı kiracıda ikinci `ESCROW` 409
  `MDM_BANK_ACCOUNT_PURPOSE_CODE_CONFLICT`, silinmiş `ESCROW`'un kodu da yeniden kullanılamaz; başka kiracıda `ESCROW` kabul;
  Update'te kod değişikliği 400 (yalnız etiket). Çekirdek kod kontrolü kaldırıldığında ilk vaka kırmızıya döner (sabotaj).
- [ ] AC-55 (zorunlu) — Kullanılan amacın pasifleştirilmesi ve silinmesi: `ESCROW` kullanan hesap varken pasifleştirme 200,
  hesabın belge sürümü değişmez, `lookup-validation` aynı kalır; yeni hesap Create ve amacı `ESCROW`'a değiştiren Update 422
  `MDM_BANK_ACCOUNT_PURPOSE_INACTIVE`, amacı değiştirmeyen Update geçer; kullanılan amacın silinmesi 409
  `MDM_BANK_ACCOUNT_PURPOSE_IN_USE`; yeniden aktive etme yeni kullanımı açar. Gerçek replica set'te pasifleştirme ile o amaçla
  hesap oluşturma eşzamanlı (50 tekrar) → pasifleştirme commit olduktan sonra o amaçla yeni hesap yoktur. `UsageFenceVersion`
  artırımı kaldırıldığında yarış vakası kırmızıya döner (sabotaj).
- [ ] AC-56 (zorunlu) — 7 dil etiketi zorunlu: Create / Update'te `en, tr, fr, es, zh, ar, ru`'dan biri eksik, boş, 80 karakteri
  aşan ya da fazladan bir kültür (`de`) → 400; yedisi geçerliyse 201 / 200; denetim niyetinde etiket metni yoktur. Kültür
  sayısı denetimi kaldırıldığında eksik dil vakası kırmızıya döner (sabotaj).
- [ ] AC-57 (zorunlu) — Kiracılar arası: başka kiracının amacına okuma, düzenleme, pasifleştirme, silme 404; kiracı B'nin
  hesabı kiracı A'nın `ESCROW` kodunu kullanamaz (B'de yoksa 400); amaç listesi yalnız kendi kiracısının ek amaçlarını ve
  çekirdek kodları döner. Depo `TenantFilter`'ı kaldırıldığında test kırmızıya döner (sabotaj).
- [ ] AC-58 (zorunlu) — Hesap amaçları ekranı (DL-4): `Views/MasterData/BankAccountPurposes/*.cshtml`'de `Layout =
  "_LayoutTenantShell"` açık; `verify_datatable_page.py . --area MasterData --module BankAccountPurposes --format gaps` sapma
  vermez ve `<table data-dt-data-mode="client">` pack `screens` satırıyla eşleşir; çekirdek satırlarda düzenle / sil düğmesi
  çizilmez; tarayıcı yedi kültürde açıldığında çekirdek kodlar resx etiketiyle, kiracı amaçları kaydın o kültürdeki etiketiyle
  görünür, sayfada sabit metin yoktur (`grep` 0) ve `BankAccountsIndex` / `BankAccountPurposesIndex` resx parite PASS;
  `Help.BANKACCOUNTPURPOSES.*` 7 dilde; `read` izni olmayan kullanıcıya UAS-001 erişim açıklaması.

## Test Expectations

- **Birim:** IBAN (MOD 97-10, normalize, ülke; sağlama geçerli ama ülke uzunluğu yanlış üretilmiş vektör), BIC, yurt içi
  numara kuralları; türe göre tekillik anahtarı (AC-33); AES-GCM zarfı (nonce tekilliği, AAD, kurcalama — AC-37); özet
  çift yazma (AC-35); başvuru lookup sınıflandırıcısı ve fikstürler (AC-42); maske ve son 4 türetimi; doğrulayıcılar
  (her alan); durum makinesi; dört göz (Guid karşılaştırması, büyük/küçük harf farklı `sub`); tedarikçi ve LE yanıt
  sınıflandırması; RCS kod portu sahtesiyle kod biçimi.
- **Entegrasyon (Mongo replika kümesi, işlem):** kiracı izolasyonu (404); veri kapsamı (AC-17, sabotajlı); soft delete;
  CAS 409; tekil indeksler ve yarış (AC-2, AC-9, AC-12); denetim niyetinin iş belgesiyle birlikte yazılması ve niyet yazım
  hatasında komutun başarısız olması (sabotaj); okuma kaydı önce-yaz; ham belge taraması (AC-13); `Guid.Empty` taraması
  (AC-26).
- **Günlük yakalama:** test günlükçüsüyle Create / Update / hata / istisna yollarında tanımlayıcı yokluğu (AC-14).
- **Sahte MOD-0140:** dört durum, `known: false`, geçersiz gövde, 401/403/500, zaman aşımı; ilişkili / ilişkisiz / kiracı
  geneli kullanıcı matrisi (AC-41); 1.3.0 main'e girince gerçek sözleşmeye karşı sözleşme testi.
- **Anahtar döndürme:** iki örnek, iki yapılandırma (h1 / h1+h2) ile eşzamanlı oluşturma (AC-35); kesintili iş (AC-36,
  AC-39); başlangıç doğrulaması (AC-38); eski / yeni örnekli kademeli dağıtım ve çit (AC-43, AC-44); atlama koşulu (AC-46).
- **Yarış:** yurt içi hesap oluşturma ile banka / şube anahtar alanı değişikliği, 100 tekrar (AC-45).
- **Gerçek Mongo şartı:** O-6 ve çitlerle ilgili AC'ler (AC-13, AC-35…AC-39, AC-43…AC-46, AC-48…AC-52) yalnız gerçek replica set'te
  (işlem ve yazma çakışması olan) geçmiş sayılır; bellek içi sahte depo bu AC'leri kapatmaz.
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
- `Help.BANKACCOUNTPURPOSES.Purpose` (çekirdek amaçlar sabit; kiracı ek amaç ekler, 7 dilde etiket zorunlu),
  `Help.BANKACCOUNTPURPOSES.Audience` (ana veri sorumlusu), `Help.BANKACCOUNTPURPOSES.Step1…Step3` + `Step{n}Title` (Amaç ekle ·
  7 dilde etiket gir · Kullanılmayanı pasifleştir).
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

**İki kapı (gap 16/2):** (a) **tasarım / onay kapısı = `ready-for-dev`** — aşağıdaki ENGELLER: kararlar, onaylar,
sözleşmeler, yazılmış eşikler ve yalnız var olan kodun / sözleşmenin **okunarak** ölçülmesi. Yeni kodun test geçmesi bu
kapının maddesi **değildir** (kod `ready-for-dev`'den önce yazılamaz; ikisini birden istemek kilitlenme olurdu). (b) **Dilim
kabul kapısı** — her dilimin kabulü / birleştirilmesi için: o dilimin AC'lerinin geçmesi (AC-50 yük testi ve her gerçek
replica set AC'si dahil), build ve tüketici kanıtı. Tanımı "Teslim dilimleri → Dilim kabul kapısı"ndadır.

**ENGELLER — tasarım / onay kapısı (hepsi kapanmadan `ready-for-dev` olmaz):**
- [ ] CT-APR-02 bağımsız inceleme bu paket sürümü için kaydedildi (`execution/approvals/reviews.tsv`), açık ENGEL yok
- [ ] CT-APR-01 sürüme bağlı onay satırı (`execution/approvals/approvals.tsv`); yüksek risk (hassas finansal veri) —
  paketi yazmamış, `high: evet` onaylayıcı
- [ ] Sahip MOD-0289 için kod alma iznini verdi (D1 — bugün izin yalnız MOD-0126 ilk adayı için konuşuldu; MOD-0289 için yok)
- [ ] DCP (`UNASSIGNED-finance-treasury-intake`) `approved`, TRM-S3 dilimi tanımlı
- [ ] MDM domain-config'e MOD-0289 satırı ve frontend/gateway kapsam cümlesi (bugün istisna yalnız MOD-0290-FU02)
- [x] O-1, O-2, O-3, O-9 sahip kararıyla yazıldı (KARAR D23/2, D23/3, D23/4, D23/1)
- [x] O-4, O-5, O-6 (sahip onayı), O-7, O-8, O-10, O-11, O-12 ve kiracısız hedef denetimi sahip kararıyla yazıldı (KARAR
  D24/1–9)
- [ ] O-13 (hesap amaçları ekranının şekli) — tek kalan açık karar
- [ ] Hesap amacı kodunun RCS-001 dışında sözlük anahtarı sayılmasının CT teyidi (D24/6 uygulaması)
- [ ] RCS-001 ortak kod servisi (BL-565) ve `BNK` / `BAC` önekleri RCS-001 §3'te (D24/4 karar verdi; `.antigravity` satırı ayrı
  onay)
- [ ] Denetim izinin genişletilmesi için Control Tower kararı (defter belirteci + CT-DECISIONS satırı)
- [ ] Şifreleme ve özet anahtarı kaynağı (üretim gizli deposu) ve döndürme prosedürü yazıldı (O-6) — DL-2 için
- [ ] **Güvenlik onayı (O-6, D24/3 sahip onayı var) — şifreleme + anahtar çiti + onaylı hedef + revizyon tasarımı; görünür
  engel.** Gerçek Mongo testleri de açıktır; onlar DL-2 dilim kabul kapısındadır (gap 16/2)
- [ ] Çit yük testinin kabul eşiği (AC-50 için: yeniden deneme oranı, p95 / p99 üst sınırı) sahip + güvenlik tarafından yazıldı
- [ ] Onaylı dağıtım revizyonunun (`ApprovalRevision`) ve `ApprovalRef`'in hangi onay kaydından geldiği yazıldı (güvenlik
  onayının parçası)
- [ ] SEC-002 kapsam sağlayıcısının MOD-0289 anahtarlarıyla çalıştığı ölçüldü (D23/1 kuralı) — DL-2 için
- [ ] MOD-0140 1.3.0'da "çağıran şirketle Active ilişkili tedarikçi kimlikleri" okumasının hangi işlemle verildiği ölçüldü
  (D23/1 liste süzgeci) — DL-3 için
- [ ] Auth: `ExplicitGrantOnlyPermissions`'a iki anahtar (D9, Auth sahibi) — DL-2 için
- [ ] MOD-0048 para birimi listesinin MdmService'ten tüketim yolu ölçüldü — DL-2 için
- [ ] MOD-0140 `supplier.openapi.yaml` 1.3.0 main'de (MVP-2) ve MdmService → MOD-0140 çağrı kimliği belirlendi — DL-3 için
- [ ] MDM `ValidationBehavior`'ın 400 gövdesinde girilen değeri döndürüp döndürmediği ölçüldü — DL-2 için
- [ ] Gateway rotaları (integration-agent) — DL-4 için

**İsteğe bağlı teknik ön doğrulama (öneri — yapılması şart değil, sahip onayı ister):** iki dar soru `ready-for-dev`'den
önce tasarımı etkileyebilir: (S-1) dizi alanı üzerinde kısmi süzgeçli tekil indeksin gerçek replica set'te beklendiği gibi
çakıştığı; (S-2) MdmService'in hedef çerçevesinde `System.Security.Cryptography.AesGcm`'in kullanılabildiği. Yapılırsa:
yalnız ayrı, atılacak bir doğrulama kapsamında (depoda üretim yolu yok, `services/**` değişmez, birleştirme yok, dal ya da
çalışma klasörü atılır), sonuç `scripts/run_record.py` kaydıyla bu pakete ölçüm olarak yazılır; sahip açıkça onaylamadan
başlamaz. Yapılmazsa iki soru DL-2 dilim kabul kapısında AC-35 ve AC-37 ile cevaplanır; olumsuz çıkarsa tasarım o dilimde
bu paketin revizyonuyla değişir.

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
D24/5; AUTH → `[HasPermission]` + explicit-grant; AUDIT → Audited Events (D24/2); PAYMENT ve CASH-CONSUMER →
`BANK-ACCOUNT-LOOKUP`; UI → DL-4; WORKFLOW → v1'de yok (D23/2); EVIDENCE → v1'de yok (D24/1); EVENTS ve SEED → taşınmaz
(yayıncı ve seed yok); CUSTOMER → kapsam dışı (sahipsiz, kimlik uydurulmaz); REGISTRY → MOD-0003 yok, sözleşme
`docs/analysis/contracts/` altında. Slot kapanışları §U kaydında izlenir; bu paket hiçbir ACK oluşturmaz.

### Gap 13 kapanışı (sahip gap listesi 13, CONDITIONAL PASS → revizyon 2)

| Madde | Bulgu | Kapanış (bu pakette) | AC |
|---|---|---|---|
| P1-a | Yurt içi tekillik çakışabilir: özet yalnız normalize hesap numarasıydı | Türe göre tekillik anahtarı ve ayrı normalizasyon: IBAN → kanonik IBAN; yurt içi → ülke + ulusal banka kodu (+ bankanın `DomesticKeyIncludesBranch` ayarıyla şube kodu) + numara (baştaki sıfırlar korunur); anahtar alanları hesap varken kilitli (Entity Fields, Runtime Constraints → "Tekillik anahtarı", Validation Rules) | AC-12, AC-33, AC-34 |
| P1-b | HMAC anahtar döndürme tanımsız | Kayıtta özet sürümleri (`UniquenessKeyDigests`, `HashKeyVersions`); her etkin sürümle çift yazma; çok anahtarlı tekil indeks; beş adımlı yeniden özetleme prosedürü; başlangıç kontrolü (Runtime Constraints → "Özet anahtarı döndürme") | AC-35, AC-36 |
| P1-c | Tedarikçi kapsamı kurala bağlı değildi | KARAR D23/1 kural olarak uygulandı: ilişkili şirket görür; ilişkisiz → liste dışı + 404; maskesiz okuma izin + ilişki; kiracı geneli izin maskeli görür, maskesiz 403 (Runtime Constraints → "Veri kapsamı", lookup 404 kuralı, Authorization) | AC-24, AC-25, AC-41 |
| P2 | IBAN denetim sınırı belirsizdi | "Biçim + sağlama geçerli" ile "ülke biçimi geçerli" ayrıldı; v1 yalnız ilkini yapar ve kaydı `FormatAndChecksum` işaretler (belge, DTO, liste rozeti, lookup); ülke biçimi kapısı (`IIbanCountryFormatSource`) mekanizması v1'de, gerçek veri MOD-0269 gerçek dağıtımından önce zorunlu kapı | AC-40 |
| İyileştirme 1 | Tüketici yalnız `referenceable`'a dayanabilirdi | `referenceable`'ın anlamı daraltıldı; E1…E9 uygunluk alanları zorunlu; negatif uyum fikstürleri + üretici testleri + başvuru sınıflandırıcısı; tüketici testleri tüketici paketlerine (Follow-up) | AC-19, AC-20, AC-42 |
| İyileştirme 2 | Şifreleme somut değildi | AES-256-GCM, CSPRNG'den 96 bit nonce (yeniden kullanılmaz), AAD = TenantId + hesap kimliği + anahtar sürümü, sürüm kayıtta; kurcalama / yanlış anahtar → 500, eksik anahtar → açılmaz / 503, karışık sürüm okunur, yeniden şifreleme işi. **O-6 hâlâ öneridir** — sahip kararı bulunmadı | AC-13, AC-22, AC-37, AC-38, AC-39 |

### Gap 14 kapanışı (sahip gap listesi 14, CONDITIONAL PASS → revizyon 3)

| Madde | Bulgu | Kapanış (bu pakette) | AC |
|---|---|---|---|
| 1 (P1) | Eski yapılandırmalı örnek, "yeni özeti olmayan kayıt 0" sayımından sonra yine yalnız eski özeti yazabilir | Kiracı başına `BankDataKeyFence` (Mongo'da tek etkin `KeySetId`); her hesap yazımı aynı işlemde çiti `ActiveKeySetId` süzgeciyle artırır, eski küme 503; döndürme adımları çit ilerletmeleriyle sıralanır, emeklilik sayımı yalnız çit yeni kümedeyken kesin sayılır. Aynı açık şifreleme anahtarı emekliliğinde de vardı — aynı çitle kapandı (Runtime Constraints → "Özet anahtarı döndürme", "Şifreleme") | AC-43, AC-44 |
| 2 (P1) | "Yurt içi hesap varsa banka kodu kilitli" sorgusu hesap oluşturmayla yarışabilir | Bank / BankBranch `DomesticKeyFenceVersion`; yurt içi hesap yazımı ve banka / şube anahtar alanı değişikliği aynı işlemde aynı alanı CAS'la artırır → biri yazma çakışmasıyla düşer, 409 (Runtime Constraints → "Tekillik anahtarı") | AC-34, AC-45 |
| 3 (P2) | Döndürme işi kaydı yalnız şifreleme sürümüne bakarak atlıyordu | Atlama yalnız üç koşul birlikteyken (güncel şifreleme + her etkin özet + emekli özet yok); aksi hâlde yalnız eksik adımlar, idempotent | AC-46 |
| 4 (P2) | Gerçek dağıtım kapısı yalnız IBAN içindi | Yurt içi için ayrı kapı: banka dizininde ülke + ulusal banka kodu (+ şube) ve ülke hesap numarası kuralı (`IBankDirectorySource`, `IDomesticAccountRuleSource`), düzey `DomesticVerified`; IBAN `CountryFormat` ve yurt içi `DomesticVerified` kapılarının **ikisi de** MOD-0269 gerçek dağıtımından önce zorunlu | AC-47 |
| — | O-6 | (Revizyon 6: sahip onayı D24/3 verildi.) Güvenlik onayı (`ready-for-dev` engeli) ile gerçek Mongo testleri (DL-2 dilim kabul kapısı — gap 16/2) **hâlâ açık** | — |

### Gap 15 kapanışı (sahip gap listesi 15, CONDITIONAL PASS → revizyon 4)

| Madde | Bulgu | Kapanış (bu pakette) | AC |
|---|---|---|---|
| 1 (P1) | `KeySetId` güncel anahtar seçimini içermiyordu; aynı listede farklı "güncel" seçen örnek aynı çit kimliğini üretirdi | Kanonik serileştirme tanımlandı (`mdm-bank-keyset/2`, `enc.current=`, `hash.current=`, sıralı anahtar satırları, `ks2-` + SHA-256); sürüm adı deseni ve başlangıç doğrulaması (Entity Fields → BankDataKeyFence) | AC-48 |
| 2 (P1) | Yeni kiracı çitini ilk yazan kendi kümesiyle kuruyordu | Servis düzeyi `BankDataKeySetTarget` (yalnız `ApproveBankDataKeySetTargetCommand` yazar, onaylı yapılandırmadan, `ApprovalRef` + yalnız-ekleme geçmiş); çit yalnız onaylı hedefle, yazma işlemi içinde hedef belgesi koşullu artırılarak kurulur; hedefle uyuşmayan yazar çit kuramaz ve yazamaz (503); hedef değişikliği kurulumla yazma çakışması üretir (Runtime Constraints → "Yazar çiti") | AC-49 |
| — | Çit yük testi ve gerçek Mongo testleri açıkça kapı olmalı | Adlandırılmış yük testi AC-50; gap 16/2 ile netleşti: eşiğin yazılması `ready-for-dev` maddesi, AC-50 ve gerçek Mongo AC'lerinin geçmesi DL-2 dilim kabul kapısı | AC-50 |
| — | O-6 | (Revizyon 6: sahip onayı D24/3 verildi.) **Güvenlik onayı hâlâ açık** | — |

### Gap 15b / Gap 16 kapanışı (sahip gap listesi 16, CONDITIONAL PASS → revizyon 5)

| Madde | Bulgu | Kapanış (bu pakette) | AC |
|---|---|---|---|
| 1 (P1) | Eski iş, yerel onaylı yapılandırmasından K2'den sonra K1'i yeniden onaylayabilirdi; CAS tek başına durdurmaz | Kesin artan `ApprovalRevision` + `ExpectedPreviousKeySetId` onaylı yapılandırmada birlikte taşınır; onay tek koşullu güncellemedir (revizyon büyük **ve** önceki küme beklenen **ve** örneğin kümesi hedef **ve** küme kayıtlardaki sürümleri içeriyor); eski / eşit revizyon 409 `MDM_BANK_KEYSET_TARGET_REVISION_STALE`, hedef değişmez; geri dönüş yalnız yeni revizyonla; `History` revizyonu yazar; çit `AppliedTargetRevision` ile yalnız ileri gider (Entity Fields → BankDataKeySetTarget, BankDataKeyFence) | AC-51, AC-52 |
| 2 (P2) | Yeni kodun gerçek Mongo testleri `ready-for-dev`'den önce istenirken kod `ready-for-dev`'den önce yasaktı (kilitlenme) | Kapılar ayrıldı: tasarım / onay kapısı = `ready-for-dev` (kararlar, onaylar, sözleşmeler, yazılmış eşikler, var olan kodun okunarak ölçümü); dilim kabul kapısı = her dilimin AC'leri (AC-50 ve gerçek replica set AC'leri dahil), build ve tüketici kanıtı. "Geçti" isteyen her madde Ready-for-dev listesinden dilim kabul kapısına taşındı; S-1 / S-2 isteğe bağlı, atılacak, sahip onaylı ön doğrulama olarak tanımlandı (Ready-for-dev Checklist, Teslim dilimleri → Dilim kabul kapısı) | — |
| — | Görünür engeller | (Revizyon 6 ile güncellendi:) kiracısız hedef kaydının denetimi KARAR D24/9 ile kapandı (Follow-up kaldı); O-6'da sahip onayı D24/3 var, **güvenlik onayı** Ready-for-dev listesinde görünür engel | — |

### D24 uygulaması (sahip kararları D24/1–9, 2026-10-25 → revizyon 6)

| Karar | Seçim | Bu pakette uygulandığı yer | AC |
|---|---|---|---|
| D24/1 (O-4) | doğrulama kodu + dört göz; MOD-0031 MOD-0269 öncesi kapı | Entity Fields (`ActivationVerificationCode`), Validation Rules, Frontend (Activate diyaloğu), Dependencies, Follow-up | AC-18, AC-24 |
| D24/2 (O-5) | güvenilir niyet yolu, K2, `MasterDataAuditOperation` | Repo Scope, Dependencies, Audited Events (K2 sütunu "evet (K2, D24/2)"); defter belirteci ayrı CT kararı olarak Ready-for-dev'de kaldı | AC-6, AC-7, AC-27 |
| D24/3 (O-6) | uygulama düzeyi AES-GCM — **yalnız sahip onayı** | Entity Fields, Runtime Constraints → "Şifreleme", Dependencies; **güvenlik onayı** Ready-for-dev'de görünür engel, gerçek Mongo testleri DL-2 dilim kabul kapısında | AC-13, AC-35…AC-39, AC-43…AC-46, AC-48…AC-52 |
| D24/4 (O-7) | `BNK` / `BAC`, OTOMATİK | Entity Fields (Bank.Code, BankAccount.Code); `.antigravity` §3 satırı ayrı onay | AC-1 |
| D24/5 (O-8) | yalnız ISO biçimi | Entity Fields, Validation Rules, Frontend, Follow-up | AC-2 |
| D24/6 (O-10) | çekirdek kodlar + kiracı ek amaçları (sahibin kendi seçimi) | Entity Fields → "Hesap amaçları" (`BankAccountPurpose`), indeksler, hata kodları, Owned Objects, Backend / Frontend File Convention, Authorization (`mdm.bank-account-purposes.*`), Audited Events, Validation Rules, Failure Path, Page Help; ekran DL-4, arka uç DL-2; kod sınıfı (sözlük anahtarı) CT teyidi Ready-for-dev'de | AC-53…AC-58 |
| D24/7 (O-11) | Banks ekranı Compact | Entity Fields notu | AC-28 |
| D24/8 (O-12) | küsurat MOD-0048 | Açık kararlar | — |
| D24/9 | kiracısız hedef denetimi: yerel `History` + kiracı çit niyeti (`Version`, `ApprovalRevision`, `ApprovalRef`) | Audited Events (Approve satırı), Ready-for-dev'den çıkarıldı, Follow-up'a merkezi kiracısız kayıt | AC-49, AC-51 |

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
13. Dış korunan referans modeli (`ProtectedAccountRef`) — Recovery'de kasa yok; yerine uygulama düzeyi AES-GCM (D24/3).
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
- Değişiklik talebi + MOD-0023 onay akışı (D23/2 ile v1 dışı; v2 ya da MOD-0269 gerçek dağıtımı öncesinde yeniden
  değerlendirilir).
- Tüketici uyum testleri (`BANK-ACCOUNT-LOOKUP` negatif fikstürleri, AC-42) — sahipleri: MOD-0126, MOD-0119, MOD-0120,
  MOD-0124 paketleri (finance-treasury); her biri E1…E9'u kendi kuralıyla uygular.
- Yurt içi doğrulama kaynakları (banka dizini, ülke hesap numarası kuralları) için sahip ve veri — MOD-0269 gerçek
  dağıtımından önce (gap 14/4).
- IBAN ülke biçimi kaynağı (ISO 13616 kayıt verisi; sahibi referans veri) ve var olan `FormatAndChecksum` kayıtları
  yeniden denetleyen iş; IBAN ↔ yurt içi numara eşleme (iki anahtar uzayını birleştirme) — MOD-0269 gerçek dağıtımından
  önce.
- Toplu içe aktarma (RCS-001 §5.11) ve ülkeye göre IBAN uzunluk / kanal kural seti (referans veri sahibiyle).
- Pasif sahibin hesaplarını pasifleştiren ya da işaretleyen iş (İ2 tetikleyici + kendi denetimli komutu).
- Merkezi ret kaydı (`Outcome = Denied`) — denetim sahibine.
- MOD-0031 kanıt bağlantısının tedarikçi hesabı aktivasyonunda zorunlu olması — MOD-0269 gerçek gönderiminden önce (D24/1).
- Varsayılan hesap ataması, istisna vakaları, kanıt paketi / vekâlet.
- MDM domain-config: MOD-0289 satırı, frontend/gateway kapsam cümlesi; MDM iş ülkesi kaynağı gelince ülke kodunun ona
  bağlanması (D24/5).
- Onaylı anahtar kümesi hedefinin (kiracısız servis kaydı) değişiklikleri için merkezi kiracısız denetim kaydı (D24/9) —
  bugün yerel `History` + kiracı çit niyetleri; merkezi yol denetim sahibiyle ayrı iş.
- `execution/registries/module-implementation-status.md` satırı ilk kodla açılır.

## Açık kararlar

| # | Karar | Seçenekler | Öneri | Sahibi |
|---|---|---|---|---|
| O-1 | Hesaplar v1'de onay iş akışı (MOD-0023) istesin mi? | (a) değişiklik talebi + MOD-0023 onayı (b) iş akışı yok, **dört göz aktivasyonu** (c) kontrol yok | **KARAR D23/2 (2026-10-25): (b)** — v1'de MOD-0023 akışı yok; hesabı oluşturan ya da son güncelleyen etkinleştiremez | sahip (K1) |
| O-2 | Şirket hesabı için şirket Active **ve** Approved mı olmalı? | (a) Create ve Activate'te Active + Approved (b) Create'te Active, Activate'te Active + Approved (c) yalnız Active | **KARAR D23/3 (2026-10-25): (b)** — oluşturmada `Active`; etkinleştirmede ve lookup'ta `Active` + `Approved` (D10/1 ile aynı çizgi) | sahip (K1) |
| O-3 | Maskesiz okuma izni kimde? | (a) `mdm.bank-accounts.read-unmasked`, Tier 3, yalnız açık atama, gerekçe kodu zorunlu (b) hesap oluşturma izniyle birlikte (c) maskesiz okuma yok | **KARAR D23/4 (2026-10-25): (a)** — yalnız açık atama (rol şablonunda varsayılan değil), her okumada gerekçe kodu, değer dönmeden önce denetim kaydı. Kalan (karar değil): gerekçe değer listesi önerisi `PAYMENT_INVESTIGATION`, `BANK_CONFIRMATION`, `AUDIT_REQUEST`, `DATA_CORRECTION`; ilk sahipler rol şablonu MOD-0018 sahibiyle | sahip (K1) + MOD-0018 / Auth sahibi (uygulama) |
| O-4 | Tedarikçi hesabı aktivasyonunda doğrulama kanıtı | (a) kanıt yok (b) zorunlu doğrulama kodu + dört göz (c) MOD-0031 kanıt bağlantısı zorunlu | **KARAR D24/1 (2026-10-25): (b)** — etkinleştirmede zorunlu kod (`CALLBACK_CONFIRMED` / `BANK_LETTER_ON_FILE` / `SUPPLIER_PORTAL_CONFIRMED`) ve farklı kişi; MOD-0031 kanıt bağlantısı MOD-0269 gerçek gönderiminden önce zorunlu kapı (Follow-up) | sahip (K1) |
| O-5 | Denetim yolu ve K2 | (a) `mdm-guvenilir-kaynak-niyeti` genişletmesi (b) `mdm-merkezi-iletim` (c) ortak CentralAudit | **KARAR D24/2 (2026-10-25): (a)** — kapalı-başarısız güvenilir niyet yolu, eklemeli genel `MasterDataAuditOperation`; hesap yazmaları ve maskesiz okuma K2. **Kalan (karar değil): defter belirteci ayrı CT kararı.** `AuditCategory` değeri MOD-0126 O-6 ile ortak | sahip (K1); defter: CT |
| O-6 | Tam tanımlayıcının saklanması | (a) uygulama düzeyi AES-GCM (b) MongoDB istemci tarafı şifreleme (c) dış kasa referansı (d) saklamama | **KARAR D24/3 (2026-10-25): (a) — yalnız sahip onayı.** Pack'teki tasarım (AES-256-GCM, rastgele 96 bit nonce, AAD = kiracı + hesap + sürüm, sürümlü anahtar, ayrı sürümlü HMAC özet, anahtar kümesi çiti, onaylı hedef + revizyon). **Güvenlik onayı ve gerçek Mongo testleri hâlâ açık**; bu karar onları kapatmaz (Ready-for-dev: güvenlik onayı; DL-2 dilim kabul kapısı: testler) | sahip (K1) — **güvenlik onayı açık** |
| O-7 | RCS-001 kayıt kodları | `BNK` / `BAC` OTOMATİK · başka | **KARAR D24/4 (2026-10-25):** banka `BNK` (grup öneki), hesap `BAC` (şirket hesabında şirket öneki, tedarikçide grup öneki), OTOMATİK; şube kodu doğal anahtar. `.antigravity` §3 satırı ayrı onay | sahip (K1); `.antigravity`: ayrı onay |
| O-8 | Ülke kodu kaynağı | (a) yalnız ISO biçimi (b) PSS-011 (c) MDM iş ülkesi referansı | **KARAR D24/5 (2026-10-25): (a)** — ISO 3166-1 alfa-2 biçimi; MDM iş ülkesi kaynağı gelince bağlanır | sahip (K1) |
| O-9 | Tedarikçi hesabı kapsamı | (a) kiracı geneli (b) **ilişkili şirketler** (c) kapsam yok | **KARAR D23/1 (2026-10-25): (b)** — liste, detay ve lookup yalnız tedarikçiyle MOD-0140'ta `Active` ilişkisi olan şirketin kullanıcısına; ilişkisiz 404; maskesiz okuma ayrıca izin + ilişki; `read-tenant-wide` tüm tedarikçi hesaplarını maskeli gösterir. Şirket hesapları `LegalEntityId` kapsamıyla; bankalar kiracı geneli. Kalan (karar değil): sağlayıcı ölçümü (DL-2) ve ilişkili tedarikçi listesinin 1.3.0 işlemi (DL-3) | sahip (K1) |
| O-10 | `AccountPurpose` değer listesi | (a) serbest metin (b) kapalı kod listesi (c) **kod listesi + kiracı ekler** | **KARAR D24/6 (2026-10-25): (c) — sahibin kendi seçimi, önceki öneri (b) değil.** Sabit çekirdek kodlar + kiracı referans listesi (`BankAccountPurpose`, 7 dilde etiket) — tasarım Entity Fields → "Hesap amaçları" | sahip (K1) |
| O-11 | Banks ekranının şekli | Compact / Slim | **KARAR D24/7 (2026-10-25): Compact** — Details'ta şube alt tablosu | sahip (K1) |
| O-12 | Para birimi küsurat hanesi kaynağı | MOD-0289 / MOD-0048 | **KARAR D24/8 (2026-10-25): MOD-0048** — MOD-0289 yalnız ISO kodu saklar ve MOD-0048 listesinde olduğunu doğrular | sahip (K1) |
| O-13 | Hesap amaçları ekranının şekli (form 8 alan: Code + 7 etiket — tek başına Slim'e düşer) | Compact (pack'in tek `golden_reference` değeri) / Slim (sapma kaydıyla) | **Compact** — pack tek `golden_reference` taşır (module-pack-standard `screens` yalnız `data_mode` taşır); Details'ta kullanan hesap sayısı; sapma kaydı gerekmez | sahip |

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
  CT-APR-02/01, kod alma izni (D1), DCP, RCS-001 servisi, CT defter kararı (D24/2, D24/4, D24/7 karar verildi). Bitiş: AC-1, 2, 3 (banka ve
  şube kısmı), 4 (banka kısmı), 5 (banka ve şube kısmı), 6 (banka kısmı), 7, 8 (banka kısmı), 9 (banka kısmı).
- **DL-2** — Yollar: `Domain/Entities/{BankAccount,BankAccountSensitiveRead}.cs`, `Features/BankAccount/**` (şirket sahibi),
  `Rules/`, `Infrastructure/{BankData,Currency}/**`, `BankAccountsController`, `BANK-ACCOUNT-LOOKUP` sözleşme dosyası.
  **Neden ayrı:** hassas veri, şifreleme ve veri kapsamı modülün en riskli kısmıdır; bağımsız inceleme tek başına
  yapılabilsin. Önkoşul: DL-1, O-6 güvenlik onayı (sahip onayı D24/3), şifreleme ve özet anahtarları, Auth explicit-grant
  eki, kapsam sağlayıcısı ölçümü, MOD-0048 tüketim yolu, `ValidationBehavior` ölçümü, AC-50 eşiği yazılmış (çok
  anahtarlı tekil indeks ve `AesGcm` soruları isteğe bağlı S-1 / S-2'de ya da bu dilimin kabul kapısında cevaplanır).
  Bitiş: AC-3 (hesap kısmı), 4 (hesap kısmı), 5 (hesap kısmı), 6 (hesap kısmı), 8 (hesap kısmı), 9 (hesap kısmı), 10, 11,
  12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 26 (şirket hesabı kısmı), 33, 34, 35, 36, 37, 38, 39, 40, 42, 43, 44, 45, 46,
  47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57.
- **DL-3** — Yollar: `Infrastructure/Suppliers/**` (`ISupplierEligibilityPort` bağdaştırıcısı), tedarikçi sahibi dalları
  (Create, Update, Activate, Deactivate, Delete, liste, detay, maskesiz okuma, `lookup-validation` — D23/1 ilişki kuralı),
  mimari test (koleksiyon okuması yok), dev ortamı denetim kanıtı. **Neden ayrı:** MOD-0140 1.3.0'a bağlı; DL-2'yi
  bekletmesin. Önkoşul: DL-2, MOD-0140 1.3.0 main'de (ilişkili tedarikçi listesi işlemi dahil), MdmService → MOD-0140 çağrı
  kimliği ve Gateway rotasının ölçümü (D24/1 karar verildi). Bitiş: AC-23, 24, 25, 26 (tedarikçi kısmı), 27, 41.
- **DL-4** — Yollar: `Views/MasterData/{Banks,BankAccounts}/**`, JS, resx (7 dil), MVC vekilleri, sayfa yardımı, nav
  anahtarları; Gateway rotaları integration-agent task'ı olarak açılır (bu dilim yazmaz). **Neden ayrı:** ekran API
  sözleşmesine bağlıdır, arka uç değişmeden ilerler. Önkoşul: DL-2 (hesap ekranı), DL-3 (tedarikçi sahibi seçimi), Gateway
  rotaları, MDM domain-config frontend/gateway kapsam cümlesi, O-13. Bitiş: AC-28, 29, 30, 31, 32, 58.

**Dilim kabul kapısı (gap 16/2) — her dilim için, kabul / birleştirmeden önce (`ready-for-dev`'den önce değil):**

| Dilim | Geçmesi gerekenler (çalıştırmak geçmek değildir) |
|---|---|
| DL-1 | "Bitiş" listesindeki AC'ler; AC-2 ve AC-9'un gerçek replica set yarış testleri; `AuditTrailStandard` (AC-7); `Diten.MdmService` build 0 hata; yerel kapıda `scripts/` Python testleri |
| DL-2 | "Bitiş" listesindeki AC'ler; gerçek replica set AC'leri (AC-13, AC-35…AC-39, AC-43…AC-46, AC-48…AC-52) ve **AC-50 yük testi** yazılmış eşiğe göre; tüketici kanıtı: `BANK-ACCOUNT-LOOKUP` sözleşme testi (AC-20, AC-42) ve en az bir Treasury tüketicisinin fikstür koşusunun kendi sahibince kaydı (Follow-up tablosu); build |
| DL-3 | "Bitiş" listesindeki AC'ler; MOD-0140 1.3.0'a karşı sözleşme testi; AC-27 dev ortamı denetim kanıtı; build |
| DL-4 | "Bitiş" listesindeki AC'ler; tarayıcı smoke ve UAS-001 canlı denemesi; DataTable doğrulayıcı; RESX 7 dil parite; `Diten.Web` build |

AC durumu kabul kaydında (`scripts/acceptance_record.py`) izlenir; bu pakette her AC yalnız "tanımlı"dır.
